using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GardenVR.Audio
{
    /// <summary>
    /// Plays cue ids from a <see cref="CueManifest"/>. Eight voices. The lowest priority band yields first.
    /// Variants rotate, and the same clip does not play twice within 3 seconds. A cue inside its cooldown
    /// is dropped. While a voice line plays, music ducks 6 dB and ambient ducks 3 dB. Mute persists and
    /// outranks every bus. A missing clip plays nothing and logs <c>PLACEHOLDER cue id</c> once.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public sealed class AudioCueService : MonoBehaviour
    {
        public const int MaxVoices = 8;
        public const float IdenticalWindow = 3f;
        public const float DuckAttack = 0.3f;
        public const float DuckRelease = 0.8f;
        public const float MusicDuckDb = -6f;
        public const float AmbientDuckDb = -3f;
        public const string MuteKey = "gardenvr.audio.mute";

        /// <summary>Tests set this before <c>AddComponent</c> so the real manifest is not loaded.</summary>
        public static bool SuppressAutoLoad;

        /// <summary>Tests set this before <c>AddComponent</c> to point the cue log at a temp file.</summary>
        public static string LogPathOverride;

        sealed class Active
        {
            public string CueId;
            public string Clip;
            public string Bus;
            public int Band;
            public bool Loop;
            public float EndAt;
            public float BaseGainDb;
            public AudioSource Source;
        }

        sealed class Memory
        {
            public int Next;
            public int Chosen = -1;
            public float LastAccept = -1000f;
            public readonly Dictionary<string, float> LastClipAt = new Dictionary<string, float>(StringComparer.Ordinal);
        }

        readonly List<Active> _voices = new List<Active>(MaxVoices);
        readonly List<AudioSource> _pool = new List<AudioSource>(MaxVoices);
        readonly Dictionary<string, Memory> _memory = new Dictionary<string, Memory>(StringComparer.Ordinal);
        readonly HashSet<string> _announced = new HashSet<string>(StringComparer.Ordinal);

        // -1 means "read PlayerPrefs". A static so a new service in the same session sees a mute
        // that was set before its Awake, including EditMode tests where Awake can be skipped.
        static int _muteCache = -1;

        CueManifest _manifest;
        CueLog _log;
        float _now;
        float _duck;
        bool _synthetic;
        float _syntheticSeconds = 1f;

        /// <summary>Off means voice-bus cues are not started. The app copies this from the save.</summary>
        public bool VoiceGuide = true;

        /// <summary>Off means music-bus cues are not started. The night bed ships off.</summary>
        public bool Beds = true;

        public CueManifest Manifest { get { return _manifest; } }
        public float Now { get { return _now; } }
        public float DuckLevel { get { return _duck; } }
        public int ActiveVoices { get { return _voices.Count; } }
        public string LastClip { get; private set; }
        public int PlaceholderAnnouncements { get { return _announced.Count; } }

        public bool Mute
        {
            get
            {
                if (_muteCache < 0) _muteCache = PlayerPrefs.GetInt(MuteKey, 0);
                return _muteCache == 1;
            }
            set
            {
                _muteCache = value ? 1 : 0;
                PlayerPrefs.SetInt(MuteKey, _muteCache);
                PlayerPrefs.Save();
                if (value) Silence();
            }
        }

        /// <summary>Tests clear the saved mute so a run cannot leak into the next one.</summary>
        public static void TestingResetMute()
        {
            _muteCache = 0;
            PlayerPrefs.DeleteKey(MuteKey);
            PlayerPrefs.Save();
        }

        /// <summary>Puts the saved mute back and drops the cache so the next read uses PlayerPrefs.</summary>
        public static void TestingRestoreMute(int stored)
        {
            _muteCache = -1;
            if (stored == 0) PlayerPrefs.DeleteKey(MuteKey);
            else PlayerPrefs.SetInt(MuteKey, stored);
            PlayerPrefs.Save();
        }

        void Awake()
        {
            if (!SuppressAutoLoad || !string.IsNullOrEmpty(LogPathOverride))
                _log = CueLog.Open(LogPathOverride);
            if (SuppressAutoLoad) return;
            string path = Path.Combine(Application.dataPath, "Audio", "cues.json");
            if (!File.Exists(path)) return;
            LoadManifest(File.ReadAllText(path));
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            Advance(Time.deltaTime);
        }

        void OnDestroy()
        {
            Silence();
        }

        public void LoadManifest(string json)
        {
            _manifest = CueManifest.Parse(json);
        }

        /// <summary>Policy tests play without audio files. Each clip lasts <paramref name="seconds"/>.</summary>
        public void UseSyntheticClips(float seconds)
        {
            _synthetic = true;
            _syntheticSeconds = seconds > 0.05f ? seconds : 0.05f;
        }

        public void Advance(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            _now += seconds;
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                Active voice = _voices[i];
                if (!voice.Loop && _now >= voice.EndAt) Release(voice);
            }
            bool voiceOn = VoicePlaying();
            float target = voiceOn ? 1f : 0f;
            float window = voiceOn ? DuckAttack : DuckRelease;
            float step = window <= 0f ? 1f : seconds / window;
            _duck = Mathf.MoveTowards(_duck, target, step);
            ApplyVolumes();
        }

        public float DuckDb(string bus)
        {
            if (bus == "music") return MusicDuckDb * _duck;
            if (bus == "ambient") return AmbientDuckDb * _duck;
            return 0f;
        }

        public bool IsPlaying(string id)
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].CueId == id) return true;
            }
            return false;
        }

        public bool Announced(string id)
        {
            return id != null && _announced.Contains(id);
        }

        public bool Play(string id, Transform at = null, float pitchSemitones = 0f)
        {
            if (string.IsNullOrEmpty(id) || Mute) return false;

            CueDefinition cue;
            if (_manifest == null || !_manifest.TryGet(id, out cue))
            {
                Announce(id);
                return false;
            }
            if (cue.bus == "voice" && !VoiceGuide) return false;
            if (cue.bus == "music" && !Beds) return false;
            if (cue.MissingClip)
            {
                Announce(id);
                return false;
            }

            Memory memory = MemoryFor(id);
            if (cue.cooldownS > 0f && _now - memory.LastAccept < cue.cooldownS) return false;

            string clip = ChooseClip(cue, memory);
            if (clip == null)
            {
                if (!_synthetic && !HasFile(cue)) Announce(id);
                return false;
            }

            if (cue.bus == "voice") StopBus("voice");
            int limit = cue.maxConcurrent <= 0 ? 1 : cue.maxConcurrent;
            while (CountCue(id) >= limit)
                Release(Oldest(id));

            if (_voices.Count >= MaxVoices)
            {
                Active worst = LowestBand();
                if (worst == null || worst.Band <= cue.priorityBand) return false;
                Release(worst);
            }

            memory.Next = (memory.Chosen + 1) % cue.clips.Length;
            memory.LastAccept = _now;
            memory.LastClipAt[clip] = _now;
            LastClip = clip;

            float pitch = Mathf.Pow(2f, pitchSemitones / 12f) * (1f + Jitter(cue.pitchJitter));
            if (pitch < 0.01f) pitch = 0.01f;
            float duration = ClipDuration(clip) / pitch;
            var voice = new Active
            {
                CueId = id,
                Clip = clip,
                Bus = cue.bus,
                Band = cue.priorityBand,
                Loop = cue.loop,
                EndAt = cue.loop ? float.PositiveInfinity : _now + duration,
                BaseGainDb = cue.gainDb
            };
            voice.Source = StartSource(voice, cue, at, pitch);
            _voices.Add(voice);

            if (_log != null)
            {
                Vector3? pos = at != null ? at.position : (Vector3?)null;
                _log.Append(_now, id, clip, cue.bus, cue.gainDb + DuckDb(cue.bus), pos);
            }
            return true;
        }

        public void Stop(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                if (_voices[i].CueId == id) Release(_voices[i]);
            }
        }

        void Announce(string id)
        {
            if (!_announced.Add(id)) return;
            Debug.Log("PLACEHOLDER cue " + id);
        }

        Memory MemoryFor(string id)
        {
            Memory memory;
            if (!_memory.TryGetValue(id, out memory))
            {
                memory = new Memory();
                _memory[id] = memory;
            }
            return memory;
        }

        string ChooseClip(CueDefinition cue, Memory memory)
        {
            int count = cue.clips.Length;
            for (int i = 0; i < count; i++)
            {
                int index = (memory.Next + i) % count;
                string clip = cue.clips[index];
                if (string.IsNullOrEmpty(clip)) continue;
                float last;
                if (memory.LastClipAt.TryGetValue(clip, out last) && _now - last < IdenticalWindow) continue;
                if (!_synthetic && !File.Exists(ClipPath(clip))) continue;
                memory.Chosen = index;
                return clip;
            }
            return null;
        }

        bool HasFile(CueDefinition cue)
        {
            if (cue.clips == null) return false;
            for (int i = 0; i < cue.clips.Length; i++)
            {
                if (!string.IsNullOrEmpty(cue.clips[i]) && File.Exists(ClipPath(cue.clips[i]))) return true;
            }
            return false;
        }

        static string ClipPath(string relative)
        {
            return Path.Combine(Application.dataPath, "Audio", relative.Replace('/', Path.DirectorySeparatorChar));
        }

        float ClipDuration(string clip)
        {
            if (_synthetic) return _syntheticSeconds;
            AudioClip asset = LoadAsset(clip);
            if (asset != null && asset.length > 0.01f) return asset.length;
            return 1f;
        }

        static float Jitter(float amount)
        {
            if (amount <= 0f) return 0f;
            return UnityEngine.Random.Range(-amount, amount);
        }

        bool VoicePlaying()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Bus == "voice") return true;
            }
            return false;
        }

        int CountCue(string id)
        {
            int count = 0;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].CueId == id) count++;
            }
            return count;
        }

        Active Oldest(string id)
        {
            Active found = null;
            for (int i = 0; i < _voices.Count; i++)
            {
                Active voice = _voices[i];
                if (voice.CueId != id) continue;
                if (found == null || voice.EndAt < found.EndAt) found = voice;
            }
            return found;
        }

        Active LowestBand()
        {
            Active found = null;
            for (int i = 0; i < _voices.Count; i++)
            {
                Active voice = _voices[i];
                if (found == null || voice.Band > found.Band) found = voice;
            }
            return found;
        }

        void StopBus(string bus)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                if (_voices[i].Bus == bus) Release(_voices[i]);
            }
        }

        void Silence()
        {
            for (int i = _voices.Count - 1; i >= 0; i--) Release(_voices[i]);
            _duck = 0f;
        }

        void Release(Active voice)
        {
            if (voice == null) return;
            if (voice.Source != null)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.gameObject.SetActive(false);
            }
            _voices.Remove(voice);
        }

        AudioSource StartSource(Active voice, CueDefinition cue, Transform at, float pitch)
        {
            if (_synthetic) return null;
            AudioClip clip = LoadAsset(voice.Clip);
            if (clip == null) return null;
            AudioSource source = TakeSource();
            source.gameObject.SetActive(true);
            source.clip = clip;
            source.loop = cue.loop;
            source.pitch = pitch;
            source.spatialBlend = cue.Is3D ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.2f;
            source.maxDistance = 3f;
            source.dopplerLevel = 0f;
            if (at != null) source.transform.position = at.position;
            else source.transform.localPosition = Vector3.zero;
            source.volume = Linear(voice.BaseGainDb + DuckDb(voice.Bus));
            source.Play();
            return source;
        }

        AudioSource TakeSource()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                AudioSource source = _pool[i];
                if (source != null && !source.gameObject.activeSelf) return source;
            }
            var go = new GameObject("CueVoice");
            go.transform.SetParent(transform, false);
            var created = go.AddComponent<AudioSource>();
            created.playOnAwake = false;
            _pool.Add(created);
            return created;
        }

        void ApplyVolumes()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                Active voice = _voices[i];
                if (voice.Source == null) continue;
                voice.Source.volume = Mute ? 0f : Linear(voice.BaseGainDb + DuckDb(voice.Bus));
            }
        }

        static float Linear(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }

        static AudioClip LoadAsset(string relative)
        {
            string assetPath = "Assets/Audio/" + relative.Replace('\\', '/');
            Type database = Type.GetType("UnityEditor.AssetDatabase, UnityEditor.CoreModule");
            if (database == null) database = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (database == null) return null;
            System.Reflection.MethodInfo found = null;
            System.Reflection.MethodInfo[] methods = database.GetMethods();
            for (int i = 0; i < methods.Length; i++)
            {
                System.Reflection.MethodInfo method = methods[i];
                if (method.Name != "LoadAssetAtPath" || !method.IsGenericMethodDefinition) continue;
                if (method.GetParameters().Length != 1) continue;
                found = method.MakeGenericMethod(typeof(AudioClip));
                break;
            }
            if (found == null) return null;
            return found.Invoke(null, new object[] { assetPath }) as AudioClip;
        }
    }
}

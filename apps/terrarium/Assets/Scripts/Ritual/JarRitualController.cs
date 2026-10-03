using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GardenVR.Audio;
using GardenVR.Core;
using GardenVR.Input;
using GardenVR.Room;
using UnityEngine;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// Pinch-hold breathing, end to end. Each frame builds a <see cref="PinchSample"/> from the hand source,
    /// steps <see cref="BreathSession"/>, and pushes uncoil, fog, ring progress, and phase to <see cref="JarView"/>.
    /// The garden answers once, with the style-bible timings. App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class JarRitualController : MonoBehaviour
    {
        public const float PaceInhaleSeconds = 4f;
        public const float PaceExhaleSeconds = 6f;
        public const float ContinueAfterSeconds = 60f;
        public const float PausedMotion = 0.2f;
        public const string ContinuePromptId = "prompt.continue";
        public const string RestorePromptId = "prompt.restore";
        const float EngageStrength = 0.85f;

        [SerializeField] JarView _view;

        BreathConfig _config;
        BreathSession _session;
        GardenService _service;
        Garden _garden;
        IHandIntentSource _source;
        KeyboardMouseIntentSource _keyboard;
        GrowthAnswer _lastAnswer;
        bool _hasAnswer;
        int _loggedEvents;
        bool _answered;
        bool _latchedPause;
        bool _needFreshPinch;
        bool _sawOpen;
        bool _awaitContinue;
        bool _autoPace;
        bool _overlay;
        bool _holdVisual;
        bool _wasHolding;
        float _pausedFor;
        float _pace;
        float _autoClock;
        float _ambience;
        float _answerTime;
        float _frozenBreath;
        float _appTime;
        int _updates;

        Renderer[] _dots;
        Material[] _dotMats;
        Material _paceMat;
        GameObject _prompt;
        GameObject _restore;
        GUIStyle _overlayStyle;
        HabitDesk _desk;
        AudioCueService _audio;
        readonly Dictionary<string, int> _cues = new Dictionary<string, int>();
        bool _cuedLand;
        bool _cuedLid;
        bool _cuedMoss;
        bool _cuedDew;

        static readonly Color DotLit = new Color(0.45f, 1.15f, 0.72f);
        static readonly Color DotDim = new Color(0.015f, 0.04f, 0.028f);
        static readonly Color PaceMint = new Color(0.55f, 1.05f, 0.78f);
        static readonly Color Etch = new Color(0.749f, 0.961f, 0.867f);

        public BreathSession Session => _session;
        public Garden Garden => _garden;
        public GardenService Service => _service;
        public GrowthAnswer LastAnswer => _lastAnswer;
        public bool HasAnswer => _hasAnswer;
        public bool FirstRunStarted { get; private set; }
        public bool RestorePromptVisible => _restore != null && _restore.activeSelf;
        public JarView View => _view;
        public bool AutoPace => _autoPace;
        public bool OverlayVisible => _overlay;
        public bool AwaitingContinue => _awaitContinue;
        public bool HoldingBreath => _holdVisual;
        public float PausedFor => _pausedFor;
        public float AppTime => _appTime;
        public float AnswerTime => _answerTime;
        public int Updates => _updates;
        public int FilledDots { get; private set; }
        public int LastPluckSemitones { get; private set; }
        public Ledger Ledger { get { return _service != null ? _service.HabitLedger : null; } }

        public int ShownLeaves(string habitId)
        {
            return _service != null ? _service.ShownLeaves(habitId) : 0;
        }

        public int CueCount(string id)
        {
            int count;
            return id != null && _cues.TryGetValue(id, out count) ? count : 0;
        }

        public bool YesterdayVisible(string preset)
        {
            return _desk != null && _desk.YesterdayVisible(preset);
        }

        public bool UndoMarkVisible(string preset)
        {
            return _desk != null && _desk.UndoVisible(preset);
        }

        /// <summary>Hook for the first-run ritual. A dev command calls this same method.</summary>
        public void OfferSeedPackets()
        {
            if (_service == null) return;
            bool first = _service.OfferSeedPackets();
            if (first)
            {
                EnsureDesk();
                Play("seed.appear", _desk != null ? _desk.transform : null, 0f);
            }
            RefreshHabits();
        }

        public void HandleDev(DevCommand command)
        {
            OnDev(command);
        }

        public bool TryUndoHabit()
        {
            if (_service == null) return false;
            string pending = _service.PendingHabitId;
            if (!_service.TryUndoHabit()) return false;
            Play("habit.undo", LabelAnchor(pending), 0f);
            RefreshHabits();
            PushGarden();
            return true;
        }

        public static string LogPath => Path.Combine(Application.persistentDataPath, "logs", "ritual.jsonl");

        public TerrariumState Snapshot()
        {
            int today = _service != null ? _service.TodayIndex : 0;
            return TerrariumState.Capture(_session, _garden, today);
        }

        /// <summary>A new evening in the same scene. The garden stays. The breath session starts over.</summary>
        public void BeginEvening()
        {
            if (_service == null || _service.RestoreOffered) return;
            _config = ConfigFrom(_service.Settings);
            _session = new BreathSession(_config);
            _answered = false;
            _hasAnswer = false;
            _answerTime = 0f;
            _loggedEvents = 0;
            _lastAnswer = new GrowthAnswer(false, false, false, 0);
            if (_view != null)
            {
                _view.answer = 0f;
                _view.answerTime = -1f;
                _view.recoveredTime = -1f;
                _view.uncoil = 0f;
            }
            _frozenBreath = 0f;
            _pace = -1f;
            _cuedMoss = false;
            _cuedDew = false;
            PushGarden();
        }

        /// <summary>Development builds only. A refused step changes nothing and shows nothing.</summary>
        public bool ShiftDay(int delta)
        {
            if (!DevClockAllowed() || _service == null) return false;
            // A missed day is quiet. The gap itself never plays a cue.
            bool moved = _service.TryShiftDay(delta);
            if (moved)
            {
                RefreshHabits();
                PushGarden();
            }
            return moved;
        }

        public void SetSource(IHandIntentSource source)
        {
            Unsubscribe();
            _source = source;
            _keyboard = source as KeyboardMouseIntentSource;
            // A swapped provider starts clean. Playback tests replace the keyboard after the scene has started.
            _latchedPause = false;
            _needFreshPinch = false;
            _sawOpen = false;
            _awaitContinue = false;
            _pausedFor = 0f;
            if (_prompt != null) _prompt.SetActive(false);
            if (isActiveAndEnabled) Subscribe();
        }

        public void SetAutoPace(bool on)
        {
            if (_autoPace == on) return;
            _autoPace = on;
            if (on) _autoClock = 0f;
            Log(_autoPace ? "AutoPaceOn" : "AutoPaceOff");
        }

        public void SetOverlay(bool on)
        {
            _overlay = on;
        }

        /// <summary>Append one JSONL line. The name must be letters, digits, or underscore.</summary>
        public void Mark(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return;
            }
            Log(name);
        }

        void Awake()
        {
            IClock clock = GardenService.ClockOverride ?? new SystemClock();
            string directory = string.IsNullOrEmpty(GardenService.DirectoryOverride)
                ? GardenService.DefaultDirectory()
                : GardenService.DirectoryOverride;
            _service = new GardenService(clock, directory);
            _garden = _service.Garden;
            _config = ConfigFrom(_service.Settings);
            _session = new BreathSession(_config);
            _autoPace = _service.Settings.AutoPace;
            if (_view == null) _view = GetComponent<JarView>();
            if (_view != null) _view.reducedMotion = _service.Settings.ReducedMotion;
            EnsureAudio();
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_source == null) SetSource(FindDefaultSource());
            ApplyHoldMode();
            BuildChrome();
            FirstRunStarted = _service != null && _service.Outcome == LoadOutcome.Fresh;
            if (_service != null && _service.RestoreOffered)
            {
                FirstRunStarted = false;
                if (_restore != null) _restore.SetActive(true);
            }
            EnsureDesk();
            PushIdle();
            RefreshHabits();
            Play("amb.room", null, 0f);
            Log("SessionStart");
        }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void OnApplicationQuit()
        {
            if (_service != null) _service.FlushHabits();
        }

        void OnDestroy()
        {
            if (_service != null) _service.FlushHabits();
            if (_dotMats != null)
            {
                for (int i = 0; i < _dotMats.Length; i++)
                {
                    if (_dotMats[i] != null) Destroy(_dotMats[i]);
                }
            }
            if (_paceMat != null) Destroy(_paceMat);
        }

        void Update()
        {
            if (!Application.isPlaying || _session == null || _view == null) return;
            if (_service != null && _service.RestoreOffered)
            {
                if (_restore != null && !_restore.activeSelf) _restore.SetActive(true);
                PushGarden();
                return;
            }
            _updates++;
            float dt = Time.deltaTime;
            if (dt < 0f) dt = 0f;
            _appTime += dt;
            TickFirstRunCues();
            if (_service != null) _service.StepHabits(dt);
            RefreshHabits();

            bool pinching = LivePinch();
            if (_needFreshPinch && !pinching)
                _sawOpen = true;
            if (_needFreshPinch && _sawOpen && !_awaitContinue && pinching)
            {
                _needFreshPinch = false;
                _latchedPause = false;
                _sawOpen = false;
                _pausedFor = 0f;
                Log("Resumed");
            }

            bool freeze = _latchedPause || _needFreshPinch;
            if (!freeze)
            {
                if (_autoPace && !_awaitContinue) _autoClock += dt;
                var sample = ReadSample();
                if (_awaitContinue) sample = new PinchSample(sample.Strength, false);
                _session.Update(dt, sample);
                TryComplete();
                FlushSessionEvents();
            }

            bool trackingHold = _session.Phase == BreathPhase.Paused;
            _holdVisual = freeze || _awaitContinue || trackingHold;
            if (_holdVisual && !_wasHolding)
                _frozenBreath = _view.breath;
            _wasHolding = _holdVisual;
            if (_holdVisual)
            {
                _pausedFor += dt;
                if (_pausedFor > ContinueAfterSeconds && !_awaitContinue)
                {
                    _awaitContinue = true;
                    Log("ContinuePrompt");
                }
            }
            else
            {
                _pausedFor = 0f;
            }

            if (_prompt != null && _prompt.activeSelf != _awaitContinue)
                _prompt.SetActive(_awaitContinue);

            float motion = _holdVisual ? PausedMotion : 1f;
            _ambience += dt * motion;
            if (_answered)
            {
                _answerTime += dt * motion;
                _view.answerTime = _answerTime;
                _view.answer = 1f;
                TickAnswerCues();
            }
            else
            {
                _view.answerTime = -1f;
                _view.answer = 0f;
            }

            _view.uncoil = _session.Uncoil;
            _view.fog = _session.Fog;
            _view.phase = _session.Phase;
            _view.time = _ambience;
            _view.sporeStep = dt * motion;
            if (_holdVisual)
            {
                _view.breath = _frozenBreath;
            }
            else
            {
                _view.breath = RingProgress();
                AdvancePace(dt);
            }
            PaintDots(_session.Breaths);
            PushGarden();
        }

        void OnGUI()
        {
            if (!_overlay || !LogEnabled()) return;
            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle(GUI.skin.label);
                _overlayStyle.fontSize = 16;
                _overlayStyle.normal.textColor = Etch;
            }
            GUI.Label(new Rect(16f, 16f, 1100f, 64f), Snapshot().ToJson(), _overlayStyle);
        }

        PinchSample ReadSample()
        {
            if (_autoPace && !_awaitContinue)
            {
                float period = PaceInhaleSeconds + PaceExhaleSeconds;
                float local = period <= 0f ? 0f : _autoClock % period;
                bool inhale = local < PaceInhaleSeconds;
                return new PinchSample(inhale ? 0.95f : 0.05f, true);
            }
            float strength = _source != null ? _source.PinchStrength : 0f;
            bool tracked = _source == null || _source.IsTracked;
            // The keyboard ramp reaches 0.8 inside 0.12 s, after the provider has already raised PinchHold.
            // Scripted playback raises PinchHold on the first frame, while strength is still near 0.
            // Lifting a reported pinch to the engage band makes the frond move inside two frames (gate S3).
            // The release still falls through PinchStrength, so the detector's 0.5 threshold still applies.
            if (tracked && _source != null && _source.IsPinching && strength < EngageStrength)
                strength = EngageStrength;
            return new PinchSample(strength, tracked);
        }

        float RingProgress()
        {
            float fraction = 0f;
            if (_session.Phase == BreathPhase.Inhaling)
                fraction = Mathf.Clamp01(_session.PhaseTime / Mathf.Max(0.01f, _config.IdealInhaleSeconds));
            else if (_session.Phase == BreathPhase.Exhaling && _session.PhaseTime < _config.MinExhaleSeconds)
                fraction = 1f;
            float target = Mathf.Max(1, _session.TargetBreaths);
            return Mathf.Clamp01((_session.Breaths + fraction) / target);
        }

        void AdvancePace(float dt)
        {
            if (_session.Phase == BreathPhase.Inhaling && _pace < 0f)
                _pace = 0f;
            if (_pace < 0f || _paceMat == null) return;
            _pace += dt;
            float shown;
            if (_pace <= PaceInhaleSeconds)
                shown = _pace / PaceInhaleSeconds;
            else if (_pace <= PaceInhaleSeconds + PaceExhaleSeconds)
                shown = 1f - (_pace - PaceInhaleSeconds) / PaceExhaleSeconds;
            else
                shown = 0f;
            _paceMat.SetFloat("_Fill", shown);
            _paceMat.SetColor("_Color", PaceMint);
        }

        void NoteInhaleStart()
        {
            _pace = 0f;
            if (_paceMat != null)
            {
                _paceMat.SetFloat("_Fill", 0f);
                _paceMat.SetColor("_Color", PaceMint);
            }
        }

        void TryComplete()
        {
            if (_answered || _session.Phase != BreathPhase.Complete) return;
            if (_service == null || _service.RestoreOffered) return;
            _answered = true;
            _lastAnswer = _service.CompleteRitual();
            _hasAnswer = true;
            _garden = _service.Garden;
            _answerTime = 0f;
            _cuedMoss = false;
            _cuedDew = false;
            Log("Answer");
            if (_lastAnswer.NewFrond)
                Play("answer.chime", FrondAnchor(), 0f);
        }

        void FlushSessionEvents()
        {
            while (_loggedEvents < _session.Events.Count)
            {
                BreathEvent ev = _session.Events[_loggedEvents++];
                if (ev.Kind == BreathEventKind.InhaleStarted) NoteInhaleStart();
                if (ev.Kind == BreathEventKind.ExhaleStarted) Play("fog.hiss", JarAnchor(), 0f);
                if (ev.Kind == BreathEventKind.BreathCounted) Play("breath.exhale.end", JarAnchor(), 0f);
                if (ev.Kind == BreathEventKind.Paused) Play("pause.hold", null, 0f);
                Log(ev.Kind.ToString());
            }
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.PalmOpen)
            {
                if (_service != null) _service.FlushHabits();
                RefreshHabits();
                PushGarden();
                LatchPause();
                return;
            }
            if ((intent.Kind == HandIntentKind.Pinch || intent.Kind == HandIntentKind.Poke)
                && intent.TargetId == RestorePromptId
                && _service != null && _service.RestoreOffered)
            {
                if (_service.TryRestore())
                {
                    _garden = _service.Garden;
                    _config = ConfigFrom(_service.Settings);
                    _session = new BreathSession(_config);
                    FirstRunStarted = false;
                    if (_restore != null) _restore.SetActive(false);
                    ApplyHoldMode();
                    PushIdle();
                    Log("Restored");
                }
                return;
            }
            if ((intent.Kind == HandIntentKind.Pinch || intent.Kind == HandIntentKind.Poke)
                && intent.TargetId == ContinuePromptId
                && _awaitContinue)
            {
                bool wasLatched = _latchedPause || _session.Phase != BreathPhase.Paused;
                _awaitContinue = false;
                _latchedPause = false;
                _pausedFor = 0f;
                if (wasLatched && !_autoPace)
                {
                    _needFreshPinch = true;
                    _sawOpen = !LivePinch();
                }
                else
                {
                    _needFreshPinch = false;
                    _sawOpen = false;
                }
                if (_prompt != null) _prompt.SetActive(false);
                Log("Continue");
                return;
            }
            if ((intent.Kind == HandIntentKind.Pinch || intent.Kind == HandIntentKind.Poke)
                && intent.TargetId == "pebble.settings")
            {
                Play("pebble.tap", _view != null ? FindNamed(_view.transform, "Pebbles") : null, 0f);
                return;
            }
            if (intent.Kind == HandIntentKind.Pinch || intent.Kind == HandIntentKind.Poke)
                HandleHabitIntent(intent);
        }

        void OnSystemPause()
        {
            if (_service != null)
            {
                _service.FlushHabits();
                _service.Save();
            }
            RefreshHabits();
            PushGarden();
            // Batch PlayMode runs have no user focus. A startup focus-loss must not freeze the scripted ritual.
            if (Application.isBatchMode) return;
            LatchPause();
        }

        void LatchPause()
        {
            if (_session.Phase == BreathPhase.Complete && _answered) return;
            if (_latchedPause)
            {
                // A second palm resumes the synthetic breath. A real pinch resumes itself once it lets go and starts again.
                if (_autoPace && !_awaitContinue)
                {
                    _latchedPause = false;
                    _needFreshPinch = false;
                    _sawOpen = false;
                    _pausedFor = 0f;
                    Log("Resumed");
                }
                return;
            }
            _latchedPause = true;
            _needFreshPinch = !_autoPace;
            _sawOpen = false;
            _frozenBreath = _view != null ? _view.breath : 0f;
            Log("PauseLatched");
            Play("pause.hold", null, 0f);
        }

        bool LivePinch()
        {
            if (_autoPace && !_awaitContinue)
            {
                float period = PaceInhaleSeconds + PaceExhaleSeconds;
                float local = period <= 0f ? 0f : _autoClock % period;
                return local < PaceInhaleSeconds;
            }
            return _source != null && _source.IsPinching;
        }

        void OnDev(DevCommand command)
        {
            if (!DevClockAllowed()) return;
            if (command == DevCommand.StateOverlay) _overlay = !_overlay;
            else if (command == DevCommand.AutoPace) SetAutoPace(!_autoPace);
            else if (command == DevCommand.NextDay) ShiftDay(1);
            else if (command == DevCommand.PreviousDay) ShiftDay(-1);
            else if (command == DevCommand.SeedPackets) OfferSeedPackets();
        }

        void PushIdle()
        {
            _view.uncoil = 0f;
            _view.fog = 0f;
            _view.breath = 0f;
            _view.answer = 0f;
            _view.answerTime = -1f;
            _view.phase = BreathPhase.Waiting;
            _frozenBreath = 0f;
            _pace = -1f;
            PushGarden();
        }

        void PushGarden()
        {
            if (_view == null || _service == null || _garden == null) return;
            bool animating = _answered && _lastAnswer.NewFrond && _answerTime >= 0f && _answerTime < 2.6f;
            bool recovered = _answered && _lastAnswer.Recovered && _answerTime >= 0f && _answerTime <= 2.5f;
            _view.recoveredTime = recovered ? _answerTime : -1f;
            if (CoilWaitingFiddle()) _view.uncoil = 0f;
            _view.PresentGarden(_garden, _service.TodayIndex, animating, recovered);
            _view.PresentCompanions(CompanionShots());
            _view.Apply();
        }

        List<CompanionGarden.Shot> CompanionShots()
        {
            var shots = new List<CompanionGarden.Shot>();
            if (_service == null) return shots;
            List<HabitDef> habits = _service.ActiveHabits();
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef habit = habits[i];
                if (habit == null) continue;
                shots.Add(new CompanionGarden.Shot
                {
                    Preset = habit.PresetKey,
                    Species = Companions.SpeciesFor(habit.PresetKey),
                    Leaves = _service.ShownLeaves(habit.Id),
                    Vitality = _service.HabitVitality(habit.Id),
                    Etch = false
                });
            }
            return shots;
        }

        void EnsureDesk()
        {
            if (_desk != null) return;
            var go = new GameObject("HabitDesk");
            PcDeskAnchor anchor = FindAnyObjectByType<PcDeskAnchor>();
            Transform parent = anchor != null ? anchor.transform : transform;
            go.transform.SetParent(parent, false);
            _desk = go.AddComponent<HabitDesk>();
            _desk.Bind(anchor);
        }

        void RefreshHabits()
        {
            if (!Application.isPlaying) return;
            EnsureDesk();
            if (_desk == null || _service == null) return;
            List<HabitDef> habits = _service.ActiveHabits();
            var views = new List<HabitDesk.HabitView>(habits.Count);
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef habit = habits[i];
                if (habit == null) continue;
                views.Add(new HabitDesk.HabitView
                {
                    Preset = habit.PresetKey,
                    Yesterday = _service.YesterdayVisible(habit.Id),
                    Undo = _service.PendingHabitId == habit.Id
                });
            }
            _desk.Apply(_service.PacketsOffered, _service.Settings.ReducedMotion, views);
        }

        void HandleHabitIntent(HandIntent intent)
        {
            if (_service == null || string.IsNullOrEmpty(intent.TargetId)) return;
            string id = intent.TargetId;
            TendSource source = intent.Kind == HandIntentKind.Poke ? TendSource.Poke : TendSource.Pinch;
            if (id.StartsWith("seed.", StringComparison.Ordinal))
            {
                _service.TryPlantHabit(id.Substring(5));
                RefreshHabits();
                PushGarden();
                return;
            }
            if (id.StartsWith("undo.", StringComparison.Ordinal))
            {
                TryUndoHabit();
                return;
            }
            string preset;
            if (SplitLabel(id, ".today", out preset))
            {
                TryCheckIn(preset, false, source);
                return;
            }
            if (SplitLabel(id, ".yesterday", out preset))
                TryCheckIn(preset, true, source);
        }

        void TryCheckIn(string preset, bool yesterday, TendSource source)
        {
            int semitones;
            if (_service == null || !_service.TryArmHabit(preset, yesterday, source, out semitones)) return;
            LastPluckSemitones = semitones;
            Play("habit.pluck", SprigAnchor(preset), semitones);
            RefreshHabits();
            PushGarden();
        }

        static bool SplitLabel(string id, string suffix, out string preset)
        {
            const string prefix = "label.";
            preset = null;
            if (id == null || !id.StartsWith(prefix, StringComparison.Ordinal) || !id.EndsWith(suffix, StringComparison.Ordinal))
                return false;
            preset = id.Substring(prefix.Length, id.Length - prefix.Length - suffix.Length);
            return preset.Length > 0;
        }

        void EnsureAudio()
        {
            if (_audio != null) return;
            _audio = GetComponent<AudioCueService>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioCueService>();
            if (_service != null && _service.Settings != null)
            {
                _audio.VoiceGuide = _service.Settings.VoiceGuide;
                _audio.Beds = _service.Settings.NightBed;
            }
        }

        /// <summary>Counts the request, then asks the cue service. The string id stays here so tests can scan it.</summary>
        void Play(string id, Transform at, float semitones)
        {
            int count;
            _cues.TryGetValue(id, out count);
            _cues[id] = count + 1;
            Debug.Log("[Terrarium] cue " + id + " semitones " + semitones.ToString(CultureInfo.InvariantCulture));
            EnsureAudio();
            if (_audio != null) _audio.Play(id, at, semitones);
        }

        void TickFirstRunCues()
        {
            if (!FirstRunStarted) return;
            if (!_cuedLand && _appTime >= 4f)
            {
                _cuedLand = true;
                Play("jar.land", JarAnchor(), 0f);
            }
            if (!_cuedLid && _appTime >= 6f)
            {
                _cuedLid = true;
                Play("jar.lid", CorkAnchor(), 0f);
            }
        }

        void TickAnswerCues()
        {
            bool ripple = _lastAnswer.NewFrond || _lastAnswer.Recovered;
            if (ripple && !_cuedMoss && _answerTime >= 0.35f)
            {
                _cuedMoss = true;
                Play("moss.ripple", MossAnchor(), 0f);
            }
            if (_lastAnswer.NewFrond && !_cuedDew && _answerTime >= 0.72f)
            {
                _cuedDew = true;
                Play("dew.drop", DewAnchor(), 0f);
            }
        }

        Transform JarAnchor()
        {
            return _view != null ? _view.transform : transform;
        }

        Transform CorkAnchor()
        {
            Transform cork = _view != null ? FindNamed(_view.transform, "Cork") : null;
            return cork != null ? cork : JarAnchor();
        }

        Transform MossAnchor()
        {
            Transform moss = _view != null ? FindNamed(_view.transform, "Moss") : null;
            return moss != null ? moss : JarAnchor();
        }

        Transform FrondAnchor()
        {
            if (_view != null && _view.newFrond != null) return _view.newFrond.transform;
            return JarAnchor();
        }

        Transform DewAnchor()
        {
            if (_view != null && _view.dew != null) return _view.dew.transform;
            return FrondAnchor();
        }

        Transform SprigAnchor(string preset)
        {
            if (_view == null || string.IsNullOrEmpty(preset)) return JarAnchor();
            Transform sprig = FindNamed(_view.transform, "Companion-" + preset);
            return sprig != null ? sprig : JarAnchor();
        }

        Transform LabelAnchor(string preset)
        {
            if (_desk == null || string.IsNullOrEmpty(preset)) return null;
            return FindNamed(_desk.transform, "Label-" + preset);
        }

        bool CoilWaitingFiddle()
        {
            if (_garden == null || _service == null || _session == null) return false;
            if (_session.Phase != BreathPhase.Complete && _session.Phase != BreathPhase.Waiting) return false;
            return _garden.DaysSinceRitual(_service.TodayIndex) > 0;
        }

        void BuildChrome()
        {
            BuildDots();
            BuildPaceRing();
            BuildPrompt();
            BuildRestorePrompt();
        }

        static BreathConfig ConfigFrom(RitualSettings settings)
        {
            var config = new BreathConfig();
            if (settings == null) return config;
            int breaths = settings.Breaths;
            if (breaths != 3 && breaths != 4 && breaths != 6 && breaths != 8) breaths = BreathConfigDefaults();
            config.TargetBreaths = breaths;
            if (settings.InhaleSec >= 1.2d) config.IdealInhaleSeconds = (float)settings.InhaleSec;
            return config;
        }

        static int BreathConfigDefaults()
        {
            return 6;
        }

        void ApplyHoldMode()
        {
            if (_keyboard == null || _service == null || _service.Settings == null) return;
            _keyboard.HoldMode = _service.Settings.HoldMode == "Toggle" ? HoldMode.Toggle : HoldMode.Hold;
        }

        static bool DevClockAllowed()
        {
            return Debug.isDebugBuild || Application.isEditor;
        }

        void BuildDots()
        {
            Transform cork = FindNamed(transform, "Cork");
            if (cork == null)
            {
                Debug.LogError("[Ritual] cork missing; breath dots were not built");
                return;
            }
            Shader shader = Shader.Find("Fidelity/Glow");
            if (shader == null)
            {
                Debug.LogError("[Ritual] Fidelity/Glow missing; breath dots were not built");
                return;
            }
            var renderer = cork.GetComponent<Renderer>();
            Bounds bounds = renderer != null ? renderer.bounds : new Bounds(cork.position, new Vector3(0.04f, 0.02f, 0.04f));
            _dots = new Renderer[6];
            _dotMats = new Material[6];
            float width = Mathf.Max(0.02f, bounds.size.x * 0.62f);
            Vector3 lossy = cork.lossyScale;
            for (int i = 0; i < 6; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "BreathDot" + i;
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                go.transform.SetParent(cork, false);
                float x = bounds.center.x + (i - 2.5f) * (width / 5f);
                float y = bounds.max.y + 0.0016f;
                float z = bounds.center.z - bounds.extents.z * 0.05f;
                go.transform.position = new Vector3(x, y, z);
                float diameter = 0.0042f;
                go.transform.localScale = new Vector3(
                    diameter / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
                    diameter / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
                    diameter / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
                var mat = new Material(shader) { name = "BreathDot" + i };
                mat.SetColor("_Tint", Color.white);
                mat.SetColor("_Emission", DotDim);
                mat.SetColor("_Rim", Color.black);
                var dotRenderer = go.GetComponent<Renderer>();
                dotRenderer.sharedMaterial = mat;
                dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dotRenderer.receiveShadows = false;
                _dots[i] = dotRenderer;
                _dotMats[i] = mat;
            }
            PaintDots(0);
        }

        void PaintDots(int filled)
        {
            FilledDots = filled;
            if (_dotMats == null) return;
            int n = Mathf.Clamp(filled, 0, _dotMats.Length);
            for (int i = 0; i < _dotMats.Length; i++)
            {
                if (_dotMats[i] == null) continue;
                _dotMats[i].SetColor("_Emission", i < n ? DotLit : DotDim);
            }
        }

        void BuildPaceRing()
        {
            Transform ring = FindNamed(transform, "BreathRing");
            if (ring == null) return;
            var pace = Instantiate(ring.gameObject, ring.parent);
            pace.name = "PaceRing";
            pace.transform.localPosition = ring.localPosition + new Vector3(0f, 0.001f, 0f);
            pace.transform.localRotation = ring.localRotation;
            pace.transform.localScale = ring.localScale * 1.08f;
            var renderer = pace.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            _paceMat = new Material(renderer.sharedMaterial) { name = "PaceRing" };
            _paceMat.SetFloat("_Fill", 0f);
            _paceMat.SetColor("_Color", PaceMint);
            renderer.sharedMaterial = _paceMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void BuildPrompt()
        {
            _prompt = new GameObject("ContinuePrompt");
            _prompt.transform.SetParent(transform, false);
            _prompt.transform.localPosition = new Vector3(0f, 0.105f, -0.09f);
            _prompt.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = _prompt.AddComponent<TextMesh>();
            text.text = "Continue breathing?";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.0024f;
            text.color = Etch;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
            var meshRenderer = text.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
            var box = _prompt.AddComponent<BoxCollider>();
            box.size = new Vector3(0.18f, 0.045f, 0.02f);
            _prompt.SetActive(true);
            var target = _prompt.AddComponent<IntentTarget>();
            target.Id = ContinuePromptId;
            _prompt.SetActive(false);
        }

        void BuildRestorePrompt()
        {
            _restore = new GameObject("RestorePrompt");
            _restore.transform.SetParent(transform, false);
            _restore.transform.localPosition = new Vector3(0f, 0.082f, -0.09f);
            _restore.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = _restore.AddComponent<TextMesh>();
            text.text = "restore the last copy?";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.0022f;
            text.color = Etch;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
            var meshRenderer = text.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
            var box = _restore.AddComponent<BoxCollider>();
            box.size = new Vector3(0.2f, 0.04f, 0.02f);
            _restore.SetActive(true);
            var target = _restore.AddComponent<IntentTarget>();
            target.Id = RestorePromptId;
            _restore.SetActive(false);
        }

        void Subscribe()
        {
            if (_source != null)
            {
                _source.Intent += OnIntent;
                _source.SystemPause += OnSystemPause;
            }
            if (_keyboard != null) _keyboard.DevCommandRaised += OnDev;
        }

        void Unsubscribe()
        {
            if (_source != null)
            {
                _source.Intent -= OnIntent;
                _source.SystemPause -= OnSystemPause;
            }
            if (_keyboard != null) _keyboard.DevCommandRaised -= OnDev;
        }

        static IHandIntentSource FindDefaultSource()
        {
            var keyboard = FindAnyObjectByType<KeyboardMouseIntentSource>();
            if (keyboard != null) return keyboard;
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var source = behaviours[i] as IHandIntentSource;
                if (source != null) return source;
            }
            return null;
        }

        static Transform FindNamed(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i];
            }
            return null;
        }

        void Log(string ev)
        {
            if (!LogEnabled()) return;
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string t = _appTime.ToString("0.######", CultureInfo.InvariantCulture);
                string line = "{\"t\":" + t + ",\"event\":\"" + ev + "\",\"state\":" + Snapshot().ToJson() + "}";
                File.AppendAllText(LogPath, line + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ritual] log failed: " + e.Message);
            }
        }

        static bool LogEnabled()
        {
            return Debug.isDebugBuild || Application.isEditor;
        }
    }
}

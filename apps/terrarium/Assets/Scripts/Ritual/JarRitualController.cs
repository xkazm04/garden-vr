using System;
using System.Globalization;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
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
        const float EngageStrength = 0.85f;

        [SerializeField] JarView _view;

        BreathConfig _config;
        BreathSession _session;
        Garden _garden;
        IHandIntentSource _source;
        KeyboardMouseIntentSource _keyboard;
        int _today;
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
        GUIStyle _overlayStyle;

        static readonly Color DotLit = new Color(0.45f, 1.15f, 0.72f);
        static readonly Color DotDim = new Color(0.015f, 0.04f, 0.028f);
        static readonly Color PaceMint = new Color(0.55f, 1.05f, 0.78f);
        static readonly Color Etch = new Color(0.749f, 0.961f, 0.867f);

        public BreathSession Session => _session;
        public Garden Garden => _garden;
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

        public static string LogPath => Path.Combine(Application.persistentDataPath, "logs", "ritual.jsonl");

        public TerrariumState Snapshot()
        {
            return TerrariumState.Capture(_session, _garden, _today);
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
            _config = new BreathConfig();
            _session = new BreathSession(_config);
            _garden = new Garden();
            _today = Garden.DayNumber(DateTime.Now);
            if (_view == null) _view = GetComponent<JarView>();
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_source == null) SetSource(FindDefaultSource());
            BuildChrome();
            PushIdle();
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

        void OnDestroy()
        {
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
            _updates++;
            float dt = Time.deltaTime;
            if (dt < 0f) dt = 0f;
            _appTime += dt;

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
            }
            else
            {
                _view.answerTime = -1f;
                _view.answer = 0f;
            }

            _view.uncoil = _session.Uncoil;
            _view.fog = _session.Fog;
            _view.phase = _session.Phase;
            _view.vitality = _garden.Vitality(_today);
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
            _view.Apply();
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
            _answered = true;
            _garden.CompleteRitual(_today);
            _answerTime = 0f;
            _view.vitality = _garden.Vitality(_today);
            Log("Answer");
        }

        void FlushSessionEvents()
        {
            while (_loggedEvents < _session.Events.Count)
            {
                BreathEvent ev = _session.Events[_loggedEvents++];
                if (ev.Kind == BreathEventKind.InhaleStarted) NoteInhaleStart();
                Log(ev.Kind.ToString());
            }
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.PalmOpen)
            {
                LatchPause();
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
            }
        }

        void OnSystemPause()
        {
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
            if (command == DevCommand.StateOverlay) _overlay = !_overlay;
            else if (command == DevCommand.AutoPace) SetAutoPace(!_autoPace);
        }

        void PushIdle()
        {
            _view.uncoil = 0f;
            _view.fog = 0f;
            _view.breath = 0f;
            _view.answer = 0f;
            _view.answerTime = -1f;
            _view.phase = BreathPhase.Waiting;
            _view.vitality = _garden.Vitality(_today);
            _frozenBreath = 0f;
            _pace = -1f;
        }

        void BuildChrome()
        {
            BuildDots();
            BuildPaceRing();
            BuildPrompt();
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

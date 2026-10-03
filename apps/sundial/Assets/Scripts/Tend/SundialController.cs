using System.Collections.Generic;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Glance and pinch. Look paints the halo after the provider's dwell (scripted looks dwell here).
    /// Pinch arms a deferred tend, plays the tock, pulses the plant and fills today's tile.
    /// The undo mark cancels inside 6 s. Palm open, system pause and quit commit early.
    /// App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class SundialController : MonoBehaviour
    {
        public const float DwellSeconds = 0.15f;
        public const float SweepSeconds = 1.5f;
        public const float PulseSeconds = 0.7f;
        public const float TileFillSeconds = 0.3f;
        public const string CueTock = "tend.tock";
        public const string CueInk = "tile.ink";
        public const string CueUndo = "tend.undo";

        public static string SaveDirectoryOverride;
        public static IClock ClockOverride;

        DialView _view;
        SundialService _service;
        IHandIntentSource _source;
        KeyboardMouseIntentSource _keyboard;
        bool _subscribed;
        readonly List<string> _cues = new List<string>();

        string _dwellId;
        float _dwellTime;
        bool _lookedThisFrame;
        string _lookArc;

        string _pendingArc;
        bool _filling;
        bool _fillDone;
        float _fillT;
        bool _pulsing;
        float _pulseT;
        int _pulseArc;

        bool _sweeping;
        bool _sweepBegun;
        float _swept;

        GameObject _undo;
        GameObject _fill;
        Material _inkMat;
        Texture2D _white;
        Texture2D _ring;
        Mesh _quad;
        GUIStyle _overlayStyle;

        public SundialService Service { get { return _service; } }
        public SundialState State { get { return _service == null ? null : _service.State; } }
        public string StateJson { get { return _service == null ? "" : _service.StateJson; } }
        public Ledger Ledger { get { return _service == null ? null : _service.Ledger; } }
        public DialView View { get { return _view; } }
        public LoadOutcome Outcome { get { return _service == null ? LoadOutcome.Failed : _service.Outcome; } }
        public bool UndoVisible { get { return _undo != null && _undo.activeSelf; } }
        public bool LookHaloOn { get { return _view != null && _view.halo > 0.5f; } }
        public bool FastClock { get { return _service != null && _service.FastClock; } }
        public bool OverlayVisible { get; private set; }
        public IReadOnlyList<string> Cues { get { return _cues; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Application.isPlaying) return;
            if (UnityEngine.Object.FindAnyObjectByType<SundialController>() != null) return;
            DialView view = UnityEngine.Object.FindAnyObjectByType<DialView>();
            if (view == null) return;
            view.gameObject.AddComponent<SundialController>();
        }

        public void SetSource(IHandIntentSource source)
        {
            Unsubscribe();
            _source = source;
            _keyboard = source as KeyboardMouseIntentSource;
            if (isActiveAndEnabled) Subscribe();
        }

        public bool ShiftDay(int delta)
        {
            if (_service == null) return false;
            return _service.TryShiftDay(delta);
        }

        public void HandleDev(DevCommand command)
        {
            if (_service == null) return;
            if (command == DevCommand.StateOverlay) OverlayVisible = !OverlayVisible;
            else if (command == DevCommand.NextDay) _service.TryShiftDay(1);
            else if (command == DevCommand.PreviousDay) _service.TryShiftDay(-1);
            else if (command == DevCommand.ClockFast) _service.ToggleFast();
        }

        public int LiveCount(string habitId)
        {
            return _service == null ? 0 : _service.LiveCount(habitId);
        }

        public int CueCount(string id)
        {
            int count = 0;
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i] == id) count++;
            }
            return count;
        }

        void Awake()
        {
            _view = GetComponent<DialView>();
            if (_view == null) _view = UnityEngine.Object.FindAnyObjectByType<DialView>();
            string directory = string.IsNullOrEmpty(SaveDirectoryOverride)
                ? Path.Combine(Application.persistentDataPath, "sundial")
                : SaveDirectoryOverride;
            IClock clock = ClockOverride ?? new SystemClock();
            _service = new SundialService(clock, directory);
            if (_view != null)
            {
                _view.halo = 0f;
                PushView();
                BeginSweep();
                _view.Apply();
            }
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_source == null)
            {
                KeyboardMouseIntentSource keyboard = UnityEngine.Object.FindAnyObjectByType<KeyboardMouseIntentSource>();
                if (keyboard != null) SetSource(keyboard);
            }
            BeginSweep();
        }

        void OnEnable() { Subscribe(); }

        void OnDisable() { Unsubscribe(); }

        void OnApplicationQuit() { CommitEarly(); }

        void OnDestroy()
        {
            CommitEarly();
            if (_inkMat != null) Destroy(_inkMat);
            if (_white != null) Destroy(_white);
            if (_ring != null) Destroy(_ring);
            if (_quad != null) Destroy(_quad);
        }

        void Update()
        {
            if (!Application.isPlaying || _service == null || _view == null) return;
            float dt = ClampStep(Time.deltaTime);
            _service.Step(dt);
            if (_pendingArc != null && !_service.IsPending)
                ClearTendVisual();

            if (!(_source is KeyboardMouseIntentSource))
            {
                if (_lookedThisFrame) _dwellTime += dt;
                if (_lookedThisFrame && _dwellTime >= DwellSeconds)
                    ShowLookHalo(_dwellId);
            }

            UpdatePulse(dt);
            UpdateFill(dt);
            PushView();
            UpdateSweep(dt);
            _view.time += dt;
            _view.Apply();
        }

        void LateUpdate()
        {
            if (!_lookedThisFrame)
            {
                _dwellId = null;
                _dwellTime = 0f;
            }
            _lookedThisFrame = false;
        }

        void OnGUI()
        {
            if (!OverlayVisible || _service == null) return;
            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle(GUI.skin.label);
                _overlayStyle.fontSize = 14;
                _overlayStyle.wordWrap = true;
                _overlayStyle.normal.textColor = new Color(0.16f, 0.15f, 0.13f, 1f);
            }
            GUI.Label(new Rect(16f, 16f, 920f, 320f), _service.StateJson, _overlayStyle);
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.Look) OnLook(intent.TargetId);
            else if (intent.Kind == HandIntentKind.Pinch) OnPinch(intent.TargetId);
            else if (intent.Kind == HandIntentKind.PalmOpen) CommitEarly();
        }

        void OnLook(string id)
        {
            _lookedThisFrame = true;
            if (_source is KeyboardMouseIntentSource)
            {
                ShowLookHalo(id);
                return;
            }
            if (id != _dwellId)
            {
                _dwellId = id;
                _dwellTime = 0f;
            }
        }

        void ShowLookHalo(string id)
        {
            string arc;
            if (!SundialArcs.TryPlant(id, out arc) || _view == null) return;
            if (_lookArc == arc && _view.halo > 0.5f) return;
            _lookArc = arc;
            _view.halo = 1f;
            _view.haloTarget = arc;
        }

        void OnPinch(string id)
        {
            string undoArc;
            if (SundialArcs.TryUndo(id, out undoArc))
            {
                TryUndo(undoArc);
                return;
            }
            string arc;
            if (!SundialArcs.TryPlant(id, out arc)) return;
            HabitDef habit = _service.HabitForArc(arc);
            PlantState plant = _service.PlantFor(habit);
            if (habit == null || plant == null || plant.Window == null || plant.Window.Length < 7) return;
            // A second pinch the same day, or a pinch while one tend is still waiting, leaves the picture alone.
            if (_service.IsPending) return;
            if (plant.Window[6] != TileState.Today) return;

            _service.Arm(habit.Id);
            PlayCue(CueTock);
            PlayCue(CueInk);
            _pendingArc = arc;
            _pulseArc = SundialArcs.Index(arc);
            _pulsing = true;
            _pulseT = 0f;
            _view.pulse = 0.001f;
            _view.pulseArc = _pulseArc;
            _fillT = 0f;
            _fillDone = _service.ReducedMotion;
            _filling = !_service.ReducedMotion;
            ShowUndo(arc);
            if (_service.ReducedMotion) HideFill();
            else ShowFill(arc, 0f);
        }

        void TryUndo(string arc)
        {
            if (!_service.IsPending || _pendingArc != arc) return;
            if (!_service.Cancel()) return;
            PlayCue(CueUndo);
            ClearTendVisual();
        }

        void CommitEarly()
        {
            if (_service == null) return;
            _service.Flush();
            ClearTendVisual();
        }

        void ClearTendVisual()
        {
            _pendingArc = null;
            _filling = false;
            _fillDone = false;
            _pulsing = false;
            if (_view != null) _view.pulse = 0f;
            HideUndo();
            HideFill();
        }

        void UpdatePulse(float dt)
        {
            if (!_pulsing || _view == null) return;
            _pulseT += dt;
            float u = Mathf.Clamp01(_pulseT / PulseSeconds);
            _view.pulse = u <= 0f ? 0.001f : u;
            _view.pulseArc = _pulseArc;
            if (u >= 1f)
            {
                _pulsing = false;
                _view.pulse = 0f;
            }
        }

        void UpdateFill(float dt)
        {
            if (!_filling) return;
            _fillT += dt;
            float u = Mathf.Clamp01(_fillT / TileFillSeconds);
            PlaceFill(u);
            if (u >= 1f)
            {
                _filling = false;
                _fillDone = true;
                HideFill();
            }
        }

        void BeginSweep()
        {
            if (_sweepBegun || _view == null || _service == null || _service.State == null) return;
            _sweepBegun = true;
            _swept = 0f;
            if (_service.ReducedMotion)
            {
                _sweeping = false;
                _view.gnomonDeg = _service.State.GnomonDeg;
            }
            else
            {
                _sweeping = true;
                _view.gnomonDeg = 0f;
            }
        }

        void UpdateSweep(float dt)
        {
            if (_view == null || _service == null || _service.State == null) return;
            float target = _service.State.GnomonDeg;
            if (!_sweeping)
            {
                _view.gnomonDeg = target;
                return;
            }
            _swept += dt;
            float u = Mathf.Clamp01(_swept / SweepSeconds);
            _view.gnomonDeg = Mathf.Lerp(0f, target, u);
            if (u >= 1f)
            {
                _sweeping = false;
                _view.gnomonDeg = target;
            }
        }

        void PushView()
        {
            SundialState state = _service.State;
            if (state == null) return;
            if (_view.tiles == null || _view.tiles.Length != DialView.TileCount)
                _view.tiles = new int[DialView.TileCount];
            for (int i = 0; i < _view.tiles.Length; i++) _view.tiles[i] = 0;

            int due = -1;
            if (_service.Save != null && _service.Save.Habits != null)
            {
                for (int i = 0; i < _service.Save.Habits.Count; i++)
                {
                    HabitDef habit = _service.Save.Habits[i];
                    PlantState plant = _service.PlantFor(habit);
                    if (habit == null || plant == null || plant.Window == null) continue;
                    int arc = SundialArcs.Index(SundialArcs.Key(habit.Group));
                    if (arc < 0) continue;
                    int stage = (int)plant.Stage;
                    float bloom = plant.Bloom == Bloom.Open ? 2f : plant.Bloom == Bloom.Bud ? 1f : 0f;
                    if (arc == 0) { _view.stageMorning = stage; _view.bloomMorning = bloom; }
                    else if (arc == 1) { _view.stageMidday = stage; _view.bloomMidday = bloom; }
                    else { _view.stageWinddown = stage; _view.bloomWinddown = bloom; }
                    int count = plant.Window.Length < 7 ? plant.Window.Length : 7;
                    for (int slot = 0; slot < count; slot++)
                        _view.tiles[arc * 7 + slot] = SundialArcs.TileDigit(plant.Window[slot]);
                    if (_fillDone && _pendingArc != null && SundialArcs.Index(_pendingArc) == arc)
                        _view.tiles[arc * 7 + 6] = SundialArcs.TileDigit(TileState.Kept);
                    if (plant.DueNow) due = arc;
                }
            }
            _view.waiting = due >= 0 ? 1f : 0f;
            if (due >= 0) _view.waitingTarget = SundialArcs.FromIndex(due);
            _view.boil = _service.Boil;
            _view.reducedMotion = _service.ReducedMotion;
        }

        void PlayCue(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _cues.Add(id);
            // The cue service lands later. Calling the id is the contract. A missing clip is harmless.
            Debug.Log("[Sundial] cue " + id);
        }

        void ShowUndo(string arc)
        {
            HideUndo();
            Transform tile = FindNamed("tile." + arc + ".6");
            _undo = MakeMark("undo." + arc, _ring, Color.white, 0.012f);
            var box = _undo.AddComponent<BoxCollider>();
            box.size = new Vector3(0.016f, 0.012f, 0.016f);
            var target = _undo.AddComponent<IntentTarget>();
            target.Id = "undo." + arc;
            if (tile != null)
            {
                _undo.transform.position = tile.position + Vector3.up * 0.008f;
                _undo.transform.rotation = tile.rotation;
            }
        }

        void ShowFill(string arc, float amount)
        {
            HideFill();
            Transform tile = FindNamed("tile." + arc + ".6");
            _fill = MakeMark("ink." + arc, _white, Wash(arc), 0.001f);
            if (tile != null)
            {
                _fill.transform.SetParent(tile, false);
                _fill.transform.localRotation = Quaternion.identity;
            }
            PlaceFill(amount);
        }

        void PlaceFill(float amount)
        {
            if (_fill == null) return;
            const float full = 0.012f;
            float width = Mathf.Max(0.0004f, full * Mathf.Clamp01(amount));
            _fill.transform.localPosition = new Vector3((-full + width) * 0.5f, 0.004f, 0f);
            _fill.transform.localScale = new Vector3(width, 1f, 0.009f);
        }

        GameObject MakeMark(string name, Texture2D texture, Color color, float size)
        {
            EnsureInk();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            if (_quad == null || _inkMat == null) return go;
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _inkMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            if (texture != null) block.SetTexture("_MainTex", texture);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            go.transform.localScale = new Vector3(size, 1f, size);
            return go;
        }

        void EnsureInk()
        {
            if (_inkMat != null) return;
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader == null) return;
            _inkMat = new Material(shader);
            _inkMat.SetFloat("_Src", 5f);
            _inkMat.SetFloat("_Dst", 10f);
            _inkMat.SetFloat("_Ring", 0f);
            _inkMat.SetFloat("_Coverage", 0f);
            _inkMat.SetFloat("_Mask", 0f);
            _white = Solid(Color.white);
            _ring = Ring();
            _quad = FlatQuad();
        }

        static Texture2D Solid(Color color)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var pixels = new Color[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Texture2D Ring()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color[n * n];
            var ink = new Color(0.165f, 0.149f, 0.133f, 1f);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - (n - 1) * 0.5f;
                    float dy = y - (n - 1) * 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * n + x] = d > 8f && d < 13f ? ink : new Color(0f, 0f, 0f, 0f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Mesh FlatQuad()
        {
            var mesh = new Mesh { name = "TendMark" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0.5f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Color Wash(string arc)
        {
            if (arc == "morning") return new Color(0.886f, 0.722f, 0.400f, 1f);
            if (arc == "winddown") return new Color(0.655f, 0.604f, 0.839f, 1f);
            return new Color(0.890f, 0.612f, 0.510f, 1f);
        }

        void HideUndo()
        {
            if (_undo == null) return;
            _undo.SetActive(false);
            Destroy(_undo);
            _undo = null;
        }

        void HideFill()
        {
            if (_fill == null) return;
            _fill.SetActive(false);
            Destroy(_fill);
            _fill = null;
        }

        Transform FindNamed(string name)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        void Subscribe()
        {
            if (_source == null || _subscribed) return;
            _source.Intent += OnIntent;
            _source.SystemPause += OnSystemPause;
            if (_keyboard != null) _keyboard.DevCommandRaised += HandleDev;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_source != null)
            {
                _source.Intent -= OnIntent;
                _source.SystemPause -= OnSystemPause;
            }
            if (_keyboard != null) _keyboard.DevCommandRaised -= HandleDev;
            _subscribed = false;
        }

        void OnSystemPause() { CommitEarly(); }

        static float ClampStep(float dt)
        {
            if (dt < 0f) return 0f;
            if (dt > 0.1f) return 0.1f;
            return dt;
        }
    }
}

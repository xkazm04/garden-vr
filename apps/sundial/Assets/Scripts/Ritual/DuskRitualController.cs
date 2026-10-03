using System.Collections.Generic;
using System.IO;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Three self-paced breaths at the wind-down plant. Pinch-hold breathes in, release breathes out.
    /// The ink circle follows that breath. It is never auto-paced. The shared <see cref="BreathSession"/>
    /// decides what counts. On the third breath the plant opens its next leaf, a sparkle ring plays,
    /// and the habit is kept at once with source Ritual. There is no undo window.
    /// App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public sealed class DuskRitualController : MonoBehaviour
    {
        public const int BreathsRequired = 3;
        public const float MinInhaleSeconds = 1.5f;
        public const float ContinueAfterSeconds = 60f;
        public const float MinDiameter = 0.034f;
        public const float MaxDiameter = 0.105f;
        public const string PlantId = "plant.winddown";
        public const string BreathPromptId = "prompt.breaths";
        public const string ContinuePromptId = "prompt.continue";
        public const string CueChime = "dusk.chime";
        public const string OfferLine = SeedCatalog.BreathOffer;
        public const string ContinueLine = "Continue?";
        const float EngageStrength = 0.85f;
        const float SparkleSeconds = 3.2f;

        SundialController _sundial;
        BreathConfig _config;
        BreathSession _session;
        IHandIntentSource _source;
        bool _subscribed;
        bool _built;
        bool _active;
        bool _answered;
        bool _reduced;
        bool _latchedPause;
        bool _needFreshPinch;
        bool _sawOpen;
        bool _awaitContinue;
        bool _holdVisual;
        float _amount;
        float _pausedFor;
        float _appTime;
        float _leafT;
        float _sparkleT;
        int _leafBefore;

        GameObject _circle;
        GameObject _sparkle;
        GameObject _offer;
        GameObject _continue;
        Material _inkMat;
        Material _sparkleMat;
        Texture2D _ringTex;
        Texture2D _fillTex;
        Texture2D _cream;
        Mesh _quad;
        readonly List<Texture2D> _sparkles = new List<Texture2D>();
        readonly List<string> _cues = new List<string>();
        float _paintedFill = -1f;
        SundialVoice _voice;
        GameObject _caption;
        TextMesh _captionMesh;

        static readonly Color Ink = new Color(0.165f, 0.149f, 0.133f, 1f);
        static readonly Color Paper = new Color(0.953f, 0.933f, 0.886f, 0.94f);

        public BreathSession Session { get { return _session; } }
        public bool Active { get { return _active; } }
        public bool Answered { get { return _answered; } }
        public int Breaths { get { return _session == null ? 0 : _session.Breaths; } }
        public float AppTime { get { return _appTime; } }
        public float CircleAmount { get; private set; }
        public float CircleDiameter { get; private set; }
        public bool FillRing { get; private set; }
        public bool OfferVisible { get { return _offer != null && _offer.activeSelf; } }
        public bool AwaitingContinue { get { return _awaitContinue; } }
        public float PausedFor { get { return _pausedFor; } }
        public bool LeafOpened { get; private set; }
        public Stage LeafStage { get; private set; }
        public bool SparkleVisible { get { return _sparkle != null && _sparkle.activeSelf; } }
        public IReadOnlyList<string> Cues { get { return _cues; } }

        public int CueCount(string id)
        {
            int count = 0;
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i] == id) count++;
            }
            return count;
        }

        /// <summary>Focus loss. The breath stays where it was. A fresh pinch continues it. It does not start over.</summary>
        public void NotifyFocusLost()
        {
            LatchPause();
        }

        public void SetSource(IHandIntentSource source)
        {
            if (_source == source) return;
            Unsubscribe();
            _source = source;
            if (isActiveAndEnabled) Subscribe();
        }

        void Awake()
        {
            _sundial = GetComponent<SundialController>();
            _config = new BreathConfig();
            _config.TargetBreaths = BreathsRequired;
            _config.MinInhaleSeconds = MinInhaleSeconds;
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            BuildChrome();
            if (_source == null && _sundial != null && _sundial.Source != null)
                SetSource(_sundial.Source);
            RefreshOffer();
        }

        void OnEnable() { Subscribe(); }

        void OnDisable() { Unsubscribe(); }

        void OnDestroy()
        {
            if (_inkMat != null) Destroy(_inkMat);
            if (_sparkleMat != null) Destroy(_sparkleMat);
            if (_ringTex != null) Destroy(_ringTex);
            if (_fillTex != null) Destroy(_fillTex);
            if (_cream != null) Destroy(_cream);
            if (_quad != null) Destroy(_quad);
            for (int i = 0; i < _sparkles.Count; i++)
            {
                if (_sparkles[i] != null) Destroy(_sparkles[i]);
            }
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            if (_sundial != null && _sundial.Source != _source)
                SetSource(_sundial.Source);
            if (!_built) BuildChrome();

            float dt = ClampStep(Time.deltaTime);
            _appTime += dt;
            _reduced = _sundial != null && _sundial.Service != null && _sundial.Service.ReducedMotion;
            TouchVoice();
            if (_voice != null)
            {
                if (_source != null) _voice.ObservePinching(_source.IsPinching);
                _voice.Tick(dt);
            }

            bool pinching = _source != null && _source.IsPinching;
            if (_needFreshPinch && !pinching) _sawOpen = true;
            if (_needFreshPinch && _sawOpen && !_awaitContinue && pinching)
            {
                _needFreshPinch = false;
                _latchedPause = false;
                _sawOpen = false;
                _pausedFor = 0f;
            }

            bool freeze = _latchedPause || _needFreshPinch || _awaitContinue;
            if (_active && !_answered && !freeze && _session != null)
            {
                _session.Update(dt, ReadSample());
                TryComplete();
            }

            bool trackingHold = _session != null && _session.Phase == BreathPhase.Paused;
            _holdVisual = (_active && !_answered && freeze) || trackingHold;
            if (!_holdVisual)
                AdvanceAmount(dt);

            if (_holdVisual && _active && !_answered)
            {
                _pausedFor += dt;
                if (_pausedFor > ContinueAfterSeconds && !_awaitContinue)
                    _awaitContinue = true;
            }
            else if (!_holdVisual)
            {
                _pausedFor = 0f;
            }

            if (_answered)
                _sparkleT += dt;
            if (LeafOpened)
                _leafT += dt;

            PublishCircle();
            RefreshOffer();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !_built) return;
            Transform plant = PlantTransform();
            if (plant == null) return;
            ApplyLeaf(plant);
            PlaceCircle(plant);
            PlaceSparkle(plant);
            PlacePrompt(plant);
            PlaceCaption(plant);
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.PalmOpen)
            {
                LatchPause();
                return;
            }
            if (_awaitContinue && intent.TargetId == ContinuePromptId && IsAcknowledge(intent.Kind))
            {
                _awaitContinue = false;
                _pausedFor = 0f;
                return;
            }

            bool starting = !_active && !_answered && CueAllows(intent);
            if (starting) Begin(intent.Kind == HandIntentKind.PinchHold);
            if (_voice != null && _active && !_answered
                && (intent.Kind == HandIntentKind.PinchHold || intent.Kind == HandIntentKind.Release))
                _voice.OnIntent(intent, Breaths, BreathsRequired);
        }

        bool CueAllows(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.PinchHold && intent.TargetId == PlantId && CueDue())
                return true;
            return intent.TargetId == BreathPromptId && IsAcknowledge(intent.Kind) && !KeptToday();
        }

        void OnSystemPause()
        {
            LatchPause();
        }

        void Begin(bool fromHold)
        {
            if (_active || _answered || KeptToday()) return;
            _config = new BreathConfig();
            _config.TargetBreaths = BreathsRequired;
            _config.MinInhaleSeconds = MinInhaleSeconds;
            _session = new BreathSession(_config);
            _active = true;
            _amount = 0f;
            CircleAmount = 0f;
            PlantState plant = WindPlant();
            _leafBefore = plant == null ? 0 : (int)plant.Stage;
            TouchVoice();
            if (_voice != null) _voice.OnRitualStart(fromHold);
        }

        void LatchPause()
        {
            if (!_active || _answered || _latchedPause) return;
            _latchedPause = true;
            _needFreshPinch = true;
            _sawOpen = false;
        }

        void TryComplete()
        {
            if (_answered || _session == null || _session.Phase != BreathPhase.Complete) return;
            _answered = true;
            _active = false;
            _sparkleT = 0f;
            _leafT = 0f;
            HabitDef habit = WindHabit();
            if (habit != null && _sundial != null && _sundial.Service != null)
            {
                TendResult result = _sundial.Service.TendRitual(habit.Id);
                PlantState after = _sundial.Service.PlantFor(habit);
                LeafOpened = true;
                LeafStage = after == null ? (Stage)_leafBefore : after.Stage;
                if (result != null && result.Ok && !result.AlreadyKept)
                    PlayCue(CueChime);
            }
            TouchVoice();
            if (_voice != null) _voice.OnComplete();
        }

        void AdvanceAmount(float dt)
        {
            if (_session == null || _config == null)
            {
                _amount = 0f;
                return;
            }
            if (_session.Phase == BreathPhase.Inhaling)
            {
                float ideal = _config.IdealInhaleSeconds <= 0.01f ? 4f : _config.IdealInhaleSeconds;
                _amount = Mathf.Clamp01(_session.PhaseTime / ideal);
            }
            else
            {
                float shrink = _config.MinExhaleSeconds <= 0.01f ? 1f : _config.MinExhaleSeconds;
                _amount = Mathf.Max(0f, _amount - dt / shrink);
            }
        }

        void PublishCircle()
        {
            FillRing = _reduced;
            CircleAmount = _amount;
            if (!CircleShown())
            {
                CircleDiameter = 0f;
                return;
            }
            CircleDiameter = _reduced ? MaxDiameter : Mathf.Lerp(MinDiameter, MaxDiameter, Mathf.Clamp01(_amount));
        }

        bool CircleShown()
        {
            if (_session == null || _answered) return false;
            bool live = _holdVisual
                || _session.Phase == BreathPhase.Inhaling
                || _session.Phase == BreathPhase.Exhaling
                || _session.Phase == BreathPhase.Paused;
            if (!_active && !_holdVisual) return false;
            if (!live) return false;
            if (_reduced) return _session.Phase == BreathPhase.Inhaling || _amount > 0.015f || _holdVisual;
            return _session.Phase == BreathPhase.Inhaling || _amount > 0.001f || _holdVisual;
        }

        PinchSample ReadSample()
        {
            float strength = _source != null ? _source.PinchStrength : 0f;
            bool tracked = _source == null || _source.IsTracked;
            if (tracked && _source != null && _source.IsPinching && strength < EngageStrength)
                strength = EngageStrength;
            return new PinchSample(strength, tracked);
        }

        bool CueDue()
        {
            if (FirstRunWizard.SuppressBreathOffer) return false;
            if (KeptToday()) return false;
            HabitDef habit = WindHabit();
            if (habit == null || _sundial == null || _sundial.Service == null) return false;
            if (habit.CreatedDay == _sundial.Service.Today().Index) return true;
            return _sundial.State != null && _sundial.State.Arc == ArcId.WindDown;
        }

        bool KeptToday()
        {
            HabitDef habit = WindHabit();
            if (habit == null || _sundial == null || _sundial.Service == null || _sundial.Ledger == null)
                return false;
            return _sundial.Ledger.IsKept(habit.Id, _sundial.Service.Today().Index);
        }

        HabitDef WindHabit()
        {
            if (_sundial == null || _sundial.Service == null) return null;
            return _sundial.Service.HabitForArc("winddown");
        }

        PlantState WindPlant()
        {
            HabitDef habit = WindHabit();
            if (habit == null || _sundial == null || _sundial.Service == null) return null;
            return _sundial.Service.PlantFor(habit);
        }

        Transform PlantTransform()
        {
            if (_sundial == null || _sundial.View == null) return null;
            return _sundial.View.transform.Find(PlantId);
        }

        void RefreshOffer()
        {
            if (_offer == null) return;
            bool showOffer = CueDue() && !_active && !_answered && !_awaitContinue;
            if (_offer.activeSelf != showOffer) _offer.SetActive(showOffer);
            if (_continue == null) return;
            if (_continue.activeSelf != _awaitContinue) _continue.SetActive(_awaitContinue);
        }

        void ApplyLeaf(Transform plant)
        {
            if (!LeafOpened || _reduced || _leafT >= 0.7f) return;
            float u = Mathf.Clamp01(_leafT / 0.7f);
            float s = Mathf.Lerp(0.82f, 1f, u * u * (3f - 2f * u));
            Vector3 scale = plant.localScale;
            plant.localScale = new Vector3(scale.x * s, scale.y * s, scale.z * s);
        }

        void PlaceCircle(Transform plant)
        {
            if (_circle == null) return;
            bool show = CircleDiameter > 0.001f;
            if (_circle.activeSelf != show) _circle.SetActive(show);
            if (!show) return;
            Fit(plant, _circle.transform, CircleDiameter, CircleDiameter, 0.5f, 0.045f);
            if (_reduced)
            {
                PaintFill(_amount);
                if (_fillTex != null) SetTexture(_circle, _fillTex);
            }
            else if (_ringTex != null)
            {
                SetTexture(_circle, _ringTex);
            }
        }

        void PlaceSparkle(Transform plant)
        {
            if (_sparkle == null) return;
            bool show = _answered && _sparkleT < SparkleSeconds && _sparkles.Count > 0;
            if (_sparkle.activeSelf != show) _sparkle.SetActive(show);
            if (!show) return;
            int frame = 0;
            if (!_reduced && _sparkles.Count > 1)
                frame = Mathf.FloorToInt(_sparkleT * 8f) % _sparkles.Count;
            SetTexture(_sparkle, _sparkles[frame]);
            float fade = _sparkleT > SparkleSeconds - 0.6f
                ? Mathf.Clamp01((SparkleSeconds - _sparkleT) / 0.6f)
                : 1f;
            Fit(plant, _sparkle.transform, 0.12f, 0.12f, 0.55f, 0.06f);
            if (_sparkleMat != null)
            {
                var color = Color.white;
                color.a = fade;
                _sparkleMat.SetColor("_Color", color);
            }
        }

        void PlacePrompt(Transform plant)
        {
            // The plant faces the camera, so local +X reads as screen-left. Negative sits in the dusk wash.
            PlaceLine(_offer, plant, -0.095f);
            PlaceLine(_continue, plant, -0.095f);
        }

        void PlaceCaption(Transform plant)
        {
            if (_caption == null || !_caption.activeSelf) return;
            PlaceLine(_caption, plant, 0.095f);
        }

        void SetCaption(string text)
        {
            if (!Application.isPlaying) return;
            if (!_built) BuildChrome();
            if (_caption == null) _caption = MakeCaption();
            if (_caption == null) return;
            bool show = !string.IsNullOrEmpty(text);
            if (_caption.activeSelf != show) _caption.SetActive(show);
            if (show && _captionMesh != null) _captionMesh.text = text;
        }

        void TouchVoice()
        {
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            if (_sundial == null) return;
            if (_voice == null)
                _voice = new SundialVoice(_sundial.Audio, id => _sundial.Play(id, null), SetCaption);
            _voice.Enabled = _sundial.Service != null && _sundial.Service.Voice;
        }

        void PlaceLine(GameObject line, Transform plant, float worldX)
        {
            if (line == null || plant == null) return;
            float sx = Mathf.Max(0.0001f, plant.localScale.x);
            float sy = Mathf.Max(0.0001f, plant.localScale.y);
            line.transform.SetParent(plant, false);
            line.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            line.transform.localPosition = new Vector3(worldX / sx, 0.40f, 0.05f);
            line.transform.localScale = new Vector3(1f / sx, 1f / sy, 1f / sy);
            var box = line.GetComponent<BoxCollider>();
            if (box != null)
            {
                Vector3 scale = line.transform.localScale;
                box.size = new Vector3(0.18f / scale.x, 0.036f / scale.y, 0.01f);
            }
        }

        static void Fit(Transform plant, Transform child, float worldW, float worldH, float localY, float localZ)
        {
            float sx = Mathf.Max(0.0001f, plant.localScale.x);
            float sy = Mathf.Max(0.0001f, plant.localScale.y);
            child.SetParent(plant, false);
            child.localRotation = Quaternion.identity;
            child.localPosition = new Vector3(0f, localY, localZ);
            child.localScale = new Vector3(worldW / sx, worldH / sy, 1f);
        }

        void BuildChrome()
        {
            if (_built) return;
            _built = true;
            EnsureInk();
            _quad = CenterQuad();
            _ringTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            _ringTex.name = "DuskInkRing";
            _ringTex.wrapMode = TextureWrapMode.Clamp;
            PaintRing(_ringTex, 1f, false);
            _fillTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            _fillTex.name = "DuskFillRing";
            _fillTex.wrapMode = TextureWrapMode.Clamp;
            PaintFill(0f);
            LoadSparkles();
            _circle = MakeQuad("DuskCircle", _inkMat, _ringTex);
            _sparkle = MakeQuad("DuskSparkle", _sparkleMat, _sparkles.Count > 0 ? _sparkles[0] : _ringTex);
            _circle.SetActive(false);
            _sparkle.SetActive(false);
            _offer = MakeLine("BreathPrompt", OfferLine, BreathPromptId);
            _continue = MakeLine("ContinuePrompt", ContinueLine, ContinuePromptId);
            _offer.SetActive(false);
            _continue.SetActive(false);
        }

        void LoadSparkles()
        {
            string[] names = { "sparkle_ring_1.png", "sparkle_ring_2.png", "sparkle_ring_3.png" };
            string folder = Path.Combine(Application.dataPath, "Art", "Textures");
            for (int i = 0; i < names.Length; i++)
            {
                string path = Path.Combine(folder, names[i]);
                if (!File.Exists(path)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                {
                    Destroy(tex);
                    continue;
                }
                tex.name = names[i];
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                _sparkles.Add(tex);
            }
        }

        GameObject MakeQuad(string name, Material material, Texture2D texture)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetTexture(go, texture);
            return go;
        }

        GameObject MakeLine(string name, string text, string id)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var chip = new GameObject("chip");
            chip.transform.SetParent(go.transform, false);
            // The line is turned 180 so TextMesh (visible from its back) faces the camera.
            // The chip quad faces +Z, so it turns again and sits just behind the glyphs.
            chip.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            chip.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            chip.transform.localScale = new Vector3(0.128f, 0.022f, 1f);
            chip.AddComponent<MeshFilter>().sharedMesh = _quad;
            var chipRenderer = chip.AddComponent<MeshRenderer>();
            chipRenderer.sharedMaterial = _inkMat;
            chipRenderer.shadowCastingMode = ShadowCastingMode.Off;
            chipRenderer.receiveShadows = false;
            SetTexture(chip, _cream);
            var block = new MaterialPropertyBlock();
            chipRenderer.GetPropertyBlock(block);
            block.SetTexture("_MainTex", _cream);
            block.SetColor("_Color", Paper);
            chipRenderer.SetPropertyBlock(block);

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = 0.0034f;
            mesh.color = Ink;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) mesh.font = font;
            var textRenderer = mesh.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.shadowCastingMode = ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
            }
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.18f, 0.036f, 0.01f);
            var target = go.AddComponent<IntentTarget>();
            target.Id = id;
            return go;
        }

        GameObject MakeCaption()
        {
            var go = new GameObject("VoiceCaption");
            go.transform.SetParent(transform, false);
            _captionMesh = go.AddComponent<TextMesh>();
            _captionMesh.text = "";
            _captionMesh.anchor = TextAnchor.MiddleCenter;
            _captionMesh.alignment = TextAlignment.Center;
            _captionMesh.fontSize = 48;
            _captionMesh.characterSize = 0.0032f;
            _captionMesh.color = Ink;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) _captionMesh.font = font;
            var textRenderer = _captionMesh.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.shadowCastingMode = ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
            }
            go.SetActive(false);
            return go;
        }

        void EnsureInk()
        {
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader == null) return;
            _inkMat = new Material(shader) { name = "DuskInk" };
            _sparkleMat = new Material(shader) { name = "DuskSparkle" };
            Prepare(_inkMat);
            Prepare(_sparkleMat);
            _cream = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var pixels = new Color[4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            _cream.SetPixels(pixels);
            _cream.Apply(false, false);
        }

        static void Prepare(Material mat)
        {
            mat.SetFloat("_Src", 5f);
            mat.SetFloat("_Dst", 10f);
            mat.SetFloat("_Ring", 0f);
            mat.SetFloat("_Coverage", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Sparkle", 0f);
            mat.SetColor("_Color", Color.white);
            mat.SetColor("_Color2", Color.clear);
        }

        static void SetTexture(GameObject go, Texture2D texture)
        {
            if (go == null || texture == null) return;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_MainTex", texture);
            renderer.SetPropertyBlock(block);
        }

        void PaintFill(float fill)
        {
            if (_fillTex == null) return;
            if (_paintedFill >= 0f && Mathf.Abs(_paintedFill - fill) < 0.01f) return;
            _paintedFill = fill;
            PaintRing(_fillTex, Mathf.Clamp01(fill), true);
        }

        static void PaintRing(Texture2D tex, float fill, bool fillMode)
        {
            int n = tex.width;
            var pixels = new Color[n * n];
            float mid = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - mid) / mid;
                    float dy = (y - mid) / mid;
                    float ang = Mathf.Atan2(dx, dy);
                    float wobble = 0.012f * Mathf.Sin(ang * 5f) + 0.008f * Mathf.Sin(ang * 9f + 1.2f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = Mathf.Abs(dist - (0.72f + wobble));
                    float stroke = Mathf.Clamp01(1f - band / 0.055f);
                    if (Mathf.Sin(ang * 13f) > 0.93f) stroke *= 0.4f;
                    float alpha = stroke;
                    if (fillMode)
                    {
                        float around = Mathf.Repeat((ang / (Mathf.PI * 2f)) + 0.5f, 1f);
                        float track = stroke * 0.2f;
                        float lit = around <= fill ? stroke : 0f;
                        alpha = Mathf.Max(track, lit);
                    }
                    pixels[y * n + x] = new Color(Ink.r, Ink.g, Ink.b, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
        }

        static Mesh CenterQuad()
        {
            var mesh = new Mesh { name = "DuskQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
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

        void PlayCue(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _cues.Add(id);
            if (_sundial != null) _sundial.Play(id, PlantTransform());
            else Debug.Log("[Sundial] cue " + id);
        }

        void Subscribe()
        {
            if (_source == null || _subscribed) return;
            _source.Intent += OnIntent;
            _source.SystemPause += OnSystemPause;
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
            _subscribed = false;
        }

        static bool IsAcknowledge(HandIntentKind kind)
        {
            return kind == HandIntentKind.Pinch
                || kind == HandIntentKind.PinchHold
                || kind == HandIntentKind.Poke;
        }

        static float ClampStep(float dt)
        {
            if (dt < 0f) return 0f;
            if (dt > 0.1f) return 0.1f;
            return dt;
        }
    }
}

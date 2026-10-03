using System.Collections.Generic;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Midday gratitude. Five small ink symbols in the midday arc.
    /// Pinch one. It is inked into the arc for the day and the midday habit is tended at once,
    /// source Ritual, with no undo window. Nothing is stored but the symbol index and that tend.
    /// One existing cue, bloom.flutter. No new audio. No text entry.
    /// App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class GratitudeRitualController : MonoBehaviour
    {
        public const int SymbolCount = GratitudeRecord.SymbolCount;
        public const string OfferLine = "One small mark?";
        public const string CueInk = "bloom.flutter";
        public const float TwoFootMetres = 0.61f;
        const float ArcRadius = 0.76f;
        const float MarkSize = 0.030f;
        const float InkScale = 2.35f;
        const float EaseSeconds = 0.45f;
        const int TexSize = 96;

        SundialController _sundial;
        IHandIntentSource _source;
        bool _subscribed;
        bool _built;
        bool _synced;
        bool _answered;
        bool _reduced;
        int _choice = -1;
        int _look = -1;
        float _ease;
        float _appTime;

        GameObject _offer;
        Material _inkMat;
        Mesh _quad;
        readonly GameObject[] _marks = new GameObject[SymbolCount];
        readonly Texture2D[] _markTex = new Texture2D[SymbolCount];
        readonly Vector3[] _home = new Vector3[SymbolCount];
        readonly float[] _yaw = new float[SymbolCount];
        readonly List<string> _cues = new List<string>();

        public bool Answered { get { return _answered; } }
        public int Choice { get { return _choice; } }
        public float Ease { get { return _ease; } }
        public bool Settled { get { return _answered && _ease >= 0.99f; } }
        public float AppTime { get { return _appTime; } }
        public bool OfferVisible { get { return _offer != null && _offer.activeSelf; } }
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

        public bool SymbolShown(int index)
        {
            if (index < 0 || index >= _marks.Length || _marks[index] == null) return false;
            return _marks[index].activeInHierarchy;
        }

        public static string SymbolId(int index)
        {
            if (index < 0 || index >= SymbolCount) return "";
            return "symbol." + GratitudeRecord.Keys[index];
        }

        public static int ParseSymbol(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            const string prefix = "symbol.";
            if (!id.StartsWith(prefix, System.StringComparison.Ordinal)) return -1;
            return GratitudeRecord.ParseKey(id.Substring(prefix.Length));
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
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            BuildMarks();
            if (_source == null && _sundial != null && _sundial.Source != null)
                SetSource(_sundial.Source);
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
            if (_inkMat != null) Destroy(_inkMat);
            if (_quad != null) Destroy(_quad);
            for (int i = 0; i < _marks.Length; i++)
            {
                if (_marks[i] == null) continue;
                Renderer glyph = _marks[i].GetComponentInChildren<Renderer>();
                if (glyph != null && glyph.sharedMaterial != null) Destroy(glyph.sharedMaterial);
            }
            for (int i = 0; i < _markTex.Length; i++)
            {
                if (_markTex[i] != null) Destroy(_markTex[i]);
            }
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            if (_sundial != null && _sundial.Source != _source)
                SetSource(_sundial.Source);
            if (!_built) BuildMarks();

            float dt = ClampStep(Time.deltaTime);
            _appTime += dt;
            _reduced = _sundial != null && _sundial.Service != null && _sundial.Service.ReducedMotion;
            SyncFromRecord();
            EaseInk(dt);
            RefreshOffer();
            RefreshMarks();
            PlaceOffer();
        }

        void OnIntent(HandIntent intent)
        {
            if (_sundial != null && _sundial.Service != null && _sundial.Service.Scrubbing
                && intent.Kind != HandIntentKind.Look)
                return;
            if (intent.Kind == HandIntentKind.Look)
            {
                _look = ParseSymbol(intent.TargetId);
                return;
            }
            if (intent.Kind != HandIntentKind.Pinch) return;
            int symbol = ParseSymbol(intent.TargetId);
            if (symbol < 0) symbol = _look;
            if (symbol < 0 || !CanAccept()) return;
            if (_sundial == null || _sundial.Service == null) return;
            TendResult result = _sundial.Service.InkGratitude(symbol);
            if (result == null || !result.Ok) return;
            _choice = symbol;
            _answered = true;
            _ease = _reduced ? 1f : 0f;
            PaintOne(symbol, true);
            if (!result.AlreadyKept)
                PlayCue(CueInk);
        }

        bool CanAccept()
        {
            if (_answered) return false;
            if (_sundial != null && _sundial.Dismissed) return false;
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk != null && dusk.Active) return false;
            return CueDue();
        }

        void SyncFromRecord()
        {
            if (_synced) return;
            if (_sundial == null || _sundial.Service == null || _sundial.Service.Gratitude == null) return;
            _synced = true;
            if (_answered) return;
            int symbol = _sundial.Service.Gratitude.SymbolOn(_sundial.Service.Today().Index);
            if (symbol < 0) return;
            _choice = symbol;
            _answered = true;
            _ease = 1f;
            PaintOne(symbol, true);
        }

        void EaseInk(float dt)
        {
            if (!_answered) return;
            if (_reduced)
            {
                _ease = 1f;
                return;
            }
            _ease = Mathf.MoveTowards(_ease, 1f, dt / EaseSeconds);
        }

        bool CueDue()
        {
            if (_answered) return false;
            if (KeptToday()) return false;
            FirstRunWizard wizard = GetComponent<FirstRunWizard>();
            if (wizard != null && wizard.Running) return false;
            if (_sundial != null && _sundial.Dismissed) return false;
            HabitDef habit = MiddayHabit();
            if (habit == null || _sundial == null || _sundial.Service == null) return false;
            if (habit.CreatedDay == _sundial.Service.Today().Index) return true;
            return _sundial.State != null && _sundial.State.Arc == ArcId.Midday;
        }

        bool KeptToday()
        {
            HabitDef habit = MiddayHabit();
            if (habit == null || _sundial == null || _sundial.Service == null || _sundial.Ledger == null)
                return false;
            return _sundial.Ledger.IsKept(habit.Id, _sundial.Service.Today().Index);
        }

        HabitDef MiddayHabit()
        {
            if (_sundial == null || _sundial.Service == null) return null;
            return _sundial.Service.HabitForArc("midday");
        }

        Transform PlantTransform()
        {
            if (_sundial == null || _sundial.View == null) return null;
            return _sundial.View.transform.Find("plant.midday");
        }

        void PlaceOffer()
        {
            if (_offer == null || _sundial == null || _sundial.View == null) return;
            // Above the far rim, on the midday side, so the note does not cover the five marks.
            float radius = Mathf.Max(0.05f, _sundial.View.faceRadius) * 1.24f;
            const float deg = 96f;
            float rad = deg * Mathf.Deg2Rad;
            _offer.transform.SetParent(transform, false);
            _offer.transform.localPosition = new Vector3(
                Mathf.Cos(rad) * radius,
                _sundial.View.faceY + 0.028f,
                Mathf.Sin(rad) * radius);
            _offer.transform.localScale = Vector3.one;
            Camera cam = OfferCamera();
            if (cam == null) return;
            Vector3 to = cam.transform.position - _offer.transform.position;
            if (to.sqrMagnitude < 1e-6f) return;
            _offer.transform.rotation = Quaternion.LookRotation(to, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        }

        static Camera OfferCamera()
        {
            if (Camera.main != null) return Camera.main;
            GameObject eye = GameObject.Find("EyeCamera");
            return eye == null ? null : eye.GetComponent<Camera>();
        }

        void RefreshOffer()
        {
            if (_offer == null) return;
            bool show = CueDue();
            if (_offer.activeSelf != show) _offer.SetActive(show);
        }

        void RefreshMarks()
        {
            bool row = CueDue();
            for (int i = 0; i < _marks.Length; i++)
            {
                if (_marks[i] == null) continue;
                bool show = _answered ? i == _choice : row;
                if (_marks[i].activeSelf != show) _marks[i].SetActive(show);
                if (!show) continue;
                Transform glyph = _marks[i].transform.GetChild(0);
                if (_answered && i == _choice)
                {
                    // The pen goes over the chosen mark and it settles into the wash.
                    float u = _ease;
                    float scale = Mathf.Lerp(1f, InkScale, u);
                    float pull = Mathf.Lerp(1f, 0.78f, u);
                    Vector3 home = _home[i];
                    _marks[i].transform.localPosition = new Vector3(home.x * pull, home.y, home.z * pull);
                    glyph.localScale = new Vector3(MarkSize * scale, MarkSize * scale, 1f);
                    glyph.localRotation = Lay(_yaw[i]);
                }
                else
                {
                    _marks[i].transform.localPosition = _home[i];
                    float emphasis = _look == i ? 1.14f : 1f;
                    glyph.localScale = new Vector3(MarkSize * emphasis, MarkSize * emphasis, 1f);
                    glyph.localRotation = Lay(_yaw[i]);
                }
            }
        }

        void BuildMarks()
        {
            if (_built) return;
            if (_sundial == null || _sundial.View == null) return;
            _built = true;
            _quad = CenterQuad();
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader != null)
            {
                _inkMat = new Material(shader) { name = "GratitudeInk" };
                _inkMat.SetFloat("_Src", 5f);
                _inkMat.SetFloat("_Dst", 10f);
                _inkMat.SetFloat("_Ring", 0f);
                _inkMat.SetFloat("_Coverage", 0f);
                _inkMat.SetFloat("_ZWrite", 0f);
                _inkMat.SetFloat("_Sparkle", 0f);
                _inkMat.SetColor("_Color", Color.white);
                _inkMat.SetColor("_Color2", Color.clear);
            }

            float radius = Mathf.Max(0.05f, _sundial.View.faceRadius) * ArcRadius;
            float y = _sundial.View.faceY + 0.013f;
            float span = Mathf.Repeat(DialView.MiddayArc0 - DialView.MiddayArc1, 360f);
            for (int i = 0; i < SymbolCount; i++)
            {
                float t = (i + 0.5f) / SymbolCount;
                float deg = DialView.MiddayArc0 - span * t;
                float rad = deg * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
                _home[i] = pos;
                _yaw[i] = YawAt(deg);
                _markTex[i] = GratitudeInk.Make(i, false, TexSize);
                var go = new GameObject(SymbolId(i));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.identity;
                var glyph = new GameObject("glyph");
                glyph.transform.SetParent(go.transform, false);
                glyph.transform.localPosition = new Vector3(0f, 0.002f, 0f);
                glyph.transform.localRotation = Lay(_yaw[i]);
                glyph.transform.localScale = new Vector3(MarkSize, MarkSize, 1f);
                glyph.AddComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = glyph.AddComponent<MeshRenderer>();
                if (_inkMat != null)
                {
                    Material mat = new Material(_inkMat) { name = "GratitudeInk" + i };
                    mat.SetTexture("_MainTex", _markTex[i]);
                    renderer.sharedMaterial = mat;
                }
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetTexture("_MainTex", _markTex[i]);
                renderer.SetPropertyBlock(block);
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.01f, 0f);
                box.size = new Vector3(0.026f, 0.02f, 0.026f);
                var target = go.AddComponent<IntentTarget>();
                target.Id = SymbolId(i);
                go.SetActive(false);
                _marks[i] = go;
            }

            _offer = new GameObject("GratitudePrompt");
            _offer.transform.SetParent(transform, false);
            TextMesh mesh = _offer.AddComponent<TextMesh>();
            InkLetter.Apply(mesh, OfferLine, 48, 0.0024f);
            InkLetter.AttachNote(_offer.transform, new Vector2(0.155f, 0.034f));
            _offer.SetActive(false);
        }

        void PaintOne(int index, bool chosen)
        {
            if (index < 0 || index >= _markTex.Length) return;
            if (_markTex[index] == null) return;
            GratitudeInk.Paint(_markTex[index], index, chosen);
        }

        /// <summary>
        /// Quad faces local +Y. Glyph up follows the radius, so the drawing sits in the page
        /// with its top toward the rim.
        /// </summary>
        static float YawAt(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return Mathf.Atan2(-Mathf.Cos(rad), -Mathf.Sin(rad)) * Mathf.Rad2Deg;
        }

        static Quaternion Lay(float yaw)
        {
            return Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
        }

        static Mesh CenterQuad()
        {
            var mesh = new Mesh { name = "GratitudeQuad" };
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
        }

        void Subscribe()
        {
            if (_source == null || _subscribed) return;
            _source.Intent += OnIntent;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_source != null) _source.Intent -= OnIntent;
            _subscribed = false;
        }

        static float ClampStep(float dt)
        {
            if (dt < 0f) return 0f;
            if (dt > 0.1f) return 0.1f;
            return dt;
        }
    }
}

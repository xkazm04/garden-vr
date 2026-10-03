using System.Collections.Generic;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Pinch the gnomon to open a 25-minute shadow hour. The gnomon's shadow is drawn across the dial in ink
    /// as the clock advances. Pinch again to end early. That still counts, and it never reads as a failure.
    /// The hour is a ritual tend of the arc it started in. Esc or a system pause holds it. Coming back continues it.
    /// Leaving the window without a pause lets the real clock keep drawing, so the hour can sit behind a work session.
    /// App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(85)]
    public sealed class FocusBlockController : MonoBehaviour
    {
        public const string GnomonId = "gnomon";
        public const string OfferLine = "A quiet hour?";
        public const string CueBegin = SundialController.CueInk;
        public const string CueDone = SundialController.CueFlutter;
        public const float TwoFootMetres = 0.61f;

        SundialController _sundial;
        IHandIntentSource _source;
        bool _subscribed;
        bool _built;
        bool _wasRunning;
        bool _doneCue;
        float _diameter = 0.3f;
        float _paintKey = float.MinValue;
        int _inkPixels;

        GameObject _gnomon;
        GameObject _offer;
        TextMesh _offerMesh;
        GameObject _ink;
        MeshRenderer _inkRenderer;
        Texture2D _inkTex;
        Color32[] _inkRaw;
        Material _inkMat;
        readonly List<string> _cues = new List<string>();

        public FocusBlock Block { get { return _sundial == null || _sundial.Service == null ? null : _sundial.Service.Focus; } }
        public bool Running { get { return Phase == FocusPhase.Running; } }
        public bool Paused { get { return Phase == FocusPhase.Paused; } }
        public bool Complete { get { return Phase == FocusPhase.Complete; } }
        public bool Counted { get { FocusBlock block = Block; return block != null && block.Counted; } }
        public bool EndedEarly { get { FocusBlock block = Block; return block != null && block.EndedEarly; } }
        public float Progress { get { FocusBlock block = Block; return block == null ? 0f : block.Progress; } }
        public float DrawnDegrees { get; private set; }
        public int InkPixels { get { return _inkPixels; } }
        public bool OfferVisible { get { return _offer != null && _offer.activeSelf; } }
        public IReadOnlyList<string> Cues { get { return _cues; } }

        FocusPhase Phase
        {
            get
            {
                FocusBlock block = Block;
                return block == null ? FocusPhase.Idle : block.Phase;
            }
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

        /// <summary>Alpha of the ink texture at a point on the dial, in the dial's local XZ.</summary>
        public float SampleDial(float x, float z)
        {
            if (_inkRaw == null || _diameter < 0.01f) return 0f;
            float u = x / _diameter + 0.5f;
            float v = -z / _diameter + 0.5f;
            int n = _inkTex != null ? _inkTex.width : 192;
            int px = Mathf.Clamp(Mathf.RoundToInt(u * (n - 1)), 0, n - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(v * (n - 1)), 0, n - 1);
            return _inkRaw[py * n + px].a / 255f;
        }

        public void SetSource(IHandIntentSource source)
        {
            if (_source == source) return;
            Unsubscribe();
            _source = source;
            if (isActiveAndEnabled) Subscribe();
        }

        /// <summary>Esc or a system pause. A running hour holds. A held hour continues. Nothing is lost.</summary>
        public void NotifyFocusLost()
        {
            if (_sundial == null || _sundial.Service == null) return;
            if (Phase == FocusPhase.Running) _sundial.Service.PauseFocus();
            else if (Phase == FocusPhase.Paused) _sundial.Service.ResumeFocus();
        }

        public void RefreshInk()
        {
            _paintKey = float.MinValue;
            Paint();
        }

        void Awake()
        {
            _sundial = GetComponent<SundialController>();
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            Build();
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

        void OnApplicationPause(bool paused)
        {
            if (_sundial == null || _sundial.Service == null) return;
            if (paused) _sundial.Service.PauseFocus();
            else _sundial.Service.ResumeFocus();
        }

        void OnDestroy()
        {
            if (_inkTex != null) Destroy(_inkTex);
            if (_inkMat != null) Destroy(_inkMat);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            if (_sundial != null && _sundial.Source != _source)
                SetSource(_sundial.Source);
            if (!_built) Build();

            bool running = Phase == FocusPhase.Running;
            bool complete = Phase == FocusPhase.Complete;
            if (_wasRunning && complete) PlayDone();
            _wasRunning = running;
            RefreshOffer();
            PlaceOffer();
        }

        void LateUpdate()
        {
            Paint();
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind != HandIntentKind.Pinch) return;
            if (intent.TargetId != GnomonId) return;
            if (Blocked()) return;
            if (_sundial == null || _sundial.Service == null) return;

            if (Phase == FocusPhase.Running || Phase == FocusPhase.Paused)
            {
                if (!_sundial.Service.TryEndFocus()) return;
                _wasRunning = false;
                PlayDone();
                _paintKey = float.MinValue;
                return;
            }

            if (!_sundial.Service.TryStartFocus()) return;
            _doneCue = false;
            _wasRunning = true;
            _paintKey = float.MinValue;
            PlayCue(CueBegin);
        }

        bool Blocked()
        {
            if (_sundial != null && _sundial.Dismissed) return true;
            FirstRunWizard wizard = GetComponent<FirstRunWizard>();
            if (wizard != null && wizard.Running) return true;
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk != null && dusk.Active) return true;
            return false;
        }

        void RefreshOffer()
        {
            if (_offer == null) return;
            bool show = !Blocked() && Phase != FocusPhase.Running && Phase != FocusPhase.Paused;
            if (_offer.activeSelf != show) _offer.SetActive(show);
        }

        void PlaceOffer()
        {
            if (_offer == null || !_offer.activeSelf) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 to = cam.transform.position - _offer.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-8f) return;
            _offer.transform.rotation = Quaternion.LookRotation(to, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        }

        void Build()
        {
            if (_built || _sundial == null || _sundial.View == null) return;
            _built = true;
            float radius = _sundial.View.faceRadius;
            if (radius < 0.05f) radius = 0.1472f;
            _diameter = radius * 2f;
            float y = _sundial.View.faceY + 0.014f;

            Transform pen = FindDeep(transform, "Gnomon");
            Vector3 penLocal = pen == null ? Vector3.zero : transform.InverseTransformPoint(pen.position);
            _gnomon = new GameObject(GnomonId);
            _gnomon.transform.SetParent(transform, false);
            _gnomon.transform.localPosition = penLocal;
            _gnomon.transform.localRotation = Quaternion.identity;
            _gnomon.transform.localScale = Vector3.one;
            var box = _gnomon.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.035f, 0f);
            box.size = new Vector3(0.046f, 0.07f, 0.046f);
            var target = _gnomon.AddComponent<IntentTarget>();
            target.Id = GnomonId;

            _inkTex = new Texture2D(192, 192, TextureFormat.RGBA32, false);
            _inkTex.name = "FocusInk";
            _inkTex.wrapMode = TextureWrapMode.Clamp;
            _inkTex.filterMode = FilterMode.Bilinear;
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader != null)
            {
                _inkMat = new Material(shader) { name = "FocusInk" };
                _inkMat.SetFloat("_Src", 5f);
                _inkMat.SetFloat("_Dst", 10f);
                _inkMat.SetFloat("_Ring", 0f);
                _inkMat.SetFloat("_Coverage", 0f);
                _inkMat.SetFloat("_ZWrite", 0f);
                _inkMat.SetFloat("_Sparkle", 0f);
                _inkMat.SetColor("_Color", Color.white);
                _inkMat.SetColor("_Color2", Color.clear);
                _inkMat.SetTexture("_MainTex", _inkTex);
                _inkMat.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                // The shadow wash sets this explicitly. A queue in the opaque range is dropped by the depth prepass.
                _inkMat.renderQueue = 3100;
            }

            _ink = new GameObject("FocusInk");
            _ink.transform.SetParent(transform, false);
            _ink.transform.localPosition = new Vector3(0f, y, 0f);
            _ink.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _ink.transform.localScale = new Vector3(_diameter, _diameter, 1f);
            _ink.AddComponent<MeshFilter>().sharedMesh = Quad();
            _inkRenderer = _ink.AddComponent<MeshRenderer>();
            if (_inkMat != null) _inkRenderer.sharedMaterial = _inkMat;
            _inkRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _inkRenderer.receiveShadows = false;
            _inkRenderer.enabled = false;

            _offer = new GameObject("FocusPrompt");
            _offer.transform.SetParent(transform, false);
            _offer.transform.localPosition = new Vector3(0f, y + 0.012f, -radius * 0.42f);
            _offerMesh = _offer.AddComponent<TextMesh>();
            InkLetter.Apply(_offerMesh, OfferLine, 48, 0.0031f);
            InkLetter.AttachNote(_offer.transform, new Vector2(0.16f, 0.038f));
            _offer.SetActive(false);
        }

        void Paint()
        {
            if (!_built || _inkTex == null) return;
            FocusBlock block = Block;
            float span = 0f;
            float start = 0f;
            if (block != null && block.Phase != FocusPhase.Idle)
            {
                start = block.StartGnomonDeg;
                span = block.Progress * FocusBlock.SweepDegrees;
                if (block.Phase == FocusPhase.Running && span < 2f) span = 2f;
            }
            DrawnDegrees = span;
            float key = start * 10f + span;
            if (Mathf.Abs(key - _paintKey) < 0.05f) return;
            _paintKey = key;
            Fill(start, span);
            if (_inkRenderer != null) _inkRenderer.enabled = _inkPixels > 8;
        }

        void Fill(float startDeg, float spanDeg)
        {
            int n = _inkTex.width;
            var pixels = new Color32[n * n];
            _inkPixels = 0;
            if (spanDeg > 0.05f)
            {
                float diam = _diameter;
                for (int py = 0; py < n; py++)
                {
                    for (int px = 0; px < n; px++)
                    {
                        float u = px / (float)(n - 1);
                        float v = py / (float)(n - 1);
                        float x = (u - 0.5f) * diam;
                        float z = -(v - 0.5f) * diam;
                        float reach = Mathf.Sqrt(x * x + z * z);
                        if (reach < 0.004f || reach > 0.125f) continue;
                        float pointDeg = Mathf.Atan2(-z, x) * Mathf.Rad2Deg;
                        if (pointDeg < 0f) pointDeg += 360f;
                        float along = Mathf.Repeat(pointDeg - startDeg, 360f);
                        const float slack = 26f;
                        float sampleAlong = along;
                        if (along > spanDeg)
                        {
                            float past = along - spanDeg;
                            float before = 360f - along;
                            if (past <= slack && past <= before) sampleAlong = spanDeg;
                            else if (before <= slack) sampleAlong = 0f;
                            else continue;
                        }
                        float sampleDeg = startDeg + sampleAlong;
                        float rad = sampleDeg * Mathf.Deg2Rad;
                        float fx = Mathf.Cos(rad);
                        float fz = -Mathf.Sin(rad);
                        float rx = -Mathf.Sin(rad);
                        float rz = -Mathf.Cos(rad);
                        float localZ = x * fx + z * fz;
                        float localX = x * rx + z * rz;
                        if (localZ < 0.004f || localZ > 0.116f) continue;
                        float t = Mathf.InverseLerp(0.006f, 0.108f, localZ);
                        float wobble = 0.0035f * Mathf.Sin(sampleDeg * 0.17f + t * 8f);
                        float half = Mathf.Lerp(0.011f, 0.050f, Mathf.Clamp01(t)) + wobble;
                        float lateral = Mathf.Abs(localX);
                        if (lateral > half) continue;
                        float edge = 1f - Mathf.Clamp01((lateral - half * 0.32f) / (half * 0.72f));
                        // Mathf.SmoothStep lerps its first two arguments. This is the edge fade, held until the tip.
                        float fadeIn = Mathf.Clamp01((t - 0.76f) / 0.30f);
                        fadeIn = fadeIn * fadeIn * (3f - 2f * fadeIn);
                        float body = Mathf.Clamp01(t * 1.35f) * (1f - fadeIn);
                        float lead = spanDeg <= 0.01f ? 1f : Mathf.Lerp(0.34f, 1f, Mathf.Clamp01(sampleAlong / spanDeg));
                        float grain = 0.84f + 0.16f * Mathf.Abs(Mathf.Sin(x * 190f + z * 130f));
                        float alpha = edge * body * lead * grain;
                        if (alpha < 0.03f) continue;
                        if (Mathf.Sin(pointDeg * 7.3f + t * 16f) > 0.965f) alpha *= 0.42f;
                        float a = Mathf.Clamp01(alpha) * 0.92f;
                        pixels[py * n + px] = new Color32(
                            (byte)Mathf.RoundToInt(0.29f * 255f),
                            (byte)Mathf.RoundToInt(0.19f * 255f),
                            (byte)Mathf.RoundToInt(0.13f * 255f),
                            (byte)Mathf.RoundToInt(a * 255f));
                        _inkPixels++;
                    }
                }
            }
            _inkRaw = pixels;
            _inkTex.SetPixels32(pixels);
            _inkTex.Apply(false, false);
            if (_inkRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                _inkRenderer.GetPropertyBlock(block);
                block.SetTexture("_MainTex", _inkTex);
                _inkRenderer.SetPropertyBlock(block);
            }
        }

        void PlayDone()
        {
            if (_doneCue) return;
            _doneCue = true;
            PlayCue(CueDone);
        }

        void PlayCue(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _cues.Add(id);
            if (_sundial != null) _sundial.Play(id, _gnomon == null ? transform : _gnomon.transform);
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

        static Mesh Quad()
        {
            var mesh = new Mesh { name = "FocusInkQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.colors = new[]
            {
                Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}

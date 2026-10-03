using System.Collections.Generic;
using System.Globalization;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Morning stretch-reach. Three ink marks on the near rim of the dial, inside a two-foot reach.
    /// Look at the next mark and hold the palm (the provider commits PalmOpen at 0.6 s).
    /// The morning plant stretches with each reach and does not shrink back while the ritual is open.
    /// The third reach tends the morning habit at once, source Ritual, with no undo window.
    /// One existing cue, bloom.flutter. No new audio.
    /// App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class StretchRitualController : MonoBehaviour
    {
        public const int MarkCount = StretchSession.MarkCount;
        public const string OfferLine = "Reach to three marks?";
        public const string CueStretch = "bloom.flutter";
        public const float TwoFootMetres = 0.61f;
        const float MarkRadius = 0.78f;
        const float MarkSize = 0.046f;
        const float EaseSeconds = 0.55f;
        const float HoldSeconds = 1.4f;
        const float SettleSeconds = 0.7f;
        const float HeightGain = 0.42f;
        const float LeanDegrees = 16f;
        const float LeanMetres = 0.012f;

        // Near rim, dial degrees. 90 is the far side, so these three sit toward the seated user.
        static readonly float[] MarkDeg = { 215f, 270f, 325f };

        static readonly Color Ink = new Color(0.165f, 0.149f, 0.133f, 1f);

        SundialController _sundial;
        StretchSession _session;
        IHandIntentSource _source;
        bool _subscribed;
        bool _built;
        bool _answered;
        bool _reduced;
        bool _homeReady;
        bool _scaleReady;
        bool _cameraHooked;
        int _lookMark = -1;
        int _paintKey = int.MinValue;
        float _pose;
        float _hold;
        float _appTime;
        Vector3 _home;
        Vector3 _baseScale;

        GameObject _offer;
        TextMesh _offerMesh;
        Material _inkMat;
        Mesh _quad;
        readonly GameObject[] _marks = new GameObject[MarkCount];
        readonly Texture2D[] _markTex = new Texture2D[MarkCount];
        readonly List<string> _cues = new List<string>();

        public StretchSession Session { get { return _session; } }
        public bool Answered { get { return _answered; } }
        public int Reaches { get { return _session == null ? 0 : _session.Reaches; } }
        public float Pose { get { return _pose; } }
        public float PlantHeight { get { return 1f + HeightGain * _pose; } }
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

        public static int ParseMark(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            const string prefix = "mark.";
            if (!id.StartsWith(prefix, System.StringComparison.Ordinal)) return -1;
            int n;
            if (!int.TryParse(id.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return -1;
            if (n < 0 || n >= MarkCount) return -1;
            return n;
        }

        public static string MarkId(int index)
        {
            return "mark." + index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// True when this palm is aimed at an ink mark and the ritual can take it.
        /// The caller should not treat that palm as dismiss or as a dusk pause.
        /// </summary>
        public bool ClaimsPalm(HandIntent intent)
        {
            if (intent.Kind != HandIntentKind.PalmOpen) return false;
            if (!CanAccept()) return false;
            return ResolveMark(intent) >= 0;
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
            HookCamera();
        }

        void OnEnable()
        {
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
            UnhookCamera();
        }

        void OnDestroy()
        {
            UnhookCamera();
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
            if (_session == null && CueDue()) _session = new StretchSession();
            if (_session != null && !_answered)
                _session.Update(dt, new StretchSample(-1, false));

            EasePose(dt);
            TryComplete();
            RefreshOffer();
            RefreshMarks();
            ApplyPlantPose();
            PlaceOffer();
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.Look)
            {
                _lookMark = ParseMark(intent.TargetId);
                return;
            }
            if (intent.Kind != HandIntentKind.PalmOpen) return;
            if (!ClaimsPalm(intent)) return;
            if (_session == null) _session = new StretchSession();
            int mark = ResolveMark(intent);
            _session.Update(0f, new StretchSample(mark, true));
            TryComplete();
        }

        bool CanAccept()
        {
            if (_answered) return false;
            if (_sundial != null && _sundial.Dismissed) return false;
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk != null && dusk.Active) return false;
            if (_session != null && _session.Phase == StretchPhase.Complete) return false;
            if (_session != null && _session.Reaches > 0) return true;
            return CueDue();
        }

        int ResolveMark(HandIntent intent)
        {
            int fromIntent = ParseMark(intent.TargetId);
            if (fromIntent >= 0) return fromIntent;
            return _lookMark;
        }

        void EasePose(float dt)
        {
            float earned = _session == null ? 0f : _session.Stretch;
            if (_answered)
            {
                // Catch the earned stretch once. After _hold starts, the pose is allowed to fall.
                // Climbing back to earned on the next frame cancels the settle.
                if (_hold <= 0f && _pose < earned - 0.001f)
                {
                    _pose = _reduced ? earned : Mathf.MoveTowards(_pose, earned, Step(dt));
                    if (_pose < earned - 0.001f) return;
                }
                _hold += dt;
                if (_reduced || _hold > HoldSeconds)
                    _pose = Mathf.MoveTowards(_pose, 0f, _reduced ? 1f : dt / SettleSeconds);
                return;
            }

            float target = earned;
            if (!_reduced && _session != null && _session.Phase != StretchPhase.Complete && _lookMark == _session.NextMark)
            {
                float share = 1f / MarkCount;
                target = Mathf.Clamp01(earned + share * 0.55f);
            }
            if (_reduced)
            {
                _pose = earned;
                return;
            }
            if (target > _pose)
                _pose = Mathf.MoveTowards(_pose, target, Step(dt));
            if (_pose < earned)
                _pose = earned;
        }

        static float Step(float dt)
        {
            return dt / EaseSeconds;
        }

        void TryComplete()
        {
            if (_answered || _session == null || !_session.TendAuthorised) return;
            HabitDef habit = MorningHabit();
            if (habit == null || _sundial == null || _sundial.Service == null) return;
            TendResult result = _sundial.Service.TendRitual(habit.Id);
            if (result == null || !result.Ok) return;
            _session.ConsumeTend();
            _answered = true;
            _hold = 0f;
            if (!result.AlreadyKept)
                PlayCue(CueStretch);
        }

        bool CueDue()
        {
            if (KeptToday()) return false;
            FirstRunWizard wizard = GetComponent<FirstRunWizard>();
            if (wizard != null && wizard.Running) return false;
            HabitDef habit = MorningHabit();
            if (habit == null || _sundial == null || _sundial.Service == null) return false;
            if (habit.CreatedDay == _sundial.Service.Today().Index) return true;
            return _sundial.State != null && _sundial.State.Arc == ArcId.Morning;
        }

        bool KeptToday()
        {
            HabitDef habit = MorningHabit();
            if (habit == null || _sundial == null || _sundial.Service == null || _sundial.Ledger == null)
                return false;
            return _sundial.Ledger.IsKept(habit.Id, _sundial.Service.Today().Index);
        }

        HabitDef MorningHabit()
        {
            if (_sundial == null || _sundial.Service == null) return null;
            return _sundial.Service.HabitForArc("morning");
        }

        Transform PlantTransform()
        {
            if (_sundial == null || _sundial.View == null) return null;
            return _sundial.View.transform.Find("plant.morning");
        }

        void ApplyPlantPose()
        {
            Transform plant = PlantTransform();
            if (plant == null) return;
            if (!_homeReady)
            {
                _home = plant.localPosition;
                _homeReady = true;
            }
            if (!_scaleReady)
            {
                _baseScale = plant.localScale;
                _scaleReady = true;
            }
            float narrow = 1f - 0.06f * _pose;
            float tall = PlantHeight;
            plant.localScale = new Vector3(_baseScale.x * narrow, _baseScale.y * tall, _baseScale.z);
            plant.localPosition = _home + LeanOffset();
            Transform bloom = BloomTransform();
            if (bloom == null) return;
            bloom.localScale = plant.localScale;
            bloom.localPosition = plant.localPosition + new Vector3(0f, 0.0015f, 0f);
        }

        Vector3 LeanOffset()
        {
            if (_pose <= 0.001f) return Vector3.zero;
            Transform mark = ActiveMark();
            if (mark == null) return Vector3.zero;
            Vector3 dir = mark.localPosition - _home;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-8f) return Vector3.zero;
            return dir.normalized * (LeanMetres * _pose);
        }

        Transform ActiveMark()
        {
            int index = _lookMark;
            if (index < 0 && _session != null) index = _session.NextMark;
            if (index < 0 || index >= _marks.Length) return null;
            return _marks[index] == null ? null : _marks[index].transform;
        }

        Transform BloomTransform()
        {
            if (_sundial == null || _sundial.View == null || _sundial.View.bloomRenderers == null) return null;
            if (_sundial.View.bloomRenderers.Length < 1 || _sundial.View.bloomRenderers[0] == null) return null;
            return _sundial.View.bloomRenderers[0].transform;
        }

        void PlaceOffer()
        {
            Transform plant = PlantTransform();
            if (_offer == null || plant == null) return;
            float sx = Mathf.Max(0.0001f, plant.localScale.x);
            float sy = Mathf.Max(0.0001f, plant.localScale.y);
            _offer.transform.SetParent(plant, false);
            _offer.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            // Local +X of a camera-facing card is screen-left. Negative sits toward the dial.
            _offer.transform.localPosition = new Vector3(-0.09f / sx, 0.42f, 0.05f);
            _offer.transform.localScale = new Vector3(1f / sx, 1f / sy, 1f / sy);
        }

        void RefreshOffer()
        {
            if (_offer == null) return;
            bool show = CueDue() && !_answered && (_session == null || _session.Reaches == 0);
            if (_offer.activeSelf != show) _offer.SetActive(show);
        }

        void RefreshMarks()
        {
            bool show = CueDue() || (_session != null && _session.Reaches > 0 && _pose > 0.02f) || (_answered && _hold < HoldSeconds + SettleSeconds);
            int key = (_session == null ? 0 : _session.Reaches) * 8 + (_lookMark + 1);
            bool repaint = key != _paintKey;
            for (int i = 0; i < _marks.Length; i++)
            {
                if (_marks[i] == null) continue;
                if (_marks[i].activeSelf != show) _marks[i].SetActive(show);
                if (!show) continue;
                bool next = _session == null || _session.Phase != StretchPhase.Complete
                    ? (_session == null ? i == 0 : i == _session.NextMark)
                    : false;
                float emphasis = next && _lookMark == i ? 1.18f : next ? 1.06f : 1f;
                _marks[i].transform.localScale = new Vector3(emphasis, 1f, emphasis);
                if (repaint) PaintMark(i, _session != null && i < _session.Reaches, next);
            }
            if (repaint) _paintKey = key;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (!Application.isPlaying || cam == null || cam.cameraType != CameraType.Game) return;
            if (_reduced || _pose <= 0.001f) return;
            Transform plant = PlantTransform();
            Transform mark = ActiveMark();
            if (plant == null || mark == null) return;
            Vector3 to = mark.position - plant.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-8f) return;
            Vector3 axis = Vector3.Cross(Vector3.up, to.normalized);
            if (axis.sqrMagnitude < 1e-8f) return;
            plant.rotation = Quaternion.AngleAxis(LeanDegrees * _pose, axis) * plant.rotation;
            Transform bloom = BloomTransform();
            if (bloom != null) bloom.rotation = plant.rotation;
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
                _inkMat = new Material(shader) { name = "StretchInk" };
                _inkMat.SetFloat("_Src", 5f);
                _inkMat.SetFloat("_Dst", 10f);
                _inkMat.SetFloat("_Ring", 0f);
                _inkMat.SetFloat("_Coverage", 0f);
                _inkMat.SetFloat("_ZWrite", 0f);
                _inkMat.SetFloat("_Sparkle", 0f);
                _inkMat.SetColor("_Color", Color.white);
                _inkMat.SetColor("_Color2", Color.clear);
            }

            float radius = Mathf.Max(0.05f, _sundial.View.faceRadius) * MarkRadius;
            float y = _sundial.View.faceY + 0.012f;
            for (int i = 0; i < MarkCount; i++)
            {
                float rad = MarkDeg[i] * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
                _markTex[i] = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                _markTex[i].name = "StretchMark" + i;
                _markTex[i].wrapMode = TextureWrapMode.Clamp;
                _markTex[i].filterMode = FilterMode.Bilinear;
                PaintMark(i, false, i == 0);
                var go = new GameObject(MarkId(i));
                go.transform.SetParent(transform, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.identity;
                var glyph = new GameObject("glyph");
                glyph.transform.SetParent(go.transform, false);
                glyph.transform.localPosition = new Vector3(0f, 0.002f, 0f);
                // Quad is authored in XY. -90 X lays it on the dial, facing local +Y.
                glyph.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                glyph.transform.localScale = new Vector3(MarkSize, MarkSize, 1f);
                glyph.AddComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = glyph.AddComponent<MeshRenderer>();
                if (_inkMat != null)
                {
                    Material mat = new Material(_inkMat) { name = "StretchInk" + i };
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
                box.size = new Vector3(0.046f, 0.02f, 0.046f);
                var target = go.AddComponent<IntentTarget>();
                target.Id = MarkId(i);
                go.SetActive(false);
                _marks[i] = go;
            }

            _offer = new GameObject("StretchPrompt");
            _offer.transform.SetParent(transform, false);
            _offerMesh = _offer.AddComponent<TextMesh>();
            InkLetter.Apply(_offerMesh, OfferLine, 48, 0.0031f);
            InkLetter.AttachNote(_offer.transform, new Vector2(0.20f, 0.040f));
            _offer.SetActive(false);
        }

        void PaintMark(int index, bool reached, bool next)
        {
            Texture2D tex = _markTex[index];
            if (tex == null) return;
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
                    float wobble = 0.03f * Mathf.Sin(ang * 5f + index) + 0.02f * Mathf.Sin(ang * 3f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float arm = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy));
                    float cross = dist < 0.78f + wobble ? Mathf.Clamp01(1f - arm / (0.18f + wobble * 0.2f)) : 0f;
                    if (Mathf.Sin(ang * 7f + index) > 0.94f) cross *= 0.55f;
                    float dot = reached ? Mathf.Clamp01(1f - dist / 0.36f) : 0f;
                    float ring = 0f;
                    if (next)
                    {
                        float band = Mathf.Abs(dist - (0.70f + wobble));
                        ring = Mathf.Clamp01(1f - band / 0.12f) * 0.9f;
                    }
                    float alpha = Mathf.Clamp01(Mathf.Max(cross, Mathf.Max(dot, ring)));
                    if (!reached && !next) alpha *= 0.55f;
                    pixels[y * n + x] = new Color(Ink.r, Ink.g, Ink.b, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
        }

        static Mesh CenterQuad()
        {
            var mesh = new Mesh { name = "StretchQuad" };
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

        void HookCamera()
        {
            if (_cameraHooked) return;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            _cameraHooked = true;
        }

        void UnhookCamera()
        {
            if (!_cameraHooked) return;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            _cameraHooked = false;
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

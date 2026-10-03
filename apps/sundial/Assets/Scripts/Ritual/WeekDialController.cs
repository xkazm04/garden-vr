using System.Collections.Generic;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Pinch-hold the page corner to turn onto the last four weeks, and hold it again to turn back.
    /// The page is a record in the same ink and wash. A miss stays pale. Nothing counts a run of days.
    /// App code never reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class WeekDialController : MonoBehaviour
    {
        public const string CornerId = "page.corner";
        public const string CaptionLine = WeekPageInk.Caption;
        public const float HoldSeconds = 0.50f;
        public const float TurnSeconds = 0.48f;
        public const float TwoFootMetres = 0.61f;
        public const string CueTurn = SundialController.CueAppear;

        static readonly Vector3 CornerLocal = new Vector3(0.102f, 0f, -0.092f);

        SundialController _sundial;
        IHandIntentSource _source;
        bool _subscribed;
        bool _built;
        bool _gate;
        float _turn;
        float _goal;
        float _diameter = 0.30f;
        string _paintKey;
        bool _covered;

        GameObject _hinge;
        GameObject _page;
        MeshRenderer _pageRenderer;
        Texture2D _pageTex;
        Color32[] _ink;
        Material _pageMat;
        Material _foldMat;
        GameObject _corner;
        GameObject _caption;
        TextMesh _captionMesh;
        readonly GameObject[] _names = new GameObject[3];
        readonly TextMesh[] _nameMesh = new TextMesh[3];
        readonly List<string> _cues = new List<string>();

        public bool OnWeekPage { get { return _goal > 0.5f && _turn > 0.98f; } }
        public bool OnDayPage { get { return _goal < 0.5f && _turn < 0.02f; } }
        public float Turn { get { return _turn; } }
        public string Caption { get { return _captionMesh == null ? "" : _captionMesh.text; } }
        public bool CornerVisible { get { return _corner != null && _corner.activeInHierarchy; } }
        public IReadOnlyList<string> Cues { get { return _cues; } }
        public Color32[] Ink { get { return _ink; } }
        public float Diameter { get { return _diameter; } }

        public WeekDial Record
        {
            get
            {
                SundialService service = Service;
                if (service == null || service.Ledger == null) return null;
                List<HabitDef> habits = service.Save == null ? null : service.Save.Habits;
                return WeekDial.Read(habits, service.Ledger, service.Today());
            }
        }

        SundialService Service { get { return _sundial == null ? null : _sundial.Service; } }

        public int CueCount(string id)
        {
            int count = 0;
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i] == id) count++;
            }
            return count;
        }

        public string PlantLabel(int arc)
        {
            if (arc < 0 || arc >= _nameMesh.Length || _nameMesh[arc] == null) return "";
            return _nameMesh[arc].text;
        }

        public bool PlantLabelVisible(int arc)
        {
            return arc >= 0 && arc < _names.Length && _names[arc] != null && _names[arc].activeInHierarchy;
        }

        public bool CaptionVisible { get { return _caption != null && _caption.activeInHierarchy; } }

        public Color SampleDial(float x, float z)
        {
            return WeekPageInk.Sample(_ink, _diameter, x, z);
        }

        public bool TryMark(string habitId, int week, int day, out float x, out float z)
        {
            x = 0f;
            z = 0f;
            SundialService service = Service;
            if (service == null || service.Save == null || service.Save.Habits == null) return false;
            for (int i = 0; i < service.Save.Habits.Count; i++)
            {
                HabitDef habit = service.Save.Habits[i];
                if (habit == null || habit.Id != habitId) continue;
                int arc = WeekPageInk.ArcOf(habit);
                if (WeekPageInk.FirstOfArc(service.Save.Habits, arc) != habit) return false;
                return WeekPageInk.TryMark(arc, week, day, out x, out z);
            }
            return false;
        }

        public void SetSource(IHandIntentSource source)
        {
            if (_source == source) return;
            Unsubscribe();
            _source = source;
            _gate = false;
            if (isActiveAndEnabled) Subscribe();
        }

        public void RefreshPage()
        {
            _paintKey = null;
            Paint();
        }

        public byte[] PagePng()
        {
            if (_pageTex == null || _ink == null) return new byte[0];
            _pageTex.SetPixels32(_ink);
            _pageTex.Apply(false, false);
            return _pageTex.EncodeToPNG();
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

        void OnDestroy()
        {
            if (_pageTex != null) Destroy(_pageTex);
            if (_pageMat != null) Destroy(_pageMat);
            if (_foldMat != null) Destroy(_foldMat);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            if (_sundial != null && _sundial.Source != _source)
                SetSource(_sundial.Source);
            if (!_built) Build();

            float dt = Time.deltaTime;
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            bool snap = Service != null && Service.ReducedMotion;
            if (snap || TurnSeconds <= 0.001f) _turn = _goal;
            else _turn = Mathf.MoveTowards(_turn, _goal, dt / TurnSeconds);
            Pose();
            if (_sundial != null && _sundial.View != null)
                _sundial.View.weekPage = _turn > 0.72f;
            Paint();
            CoverOthers(_turn > 0.72f);
            PlaceWords();
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.Release)
            {
                _gate = false;
                return;
            }
            if (intent.Kind != HandIntentKind.PinchHold) return;
            if (intent.TargetId != CornerId) return;
            if (_gate) return;
            if (intent.Held < HoldSeconds) return;
            if (Blocked()) return;
            _gate = true;
            _goal = _goal > 0.5f ? 0f : 1f;
            PlayCue(CueTurn);
        }

        bool Blocked()
        {
            if (_sundial != null && _sundial.Service != null && _sundial.Service.Scrubbing) return true;
            if (_sundial != null && _sundial.Dismissed) return true;
            FirstRunWizard wizard = GetComponent<FirstRunWizard>();
            if (wizard != null && wizard.Running) return true;
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk != null && dusk.Active) return true;
            return false;
        }

        void Build()
        {
            if (_built || _sundial == null || _sundial.View == null) return;
            _built = true;
            float radius = _sundial.View.faceRadius;
            if (radius < 0.05f) radius = 0.1472f;
            _diameter = radius * 2.04f;
            float y = _sundial.View.faceY + 0.012f;

            _hinge = new GameObject("WeekHinge");
            _hinge.transform.SetParent(transform, false);
            _hinge.transform.localPosition = new Vector3(CornerLocal.x, y, CornerLocal.z);
            _hinge.transform.localRotation = Quaternion.identity;
            _hinge.transform.localScale = Vector3.one;

            _pageTex = new Texture2D(WeekPageInk.Size, WeekPageInk.Size, TextureFormat.RGBA32, false);
            _pageTex.name = "WeekPage";
            _pageTex.wrapMode = TextureWrapMode.Clamp;
            _pageTex.filterMode = FilterMode.Bilinear;
            Shader shader = Shader.Find("Fidelity/Card");
            if (shader != null)
            {
                _pageMat = new Material(shader) { name = "WeekPage" };
                _pageMat.SetFloat("_Src", 5f);
                _pageMat.SetFloat("_Dst", 10f);
                _pageMat.SetFloat("_Ring", 0f);
                _pageMat.SetFloat("_Coverage", 0f);
                _pageMat.SetFloat("_ZWrite", 0f);
                _pageMat.SetFloat("_Sparkle", 0f);
                _pageMat.SetColor("_Color", Color.white);
                _pageMat.SetColor("_Color2", Color.clear);
                _pageMat.SetTexture("_MainTex", _pageTex);
                _pageMat.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                _pageMat.renderQueue = 3200;
            }

            _page = new GameObject("WeekPage");
            _page.transform.SetParent(_hinge.transform, false);
            _page.transform.localPosition = new Vector3(-CornerLocal.x, 0f, -CornerLocal.z);
            _page.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _page.transform.localScale = new Vector3(_diameter, _diameter, 1f);
            _page.AddComponent<MeshFilter>().sharedMesh = Quad();
            _pageRenderer = _page.AddComponent<MeshRenderer>();
            if (_pageMat != null) _pageRenderer.sharedMaterial = _pageMat;
            _pageRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _pageRenderer.receiveShadows = false;
            _pageRenderer.enabled = false;

            _corner = new GameObject(CornerId);
            _corner.transform.SetParent(transform, false);
            _corner.transform.localPosition = new Vector3(CornerLocal.x, y + 0.004f, CornerLocal.z);
            _corner.transform.localRotation = Quaternion.identity;
            var box = _corner.AddComponent<BoxCollider>();
            box.size = new Vector3(0.040f, 0.012f, 0.040f);
            var target = _corner.AddComponent<IntentTarget>();
            target.Id = CornerId;
            var fold = new GameObject("fold");
            fold.transform.SetParent(_corner.transform, false);
            fold.transform.localRotation = Quaternion.Euler(-90f, 38f, 0f);
            fold.transform.localScale = new Vector3(0.034f, 0.034f, 1f);
            fold.AddComponent<MeshFilter>().sharedMesh = Quad();
            var foldRenderer = fold.AddComponent<MeshRenderer>();
            foldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            foldRenderer.receiveShadows = false;
            if (_pageMat != null)
            {
                _foldMat = new Material(_pageMat) { name = "WeekCorner" };
                _foldMat.SetTexture("_MainTex", FoldTexture());
                _foldMat.renderQueue = 3210;
                foldRenderer.sharedMaterial = _foldMat;
            }

            _caption = new GameObject("WeekCaption");
            _caption.transform.SetParent(transform, false);
            _captionMesh = _caption.AddComponent<TextMesh>();
            InkLetter.Apply(_captionMesh, CaptionLine, 52, 0.0034f);
            LiftWords(_captionMesh);
            _caption.SetActive(false);

            for (int arc = 0; arc < 3; arc++)
            {
                var go = new GameObject(arc == 0 ? "week.morning" : arc == 1 ? "week.midday" : "week.winddown");
                go.transform.SetParent(transform, false);
                TextMesh mesh = go.AddComponent<TextMesh>();
                InkLetter.Apply(mesh, "", 46, 0.0025f);
                LiftWords(mesh);
                _names[arc] = go;
                _nameMesh[arc] = mesh;
                go.SetActive(false);
            }
        }

        void Pose()
        {
            if (_hinge == null) return;
            float u = _turn * _turn * (3f - 2f * _turn);
            float lift = (1f - u) * -58f;
            _hinge.transform.localRotation = Quaternion.Euler(lift, 0f, 0f);
            float s = Mathf.Max(u, 0.001f);
            _hinge.transform.localScale = new Vector3(s, 1f, s);
            if (_pageRenderer != null) _pageRenderer.enabled = u > 0.04f;
        }

        void Paint()
        {
            if (!_built || _pageTex == null) return;
            WeekDial dial = Record;
            string key = dial == null ? "" : dial.ToJson();
            if (key == _paintKey && _ink != null) return;
            _paintKey = key;
            IList<HabitDef> habits = null;
            if (Service != null && Service.Save != null) habits = Service.Save.Habits;
            _ink = WeekPageInk.Paint(_diameter, dial, habits);
            _pageTex.SetPixels32(_ink);
            _pageTex.Apply(false, false);
            if (_pageRenderer != null)
            {
                var block = new MaterialPropertyBlock();
                _pageRenderer.GetPropertyBlock(block);
                block.SetTexture("_MainTex", _pageTex);
                _pageRenderer.SetPropertyBlock(block);
            }
        }

        void PlaceWords()
        {
            bool show = OnWeekPage && !Blocked();
            if (_caption != null && _caption.activeSelf != show) _caption.SetActive(show);
            SundialService service = Service;
            IList<HabitDef> habits = service != null && service.Save != null ? service.Save.Habits : null;
            float y = _sundial != null && _sundial.View != null ? _sundial.View.faceY + 0.028f : 0.04f;
            if (_caption != null && show)
            {
                _caption.transform.localPosition = new Vector3(0f, y, 0.086f);
                Face(_caption.transform);
            }
            for (int arc = 0; arc < 3; arc++)
            {
                if (_names[arc] == null) continue;
                HabitDef habit = WeekPageInk.FirstOfArc(habits, arc);
                string line = habit == null ? "" : WeekPageInk.PlantLine(habit);
                bool on = show && line.Length > 0;
                if (_names[arc].activeSelf != on) _names[arc].SetActive(on);
                if (!on) continue;
                if (_nameMesh[arc] != null && _nameMesh[arc].text != line)
                {
                    InkLetter.Apply(_nameMesh[arc], line, 46, 0.0025f);
                    LiftWords(_nameMesh[arc]);
                }
                _names[arc].transform.localPosition = new Vector3(-0.096f, y, WeekPageInk.BandZ[arc]);
                Face(_names[arc].transform);
            }
        }

        static void LiftWords(TextMesh mesh)
        {
            if (mesh == null) return;
            Renderer renderer = mesh.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            renderer.material.renderQueue = 3220;
        }

        void Face(Transform words)
        {
            Camera cam = Camera.main;
            if (cam == null || words == null) return;
            Vector3 to = cam.transform.position - words.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-8f) return;
            words.rotation = Quaternion.LookRotation(to, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        }

        void CoverOthers(bool cover)
        {
            if (!cover)
            {
                if (_covered)
                {
                    FocusBlockController focus = GetComponent<FocusBlockController>();
                    if (focus != null) focus.RefreshInk();
                }
                _covered = false;
                return;
            }
            _covered = true;
            HideRenderer("Tiles");
            HideRenderer("Gnomon");
            HideRenderer("GnomonShadow");
            HideRenderer("Soil");
            HideRenderer("PinchHalo");
            HideRenderer("HaloPool");
            HideRenderer("PlantContacts");
            HideRenderer("FocusInk");
            Suppress("FocusPrompt");
            Suppress("GratitudePrompt");
            Suppress("StretchPrompt");
            Suppress("BreathPrompt");
            Suppress("ContinuePrompt");
            Suppress("BackfillPrompt");
            Suppress("VoiceCaption");
            Suppress("DuskCircle");
            Suppress("DuskSparkle");
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                string name = t.name;
                if (name.StartsWith("plant.") || name.StartsWith("bloom."))
                    DisableRenderer(t);
                else if (name.StartsWith("symbol.") || name.StartsWith("mark."))
                    t.gameObject.SetActive(false);
            }
        }

        void HideRenderer(string name)
        {
            Transform found = FindDeep(transform, name);
            if (found != null) DisableRenderer(found);
        }

        static void Suppress(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null && go.activeSelf) go.SetActive(false);
        }

        static void DisableRenderer(Transform t)
        {
            Renderer renderer = t.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
        }

        void PlayCue(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _cues.Add(id);
            if (_sundial != null) _sundial.Play(id, _corner == null ? transform : _corner.transform);
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

        static Texture2D FoldTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.name = "WeekCorner";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    if (x + y > n - 2)
                    {
                        pixels[y * n + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    int crease = Mathf.Abs(x + y - (n - 6));
                    Color paper = WeekPageInk.Paper;
                    if (crease < 3) paper = Color.Lerp(paper, WeekPageInk.Ink, crease == 0 ? 0.85f : 0.4f);
                    else paper = Color.Lerp(paper, WeekPageInk.Pencil, 0.08f);
                    pixels[y * n + x] = new Color32(
                        (byte)Mathf.RoundToInt(paper.r * 255f),
                        (byte)Mathf.RoundToInt(paper.g * 255f),
                        (byte)Mathf.RoundToInt(paper.b * 255f),
                        255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static Mesh Quad()
        {
            var mesh = new Mesh { name = "WeekPageQuad" };
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

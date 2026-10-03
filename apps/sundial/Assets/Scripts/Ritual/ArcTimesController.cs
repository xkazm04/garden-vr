using System;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Settings opens three edges on the rim. Pinch-hold one and drag it along the rim.
    /// The core snaps the boundary to 15 minutes and keeps each arc at least an hour.
    /// The words are Morning, Midday and Dusk. Nothing here reads a keyboard or a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(170)]
    public sealed class ArcTimesController : MonoBehaviour
    {
        public const string MorningId = "arc.edge.morning";
        public const string MiddayId = "arc.edge.midday";
        public const string DuskId = "arc.edge.dusk";
        public const string MorningLabel = "Morning";
        public const string MiddayLabel = "Midday";
        public const string DuskLabel = "Dusk";

        SundialController _sundial;
        IHandIntentSource _source;
        bool _subscribed;
        bool _built;
        bool _gesture;
        bool _moved;
        float _prevDeg;
        ArcEdge _edge;
        GameObject _root;
        readonly Edge[] _edges = new Edge[3];

        public bool Dragging { get { return _gesture; } }
        public bool EdgesVisible { get { return _root != null && _root.activeSelf; } }
        public ArcEdge Grabbed { get { return _edge; } }

        SundialService Service { get { return _sundial == null ? null : _sundial.Service; } }

        public static bool TryEdge(string id, out ArcEdge edge)
        {
            edge = ArcEdge.Morning;
            if (id == MorningId) { edge = ArcEdge.Morning; return true; }
            if (id == MiddayId) { edge = ArcEdge.Midday; return true; }
            if (id == DuskId) { edge = ArcEdge.Dusk; return true; }
            return false;
        }

        public static string IdFor(ArcEdge edge)
        {
            if (edge == ArcEdge.Morning) return MorningId;
            if (edge == ArcEdge.Midday) return MiddayId;
            return DuskId;
        }

        public string Label(ArcEdge edge)
        {
            Edge found = Find(edge);
            if (found == null || found.Words == null) return "";
            return found.Words.text;
        }

        public void SetSource(IHandIntentSource source)
        {
            if (_source == source) return;
            Unsubscribe();
            _source = source;
            _gesture = false;
            _moved = false;
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
            _gesture = false;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (_sundial == null) _sundial = GetComponent<SundialController>();
            if (_sundial != null && _sundial.Source != _source)
                SetSource(_sundial.Source);
            if (!_built) Build();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !_built || _root == null) return;
            bool open = CanShow();
            if (_root.activeSelf != open) _root.SetActive(open);
            if (!open)
            {
                _gesture = false;
                _moved = false;
                return;
            }
            Place();
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.Release)
            {
                _gesture = false;
                _moved = false;
                return;
            }
            if (intent.Kind != HandIntentKind.PinchHold) return;
            if (!CanDrag()) return;
            float deg;
            if (!RimScrubController.TryDialAngle(transform, intent.Ray, PlaneY(), out deg)) return;

            if (!_gesture)
            {
                ArcEdge edge;
                if (!TryEdge(intent.TargetId, out edge)) return;
                _gesture = true;
                _moved = false;
                _edge = edge;
                _prevDeg = deg;
                return;
            }

            if (!_moved)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(_prevDeg, deg)) < 2f)
                {
                    _prevDeg = deg;
                    return;
                }
                _moved = true;
            }
            _prevDeg = deg;
            if (Service != null) Service.DragArcEdge(_edge, deg);
        }

        bool CanShow()
        {
            if (_sundial == null || _sundial.Settings == null || !_sundial.Settings.Open) return false;
            if (_sundial.Dismissed) return false;
            if (_sundial.View != null && _sundial.View.weekPage) return false;
            return true;
        }

        bool CanDrag()
        {
            if (!CanShow()) return false;
            if (Service != null && Service.Scrubbing) return false;
            FirstRunWizard wizard = GetComponent<FirstRunWizard>();
            if (wizard != null && wizard.Running) return false;
            return true;
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            _edges[0] = Make(ArcEdge.Morning, MorningId, MorningLabel);
            _edges[1] = Make(ArcEdge.Midday, MiddayId, MiddayLabel);
            _edges[2] = Make(ArcEdge.Dusk, DuskId, DuskLabel);
            _root = new GameObject("ArcEdges");
            _root.transform.SetParent(transform, false);
            for (int i = 0; i < _edges.Length; i++)
                _edges[i].Root.transform.SetParent(_root.transform, false);
            _root.SetActive(false);
        }

        static Edge Make(ArcEdge edge, string id, string label)
        {
            var root = new GameObject(id);
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.036f, 0.02f, 0.036f);
            var target = root.AddComponent<IntentTarget>();
            target.Id = id;
            var wordsGo = new GameObject("words");
            wordsGo.transform.SetParent(root.transform, false);
            wordsGo.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            var words = wordsGo.AddComponent<TextMesh>();
            InkLetter.Apply(words, label, 48, 0.0016f);
            return new Edge { EdgeId = edge, Root = root, Words = words };
        }

        void Place()
        {
            if (Service == null) return;
            ArcTimes schedule = Service.ArcSchedule;
            float radius = 0.1472f;
            float y = 0.012f;
            if (_sundial != null && _sundial.View != null)
            {
                if (_sundial.View.faceRadius > 0.05f) radius = _sundial.View.faceRadius;
                y = _sundial.View.faceY;
            }
            float ring = radius * 1.06f;
            for (int i = 0; i < _edges.Length; i++)
            {
                Edge edge = _edges[i];
                if (edge == null || edge.Root == null) continue;
                float deg = schedule.Angle(edge.EdgeId);
                float rad = deg * Mathf.Deg2Rad;
                edge.Root.transform.localPosition = new Vector3(Mathf.Cos(rad) * ring, y, -Mathf.Sin(rad) * ring);
                if (edge.Words != null) Face(edge.Words.transform);
            }
        }

        float PlaneY()
        {
            if (_sundial != null && _sundial.View != null) return _sundial.View.faceY;
            return 0.012f;
        }

        static void Face(Transform words)
        {
            Camera cam = Camera.main;
            if (cam == null || words == null) return;
            Vector3 to = cam.transform.position - words.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-8f) return;
            words.rotation = Quaternion.LookRotation(to, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        }

        Edge Find(ArcEdge edge)
        {
            for (int i = 0; i < _edges.Length; i++)
            {
                if (_edges[i] != null && _edges[i].EdgeId == edge) return _edges[i];
            }
            return null;
        }

        void Subscribe()
        {
            if (_subscribed || _source == null) return;
            _source.Intent += OnIntent;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || _source == null) return;
            _source.Intent -= OnIntent;
            _subscribed = false;
        }

        sealed class Edge
        {
            public ArcEdge EdgeId;
            public GameObject Root;
            public TextMesh Words;
        }
    }
}

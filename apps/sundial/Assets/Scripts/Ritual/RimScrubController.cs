using System;
using System.Globalization;
using GardenVR.Core;
using GardenVR.Input;
using UnityEngine;

namespace GardenVR.Sundial
{
    /// <summary>
    /// Pinch-hold the rim and drag backward to read earlier today or this week.
    /// Release eases the shadow home. The ledger is not written while the read is up.
    /// The ray comes from the intent. This class does not read a mouse or a keyboard.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(160)]
    public sealed class RimScrubController : MonoBehaviour
    {
        public const int RimMarks = 24;
        public const float SettleSeconds = 0.55f;
        public const float TwoFootMetres = 0.61f;
        public const string RimPrefix = "dial.rim.";

        SundialController _sundial;
        IHandIntentSource _source;
        bool _subscribed;
        bool _built;
        bool _gesture;
        bool _settling;
        bool _haveAngle;
        float _prevDeg;
        float _rewind;
        float _settleFrom;
        float _settleT;
        float _shownDeg;
        GameObject _caption;
        TextMesh _captionMesh;

        public bool Scrubbing { get { return _gesture; } }
        public bool Settling { get { return _settling; } }
        public bool Reading { get { return _gesture || _settling; } }
        public float RewindDegrees { get { return _rewind; } }
        public float ShownDegrees { get { return _shownDeg; } }
        public string Caption { get { return _captionMesh == null || !_caption.activeSelf ? "" : _captionMesh.text; } }

        public RimRead Read
        {
            get { return RimScrub.FromDrag(NowMin(), Reading ? _rewind : 0f); }
        }

        public int DaysBack { get { return Read.DaysBack; } }
        public int Minute { get { return Read.Minute; } }

        SundialService Service { get { return _sundial == null ? null : _sundial.Service; } }

        public static bool IsRim(string id)
        {
            return !string.IsNullOrEmpty(id) && id.StartsWith(RimPrefix, StringComparison.Ordinal);
        }

        public static string RimId(int index)
        {
            int mark = index % RimMarks;
            if (mark < 0) mark += RimMarks;
            return RimPrefix + mark.ToString("00", CultureInfo.InvariantCulture);
        }

        public static bool TryDialAngle(Transform dial, Ray ray, float planeY, out float degrees)
        {
            degrees = 0f;
            if (dial == null) return false;
            Vector3 origin = dial.InverseTransformPoint(ray.origin);
            Vector3 dir = dial.InverseTransformDirection(ray.direction);
            if (Mathf.Abs(dir.y) < 1e-6f) return false;
            float t = (planeY - origin.y) / dir.y;
            if (t < 0f) return false;
            Vector3 hit = origin + dir * t;
            if (hit.x * hit.x + hit.z * hit.z < 1e-8f) return false;
            float deg = Mathf.Atan2(-hit.z, hit.x) * Mathf.Rad2Deg;
            if (deg < 0f) deg += 360f;
            degrees = deg;
            return true;
        }

        public void SetSource(IHandIntentSource source)
        {
            if (_source == source) return;
            Unsubscribe();
            _source = source;
            _gesture = false;
            _settling = false;
            _haveAngle = false;
            _rewind = 0f;
            if (Service != null) Service.SetScrubbing(false);
            HideCaption();
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
            _settling = false;
            if (Service != null) Service.SetScrubbing(false);
            HideCaption();
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

            if (_settling)
            {
                bool snap = Service != null && Service.ReducedMotion;
                float dur = snap ? 0f : SettleSeconds;
                _settleT += snap ? 1f : dt;
                float u = dur <= 0.001f ? 1f : Mathf.Clamp01(_settleT / dur);
                u = u * u * (3f - 2f * u);
                float home = LiveDegrees();
                ApplyShown(Mathf.LerpAngle(_settleFrom, home, u), 1f - u);
                if (_settleT >= dur - 0.0001f)
                {
                    _settling = false;
                    _rewind = 0f;
                    if (Service != null) Service.SetScrubbing(false);
                    HideCaption();
                }
                return;
            }

            if (_gesture)
                ApplyShown(Read.GnomonDeg, 1f);
        }

        void OnIntent(HandIntent intent)
        {
            if (intent.Kind == HandIntentKind.Release)
            {
                if (_gesture) BeginSettle();
                return;
            }
            if (intent.Kind != HandIntentKind.PinchHold) return;
            float deg;
            if (!TryDialAngle(transform, intent.Ray, PlaneY(), out deg)) return;

            if (!_gesture)
            {
                if (!IsRim(intent.TargetId) || Blocked()) return;
                _gesture = true;
                _haveAngle = true;
                _prevDeg = deg;
                _rewind = 0f;
                _shownDeg = Read.GnomonDeg;
                if (Service != null) Service.SetScrubbing(true);
                return;
            }

            if (_haveAngle)
            {
                float delta = Mathf.DeltaAngle(_prevDeg, deg);
                _rewind -= delta;
                if (_rewind < 0f) _rewind = 0f;
            }
            _prevDeg = deg;
            _haveAngle = true;
        }

        void BeginSettle()
        {
            _gesture = false;
            _haveAngle = false;
            _settling = true;
            _settleFrom = _shownDeg;
            _settleT = 0f;
            if (Service != null) Service.SetScrubbing(true);
        }

        bool Blocked()
        {
            if (_sundial != null && _sundial.Dismissed) return true;
            if (_sundial != null && _sundial.View != null && _sundial.View.weekPage) return true;
            FirstRunWizard wizard = GetComponent<FirstRunWizard>();
            if (wizard != null && wizard.Running) return true;
            DuskRitualController dusk = GetComponent<DuskRitualController>();
            if (dusk != null && dusk.Active) return true;
            FocusBlockController focus = GetComponent<FocusBlockController>();
            if (focus != null && (focus.Running || focus.Paused)) return true;
            return false;
        }

        void ApplyShown(float gnomonDeg, float captionAlpha)
        {
            _shownDeg = gnomonDeg;
            if (_sundial == null || _sundial.View == null || Service == null) return;
            DialView view = _sundial.View;
            RimRead read = Read;
            SundialState picture = null;
            if (Service.Ledger != null)
                picture = RimScrub.Query(Service.Save == null ? null : Service.Save.Habits, Service.Ledger, Service.Today(), read);
            if (view.tiles == null || view.tiles.Length != DialView.TileCount)
                view.tiles = new int[DialView.TileCount];
            for (int t = 0; t < view.tiles.Length; t++) view.tiles[t] = DialView.HiddenTile;
            if (picture != null && picture.Plants != null && Service.Save != null && Service.Save.Habits != null)
            {
                for (int i = 0; i < Service.Save.Habits.Count; i++)
                {
                    HabitDef habit = Service.Save.Habits[i];
                    if (habit == null) continue;
                    int arc = SundialArcs.Index(SundialArcs.Key(habit.Group));
                    int row = SundialRules.RowOf(Service.Save.Habits, habit);
                    if (arc < 0 || row < 0) continue;
                    PlantState plant = null;
                    for (int p = 0; p < picture.Plants.Count; p++)
                    {
                        PlantState found = picture.Plants[p];
                        if (found != null && found.HabitId == habit.Id)
                        {
                            plant = found;
                            break;
                        }
                    }
                    if (plant == null || plant.Window == null) continue;
                    int count = plant.Window.Length < 7 ? plant.Window.Length : 7;
                    for (int slot = 0; slot < count; slot++)
                        view.tiles[DialView.TileIndex(arc, row, slot)] = SundialArcs.TileDigit(plant.Window[slot]);
                }
            }
            view.waiting = 0f;
            view.gnomonDeg = gnomonDeg;
            view.Apply();
            ShowCaption(read.Caption, captionAlpha);
        }

        float LiveDegrees()
        {
            if (_sundial != null && _sundial.View != null) return _sundial.View.gnomonDeg;
            if (Service != null && Service.State != null) return Service.State.GnomonDeg;
            return _settleFrom;
        }

        int NowMin()
        {
            if (Service == null || Service.Clock == null) return 0;
            int minute = (int)Service.Clock.Now.TimeOfDay.TotalMinutes;
            if (minute < 0) minute = 0;
            if (minute > 1439) minute = 1439;
            return minute;
        }

        float PlaneY()
        {
            if (_sundial != null && _sundial.View != null) return _sundial.View.faceY;
            return 0.012f;
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            float radius = 0.1472f;
            float y = 0.012f;
            if (_sundial != null && _sundial.View != null)
            {
                if (_sundial.View.faceRadius > 0.05f) radius = _sundial.View.faceRadius;
                y = _sundial.View.faceY;
            }
            float ring = radius * 0.97f;
            var root = new GameObject("DialRim");
            root.transform.SetParent(transform, false);
            for (int i = 0; i < RimMarks; i++)
            {
                float deg = i * (360f / RimMarks);
                float rad = deg * Mathf.Deg2Rad;
                var mark = new GameObject(RimId(i));
                mark.transform.SetParent(root.transform, false);
                mark.transform.localPosition = new Vector3(Mathf.Cos(rad) * ring, y, -Mathf.Sin(rad) * ring);
                mark.transform.localRotation = Quaternion.identity;
                var box = mark.AddComponent<BoxCollider>();
                box.size = new Vector3(0.026f, 0.02f, 0.026f);
                var target = mark.AddComponent<IntentTarget>();
                target.Id = RimId(i);
            }

            _caption = new GameObject("RimCaption");
            _caption.transform.SetParent(transform, false);
            _captionMesh = _caption.AddComponent<TextMesh>();
            InkLetter.Apply(_captionMesh, "", 48, 0.0026f);
            Lift(_captionMesh);
            _caption.SetActive(false);
        }

        void ShowCaption(string text, float alpha)
        {
            if (_caption == null || _captionMesh == null) return;
            bool show = !string.IsNullOrEmpty(text) && alpha > 0.04f;
            if (_caption.activeSelf != show) _caption.SetActive(show);
            if (!show) return;
            if (_captionMesh.text != text)
            {
                InkLetter.Apply(_captionMesh, text, 48, 0.0026f);
                Lift(_captionMesh);
            }
            Color ink = InkLetter.Ink;
            ink.a = Mathf.Clamp01(alpha);
            _captionMesh.color = ink;
            float y = PlaneY() + 0.03f;
            _caption.transform.localPosition = new Vector3(0f, y, -0.108f);
            Face(_caption.transform);
        }

        void HideCaption()
        {
            if (_caption != null && _caption.activeSelf) _caption.SetActive(false);
        }

        static void Lift(TextMesh mesh)
        {
            if (mesh == null) return;
            Renderer renderer = mesh.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            renderer.material.renderQueue = 3220;
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
    }
}

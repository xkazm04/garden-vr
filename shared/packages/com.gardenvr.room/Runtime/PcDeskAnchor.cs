using System;
using UnityEngine;

namespace GardenVR.Room
{
    /// <summary>
    /// Fixed desk pose for the PC stand-in. Parent this to the seated rig at floor level.
    /// Local position is (lateral, height, distance), facing the user's forward (+Z).
    /// Terrarium sits 0.40 m ahead. Sundial uses 0.55 m. Height 0.75 m is 0.30 m below a 1.05 m eye.
    /// The mint card traces the near edge (toward the user) over 0.8 s starting at t = 2.5 s, then raises Placed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PcDeskAnchor : MonoBehaviour, IPlacementProvider
    {
        public const float DefaultHeight = 0.75f;
        public const float BelowEye = 0.30f;
        public const float TerrariumDistance = 0.40f;
        public const float SundialDistance = 0.55f;
        public const float TraceDelaySeconds = 2.5f;
        public const float TraceSeconds = 0.8f;

        static readonly Color Mint = new Color(0x8F / 255f, 0xF0 / 255f, 0xC8 / 255f, 1f);

        [SerializeField] float _height = DefaultHeight;
        [SerializeField] float _distance = TerrariumDistance;
        [SerializeField] float _lateral;
        [SerializeField] float _traceDelay = TraceDelaySeconds;
        [SerializeField] float _traceSeconds = TraceSeconds;
        [SerializeField] float _traceWidth = 0.48f;
        [SerializeField] float _traceThickness = 0.006f;
        [SerializeField] float _nearEdge = 0.16f;
        [SerializeField] Transform _trace;
        [SerializeField] bool _playIntro = true;

        float _time;
        bool _running;
        bool _placed;

        public float Height
        {
            get => _height;
            set
            {
                _height = value;
                ApplyPose();
            }
        }

        public float Distance
        {
            get => _distance;
            set
            {
                _distance = value;
                ApplyPose();
            }
        }

        public float Lateral
        {
            get => _lateral;
            set
            {
                _lateral = value;
                ApplyPose();
            }
        }

        public float Trace01 { get; private set; } = 1f;
        public float TraceDelay => _traceDelay;
        public float TraceDuration => _traceSeconds;
        public float Elapsed => _time;
        public bool TraceRunning => _running;

        /// <summary>Local distance from the desk origin to the near edge, toward the user.</summary>
        public float NearEdge => _nearEdge;

        public Pose DeskPose => new Pose(transform.position, transform.rotation);

        public Plane DeskPlane => new Plane(transform.up, transform.position);

        public event Action Placed;

        public static Color TraceMint => Mint;

        public void ApplyPose()
        {
            transform.localPosition = new Vector3(_lateral, _height, _distance);
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Reset the edge trace. It stays hidden until <see cref="TraceDelaySeconds"/>, then draws, then raises Placed once.</summary>
        public void Begin()
        {
            _time = 0f;
            _placed = false;
            _running = true;
            Trace01 = 0f;
            ApplyTrace();
        }

        public void Tick(float dt)
        {
            if (!_running || _placed) return;
            _time += Mathf.Max(0f, dt);
            float duration = Mathf.Max(0.0001f, _traceSeconds);
            if (_time < _traceDelay) Trace01 = 0f;
            else Trace01 = Mathf.Clamp01((_time - _traceDelay) / duration);
            ApplyTrace();
            // A hair under the sum still counts as finished. 2.5 + 0.8 is not exact in float.
            if (_time + 0.0001f < _traceDelay + duration) return;
            _placed = true;
            _running = false;
            ApplyTrace();
            Placed?.Invoke();
        }

        public void BindTrace(Transform trace)
        {
            _trace = trace;
        }

        void OnEnable()
        {
            // Edit-mode captures never enter play, so the saved trace quad would sit on the desk as a line.
            if (!Application.isPlaying) ApplyTrace();
        }

        void Start()
        {
            ApplyPose();
            if (_playIntro) Begin();
        }

        void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        void OnValidate()
        {
            _traceSeconds = Mathf.Max(0.01f, _traceSeconds);
            _traceDelay = Mathf.Max(0f, _traceDelay);
            if (!Application.isPlaying) ApplyPose();
        }

        void ApplyTrace()
        {
            if (_trace == null) return;
            // The mint line is only the intro drawing across the near edge. Before it starts, and after Placed, it is hidden.
            bool drawing = _running && !_placed && Trace01 > 0f;
            float width = drawing ? _traceWidth * Mathf.Clamp01(Trace01) : 0f;
            float drawn = Mathf.Max(width, 0.0001f);
            _trace.localScale = new Vector3(drawn, _traceThickness, 1f);
            // The quad is centred. Hold the left end still so the line grows along the edge.
            float x = -0.5f * _traceWidth + 0.5f * width;
            _trace.localPosition = new Vector3(x, 0.012f, -_nearEdge);
            _trace.localRotation = Quaternion.identity;
        }
    }
}

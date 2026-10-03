using UnityEngine;
using GardenVR.Input;

namespace GardenVR.Room
{
    /// <summary>
    /// Seated eye for the PC stand-in. Position stays at eye height. Rotation comes only from
    /// <see cref="IHeadPoseSource"/>, then yaw and pitch are clamped. The source's rotation is an
    /// offset on top of the rest pitch, which aims the eye at the desk anchor, so a right-drag looks
    /// around the desk instead of replacing the look-down.
    /// The room card is not a child of the camera. It stays on <see cref="PlateAnchor"/> at the rest pose.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    [ExecuteAlways]
    public sealed class SeatedRig : MonoBehaviour
    {
        public const float DefaultEyeHeight = 1.05f;
        public const float DefaultYawLimit = 40f;
        public const float DefaultPitchLimit = 25f;
        public const float DefaultFieldOfView = 60f;
        public const float DefaultNearClip = 0.02f;

        /// <summary>Seated plate distance in front of the eye. The hero is well inside this.</summary>
        public const float PlateDistance = 4f;

        /// <summary>Capture <c>SeatedPOV</c> vertical field of view. The plate card is sized to fill it.</summary>
        public const float SeatedCaptureFov = 90f;

        public const float SeatedCaptureAspect = 1824f / 1024f;

        /// <summary>A hair past the capture frustum so the clear colour cannot fringe the frame.</summary>
        public const float SeatedCardMargin = 1.02f;

        [SerializeField] float _eyeHeight = DefaultEyeHeight;
        [SerializeField] float _yawLimit = DefaultYawLimit;
        [SerializeField] float _pitchLimit = DefaultPitchLimit;
        [SerializeField] float _fieldOfView = DefaultFieldOfView;
        [SerializeField] float _nearClip = DefaultNearClip;
        [SerializeField] float _farClip = 20f;
        [SerializeField] float _lookAboveDesk;
        [SerializeField] Transform _headPivot;
        [SerializeField] Camera _eye;
        [SerializeField] MonoBehaviour _headPoseBehaviour;
        [SerializeField] Transform _plateAnchor;

        IHeadPoseSource _source;
        bool _subscribed;

        public float EyeHeight
        {
            get => _eyeHeight;
            set
            {
                _eyeHeight = value;
                ApplyBody();
            }
        }

        public float YawLimit => _yawLimit;
        public float PitchLimit => _pitchLimit;
        public float YawDegrees { get; private set; }
        public float PitchDegrees { get; private set; }

        /// <summary>Terrarium look-down, used when the rig has no desk anchor. Live aim is <see cref="RestPitchDegrees"/>.</summary>
        public static float DefaultSeatedPitch =>
            Mathf.Atan2(PcDeskAnchor.BelowEye, PcDeskAnchor.TerrariumDistance) * Mathf.Rad2Deg;

        public static Quaternion SeatedPitchQuaternion => Quaternion.Euler(DefaultSeatedPitch, 0f, 0f);

        /// <summary>Pitch that puts the desk anchor in the centre of the view, minus <see cref="LookAboveDesk"/>. Positive looks down.</summary>
        public float RestPitchDegrees { get; private set; } = DefaultSeatedPitch;

        /// <summary>
        /// Degrees the rest pose looks above the desk point, so the hero sits lower in the frame.
        /// Zero aims at the desk. The plate anchor uses the same pitch, so the photograph stays square to the lens.
        /// </summary>
        public float LookAboveDesk
        {
            get => _lookAboveDesk;
            set
            {
                _lookAboveDesk = Mathf.Max(0f, value);
                ApplyRest();
                if (!Application.isPlaying) ApplyPose();
            }
        }

        public Transform PlateAnchor => _plateAnchor;

        /// <summary>Width and height of a card at <paramref name="distance"/> that fills <paramref name="verticalFovDegrees"/>.</summary>
        public static Vector2 SeatedCardSize(float distance, float verticalFovDegrees, float aspect)
        {
            float height = 2f * distance * Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            return new Vector2(height * aspect, height);
        }

        /// <summary>Put a unit quad (XY, facing the eye) on the capture frustum. Parent it to the plate anchor.</summary>
        public static void PlaceSeatedCard(Transform card)
        {
            PlaceCard(card, PlateDistance, SeatedCaptureFov);
        }

        /// <summary>Same card, sized to the seated lens (<see cref="DefaultFieldOfView"/>) rather than the wide capture frustum.</summary>
        public static void PlaceLensCard(Transform card)
        {
            PlaceCard(card, PlateDistance, DefaultFieldOfView);
        }

        public static void PlaceCard(Transform card, float distance, float verticalFovDegrees)
        {
            Vector2 size = SeatedCardSize(distance, verticalFovDegrees, SeatedCaptureAspect) * SeatedCardMargin;
            card.localPosition = new Vector3(0f, 0f, distance);
            card.localRotation = Quaternion.identity;
            card.localScale = new Vector3(size.x, size.y, 1f);
        }

        public static float PitchAimingAt(float dropBelowEye, float distanceAhead)
        {
            return Mathf.Atan2(Mathf.Max(0f, dropBelowEye), Mathf.Max(0.05f, distanceAhead)) * Mathf.Rad2Deg;
        }

        public void Bind(Transform headPivot, Camera eye, MonoBehaviour headPose)
        {
            _headPivot = headPivot;
            _eye = eye;
            SetHeadSource(headPose);
            ApplyBody();
            ApplyRest();
            ApplyPose();
        }

        public void SetPlateAnchor(Transform plateAnchor)
        {
            _plateAnchor = plateAnchor;
            ApplyRest();
        }

        public void SetHeadSource(MonoBehaviour headPose)
        {
            Unsubscribe();
            _headPoseBehaviour = headPose;
            _source = headPose as IHeadPoseSource;
            Subscribe();
        }

        public void ApplyBody()
        {
            if (_headPivot != null)
                _headPivot.localPosition = new Vector3(0f, _eyeHeight, 0f);
            if (_eye == null) return;
            _eye.fieldOfView = _fieldOfView;
            _eye.nearClipPlane = _nearClip;
            _eye.farClipPlane = _farClip;
        }

        /// <summary>
        /// Aim the rest pose at the desk anchor and park the room card on that pose.
        /// Edit-mode captures do not run LateUpdate, so OnEnable calls this before the shot copies the eye.
        /// </summary>
        public void ApplyRest()
        {
            float pitch = DefaultSeatedPitch;
            PcDeskAnchor desk = GetComponentInChildren<PcDeskAnchor>(true);
            if (desk != null)
                pitch = PitchAimingAt(_eyeHeight - desk.Height, desk.Distance);
            pitch = Mathf.Max(0f, pitch - _lookAboveDesk);
            RestPitchDegrees = pitch;
            if (_plateAnchor == null)
            {
                Transform found = transform.Find("RoomPlateAnchor");
                if (found != null) _plateAnchor = found;
            }
            if (_plateAnchor == null) return;
            Vector3 position = new Vector3(0f, _eyeHeight, 0f);
            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            if ((_plateAnchor.localPosition - position).sqrMagnitude > 1e-8f)
                _plateAnchor.localPosition = position;
            if (Quaternion.Angle(_plateAnchor.localRotation, rotation) > 0.01f)
                _plateAnchor.localRotation = rotation;
        }

        /// <summary>Read the head source, clamp yaw and pitch, and write the pivot. LateUpdate calls this.</summary>
        public void ApplyPose()
        {
            Quaternion local = _source != null ? _source.LocalRotation : Quaternion.identity;
            Quaternion clamped = ClampLook(local, _yawLimit, _pitchLimit);
            ExtractLook(clamped, out float yaw, out float pitch);
            YawDegrees = yaw;
            PitchDegrees = pitch;
            if (_headPivot == null) return;
            Quaternion next = Quaternion.Euler(RestPitchDegrees, 0f, 0f) * clamped;
            if (Quaternion.Angle(_headPivot.localRotation, next) > 0.01f)
                _headPivot.localRotation = next;
        }

        public void Recentre()
        {
            YawDegrees = 0f;
            PitchDegrees = 0f;
            if (_headPivot != null)
                _headPivot.localRotation = Quaternion.Euler(RestPitchDegrees, 0f, 0f);
        }

        /// <summary>
        /// Clamp the look offset. Pitch is positive looking down, matching Quaternion.Euler(pitch, yaw, 0)
        /// from the keyboard/mouse provider (RotY * RotX).
        /// </summary>
        public static Quaternion ClampLook(Quaternion local, float yawLimit, float pitchLimit)
        {
            ExtractLook(local, out float yaw, out float pitch);
            yaw = Mathf.Clamp(yaw, -Mathf.Abs(yawLimit), Mathf.Abs(yawLimit));
            pitch = Mathf.Clamp(pitch, -Mathf.Abs(pitchLimit), Mathf.Abs(pitchLimit));
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        public static void ExtractLook(Quaternion local, out float yaw, out float pitch)
        {
            Vector3 forward = local * Vector3.forward;
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float planar = Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z);
            pitch = Mathf.Atan2(-forward.y, planar) * Mathf.Rad2Deg;
        }

        void Awake()
        {
            ResolveHead();
            ApplyRest();
            ApplyBody();
        }

        void OnEnable()
        {
            ResolveHead();
            ApplyRest();
            Subscribe();
            if (!Application.isPlaying) ApplyPose();
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            ApplyRest();
            if (!Application.isPlaying) ApplyPose();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void LateUpdate()
        {
            ApplyPose();
        }

        void HandleRecentred()
        {
            Recentre();
        }

        void ResolveHead()
        {
            if (_source != null) return;
            if (_headPoseBehaviour is IHeadPoseSource serialized)
            {
                _source = serialized;
                return;
            }
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null || behaviours[i] == this) continue;
                if (behaviours[i] is IHeadPoseSource found)
                {
                    _headPoseBehaviour = behaviours[i];
                    _source = found;
                    return;
                }
            }
        }

        void Subscribe()
        {
            if (_subscribed || _source == null) return;
            _source.Recentred += HandleRecentred;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || _source == null)
            {
                _subscribed = false;
                return;
            }
            _source.Recentred -= HandleRecentred;
            _subscribed = false;
        }
    }
}

using UnityEngine;
using GardenVR.Input;

namespace GardenVR.Room
{
    /// <summary>
    /// Seated eye for the PC stand-in. Position stays at eye height. Rotation comes only from
    /// <see cref="IHeadPoseSource"/>, then yaw and pitch are clamped. The source's rotation is an
    /// offset on top of the seated pitch, so a right-drag looks around the desk instead of replacing the look-down.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class SeatedRig : MonoBehaviour
    {
        public const float DefaultEyeHeight = 1.05f;
        public const float DefaultYawLimit = 40f;
        public const float DefaultPitchLimit = 25f;
        public const float DefaultFieldOfView = 60f;
        public const float DefaultNearClip = 0.02f;

        [SerializeField] float _eyeHeight = DefaultEyeHeight;
        [SerializeField] float _yawLimit = DefaultYawLimit;
        [SerializeField] float _pitchLimit = DefaultPitchLimit;
        [SerializeField] float _fieldOfView = DefaultFieldOfView;
        [SerializeField] float _nearClip = DefaultNearClip;
        [SerializeField] float _farClip = 20f;
        [SerializeField] Transform _headPivot;
        [SerializeField] Camera _eye;
        [SerializeField] MonoBehaviour _headPoseBehaviour;

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

        /// <summary>Look-down that puts the terrarium desk (0.30 m below the eye, 0.40 m ahead) in the centre of the view.</summary>
        public static float DefaultSeatedPitch =>
            Mathf.Atan2(PcDeskAnchor.BelowEye, PcDeskAnchor.TerrariumDistance) * Mathf.Rad2Deg;

        public static Quaternion SeatedPitchQuaternion => Quaternion.Euler(DefaultSeatedPitch, 0f, 0f);

        public void Bind(Transform headPivot, Camera eye, MonoBehaviour headPose)
        {
            _headPivot = headPivot;
            _eye = eye;
            SetHeadSource(headPose);
            ApplyBody();
            ApplyPose();
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

        /// <summary>Read the head source, clamp yaw and pitch, and write the pivot. LateUpdate calls this.</summary>
        public void ApplyPose()
        {
            Quaternion local = _source != null ? _source.LocalRotation : Quaternion.identity;
            Quaternion clamped = ClampLook(local, _yawLimit, _pitchLimit);
            ExtractLook(clamped, out float yaw, out float pitch);
            YawDegrees = yaw;
            PitchDegrees = pitch;
            if (_headPivot != null)
                _headPivot.localRotation = SeatedPitchQuaternion * clamped;
        }

        public void Recentre()
        {
            YawDegrees = 0f;
            PitchDegrees = 0f;
            if (_headPivot != null)
                _headPivot.localRotation = SeatedPitchQuaternion;
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
            ApplyBody();
        }

        void OnEnable()
        {
            ResolveHead();
            Subscribe();
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

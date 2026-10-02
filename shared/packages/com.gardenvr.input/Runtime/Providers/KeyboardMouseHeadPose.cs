using System;
using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>
    /// Seated head pose from the keyboard/mouse provider. Place this on a pivot above the eye camera
    /// so the camera keeps its own seated pitch. <see cref="LocalRotation"/> is the drag offset
    /// (identity when recentred). The pivot's rotation is the authored pose times that offset.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class KeyboardMouseHeadPose : MonoBehaviour, IHeadPoseSource
    {
        [SerializeField] KeyboardMouseIntentSource _source;
        Quaternion _authored = Quaternion.identity;

        public Quaternion LocalRotation => _source != null ? _source.HeadLocalRotation : Quaternion.identity;

        public event Action Recentred;

        void Awake()
        {
            Resolve();
            _authored = transform.localRotation;
        }

        void OnEnable()
        {
            Resolve();
            if (_source != null) _source.Recentred += HandleRecentred;
        }

        void OnDisable()
        {
            if (_source != null) _source.Recentred -= HandleRecentred;
        }

        void LateUpdate()
        {
            if (_source == null) return;
            transform.localRotation = _authored * _source.HeadLocalRotation;
        }

        void Resolve()
        {
            if (_source != null) return;
            _source = GetComponent<KeyboardMouseIntentSource>();
            if (_source == null) _source = GetComponentInParent<KeyboardMouseIntentSource>();
        }

        void HandleRecentred()
        {
            Recentred?.Invoke();
        }
    }
}

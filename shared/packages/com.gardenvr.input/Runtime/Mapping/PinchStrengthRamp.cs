using UnityEngine;

namespace GardenVR.Input
{
    /// <summary>
    /// Smoothstep pinch strength: 0 to 1 over 0.12 seconds on press, and 1 to 0 over 0.12 seconds on release.
    /// Engage 0.8 and release 0.5 both fall inside that window, so PinchDetector sees the same shape a hand gives.
    /// </summary>
    public sealed class PinchStrengthRamp
    {
        public const float Duration = 0.12f;

        float _from;
        float _to;
        float _elapsed;
        bool _moving;

        public float Value { get; private set; }

        public void Rise()
        {
            Begin(1f);
        }

        public void Fall()
        {
            Begin(0f);
        }

        public void Tick(float dt)
        {
            if (!_moving) return;
            if (dt < 0f) dt = 0f;
            _elapsed += dt;
            float u = Duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / Duration);
            Value = Mathf.Lerp(_from, _to, Smooth(u));
            if (u >= 1f)
            {
                Value = _to;
                _moving = false;
            }
        }

        /// <summary>Hermite smoothstep on 0..1.</summary>
        public static float Smooth(float t)
        {
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        void Begin(float to)
        {
            _from = Value;
            _to = to;
            _elapsed = 0f;
            _moving = Value != to;
        }
    }
}

using System;

namespace GardenVR.Core
{
    /// <summary>One hand-tracking reading: how closed the thumb-index pinch is, and whether the hand is tracked at all.</summary>
    public readonly struct PinchSample
    {
        public readonly float Strength; // 0 = open, 1 = fingertips touching
        public readonly bool Tracked;
        public PinchSample(float strength, bool tracked) { Strength = strength; Tracked = tracked; }
        public static PinchSample Lost => new PinchSample(0f, false);
    }

    public enum PinchState { Open, Held, Lost }

    /// <summary>
    /// Turns a noisy pinch-strength stream into Open / Held / Lost with hysteresis, so tracker jitter around one
    /// threshold never reads as a flurry of breaths, and a short tracking drop never ends a held inhale.
    /// </summary>
    public sealed class PinchDetector
    {
        public float EngageAt { get; }
        public float ReleaseAt { get; }
        public float LostGraceSeconds { get; }

        public PinchState State { get; private set; } = PinchState.Open;
        PinchState _lastTrackedState = PinchState.Open;
        float _lostFor;

        public PinchDetector(float engageAt = 0.8f, float releaseAt = 0.5f, float lostGraceSeconds = 0.4f)
        {
            if (releaseAt >= engageAt) throw new ArgumentException("releaseAt must be below engageAt (hysteresis)");
            EngageAt = engageAt; ReleaseAt = releaseAt; LostGraceSeconds = lostGraceSeconds;
        }

        public PinchState Update(float dt, PinchSample s)
        {
            if (!s.Tracked)
            {
                _lostFor += dt;
                State = _lostFor < LostGraceSeconds ? _lastTrackedState : PinchState.Lost;
                return State;
            }
            _lostFor = 0f;
            var prev = _lastTrackedState;
            var next = prev == PinchState.Held
                ? (s.Strength <= ReleaseAt ? PinchState.Open : PinchState.Held)
                : (s.Strength >= EngageAt ? PinchState.Held : PinchState.Open);
            _lastTrackedState = next;
            State = next;
            return State;
        }
    }
}

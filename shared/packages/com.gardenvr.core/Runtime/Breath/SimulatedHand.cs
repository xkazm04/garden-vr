using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// A scripted hand for tests, recordings and the headless scene: a list of (hold, release) breaths rendered as a
    /// noisy pinch-strength stream with optional tracking drop-outs. Deterministic for a given seed, so a failing
    /// run replays exactly. This is the "simulated hands" that drive the core with no headset and no simulator.
    /// </summary>
    public sealed class SimulatedHand
    {
        public readonly struct Segment
        {
            public readonly float Seconds; public readonly bool Pinched; public readonly bool Dropout;
            public Segment(float s, bool p, bool d = false) { Seconds = s; Pinched = p; Dropout = d; }
        }

        readonly List<Segment> _script = new List<Segment>();
        readonly Random _rng;
        public float Noise { get; set; } = 0.06f;
        public float RampSeconds { get; set; } = 0.18f;
        public float Duration { get; private set; }

        public SimulatedHand(int seed = 7) { _rng = new Random(seed); }

        public SimulatedHand Rest(float s) { Add(new Segment(s, false)); return this; }
        public SimulatedHand Hold(float s) { Add(new Segment(s, true)); return this; }
        public SimulatedHand Drop(float s, bool pinched = false) { Add(new Segment(s, pinched, true)); return this; }
        public SimulatedHand Breaths(int n, float inhale = 4f, float exhale = 4f)
        {
            for (int i = 0; i < n; i++) { Hold(inhale); Rest(exhale); }
            return this;
        }

        void Add(Segment s) { _script.Add(s); Duration += s.Seconds; }

        /// <summary>Pinch sample at time t. Strength ramps between open (~0.1) and closed (~0.95) with jitter.</summary>
        public PinchSample Sample(float t)
        {
            float start = 0f; Segment? prev = null;
            foreach (var seg in _script)
            {
                if (t < start + seg.Seconds)
                {
                    if (seg.Dropout) return PinchSample.Lost;
                    float local = t - start;
                    float from = prev.HasValue && prev.Value.Pinched ? 0.95f : 0.1f;
                    float to = seg.Pinched ? 0.95f : 0.1f;
                    float k = Math.Min(1f, local / RampSeconds);
                    float v = from + (to - from) * k + (float)((_rng.NextDouble() * 2 - 1) * Noise);
                    return new PinchSample(Math.Max(0f, Math.Min(1f, v)), true);
                }
                start += seg.Seconds; prev = seg;
            }
            return new PinchSample(0.1f, true);
        }

        /// <summary>Run a whole script through a session at a fixed step (default 72 Hz, the Quest display rate).</summary>
        public BreathSession Drive(BreathSession session, float hz = 72f, float extraSeconds = 2f)
        {
            float dt = 1f / hz;
            int steps = (int)Math.Ceiling((Duration + extraSeconds) * hz);
            for (int i = 0; i < steps; i++) session.Update(dt, Sample(i * dt));
            return session;
        }
    }
}

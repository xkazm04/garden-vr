using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    public enum BreathPhase { Waiting, Inhaling, Exhaling, Paused, Complete }

    public enum BreathEventKind { InhaleStarted, ExhaleStarted, BreathCounted, ShortInhaleIgnored, Paused, Resumed, RitualComplete }

    public readonly struct BreathEvent
    {
        public readonly BreathEventKind Kind; public readonly float Time; public readonly int Breaths;
        public BreathEvent(BreathEventKind k, float t, int b) { Kind = k; Time = t; Breaths = b; }
        public override string ToString() => $"{Time,6:0.00}s {Kind} (breaths={Breaths})";
    }

    public sealed class BreathConfig
    {
        public int TargetBreaths = 6;
        public float MinInhaleSeconds = 1.2f;   // shorter holds are fidgets, not breaths - ignored, never penalised
        public float IdealInhaleSeconds = 4.0f; // the frond finishes this breath's share of uncoil at this length
        public float MinExhaleSeconds = 1.0f;   // the breath counts once the release has lasted this long
        public float FogDecaySeconds = 2.5f;
    }

    /// <summary>
    /// Pinch-hold breathing ritual. Hold = inhale (the fiddlehead uncoils with you), release = exhale (the glass fogs).
    /// Pure: no engine types, driven by (dt, PinchSample), so Unity, a test and a recording all run the same rules.
    /// </summary>
    public sealed class BreathSession
    {
        readonly BreathConfig _c;
        readonly PinchDetector _pinch;
        public readonly List<BreathEvent> Events = new List<BreathEvent>();

        public BreathPhase Phase { get; private set; } = BreathPhase.Waiting;
        public int Breaths { get; private set; }
        public float Time { get; private set; }
        public float PhaseTime { get; private set; }
        /// <summary>0..1 - how far the frond has uncoiled. Rises while inhaling; never curls back past earned breaths.</summary>
        public float Uncoil { get; private set; }
        /// <summary>0..1 - condensation on the glass; peaks at each release and decays.</summary>
        public float Fog { get; private set; }
        public int TargetBreaths => _c.TargetBreaths;
        public float Progress => (float)Breaths / _c.TargetBreaths;
        public PinchState Pinch => _pinch.State;

        BreathPhase _beforePause;
        bool _exhaleCounted = true;

        public BreathSession(BreathConfig config = null, PinchDetector detector = null)
        {
            _c = config ?? new BreathConfig();
            _pinch = detector ?? new PinchDetector();
        }

        public void Update(float dt, PinchSample sample)
        {
            Time += dt; PhaseTime += dt;
            Fog = Math.Max(0f, Fog - dt / _c.FogDecaySeconds);
            if (Phase == BreathPhase.Complete) { Settle(dt); return; }

            var p = _pinch.Update(dt, sample);

            if (p == PinchState.Lost)
            {
                if (Phase != BreathPhase.Paused) { _beforePause = Phase; Enter(BreathPhase.Paused, BreathEventKind.Paused); }
                return; // nothing is lost while paused - the breath count and the frond hold still
            }
            if (Phase == BreathPhase.Paused)
            {
                // Resume into whatever the hand is doing now; an inhale cut by a long drop restarts cleanly.
                Emit(BreathEventKind.Resumed);
                Phase = p == PinchState.Held ? BreathPhase.Inhaling
                      : (_beforePause == BreathPhase.Waiting ? BreathPhase.Waiting : BreathPhase.Exhaling);
                PhaseTime = 0f; _exhaleCounted = true;
                if (Phase == BreathPhase.Inhaling) Emit(BreathEventKind.InhaleStarted);
            }

            switch (Phase)
            {
                case BreathPhase.Waiting:
                    if (p == PinchState.Held) Enter(BreathPhase.Inhaling, BreathEventKind.InhaleStarted);
                    Settle(dt);
                    break;

                case BreathPhase.Inhaling:
                    if (p == PinchState.Open)
                    {
                        if (PhaseTime < _c.MinInhaleSeconds)
                        {
                            Emit(BreathEventKind.ShortInhaleIgnored);
                            Phase = Breaths == 0 ? BreathPhase.Waiting : BreathPhase.Exhaling;
                            PhaseTime = 0f; _exhaleCounted = true; // a fidget never counts, never costs
                        }
                        else
                        {
                            Enter(BreathPhase.Exhaling, BreathEventKind.ExhaleStarted);
                            _exhaleCounted = false;
                            Fog = 1f;
                        }
                        break;
                    }
                    // Ease-out toward this breath's share of uncoil over the ideal inhale; holding longer never overshoots.
                    float x = Math.Min(1f, PhaseTime / _c.IdealInhaleSeconds);
                    float eased = 1f - (1f - x) * (1f - x);
                    float share = 1f / _c.TargetBreaths;
                    Uncoil = Math.Max(Uncoil, Progress + share * eased);
                    break;

                case BreathPhase.Exhaling:
                    if (!_exhaleCounted && PhaseTime >= _c.MinExhaleSeconds)
                    {
                        _exhaleCounted = true;
                        Breaths++;
                        Emit(BreathEventKind.BreathCounted);
                        if (Breaths >= _c.TargetBreaths) { Enter(BreathPhase.Complete, BreathEventKind.RitualComplete); break; }
                    }
                    if (p == PinchState.Held) { Enter(BreathPhase.Inhaling, BreathEventKind.InhaleStarted); break; }
                    Settle(dt);
                    break;
            }
        }

        void Settle(float dt)
        {
            // Settle toward what has been earned. A released breath that was long enough keeps its uncoil until it counts.
            float target = Phase == BreathPhase.Complete ? 1f : Progress;
            if (Phase == BreathPhase.Exhaling && !_exhaleCounted) return;
            if (Uncoil > target) Uncoil = Math.Max(target, Uncoil - dt * 0.15f);
            else if (Uncoil < target) Uncoil = Math.Min(target, Uncoil + dt * 0.5f);
        }

        void Enter(BreathPhase phase, BreathEventKind kind) { Phase = phase; PhaseTime = 0f; Emit(kind); }
        void Emit(BreathEventKind k) => Events.Add(new BreathEvent(k, Time, Breaths));
    }
}

using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    public enum BreathPhase { Waiting, Inhaling, Exhaling, Paused, Complete, HoldingFull, HoldingEmpty }

    public enum BreathEventKind { InhaleStarted, ExhaleStarted, BreathCounted, ShortInhaleIgnored, Paused, Resumed, RitualComplete, HoldFullStarted, HoldEmptyStarted }

    public readonly struct BreathEvent
    {
        public readonly BreathEventKind Kind; public readonly float Time; public readonly int Breaths;
        public BreathEvent(BreathEventKind k, float t, int b) { Kind = k; Time = t; Breaths = b; }
        public override string ToString() => $"{Time,6:0.00}s {Kind} (breaths={Breaths})";
    }

    public sealed class BreathConfig
    {
        /// <summary>One side of the 4-4-4-4 pace. Inhale, hold full, exhale, hold empty.</summary>
        public const float BoxSideSeconds = 4f;

        public int TargetBreaths = 6;
        public float MinInhaleSeconds = 1.2f;   // shorter holds are fidgets, not breaths - ignored, never penalised
        public float IdealInhaleSeconds = 4.0f; // the frond finishes this breath's share of uncoil at this length
        public float MinExhaleSeconds = 1.0f;   // the breath counts once the release has lasted this long
        public float FogDecaySeconds = 2.5f;
        /// <summary>0 keeps the free pace: release after the inhale goes straight to the exhale. Above 0, a pinch that is still held when the inhale side ends enters <see cref="BreathPhase.HoldingFull"/>.</summary>
        public float HoldFullSeconds = 0f;
        /// <summary>Length of the open side after the exhale. 0 skips it. The breath still counts at <see cref="MinExhaleSeconds"/>.</summary>
        public float HoldEmptySeconds = 0f;
        /// <summary>How long the exhale side lasts before the empty hold. 0 skips that gate.</summary>
        public float IdealExhaleSeconds = 0f;

        /// <summary>Four equal sides. The free-pace defaults for fidgets and fog stay.</summary>
        public static BreathConfig Box(int targetBreaths)
        {
            return new BreathConfig
            {
                TargetBreaths = targetBreaths,
                IdealInhaleSeconds = BoxSideSeconds,
                HoldFullSeconds = BoxSideSeconds,
                IdealExhaleSeconds = BoxSideSeconds,
                HoldEmptySeconds = BoxSideSeconds
            };
        }
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
                // A held-full beat resumes as itself, so the pinch does not start a second inhale.
                Emit(BreathEventKind.Resumed);
                if (p == PinchState.Held)
                    Phase = _beforePause == BreathPhase.HoldingFull ? BreathPhase.HoldingFull : BreathPhase.Inhaling;
                else if (_beforePause == BreathPhase.Waiting)
                    Phase = BreathPhase.Waiting;
                else if (_beforePause == BreathPhase.HoldingEmpty)
                    Phase = BreathPhase.HoldingEmpty;
                else
                    Phase = BreathPhase.Exhaling;
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
                    // The pinch stays down. The held-full beat is the next side, not a new inhale.
                    if (_c.HoldFullSeconds > 0f && PhaseTime >= _c.IdealInhaleSeconds)
                        Enter(BreathPhase.HoldingFull, BreathEventKind.HoldFullStarted);
                    break;

                case BreathPhase.HoldingFull:
                    if (p == PinchState.Open)
                    {
                        Enter(BreathPhase.Exhaling, BreathEventKind.ExhaleStarted);
                        _exhaleCounted = false;
                        Fog = 1f;
                    }
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
                    if (_c.HoldEmptySeconds > 0f && _c.IdealExhaleSeconds > 0f && _exhaleCounted && PhaseTime >= _c.IdealExhaleSeconds)
                    {
                        Enter(BreathPhase.HoldingEmpty, BreathEventKind.HoldEmptyStarted);
                        break;
                    }
                    Settle(dt);
                    break;

                case BreathPhase.HoldingEmpty:
                    if (p == PinchState.Held) { Enter(BreathPhase.Inhaling, BreathEventKind.InhaleStarted); break; }
                    if (PhaseTime >= _c.HoldEmptySeconds)
                    {
                        Phase = BreathPhase.Waiting;
                        PhaseTime = 0f;
                    }
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

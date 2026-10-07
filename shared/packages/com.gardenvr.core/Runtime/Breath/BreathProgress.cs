using System;

namespace GardenVR.Core
{
    /// <summary>Progress read-outs over a <see cref="BreathSession"/>, for the ring and the box ring. Pure; the app only draws them.</summary>
    public static class BreathProgress
    {
        /// <summary>
        /// 0..1 around the breath-count ring: finished breaths plus the fraction of the current one, over the target.
        /// The fraction rises through the inhale, stays full through the held-full side and the first MinExhaleSeconds of the exhale, and is 0 otherwise.
        /// </summary>
        public static float Ring(BreathSession session, BreathConfig config)
        {
            float fraction = 0f;
            if (session.Phase == BreathPhase.Inhaling)
                fraction = Clamp01(session.PhaseTime / Math.Max(0.01f, config.IdealInhaleSeconds));
            else if (session.Phase == BreathPhase.HoldingFull)
                fraction = 1f;
            else if (session.Phase == BreathPhase.Exhaling && session.PhaseTime < config.MinExhaleSeconds)
                fraction = 1f;
            float target = Math.Max(1, session.TargetBreaths);
            return Clamp01((session.Breaths + fraction) / target);
        }

        /// <summary>0..1 around the box ring. Each of the four sides is one quarter; Complete is 1, Waiting and Paused are 0.</summary>
        public static float Box(BreathSession session)
        {
            float side = Math.Max(0.01f, BreathConfig.BoxSideSeconds);
            float u = Clamp01(session.PhaseTime / side);
            switch (session.Phase)
            {
                case BreathPhase.Inhaling: return 0.25f * u;
                case BreathPhase.HoldingFull: return 0.25f + 0.25f * u;
                case BreathPhase.Exhaling: return 0.50f + 0.25f * u;
                case BreathPhase.HoldingEmpty: return 0.75f + 0.25f * u;
                case BreathPhase.Complete: return 1f;
                default: return 0f;
            }
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}

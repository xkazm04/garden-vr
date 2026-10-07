namespace GardenVR.Core
{
    /// <summary>
    /// The auto-pace hand: whether a pinch is held at a given time on the pace clock. Pure, so the app asks and does not
    /// compute. Box pace holds for the first two of four 4 s sides (a duty of one half). The free pace holds for the
    /// inhale and rests for the exhale. Inhale or exhale seconds below 1.2 fall back to 4 and 6.
    /// </summary>
    public static class PacedHand
    {
        public const float DefaultInhaleSeconds = 4f;
        public const float DefaultExhaleSeconds = 6f;
        const double MinPaceSeconds = 1.2d;

        public static float InhaleSeconds(RitualSettings s) =>
            s != null && s.InhaleSec >= MinPaceSeconds ? (float)s.InhaleSec : DefaultInhaleSeconds;

        public static float ExhaleSeconds(RitualSettings s) =>
            s != null && s.ExhaleSec >= MinPaceSeconds ? (float)s.ExhaleSec : DefaultExhaleSeconds;

        /// <summary>One full pace cycle in seconds.</summary>
        public static float Period(RitualSettings s) =>
            s != null && s.BoxPace ? BreathConfig.BoxSideSeconds * 4f : InhaleSeconds(s) + ExhaleSeconds(s);

        /// <summary>The share of a cycle the pinch is held: one half for the box, inhale / (inhale + exhale) otherwise.</summary>
        public static float Duty(RitualSettings s) =>
            s != null && s.BoxPace ? 0.5f : InhaleSeconds(s) / (InhaleSeconds(s) + ExhaleSeconds(s));

        /// <summary>True when the paced hand is pinching at <paramref name="clock"/> seconds into the pace.</summary>
        public static bool Held(float clock, RitualSettings s)
        {
            float period = Period(s);
            float along = period <= 0f ? 0f : clock % period;
            float heldFor = s != null && s.BoxPace ? BreathConfig.BoxSideSeconds * 2f : InhaleSeconds(s);
            return along < heldFor;
        }

        /// <summary>The pinch sample the app feeds the session for the paced hand: 0.95 held, 0.05 open, always tracked.</summary>
        public static PinchSample Sample(float clock, RitualSettings s) =>
            new PinchSample(Held(clock, s) ? 0.95f : 0.05f, true);
    }
}

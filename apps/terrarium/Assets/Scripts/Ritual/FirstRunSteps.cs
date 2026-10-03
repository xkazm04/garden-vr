namespace GardenVR.Terrarium
{
    /// <summary>
    /// Stable ids for the first-run tour. A later id is further along. The save stores the furthest id reached.
    /// </summary>
    public static class FirstRunSteps
    {
        public const string Room = "tour.room";
        public const string Desk = "tour.desk";
        public const string Arrive = "tour.arrive";
        public const string Hold = "tour.hold";
        public const string Release = "tour.release";
        public const string Breathe = "tour.breathe";
        public const string Answer = "tour.answer";
        public const string Seeds = "tour.seeds";
        public const string Voice = "tour.voice";
        public const string Done = "tour.done";

        public static readonly string[] Order =
        {
            Room, Desk, Arrive, Hold, Release, Breathe, Answer, Seeds, Voice, Done
        };

        public static int IndexOf(string step)
        {
            if (string.IsNullOrEmpty(step)) return -1;
            for (int i = 0; i < Order.Length; i++)
            {
                if (Order[i] == step) return i;
            }
            return -1;
        }

        public static bool AtLeast(string step, string marker)
        {
            int have = IndexOf(step);
            int need = IndexOf(marker);
            return have >= 0 && need >= 0 && have >= need;
        }

        /// <summary>
        /// The tour starts on a fresh garden. A saved unfinished step resumes.
        /// A failed load is never a first run. A finished tour, or a garden with no step, is a normal evening.
        /// </summary>
        public static bool ShouldRun(GardenVR.Core.LoadOutcome outcome, string step)
        {
            if (outcome == GardenVR.Core.LoadOutcome.Failed) return false;
            if (outcome == GardenVR.Core.LoadOutcome.Fresh) return true;
            int index = IndexOf(step);
            return index >= 0 && step != Done;
        }
    }
}

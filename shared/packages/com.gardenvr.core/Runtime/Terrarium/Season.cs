using System;

namespace GardenVR.Core
{
    /// <summary>
    /// The jar's season is lifetime fronds and nothing else. A missed week adds no frond, so the
    /// season pauses. Nothing here reads the calendar, and nothing falls when a day is missed.
    /// Week 1 (fronds 0 through 6) is the locked cool mint. Warmth then rises with each new frond
    /// and holds at 1 from the first frond of week 6.
    /// </summary>
    public static class Season
    {
        public const int DaysInWeek = 7;
        /// <summary>Warmth is 0 for every frond in this week. That is the locked look.</summary>
        public const int LockedWeek = 1;
        /// <summary>Warmth reaches 1 at this week and does not rise further.</summary>
        public const int GoldWeek = 6;
        /// <summary>A second species and the first tiny flower appear together, and stay.</summary>
        public const int DetailWeek = 3;

        /// <summary>1 at zero fronds. Advances every <see cref="DaysInWeek"/> lifetime fronds. Never falls as the count rises.</summary>
        public static int Week(int lifetimeFronds)
        {
            if (lifetimeFronds < 0)
                throw new ArgumentOutOfRangeException(nameof(lifetimeFronds), "lifetime fronds cannot fall below zero");
            return 1 + lifetimeFronds / DaysInWeek;
        }

        /// <summary>
        /// 0 through the end of week 1, then a straight rise to 1 at the first frond of week 6.
        /// Later fronds stay at 1.
        /// </summary>
        public static float Warmth(int lifetimeFronds)
        {
            int fronds = lifetimeFronds;
            Week(fronds);
            int start = DaysInWeek;
            int end = (GoldWeek - 1) * DaysInWeek;
            if (fronds <= start) return 0f;
            if (fronds >= end) return 1f;
            return (fronds - start) / (float)(end - start);
        }

        public static bool SecondSpecies(int lifetimeFronds)
        {
            return Week(lifetimeFronds) >= DetailWeek;
        }

        /// <summary>0 before week 3, then one new blossom each week. The count only rises.</summary>
        public static int TinyFlowers(int lifetimeFronds)
        {
            int week = Week(lifetimeFronds);
            if (week < DetailWeek) return 0;
            return week - (DetailWeek - 1);
        }

        /// <summary>0 before week 3, one sprig of the second species from week 3, two from week 6.</summary>
        public static int Sprigs(int lifetimeFronds)
        {
            int week = Week(lifetimeFronds);
            if (week < DetailWeek) return 0;
            if (week < GoldWeek) return 1;
            return 2;
        }

        /// <summary>First lifetime-frond count of <paramref name="week"/>. Week 1 is 0.</summary>
        public static int FrondsAtWeek(int week)
        {
            if (week < 1)
                throw new ArgumentOutOfRangeException(nameof(week), "a season week starts at 1");
            return (week - 1) * DaysInWeek;
        }

        /// <summary>Last lifetime-frond count that still belongs to <paramref name="week"/>. Week 1 is 6.</summary>
        public static int FrondsAtEndOfWeek(int week)
        {
            if (week < 1)
                throw new ArgumentOutOfRangeException(nameof(week), "a season week starts at 1");
            return week * DaysInWeek - 1;
        }
    }
}

using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// Where a rim drag is reading. The ledger is not part of this value.
    /// <see cref="DaysBack"/> is 0 for today and 6 at the oldest day of the window.
    /// <see cref="Minute"/> is the clock on that day, minutes after local midnight.
    /// </summary>
    public readonly struct RimRead
    {
        public readonly int DaysBack;
        public readonly int Minute;
        public readonly float GnomonDeg;
        public readonly int RewindMinutes;
        public readonly bool AtNow;

        public RimRead(int daysBack, int minute, float gnomonDeg, int rewindMinutes, bool atNow)
        {
            DaysBack = daysBack;
            Minute = minute;
            GnomonDeg = gnomonDeg;
            RewindMinutes = rewindMinutes;
            AtNow = atNow;
        }

        /// <summary>
        /// Empty at the current minute. "Earlier today", "Yesterday", or "Earlier this week".
        /// No digits and no run count.
        /// </summary>
        public string Caption
        {
            get { return RimScrub.CaptionFor(DaysBack, AtNow); }
        }
    }

    /// <summary>
    /// Pinch-drag along the rim reads earlier today or this week. It does not write.
    /// The gnomon moves 15 degrees per hour, so one degree of backward drag is four minutes
    /// and the shadow can follow the hand. Forward drag stays at now.
    /// The far end is 03:00 on the day six days before today, the start of the seven-day window.
    /// </summary>
    public static class RimScrub
    {
        public const int WeekDays = 7;
        public const float DegreesPerHour = 15f;
        public const float MinutesPerDegree = 4f;

        public const string EarlierToday = "Earlier today";
        public const string Yesterday = "Yesterday";
        public const string EarlierWeek = "Earlier this week";

        /// <summary>A scrub, including the settle back to now, accepts no writes.</summary>
        public static bool AllowsWrite(bool scrubbing)
        {
            return !scrubbing;
        }

        public static string CaptionFor(int daysBack, bool atNow)
        {
            if (atNow || daysBack < 0) return "";
            if (daysBack == 0) return EarlierToday;
            if (daysBack == 1) return Yesterday;
            return EarlierWeek;
        }

        /// <summary>Minutes from 03:00 on this garden day to <paramref name="nowMin"/>.</summary>
        public static int MinutesAfterStart(int nowMin)
        {
            int minute = Normalize(nowMin);
            if (minute >= SundialRules.DayBoundaryMin)
                return minute - SundialRules.DayBoundaryMin;
            return (SundialRules.MinutesPerDay - SundialRules.DayBoundaryMin) + minute;
        }

        /// <summary>How far back the rim can go from <paramref name="nowMin"/>, in minutes.</summary>
        public static int MaxRewindMinutes(int nowMin)
        {
            return (WeekDays - 1) * SundialRules.MinutesPerDay + MinutesAfterStart(nowMin);
        }

        /// <summary>
        /// <paramref name="rewindDegrees"/> is how far the hand has moved backward along the rim.
        /// Zero, negative, and non-finite values read now.
        /// </summary>
        public static RimRead FromDrag(int nowMin, float rewindDegrees)
        {
            int now = Normalize(nowMin);
            int back = RewindMinutes(rewindDegrees);
            int max = MaxRewindMinutes(now);
            if (back > max) back = max;
            if (back <= 0)
                return Make(0, now, 0, true);

            int after = MinutesAfterStart(now) - back;
            int days = 0;
            int span = SundialRules.MinutesPerDay;
            int limit = WeekDays - 1;
            while (after < 0 && days < limit)
            {
                after += span;
                days++;
            }
            if (after < 0) after = 0;
            int minute = SundialRules.DayBoundaryMin + after;
            if (minute >= span) minute -= span;
            return Make(days, minute, back, false);
        }

        /// <summary>
        /// The dial as it read at <paramref name="read"/>. The window ends on that day.
        /// Due and backfill are false, so the picture cannot be tended.
        /// Nothing in <paramref name="ledger"/> is added or undone.
        /// </summary>
        public static SundialState Query(IReadOnlyList<HabitDef> habits, Ledger ledger, GardenDay today, RimRead read)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            var day = new GardenDay(today.Index - read.DaysBack);
            SundialState state = SundialState.Capture(habits, ledger, day, read.Minute);
            state.Now = read.Minute;
            state.Arc = SundialRules.ArcAt(read.Minute);
            state.GnomonDeg = read.GnomonDeg;
            if (state.Plants == null) return state;
            for (int i = 0; i < state.Plants.Count; i++)
            {
                PlantState plant = state.Plants[i];
                if (plant == null) continue;
                plant.DueNow = false;
                plant.CanBackfillYesterday = false;
            }
            return state;
        }

        public static string ToJson(RimRead read)
        {
            var root = new JsonObject();
            root.Set("daysBack", JsonValue.Number(read.DaysBack));
            root.Set("minute", JsonValue.Number(read.Minute));
            root.Set("gnomonDeg", JsonValue.Number((double)read.GnomonDeg));
            root.Set("rewindMinutes", JsonValue.Number(read.RewindMinutes));
            root.Set("atNow", JsonValue.Bool(read.AtNow));
            root.Set("caption", JsonValue.String(read.Caption ?? ""));
            return Json.Write(root);
        }

        static RimRead Make(int daysBack, int minute, int rewindMinutes, bool atNow)
        {
            return new RimRead(daysBack, minute, SundialRules.GnomonAngleDeg(minute), rewindMinutes, atNow);
        }

        static int RewindMinutes(float rewindDegrees)
        {
            if (float.IsNaN(rewindDegrees) || float.IsInfinity(rewindDegrees) || rewindDegrees <= 0f)
                return 0;
            double raw = (double)rewindDegrees * MinutesPerDegree;
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw <= 0d) return 0;
            if (raw > int.MaxValue) return int.MaxValue;
            return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        }

        static int Normalize(int nowMin)
        {
            int minute = nowMin % SundialRules.MinutesPerDay;
            if (minute < 0) minute += SundialRules.MinutesPerDay;
            return minute;
        }
    }
}

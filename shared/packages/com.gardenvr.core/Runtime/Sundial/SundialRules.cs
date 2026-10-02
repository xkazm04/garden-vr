using System;

namespace GardenVR.Core
{
    /// <summary>
    /// What the dial shows. Pure C#, no engine. The clock argument is minutes after local midnight.
    /// The garden day still ends at 03:00, so the minutes before that boundary stay in wind-down.
    /// </summary>
    public static partial class SundialRules
    {
        public const int MinutesPerDay = 24 * 60;

        /// <summary>03:00. Same boundary as <see cref="GardenDay"/>. Minutes before it still belong to wind-down.</summary>
        public static readonly int DayBoundaryMin = (int)GardenDay.DefaultBoundary.TotalMinutes;

        /// <summary>
        /// Morning 06:00-11:00, midday 11:00-18:00, wind-down 18:00-03:00 the next calendar day (PLAN D4).
        /// </summary>
        public static readonly ArcDef[] Arcs = new ArcDef[]
        {
            new ArcDef { Id = ArcId.Morning, StartMin = 6 * 60, EndMin = 11 * 60 },
            new ArcDef { Id = ArcId.Midday, StartMin = 11 * 60, EndMin = 18 * 60 },
            new ArcDef { Id = ArcId.WindDown, StartMin = 18 * 60, EndMin = MinutesPerDay + DayBoundaryMin },
        };

        /// <summary>
        /// Arc that owns <paramref name="nowMin"/>, or null after 03:00 and before 06:00.
        /// 00:00 through 02:59 are shifted onto the wind-down range that crosses midnight.
        /// </summary>
        public static ArcId? ArcAt(int nowMin)
        {
            int minute = nowMin;
            if (minute >= 0 && minute < DayBoundaryMin)
                minute += MinutesPerDay;
            for (int i = 0; i < Arcs.Length; i++)
            {
                var arc = Arcs[i];
                if (arc == null) continue;
                if (minute >= arc.StartMin && minute < arc.EndMin)
                    return arc.Id;
            }
            return null;
        }
    }
}

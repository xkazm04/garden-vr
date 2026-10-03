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

        /// <summary>06:00. The gnomon reads 0 here, and the morning arc opens.</summary>
        public const int MorningStartMin = 6 * 60;

        /// <summary>03:00. Same boundary as <see cref="GardenDay"/>. Minutes before it still belong to wind-down.</summary>
        public static readonly int DayBoundaryMin = (int)GardenDay.DefaultBoundary.TotalMinutes;

        /// <summary>
        /// Morning 06:00-11:00, midday 11:00-18:00, wind-down 18:00-03:00 the next calendar day (PLAN D4).
        /// </summary>
        public static readonly ArcDef[] Arcs = new ArcDef[]
        {
            new ArcDef { Id = ArcId.Morning, StartMin = MorningStartMin, EndMin = 11 * 60 },
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

        /// <summary>
        /// <paramref name="group"/> is the habit's arc key: morning, midday, or wind-down.
        /// Hyphens, underscores and spaces are ignored. The match is case insensitive.
        /// </summary>
        public static bool TryArc(string group, out ArcId arc)
        {
            arc = default(ArcId);
            if (string.IsNullOrEmpty(group)) return false;
            string key = group.Replace("-", "").Replace("_", "").Replace(" ", "");
            if (key.Equals("morning", StringComparison.OrdinalIgnoreCase)) { arc = ArcId.Morning; return true; }
            if (key.Equals("midday", StringComparison.OrdinalIgnoreCase)) { arc = ArcId.Midday; return true; }
            if (key.Equals("winddown", StringComparison.OrdinalIgnoreCase)) { arc = ArcId.WindDown; return true; }
            return false;
        }

        /// <summary>
        /// The plant as of <paramref name="today"/>. A missed day adds nothing.
        /// Stage follows <see cref="PlantState.LifetimeKept"/> and bloom follows <see cref="PlantState.WindowKept"/>.
        /// Days before <see cref="HabitDef.CreatedDay"/> are <see cref="TileState.Before"/>.
        /// </summary>
        public static PlantState Plant(HabitDef habit, Ledger ledger, GardenDay today, int nowMin)
        {
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (string.IsNullOrEmpty(habit.Id)) throw new ArgumentException("habit id");

            var window = new TileState[7];
            int windowKept = 0;
            for (int i = 0; i < window.Length; i++)
            {
                int day = today.Index - 6 + i;
                TileState tile = TileOn(habit, ledger, day, today.Index);
                window[i] = tile;
                if (tile == TileState.Kept || tile == TileState.Late) windowKept++;
            }

            int lifetime = LifetimeKept(ledger, habit, today);
            ArcId arc;
            var state = new PlantState();
            state.HabitId = habit.Id;
            state.Window = window;
            state.WindowKept = windowKept;
            state.LifetimeKept = lifetime;
            state.Stage = StageFor(lifetime);
            state.Bloom = BloomFor(windowKept);
            state.DueNow = window[6] == TileState.Today && TryArc(habit.Group, out arc) && ArcAt(nowMin) == arc;
            int yesterday = today.Index - 1;
            state.CanBackfillYesterday = yesterday >= habit.CreatedDay && !ledger.IsKept(habit.Id, yesterday);
            return state;
        }

        /// <summary>
        /// One day of the record. A day before the habit existed is blank.
        /// A live tend is kept, or late when it was written afterwards.
        /// Today with no tend is still open. Any earlier open day is a quiet miss.
        /// A miss does not change any other day.
        /// </summary>
        public static TileState TileOn(HabitDef habit, Ledger ledger, int day, int todayIndex)
        {
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (day < habit.CreatedDay) return TileState.Before;
            TendEvent live = Live(ledger, habit.Id, day);
            if (live != null) return live.Late ? TileState.Late : TileState.Kept;
            if (day == todayIndex) return TileState.Today;
            if (day > todayIndex) return TileState.Before;
            return TileState.Missed;
        }

        /// <summary>15 degrees per hour, 0 at 06:00. The angle wraps every full turn.</summary>
        public static float GnomonAngleDeg(int nowMin)
        {
            float deg = (nowMin - MorningStartMin) * 0.25f;
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return deg;
        }

        /// <summary>
        /// Logs yesterday once, marked late, with the clock's real timestamp.
        /// Refuses a yesterday that falls before the habit existed. Every other refusal comes from the ledger:
        /// not yesterday, already kept, after today's boundary, or a future day.
        /// </summary>
        public static TendResult Backfill(HabitDef habit, Ledger ledger, GardenDay today, IClock clock)
        {
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (string.IsNullOrEmpty(habit.Id)) throw new ArgumentException("habit id");
            var yesterday = new GardenDay(today.Index - 1);
            if (yesterday.Index < habit.CreatedDay) return TendResult.Refused("before-created");
            return ledger.Backfill(habit.Id, yesterday, today, clock);
        }

        static int LifetimeKept(Ledger ledger, HabitDef habit, GardenDay today)
        {
            long span = (long)today.Index - habit.CreatedDay + 1;
            if (span < 1) return 0;
            if (span > int.MaxValue) span = int.MaxValue;
            return ledger.KeptDaysInWindow(habit.Id, today, (int)span);
        }

        static TendEvent Live(Ledger ledger, string habitId, int day)
        {
            var events = ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.HabitId == habitId && ev.Day == day && !ev.UndoneAtUtcMs.HasValue)
                    return ev;
            }
            return null;
        }
    }
}

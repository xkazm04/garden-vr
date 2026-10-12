using System;
using System.Collections.Generic;
using System.Globalization;

namespace GardenVR.Core
{
    /// <summary>The garden days of one civil month, by the civil date of each garden day.</summary>
    public readonly struct GardenMonth : IEquatable<GardenMonth>
    {
        public readonly GardenDay First;
        public readonly GardenDay Last;

        GardenMonth(GardenDay first, GardenDay last) { First = first; Last = last; }

        public static GardenMonth Of(GardenDay day)
        {
            DateTime civil = day.CivilDate;
            var start = new DateTime(civil.Year, civil.Month, 1);
            int first = day.Index - (civil - start).Days;
            int last = first + DateTime.DaysInMonth(civil.Year, civil.Month) - 1;
            return new GardenMonth(new GardenDay(first), new GardenDay(last));
        }

        public int Days { get { return Last.Index - First.Index + 1; } }
        public int Year { get { return First.CivilDate.Year; } }
        public int Month { get { return First.CivilDate.Month; } }

        public bool Contains(GardenDay day) { return day.Index >= First.Index && day.Index <= Last.Index; }

        public bool Equals(GardenMonth other) { return First.Index == other.First.Index; }
        public override bool Equals(object obj) { return obj is GardenMonth other && Equals(other); }
        public override int GetHashCode() { return First.Index; }
        public override string ToString() { return Year.ToString("D4", CultureInfo.InvariantCulture) + "-" + Month.ToString("D2", CultureInfo.InvariantCulture); }
    }

    /// <summary>
    /// The month's totals per habit (upgrade plan section 6.2): "kept 18 days this month". Every value here is a count
    /// of days or a share of planned days; none depends on the order of the days, so none can be a consecutive-day count.
    /// </summary>
    public static class MonthlyRecord
    {
        /// <summary>Distinct live kept days of <paramref name="habitId"/> inside <paramref name="month"/>.</summary>
        public static int KeptDays(Ledger ledger, string habitId, GardenMonth month)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId is null or empty", nameof(habitId));
            return ledger.KeptDaysInWindow(habitId, month.Last, month.Days);
        }

        /// <summary>
        /// The days of <paramref name="month"/> the habit was planned for, counted from the later of the month's start and
        /// the habit's creation, to the earliest of the month's end, <paramref name="today"/> and the day before it was
        /// archived. A times-a-week habit counts at most its N in each week.
        /// </summary>
        public static int PlannedDays(HabitDef habit, GardenMonth month, GardenDay today)
        {
            int from, to;
            if (!Range(habit, month, today, out from, out to)) return 0;
            HabitSchedule schedule = HabitProfiles.ScheduleOf(habit);
            int planned = 0;
            switch (schedule.Kind)
            {
                case ScheduleKind.Weekdays:
                    for (int d = from; d <= to; d++)
                        if (schedule.IsPlannedOn(new GardenDay(d))) planned++;
                    return planned;
                case ScheduleKind.TimesPerWeek:
                    for (int start = from; start <= to; )
                    {
                        int weekEnd = HabitSchedule.WeekStart(new GardenDay(start)).Index + 6;
                        int end = Math.Min(weekEnd, to);
                        planned += Math.Min(schedule.TimesPerWeek, end - start + 1);
                        start = end + 1;
                    }
                    return planned;
                default:
                    return to - from + 1;
            }
        }

        /// <summary>
        /// Kept days that count toward the plan: kept days in the same range as <see cref="PlannedDays"/>, at most the
        /// planned days of each week for a times-a-week habit and at most the planned days in all. A day kept off the plan
        /// still shows as kept in <see cref="KeptDays"/>; it only cannot fill past the plan.
        /// </summary>
        public static int KeptTowardPlan(Ledger ledger, HabitDef habit, GardenMonth month, GardenDay today)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            int from, to;
            if (!Range(habit, month, today, out from, out to) || string.IsNullOrEmpty(habit.Id)) return 0;
            HabitSchedule schedule = HabitProfiles.ScheduleOf(habit);
            int kept = 0;
            for (int start = from; start <= to; )
            {
                int weekEnd = HabitSchedule.WeekStart(new GardenDay(start)).Index + 6;
                int end = Math.Min(weekEnd, to);
                int keptInWeek = 0;
                for (int d = start; d <= end; d++)
                    if (ledger.IsKept(habit.Id, d)) keptInWeek++;
                int cap = schedule.Kind == ScheduleKind.TimesPerWeek
                    ? Math.Min(schedule.TimesPerWeek, end - start + 1)
                    : end - start + 1;
                kept += Math.Min(keptInWeek, cap);
                start = end + 1;
            }
            return Math.Min(kept, PlannedDays(habit, month, today));
        }

        static bool Range(HabitDef habit, GardenMonth month, GardenDay today, out int from, out int to)
        {
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            from = Math.Max(month.First.Index, habit.CreatedDay);
            to = Math.Min(month.Last.Index, today.Index);
            if (habit.ArchivedDay.HasValue) to = Math.Min(to, habit.ArchivedDay.Value - 1);
            return from <= to;
        }
    }

    /// <summary>A zone's fill for the month: how much of its habits' plan was kept, shown as a level, never as red.</summary>
    public static class ZoneRecord
    {
        /// <summary>
        /// Kept toward plan divided by planned, over every habit in <paramref name="zone"/>, from 0 to 1. Null when no
        /// habit in the zone was planned on any day of the month so far.
        /// </summary>
        public static float? Fill(Ledger ledger, IReadOnlyList<HabitDef> habits, LifeZone zone, GardenMonth month, GardenDay today)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (habits == null) return null;
            int planned = 0, kept = 0;
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef habit = habits[i];
                if (habit == null || HabitProfiles.ZoneOf(habit) != zone) continue;
                planned += MonthlyRecord.PlannedDays(habit, month, today);
                kept += MonthlyRecord.KeptTowardPlan(ledger, habit, month, today);
            }
            if (planned == 0) return null;
            return Math.Min(1f, (float)kept / planned);
        }
    }

    /// <summary>One counted unit of a quantity habit. Append-only; undo sets <see cref="UndoneAtUtcMs"/>.</summary>
    public sealed class CountEvent
    {
        public string Id;
        public string HabitId;
        public int Day;
        public long AtUtcMs;
        public long? UndoneAtUtcMs;
    }

    /// <summary>The counts of quantity habits, kept beside the ledger. The ledger still holds one tend per kept day.</summary>
    public sealed class QuantityTally
    {
        readonly List<CountEvent> _events = new List<CountEvent>();
        int _seq;

        public QuantityTally() { }

        public QuantityTally(IEnumerable<CountEvent> events)
        {
            if (events == null) return;
            foreach (var e in events)
                if (e != null) _events.Add(e);
        }

        public IReadOnlyList<CountEvent> Events { get { return _events; } }

        /// <summary>Live counts of <paramref name="habitId"/> on <paramref name="day"/>.</summary>
        public int Count(string habitId, int day)
        {
            int n = 0;
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.HabitId == habitId && e.Day == day && !e.UndoneAtUtcMs.HasValue) n++;
            }
            return n;
        }

        /// <summary>Appends one count. Null for a day after the clock's own garden day.</summary>
        public CountEvent Add(string habitId, GardenDay day, IClock clock, TimeSpan boundary)
        {
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId is null or empty", nameof(habitId));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (day.Index > GardenDay.From(clock.Now, boundary).Index) return null;
            _seq++;
            var e = new CountEvent();
            e.Id = "count-" + clock.Now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
                + "-" + _seq.ToString(CultureInfo.InvariantCulture);
            e.HabitId = habitId;
            e.Day = day.Index;
            e.AtUtcMs = clock.Now.ToUnixTimeMilliseconds();
            _events.Add(e);
            return e;
        }

        /// <summary>Marks the count when the clock is still inside the window (inclusive). Default 6 seconds.</summary>
        public bool Undo(string countId, IClock clock, double windowSeconds = 6)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (windowSeconds < 0) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            if (string.IsNullOrEmpty(countId)) return false;
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.Id != countId) continue;
                if (e.UndoneAtUtcMs.HasValue) return false;
                long now = clock.Now.ToUnixTimeMilliseconds();
                if (now - e.AtUtcMs > (long)Math.Round(windowSeconds * 1000.0)) return false;
                e.UndoneAtUtcMs = now;
                return true;
            }
            return false;
        }
    }

    /// <summary>What one pinch on a quantity habit did.</summary>
    public sealed class QuantityStep
    {
        public CountEvent Count;
        public int Total;
        public int Target;
        /// <summary>The ledger tend made when this count reached the target; null otherwise.</summary>
        public TendResult Tend;
        public bool Reached { get { return Total >= Target; } }
    }

    /// <summary>
    /// A quantity habit (8 glasses, 20 pages) is kept on a day when its counts that day reach its target. The count that
    /// reaches the target tends the ledger; later counts only add to the tally.
    /// </summary>
    public static class Quantity
    {
        /// <summary>Counts one unit. Null for a habit with no target, or a day after the clock's own garden day.</summary>
        public static QuantityStep Add(QuantityTally tally, Ledger ledger, HabitDef habit, GardenDay day, IClock clock, TimeSpan boundary)
        {
            if (tally == null) throw new ArgumentNullException(nameof(tally));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            int? target = HabitProfiles.CleanTarget(habit.Target);
            if (!target.HasValue) return null;
            CountEvent count = tally.Add(habit.Id, day, clock, boundary);
            if (count == null) return null;
            var step = new QuantityStep { Count = count, Total = tally.Count(habit.Id, day.Index), Target = target.Value };
            if (step.Total == target.Value && !ledger.IsKept(habit.Id, day.Index))
                step.Tend = ledger.Tend(habit.Id, day, TendSource.Pinch, clock, boundary);
            return step;
        }

        /// <summary>
        /// Undoes the count of <paramref name="step"/> inside the window, and the tend it made when the day drops back
        /// below the target. False when the window has passed.
        /// </summary>
        public static bool Undo(QuantityTally tally, Ledger ledger, QuantityStep step, IClock clock)
        {
            if (tally == null) throw new ArgumentNullException(nameof(tally));
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (step == null || step.Count == null) return false;
            if (!tally.Undo(step.Count.Id, clock)) return false;
            if (step.Tend != null && step.Tend.Ok && !step.Tend.AlreadyKept
                && tally.Count(step.Count.HabitId, step.Count.Day) < step.Target)
                ledger.Undo(step.Tend.Event.Id, clock);
            return true;
        }
    }

    /// <summary>The JSON codec for the "Counts" array of a save that holds quantity habits.</summary>
    public static class QuantityJson
    {
        public static List<CountEvent> ReadCounts(JsonObject root)
        {
            var counts = new List<CountEvent>();
            if (root == null || !root.Has("Counts") || root.Get("Counts").Kind != JsonKind.Array) return counts;
            JsonArray rows = root.Get("Counts").AsArray();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind != JsonKind.Object) continue;
                JsonObject obj = rows[i].AsObject();
                var e = new CountEvent();
                e.Id = LedgerJson.StringMember(obj, "Id");
                e.HabitId = LedgerJson.StringMember(obj, "HabitId");
                e.Day = obj.Has("Day") ? obj.Get("Day").AsInt() : 0;
                e.AtUtcMs = obj.Has("AtUtcMs") ? obj.Get("AtUtcMs").AsLong() : 0L;
                if (obj.Has("UndoneAtUtcMs") && !obj.Get("UndoneAtUtcMs").IsNull)
                    e.UndoneAtUtcMs = obj.Get("UndoneAtUtcMs").AsLong();
                counts.Add(e);
            }
            return counts;
        }

        public static JsonArray WriteCounts(IReadOnlyList<CountEvent> counts)
        {
            var array = new JsonArray();
            if (counts == null) return array;
            for (int i = 0; i < counts.Count; i++)
            {
                CountEvent e = counts[i];
                if (e == null) continue;
                var obj = new JsonObject();
                obj.Set("Id", JsonValue.String(e.Id ?? ""));
                obj.Set("HabitId", JsonValue.String(e.HabitId ?? ""));
                obj.Set("Day", JsonValue.Number(e.Day));
                obj.Set("AtUtcMs", JsonValue.Number(e.AtUtcMs));
                obj.Set("UndoneAtUtcMs", e.UndoneAtUtcMs.HasValue ? JsonValue.Number(e.UndoneAtUtcMs.Value) : JsonValue.Null());
                array.Add(obj);
            }
            return array;
        }
    }
}

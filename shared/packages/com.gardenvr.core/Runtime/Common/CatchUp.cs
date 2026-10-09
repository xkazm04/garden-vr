using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>One habit-day the catch-up offers: a planned day since the last visit that was not kept.</summary>
    public sealed class CatchUpItem
    {
        public HabitDef Habit;
        public GardenDay Day;
    }

    /// <summary>
    /// "What happened since?" at the next visit (upgrade plan section 6.3). It lists the planned, unkept habit-days from
    /// the day after the last visit up to yesterday, at most <see cref="Depth"/> days back. Each one logged is a late
    /// tend, drawn late forever; skipping one changes nothing.
    /// </summary>
    public static class CatchUp
    {
        public const int DefaultDepth = 2;
        public const int MaxDepth = 7;

        /// <summary>The depth setting made safe: 1 to <see cref="MaxDepth"/>; anything else is <see cref="DefaultDepth"/>.</summary>
        public static int Depth(int setting)
        {
            return setting >= 1 && setting <= MaxDepth ? setting : DefaultDepth;
        }

        /// <summary>
        /// The items to offer, oldest day first, then in habit order. Empty on a first visit (no last visit), when the last
        /// visit was today or yesterday's items are all kept, and for archived habits or days before a habit existed. A
        /// weekday habit is offered only on its days; a times-a-week habit only while its week is still short of N.
        /// </summary>
        public static List<CatchUpItem> Pending(Ledger ledger, IReadOnlyList<HabitDef> habits, int? lastVisitDay, GardenDay today, int depthSetting)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            var items = new List<CatchUpItem>();
            if (habits == null || !lastVisitDay.HasValue) return items;
            int depth = Depth(depthSetting);
            int from = Math.Max(lastVisitDay.Value + 1, today.Index - depth);
            for (int d = from; d < today.Index; d++)
            {
                var day = new GardenDay(d);
                for (int i = 0; i < habits.Count; i++)
                {
                    HabitDef habit = habits[i];
                    if (habit == null || string.IsNullOrEmpty(habit.Id)) continue;
                    if (d < habit.CreatedDay || (habit.ArchivedDay.HasValue && d >= habit.ArchivedDay.Value)) continue;
                    if (ledger.IsKept(habit.Id, d)) continue;
                    if (!Offered(ledger, habit, day)) continue;
                    items.Add(new CatchUpItem { Habit = habit, Day = day });
                }
            }
            return items;
        }

        /// <summary>Logs one item. Refused outside the window, after today's boundary, or when the day is already kept.</summary>
        public static TendResult Log(Ledger ledger, CatchUpItem item, GardenDay today, int depthSetting, IClock clock, TimeSpan boundary)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (item == null || item.Habit == null) throw new ArgumentNullException(nameof(item));
            return ledger.BackfillWithin(item.Habit.Id, item.Day, today, Depth(depthSetting), clock, boundary);
        }

        static bool Offered(Ledger ledger, HabitDef habit, GardenDay day)
        {
            HabitSchedule schedule = HabitProfiles.ScheduleOf(habit);
            if (!schedule.IsPlannedOn(day)) return false;
            if (schedule.Kind != ScheduleKind.TimesPerWeek) return true;
            int start = HabitSchedule.WeekStart(day).Index;
            int kept = 0;
            for (int d = start; d < start + 7; d++)
                if (ledger.IsKept(habit.Id, d)) kept++;
            return kept < schedule.TimesPerWeek;
        }
    }
}

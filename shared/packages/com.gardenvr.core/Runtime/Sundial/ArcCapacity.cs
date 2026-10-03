using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    public static partial class SundialRules
    {
        /// <summary>An arc holds at most three live habits. The day dial then has nine plants and 63 tiles.</summary>
        public const int MaxHabitsPerArc = 3;

        public const int DaysPerHabit = 7;

        /// <summary>Three arcs, three rows, seven days. One combined tile draw.</summary>
        public const int MaxTiles = 3 * MaxHabitsPerArc * DaysPerHabit;

        /// <summary>
        /// Adds <paramref name="candidate"/> on the lowest free row of its arc.
        /// An archived habit does not hold a row. Returns null on success, or
        /// <c>habit-id</c>, <c>arc</c>, <c>duplicate</c>, or <c>arc-full</c>.
        /// </summary>
        public static string Admit(IList<HabitDef> habits, HabitDef candidate)
        {
            if (habits == null) throw new ArgumentNullException(nameof(habits));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (string.IsNullOrEmpty(candidate.Id)) return "habit-id";
            ArcId arc;
            if (!TryArc(candidate.Group, out arc)) return "arc";
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef existing = habits[i];
                if (existing == null) continue;
                if (ReferenceEquals(existing, candidate)) return "duplicate";
                if (!string.IsNullOrEmpty(existing.Id) && existing.Id == candidate.Id) return "duplicate";
            }
            bool[] used = UsedRows(habits, arc);
            int free = -1;
            for (int row = 0; row < MaxHabitsPerArc; row++)
            {
                if (!used[row]) { free = row; break; }
            }
            if (free < 0) return "arc-full";
            candidate.Row = free;
            candidate.Slot = free;
            habits.Add(candidate);
            return null;
        }

        /// <summary>Live habits whose group is this arc. Archived habits are not counted.</summary>
        public static int LiveInArc(IReadOnlyList<HabitDef> habits, string group)
        {
            ArcId arc;
            if (!TryArc(group, out arc) || habits == null) return 0;
            int count = 0;
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef habit = habits[i];
                if (!LiveIn(habit, arc)) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Row 0, 1 or 2 for a live habit. A lone habit is row 0 even when an older save
        /// stored the arc index in <see cref="HabitDef.Slot"/> and left <see cref="HabitDef.Row"/> at 0.
        /// Archived habits return -1. Duplicate row claims are broken by created day, then id.
        /// </summary>
        public static int RowOf(IReadOnlyList<HabitDef> habits, HabitDef habit)
        {
            if (habit == null || habit.ArchivedDay.HasValue) return -1;
            ArcId arc;
            if (!TryArc(habit.Group, out arc)) return -1;
            int claimed = NormalizedRow(habit);
            int claims = 0;
            if (habits != null)
            {
                for (int i = 0; i < habits.Count; i++)
                {
                    HabitDef other = habits[i];
                    if (!LiveIn(other, arc)) continue;
                    if (NormalizedRow(other) == claimed) claims++;
                }
            }
            if (claims <= 1) return claimed;
            var ordered = new List<HabitDef>();
            if (habits != null)
            {
                for (int i = 0; i < habits.Count; i++)
                {
                    if (LiveIn(habits[i], arc)) ordered.Add(habits[i]);
                }
            }
            ordered.Sort(ByAge);
            int found = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ReferenceEquals(ordered[i], habit) || ordered[i].Id == habit.Id)
                {
                    found = i;
                    break;
                }
            }
            if (found >= MaxHabitsPerArc) return MaxHabitsPerArc - 1;
            return found;
        }

        static bool[] UsedRows(IList<HabitDef> habits, ArcId arc)
        {
            var used = new bool[MaxHabitsPerArc];
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef habit = habits[i];
                if (!LiveIn(habit, arc)) continue;
                int row = NormalizedRow(habit);
                // Two live habits claiming one row still occupy distinct slots once RowOf splits them.
                if (!used[row]) used[row] = true;
                else
                {
                    for (int alt = 0; alt < MaxHabitsPerArc; alt++)
                    {
                        if (!used[alt]) { used[alt] = true; break; }
                    }
                }
            }
            return used;
        }

        static bool LiveIn(HabitDef habit, ArcId arc)
        {
            if (habit == null || habit.ArchivedDay.HasValue || string.IsNullOrEmpty(habit.Id)) return false;
            ArcId have;
            return TryArc(habit.Group, out have) && have == arc;
        }

        static int NormalizedRow(HabitDef habit)
        {
            int row = habit.Row;
            if (row < 0 || row >= MaxHabitsPerArc) return 0;
            return row;
        }

        static int ByAge(HabitDef a, HabitDef b)
        {
            int day = a.CreatedDay.CompareTo(b.CreatedDay);
            if (day != 0) return day;
            return string.CompareOrdinal(a.Id ?? "", b.Id ?? "");
        }
    }
}

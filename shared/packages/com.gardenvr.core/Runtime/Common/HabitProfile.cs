using System;
using System.Collections.Generic;
using System.Text;

namespace GardenVR.Core
{
    /// <summary>The four life areas a habit grows in. Each is a region of the garden with its own species family.</summary>
    public enum LifeZone { Body, Mind, Work, Connection }

    public enum ScheduleKind { Daily, TimesPerWeek, Weekdays }

    /// <summary>
    /// When a habit is meant to happen. A habit with no schedule is <see cref="ScheduleKind.Daily"/>.
    /// Weeks start on Monday of the garden day's civil date. Bit 0 of <see cref="WeekdayMask"/> is Monday, bit 6 Sunday.
    /// </summary>
    public sealed class HabitSchedule
    {
        public const byte AllWeekdays = 0x7F;
        public const byte WorkWeek = 0x1F;

        public ScheduleKind Kind;
        /// <summary>1 to 7, read only for <see cref="ScheduleKind.TimesPerWeek"/>.</summary>
        public int TimesPerWeek;
        /// <summary>Read only for <see cref="ScheduleKind.Weekdays"/>; at least one of the low seven bits is set.</summary>
        public byte WeekdayMask;

        public static HabitSchedule Daily() { return new HabitSchedule { Kind = ScheduleKind.Daily }; }

        public static HabitSchedule PerWeek(int times)
        {
            if (times < 1 || times > 7) throw new ArgumentOutOfRangeException(nameof(times), "times a week is 1 to 7");
            return new HabitSchedule { Kind = ScheduleKind.TimesPerWeek, TimesPerWeek = times };
        }

        public static HabitSchedule OnWeekdays(byte mask)
        {
            if ((mask & AllWeekdays) == 0 || (mask & ~AllWeekdays) != 0)
                throw new ArgumentOutOfRangeException(nameof(mask), "mask sets one or more of the low seven bits and no other");
            return new HabitSchedule { Kind = ScheduleKind.Weekdays, WeekdayMask = mask };
        }

        /// <summary>True when the fields describe a schedule the factories could have made.</summary>
        public bool IsValid
        {
            get
            {
                switch (Kind)
                {
                    case ScheduleKind.Daily: return true;
                    case ScheduleKind.TimesPerWeek: return TimesPerWeek >= 1 && TimesPerWeek <= 7;
                    case ScheduleKind.Weekdays: return (WeekdayMask & AllWeekdays) != 0 && (WeekdayMask & ~AllWeekdays) == 0;
                    default: return false;
                }
            }
        }

        /// <summary>
        /// Whether the habit may be kept on <paramref name="day"/> as part of its plan. Daily and times-a-week habits may be
        /// kept any day; a weekday habit only on its days. Keeping it on another day still counts as kept, never as wrong.
        /// </summary>
        public bool IsPlannedOn(GardenDay day)
        {
            switch (Kind)
            {
                case ScheduleKind.Weekdays: return (WeekdayMask & WeekdayBit(day)) != 0;
                default: return true;
            }
        }

        /// <summary>How many days of one week the habit is planned for.</summary>
        public int PlannedInWeek()
        {
            switch (Kind)
            {
                case ScheduleKind.TimesPerWeek: return TimesPerWeek;
                case ScheduleKind.Weekdays: return BitCount(WeekdayMask & AllWeekdays);
                default: return 7;
            }
        }

        /// <summary>Monday of the week <paramref name="day"/> falls in.</summary>
        public static GardenDay WeekStart(GardenDay day)
        {
            return new GardenDay(day.Index - MondayOffset(day));
        }

        /// <summary>The <see cref="WeekdayMask"/> bit of <paramref name="day"/>.</summary>
        public static byte WeekdayBit(GardenDay day)
        {
            return (byte)(1 << MondayOffset(day));
        }

        static int MondayOffset(GardenDay day)
        {
            // DayOfWeek: Sunday = 0. Monday becomes 0 and Sunday 6.
            return ((int)day.CivilDate.DayOfWeek + 6) % 7;
        }

        static int BitCount(int value)
        {
            int n = 0;
            while (value != 0) { n += value & 1; value >>= 1; }
            return n;
        }
    }

    /// <summary>The rules of a habit's own name, zone, schedule and quantity target (upgrade plan section 6.1).</summary>
    public static class HabitProfiles
    {
        /// <summary>At most this many live (not archived) habits.</summary>
        public const int MaxLive = 9;
        public const int MaxNameLength = 32;
        public const int MaxTarget = 99;

        static readonly Dictionary<string, LifeZone> PresetZones = new Dictionary<string, LifeZone>
        {
            { "walk", LifeZone.Body },
            { "water", LifeZone.Body },
            { "stretch", LifeZone.Body },
            { "early-night", LifeZone.Body },
            { "bed", LifeZone.Body },
            { "read", LifeZone.Mind },
            { "journal", LifeZone.Mind },
            { "breaths", LifeZone.Mind },
            { "phone", LifeZone.Mind },
            { "lunch", LifeZone.Mind },
            { "top3", LifeZone.Work }
        };

        /// <summary>The zone a preset grows in when the habit names none. False for an unknown or empty key.</summary>
        public static bool TryPresetZone(string presetKey, out LifeZone zone)
        {
            zone = LifeZone.Mind;
            return !string.IsNullOrEmpty(presetKey) && PresetZones.TryGetValue(presetKey, out zone);
        }

        /// <summary>The habit's own zone, else its preset's, else Mind.</summary>
        public static LifeZone ZoneOf(HabitDef habit)
        {
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            if (habit.Zone.HasValue) return habit.Zone.Value;
            LifeZone zone;
            return TryPresetZone(habit.PresetKey, out zone) ? zone : LifeZone.Mind;
        }

        /// <summary>The habit's schedule when it is set and valid, else daily.</summary>
        public static HabitSchedule ScheduleOf(HabitDef habit)
        {
            if (habit == null) throw new ArgumentNullException(nameof(habit));
            return habit.Schedule != null && habit.Schedule.IsValid ? habit.Schedule : HabitSchedule.Daily();
        }

        /// <summary>
        /// A spoken or typed name made safe to keep and show: control characters and dashes longer than a hyphen become
        /// spaces, runs of white space become one, the ends are trimmed, and it is cut to <see cref="MaxNameLength"/> at a
        /// word boundary when one exists. Null when nothing is left.
        /// </summary>
        public static string CleanName(string raw)
        {
            if (raw == null) return null;
            var sb = new StringBuilder(raw.Length);
            bool space = false;
            foreach (char c in raw)
            {
                bool blank = char.IsWhiteSpace(c) || char.IsControl(c) || c == '—' || c == '–' || c == '―';
                if (blank)
                {
                    if (sb.Length > 0) space = true;
                    continue;
                }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
            }
            string name = sb.ToString();
            if (name.Length > MaxNameLength)
            {
                int cut = name.LastIndexOf(' ', MaxNameLength);
                name = cut > 0 ? name.Substring(0, cut) : name.Substring(0, MaxNameLength);
            }
            return name.Length == 0 ? null : name;
        }

        /// <summary>A target is 2 to <see cref="MaxTarget"/>; anything else means a yes-or-no habit.</summary>
        public static int? CleanTarget(int? target)
        {
            return target.HasValue && target.Value >= 2 && target.Value <= MaxTarget ? target : null;
        }

        /// <summary>Live habits in <paramref name="habits"/>: those with no archived day.</summary>
        public static int LiveCount(IReadOnlyList<HabitDef> habits)
        {
            if (habits == null) return 0;
            int n = 0;
            for (int i = 0; i < habits.Count; i++)
                if (habits[i] != null && !habits[i].ArchivedDay.HasValue) n++;
            return n;
        }

        public static bool CanAdd(IReadOnlyList<HabitDef> habits)
        {
            return LiveCount(habits) < MaxLive;
        }
    }
}

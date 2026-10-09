using System;
using System.Collections.Generic;
using System.Globalization;

namespace GardenVR.Core
{
    /// <summary>
    /// The JSON codec for habit and tend rows, shared by every app save. Unknown members of a row are kept in
    /// a dictionary the save holds, keyed by the row Id, or by "#index" for a row with no Id, and written back.
    /// What differs between saves is passed in as <see cref="Choices"/>.
    /// </summary>
    public static class LedgerJson
    {
        /// <summary>The three rules the saves do not share.</summary>
        public struct Choices
        {
            /// <summary>Write a habit with no Group as "" instead of null.</summary>
            public bool NullGroupAsEmpty;
            /// <summary>The Source a tend takes when its member is absent, null or not a <see cref="TendSource"/> name.</summary>
            public TendSource SourceFallback;
            /// <summary>Row is a known member: read into <see cref="HabitDef.Row"/> and written after Slot. Otherwise it passes through as unknown.</summary>
            public bool RowKnown;
            /// <summary>
            /// Name, Zone, Schedule, Target and Unit are known members: read into the <see cref="HabitDef"/> fields and
            /// written after ArchivedDay, each only when set. Otherwise they pass through as unknown.
            /// </summary>
            public bool ProfileKnown;
        }

        static readonly string[] ProfileMembers = { "Name", "Zone", "Schedule", "Target", "Unit" };

        static readonly string[] HabitKnownWithoutRow =
        {
            "Id", "PresetKey", "Group", "Species", "Kind", "Slot", "CreatedDay", "ArchivedDay"
        };

        static readonly string[] HabitKnownWithRow =
        {
            "Id", "PresetKey", "Group", "Species", "Kind", "Slot", "Row", "CreatedDay", "ArchivedDay"
        };

        static readonly string[] TendKnownMembers =
        {
            "Id", "HabitId", "Tz", "Day", "AtUtcMs", "Source", "Late", "UndoneAtUtcMs"
        };

        /// <summary>The members a habit row reads. Any other member is kept as unknown.</summary>
        public static string[] HabitKnown(Choices choices)
        {
            return (string[])HabitKnownFor(choices).Clone();
        }

        static string[] HabitKnownFor(Choices choices)
        {
            string[] known = choices.RowKnown ? HabitKnownWithRow : HabitKnownWithoutRow;
            if (!choices.ProfileKnown) return known;
            var all = new string[known.Length + ProfileMembers.Length];
            known.CopyTo(all, 0);
            ProfileMembers.CopyTo(all, known.Length);
            return all;
        }

        /// <summary>The members a tend row reads. Any other member is kept as unknown.</summary>
        public static string[] TendKnown()
        {
            return (string[])TendKnownMembers.Clone();
        }

        public static HabitDef ReadHabit(JsonObject obj, int index, Dictionary<string, Dictionary<string, JsonValue>> extras, Choices choices)
        {
            var habit = new HabitDef();
            habit.Id = StringMember(obj, "Id");
            habit.PresetKey = StringMember(obj, "PresetKey");
            habit.Group = StringMember(obj, "Group");
            habit.Species = StringMember(obj, "Species");
            habit.Kind = EnumMember(obj, "Kind", HabitKind.LifeCheckIn);
            habit.Slot = obj.Has("Slot") ? obj.Get("Slot").AsInt() : 0;
            if (choices.RowKnown) habit.Row = obj.Has("Row") ? obj.Get("Row").AsInt() : 0;
            habit.CreatedDay = obj.Has("CreatedDay") ? obj.Get("CreatedDay").AsInt() : 0;
            if (obj.Has("ArchivedDay") && !obj.Get("ArchivedDay").IsNull)
                habit.ArchivedDay = obj.Get("ArchivedDay").AsInt();
            if (choices.ProfileKnown) ReadProfile(obj, habit);
            KeepExtra(obj, HabitKnownFor(choices), RowKey(habit.Id, index), extras);
            return habit;
        }

        public static JsonObject WriteHabit(HabitDef habit, int index, Dictionary<string, Dictionary<string, JsonValue>> extras, Choices choices)
        {
            if (habit == null) habit = new HabitDef();
            var obj = new JsonObject();
            obj.Set("Id", JsonValue.String(habit.Id ?? ""));
            obj.Set("PresetKey", JsonValue.String(habit.PresetKey ?? ""));
            if (habit.Group == null && !choices.NullGroupAsEmpty) obj.Set("Group", JsonValue.Null());
            else obj.Set("Group", JsonValue.String(habit.Group ?? ""));
            obj.Set("Species", habit.Species == null ? JsonValue.Null() : JsonValue.String(habit.Species));
            obj.Set("Kind", JsonValue.String(habit.Kind.ToString()));
            obj.Set("Slot", JsonValue.Number(habit.Slot));
            if (choices.RowKnown) obj.Set("Row", JsonValue.Number(habit.Row));
            obj.Set("CreatedDay", JsonValue.Number(habit.CreatedDay));
            obj.Set("ArchivedDay", habit.ArchivedDay.HasValue ? JsonValue.Number(habit.ArchivedDay.Value) : JsonValue.Null());
            if (choices.ProfileKnown) WriteProfile(obj, habit);
            RestoreExtra(obj, RowKey(habit.Id, index), extras);
            return obj;
        }

        // A member that cannot be read leaves its field unset: a habit falls back to its preset's zone, to daily, or to
        // yes or no. Nothing else in the row is affected.
        static void ReadProfile(JsonObject obj, HabitDef habit)
        {
            habit.Name = HabitProfiles.CleanName(StringMember(obj, "Name"));
            if (obj.Has("Zone") && obj.Get("Zone").Kind == JsonKind.String)
            {
                LifeZone zone;
                if (Enum.TryParse(obj.Get("Zone").AsString(), false, out zone) && Enum.IsDefined(typeof(LifeZone), zone))
                    habit.Zone = zone;
            }
            if (obj.Has("Schedule") && obj.Get("Schedule").Kind == JsonKind.Object)
                habit.Schedule = ReadSchedule(obj.Get("Schedule").AsObject());
            if (obj.Has("Target") && obj.Get("Target").Kind == JsonKind.Number)
            {
                double target = obj.Get("Target").AsDouble();
                if (target == Math.Floor(target) && target >= int.MinValue && target <= int.MaxValue)
                    habit.Target = HabitProfiles.CleanTarget((int)target);
            }
            habit.Unit = habit.Target.HasValue ? HabitProfiles.CleanName(StringMember(obj, "Unit")) : null;
        }

        static HabitSchedule ReadSchedule(JsonObject obj)
        {
            if (!obj.Has("Kind") || obj.Get("Kind").Kind != JsonKind.String) return null;
            ScheduleKind kind;
            if (!Enum.TryParse(obj.Get("Kind").AsString(), false, out kind) || !Enum.IsDefined(typeof(ScheduleKind), kind)) return null;
            var schedule = new HabitSchedule { Kind = kind };
            if (kind == ScheduleKind.TimesPerWeek && obj.Has("Times") && obj.Get("Times").Kind == JsonKind.Number)
                schedule.TimesPerWeek = (int)Math.Max(0, Math.Min(8, obj.Get("Times").AsDouble()));
            if (kind == ScheduleKind.Weekdays && obj.Has("Days") && obj.Get("Days").Kind == JsonKind.Number)
                schedule.WeekdayMask = (byte)Math.Max(0, Math.Min(255, obj.Get("Days").AsDouble()));
            return schedule.IsValid ? schedule : null;
        }

        static void WriteProfile(JsonObject obj, HabitDef habit)
        {
            string name = HabitProfiles.CleanName(habit.Name);
            if (name != null) obj.Set("Name", JsonValue.String(name));
            if (habit.Zone.HasValue && Enum.IsDefined(typeof(LifeZone), habit.Zone.Value))
                obj.Set("Zone", JsonValue.String(habit.Zone.Value.ToString()));
            if (habit.Schedule != null && habit.Schedule.IsValid)
            {
                var schedule = new JsonObject();
                schedule.Set("Kind", JsonValue.String(habit.Schedule.Kind.ToString()));
                if (habit.Schedule.Kind == ScheduleKind.TimesPerWeek) schedule.Set("Times", JsonValue.Number(habit.Schedule.TimesPerWeek));
                if (habit.Schedule.Kind == ScheduleKind.Weekdays) schedule.Set("Days", JsonValue.Number((int)habit.Schedule.WeekdayMask));
                obj.Set("Schedule", schedule);
            }
            int? target = HabitProfiles.CleanTarget(habit.Target);
            if (target.HasValue)
            {
                obj.Set("Target", JsonValue.Number(target.Value));
                string unit = HabitProfiles.CleanName(habit.Unit);
                if (unit != null) obj.Set("Unit", JsonValue.String(unit));
            }
        }

        public static TendEvent ReadTend(JsonObject obj, int index, Dictionary<string, Dictionary<string, JsonValue>> extras, Choices choices)
        {
            var tend = new TendEvent();
            tend.Id = StringMember(obj, "Id");
            tend.HabitId = StringMember(obj, "HabitId");
            tend.Tz = StringMember(obj, "Tz");
            tend.Day = obj.Has("Day") ? obj.Get("Day").AsInt() : 0;
            tend.AtUtcMs = obj.Has("AtUtcMs") ? obj.Get("AtUtcMs").AsLong() : 0L;
            tend.Source = EnumMember(obj, "Source", choices.SourceFallback);
            tend.Late = obj.Has("Late") && obj.Get("Late").AsBool();
            if (obj.Has("UndoneAtUtcMs") && !obj.Get("UndoneAtUtcMs").IsNull)
                tend.UndoneAtUtcMs = obj.Get("UndoneAtUtcMs").AsLong();
            KeepExtra(obj, TendKnownMembers, RowKey(tend.Id, index), extras);
            return tend;
        }

        public static JsonObject WriteTend(TendEvent tend, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            if (tend == null) tend = new TendEvent();
            var obj = new JsonObject();
            obj.Set("Id", JsonValue.String(tend.Id ?? ""));
            obj.Set("HabitId", JsonValue.String(tend.HabitId ?? ""));
            obj.Set("Tz", JsonValue.String(tend.Tz ?? ""));
            obj.Set("Day", JsonValue.Number(tend.Day));
            obj.Set("AtUtcMs", JsonValue.Number(tend.AtUtcMs));
            obj.Set("Source", JsonValue.String(tend.Source.ToString()));
            obj.Set("Late", JsonValue.Bool(tend.Late));
            obj.Set("UndoneAtUtcMs", tend.UndoneAtUtcMs.HasValue ? JsonValue.Number(tend.UndoneAtUtcMs.Value) : JsonValue.Null());
            RestoreExtra(obj, RowKey(tend.Id, index), extras);
            return obj;
        }

        /// <summary>Reads the "Habits" array of <paramref name="root"/>. A missing key or JSON null reads as an empty list.</summary>
        public static List<HabitDef> ReadHabits(JsonObject root, Dictionary<string, Dictionary<string, JsonValue>> extras, Choices choices)
        {
            var habits = new List<HabitDef>();
            if (root.Has("Habits") && !root.Get("Habits").IsNull)
            {
                JsonArray rows = root.Get("Habits").AsArray();
                for (int i = 0; i < rows.Count; i++)
                    habits.Add(ReadHabit(rows[i].AsObject(), i, extras, choices));
            }
            return habits;
        }

        /// <summary>Writes <paramref name="habits"/> as an array. A null list writes an empty array.</summary>
        public static JsonArray WriteHabits(List<HabitDef> habits, Dictionary<string, Dictionary<string, JsonValue>> extras, Choices choices)
        {
            var array = new JsonArray();
            if (habits != null)
            {
                for (int i = 0; i < habits.Count; i++)
                    array.Add(WriteHabit(habits[i], i, extras, choices));
            }
            return array;
        }

        /// <summary>Reads the "Tends" array of <paramref name="root"/>. A missing key or JSON null reads as an empty list.</summary>
        public static List<TendEvent> ReadTends(JsonObject root, Dictionary<string, Dictionary<string, JsonValue>> extras, Choices choices)
        {
            var tends = new List<TendEvent>();
            if (root.Has("Tends") && !root.Get("Tends").IsNull)
            {
                JsonArray rows = root.Get("Tends").AsArray();
                for (int i = 0; i < rows.Count; i++)
                    tends.Add(ReadTend(rows[i].AsObject(), i, extras, choices));
            }
            return tends;
        }

        /// <summary>Writes <paramref name="tends"/> as an array. A null list writes an empty array.</summary>
        public static JsonArray WriteTends(List<TendEvent> tends, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            var array = new JsonArray();
            if (tends != null)
            {
                for (int i = 0; i < tends.Count; i++)
                    array.Add(WriteTend(tends[i], i, extras));
            }
            return array;
        }

        /// <summary>Keeps the members of <paramref name="obj"/> not in <paramref name="known"/> under <paramref name="key"/>. Nothing is kept when there are none.</summary>
        public static void KeepExtra(JsonObject obj, string[] known, string key, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            Dictionary<string, JsonValue> extra = obj.Passthrough(known);
            if (extra.Count > 0 && extras != null && !string.IsNullOrEmpty(key))
                extras[key] = extra;
        }

        /// <summary>Writes back what <see cref="KeepExtra"/> kept under <paramref name="key"/>.</summary>
        public static void RestoreExtra(JsonObject obj, string key, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            Dictionary<string, JsonValue> extra;
            if (extras != null && key != null && extras.TryGetValue(key, out extra))
                obj.Restore(extra);
        }

        /// <summary>The extras key of a row: its Id, or "#index" when it has none.</summary>
        public static string RowKey(string id, int index)
        {
            if (!string.IsNullOrEmpty(id)) return id;
            return "#" + index.ToString(CultureInfo.InvariantCulture);
        }

        public static string StringMember(JsonObject obj, string key)
        {
            if (!obj.Has(key) || obj.Get(key).IsNull) return null;
            return obj.Get(key).AsString();
        }

        public static T EnumMember<T>(JsonObject obj, string key, T fallback) where T : struct
        {
            if (!obj.Has(key) || obj.Get(key).IsNull) return fallback;
            T value;
            if (Enum.TryParse(obj.Get(key).AsString(), false, out value)) return value;
            return fallback;
        }
    }
}

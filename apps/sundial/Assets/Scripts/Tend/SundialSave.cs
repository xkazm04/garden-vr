using System;
using System.Collections.Generic;
using GardenVR.Core;

namespace GardenVR.Sundial
{
    /// <summary>Dial preferences stored in the save. Defaults match the plan: day boundary 03:00, boil on.</summary>
    public sealed class SundialSettings
    {
        public int BoundaryMin = 180;
        public bool Voice;
        public bool Beds;
        public bool ReducedMotion;
        public bool Boil = true;
        /// <summary>Silences every bus. Off by default, so a fresh dial is quiet only when the user asks.</summary>
        public bool Mute;
    }

    /// <summary>
    /// One JSON document: habits, the append-only tend list, settings, and the first-run step.
    /// Unknown members are kept and written back.
    /// </summary>
    public sealed class SundialSave
    {
        public int SchemaVersion = 1;
        public List<HabitDef> Habits = new List<HabitDef>();
        public List<TendEvent> Tends = new List<TendEvent>();
        public SundialSettings Settings = new SundialSettings();
        public string FirstRunStep;
        /// <summary>Inked middays. Each row is a garden day and a symbol index. No note.</summary>
        public List<GratitudeMark> Gratitude = new List<GratitudeMark>();
        /// <summary>The open or finished shadow hour. Null when no hour has been started.</summary>
        public FocusSnapshot Focus;
        public Dictionary<string, JsonValue> Extra;
        public Dictionary<string, Dictionary<string, JsonValue>> HabitExtra;
        public Dictionary<string, Dictionary<string, JsonValue>> TendExtra;
    }

    /// <summary>Reads and writes <see cref="SundialSave"/> with the shared JSON codec.</summary>
    public static class SundialCodec
    {
        static readonly string[] RootKnown =
        {
            "SchemaVersion", "Habits", "Tends", "Settings", "FirstRunStep", "Focus"
        };

        static readonly string[] RootKnownWithGratitude =
        {
            "SchemaVersion", "Habits", "Tends", "Settings", "FirstRunStep", "Gratitude", "Focus"
        };

        static readonly string[] HabitKnown =
        {
            "Id", "PresetKey", "Group", "Species", "Kind", "Slot", "CreatedDay", "ArchivedDay"
        };

        static readonly string[] TendKnown =
        {
            "Id", "HabitId", "Tz", "Day", "AtUtcMs", "Source", "Late", "UndoneAtUtcMs"
        };

        public static SundialSave Read(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var save = new SundialSave();
            save.SchemaVersion = obj.Has("SchemaVersion") ? obj.Get("SchemaVersion").AsInt() : 1;
            bool gratitudeArray = obj.Has("Gratitude") && !obj.Get("Gratitude").IsNull
                && obj.Get("Gratitude").Kind == JsonKind.Array;
            save.Extra = obj.Passthrough(gratitudeArray ? RootKnownWithGratitude : RootKnown);
            save.Gratitude = gratitudeArray ? ReadGratitude(obj.Get("Gratitude").AsArray()) : new List<GratitudeMark>();
            if (obj.Has("Focus") && !obj.Get("Focus").IsNull && obj.Get("Focus").Kind == JsonKind.Object)
                save.Focus = ReadFocus(obj.Get("Focus").AsObject());
            save.Settings = ReadSettings(obj.Has("Settings") && !obj.Get("Settings").IsNull ? obj.Get("Settings").AsObject() : null);
            save.Habits = new List<HabitDef>();
            save.HabitExtra = new Dictionary<string, Dictionary<string, JsonValue>>();
            if (obj.Has("Habits") && !obj.Get("Habits").IsNull)
            {
                JsonArray habits = obj.Get("Habits").AsArray();
                for (int i = 0; i < habits.Count; i++)
                {
                    HabitDef habit = ReadHabit(habits[i].AsObject(), save.HabitExtra);
                    save.Habits.Add(habit);
                }
            }
            save.Tends = new List<TendEvent>();
            save.TendExtra = new Dictionary<string, Dictionary<string, JsonValue>>();
            if (obj.Has("Tends") && !obj.Get("Tends").IsNull)
            {
                JsonArray tends = obj.Get("Tends").AsArray();
                for (int i = 0; i < tends.Count; i++)
                {
                    TendEvent tend = ReadTend(tends[i].AsObject(), save.TendExtra);
                    save.Tends.Add(tend);
                }
            }
            if (obj.Has("FirstRunStep") && !obj.Get("FirstRunStep").IsNull)
                save.FirstRunStep = obj.Get("FirstRunStep").AsString();
            return save;
        }

        public static JsonObject Write(SundialSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            var obj = new JsonObject();
            obj.Set("SchemaVersion", JsonValue.Number(save.SchemaVersion < 1 ? 1 : save.SchemaVersion));
            var habits = new JsonArray();
            if (save.Habits != null)
            {
                for (int i = 0; i < save.Habits.Count; i++)
                    habits.Add(WriteHabit(save.Habits[i], save.HabitExtra));
            }
            obj.Set("Habits", habits);
            var tends = new JsonArray();
            if (save.Tends != null)
            {
                for (int i = 0; i < save.Tends.Count; i++)
                    tends.Add(WriteTend(save.Tends[i], save.TendExtra));
            }
            obj.Set("Tends", tends);
            obj.Set("Settings", WriteSettings(save.Settings ?? new SundialSettings()));
            obj.Set("FirstRunStep", save.FirstRunStep == null ? JsonValue.Null() : JsonValue.String(save.FirstRunStep));
            if (save.Gratitude != null && save.Gratitude.Count > 0)
                obj.Set("Gratitude", WriteGratitude(save.Gratitude));
            if (save.Focus != null && !string.IsNullOrEmpty(save.Focus.Phase) && save.Focus.Phase != "Idle")
                obj.Set("Focus", WriteFocus(save.Focus));
            obj.Restore(save.Extra);
            return obj;
        }

        static SundialSettings ReadSettings(JsonObject obj)
        {
            var settings = new SundialSettings();
            if (obj == null) return settings;
            if (obj.Has("BoundaryMin")) settings.BoundaryMin = obj.Get("BoundaryMin").AsInt();
            if (obj.Has("Voice")) settings.Voice = obj.Get("Voice").AsBool();
            if (obj.Has("Beds")) settings.Beds = obj.Get("Beds").AsBool();
            if (obj.Has("ReducedMotion")) settings.ReducedMotion = obj.Get("ReducedMotion").AsBool();
            if (obj.Has("Boil")) settings.Boil = obj.Get("Boil").AsBool();
            if (obj.Has("Mute")) settings.Mute = obj.Get("Mute").AsBool();
            return settings;
        }

        static JsonObject WriteSettings(SundialSettings settings)
        {
            var obj = new JsonObject();
            obj.Set("BoundaryMin", JsonValue.Number(settings.BoundaryMin));
            obj.Set("Voice", JsonValue.Bool(settings.Voice));
            obj.Set("Beds", JsonValue.Bool(settings.Beds));
            obj.Set("ReducedMotion", JsonValue.Bool(settings.ReducedMotion));
            obj.Set("Boil", JsonValue.Bool(settings.Boil));
            obj.Set("Mute", JsonValue.Bool(settings.Mute));
            return obj;
        }

        static HabitDef ReadHabit(JsonObject obj, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            var habit = new HabitDef();
            habit.Id = StringMember(obj, "Id");
            habit.PresetKey = StringMember(obj, "PresetKey");
            habit.Group = StringMember(obj, "Group");
            habit.Species = StringMember(obj, "Species");
            habit.Kind = EnumMember(obj, "Kind", HabitKind.LifeCheckIn);
            habit.Slot = obj.Has("Slot") ? obj.Get("Slot").AsInt() : 0;
            habit.CreatedDay = obj.Has("CreatedDay") ? obj.Get("CreatedDay").AsInt() : 0;
            if (obj.Has("ArchivedDay") && !obj.Get("ArchivedDay").IsNull)
                habit.ArchivedDay = obj.Get("ArchivedDay").AsInt();
            Dictionary<string, JsonValue> extra = obj.Passthrough(HabitKnown);
            if (extra.Count > 0 && !string.IsNullOrEmpty(habit.Id) && extras != null)
                extras[habit.Id] = extra;
            return habit;
        }

        static JsonObject WriteHabit(HabitDef habit, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            if (habit == null) habit = new HabitDef();
            var obj = new JsonObject();
            obj.Set("Id", JsonValue.String(habit.Id ?? ""));
            obj.Set("PresetKey", JsonValue.String(habit.PresetKey ?? ""));
            obj.Set("Group", JsonValue.String(habit.Group ?? ""));
            obj.Set("Species", habit.Species == null ? JsonValue.Null() : JsonValue.String(habit.Species));
            obj.Set("Kind", JsonValue.String(habit.Kind.ToString()));
            obj.Set("Slot", JsonValue.Number(habit.Slot));
            obj.Set("CreatedDay", JsonValue.Number(habit.CreatedDay));
            obj.Set("ArchivedDay", habit.ArchivedDay.HasValue ? JsonValue.Number(habit.ArchivedDay.Value) : JsonValue.Null());
            Dictionary<string, JsonValue> extra;
            if (extras != null && habit.Id != null && extras.TryGetValue(habit.Id, out extra))
                obj.Restore(extra);
            return obj;
        }

        static TendEvent ReadTend(JsonObject obj, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            var tend = new TendEvent();
            tend.Id = StringMember(obj, "Id");
            tend.HabitId = StringMember(obj, "HabitId");
            tend.Tz = StringMember(obj, "Tz");
            tend.Day = obj.Has("Day") ? obj.Get("Day").AsInt() : 0;
            tend.AtUtcMs = obj.Has("AtUtcMs") ? obj.Get("AtUtcMs").AsLong() : 0L;
            tend.Source = EnumMember(obj, "Source", TendSource.Pinch);
            tend.Late = obj.Has("Late") && obj.Get("Late").AsBool();
            if (obj.Has("UndoneAtUtcMs") && !obj.Get("UndoneAtUtcMs").IsNull)
                tend.UndoneAtUtcMs = obj.Get("UndoneAtUtcMs").AsLong();
            Dictionary<string, JsonValue> extra = obj.Passthrough(TendKnown);
            if (extra.Count > 0 && !string.IsNullOrEmpty(tend.Id) && extras != null)
                extras[tend.Id] = extra;
            return tend;
        }

        static JsonObject WriteTend(TendEvent tend, Dictionary<string, Dictionary<string, JsonValue>> extras)
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
            Dictionary<string, JsonValue> extra;
            if (extras != null && tend.Id != null && extras.TryGetValue(tend.Id, out extra))
                obj.Restore(extra);
            return obj;
        }

        static List<GratitudeMark> ReadGratitude(JsonArray rows)
        {
            var list = new List<GratitudeMark>();
            if (rows == null) return list;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].Kind != JsonKind.Object) continue;
                JsonObject row = rows[i].AsObject();
                if (!row.Has("Day") || !row.Has("Symbol")) continue;
                JsonValue day = row.Get("Day");
                JsonValue symbol = row.Get("Symbol");
                if (day.Kind != JsonKind.Number || symbol.Kind != JsonKind.Number) continue;
                int index = symbol.AsInt();
                if (!GratitudeRecord.IsSymbol(index)) continue;
                int dayIndex = day.AsInt();
                bool duplicate = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (list[j].Day == dayIndex) { duplicate = true; break; }
                }
                if (duplicate) continue;
                list.Add(new GratitudeMark { Day = dayIndex, Symbol = index });
            }
            return list;
        }

        static JsonArray WriteGratitude(List<GratitudeMark> marks)
        {
            var rows = new JsonArray();
            for (int i = 0; i < marks.Count; i++)
            {
                GratitudeMark mark = marks[i];
                if (mark == null || !GratitudeRecord.IsSymbol(mark.Symbol)) continue;
                var row = new JsonObject();
                row.Set("Day", JsonValue.Number(mark.Day));
                row.Set("Symbol", JsonValue.Number(mark.Symbol));
                rows.Add(row);
            }
            return rows;
        }

        static FocusSnapshot ReadFocus(JsonObject obj)
        {
            var snap = new FocusSnapshot();
            if (obj == null) return snap;
            snap.Phase = StringMember(obj, "Phase");
            if (obj.Has("StartedUtcMs") && !obj.Get("StartedUtcMs").IsNull) snap.StartedUtcMs = obj.Get("StartedUtcMs").AsLong();
            if (obj.Has("PausedMs") && !obj.Get("PausedMs").IsNull) snap.PausedMs = obj.Get("PausedMs").AsLong();
            if (obj.Has("PauseUtcMs") && !obj.Get("PauseUtcMs").IsNull) snap.PauseUtcMs = obj.Get("PauseUtcMs").AsLong();
            snap.Arc = StringMember(obj, "Arc");
            if (obj.Has("StartGnomonDeg") && !obj.Get("StartGnomonDeg").IsNull) snap.StartGnomonDeg = (float)obj.Get("StartGnomonDeg").AsDouble();
            snap.EndedEarly = obj.Has("EndedEarly") && !obj.Get("EndedEarly").IsNull && obj.Get("EndedEarly").AsBool();
            snap.TendPending = obj.Has("TendPending") && !obj.Get("TendPending").IsNull && obj.Get("TendPending").AsBool();
            if (obj.Has("ElapsedSeconds") && !obj.Get("ElapsedSeconds").IsNull) snap.ElapsedSeconds = obj.Get("ElapsedSeconds").AsInt();
            return snap;
        }

        static JsonObject WriteFocus(FocusSnapshot snap)
        {
            var obj = new JsonObject();
            obj.Set("Phase", JsonValue.String(snap.Phase ?? "Idle"));
            obj.Set("StartedUtcMs", JsonValue.Number(snap.StartedUtcMs));
            obj.Set("PausedMs", JsonValue.Number(snap.PausedMs));
            obj.Set("PauseUtcMs", JsonValue.Number(snap.PauseUtcMs));
            obj.Set("Arc", snap.Arc == null ? JsonValue.Null() : JsonValue.String(snap.Arc));
            obj.Set("StartGnomonDeg", JsonValue.Number((double)snap.StartGnomonDeg));
            obj.Set("EndedEarly", JsonValue.Bool(snap.EndedEarly));
            obj.Set("TendPending", JsonValue.Bool(snap.TendPending));
            obj.Set("ElapsedSeconds", JsonValue.Number(snap.ElapsedSeconds));
            return obj;
        }

        static string StringMember(JsonObject obj, string key)
        {
            if (!obj.Has(key) || obj.Get(key).IsNull) return null;
            return obj.Get(key).AsString();
        }

        static T EnumMember<T>(JsonObject obj, string key, T fallback) where T : struct
        {
            if (!obj.Has(key) || obj.Get(key).IsNull) return fallback;
            T value;
            if (Enum.TryParse(obj.Get(key).AsString(), false, out value)) return value;
            return fallback;
        }
    }
}

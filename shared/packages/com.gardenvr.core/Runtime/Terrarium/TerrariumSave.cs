using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// Ritual preferences. Only values that differ from these defaults are written.
    /// <see cref="HoldMode"/> is "Hold" or "Toggle", the same names the input provider uses.
    /// </summary>
    public sealed class RitualSettings
    {
        public const int DefaultBreaths = 6;
        public const double DefaultInhaleSec = 4d;
        public const double DefaultExhaleSec = 6d;
        public const string DefaultHoldMode = "Hold";

        public int Breaths = DefaultBreaths;
        public double InhaleSec = DefaultInhaleSec;
        public double ExhaleSec = DefaultExhaleSec;
        public bool AutoPace;
        public bool VoiceGuide;
        public bool NightBed;
        public bool ReducedMotion;
        public string HoldMode = DefaultHoldMode;
    }

    /// <summary>
    /// The terrarium save document. Schema 1. Unknown members are kept and written back.
    /// Habits and tends are stored for the life-habit task; this document does not interpret them.
    /// </summary>
    public sealed class TerrariumSave
    {
        public int SchemaVersion = TerrariumSaveMigrations.CurrentSchema;
        public int[] FrondDays = new int[0];
        public int DewToday;
        public int? LastRitualDay;
        public int Returns;
        public int RitualsCompleted;
        public List<HabitDef> Habits = new List<HabitDef>();
        public List<TendEvent> Tends = new List<TendEvent>();
        public RitualSettings Settings = new RitualSettings();
        public string FirstRunStep;
        public Dictionary<string, JsonValue> Extra;
        public Dictionary<string, JsonValue> SettingsExtra;
        public Dictionary<string, Dictionary<string, JsonValue>> HabitExtra;
        public Dictionary<string, Dictionary<string, JsonValue>> TendExtra;

        public string ToJson()
        {
            return Json.Write(Write(this));
        }

        public static TerrariumSave FromJson(string text)
        {
            return Read(Json.ParseObject(text));
        }

        public static TerrariumSave Read(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var save = new TerrariumSave();
            save.SchemaVersion = obj.Has("SchemaVersion") ? obj.Get("SchemaVersion").AsInt() : TerrariumSaveMigrations.CurrentSchema;
            save.Extra = obj.Passthrough(RootKnown);
            save.FrondDays = ReadInts(obj, "FrondDays");
            save.DewToday = obj.Has("DewToday") ? obj.Get("DewToday").AsInt() : 0;
            if (obj.Has("LastRitualDay") && !obj.Get("LastRitualDay").IsNull)
                save.LastRitualDay = obj.Get("LastRitualDay").AsInt();
            save.Returns = obj.Has("Returns") ? obj.Get("Returns").AsInt() : 0;
            save.RitualsCompleted = obj.Has("RitualsCompleted") ? obj.Get("RitualsCompleted").AsInt() : 0;
            Dictionary<string, JsonValue> settingsExtra;
            save.Settings = ReadSettings(obj.Has("Settings") && !obj.Get("Settings").IsNull ? obj.Get("Settings").AsObject() : null, out settingsExtra);
            save.SettingsExtra = settingsExtra;
            save.Habits = new List<HabitDef>();
            save.HabitExtra = new Dictionary<string, Dictionary<string, JsonValue>>();
            if (obj.Has("Habits") && !obj.Get("Habits").IsNull)
            {
                JsonArray habits = obj.Get("Habits").AsArray();
                for (int i = 0; i < habits.Count; i++)
                    save.Habits.Add(ReadHabit(habits[i].AsObject(), i, save.HabitExtra));
            }
            save.Tends = new List<TendEvent>();
            save.TendExtra = new Dictionary<string, Dictionary<string, JsonValue>>();
            if (obj.Has("Tends") && !obj.Get("Tends").IsNull)
            {
                JsonArray tends = obj.Get("Tends").AsArray();
                for (int i = 0; i < tends.Count; i++)
                    save.Tends.Add(ReadTend(tends[i].AsObject(), i, save.TendExtra));
            }
            if (obj.Has("FirstRunStep") && !obj.Get("FirstRunStep").IsNull)
                save.FirstRunStep = obj.Get("FirstRunStep").AsString();
            return save;
        }

        public static JsonObject Write(TerrariumSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            var obj = new JsonObject();
            int version = save.SchemaVersion < 1 ? TerrariumSaveMigrations.CurrentSchema : save.SchemaVersion;
            obj.Set("SchemaVersion", JsonValue.Number(version));
            obj.Set("FrondDays", WriteInts(save.FrondDays));
            obj.Set("DewToday", JsonValue.Number(save.DewToday));
            obj.Set("LastRitualDay", save.LastRitualDay.HasValue ? JsonValue.Number(save.LastRitualDay.Value) : JsonValue.Null());
            obj.Set("Returns", JsonValue.Number(save.Returns));
            obj.Set("RitualsCompleted", JsonValue.Number(save.RitualsCompleted));
            var habits = new JsonArray();
            if (save.Habits != null)
            {
                for (int i = 0; i < save.Habits.Count; i++)
                    habits.Add(WriteHabit(save.Habits[i], i, save.HabitExtra));
            }
            obj.Set("Habits", habits);
            var tends = new JsonArray();
            if (save.Tends != null)
            {
                for (int i = 0; i < save.Tends.Count; i++)
                    tends.Add(WriteTend(save.Tends[i], i, save.TendExtra));
            }
            obj.Set("Tends", tends);
            JsonObject settings = WriteSettings(save.Settings ?? new RitualSettings(), save.SettingsExtra);
            if (settings.Count > 0) obj.Set("Settings", settings);
            obj.Set("FirstRunStep", save.FirstRunStep == null ? JsonValue.Null() : JsonValue.String(save.FirstRunStep));
            obj.Restore(save.Extra);
            return obj;
        }

        static readonly string[] RootKnown =
        {
            "SchemaVersion", "FrondDays", "DewToday", "LastRitualDay", "Returns", "RitualsCompleted",
            "Habits", "Tends", "Settings", "FirstRunStep"
        };

        static readonly string[] SettingKnown =
        {
            "Breaths", "InhaleSec", "ExhaleSec", "AutoPace", "VoiceGuide", "NightBed", "ReducedMotion", "HoldMode"
        };

        static readonly string[] HabitKnown =
        {
            "Id", "PresetKey", "Group", "Species", "Kind", "Slot", "CreatedDay", "ArchivedDay"
        };

        static readonly string[] TendKnown =
        {
            "Id", "HabitId", "Tz", "Day", "AtUtcMs", "Source", "Late", "UndoneAtUtcMs"
        };

        static RitualSettings ReadSettings(JsonObject obj, out Dictionary<string, JsonValue> extra)
        {
            var settings = new RitualSettings();
            if (obj == null)
            {
                extra = new Dictionary<string, JsonValue>();
                return settings;
            }
            extra = obj.Passthrough(SettingKnown);
            if (obj.Has("Breaths")) settings.Breaths = obj.Get("Breaths").AsInt();
            if (obj.Has("InhaleSec")) settings.InhaleSec = obj.Get("InhaleSec").AsDouble();
            if (obj.Has("ExhaleSec")) settings.ExhaleSec = obj.Get("ExhaleSec").AsDouble();
            if (obj.Has("AutoPace")) settings.AutoPace = obj.Get("AutoPace").AsBool();
            if (obj.Has("VoiceGuide")) settings.VoiceGuide = obj.Get("VoiceGuide").AsBool();
            if (obj.Has("NightBed")) settings.NightBed = obj.Get("NightBed").AsBool();
            if (obj.Has("ReducedMotion")) settings.ReducedMotion = obj.Get("ReducedMotion").AsBool();
            if (obj.Has("HoldMode") && !obj.Get("HoldMode").IsNull)
                settings.HoldMode = obj.Get("HoldMode").AsString();
            return settings;
        }

        static JsonObject WriteSettings(RitualSettings settings, Dictionary<string, JsonValue> extra)
        {
            var obj = new JsonObject();
            if (settings.Breaths != RitualSettings.DefaultBreaths)
                obj.Set("Breaths", JsonValue.Number(settings.Breaths));
            if (settings.InhaleSec != RitualSettings.DefaultInhaleSec)
                obj.Set("InhaleSec", JsonValue.Number(settings.InhaleSec));
            if (settings.ExhaleSec != RitualSettings.DefaultExhaleSec)
                obj.Set("ExhaleSec", JsonValue.Number(settings.ExhaleSec));
            if (settings.AutoPace) obj.Set("AutoPace", JsonValue.Bool(true));
            if (settings.VoiceGuide) obj.Set("VoiceGuide", JsonValue.Bool(true));
            if (settings.NightBed) obj.Set("NightBed", JsonValue.Bool(true));
            if (settings.ReducedMotion) obj.Set("ReducedMotion", JsonValue.Bool(true));
            if (!string.IsNullOrEmpty(settings.HoldMode) && settings.HoldMode != RitualSettings.DefaultHoldMode)
                obj.Set("HoldMode", JsonValue.String(settings.HoldMode));
            obj.Restore(extra);
            return obj;
        }

        static int[] ReadInts(JsonObject obj, string key)
        {
            if (!obj.Has(key) || obj.Get(key).IsNull) return new int[0];
            JsonArray array = obj.Get(key).AsArray();
            var values = new int[array.Count];
            for (int i = 0; i < array.Count; i++) values[i] = array[i].AsInt();
            return values;
        }

        static JsonArray WriteInts(int[] values)
        {
            var array = new JsonArray();
            if (values == null) return array;
            for (int i = 0; i < values.Length; i++) array.Add(JsonValue.Number(values[i]));
            return array;
        }

        static HabitDef ReadHabit(JsonObject obj, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
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
            KeepExtra(obj, HabitKnown, HabitKey(habit.Id, index), extras);
            return habit;
        }

        static JsonObject WriteHabit(HabitDef habit, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            if (habit == null) habit = new HabitDef();
            var obj = new JsonObject();
            obj.Set("Id", JsonValue.String(habit.Id ?? ""));
            obj.Set("PresetKey", JsonValue.String(habit.PresetKey ?? ""));
            obj.Set("Group", habit.Group == null ? JsonValue.Null() : JsonValue.String(habit.Group));
            obj.Set("Species", habit.Species == null ? JsonValue.Null() : JsonValue.String(habit.Species));
            obj.Set("Kind", JsonValue.String(habit.Kind.ToString()));
            obj.Set("Slot", JsonValue.Number(habit.Slot));
            obj.Set("CreatedDay", JsonValue.Number(habit.CreatedDay));
            obj.Set("ArchivedDay", habit.ArchivedDay.HasValue ? JsonValue.Number(habit.ArchivedDay.Value) : JsonValue.Null());
            RestoreExtra(obj, HabitKey(habit.Id, index), extras);
            return obj;
        }

        static TendEvent ReadTend(JsonObject obj, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            var tend = new TendEvent();
            tend.Id = StringMember(obj, "Id");
            tend.HabitId = StringMember(obj, "HabitId");
            tend.Tz = StringMember(obj, "Tz");
            tend.Day = obj.Has("Day") ? obj.Get("Day").AsInt() : 0;
            tend.AtUtcMs = obj.Has("AtUtcMs") ? obj.Get("AtUtcMs").AsLong() : 0L;
            tend.Source = EnumMember(obj, "Source", TendSource.Ritual);
            tend.Late = obj.Has("Late") && obj.Get("Late").AsBool();
            if (obj.Has("UndoneAtUtcMs") && !obj.Get("UndoneAtUtcMs").IsNull)
                tend.UndoneAtUtcMs = obj.Get("UndoneAtUtcMs").AsLong();
            KeepExtra(obj, TendKnown, HabitKey(tend.Id, index), extras);
            return tend;
        }

        static JsonObject WriteTend(TendEvent tend, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
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
            RestoreExtra(obj, HabitKey(tend.Id, index), extras);
            return obj;
        }

        static void KeepExtra(JsonObject obj, string[] known, string key, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            Dictionary<string, JsonValue> extra = obj.Passthrough(known);
            if (extra.Count > 0 && extras != null && !string.IsNullOrEmpty(key))
                extras[key] = extra;
        }

        static void RestoreExtra(JsonObject obj, string key, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            Dictionary<string, JsonValue> extra;
            if (extras != null && key != null && extras.TryGetValue(key, out extra))
                obj.Restore(extra);
        }

        static string HabitKey(string id, int index)
        {
            if (!string.IsNullOrEmpty(id)) return id;
            return "#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

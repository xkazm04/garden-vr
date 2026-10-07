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
        /// <summary>The breath counts the settings pebbles offer. Any other count falls back to <see cref="DefaultBreaths"/>.</summary>
        public static readonly int[] BreathChoices = { 3, 4, 6, 8 };

        public int Breaths = DefaultBreaths;
        public double InhaleSec = DefaultInhaleSec;
        public double ExhaleSec = DefaultExhaleSec;
        public bool AutoPace;
        public bool VoiceGuide;
        public bool NightBed;
        public bool ReducedMotion;
        public bool Mute;
        public string HoldMode = DefaultHoldMode;
        /// <summary>Four equal sides of 4 s. Omitted from the save when false, which is the free pace.</summary>
        public bool BoxPace;
    }

    /// <summary>
    /// The terrarium save document. Schema 1. Unknown members are kept and written back.
    /// Habits and tends live here. Companion leaves are the live rows in <see cref="Tends"/>.
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
        /// <summary>A ritual was started and not finished. The next launch offers it back. It does not start itself.</summary>
        public bool RitualOpen;
        /// <summary>The garden day three good things was finished. The three things are not stored.</summary>
        public int? GladDay;
        /// <summary>Evening words, one stone a day. Empty days are simply absent. Omitted from the save when none are kept.</summary>
        public List<KeptWord> DayWords = new List<KeptWord>();
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
            if (obj.Has("RitualOpen")) save.RitualOpen = obj.Get("RitualOpen").AsBool();
            if (obj.Has("GladDay") && !obj.Get("GladDay").IsNull)
                save.GladDay = obj.Get("GladDay").AsInt();
            save.DayWords = new List<KeptWord>();
            if (obj.Has("DayWords") && !obj.Get("DayWords").IsNull)
                save.DayWords = ReadWords(obj.Get("DayWords").AsArray());
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
            if (save.RitualOpen) obj.Set("RitualOpen", JsonValue.Bool(true));
            if (save.GladDay.HasValue) obj.Set("GladDay", JsonValue.Number(save.GladDay.Value));
            JsonArray dayWords = WriteWords(save.DayWords);
            if (dayWords.Count > 0) obj.Set("DayWords", dayWords);
            obj.Restore(save.Extra);
            return obj;
        }

        static readonly string[] RootKnown =
        {
            "SchemaVersion", "FrondDays", "DewToday", "LastRitualDay", "Returns", "RitualsCompleted",
            "Habits", "Tends", "Settings", "FirstRunStep", "RitualOpen", "GladDay", "DayWords"
        };

        static readonly string[] SettingKnown =
        {
            "Breaths", "InhaleSec", "ExhaleSec", "AutoPace", "VoiceGuide", "NightBed", "ReducedMotion", "Mute", "HoldMode", "BoxPace"
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
            if (obj.Has("Mute")) settings.Mute = obj.Get("Mute").AsBool();
            if (obj.Has("HoldMode") && !obj.Get("HoldMode").IsNull)
                settings.HoldMode = obj.Get("HoldMode").AsString();
            if (obj.Has("BoxPace")) settings.BoxPace = obj.Get("BoxPace").AsBool();
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
            if (settings.Mute) obj.Set("Mute", JsonValue.Bool(true));
            if (!string.IsNullOrEmpty(settings.HoldMode) && settings.HoldMode != RitualSettings.DefaultHoldMode)
                obj.Set("HoldMode", JsonValue.String(settings.HoldMode));
            if (settings.BoxPace) obj.Set("BoxPace", JsonValue.Bool(true));
            obj.Restore(extra);
            return obj;
        }

        static List<KeptWord> ReadWords(JsonArray array)
        {
            var words = new List<KeptWord>();
            if (array == null) return words;
            for (int i = 0; i < array.Count; i++)
            {
                JsonObject row = array[i].AsObject();
                int day = row.Has("Day") ? row.Get("Day").AsInt() : 0;
                string raw = row.Has("Word") && !row.Get("Word").IsNull ? row.Get("Word").AsString() : null;
                string word = OneWord.Canonical(raw);
                if (word == null) continue;
                if (OneWord.On(words, day) != null) continue;
                words.Add(new KeptWord { Day = day, Word = word });
            }
            return words;
        }

        static JsonArray WriteWords(List<KeptWord> words)
        {
            var array = new JsonArray();
            if (words == null) return array;
            for (int i = 0; i < words.Count; i++)
            {
                KeptWord row = words[i];
                if (row == null) continue;
                string word = OneWord.Canonical(row.Word);
                if (word == null) continue;
                var obj = new JsonObject();
                obj.Set("Day", JsonValue.Number(row.Day));
                obj.Set("Word", JsonValue.String(word));
                array.Add(obj);
            }
            return array;
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

        static readonly LedgerJson.Choices RowChoices = new LedgerJson.Choices
        {
            NullGroupAsEmpty = false,
            SourceFallback = TendSource.Ritual,
            RowKnown = false
        };

        static HabitDef ReadHabit(JsonObject obj, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            return LedgerJson.ReadHabit(obj, index, extras, RowChoices);
        }

        static JsonObject WriteHabit(HabitDef habit, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            return LedgerJson.WriteHabit(habit, index, extras, RowChoices);
        }

        static TendEvent ReadTend(JsonObject obj, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            return LedgerJson.ReadTend(obj, index, extras, RowChoices);
        }

        static JsonObject WriteTend(TendEvent tend, int index, Dictionary<string, Dictionary<string, JsonValue>> extras)
        {
            return LedgerJson.WriteTend(tend, index, extras);
        }
    }
}

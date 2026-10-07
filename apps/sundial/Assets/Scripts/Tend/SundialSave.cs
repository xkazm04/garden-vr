using System;
using System.Collections.Generic;
using GardenVR.Core;

namespace GardenVR.Sundial
{
    /// <summary>Dial preferences stored in the save. Defaults match the plan: day boundary 03:00, boil on.</summary>
    public sealed class SundialSettings
    {
        public int BoundaryMin = 180;
        /// <summary>06:00. The morning arc opens here. A drag snaps this to 15 minutes.</summary>
        public int MorningMin = 6 * 60;
        /// <summary>11:00. Midday opens here.</summary>
        public int MiddayMin = 11 * 60;
        /// <summary>18:00. Dusk, the wind-down arc, opens here. It still ends at <see cref="BoundaryMin"/>.</summary>
        public int DuskMin = 18 * 60;
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
        public int SchemaVersion = SundialService.SchemaVersion;
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

        public static SundialSave Read(JsonObject obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var save = new SundialSave();
            save.SchemaVersion = obj.Has("SchemaVersion") ? obj.Get("SchemaVersion").AsInt() : SundialService.SchemaVersion;
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
                    HabitDef habit = ReadHabit(habits[i].AsObject(), i, save.HabitExtra);
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
                    TendEvent tend = ReadTend(tends[i].AsObject(), i, save.TendExtra);
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
            obj.Set("SchemaVersion", JsonValue.Number(save.SchemaVersion < 1 ? SundialService.SchemaVersion : save.SchemaVersion));
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
            if (obj.Has("MorningMin")) settings.MorningMin = obj.Get("MorningMin").AsInt();
            if (obj.Has("MiddayMin")) settings.MiddayMin = obj.Get("MiddayMin").AsInt();
            if (obj.Has("DuskMin")) settings.DuskMin = obj.Get("DuskMin").AsInt();
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
            obj.Set("MorningMin", JsonValue.Number(settings.MorningMin));
            obj.Set("MiddayMin", JsonValue.Number(settings.MiddayMin));
            obj.Set("DuskMin", JsonValue.Number(settings.DuskMin));
            obj.Set("Voice", JsonValue.Bool(settings.Voice));
            obj.Set("Beds", JsonValue.Bool(settings.Beds));
            obj.Set("ReducedMotion", JsonValue.Bool(settings.ReducedMotion));
            obj.Set("Boil", JsonValue.Bool(settings.Boil));
            obj.Set("Mute", JsonValue.Bool(settings.Mute));
            return obj;
        }

        static readonly LedgerJson.Choices RowChoices = new LedgerJson.Choices
        {
            NullGroupAsEmpty = true,
            SourceFallback = TendSource.Pinch,
            RowKnown = true
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
            snap.Phase = LedgerJson.StringMember(obj, "Phase");
            if (obj.Has("StartedUtcMs") && !obj.Get("StartedUtcMs").IsNull) snap.StartedUtcMs = obj.Get("StartedUtcMs").AsLong();
            if (obj.Has("PausedMs") && !obj.Get("PausedMs").IsNull) snap.PausedMs = obj.Get("PausedMs").AsLong();
            if (obj.Has("PauseUtcMs") && !obj.Get("PauseUtcMs").IsNull) snap.PauseUtcMs = obj.Get("PauseUtcMs").AsLong();
            snap.Arc = LedgerJson.StringMember(obj, "Arc");
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
    }
}

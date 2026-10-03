using System.Collections.Generic;
using GardenVR.Core;

namespace GardenVR.Sundial
{
    /// <summary>
    /// One preset the first run can plant. No free text. The cue is the chip on the packet.
    /// </summary>
    public sealed class SeedPreset
    {
        public string Arc;
        public string Key;
        public string Title;
        public string Cue;
        public string Group;
        public HabitKind Kind;
        public string HabitId;
    }

    /// <summary>
    /// Every first-run string lives here. Hyphens only. Nothing about counters or health outcomes.
    /// </summary>
    public static class SeedCatalog
    {
        public const string DayCaption = "This is your day.";
        public const string BreathOffer = "Try three breaths?";
        public const string RestoreTitle = "This page didn't load.";
        public const string RestoreAction = "Restore the previous page";

        public static readonly SeedPreset[] All =
        {
            Preset("morning", "water", "Water", "After I wake, I drink a glass of water.", "morning", HabitKind.LifeCheckIn),
            Preset("morning", "stretch", "Stretch", "After coffee, I stretch for a minute.", "morning", HabitKind.LifeCheckIn),
            Preset("morning", "bed", "Make the bed", "After I get up, I make the bed.", "morning", HabitKind.LifeCheckIn),
            Preset("midday", "top3", "Top-3 plan", "When I sit down to work, I write my top three.", "midday", HabitKind.LifeCheckIn),
            Preset("midday", "walk", "Walk a stop", "After work, I walk one stop.", "midday", HabitKind.LifeCheckIn),
            Preset("midday", "lunch", "Screen-free lunch", "At lunch, the screen stays closed.", "midday", HabitKind.LifeCheckIn),
            Preset("winddown", "breaths", "Three breaths", "After I brush my teeth, three breaths.", "wind-down", HabitKind.InAppRitual),
            Preset("winddown", "phone", "Phone away", "At 22:30 the phone goes on the shelf.", "wind-down", HabitKind.LifeCheckIn),
            Preset("winddown", "read", "Read", "In bed, I read ten pages.", "wind-down", HabitKind.LifeCheckIn)
        };

        public static SeedPreset Find(string arc, string key)
        {
            if (string.IsNullOrEmpty(arc) || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                SeedPreset preset = All[i];
                if (preset.Arc == arc && preset.Key == key) return preset;
            }
            return null;
        }

        public static List<SeedPreset> ForArc(string arc)
        {
            var list = new List<SeedPreset>();
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Arc == arc) list.Add(All[i]);
            }
            return list;
        }

        /// <summary>
        /// The next preset this arc does not already have. Archived habits do not count.
        /// Null when the arc is full or every preset is taken.
        /// </summary>
        public static SeedPreset NextFree(string arc, IReadOnlyList<HabitDef> habits)
        {
            if (string.IsNullOrEmpty(arc)) return null;
            if (habits != null && SundialRules.LiveInArc(habits, arc) >= SundialRules.MaxHabitsPerArc) return null;
            for (int i = 0; i < All.Length; i++)
            {
                SeedPreset preset = All[i];
                if (preset.Arc != arc || Taken(habits, preset)) continue;
                return preset;
            }
            return null;
        }

        static bool Taken(IReadOnlyList<HabitDef> habits, SeedPreset preset)
        {
            if (habits == null || preset == null) return false;
            for (int i = 0; i < habits.Count; i++)
            {
                HabitDef habit = habits[i];
                if (habit == null || habit.ArchivedDay.HasValue) continue;
                if (habit.PresetKey == preset.Key || habit.Id == preset.HabitId) return true;
            }
            return false;
        }

        public static string PacketId(string arc) { return "seed." + arc; }

        public static string PresetId(SeedPreset preset) { return "seed." + preset.Arc + "." + preset.Key; }

        static SeedPreset Preset(string arc, string key, string title, string cue, string group, HabitKind kind)
        {
            return new SeedPreset
            {
                Arc = arc,
                Key = key,
                Title = title,
                Cue = cue,
                Group = group,
                Kind = kind,
                HabitId = key
            };
        }
    }

    /// <summary>Ink species for the nine sundial plants. Order is the card row in <c>DialLibrary.SpeciesCards</c>.</summary>
    public static class SundialSpecies
    {
        public static readonly string[] Keys =
        {
            "sunrise", "midday", "dusk", "reed", "clover", "vine", "sprig", "bell", "page"
        };

        public static int Index(string key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i] == key) return i;
            }
            return -1;
        }

        public static string ForPreset(string preset)
        {
            if (preset == "water") return "sunrise";
            if (preset == "stretch") return "reed";
            if (preset == "bed") return "clover";
            if (preset == "top3") return "midday";
            if (preset == "walk") return "vine";
            if (preset == "lunch") return "sprig";
            if (preset == "breaths") return "dusk";
            if (preset == "phone") return "bell";
            if (preset == "read") return "page";
            return null;
        }
    }

    /// <summary>Stable first-run step ids. Order is the journey order.</summary>
    public static class FirstRunSteps
    {
        public const string Appear = "fr.appear";
        public const string Sweep = "fr.sweep";
        public const string Caption = "fr.caption";
        public const string Packets = "fr.packets";
        public const string PickMorning = "fr.pick.morning";
        public const string PickMidday = "fr.pick.midday";
        public const string PickWinddown = "fr.pick.winddown";
        public const string Drop = "fr.drop";
        public const string FirstTend = "fr.firsttend";
        public const string BreathsOffer = "fr.breaths.offer";
        public const string Breaths = "fr.breaths";
        public const string Done = "fr.done";

        static readonly string[] Order =
        {
            Appear, Sweep, Caption, Packets, PickMorning, PickMidday, PickWinddown,
            Drop, FirstTend, BreathsOffer, Breaths, Done
        };

        public static int Index(string step)
        {
            if (string.IsNullOrEmpty(step)) return -1;
            for (int i = 0; i < Order.Length; i++)
            {
                if (Order[i] == step) return i;
            }
            return -1;
        }

        public static bool IsResume(string step)
        {
            int index = Index(step);
            return index >= 0 && step != Done;
        }

        public static bool WaitsForIntent(string step)
        {
            return step == PickMorning || step == PickMidday || step == PickWinddown
                || step == FirstTend || step == BreathsOffer || step == Breaths;
        }

        public static string PickArc(string step)
        {
            if (step == PickMorning) return "morning";
            if (step == PickMidday) return "midday";
            if (step == PickWinddown) return "winddown";
            return null;
        }

        public static string Next(string step)
        {
            int index = Index(step);
            if (index < 0 || index + 1 >= Order.Length) return null;
            return Order[index + 1];
        }
    }
}

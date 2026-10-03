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
    /// Every first-run string lives here. Hyphens only. Nothing about streaks or health outcomes.
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

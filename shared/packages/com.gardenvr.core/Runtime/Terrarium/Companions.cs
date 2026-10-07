using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>One mesh, three tints. The cut line keeps the shape and drops the extra species.</summary>
    public enum CompanionSpecies
    {
        GlowSprig,
        MoonMoss,
        StarFern
    }

    /// <summary>
    /// Life habits as companion plants. Leaves are live ledger days.
    /// They rise by one per kept day and fall only when that day is undone.
    /// </summary>
    public static class Companions
    {
        public const int MaxHabits = 3;

        public static readonly string[] PresetKeys =
        {
            "walk", "water", "read", "stretch", "journal", "early-night"
        };

        public static int Leaves(Ledger ledger, string habitId)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            return ledger.KeptDays(habitId);
        }

        /// <summary>Same curve and floor as <see cref="Garden.Vitality"/>. No kept day reads as full glow.</summary>
        public static float Vitality(Ledger ledger, string habitId, GardenDay today)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            int last = LastKeptDay(ledger, habitId);
            if (last < 0) return 1f;
            int gap = today.Index - last;
            return Garden.VitalityForGap(Math.Max(0, gap));
        }

        public static int LastKeptDay(Ledger ledger, string habitId)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (string.IsNullOrEmpty(habitId)) throw new ArgumentException("habitId is null or empty", nameof(habitId));
            int last = int.MinValue;
            IReadOnlyList<TendEvent> events = ledger.Events;
            for (int i = 0; i < events.Count; i++)
            {
                TendEvent ev = events[i];
                if (ev == null || ev.HabitId != habitId || ev.UndoneAtUtcMs.HasValue) continue;
                if (ev.Day > last) last = ev.Day;
            }
            return last == int.MinValue ? -1 : last;
        }

        public static bool TryPreset(string key, out string label, out CompanionSpecies species)
        {
            label = null;
            species = CompanionSpecies.GlowSprig;
            if (string.IsNullOrEmpty(key)) return false;
            switch (key)
            {
                case "walk": label = "Walk"; species = CompanionSpecies.GlowSprig; return true;
                case "water": label = "Water"; species = CompanionSpecies.MoonMoss; return true;
                case "read": label = "Read"; species = CompanionSpecies.StarFern; return true;
                case "stretch": label = "Stretch"; species = CompanionSpecies.GlowSprig; return true;
                case "journal": label = "Journal"; species = CompanionSpecies.MoonMoss; return true;
                case "early-night": label = "Early night"; species = CompanionSpecies.StarFern; return true;
                default: return false;
            }
        }

        public static string Label(string key)
        {
            string label;
            CompanionSpecies species;
            return TryPreset(key, out label, out species) ? label : key;
        }

        public static CompanionSpecies SpeciesFor(string key)
        {
            string label;
            CompanionSpecies species;
            return TryPreset(key, out label, out species) ? species : CompanionSpecies.GlowSprig;
        }
    }
}

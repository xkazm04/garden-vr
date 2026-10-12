using System;
using System.Collections.Generic;
using System.Globalization;

namespace GardenVR.Core
{
    /// <summary>
    /// The coach with no model: the complete path when the coach is off, offline, slow or refused (upgrade plan 6.5).
    /// Onboarding matches the answers against a small set of starter habits; the reflection is a line built from the
    /// evening word stone. Same input, same output.
    /// </summary>
    public static class ScriptedCoach
    {
        sealed class Starter
        {
            public string[] Keys;
            public CoachProposal Proposal;
        }

        static CoachProposal P(string name, LifeZone zone, HabitSchedule schedule, string cue, int? target = null, string unit = null)
        {
            return new CoachProposal { Name = name, Zone = zone, Schedule = schedule, Cue = cue, Target = target, Unit = unit };
        }

        // Order is priority when several match. Every name, cue and unit passes CoachGuards (a test holds this).
        static Starter[] Starters()
        {
            return new[]
            {
                new Starter { Keys = new[] { "walk", "move", "outside", "outdoors", "legs", "fresh air", "step" }, Proposal = P("Walk after lunch", LifeZone.Body, HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek), "After lunch") },
                new Starter { Keys = new[] { "water", "drink", "thirst", "hydrat" }, Proposal = P("Water", LifeZone.Body, HabitSchedule.Daily(), "With each meal", 8, "glasses") },
                new Starter { Keys = new[] { "stretch", "back", "neck", "shoulder", "stiff", "sitting" }, Proposal = P("Stretch", LifeZone.Body, HabitSchedule.Daily(), "After my first coffee") },
                new Starter { Keys = new[] { "sleep", "tired", "bed", "night", "rest" }, Proposal = P("Early night", LifeZone.Body, HabitSchedule.Daily(), "At ten, screens away") },
                new Starter { Keys = new[] { "mum", "mom", "mother", "dad", "father", "parent" }, Proposal = P("Call home", LifeZone.Connection, HabitSchedule.PerWeek(2), "On my walk home") },
                new Starter { Keys = new[] { "friend", "family", "partner", "sister", "brother", "call", "closer", "someone" }, Proposal = P("Message a friend", LifeZone.Connection, HabitSchedule.PerWeek(2), "After dinner") },
                new Starter { Keys = new[] { "focus", "plan", "work", "busy", "priorit", "organis", "organiz" }, Proposal = P("Plan tomorrow's top 3", LifeZone.Work, HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek), "Before I close the laptop") },
                new Starter { Keys = new[] { "read", "book", "learn", "page" }, Proposal = P("Read", LifeZone.Mind, HabitSchedule.Daily(), "In bed, before the light goes out", 10, "pages") },
                new Starter { Keys = new[] { "calm", "quiet", "slow", "breath", "breathe", "peace", "still" }, Proposal = P("Three breaths", LifeZone.Mind, HabitSchedule.Daily(), "When I sit down at my desk") },
                new Starter { Keys = new[] { "write", "journal", "think", "reflect", "grateful", "thank" }, Proposal = P("One line of thanks", LifeZone.Mind, HabitSchedule.Daily(), "After I brush my teeth") }
            };
        }

        static readonly string[] FallbackNames = { "Walk after lunch", "Read", "Message a friend" };

        /// <summary>
        /// Up to three starter habits for the answers: matches in priority order, one per zone first, then a second in a
        /// zone already used; topped up from Walk, Read and Message a friend when fewer than three match.
        /// </summary>
        public static List<CoachProposal> Onboarding(IReadOnlyList<string> answers)
        {
            string text = " ";
            if (answers != null)
                foreach (string a in answers)
                    if (a != null) text += a.ToLower(CultureInfo.InvariantCulture) + " ";
            Starter[] starters = Starters();
            var matched = new List<CoachProposal>();
            foreach (Starter s in starters)
                foreach (string key in s.Keys)
                    if (text.IndexOf(" " + key, StringComparison.Ordinal) >= 0) { matched.Add(s.Proposal); break; }

            var picked = new List<CoachProposal>();
            var zones = new HashSet<LifeZone>();
            foreach (CoachProposal p in matched)
                if (picked.Count < CoachPrompts.MaxProposals && zones.Add(p.Zone)) picked.Add(p);
            foreach (CoachProposal p in matched)
                if (picked.Count < CoachPrompts.MaxProposals && !picked.Contains(p)) picked.Add(p);
            foreach (string name in FallbackNames)
            {
                if (picked.Count >= CoachPrompts.MaxProposals) break;
                if (picked.Exists(p => p.Name == name)) continue;
                foreach (Starter s in starters)
                    if (s.Proposal.Name == name) { picked.Add(s.Proposal); break; }
            }
            return picked;
        }

        static readonly Dictionary<string, string> StoneLines = new Dictionary<string, string>
        {
            { "calm", "A calm day, kept gently." },
            { "tired", "A tired day. You still came back to the garden." },
            { "glad", "A glad day. Something good grew today." },
            { "full", "A full day, and you made room for this." },
            { "quiet", "A quiet day. The garden kept you company." },
            { "light", "A light day, carried easily." }
        };

        /// <summary>The scripted journal line for an evening word stone, naming one kept habit when there is one.</summary>
        public static string Reflection(string stone, IReadOnlyList<string> keptToday)
        {
            string word = OneWord.Canonical(stone) ?? "quiet";
            string line = StoneLines[word];
            if (keptToday != null && keptToday.Count > 0)
            {
                string first = HabitProfiles.CleanName(keptToday[0]);
                string withHabit = first == null ? null : line + " " + first + " is in the garden.";
                if (withHabit != null && CoachGuards.IsClean(withHabit, CoachGuards.ReflectionMax)) return withHabit;
            }
            return line;
        }
    }
}

using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>Interview progress contains suggestions and decisions, never spoken answers.</summary>
    public sealed class OnboardingDraft
    {
        public List<CoachProposal> Proposals = new List<CoachProposal>();
        public int[] Decisions = new int[CoachPrompts.MaxProposals]; // 0 offered, 1 planted, 2 declined
        public int Selected = -1;

        public bool Pending(int index) => index >= 0 && index < Proposals.Count && Decisions[index] == 0;
        public bool Complete
        {
            get { for (int i = 0; i < Proposals.Count; i++) if (Pending(i)) return false; return true; }
        }

        public JsonObject Write()
        {
            var root = new JsonObject();
            var rows = new JsonArray();
            var decisions = new JsonArray();
            for (int i = 0; i < Proposals.Count && i < CoachPrompts.MaxProposals; i++)
            {
                var p = Proposals[i];
                var row = new JsonObject();
                row.Set("name", JsonValue.String(p.Name));
                row.Set("zone", JsonValue.String(p.Zone.ToString()));
                row.Set("cue", JsonValue.String(p.Cue));
                var schedule = new JsonObject();
                schedule.Set("kind", JsonValue.String(p.Schedule.Kind.ToString()));
                schedule.Set("times", JsonValue.Number(p.Schedule.TimesPerWeek));
                var days = new JsonArray();
                string[] names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                for (int d = 0; d < 7; d++) if ((p.Schedule.WeekdayMask & (1 << d)) != 0) days.Add(JsonValue.String(names[d]));
                schedule.Set("days", days);
                row.Set("schedule", schedule);
                if (p.Target.HasValue) { row.Set("target", JsonValue.Number(p.Target.Value)); row.Set("unit", JsonValue.String(p.Unit)); }
                rows.Add(row);
                decisions.Add(JsonValue.Number(Decisions[i]));
            }
            root.Set("habits", rows);
            root.Set("decisions", decisions);
            root.Set("selected", JsonValue.Number(Selected));
            return root;
        }

        public static OnboardingDraft Read(JsonObject root)
        {
            var draft = new OnboardingDraft();
            if (root == null) return draft;
            draft.Proposals = CoachPrompts.ParseOnboarding(Json.Write(root));
            // Invalid rows cannot shift a decision onto a different proposal.
            if (!root.Has("habits") || root.Get("habits").Kind != JsonKind.Array ||
                root.Get("habits").AsArray().Count != draft.Proposals.Count) return new OnboardingDraft();
            if (root.Has("decisions") && root.Get("decisions").Kind == JsonKind.Array)
            {
                var rows = root.Get("decisions").AsArray();
                for (int i = 0; i < rows.Count && i < draft.Proposals.Count; i++)
                    if (rows[i].Kind == JsonKind.Number && rows[i].AsInt() >= 0 && rows[i].AsInt() <= 2)
                        draft.Decisions[i] = rows[i].AsInt();
            }
            if (root.Has("selected") && root.Get("selected").Kind == JsonKind.Number && draft.Pending(root.Get("selected").AsInt()))
                draft.Selected = root.Get("selected").AsInt();
            return draft;
        }
    }

    public static class Onboarding
    {
        public const string OptIn = "fr.interview.optin";
        public const string Review = "fr.interview.review";
        public const string Waiting = "fr.interview.waiting";
        public static string QuestionStep(int index) => index >= 0 && index < CoachPrompts.OnboardingQuestions.Length ? "fr.interview.q" + index : null;
        public static int QuestionIndex(string step)
        {
            for (int i = 0; i < CoachPrompts.OnboardingQuestions.Length; i++) if (step == QuestionStep(i)) return i;
            return -1;
        }
        public static bool IsStep(string step) => step == OptIn || step == Review || step == Waiting || QuestionIndex(step) >= 0;
        public static string AfterQuestion(string step)
        {
            int i = QuestionIndex(step);
            return i < 0 ? null : i + 1 == CoachPrompts.OnboardingQuestions.Length ? Waiting : QuestionStep(i + 1);
        }

        /// <summary>Only unambiguous time cues place a packet automatically; other cues ask for an arc.</summary>
        public static string CueArc(string cue)
        {
            string text = (cue ?? "").ToLowerInvariant();
            bool morning = Has(text, "morning", "breakfast", "first coffee");
            bool midday = Has(text, "midday", "lunch", "afternoon");
            bool evening = Has(text, "evening", "dinner", "bed", "night", "screens away", "close the laptop", "brush my teeth");
            if ((morning ? 1 : 0) + (midday ? 1 : 0) + (evening ? 1 : 0) != 1) return null;
            return morning ? "morning" : midday ? "midday" : "winddown";
        }
        static bool Has(string text, params string[] words)
        {
            foreach (string word in words) if (text.Contains(word)) return true;
            return false;
        }

        /// <summary>First tend uses a planted arc, preferring the current time of day.</summary>
        public static string FirstTendArc(IReadOnlyList<HabitDef> habits, string current)
        {
            string first = null;
            if (habits != null) foreach (var h in habits)
            {
                if (h == null || h.ArchivedDay.HasValue) continue;
                string arc = h.Group == "wind-down" ? "winddown" : h.Group;
                if (arc != "morning" && arc != "midday" && arc != "winddown") continue;
                if (arc == current) return arc;
                if (first == null) first = arc;
            }
            return first;
        }
    }
}

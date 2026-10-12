using System;
using System.Globalization;
using System.Text;

namespace GardenVR.Core
{
    /// <summary>Why a coach line was refused. <see cref="None"/> means it may be shown.</summary>
    public enum GuardReason { None, Empty, TooLong, MultiLine, Dash, Exclamation, Medical, Count, Guilt }

    /// <summary>
    /// The checks every coach line passes before it is shown (upgrade plan section 6.5): no medical words, no em or en
    /// dash (AGENTS.md rule 7), no consecutive-day count, no guilt, calm punctuation, one line, a length cap. A line that
    /// fails is never edited into shape; the caller shows the scripted line instead.
    /// </summary>
    public static class CoachGuards
    {
        public const int ReflectionMax = 90;
        public const int NameMax = HabitProfiles.MaxNameLength;
        public const int CueMax = 48;

        // Matched at the start of a word, so "cure" does not catch "secure".
        static readonly string[] MedicalStems =
        {
            "therap", "medical", "medicat", "medicine", "anxiety", "anxious", "cure", "diagnos", "symptom", "treatment",
            "clinical", "clinic", "stress", "heal", "depress", "disorder", "doctor", "prescri", "illness", "disease",
            "patient", "insomnia", "panic", "trauma", "mental health"
        };

        static readonly string[] CountPhrases = { "streak", "in a row", "consecutive", "days straight", "straight days", "unbroken" };

        static readonly string[] GuiltStems =
        {
            "missed", "miss ", "fail", "broke your", "broken", "lost your", "forgot", "should have", "disappoint", "lazy",
            "guilt", "shame", "behind on", "slack", "give up", "excuse"
        };

        public static GuardReason Check(string line, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(line)) return GuardReason.Empty;
            if (line.Length > maxLength) return GuardReason.TooLong;
            if (line.IndexOf('\n') >= 0 || line.IndexOf('\r') >= 0) return GuardReason.MultiLine;
            if (line.IndexOf('—') >= 0 || line.IndexOf('–') >= 0 || line.IndexOf('―') >= 0) return GuardReason.Dash;
            if (line.IndexOf('!') >= 0) return GuardReason.Exclamation;
            string lower = " " + Normalise(line) + " ";
            foreach (string stem in MedicalStems)
                if (lower.IndexOf(" " + stem, StringComparison.Ordinal) >= 0) return GuardReason.Medical;
            foreach (string phrase in CountPhrases)
                if (lower.IndexOf(phrase, StringComparison.Ordinal) >= 0) return GuardReason.Count;
            foreach (string stem in GuiltStems)
                if (lower.IndexOf(" " + stem, StringComparison.Ordinal) >= 0) return GuardReason.Guilt;
            return GuardReason.None;
        }

        public static bool IsClean(string line, int maxLength) { return Check(line, maxLength) == GuardReason.None; }

        /// <summary>Lower case, with every character that is not a letter or digit read as a space.</summary>
        static string Normalise(string line)
        {
            var sb = new StringBuilder(line.Length);
            foreach (char c in line.ToLower(CultureInfo.InvariantCulture))
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            return sb.ToString();
        }
    }
}

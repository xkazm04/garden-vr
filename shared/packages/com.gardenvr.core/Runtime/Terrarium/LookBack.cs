using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// One day of the week replay. A kept day lights the next frond.
    /// A missed day is a held frame: <see cref="Lit"/> does not rise, and nothing is marked wrong.
    /// </summary>
    public readonly struct LookFrame
    {
        public readonly int Day;
        public readonly bool Kept;
        public readonly int Lit;
        /// <summary>The evening word for this day, or null when the day has none.</summary>
        public readonly string Word;

        public LookFrame(int day, bool kept, int lit, string word = null)
        {
            Day = day;
            Kept = kept;
            Lit = lit;
            Word = word;
        }
    }

    /// <summary>
    /// The day-7 look-back. Pinch-hold the cork for <see cref="HoldSeconds"/>, then the week
    /// plays one frame at a time. The jar only paints <see cref="LookFrame.Lit"/>.
    /// </summary>
    public static class LookBack
    {
        public const float HoldSeconds = 2f;
        public const float FrameSeconds = 0.8f;
        public const int WeekDays = 7;

        public static LookFrame[] Week(Garden garden, int today)
        {
            return Week(garden, today, null);
        }

        /// <summary>
        /// The week, with each day's word when <paramref name="words"/> has one.
        /// A missed day and a day with no word stay blank. Nothing is marked wrong.
        /// </summary>
        public static LookFrame[] Week(Garden garden, int today, IList<KeptWord> words)
        {
            if (garden == null || garden.FrondDays == null || garden.FrondDays.Count == 0)
                return new LookFrame[0];
            int first = garden.FrondDays[0];
            int start = today - (WeekDays - 1);
            if (start < first) start = first;
            if (start > today) return new LookFrame[0];

            int lit = 0;
            for (int i = 0; i < garden.FrondDays.Count; i++)
            {
                if (garden.FrondDays[i] < start) lit++;
            }

            var frames = new List<LookFrame>();
            for (int day = start; day <= today; day++)
            {
                bool kept = false;
                for (int i = 0; i < garden.FrondDays.Count; i++)
                {
                    if (garden.FrondDays[i] != day) continue;
                    kept = true;
                    break;
                }
                if (kept) lit++;
                frames.Add(new LookFrame(day, kept, lit, OneWord.On(words, day)));
            }
            return frames.ToArray();
        }
    }
}

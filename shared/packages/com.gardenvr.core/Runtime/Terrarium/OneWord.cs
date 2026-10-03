using System;
using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>One kept evening word. The stone index is not stored; the word is.</summary>
    public sealed class KeptWord
    {
        public int Day;
        public string Word;
    }

    /// <summary>
    /// Six word stones. Pinch one and it is that day's word.
    /// Nothing is scored, charted, or typed. A second pinch the same day does nothing.
    /// Keeping a word does not grow the garden. The ritual for that day has to be done first.
    /// </summary>
    public sealed class OneWord
    {
        public const int Count = 6;

        public static readonly string[] Stones = { "calm", "tired", "glad", "full", "quiet", "light" };

        public int? Picked { get; private set; }

        public bool Kept { get { return Picked.HasValue; } }

        public string Word
        {
            get { return Picked.HasValue ? Stones[Picked.Value] : null; }
        }

        public static int IndexOf(string word)
        {
            if (string.IsNullOrEmpty(word)) return -1;
            for (int i = 0; i < Stones.Length; i++)
            {
                if (string.Equals(Stones[i], word, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        public static string Canonical(string word)
        {
            int index = IndexOf(word);
            return index < 0 ? null : Stones[index];
        }

        /// <summary>The word kept on <paramref name="day"/>, or null.</summary>
        public static string On(IList<KeptWord> words, int day)
        {
            if (words == null) return null;
            for (int i = 0; i < words.Count; i++)
            {
                KeptWord row = words[i];
                if (row == null || row.Day != day) continue;
                string word = Canonical(row.Word);
                if (word != null) return word;
            }
            return null;
        }

        /// <summary>One stone, once. A repeat, a finished choice, or a bad index does nothing.</summary>
        public bool TryPick(int index)
        {
            if (Kept) return false;
            if (index < 0 || index >= Count) return false;
            Picked = index;
            return true;
        }

        public void Restore(string word)
        {
            int index = IndexOf(word);
            if (index < 0) return;
            Picked = index;
        }

        public void ClearPick()
        {
            Picked = null;
        }

        public void Reset()
        {
            Picked = null;
        }

        /// <summary>
        /// Writes <see cref="Word"/> for <paramref name="today"/> when that day already has a ritual
        /// and no word yet. A day before the garden, or before a word already kept, throws.
        /// The garden is not changed.
        /// </summary>
        public bool TryKeep(Garden garden, int today, IList<KeptWord> words)
        {
            if (!Kept) return false;
            if (garden == null) throw new ArgumentNullException(nameof(garden));
            if (words == null) throw new ArgumentNullException(nameof(words));
            if (!garden.AcceptsDay(today))
                throw new ArgumentException("clock went backwards; the core refuses to rewrite history");
            for (int i = 0; i < words.Count; i++)
            {
                KeptWord row = words[i];
                if (row != null && row.Day > today)
                    throw new ArgumentException("clock went backwards; the core refuses to rewrite history");
            }
            if (garden.LastRitualDay != today) return false;
            if (On(words, today) != null) return false;
            words.Add(new KeptWord { Day = today, Word = Word });
            return true;
        }
    }
}

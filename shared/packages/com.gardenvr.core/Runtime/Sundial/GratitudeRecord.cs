using System.Collections.Generic;

namespace GardenVR.Core
{
    /// <summary>
    /// One inked midday. <see cref="Day"/> is the garden-day index.
    /// <see cref="Symbol"/> is 0..4. There is no caption and no note.
    /// </summary>
    public sealed class GratitudeMark
    {
        public int Day;
        public int Symbol;
    }

    /// <summary>
    /// The midday gratitude record. Five symbols, addressed by index.
    /// The first choice for a garden day sticks. A second symbol that day is refused.
    /// Each new ink authorises one ritual tend of the midday arc.
    /// Marks loaded from a save do not authorise a tend. This type does not write the ledger.
    /// </summary>
    public sealed class GratitudeRecord
    {
        public const int SymbolCount = 5;

        /// <summary>Stable ids for the five ink drawings. The save stores the index, not the id.</summary>
        public static readonly string[] Keys = { "sun", "leaf", "cup", "star", "hearth" };

        readonly List<GratitudeMark> _marks = new List<GratitudeMark>();
        int _pendingTends;

        public ArcId TendsArc { get { return ArcId.Midday; } }
        public TendSource TendsAs { get { return TendSource.Ritual; } }
        public bool TendAuthorised { get { return _pendingTends > 0; } }
        public IReadOnlyList<GratitudeMark> Marks { get { return _marks; } }

        public GratitudeRecord() { }

        public GratitudeRecord(IEnumerable<GratitudeMark> marks)
        {
            if (marks == null) return;
            foreach (GratitudeMark mark in marks)
            {
                if (mark == null || !IsSymbol(mark.Symbol)) continue;
                if (Find(mark.Day) != null) continue;
                _marks.Add(new GratitudeMark { Day = mark.Day, Symbol = mark.Symbol });
            }
        }

        public static bool IsSymbol(int symbol)
        {
            return symbol >= 0 && symbol < SymbolCount;
        }

        public static int ParseKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i] == key) return i;
            }
            return -1;
        }

        /// <summary>
        /// Inks <paramref name="symbol"/> for <paramref name="day"/>.
        /// True when this call wrote the mark and authorised one midday tend.
        /// </summary>
        public bool TryInk(int day, int symbol)
        {
            if (!IsSymbol(symbol)) return false;
            if (Find(day) != null) return false;
            _marks.Add(new GratitudeMark { Day = day, Symbol = symbol });
            _pendingTends++;
            return true;
        }

        public bool Done(int day)
        {
            return Find(day) != null;
        }

        /// <summary>The symbol inked on <paramref name="day"/>, or -1 when that day is blank.</summary>
        public int SymbolOn(int day)
        {
            GratitudeMark mark = Find(day);
            return mark == null ? -1 : mark.Symbol;
        }

        /// <summary>True once per ink that has not been handed to the ledger yet.</summary>
        public bool ConsumeTend()
        {
            if (_pendingTends <= 0) return false;
            _pendingTends--;
            return true;
        }

        GratitudeMark Find(int day)
        {
            for (int i = 0; i < _marks.Count; i++)
            {
                if (_marks[i].Day == day) return _marks[i];
            }
            return null;
        }
    }
}

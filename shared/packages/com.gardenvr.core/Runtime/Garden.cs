using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GardenVR.Core
{
    /// <summary>What the garden does in answer to a completed ritual. The scene plays exactly this, nothing invented.</summary>
    public readonly struct GrowthAnswer
    {
        public readonly bool NewFrond;        // first ritual of the day: one permanent frond
        public readonly bool Recovered;       // the garden was drooping and lifts again
        public readonly bool Flower;          // every 7th frond opens a flower
        public readonly int DewBeads;         // extra rituals on the same day add dew, never extra fronds
        public GrowthAnswer(bool f, bool r, bool fl, int dew) { NewFrond = f; Recovered = r; Flower = fl; DewBeads = dew; }
    }

    /// <summary>
    /// The growth record. Rules, each with a test:
    ///  1. Fronds only accumulate - nothing ever removes one (no streak, no reset).
    ///  2. One frond per day of practice; more sessions that day add dew, so growth cannot be farmed.
    ///  3. Missed days lower Vitality (a droop the scene shows), floored at 0.6 - the garden never dies.
    ///  4. The next completed ritual restores full vitality at once: a missed day is always recoverable.
    /// Days are integers (days since 2000-01-01, local calendar) so the core never touches a clock.
    /// </summary>
    public sealed class Garden
    {
        public const float VitalityFloor = 0.6f;
        readonly List<int> _frondDays = new List<int>();
        public IReadOnlyList<int> FrondDays => _frondDays;
        public int Fronds => _frondDays.Count;
        public int RitualsCompleted { get; private set; }
        public int DewToday { get; private set; }
        public int? LastRitualDay { get; private set; }
        /// <summary>How many times the user came back after a gap. Shown as a kindness, never as a count of misses.</summary>
        public int Returns { get; private set; }

        public int Flowers => Fronds / 7;

        public int DaysSinceRitual(int today) => LastRitualDay.HasValue ? Math.Max(0, today - LastRitualDay.Value) : 0;

        public float Vitality(int today)
        {
            int gap = DaysSinceRitual(today);
            if (gap <= 1) return 1f;
            return Math.Max(VitalityFloor, 1f - 0.15f * (gap - 1));
        }

        public GrowthAnswer CompleteRitual(int today)
        {
            if (LastRitualDay.HasValue && today < LastRitualDay.Value)
                throw new ArgumentException("clock went backwards; the core refuses to rewrite history");
            bool drooping = Vitality(today) < 1f;
            bool newDay = LastRitualDay != today;
            RitualsCompleted++;
            if (!newDay)
            {
                DewToday++;
                return new GrowthAnswer(false, false, false, DewToday);
            }
            if (drooping) Returns++;
            DewToday = 0;
            LastRitualDay = today;
            _frondDays.Add(today);
            return new GrowthAnswer(true, drooping, Fronds % 7 == 0, 0);
        }

        // ---- persistence: one plain line, versioned, human-readable on disk ----
        public string Serialize() =>
            string.Format(CultureInfo.InvariantCulture, "terrarium/v1;rituals={0};last={1};returns={2};dew={3};fronds={4}",
                RitualsCompleted, LastRitualDay?.ToString(CultureInfo.InvariantCulture) ?? "-", Returns, DewToday,
                string.Join(",", _frondDays.Select(d => d.ToString(CultureInfo.InvariantCulture))));

        public static Garden Deserialize(string line)
        {
            var g = new Garden();
            if (string.IsNullOrWhiteSpace(line)) return g;
            var parts = line.Split(';');
            if (parts[0] != "terrarium/v1") throw new FormatException("unknown garden format: " + parts[0]);
            foreach (var kv in parts.Skip(1))
            {
                var i = kv.IndexOf('=');
                var k = kv.Substring(0, i); var v = kv.Substring(i + 1);
                switch (k)
                {
                    case "rituals": g.RitualsCompleted = int.Parse(v, CultureInfo.InvariantCulture); break;
                    case "last": g.LastRitualDay = v == "-" ? (int?)null : int.Parse(v, CultureInfo.InvariantCulture); break;
                    case "returns": g.Returns = int.Parse(v, CultureInfo.InvariantCulture); break;
                    case "dew": g.DewToday = int.Parse(v, CultureInfo.InvariantCulture); break;
                    case "fronds":
                        if (v.Length > 0) g._frondDays.AddRange(v.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)));
                        break;
                }
            }
            return g;
        }

        public static int DayNumber(DateTime localDate) => (int)(localDate.Date - new DateTime(2000, 1, 1)).TotalDays;
    }
}

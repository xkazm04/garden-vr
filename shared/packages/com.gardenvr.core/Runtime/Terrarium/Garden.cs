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
        public readonly bool Flower;          // a new frond at 6, 12, 18, ... opens a flower
        public readonly int DewBeads;         // extra rituals on the same day add dew, never extra fronds
        public GrowthAnswer(bool f, bool r, bool fl, int dew) { NewFrond = f; Recovered = r; Flower = fl; DewBeads = dew; }
    }

    /// <summary>
    /// The growth record. Rules, each with a test:
    ///  1. Fronds only accumulate - nothing ever removes one (no streak, no reset).
    ///  2. One frond per day of practice; more sessions that day add dew, so growth cannot be farmed.
    ///  3. Missed days lower Vitality (a droop the scene shows), floored at 0.6 - the garden never dies.
    ///  4. The next completed ritual restores full vitality at once: a missed day is always recoverable.
    ///  5. The first flower opens at 6 fronds, then one more every 6 (6, 12, 18, ...).
    /// Days are integers (days since 2000-01-01, local calendar) so the core never touches a clock.
    /// </summary>
    public sealed class Garden
    {
        public const float VitalityFloor = 0.6f;
        public const int FirstFlowerAt = 6;   // one miss in a week still flowers on day 7
        public const int FlowerEvery = 6;
        public const int FrondsBeforeInnerLayer = 12;
        readonly List<int> _frondDays = new List<int>();
        public IReadOnlyList<int> FrondDays => _frondDays;
        public int Fronds => _frondDays.Count;
        public int RitualsCompleted { get; private set; }
        public int DewToday { get; private set; }
        public int? LastRitualDay { get; private set; }
        /// <summary>How many times the user came back after a gap. Shown as a kindness, never as a count of misses.</summary>
        public int Returns { get; private set; }

        public int Flowers => Fronds < FirstFlowerAt ? 0 : 1 + (Fronds - FirstFlowerAt) / FlowerEvery;

        /// <summary>Season week from lifetime fronds. A quiet gap does not move it.</summary>
        public int SeasonWeek => Season.Week(Fronds);
        /// <summary>0 through week 1, 1 from week 6 on. Derived, so it is not stored.</summary>
        public float SeasonWarmth => Season.Warmth(Fronds);
        public bool SeasonSecondSpecies => Season.SecondSpecies(Fronds);
        public int SeasonTinyFlowers => Season.TinyFlowers(Fronds);
        public int SeasonSprigs => Season.Sprigs(Fronds);

        public int DaysSinceRitual(int today) => LastRitualDay.HasValue ? Math.Max(0, today - LastRitualDay.Value) : 0;

        public float Vitality(int today)
        {
            return VitalityForGap(DaysSinceRitual(today));
        }

        /// <summary>True when the garden is drooping on <paramref name="today"/>: a gap of more than one day, so Vitality is below 1.</summary>
        public bool Drooping(int today) => Vitality(today) < 1f;

        /// <summary>
        /// True when a ritual was completed on exactly <paramref name="today"/>. False on a fresh garden and false when the clock
        /// was set back before the last ritual (that day is not today). This is the definition JarView.cs:588 (Waiting is its
        /// negation) and JarRitualController.cs:969 use. JarRitualController.cs:1366 differs: it tests DaysSinceRitual(today) &gt; 0,
        /// which is false on a fresh garden and false after a clock set back (the gap clamps to 0), so there it reads as "done".
        /// </summary>
        public bool RitualDoneOn(int today) => LastRitualDay.HasValue && LastRitualDay.Value == today;

        /// <summary>The one droop curve: full through a one-day gap, then 0.15 lower per further day, floored.</summary>
        public static float VitalityForGap(int gap)
        {
            if (gap <= 1) return 1f;
            return Math.Max(VitalityFloor, 1f - 0.15f * (gap - 1));
        }

        /// <summary>False when <paramref name="today"/> is before the last ritual. The core will not rewrite that day.</summary>
        public bool AcceptsDay(int today)
        {
            return !LastRitualDay.HasValue || today >= LastRitualDay.Value;
        }

        public GrowthAnswer CompleteRitual(int today)
        {
            if (!AcceptsDay(today))
                throw new ArgumentException("clock went backwards; the core refuses to rewrite history");
            bool drooping = Drooping(today);
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
            return new GrowthAnswer(true, drooping, OpensFlower(Fronds), 0);
        }

        /// <summary>Growth fields only. Habits, tends, and settings stay with the document the app owns.</summary>
        public TerrariumSave ToSave()
        {
            var save = new TerrariumSave();
            save.SchemaVersion = TerrariumSaveMigrations.CurrentSchema;
            save.FrondDays = _frondDays.ToArray();
            save.DewToday = DewToday;
            save.LastRitualDay = LastRitualDay;
            save.Returns = Returns;
            save.RitualsCompleted = RitualsCompleted;
            return save;
        }

        public static Garden FromSave(TerrariumSave save)
        {
            var garden = new Garden();
            if (save == null) return garden;
            if (save.FrondDays != null)
            {
                for (int i = 0; i < save.FrondDays.Length; i++)
                    garden._frondDays.Add(save.FrondDays[i]);
            }
            garden.DewToday = save.DewToday;
            garden.LastRitualDay = save.LastRitualDay;
            garden.Returns = save.Returns;
            garden.RitualsCompleted = save.RitualsCompleted;
            return garden;
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

        /// <summary>True when this frond count is 6, 12, 18, ...</summary>
        static bool OpensFlower(int fronds) =>
            fronds >= FirstFlowerAt && (fronds - FirstFlowerAt) % FlowerEvery == 0;
    }
}

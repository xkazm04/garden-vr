using System;
using GardenVR.Core;

namespace GardenVR.Terrarium
{
    /// <summary>
    /// One posed garden built from <see cref="Garden"/> and a <see cref="FixedClock"/>.
    /// Captures and the day tests share these four journeys.
    /// </summary>
    public sealed class GardenJourney
    {
        public static readonly DateTimeOffset Origin = new DateTimeOffset(2026, 10, 3, 21, 0, 0, TimeSpan.Zero);

        public readonly Garden Garden;
        public readonly int Today;
        public readonly bool Recovered;
        public readonly float Vitality;

        public GardenJourney(Garden garden, int today, bool recovered)
        {
            Garden = garden;
            Today = today;
            Recovered = recovered;
            Vitality = garden == null ? 1f : garden.Vitality(today);
        }

        public static GardenJourney Build(string name)
        {
            var clock = new FixedClock(Origin, TimeZoneInfo.Utc);
            var garden = new Garden();
            bool recovered = false;
            if (name == "day1")
            {
                garden.CompleteRitual(Day(clock));
            }
            else if (name == "missed2")
            {
                garden.CompleteRitual(Day(clock));
                clock.Advance(TimeSpan.FromDays(3));
            }
            else if (name == "recovered")
            {
                garden.CompleteRitual(Day(clock));
                clock.Advance(TimeSpan.FromDays(3));
                recovered = garden.CompleteRitual(Day(clock)).Recovered;
            }
            else if (name == "day7")
            {
                for (int d = 0; d < 7; d++)
                {
                    if (d == 5)
                    {
                        clock.Advance(TimeSpan.FromDays(1));
                        continue;
                    }
                    garden.CompleteRitual(Day(clock));
                    if (d < 6) clock.Advance(TimeSpan.FromDays(1));
                }
            }
            else
            {
                throw new ArgumentException("unknown journey '" + name + "'");
            }
            return new GardenJourney(garden, Day(clock), recovered);
        }

        static int Day(FixedClock clock)
        {
            return GardenDay.From(clock.Now, GardenDay.DefaultBoundary).Index;
        }
    }
}

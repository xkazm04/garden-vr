using System;
using System.Globalization;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

/// <summary>Season is lifetime fronds only. It pauses on a quiet week and it never runs backwards.</summary>
public class SeasonTests
{
    const int Day0 = 9770; // 2026-10-01

    [Fact]
    public void Week_one_is_the_locked_mint_for_every_frond_in_it()
    {
        Assert.Equal(0, Season.FrondsAtWeek(1));
        Assert.Equal(6, Season.FrondsAtEndOfWeek(1));
        for (int n = 0; n <= 6; n++)
        {
            Assert.Equal(1, Season.Week(n));
            Assert.Equal(0f, Season.Warmth(n));
            Assert.False(Season.SecondSpecies(n));
            Assert.Equal(0, Season.TinyFlowers(n));
            Assert.Equal(0, Season.Sprigs(n));
        }
    }

    [Fact]
    public void Week_three_and_week_six_are_the_named_steps()
    {
        int week3 = Season.FrondsAtWeek(3);
        int week3End = Season.FrondsAtEndOfWeek(3);
        int week6 = Season.FrondsAtWeek(6);
        int week6End = Season.FrondsAtEndOfWeek(6);

        Assert.Equal(14, week3);
        Assert.Equal(20, week3End);
        Assert.Equal(3, Season.Week(week3));
        Assert.Equal(3, Season.Week(week3End));
        Assert.Equal(0.25f, Season.Warmth(week3), 5);
        Assert.True(Season.Warmth(week3End) > Season.Warmth(week3));
        Assert.True(Season.Warmth(week3End) < 1f);
        Assert.True(Season.SecondSpecies(week3));
        Assert.Equal(1, Season.TinyFlowers(week3));
        Assert.Equal(1, Season.TinyFlowers(week3End));
        Assert.Equal(1, Season.Sprigs(week3End));

        Assert.Equal(35, week6);
        Assert.Equal(41, week6End);
        Assert.Equal(6, Season.Week(week6));
        Assert.Equal(6, Season.Week(week6End));
        Assert.Equal(1f, Season.Warmth(week6));
        Assert.Equal(1f, Season.Warmth(week6End));
        Assert.Equal(4, Season.TinyFlowers(week6));
        Assert.Equal(4, Season.TinyFlowers(week6End));
        Assert.Equal(2, Season.Sprigs(week6));
        Assert.Equal(1f, Season.Warmth(400));
        Assert.True(Season.Week(400) > Season.GoldWeek);
        Assert.True(Season.TinyFlowers(400) > Season.TinyFlowers(week6));
    }

    [Fact]
    public void Warmth_rises_with_each_frond_between_week_two_and_week_six()
    {
        float previous = Season.Warmth(Season.DaysInWeek);
        Assert.Equal(0f, previous);
        for (int n = Season.DaysInWeek + 1; n <= Season.FrondsAtWeek(Season.GoldWeek); n++)
        {
            float warmth = Season.Warmth(n);
            Assert.True(warmth > previous, "warmth stalled at frond " + n.ToString(CultureInfo.InvariantCulture));
            previous = warmth;
        }
        Assert.Equal(1f, previous);
    }

    [Fact]
    public void Season_is_monotonic_for_the_first_eighty_fronds()
    {
        int week = 0;
        float warmth = 0f;
        int blooms = 0;
        int sprigs = 0;
        bool species = false;
        for (int n = 0; n <= 80; n++)
        {
            int nextWeek = Season.Week(n);
            float nextWarmth = Season.Warmth(n);
            int nextBlooms = Season.TinyFlowers(n);
            int nextSprigs = Season.Sprigs(n);
            bool nextSpecies = Season.SecondSpecies(n);
            Assert.True(nextWeek >= week);
            Assert.True(nextWarmth + 1e-5f >= warmth);
            Assert.True(nextBlooms >= blooms);
            Assert.True(nextSprigs >= sprigs);
            if (species) Assert.True(nextSpecies);
            Assert.Equal(nextSpecies, nextSprigs > 0);
            week = nextWeek;
            warmth = nextWarmth;
            blooms = nextBlooms;
            sprigs = nextSprigs;
            species = nextSpecies;
        }
    }

    [Fact]
    public void A_quiet_fortnight_pauses_the_season_and_the_next_frond_does_not_reverse_it()
    {
        var garden = new Garden();
        for (int d = 0; d < 20; d++) garden.CompleteRitual(Day0 + d);
        Assert.Equal(20, garden.Fronds);
        Assert.Equal(3, garden.SeasonWeek);
        float warmth = garden.SeasonWarmth;
        int blooms = garden.SeasonTinyFlowers;
        int sprigs = garden.SeasonSprigs;
        Assert.True(garden.SeasonSecondSpecies);
        Assert.Equal(warmth, Season.Warmth(20));

        int quiet = Day0 + 19 + 14;
        Assert.True(garden.Vitality(quiet) < 1f);
        Assert.Equal(20, garden.Fronds);
        Assert.Equal(3, garden.SeasonWeek);
        Assert.Equal(warmth, garden.SeasonWarmth);
        Assert.Equal(blooms, garden.SeasonTinyFlowers);
        Assert.Equal(sprigs, garden.SeasonSprigs);

        garden.CompleteRitual(Day0 + 19);
        Assert.Equal(20, garden.Fronds);
        Assert.Equal(warmth, garden.SeasonWarmth);
        Assert.Equal(1, garden.DewToday);

        garden.CompleteRitual(quiet);
        Assert.Equal(21, garden.Fronds);
        Assert.True(garden.SeasonWarmth + 1e-6f >= warmth);
        Assert.True(garden.SeasonTinyFlowers >= blooms);
        Assert.True(garden.SeasonSprigs >= sprigs);
        Assert.True(garden.SeasonWeek >= 3);
    }

    [Fact]
    public void Negative_frond_count_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Season.Week(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Season.Warmth(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Season.FrondsAtWeek(0));
    }

    [Fact]
    public void Property_season_never_reverses_over_random_lives()
    {
        string detail = SeasonMonotone.FindViolation(() => new GardenLife(), seed: 42, lives: 1000, steps: 120, day0: Day0);
        Assert.Null(detail);
    }

    [Fact]
    public void Negative_control_shedding_a_frond_reverses_the_season()
    {
        string detail = SeasonMonotone.FindViolation(() => new ShedsAtTwenty(), seed: 42, lives: 40, steps: 80, day0: Day0);
        Assert.False(string.IsNullOrEmpty(detail));
        Assert.Contains("warmth fell", detail);
    }
}

/// <summary>Grows to 20 fronds, then the next new day removes one. The season check must catch that.</summary>
sealed class ShedsAtTwenty : IFrondLife
{
    readonly System.Collections.Generic.List<int> _days = new System.Collections.Generic.List<int>();
    int? _last;
    public int Fronds => _days.Count;

    public void CompleteRitual(int today)
    {
        if (_last.HasValue && today < _last.Value)
            throw new ArgumentException("clock went backwards");
        if (_last == today) return;
        _last = today;
        if (_days.Count >= 20)
            _days.RemoveAt(_days.Count - 1);
        else
            _days.Add(today);
    }
}

/// <summary>Same seeded schedule as the frond property. Null means week, warmth, blossoms and sprigs never fell.</summary>
static class SeasonMonotone
{
    public static string FindViolation(Func<IFrondLife> create, int seed, int lives, int steps, int day0)
    {
        var rng = new Random(seed);
        for (int life = 0; life < lives; life++)
        {
            IFrondLife garden = create();
            int day = day0;
            int fronds = garden.Fronds;
            int week = Season.Week(fronds);
            float warmth = Season.Warmth(fronds);
            int blooms = Season.TinyFlowers(fronds);
            int sprigs = Season.Sprigs(fronds);
            for (int step = 0; step < steps; step++)
            {
                day += rng.Next(0, 4);
                if (rng.NextDouble() < 0.6) garden.CompleteRitual(day);
                fronds = garden.Fronds;
                int nextWeek = Season.Week(fronds);
                float nextWarmth = Season.Warmth(fronds);
                int nextBlooms = Season.TinyFlowers(fronds);
                int nextSprigs = Season.Sprigs(fronds);
                if (nextWeek < week || nextWarmth + 1e-6f < warmth || nextBlooms < blooms || nextSprigs < sprigs)
                {
                    return string.Format(CultureInfo.InvariantCulture,
                        "life {0} step {1}: warmth fell from {2:0.####} to {3:0.####} (fronds {4}, week {5} to {6})",
                        life, step, warmth, nextWarmth, fronds, week, nextWeek);
                }
                week = nextWeek;
                warmth = nextWarmth;
                blooms = nextBlooms;
                sprigs = nextSprigs;
            }
        }
        return null;
    }
}

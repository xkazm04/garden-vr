using System;
using System.Collections.Generic;
using System.Globalization;
using GardenVR.Core;
using Xunit;

/// <summary>Invariants added when the spike core became the Terrarium-owned rules (D5 and the state oracle).</summary>
public class TerrariumInvariantTests
{
    const int Day0 = 9770; // 2026-10-01

    [Fact]
    public void First_flower_at_6_and_second_at_12()
    {
        var g = new Garden();
        for (int n = 1; n <= 12; n++)
        {
            var answer = g.CompleteRitual(Day0 + n - 1);
            bool opens = n == Garden.FirstFlowerAt || n == Garden.FirstFlowerAt + Garden.FlowerEvery;
            Assert.Equal(opens, answer.Flower);
            int expectFlowers = n < Garden.FirstFlowerAt ? 0 : 1 + (n - Garden.FirstFlowerAt) / Garden.FlowerEvery;
            Assert.Equal(expectFlowers, g.Flowers);
        }
        Assert.Equal(2, g.Flowers);
        var dew = g.CompleteRitual(Day0 + 11); // same day as the 12th frond: dew, not another flower
        Assert.False(dew.NewFrond);
        Assert.False(dew.Flower);
        Assert.Equal(1, dew.DewBeads);
        Assert.Equal(2, g.Flowers);
        Assert.Equal(12, g.Fronds);
    }

    [Fact]
    public void A_seven_day_week_with_one_missed_day_flowers_on_day_7()
    {
        var g = new Garden();
        GrowthAnswer last = default;
        const int missed = 3;
        for (int d = 0; d < 7; d++)
        {
            if (d == missed) continue;
            last = g.CompleteRitual(Day0 + d);
        }
        Assert.Equal(6, g.Fronds);
        Assert.True(last.Flower);
        Assert.Equal(1, g.Flowers);
        Assert.Equal(Day0 + 6, g.LastRitualDay); // day 7 of the week
    }

    [Fact]
    public void Vitality_reaches_the_floor_after_a_long_gap_and_one_ritual_restores_it()
    {
        var g = new Garden();
        g.CompleteRitual(Day0);
        int floorGap = -1;
        for (int gap = 0; gap <= 40; gap++)
        {
            if (g.Vitality(Day0 + gap) <= Garden.VitalityFloor + 1e-4f)
            {
                floorGap = gap;
                break;
            }
        }
        Assert.True(floorGap > 1, "a long gap is required before the floor");
        Assert.Equal(Garden.VitalityFloor, g.Vitality(Day0 + floorGap), 3);
        int longGap = floorGap + 100;
        Assert.Equal(Garden.VitalityFloor, g.Vitality(Day0 + longGap), 3);
        int fronds = g.Fronds;
        var answer = g.CompleteRitual(Day0 + longGap);
        Assert.True(answer.Recovered);
        Assert.True(answer.NewFrond);
        Assert.False(answer.Flower);
        Assert.Equal(1f, g.Vitality(Day0 + longGap));
        Assert.Equal(fronds + 1, g.Fronds);
    }

    [Fact]
    public void TerrariumState_ToJson_matches_the_golden_string()
    {
        var breath = new BreathSession();
        var garden = new Garden();
        var fresh = TerrariumState.Capture(breath, garden, Day0);
        Assert.Equal(
            "{\"phase\":\"Waiting\",\"breaths\":0,\"uncoil\":0,\"fog\":0,\"fronds\":0,\"flowers\":0,\"dewToday\":0,\"vitality\":1,\"rituals\":0}",
            fresh.ToJson());

        garden.CompleteRitual(Day0);
        garden.CompleteRitual(Day0);
        var answered = new TerrariumState(
            BreathPhase.Complete, 6, 1f, 0f,
            garden.Fronds, garden.Flowers, garden.DewToday, garden.Vitality(Day0), garden.RitualsCompleted);
        Assert.Equal(
            "{\"phase\":\"Complete\",\"breaths\":6,\"uncoil\":1,\"fog\":0,\"fronds\":1,\"flowers\":0,\"dewToday\":1,\"vitality\":1,\"rituals\":2}",
            answered.ToJson());
    }

    [Fact]
    public void Property_fronds_never_decrease_over_1000_random_lives()
    {
        var detail = FrondMonotone.FindViolation(() => new GardenLife(), seed: 42, lives: 1000, steps: 120, day0: Day0);
        Assert.Null(detail);
    }

    [Fact]
    public void Negative_control_a_garden_that_drops_a_frond_reports_a_violation()
    {
        var detail = FrondMonotone.FindViolation(() => new DroppingGarden(), seed: 42, lives: 1000, steps: 120, day0: Day0);
        Assert.False(string.IsNullOrEmpty(detail));
        Assert.Contains("fronds fell", detail);
    }
}

/// <summary>The frond-count surface the monotone property checks. The broken double implements it too.</summary>
interface IFrondLife
{
    int Fronds { get; }
    void CompleteRitual(int today);
}

/// <summary>Real garden. Fronds only accumulate.</summary>
sealed class GardenLife : IFrondLife
{
    readonly Garden _garden = new Garden();
    public int Fronds => _garden.Fronds;
    public void CompleteRitual(int today) => _garden.CompleteRitual(today);
}

/// <summary>
/// Deliberately broken garden: one frond per new day, except a long gap (3 or more days) removes one.
/// The property checker must report that removal.
/// </summary>
sealed class DroppingGarden : IFrondLife
{
    readonly List<int> _frondDays = new List<int>();
    int? _last;
    public int Fronds => _frondDays.Count;

    public void CompleteRitual(int today)
    {
        if (_last.HasValue && today < _last.Value)
            throw new ArgumentException("clock went backwards");
        if (_last == today) return;
        int gap = _last.HasValue ? today - _last.Value : 0;
        _last = today;
        if (gap >= 3 && _frondDays.Count > 0)
            _frondDays.RemoveAt(_frondDays.Count - 1);
        else
            _frondDays.Add(today);
    }
}

/// <summary>Same seeded schedule for the real garden and the broken double. Null means the property held.</summary>
static class FrondMonotone
{
    public static string FindViolation(Func<IFrondLife> create, int seed, int lives, int steps, int day0)
    {
        var rng = new Random(seed);
        for (int life = 0; life < lives; life++)
        {
            var garden = create();
            int day = day0;
            int prev = garden.Fronds;
            for (int step = 0; step < steps; step++)
            {
                day += rng.Next(0, 4);
                if (rng.NextDouble() < 0.6) garden.CompleteRitual(day);
                if (garden.Fronds < prev)
                    return string.Format(CultureInfo.InvariantCulture,
                        "life {0} step {1}: fronds fell from {2} to {3}", life, step, prev, garden.Fronds);
                prev = garden.Fronds;
            }
        }
        return null;
    }
}

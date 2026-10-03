using System;
using GardenVR.Core;
using Xunit;

public class ThreeGoodThingsTests
{
    const int Day0 = 9770;

    [Fact]
    public void Three_distinct_leaves_count_the_day_once()
    {
        var garden = new Garden();
        var ritual = new ThreeGoodThings();
        Assert.True(ritual.TryPlace(0));
        Assert.False(ritual.TryPlace(0));
        Assert.Equal(1, ritual.Placed);
        Assert.True(ritual.TryPlace(1));
        GrowthAnswer early;
        Assert.False(ritual.TryCountDay(garden, Day0, null, out early));
        Assert.Equal(0, garden.Fronds);
        Assert.Equal(0, garden.RitualsCompleted);
        Assert.True(ritual.TryPlace(2));
        Assert.True(ritual.Complete);
        Assert.False(ritual.TryPlace(1));
        Assert.False(ritual.TryPlace(3));
        Assert.False(ritual.TryPlace(-1));

        GrowthAnswer answer;
        Assert.True(ritual.TryCountDay(garden, Day0, null, out answer));
        Assert.True(answer.NewFrond);
        Assert.False(answer.Recovered);
        Assert.Equal(0, answer.DewBeads);
        Assert.Equal(1, garden.Fronds);
        Assert.Equal(1, garden.RitualsCompleted);
        Assert.Equal(Day0, garden.LastRitualDay);

        var again = new ThreeGoodThings();
        Assert.True(again.TryPlace(0));
        Assert.True(again.TryPlace(1));
        Assert.True(again.TryPlace(2));
        GrowthAnswer extra;
        Assert.False(again.TryCountDay(garden, Day0, Day0, out extra));
        Assert.Equal(1, garden.Fronds);
        Assert.Equal(0, garden.DewToday);
        Assert.Equal(1, garden.RitualsCompleted);
    }

    [Fact]
    public void After_the_breaths_the_same_day_adds_dew_not_a_frond()
    {
        var garden = new Garden();
        garden.CompleteRitual(Day0);
        var ritual = new ThreeGoodThings();
        ritual.TryPlace(2);
        ritual.TryPlace(0);
        ritual.TryPlace(1);
        GrowthAnswer answer;
        Assert.True(ritual.TryCountDay(garden, Day0, null, out answer));
        Assert.False(answer.NewFrond);
        Assert.Equal(1, answer.DewBeads);
        Assert.Equal(1, garden.Fronds);
        Assert.Equal(1, garden.DewToday);
        Assert.Equal(2, garden.RitualsCompleted);
    }

    [Fact]
    public void A_missed_day_then_three_drops_restores_and_fronds_only_rise()
    {
        var garden = new Garden();
        garden.CompleteRitual(Day0);
        Assert.True(garden.Vitality(Day0 + 4) < 1f);
        int before = garden.Fronds;
        var ritual = new ThreeGoodThings();
        ritual.TryPlace(0);
        ritual.TryPlace(1);
        ritual.TryPlace(2);
        GrowthAnswer answer;
        Assert.True(ritual.TryCountDay(garden, Day0 + 4, null, out answer));
        Assert.True(answer.NewFrond);
        Assert.True(answer.Recovered);
        Assert.Equal(before + 1, garden.Fronds);
        Assert.Equal(1f, garden.Vitality(Day0 + 4));
        Assert.True(garden.Fronds >= before);
    }

    [Fact]
    public void Fronds_only_rise_across_breaths_and_glad_days()
    {
        var garden = new Garden();
        var rng = new Random(25);
        int previous = 0;
        int? gladDay = null;
        for (int day = 0; day < 48; day++)
        {
            int today = Day0 + day;
            if (rng.Next(2) == 0)
            {
                garden.CompleteRitual(today);
            }
            else
            {
                var ritual = new ThreeGoodThings();
                int leaves = rng.Next(2) == 0 ? 3 : 2;
                for (int i = 0; i < leaves; i++) ritual.TryPlace(i);
                GrowthAnswer answer;
                if (ritual.TryCountDay(garden, today, gladDay, out answer)) gladDay = today;
            }
            Assert.True(garden.Fronds >= previous);
            previous = garden.Fronds;
        }
        Assert.True(garden.Fronds > 0);
    }

    [Fact]
    public void A_clock_set_back_is_still_refused()
    {
        var garden = new Garden();
        var ritual = new ThreeGoodThings();
        ritual.TryPlace(0);
        ritual.TryPlace(1);
        ritual.TryPlace(2);
        GrowthAnswer answer;
        Assert.True(ritual.TryCountDay(garden, Day0, null, out answer));
        var next = new ThreeGoodThings();
        next.TryPlace(0);
        next.TryPlace(1);
        next.TryPlace(2);
        GrowthAnswer ignored;
        Assert.Throws<ArgumentException>(() => next.TryCountDay(garden, Day0 - 1, null, out ignored));
        Assert.Equal(1, garden.Fronds);
    }

    [Fact]
    public void The_save_keeps_the_done_day_and_nothing_else_about_the_ritual()
    {
        var save = new TerrariumSave();
        Assert.False(save.GladDay.HasValue);
        Assert.DoesNotContain("GladDay", save.ToJson());
        save.GladDay = Day0;
        string json = save.ToJson();
        Assert.Contains("\"GladDay\":9770", json);
        Assert.DoesNotContain("leaf", json);
        Assert.DoesNotContain("glad", json.ToLowerInvariant().Replace("gladday", ""));
        TerrariumSave again = TerrariumSave.FromJson(json);
        Assert.Equal(Day0, again.GladDay);
        Assert.Equal(json, again.ToJson());
        again.GladDay = null;
        Assert.DoesNotContain("GladDay", again.ToJson());
    }
}

using System;
using GardenVR.Core;
using GardenVR.Terrarium;
using Xunit;

// GardenJourney.cs: the four posed gardens (Build, line 27) the captures and the day tests share.
public class GardenJourneyTests
{
    static int OriginDay()
    {
        return GardenDay.From(GardenJourney.Origin, GardenDay.DefaultBoundary).Index;
    }

    [Fact]
    public void Day1_has_one_frond_and_full_vitality_on_the_origin_day()
    {
        GardenJourney j = GardenJourney.Build("day1");
        Assert.Equal(OriginDay(), j.Today);
        Assert.Equal(1, j.Garden.Fronds);
        Assert.Equal(1f, j.Vitality);
        Assert.False(j.Recovered);
    }

    [Fact]
    public void Missed2_droops_without_losing_the_frond()
    {
        GardenJourney j = GardenJourney.Build("missed2");
        Assert.Equal(OriginDay() + 3, j.Today);
        Assert.Equal(1, j.Garden.Fronds);
        Assert.True(j.Vitality < 1f);
        Assert.True(j.Vitality >= Garden.VitalityFloor);
        Assert.True(j.Garden.Drooping(j.Today));
    }

    [Fact]
    public void Recovered_lifts_the_garden_and_keeps_both_fronds()
    {
        GardenJourney j = GardenJourney.Build("recovered");
        Assert.True(j.Recovered);
        Assert.Equal(2, j.Garden.Fronds);
        Assert.Equal(1f, j.Vitality);
    }

    [Fact]
    public void Day7_skips_one_day_and_still_flowers()
    {
        GardenJourney j = GardenJourney.Build("day7");
        Assert.Equal(OriginDay() + 6, j.Today);
        Assert.Equal(6, j.Garden.Fronds);
        Assert.Equal(1, j.Garden.Flowers);
    }

    [Fact]
    public void An_unknown_journey_name_is_refused()
    {
        Assert.Throws<ArgumentException>(() => GardenJourney.Build("day99"));
    }
}

using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class LookBackTests
{
    [Fact]
    public void A_week_with_one_miss_holds_that_frame_and_still_flowers()
    {
        var garden = new Garden();
        garden.CompleteRitual(0);
        garden.CompleteRitual(1);
        garden.CompleteRitual(2);
        garden.CompleteRitual(3);
        garden.CompleteRitual(4);
        garden.CompleteRitual(6);

        Assert.Equal(6, garden.Fronds);
        Assert.Equal(1, garden.Flowers);

        LookFrame[] frames = LookBack.Week(garden, 6);
        Assert.Equal(7, frames.Length);
        Assert.Equal(2f, LookBack.HoldSeconds);
        for (int i = 0; i < 5; i++)
        {
            Assert.True(frames[i].Kept);
            Assert.Equal(i + 1, frames[i].Lit);
        }
        Assert.False(frames[5].Kept);
        Assert.Equal(frames[4].Lit, frames[5].Lit);
        Assert.Equal(5, frames[5].Lit);
        Assert.True(frames[6].Kept);
        Assert.Equal(6, frames[6].Lit);
        Assert.Equal(6, frames[6].Day);
    }

    [Fact]
    public void Days_before_the_garden_are_not_misses_and_an_empty_garden_is_silent()
    {
        Assert.Empty(LookBack.Week(new Garden(), 10));
        Assert.Empty(LookBack.Week(null, 10));

        var young = new Garden();
        young.CompleteRitual(4);
        young.CompleteRitual(5);
        LookFrame[] frames = LookBack.Week(young, 5);
        Assert.Equal(2, frames.Length);
        Assert.True(frames[0].Kept);
        Assert.Equal(1, frames[0].Lit);
        Assert.True(frames[1].Kept);
        Assert.Equal(2, frames[1].Lit);
    }

    [Fact]
    public void Older_fronds_stay_lit_while_the_last_week_plays()
    {
        var garden = new Garden();
        for (int day = 0; day <= 10; day++)
        {
            if (day == 8) continue;
            garden.CompleteRitual(day);
        }
        LookFrame[] frames = LookBack.Week(garden, 10);
        Assert.Equal(7, frames.Length);
        Assert.Equal(4, frames[0].Day);
        Assert.True(frames[0].Kept);
        Assert.Equal(5, frames[0].Lit);
        LookFrame miss = frames[4];
        Assert.Equal(8, miss.Day);
        Assert.False(miss.Kept);
        Assert.Equal(frames[3].Lit, miss.Lit);
        Assert.True(frames[6].Kept);
        Assert.Equal(10, frames[6].Lit);
    }
}

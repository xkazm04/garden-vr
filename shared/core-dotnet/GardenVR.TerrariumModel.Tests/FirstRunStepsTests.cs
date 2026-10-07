using GardenVR.Core;
using GardenVR.Terrarium;
using Xunit;

// FirstRunSteps.cs: ShouldRun (line 45) decides whether the tour starts, resumes or stays out of the way.
public class FirstRunStepsTests
{
    [Fact]
    public void A_fresh_garden_runs_the_tour_and_a_failed_load_never_does()
    {
        Assert.True(FirstRunSteps.ShouldRun(LoadOutcome.Fresh, null));
        Assert.False(FirstRunSteps.ShouldRun(LoadOutcome.Failed, null));
        Assert.False(FirstRunSteps.ShouldRun(LoadOutcome.Failed, FirstRunSteps.Hold));
    }

    [Fact]
    public void A_saved_unfinished_step_resumes_and_a_finished_or_missing_one_does_not()
    {
        Assert.True(FirstRunSteps.ShouldRun(LoadOutcome.Loaded, FirstRunSteps.Hold));
        Assert.True(FirstRunSteps.ShouldRun(LoadOutcome.Migrated, FirstRunSteps.Voice));
        Assert.False(FirstRunSteps.ShouldRun(LoadOutcome.Loaded, FirstRunSteps.Done));
        Assert.False(FirstRunSteps.ShouldRun(LoadOutcome.Loaded, null));
        Assert.False(FirstRunSteps.ShouldRun(LoadOutcome.Loaded, "tour.unknown"));
    }

    [Fact]
    public void A_later_step_is_further_along()
    {
        Assert.True(FirstRunSteps.AtLeast(FirstRunSteps.Seeds, FirstRunSteps.Hold));
        Assert.True(FirstRunSteps.AtLeast(FirstRunSteps.Hold, FirstRunSteps.Hold));
        Assert.False(FirstRunSteps.AtLeast(FirstRunSteps.Desk, FirstRunSteps.Hold));
        Assert.False(FirstRunSteps.AtLeast(null, FirstRunSteps.Room));
        Assert.False(FirstRunSteps.AtLeast(FirstRunSteps.Room, "tour.unknown"));
        Assert.Equal(0, FirstRunSteps.IndexOf(FirstRunSteps.Room));
        Assert.Equal(FirstRunSteps.Order.Length - 1, FirstRunSteps.IndexOf(FirstRunSteps.Done));
    }
}

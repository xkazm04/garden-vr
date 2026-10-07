using GardenVR.Sundial;
using Xunit;

// SeedCatalog.cs FirstRunSteps (lines 146-203): the order of the wizard and what it waits for.
public class FirstRunStepsTests
{
    [Fact]
    public void Steps_advance_in_order_and_done_has_no_next()
    {
        Assert.Equal(FirstRunSteps.Sweep, FirstRunSteps.Next(FirstRunSteps.Appear));
        Assert.Equal(FirstRunSteps.Done, FirstRunSteps.Next(FirstRunSteps.Breaths));
        Assert.Null(FirstRunSteps.Next(FirstRunSteps.Done));
        Assert.Null(FirstRunSteps.Next("nonsense"));
        Assert.Equal(-1, FirstRunSteps.Index(null));
    }

    [Fact]
    public void Only_an_unfinished_known_step_resumes()
    {
        Assert.True(FirstRunSteps.IsResume(FirstRunSteps.Drop));
        Assert.False(FirstRunSteps.IsResume(FirstRunSteps.Done));
        Assert.False(FirstRunSteps.IsResume(SundialService.DevSeedStep));
    }

    [Fact]
    public void A_pick_step_names_its_arc_and_waits_for_intent()
    {
        Assert.Equal("winddown", FirstRunSteps.PickArc(FirstRunSteps.PickWinddown));
        Assert.Null(FirstRunSteps.PickArc(FirstRunSteps.Drop));
        Assert.True(FirstRunSteps.WaitsForIntent(FirstRunSteps.PickMorning));
        Assert.False(FirstRunSteps.WaitsForIntent(FirstRunSteps.Sweep));
    }
}

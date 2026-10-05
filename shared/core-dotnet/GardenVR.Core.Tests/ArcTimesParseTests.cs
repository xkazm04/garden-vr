using GardenVR.Core;
using Xunit;

public class ArcTimesParseTests
{
    // ArcTimes.Parse promises the plan default for anything it cannot read. A member of the wrong type or a
    // number past the long range throws a different exception than the FormatException it catches.
    [Theory]
    [InlineData("{\"morning\":\"06:00\"}")]
    [InlineData("{\"dusk\":99999999999999999999}")]
    [InlineData("{\"boundary\":true}")]
    [InlineData("[1,2]")]
    [InlineData("{\"morning\":")]
    public void An_unreadable_schedule_falls_back_to_the_plan_default(string json)
    {
        Assert.Equal(ArcTimes.Default.ToJson(), ArcTimes.Parse(json).ToJson());
    }
}

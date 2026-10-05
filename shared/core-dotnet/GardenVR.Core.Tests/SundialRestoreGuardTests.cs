using System;
using GardenVR.Core;
using Xunit;

public class SundialRestoreGuardTests
{
    // A snapshot that says Running or Paused with no start instant is not an hour in progress. Restoring it
    // read the start as 1970, so the next Observe finished a full hour and authorised a tend nobody earned.
    [Theory]
    [InlineData("Running")]
    [InlineData("Paused")]
    public void A_snapshot_with_no_start_instant_restores_idle_and_authorises_nothing(string phase)
    {
        var block = FocusBlock.Restore(new FocusSnapshot { Phase = phase, StartedUtcMs = 0L, Arc = "Midday" });
        block.Observe(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(FocusPhase.Idle, block.Phase);
        Assert.False(block.TendAuthorised);
    }

    [Fact]
    public void A_real_running_snapshot_still_restores_and_finishes_on_time()
    {
        var start = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var live = new FocusBlock();
        live.TryStart(start, 12 * 60);
        var block = FocusBlock.Restore(live.Capture());
        block.Observe(start.AddMinutes(10));
        Assert.Equal(FocusPhase.Running, block.Phase);
        block.Observe(start.AddMinutes(25));
        Assert.Equal(FocusPhase.Complete, block.Phase);
        Assert.True(block.TendAuthorised);
    }
}

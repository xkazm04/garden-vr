using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class BreathPauseTests
{
    // A released inhale that was long enough is a breath the moment its exhale has lasted MinExhaleSeconds.
    // A tracking drop that parks the session in Paused before that moment must not erase it:
    // BreathSession's own comment says nothing is lost while paused.

    [Fact]
    public void A_drop_during_an_uncounted_exhale_still_counts_that_breath()
    {
        // Hold 4s, release, then 0.5s open + a drop: Lost lands at 0.9s into the exhale, under MinExhaleSeconds (1.0s).
        var s = new SimulatedHand(seed: 11).Rest(1).Hold(4f).Rest(0.5f).Drop(3f).Rest(3f).Drive(new BreathSession());
        Assert.Contains(s.Events, e => e.Kind == BreathEventKind.Paused);
        Assert.Equal(1, s.Breaths);
    }

    [Fact]
    public void A_drop_during_the_held_full_beat_then_a_release_still_counts_that_breath()
    {
        var s = new SimulatedHand(seed: 12).Rest(1).Hold(4.5f).Drop(2f).Rest(5f).Drive(new BreathSession(BreathConfig.Box(6)));
        Assert.Contains(s.Events, e => e.Kind == BreathEventKind.Paused);
        Assert.Equal(1, s.Breaths);
    }

    [Fact]
    public void A_pause_that_resumes_into_a_new_pinch_keeps_the_released_breath()
    {
        var s = new SimulatedHand(seed: 13).Rest(1).Hold(4f).Rest(0.5f).Drop(1f, pinched: true).Hold(3f).Rest(4f).Drive(new BreathSession());
        Assert.Contains(s.Events, e => e.Kind == BreathEventKind.Paused);
        Assert.Equal(2, s.Breaths);
    }
}

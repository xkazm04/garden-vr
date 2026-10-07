using System;
using GardenVR.Core;
using Xunit;

public class RitualPauseTests
{
    const float Dt = 0.1f;

    // Last non-None event over `seconds` of ticks.
    static RitualPauseEvent Run(RitualPause p, float seconds, bool pinching, bool sessionPaused = false)
    {
        var last = RitualPauseEvent.None;
        int n = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < n; i++)
        {
            var e = p.Tick(Dt, pinching, sessionPaused);
            if (e != RitualPauseEvent.None) last = e;
        }
        return last;
    }

    static int Count(RitualPause p, float seconds, RitualPauseEvent kind)
    {
        int hits = 0, n = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < n; i++)
            if (p.Tick(Dt, false, false) == kind) hits++;
        return hits;
    }

    [Fact]
    public void A_latch_freezes_and_holds()
    {
        var p = new RitualPause();
        Assert.False(p.Frozen);
        p.Latch(autoPace: false);
        p.Tick(Dt, false, false);
        Assert.True(p.Frozen);
        Assert.True(p.Holding);
    }

    [Fact]
    public void A_second_latch_is_harmless()
    {
        var p = new RitualPause();
        p.Latch(false);
        Run(p, 1f, false);
        float held = p.PausedFor;
        Assert.Equal(RitualPauseEvent.None, p.Latch(false));
        Assert.True(p.Frozen);
        Assert.Equal(held, p.PausedFor);
        Assert.Equal(RitualPauseEvent.Resumed, Run(p, 0.5f, true));
    }

    [Fact]
    public void A_pinch_held_through_the_latch_does_not_resume_until_it_is_released()
    {
        var p = new RitualPause();
        Run(p, 1f, true);
        p.Latch(false);
        Run(p, 3f, true);
        Assert.True(p.Frozen);
        Run(p, 0.2f, false);
        Assert.True(p.Frozen);
        Assert.Equal(RitualPauseEvent.Resumed, Run(p, 0.2f, true));
        Assert.False(p.Frozen);
        Assert.False(p.Holding);
    }

    [Fact]
    public void Continue_appears_once_after_sixty_seconds_of_hold_and_not_before()
    {
        var p = new RitualPause();
        p.Latch(false);
        Assert.Equal(RitualPauseEvent.None, Run(p, RitualPause.ContinueAfterSeconds - 0.5f, false));
        Assert.False(p.AwaitingContinue);
        Assert.Equal(1, Count(p, 5f, RitualPauseEvent.ContinuePrompt));
        Assert.True(p.AwaitingContinue);
        Assert.Equal(0, Count(p, 30f, RitualPauseEvent.ContinuePrompt));
    }

    [Fact]
    public void A_pinch_does_not_resume_while_the_prompt_is_up()
    {
        var p = new RitualPause();
        p.Latch(false);
        Run(p, 61f, false);
        Assert.True(p.AwaitingContinue);
        Run(p, 1f, true);
        Assert.True(p.Frozen);
        Assert.True(p.AwaitingContinue);
    }

    [Fact]
    public void Acknowledging_Continue_while_pinching_needs_a_release()
    {
        var p = new RitualPause();
        p.Latch(false);
        Run(p, 61f, false);
        p.Continue(sessionPaused: false, autoPace: false, pinching: true);
        Assert.False(p.AwaitingContinue);
        Assert.True(p.Frozen);
        Run(p, 2f, true);
        Assert.True(p.Frozen);
        Run(p, 0.2f, false);
        Assert.Equal(RitualPauseEvent.Resumed, Run(p, 0.2f, true));
        Assert.False(p.Frozen);
    }

    [Fact]
    public void Acknowledging_Continue_with_an_open_hand_resumes_on_the_next_pinch()
    {
        var p = new RitualPause();
        p.Latch(false);
        Run(p, 61f, false);
        p.Continue(false, false, pinching: false);
        Assert.Equal(RitualPauseEvent.Resumed, Run(p, 0.1f, true));
    }

    [Fact]
    public void A_tracking_pause_counts_toward_the_sixty_seconds()
    {
        var p = new RitualPause();
        Assert.Equal(RitualPauseEvent.None, Run(p, 59f, false, sessionPaused: true));
        Assert.True(p.Holding);
        Assert.False(p.Frozen);
        Assert.Equal(RitualPauseEvent.ContinuePrompt, Run(p, 2f, false, sessionPaused: true));
    }

    [Fact]
    public void A_tracking_pause_that_ends_resets_the_clock()
    {
        var p = new RitualPause();
        Run(p, 50f, false, sessionPaused: true);
        Run(p, 0.1f, false, sessionPaused: false);
        Assert.False(p.Holding);
        Assert.Equal(0f, p.PausedFor);
    }

    [Fact]
    public void The_prompt_answers_the_same_whether_or_not_the_session_is_stepped_meanwhile()
    {
        foreach (bool pinching in new[] { false, true })
        {
            var stepped = new RitualPause();
            var skipped = new RitualPause();
            // A tracking-only pause rises to the prompt. Meanwhile one caller steps the session untracked (it reads
            // Paused), the other leaves it alone and reads whatever it last saw.
            Run(stepped, 61f, false, sessionPaused: true);
            Run(skipped, 61f, false, sessionPaused: true);
            Run(stepped, 2f, false, sessionPaused: true);
            Run(skipped, 2f, false, sessionPaused: false);
            Assert.True(stepped.Holding);
            Assert.True(skipped.Holding);
            stepped.Continue(true, false, pinching);
            skipped.Continue(false, false, pinching);
            Assert.Equal(stepped.Frozen, skipped.Frozen);
            Assert.Equal(stepped.Holding, skipped.Holding);
            Assert.Equal(pinching, skipped.Frozen);
        }
    }

    [Fact]
    public void Under_auto_pace_a_second_palm_resumes()
    {
        var p = new RitualPause();
        p.Latch(autoPace: true);
        Run(p, 1f, false);
        Assert.True(p.Frozen);
        Assert.Equal(RitualPauseEvent.Resumed, p.Latch(autoPace: true));
        Assert.False(p.Frozen);
        Run(p, 0.1f, false);
        Assert.False(p.Holding);
    }

    [Fact]
    public void Under_auto_pace_a_second_palm_does_not_resume_while_the_prompt_is_up()
    {
        var p = new RitualPause();
        p.Latch(true);
        Run(p, 61f, false);
        Assert.Equal(RitualPauseEvent.None, p.Latch(true));
        Assert.True(p.Frozen);
    }

    [Fact]
    public void An_offer_back_survives_a_source_swap_and_never_restarts_itself()
    {
        var p = new RitualPause();
        p.OfferBack(pinching: true);
        p.SourceSwapped();
        Assert.True(p.AwaitingContinue);
        Assert.True(p.Frozen);
        Run(p, 5f, false);
        Run(p, 5f, true);
        Assert.True(p.Frozen);
        Assert.True(p.AwaitingContinue);
    }

    [Fact]
    public void A_source_swap_without_an_offer_starts_clean()
    {
        var p = new RitualPause();
        p.Latch(false);
        Run(p, 2f, false);
        p.SourceSwapped();
        Assert.False(p.Frozen);
        Assert.False(p.Holding);
        Assert.Equal(0f, p.PausedFor);
    }

    [Fact]
    public void An_offer_back_acknowledged_with_the_hand_open_resumes_on_a_fresh_pinch()
    {
        var p = new RitualPause();
        p.OfferBack(pinching: false);
        p.Continue(false, false, pinching: false);
        Assert.True(p.Frozen);
        Assert.Equal(RitualPauseEvent.Resumed, Run(p, 0.1f, true));
    }

    [Fact]
    public void End_to_end_the_count_is_still_while_frozen_and_no_breath_is_lost_across_a_pause()
    {
        // Two breaths, then a third inhale that a palm cuts off, then four more breaths.
        var hand = new SimulatedHand(seed: 21).Rest(1).Breaths(2).Hold(4f).Rest(6f).Breaths(4);
        var session = new BreathSession(new BreathConfig { TargetBreaths = 6 });
        var pause = new RitualPause();

        const float step = 1f / 72f;
        int steps = (int)Math.Ceiling((hand.Duration + 2f) / step);
        float latchAt = 1f + 2 * 8f + 2f;   // two seconds into the third inhale
        bool latched = false, wasFrozen = false;
        int breathsAtLatch = -1, frozenFrames = 0;

        for (int i = 0; i < steps; i++)
        {
            float t = i * step;
            var sample = hand.Sample(t);
            bool pinching = sample.Tracked && sample.Strength >= 0.8f;
            if (!latched && t >= latchAt) { pause.Latch(false); latched = true; breathsAtLatch = session.Breaths; }
            pause.Tick(step, pinching, session.Phase == BreathPhase.Paused);
            if (pause.Frozen)
            {
                frozenFrames++;
                wasFrozen = true;
                Assert.Equal(breathsAtLatch, session.Breaths);
                continue;
            }
            session.Update(step, sample);
        }

        Assert.True(wasFrozen && frozenFrames > 0);
        Assert.False(pause.Frozen);
        // Two breaths before the palm, none during it, and the four after it all counted: the pause lost nothing.
        Assert.Equal(2, breathsAtLatch);
        Assert.Equal(6, session.Breaths);
        Assert.Equal(BreathPhase.Complete, session.Phase);
    }
}

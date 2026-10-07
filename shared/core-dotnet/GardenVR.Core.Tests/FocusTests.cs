using System;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class FocusTests
{
    [Fact]
    public void A_full_hour_authorises_one_ritual_tend_of_the_arc_it_started_in()
    {
        var block = new FocusBlock();
        Assert.Equal(25 * 60, FocusBlock.DurationSeconds);
        Assert.Equal(90f, FocusBlock.SweepDegrees);
        Assert.Equal(TendSource.Ritual, block.TendsAs);
        Assert.False(block.ConsumeTend());

        var start = At(2026, 10, 3, 14, 20);
        Assert.True(block.TryStart(start, 14 * 60 + 20));
        Assert.Equal(FocusPhase.Running, block.Phase);
        Assert.Equal(ArcId.Midday, block.Arc);
        Assert.Equal(125f, block.StartGnomonDeg, 3);
        Assert.False(block.TryStart(start.AddMinutes(1), 14 * 60 + 21));

        block.Observe(start.AddMinutes(24));
        Assert.Equal(FocusPhase.Running, block.Phase);
        Assert.Equal(24 * 60, block.ElapsedSeconds);
        Assert.False(block.Counted);
        Assert.False(block.EndedEarly);
        Assert.Equal(24 * 60 / (float)FocusBlock.DurationSeconds, block.Progress, 4);

        block.Observe(start.AddMinutes(25));
        Assert.Equal(FocusPhase.Complete, block.Phase);
        Assert.True(block.Counted);
        Assert.False(block.EndedEarly);
        Assert.Equal(1f, block.Progress, 4);
        Assert.Equal(FocusBlock.DurationSeconds, block.ElapsedSeconds);
        Assert.True(block.TendAuthorised);
        Assert.True(block.ConsumeTend());
        Assert.False(block.ConsumeTend());

        block.Observe(start.AddHours(3));
        Assert.Equal(1f, block.Progress, 4);
        Assert.False(block.EndedEarly);
    }

    [Fact]
    public void Ending_early_still_counts_and_is_not_a_failure()
    {
        var block = StartMidday();
        var start = block.Started;
        Assert.False(new FocusBlock().TryEndEarly(start));

        block.Observe(start.AddMinutes(5));
        Assert.True(block.TryEndEarly(start.AddMinutes(5)));
        Assert.Equal(FocusPhase.Complete, block.Phase);
        Assert.True(block.Counted);
        Assert.True(block.EndedEarly);
        Assert.Equal(5 * 60, block.ElapsedSeconds);
        Assert.Equal(5 * 60 / (float)FocusBlock.DurationSeconds, block.Progress, 4);
        Assert.Equal(ArcId.Midday, block.Arc);
        Assert.True(block.ConsumeTend());
        Assert.False(block.TryEndEarly(start.AddMinutes(10)));
        Assert.False(block.TryPause(start.AddMinutes(10)));
    }

    [Fact]
    public void A_pause_does_not_count_and_the_hour_still_finishes()
    {
        var block = StartMidday();
        var start = block.Started;
        block.Observe(start.AddMinutes(10));
        Assert.True(block.TryPause(start.AddMinutes(10)));
        Assert.False(block.TryPause(start.AddMinutes(11)));

        block.Observe(start.AddMinutes(50));
        Assert.Equal(FocusPhase.Paused, block.Phase);
        Assert.Equal(10 * 60, block.ElapsedSeconds);
        Assert.False(block.Counted);

        Assert.True(block.TryResume(start.AddMinutes(50)));
        Assert.Equal(FocusPhase.Running, block.Phase);
        Assert.Equal(10 * 60, block.ElapsedSeconds);

        block.Observe(start.AddMinutes(64));
        Assert.Equal(FocusPhase.Running, block.Phase);
        Assert.Equal(24 * 60, block.ElapsedSeconds);

        block.Observe(start.AddMinutes(65));
        Assert.Equal(FocusPhase.Complete, block.Phase);
        Assert.True(block.Counted);
        Assert.False(block.EndedEarly);
        Assert.Equal(1f, block.Progress, 4);
        Assert.True(block.ConsumeTend());
    }

    [Fact]
    public void Ending_while_paused_keeps_the_earned_minutes_and_drops_the_gap()
    {
        var block = StartMidday();
        var start = block.Started;
        block.Observe(start.AddMinutes(5));
        Assert.True(block.TryPause(start.AddMinutes(5)));
        Assert.True(block.TryEndEarly(start.AddHours(3)));
        Assert.True(block.Counted);
        Assert.True(block.EndedEarly);
        Assert.Equal(5 * 60, block.ElapsedSeconds);
        Assert.Equal(FocusPhase.Complete, block.Phase);
    }

    [Fact]
    public void A_spring_forward_counts_real_minutes_not_the_wall_jump()
    {
        // 01:40 at offset 0, then the clock shows 03:00 at offset +1. Wall gap is 80 minutes.
        // The UTC gap is 20 minutes, so the hour is still running.
        var block = new FocusBlock();
        var start = new DateTimeOffset(2026, 3, 8, 1, 40, 0, TimeSpan.Zero);
        var afterJump = new DateTimeOffset(2026, 3, 8, 3, 0, 0, TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromMinutes(20), afterJump.UtcDateTime - start.UtcDateTime);
        Assert.Equal(TimeSpan.FromMinutes(80), afterJump.DateTime - start.DateTime);

        Assert.True(block.TryStart(start, 1 * 60 + 40));
        block.Observe(afterJump);
        Assert.Equal(FocusPhase.Running, block.Phase);
        Assert.Equal(20 * 60, block.ElapsedSeconds);
        Assert.False(block.Counted);
        Assert.Equal(20 * 60 / (float)FocusBlock.DurationSeconds, block.Progress, 4);
    }

    [Fact]
    public void A_fall_back_does_not_rewind_the_hour()
    {
        // 01:50 at offset +1, then the clock shows 01:20 at offset 0. The wall clock went backwards.
        // Thirty real minutes have passed, so the hour is complete.
        var block = new FocusBlock();
        var start = new DateTimeOffset(2026, 11, 1, 1, 50, 0, TimeSpan.FromHours(1));
        var later = new DateTimeOffset(2026, 11, 1, 1, 20, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMinutes(30), later.UtcDateTime - start.UtcDateTime);
        Assert.True(later.DateTime < start.DateTime);

        Assert.True(block.TryStart(start, 1 * 60 + 50));
        block.Observe(later);
        Assert.Equal(FocusPhase.Complete, block.Phase);
        Assert.True(block.Counted);
        Assert.False(block.EndedEarly);
        Assert.Equal(1f, block.Progress, 4);
        Assert.True(block.ElapsedSeconds >= FocusBlock.DurationSeconds);
    }

    [Fact]
    public void A_pause_across_a_spring_forward_drops_only_the_real_gap()
    {
        var block = new FocusBlock();
        var start = new DateTimeOffset(2026, 3, 8, 1, 40, 0, TimeSpan.Zero);
        var pausedAt = start.AddMinutes(10);
        var resumeAt = new DateTimeOffset(2026, 3, 8, 3, 10, 0, TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromMinutes(20), resumeAt.UtcDateTime - pausedAt.UtcDateTime);

        Assert.True(block.TryStart(start, 1 * 60 + 40));
        block.Observe(pausedAt);
        Assert.True(block.TryPause(pausedAt));
        Assert.True(block.TryResume(resumeAt));
        Assert.Equal(10 * 60, block.ElapsedSeconds);

        var done = resumeAt.AddMinutes(15);
        block.Observe(done);
        Assert.Equal(FocusPhase.Complete, block.Phase);
        Assert.False(block.EndedEarly);
        Assert.Equal(1f, block.Progress, 4);
    }

    [Fact]
    public void Midnight_is_a_normal_utc_gap()
    {
        var block = new FocusBlock();
        var start = At(2026, 6, 1, 23, 50);
        Assert.True(block.TryStart(start, 23 * 60 + 50));
        Assert.Equal(ArcId.WindDown, block.Arc);
        block.Observe(At(2026, 6, 2, 0, 15));
        Assert.Equal(FocusPhase.Complete, block.Phase);
        Assert.True(block.Counted);
        Assert.False(block.EndedEarly);
    }

    [Fact]
    public void The_quiet_hours_before_morning_have_no_arc_and_still_count()
    {
        var block = new FocusBlock();
        var start = At(2026, 6, 1, 4, 0);
        Assert.True(block.TryStart(start, 4 * 60));
        Assert.False(block.Arc.HasValue);
        block.Observe(start.AddMinutes(25));
        Assert.True(block.Counted);
        Assert.False(block.TendAuthorised);
        Assert.False(block.ConsumeTend());
    }

    [Fact]
    public void One_in_the_morning_belongs_to_wind_down()
    {
        var block = new FocusBlock();
        Assert.True(block.TryStart(At(2026, 6, 2, 1, 0), 60));
        Assert.Equal(ArcId.WindDown, block.Arc);
    }

    [Fact]
    public void A_clock_step_backward_does_not_unwind_progress()
    {
        var block = StartMidday();
        var start = block.Started;
        block.Observe(start.AddMinutes(12));
        float earned = block.Progress;
        int seconds = block.ElapsedSeconds;
        block.Observe(start.AddMinutes(3));
        Assert.Equal(earned, block.Progress);
        Assert.Equal(seconds, block.ElapsedSeconds);
        Assert.Equal(FocusPhase.Running, block.Phase);
    }

    [Fact]
    public void Progress_never_falls_across_pauses_and_jumps()
    {
        var block = StartMidday();
        var t = block.Started;
        var rng = new Random(25);
        float previous = 0f;
        for (int i = 0; i < 48; i++)
        {
            t = t.AddSeconds(rng.Next(0, 80));
            if (block.Phase == FocusPhase.Running && rng.Next(0, 5) == 0)
            {
                Assert.True(block.TryPause(t));
                previous = block.Progress;
                t = t.AddMinutes(rng.Next(1, 40));
                block.Observe(t);
                Assert.Equal(previous, block.Progress);
                Assert.True(block.TryResume(t));
            }
            else if (block.Phase == FocusPhase.Running && rng.Next(0, 11) == 0)
            {
                Assert.True(block.TryEndEarly(t));
            }
            else
            {
                block.Observe(t);
            }

            Assert.True(block.Progress >= previous);
            Assert.True(block.Progress <= 1f);
            Assert.True(block.ElapsedSeconds <= FocusBlock.DurationSeconds);
            previous = block.Progress;
            if (block.Phase == FocusPhase.Complete) break;
        }
    }

    [Fact]
    public void A_snapshot_round_trip_keeps_a_paused_hour()
    {
        var block = StartMidday();
        var start = block.Started;
        block.Observe(start.AddMinutes(8));
        Assert.True(block.TryPause(start.AddMinutes(8)));
        FocusBlock restored = FocusBlock.Restore(block.Capture());
        Assert.Equal(FocusPhase.Paused, restored.Phase);
        Assert.Equal(ArcId.Midday, restored.Arc);
        Assert.Equal(125f, restored.StartGnomonDeg, 3);
        Assert.Equal(8 * 60, restored.ElapsedSeconds);
        Assert.False(restored.Counted);
        Assert.False(restored.TendAuthorised);

        restored.Observe(start.AddHours(2));
        Assert.Equal(8 * 60, restored.ElapsedSeconds);
        Assert.True(restored.TryResume(start.AddMinutes(40)));
        restored.Observe(start.AddMinutes(57));
        Assert.Equal(FocusPhase.Complete, restored.Phase);
        Assert.False(restored.EndedEarly);
        Assert.True(restored.ConsumeTend());

        FocusBlock again = FocusBlock.Restore(restored.Capture());
        Assert.Equal(FocusPhase.Complete, again.Phase);
        Assert.True(again.Counted);
        Assert.False(again.EndedEarly);
        Assert.False(again.TendAuthorised);
        Assert.False(again.ConsumeTend());
    }

    [Fact]
    public void An_empty_snapshot_is_idle()
    {
        FocusBlock restored = FocusBlock.Restore(null);
        Assert.Equal(FocusPhase.Idle, restored.Phase);
        Assert.False(restored.TryPause(At(2026, 1, 1, 12, 0)));
        Assert.False(restored.TryResume(At(2026, 1, 1, 12, 0)));
        Assert.False(FocusBlock.Restore(new FocusSnapshot { Phase = "Idle" }).Counted);
        Assert.False(FocusBlock.Restore(new FocusSnapshot { Phase = "Complete", TendPending = true }).TendAuthorised);
    }

    static FocusBlock StartMidday()
    {
        var block = new FocusBlock();
        Assert.True(block.TryStart(At(2026, 10, 3, 14, 20), 14 * 60 + 20));
        return block;
    }

    static DateTimeOffset At(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }
}

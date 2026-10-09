using System;
using System.Collections.Generic;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Upgrade plan section 6.4: the break (stretch, hand poses, eye rest) and the quiet nudge planner.
public class BreaksTests
{
    static BreakSample Reach(int mark) { return new BreakSample(mark, true, true, false, false); }
    static BreakSample Palm { get { return new BreakSample(-1, false, true, false, false); } }
    static BreakSample Pinch { get { return new BreakSample(-1, false, false, true, false); } }
    static BreakSample Look { get { return new BreakSample(-1, false, false, false, true); } }

    static void Hold(BreakSession s, BreakSample sample, float seconds, float step = 0.1f)
    {
        for (float t = 0; t < seconds - 1e-4f; t += step) s.Update(step, sample);
    }

    [Fact]
    public void A_break_runs_stretch_then_poses_then_eye_rest_and_authorises_one_tend()
    {
        var s = new BreakSession(withMobility: true);
        Assert.Equal(BreakPart.Stretch, s.Part);
        for (int m = 0; m < 3; m++) s.Update(0.5f, Reach(m));
        Assert.Equal(BreakPart.Mobility, s.Part);
        Hold(s, Palm, 3f);
        Assert.Equal(HandPose.Pinch, s.Mobility.Current);
        Hold(s, Pinch, 3f);
        Hold(s, Palm, 3f);
        Assert.Equal(BreakPart.EyeRest, s.Part);
        Assert.False(s.ConsumeTend());
        Hold(s, Look, 20f);
        Assert.True(s.Complete);
        Assert.True(s.ConsumeTend());
        Assert.False(s.ConsumeTend());
    }

    [Fact]
    public void Without_poses_the_stretch_leads_straight_to_the_eye_rest()
    {
        var s = new BreakSession(withMobility: false);
        for (int m = 0; m < 3; m++) s.Update(0.5f, Reach(m));
        Assert.Equal(BreakPart.EyeRest, s.Part);
    }

    [Fact]
    public void Letting_go_of_a_pose_early_pauses_it_and_keeps_poses_done()
    {
        var m = new HandMobility();
        for (int i = 0; i < 31; i++) m.Update(0.1f, Palm);
        Assert.Equal(1, m.Done);
        for (int i = 0; i < 10; i++) m.Update(0.1f, Pinch);
        float progress = m.Progress;
        m.Update(0.1f, BreakSample.None);
        m.Update(0.1f, Palm); // the wrong pose for step two
        Assert.Equal(1, m.Done);
        Assert.Equal(progress, m.Progress);
        m.Update(float.NaN, Pinch);
        Assert.Equal(progress, m.Progress);
    }

    [Fact]
    public void Eye_rest_counts_only_while_looking_and_never_resets()
    {
        var e = new EyeRest();
        for (int i = 0; i < 70; i++) e.Update(0.1f, Look);
        Assert.Equal(13, e.SecondsLeft);
        for (int i = 0; i < 50; i++) e.Update(0.1f, BreakSample.None);
        Assert.Equal(13, e.SecondsLeft);
        e.Update(-1f, Look);
        e.Update(float.PositiveInfinity, Look);
        Assert.Equal(13, e.SecondsLeft);
        for (int i = 0; i < 200; i++) e.Update(0.1f, Look);
        Assert.True(e.Complete);
        Assert.Equal(0, e.SecondsLeft);
        Assert.Equal(EyeRest.Seconds, e.Rested);
    }

    [Theory]
    [InlineData(null, 3f)]
    [InlineData(0f, 3f)]
    [InlineData(-2f, 3f)]
    [InlineData(1.2f, 2f)]
    [InlineData(4.5f, 4.5f)]
    [InlineData(20f, 8f)]
    public void The_target_sits_at_the_far_wall_between_two_and_eight_metres(float? farthest, float expected)
    {
        Assert.Equal(expected, EyeRest.TargetDistance(farthest));
        Assert.Equal(3f, EyeRest.TargetDistance(float.NaN));
    }

    [Fact]
    public void A_break_habit_grows_in_the_body_zone()
    {
        Assert.Equal(LifeZone.Body, HabitProfiles.ZoneOf(new HabitDef { PresetKey = BreakSession.PresetKey }));
    }

    // ---------- nudges ----------

    static DateTimeOffset At(int h, int m) { return new DateTimeOffset(2026, 10, 13, h, m, 0, TimeSpan.FromHours(2)); }
    static readonly List<DateTimeOffset> None = new List<DateTimeOffset>();

    [Fact]
    public void Before_the_first_window_the_nudge_waits_for_it()
    {
        Assert.Equal(At(10, 30), NudgePlanner.Next(At(8, 0), NudgePlanner.DefaultWindows, None, null));
    }

    [Fact]
    public void Inside_a_window_the_nudge_may_come_now()
    {
        Assert.Equal(At(11, 0), NudgePlanner.Next(At(11, 0), NudgePlanner.DefaultWindows, None, null));
    }

    [Fact]
    public void A_nudge_keeps_ninety_minutes_from_the_last_one()
    {
        var sent = new List<DateTimeOffset> { At(10, 45) };
        Assert.Equal(At(14, 30), NudgePlanner.Next(At(11, 0), NudgePlanner.DefaultWindows, sent, null));
        var lateSent = new List<DateTimeOffset> { At(14, 40) };
        Assert.Equal(At(16, 10), NudgePlanner.Next(At(14, 50), NudgePlanner.DefaultWindows, lateSent, null));
    }

    [Fact]
    public void A_break_just_taken_quiets_the_next_fifty_minutes()
    {
        Assert.Equal(At(11, 30), NudgePlanner.Next(At(10, 45), NudgePlanner.DefaultWindows, None, At(10, 40)));
        Assert.Equal(At(14, 30), NudgePlanner.Next(At(11, 30), NudgePlanner.DefaultWindows, None, At(11, 20)));
    }

    [Fact]
    public void Two_a_day_at_most_and_none_after_the_last_window()
    {
        var two = new List<DateTimeOffset> { At(10, 30), At(14, 30) };
        Assert.Null(NudgePlanner.Next(At(15, 0), NudgePlanner.DefaultWindows, two, null));
        Assert.Null(NudgePlanner.Next(At(17, 0), NudgePlanner.DefaultWindows, None, null));
        Assert.Null(NudgePlanner.Next(At(23, 0), NudgePlanner.DefaultWindows, new List<DateTimeOffset> { At(22, 0) }, null));
    }

    [Fact]
    public void Invalid_windows_are_ignored()
    {
        var windows = new[] { new NudgeWindow(new TimeSpan(12, 0, 0), new TimeSpan(11, 0, 0)), new NudgeWindow(new TimeSpan(13, 0, 0), new TimeSpan(13, 30, 0)) };
        Assert.Equal(At(13, 0), NudgePlanner.Next(At(9, 0), windows, None, null));
        Assert.Null(NudgePlanner.Next(At(9, 0), null, None, null));
        Assert.Null(NudgePlanner.Next(At(9, 0), new NudgeWindow[0], None, null));
    }

    [Fact]
    public void The_nudge_copy_is_calm()
    {
        Assert.True(CoachGuards.IsClean(NudgePlanner.Text, 60));
    }

    [Fact]
    public void Property_no_random_day_ever_gets_more_than_two_nudges_or_one_outside_a_window()
    {
        var random = new Random(5);
        for (int life = 0; life < 300; life++)
        {
            var sent = new List<DateTimeOffset>();
            DateTimeOffset? lastBreak = null;
            DateTimeOffset now = At(6, 0);
            while (now.Hour < 23)
            {
                DateTimeOffset? next = NudgePlanner.Next(now, NudgePlanner.DefaultWindows, sent, lastBreak);
                if (next.HasValue && next.Value <= now) sent.Add(now);
                if (random.NextDouble() < 0.05) lastBreak = now;
                now = now.AddMinutes(random.Next(1, 30));
            }
            Assert.True(sent.Count <= NudgePlanner.MaxPerDay);
            foreach (var t in sent)
            {
                var tod = t.TimeOfDay;
                Assert.Contains(NudgePlanner.DefaultWindows, w => tod >= w.Start && tod < w.End);
            }
            for (int i = 1; i < sent.Count; i++) Assert.True(sent[i] - sent[i - 1] >= NudgePlanner.MinSpacing);
        }
    }
}

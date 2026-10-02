using System;
using System.Linq;
using GardenVR.Core;
using Xunit;
using Xunit.Abstractions;

public class BreathRitualTests
{
    readonly ITestOutputHelper _out;
    public BreathRitualTests(ITestOutputHelper o) { _out = o; }

    [Fact]
    public void Six_calm_breaths_complete_the_ritual_in_under_a_minute()
    {
        var s = new SimulatedHand(seed: 1).Rest(1).Breaths(6, inhale: 4, exhale: 4).Drive(new BreathSession());
        foreach (var e in s.Events) _out.WriteLine(e.ToString());
        Assert.Equal(BreathPhase.Complete, s.Phase);
        Assert.Equal(6, s.Breaths);
        var done = s.Events.Single(e => e.Kind == BreathEventKind.RitualComplete);
        Assert.InRange(done.Time, 40f, 60f); // design target: one complete moment in under a minute of breathing
        Assert.Equal(1f, s.Uncoil, 3);
    }

    [Fact]
    public void Uncoil_rises_while_holding_and_never_falls_below_earned_breaths()
    {
        var hand = new SimulatedHand(seed: 2).Rest(0.5f).Breaths(6, 3.5f, 3.5f);
        var s = new BreathSession();
        float dt = 1f / 72f;
        float prev = 0;
        for (int i = 0; i < (hand.Duration + 2) * 72; i++)
        {
            s.Update(dt, hand.Sample(i * dt));
            // The frond may relax only while it is ahead of what was earned; it never curls back past a counted breath.
            if (s.Uncoil < prev - 1e-6f)
                Assert.True(s.Uncoil + 1e-4f >= s.Progress, $"t={s.Time:0.00} curled back to {s.Uncoil} below earned {s.Progress}");
            prev = s.Uncoil;
        }
        Assert.Equal(1f, s.Uncoil, 3);
    }

    [Fact]
    public void Fidget_pinches_are_ignored_not_counted_and_not_punished()
    {
        var s = new SimulatedHand(seed: 3).Rest(1).Hold(0.4f).Rest(0.6f).Hold(0.5f).Rest(1).Breaths(2).Drive(new BreathSession());
        Assert.Equal(2, s.Events.Count(e => e.Kind == BreathEventKind.ShortInhaleIgnored));
        Assert.Equal(2, s.Breaths);
    }

    [Fact]
    public void Jitter_around_the_threshold_does_not_create_phantom_breaths()
    {
        // Strength wobbling 0.55..0.78 sits between release (0.5) and engage (0.8): never a breath.
        var s = new BreathSession(); var rng = new Random(4);
        for (int i = 0; i < 72 * 30; i++) s.Update(1f / 72, new PinchSample(0.55f + (float)rng.NextDouble() * 0.23f, true));
        Assert.Equal(0, s.Breaths);
        Assert.DoesNotContain(s.Events, e => e.Kind == BreathEventKind.InhaleStarted);
    }

    [Fact]
    public void A_short_tracking_drop_mid_inhale_is_bridged()
    {
        var s = new SimulatedHand(seed: 5).Rest(1).Hold(1.5f).Drop(0.25f, pinched: true).Hold(2f).Rest(4).Drive(new BreathSession());
        Assert.DoesNotContain(s.Events, e => e.Kind == BreathEventKind.Paused);
        Assert.Equal(1, s.Breaths);
    }

    [Fact]
    public void A_long_tracking_drop_pauses_and_resumes_without_losing_breaths()
    {
        var s = new SimulatedHand(seed: 6).Rest(1).Breaths(3).Drop(5f).Rest(1).Breaths(3).Drive(new BreathSession());
        foreach (var e in s.Events) _out.WriteLine(e.ToString());
        Assert.Contains(s.Events, e => e.Kind == BreathEventKind.Paused && e.Breaths == 3);
        Assert.Contains(s.Events, e => e.Kind == BreathEventKind.Resumed && e.Breaths == 3);
        Assert.Equal(BreathPhase.Complete, s.Phase);
    }

    [Fact]
    public void Glass_fogs_on_each_release_then_clears()
    {
        var hand = new SimulatedHand(seed: 7).Rest(1).Hold(4).Rest(4);
        var s = new BreathSession(); float dt = 1f / 72; float peak = 0;
        for (int i = 0; i < 72 * 5.3f; i++) { s.Update(dt, hand.Sample(i * dt)); peak = Math.Max(peak, s.Fog); }
        Assert.True(peak > 0.95f);
        for (int i = (int)(72 * 5.3f); i < 72 * 9; i++) s.Update(dt, hand.Sample(i * dt));
        Assert.True(s.Fog < 0.1f);
    }

    [Fact]
    public void Hysteresis_must_be_real()
    {
        Assert.Throws<ArgumentException>(() => new PinchDetector(engageAt: 0.5f, releaseAt: 0.6f));
    }

    [Fact]
    public void Sundial_config_completes_in_three_calm_breaths()
    {
        // Sundial dusk ritual: three breaths, holds shorter than 1.5 s are fidgets.
        var config = new BreathConfig { TargetBreaths = 3, MinInhaleSeconds = 1.5f };
        var s = new SimulatedHand(seed: 11).Rest(1f).Breaths(3, inhale: 4f, exhale: 4f).Drive(new BreathSession(config));
        Assert.Equal(3, s.TargetBreaths);
        Assert.Equal(BreathPhase.Complete, s.Phase);
        Assert.Equal(3, s.Breaths);
        Assert.Equal(3, s.Events.Count(e => e.Kind == BreathEventKind.BreathCounted));
        Assert.DoesNotContain(s.Events, e => e.Kind == BreathEventKind.ShortInhaleIgnored);
    }
}

public class GardenTests
{
    const int Day0 = 9770; // 2026-10-01

    [Fact]
    public void First_ritual_grows_one_permanent_frond()
    {
        var g = new Garden();
        var a = g.CompleteRitual(Day0);
        Assert.True(a.NewFrond); Assert.False(a.Recovered);
        Assert.Equal(1, g.Fronds);
    }

    [Fact]
    public void Extra_sessions_the_same_day_add_dew_not_fronds()
    {
        var g = new Garden(); g.CompleteRitual(Day0);
        var a = g.CompleteRitual(Day0); var b = g.CompleteRitual(Day0);
        Assert.False(a.NewFrond); Assert.Equal(2, b.DewBeads);
        Assert.Equal(1, g.Fronds); Assert.Equal(3, g.RitualsCompleted);
    }

    [Fact]
    public void Missed_days_droop_to_a_floor_and_one_ritual_restores_everything()
    {
        var g = new Garden(); g.CompleteRitual(Day0); g.CompleteRitual(Day0 + 1);
        Assert.Equal(1f, g.Vitality(Day0 + 2));
        Assert.Equal(0.85f, g.Vitality(Day0 + 3), 3);
        Assert.Equal(Garden.VitalityFloor, g.Vitality(Day0 + 30), 3);
        var a = g.CompleteRitual(Day0 + 30);
        Assert.True(a.Recovered); Assert.Equal(1f, g.Vitality(Day0 + 30));
        Assert.Equal(3, g.Fronds); Assert.Equal(1, g.Returns);
    }

    [Fact]
    public void Flower_opens_at_six_fronds_not_every_seventh()
    {
        // D5 replaced the spike's every-7th rule. Six practice days open the first flower;
        // the seventh frond does not.
        var g = new Garden();
        GrowthAnswer sixth = default;
        for (int d = 0; d < 6; d++) sixth = g.CompleteRitual(Day0 + d);
        Assert.Equal(6, g.Fronds);
        Assert.True(sixth.Flower);
        Assert.Equal(1, g.Flowers);
        var seventh = g.CompleteRitual(Day0 + 6);
        Assert.False(seventh.Flower);
        Assert.Equal(1, g.Flowers);
        Assert.Equal(7, g.Fronds);
    }

    [Fact]
    public void Property_fronds_never_decrease_over_random_lives()
    {
        var rng = new Random(42);
        for (int life = 0; life < 500; life++)
        {
            var g = new Garden(); int day = Day0, prev = 0;
            for (int step = 0; step < 120; step++)
            {
                day += rng.Next(0, 4);
                if (rng.NextDouble() < 0.6) g.CompleteRitual(day);
                Assert.True(g.Fronds >= prev); prev = g.Fronds;
                Assert.InRange(g.Vitality(day), Garden.VitalityFloor, 1f);
            }
        }
    }

    [Fact]
    public void Clock_going_backwards_is_refused()
    {
        var g = new Garden(); g.CompleteRitual(Day0 + 5);
        Assert.Throws<ArgumentException>(() => g.CompleteRitual(Day0 + 4));
    }

    [Fact]
    public void Persistence_round_trips_exactly()
    {
        var g = new Garden(); g.CompleteRitual(Day0); g.CompleteRitual(Day0); g.CompleteRitual(Day0 + 4);
        var line = g.Serialize();
        var h = Garden.Deserialize(line);
        Assert.Equal(line, h.Serialize());
        Assert.Equal(g.Fronds, h.Fronds); Assert.Equal(g.Returns, h.Returns);
        Assert.Equal(new Garden().Serialize(), Garden.Deserialize("").Serialize());
    }

    [Fact]
    public void The_ten_minute_contract_cold_start_to_answer()
    {
        // First launch: one ritual from a fresh garden, played by the simulated hand, ends in a GrowthAnswer.
        var g = new Garden();
        var s = new SimulatedHand(seed: 9).Rest(3).Breaths(6).Drive(new BreathSession());
        Assert.Equal(BreathPhase.Complete, s.Phase);
        var a = g.CompleteRitual(Day0);
        Assert.True(a.NewFrond);
        Assert.True(s.Time < 10 * 60);
    }
}

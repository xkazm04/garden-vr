using System;
using GardenVR.Core;
using Xunit;

public class BreathRitualRulesTests
{
    // ---- Garden.Drooping, Garden.RitualDoneOn ----

    [Fact]
    public void Drooping_equals_the_curve_below_one_for_gaps_0_to_20()
    {
        for (int gap = 0; gap <= 20; gap++)
        {
            var g = new Garden();
            g.CompleteRitual(100);
            Assert.Equal(Garden.VitalityForGap(gap) < 1f, g.Drooping(100 + gap));
        }
    }

    [Fact]
    public void Drooping_agrees_with_Recovered_on_the_next_ritual()
    {
        for (int gap = 1; gap <= 20; gap++)
        {
            var g = new Garden();
            g.CompleteRitual(100);
            bool drooping = g.Drooping(100 + gap);
            Assert.Equal(drooping, g.CompleteRitual(100 + gap).Recovered);
        }
    }

    [Fact]
    public void A_fresh_garden_does_not_droop()
    {
        Assert.False(new Garden().Drooping(500));
    }

    [Fact]
    public void RitualDone_is_false_on_a_fresh_garden()
    {
        Assert.False(new Garden().RitualDoneOn(0));
        Assert.False(new Garden().RitualDoneOn(500));
    }

    [Fact]
    public void RitualDone_is_true_only_on_the_day_of_the_ritual()
    {
        var g = new Garden();
        g.CompleteRitual(100);
        Assert.True(g.RitualDoneOn(100));
        Assert.False(g.RitualDoneOn(101));
    }

    [Fact]
    public void RitualDone_is_false_after_the_clock_is_set_back()
    {
        var g = new Garden();
        g.CompleteRitual(100);
        // JarRitualController.cs:1366 (DaysSinceRitual > 0) reads 0 here and treats the day as done; the core says not today.
        Assert.Equal(0, g.DaysSinceRitual(98));
        Assert.False(g.RitualDoneOn(98));
    }

    [Fact]
    public void A_fresh_garden_differs_from_the_DaysSinceRitual_test_at_JarRitualController_1366()
    {
        var g = new Garden();
        Assert.False(g.DaysSinceRitual(500) > 0);   // the app copy at :1366
        Assert.False(g.RitualDoneOn(500));          // the core answer: not done, so waiting
    }

    // ---- RitualSettings.BreathChoices, BreathConfig.From ----

    // The jar's ConfigFrom (JarRitualController.cs:1447-1456), copied here as the oracle.
    static BreathConfig ConfigFrom(RitualSettings settings)
    {
        var config = new BreathConfig();
        if (settings == null) return config;
        int breaths = settings.Breaths;
        if (breaths != 3 && breaths != 4 && breaths != 6 && breaths != 8) breaths = 6;
        if (settings.BoxPace) return BreathConfig.Box(breaths);
        config.TargetBreaths = breaths;
        if (settings.InhaleSec >= 1.2d) config.IdealInhaleSeconds = (float)settings.InhaleSec;
        return config;
    }

    static void AssertSame(BreathConfig a, BreathConfig b)
    {
        Assert.Equal(a.TargetBreaths, b.TargetBreaths);
        Assert.Equal(a.IdealInhaleSeconds, b.IdealInhaleSeconds);
        Assert.Equal(a.HoldFullSeconds, b.HoldFullSeconds);
        Assert.Equal(a.IdealExhaleSeconds, b.IdealExhaleSeconds);
        Assert.Equal(a.HoldEmptySeconds, b.HoldEmptySeconds);
        Assert.Equal(a.MinInhaleSeconds, b.MinInhaleSeconds);
        Assert.Equal(a.MinExhaleSeconds, b.MinExhaleSeconds);
    }

    [Fact]
    public void BreathChoices_are_3_4_6_8()
    {
        Assert.Equal(new[] { 3, 4, 6, 8 }, RitualSettings.BreathChoices);
    }

    [Fact]
    public void From_maps_every_allowed_count()
    {
        foreach (int n in RitualSettings.BreathChoices)
            Assert.Equal(n, BreathConfig.From(new RitualSettings { Breaths = n }).TargetBreaths);
    }

    [Fact]
    public void From_falls_back_to_6_for_any_other_count()
    {
        foreach (int n in new[] { -1, 0, 1, 2, 5, 7, 9, 100 })
            Assert.Equal(6, BreathConfig.From(new RitualSettings { Breaths = n }).TargetBreaths);
    }

    [Fact]
    public void From_null_gives_the_defaults()
    {
        AssertSame(new BreathConfig(), BreathConfig.From(null));
    }

    [Fact]
    public void From_matches_ConfigFrom_case_by_case()
    {
        foreach (int n in new[] { 0, 3, 4, 5, 6, 7, 8, 12 })
        foreach (bool box in new[] { false, true })
        foreach (double inhale in new[] { 0d, 1.19d, 1.2d, 3d, 4d, 5d })
            AssertSame(ConfigFrom(new RitualSettings { Breaths = n, BoxPace = box, InhaleSec = inhale }),
                       BreathConfig.From(new RitualSettings { Breaths = n, BoxPace = box, InhaleSec = inhale }));
    }

    [Fact]
    public void From_box_pace_gives_four_sides_and_ignores_InhaleSec()
    {
        var c = BreathConfig.From(new RitualSettings { Breaths = 4, BoxPace = true, InhaleSec = 5d });
        Assert.Equal(BreathConfig.BoxSideSeconds, c.IdealInhaleSeconds);
        Assert.Equal(BreathConfig.BoxSideSeconds, c.HoldFullSeconds);
        Assert.Equal(4, c.TargetBreaths);
    }

    [Fact]
    public void The_jar_view_floor_literal_is_the_garden_floor()
    {
        Assert.Equal(0.6f, Garden.VitalityFloor); // JarView.cs:1020 clamps with a bare 0.6f
    }

    // ---- PacedHand ----

    [Fact]
    public void Box_duty_is_one_half()
    {
        var s = new RitualSettings { BoxPace = true };
        Assert.Equal(16f, PacedHand.Period(s));
        Assert.Equal(0.5f, PacedHand.Duty(s));
        Assert.True(PacedHand.Held(0f, s));
        Assert.True(PacedHand.Held(7.9f, s));
        Assert.False(PacedHand.Held(8f, s));
        Assert.False(PacedHand.Held(15.9f, s));
        Assert.True(PacedHand.Held(16f, s));
    }

    [Fact]
    public void Free_pace_duty_is_inhale_over_inhale_plus_exhale()
    {
        foreach (var (i, e) in new[] { (4d, 6d), (3d, 5d), (5d, 7d), (0d, 0d), (1.19d, 1.19d) })
        {
            var s = new RitualSettings { InhaleSec = i, ExhaleSec = e };
            float inh = i >= 1.2d ? (float)i : 4f, exh = e >= 1.2d ? (float)e : 6f;
            Assert.Equal(inh / (inh + exh), PacedHand.Duty(s), 5);
            int held = 0, n = 10000;
            for (int k = 0; k < n; k++)
                if (PacedHand.Held(k * (inh + exh) / n, s)) held++;
            Assert.Equal(PacedHand.Duty(s), (float)held / n, 3);
        }
    }

    [Fact]
    public void Null_settings_use_the_default_pace()
    {
        Assert.Equal(10f, PacedHand.Period(null));
        Assert.True(PacedHand.Held(3.9f, null));
        Assert.False(PacedHand.Held(4f, null));
        Assert.Equal(0.95f, PacedHand.Sample(1f, null).Strength);
        Assert.Equal(0.05f, PacedHand.Sample(5f, null).Strength);
    }

    [Fact]
    public void The_paced_hand_drives_a_session_to_completion()
    {
        var s = new RitualSettings { Breaths = 3 };
        var session = new BreathSession(BreathConfig.From(s));
        for (int i = 0; i < 72 * 60 && session.Phase != BreathPhase.Complete; i++)
            session.Update(1f / 72f, PacedHand.Sample(i / 72f, s));
        Assert.Equal(BreathPhase.Complete, session.Phase);
    }

    // ---- BreathProgress ----

    static void RunRing(RitualSettings s)
    {
        var config = BreathConfig.From(s);
        var session = new BreathSession(config);
        float last = 0f;
        for (int i = 0; i < 72 * 120 && session.Phase != BreathPhase.Complete; i++)
        {
            session.Update(1f / 72f, PacedHand.Sample(i / 72f, s));
            float p = BreathProgress.Ring(session, config);
            Assert.InRange(p, 0f, 1f);
            Assert.True(p >= last - 1e-6f, $"ring fell from {last} to {p} at tick {i} in {session.Phase}");
            last = p;
        }
        Assert.Equal(BreathPhase.Complete, session.Phase);
        Assert.Equal(1f, BreathProgress.Ring(session, config));
    }

    [Fact]
    public void Ring_never_falls_and_is_1_at_Complete_on_the_free_pace() => RunRing(new RitualSettings { Breaths = 4 });

    [Fact]
    public void Ring_never_falls_and_is_1_at_Complete_on_the_box_pace() => RunRing(new RitualSettings { Breaths = 3, BoxPace = true });

    [Fact]
    public void Ring_is_0_before_the_first_pinch()
    {
        var config = new BreathConfig();
        Assert.Equal(0f, BreathProgress.Ring(new BreathSession(config), config));
    }

    [Fact]
    public void Box_ring_rises_by_quarters_and_is_1_at_Complete()
    {
        var s = new RitualSettings { Breaths = 3, BoxPace = true };
        var session = new BreathSession(BreathConfig.From(s));
        Assert.Equal(0f, BreathProgress.Box(session));
        bool sawHoldFull = false, sawHoldEmpty = false;
        for (int i = 0; i < 72 * 120 && session.Phase != BreathPhase.Complete; i++)
        {
            session.Update(1f / 72f, PacedHand.Sample(i / 72f, s));
            float b = BreathProgress.Box(session);
            if (session.Phase == BreathPhase.HoldingFull) { sawHoldFull = true; Assert.InRange(b, 0.25f, 0.5f); }
            if (session.Phase == BreathPhase.HoldingEmpty) { sawHoldEmpty = true; Assert.InRange(b, 0.75f, 1f); }
            if (session.Phase == BreathPhase.Inhaling) Assert.InRange(b, 0f, 0.25f);
            if (session.Phase == BreathPhase.Exhaling) Assert.InRange(b, 0.5f, 0.75f);
        }
        Assert.True(sawHoldFull && sawHoldEmpty);
        Assert.Equal(1f, BreathProgress.Box(session));
    }
}

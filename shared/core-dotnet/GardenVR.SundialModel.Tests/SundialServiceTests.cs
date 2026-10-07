using System;
using System.IO;
using System.Linq;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

// Line numbers cite apps/sundial/Assets/Scripts/Tend/SundialService.cs at the sha these tests were written on.
public sealed class SundialServiceTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "gardenvr-sundial-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    public SundialServiceTests() { Directory.CreateDirectory(_dir); }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    SundialService Open() { return new SundialService(_clock, _dir); }

    static HabitDef Plant(SundialService svc, string arc, string key)
    {
        return svc.PlantPreset(SeedCatalog.Find(arc, key));
    }

    [Fact]
    public void A_fresh_directory_starts_empty_without_the_dev_seed()
    {
        // :65 DevSeedOnFresh is false in the player, so a fresh file waits for the first run.
        Assert.False(SundialService.DevSeedOnFresh);
        SundialService svc = Open();
        Assert.Equal(LoadOutcome.Fresh, svc.Outcome);
        Assert.Empty(svc.Save.Habits);
    }

    [Fact]
    public void The_dev_seed_static_plants_three_habits_and_is_restored()
    {
        // :65 and :149-154. The static is restored in finally so later tests see the player default.
        try
        {
            SundialService.DevSeedOnFresh = true;
            SundialService svc = Open();
            Assert.Equal(3, svc.Save.Habits.Count);
            Assert.Equal(SundialService.DevSeedStep, svc.Save.FirstRunStep);
        }
        finally { SundialService.DevSeedOnFresh = false; }
    }

    [Fact]
    public void Fresh_reduced_motion_static_is_applied_to_a_fresh_file_only()
    {
        // :68 and :148.
        try
        {
            SundialService.FreshReducedMotion = true;
            SundialService svc = Open();
            Assert.True(svc.ReducedMotion);
            svc.SetMute(false); // any settings edit writes the file
        }
        finally { SundialService.FreshReducedMotion = null; }
        // The file written above is loaded, not fresh, so the static (now null) changes nothing.
        Assert.True(Open().ReducedMotion);
    }

    [Fact]
    public void A_step_back_over_a_live_tend_is_refused()
    {
        // :484-502 TryShiftDay: a live tend after the new today hides a kept day, so the step back is refused.
        SundialService svc = Open();
        HabitDef water = Plant(svc, "morning", "water");
        Assert.True(svc.TendRitual(water.Id).Ok);
        int day = svc.Today().Index;

        Assert.False(svc.TryShiftDay(-1));
        Assert.Equal(day, svc.Today().Index);

        Assert.True(svc.TryShiftDay(1));
        Assert.Equal(day + 1, svc.Today().Index);
        // Back onto the tend's own day is allowed: the tend is not after it.
        Assert.True(svc.TryShiftDay(-1));
        Assert.Equal(day, svc.Today().Index);
    }

    [Fact]
    public void A_step_back_with_no_tend_is_allowed_and_zero_is_a_no_op()
    {
        // :487 delta 0 and :495 no live tend after the new today.
        SundialService svc = Open();
        Plant(svc, "morning", "water");
        int day = svc.Today().Index;
        Assert.True(svc.TryShiftDay(0));
        Assert.Equal(day, svc.Today().Index);
        Assert.True(svc.TryShiftDay(-1));
        Assert.Equal(day - 1, svc.Today().Index);
    }

    [Fact]
    public void Backfill_closes_at_the_default_boundary_instant()
    {
        // :545-553 BackfillOffered compares against EndsAt with GardenDay.DefaultBoundary (03:00), not the saved boundary.
        // With a 06:00 saved boundary, 03:00 to 06:00 is still the previous garden day, and the offer is already closed.
        SundialService svc = Open();
        HabitDef water = Plant(svc, "morning", "water");
        svc.Save.Settings.BoundaryMin = 6 * 60;
        svc.JumpTo(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        Assert.True(svc.BackfillOffered(water));

        svc.JumpTo(new DateTimeOffset(2026, 10, 9, 2, 59, 59, TimeSpan.Zero));
        Assert.True(svc.BackfillOffered(water));

        svc.JumpTo(new DateTimeOffset(2026, 10, 9, 3, 0, 0, TimeSpan.Zero));
        Assert.False(svc.BackfillOffered(water));
    }

    [Fact]
    public void Backfill_is_not_offered_for_a_null_habit_or_an_already_kept_yesterday()
    {
        // :547 null habit, :549 the plant flag is false once yesterday has a live tend.
        SundialService svc = Open();
        HabitDef water = Plant(svc, "morning", "water");
        Assert.False(svc.BackfillOffered(null));
        Assert.True(svc.TryShiftDay(1));
        Assert.True(svc.BackfillOffered(water));
        Assert.True(svc.BackfillYesterday(water).Ok);
        Assert.False(svc.BackfillOffered(water));
    }

    [Fact]
    public void A_finished_focus_hour_tends_its_arc_once()
    {
        // :286-300 CommitFocus reached through TryStartFocus and JumpTo (:222-227, :279-284).
        SundialService svc = Open();
        HabitDef top3 = Plant(svc, "midday", "top3");
        Assert.True(svc.TryStartFocus());
        Assert.Equal(0, svc.LiveCount(top3.Id));

        svc.JumpTo(_clock.Now.AddMinutes(25));
        Assert.Equal(FocusPhase.Complete, svc.Focus.Phase);
        Assert.Equal(1, svc.LiveCount(top3.Id));
        Assert.False(svc.Focus.TendAuthorised);

        svc.JumpTo(_clock.Now.AddMinutes(40));
        svc.Step(0.05f);
        Assert.False(svc.TryEndFocus());
        Assert.Equal(1, svc.LiveCount(top3.Id));

        // A reload restores the finished hour without tending again (:171-172).
        SundialService again = Open();
        Assert.Equal(FocusPhase.Complete, again.Focus.Phase);
        Assert.Equal(1, again.LiveCount(top3.Id));
    }

    [Fact]
    public void Ending_a_focus_hour_early_tends_the_arc_it_started_in()
    {
        // :189-195 TryEndFocus into CommitFocus.
        SundialService svc = Open();
        HabitDef top3 = Plant(svc, "midday", "top3");
        Assert.True(svc.TryStartFocus());
        svc.JumpTo(_clock.Now.AddMinutes(5));
        Assert.True(svc.TryEndFocus());
        Assert.Equal(1, svc.LiveCount(top3.Id));
    }

    [Fact]
    public void A_focus_hour_with_no_habit_on_its_arc_tends_nothing()
    {
        // :292-293 habit == null skips the tend, and the hour is still saved.
        SundialService svc = Open();
        HabitDef water = Plant(svc, "morning", "water");
        Assert.True(svc.TryStartFocus());
        svc.JumpTo(_clock.Now.AddMinutes(25));
        Assert.Equal(FocusPhase.Complete, svc.Focus.Phase);
        Assert.Equal(0, svc.LiveCount(water.Id));
        Assert.NotNull(svc.Save.Focus);
    }

    [Fact]
    public void Gratitude_tends_midday_once_a_day()
    {
        // :234-251 InkGratitude. A second symbol the same day returns null and writes nothing.
        SundialService svc = Open();
        HabitDef top3 = Plant(svc, "midday", "top3");
        TendResult first = svc.InkGratitude(2);
        Assert.NotNull(first);
        Assert.True(first.Ok);
        Assert.Equal(1, svc.LiveCount(top3.Id));

        Assert.Null(svc.InkGratitude(3));
        Assert.Equal(1, svc.LiveCount(top3.Id));
        Assert.Single(svc.Save.Gratitude);
        Assert.Equal(2, svc.Save.Gratitude[0].Symbol);

        Assert.True(svc.TryShiftDay(1));
        Assert.True(svc.InkGratitude(3).Ok);
        Assert.Equal(2, svc.LiveCount(top3.Id));
        Assert.Equal(2, svc.Save.Gratitude.Count);
    }

    [Fact]
    public void Gratitude_without_a_midday_habit_still_inks_the_day_and_returns_null()
    {
        // :242-246.
        SundialService svc = Open();
        Plant(svc, "morning", "water");
        Assert.Null(svc.InkGratitude(1));
        Assert.Single(svc.Save.Gratitude);
    }

    [Fact]
    public void A_failed_load_writes_nothing()
    {
        // :620-622 Persist returns early on a Failed outcome, so the unreadable file is left as it is.
        string live = Path.Combine(_dir, "save.json");
        File.WriteAllText(live, "{ this is not json");
        byte[] before = File.ReadAllBytes(live);

        SundialService svc = Open();
        Assert.Equal(LoadOutcome.Failed, svc.Outcome);
        Assert.NotNull(Plant(svc, "morning", "water"));
        svc.SetMute(true);
        svc.SetFirstRunStep(FirstRunSteps.Appear);

        Assert.Equal(before, File.ReadAllBytes(live));
        Assert.Equal(new[] { live }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void A_second_plant_on_an_arc_keeps_the_first()
    {
        // :355-356 PlantPreset returns the existing row-0 habit.
        SundialService svc = Open();
        HabitDef first = Plant(svc, "morning", "water");
        HabitDef second = Plant(svc, "morning", "stretch");
        Assert.Same(first, second);
        Assert.Single(svc.Save.Habits);
        Assert.Equal("water", svc.Save.Habits[0].PresetKey);

        // A different arc is a different plant.
        Assert.NotSame(first, Plant(svc, "midday", "walk"));
        Assert.Equal(2, svc.Save.Habits.Count);
    }

    [Fact]
    public void A_planted_habit_and_a_tend_survive_a_reload()
    {
        // :620-632 Persist writes the habits and the ledger rows.
        SundialService svc = Open();
        HabitDef water = Plant(svc, "morning", "water");
        Assert.True(svc.TendRitual(water.Id).Ok);

        SundialService again = Open();
        Assert.Equal(LoadOutcome.Loaded, again.Outcome);
        Assert.Equal(1, again.LiveCount(water.Id));
        Assert.Equal("water", again.Save.Habits.Single().PresetKey);
    }

    [Fact]
    public void An_armed_tend_commits_after_six_seconds_and_undo_writes_nothing()
    {
        // :448-473 Arm, Cancel and Step through the deferred window.
        SundialService svc = Open();
        HabitDef water = Plant(svc, "morning", "water");
        svc.Arm(water.Id);
        Assert.True(svc.IsPending);
        Assert.True(svc.Cancel());
        Assert.Equal(0, svc.LiveCount(water.Id));

        svc.Arm(water.Id);
        for (int i = 0; i < 70; i++) svc.Step(0.1f);
        Assert.False(svc.IsPending);
        Assert.Equal(1, svc.LiveCount(water.Id));
    }
}

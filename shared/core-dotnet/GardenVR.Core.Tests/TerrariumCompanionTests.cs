using System;
using System.Globalization;
using GardenVR.Core;
using Xunit;

/// <summary>Companion leaves, yesterday, and the 6 second undo. T-TER-009.</summary>
public class TerrariumCompanionTests
{
    const int Day0 = 9770;

    [Fact]
    public void Six_presets_and_a_cap_of_three()
    {
        Assert.Equal(3, Companions.MaxHabits);
        Assert.Equal(6, Companions.PresetKeys.Length);
        Assert.Equal("Walk", Companions.Label("walk"));
        Assert.Equal("Early night", Companions.Label("early-night"));
        Assert.Equal(CompanionSpecies.GlowSprig, Companions.SpeciesFor("walk"));
        Assert.Equal(CompanionSpecies.MoonMoss, Companions.SpeciesFor("water"));
        Assert.Equal(CompanionSpecies.StarFern, Companions.SpeciesFor("read"));
        Assert.Equal(CompanionSpecies.GlowSprig, Companions.SpeciesFor("stretch"));
        Assert.Equal(CompanionSpecies.MoonMoss, Companions.SpeciesFor("journal"));
        Assert.Equal(CompanionSpecies.StarFern, Companions.SpeciesFor("early-night"));
    }

    [Fact]
    public void Leaves_rise_one_per_kept_day()
    {
        var ledger = new Ledger();
        Assert.Equal(0, Companions.Leaves(ledger, "walk"));
        for (int i = 0; i < 5; i++)
        {
            var result = ledger.Tend("walk", new GardenDay(Day0 + i), TendSource.Poke, Clock(Day0 + i, 21, 0));
            Assert.True(result.Ok);
            Assert.False(result.AlreadyKept);
            Assert.Equal(i + 1, Companions.Leaves(ledger, "walk"));
        }
    }

    [Fact]
    public void A_second_checkin_the_same_day_adds_nothing()
    {
        var ledger = new Ledger();
        var day = new GardenDay(Day0);
        var clock = Clock(Day0, 21, 0);
        Assert.True(ledger.Tend("read", day, TendSource.Pinch, clock).Ok);
        var again = ledger.Tend("read", day, TendSource.Poke, clock);
        Assert.True(again.Ok);
        Assert.True(again.AlreadyKept);
        Assert.Equal(1, Companions.Leaves(ledger, "read"));
        Assert.Single(ledger.Events);
    }

    [Fact]
    public void Yesterday_dates_to_yesterday_only_before_todays_boundary()
    {
        var evening = new DateTimeOffset(2026, 10, 4, 21, 0, 0, TimeSpan.Zero);
        var today = GardenDay.From(evening, GardenDay.DefaultBoundary);
        var clock = new FixedClock(evening, TimeZoneInfo.Utc);
        var ledger = new Ledger();

        var kept = ledger.Backfill("read", new GardenDay(today.Index - 1), today, clock);
        Assert.True(kept.Ok);
        Assert.True(kept.Event.Late);
        Assert.Equal(today.Index - 1, kept.Event.Day);
        Assert.Equal(clock.Now.ToUnixTimeMilliseconds(), kept.Event.AtUtcMs);
        Assert.Equal(1, Companions.Leaves(ledger, "read"));

        var boundary = today.EndsAt(TimeZoneInfo.Utc, GardenDay.DefaultBoundary);
        var after = new FixedClock(boundary, TimeZoneInfo.Utc);
        var rolled = GardenDay.From(after.Now, GardenDay.DefaultBoundary);
        Assert.Equal(today.Index + 1, rolled.Index);

        var closed = ledger.Backfill("journal", new GardenDay(today.Index - 1), today, after);
        Assert.False(closed.Ok);
        Assert.Equal("after-boundary", closed.Reason);

        var skipped = ledger.Backfill("water", new GardenDay(rolled.Index - 2), rolled, after);
        Assert.False(skipped.Ok);
        Assert.Equal("not-yesterday", skipped.Reason);

        var next = ledger.Backfill("water", new GardenDay(rolled.Index - 1), rolled, after);
        Assert.True(next.Ok);
        Assert.Equal(rolled.Index - 1, next.Event.Day);
        Assert.True(next.Event.Late);
    }

    [Fact]
    public void Undo_inside_six_seconds_restores_the_exact_prior_state()
    {
        var clock = Clock(Day0, 21, 0);
        var ledger = new Ledger();
        ledger.Tend("walk", new GardenDay(Day0 - 1), TendSource.Poke, Clock(Day0 - 1, 21, 0));
        int before = Companions.Leaves(ledger, "walk");
        float vitality = Companions.Vitality(ledger, "walk", new GardenDay(Day0));
        int rows = ledger.Events.Count;

        var deferred = new DeferredTend(ledger);
        deferred.Arm("walk", new GardenDay(Day0), TendSource.Poke, clock);
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Null(deferred.Tick(clock));
        Assert.True(deferred.Undo());
        Assert.True(deferred.Undo());
        Assert.False(deferred.IsPending);
        Assert.Equal(before, Companions.Leaves(ledger, "walk"));
        Assert.Equal(rows, ledger.Events.Count);
        Assert.Equal(vitality, Companions.Vitality(ledger, "walk", new GardenDay(Day0)));

        var committed = ledger.Tend("walk", new GardenDay(Day0), TendSource.Pinch, clock);
        Assert.Equal(before + 1, Companions.Leaves(ledger, "walk"));
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.True(ledger.Undo(committed.Event.Id, clock, 6));
        Assert.Equal(before, Companions.Leaves(ledger, "walk"));
        Assert.Equal(vitality, Companions.Vitality(ledger, "walk", new GardenDay(Day0)));

        var kept = ledger.Tend("walk", new GardenDay(Day0), TendSource.Poke, clock);
        clock.Advance(TimeSpan.FromSeconds(7));
        Assert.False(ledger.Undo(kept.Event.Id, clock, 6));
        Assert.Equal(before + 1, Companions.Leaves(ledger, "walk"));
    }

    [Fact]
    public void Vitality_uses_the_garden_curve_and_floor()
    {
        var ledger = new Ledger();
        ledger.Tend("walk", new GardenDay(Day0), TendSource.Poke, Clock(Day0, 21, 0));
        var garden = new Garden();
        garden.CompleteRitual(Day0);
        for (int gap = 0; gap <= 20; gap++)
        {
            var today = new GardenDay(Day0 + gap);
            Assert.Equal(garden.Vitality(today.Index), Companions.Vitality(ledger, "walk", today));
        }
        Assert.Equal(Garden.VitalityFloor, Companions.Vitality(ledger, "walk", new GardenDay(Day0 + 80)));
        Assert.Equal(1f, Companions.Vitality(new Ledger(), "stretch", new GardenDay(Day0)));
    }

    [Fact]
    public void Leaves_never_decrease_over_1000_random_lives_except_a_same_day_undo()
    {
        string detail = CompanionMonotone.FindViolation(seed: 42, lives: 1000, steps: 80);
        Assert.Null(detail);
    }

    static FixedClock Clock(int day, int hour, int minute)
    {
        return new FixedClock(At(day, hour, minute), TimeZoneInfo.Utc);
    }

    static DateTimeOffset At(int dayIndex, int hour, int minute)
    {
        DateTime wall = GardenDay.Epoch.AddDays(dayIndex).AddHours(hour).AddMinutes(minute);
        return new DateTimeOffset(wall, TimeSpan.Zero);
    }
}

/// <summary>Null means leaves only fell on a same-day undo inside the 6 second window.</summary>
static class CompanionMonotone
{
    public static string FindViolation(int seed, int lives, int steps)
    {
        var rng = new Random(seed);
        for (int life = 0; life < lives; life++)
        {
            var ledger = new Ledger();
            var clock = new FixedClock(new DateTimeOffset(GardenDay.Epoch.AddDays(9770).AddHours(21), TimeSpan.Zero), TimeZoneInfo.Utc);
            int prev = 0;
            for (int step = 0; step < steps; step++)
            {
                bool sameDayUndo = false;
                int roll = rng.Next(5);
                if (roll == 0)
                {
                    clock.Advance(TimeSpan.FromDays(rng.Next(0, 4)));
                    clock.Advance(TimeSpan.FromSeconds(rng.Next(0, 40)));
                }
                else if (roll == 1 || roll == 2)
                {
                    GardenDay today = GardenDay.From(clock.Now, GardenDay.DefaultBoundary);
                    ledger.Tend("walk", today, roll == 1 ? TendSource.Poke : TendSource.Pinch, clock);
                }
                else if (roll == 3)
                {
                    GardenDay today = GardenDay.From(clock.Now, GardenDay.DefaultBoundary);
                    ledger.Backfill("walk", new GardenDay(today.Index - 1), today, clock);
                }
                else
                {
                    sameDayUndo = UndoSameDay(ledger, clock);
                }

                int now = Companions.Leaves(ledger, "walk");
                if (now < prev)
                {
                    if (!sameDayUndo || prev - now != 1)
                    {
                        return string.Format(CultureInfo.InvariantCulture,
                            "life {0} step {1}: leaves fell from {2} to {3}, same-day undo {4}",
                            life, step, prev, now, sameDayUndo);
                    }
                }
                prev = now;
            }
        }
        return null;
    }

    static bool UndoSameDay(Ledger ledger, FixedClock clock)
    {
        GardenDay today = GardenDay.From(clock.Now, GardenDay.DefaultBoundary);
        TendEvent found = null;
        for (int i = 0; i < ledger.Events.Count; i++)
        {
            TendEvent ev = ledger.Events[i];
            if (ev == null || ev.HabitId != "walk" || ev.UndoneAtUtcMs.HasValue) continue;
            if (ev.Day != today.Index) continue;
            found = ev;
        }
        if (found == null) return false;
        long age = clock.Now.ToUnixTimeMilliseconds() - found.AtUtcMs;
        if (age > 6000) return false;
        return ledger.Undo(found.Id, clock, 6);
    }
}

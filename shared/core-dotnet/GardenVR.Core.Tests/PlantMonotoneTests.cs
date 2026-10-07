using System;
using System.Collections.Generic;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class PlantMonotoneTests
{
    const int Now = 14 * 60 + 20;

    static FixedClock ClockOn(int day)
    {
        var at = new DateTimeOffset(GardenDay.Epoch.AddDays(day).AddHours(12), TimeSpan.Zero);
        return new FixedClock(at, TimeZoneInfo.Utc);
    }

    static HabitDef Habit(int created)
    {
        return new HabitDef { Id = "water", Group = "midday", CreatedDay = created };
    }

    [Fact]
    public void Clock_set_back_two_days_keeps_the_monotone_lifetime_while_Plant_drops()
    {
        var habit = Habit(100);
        var ledger = new Ledger();
        for (int d = 100; d <= 104; d++)
            Assert.True(ledger.Tend(habit.Id, new GardenDay(d), TendSource.Pinch, ClockOn(d)).Ok);

        var today = new GardenDay(102);
        PlantState plain = SundialRules.Plant(habit, ledger, today, Now);
        PlantState monotone = SundialRules.PlantMonotone(habit, ledger, today, Now);

        Assert.Equal(3, plain.LifetimeKept);
        Assert.Equal(5, monotone.LifetimeKept);
        Assert.Equal(SundialRules.StageFor(5), monotone.Stage);
        Assert.Equal(SundialRules.StageFor(3), plain.Stage);
        Assert.Equal(plain.WindowKept, monotone.WindowKept);
    }

    [Fact]
    public void Both_overloads_agree()
    {
        var habit = Habit(10);
        var ledger = new Ledger();
        ledger.Tend(habit.Id, new GardenDay(10), TendSource.Pinch, ClockOn(10));
        var today = new GardenDay(10);
        PlantState a = SundialRules.PlantMonotone(habit, ledger, today, Now);
        PlantState b = SundialRules.PlantMonotone(habit, ledger, today, Now, null);
        Assert.Equal(a.LifetimeKept, b.LifetimeKept);
        Assert.Equal(a.Stage, b.Stage);
        Assert.Equal(a.DueNow, b.DueNow);
    }

    [Fact]
    public void Undo_removes_the_day_from_the_monotone_lifetime()
    {
        var habit = Habit(100);
        var ledger = new Ledger();
        var clock = ClockOn(103);
        ledger.Tend(habit.Id, new GardenDay(101), TendSource.Pinch, clock);
        TendEvent last = ledger.Tend(habit.Id, new GardenDay(103), TendSource.Pinch, clock).Event;
        var today = new GardenDay(103);
        Assert.Equal(2, SundialRules.PlantMonotone(habit, ledger, today, Now).LifetimeKept);

        Assert.True(ledger.Undo(last.Id, clock));
        PlantState after = SundialRules.PlantMonotone(habit, ledger, today, Now);
        Assert.Equal(1, after.LifetimeKept);
        Assert.Equal(SundialRules.StageFor(1), after.Stage);

        // an undone day after today does not count either
        var back = new GardenDay(101);
        Assert.Equal(1, SundialRules.PlantMonotone(habit, ledger, back, Now).LifetimeKept);
    }

    [Fact]
    public void KeptDaysFrom_ignores_days_before_firstDay_and_counts_past_today()
    {
        var ledger = new Ledger();
        var clock = ClockOn(120);
        foreach (int d in new[] { 95, 99, 100, 100, 110, 120 })
            ledger.Tend("h", new GardenDay(d), TendSource.Pinch, clock);
        ledger.Tend("other", new GardenDay(105), TendSource.Pinch, clock);

        Assert.Equal(3, ledger.KeptDaysFrom("h", 100));
        Assert.Equal(2, ledger.KeptDaysFrom("h", 101));
        Assert.Equal(4, ledger.KeptDaysFrom("h", 99));
        Assert.Equal(0, ledger.KeptDaysFrom("h", 121));
        Assert.Equal(1, ledger.KeptDaysFrom("other", int.MinValue));
        Assert.Equal(0, ledger.KeptDaysFrom("absent", 0));
    }

    [Fact]
    public void KeptDaysFrom_rejects_null_and_empty_habit_id()
    {
        var ledger = new Ledger();
        Assert.Throws<ArgumentException>(() => ledger.KeptDaysFrom(null, 0));
        Assert.Throws<ArgumentException>(() => ledger.KeptDaysFrom("", 0));
    }

    [Fact]
    public void Random_walk_never_falls_except_by_undo_and_matches_Plant_when_nothing_is_ahead()
    {
        const int lives = 1200;
        const int steps = 120;
        var rng = new Random(20261007);
        int compared = 0, setBacks = 0;

        for (int life = 0; life < lives; life++)
        {
            int created = 1000 + rng.Next(50);
            var habit = Habit(created);
            var ledger = new Ledger();
            int today = created + rng.Next(3);
            int prev = 0;

            for (int s = 0; s < steps; s++)
            {
                bool undid = false;
                int roll = rng.Next(100);
                if (roll < 25) today += 1 + rng.Next(2);
                else if (roll < 65)
                {
                    int day = rng.Next(3) == 0 ? today : created + rng.Next(today - created + 1);
                    ledger.Tend(habit.Id, new GardenDay(day), TendSource.Pinch, ClockOn(today));
                }
                else if (roll < 80)
                {
                    var live = new List<TendEvent>();
                    foreach (var e in ledger.Events) if (!e.UndoneAtUtcMs.HasValue) live.Add(e);
                    if (live.Count > 0)
                    {
                        TendEvent pick = live[rng.Next(live.Count)];
                        var clock = new FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(pick.AtUtcMs), TimeZoneInfo.Utc);
                        undid = ledger.Undo(pick.Id, clock);
                    }
                }
                else
                {
                    today = Math.Max(created, today - (1 + rng.Next(3)));
                    setBacks++;
                }

                var day0 = new GardenDay(today);
                PlantState mono = SundialRules.PlantMonotone(habit, ledger, day0, Now);
                PlantState plain = SundialRules.Plant(habit, ledger, day0, Now);

                if (!undid) Assert.True(mono.LifetimeKept >= prev, "lifetime fell without an undo");
                Assert.Equal(SundialRules.StageFor(mono.LifetimeKept), mono.Stage);
                Assert.True(mono.LifetimeKept >= plain.LifetimeKept);
                prev = mono.LifetimeKept;

                bool ahead = false;
                foreach (var e in ledger.Events)
                    if (!e.UndoneAtUtcMs.HasValue && e.Day > today) { ahead = true; break; }
                if (!ahead)
                {
                    compared++;
                    Assert.Equal(plain.HabitId, mono.HabitId);
                    Assert.Equal(plain.LifetimeKept, mono.LifetimeKept);
                    Assert.Equal(plain.Stage, mono.Stage);
                    Assert.Equal(plain.WindowKept, mono.WindowKept);
                    Assert.Equal(plain.Bloom, mono.Bloom);
                    Assert.Equal(plain.DueNow, mono.DueNow);
                    Assert.Equal(plain.CanBackfillYesterday, mono.CanBackfillYesterday);
                    Assert.Equal(plain.Window, mono.Window);
                }
            }
        }

        Assert.True(compared > lives);
        Assert.True(setBacks > lives);
    }
}

using System;
using System.Collections.Generic;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class SundialScheduleTests
{
    static readonly GardenDay Saturday = GardenDay.From(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), GardenDay.DefaultBoundary);

    [Fact]
    public void Off_days_are_blank_but_a_tend_is_still_kept()
    {
        var habit = new HabitDef { Id = "walk", Group = "midday", CreatedDay = Saturday.Index - 10, Schedule = HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek) };
        var ledger = new Ledger();
        Assert.Equal(TileState.NotPlanned, SundialRules.TileOn(habit, ledger, Saturday.Index, Saturday.Index));
        Assert.False(SundialRules.Plant(habit, ledger, Saturday, 720).DueNow);
        Assert.Equal(TileState.Missed, SundialRules.TileOn(habit, ledger, Saturday.Index - 1, Saturday.Index));
        ledger.Tend(habit.Id, Saturday, TendSource.Pinch, new FixedClock(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc));
        Assert.Equal(TileState.Kept, SundialRules.TileOn(habit, ledger, Saturday.Index, Saturday.Index));
    }

    [Fact]
    public void Weekly_days_never_miss_and_tally_resets_on_Monday()
    {
        var habit = new HabitDef { Id = "call", CreatedDay = Saturday.Index - 30, Schedule = HabitSchedule.PerWeek(3) };
        var ledger = new Ledger(new[]
        {
            new TendEvent { Id = "a", HabitId = "call", Day = Saturday.Index - 4 },
            new TendEvent { Id = "b", HabitId = "call", Day = Saturday.Index - 2, Late = true },
            new TendEvent { Id = "c", HabitId = "call", Day = Saturday.Index - 7 },
            new TendEvent { Id = "d", HabitId = "call", Day = Saturday.Index - 1, UndoneAtUtcMs = 1 }
        });
        for (int day = Saturday.Index - 28; day <= Saturday.Index; day++)
            Assert.NotEqual(TileState.Missed, SundialRules.TileOn(habit, ledger, day, Saturday.Index));
        Assert.Equal(TileState.Flexible, SundialRules.TileOn(habit, ledger, Saturday.Index - 1, Saturday.Index));
        Assert.Equal(2, SundialRules.KeptThisWeek(habit, ledger, Saturday));
        Assert.Equal(0, SundialRules.KeptThisWeek(habit, ledger, new GardenDay(Saturday.Index + 2)));
        Assert.Equal(TileState.Before, SundialRules.TileOn(habit, ledger, Saturday.Index + 1, Saturday.Index));
    }

    [Fact]
    public void Least_populated_arc_ignores_archived_habits_and_balances_nine()
    {
        var habits = new List<HabitDef> { new HabitDef { Id = "old", Group = "morning", ArchivedDay = 1 } };
        for (int i = 0; i < 9; i++)
        {
            Assert.True(HabitProfiles.CanAdd(habits));
            string arc = SundialRules.LeastPopulatedArc(habits);
            Assert.Equal(new[] { "morning", "midday", "winddown" }[i % 3], arc);
            Assert.Null(SundialRules.Admit(habits, new HabitDef { Id = "h" + i, Group = arc }));
        }
        Assert.False(HabitProfiles.CanAdd(habits));
        Assert.Equal("arc-full", SundialRules.Admit(habits, new HabitDef { Id = "tenth", Group = SundialRules.LeastPopulatedArc(habits) }));
    }
}


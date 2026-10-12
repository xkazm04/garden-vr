using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Upgrade plan section 6.2: monthly totals, zone fill and quantity habits. Never a consecutive-day count.
public class RecordTests
{
    static readonly TimeSpan Boundary = GardenDay.DefaultBoundary;
    static GardenDay Day(int year, int month, int day) { return new GardenDay((int)(new DateTime(year, month, day) - GardenDay.Epoch).TotalDays); }
    // October 2026: Thursday the 1st to Saturday the 31st. Monday the 5th starts the first full week.
    static readonly GardenMonth October = GardenMonth.Of(Day(2026, 10, 15));

    static FixedClock ClockOn(GardenDay day, int hour = 12)
    {
        var civil = day.CivilDate;
        return new FixedClock(new DateTimeOffset(civil.Year, civil.Month, civil.Day, hour, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    }

    static Ledger Kept(string habitId, params GardenDay[] days)
    {
        var ledger = new Ledger();
        foreach (var d in days)
            Assert.True(ledger.Tend(habitId, d, TendSource.Pinch, ClockOn(d), Boundary).Ok);
        return ledger;
    }

    [Fact]
    public void A_month_spans_its_civil_days()
    {
        Assert.Equal(Day(2026, 10, 1), October.First);
        Assert.Equal(Day(2026, 10, 31), October.Last);
        Assert.Equal(31, October.Days);
        Assert.Equal(29, GardenMonth.Of(Day(2028, 2, 10)).Days);
        Assert.Equal("2026-10", October.ToString());
        Assert.True(October.Contains(Day(2026, 10, 31)));
        Assert.False(October.Contains(Day(2026, 11, 1)));
    }

    [Fact]
    public void Kept_days_count_only_the_month_and_ignore_undone_tends()
    {
        var ledger = Kept("h", Day(2026, 9, 30), Day(2026, 10, 1), Day(2026, 10, 2), Day(2026, 11, 1));
        var clock = ClockOn(Day(2026, 10, 3));
        var tend = ledger.Tend("h", Day(2026, 10, 3), TendSource.Pinch, clock, Boundary);
        Assert.True(ledger.Undo(tend.Event.Id, clock));
        Assert.Equal(2, MonthlyRecord.KeptDays(ledger, "h", October));
    }

    [Fact]
    public void A_daily_habit_is_planned_from_its_creation_to_today()
    {
        var habit = new HabitDef { Id = "h", CreatedDay = Day(2026, 10, 10).Index };
        Assert.Equal(6, MonthlyRecord.PlannedDays(habit, October, Day(2026, 10, 15)));
        Assert.Equal(22, MonthlyRecord.PlannedDays(habit, October, Day(2026, 12, 1)));
        Assert.Equal(0, MonthlyRecord.PlannedDays(habit, October, Day(2026, 10, 9)));
    }

    [Fact]
    public void An_archived_habit_stops_being_planned_the_day_it_was_archived()
    {
        var habit = new HabitDef { Id = "h", CreatedDay = Day(2026, 10, 1).Index, ArchivedDay = Day(2026, 10, 11).Index };
        Assert.Equal(10, MonthlyRecord.PlannedDays(habit, October, Day(2026, 10, 31)));
    }

    [Fact]
    public void A_weekday_habit_is_planned_only_on_its_days()
    {
        var habit = new HabitDef { Id = "h", Schedule = HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek) };
        Assert.Equal(22, MonthlyRecord.PlannedDays(habit, October, Day(2026, 10, 31)));
    }

    [Fact]
    public void A_times_a_week_habit_is_planned_at_most_n_in_each_week()
    {
        var habit = new HabitDef { Id = "h", Schedule = HabitSchedule.PerWeek(3) };
        // Weeks of October 2026: 1-4 (4 days), 5-11, 12-18, 19-25, 26-31 (6 days): 3 + 3 + 3 + 3 + 3.
        Assert.Equal(15, MonthlyRecord.PlannedDays(habit, October, Day(2026, 10, 31)));
        // Up to Friday the 2nd only two days have passed.
        Assert.Equal(2, MonthlyRecord.PlannedDays(habit, October, Day(2026, 10, 2)));
    }

    [Fact]
    public void Extra_keeps_in_a_week_do_not_fill_past_the_plan()
    {
        var habit = new HabitDef { Id = "h", Schedule = HabitSchedule.PerWeek(2) };
        var ledger = Kept("h", Day(2026, 10, 5), Day(2026, 10, 6), Day(2026, 10, 7), Day(2026, 10, 8));
        Assert.Equal(4, MonthlyRecord.KeptDays(ledger, "h", October));
        Assert.Equal(2, MonthlyRecord.KeptTowardPlan(ledger, habit, October, Day(2026, 10, 11)));
    }

    [Fact]
    public void Zone_fill_is_kept_over_planned_across_the_zones_habits()
    {
        var today = Day(2026, 10, 10);
        var habits = new List<HabitDef>
        {
            new HabitDef { Id = "walk", PresetKey = "walk", CreatedDay = Day(2026, 10, 1).Index },
            new HabitDef { Id = "water", PresetKey = "water", CreatedDay = Day(2026, 10, 1).Index },
            new HabitDef { Id = "read", PresetKey = "read", CreatedDay = Day(2026, 10, 1).Index }
        };
        var ledger = new Ledger();
        for (int d = 0; d < 10; d++)
        {
            var day = new GardenDay(Day(2026, 10, 1).Index + d);
            ledger.Tend("walk", day, TendSource.Pinch, ClockOn(day), Boundary);
            if (d % 2 == 0) ledger.Tend("water", day, TendSource.Pinch, ClockOn(day), Boundary);
        }
        Assert.Equal(15f / 20f, ZoneRecord.Fill(ledger, habits, LifeZone.Body, October, today));
        Assert.Equal(0f, ZoneRecord.Fill(ledger, habits, LifeZone.Mind, October, today));
        Assert.Null(ZoneRecord.Fill(ledger, habits, LifeZone.Work, October, today));
    }

    [Fact]
    public void Property_month_totals_and_zone_fill_do_not_depend_on_which_days_were_kept_in_a_row()
    {
        // A consecutive-day count changes when the same number of kept days is spread differently.
        // Every record value must not: the same count of kept days in the month gives the same answer.
        var random = new Random(11);
        var habit = new HabitDef { Id = "h", PresetKey = "walk", CreatedDay = October.First.Index };
        var habits = new List<HabitDef> { habit };
        for (int life = 0; life < 300; life++)
        {
            int keptCount = random.Next(0, October.Days + 1);
            var together = Enumerable.Range(0, keptCount).Select(i => new GardenDay(October.First.Index + i)).ToArray();
            var spread = Enumerable.Range(0, October.Days).OrderBy(_ => random.Next()).Take(keptCount)
                .Select(i => new GardenDay(October.First.Index + i)).ToArray();
            Ledger a = Kept("h", together), b = Kept("h", spread);
            Assert.Equal(MonthlyRecord.KeptDays(a, "h", October), MonthlyRecord.KeptDays(b, "h", October));
            Assert.Equal(MonthlyRecord.KeptTowardPlan(a, habit, October, October.Last), MonthlyRecord.KeptTowardPlan(b, habit, October, October.Last));
            Assert.Equal(ZoneRecord.Fill(a, habits, LifeZone.Body, October, October.Last), ZoneRecord.Fill(b, habits, LifeZone.Body, October, October.Last));
        }
    }

    static string Here([CallerFilePath] string path = "") { return Path.GetDirectoryName(path); }

    [Fact]
    public void No_public_core_member_is_named_for_a_streak_or_a_run_of_days()
    {
        string[] surface = File.ReadAllLines(Path.Combine(Here(), "PublicSurface.approved.txt"));
        foreach (string line in surface)
        {
            string lower = line.ToLowerInvariant();
            Assert.DoesNotContain("streak", lower);
            Assert.DoesNotContain("consecutive", lower);
            Assert.DoesNotContain("inarow", lower);
        }
    }

    [Fact]
    public void A_quantity_habit_is_kept_when_the_target_is_reached()
    {
        var habit = new HabitDef { Id = "water", Target = 3, Unit = "glasses" };
        var tally = new QuantityTally();
        var ledger = new Ledger();
        var day = Day(2026, 10, 7);
        var clock = ClockOn(day);
        QuantityStep one = Quantity.Add(tally, ledger, habit, day, clock, Boundary);
        Assert.Equal(1, one.Total);
        Assert.Null(one.Tend);
        Assert.False(ledger.IsKept("water", day.Index));
        Quantity.Add(tally, ledger, habit, day, clock, Boundary);
        QuantityStep three = Quantity.Add(tally, ledger, habit, day, clock, Boundary);
        Assert.True(three.Reached);
        Assert.True(three.Tend.Ok);
        Assert.True(ledger.IsKept("water", day.Index));
        QuantityStep four = Quantity.Add(tally, ledger, habit, day, clock, Boundary);
        Assert.Equal(4, four.Total);
        Assert.Null(four.Tend);
        Assert.Equal(1, ledger.KeptDays("water"));
    }

    [Fact]
    public void Undoing_the_count_that_reached_the_target_undoes_the_tend()
    {
        var habit = new HabitDef { Id = "water", Target = 2 };
        var tally = new QuantityTally();
        var ledger = new Ledger();
        var day = Day(2026, 10, 7);
        var clock = ClockOn(day);
        Quantity.Add(tally, ledger, habit, day, clock, Boundary);
        QuantityStep two = Quantity.Add(tally, ledger, habit, day, clock, Boundary);
        Assert.True(ledger.IsKept("water", day.Index));
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(Quantity.Undo(tally, ledger, two, clock));
        Assert.Equal(1, tally.Count("water", day.Index));
        Assert.False(ledger.IsKept("water", day.Index));
    }

    [Fact]
    public void A_count_cannot_be_undone_after_the_window()
    {
        var habit = new HabitDef { Id = "water", Target = 2 };
        var tally = new QuantityTally();
        var ledger = new Ledger();
        var day = Day(2026, 10, 7);
        var clock = ClockOn(day);
        QuantityStep one = Quantity.Add(tally, ledger, habit, day, clock, Boundary);
        clock.Advance(TimeSpan.FromSeconds(7));
        Assert.False(Quantity.Undo(tally, ledger, one, clock));
        Assert.Equal(1, tally.Count("water", day.Index));
    }

    [Fact]
    public void A_habit_without_a_target_and_a_future_day_count_nothing()
    {
        var tally = new QuantityTally();
        var ledger = new Ledger();
        var day = Day(2026, 10, 7);
        Assert.Null(Quantity.Add(tally, ledger, new HabitDef { Id = "read" }, day, ClockOn(day), Boundary));
        Assert.Null(Quantity.Add(tally, ledger, new HabitDef { Id = "water", Target = 4 }, new GardenDay(day.Index + 1), ClockOn(day), Boundary));
        Assert.Empty(tally.Events);
    }

    [Fact]
    public void Counts_round_trip_through_json()
    {
        var tally = new QuantityTally();
        var day = Day(2026, 10, 7);
        var clock = ClockOn(day);
        tally.Add("water", day, clock, Boundary);
        CountEvent second = tally.Add("water", day, clock, Boundary);
        tally.Undo(second.Id, clock);
        var root = new JsonObject();
        root.Set("Counts", QuantityJson.WriteCounts(tally.Events));
        var back = new QuantityTally(QuantityJson.ReadCounts(Json.ParseObject(Json.Write(root))));
        Assert.Equal(2, back.Events.Count);
        Assert.Equal(1, back.Count("water", day.Index));
        Assert.Empty(QuantityJson.ReadCounts(Json.ParseObject("{}")));
        Assert.Empty(QuantityJson.ReadCounts(Json.ParseObject("{\"Counts\":null}")));
    }
}

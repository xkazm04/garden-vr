using System;
using System.Collections.Generic;
using System.Linq;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Upgrade plan section 6.3: "What happened since?" at the next visit, at most two days back by default.
public class CatchUpTests
{
    static readonly TimeSpan Boundary = GardenDay.DefaultBoundary;
    static GardenDay Day(int year, int month, int day) { return new GardenDay((int)(new DateTime(year, month, day) - GardenDay.Epoch).TotalDays); }
    // Monday 2026-10-12.
    static readonly GardenDay Monday = Day(2026, 10, 12);

    static FixedClock ClockOn(GardenDay day, int hour = 12, TimeZoneInfo zone = null)
    {
        var civil = day.CivilDate;
        zone = zone ?? TimeZoneInfo.Utc;
        var wall = new DateTime(civil.Year, civil.Month, civil.Day, hour, 0, 0, DateTimeKind.Unspecified);
        return new FixedClock(new DateTimeOffset(wall, zone.GetUtcOffset(wall)), zone);
    }

    static List<HabitDef> Habits(params HabitDef[] habits) { return habits.ToList(); }

    [Fact]
    public void Depth_is_one_to_seven_and_defaults_to_two()
    {
        Assert.Equal(2, CatchUp.Depth(2));
        Assert.Equal(1, CatchUp.Depth(1));
        Assert.Equal(7, CatchUp.Depth(7));
        Assert.Equal(2, CatchUp.Depth(0));
        Assert.Equal(2, CatchUp.Depth(8));
        Assert.Equal(2, CatchUp.Depth(-1));
    }

    [Fact]
    public void After_four_days_away_only_the_last_two_are_offered()
    {
        var habits = Habits(new HabitDef { Id = "walk" }, new HabitDef { Id = "read" });
        var today = new GardenDay(Monday.Index + 5);
        List<CatchUpItem> items = CatchUp.Pending(new Ledger(), habits, Monday.Index, today, CatchUp.DefaultDepth);
        Assert.Equal(new[] { "walk", "read", "walk", "read" }, items.Select(i => i.Habit.Id));
        Assert.Equal(new[] { today.Index - 2, today.Index - 2, today.Index - 1, today.Index - 1 }, items.Select(i => i.Day.Index));
    }

    [Fact]
    public void Nothing_is_offered_on_a_first_visit_or_after_visiting_yesterday_and_keeping_it()
    {
        var habits = Habits(new HabitDef { Id = "walk" });
        var today = new GardenDay(Monday.Index + 1);
        Assert.Empty(CatchUp.Pending(new Ledger(), habits, null, today, 2));
        Assert.Empty(CatchUp.Pending(new Ledger(), habits, today.Index, today, 2));
        var ledger = new Ledger();
        ledger.Tend("walk", Monday, TendSource.Pinch, ClockOn(Monday), Boundary);
        Assert.Empty(CatchUp.Pending(ledger, habits, Monday.Index, today, 2));
    }

    [Fact]
    public void A_day_visited_but_not_kept_is_offered_the_next_day_like_yesterdays_backfill()
    {
        var habits = Habits(new HabitDef { Id = "walk" });
        var items = CatchUp.Pending(new Ledger(), habits, Monday.Index - 1, new GardenDay(Monday.Index + 1), 2);
        Assert.Equal(new[] { Monday.Index }, items.Select(i => i.Day.Index));
    }

    [Fact]
    public void Kept_archived_and_not_yet_created_habit_days_are_not_offered()
    {
        var habits = Habits(
            new HabitDef { Id = "old", ArchivedDay = Monday.Index + 1 },
            new HabitDef { Id = "new", CreatedDay = Monday.Index + 1 },
            new HabitDef { Id = "kept" });
        var ledger = new Ledger();
        ledger.Tend("kept", Monday, TendSource.Pinch, ClockOn(Monday), Boundary);
        ledger.Tend("kept", new GardenDay(Monday.Index + 1), TendSource.Pinch, ClockOn(new GardenDay(Monday.Index + 1)), Boundary);
        var items = CatchUp.Pending(ledger, habits, Monday.Index - 1, new GardenDay(Monday.Index + 2), 2);
        Assert.Equal(new[] { "old@0", "new@1" }, items.Select(i => i.Habit.Id + "@" + (i.Day.Index - Monday.Index)));
    }

    [Fact]
    public void A_weekday_habit_is_offered_only_on_its_days()
    {
        // Away Friday to Sunday, back on Monday. Depth 2 reaches Saturday and Sunday, and a work-week habit plans neither.
        var habits = Habits(new HabitDef { Id = "standup", Schedule = HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek) });
        var items = CatchUp.Pending(new Ledger(), habits, Monday.Index - 4, Monday, 2);
        Assert.Empty(items);
        var friday = CatchUp.Pending(new Ledger(), habits, Monday.Index - 4, Monday, 3);
        Assert.Equal(new[] { Monday.Index - 3 }, friday.Select(i => i.Day.Index));
    }

    [Fact]
    public void A_times_a_week_habit_is_offered_only_while_its_week_is_short()
    {
        var habit = new HabitDef { Id = "gym", Schedule = HabitSchedule.PerWeek(2) };
        var ledger = new Ledger();
        ledger.Tend("gym", Monday, TendSource.Pinch, ClockOn(Monday), Boundary);
        var thursday = new GardenDay(Monday.Index + 3);
        Assert.Equal(2, CatchUp.Pending(ledger, Habits(habit), Monday.Index, thursday, 2).Count);
        ledger.Tend("gym", new GardenDay(Monday.Index + 1), TendSource.Pinch, ClockOn(new GardenDay(Monday.Index + 1)), Boundary);
        Assert.Empty(CatchUp.Pending(ledger, Habits(habit), Monday.Index + 1, thursday, 2));
    }

    [Fact]
    public void A_logged_item_is_a_late_backfill_with_the_real_time()
    {
        var habit = new HabitDef { Id = "walk" };
        var today = new GardenDay(Monday.Index + 2);
        var clock = ClockOn(today, 9);
        var items = CatchUp.Pending(new Ledger(), Habits(habit), Monday.Index - 1, today, 2);
        var ledger = new Ledger();
        TendResult result = CatchUp.Log(ledger, items[0], today, 2, clock, Boundary);
        Assert.True(result.Ok);
        Assert.True(result.Event.Late);
        Assert.Equal(TendSource.Backfill, result.Event.Source);
        Assert.Equal(Monday.Index, result.Event.Day);
        Assert.Equal(clock.Now.ToUnixTimeMilliseconds(), result.Event.AtUtcMs);
        Assert.Equal("already-kept", CatchUp.Log(ledger, items[0], today, 2, clock, Boundary).Reason);
    }

    [Fact]
    public void Logging_is_refused_outside_the_window_and_after_todays_boundary()
    {
        var ledger = new Ledger();
        var today = new GardenDay(Monday.Index + 3);
        var habit = new HabitDef { Id = "walk" };
        Assert.Equal("outside-window", CatchUp.Log(ledger, new CatchUpItem { Habit = habit, Day = Monday }, today, 2, ClockOn(today), Boundary).Reason);
        Assert.Equal("outside-window", CatchUp.Log(ledger, new CatchUpItem { Habit = habit, Day = today }, today, 2, ClockOn(today), Boundary).Reason);
        // 04:00 the next civil day is past today's 03:00 boundary.
        var late = ClockOn(new GardenDay(today.Index + 1), 4);
        Assert.Equal("after-boundary", CatchUp.Log(ledger, new CatchUpItem { Habit = habit, Day = new GardenDay(today.Index - 1) }, today, 2, late, Boundary).Reason);
        Assert.Empty(ledger.Events);
    }

    [Fact]
    public void Backfill_within_one_day_accepts_what_yesterdays_backfill_accepts()
    {
        var today = new GardenDay(Monday.Index + 1);
        var clock = ClockOn(today);
        Ledger a = new Ledger(), b = new Ledger();
        Assert.Equal(a.Backfill("h", Monday, today, clock, Boundary).Ok, b.BackfillWithin("h", Monday, today, 1, clock, Boundary).Ok);
        var before = new GardenDay(Monday.Index - 1);
        Assert.Equal(a.Backfill("h", before, today, clock, Boundary).Ok, b.BackfillWithin("h", before, today, 1, clock, Boundary).Ok);
        Assert.Throws<ArgumentOutOfRangeException>("depth", () => b.BackfillWithin("h", Monday, today, 0, clock, Boundary));
    }

    [Theory]
    [InlineData(28, 1)]
    [InlineData(90, 2)]
    [InlineData(90, 3)]
    public void Property_random_lives_keep_catch_up_honest(int days, int seed)
    {
        // Prague has a DST change at the end of October, inside every 90-day life starting in September.
        TimeZoneInfo zone = FindZone("Europe/Prague", "Central Europe Standard Time");
        var random = new Random(seed);
        var habits = Habits(
            new HabitDef { Id = "walk" },
            new HabitDef { Id = "standup", Schedule = HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek) },
            new HabitDef { Id = "gym", Schedule = HabitSchedule.PerWeek(3) });
        var ledger = new Ledger();
        var start = Day(2026, 9, 1);
        int? lastVisit = null;
        int lastKept = 0;
        for (int d = 0; d < days; d++)
        {
            var today = new GardenDay(start.Index + d);
            if (random.NextDouble() < 0.4) continue; // a day away
            var clock = ClockOn(today, 8 + random.Next(0, 14), zone);
            foreach (var item in CatchUp.Pending(ledger, habits, lastVisit, today, CatchUp.DefaultDepth))
            {
                Assert.InRange(item.Day.Index, today.Index - CatchUp.DefaultDepth, today.Index - 1);
                if (random.NextDouble() < 0.5)
                    Assert.True(CatchUp.Log(ledger, item, today, CatchUp.DefaultDepth, clock, Boundary).Ok);
            }
            foreach (var habit in habits)
                if (random.NextDouble() < 0.6) ledger.Tend(habit.Id, today, TendSource.Pinch, clock, Boundary);
            lastVisit = today.Index;

            int kept = habits.Sum(h => ledger.KeptDays(h.Id));
            Assert.True(kept >= lastKept, "growth fell");
            lastKept = kept;
        }
        foreach (var e in ledger.Events)
        {
            if (e.Source == TendSource.Backfill) Assert.True(e.Late);
            else Assert.False(e.Late);
        }
        Assert.Equal(ledger.Events.Count, ledger.Events.Select(e => e.HabitId + "@" + e.Day).Distinct().Count());
    }

    static TimeZoneInfo FindZone(params string[] ids)
    {
        foreach (string id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}

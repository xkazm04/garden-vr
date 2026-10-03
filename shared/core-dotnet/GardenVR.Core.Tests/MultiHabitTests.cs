using System;
using System.Collections.Generic;
using Xunit;

namespace GardenVR.Core.Tests
{
    /// <summary>
    /// Three habits share one arc. The ledger, the seven-day window, backfill and undo stay per habit.
    /// A fourth live habit in that arc is refused.
    /// </summary>
    public class MultiHabitTests
    {
        [Fact]
        public void An_arc_admits_three_habits_and_refuses_a_fourth()
        {
            var habits = new List<HabitDef>();
            Assert.Null(SundialRules.Admit(habits, Habit("water", "morning", 100)));
            Assert.Null(SundialRules.Admit(habits, Habit("stretch", "morning", 100)));
            Assert.Null(SundialRules.Admit(habits, Habit("bed", "morning", 100)));
            Assert.Equal(3, SundialRules.LiveInArc(habits, "morning"));
            Assert.Equal(0, habits[0].Row);
            Assert.Equal(1, habits[1].Row);
            Assert.Equal(2, habits[2].Row);
            Assert.Equal(SundialRules.MaxTiles, 63);

            HabitDef fourth = Habit("tea", "morning", 100);
            Assert.Equal("arc-full", SundialRules.Admit(habits, fourth));
            Assert.Equal(3, habits.Count);
            Assert.Equal(3, SundialRules.LiveInArc(habits, "Morning"));

            Assert.Null(SundialRules.Admit(habits, Habit("top3", "midday", 100)));
            Assert.Equal(4, habits.Count);
            Assert.Equal(1, SundialRules.LiveInArc(habits, "midday"));
        }

        [Fact]
        public void Archiving_a_habit_frees_its_row()
        {
            var habits = new List<HabitDef>();
            HabitDef water = Habit("water", "morning", 100);
            SundialRules.Admit(habits, water);
            SundialRules.Admit(habits, Habit("stretch", "morning", 100));
            SundialRules.Admit(habits, Habit("bed", "morning", 100));
            water.ArchivedDay = 101;
            Assert.Equal(2, SundialRules.LiveInArc(habits, "morning"));
            Assert.Equal(-1, SundialRules.RowOf(habits, water));

            HabitDef tea = Habit("tea", "morning", 102);
            Assert.Null(SundialRules.Admit(habits, tea));
            Assert.Equal(0, tea.Row);
            Assert.Equal(3, SundialRules.LiveInArc(habits, "morning"));
        }

        [Fact]
        public void A_duplicate_id_or_a_missing_arc_is_refused()
        {
            var habits = new List<HabitDef>();
            SundialRules.Admit(habits, Habit("water", "morning", 100));
            Assert.Equal("duplicate", SundialRules.Admit(habits, Habit("water", "midday", 100)));
            Assert.Single(habits);
            Assert.Equal("arc", SundialRules.Admit(habits, Habit("tea", "evening", 100)));
            Assert.Equal("habit-id", SundialRules.Admit(habits, Habit("", "morning", 100)));
            Assert.Single(habits);
        }

        [Fact]
        public void A_lone_habit_stays_on_row_zero_when_slot_still_holds_the_arc_index()
        {
            var habits = new List<HabitDef>();
            HabitDef wind = Habit("breaths", "wind-down", 40);
            wind.Slot = 2;
            wind.Row = 0;
            habits.Add(wind);
            Assert.Equal(0, SundialRules.RowOf(habits, wind));
            Assert.Equal(1, SundialRules.LiveInArc(habits, "winddown"));
        }

        [Fact]
        public void Three_habits_in_one_arc_keep_separate_ledgers_and_windows()
        {
            const int origin = 9000;
            var habits = new List<HabitDef>();
            HabitDef water = Habit("water", "morning", origin);
            HabitDef stretch = Habit("stretch", "morning", origin);
            HabitDef bed = Habit("bed", "morning", origin);
            Assert.Null(SundialRules.Admit(habits, water));
            Assert.Null(SundialRules.Admit(habits, stretch));
            Assert.Null(SundialRules.Admit(habits, bed));

            var ledger = new Ledger();
            for (int i = 0; i < 5; i++)
                Tend(ledger, water, new GardenDay(origin + i));
            Tend(ledger, stretch, new GardenDay(origin));

            var today = new GardenDay(origin + 6);
            PlantState a = SundialRules.Plant(water, ledger, today, 8 * 60);
            PlantState b = SundialRules.Plant(stretch, ledger, today, 8 * 60);
            PlantState c = SundialRules.Plant(bed, ledger, today, 8 * 60);

            Assert.Equal(5, a.WindowKept);
            Assert.Equal(5, a.LifetimeKept);
            Assert.Equal(Stage.Young, a.Stage);
            Assert.Equal(Bloom.Open, a.Bloom);
            Assert.Equal(TileState.Kept, a.Window[0]);
            Assert.Equal(TileState.Missed, a.Window[5]);
            Assert.Equal(TileState.Today, a.Window[6]);
            Assert.True(a.DueNow);

            Assert.Equal(1, b.WindowKept);
            Assert.Equal(1, b.LifetimeKept);
            Assert.Equal(Stage.Sprout, b.Stage);
            Assert.Equal(Bloom.None, b.Bloom);
            Assert.Equal(TileState.Kept, b.Window[0]);
            Assert.Equal(TileState.Missed, b.Window[1]);
            Assert.True(b.DueNow);

            Assert.Equal(0, c.WindowKept);
            Assert.Equal(0, c.LifetimeKept);
            Assert.Equal(Stage.Seed, c.Stage);
            Assert.Equal(TileState.Missed, c.Window[0]);
            Assert.Equal(TileState.Today, c.Window[6]);
            Assert.True(c.DueNow);
            Assert.Equal(1, ledger.KeptDays("stretch"));
            Assert.Equal(0, ledger.KeptDays("bed"));
            Assert.Equal(5, ledger.KeptDaysInWindow(water.Id, today, 7));
            Assert.Equal(1, ledger.KeptDaysInWindow(stretch.Id, today, 7));
        }

        [Fact]
        public void A_kept_day_ageing_out_lowers_only_that_habits_window()
        {
            const int origin = 9100;
            HabitDef water = Habit("water", "morning", origin);
            HabitDef stretch = Habit("stretch", "morning", origin);
            var ledger = new Ledger();
            for (int i = 0; i < 5; i++)
                Tend(ledger, water, new GardenDay(origin + i));
            Tend(ledger, stretch, new GardenDay(origin));

            var later = new GardenDay(origin + 7);
            PlantState a = SundialRules.Plant(water, ledger, later, 8 * 60);
            PlantState b = SundialRules.Plant(stretch, ledger, later, 8 * 60);
            Assert.Equal(4, a.WindowKept);
            Assert.Equal(5, a.LifetimeKept);
            Assert.Equal(Stage.Young, a.Stage);
            Assert.Equal(0, b.WindowKept);
            Assert.Equal(1, b.LifetimeKept);
            Assert.Equal(Stage.Sprout, b.Stage);
            Assert.Equal(Bloom.None, b.Bloom);
        }

        [Fact]
        public void Backfill_and_undo_touch_one_habit_in_the_arc()
        {
            var today = new GardenDay(9200);
            HabitDef water = Habit("water", "morning", today.Index - 4);
            HabitDef stretch = Habit("stretch", "morning", today.Index - 4);
            HabitDef bed = Habit("bed", "morning", today.Index - 4);
            var ledger = new Ledger();
            var clock = Clock(today, 8, 0);

            TendResult filled = SundialRules.Backfill(water, ledger, today, clock);
            Assert.True(filled.Ok);
            Assert.True(filled.Event.Late);
            Assert.Equal(water.Id, filled.Event.HabitId);

            PlantState waterNow = SundialRules.Plant(water, ledger, today, 8 * 60);
            PlantState stretchNow = SundialRules.Plant(stretch, ledger, today, 8 * 60);
            PlantState bedNow = SundialRules.Plant(bed, ledger, today, 8 * 60);
            Assert.Equal(TileState.Late, waterNow.Window[5]);
            Assert.False(waterNow.CanBackfillYesterday);
            Assert.Equal(TileState.Missed, stretchNow.Window[5]);
            Assert.True(stretchNow.CanBackfillYesterday);
            Assert.True(bedNow.CanBackfillYesterday);

            TendResult stretchFill = SundialRules.Backfill(stretch, ledger, today, clock);
            Assert.True(stretchFill.Ok);
            Assert.True(stretchFill.Event.Late);
            TendResult again = SundialRules.Backfill(water, ledger, today, clock);
            Assert.False(again.Ok);
            Assert.Equal("already-kept", again.Reason);
            Assert.True(SundialRules.Plant(bed, ledger, today, 8 * 60).CanBackfillYesterday);
            Assert.Equal(2, ledger.Events.Count);

            TendResult kept = ledger.Tend(bed.Id, today, TendSource.Pinch, clock);
            Assert.True(kept.Ok);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(ledger.Undo(kept.Event.Id, clock));
            Assert.Equal(0, SundialRules.Plant(bed, ledger, today, 8 * 60).LifetimeKept);
            Assert.Equal(TileState.Today, SundialRules.Plant(bed, ledger, today, 8 * 60).Window[6]);
            Assert.Equal(1, SundialRules.Plant(water, ledger, today, 8 * 60).LifetimeKept);
            Assert.Equal(1, SundialRules.Plant(stretch, ledger, today, 8 * 60).LifetimeKept);
            Assert.Equal(3, ledger.Events.Count);
        }

        [Fact]
        public void Tending_one_habit_clears_only_its_due_flag()
        {
            var today = new GardenDay(9300);
            HabitDef water = Habit("water", "morning", today.Index);
            HabitDef stretch = Habit("stretch", "morning", today.Index);
            HabitDef top3 = Habit("top3", "midday", today.Index);
            var ledger = new Ledger();
            Assert.True(SundialRules.Plant(water, ledger, today, 8 * 60).DueNow);
            Assert.True(SundialRules.Plant(stretch, ledger, today, 8 * 60).DueNow);
            Assert.False(SundialRules.Plant(top3, ledger, today, 8 * 60).DueNow);

            Tend(ledger, water, today);
            Assert.False(SundialRules.Plant(water, ledger, today, 8 * 60).DueNow);
            Assert.True(SundialRules.Plant(stretch, ledger, today, 8 * 60).DueNow);
            Assert.Equal(TileState.Kept, SundialRules.Plant(water, ledger, today, 8 * 60).Window[6]);
            Assert.Equal(TileState.Today, SundialRules.Plant(stretch, ledger, today, 8 * 60).Window[6]);
            Assert.False(SundialRules.Plant(water, ledger, today, 14 * 60).DueNow);
            Assert.False(SundialRules.Plant(stretch, ledger, today, 14 * 60).DueNow);
            Assert.True(SundialRules.Plant(top3, ledger, today, 14 * 60).DueNow);
        }

        [Fact]
        public void The_state_oracle_lists_every_habit_in_the_arc()
        {
            var today = new GardenDay(9400);
            var habits = new List<HabitDef>
            {
                Habit("water", "morning", today.Index),
                Habit("stretch", "morning", today.Index),
                Habit("bed", "morning", today.Index)
            };
            var state = SundialState.Capture(habits, new Ledger(), today, 8 * 60);
            Assert.Equal(3, state.Plants.Count);
            Assert.True(state.Plants[0].DueNow);
            Assert.True(state.Plants[1].DueNow);
            Assert.True(state.Plants[2].DueNow);
            Assert.Contains("\"habit\":\"stretch\"", state.ToJson());
        }

        static HabitDef Habit(string id, string group, int createdDay)
        {
            return new HabitDef
            {
                Id = id,
                PresetKey = id,
                Group = group,
                Kind = HabitKind.LifeCheckIn,
                Slot = 0,
                CreatedDay = createdDay
            };
        }

        static FixedClock Clock(GardenDay day, int hour, int minute)
        {
            var civil = day.CivilDate;
            var wall = new DateTimeOffset(civil.Year, civil.Month, civil.Day, hour, minute, 0, TimeSpan.Zero);
            return new FixedClock(wall, TimeZoneInfo.Utc);
        }

        static void Tend(Ledger ledger, HabitDef habit, GardenDay day)
        {
            TendResult result = ledger.Tend(habit.Id, day, TendSource.Pinch, Clock(day, 12, 0));
            Assert.True(result.Ok, result.Reason);
            Assert.False(result.AlreadyKept);
        }
    }
}

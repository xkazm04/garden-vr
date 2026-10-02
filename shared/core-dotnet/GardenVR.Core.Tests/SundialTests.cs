using System;
using System.Globalization;
using GardenVR.Core;
using Xunit;

public class SundialTests
{
    const int Seed = 20261002;

    [Fact]
    public void Arc_defaults_run_from_0600_1100_1800_to_0300()
    {
        Assert.Equal(3, SundialRules.Arcs.Length);
        Assert.Equal(ArcId.Morning, SundialRules.Arcs[0].Id);
        Assert.Equal(360, SundialRules.Arcs[0].StartMin);
        Assert.Equal(660, SundialRules.Arcs[0].EndMin);
        Assert.Equal(ArcId.Midday, SundialRules.Arcs[1].Id);
        Assert.Equal(660, SundialRules.Arcs[1].StartMin);
        Assert.Equal(1080, SundialRules.Arcs[1].EndMin);
        Assert.Equal(ArcId.WindDown, SundialRules.Arcs[2].Id);
        Assert.Equal(1080, SundialRules.Arcs[2].StartMin);
        Assert.Equal(1620, SundialRules.Arcs[2].EndMin);
        Assert.Equal(180, SundialRules.DayBoundaryMin);
    }

    [Fact]
    public void Arc_at_0559_is_before_morning()
    {
        Assert.False(SundialRules.ArcAt(5 * 60 + 59).HasValue);
        var state = SundialState.Capture(new HabitDef[0], new Ledger(), new GardenDay(10000), 5 * 60 + 59);
        Assert.False(state.Arc.HasValue);
        Assert.Contains("\"arc\":null", state.ToJson());
    }

    [Fact]
    public void Arc_at_0600_is_morning()
    {
        var arc = SundialRules.ArcAt(6 * 60);
        Assert.True(arc.HasValue);
        Assert.Equal(ArcId.Morning, arc.Value);
    }

    [Fact]
    public void Arc_at_1059_is_still_morning()
    {
        var arc = SundialRules.ArcAt(10 * 60 + 59);
        Assert.True(arc.HasValue);
        Assert.Equal(ArcId.Morning, arc.Value);
    }

    [Fact]
    public void Arc_at_1100_is_midday()
    {
        var arc = SundialRules.ArcAt(11 * 60);
        Assert.True(arc.HasValue);
        Assert.Equal(ArcId.Midday, arc.Value);
    }

    [Fact]
    public void Arc_at_1759_is_still_midday()
    {
        var arc = SundialRules.ArcAt(17 * 60 + 59);
        Assert.True(arc.HasValue);
        Assert.Equal(ArcId.Midday, arc.Value);
    }

    [Fact]
    public void Arc_at_1800_is_wind_down()
    {
        var arc = SundialRules.ArcAt(18 * 60);
        Assert.True(arc.HasValue);
        Assert.Equal(ArcId.WindDown, arc.Value);
    }

    [Fact]
    public void Arc_at_0259_is_still_wind_down()
    {
        var atTwoFiftyNine = SundialRules.ArcAt(2 * 60 + 59);
        Assert.True(atTwoFiftyNine.HasValue);
        Assert.Equal(ArcId.WindDown, atTwoFiftyNine.Value);
        // Midnight, either as 00:00 or as minute 1440 from the previous midnight.
        Assert.Equal(ArcId.WindDown, SundialRules.ArcAt(0).Value);
        Assert.Equal(ArcId.WindDown, SundialRules.ArcAt(24 * 60).Value);
        Assert.Equal(ArcId.WindDown, SundialRules.ArcAt(24 * 60 + 2 * 60 + 59).Value);
    }

    [Fact]
    public void Arc_at_0300_is_a_new_day_before_morning()
    {
        Assert.False(SundialRules.ArcAt(3 * 60).HasValue);
        Assert.False(SundialRules.ArcAt(24 * 60 + 3 * 60).HasValue);
    }

    [Fact]
    public void Stage_follows_lifetime_and_bloom_follows_the_week()
    {
        Assert.Equal(Stage.Seed, SundialRules.StageFor(0));
        Assert.Equal(Stage.Seed, SundialRules.StageFor(-1));
        Assert.Equal(Stage.Sprout, SundialRules.StageFor(1));
        Assert.Equal(Stage.Sprout, SundialRules.StageFor(2));
        Assert.Equal(Stage.Young, SundialRules.StageFor(3));
        Assert.Equal(Stage.Young, SundialRules.StageFor(6));
        Assert.Equal(Stage.Leafy, SundialRules.StageFor(7));
        Assert.Equal(Stage.Leafy, SundialRules.StageFor(13));
        Assert.Equal(Stage.Full, SundialRules.StageFor(14));
        Assert.Equal(Stage.Full, SundialRules.StageFor(40));

        Assert.Equal(Bloom.None, SundialRules.BloomFor(0));
        Assert.Equal(Bloom.None, SundialRules.BloomFor(2));
        Assert.Equal(Bloom.Bud, SundialRules.BloomFor(3));
        Assert.Equal(Bloom.Bud, SundialRules.BloomFor(4));
        Assert.Equal(Bloom.Open, SundialRules.BloomFor(5));
        Assert.Equal(Bloom.Open, SundialRules.BloomFor(7));
    }

    [Fact]
    public void A_missed_day_subtracts_nothing_from_lifetime_or_stage()
    {
        var origin = new GardenDay(3000);
        var habit = Habit("water", "midday", origin.Index);
        var ledger = new Ledger();
        Tend(ledger, habit, origin);
        Tend(ledger, habit, new GardenDay(origin.Index + 1));
        Tend(ledger, habit, new GardenDay(origin.Index + 2));

        var kept = SundialRules.Plant(habit, ledger, new GardenDay(origin.Index + 2), 12 * 60);
        Assert.Equal(3, kept.LifetimeKept);
        Assert.Equal(3, kept.WindowKept);
        Assert.Equal(Stage.Young, kept.Stage);

        var across = SundialRules.Plant(habit, ledger, new GardenDay(origin.Index + 4), 12 * 60);
        Assert.Equal(new[]
        {
            TileState.Before, TileState.Before,
            TileState.Kept, TileState.Kept, TileState.Kept,
            TileState.Missed, TileState.Today
        }, across.Window);
        Assert.Equal(3, across.WindowKept);
        Assert.Equal(kept.LifetimeKept, across.LifetimeKept);
        Assert.Equal(kept.Stage, across.Stage);
        Assert.Equal(Stage.Young, across.Stage);
    }

    [Fact]
    public void A_kept_day_ageing_out_lowers_window_kept_and_can_close_a_bloom_but_not_stage()
    {
        var origin = new GardenDay(2000);
        var habit = Habit("water", "midday", origin.Index);
        var ledger = new Ledger();
        for (int i = 0; i < 7; i++)
            Tend(ledger, habit, new GardenDay(origin.Index + i));

        var full = SundialRules.Plant(habit, ledger, new GardenDay(origin.Index + 6), 12 * 60);
        Assert.Equal(7, full.LifetimeKept);
        Assert.Equal(7, full.WindowKept);
        Assert.Equal(Stage.Leafy, full.Stage);
        Assert.Equal(Bloom.Open, full.Bloom);

        var aged = SundialRules.Plant(habit, ledger, new GardenDay(origin.Index + 9), 12 * 60);
        Assert.Equal(new[]
        {
            TileState.Kept, TileState.Kept, TileState.Kept, TileState.Kept,
            TileState.Missed, TileState.Missed, TileState.Today
        }, aged.Window);
        Assert.Equal(4, aged.WindowKept);
        Assert.True(aged.WindowKept < full.WindowKept);
        Assert.Equal(Bloom.Bud, aged.Bloom);
        Assert.NotEqual(full.Bloom, aged.Bloom);
        Assert.Equal(7, aged.LifetimeKept);
        Assert.Equal(full.Stage, aged.Stage);
        Assert.Equal(Stage.Leafy, aged.Stage);
    }

    [Fact]
    public void Lifetime_and_stage_never_decrease_over_one_thousand_seeded_lives()
    {
        var walk = FindGrowthFall(SundialRules.Plant, Seed, 1000);
        Assert.Null(walk.Fall);
        Assert.Equal(1000, walk.LivesCompleted);
        Assert.True(walk.WindowFalls > 0);
        Assert.True(walk.BloomFalls > 0);
    }

    [Fact]
    public void A_window_derived_stage_is_flagged_by_the_property_checker()
    {
        // Round-1 shape: the stage is the window count. A kept day ageing out steps it down.
        var walk = FindGrowthFall(StageFromTheWindow, Seed, 1000);
        Assert.NotNull(walk.Fall);
        Assert.Contains("stage fell", walk.Fall);
        Assert.DoesNotContain("lifetime fell", walk.Fall);
        Assert.True(walk.LivesCompleted < 1000);
    }

    [Fact]
    public void A_late_tile_counts_as_kept_and_renders_late()
    {
        var today = new GardenDay(5000);
        var habit = Habit("water", "midday", today.Index - 6);
        var ledger = new Ledger();
        Tend(ledger, habit, new GardenDay(today.Index - 6));
        var clock = Clock(today, 12, 0);
        var filled = SundialRules.Backfill(habit, ledger, today, clock);
        Assert.True(filled.Ok);
        Assert.True(filled.Event.Late);
        Assert.Equal(TendSource.Backfill, filled.Event.Source);
        Assert.Equal(today.Index - 1, filled.Event.Day);
        Assert.Equal(clock.Now.ToUnixTimeMilliseconds(), filled.Event.AtUtcMs);

        var state = SundialRules.Plant(habit, ledger, today, 14 * 60 + 20);
        Assert.Equal(TileState.Kept, state.Window[0]);
        Assert.Equal(TileState.Late, state.Window[5]);
        Assert.Equal(TileState.Today, state.Window[6]);
        Assert.Equal(2, state.WindowKept);
        Assert.Equal(2, state.LifetimeKept);
        Assert.False(state.CanBackfillYesterday);

        var again = SundialRules.Backfill(habit, ledger, today, clock);
        Assert.False(again.Ok);
        Assert.Equal("already-kept", again.Reason);
        Assert.Equal(2, ledger.Events.Count);
    }

    [Fact]
    public void Due_now_is_false_after_the_arc()
    {
        var today = new GardenDay(5100);
        var midday = Habit("water", "midday", today.Index);
        var ledger = new Ledger();
        Assert.False(SundialRules.Plant(midday, ledger, today, 18 * 60).DueNow);
        Assert.Equal(ArcId.WindDown, SundialRules.ArcAt(18 * 60).Value);

        var morning = Habit("stretch", "morning", today.Index);
        Assert.False(SundialRules.Plant(morning, ledger, today, 11 * 60).DueNow);
    }

    [Fact]
    public void Due_now_is_false_after_keeping_today()
    {
        var today = new GardenDay(5200);
        var habit = Habit("water", "midday", today.Index);
        var ledger = new Ledger();
        Assert.True(SundialRules.Plant(habit, ledger, today, 14 * 60 + 20).DueNow);
        Tend(ledger, habit, today);
        var kept = SundialRules.Plant(habit, ledger, today, 14 * 60 + 20);
        Assert.False(kept.DueNow);
        Assert.Equal(TileState.Kept, kept.Window[6]);
    }

    [Fact]
    public void Due_now_is_true_only_inside_the_arc_while_today_is_open()
    {
        var today = new GardenDay(5300);
        var ledger = new Ledger();
        Assert.True(SundialRules.Plant(Habit("water", "midday", today.Index), ledger, today, 14 * 60 + 20).DueNow);
        Assert.True(SundialRules.Plant(Habit("water", "Midday", today.Index), ledger, today, 11 * 60).DueNow);
        Assert.False(SundialRules.Plant(Habit("water", "midday", today.Index), ledger, today, 10 * 60 + 59).DueNow);
        Assert.False(SundialRules.Plant(Habit("water", null, today.Index), ledger, today, 14 * 60 + 20).DueNow);
    }

    [Fact]
    public void Wind_down_is_due_at_0259_and_not_at_0300()
    {
        var today = new GardenDay(5400);
        var ledger = new Ledger();
        var wind = Habit("breaths", "wind-down", today.Index);
        Assert.True(SundialRules.Plant(wind, ledger, today, 2 * 60 + 59).DueNow);
        Assert.False(SundialRules.Plant(wind, ledger, today, 3 * 60).DueNow);
        Assert.False(SundialRules.Plant(Habit("water", "morning", today.Index), ledger, today, 2 * 60 + 59).DueNow);
    }

    [Fact]
    public void Gnomon_at_1420_is_125_degrees()
    {
        Assert.Equal(125f, SundialRules.GnomonAngleDeg(14 * 60 + 20));
    }

    [Fact]
    public void Gnomon_is_zero_at_0600_and_wraps_after_a_full_turn()
    {
        Assert.Equal(0f, SundialRules.GnomonAngleDeg(6 * 60));
        Assert.Equal(0f, SundialRules.GnomonAngleDeg(6 * 60 + 24 * 60));
        Assert.Equal(345f, SundialRules.GnomonAngleDeg(5 * 60));
        Assert.Equal(300f, SundialRules.GnomonAngleDeg(2 * 60));
    }

    [Fact]
    public void A_habit_created_today_shows_six_before_tiles()
    {
        var today = new GardenDay(8000);
        var habit = Habit("water", "morning", today.Index);
        var ledger = new Ledger();
        Assert.True(ledger.Tend(habit.Id, new GardenDay(today.Index - 1), TendSource.Pinch, Clock(today, 8, 0)).Ok);

        var state = SundialRules.Plant(habit, ledger, today, 8 * 60);
        Assert.Equal(7, state.Window.Length);
        for (int i = 0; i < 6; i++)
            Assert.Equal(TileState.Before, state.Window[i]);
        Assert.Equal(TileState.Today, state.Window[6]);
        Assert.Equal(0, state.WindowKept);
        Assert.Equal(0, state.LifetimeKept);
        Assert.Equal(Stage.Seed, state.Stage);
        Assert.Equal(Bloom.None, state.Bloom);
        Assert.False(state.CanBackfillYesterday);
        Assert.True(state.DueNow);
    }

    [Fact]
    public void Backfill_delegates_once_marks_late_and_stops_at_the_boundary()
    {
        var today = new GardenDay(6000);
        var habit = Habit("water", "midday", today.Index - 3);
        var ledger = new Ledger();
        var end = today.EndsAt(TimeZoneInfo.Utc, GardenDay.DefaultBoundary);
        var clock = new FixedClock(end.AddMilliseconds(-1), TimeZoneInfo.Utc);
        var filled = SundialRules.Backfill(habit, ledger, today, clock);
        Assert.True(filled.Ok);
        Assert.True(filled.Event.Late);
        Assert.Equal(TendSource.Backfill, filled.Event.Source);
        Assert.Equal(today.Index - 1, filled.Event.Day);
        Assert.Equal(clock.Now.ToUnixTimeMilliseconds(), filled.Event.AtUtcMs);
        Assert.NotEqual(today.Index - 2, filled.Event.Day);

        var twice = SundialRules.Backfill(habit, ledger, today, clock);
        Assert.False(twice.Ok);
        Assert.Equal("already-kept", twice.Reason);
        Assert.Single(ledger.Events);

        var closed = new Ledger();
        var atBoundary = new FixedClock(end, TimeZoneInfo.Utc);
        var refused = SundialRules.Backfill(habit, closed, today, atBoundary);
        Assert.False(refused.Ok);
        Assert.Equal("after-boundary", refused.Reason);
        Assert.Empty(closed.Events);

        var born = Habit("water", "midday", today.Index);
        var tooSoon = SundialRules.Backfill(born, new Ledger(), today, clock);
        Assert.False(tooSoon.Ok);
        Assert.Equal("before-created", tooSoon.Reason);
    }

    [Fact]
    public void Undo_removes_the_day_from_the_lifetime_a_miss_does_not()
    {
        var today = new GardenDay(4000);
        var habit = Habit("water", "midday", today.Index);
        var ledger = new Ledger();
        var clock = Clock(today, 12, 0);
        var tend = ledger.Tend(habit.Id, today, TendSource.Pinch, clock);
        Assert.True(tend.Ok);
        var kept = SundialRules.Plant(habit, ledger, today, 12 * 60);
        Assert.Equal(1, kept.LifetimeKept);
        Assert.Equal(Stage.Sprout, kept.Stage);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(ledger.Undo(tend.Event.Id, clock));
        var undone = SundialRules.Plant(habit, ledger, today, 12 * 60);
        Assert.Equal(0, undone.LifetimeKept);
        Assert.Equal(Stage.Seed, undone.Stage);
        Assert.Equal(TileState.Today, undone.Window[6]);
        Assert.Single(ledger.Events);
    }

    [Fact]
    public void Sundial_state_matches_the_golden_json()
    {
        var today = new GardenDay(10000);
        int created = today.Index - 6;
        var habit = Habit("water", "midday", created);
        var ledger = new Ledger();
        Tend(ledger, habit, new GardenDay(created));
        var backfillOn = new GardenDay(created + 3);
        var late = SundialRules.Backfill(habit, ledger, backfillOn, Clock(backfillOn, 12, 0));
        Assert.True(late.Ok);
        Assert.Equal(created + 2, late.Event.Day);
        Tend(ledger, habit, backfillOn);
        Tend(ledger, habit, new GardenDay(created + 4));

        var state = SundialState.Capture(new[] { habit }, ledger, today, 14 * 60 + 20);
        Assert.Equal(860, state.Now);
        Assert.Equal(ArcId.Midday, state.Arc);
        Assert.Equal(125f, state.GnomonDeg);
        Assert.Equal(TileState.Kept, state.Plants[0].Window[0]);
        Assert.Equal(TileState.Today, state.Plants[0].Window[6]);

        const string Golden =
            "{\"now\":860,\"arc\":\"Midday\",\"gnomonDeg\":125,\"plants\":[{\"habit\":\"water\",\"window\":[\"Kept\",\"Missed\",\"Late\",\"Kept\",\"Kept\",\"Missed\",\"Today\"],\"windowKept\":4,\"lifetimeKept\":4,\"stage\":\"Young\",\"bloom\":\"Bud\",\"dueNow\":true,\"canBackfill\":true}]}";
        Assert.Equal(Golden, state.ToJson());
    }

    delegate PlantState PlantFn(HabitDef habit, Ledger ledger, GardenDay today, int nowMin);

    /// <summary>
    /// Round 1 sized the plant from the 7-day window, so the stage fell when a kept day aged out.
    /// </summary>
    static PlantState StageFromTheWindow(HabitDef habit, Ledger ledger, GardenDay today, int nowMin)
    {
        var state = SundialRules.Plant(habit, ledger, today, nowMin);
        state.Stage = SundialRules.StageFor(state.WindowKept);
        return state;
    }

    struct GrowthWalk
    {
        public string Fall;
        public int LivesCompleted;
        public int BloomFalls;
        public int WindowFalls;
    }

    /// <summary>
    /// Each life is a seeded run of kept days and then missed days, repeated.
    /// Lifetime and stage must not fall as the clock moves forward. Undo is not in these lives:
    /// that reversal is allowed to lower the count, and it has its own test.
    /// </summary>
    static GrowthWalk FindGrowthFall(PlantFn plant, int seed, int lives)
    {
        var walk = new GrowthWalk();
        var rng = new Random(seed);
        for (int life = 0; life < lives; life++)
        {
            int origin = 20000 + life * 500;
            var habit = Habit("h" + life.ToString(CultureInfo.InvariantCulture), "midday", origin);
            var ledger = new Ledger();
            int day = 0;
            int prevKept = 0;
            Stage prevStage = Stage.Seed;
            Bloom prevBloom = Bloom.None;
            int prevWindow = 0;
            bool started = false;
            int cycles = 2 + rng.Next(2);
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                int keep = 3 + rng.Next(6);
                int miss = 6 + rng.Next(5);
                string fell = RunDays(plant, habit, ledger, origin, ref day, keep, true, ref prevKept, ref prevStage, ref prevBloom, ref prevWindow, ref started, ref walk);
                if (fell != null) { walk.Fall = fell; return walk; }
                fell = RunDays(plant, habit, ledger, origin, ref day, miss, false, ref prevKept, ref prevStage, ref prevBloom, ref prevWindow, ref started, ref walk);
                if (fell != null) { walk.Fall = fell; return walk; }
            }
            walk.LivesCompleted++;
        }
        return walk;
    }

    static string RunDays(
        PlantFn plant, HabitDef habit, Ledger ledger, int origin, ref int day, int count, bool tend,
        ref int prevKept, ref Stage prevStage, ref Bloom prevBloom, ref int prevWindow, ref bool started, ref GrowthWalk walk)
    {
        for (int i = 0; i < count; i++)
        {
            var today = new GardenDay(origin + day);
            if (tend)
            {
                var result = ledger.Tend(habit.Id, today, TendSource.Pinch, Clock(today, 12, 0));
                if (!result.Ok || result.AlreadyKept)
                    return "tend did not record at " + habit.Id + " day " + day.ToString(CultureInfo.InvariantCulture);
            }
            var state = plant(habit, ledger, today, 12 * 60);
            if (state == null || state.Window == null || state.Window.Length != 7)
                return "plant window is not 7 tiles";
            if (started)
            {
                if (state.LifetimeKept < prevKept)
                    return "lifetime fell at " + habit.Id + " day " + day.ToString(CultureInfo.InvariantCulture)
                        + ": " + prevKept.ToString(CultureInfo.InvariantCulture)
                        + " -> " + state.LifetimeKept.ToString(CultureInfo.InvariantCulture);
                if (Rank(state.Stage) < Rank(prevStage))
                    return "stage fell at " + habit.Id + " day " + day.ToString(CultureInfo.InvariantCulture)
                        + ": " + prevStage + " -> " + state.Stage;
                if (BloomRank(state.Bloom) < BloomRank(prevBloom)) walk.BloomFalls++;
                if (state.WindowKept < prevWindow) walk.WindowFalls++;
            }
            prevKept = state.LifetimeKept;
            prevStage = state.Stage;
            prevBloom = state.Bloom;
            prevWindow = state.WindowKept;
            started = true;
            day++;
        }
        return null;
    }

    static int Rank(Stage stage)
    {
        switch (stage)
        {
            case Stage.Seed: return 0;
            case Stage.Sprout: return 1;
            case Stage.Young: return 2;
            case Stage.Leafy: return 3;
            case Stage.Full: return 4;
            default: throw new InvalidOperationException("unknown stage " + stage);
        }
    }

    static int BloomRank(Bloom bloom)
    {
        switch (bloom)
        {
            case Bloom.None: return 0;
            case Bloom.Bud: return 1;
            case Bloom.Open: return 2;
            default: throw new InvalidOperationException("unknown bloom " + bloom);
        }
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
        var result = ledger.Tend(habit.Id, day, TendSource.Pinch, Clock(day, 12, 0));
        Assert.True(result.Ok, result.Reason);
        Assert.False(result.AlreadyKept);
    }
}

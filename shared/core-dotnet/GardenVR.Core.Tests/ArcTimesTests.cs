using System;
using System.Globalization;
using GardenVR.Core;
using Xunit;

public class ArcTimesTests
{
    [Fact]
    public void Default_schedule_matches_the_fixed_arcs_at_every_minute()
    {
        ArcTimes times = ArcTimes.Default;
        Assert.Equal(360, times.MorningMin);
        Assert.Equal(660, times.MiddayMin);
        Assert.Equal(1080, times.DuskMin);
        Assert.Equal(180, times.DayBoundaryMin);
        for (int minute = 0; minute <= 24 * 60 + 3 * 60; minute++)
            Assert.Equal(SundialRules.ArcAt(minute), times.ArcAt(minute));
        Assert.Equal(SundialRules.ArcAt(-20), times.ArcAt(-20));
        Assert.Equal(ArcId.WindDown, times.ArcAt(2 * 60 + 59));
        Assert.False(times.ArcAt(3 * 60).HasValue);
    }

    [Fact]
    public void Edges_snap_to_fifteen_minutes_and_leave_the_others()
    {
        Assert.Equal(10 * 60, ArcTimes.Snap(10 * 60 + 7));
        Assert.Equal(10 * 60 + 15, ArcTimes.Snap(10 * 60 + 8));
        Assert.Equal(0, ArcTimes.Snap(7));
        Assert.Equal(15, ArcTimes.Snap(8));

        ArcTimes early = ArcTimes.Default.Moved(ArcEdge.Midday, 10 * 60 + 7);
        Assert.Equal(10 * 60, early.MiddayMin);
        Assert.Equal(6 * 60, early.MorningMin);
        Assert.Equal(18 * 60, early.DuskMin);
        Assert.Equal(180, early.DayBoundaryMin);

        ArcTimes later = ArcTimes.Default.Moved(ArcEdge.Midday, 10 * 60 + 8);
        Assert.Equal(10 * 60 + 15, later.MiddayMin);
        Assert.Equal(11 * 60, ArcTimes.Default.MiddayMin);
    }

    [Fact]
    public void A_rim_angle_is_fifteen_degrees_an_hour_from_0600()
    {
        Assert.Equal(6 * 60, ArcTimes.MinuteAtAngle(0f));
        Assert.Equal(6 * 60, ArcTimes.MinuteAtAngle(360f));
        Assert.Equal(11 * 60, ArcTimes.MinuteAtAngle(75f));
        Assert.Equal(15 * 60, ArcTimes.MinuteAtAngle(135f));
        Assert.Equal(0, ArcTimes.MinuteAtAngle(270f));
        Assert.Equal(3 * 60, ArcTimes.MinuteAtAngle(315f));
        Assert.Equal(6 * 60, ArcTimes.MinuteAtAngle(float.NaN));

        ArcTimes times = ArcTimes.Default.Dragged(ArcEdge.Midday, 135f);
        Assert.Equal(15 * 60, times.MiddayMin);
        Assert.Equal(ArcTimes.Default.MorningMin, ArcTimes.Default.Dragged(ArcEdge.Morning, float.NaN).MorningMin);
        Assert.Equal(18 * 60, ArcTimes.Default.Dragged(ArcEdge.Dusk, float.PositiveInfinity).DuskMin);
    }

    [Fact]
    public void An_arc_cannot_shrink_under_an_hour_or_cross_0300()
    {
        ArcTimes squeezed = ArcTimes.Default.Moved(ArcEdge.Midday, 6 * 60 + 15);
        Assert.Equal(7 * 60, squeezed.MiddayMin);

        ArcTimes late = ArcTimes.Default.Moved(ArcEdge.Midday, 17 * 60 + 45);
        Assert.Equal(17 * 60, late.MiddayMin);
        Assert.Equal(18 * 60, late.DuskMin);

        ArcTimes morningEarly = ArcTimes.Default.Dragged(ArcEdge.Morning, SundialRules.GnomonAngleDeg(2 * 60));
        Assert.Equal(3 * 60, morningEarly.MorningMin);
        Assert.Equal(ArcId.Morning, morningEarly.ArcAt(3 * 60));
        Assert.Equal(ArcId.WindDown, morningEarly.ArcAt(2 * 60 + 59));

        ArcTimes morningLate = ArcTimes.Default.Moved(ArcEdge.Morning, 16 * 60);
        Assert.Equal(10 * 60, morningLate.MorningMin);
        Assert.Equal(11 * 60, morningLate.MiddayMin);

        ArcTimes duskLate = ArcTimes.Default.Moved(ArcEdge.Dusk, 2 * 60 + 30);
        Assert.Equal(2 * 60, duskLate.DuskMin);
        Assert.Equal(ArcId.Midday, duskLate.ArcAt(1 * 60 + 59));
        Assert.Equal(ArcId.WindDown, duskLate.ArcAt(2 * 60));
        Assert.Equal(ArcId.WindDown, duskLate.ArcAt(2 * 60 + 59));
        Assert.False(duskLate.ArcAt(3 * 60).HasValue);
        Assert.Equal(180, duskLate.DayBoundaryMin);
    }

    [Fact]
    public void The_day_still_rolls_at_0300_after_the_edges_move()
    {
        ArcTimes times = ArcTimes.Default
            .Moved(ArcEdge.Morning, 7 * 60)
            .Moved(ArcEdge.Midday, 12 * 60)
            .Moved(ArcEdge.Dusk, 20 * 60);
        Assert.Equal(7 * 60, times.MorningMin);
        Assert.Equal(12 * 60, times.MiddayMin);
        Assert.Equal(20 * 60, times.DuskMin);
        Assert.Equal(180, times.DayBoundaryMin);
        Assert.Equal(ArcId.WindDown, times.ArcAt(2 * 60 + 59));
        Assert.Equal(ArcId.WindDown, times.ArcAt(0));
        Assert.False(times.ArcAt(3 * 60).HasValue);
        Assert.False(times.ArcAt(6 * 60 + 59).HasValue);
        Assert.Equal(ArcId.Morning, times.ArcAt(7 * 60));
        Assert.Equal(ArcId.Midday, times.ArcAt(14 * 60 + 20));

        var boundary = GardenDay.DefaultBoundary;
        var before = GardenDay.From(Wall(2026, 10, 2, 2, 59), boundary);
        var atBoundary = GardenDay.From(Wall(2026, 10, 2, 3, 0), boundary);
        Assert.Equal(before.Index + 1, atBoundary.Index);
        Assert.NotEqual(before, atBoundary);
        var end = before.EndsAt(TimeZoneInfo.Utc, boundary);
        Assert.Equal(3, end.Hour);
        Assert.Equal(0, end.Minute);
    }

    [Fact]
    public void Spring_forward_keeps_local_arc_minutes_and_the_0300_roll()
    {
        // Clocks jump from 02:00 to 03:00 on 8 March. The skipped hour is not added to an arc.
        TimeZoneInfo tz = DstZone(3, 8, 2, 11, 1, 2, 1);
        ArcTimes times = ArcTimes.Default.Moved(ArcEdge.Midday, 10 * 60);
        Assert.Equal(ArcId.Morning, times.ArcAt(9 * 60 + 30));
        Assert.Equal(ArcId.Midday, times.ArcAt(10 * 60));

        var march7 = GardenDay.From(Wall(2026, 3, 7, 12, 0), GardenDay.DefaultBoundary);
        Assert.Equal(march7, GardenDay.From(Wall(2026, 3, 8, 1, 30), GardenDay.DefaultBoundary));
        Assert.Equal(ArcId.WindDown, times.ArcAt(1 * 60 + 30));
        Assert.Equal(march7.Index + 1, GardenDay.From(Wall(2026, 3, 8, 3, 0), GardenDay.DefaultBoundary).Index);
        Assert.False(times.ArcAt(3 * 60).HasValue);

        var length = march7.EndsAt(tz, GardenDay.DefaultBoundary) - new GardenDay(march7.Index - 1).EndsAt(tz, GardenDay.DefaultBoundary);
        Assert.Equal(TimeSpan.FromHours(23), length);

        var afterJump = new DateTimeOffset(2026, 3, 8, 10, 0, 0, TimeSpan.FromHours(1));
        Assert.Equal(10 * 60, (int)afterJump.TimeOfDay.TotalMinutes);
        Assert.Equal(ArcId.Midday, times.ArcAt((int)afterJump.TimeOfDay.TotalMinutes));
        Assert.Equal(125f, SundialRules.GnomonAngleDeg(14 * 60 + 20));
    }

    [Fact]
    public void A_spring_gap_that_skips_0300_still_ends_the_garden_day_on_that_boundary()
    {
        // A two-hour jump at 02:00 never shows 03:00. The day ends at the transition.
        TimeZoneInfo tz = DstZone(3, 8, 2, 11, 1, 2, 2);
        var march7 = GardenDay.From(Wall(2026, 3, 7, 12, 0), GardenDay.DefaultBoundary);
        var end = march7.EndsAt(tz, GardenDay.DefaultBoundary);
        Assert.Equal(new DateTime(2026, 3, 8, 2, 0, 0, DateTimeKind.Utc), end.UtcDateTime);
        Assert.False(ArcTimes.Default.ArcAt(4 * 60).HasValue);

        ArcTimes early = ArcTimes.Default.Moved(ArcEdge.Morning, 4 * 60);
        Assert.Equal(4 * 60, early.MorningMin);
        Assert.Equal(180, early.DayBoundaryMin);
        Assert.Equal(ArcId.Morning, early.ArcAt(4 * 60));
        Assert.Equal(ArcId.WindDown, early.ArcAt(2 * 60 + 59));
    }

    [Fact]
    public void Fall_back_keeps_both_showings_of_0130_in_wind_down()
    {
        // Clocks fall back from 02:00 to 01:00 on 1 November. 03:00 itself is unambiguous.
        TimeZoneInfo tz = DstZone(3, 8, 2, 11, 1, 2, 1);
        var oct31 = GardenDay.From(Wall(2026, 10, 31, 12, 0), GardenDay.DefaultBoundary);
        var length = oct31.EndsAt(tz, GardenDay.DefaultBoundary) - new GardenDay(oct31.Index - 1).EndsAt(tz, GardenDay.DefaultBoundary);
        Assert.Equal(TimeSpan.FromHours(25), length);

        ArcTimes times = ArcTimes.Default.Moved(ArcEdge.Dusk, 20 * 60);
        Assert.Equal(ArcId.WindDown, times.ArcAt(1 * 60 + 30));
        Assert.Equal(ArcId.WindDown, times.ArcAt(2 * 60 + 59));
        Assert.False(times.ArcAt(3 * 60).HasValue);
        var rolled = GardenDay.From(Wall(2026, 11, 1, 3, 0), GardenDay.DefaultBoundary);
        Assert.Equal(oct31.Index + 1, rolled.Index);
        Assert.Equal(180, times.DayBoundaryMin);
    }

    [Fact]
    public void Due_follows_the_moved_edge_and_the_shadow_stays_on_the_clock()
    {
        var today = new GardenDay(10000);
        var morning = Habit("water", "morning", today.Index);
        var midday = Habit("top3", "midday", today.Index);
        var ledger = new Ledger();
        int now = 14 * 60 + 20;

        SundialState before = SundialState.Capture(new[] { morning, midday }, ledger, today, now);
        Assert.Equal(ArcId.Midday, before.Arc);
        Assert.Equal(125f, before.GnomonDeg);
        Assert.False(before.Plants[0].DueNow);
        Assert.True(before.Plants[1].DueNow);

        ArcTimes times = ArcTimes.Default.Dragged(ArcEdge.Midday, 135f);
        Assert.Equal(15 * 60, times.MiddayMin);
        SundialState after = SundialState.Capture(new[] { morning, midday }, ledger, today, now, times);
        Assert.Equal(ArcId.Morning, after.Arc);
        Assert.Equal(125f, after.GnomonDeg);
        Assert.Equal(before.GnomonDeg, after.GnomonDeg);
        Assert.True(after.Plants[0].DueNow);
        Assert.False(after.Plants[1].DueNow);
        Assert.Contains("\"gnomonDeg\":125", after.ToJson());
        Assert.Contains("\"arc\":\"Morning\"", after.ToJson());
        Assert.Empty(ledger.Events);

        var block = new FocusBlock();
        var instant = new DateTimeOffset(2026, 10, 3, 14, 20, 0, TimeSpan.Zero);
        Assert.True(block.TryStart(instant, now, times));
        Assert.Equal(ArcId.Morning, block.Arc);
        Assert.Equal(125f, block.StartGnomonDeg);

        var plain = new FocusBlock();
        Assert.True(plain.TryStart(instant, now));
        Assert.Equal(ArcId.Midday, plain.Arc);
    }

    [Fact]
    public void Json_round_trip_keeps_a_dusk_that_starts_after_midnight()
    {
        ArcTimes times = ArcTimes.Default.Moved(ArcEdge.Dusk, 2 * 60);
        ArcTimes again = ArcTimes.Parse(times.ToJson());
        Assert.Equal(times.MorningMin, again.MorningMin);
        Assert.Equal(times.MiddayMin, again.MiddayMin);
        Assert.Equal(2 * 60, again.DuskMin);
        Assert.Equal(180, again.DayBoundaryMin);
        Assert.Equal(times.ArcAt(2 * 60 + 30), again.ArcAt(2 * 60 + 30));
        Assert.Equal(ArcTimes.Default.MiddayMin, ArcTimes.Parse("not json").MiddayMin);
        Assert.Equal(180, ArcTimes.From(24 * 60, 6 * 60, 11 * 60, 18 * 60).DayBoundaryMin);
    }

    [Fact]
    public void A_later_day_boundary_still_owns_the_minutes_before_it()
    {
        ArcTimes times = ArcTimes.From(4 * 60, 6 * 60, 11 * 60, 18 * 60);
        Assert.Equal(4 * 60, times.DayBoundaryMin);
        Assert.Equal(ArcId.WindDown, times.ArcAt(3 * 60 + 30));
        Assert.False(times.ArcAt(4 * 60).HasValue);
        Assert.Equal(ArcId.Morning, times.ArcAt(6 * 60));
        var boundary = TimeSpan.FromHours(4);
        var before = GardenDay.From(Wall(2026, 10, 2, 3, 30), boundary);
        var after = GardenDay.From(Wall(2026, 10, 2, 4, 0), boundary);
        Assert.Equal(before.Index + 1, after.Index);
    }

    static HabitDef Habit(string id, string group, int created)
    {
        return new HabitDef
        {
            Id = id,
            PresetKey = id,
            Group = group,
            Kind = HabitKind.LifeCheckIn,
            CreatedDay = created
        };
    }

    static DateTimeOffset Wall(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }

    static int _zoneIds;

    static TimeZoneInfo DstZone(int startMonth, int startDay, int startHour, int endMonth, int endDay, int endHour, int deltaHours)
    {
        _zoneIds++;
        string id = "GvrArc" + _zoneIds.ToString(CultureInfo.InvariantCulture);
        var start = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, startHour, 0, 0, DateTimeKind.Unspecified), startMonth, startDay);
        var end = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, endHour, 0, 0, DateTimeKind.Unspecified), endMonth, endDay);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Unspecified),
            TimeSpan.FromHours(deltaHours),
            start,
            end);
        return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.Zero, id, id + "S", id + "D", new[] { rule });
    }
}

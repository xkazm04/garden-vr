using System;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class RimScrubTests
{
    const int Now = 14 * 60 + 20;

    [Fact]
    public void A_still_hand_reads_now_and_writes_nothing()
    {
        var today = new GardenDay(8000);
        HabitDef water = Habit("water", "midday", today.Index - 6);
        var ledger = new Ledger();
        PlantState open = SundialRules.Plant(water, ledger, today, Now);
        Assert.True(open.DueNow);
        Assert.True(open.CanBackfillYesterday);
        int before = ledger.Events.Count;
        string live = SundialState.Capture(new[] { water }, ledger, today, Now).ToJson();

        RimRead read = RimScrub.FromDrag(Now, 0f);
        Assert.True(read.AtNow);
        Assert.Equal(0, read.DaysBack);
        Assert.Equal(Now, read.Minute);
        Assert.Equal(0, read.RewindMinutes);
        Assert.Equal(125f, read.GnomonDeg, 3);
        Assert.Equal("", read.Caption);
        Assert.False(RimScrub.AllowsWrite(true));
        Assert.True(RimScrub.AllowsWrite(false));

        SundialState picture = RimScrub.Query(new[] { water }, ledger, today, read);
        Assert.Equal(Now, picture.Now);
        Assert.Equal(ArcId.Midday, picture.Arc);
        Assert.Equal(125f, picture.GnomonDeg, 3);
        Assert.False(picture.Plants[0].DueNow);
        Assert.False(picture.Plants[0].CanBackfillYesterday);
        Assert.Equal(TileState.Today, picture.Plants[0].Window[6]);
        Assert.Equal(before, ledger.Events.Count);
        Assert.Equal(live, SundialState.Capture(new[] { water }, ledger, today, Now).ToJson());

        string json = RimScrub.ToJson(read);
        AssertQuiet(json);
        JsonObject root = Json.ParseObject(json);
        Assert.Equal(0, root.Get("daysBack").AsInt());
        Assert.Equal(Now, root.Get("minute").AsInt());
        Assert.True(root.Get("atNow").AsBool());
        Assert.Equal("", root.Get("caption").AsString());
    }

    [Fact]
    public void Eight_hours_back_is_earlier_today()
    {
        Ledger ledger = Seed(out HabitDef water, out GardenDay today);
        RimRead read = RimScrub.FromDrag(Now, 8f * RimScrub.DegreesPerHour);
        Assert.False(read.AtNow);
        Assert.Equal(0, read.DaysBack);
        Assert.Equal(6 * 60 + 20, read.Minute);
        Assert.Equal(8 * 60, read.RewindMinutes);
        Assert.Equal(5f, read.GnomonDeg, 3);
        Assert.Equal(RimScrub.EarlierToday, read.Caption);
        Assert.DoesNotContain("\u2014", read.Caption);
        Assert.DoesNotContain("0", read.Caption);
        Assert.DoesNotContain("1", read.Caption);

        SundialState picture = RimScrub.Query(new[] { water }, ledger, today, read);
        Assert.Equal(ArcId.Morning, picture.Arc);
        Assert.Equal(TileState.Today, picture.Plants[0].Window[6]);
        Assert.False(picture.Plants[0].DueNow);
        Assert.Equal(2, picture.Plants[0].LifetimeKept);
        Assert.Equal(2, ledger.Events.Count);
    }

    [Fact]
    public void A_full_turn_reads_yesterday_and_leaves_the_miss_beside_it()
    {
        Ledger ledger = Seed(out HabitDef water, out GardenDay today);
        string live = SundialState.Capture(new[] { water }, ledger, today, Now).ToJson();
        RimRead read = RimScrub.FromDrag(Now, 360f);
        Assert.Equal(1, read.DaysBack);
        Assert.Equal(Now, read.Minute);
        Assert.Equal(24 * 60, read.RewindMinutes);
        Assert.Equal(125f, read.GnomonDeg, 3);
        Assert.Equal(RimScrub.Yesterday, read.Caption);

        SundialState picture = RimScrub.Query(new[] { water }, ledger, today, read);
        Assert.Equal(ArcId.Midday, picture.Arc);
        Assert.Equal(TileState.Kept, picture.Plants[0].Window[6]);
        Assert.Equal(TileState.Missed, picture.Plants[0].Window[5]);
        Assert.Equal(TileState.Kept, picture.Plants[0].Window[4]);
        Assert.False(picture.Plants[0].DueNow);
        Assert.False(picture.Plants[0].CanBackfillYesterday);
        Assert.Equal(2, picture.Plants[0].LifetimeKept);
        Assert.Equal(2, ledger.Events.Count);
        Assert.Equal(live, SundialState.Capture(new[] { water }, ledger, today, Now).ToJson());
        AssertQuiet(picture.ToJson());
        AssertQuiet(RimScrub.ToJson(read));
    }

    [Fact]
    public void The_week_ends_at_the_oldest_morning_boundary()
    {
        RimRead read = RimScrub.FromDrag(Now, 100000f);
        Assert.Equal(6, read.DaysBack);
        Assert.Equal(SundialRules.DayBoundaryMin, read.Minute);
        Assert.Equal(RimScrub.MaxRewindMinutes(Now), read.RewindMinutes);
        Assert.Equal(315f, read.GnomonDeg, 3);
        Assert.Equal(RimScrub.EarlierWeek, read.Caption);
        Assert.False(SundialRules.ArcAt(read.Minute).HasValue);

        RimRead edge = RimScrub.FromDrag(Now, 170f);
        Assert.Equal(0, edge.DaysBack);
        Assert.Equal(3 * 60, edge.Minute);

        RimRead over = RimScrub.FromDrag(Now, 170.25f);
        Assert.Equal(1, over.DaysBack);
        Assert.Equal(2 * 60 + 59, over.Minute);
        Assert.Equal(ArcId.WindDown, SundialRules.ArcAt(over.Minute));
    }

    [Fact]
    public void A_backward_clock_and_a_bad_drag_stay_at_now()
    {
        Assert.True(RimScrub.FromDrag(Now, -40f).AtNow);
        Assert.True(RimScrub.FromDrag(Now, float.NaN).AtNow);
        Assert.True(RimScrub.FromDrag(Now, float.PositiveInfinity).AtNow);
        RimRead wrapped = RimScrub.FromDrag(-20, 0f);
        Assert.True(wrapped.AtNow);
        Assert.Equal(23 * 60 + 40, wrapped.Minute);

        RimRead late = RimScrub.FromDrag(1 * 60 + 30, 0f);
        Assert.Equal(1 * 60 + 30, late.Minute);
        Assert.True(late.AtNow);
        Assert.Equal(ArcId.WindDown, SundialRules.ArcAt(late.Minute));

        RimRead quiet = RimScrub.FromDrag(4 * 60 + 30, 0f);
        Assert.Equal(4 * 60 + 30, quiet.Minute);
        Assert.False(SundialRules.ArcAt(quiet.Minute).HasValue);
    }

    [Fact]
    public void A_null_ledger_is_refused_and_an_empty_list_stays_quiet()
    {
        var today = new GardenDay(40);
        Assert.Throws<ArgumentNullException>(() => RimScrub.Query(Array.Empty<HabitDef>(), null, today, RimScrub.FromDrag(Now, 0f)));

        SundialState none = RimScrub.Query(null, new Ledger(), today, RimScrub.FromDrag(Now, 90f));
        Assert.Empty(none.Plants);
        Assert.Equal(RimScrub.EarlierToday, RimScrub.FromDrag(Now, 90f).Caption);
        AssertQuiet(none.ToJson());
    }

    static Ledger Seed(out HabitDef water, out GardenDay today)
    {
        today = new GardenDay(8000);
        water = Habit("water", "morning", today.Index - 6);
        var ledger = new Ledger();
        Tend(ledger, water, new GardenDay(today.Index - 3));
        Tend(ledger, water, new GardenDay(today.Index - 1));
        return ledger;
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

    static void Tend(Ledger ledger, HabitDef habit, GardenDay day)
    {
        var civil = day.CivilDate;
        var wall = new DateTimeOffset(civil.Year, civil.Month, civil.Day, 8, 0, 0, TimeSpan.Zero);
        TendResult result = ledger.Tend(habit.Id, day, TendSource.Pinch, new FixedClock(wall, TimeZoneInfo.Utc));
        Assert.True(result.Ok, result.Reason);
    }

    static void AssertQuiet(string json)
    {
        Assert.DoesNotContain("streak", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shame", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wilt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\u2014", json, StringComparison.Ordinal);
        Assert.False(json.Contains("count", StringComparison.OrdinalIgnoreCase) && json.Contains("run", StringComparison.OrdinalIgnoreCase));
    }
}

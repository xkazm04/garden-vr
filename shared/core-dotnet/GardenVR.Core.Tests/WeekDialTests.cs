using System;
using GardenVR.Core;
using Xunit;

public class WeekDialTests
{
    [Fact]
    public void Four_weeks_over_twenty_eight_days_keep_misses_and_a_backfill()
    {
        Ledger ledger = new Ledger();
        int day0 = GardenDay.From(At(0, 8, 0), GardenDay.DefaultBoundary).Index;
        HabitDef water = Habit("water", "morning", day0);
        HabitDef top3 = Habit("top3", "midday", day0 + 7);
        HabitDef breaths = Habit("breaths", "wind-down", day0 + 21);
        Walk(ledger, water, top3, breaths, day0);

        var today = new GardenDay(day0 + 27);
        WeekDial dial = WeekDial.Read(new[] { water, top3, breaths }, ledger, today);
        Assert.Equal(3, dial.Plants.Count);

        AssertTiles(dial.For("water"),
            "KKKMLKM" +
            "KKKMLKK" +
            "KKKMLKK" +
            "KKKMLKT");
        AssertTiles(dial.For("top3"),
            "BBBBBBB" +
            "KKKKKKK" +
            "MLKKKKK" +
            "MKKKKKT");
        AssertTiles(dial.For("breaths"),
            "BBBBBBB" +
            "BBBBBBB" +
            "BBBBBBB" +
            "KKKMLKT");

        WeekRecord waterRow = dial.For("water");
        Assert.Equal(TileState.Missed, waterRow.At(0, 3));
        Assert.Equal(TileState.Late, waterRow.At(0, 4));
        Assert.Equal(TileState.Missed, waterRow.At(0, 6));
        Assert.Equal(TileState.Kept, waterRow.At(0, 5));
        Assert.Equal(TileState.Today, waterRow.At(3, 6));
        Assert.Equal(TileState.Before, dial.For("breaths").At(0, 0));
        Assert.Equal(TileState.Before, dial.For("top3").At(0, 6));
        Assert.Equal(TileState.Kept, dial.For("top3").At(1, 0));

        PlantState plant = SundialRules.Plant(water, ledger, today, 14 * 60 + 20);
        for (int i = 0; i < 7; i++)
            Assert.Equal(plant.Window[i], waterRow.Days[21 + i]);
        Assert.Equal(22, plant.LifetimeKept);
        Assert.Equal(Stage.Full, plant.Stage);
        Assert.Equal(plant.WindowKept, CountWindow(waterRow));

        string json = dial.ToJson();
        Assert.DoesNotContain("streak", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shame", json, StringComparison.OrdinalIgnoreCase);
        JsonObject root = Json.ParseObject(json);
        Assert.Equal(4, root.Get("weeks").AsInt());
        Assert.Equal(28, root.Get("span").AsInt());
        Assert.False(root.Has("count"));
        JsonArray plants = root.Get("plants").AsArray();
        Assert.Equal(3, plants.Count);
        JsonObject first = plants[0].AsObject();
        Assert.Equal("water", first.Get("habit").AsString());
        Assert.Equal(28, first.Get("tiles").AsArray().Count);
        Assert.False(first.Has("count"));
        Assert.Equal("Late", first.Get("tiles").AsArray()[4].AsString());
        Assert.Equal("Missed", first.Get("tiles").AsArray()[3].AsString());
        Assert.Equal("Today", first.Get("tiles").AsArray()[27].AsString());
    }

    [Fact]
    public void A_miss_leaves_the_days_around_it()
    {
        var ledger = new Ledger();
        int day0 = GardenDay.From(At(0, 14, 20), GardenDay.DefaultBoundary).Index;
        var habit = Habit("water", "midday", day0);
        var clock = new FixedClock(At(0, 14, 20), TimeZoneInfo.Utc);
        Assert.True(ledger.Tend(habit.Id, new GardenDay(day0), TendSource.Pinch, clock).Ok);
        clock.Now = At(2, 14, 20);
        Assert.True(ledger.Tend(habit.Id, new GardenDay(day0 + 2), TendSource.Pinch, clock).Ok);

        var today = new GardenDay(day0 + 2);
        WeekRecord row = WeekDial.Read(new[] { habit }, ledger, today).For("water");
        Assert.Equal(TileState.Kept, row.Days[25]);
        Assert.Equal(TileState.Missed, row.Days[26]);
        Assert.Equal(TileState.Kept, row.Days[27]);
        Assert.Equal(2, SundialRules.Plant(habit, ledger, today, 14 * 60).LifetimeKept);
    }

    [Fact]
    public void An_empty_ledger_and_a_null_habit_list_stay_quiet()
    {
        var today = new GardenDay(40);
        WeekDial none = WeekDial.Read(null, new Ledger(), today);
        Assert.Empty(none.Plants);
        Assert.DoesNotContain("streak", none.ToJson(), StringComparison.OrdinalIgnoreCase);

        var habit = Habit("water", "morning", 40);
        WeekDial fresh = WeekDial.Read(new[] { habit, null }, new Ledger(), today);
        WeekRecord row = fresh.For("water");
        Assert.Equal(28, row.Days.Length);
        for (int i = 0; i < 27; i++)
            Assert.Equal(TileState.Before, row.Days[i]);
        Assert.Equal(TileState.Today, row.Days[27]);
        Assert.Null(fresh.For("missing"));
    }

    [Fact]
    public void A_null_ledger_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => WeekDial.Read(new HabitDef[0], null, new GardenDay(1)));
        Assert.Throws<ArgumentNullException>(() => SundialRules.TileOn(null, new Ledger(), 1, 1));
    }

    static void Walk(Ledger ledger, HabitDef water, HabitDef top3, HabitDef breaths, int day0)
    {
        for (int i = 0; i < 28; i++)
        {
            var morning = new FixedClock(At(i, 8, 0), TimeZoneInfo.Utc);
            var today = GardenDay.From(morning.Now, GardenDay.DefaultBoundary);
            Assert.Equal(day0 + i, today.Index);
            if (i > 0 && (i - 1) % 7 == 4)
            {
                TendResult filled = SundialRules.Backfill(water, ledger, today, morning);
                Assert.True(filled.Ok, filled.Reason);
                Assert.True(filled.Event.Late);
                Assert.Equal(day0 + i - 1, filled.Event.Day);
            }
            if (i == 16)
            {
                TendResult filled = SundialRules.Backfill(top3, ledger, today, morning);
                Assert.True(filled.Ok, filled.Reason);
                Assert.True(filled.Event.Late);
            }
            if (i == 26)
            {
                TendResult filled = SundialRules.Backfill(breaths, ledger, today, morning);
                Assert.True(filled.Ok, filled.Reason);
                Assert.True(filled.Event.Late);
            }

            var afternoon = new FixedClock(At(i, 14, 20), TimeZoneInfo.Utc);
            today = GardenDay.From(afternoon.Now, GardenDay.DefaultBoundary);
            if (KeepWater(i))
            {
                TendResult tend = ledger.Tend(water.Id, today, TendSource.Pinch, afternoon);
                Assert.True(tend.Ok, tend.Reason);
                if (i == 6)
                    Assert.True(ledger.Undo(tend.Event.Id, afternoon, 6));
            }
            if (KeepTop3(i))
            {
                TendResult tend = ledger.Tend(top3.Id, today, TendSource.Pinch, afternoon);
                Assert.True(tend.Ok, tend.Reason);
            }
            if (KeepBreaths(i))
            {
                TendResult tend = ledger.Tend(breaths.Id, today, TendSource.Pinch, afternoon);
                Assert.True(tend.Ok, tend.Reason);
            }
        }
    }

    static bool KeepWater(int day)
    {
        if (day == 27 || day % 7 == 3 || day % 7 == 4) return false;
        return true;
    }

    static bool KeepTop3(int day)
    {
        if (day < 7 || day == 27 || day == 14 || day == 15 || day == 21) return false;
        return true;
    }

    static bool KeepBreaths(int day)
    {
        return day == 21 || day == 22 || day == 23 || day == 26;
    }

    static void AssertTiles(WeekRecord row, string pattern)
    {
        Assert.NotNull(row);
        Assert.Equal(28, pattern.Length);
        Assert.Equal(28, row.Days.Length);
        for (int i = 0; i < pattern.Length; i++)
            Assert.Equal(Expect(pattern[i]), row.Days[i]);
    }

    static TileState Expect(char mark)
    {
        switch (mark)
        {
            case 'K': return TileState.Kept;
            case 'L': return TileState.Late;
            case 'M': return TileState.Missed;
            case 'T': return TileState.Today;
            case 'B': return TileState.Before;
            default: throw new InvalidOperationException("mark " + mark);
        }
    }

    static int CountWindow(WeekRecord row)
    {
        int kept = 0;
        for (int i = 21; i < 28; i++)
        {
            if (row.Days[i] == TileState.Kept || row.Days[i] == TileState.Late) kept++;
        }
        return kept;
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

    static DateTimeOffset At(int day, int hour, int minute)
    {
        return new DateTimeOffset(2026, 6, 1, hour, minute, 0, TimeSpan.Zero).AddDays(day);
    }
}

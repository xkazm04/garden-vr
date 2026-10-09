using System;
using System.Collections.Generic;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Upgrade plan section 6.1: a habit's own name, zone, schedule and quantity target, and how saves carry them.
public class HabitProfileTests
{
    // 2026-10-05 is a Monday.
    static GardenDay Day(int year, int month, int day) { return new GardenDay((int)(new DateTime(year, month, day) - GardenDay.Epoch).TotalDays); }
    static readonly GardenDay Monday = Day(2026, 10, 5);

    static LedgerJson.Choices With(bool rowKnown, bool profileKnown)
    {
        return new LedgerJson.Choices { NullGroupAsEmpty = rowKnown, SourceFallback = TendSource.Pinch, RowKnown = rowKnown, ProfileKnown = profileKnown };
    }

    static Dictionary<string, Dictionary<string, JsonValue>> NewExtras() { return new Dictionary<string, Dictionary<string, JsonValue>>(); }

    [Fact]
    public void Weeks_start_on_monday_and_weekday_bits_count_from_monday()
    {
        Assert.Equal(DayOfWeek.Monday, Monday.CivilDate.DayOfWeek);
        for (int i = 0; i < 7; i++)
        {
            var day = new GardenDay(Monday.Index + i);
            Assert.Equal(Monday, HabitSchedule.WeekStart(day));
            Assert.Equal((byte)(1 << i), HabitSchedule.WeekdayBit(day));
        }
        Assert.Equal(new GardenDay(Monday.Index + 7), HabitSchedule.WeekStart(new GardenDay(Monday.Index + 13)));
    }

    [Fact]
    public void A_weekday_schedule_is_planned_only_on_its_days()
    {
        var workWeek = HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek);
        for (int i = 0; i < 7; i++)
            Assert.Equal(i < 5, workWeek.IsPlannedOn(new GardenDay(Monday.Index + i)));
        Assert.Equal(5, workWeek.PlannedInWeek());
    }

    [Fact]
    public void Daily_and_times_a_week_are_planned_every_day()
    {
        var thrice = HabitSchedule.PerWeek(3);
        for (int i = 0; i < 7; i++)
        {
            Assert.True(HabitSchedule.Daily().IsPlannedOn(new GardenDay(Monday.Index + i)));
            Assert.True(thrice.IsPlannedOn(new GardenDay(Monday.Index + i)));
        }
        Assert.Equal(7, HabitSchedule.Daily().PlannedInWeek());
        Assert.Equal(3, thrice.PlannedInWeek());
    }

    [Fact]
    public void The_factories_refuse_schedules_that_cannot_happen()
    {
        Assert.Throws<ArgumentOutOfRangeException>("times", () => HabitSchedule.PerWeek(0));
        Assert.Throws<ArgumentOutOfRangeException>("times", () => HabitSchedule.PerWeek(8));
        Assert.Throws<ArgumentOutOfRangeException>("mask", () => HabitSchedule.OnWeekdays(0));
        Assert.Throws<ArgumentOutOfRangeException>("mask", () => HabitSchedule.OnWeekdays(0x80));
        Assert.False(new HabitSchedule { Kind = ScheduleKind.TimesPerWeek, TimesPerWeek = 9 }.IsValid);
        Assert.False(new HabitSchedule { Kind = (ScheduleKind)7 }.IsValid);
    }

    [Fact]
    public void A_habit_with_no_schedule_or_an_invalid_one_is_daily()
    {
        Assert.Equal(ScheduleKind.Daily, HabitProfiles.ScheduleOf(new HabitDef()).Kind);
        var broken = new HabitDef { Schedule = new HabitSchedule { Kind = ScheduleKind.Weekdays } };
        Assert.Equal(ScheduleKind.Daily, HabitProfiles.ScheduleOf(broken).Kind);
    }

    [Theory]
    [InlineData("walk", LifeZone.Body)]
    [InlineData("water", LifeZone.Body)]
    [InlineData("read", LifeZone.Mind)]
    [InlineData("journal", LifeZone.Mind)]
    [InlineData("top3", LifeZone.Work)]
    [InlineData("breaths", LifeZone.Mind)]
    public void A_preset_brings_its_zone(string preset, LifeZone zone)
    {
        Assert.Equal(zone, HabitProfiles.ZoneOf(new HabitDef { PresetKey = preset }));
    }

    [Fact]
    public void Every_terrarium_preset_has_a_zone()
    {
        foreach (string key in Companions.PresetKeys)
        {
            LifeZone zone;
            Assert.True(HabitProfiles.TryPresetZone(key, out zone), key);
        }
    }

    [Fact]
    public void The_habits_own_zone_wins_and_an_unknown_preset_is_mind()
    {
        Assert.Equal(LifeZone.Connection, HabitProfiles.ZoneOf(new HabitDef { PresetKey = "walk", Zone = LifeZone.Connection }));
        Assert.Equal(LifeZone.Mind, HabitProfiles.ZoneOf(new HabitDef { PresetKey = "juggle" }));
        Assert.Equal(LifeZone.Mind, HabitProfiles.ZoneOf(new HabitDef()));
    }

    [Theory]
    [InlineData("  call   mum  ", "call mum")]
    [InlineData("tea—no sugar", "tea no sugar")]
    [InlineData("line\nbreak\ttab", "line break tab")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("practise the guitar for twenty minutes", "practise the guitar for twenty")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghij", "abcdefghijklmnopqrstuvwxyzabcdef")]
    public void Names_are_cleaned_before_they_are_kept(string raw, string expected)
    {
        Assert.Equal(expected, HabitProfiles.CleanName(raw));
    }

    [Fact]
    public void A_clean_name_never_holds_an_em_dash_or_runs_past_the_limit()
    {
        var random = new Random(7);
        const string alphabet = "ab —–\t\n.";
        for (int n = 0; n < 1000; n++)
        {
            var chars = new char[random.Next(0, 60)];
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[random.Next(alphabet.Length)];
            string name = HabitProfiles.CleanName(new string(chars));
            if (name == null) continue;
            Assert.DoesNotContain("—", name);
            Assert.True(name.Length <= HabitProfiles.MaxNameLength);
            Assert.Equal(name.Trim(), name);
            Assert.DoesNotContain("  ", name);
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(2, 2)]
    [InlineData(99, 99)]
    [InlineData(100, null)]
    [InlineData(-3, null)]
    public void A_target_is_two_to_ninety_nine_or_none(int? raw, int? expected)
    {
        Assert.Equal(expected, HabitProfiles.CleanTarget(raw));
    }

    [Fact]
    public void Nine_live_habits_is_the_limit_and_archived_ones_do_not_count()
    {
        var habits = new List<HabitDef>();
        for (int i = 0; i < HabitProfiles.MaxLive; i++) habits.Add(new HabitDef { Id = "h" + i });
        Assert.False(HabitProfiles.CanAdd(habits));
        habits[0].ArchivedDay = 10;
        Assert.True(HabitProfiles.CanAdd(habits));
        Assert.Equal(8, HabitProfiles.LiveCount(habits));
        Assert.True(HabitProfiles.CanAdd(null));
    }

    [Fact]
    public void Without_the_profile_choice_a_row_is_written_exactly_as_before()
    {
        var habit = new HabitDef { Id = "a", Name = "Call mum", Zone = LifeZone.Connection, Schedule = HabitSchedule.PerWeek(2), Target = 3, Unit = "calls" };
        Assert.Equal("{\"Id\":\"a\",\"PresetKey\":\"\",\"Group\":\"\",\"Species\":null,\"Kind\":\"InAppRitual\",\"Slot\":0,\"Row\":0,\"CreatedDay\":0,\"ArchivedDay\":null}",
            Json.Write(LedgerJson.WriteHabit(habit, 0, null, With(true, false))));
        Assert.DoesNotContain("Name", LedgerJson.HabitKnown(With(true, false)));
        Assert.DoesNotContain("Name", LedgerJson.HabitKnown(With(false, false)));
    }

    [Fact]
    public void Without_the_profile_choice_profile_members_pass_through_untouched()
    {
        string row = "{\"Id\":\"a\",\"PresetKey\":\"\",\"Group\":\"\",\"Species\":null,\"Kind\":\"LifeCheckIn\",\"Slot\":0,\"Row\":0,\"CreatedDay\":3,\"ArchivedDay\":null,"
            + "\"Name\":\"Call mum\",\"Zone\":\"Connection\",\"Schedule\":{\"Kind\":\"TimesPerWeek\",\"Times\":2}}";
        var extras = NewExtras();
        HabitDef habit = LedgerJson.ReadHabit(Json.ParseObject(row), 0, extras, With(true, false));
        Assert.Null(habit.Name);
        Assert.Null(habit.Zone);
        Assert.Equal(row, Json.Write(LedgerJson.WriteHabit(habit, 0, extras, With(true, false))));
    }

    [Fact]
    public void With_the_profile_choice_a_habit_with_no_profile_is_written_exactly_as_before()
    {
        var habit = new HabitDef { Id = "a" };
        Assert.Equal(Json.Write(LedgerJson.WriteHabit(habit, 0, null, With(false, false))),
            Json.Write(LedgerJson.WriteHabit(habit, 0, null, With(false, true))));
        Assert.Equal(Json.Write(LedgerJson.WriteHabit(habit, 0, null, With(true, false))),
            Json.Write(LedgerJson.WriteHabit(habit, 0, null, With(true, true))));
    }

    [Fact]
    public void With_the_profile_choice_a_full_profile_round_trips()
    {
        var choices = With(true, true);
        var habit = new HabitDef
        {
            Id = "h1", PresetKey = "water", Kind = HabitKind.LifeCheckIn, CreatedDay = 9000,
            Name = "Water", Zone = LifeZone.Body, Schedule = HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek), Target = 8, Unit = "glasses"
        };
        string text = Json.Write(LedgerJson.WriteHabit(habit, 0, null, choices));
        Assert.EndsWith(",\"ArchivedDay\":null,\"Name\":\"Water\",\"Zone\":\"Body\",\"Schedule\":{\"Kind\":\"Weekdays\",\"Days\":31},\"Target\":8,\"Unit\":\"glasses\"}", text);

        var extras = NewExtras();
        HabitDef back = LedgerJson.ReadHabit(Json.ParseObject(text), 0, extras, choices);
        Assert.Empty(extras);
        Assert.Equal("Water", back.Name);
        Assert.Equal(LifeZone.Body, back.Zone);
        Assert.Equal(ScheduleKind.Weekdays, back.Schedule.Kind);
        Assert.Equal(HabitSchedule.WorkWeek, back.Schedule.WeekdayMask);
        Assert.Equal(8, back.Target);
        Assert.Equal("glasses", back.Unit);
        Assert.Equal(text, Json.Write(LedgerJson.WriteHabit(back, 0, extras, choices)));
    }

    [Fact]
    public void A_times_a_week_schedule_round_trips()
    {
        var choices = With(false, true);
        var habit = new HabitDef { Id = "h", Schedule = HabitSchedule.PerWeek(3) };
        string text = Json.Write(LedgerJson.WriteHabit(habit, 0, null, choices));
        Assert.Contains("\"Schedule\":{\"Kind\":\"TimesPerWeek\",\"Times\":3}", text);
        HabitDef back = LedgerJson.ReadHabit(Json.ParseObject(text), 0, NewExtras(), choices);
        Assert.Equal(3, back.Schedule.TimesPerWeek);
    }

    [Theory]
    [InlineData("\"Zone\":\"Garage\"")]
    [InlineData("\"Zone\":7")]
    [InlineData("\"Schedule\":{\"Kind\":\"Fortnightly\"}")]
    [InlineData("\"Schedule\":{\"Kind\":\"TimesPerWeek\",\"Times\":12}")]
    [InlineData("\"Schedule\":{\"Kind\":\"Weekdays\",\"Days\":0}")]
    [InlineData("\"Schedule\":\"Daily\"")]
    [InlineData("\"Target\":1")]
    [InlineData("\"Target\":2.5")]
    [InlineData("\"Target\":\"8\"")]
    [InlineData("\"Name\":\"   \"")]
    public void An_unreadable_profile_member_leaves_its_field_unset_and_the_rest_of_the_row_reads(string member)
    {
        string row = "{\"Id\":\"a\",\"PresetKey\":\"read\",\"Kind\":\"LifeCheckIn\",\"CreatedDay\":5," + member + "}";
        HabitDef habit = LedgerJson.ReadHabit(Json.ParseObject(row), 0, NewExtras(), With(false, true));
        Assert.Equal("a", habit.Id);
        Assert.Equal(5, habit.CreatedDay);
        Assert.Equal(LifeZone.Mind, HabitProfiles.ZoneOf(habit));
        Assert.Equal(ScheduleKind.Daily, HabitProfiles.ScheduleOf(habit).Kind);
        Assert.Null(habit.Target);
        Assert.Null(habit.Name);
    }

    [Fact]
    public void A_unit_without_a_target_is_dropped()
    {
        string row = "{\"Id\":\"a\",\"Unit\":\"glasses\"}";
        HabitDef habit = LedgerJson.ReadHabit(Json.ParseObject(row), 0, NewExtras(), With(false, true));
        Assert.Null(habit.Unit);
        habit.Unit = "glasses";
        Assert.DoesNotContain("Unit", Json.Write(LedgerJson.WriteHabit(habit, 0, null, With(false, true))));
    }

    [Fact]
    public void An_unknown_member_still_passes_through_with_the_profile_choice()
    {
        string row = "{\"Id\":\"a\",\"PresetKey\":\"\",\"Group\":null,\"Species\":null,\"Kind\":\"LifeCheckIn\",\"Slot\":0,\"CreatedDay\":0,\"ArchivedDay\":null,\"Name\":\"Run\",\"Mood\":\"sunny\"}";
        var extras = NewExtras();
        HabitDef habit = LedgerJson.ReadHabit(Json.ParseObject(row), 0, extras, With(false, true));
        Assert.Equal(row, Json.Write(LedgerJson.WriteHabit(habit, 0, extras, With(false, true))));
    }
}

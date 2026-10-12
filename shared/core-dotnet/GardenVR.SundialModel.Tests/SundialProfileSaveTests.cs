using System.Collections.Generic;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

// SundialSave.cs RowChoices.ProfileKnown (decision 0016, upgrade C11): a habit's own name, zone, schedule and target
// survive the Sundial codec. Saves without them are pinned byte for byte by SundialCodecGoldenTests.
public class SundialProfileSaveTests
{
    [Fact]
    public void A_habit_profile_round_trips_through_the_sundial_codec()
    {
        var save = new SundialSave
        {
            SchemaVersion = 1,
            Settings = new SundialSettings(),
            Habits = new List<HabitDef>
            {
                new HabitDef
                {
                    Id = "water", PresetKey = "water", Group = "morning", Kind = HabitKind.LifeCheckIn, Slot = 0, Row = 1, CreatedDay = 9780,
                    Name = "Water", Zone = LifeZone.Body, Schedule = HabitSchedule.OnWeekdays(HabitSchedule.WorkWeek), Target = 8, Unit = "glasses"
                },
                new HabitDef { Id = "call", Group = "wind-down", Kind = HabitKind.LifeCheckIn, Slot = 2, CreatedDay = 9781, Name = "Call home", Zone = LifeZone.Connection, Schedule = HabitSchedule.PerWeek(2) }
            },
            Tends = new List<TendEvent>()
        };

        string text = Json.Write(SundialCodec.Write(save));
        SundialSave back = SundialCodec.Read(Json.ParseObject(text));

        Assert.Equal("Water", back.Habits[0].Name);
        Assert.Equal(LifeZone.Body, back.Habits[0].Zone);
        Assert.Equal(HabitSchedule.WorkWeek, back.Habits[0].Schedule.WeekdayMask);
        Assert.Equal(8, back.Habits[0].Target);
        Assert.Equal("glasses", back.Habits[0].Unit);
        Assert.Equal(1, back.Habits[0].Row);
        Assert.Equal(2, back.Habits[1].Schedule.TimesPerWeek);
        Assert.Equal(LifeZone.Connection, back.Habits[1].Zone);
        Assert.Equal(text, Json.Write(SundialCodec.Write(back)));
    }

    [Fact]
    public void A_preset_habit_without_a_profile_writes_no_profile_members()
    {
        var save = new SundialSave
        {
            SchemaVersion = 1,
            Settings = new SundialSettings(),
            Habits = new List<HabitDef> { new HabitDef { Id = "water", PresetKey = "water", Group = "morning", Kind = HabitKind.LifeCheckIn, CreatedDay = 9780 } },
            Tends = new List<TendEvent>()
        };
        string text = Json.Write(SundialCodec.Write(save));
        foreach (string member in new[] { "\"Name\"", "\"Zone\"", "\"Schedule\"", "\"Target\"", "\"Unit\"" })
            Assert.DoesNotContain(member, text);
    }
}

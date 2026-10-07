using System.Collections.Generic;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

// SundialSave.cs: the codec Read and Write pair (lines 68-134).
public class SundialSaveTests
{
    [Fact]
    public void A_full_save_round_trips_through_the_codec_text()
    {
        var save = new SundialSave
        {
            SchemaVersion = 1,
            FirstRunStep = FirstRunSteps.Drop,
            Settings = new SundialSettings { BoundaryMin = 240, MorningMin = 390, MiddayMin = 690, DuskMin = 1080, Voice = true, Mute = true, Boil = false },
            Habits = new List<HabitDef>
            {
                new HabitDef { Id = "water", PresetKey = "water", Group = "morning", Species = "fern", Kind = HabitKind.LifeCheckIn, Slot = 0, Row = 0, CreatedDay = 9780 },
                new HabitDef { Id = "breaths", PresetKey = "breaths", Group = "wind-down", Kind = HabitKind.InAppRitual, Slot = 2, CreatedDay = 9781, ArchivedDay = 9790 }
            },
            Tends = new List<TendEvent>
            {
                new TendEvent { Id = "t1", HabitId = "water", Tz = "UTC", Day = 9781, AtUtcMs = 1760000000000L, Source = TendSource.Ritual },
                new TendEvent { Id = "t2", HabitId = "water", Tz = "UTC", Day = 9782, AtUtcMs = 1760086400000L, Source = TendSource.Backfill, Late = true, UndoneAtUtcMs = 1760090000000L }
            },
            Gratitude = new List<GratitudeMark> { new GratitudeMark { Day = 9781, Symbol = 3 } },
            Focus = new FocusSnapshot { Phase = "Running", StartedUtcMs = 1760000000000L, Arc = "Midday", StartGnomonDeg = 125f, ElapsedSeconds = 60 }
        };

        string text = Json.Write(SundialCodec.Write(save));
        SundialSave back = SundialCodec.Read(Json.ParseObject(text));

        Assert.Equal(1, back.SchemaVersion);
        Assert.Equal(FirstRunSteps.Drop, back.FirstRunStep);
        Assert.Equal(240, back.Settings.BoundaryMin);
        Assert.Equal(390, back.Settings.MorningMin);
        Assert.Equal(690, back.Settings.MiddayMin);
        Assert.Equal(1080, back.Settings.DuskMin);
        Assert.True(back.Settings.Voice);
        Assert.True(back.Settings.Mute);
        Assert.False(back.Settings.Boil);

        Assert.Equal(2, back.Habits.Count);
        Assert.Equal("fern", back.Habits[0].Species);
        Assert.Null(back.Habits[0].ArchivedDay);
        Assert.Equal(HabitKind.InAppRitual, back.Habits[1].Kind);
        Assert.Equal(9790, back.Habits[1].ArchivedDay);

        Assert.Equal(2, back.Tends.Count);
        Assert.Equal(TendSource.Backfill, back.Tends[1].Source);
        Assert.True(back.Tends[1].Late);
        Assert.Equal(1760090000000L, back.Tends[1].UndoneAtUtcMs);
        Assert.Null(back.Tends[0].UndoneAtUtcMs);

        Assert.Equal(9781, back.Gratitude[0].Day);
        Assert.Equal(3, back.Gratitude[0].Symbol);
        Assert.Equal("Running", back.Focus.Phase);
        Assert.Equal(125f, back.Focus.StartGnomonDeg, 3);

        // A second pass writes the same text.
        Assert.Equal(text, Json.Write(SundialCodec.Write(back)));
    }

    [Fact]
    public void Unknown_members_are_kept_and_written_back()
    {
        string text = "{\"SchemaVersion\":1,\"Habits\":[{\"Id\":\"water\",\"Group\":\"morning\",\"Future\":7}],\"Tends\":[],\"Settings\":{},\"Later\":\"x\"}";
        SundialSave save = SundialCodec.Read(Json.ParseObject(text));
        string written = Json.Write(SundialCodec.Write(save));
        Assert.Contains("\"Later\"", written);
        Assert.Contains("\"Future\"", written);
    }

    [Fact]
    public void A_gratitude_row_with_a_bad_symbol_or_a_repeated_day_is_dropped()
    {
        string text = "{\"Gratitude\":[{\"Day\":1,\"Symbol\":2},{\"Day\":1,\"Symbol\":3},{\"Day\":2,\"Symbol\":99}]}";
        SundialSave save = SundialCodec.Read(Json.ParseObject(text));
        Assert.Single(save.Gratitude);
        Assert.Equal(2, save.Gratitude[0].Symbol);
    }
}

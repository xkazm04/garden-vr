using System.IO;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

// Pins the exact text SundialCodec.Write produces for one full document, before the row codec moves to one owner in core.
public class SundialCodecGoldenTests
{
    // Habit a: Id, Row 1, unknown member. Habit b: no Id, null Group. Tend a: Id, unknown member. Tend b: no Id, a Source that
    // cannot be read. An id-less row carries no unknown member here: today it would be lost, and a test below pins that.
    const string Input =
        "{\"SchemaVersion\":1,\"Habits\":["
        + "{\"Id\":\"water\",\"PresetKey\":\"water\",\"Group\":\"morning\",\"Species\":\"fern\",\"Kind\":\"LifeCheckIn\",\"Slot\":1,\"Row\":1,\"CreatedDay\":9780,\"Future\":7},"
        + "{\"PresetKey\":\"walk\",\"Group\":null,\"Kind\":\"InAppRitual\",\"CreatedDay\":9781,\"ArchivedDay\":9790}],"
        + "\"Tends\":["
        + "{\"Id\":\"t1\",\"HabitId\":\"water\",\"Tz\":\"UTC\",\"Day\":9781,\"AtUtcMs\":1760000000000,\"Source\":\"Poke\",\"Late\":true,\"Extra1\":[1,2]},"
        + "{\"HabitId\":\"water\",\"Day\":9782,\"Source\":\"Sideways\"}],"
        + "\"Settings\":{\"BoundaryMin\":240,\"Voice\":true},\"FirstRunStep\":\"drop\","
        + "\"Gratitude\":[{\"Day\":9781,\"Symbol\":3}],"
        + "\"Focus\":{\"Phase\":\"Running\",\"StartedUtcMs\":1760000000000,\"Arc\":\"Midday\",\"StartGnomonDeg\":125,\"ElapsedSeconds\":60},"
        + "\"Top\":true}";

    const string Golden =
        "{\"SchemaVersion\":1,\"Habits\":[{\"Id\":\"water\",\"PresetKey\":\"water\",\"Group\":\"morning\",\"Species\":\"fern\",\"Kind\":\"LifeCheckIn\",\"Slot\":1,\"Row\":1,\"CreatedDay\":9780,\"ArchivedDay\":null,\"Future\":7},{\"Id\":\"\",\"PresetKey\":\"walk\",\"Group\":\"\",\"Species\":null,\"Kind\":\"InAppRitual\",\"Slot\":0,\"Row\":0,\"CreatedDay\":9781,\"ArchivedDay\":9790}],\"Tends\":[{\"Id\":\"t1\",\"HabitId\":\"water\",\"Tz\":\"UTC\",\"Day\":9781,\"AtUtcMs\":1760000000000,\"Source\":\"Poke\",\"Late\":true,\"UndoneAtUtcMs\":null,\"Extra1\":[1,2]},{\"Id\":\"\",\"HabitId\":\"water\",\"Tz\":\"\",\"Day\":9782,\"AtUtcMs\":0,\"Source\":\"Pinch\",\"Late\":false,\"UndoneAtUtcMs\":null}],\"Settings\":{\"BoundaryMin\":240,\"MorningMin\":360,\"MiddayMin\":660,\"DuskMin\":1080,\"Voice\":true,\"Beds\":false,\"ReducedMotion\":false,\"Boil\":true,\"Mute\":false},\"FirstRunStep\":\"drop\",\"Gratitude\":[{\"Day\":9781,\"Symbol\":3}],\"Focus\":{\"Phase\":\"Running\",\"StartedUtcMs\":1760000000000,\"PausedMs\":0,\"PauseUtcMs\":0,\"Arc\":\"Midday\",\"StartGnomonDeg\":125,\"EndedEarly\":false,\"TendPending\":false,\"ElapsedSeconds\":60},\"Top\":true}";

    [Fact]
    public void The_text_written_for_one_full_document_is_pinned()
    {
        SundialSave save = SundialCodec.Read(Json.ParseObject(Input));
        Assert.Equal(Golden, Json.Write(SundialCodec.Write(save)));
    }

    [Fact]
    public void An_unknown_member_of_a_row_with_no_id_is_lost_today()
    {
        string text = "{\"Habits\":[{\"PresetKey\":\"walk\",\"Later\":\"x\"}],\"Tends\":[{\"HabitId\":\"water\",\"Extra2\":\"y\"}]}";
        string written = Json.Write(SundialCodec.Write(SundialCodec.Read(Json.ParseObject(text))));
        Assert.DoesNotContain("Later", written);
        Assert.DoesNotContain("Extra2", written);
    }

    [Fact]
    public void Reading_the_document_gives_the_rows_the_codec_reads_today()
    {
        SundialSave save = SundialCodec.Read(Json.ParseObject(Input));

        Assert.Equal(2, save.Habits.Count);
        Assert.Equal(1, save.Habits[0].Row);
        Assert.Equal("fern", save.Habits[0].Species);
        Assert.Null(save.Habits[1].Id);
        Assert.Null(save.Habits[1].Group);
        Assert.Equal(9790, save.Habits[1].ArchivedDay);

        Assert.Equal(2, save.Tends.Count);
        Assert.Equal(TendSource.Poke, save.Tends[0].Source);
        Assert.Null(save.Tends[1].Id);
        Assert.Equal(TendSource.Pinch, save.Tends[1].Source);

        Assert.Equal(7, save.HabitExtra["water"]["Future"].AsInt());
        Assert.Equal(2, save.TendExtra["t1"]["Extra1"].AsArray().Count);
        Assert.True(save.Extra["Top"].AsBool());
    }
}

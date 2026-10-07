using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Pins the exact text TerrariumSave.ToJson writes for habit and tend rows, before the row codec moves to one owner.
public class RowCodecGoldenTests
{
    // Habit a: Id, unknown member, Row 2 (not a known member here). Habit b: no Id, null Group, unknown member.
    // Tend a: Id, unknown member. Tend b: no Id, unknown member, a Source that cannot be read.
    const string Input =
        "{\"SchemaVersion\":1,\"Habits\":["
        + "{\"Id\":\"water\",\"PresetKey\":\"water\",\"Group\":\"morning\",\"Species\":\"fern\",\"Kind\":\"LifeCheckIn\",\"Slot\":1,\"Row\":2,\"CreatedDay\":9780,\"Future\":7},"
        + "{\"PresetKey\":\"walk\",\"Group\":null,\"Kind\":\"InAppRitual\",\"CreatedDay\":9781,\"ArchivedDay\":9790,\"Later\":\"x\"}],"
        + "\"Tends\":["
        + "{\"Id\":\"t1\",\"HabitId\":\"water\",\"Tz\":\"UTC\",\"Day\":9781,\"AtUtcMs\":1760000000000,\"Source\":\"Poke\",\"Late\":true,\"Extra1\":[1,2]},"
        + "{\"HabitId\":\"water\",\"Day\":9782,\"Source\":\"Sideways\",\"Extra2\":\"y\"}]}";

    const string Golden =
        "{\"SchemaVersion\":1,\"FrondDays\":[],\"DewToday\":0,\"LastRitualDay\":null,\"Returns\":0,\"RitualsCompleted\":0,\"Habits\":[{\"Id\":\"water\",\"PresetKey\":\"water\",\"Group\":\"morning\",\"Species\":\"fern\",\"Kind\":\"LifeCheckIn\",\"Slot\":1,\"CreatedDay\":9780,\"ArchivedDay\":null,\"Row\":2,\"Future\":7},{\"Id\":\"\",\"PresetKey\":\"walk\",\"Group\":null,\"Species\":null,\"Kind\":\"InAppRitual\",\"Slot\":0,\"CreatedDay\":9781,\"ArchivedDay\":9790,\"Later\":\"x\"}],\"Tends\":[{\"Id\":\"t1\",\"HabitId\":\"water\",\"Tz\":\"UTC\",\"Day\":9781,\"AtUtcMs\":1760000000000,\"Source\":\"Poke\",\"Late\":true,\"UndoneAtUtcMs\":null,\"Extra1\":[1,2]},{\"Id\":\"\",\"HabitId\":\"water\",\"Tz\":\"\",\"Day\":9782,\"AtUtcMs\":0,\"Source\":\"Ritual\",\"Late\":false,\"UndoneAtUtcMs\":null,\"Extra2\":\"y\"}],\"FirstRunStep\":null}";

    [Fact]
    public void The_text_written_for_habit_and_tend_rows_is_pinned()
    {
        TerrariumSave save = TerrariumSave.FromJson(Input);
        Assert.Equal(Golden, save.ToJson());
    }

    [Fact]
    public void Reading_the_written_text_back_gives_the_same_rows_and_extras()
    {
        TerrariumSave back = TerrariumSave.FromJson(TerrariumSave.FromJson(Input).ToJson());

        Assert.Equal(2, back.Habits.Count);
        Assert.Equal("water", back.Habits[0].Id);
        Assert.Equal(0, back.Habits[0].Row);
        Assert.Equal("", back.Habits[1].Id);
        Assert.Null(back.Habits[1].Group);
        Assert.Equal(HabitKind.InAppRitual, back.Habits[1].Kind);
        Assert.Equal(9790, back.Habits[1].ArchivedDay);

        Assert.Equal(2, back.Tends.Count);
        Assert.Equal(TendSource.Poke, back.Tends[0].Source);
        Assert.True(back.Tends[0].Late);
        Assert.Equal("", back.Tends[1].Id);
        Assert.Equal(TendSource.Ritual, back.Tends[1].Source);

        Assert.Equal(7, back.HabitExtra["water"]["Future"].AsInt());
        Assert.Equal(2, back.HabitExtra["water"]["Row"].AsInt());
        Assert.Equal("x", back.HabitExtra["#1"]["Later"].AsString());
        Assert.Equal(2, back.TendExtra["t1"]["Extra1"].AsArray().Count);
        Assert.Equal("y", back.TendExtra["#1"]["Extra2"].AsString());
        Assert.Equal(new[] { "Future", "Row" }, Sorted(back.HabitExtra["water"]));
    }

    static string[] Sorted(System.Collections.Generic.Dictionary<string, JsonValue> d)
    {
        var keys = new System.Collections.Generic.List<string>(d.Keys);
        keys.Sort(System.StringComparer.Ordinal);
        return keys.ToArray();
    }
}

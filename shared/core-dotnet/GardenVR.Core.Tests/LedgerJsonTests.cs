using System.Collections.Generic;
using GardenVR.Core;
using Xunit;

// LedgerJson: the habit and tend row codec. Each of its three choices is exercised both ways.
public class LedgerJsonTests
{
    static LedgerJson.Choices Terrarium { get { return new LedgerJson.Choices { NullGroupAsEmpty = false, SourceFallback = TendSource.Ritual, RowKnown = false }; } }
    static LedgerJson.Choices Sundial { get { return new LedgerJson.Choices { NullGroupAsEmpty = true, SourceFallback = TendSource.Pinch, RowKnown = true }; } }

    static Dictionary<string, Dictionary<string, JsonValue>> NewExtras() { return new Dictionary<string, Dictionary<string, JsonValue>>(); }

    [Fact]
    public void A_null_group_is_written_null_or_empty_by_the_choice()
    {
        var habit = new HabitDef { Id = "a" };
        Assert.Equal("{\"Id\":\"a\",\"PresetKey\":\"\",\"Group\":null,\"Species\":null,\"Kind\":\"InAppRitual\",\"Slot\":0,\"CreatedDay\":0,\"ArchivedDay\":null}",
            Json.Write(LedgerJson.WriteHabit(habit, 0, null, Terrarium)));
        Assert.Equal("{\"Id\":\"a\",\"PresetKey\":\"\",\"Group\":\"\",\"Species\":null,\"Kind\":\"InAppRitual\",\"Slot\":0,\"Row\":0,\"CreatedDay\":0,\"ArchivedDay\":null}",
            Json.Write(LedgerJson.WriteHabit(habit, 0, null, Sundial)));
    }

    [Fact]
    public void A_group_that_is_set_is_written_as_it_is_under_either_choice()
    {
        var habit = new HabitDef { Id = "a", Group = "morning" };
        Assert.Contains("\"Group\":\"morning\"", Json.Write(LedgerJson.WriteHabit(habit, 0, null, Terrarium)));
        Assert.Contains("\"Group\":\"morning\"", Json.Write(LedgerJson.WriteHabit(habit, 0, null, Sundial)));
    }

    [Fact]
    public void An_unreadable_source_takes_the_fallback_of_the_choice()
    {
        JsonObject row = Json.ParseObject("{\"Id\":\"t\",\"Source\":\"Sideways\"}");
        Assert.Equal(TendSource.Ritual, LedgerJson.ReadTend(row, 0, null, Terrarium).Source);
        Assert.Equal(TendSource.Pinch, LedgerJson.ReadTend(row, 0, null, Sundial).Source);
    }

    [Fact]
    public void A_source_that_reads_is_not_replaced_by_the_fallback()
    {
        JsonObject row = Json.ParseObject("{\"Id\":\"t\",\"Source\":\"Backfill\"}");
        Assert.Equal(TendSource.Backfill, LedgerJson.ReadTend(row, 0, null, Terrarium).Source);
        Assert.Equal(TendSource.Backfill, LedgerJson.ReadTend(row, 0, null, Sundial).Source);
    }

    [Fact]
    public void Row_is_an_unknown_member_when_not_known_and_a_field_when_known()
    {
        JsonObject row = Json.ParseObject("{\"Id\":\"h\",\"Row\":2}");

        var extras = NewExtras();
        HabitDef plain = LedgerJson.ReadHabit(row, 0, extras, Terrarium);
        Assert.Equal(0, plain.Row);
        Assert.Equal(2, extras["h"]["Row"].AsInt());
        Assert.Contains("\"Row\":2", Json.Write(LedgerJson.WriteHabit(plain, 0, extras, Terrarium)));

        extras = NewExtras();
        HabitDef known = LedgerJson.ReadHabit(row, 0, extras, Sundial);
        Assert.Equal(2, known.Row);
        Assert.Empty(extras);
        Assert.Contains("\"Row\":2", Json.Write(LedgerJson.WriteHabit(known, 0, extras, Sundial)));
    }

    [Fact]
    public void The_known_sets_follow_the_row_choice_and_cannot_be_edited_through_the_returned_array()
    {
        Assert.DoesNotContain("Row", LedgerJson.HabitKnown(Terrarium));
        Assert.Contains("Row", LedgerJson.HabitKnown(Sundial));
        Assert.Equal(8, LedgerJson.TendKnown().Length);
        LedgerJson.TendKnown()[0] = "X";
        Assert.Equal("Id", LedgerJson.TendKnown()[0]);
    }

    [Fact]
    public void A_row_with_no_id_keeps_its_unknown_members_under_the_index_key()
    {
        var extras = NewExtras();
        HabitDef habit = LedgerJson.ReadHabit(Json.ParseObject("{\"Later\":\"x\"}"), 3, extras, Sundial);
        Assert.Equal("x", extras["#3"]["Later"].AsString());
        Assert.Contains("\"Later\":\"x\"", Json.Write(LedgerJson.WriteHabit(habit, 3, extras, Sundial)));
        Assert.DoesNotContain("Later", Json.Write(LedgerJson.WriteHabit(habit, 4, extras, Sundial)));

        var tendExtras = NewExtras();
        TendEvent tend = LedgerJson.ReadTend(Json.ParseObject("{\"Later\":1}"), 0, tendExtras, Terrarium);
        Assert.Contains("\"Later\":1", Json.Write(LedgerJson.WriteTend(tend, 0, tendExtras)));
    }

    [Fact]
    public void A_row_with_an_id_keeps_its_unknown_members_under_the_id_and_a_row_with_none_keeps_nothing()
    {
        var extras = NewExtras();
        LedgerJson.ReadHabit(Json.ParseObject("{\"Id\":\"h\",\"Future\":7}"), 5, extras, Terrarium);
        LedgerJson.ReadHabit(Json.ParseObject("{\"Id\":\"g\"}"), 6, extras, Terrarium);
        Assert.Single(extras);
        Assert.Equal(7, extras["h"]["Future"].AsInt());
        Assert.Equal("h", LedgerJson.RowKey("h", 5));
        Assert.Equal("#5", LedgerJson.RowKey(null, 5));
        Assert.Equal("#5", LedgerJson.RowKey("", 5));
    }

    [Fact]
    public void A_null_extras_dictionary_keeps_and_restores_nothing_and_does_not_throw()
    {
        HabitDef habit = LedgerJson.ReadHabit(Json.ParseObject("{\"Id\":\"h\",\"Future\":7}"), 0, null, Terrarium);
        Assert.DoesNotContain("Future", Json.Write(LedgerJson.WriteHabit(habit, 0, null, Terrarium)));
    }

    [Fact]
    public void String_and_enum_members_read_absent_and_null_as_the_default()
    {
        JsonObject row = Json.ParseObject("{\"A\":\"x\",\"B\":null,\"K\":\"Backfill\",\"Bad\":\"nope\"}");
        Assert.Equal("x", LedgerJson.StringMember(row, "A"));
        Assert.Null(LedgerJson.StringMember(row, "B"));
        Assert.Null(LedgerJson.StringMember(row, "Missing"));
        Assert.Equal(TendSource.Backfill, LedgerJson.EnumMember(row, "K", TendSource.Pinch));
        Assert.Equal(TendSource.Poke, LedgerJson.EnumMember(row, "Bad", TendSource.Poke));
        Assert.Equal(TendSource.Poke, LedgerJson.EnumMember(row, "B", TendSource.Poke));
        Assert.Equal(TendSource.Poke, LedgerJson.EnumMember(row, "Missing", TendSource.Poke));
    }
}

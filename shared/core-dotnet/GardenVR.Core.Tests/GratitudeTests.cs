using System.Reflection;
using GardenVR.Core;
using Xunit;

public class GratitudeTests
{
    [Fact]
    public void Five_symbols_and_the_mark_stores_only_a_day_and_an_index()
    {
        Assert.Equal(5, GratitudeRecord.SymbolCount);
        Assert.Equal(GratitudeRecord.SymbolCount, GratitudeRecord.Keys.Length);
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < GratitudeRecord.Keys.Length; i++)
        {
            Assert.False(string.IsNullOrEmpty(GratitudeRecord.Keys[i]));
            Assert.Equal(i, GratitudeRecord.ParseKey(GratitudeRecord.Keys[i]));
            Assert.True(seen.Add(GratitudeRecord.Keys[i]));
        }
        Assert.Equal(-1, GratitudeRecord.ParseKey("note"));
        Assert.Equal(-1, GratitudeRecord.ParseKey(null));

        FieldInfo[] fields = typeof(GratitudeMark).GetFields();
        Assert.Equal(2, fields.Length);
        Assert.Contains(fields, field => field.Name == "Day" && field.FieldType == typeof(int));
        Assert.Contains(fields, field => field.Name == "Symbol" && field.FieldType == typeof(int));
        Assert.Empty(typeof(GratitudeMark).GetProperties());
    }

    [Fact]
    public void The_first_symbol_inks_the_day_and_authorises_one_midday_tend()
    {
        var record = new GratitudeRecord();
        Assert.Equal(ArcId.Midday, record.TendsArc);
        Assert.Equal(TendSource.Ritual, record.TendsAs);
        Assert.False(record.ConsumeTend());
        Assert.False(record.TryInk(10, -1));
        Assert.False(record.TryInk(10, 5));
        Assert.Empty(record.Marks);

        Assert.True(record.TryInk(10, GratitudeRecord.ParseKey("leaf")));
        Assert.True(record.Done(10));
        Assert.Equal(1, record.SymbolOn(10));
        Assert.Equal(1, record.Marks.Count);
        Assert.Equal(10, record.Marks[0].Day);
        Assert.Equal(1, record.Marks[0].Symbol);
        Assert.True(record.TendAuthorised);
        Assert.True(record.ConsumeTend());
        Assert.False(record.TendAuthorised);
        Assert.False(record.ConsumeTend());
    }

    [Fact]
    public void A_second_symbol_the_same_day_does_not_replace_the_first()
    {
        var record = new GratitudeRecord();
        Assert.True(record.TryInk(4, 0));
        Assert.True(record.ConsumeTend());

        Assert.False(record.TryInk(4, 3));
        Assert.False(record.TryInk(4, 0));
        Assert.Equal(0, record.SymbolOn(4));
        Assert.Equal(1, record.Marks.Count);
        Assert.False(record.TendAuthorised);
        Assert.False(record.ConsumeTend());
    }

    [Fact]
    public void Another_day_can_take_another_symbol()
    {
        var record = new GratitudeRecord();
        Assert.True(record.TryInk(4, 2));
        Assert.True(record.TryInk(5, 4));
        Assert.Equal(2, record.SymbolOn(4));
        Assert.Equal(4, record.SymbolOn(5));
        Assert.Equal(-1, record.SymbolOn(6));
        Assert.True(record.ConsumeTend());
        Assert.True(record.ConsumeTend());
        Assert.False(record.ConsumeTend());
    }

    [Fact]
    public void Marks_loaded_from_a_save_do_not_authorise_a_tend()
    {
        var loaded = new GratitudeRecord(new[]
        {
            new GratitudeMark { Day = 3, Symbol = 2 },
            new GratitudeMark { Day = 3, Symbol = 4 },
            new GratitudeMark { Day = 8, Symbol = 9 },
            null,
            new GratitudeMark { Day = 8, Symbol = 0 }
        });

        Assert.Equal(2, loaded.Marks.Count);
        Assert.Equal(2, loaded.SymbolOn(3));
        Assert.Equal(0, loaded.SymbolOn(8));
        Assert.False(loaded.TendAuthorised);
        Assert.False(loaded.ConsumeTend());
        Assert.False(new GratitudeRecord(null).Done(0));
    }
}

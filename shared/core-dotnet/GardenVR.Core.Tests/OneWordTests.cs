using System;
using System.Collections.Generic;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class OneWordTests
{
    const int Day0 = 9770;

    [Fact]
    public void The_six_stones_are_the_evening_words()
    {
        Assert.Equal(new[] { "calm", "tired", "glad", "full", "quiet", "light" }, OneWord.Stones);
        Assert.Equal(6, OneWord.Count);
        for (int i = 0; i < OneWord.Stones.Length; i++)
        {
            Assert.DoesNotContain("\u2014", OneWord.Stones[i]);
            Assert.Equal(i, OneWord.IndexOf(OneWord.Stones[i]));
        }
        Assert.Equal("quiet", OneWord.Canonical("Quiet"));
        Assert.Null(OneWord.Canonical("fine"));
        Assert.Equal(-1, OneWord.IndexOf(null));
    }

    [Fact]
    public void One_pinch_keeps_that_days_word_and_does_not_grow()
    {
        var garden = new Garden();
        var stones = new OneWord();
        var words = new List<KeptWord>();
        Assert.False(stones.TryPick(-1));
        Assert.False(stones.TryPick(6));
        Assert.True(stones.TryPick(4));
        Assert.False(stones.TryPick(4));
        Assert.False(stones.TryPick(0));
        Assert.Equal("quiet", stones.Word);
        Assert.False(stones.TryKeep(garden, Day0, words));
        Assert.Empty(words);
        Assert.Equal(0, garden.Fronds);
        Assert.Equal(0, garden.RitualsCompleted);

        garden.CompleteRitual(Day0);
        int fronds = garden.Fronds;
        int rituals = garden.RitualsCompleted;
        Assert.True(stones.TryKeep(garden, Day0, words));
        Assert.Equal("quiet", OneWord.On(words, Day0));
        Assert.Null(OneWord.On(words, Day0 + 1));
        Assert.Equal(fronds, garden.Fronds);
        Assert.Equal(rituals, garden.RitualsCompleted);
        Assert.Equal(0, garden.DewToday);

        var again = new OneWord();
        Assert.True(again.TryPick(1));
        Assert.False(again.TryKeep(garden, Day0, words));
        Assert.Equal("quiet", OneWord.On(words, Day0));
        Assert.Single(words);
        Assert.Equal(fronds, garden.Fronds);
    }

    [Fact]
    public void A_new_day_can_keep_another_word_and_a_clock_set_back_is_refused()
    {
        var garden = new Garden();
        garden.CompleteRitual(Day0);
        var words = new List<KeptWord>();
        var first = new OneWord();
        first.TryPick(0);
        Assert.True(first.TryKeep(garden, Day0, words));

        garden.CompleteRitual(Day0 + 1);
        var next = new OneWord();
        next.TryPick(5);
        Assert.True(next.TryKeep(garden, Day0 + 1, words));
        Assert.Equal("calm", OneWord.On(words, Day0));
        Assert.Equal("light", OneWord.On(words, Day0 + 1));
        Assert.Equal(2, garden.Fronds);

        var back = new OneWord();
        back.TryPick(2);
        Assert.Throws<ArgumentException>(() => back.TryKeep(garden, Day0 - 1, words));
        Assert.Equal(2, words.Count);
        Assert.Equal(2, garden.Fronds);
        Assert.Null(OneWord.On(words, Day0 - 1));
    }

    [Fact]
    public void Look_back_carries_the_word_and_a_miss_stays_blank()
    {
        var garden = new Garden();
        for (int day = 0; day <= 6; day++)
        {
            if (day == 5) continue;
            garden.CompleteRitual(Day0 + day);
        }
        var words = new List<KeptWord>
        {
            new KeptWord { Day = Day0, Word = "calm" },
            new KeptWord { Day = Day0 + 2, Word = "glad" },
            new KeptWord { Day = Day0 + 6, Word = "light" }
        };

        LookFrame[] frames = LookBack.Week(garden, Day0 + 6, words);
        Assert.Equal(7, frames.Length);
        Assert.Equal("calm", frames[0].Word);
        Assert.Null(frames[1].Word);
        Assert.Equal("glad", frames[2].Word);
        Assert.False(frames[5].Kept);
        Assert.Null(frames[5].Word);
        Assert.Equal("light", frames[6].Word);
        Assert.Equal(6, frames[6].Lit);

        LookFrame[] plain = LookBack.Week(garden, Day0 + 6);
        Assert.Equal(7, plain.Length);
        Assert.Null(plain[0].Word);
        Assert.Null(plain[6].Word);
    }

    [Fact]
    public void The_save_keeps_the_words_and_nothing_when_the_week_is_blank()
    {
        var save = new TerrariumSave();
        Assert.Empty(save.DayWords);
        Assert.DoesNotContain("DayWords", save.ToJson());

        save.DayWords.Add(new KeptWord { Day = Day0, Word = "full" });
        save.DayWords.Add(new KeptWord { Day = Day0 + 1, Word = "tired" });
        string json = save.ToJson();
        Assert.Contains("\"DayWords\":[{\"Day\":9770,\"Word\":\"full\"},{\"Day\":9771,\"Word\":\"tired\"}]", json);
        Assert.DoesNotContain("score", json);
        Assert.DoesNotContain("mood", json);
        Assert.DoesNotContain("\u2014", json);

        TerrariumSave again = TerrariumSave.FromJson(json);
        Assert.Equal(2, again.DayWords.Count);
        Assert.Equal("full", again.DayWords[0].Word);
        Assert.Equal(Day0 + 1, again.DayWords[1].Day);
        Assert.Equal(json, again.ToJson());

        again.DayWords.Clear();
        Assert.DoesNotContain("DayWords", again.ToJson());
    }

    [Fact]
    public void A_loaded_list_drops_an_unknown_word_and_a_second_line_for_the_same_day()
    {
        const string json =
            "{\"SchemaVersion\":1,\"FrondDays\":[],\"DewToday\":0,\"LastRitualDay\":null,\"Returns\":0,\"RitualsCompleted\":0," +
            "\"Habits\":[],\"Tends\":[],\"FirstRunStep\":null," +
            "\"DayWords\":[{\"Day\":9770,\"Word\":\"nope\"},{\"Day\":9770,\"Word\":\"Quiet\"},{\"Day\":9770,\"Word\":\"glad\"},{\"Day\":9772,\"Word\":\"light\"}]}";
        TerrariumSave save = TerrariumSave.FromJson(json);
        Assert.Equal(2, save.DayWords.Count);
        Assert.Equal(9770, save.DayWords[0].Day);
        Assert.Equal("quiet", save.DayWords[0].Word);
        Assert.Equal("light", save.DayWords[1].Word);
        Assert.DoesNotContain("nope", save.ToJson());
        Assert.DoesNotContain("glad", save.ToJson());
    }
}

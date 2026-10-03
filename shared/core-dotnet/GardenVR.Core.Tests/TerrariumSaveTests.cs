using System;
using System.IO;
using GardenVR.Core;
using Xunit;

public class TerrariumSaveTests
{
    [Fact]
    public void Round_trip_keeps_growth_habits_tends_and_changed_settings()
    {
        var save = new TerrariumSave();
        save.FrondDays = new[] { 9770, 9774 };
        save.DewToday = 2;
        save.LastRitualDay = 9774;
        save.Returns = 1;
        save.RitualsCompleted = 4;
        save.FirstRunStep = "tour.hold";
        save.Settings.Breaths = 4;
        save.Settings.VoiceGuide = true;
        save.Settings.HoldMode = "Toggle";
        save.Habits.Add(new HabitDef
        {
            Id = "walk",
            PresetKey = "walk",
            Species = "GlowSprig",
            Kind = HabitKind.LifeCheckIn,
            Slot = 1,
            CreatedDay = 9770
        });
        save.Tends.Add(new TendEvent
        {
            Id = "tend-1",
            HabitId = "walk",
            Tz = "UTC",
            Day = 9770,
            AtUtcMs = 1700000000000L,
            Source = TendSource.Poke
        });

        string json = save.ToJson();
        TerrariumSave again = TerrariumSave.FromJson(json);
        Assert.Equal(json, again.ToJson());
        Assert.Equal(new[] { 9770, 9774 }, again.FrondDays);
        Assert.Equal(2, again.DewToday);
        Assert.Equal(9774, again.LastRitualDay);
        Assert.Equal(1, again.Returns);
        Assert.Equal(4, again.RitualsCompleted);
        Assert.Equal("tour.hold", again.FirstRunStep);
        Assert.Equal(4, again.Settings.Breaths);
        Assert.True(again.Settings.VoiceGuide);
        Assert.False(again.Settings.AutoPace);
        Assert.Equal("Toggle", again.Settings.HoldMode);
        Assert.Equal("walk", again.Habits[0].Id);
        Assert.Equal("GlowSprig", again.Habits[0].Species);
        Assert.Equal(HabitKind.LifeCheckIn, again.Habits[0].Kind);
        Assert.Equal("tend-1", again.Tends[0].Id);
        Assert.Equal(1700000000000L, again.Tends[0].AtUtcMs);
        Assert.Contains("\"Breaths\":4", json);
        Assert.Contains("\"VoiceGuide\":true", json);
        Assert.DoesNotContain("AutoPace", json);
        Assert.DoesNotContain("NightBed", json);
    }

    [Fact]
    public void Unknown_members_are_preserved()
    {
        const string text =
            "{\"SchemaVersion\":1,\"FrondDays\":[3],\"DewToday\":0,\"LastRitualDay\":3,\"Returns\":0,\"RitualsCompleted\":1," +
            "\"Habits\":[{\"Id\":\"h\",\"PresetKey\":\"read\",\"Group\":null,\"Species\":\"MoonMoss\",\"Kind\":\"LifeCheckIn\",\"Slot\":0,\"CreatedDay\":3,\"ArchivedDay\":null,\"Tint\":\"mint\"}]," +
            "\"Tends\":[{\"Id\":\"t\",\"HabitId\":\"h\",\"Tz\":\"UTC\",\"Day\":3,\"AtUtcMs\":9,\"Source\":\"Ritual\",\"Late\":false,\"UndoneAtUtcMs\":null,\"Device\":\"desk\"}]," +
            "\"Settings\":{\"PaceName\":\"slow\"},\"FirstRunStep\":null,\"OwnerNote\":\"kept\"}";

        TerrariumSave save = TerrariumSave.FromJson(text);
        string written = save.ToJson();
        TerrariumSave again = TerrariumSave.FromJson(written);
        Assert.Equal(written, again.ToJson());
        Assert.Contains("\"OwnerNote\":\"kept\"", written);
        Assert.Contains("\"Tint\":\"mint\"", written);
        Assert.Contains("\"Device\":\"desk\"", written);
        Assert.Contains("\"PaceName\":\"slow\"", written);
        Assert.Equal("mint", save.HabitExtra["h"]["Tint"].AsString());
        Assert.Equal("kept", save.Extra["OwnerNote"].AsString());
    }

    [Fact]
    public void Default_settings_are_not_written()
    {
        string json = new TerrariumSave().ToJson();
        Assert.DoesNotContain("Breaths", json);
        Assert.DoesNotContain("InhaleSec", json);
        Assert.DoesNotContain("ExhaleSec", json);
        Assert.DoesNotContain("AutoPace", json);
        Assert.DoesNotContain("VoiceGuide", json);
        Assert.DoesNotContain("NightBed", json);
        Assert.DoesNotContain("ReducedMotion", json);
        Assert.DoesNotContain("HoldMode", json);
        Assert.DoesNotContain("Settings", json);

        var changed = new TerrariumSave();
        changed.Settings.ExhaleSec = 7d;
        string withOne = changed.ToJson();
        Assert.Contains("\"ExhaleSec\":7", withOne);
        Assert.DoesNotContain("Breaths", withOne);
        Assert.DoesNotContain("HoldMode", withOne);
    }

    [Fact]
    public void Newer_schema_opens_read_only_and_refuses_the_write()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gvr-ter-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const string text = "{\"SchemaVersion\":2,\"FrondDays\":[5],\"DewToday\":1,\"LastRitualDay\":5,\"Returns\":0,\"RitualsCompleted\":2,\"Habits\":[],\"Tends\":[],\"FutureField\":true}";
            string live = Path.Combine(dir, SaveStore<TerrariumSave>.LiveName);
            File.WriteAllText(live, text);
            SaveStore<TerrariumSave> store = Store(dir);
            LoadResult<TerrariumSave> loaded = store.Load();
            Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
            Assert.True(loaded.ReadOnly);
            Assert.False(loaded.Doc == null);
            Assert.Equal(2, loaded.Doc.SchemaVersion);
            Assert.Equal(5, loaded.Doc.FrondDays[0]);
            Assert.Equal(1, loaded.Doc.DewToday);
            Assert.True(loaded.Doc.Extra.ContainsKey("FutureField"));
            Assert.Throws<InvalidOperationException>(() => store.Save(loaded.Doc));
            Assert.Equal(text, File.ReadAllText(live));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Store_round_trips_v1_and_rotates_the_previous_file()
    {
        string dir = Path.Combine(Path.GetTempPath(), "gvr-ter-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            SaveStore<TerrariumSave> store = Store(dir);
            LoadResult<TerrariumSave> fresh = store.Load();
            Assert.Equal(LoadOutcome.Fresh, fresh.Outcome);
            Assert.False(fresh.ReadOnly);
            fresh.Doc.FrondDays = new[] { 11 };
            fresh.Doc.LastRitualDay = 11;
            fresh.Doc.RitualsCompleted = 1;
            store.Save(fresh.Doc);

            var second = TerrariumSave.FromJson(fresh.Doc.ToJson());
            second.FrondDays = new[] { 11, 12 };
            second.LastRitualDay = 12;
            second.RitualsCompleted = 2;
            store.Save(second);

            string live = File.ReadAllText(Path.Combine(dir, SaveStore<TerrariumSave>.LiveName));
            string prev = File.ReadAllText(Path.Combine(dir, SaveStore<TerrariumSave>.Prev1Name));
            Assert.Contains("\"FrondDays\":[11,12]", live);
            Assert.Contains("\"FrondDays\":[11]", prev);
            Assert.DoesNotContain("Settings", live);

            SaveStore<TerrariumSave> again = Store(dir);
            LoadResult<TerrariumSave> loaded = again.Load();
            Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
            Assert.False(loaded.ReadOnly);
            Assert.Equal(new[] { 11, 12 }, loaded.Doc.FrondDays);
            Assert.Empty(TerrariumSaveMigrations.Steps);
            Assert.Equal(1, TerrariumSaveMigrations.CurrentSchema);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Garden_FromSave_ToSave_round_trips_and_still_refuses_a_day_before_the_last()
    {
        var garden = new Garden();
        garden.CompleteRitual(9770);
        garden.CompleteRitual(9770);
        garden.CompleteRitual(9774);
        TerrariumSave save = garden.ToSave();
        Garden copy = Garden.FromSave(save);
        Assert.Equal(garden.Serialize(), copy.Serialize());
        Assert.Equal(garden.Fronds, copy.Fronds);
        Assert.Equal(garden.DewToday, copy.DewToday);
        Assert.Equal(garden.Returns, copy.Returns);
        Assert.Equal(garden.LastRitualDay, copy.LastRitualDay);
        Assert.False(copy.AcceptsDay(9773));
        Assert.Throws<ArgumentException>(() => copy.CompleteRitual(9773));
        Assert.Equal(garden.Fronds, copy.Fronds);
        Assert.NotNull(Garden.FromSave(null));
        Assert.Equal(0, Garden.FromSave(null).Fronds);
    }

    static SaveStore<TerrariumSave> Store(string dir)
    {
        return new SaveStore<TerrariumSave>(
            new DiskSaveIo(dir),
            TerrariumSaveMigrations.CurrentSchema,
            () => new TerrariumSave(),
            TerrariumSave.Read,
            TerrariumSave.Write,
            TerrariumSaveMigrations.Steps);
    }
}

using System;
using System.IO;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

public sealed class SundialReflectionTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "sundial-reflection-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
    [Fact]
    public void Context_contains_only_todays_kept_live_names_including_preset_labels()
    {
        var s = new SundialService(_clock, _dir);
        var water = s.PlantPreset(SeedCatalog.Find("morning", "water"));
        var read = s.PlantProposal(new CoachProposal { Name = "Read outside", Zone = LifeZone.Mind }, "midday");
        s.PlantProposal(new CoachProposal { Name = "Unkept private name", Zone = LifeZone.Mind }, "midday");
        s.TendRitual(water.Id); s.TendRitual(read.Id);
        var context = s.ReflectionToday();
        Assert.Equal(new[] { "Water", "Read outside" }, context.KeptToday);
        Assert.Empty(context.PlannedNotKept); Assert.Null(context.Weekday);
        read.ArchivedDay = s.Today().Index;
        Assert.Equal(new[] { "Water" }, s.ReflectionToday().KeptToday);
        s.TryShiftDay(1); Assert.Empty(s.ReflectionToday().KeptToday);
    }
    [Fact]
    public void Replacement_persists_and_delete_removes_recoverable_journal_without_losing_habits()
    {
        var s = new SundialService(_clock, _dir);
        s.PlantPreset(SeedCatalog.Find("morning", "water"));
        int day = s.Today().Index;
        Assert.True(s.KeepReflection(day, "A quiet day."));
        Assert.True(s.KeepReflection(day, "A light evening."));
        Assert.False(s.KeepReflection(day, "Your streak is growing."));
        Assert.Single(s.Save.Journal.Entries);
        Assert.Equal("A light evening.", new SundialService(_clock, _dir).Save.Journal.On(day));
        File.Copy(Path.Combine(_dir, "save.json"), Path.Combine(_dir, "save.snapshot.json"));
        Assert.True(s.DeleteJournal());
        var reload = new SundialService(_clock, _dir);
        Assert.Empty(reload.Save.Journal.Entries); Assert.Single(reload.Save.Habits);
        foreach (string file in Directory.GetFiles(_dir))
        {
            Assert.DoesNotContain("A quiet day.", File.ReadAllText(file));
            Assert.DoesNotContain("A light evening.", File.ReadAllText(file));
        }
        Assert.False(reload.BackupAvailable);
    }
    [Fact]
    public void Week_is_bounded_sorted_and_does_not_expose_mutable_journal_entries()
    {
        var journal = new CoachJournal();
        foreach (int day in new[] { 20, 13, 14, 21, 17 }) journal.Keep(day, "A quiet day.");
        var week = journal.WeekEnding(20);
        Assert.Equal(new[] { 14, 17, 20 }, week.ConvertAll(e => e.Day));
        week[0].Line = "changed";
        Assert.Equal("A quiet day.", journal.On(14));
    }
}

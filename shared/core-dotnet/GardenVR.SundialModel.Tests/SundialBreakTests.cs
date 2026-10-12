using System;
using System.IO;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

public sealed class SundialBreakTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "sundial-break-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Break_is_admitted_once_in_a_free_arc_and_persists_as_a_body_ritual()
    {
        var s = new SundialService(_clock, _dir);
        for (int i = 0; i < 3; i++) s.PlantProposal(new CoachProposal { Name = "Read", Zone = LifeZone.Mind }, "morning");
        var habit = s.EnsureBreakHabit();
        Assert.Equal("midday", habit.Group);
        Assert.Equal(BreakSession.PresetKey, habit.PresetKey);
        Assert.Equal(LifeZone.Body, HabitProfiles.ZoneOf(habit));
        Assert.Equal(HabitKind.InAppRitual, habit.Kind);
        Assert.Same(habit, s.EnsureBreakHabit());
        Assert.True(s.TendRitual(habit.Id).Ok);
        Assert.True(s.TendRitual(habit.Id).AlreadyKept);
        var reload = new SundialService(_clock, _dir);
        Assert.Equal(habit.Id, reload.EnsureBreakHabit().Id);
        Assert.Equal(4, reload.Save.Habits.Count);
        Assert.Equal(TendSource.Ritual, Assert.Single(reload.Ledger.Events).Source);
    }

    [Fact]
    public void Full_garden_refuses_break_admission_without_changing_the_save()
    {
        var s = new SundialService(_clock, _dir);
        for (int i = 0; i < 9; i++) s.PlantProposal(new CoachProposal { Name = "Read", Zone = LifeZone.Mind }, null);
        string before = File.ReadAllText(Path.Combine(_dir, "save.json"));
        Assert.Null(s.EnsureBreakHabit());
        Assert.Equal(before, File.ReadAllText(Path.Combine(_dir, "save.json")));
    }
}

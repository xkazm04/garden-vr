using System;
using System.IO;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

public class SundialProposalTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "sundial-proposals-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new FixedClock(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Proposal_uses_chosen_arc_and_round_trips_an_owned_schedule()
    {
        var service = new SundialService(_clock, _dir);
        var proposal = new CoachProposal { Name = "  Call\nmum  ", Zone = LifeZone.Connection, Schedule = HabitSchedule.PerWeek(3), Target = 2, Unit = "calls" };
        HabitDef habit = service.PlantProposal(proposal, "wind-down");
        Assert.Equal("winddown", habit.Group);
        Assert.Equal("Call mum", habit.Name);
        Assert.NotSame(proposal.Schedule, habit.Schedule);
        proposal.Schedule.TimesPerWeek = 7;
        var reloaded = new SundialService(_clock, _dir).Save.Habits[0];
        Assert.Equal(habit.Id, reloaded.Id);
        Assert.Equal(LifeZone.Connection, reloaded.Zone);
        Assert.Equal(3, reloaded.Schedule.TimesPerWeek);
        Assert.Equal(2, reloaded.Target);
        Assert.Equal("calls", reloaded.Unit);
        Assert.Null(service.PlantProposal(proposal, "invalid"));
        Assert.Single(service.Save.Habits);
    }

    [Fact]
    public void Presets_keep_their_zone_and_full_arc_refusal_does_not_redirect()
    {
        var service = new SundialService(_clock, _dir);
        var preset = service.PlantPreset(SeedCatalog.Find("morning", "water"));
        Assert.Equal(LifeZone.Body, preset.Zone);
        Assert.Same(preset, service.PlantPreset(SeedCatalog.Find("morning", "water")));
        var p = new CoachProposal { Name = "Read", Zone = LifeZone.Mind };
        Assert.NotNull(service.PlantProposal(p, "morning"));
        Assert.NotNull(service.PlantProposal(p, "morning"));
        string before = File.ReadAllText(Path.Combine(_dir, "save.json"));
        Assert.Null(service.PlantProposal(p, "morning"));
        Assert.Equal(before, File.ReadAllText(Path.Combine(_dir, "save.json")));
        Assert.Equal("midday", service.PlantProposal(p, null).Group);
    }
}

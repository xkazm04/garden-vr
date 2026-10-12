using System;
using System.IO;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

public class SundialQuantityTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "sundial-quantity-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new FixedClock(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Partial_counts_reload_and_target_undo_persists_both_marks()
    {
        var service = new SundialService(_clock, _dir);
        var habit = service.PlantProposal(new CoachProposal { Name = "Water", Zone = LifeZone.Body, Target = 3 }, "morning");
        Assert.Null(service.CountQuantity(habit.Id).Tend);
        service = new SundialService(_clock, _dir);
        Assert.Equal(1, service.Counts.Count(habit.Id, service.Today().Index));
        Assert.False(service.QuantityUndoAvailable);
        _clock.Advance(TimeSpan.FromSeconds(1));
        service.CountQuantity(habit.Id);
        var step = service.CountQuantity(habit.Id);
        Assert.True(step.Tend.Ok);
        Assert.True(service.UndoQuantity());
        Assert.False(service.UndoQuantity());
        service = new SundialService(_clock, _dir);
        Assert.Equal(2, service.Counts.Count(habit.Id, service.Today().Index));
        Assert.False(service.Ledger.IsKept(habit.Id, service.Today().Index));
        Assert.Single(service.Save.Counts, c => c.UndoneAtUtcMs.HasValue);
        Assert.Single(service.Save.Tends, t => t.UndoneAtUtcMs.HasValue);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(service.CountQuantity(habit.Id).Tend.Ok);
        Assert.Single(service.Save.Tends, t => !t.UndoneAtUtcMs.HasValue);
    }

    [Fact]
    public void Quantity_respects_reading_pending_tends_and_expired_undo()
    {
        var service = new SundialService(_clock, _dir);
        var habit = service.PlantProposal(new CoachProposal { Name = "Water", Target = 2 }, "morning");
        service.SetScrubbing(true);
        Assert.Null(service.CountQuantity(habit.Id));
        service.SetScrubbing(false);
        Assert.NotNull(service.CountQuantity(habit.Id));
        Assert.NotNull(service.CountQuantity(habit.Id));
        _clock.Advance(TimeSpan.FromMilliseconds(6001));
        Assert.False(service.QuantityUndoAvailable);
        Assert.False(service.UndoQuantity());
        Assert.True(service.Ledger.IsKept(habit.Id, service.Today().Index));
        Assert.Null(service.CountQuantity("unknown"));
        service.Arm("other");
        Assert.Null(service.CountQuantity(habit.Id));
        Assert.False(service.QuantityUndoAvailable);
    }
}

using System;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

public class VitalityParityTests
{
    // Companions.Vitality says it has the same curve and floor as Garden.Vitality. Both now read
    // Garden.VitalityForGap, and this pins that they agree at every gap.
    [Fact]
    public void A_companion_droops_on_the_same_curve_as_the_garden()
    {
        var clock = new FixedClock(new DateTimeOffset(2100, 1, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        var ledger = new Ledger();
        var kept = new GardenDay(9000);
        Assert.True(ledger.Tend("walk", kept, TendSource.Ritual, clock).Ok);
        var garden = new Garden();
        garden.CompleteRitual(kept.Index);
        for (int gap = 0; gap <= 20; gap++)
        {
            var today = new GardenDay(kept.Index + gap);
            Assert.Equal(garden.Vitality(today.Index), Companions.Vitality(ledger, "walk", today));
        }
        Assert.Equal(Garden.VitalityFloor, Companions.Vitality(ledger, "walk", new GardenDay(kept.Index + 20)));
    }
}

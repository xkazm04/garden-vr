using System;
using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// Each guard names its parameter, so ParamName is set (CA2208). Type stays ArgumentException, not ArgumentNullException.
public class ArgumentGuardParamNameTests
{
    static readonly FixedClock Clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    static void Names(string param, Action act)
    {
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal(param, ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ledger_guards_name_habitId(string habitId)
    {
        var ledger = new Ledger();
        GardenDay day = GardenDay.From(Clock.Now, GardenDay.DefaultBoundary);
        Names("habitId", () => ledger.KeptDays(habitId));
        Names("habitId", () => ledger.KeptDaysInWindow(habitId, day, 7));
        Names("habitId", () => ledger.KeptDaysFrom(habitId, 0));
        Names("habitId", () => ledger.Tend(habitId, day, TendSource.Pinch, Clock));
        Names("habitId", () => ledger.Backfill(habitId, day, day, Clock));
        Names("habitId", () => new DeferredTend(ledger).Arm(habitId, day, TendSource.Pinch, Clock));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DiskSaveIo_names_directory(string directory)
    {
        Names("directory", () => new DiskSaveIo(directory));
    }

    [Fact]
    public void SundialState_ToJson_names_a_null_plant()
    {
        var state = new SundialState();
        state.Plants.Add(null);
        Names("plant", () => state.ToJson());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Companions_LastKeptDay_names_habitId(string habitId)
    {
        Names("habitId", () => Companions.LastKeptDay(new Ledger(), habitId));
    }
}

using System;
using System.IO;
using GardenVR.Core;
using GardenVR.Sundial;
using Xunit;

namespace GardenVR.SundialModel.Tests;

// SundialService guards name their parameter, so ParamName is set (CA2208).
public sealed class SundialServiceGuardTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "gardenvr-sundial-guard-" + Guid.NewGuid().ToString("N"));
    readonly FixedClock _clock = new FixedClock(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    static void Names(string param, Action act)
    {
        var ex = Assert.Throws<ArgumentException>(act);
        Assert.Equal(param, ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void The_constructor_names_directory(string directory)
    {
        Names("directory", () => new SundialService(_clock, directory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TendRitual_and_Arm_name_habitId(string habitId)
    {
        var svc = new SundialService(_clock, _dir);
        Names("habitId", () => svc.TendRitual(habitId));
        Names("habitId", () => svc.Arm(habitId));
    }
}

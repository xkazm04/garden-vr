using System;
using GardenVR.Core;
using Xunit;

public class SettingsNonFiniteTests
{
    // Define already refuses a NaN or infinite default because JSON cannot hold one. Set must hold the same line,
    // or one bad slider value is stored and then every ToJson (every save) throws.

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_non_finite_value_is_refused_and_the_registry_stays_writable(double bad)
    {
        var registry = new SettingsRegistry();
        var volume = registry.Define("volume", 0.5);
        registry.Set(volume, 0.8);

        Assert.Throws<ArgumentException>(() => registry.Set(volume, bad));

        Assert.Equal(0.8, registry.Get(volume));
        Assert.Equal(0.8, registry.ToJson().Get("volume").AsDouble());
    }
}

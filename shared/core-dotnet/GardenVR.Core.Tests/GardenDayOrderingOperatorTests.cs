using GardenVR.Core;
using Xunit;

namespace GardenVR.Core.Tests;

// <= and >= sit beside < and > on GardenDay (CA1036).
public class GardenDayOrderingOperatorTests
{
    [Fact]
    public void FiveAndSix()
    {
        var five = new GardenDay(5);
        var six = new GardenDay(6);
        Assert.True(five <= six);
        Assert.False(six <= five);
        Assert.False(five >= six);
        Assert.True(six >= five);
    }

    [Fact]
    public void EqualDaysAreBothTrue()
    {
        var a = new GardenDay(5);
        var b = new GardenDay(5);
        Assert.True(a <= b);
        Assert.True(a >= b);
    }

    [Fact]
    public void AgreesWithCompareToAndTheStrictOperators()
    {
        for (int i = -2; i <= 2; i++)
        for (int j = -2; j <= 2; j++)
        {
            var a = new GardenDay(i);
            var b = new GardenDay(j);
            Assert.Equal(a.CompareTo(b) <= 0, a <= b);
            Assert.Equal(a.CompareTo(b) >= 0, a >= b);
            Assert.Equal(!(a > b), a <= b);
            Assert.Equal(!(a < b), a >= b);
        }
    }
}

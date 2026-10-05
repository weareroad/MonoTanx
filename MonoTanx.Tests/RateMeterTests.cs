using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class RateMeterTests
{
    [Fact]
    public void ItReadsZeroUntilAWindowHasPassed()
    {
        var meter = new RateMeter(0.5);

        meter.Tick(0.0);
        meter.Tick(0.1);

        Assert.Equal(0.0, meter.PerSecond);
    }

    [Theory]
    [InlineData(60.0)]
    [InlineData(30.0)]
    [InlineData(144.0)]
    public void ItMeasuresASteadyRate(double rate)
    {
        var meter = new RateMeter(0.5);

        for (var i = 0; i <= 3 * rate; i++)
            meter.Tick(i / rate);

        Assert.Equal(rate, meter.PerSecond, 0);
    }

    [Fact]
    public void ItFollowsAChangeOfRate()
    {
        var meter = new RateMeter(0.5);
        for (var i = 0; i <= 120; i++)
            meter.Tick(i / 60.0);
        var time = 2.0;

        for (var i = 0; i < 60; i++)
        {
            time += 1.0 / 20.0;
            meter.Tick(time);
        }

        Assert.Equal(20.0, meter.PerSecond, 0);
    }

    [Fact]
    public void AStallShowsAsALowRate()
    {
        var meter = new RateMeter(0.5);
        meter.Tick(0.0);
        meter.Tick(0.016);

        meter.Tick(2.0);

        Assert.True(meter.PerSecond < 2.0, $"two ticks in two seconds should read about 1 a second, not {meter.PerSecond}");
    }
}

using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class ScreenShakeTests
{
    [Fact]
    public void IsIdleUntilStarted()
    {
        var shake = new ScreenShake();

        shake.Update(0.016f, new Random(1));

        Assert.False(shake.IsActive);
        Assert.Equal(Vector2.Zero, shake.Offset);
    }

    [Fact]
    public void ShakesWithinTheMagnitudeAndStopsAfterTheDuration()
    {
        var shake = new ScreenShake();
        var random = new Random(3);
        shake.Start(0.2f, 2.0f);

        for (var i = 0; i < 12; i++) // 0.2s at 60 steps/s
        {
            shake.Update(1.0f / 60.0f, random);
            Assert.InRange(shake.Offset.X, -2.0f, 2.0f);
            Assert.InRange(shake.Offset.Y, -2.0f, 2.0f);
        }

        shake.Update(0.05f, random);
        Assert.False(shake.IsActive);
        Assert.Equal(Vector2.Zero, shake.Offset);
    }

    [Fact]
    public void StrengthDecaysTowardsTheEnd()
    {
        const float magnitude = 10.0f;
        var shake = new ScreenShake();
        var random = new Random(5);
        shake.Start(1.0f, magnitude);

        shake.Update(1.0f / 60.0f, random);
        var earlyBound = magnitude * (59.0f / 60.0f) * 1.415f;
        Assert.InRange(shake.Offset.Length(), 0.0f, earlyBound);

        for (var i = 0; i < 58; i++)
            shake.Update(1.0f / 60.0f, random);

        var lateBound = magnitude * (1.0f / 60.0f) * 1.415f + 0.001f; // about 1/60 of the strength remains
        Assert.InRange(shake.Offset.Length(), 0.0f, lateBound);
    }

    [Fact]
    public void ReadingTheOffsetRepeatedlyDoesNotChangeIt()
    {
        var shake = new ScreenShake();
        shake.Start(0.5f, 3.0f);
        shake.Update(1.0f / 60.0f, new Random(9));

        var first = shake.Offset;
        for (var i = 0; i < 10; i++)
            Assert.Equal(first, shake.Offset);
    }

    [Fact]
    public void DrawRateDoesNotAffectTheSequenceOfOffsets()
    {
        var once = new ScreenShake();
        var many = new ScreenShake();
        var randomOnce = new Random(11);
        var randomMany = new Random(11);
        once.Start(0.5f, 3.0f);
        many.Start(0.5f, 3.0f);

        for (var i = 0; i < 20; i++)
        {
            once.Update(1.0f / 60.0f, randomOnce);
            many.Update(1.0f / 60.0f, randomMany);
            for (var draw = 0; draw < 5; draw++)
                _ = many.Offset; // extra draws per update

            Assert.Equal(once.Offset, many.Offset);
        }
    }

    [Fact]
    public void JitterIsResampledOnAFixedTimeBaseRegardlessOfUpdateSize()
    {
        // The same elapsed time consumes the same number of random draws at any step size.
        int DrawsFor(int stepsPerSecond)
        {
            var counting = new CountingRandom(21);
            var shake = new ScreenShake();
            shake.Start(1.0f, 3.0f);
            for (var i = 0; i < stepsPerSecond / 2; i++) // half a second
                shake.Update(1.0f / stepsPerSecond, counting);
            return counting.Calls;
        }

        Assert.Equal(DrawsFor(60), DrawsFor(120));
        Assert.Equal(DrawsFor(60), DrawsFor(30));
    }

    [Fact]
    public void OverlappingShakesKeepTheStrongestUntilTheyAllFinish()
    {
        var shake = new ScreenShake();
        var random = new Random(2);
        shake.Start(0.16f, 1.8f);
        shake.Update(0.01f, random);
        shake.Start(0.12f, 1.0f);

        for (var i = 0; i < 50; i++)
        {
            shake.Update(1.0f / 60.0f, random);
            Assert.InRange(shake.Offset.Length(), 0.0f, 1.8f * 1.5f);
        }

        Assert.False(shake.IsActive);
    }

    [Fact]
    public void AFinishedShakeDoesNotLeaveItsMagnitudeBehindForTheNextOne()
    {
        var shake = new ScreenShake();
        var random = new Random(4);
        shake.Start(0.1f, 100.0f);
        for (var i = 0; i < 20; i++) shake.Update(1.0f / 60.0f, random);
        Assert.False(shake.IsActive);

        shake.Start(0.1f, 1.0f);
        shake.Update(1.0f / 60.0f, random);

        Assert.InRange(shake.Offset.X, -1.0f, 1.0f);
        Assert.InRange(shake.Offset.Y, -1.0f, 1.0f);
    }

    private sealed class CountingRandom : Random
    {
        public int Calls { get; private set; }

        public CountingRandom(int seed) : base(seed) { }

        public override double NextDouble()
        {
            Calls++;
            return base.NextDouble();
        }
    }
}

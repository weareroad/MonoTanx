using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class RandomStreamsTests
{
    private static int[] Draw(Random random, int count = 10) =>
        Enumerable.Range(0, count).Select(_ => random.Next()).ToArray();

    [Fact]
    public void ExposesTheMasterSeed()
    {
        Assert.Equal(1234, new RandomStreams(1234).Seed);
    }

    [Fact]
    public void TheSameSeedReproducesEachStream()
    {
        var first = new RandomStreams(99);
        var second = new RandomStreams(99);

        Assert.Equal(Draw(first.Gameplay), Draw(second.Gameplay));
        Assert.Equal(Draw(first.Cosmetic), Draw(second.Cosmetic));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentSequences()
    {
        Assert.NotEqual(Draw(new RandomStreams(1).Gameplay), Draw(new RandomStreams(2).Gameplay));
    }

    [Fact]
    public void GameplayAndCosmeticStreamsAreIndependentOfEachOther()
    {
        var streams = new RandomStreams(7);

        Assert.NotEqual(Draw(new RandomStreams(7).Gameplay), Draw(streams.Cosmetic));
    }

    [Fact]
    public void CosmeticDrawsDoNotChangeTheGameplaySequence()
    {
        var undisturbed = new RandomStreams(7);
        var disturbed = new RandomStreams(7);

        Draw(disturbed.Cosmetic, 1000);

        Assert.Equal(Draw(undisturbed.Gameplay), Draw(disturbed.Gameplay));
    }

    [Fact]
    public void NamedStreamsAreReproducibleAndDistinct()
    {
        var streams = new RandomStreams(7);

        Assert.Equal(Draw(streams.CreateStream("ai")), Draw(new RandomStreams(7).CreateStream("ai")));
        Assert.NotEqual(Draw(streams.CreateStream("ai")), Draw(streams.CreateStream("pickups")));
    }

    [Fact]
    public void NewSeedsAreNonNegative()
    {
        for (var i = 0; i < 20; i++)
            Assert.True(RandomStreams.NewSeed() >= 0);
    }
}

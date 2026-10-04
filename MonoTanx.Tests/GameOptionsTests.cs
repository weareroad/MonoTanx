using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class GameOptionsTests
{
    [Fact]
    public void NoArgumentsLeavesTheSeedUnset()
    {
        Assert.Null(GameOptions.Parse(Array.Empty<string>()).Seed);
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("0", 0)]
    [InlineData("-5", -5)]
    public void ParsesTheSeed(string value, int expected)
    {
        Assert.Equal(expected, GameOptions.Parse(new[] { "--seed", value }).Seed);
    }

    [Theory]
    [InlineData("--seed")]
    [InlineData("--seed", "abc")]
    [InlineData("--seed", "1.5")]
    public void RejectsAMissingOrInvalidSeed(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(args));
    }

    [Fact]
    public void RejectsUnknownOptions()
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--bogus" }));
    }

    [Fact]
    public void DefaultsToFullscreen()
    {
        var options = GameOptions.Parse(Array.Empty<string>());

        Assert.False(options.Windowed);
        Assert.Equal(GameOptions.DefaultScale, options.Scale);
    }

    [Fact]
    public void ParsesWindowedWithTheDefaultScale()
    {
        var options = GameOptions.Parse(new[] { "--windowed" });

        Assert.True(options.Windowed);
        Assert.Equal(2, options.Scale);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("4", 4)]
    public void ParsesTheScale(string value, int expected)
    {
        var options = GameOptions.Parse(new[] { "--windowed", "--scale", value });

        Assert.True(options.Windowed);
        Assert.Equal(expected, options.Scale);
    }

    [Fact]
    public void OptionOrderDoesNotMatter()
    {
        var options = GameOptions.Parse(new[] { "--scale", "3", "--seed", "7", "--windowed" });

        Assert.True(options.Windowed);
        Assert.Equal(3, options.Scale);
        Assert.Equal(7, options.Seed);
    }

    [Theory]
    [InlineData("--windowed", "--scale")]
    [InlineData("--windowed", "--scale", "abc")]
    [InlineData("--windowed", "--scale", "0")]
    [InlineData("--windowed", "--scale", "5")]
    [InlineData("--windowed", "--scale", "1.5")]
    public void RejectsAMissingOrOutOfRangeScale(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(args));
    }

    [Fact]
    public void RejectsScaleWithoutWindowed()
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--scale", "2" }));
    }

    [Fact]
    public void DefaultsToTheMenuAndOnePlayer()
    {
        var options = GameOptions.Parse(Array.Empty<string>());

        Assert.False(options.Test);
        Assert.Equal(MatchSetup.OnePlayer, options.Setup);
    }

    [Theory]
    [InlineData("--test")]
    [InlineData("-test")]
    public void TestSkipsTheMenu(string argument)
    {
        Assert.True(GameOptions.Parse(new[] { argument }).Test);
    }

    [Fact]
    public void TwoPlayerSelectsTheTwoPlayerMode()
    {
        Assert.Equal(MatchSetup.TwoPlayer, GameOptions.Parse(new[] { "--two-player" }).Setup);
    }

    [Fact]
    public void TestAndTwoPlayerCombineWithOtherOptionsInAnyOrder()
    {
        var options = GameOptions.Parse(new[] { "--seed", "5", "--two-player", "--windowed", "--test", "--scale", "1" });

        Assert.True(options.Test);
        Assert.Equal(MatchSetup.TwoPlayer, options.Setup);
        Assert.Equal(5, options.Seed);
        Assert.True(options.Windowed);
        Assert.Equal(1, options.Scale);
    }

    [Fact]
    public void SoundIsOnUnlessMuted()
    {
        Assert.False(GameOptions.Parse(Array.Empty<string>()).Mute);
        Assert.True(GameOptions.Parse(new[] { "--mute" }).Mute);
    }

    [Fact]
    public void MuteCombinesWithOtherOptions()
    {
        var options = GameOptions.Parse(new[] { "--test", "--mute", "--seed", "3" });

        Assert.True(options.Mute);
        Assert.True(options.Test);
        Assert.Equal(3, options.Seed);
    }

    [Fact]
    public void DemoSelectsTwoComputers()
    {
        Assert.Equal(MatchSetup.Demo, GameOptions.Parse(new[] { "--demo" }).Setup);
    }

    [Fact]
    public void DemoCombinesWithOtherOptions()
    {
        var options = GameOptions.Parse(new[] { "--test", "--demo", "--windowed", "--seed", "9" });

        Assert.True(options.Test);
        Assert.Equal(MatchSetup.Demo, options.Setup);
        Assert.Equal(9, options.Seed);
    }

    [Fact]
    public void TwoPlayerAndDemoCannotBeCombined()
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--two-player", "--demo" }));
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--demo", "--two-player" }));
    }

    [Fact]
    public void RepeatingTheSameSetupOptionIsHarmless()
    {
        Assert.Equal(MatchSetup.Demo, GameOptions.Parse(new[] { "--demo", "--demo" }).Setup);
    }
}

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
}

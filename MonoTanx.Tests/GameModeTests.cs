using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class GameModeTests
{
    [Fact]
    public void ToggleFlipsBetweenOneAndTwoPlayers()
    {
        Assert.Equal(GameMode.TwoPlayer, GameMode.OnePlayer.Toggle());
        Assert.Equal(GameMode.OnePlayer, GameMode.TwoPlayer.Toggle());
    }

    [Fact]
    public void TogglingTwiceReturnsToTheStart()
    {
        Assert.Equal(GameMode.OnePlayer, GameMode.OnePlayer.Toggle().Toggle());
    }

    [Theory]
    [InlineData(GameMode.OnePlayer, "Players: 1")]
    [InlineData(GameMode.TwoPlayer, "Players: 2")]
    public void LabelsTheMode(GameMode mode, string expected)
    {
        Assert.Equal(expected, mode.Label());
    }
}

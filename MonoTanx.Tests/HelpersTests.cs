using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class HelpersTests
{
    [Theory]
    [InlineData(-1, 1, "L")]
    [InlineData(1, -1, "R")]
    [InlineData(0, 1, "D")]
    [InlineData(0, -1, "U")]
    [InlineData(0, 0, "N")]
    public void DirectionFromVelocityGivesHorizontalPrecedence(float x, float y, string expected)
    {
        Assert.Equal(expected, Helpers.GetDirectionFromVelocity(new Vector2(x, y), "L", "R", "U", "D", "N"));
    }

    [Fact]
    public void RandomVelocityIsNeverZeroAndIsDeterministicForASeed()
    {
        var first = new System.Random(42);
        var second = new System.Random(42);

        for (int i = 0; i < 100; i++)
        {
            Vector2 velocity = Helpers.GetRandomVelocity(first);

            Assert.NotEqual(Vector2.Zero, velocity);
            Assert.InRange(velocity.X, -1, 1);
            Assert.InRange(velocity.Y, -1, 1);
            Assert.Equal(velocity, Helpers.GetRandomVelocity(second));
        }
    }
}

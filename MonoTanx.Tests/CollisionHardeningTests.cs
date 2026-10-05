using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

// A tank must never end an update inside blocking terrain, outside the map or overlapping the
// other tank, however fast it goes or however long the step is (#49). The divided map has a
// one-tile wall down column 11 (x 176 to 192); the corridor map has a one-tile-wide passage.
public class CollisionHardeningTests
{
    private static WorldMap Divided() => new WorldMap(FixturePath("divided.tmx"));
    private static WorldMap Corridor() => new WorldMap(FixturePath("corridor.tmx"));

    private static Player Tank(Vector2 position, float heading, float forwardSpeed = Tuning.Tank.ForwardSpeed)
    {
        var settings = new GameSettings();
        settings.Set(SettingKeys.ForwardSpeed, forwardSpeed);
        var tank = new Player("Test", "Sprites/tank", Color.White, Player.StandardAmmunition(settings), 20, settings: settings);
        tank.Position = position;
        tank.Heading = heading;
        return tank;
    }

    private static Player Far() => Tank(new Vector2(1000.0f, 1000.0f), 0.0f);

    private static void AssertLegal(WorldMap map, Player tank, Player other, string context)
    {
        Assert.True(map.CanOccupyCircle(tank.Position, tank.CollisionRadius), $"{context}: tank at {tank.Position} is in terrain or off the map");
        var minimum = tank.CollisionRadius + other.CollisionRadius;
        Assert.True(Vector2.Distance(tank.Position, other.Position) > minimum, $"{context}: tank at {tank.Position} overlaps the other at {other.Position}");
    }

    [Theory]
    [InlineData(0.0167f)]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(3.0f)]
    public void ALongStepDoesNotCarryATankThroughAThinWall(float step)
    {
        var map = Divided();
        var tank = Tank(new Vector2(140.0f, 24.0f), 0.0f, forwardSpeed: 240.0f);
        var other = Far();

        for (var i = 0; i < 4; i++)
            TankMovement.ApplyInput(map, tank, other, 0.0f, 1.0f, step);

        Assert.True(tank.Position.X <= 176.0f - tank.CollisionRadius + 0.001f, $"step {step}: tank got to x={tank.Position.X}");
        AssertLegal(map, tank, other, $"step {step}");
    }

    [Theory]
    [InlineData(240.0f)]
    [InlineData(480.0f)]
    public void TheFastestSpeedsDoNotTunnelAtTheFixedStepEither(float speed)
    {
        var map = Divided();
        var tank = Tank(new Vector2(100.0f, 24.0f), 0.0f, forwardSpeed: speed);
        var other = Far();

        for (var i = 0; i < 600; i++)
        {
            TankMovement.ApplyInput(map, tank, other, 0.0f, 1.0f, 1.0f / 60.0f);
            AssertLegal(map, tank, other, $"speed {speed} step {i}");
        }

        Assert.True(tank.Position.X < 176.0f);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void ALongStepDoesNotCarryATankThroughTheOtherTank(float step)
    {
        var map = Divided();
        var tank = Tank(new Vector2(20.0f, 24.0f), 0.0f, forwardSpeed: 240.0f);
        var other = Tank(new Vector2(100.0f, 24.0f), 0.0f);

        TankMovement.ApplyInput(map, tank, other, 0.0f, 1.0f, step);

        Assert.True(tank.Position.X <= 100.0f - tank.CollisionRadius - other.CollisionRadius + 0.001f, $"step {step}: tank got to x={tank.Position.X}");
        AssertLegal(map, tank, other, $"step {step}");
    }

    [Fact]
    public void ReversingALongStepDoesNotTunnelEither()
    {
        var map = Divided();
        var tank = Tank(new Vector2(210.0f, 24.0f), 0.0f);
        var other = Far();

        TankMovement.ApplyInput(map, tank, other, 0.0f, -1.0f, 2.0f);

        Assert.True(tank.Position.X >= 192.0f + tank.CollisionRadius - 0.001f, $"tank got to x={tank.Position.X}");
        AssertLegal(map, tank, other, "reverse");
    }

    [Fact]
    public void ATankPassesAlongAOneTileCorridorAtAnySpeedAndStep()
    {
        foreach (var step in new[] { 1.0f / 60.0f, 0.25f, 1.0f })
        {
            var map = Corridor();
            var tank = Tank(new Vector2(24.0f, 24.0f), 0.0f, forwardSpeed: 240.0f);
            var other = Far();

            for (var i = 0; i < (int)System.Math.Ceiling(3.0f / step); i++)
            {
                TankMovement.ApplyInput(map, tank, other, 0.0f, 1.0f, step);
                AssertLegal(map, tank, other, $"corridor step {step}");
            }

            Assert.Equal(24.0f, tank.Position.Y, 3);
            Assert.True(tank.Position.X > 280.0f, $"step {step}: the tank should reach the far end (x={tank.Position.X})");
        }
    }

    [Fact]
    public void ATankHeadedDiagonallyIntoACorridorWallSlidesAlongItAndIsNotStuck()
    {
        var map = Corridor();
        var tank = Tank(new Vector2(24.0f, 24.0f), MathHelper.ToRadians(20.0f), forwardSpeed: 240.0f);
        var other = Far();

        for (var i = 0; i < 20; i++)
        {
            TankMovement.ApplyInput(map, tank, other, 0.0f, 1.0f, 0.25f);
            AssertLegal(map, tank, other, "slide");
        }

        Assert.True(tank.Position.X > 200.0f, $"the tank should have slid a long way along the corridor (x={tank.Position.X})");
    }

    [Fact]
    public void SubSteppingGivesTheSameResultAsOneStepWhenTheMoveIsShort()
    {
        var map = Divided();
        var a = Tank(new Vector2(100.0f, 40.0f), MathHelper.ToRadians(30.0f));
        var b = Tank(new Vector2(100.0f, 40.0f), MathHelper.ToRadians(30.0f));
        var other = Far();

        TankMovement.ApplyInput(map, a, other, 0.0f, 1.0f, 1.0f / 60.0f);
        TankMovement.Move(map, b, other, new Vector2((float)System.Math.Cos(b.Heading), (float)System.Math.Sin(b.Heading)) * b.MovementSpeed * (1.0f / 60.0f));

        Assert.Equal(a.Position, b.Position);
    }

    [Fact]
    public void ATankDrivenInEveryDirectionAtAnySpeedAndStepNeverEndsInsideAnything()
    {
        var map = Divided();
        var random = new System.Random(2026);
        foreach (var step in new[] { 1.0f / 60.0f, 0.05f, 0.3f, 1.5f })
        {
            var tank = Tank(new Vector2(140.0f, 40.0f), 0.0f, forwardSpeed: 480.0f);
            var other = Tank(new Vector2(60.0f, 56.0f), 0.0f);
            for (var i = 0; i < 400; i++)
            {
                var turn = (float)(random.NextDouble() * 2.0 - 1.0);
                var drive = random.Next(3) - 1;
                TankMovement.ApplyInput(map, tank, other, turn, drive, step);
                AssertLegal(map, tank, other, $"step {step} update {i}");
                tank.Fuel = tank.MaximumFuel;
            }
        }
    }
}

using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

// The simulation is time-based: every rule takes elapsed seconds. These tests
// run the same simulated time at different step sizes and expect the same result.
public class FrameRateIndependenceTests
{
    public static IEnumerable<object[]> StepsPerSecond => new[]
    {
        new object[] { 30 },
        new object[] { 60 },
        new object[] { 120 },
    };

    private const float TotalSeconds = 0.6f;

    private static int StepCount(int stepsPerSecond) => (int)Math.Round(TotalSeconds * stepsPerSecond);

    [Theory]
    [MemberData(nameof(StepsPerSecond))]
    public void DrivingForwardCoversTheSameDistanceAndFuel(int stepsPerSecond)
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(new Vector2(8.0f, 40.0f), heading: 0.0f); // along open ground on row 2
        var bystander = NewBystander();
        var step = 1.0f / stepsPerSecond;

        for (var i = 0; i < StepCount(stepsPerSecond); i++)
            TankMovement.ApplyInput(map, tank, bystander, 0.0f, 1.0f, step);

        Assert.Equal(8.0f + tank.MovementSpeed * TotalSeconds, tank.Position.X, 2);
        Assert.Equal(40.0f, tank.Position.Y, 2);
        Assert.Equal(tank.MaximumFuel - tank.ForwardFuelPerSecond * TotalSeconds, tank.Fuel, 2);
    }

    [Theory]
    [MemberData(nameof(StepsPerSecond))]
    public void ReversingCoversTheSameDistanceAndFuel(int stepsPerSecond)
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(new Vector2(100.0f, 40.0f), heading: 0.0f);
        var bystander = NewBystander();
        var step = 1.0f / stepsPerSecond;

        for (var i = 0; i < StepCount(stepsPerSecond); i++)
            TankMovement.ApplyInput(map, tank, bystander, 0.0f, -1.0f, step);

        Assert.Equal(100.0f - tank.ReverseMovementSpeed * TotalSeconds, tank.Position.X, 2);
        Assert.Equal(tank.MaximumFuel - tank.ReverseFuelPerSecond * TotalSeconds, tank.Fuel, 2);
    }

    [Theory]
    [MemberData(nameof(StepsPerSecond))]
    public void TurningRotatesByTheSameAngleAndUsesTheSameFuel(int stepsPerSecond)
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(3, 2), heading: 0.0f);
        var bystander = NewBystander();
        var step = 1.0f / stepsPerSecond;

        for (var i = 0; i < StepCount(stepsPerSecond); i++)
            TankMovement.ApplyInput(map, tank, bystander, 1.0f, 0.0f, step);

        Assert.Equal(tank.TurnSpeed * TotalSeconds, tank.Heading, 2);
        Assert.Equal(tank.MaximumFuel - TankMovement.TurnFuelPerSecond * TotalSeconds, tank.Fuel, 2);
    }

    [Theory]
    [MemberData(nameof(StepsPerSecond))]
    public void ReloadFinishesAfterTheSameElapsedTime(int stepsPerSecond)
    {
        var tank = NewPlayer(Vector2.Zero);
        tank.TryFire(0.0f, 1.0f, out _);
        var reload = Player.DefaultAmmunition.ReloadTimeSeconds;
        var step = 1.0f / stepsPerSecond;
        var steps = (int)Math.Round(reload * stepsPerSecond);

        for (var i = 0; i < steps - 1; i++)
            tank.TickReload(step);
        Assert.True(tank.ReloadTimer > 0.0f, "still reloading just before the reload time has passed");

        tank.TickReload(step);
        Assert.Equal(0.0f, tank.ReloadTimer, 3);
    }

    [Theory]
    [MemberData(nameof(StepsPerSecond))]
    public void ShellsTravelTheSameDistanceAndAge(int stepsPerSecond)
    {
        var map = LoadTerrainMap();
        var shell = new Shell(Player.DefaultAmmunition, new Vector2(8.0f, 8.0f), new Vector2(100.0f, 0.0f)); // along row 0
        var tanks = new[] { NewBystander() };
        var step = 1.0f / stepsPerSecond;

        for (var i = 0; i < StepCount(stepsPerSecond); i++)
            Assert.False(shell.Step(map, tanks, step).Removed);

        Assert.Equal(8.0f + 100.0f * TotalSeconds, shell.Position.X, 2);
        Assert.Equal(8.0f, shell.Position.Y, 2);
        Assert.Equal(TotalSeconds, shell.Age, 2);
    }

    [Theory]
    [MemberData(nameof(StepsPerSecond))]
    public void ShellsExpireAtTheSameSimulatedTime(int stepsPerSecond)
    {
        var map = LoadTerrainMap();
        var shell = new Shell(Player.DefaultAmmunition, new Vector2(8.0f, 8.0f), Vector2.Zero);
        var tanks = new[] { NewBystander() };
        var step = 1.0f / stepsPerSecond;
        var lifetime = Player.DefaultAmmunition.MaxFlightDurationSeconds;

        var elapsed = 0.0f;
        while (!shell.Step(map, tanks, step).Removed)
            elapsed += step;
        elapsed += step; // the step that removed it

        Assert.InRange(elapsed, lifetime, lifetime + step + 0.001f);
    }

    [Theory]
    [MemberData(nameof(StepsPerSecond))]
    public void ShellsStopAtAWallAtAnyStepSize(int stepsPerSecond)
    {
        var map = LoadTerrainMap();
        var shell = new Shell(Player.DefaultAmmunition, new Vector2(20.0f, 24.0f), new Vector2(260.0f, 0.0f)); // wall at tile (3, 1) starts at x = 48
        var tanks = new[] { NewBystander() };
        var step = 1.0f / stepsPerSecond;

        ShellStepResult result;
        var guard = 0;
        do { result = shell.Step(map, tanks, step); }
        while (!result.Removed && ++guard < 10 * stepsPerSecond);

        Assert.True(result.Removed);
        Assert.InRange(shell.Position.X, 48.0f, 48.0f + 4.0f + 0.01f); // within one sub-step of the wall face
    }

    [Fact]
    public void ShellsCannotTunnelThroughAWallEvenWithVeryLongSteps()
    {
        var map = LoadTerrainMap();
        var shell = new Shell(Player.DefaultAmmunition, new Vector2(20.0f, 24.0f), new Vector2(260.0f, 0.0f));
        var tanks = new[] { NewBystander() };

        var result = shell.Step(map, tanks, 0.2f); // 52px in one step; the wall at x = 48..64 is in the way

        Assert.True(result.Removed);
    }
}

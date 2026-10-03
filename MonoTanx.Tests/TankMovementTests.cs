using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class TankMovementTests
{
    private const float Elapsed = 0.1f;

    [Fact]
    public void DrivingForwardMovesAlongTheHeadingAndUsesFuel()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(1, 2), heading: 0.0f);

        var drove = TankMovement.ApplyInput(map, tank, NewBystander(), 0.0f, 1.0f, Elapsed);

        Assert.True(drove);
        Assert.Equal(24.0f + tank.MovementSpeed * Elapsed, tank.Position.X, 3);
        Assert.Equal(40.0f, tank.Position.Y, 3);
        Assert.Equal(tank.MaximumFuel - tank.ForwardFuelPerSecond * Elapsed, tank.Fuel, 3);
    }

    [Fact]
    public void ReversingIsSlowerAndCostsMoreFuel()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(3, 2), heading: 0.0f);

        TankMovement.ApplyInput(map, tank, NewBystander(), 0.0f, -1.0f, Elapsed);

        Assert.Equal(56.0f - tank.ReverseMovementSpeed * Elapsed, tank.Position.X, 3);
        Assert.Equal(tank.MaximumFuel - tank.ReverseFuelPerSecond * Elapsed, tank.Fuel, 3);
        Assert.True(tank.ReverseMovementSpeed < tank.MovementSpeed);
        Assert.True(tank.ReverseFuelPerSecond > tank.ForwardFuelPerSecond);
    }

    [Fact]
    public void TurningChangesHeadingAndCostsFuelWithoutMoving()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(3, 2), heading: 0.0f);

        var drove = TankMovement.ApplyInput(map, tank, NewBystander(), 1.0f, 0.0f, Elapsed);

        Assert.False(drove);
        Assert.Equal(tank.TurnSpeed * Elapsed, tank.Heading, 3);
        Assert.Equal(Centre(3, 2), tank.Position);
        Assert.Equal(tank.MaximumFuel - TankMovement.TurnFuelPerSecond * Elapsed, tank.Fuel, 3);
    }

    [Fact]
    public void HeadingWrapsAroundPi()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(3, 2), heading: MathHelper.Pi - 0.01f);

        TankMovement.ApplyInput(map, tank, NewBystander(), 1.0f, 0.0f, Elapsed);

        Assert.InRange(tank.Heading, -MathHelper.Pi, -MathHelper.Pi + 0.5f);
    }

    [Fact]
    public void TerrainMultipliersScaleSpeedAndFuelCost()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(7, 1), heading: -MathHelper.PiOver2); // mud: speed x0.5, fuel x2

        TankMovement.ApplyInput(map, tank, NewBystander(), 0.0f, 1.0f, Elapsed);

        Assert.Equal(24.0f - tank.MovementSpeed * 0.5f * Elapsed, tank.Position.Y, 3);
        Assert.Equal(tank.MaximumFuel - tank.ForwardFuelPerSecond * 2.0f * Elapsed, tank.Fuel, 3);
    }

    [Fact]
    public void WithoutFuelATankCannotTurnOrDrive()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(3, 2), heading: 0.0f);
        tank.Fuel = 0.0f;

        var drove = TankMovement.ApplyInput(map, tank, NewBystander(), 1.0f, 1.0f, Elapsed);

        Assert.False(drove);
        Assert.Equal(Centre(3, 2), tank.Position);
        Assert.Equal(0.0f, tank.Heading);
        Assert.Equal(0.0f, tank.Fuel);
    }

    [Fact]
    public void WithFuelForOnlyTheTurnATankTurnsButDoesNotDrive()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(3, 2), heading: 0.0f);
        var turnCost = TankMovement.TurnFuelPerSecond * Elapsed;
        tank.Fuel = turnCost + 0.01f; // enough to turn, not enough to turn and drive

        var drove = TankMovement.ApplyInput(map, tank, NewBystander(), 1.0f, 1.0f, Elapsed);

        Assert.False(drove);
        Assert.Equal(Centre(3, 2), tank.Position);
        Assert.Equal(tank.TurnSpeed * Elapsed, tank.Heading, 3);
        Assert.Equal(0.01f, tank.Fuel, 3);
    }

    [Fact]
    public void FuelNeverGoesBelowZero()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(1, 2), heading: 0.0f);
        var driveCost = tank.ForwardFuelPerSecond * Elapsed;
        tank.Fuel = driveCost; // exactly enough

        TankMovement.ApplyInput(map, tank, NewBystander(), 0.0f, 1.0f, Elapsed);

        Assert.Equal(0.0f, tank.Fuel, 3);
        Assert.True(tank.Fuel >= 0.0f);
    }

    [Fact]
    public void FiringIsStillPossibleWithoutFuel()
    {
        var tank = NewPlayer(Centre(3, 2));
        tank.Fuel = 0.0f;

        Assert.True(tank.TryFire(0.0f, 1.0f, out _));
    }

    [Fact]
    public void MovementSlidesAlongAnObstacle()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(new Vector2(56.0f, 40.0f)); // directly below the wall at tile (3, 1)

        TankMovement.Move(map, tank, NewBystander(), new Vector2(3.0f, -5.0f));

        Assert.Equal(59.0f, tank.Position.X, 3); // horizontal part allowed
        Assert.Equal(40.0f, tank.Position.Y, 3); // vertical part blocked by the wall
    }

    [Fact]
    public void MovementIsBlockedByBlockingTerrain()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(new Vector2(56.0f, 40.0f));

        TankMovement.Move(map, tank, NewBystander(), new Vector2(0.0f, -10.0f));

        Assert.Equal(new Vector2(56.0f, 40.0f), tank.Position);
    }

    [Fact]
    public void MovementCannotLeaveTheMap()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(new Vector2(8.0f, 40.0f));

        TankMovement.Move(map, tank, NewBystander(), new Vector2(-5.0f, 0.0f));

        Assert.Equal(new Vector2(8.0f, 40.0f), tank.Position);
    }

    [Fact]
    public void TanksCannotOverlapEachOther()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(new Vector2(40.0f, 40.0f));
        var other = NewPlayer(new Vector2(53.0f, 40.0f)); // 13 apart; collision radii sum to 12

        TankMovement.Move(map, tank, other, new Vector2(2.0f, 0.0f)); // would close to 11

        Assert.Equal(new Vector2(40.0f, 40.0f), tank.Position);
        Assert.True(TankMovement.CanOccupy(map, tank, other, new Vector2(40.0f, 40.0f)));
        Assert.False(TankMovement.CanOccupy(map, tank, other, new Vector2(42.0f, 40.0f)));
    }
}

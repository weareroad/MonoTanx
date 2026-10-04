using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class TankCommandTests
{
    [Fact]
    public void NoneDoesNothing()
    {
        Assert.True(TankCommand.None.IsIdle);
        Assert.Equal(0.0f, TankCommand.None.Turn);
        Assert.Equal(0.0f, TankCommand.None.Drive);
        Assert.False(TankCommand.None.Fire);
    }

    [Theory]
    [InlineData(1.0f, 0.0f, false)]
    [InlineData(0.0f, -1.0f, false)]
    [InlineData(0.0f, 0.0f, true)]
    public void AnyInputMakesItNotIdle(float turn, float drive, bool fire)
    {
        Assert.False(new TankCommand(turn, drive, fire).IsIdle);
    }

    [Fact]
    public void FireDefaultsToFalse()
    {
        var command = new TankCommand(-1.0f, 1.0f);

        Assert.Equal(-1.0f, command.Turn);
        Assert.Equal(1.0f, command.Drive);
        Assert.False(command.Fire);
    }

    // ---- applying a command

    [Fact]
    public void ApplyingACommandIsTheSameAsApplyingItsParts()
    {
        var map = LoadTerrainMap();
        var a = NewPlayer(Centre(1, 2), heading: 0.3f);
        var b = NewPlayer(Centre(1, 2), heading: 0.3f);

        var droveA = TankMovement.ApplyInput(map, a, NewBystander(), new TankCommand(1.0f, 1.0f), 0.1f);
        var droveB = TankMovement.ApplyInput(map, b, NewBystander(), 1.0f, 1.0f, 0.1f);

        Assert.Equal(droveB, droveA);
        Assert.Equal(b.Position, a.Position);
        Assert.Equal(b.Heading, a.Heading);
        Assert.Equal(b.Fuel, a.Fuel);
    }

    [Theory]
    [InlineData(0.0f, 1.0f)]    // forward
    [InlineData(0.0f, -1.0f)]   // reverse
    [InlineData(1.0f, 0.0f)]    // turn
    [InlineData(-1.0f, 1.0f)]   // turn and drive
    [InlineData(0.0f, 0.0f)]    // nothing
    public void ApplyInputChargesExactlyTheFuelCost(float turn, float drive)
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(1, 2), heading: 0.0f);
        var command = new TankCommand(turn, drive);
        var expected = TankMovement.FuelCost(map, tank, command, 0.1f);
        var before = tank.Fuel;

        TankMovement.ApplyInput(map, tank, NewBystander(), command, 0.1f);

        Assert.Equal(before - expected, tank.Fuel, 3);
    }

    [Fact]
    public void FuelCostIsZeroForNoCommandAndAddsTurnAndDrive()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(1, 2));
        var turnOnly = TankMovement.FuelCost(map, tank, new TankCommand(1.0f, 0.0f), 0.1f);
        var driveOnly = TankMovement.FuelCost(map, tank, new TankCommand(0.0f, 1.0f), 0.1f);

        Assert.Equal(0.0f, TankMovement.FuelCost(map, tank, TankCommand.None, 0.1f));
        Assert.Equal(Tuning.Tank.TurnFuelPerSecond * 0.1f, turnOnly, 5);
        Assert.Equal(Tuning.Tank.ForwardFuelPerSecond * 0.1f, driveOnly, 5);
        Assert.Equal(turnOnly + driveOnly, TankMovement.FuelCost(map, tank, new TankCommand(1.0f, 1.0f), 0.1f), 5);
    }

    [Fact]
    public void ReversingAndTerrainChangeTheFuelCost()
    {
        var map = LoadTerrainMap();
        var onGround = NewPlayer(Centre(1, 2));
        var onMud = NewPlayer(Centre(7, 1)); // fuel multiplier 2

        var forward = TankMovement.FuelCost(map, onGround, new TankCommand(0.0f, 1.0f), 0.1f);
        var reverse = TankMovement.FuelCost(map, onGround, new TankCommand(0.0f, -1.0f), 0.1f);
        var mud = TankMovement.FuelCost(map, onMud, new TankCommand(0.0f, 1.0f), 0.1f);

        Assert.Equal(forward * Tuning.Tank.ReverseFuelMultiplier, reverse, 5);
        Assert.Equal(forward * 2.0f, mud, 5);
    }

    // The computer keeps an all-or-nothing rule (it does nothing at all when it cannot pay for
    // both the turn and the drive). ApplyInput is gentler: it still turns. The controller
    // therefore checks the cost itself; this test pins the difference it has to respect.
    [Fact]
    public void ApplyInputStillTurnsWhenItCanPayForTheTurnButNotTheDrive()
    {
        var map = LoadTerrainMap();
        var tank = NewPlayer(Centre(3, 2), heading: 0.0f);
        var command = new TankCommand(1.0f, 1.0f);
        var turnCost = TankMovement.FuelCost(map, tank, new TankCommand(1.0f, 0.0f), 0.1f);
        tank.Fuel = turnCost + 0.001f; // enough to turn, not enough to turn and drive

        var drove = TankMovement.ApplyInput(map, tank, NewBystander(), command, 0.1f);

        Assert.False(drove);
        Assert.NotEqual(0.0f, tank.Heading);
        Assert.True(tank.Fuel < turnCost + 0.001f);
    }

    // ---- keyboard layouts

    private static KeyboardState Down(params Keys[] keys) => new KeyboardState(keys);

    private static readonly KeyboardState NoKeys = new KeyboardState();

    [Fact]
    public void NoKeysIsNoCommand()
    {
        Assert.True(SeatKeys.PlayerOne.ToCommand(NoKeys, NoKeys).IsIdle);
        Assert.True(SeatKeys.PlayerTwo.ToCommand(NoKeys, NoKeys).IsIdle);
    }

    [Fact]
    public void PlayerOneUsesWasdAndSpace()
    {
        var keys = SeatKeys.PlayerOne;

        Assert.Equal(1.0f, keys.ToCommand(Down(Keys.W), NoKeys).Drive);
        Assert.Equal(-1.0f, keys.ToCommand(Down(Keys.S), NoKeys).Drive);
        Assert.Equal(-1.0f, keys.ToCommand(Down(Keys.A), NoKeys).Turn);
        Assert.Equal(1.0f, keys.ToCommand(Down(Keys.D), NoKeys).Turn);
        Assert.True(keys.ToCommand(Down(Keys.Space), NoKeys).Fire);
    }

    [Fact]
    public void PlayerTwoUsesTheArrowsAndEnter()
    {
        var keys = SeatKeys.PlayerTwo;

        Assert.Equal(1.0f, keys.ToCommand(Down(Keys.Up), NoKeys).Drive);
        Assert.Equal(-1.0f, keys.ToCommand(Down(Keys.Down), NoKeys).Drive);
        Assert.Equal(-1.0f, keys.ToCommand(Down(Keys.Left), NoKeys).Turn);
        Assert.Equal(1.0f, keys.ToCommand(Down(Keys.Right), NoKeys).Turn);
        Assert.True(keys.ToCommand(Down(Keys.Enter), NoKeys).Fire);
    }

    [Fact]
    public void EachLayoutIgnoresTheOthersKeys()
    {
        Assert.True(SeatKeys.PlayerOne.ToCommand(Down(Keys.Up, Keys.Left, Keys.Enter), NoKeys).IsIdle);
        Assert.True(SeatKeys.PlayerTwo.ToCommand(Down(Keys.W, Keys.A, Keys.Space), NoKeys).IsIdle);
    }

    [Fact]
    public void OppositeKeysCancelOut()
    {
        var command = SeatKeys.PlayerOne.ToCommand(Down(Keys.A, Keys.D, Keys.W, Keys.S), NoKeys);

        Assert.Equal(0.0f, command.Turn);
        Assert.Equal(0.0f, command.Drive);
    }

    [Fact]
    public void TurningAndDrivingTogetherAreBothReported()
    {
        var command = SeatKeys.PlayerOne.ToCommand(Down(Keys.D, Keys.W), NoKeys);

        Assert.Equal(1.0f, command.Turn);
        Assert.Equal(1.0f, command.Drive);
    }

    [Fact]
    public void FireIsOnlyReportedOnTheUpdateTheKeyGoesDown()
    {
        var keys = SeatKeys.PlayerOne;

        Assert.True(keys.ToCommand(Down(Keys.Space), NoKeys).Fire);               // just pressed
        Assert.False(keys.ToCommand(Down(Keys.Space), Down(Keys.Space)).Fire);   // held
        Assert.False(keys.ToCommand(NoKeys, Down(Keys.Space)).Fire);              // just released
        Assert.False(keys.ToCommand(NoKeys, NoKeys).Fire);
    }

    [Fact]
    public void HoldingFireDoesNotBypassTheReload()
    {
        var tank = NewPlayer(Vector2.Zero);
        var keys = SeatKeys.PlayerOne;
        var previous = NoKeys;
        var shots = 0;

        for (var update = 0; update < 10; update++)
        {
            var current = Down(Keys.Space); // held down the whole time
            if (keys.ToCommand(current, previous).Fire && tank.TryFire(0.0f, out _))
                shots++;
            previous = current;
        }

        Assert.Equal(1, shots);
    }

    // ---- pickups

    [Fact]
    public void APickupStateStartsActiveAndKeepsItsSpawn()
    {
        var spawn = new PickupSpawn(5, PickupKind.Fuel, new Vector2(10, 20), 50, null, "Sprites/fueldrop_1");

        var state = new PickupState(spawn);

        Assert.Same(spawn, state.Spawn);
        Assert.True(state.Active);

        state.Active = false;
        Assert.False(state.Active);
    }
}

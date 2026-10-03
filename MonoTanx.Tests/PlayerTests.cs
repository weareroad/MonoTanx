using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class PlayerTests
{
    private static readonly Ammunition Small = new Ammunition("small", "", "s", "s-fire", 1.0f, 2.0f, 1);
    private static readonly Ammunition Large = new Ammunition("large", "", "l", "l-fire", 4.0f, 6.0f, 9);

    private static Player TwoSlotPlayer() =>
        new Player("Two", "Sprites/tank", Color.White, Small, 2, Large, 3);

    [Fact]
    public void StartsWithFullResources()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);

        Assert.Equal(Player.DefaultMaximumHealth, player.Health);
        Assert.Equal(Player.DefaultMaximumFuel, player.Fuel);
        Assert.Equal(Player.DefaultStartingShells, player.RemainingAmmunition);
        Assert.Equal(Player.DefaultStartingShells, player.StartingAmmunition);
        Assert.Equal(0.0f, player.ReloadTimer);
    }

    [Fact]
    public void ResetFuelAndAmmunitionRestoresFuelAmmunitionAndReloadButNotHealth()
    {
        var player = TwoSlotPlayer();
        player.Fuel = 10.0f;
        player.Health = 40;
        player.ReloadTimer = 2.0f;
        player.TryConsumeAmmunition(out _);

        player.ResetFuelAndAmmunition();

        Assert.Equal(player.MaximumFuel, player.Fuel);
        Assert.Equal(5, player.RemainingAmmunition);
        Assert.Equal(0.0f, player.ReloadTimer);
        Assert.Equal(40, player.Health);
    }

    [Fact]
    public void ResetResourcesAlsoRestoresHealth()
    {
        var player = TwoSlotPlayer();
        player.Health = 40;

        player.ResetResources();

        Assert.Equal(player.MaximumHealth, player.Health);
    }

    [Fact]
    public void AmmunitionIsConsumedFromTheFirstSlotBeforeTheSecond()
    {
        var player = TwoSlotPlayer();

        var fired = new List<string>();
        while (player.TryConsumeAmmunition(out var ammunition))
            fired.Add(ammunition.Id);

        Assert.Equal(new[] { "small", "small", "large", "large", "large" }, fired);
        Assert.Equal(0, player.RemainingAmmunition);
    }

    [Fact]
    public void ConsumingWithNoAmmunitionFails()
    {
        var player = TwoSlotPlayer();
        foreach (var slot in player.AmmunitionSlots) slot.Remaining = 0;

        Assert.False(player.TryConsumeAmmunition(out var ammunition));
        Assert.Null(ammunition);
    }

    [Fact]
    public void ReportsRemainingAndStartingTotalsAcrossSlots()
    {
        var player = TwoSlotPlayer();
        player.TryConsumeAmmunition(out _);

        Assert.Equal(4, player.RemainingAmmunition);
        Assert.Equal(5, player.StartingAmmunition);
    }

    [Fact]
    public void TickReloadCountsDownAndStopsAtZero()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);
        player.ReloadTimer = 1.0f;

        player.TickReload(0.4f);
        Assert.Equal(0.6f, player.ReloadTimer, 3);

        player.TickReload(5.0f);
        Assert.Equal(0.0f, player.ReloadTimer);
    }

    [Fact]
    public void FiringLaunchesAlongTheHeadingFromTheMuzzle()
    {
        var player = TestSupport.NewPlayer(new Vector2(100.0f, 50.0f), heading: -MathHelper.PiOver2);

        Assert.True(player.TryFire(10.0f, 260.0f, out var launch));

        Assert.Equal(Player.DefaultAmmunition, launch.Ammunition);
        Assert.Equal(100.0f, launch.Position.X, 3);
        Assert.Equal(40.0f, launch.Position.Y, 3);
        Assert.Equal(0.0f, launch.Velocity.X, 3);
        Assert.Equal(-260.0f, launch.Velocity.Y, 3);
    }

    [Fact]
    public void FiringConsumesAmmunitionAndStartsTheReloadFromTheAmmunitionType()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);

        player.TryFire(0.0f, 1.0f, out _);

        Assert.Equal(Player.DefaultStartingShells - 1, player.RemainingAmmunition);
        Assert.Equal(Player.DefaultAmmunition.ReloadTimeSeconds, player.ReloadTimer);
    }

    [Fact]
    public void FiringIsRefusedWhileReloading()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);
        player.TryFire(0.0f, 1.0f, out _);

        Assert.False(player.TryFire(0.0f, 1.0f, out _));
        Assert.Equal(Player.DefaultStartingShells - 1, player.RemainingAmmunition);

        player.TickReload(Player.DefaultAmmunition.ReloadTimeSeconds);
        Assert.True(player.TryFire(0.0f, 1.0f, out _));
    }

    [Fact]
    public void FiringIsRefusedWithNoAmmunition()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);
        foreach (var slot in player.AmmunitionSlots) slot.Remaining = 0;

        Assert.False(player.TryFire(0.0f, 1.0f, out _));
        Assert.Equal(0.0f, player.ReloadTimer);
    }
}

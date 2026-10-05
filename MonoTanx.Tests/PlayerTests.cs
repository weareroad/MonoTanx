using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class PlayerTests
{
    private static readonly Ammunition Small = new Ammunition("small", "", "s", "s-fire", 1.0f, 2.0f, 100.0f, 1);
    private static readonly Ammunition Large = new Ammunition("large", "", "l", "l-fire", 4.0f, 6.0f, 150.0f, 9);

    private static Player TwoSlotPlayer() =>
        new Player("Two", "Sprites/tank", Color.White, Small, 2, Large, 3);

    [Fact]
    public void StartsWithFullResources()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);

        Assert.Equal(Tuning.Tank.MaximumHealth, player.Health);
        Assert.Equal(Tuning.Tank.MaximumFuel, player.Fuel);
        Assert.Equal(Tuning.Tank.StartingShells, player.RemainingAmmunition);
        Assert.Equal(Tuning.Tank.StartingShells, player.StartingAmmunition);
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

        Assert.True(player.TryFire(10.0f, out var launch));

        Assert.Equal(Player.DefaultAmmunition, launch.Ammunition);
        Assert.Equal(100.0f, launch.Position.X, 3);
        Assert.Equal(40.0f, launch.Position.Y, 3);
        Assert.Equal(0.0f, launch.Velocity.X, 3);
        Assert.Equal(-Tuning.StandardShell.Speed, launch.Velocity.Y, 3);
    }

    [Fact]
    public void ShellSpeedComesFromTheAmmunitionType()
    {
        var player = TwoSlotPlayer();
        player.Heading = 0.0f;

        player.TryFire(0.0f, out var first);   // first slot: Small
        foreach (var slot in player.AmmunitionSlots) slot.Remaining = 0;
        player.AmmunitionSlots[1].Remaining = 1;
        player.ReloadTimer = 0.0f;
        player.TryFire(0.0f, out var second);  // second slot: Large

        Assert.Equal(Small.Speed, first.Velocity.X, 3);
        Assert.Equal(Large.Speed, second.Velocity.X, 3);
    }

    [Fact]
    public void FiringConsumesAmmunitionAndStartsTheReloadFromTheAmmunitionType()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);

        player.TryFire(0.0f, out _);

        Assert.Equal(Tuning.Tank.StartingShells - 1, player.RemainingAmmunition);
        Assert.Equal(Player.DefaultAmmunition.ReloadTimeSeconds, player.ReloadTimer);
    }

    [Fact]
    public void FiringIsRefusedWhileReloading()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);
        player.TryFire(0.0f, out _);

        Assert.False(player.TryFire(0.0f, out _));
        Assert.Equal(Tuning.Tank.StartingShells - 1, player.RemainingAmmunition);

        player.TickReload(Player.DefaultAmmunition.ReloadTimeSeconds);
        Assert.True(player.TryFire(0.0f, out _));
    }

    [Fact]
    public void FiringIsRefusedWithNoAmmunition()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);
        foreach (var slot in player.AmmunitionSlots) slot.Remaining = 0;

        Assert.False(player.TryFire(0.0f, out _));
        Assert.Equal(0.0f, player.ReloadTimer);
    }

    [Fact]
    public void TickReloadReportsReadyOnceWhenTheReloadFinishesWithAmmunitionLeft()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);
        player.TryFire(0.0f, out _);
        var reload = Player.DefaultAmmunition.ReloadTimeSeconds;

        Assert.False(player.TickReload(reload - 0.5f));  // still reloading
        Assert.True(player.TickReload(0.5f));            // finishes here
        Assert.False(player.TickReload(0.5f));           // already ready: not reported again
        Assert.False(player.TickReload(0.5f));
    }

    [Fact]
    public void TickReloadDoesNotReportReadyWhenTheTankIsOutOfShells()
    {
        var player = TestSupport.NewPlayer(Vector2.Zero);
        player.TryFire(0.0f, out _);
        foreach (var slot in player.AmmunitionSlots) slot.Remaining = 0;

        Assert.False(player.TickReload(Player.DefaultAmmunition.ReloadTimeSeconds + 1.0f));
        Assert.Equal(0.0f, player.ReloadTimer);
    }

    [Fact]
    public void TickReloadReportsNothingForATankThatHasNotFired()
    {
        Assert.False(TestSupport.NewPlayer(Vector2.Zero).TickReload(1.0f));
    }

    [Fact]
    public void ACopyForPredictionHasTheSameRulesStateAndMovesIndependently()
    {
        var player = TestSupport.NewPlayer(new Vector2(40.0f, 24.0f), 1.0f);
        player.Fuel = 123.0f;

        var copy = player.CopyForPrediction();
        copy.Position = new Vector2(80.0f, 80.0f);
        copy.Heading = 2.0f;
        copy.Fuel = 5.0f;

        Assert.Equal(new Vector2(40.0f, 24.0f), player.Position);
        Assert.Equal((1.0f, 123.0f), (player.Heading, player.Fuel));
        var fresh = player.CopyForPrediction();
        Assert.Equal((player.Position, player.Heading, player.Fuel, player.MovementSpeed, player.TurnSpeed, player.CollisionRadius),
            (fresh.Position, fresh.Heading, fresh.Fuel, fresh.MovementSpeed, fresh.TurnSpeed, fresh.CollisionRadius));
    }
}

using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

// Pins the pacing figures quoted in docs/tuning.md. If you change a value in
// Tuning and one of these fails, update the document as well as the test.
public class TuningTests
{
    private static WorldMap LoadArena() => new WorldMap(FixturePath("arena", "arena_01.tmx"));

    [Fact]
    public void CrossingTheArenaTakesAboutTenAndAHalfSeconds()
    {
        var seconds = LoadArena().Bounds.Width / Tuning.Tank.ForwardSpeed;

        Assert.InRange(seconds, 10.5f, 10.8f);
    }

    [Fact]
    public void AFullTankOfFuelIsAboutFiftySecondsOfDriving()
    {
        var seconds = Tuning.Tank.MaximumFuel / Tuning.Tank.ForwardFuelPerSecond;
        var widths = seconds * Tuning.Tank.ForwardSpeed / LoadArena().Bounds.Width;

        Assert.Equal(50.0f, seconds, 1);
        Assert.InRange(widths, 4.6f, 4.8f);
    }

    [Fact]
    public void ReversingCostsFourTimesTheFuelPerPixel()
    {
        var forward = Tuning.Tank.ForwardFuelPerSecond / Tuning.Tank.ForwardSpeed;
        var reverse = Tuning.Tank.ForwardFuelPerSecond * Tuning.Tank.ReverseFuelMultiplier / Tuning.Tank.ReverseSpeed;

        Assert.Equal(4.0f, reverse / forward, 3);
    }

    [Fact]
    public void ANinthHitIsNeededToKillAndThatMeansAtLeastTwentyFourSecondsOfFiring()
    {
        var hits = (int)Math.Ceiling(Tuning.Tank.MaximumHealth / (float)Tuning.StandardShell.Damage);
        var minimumSeconds = (hits - 1) * Tuning.StandardShell.ReloadSeconds;

        Assert.Equal(9, hits);
        Assert.Equal(24.0f, minimumSeconds, 3);
        Assert.True(hits < Tuning.Tank.StartingShells, "a kill must be possible within the starting ammunition");
    }

    [Fact]
    public void ShellRangeExceedsTheArenaDiagonalAndShellsOutrunTanks()
    {
        var bounds = LoadArena().Bounds;
        var diagonal = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
        var range = Tuning.StandardShell.Speed * Tuning.StandardShell.MaxFlightSeconds;

        Assert.Equal(1300.0f, range, 1);
        Assert.True(range > diagonal);
        Assert.InRange(Tuning.StandardShell.Speed / Tuning.Tank.ForwardSpeed, 2.8f, 3.0f);
    }

    [Fact]
    public void ATankFitsAOneTileCorridorWithFourPixelsToSpare()
    {
        var map = LoadArena();

        Assert.Equal(4.0f, map.TileWidth - 2 * Tuning.Tank.CollisionRadius, 3);
    }

    [Fact]
    public void ComputerFireCadenceIsSlightlySlowerThanTheReload()
    {
        var cadence = Tuning.Ai.FireCooldownSeconds + Tuning.Ai.ReactionDelaySeconds;

        Assert.Equal(3.25f, cadence, 3);
        Assert.True(cadence > Tuning.StandardShell.ReloadSeconds);
    }

    [Fact]
    public void TurningIsAlmostFreeComparedWithDriving()
    {
        var fullTurnSeconds = 2.0f * MathF.PI / Tuning.Tank.TurnSpeed;
        var turnFuel = fullTurnSeconds * Tuning.Tank.TurnFuelPerSecond;
        var driveFuel = fullTurnSeconds * Tuning.Tank.ForwardFuelPerSecond;

        Assert.InRange(turnFuel, 0.5f, 0.7f);
        Assert.InRange(driveFuel, 9.5f, 10.5f);
    }

    [Fact]
    public void ShellCollisionAndHitDistanceAreAsDocumented()
    {
        Assert.Equal(9.0f, Tuning.Projectile.CollisionRadius + Tuning.Tank.CollisionRadius, 3);
        Assert.True(Tuning.Projectile.SubStepLength < 16.0f / 2, "sub-steps must be well under a tile");
    }

    [Fact]
    public void TheDefaultAmmunitionIsBuiltFromTheTuningValues()
    {
        var shell = Player.DefaultAmmunition;

        Assert.Equal(Tuning.StandardShell.ReloadSeconds, shell.ReloadTimeSeconds);
        Assert.Equal(Tuning.StandardShell.MaxFlightSeconds, shell.MaxFlightDurationSeconds);
        Assert.Equal(Tuning.StandardShell.Speed, shell.Speed);
        Assert.Equal(Tuning.StandardShell.Damage, shell.Damage);
    }

    [Fact]
    public void NewPlayersTakeTheirDefaultsFromTuning()
    {
        var player = NewPlayer(Microsoft.Xna.Framework.Vector2.Zero);

        Assert.Equal(Tuning.Tank.MaximumHealth, player.MaximumHealth);
        Assert.Equal(Tuning.Tank.MaximumFuel, player.MaximumFuel);
        Assert.Equal(Tuning.Tank.ForwardSpeed, player.MovementSpeed);
        Assert.Equal(Tuning.Tank.ReverseSpeed, player.ReverseMovementSpeed);
        Assert.Equal(Tuning.Tank.TurnSpeed, player.TurnSpeed);
        Assert.Equal(Tuning.Tank.CollisionRadius, player.CollisionRadius);
        Assert.Equal(Tuning.Tank.ForwardFuelPerSecond, player.ForwardFuelPerSecond);
        Assert.Equal(Tuning.Tank.ForwardFuelPerSecond * Tuning.Tank.ReverseFuelMultiplier, player.ReverseFuelPerSecond);
        Assert.Equal(Tuning.Ai.EngageDistanceTiles, player.PreferredCombatDistanceTiles);
        Assert.Equal(Tuning.Ai.LongRangePursuitDistanceFraction, player.LongRangePursuitDistanceFraction);
        Assert.Equal(Tuning.Ai.AimToleranceRadians, player.ComputerAimToleranceRadians);
        Assert.Equal(Tuning.Ai.ReactionDelaySeconds, player.ComputerReactionDelaySeconds);
        Assert.Equal(Tuning.Ai.FireCooldownSeconds, player.ComputerFireCooldownSeconds);
    }

    [Fact]
    public void TheRoundTimeLimitCoversAFullTankOfDrivingPlusTheFastestKill()
    {
        var drivingSeconds = Tuning.Tank.MaximumFuel / Tuning.Tank.ForwardFuelPerSecond;
        var hitsToKill = (int)Math.Ceiling(Tuning.Tank.MaximumHealth / (float)Player.DefaultAmmunition.Damage);
        var fastestKillSeconds = (hitsToKill - 1) * Player.DefaultAmmunition.ReloadTimeSeconds;

        Assert.Equal(24.0f, fastestKillSeconds, 1);
        Assert.True(Tuning.Match.RoundTimeLimitSeconds >= drivingSeconds + fastestKillSeconds);
    }

    [Fact]
    public void AMatchIsAtMostFiveDecidedRounds()
    {
        Assert.Equal(3, Tuning.Match.RoundsToWin);
        Assert.Equal(5, 2 * (Tuning.Match.RoundsToWin - 1) + 1);
    }
}

using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class ShellTests
{
    private static Shell NewShell(float x, float y, float vx, float vy) =>
        new Shell(Player.DefaultAmmunition, new Vector2(x, y), new Vector2(vx, vy));

    private static Player[] NoTanks() => new[] { NewPlayer(Centre(7, 3)) };

    [Fact]
    public void ShellExpiresAfterItsMaximumFlightTime()
    {
        var shell = NewShell(8, 8, 10, 0);
        shell.Age = Player.DefaultAmmunition.MaxFlightDurationSeconds - 0.01f;

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.02f);

        Assert.True(result.Removed);
        Assert.Null(result.Hit);
    }

    [Fact]
    public void ShellFliesOverOpenGround()
    {
        var shell = NewShell(8, 8, 100, 0);

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.False(result.Removed);
        Assert.Equal(13.0f, shell.Position.X, 3);
        Assert.Equal(8.0f, shell.Position.Y, 3);
        Assert.Equal(0.05f, shell.Age, 3);
    }

    [Fact]
    public void ShellIsStoppedByAWall()
    {
        var shell = NewShell(40, 24, 100, 0); // wall at tile (3, 1) starts at x = 48

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.1f);

        Assert.True(result.Removed);
        Assert.Null(result.Hit);
    }

    [Fact]
    public void ShellIsStoppedByAHill()
    {
        var shell = NewShell(72, 24, 100, 0); // hill at tile (5, 1) starts at x = 80; ravine (4, 1) is passable

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.1f);

        Assert.True(result.Removed);
    }

    [Fact]
    public void ShellIsStoppedAtTheMapEdge()
    {
        var shell = NewShell(2, 8, -100, 0);

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.1f);

        Assert.True(result.Removed);
    }

    [Theory]
    [InlineData(14.0f, 19.0f)] // across the water at tile (1, 1)
    [InlineData(66.0f, 71.0f)] // across the ravine at tile (4, 1)
    public void ShellFliesOverWaterAndRavines(float startX, float expectedX)
    {
        var shell = NewShell(startX, 24, 100, 0);

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.False(result.Removed);
        Assert.Equal(expectedX, shell.Position.X, 3);
    }

    [Fact]
    public void ShellReflectsOffAHorizontalReflectiveRunByFlippingY()
    {
        var shell = NewShell(56, 46, 0, 100); // heading down into the run on row 3 (tiles 2..4)

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.False(result.Removed);
        Assert.Equal(-100.0f, shell.Velocity.Y, 3);
        Assert.Equal(0.0f, shell.Velocity.X, 3);
        Assert.Equal(1, shell.ReflectionCount);
        Assert.Equal(0.08f, shell.ReflectionCooldown, 3);
        Assert.Equal(44.0f, shell.Position.Y, 3);
    }

    [Fact]
    public void ShellReflectsOffAnIsolatedTileUsingItsFasterAxis()
    {
        var shell = NewShell(114, 24, -100, 0); // approaching the lone reflective tile (6, 1) from the right

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.False(result.Removed);
        Assert.Equal(100.0f, shell.Velocity.X, 3);
        Assert.Equal(1, shell.ReflectionCount);
    }

    [Fact]
    public void ReflectionCooldownStopsTheSameSurfaceReflectingTwice()
    {
        var shell = NewShell(56, 52, 0, -100); // inside the reflective run
        shell.ReflectionCooldown = 0.08f;

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.001f);

        Assert.False(result.Removed);
        Assert.Equal(0, shell.ReflectionCount);
        Assert.Equal(-100.0f, shell.Velocity.Y, 3);
    }

    [Fact]
    public void ShellIsRemovedAfterTooManyReflections()
    {
        var shell = NewShell(56, 46, 0, 100);
        shell.ReflectionCount = Tuning.Projectile.MaxReflections;

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.True(result.Removed);
        Assert.Null(result.Hit);
    }

    [Fact]
    public void ShellHitsATankInItsPath()
    {
        var tank = NewPlayer(new Vector2(60, 8));
        var shell = NewShell(40, 8, 100, 0);

        var result = shell.Step(LoadTerrainMap(), new[] { tank, NewBystander() }, 0.2f);

        Assert.True(result.Removed);
        Assert.Same(tank, result.Hit);
    }

    [Fact]
    public void ShellCanHitAnyTankIncludingTheOneThatFiredIt()
    {
        var shooter = NewPlayer(new Vector2(20, 8), heading: 0.0f);
        shooter.TryFire(0.0f, out var launch); // fired from its own centre, so it is already within range of itself
        var shell = new Shell(launch.Ammunition, launch.Position, launch.Velocity);

        var result = shell.Step(LoadTerrainMap(), new[] { shooter }, 0.01f);

        Assert.Same(shooter, result.Hit);
    }

    [Fact]
    public void WhenShellIsInRangeOfTwoTanksTheFirstListedIsHit()
    {
        var first = NewPlayer(new Vector2(60, 8), name: "First");
        var second = NewPlayer(new Vector2(62, 8), name: "Second");
        var shell = NewShell(60, 8, 10, 0);

        var result = shell.Step(LoadTerrainMap(), new[] { first, second }, 0.01f);

        Assert.Same(first, result.Hit);
    }

    [Fact]
    public void ShellMissesATankOutsideTheCollisionRadius()
    {
        var tank = NewPlayer(new Vector2(60, 8 + Tuning.Projectile.CollisionRadius + 6.0f + 1.0f));
        var shell = NewShell(40, 8, 100, 0);

        var result = shell.Step(LoadTerrainMap(), new[] { tank }, 0.2f);

        Assert.Null(result.Hit);
    }

    [Fact]
    public void ExpiredShellsReportTheirFateAndNoReflections()
    {
        var shell = NewShell(8, 8, 10, 0);
        shell.Age = Player.DefaultAmmunition.MaxFlightDurationSeconds - 0.01f;

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.02f);

        Assert.Equal(ShellFate.Expired, result.Fate);
        Assert.Equal(0, result.Reflections);
    }

    [Fact]
    public void ShellsInFlightReportInFlightAndNoReflections()
    {
        var result = NewShell(8, 8, 100, 0).Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.Equal(ShellFate.InFlight, result.Fate);
        Assert.False(result.Removed);
        Assert.Equal(0, result.Reflections);
    }

    [Theory]
    [InlineData(40.0f, 24.0f, 100.0f, 0.0f)]   // wall at tile (3, 1)
    [InlineData(72.0f, 24.0f, 100.0f, 0.0f)]   // hill at tile (5, 1)
    [InlineData(2.0f, 8.0f, -100.0f, 0.0f)]    // the map edge
    public void ShellsThatHitSolidTerrainReportHitTerrain(float x, float y, float vx, float vy)
    {
        var result = NewShell(x, y, vx, vy).Step(LoadTerrainMap(), NoTanks(), 0.1f);

        Assert.Equal(ShellFate.HitTerrain, result.Fate);
        Assert.Null(result.Hit);
        Assert.Equal(0, result.Reflections);
    }

    [Fact]
    public void ShellsThatHitATankReportHitTank()
    {
        var tank = NewPlayer(new Vector2(60, 8));

        var result = NewShell(40, 8, 100, 0).Step(LoadTerrainMap(), new[] { tank }, 0.2f);

        Assert.Equal(ShellFate.HitTank, result.Fate);
        Assert.Same(tank, result.Hit);
    }

    [Fact]
    public void AReflectionIsReportedAndTheShellKeepsFlying()
    {
        var shell = NewShell(56, 46, 0, 100); // down into the reflective run on row 3

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.Equal(ShellFate.InFlight, result.Fate);
        Assert.Equal(1, result.Reflections);
    }

    [Fact]
    public void AShellCannotReflectMoreThanOnceInOneStepBecauseOfTheCooldown()
    {
        var shell = NewShell(56, 46, 0, 100);

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 1.0f); // a very long step

        Assert.True(result.Reflections <= 1);
    }

    [Fact]
    public void TheReflectionThatExceedsTheLimitIsStillReportedWithItsFate()
    {
        var shell = NewShell(56, 46, 0, 100);
        shell.ReflectionCount = Tuning.Projectile.MaxReflections;

        var result = shell.Step(LoadTerrainMap(), NoTanks(), 0.05f);

        Assert.Equal(ShellFate.TooManyReflections, result.Fate);
        Assert.Equal(1, result.Reflections); // so a ping can still be played
    }
}

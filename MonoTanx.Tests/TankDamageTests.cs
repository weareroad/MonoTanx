using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class TankDamageTests
{
    [Fact]
    public void DamageReducesHealth()
    {
        var tank = NewPlayer(Centre(1, 2));

        var destroyed = TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), 12, Vector2.Zero, new Random(1));

        Assert.Equal(Tuning.Tank.MaximumHealth - 12, tank.Health);
        Assert.False(destroyed);
    }

    [Fact]
    public void HealthNeverGoesBelowZeroAndZeroHealthReportsDestroyed()
    {
        var tank = NewPlayer(Centre(1, 2));
        tank.Health = 5;

        var destroyed = TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), 50, Vector2.Zero, new Random(1));

        Assert.Equal(0, tank.Health);
        Assert.True(destroyed);
    }

    [Fact]
    public void NegativeDamageDoesNotHeal()
    {
        var tank = NewPlayer(Centre(1, 2));
        tank.Health = 50;

        TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), -20, Vector2.Zero, new Random(1));

        Assert.Equal(50, tank.Health);
    }

    [Fact]
    public void ImpactKnocksTheTankBackAlongTheShellDirection()
    {
        var tank = NewPlayer(Centre(1, 2));

        TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), 1, new Vector2(100, 0), new Random(1));

        Assert.Equal(24.0f + Tuning.Damage.KnockbackDistance, tank.Position.X, 3);
        Assert.Equal(40.0f, tank.Position.Y, 3);
    }

    [Fact]
    public void NoKnockbackWithoutImpactVelocity()
    {
        var tank = NewPlayer(Centre(1, 2));

        TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), 1, Vector2.Zero, new Random(1));

        Assert.Equal(Centre(1, 2), tank.Position);
    }

    [Fact]
    public void KnockbackIsSkippedWhenTheDestinationIsBlocked()
    {
        var tank = NewPlayer(new Vector2(56.0f, 38.1f)); // just below the wall at tile (3, 1)

        TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), 1, new Vector2(0, -100), new Random(1));

        Assert.Equal(new Vector2(56.0f, 38.1f), tank.Position);
        Assert.Equal(Tuning.Tank.MaximumHealth - 1, tank.Health);
    }

    [Fact]
    public void HeadingDisruptionIsBoundedAndComesFromTheSuppliedRandom()
    {
        var random = new Random(5);
        var expectedDraw = new Random(5).NextDouble();
        var tank = NewPlayer(Centre(1, 2), heading: 1.0f);

        TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), 1, Vector2.Zero, random);

        var expected = 1.0f + ((float)expectedDraw * 2.0f - 1.0f) * Tuning.Damage.MaximumHeadingDisruptionRadians;
        Assert.Equal(expected, tank.Heading, 5);
    }

    [Fact]
    public void TheSameSeedGivesTheSameOutcome()
    {
        float Run(int seed)
        {
            var tank = NewPlayer(Centre(1, 2), heading: 0.0f);
            TankDamage.Apply(LoadTerrainMap(), tank, NewBystander(), 1, Vector2.Zero, new Random(seed));
            return tank.Heading;
        }

        Assert.Equal(Run(42), Run(42));
        Assert.NotEqual(Run(42), Run(43));
        for (var seed = 0; seed < 50; seed++)
            Assert.InRange(Run(seed), -Tuning.Damage.MaximumHeadingDisruptionRadians, Tuning.Damage.MaximumHeadingDisruptionRadians);
    }
}

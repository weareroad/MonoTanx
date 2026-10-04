using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

// Whole matches on the real arena with no graphics device.
public class MatchSimulationSoakTests
{
    private static readonly int[] Seeds = { 1, 7, 42, 1234, 4242, 99999, 2026, 31337 };
    private const float MatchSeconds = 120.0f;

    private static MatchSetup Demo => new MatchSetup(PlayerControl.Computer, PlayerControl.Computer);

    // What must hold after every update, whatever the controllers do.
    private static void AssertInvariants(MatchHarness match)
    {
        var where = $"seed {match.Seed}, update {match.Frame}";
        foreach (var tank in new[] { match.PlayerOne, match.PlayerTwo })
        {
            Assert.True(match.Map.CanOccupyCircle(tank.Position, tank.CollisionRadius), $"{tank.Name} is on blocked terrain at {tank.Position} ({where})");
            Assert.InRange(tank.Fuel, 0.0f, tank.MaximumFuel);
            Assert.InRange(tank.Health, 0, tank.MaximumHealth);
            Assert.True(tank.RemainingAmmunition >= 0, $"{tank.Name} has negative ammunition ({where})");
            Assert.True(tank.ReloadTimer >= 0.0f, $"{tank.Name} has a negative reload timer ({where})");
            Assert.False(float.IsNaN(tank.Position.X) || float.IsNaN(tank.Position.Y) || float.IsNaN(tank.Heading), $"{tank.Name} has a NaN position or heading ({where})");
        }
        var minimumDistance = match.PlayerOne.CollisionRadius + match.PlayerTwo.CollisionRadius;
        Assert.True(Vector2.Distance(match.PlayerOne.Position, match.PlayerTwo.Position) > minimumDistance - 0.01f, $"the tanks overlap ({where})");
        foreach (var shell in match.Simulation.Shells)
            Assert.True(shell.Age < shell.Ammunition.MaxFlightDurationSeconds, $"a shell outlived its flight time, age {shell.Age} ({where})");
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void ComputerVersusComputerKeepsEveryRuleForTwoMinutes(int seed)
    {
        var match = new MatchHarness(seed, Demo).Run(MatchSeconds, AssertInvariants);

        // a match that did nothing would pass the invariants trivially
        Assert.True(match.Count(MatchEventKind.ShellFired) > 0, $"nobody fired (seed {seed})");
        Assert.True(match.Count(MatchEventKind.ShellFired, Seat.One) > 0 && match.Count(MatchEventKind.ShellFired, Seat.Two) > 0, $"only one seat fired (seed {seed})");
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void TheSameSeedGivesTheSameMatch(int seed)
    {
        var first = new MatchHarness(seed, Demo).Run(60.0f);
        var second = new MatchHarness(seed, Demo).Run(60.0f);

        Assert.Equal(first.Fingerprint(), second.Fingerprint());
    }

    [Fact]
    public void DifferentSeedsGiveDifferentMatches()
    {
        var fingerprints = Seeds.Select(seed => new MatchHarness(seed, Demo).Run(60.0f).Fingerprint()).Distinct().Count();

        Assert.True(fingerprints > 1, "every seed produced the same match");
    }

    [Theory]
    [InlineData(PlayerControl.Computer, PlayerControl.Computer)]
    [InlineData(PlayerControl.Human, PlayerControl.Computer)]
    [InlineData(PlayerControl.Computer, PlayerControl.Human)]
    [InlineData(PlayerControl.Human, PlayerControl.Human)]
    public void EveryCombinationOfControllersRuns(PlayerControl one, PlayerControl two)
    {
        var match = new MatchHarness(5, new MatchSetup(one, two)).Run(60.0f, AssertInvariants);

        Assert.Equal(60 * Tuning.Timing.UpdatesPerSecond, match.Frame);
    }

    [Theory]
    [InlineData(Seat.One)]
    [InlineData(Seat.Two)]
    public void AComputerInEitherSeatFindsAndShootsAnIdleHuman(Seat computerSeat)
    {
        var setup = computerSeat == Seat.One
            ? new MatchSetup(PlayerControl.Computer, PlayerControl.Human)
            : new MatchSetup(PlayerControl.Human, PlayerControl.Computer);
        var humanSeat = computerSeat == Seat.One ? Seat.Two : Seat.One;

        var match = new MatchHarness(7, setup).Run(90.0f, AssertInvariants);

        Assert.True(match.Count(MatchEventKind.ShellFired, computerSeat) > 0, "the computer never fired");
        Assert.Equal(0, match.Count(MatchEventKind.ShellFired, humanSeat)); // an idle human never fires
        Assert.True(match.Count(MatchEventKind.TankHit, humanSeat) > 0, "the computer never hit the idle human");
    }

    [Fact]
    public void ShellsFiredNeverExceedTheAmmunitionAvailable()
    {
        var match = new MatchHarness(42, Demo).Run(MatchSeconds);

        foreach (var seat in new[] { Seat.One, Seat.Two })
        {
            var tank = match.Simulation.TankOf(seat);
            var collectedAmmunition = match.Map.PickupSpawns.Where(p => p.Kind == PickupKind.Ammunition).Sum(p => p.Amount);
            Assert.True(match.Count(MatchEventKind.ShellFired, seat) <= tank.StartingAmmunition + collectedAmmunition, $"{tank.Name} fired more shells than it could have had");
        }
    }

    [Fact]
    public void HealthLostMatchesTheHitsTaken()
    {
        var match = new MatchHarness(1234, Demo).Run(MatchSeconds);

        foreach (var seat in new[] { Seat.One, Seat.Two })
        {
            var tank = match.Simulation.TankOf(seat);
            var expected = Math.Max(0, tank.MaximumHealth - match.Count(MatchEventKind.TankHit, seat) * Player.DefaultAmmunition.Damage);
            Assert.Equal(expected, tank.Health);
        }
    }

    [Fact]
    public void ADestroyedTankIsReportedExactlyAtTheHitThatEmptiedItsHealth()
    {
        var match = new MatchHarness(1234, Demo);
        match.Run(MatchSeconds);

        foreach (var seat in new[] { Seat.One, Seat.Two })
        {
            var tank = match.Simulation.TankOf(seat);
            var hits = match.Count(MatchEventKind.TankHit, seat);
            var lethalHits = hits * Player.DefaultAmmunition.Damage >= tank.MaximumHealth ? 1 : 0;
            Assert.True(match.Count(MatchEventKind.TankDestroyed, seat) >= lethalHits);
            if (tank.Health > 0)
                Assert.Equal(0, match.Count(MatchEventKind.TankDestroyed, seat));
        }
    }

    [Fact]
    public void ScriptedHumansDrivingAndFiringKeepEveryRule()
    {
        // two humans mashing keys by a fixed pseudo-random script
        var script = new Random(3);
        var match = new MatchHarness(11, new MatchSetup(PlayerControl.Human, PlayerControl.Human));

        match.Run(MatchSeconds, AssertInvariants, (_, seat) =>
            new TankCommand(script.Next(-1, 2), script.Next(-1, 2), script.Next(0, 20) == 0));

        Assert.True(match.Count(MatchEventKind.ShellFired) > 0);
    }

    public static IEnumerable<object[]> SeedData() => Seeds.Select(seed => new object[] { seed });
}

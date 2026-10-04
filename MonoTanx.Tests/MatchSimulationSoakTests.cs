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

    // Whole matches through the rounds

    private static readonly int[] MatchSeeds = { 1, 7, 42, 4242 };
    private const float LongestMatchSeconds = 3600.0f; // an hour of simulated play; the slowest of these seeds finishes in about 36 minutes (most rounds draw on the time limit)

    [Theory]
    [MemberData(nameof(MatchSeedData))]
    public void ComputerVersusComputerPlaysAWholeMatchToAWinner(int seed)
    {
        var match = new MatchHarness(seed, Demo).RunMatch(LongestMatchSeconds, AssertInvariants);
        var state = match.Session.State;

        Assert.Equal(MatchPhase.MatchOver, state.Phase);
        Assert.NotNull(state.Winner);
        var loser = state.Winner == Seat.One ? Seat.Two : Seat.One;
        Assert.Equal(Tuning.Match.RoundsToWin, state.ScoreOf(state.Winner.Value));
        Assert.True(state.ScoreOf(loser) < Tuning.Match.RoundsToWin);
        // every decided round was announced, and the match was won exactly once
        var decided = match.StateLog.Count(e => e.Kind == MatchStateEventKind.RoundWon);
        Assert.Equal(state.ScoreOf(Seat.One) + state.ScoreOf(Seat.Two), decided);
        Assert.Equal(1, match.StateLog.Count(e => e.Kind == MatchStateEventKind.MatchWon));
        Assert.Equal(decided + match.StateLog.Count(e => e.Kind == MatchStateEventKind.RoundDrawn), state.Round);
    }

    [Theory]
    [MemberData(nameof(MatchSeedData))]
    public void EveryRoundStartsFromTheSameCleanPosition(int seed)
    {
        var match = new MatchHarness(seed, Demo);
        var start = (match.PlayerOne.Position, match.PlayerOne.Heading, match.PlayerTwo.Position, match.PlayerTwo.Heading);
        var rounds = 0;

        match.RunMatch(LongestMatchSeconds, m =>
        {
            if (m.Session.State.Phase != MatchPhase.Countdown) return;
            rounds++;
            Assert.Equal(start, (m.PlayerOne.Position, m.PlayerOne.Heading, m.PlayerTwo.Position, m.PlayerTwo.Heading));
            Assert.Equal(m.PlayerOne.MaximumHealth, m.PlayerOne.Health);
            Assert.Equal(m.PlayerTwo.MaximumHealth, m.PlayerTwo.Health);
            Assert.Equal(m.PlayerOne.MaximumFuel, m.PlayerOne.Fuel);
            Assert.Equal(m.PlayerOne.StartingAmmunition, m.PlayerOne.RemainingAmmunition);
            Assert.Empty(m.Simulation.Shells);
            Assert.All(m.Simulation.Pickups, p => Assert.True(p.Active));
        });

        Assert.True(rounds > 0);
        Assert.True(match.Session.State.Round >= Tuning.Match.RoundsToWin);
    }

    [Fact]
    public void TheSameSeedPlaysTheSameWholeMatch()
    {
        var first = new MatchHarness(7, Demo).RunMatch(LongestMatchSeconds);
        var second = new MatchHarness(7, Demo).RunMatch(LongestMatchSeconds);

        Assert.Equal(first.Fingerprint(), second.Fingerprint());
        Assert.Equal(first.Session.State.Winner, second.Session.State.Winner);
    }

    [Fact]
    public void AStalledRoundEndsInADrawOnTheTimeLimitAndIsReplayed()
    {
        var match = new MatchHarness(3, Demo);
        // out of ammunition, neither computer can hurt the other
        foreach (var tank in new[] { match.PlayerOne, match.PlayerTwo })
            foreach (var slot in tank.AmmunitionSlots) slot.Remaining = 0;

        match.RunMatch(Tuning.Match.CountdownSeconds + Tuning.Match.RoundTimeLimitSeconds + Tuning.Match.RoundOverSeconds + 1.0f, AssertInvariants);

        var drawn = Assert.Single(match.StateLog, e => e.Kind == MatchStateEventKind.RoundDrawn);
        Assert.True(drawn.Frame >= (Tuning.Match.CountdownSeconds + Tuning.Match.RoundTimeLimitSeconds) * Tuning.Timing.UpdatesPerSecond);
        Assert.Equal(0, match.Session.State.ScoreOf(Seat.One) + match.Session.State.ScoreOf(Seat.Two));
        Assert.Equal(2, match.Session.State.Round);
        Assert.Equal(MatchPhase.Countdown, match.Session.State.Phase);
        Assert.Equal(match.PlayerOne.StartingAmmunition, match.PlayerOne.RemainingAmmunition); // the replay starts with ammunition again
    }

    [Theory]
    [InlineData(PlayerControl.Computer, PlayerControl.Computer)]
    [InlineData(PlayerControl.Human, PlayerControl.Computer)]
    [InlineData(PlayerControl.Computer, PlayerControl.Human)]
    [InlineData(PlayerControl.Human, PlayerControl.Human)]
    public void EveryCombinationOfControllersGetsThroughSeveralRounds(PlayerControl one, PlayerControl two)
    {
        // idle humans never fire, so a human-only match draws on the time limit and replays; a computer wins against an idle human
        var rounds = 2 * (Tuning.Match.CountdownSeconds + Tuning.Match.RoundTimeLimitSeconds + Tuning.Match.RoundOverSeconds);
        var match = new MatchHarness(5, new MatchSetup(one, two)).RunMatch(rounds, AssertInvariants);

        Assert.True(match.StateLog.Count(e => e.Kind == MatchStateEventKind.RoundWon || e.Kind == MatchStateEventKind.RoundDrawn) >= 2
            || match.Session.State.Phase == MatchPhase.MatchOver);
    }

    public static IEnumerable<object[]> SeedData() => Seeds.Select(seed => new object[] { seed });

    public static IEnumerable<object[]> MatchSeedData() => MatchSeeds.Select(seed => new object[] { seed });
}

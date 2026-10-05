using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class MatchSessionTests
{
    private const float Step = 1.0f / 60.0f;

    private static (MatchSession Session, Player One, Player Two) Humans()
    {
        var map = LoadTerrainMap();
        var one = NewPlayer(Vector2.Zero, name: "One");
        var two = NewPlayer(Vector2.Zero, name: "Two");
        var simulation = new MatchSimulation(map, one, two, map.PickupSpawns, new Random(1), new SimulationSettings(20.0f, 20.0f));
        simulation.PlaceAtStart();
        return (new MatchSession(simulation), one, two);
    }

    private static void RunFor(MatchSession session, float seconds, TankCommand? one = null, TankCommand? two = null)
    {
        for (var i = 0; i < (int)Math.Round(seconds / Step); i++)
            session.Step(Step, one, two);
    }

    private static void EndCountdown(MatchSession session) => RunFor(session, Tuning.Match.CountdownSeconds + 0.05f);

    [Fact]
    public void NothingHappensDuringTheCountdown()
    {
        var (session, one, two) = Humans();
        var start = (one.Position, two.Position);

        RunFor(session, Tuning.Match.CountdownSeconds - 0.1f, new TankCommand(1.0f, 1.0f, fire: true), new TankCommand(-1.0f, 1.0f, fire: true));

        Assert.Equal(MatchPhase.Countdown, session.State.Phase);
        Assert.Equal(start, (one.Position, two.Position));
        Assert.Empty(session.Simulation.Shells);
        Assert.Empty(session.Simulation.Events);
    }

    [Fact]
    public void WhenPlayStartsCommandsTakeEffect()
    {
        var (session, one, _) = Humans();
        EndCountdown(session);
        var start = one.Position;

        RunFor(session, 0.5f, one: new TankCommand(0.0f, 1.0f));

        Assert.Equal(MatchPhase.Playing, session.State.Phase);
        Assert.NotEqual(start, one.Position);
    }

    // Both humans stand where they are, one shell is fired at seat two, whose health is one hit from zero.
    private static void KillSeatTwo(MatchSession session, Player one, Player two)
    {
        two.Health = 1;
        two.Position = one.Position + new Vector2(60.0f, 0.0f);
        one.Heading = 0.0f;
        session.Simulation.Launch(one, new ShellLaunch(Player.DefaultAmmunition, one.Position + new Vector2(20.0f, 0.0f), new Vector2(200.0f, 0.0f)));
    }

    [Fact]
    public void ADestroyedTankScoresAndTheRoundPausesWithNobodyAbleToAct()
    {
        var (session, one, two) = Humans();
        EndCountdown(session);
        KillSeatTwo(session, one, two);
        RunFor(session, 0.5f);
        Assert.Equal(MatchPhase.RoundOver, session.State.Phase);
        Assert.Equal(1, session.State.ScoreOf(Seat.One));
        var heldAt = (one.Position, two.Position, one.Fuel);
        var firedBefore = session.Simulation.Events.Count(e => e.Kind == MatchEventKind.ShellFired);

        RunFor(session, 1.0f, new TankCommand(1.0f, 1.0f, fire: true), new TankCommand(1.0f, 1.0f, fire: true));

        Assert.Equal(MatchPhase.RoundOver, session.State.Phase);
        Assert.Equal(heldAt, (one.Position, two.Position, one.Fuel));
        Assert.Equal(firedBefore, session.Simulation.Events.Count(e => e.Kind == MatchEventKind.ShellFired)); // nobody fired
    }

    [Fact]
    public void ShellsAlreadyInFlightFinishDuringThePause()
    {
        var (session, one, two) = Humans();
        EndCountdown(session);
        KillSeatTwo(session, one, two);
        RunFor(session, 0.5f);
        Assert.Equal(MatchPhase.RoundOver, session.State.Phase);
        // a second shell is still flying when the round ends
        session.Simulation.Launch(two, new ShellLaunch(Player.DefaultAmmunition, one.Position + new Vector2(0.0f, -40.0f), new Vector2(0.0f, -50.0f)));
        var before = session.Simulation.Shells.Single().Position;

        RunFor(session, 0.2f);

        Assert.True(session.Simulation.Shells.Count == 0 || session.Simulation.Shells.Single().Position.Y < before.Y);
    }

    [Fact]
    public void TheNextCountdownResetsTheWorldAndThePauseKeepsTheScore()
    {
        var (session, one, two) = Humans();
        var start = (one.Position, one.Heading, two.Position, two.Heading);
        EndCountdown(session);
        one.Fuel = 5.0f;
        KillSeatTwo(session, one, two);
        RunFor(session, 0.5f);

        RunFor(session, Tuning.Match.RoundOverSeconds);

        Assert.Equal(MatchPhase.Countdown, session.State.Phase);
        Assert.Equal(2, session.State.Round);
        Assert.Equal(1, session.State.ScoreOf(Seat.One));
        Assert.Equal(start, (one.Position, one.Heading, two.Position, two.Heading));
        Assert.Equal(one.MaximumFuel, one.Fuel);
        Assert.Equal(two.MaximumHealth, two.Health);
        Assert.Empty(session.Simulation.Shells);
    }

    [Fact]
    public void StartingANewMatchClearsTheScoreAndTheWorld()
    {
        var (session, one, two) = Humans();
        EndCountdown(session);
        KillSeatTwo(session, one, two);
        RunFor(session, 0.5f);
        one.Fuel = 1.0f;

        session.StartNewMatch();

        Assert.Equal(MatchPhase.Countdown, session.State.Phase);
        Assert.Equal(1, session.State.Round);
        Assert.Equal(0, session.State.ScoreOf(Seat.One));
        Assert.Equal(one.MaximumFuel, one.Fuel);
        Assert.Equal(two.MaximumHealth, two.Health);
    }

    [Fact]
    public void TheSimulationsOwnEventsStillReachTheStage()
    {
        var (session, one, two) = Humans();
        EndCountdown(session);
        KillSeatTwo(session, one, two);

        RunFor(session, 0.5f);

        Assert.Contains(session.Simulation.Events, e => e.Kind == MatchEventKind.TankDestroyed && e.Seat == Seat.Two);
        Assert.Contains(session.State.Events, e => e.Kind == MatchStateEventKind.RoundWon && e.Seat == Seat.One);
    }
}

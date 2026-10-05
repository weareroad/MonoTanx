using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class RunLoggerTests
{
    private static readonly MatchSetup Setup = MatchSetup.OnePlayer; // P1 human, P2 computer
    private const float Step = 1.0f / 60.0f;

    private static (RunLogger Logger, List<string> Lines, MatchState State) NewLog()
    {
        var lines = new List<string>();
        return (new RunLogger(lines.Add), lines, new MatchState());
    }

    private static MatchEvent Decision(Seat seat, ComputerDecisionKind kind, Seat? shooter = Seat.One, float distance = 140.0f, float toHit = 0.43f,
        TankCommand move = default, float draw = 0.0f, float chance = 0.75f, float fuel = 100.0f) =>
        new MatchEvent(MatchEventKind.ComputerDecision, seat, decision: new ComputerDecision(kind, new Vector2(100, 50), fuel, shooter, distance, toHit, move, draw, chance));

    [Fact]
    public void TheHeaderGivesTheSeedTheOptionsAndWhoControlsWhat()
    {
        var (logger, lines, _) = NewLog();

        logger.Header(4242, "--test --seed 4242", Setup, "1.2.3");

        Assert.Contains("# seed 4242  (re-run with --seed 4242 to reproduce it)", lines);
        Assert.Contains("# options --test --seed 4242", lines);
        Assert.Contains("# players P1(human) P2(CPU)", lines);
        Assert.Contains("# version 1.2.3", lines);
        Assert.StartsWith("# MonoTanx run log", lines[0]);
    }

    [Fact]
    public void NoOptionsReadsAsNone()
    {
        var (logger, lines, _) = NewLog();

        logger.Header(1, "", Setup, "x");

        Assert.Contains("# options (none)", lines);
    }

    [Fact]
    public void ADodgeIsLoggedWithTheShellWhoFiredItAndTheMove()
    {
        var (logger, lines, state) = NewLog();

        logger.Record(12.34f, Setup, state, new[] { Decision(Seat.Two, ComputerDecisionKind.Dodging, move: new TankCommand(-1.0f, 1.0f)) });

        var line = Assert.Single(lines);
        Assert.Equal("[00:12.34 R1] P2(CPU) DODGE       shell from P1(human) 140px away, would hit in 0.43s: moving forward, turning left", line);
    }

    [Fact]
    public void AShellItDidNotNoticeShowsTheDrawAndTheChance()
    {
        var (logger, lines, state) = NewLog();

        logger.Record(1.0f, Setup, state, new[] { Decision(Seat.Two, ComputerDecisionKind.ShellNotNoticed, draw: 0.81f, chance: 0.75f) });

        Assert.Contains("NOT-NOTICED", lines[0]);
        Assert.Contains("(draw 0.81 needed under 0.75)", lines[0]);
    }

    [Fact]
    public void AnUnavoidableShellSaysSoAndMentionsNoFuel()
    {
        var (logger, lines, state) = NewLog();

        logger.Record(1.0f, Setup, state, new[]
        {
            Decision(Seat.Two, ComputerDecisionKind.ShellUnavoidable, distance: 30.0f, toHit: 0.1f),
            Decision(Seat.Two, ComputerDecisionKind.ShellUnavoidable, fuel: 0.0f)
        });

        Assert.Contains("UNAVOIDABLE", lines[0]);
        Assert.DoesNotContain("no fuel", lines[0]);
        Assert.Contains("(it has no fuel)", lines[1]);
    }

    [Fact]
    public void AHitSoonAfterADodgeSaysTheDodgeFailed()
    {
        var (logger, lines, state) = NewLog();
        logger.Record(5.0f, Setup, state, new[] { Decision(Seat.Two, ComputerDecisionKind.Dodging, move: new TankCommand(1.0f, 1.0f)) });

        logger.Record(0.4f, Setup, state, new[] { new MatchEvent(MatchEventKind.TankHit, Seat.Two, Seat.One) });

        Assert.Contains("HIT", lines[1]);
        Assert.Contains("hit by a shell from P1(human) (dodge failed: it started dodging 0.40s earlier)", lines[1]);
    }

    [Fact]
    public void ADodgeWithNoHitAfterwardsIsLoggedAsHavingWorked()
    {
        var (logger, lines, state) = NewLog();
        logger.Record(5.0f, Setup, state, new[] { Decision(Seat.Two, ComputerDecisionKind.Dodging, move: new TankCommand(1.0f, 1.0f)) });

        logger.Record(1.0f, Setup, state, new MatchEvent[0]);
        Assert.Single(lines); // not yet
        logger.Record(0.7f, Setup, state, new MatchEvent[0]);

        Assert.Equal(2, lines.Count);
        Assert.Contains("DODGE-OK", lines[1]);
        Assert.Contains("no hit within 1.6s", lines[1]);
    }

    [Fact]
    public void AHitWithNoPrecedingDodgeHasNoDodgeNote()
    {
        var (logger, lines, state) = NewLog();

        logger.Record(1.0f, Setup, state, new[] { new MatchEvent(MatchEventKind.TankHit, Seat.One, Seat.Two) });

        Assert.Equal("[00:01.00 R1] P1(human) HIT         hit by a shell from P2(CPU)", lines[0]);
    }

    [Fact]
    public void ShotsPickupsAndDestroyedTanksAreLogged()
    {
        var (logger, lines, state) = NewLog();

        logger.Record(2.0f, Setup, state, new[]
        {
            new MatchEvent(MatchEventKind.ShellFired, Seat.Two),
            new MatchEvent(MatchEventKind.PickupCollected, Seat.One),
            new MatchEvent(MatchEventKind.TankDestroyed, Seat.One)
        });

        Assert.Contains("P2(CPU) FIRE", lines[0]);
        Assert.Contains("P1(human) PICKUP", lines[1]);
        Assert.Contains("P1(human) DESTROYED", lines[2]);
    }

    [Fact]
    public void StuckIsLoggedWithWhereItWas()
    {
        var (logger, lines, state) = NewLog();

        logger.Record(3.0f, Setup, state, new[] { Decision(Seat.Two, ComputerDecisionKind.Stuck) });

        Assert.Contains("STUCK", lines[0]);
        Assert.Contains("at (100,50)", lines[0]);
    }

    [Fact]
    public void RoundsAndMatchesAreLoggedFromTheMatchState()
    {
        var (logger, lines, _) = NewLog();
        var state = new MatchState(roundsToWin: 1, countdownSeconds: 1.0f, roundOverSeconds: 1.0f, roundTimeLimitSeconds: 60.0f);
        var none = new MatchEvent[0];

        state.Update(1.0f, none); // the countdown ends: play starts
        logger.Record(1.0f, Setup, state, none);
        state.ClearEvents();
        state.Update(0.1f, new[] { new MatchEvent(MatchEventKind.TankDestroyed, Seat.Two) });
        logger.Record(0.1f, Setup, state, none);
        state.ClearEvents();
        state.Update(1.0f, none);
        logger.Record(1.0f, Setup, state, none);

        Assert.Contains(lines, l => l.Contains("ROUND 1 starts"));
        Assert.Contains(lines, l => l.Contains("ROUND 1 won by P1(human) (score 1-0)"));
        Assert.Contains(lines, l => l.Contains("MATCH won by P1(human)"));
    }

    [Fact]
    public void TheSummaryCountsWhatHappened()
    {
        var (logger, lines, state) = NewLog();
        logger.Record(10.0f, Setup, state, new[]
        {
            new MatchEvent(MatchEventKind.ShellFired, Seat.Two),
            new MatchEvent(MatchEventKind.ShellFired, Seat.Two),
            Decision(Seat.Two, ComputerDecisionKind.Dodging, move: new TankCommand(1.0f, 1.0f)),
            Decision(Seat.Two, ComputerDecisionKind.ShellNotNoticed),
            Decision(Seat.Two, ComputerDecisionKind.ShellUnavoidable),
            new MatchEvent(MatchEventKind.TankHit, Seat.Two, Seat.One)
        });
        lines.Clear();

        logger.Summary(Setup);

        Assert.Contains("# summary: 00:10.00 of play, 0 rounds won, 0 drawn, 0 matches finished", lines);
        Assert.Contains("# P2(CPU): 2 shots fired, 1 hits taken, 1 dodges (1 failed), 1 shells not noticed, 1 unavoidable, 0 times stuck", lines);
    }

    [Fact]
    public void LabelsFollowWhoControlsTheSeat()
    {
        var (logger, lines, state) = NewLog();

        logger.Record(1.0f, MatchSetup.Demo, state, new[] { new MatchEvent(MatchEventKind.ShellFired, Seat.One) });
        logger.Record(1.0f, new MatchSetup(PlayerControl.Computer, PlayerControl.Human), state, new[] { new MatchEvent(MatchEventKind.ShellFired, Seat.Two) });

        Assert.Contains("P1(CPU) FIRE", lines[0]);
        Assert.Contains("P2(human) FIRE", lines[1]);
    }
}

// The log of whole matches: it watches without changing them, and a run from a seed logs the same lines.
public class RunLogRunTests
{
    private static MatchSetup Demo => MatchSetup.Demo;

    [Fact]
    public void TheSameSeedWritesTheSameLog()
    {
        var first = new MatchHarness(7, Demo, recordLog: true).Run(120.0f);
        var second = new MatchHarness(7, Demo, recordLog: true).Run(120.0f);

        Assert.Equal(first.LogLines, second.LogLines);
        Assert.True(first.LogLines.Count > 10, "the log is nearly empty");
    }

    [Fact]
    public void DifferentSeedsWriteDifferentLogs()
    {
        var one = new MatchHarness(7, Demo, recordLog: true).Run(120.0f);
        var two = new MatchHarness(8, Demo, recordLog: true).Run(120.0f);

        Assert.NotEqual(one.LogLines.Skip(6), two.LogLines.Skip(6)); // past the header, which names the seed
    }

    [Fact]
    public void LoggingDoesNotChangeHowTheMatchGoes()
    {
        var logged = new MatchHarness(42, Demo, recordLog: true).Run(120.0f);
        var silent = new MatchHarness(42, Demo, recordLog: false).Run(120.0f);

        Assert.Equal(silent.Fingerprint(), logged.Fingerprint());
    }

    [Fact]
    public void AMatchBetweenComputersLogsTheirDodgingAndWhatBecameOfIt()
    {
        var match = new MatchHarness(7, Demo, recordLog: true).Run(600.0f);
        match.FinishLog();
        var text = string.Join("\n", match.LogLines);

        Assert.Contains("FIRE", text);
        Assert.Contains("DODGE ", text);
        Assert.True(text.Contains("DODGE-OK") || text.Contains("dodge failed"), "no dodge was followed up");
        Assert.Contains("# summary:", text);
    }

    [Fact]
    public void EveryDodgeNamesTheShellsGunAndALaterLineSaysHowItWentOrTheRunEnded()
    {
        var match = new MatchHarness(42, Demo, recordLog: true).Run(600.0f);

        var dodges = match.LogLines.Where(line => line.Contains(" DODGE ")).ToList();

        Assert.NotEmpty(dodges);
        Assert.All(dodges, line => Assert.Matches(@"shell from P[12]\((CPU|human)\) \d+px away, would hit in \d\.\d\ds: moving", line));
    }
}

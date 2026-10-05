using Microsoft.Xna.Framework;
using MonoTanx.Core;

namespace MonoTanx.Tests;

// Runs a whole match on the real arena with no graphics device: the simulation
// stepped at the fixed rate for a number of simulated seconds from a seed, with
// every event recorded and a check called after every update. Human seats are
// driven by an optional command source (idle when none is given).
internal sealed class MatchHarness
{
    public const float Step = 1.0f / Tuning.Timing.UpdatesPerSecond;

    // 16px tank sprite: half its width plus the muzzle clearance, as the stage works it out.
    private const float MuzzleOffset = 16.0f / 2.0f + Tuning.Presentation.MuzzleClearance;

    public int Seed { get; }
    public WorldMap Map { get; }
    public Player PlayerOne { get; }
    public Player PlayerTwo { get; }
    public MatchSimulation Simulation { get; }
    public MatchSession Session { get; }
    public List<(int Frame, MatchEventKind Kind, Seat? Seat)> Log { get; } = new();
    public int Frame { get; private set; }

    public MatchHarness(int seed, MatchSetup setup, int roundsToWin = Tuning.Match.RoundsToWin)
    {
        Seed = seed;
        Map = new WorldMap(TestSupport.FixturePath("arena", "arena_01.tmx"));
        PlayerOne = new Player("Player 1", "Sprites/tank", Color.White, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
            isComputerControlled: setup.PlayerOne == PlayerControl.Computer);
        PlayerTwo = new Player("Player 2", "Sprites/tank2", Color.LightGray, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
            isComputerControlled: setup.PlayerTwo == PlayerControl.Computer);
        var streams = new RandomStreams(seed);
        Simulation = new MatchSimulation(Map, PlayerOne, PlayerTwo, Map.PickupSpawns, streams.Gameplay,
            new SimulationSettings(MuzzleOffset, MuzzleOffset), streams.CreateStream("ai-1"), streams.CreateStream("ai-2"));
        Simulation.PlaceAtStart();
        Session = new MatchSession(Simulation, roundsToWin);
    }

    // Runs for the simulated seconds, calling check(harness) after each update.
    // humanCommand (if given) supplies the command for a human seat each update.
    public MatchHarness Run(float seconds, Action<MatchHarness> check = null, Func<MatchHarness, Seat, TankCommand?> humanCommand = null)
    {
        var updates = (int)Math.Round(seconds * Tuning.Timing.UpdatesPerSecond);
        for (var i = 0; i < updates; i++)
        {
            Frame++;
            Simulation.Step(Step, humanCommand?.Invoke(this, Seat.One), humanCommand?.Invoke(this, Seat.Two));
            foreach (var matchEvent in Simulation.Events)
                Log.Add((Frame, matchEvent.Kind, matchEvent.Seat));
            Simulation.ClearEvents();
            check?.Invoke(this);
        }
        return this;
    }

    // Plays the match through its rounds (countdowns, pauses, resets) until it is
    // over or the simulated seconds run out, with the same per-update check. Use
    // this or Run on a harness, not both.
    public MatchHarness RunMatch(float maxSeconds, Action<MatchHarness> check = null)
    {
        var updates = (int)Math.Round(maxSeconds * Tuning.Timing.UpdatesPerSecond);
        for (var i = 0; i < updates && Session.State.Phase != MatchPhase.MatchOver; i++)
        {
            Frame++;
            Session.Step(Step, null, null);
            foreach (var matchEvent in Simulation.Events)
                Log.Add((Frame, matchEvent.Kind, matchEvent.Seat));
            Simulation.ClearEvents();
            foreach (var stateEvent in Session.State.Events)
                StateLog.Add((Frame, stateEvent.Kind, stateEvent.Seat));
            Session.State.ClearEvents();
            check?.Invoke(this);
        }
        return this;
    }

    public List<(int Frame, MatchStateEventKind Kind, Seat? Seat)> StateLog { get; } = new();

    // Plays computer against computer for the simulated seconds without stopping
    // at the end of a match (a finished one is followed by a new one, as in a demo)
    // and reports the numbers that say how the match plays. See BalanceReport.
    public BalanceReport Measure(float seconds)
    {
        var report = new BalanceReport { Seconds = seconds };
        var updates = (int)Math.Round(seconds * Tuning.Timing.UpdatesPerSecond);
        var streak = new int[2];
        var stuckRun = new int[2];
        var lastPosition = new Vector2[2];
        var lastHeading = new float[2];
        var streakStart = new Vector2[2];
        for (var i = 0; i < updates; i++)
        {
            Frame++;
            var playing = Session.State.Phase == MatchPhase.Playing;
            Session.Step(Step, null, null);
            foreach (var matchEvent in Simulation.Events)
            {
                if (matchEvent.Kind == MatchEventKind.TankHit) report.Hits++;
                if (matchEvent.Kind == MatchEventKind.ShellFired) report.ShellsFired++;
            }
            Simulation.ClearEvents();
            foreach (var stateEvent in Session.State.Events)
            {
                if (stateEvent.Kind == MatchStateEventKind.RoundWon) report.RoundsDecided++;
                if (stateEvent.Kind == MatchStateEventKind.RoundDrawn) report.RoundsDrawn++;
                if (stateEvent.Kind == MatchStateEventKind.MatchWon) report.MatchesFinished++;
            }
            Session.State.ClearEvents();

            // a tank asked to drive for a whole window that has hardly moved is stuck
            for (var index = 0; index < 2; index++)
            {
                var tank = Simulation.TankOf((Seat)index);
                var driving = playing && Simulation.ControllerOf((Seat)index).LastMove.Drive != 0.0f;
                if (!driving) { streak[index] = 0; stuckRun[index] = 0; continue; }
                if (streak[index] == 0) streakStart[index] = tank.Position;
                streak[index]++;
                if (streak[index] >= BalanceReport.StuckWindowUpdates)
                {
                    if (Vector2.Distance(streakStart[index], tank.Position) < BalanceReport.StuckMinimumDistance)
                    {
                        report.StuckUpdates++;
                        report.LongestStuckRunUpdates = Math.Max(report.LongestStuckRunUpdates, ++stuckRun[index]);
                    }
                    else
                        stuckRun[index] = 0;
                    if (streak[index] % BalanceReport.StuckWindowUpdates == 0) { streakStart[index] = tank.Position; }
                }
            }

            // a computer tank that stands quite still with the opponent in view and nothing to wait for
            // (not hit a moment ago, not backing away, and its next shot more than a second off) looks frozen
            for (var index = 0; index < 2; index++)
            {
                var tank = Simulation.TankOf((Seat)index);
                var controller = Simulation.ControllerOf((Seat)index);
                if (playing && tank.IsComputerControlled)
                {
                    report.ComputerTankUpdates++;
                    var stood = Vector2.Distance(lastPosition[index], tank.Position) < 0.001f && Math.Abs(lastHeading[index] - tank.Heading) < 0.0001f;
                    var other = Simulation.TankOf((Seat)(1 - index));
                    if (stood && tank.RemainingAmmunition > 0 && controller.RetaliationTimer <= 0.0f && !controller.Recovering
                        && controller.FireTimer > BalanceReport.ShotImminentSeconds && Map.HasLineOfSight(tank.Position, other.Position))
                        report.MotionlessInViewUpdates++;
                }
                lastPosition[index] = tank.Position;
                lastHeading[index] = tank.Heading;
            }

            if (Session.State.Phase == MatchPhase.MatchOver)
                Session.StartNewMatch();
        }
        return report;
    }

    public int Count(MatchEventKind kind, Seat? seat = null) =>
        Log.Count(e => e.Kind == kind && (seat == null || e.Seat == seat));

    // Everything that identifies where a match ended up.
    public string Fingerprint() => string.Join("|",
        Frame, FormatTank(PlayerOne), FormatTank(PlayerTwo), Log.Count, string.Join(",", Log.Select(e => $"{e.Frame}{e.Kind}{e.Seat}")),
        string.Join(",", StateLog.Select(e => $"{e.Frame}{e.Kind}{e.Seat}")));

    private static string FormatTank(Player tank) =>
        $"{tank.Position.X:R},{tank.Position.Y:R},{tank.Heading:R},{tank.Fuel:R},{tank.Health},{tank.RemainingAmmunition}";
}

// How a stretch of computer-versus-computer play went. These are what a change to
// the computer is judged by (see docs/tuning.md).
internal sealed class BalanceReport
{
    // A tank commanded to drive for this many updates (1.5s) that moved less than this far counts as stuck.
    public const int StuckWindowUpdates = 90;
    public const float StuckMinimumDistance = 6.0f;

    // A computer standing still with the opponent in view only counts as frozen when its next shot is more than this far off (seconds).
    public const float ShotImminentSeconds = 1.0f;

    public float Seconds;
    public int Hits;
    public int ShellsFired;
    public int RoundsDecided;
    public int RoundsDrawn;
    public int MatchesFinished;
    public int StuckUpdates;
    public int LongestStuckRunUpdates;
    public int MotionlessInViewUpdates;
    public int ComputerTankUpdates;

    public float HitsPerMinute => Hits / (Seconds / 60.0f);
    public float Accuracy => ShellsFired == 0 ? 0.0f : Hits / (float)ShellsFired;
    public float DecidedShare => RoundsDecided + RoundsDrawn == 0 ? 0.0f : RoundsDecided / (float)(RoundsDecided + RoundsDrawn);
    // Share of the time a computer tank was in play that it stood motionless looking at the opponent with nothing to wait for.
    public float MotionlessInViewShare => ComputerTankUpdates == 0 ? 0.0f : MotionlessInViewUpdates / (float)ComputerTankUpdates;
    public float LongestStuckRunSeconds => LongestStuckRunUpdates / (float)Tuning.Timing.UpdatesPerSecond;
    public float StuckSecondsPerMinute => StuckUpdates / (float)Tuning.Timing.UpdatesPerSecond / (Seconds / 60.0f);

    public override string ToString() =>
        $"{Seconds:0}s: hits {Hits} ({HitsPerMinute:0.0}/min), fired {ShellsFired}, accuracy {Accuracy:P0}, rounds decided {RoundsDecided} drawn {RoundsDrawn} (decided {DecidedShare:P0}), matches {MatchesFinished}, motionless in view {MotionlessInViewShare:P0}, stuck {StuckUpdates} updates ({StuckSecondsPerMinute:0.0}s/min, longest run {LongestStuckRunSeconds:0.0}s)";

    // Several seeds added together.
    public static BalanceReport Sum(IEnumerable<BalanceReport> reports)
    {
        var total = new BalanceReport();
        foreach (var report in reports)
        {
            total.Seconds += report.Seconds;
            total.Hits += report.Hits;
            total.ShellsFired += report.ShellsFired;
            total.RoundsDecided += report.RoundsDecided;
            total.RoundsDrawn += report.RoundsDrawn;
            total.MatchesFinished += report.MatchesFinished;
            total.StuckUpdates += report.StuckUpdates;
            total.MotionlessInViewUpdates += report.MotionlessInViewUpdates;
            total.ComputerTankUpdates += report.ComputerTankUpdates;
            total.LongestStuckRunUpdates = Math.Max(total.LongestStuckRunUpdates, report.LongestStuckRunUpdates);
        }
        return total;
    }
}

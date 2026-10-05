using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MonoTanx.Core
{
    // A log of one run: the seed and setup first, then what happened, with the simulated time and the
    // round, in the order it happened, and a summary at the end. It only watches: it reads the events
    // the simulation and the match already report and writes text lines, so it cannot change how a run
    // goes, and a run from the same seed gives the same lines. Where the lines go (a file, the
    // console, a test's list) is the writer it is given. See docs/run-log-spec.md.
    public sealed class RunLogger
    {
        // A dodge with no hit this soon after it is taken to have worked.
        private const float DodgeCheckSeconds = 1.6f;

        private readonly Action<string> write;
        private readonly (float Time, Seat? Shooter)?[] pendingDodges = new (float, Seat?)?[2];
        private readonly int[] shots = new int[2];
        private readonly int[] hitsTaken = new int[2];
        private readonly int[] dodges = new int[2];
        private readonly int[] dodgesFailed = new int[2];
        private readonly int[] notNoticed = new int[2];
        private readonly int[] unavoidable = new int[2];
        private readonly int[] stuck = new int[2];
        private int roundsWon;
        private int roundsDrawn;
        private int matchesWon;

        public RunLogger(Action<string> write)
        {
            this.write = write;
        }

        // Simulated seconds since the run began (time the game is paused does not count).
        public float Time { get; private set; }

        public void Header(int seed, string arguments, MatchSetup setup, string version, GameSettings settings = null)
        {
            write("# MonoTanx run log");
            write("# version " + version);
            write("# seed " + seed.ToString(CultureInfo.InvariantCulture) + "  (re-run with --seed " + seed.ToString(CultureInfo.InvariantCulture) + " to reproduce it)");
            write("# options " + (string.IsNullOrWhiteSpace(arguments) ? "(none)" : arguments));
            write("# players " + Tag(setup, Seat.One) + " " + Tag(setup, Seat.Two));
            var differences = settings?.Differences().ToList() ?? new List<KeyValuePair<string, float>>();
            write("# settings " + (differences.Count == 0 ? "(all defaults)"
                : string.Join(" ", differences.Select(pair => pair.Key + "=" + pair.Value.ToString("0.#####", CultureInfo.InvariantCulture)))));
            write("# times are simulated seconds; P1 and P2 are the seats, (human) or (CPU) who controls them");
        }

        // Records one update: the time that passed, and what the simulation and the match reported.
        public void Record(float elapsed, MatchSetup setup, MatchState state, IEnumerable<MatchEvent> events)
        {
            Time += elapsed;
            foreach (var matchEvent in events)
                Write(setup, state, matchEvent);
            foreach (var stateEvent in state.Events)
                Write(state, stateEvent, setup);
            ExpireDodges(setup, state);
        }

        public void Summary(MatchSetup setup)
        {
            write("# summary: " + Format(Time) + " of play, " + roundsWon + " rounds won, " + roundsDrawn + " drawn, " + matchesWon + " matches finished");
            for (var index = 0; index < 2; index++)
            {
                var seat = (Seat)index;
                write("# " + Tag(setup, seat) + ": " + shots[index] + " shots fired, " + hitsTaken[index] + " hits taken, "
                    + dodges[index] + " dodges (" + dodgesFailed[index] + " failed), " + notNoticed[index] + " shells not noticed, "
                    + unavoidable[index] + " unavoidable, " + stuck[index] + " times stuck");
            }
        }

        private void Write(MatchSetup setup, MatchState state, MatchEvent matchEvent)
        {
            var seat = matchEvent.Seat;
            switch (matchEvent.Kind)
            {
                case MatchEventKind.ShellFired:
                    shots[(int)seat.Value]++;
                    Line(state, setup, seat, "FIRE", "shell fired");
                    break;
                case MatchEventKind.TankHit:
                    hitsTaken[(int)seat.Value]++;
                    Line(state, setup, seat, "HIT", "hit by a shell from " + SeatName(setup, matchEvent.Source) + HitAfterDodge(seat.Value));
                    break;
                case MatchEventKind.TankDestroyed:
                    Line(state, setup, seat, "DESTROYED", "tank destroyed");
                    break;
                case MatchEventKind.PickupCollected:
                    Line(state, setup, seat, "PICKUP", "collected a pickup");
                    break;
                case MatchEventKind.ComputerDecision:
                    WriteDecision(setup, state, seat.Value, matchEvent.Decision.Value);
                    break;
            }
        }

        private void WriteDecision(MatchSetup setup, MatchState state, Seat seat, ComputerDecision decision)
        {
            var index = (int)seat;
            var shell = "shell from " + SeatName(setup, decision.ShellShooter) + " " + decision.ShellDistance.ToString("0", CultureInfo.InvariantCulture)
                + "px away, would hit in " + decision.SecondsToHit.ToString("0.00", CultureInfo.InvariantCulture) + "s";
            switch (decision.Kind)
            {
                case ComputerDecisionKind.Dodging:
                    dodges[index]++;
                    pendingDodges[index] = (Time, decision.ShellShooter);
                    Line(state, setup, seat, "DODGE", shell + ": moving " + Describe(decision.Move));
                    break;
                case ComputerDecisionKind.ShellNotNoticed:
                    notNoticed[index]++;
                    Line(state, setup, seat, "NOT-NOTICED", shell + ": it did not see it (draw " + decision.Draw.ToString("0.00", CultureInfo.InvariantCulture)
                        + " needed under " + decision.Chance.ToString("0.00", CultureInfo.InvariantCulture) + ")");
                    break;
                case ComputerDecisionKind.ShellUnavoidable:
                    unavoidable[index]++;
                    Line(state, setup, seat, "UNAVOIDABLE", shell + ": no move gets clear in time" + (decision.Fuel <= 0.0f ? " (it has no fuel)" : ""));
                    break;
                case ComputerDecisionKind.Stuck:
                    stuck[index]++;
                    Line(state, setup, seat, "STUCK", "not moving while driving, at (" + decision.Position.X.ToString("0", CultureInfo.InvariantCulture)
                        + "," + decision.Position.Y.ToString("0", CultureInfo.InvariantCulture) + "): backing away");
                    break;
            }
        }

        private void Write(MatchState state, MatchStateEvent stateEvent, MatchSetup setup)
        {
            switch (stateEvent.Kind)
            {
                case MatchStateEventKind.PlayStarted:
                    write("[" + Format(Time) + " R" + state.Round + "] ROUND " + state.Round + " starts");
                    break;
                case MatchStateEventKind.RoundWon:
                    roundsWon++;
                    write("[" + Format(Time) + " R" + state.Round + "] ROUND " + state.Round + " won by " + Tag(setup, stateEvent.Seat.Value)
                        + " (score " + state.ScoreOf(Seat.One) + "-" + state.ScoreOf(Seat.Two) + ")");
                    break;
                case MatchStateEventKind.RoundDrawn:
                    roundsDrawn++;
                    write("[" + Format(Time) + " R" + state.Round + "] ROUND " + state.Round + " drawn");
                    break;
                case MatchStateEventKind.MatchWon:
                    matchesWon++;
                    write("[" + Format(Time) + " R" + state.Round + "] MATCH won by " + Tag(setup, stateEvent.Seat.Value));
                    break;
            }
        }

        // A dodge that nothing hit for a moment afterwards is taken to have worked.
        private void ExpireDodges(MatchSetup setup, MatchState state)
        {
            for (var index = 0; index < 2; index++)
            {
                var pending = pendingDodges[index];
                if (pending.HasValue && Time - pending.Value.Time >= DodgeCheckSeconds)
                {
                    pendingDodges[index] = null;
                    Line(state, setup, (Seat)index, "DODGE-OK", "no hit within " + DodgeCheckSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s of dodging the shell from " + SeatName(setup, pending.Value.Shooter));
                }
            }
        }

        // A hit soon after a dodge means the dodge failed.
        private string HitAfterDodge(Seat seat)
        {
            var pending = pendingDodges[(int)seat];
            if (!pending.HasValue)
                return "";
            pendingDodges[(int)seat] = null;
            dodgesFailed[(int)seat]++;
            return " (dodge failed: it started dodging " + (Time - pending.Value.Time).ToString("0.00", CultureInfo.InvariantCulture) + "s earlier)";
        }

        private void Line(MatchState state, MatchSetup setup, Seat? seat, string what, string text)
        {
            write("[" + Format(Time) + " R" + state.Round + "] " + (seat.HasValue ? Tag(setup, seat.Value) : "--") + " " + what.PadRight(11) + " " + text);
        }

        private static string Describe(TankCommand move)
        {
            var turn = move.Turn < 0.0f ? "turning left" : move.Turn > 0.0f ? "turning right" : "straight";
            var drive = move.Drive < 0.0f ? "reversing" : move.Drive > 0.0f ? "forward" : "not driving";
            return drive + ", " + turn;
        }

        private static string Tag(MatchSetup setup, Seat seat) =>
            (seat == Seat.One ? "P1" : "P2") + (setup.ControlOf(seat) == PlayerControl.Human ? "(human)" : "(CPU)");

        private static string SeatName(MatchSetup setup, Seat? seat) => seat.HasValue ? Tag(setup, seat.Value) : "an unknown gun";

        private static string Format(float seconds)
        {
            var whole = (int)seconds;
            return (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (whole % 60).ToString("00", CultureInfo.InvariantCulture)
                + "." + ((int)((seconds - whole) * 100.0f)).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}

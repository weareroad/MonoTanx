using System;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    // Where a match is in its rounds.
    public enum MatchPhase
    {
        // The pause before a round: nothing moves or fires.
        Countdown,

        // The round is live.
        Playing,

        // A round has been decided; a short pause to show what happened.
        RoundOver,

        // A seat has won enough rounds.
        MatchOver
    }

    public enum MatchStateEventKind
    {
        // The countdown ended and the round is live.
        PlayStarted,

        // A round was won by Seat.
        RoundWon,

        // A round ended with no winner (both tanks destroyed, or time ran out).
        RoundDrawn,

        // A new round's countdown began, so the world should be reset.
        RoundStarting,

        // Seat has won the match.
        MatchWon
    }

    public readonly struct MatchStateEvent
    {
        public MatchStateEventKind Kind { get; }
        public Seat? Seat { get; }

        public MatchStateEvent(MatchStateEventKind kind, Seat? seat = null)
        {
            Kind = kind;
            Seat = seat;
        }
    }

    // The rules of rounds and score: pure, with no tanks, graphics or input. It is
    // told how much time passed and what happened in the simulation, and moves
    // through Countdown, Playing, RoundOver and MatchOver. A destroyed tank scores
    // for the other seat; both destroyed together, or the time limit, is a draw
    // that scores nothing and is replayed.
    public sealed class MatchState
    {
        private readonly int roundsToWin;
        private readonly float countdownSeconds;
        private readonly float roundOverSeconds;
        private readonly float roundTimeLimitSeconds;
        private readonly int[] scores = new int[2];
        private readonly List<MatchStateEvent> events = new List<MatchStateEvent>();
        private Seat? pendingMatchWinner;

        public MatchState(
            int roundsToWin = Tuning.Match.RoundsToWin,
            float countdownSeconds = Tuning.Match.CountdownSeconds,
            float roundOverSeconds = Tuning.Match.RoundOverSeconds,
            float roundTimeLimitSeconds = Tuning.Match.RoundTimeLimitSeconds)
        {
            this.roundsToWin = roundsToWin;
            this.countdownSeconds = countdownSeconds;
            this.roundOverSeconds = roundOverSeconds;
            this.roundTimeLimitSeconds = roundTimeLimitSeconds;
        }

        public MatchPhase Phase { get; private set; } = MatchPhase.Countdown;

        // Seconds spent in the current phase.
        public float PhaseTimer { get; private set; }

        // The round being played or about to be, counting from 1 (a drawn round is
        // replayed as the next number).
        public int Round { get; private set; } = 1;

        public int ScoreOf(Seat seat) => scores[(int)seat];

        // Who won the round that just ended, or null for a draw (or before any round ends).
        public Seat? RoundWinner { get; private set; }

        // Who won the match, once it is over.
        public Seat? Winner { get; private set; }

        public float CountdownRemaining => Phase == MatchPhase.Countdown ? Math.Max(0.0f, countdownSeconds - PhaseTimer) : 0.0f;

        // Seconds left before a live round is a draw on the time limit; 0 outside a live round.
        public float RoundTimeRemaining => Phase == MatchPhase.Playing ? Math.Max(0.0f, roundTimeLimitSeconds - PhaseTimer) : 0.0f;

        // What to flash on screen about the round's time, or null for nothing: "60s remaining"
        // for a few seconds when a long enough round reaches that mark, then in the last
        // seconds the whole number left as a countdown. Only during a live round.
        public RoundTimerMessage? TimerMessage
        {
            get
            {
                if (Phase != MatchPhase.Playing)
                    return null;
                var remaining = RoundTimeRemaining;
                if (remaining <= Tuning.Match.FinalCountdownSeconds)
                    return new RoundTimerMessage(((int)Math.Ceiling(remaining)).ToString(System.Globalization.CultureInfo.InvariantCulture), isCountdown: true);
                if (roundTimeLimitSeconds > Tuning.Match.TimeWarningSeconds
                    && remaining <= Tuning.Match.TimeWarningSeconds && remaining > Tuning.Match.TimeWarningSeconds - Tuning.Match.TimeWarningShownSeconds)
                    return new RoundTimerMessage(((int)Tuning.Match.TimeWarningSeconds) + "s remaining", isCountdown: false);
                return null;
            }
        }

        // What has happened since the stage last cleared the list.
        public IReadOnlyList<MatchStateEvent> Events => events;

        public void ClearEvents() => events.Clear();

        // Advances the match by the elapsed time. simulationEvents are what the
        // simulation reported this update; only a live round looks at them.
        public void Update(float elapsed, IEnumerable<MatchEvent> simulationEvents)
        {
            PhaseTimer += elapsed;
            switch (Phase)
            {
                case MatchPhase.Countdown:
                    if (PhaseTimer >= countdownSeconds)
                    {
                        Enter(MatchPhase.Playing);
                        events.Add(new MatchStateEvent(MatchStateEventKind.PlayStarted));
                    }
                    break;
                case MatchPhase.Playing:
                    UpdatePlaying(simulationEvents);
                    break;
                case MatchPhase.RoundOver:
                    if (PhaseTimer >= roundOverSeconds)
                        EndRoundOverPause();
                    break;
            }
        }

        private void UpdatePlaying(IEnumerable<MatchEvent> simulationEvents)
        {
            var oneDestroyed = false;
            var twoDestroyed = false;
            foreach (var matchEvent in simulationEvents)
            {
                if (matchEvent.Kind != MatchEventKind.TankDestroyed) continue;
                if (matchEvent.Seat == Seat.One) oneDestroyed = true;
                if (matchEvent.Seat == Seat.Two) twoDestroyed = true;
            }

            if (oneDestroyed && twoDestroyed)
                EndRound(null);
            else if (oneDestroyed)
                EndRound(Seat.Two);
            else if (twoDestroyed)
                EndRound(Seat.One);
            else if (PhaseTimer >= roundTimeLimitSeconds)
                EndRound(null);
        }

        private void EndRound(Seat? winner)
        {
            RoundWinner = winner;
            Enter(MatchPhase.RoundOver);
            if (winner == null)
            {
                events.Add(new MatchStateEvent(MatchStateEventKind.RoundDrawn));
                return;
            }
            scores[(int)winner.Value]++;
            events.Add(new MatchStateEvent(MatchStateEventKind.RoundWon, winner));
            if (scores[(int)winner.Value] >= roundsToWin)
                pendingMatchWinner = winner;
        }

        private void EndRoundOverPause()
        {
            if (pendingMatchWinner != null)
            {
                Winner = pendingMatchWinner;
                Enter(MatchPhase.MatchOver);
                events.Add(new MatchStateEvent(MatchStateEventKind.MatchWon, Winner));
                return;
            }
            Round++;
            Enter(MatchPhase.Countdown);
            events.Add(new MatchStateEvent(MatchStateEventKind.RoundStarting));
        }

        private void Enter(MatchPhase phase)
        {
            Phase = phase;
            PhaseTimer = 0.0f;
        }
    }

    // Something to show about the time left in a round: the text, and whether it is one of
    // the final countdown's numbers (shown large, like the 3-2-1) or a message (smaller).
    public readonly struct RoundTimerMessage
    {
        public string Text { get; }
        public bool IsCountdown { get; }

        public RoundTimerMessage(string text, bool isCountdown)
        {
            Text = text;
            IsCountdown = isCountdown;
        }
    }
}

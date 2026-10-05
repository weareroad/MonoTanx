using System.Linq;

namespace MonoTanx.Core
{
    // A whole match: the simulation and the rounds rules together, so a match can
    // be played headlessly. It decides what the simulation may do in each phase
    // (nothing during the countdown, only shells in flight while a result sinks in,
    // everything while a round is live), feeds the rounds rules what
    // happened, and resets the world when the next round's countdown begins.
    public sealed class MatchSession
    {
        private readonly GameSettings settings;
        private readonly int roundsToWin;

        public MatchSession(MatchSimulation simulation, int roundsToWin = Tuning.Match.RoundsToWin)
            : this(simulation, roundsToWin, new GameSettings())
        {
        }

        // The rounds, pauses and time limit come from the settings.
        public MatchSession(MatchSimulation simulation, GameSettings settings)
            : this(simulation, settings.GetWhole(SettingKeys.RoundsToWin), settings)
        {
        }

        private MatchSession(MatchSimulation simulation, int roundsToWin, GameSettings settings)
        {
            Simulation = simulation;
            this.settings = settings;
            this.roundsToWin = roundsToWin;
            State = NewState();
        }

        private MatchState NewState() => new MatchState(roundsToWin,
            settings.Get(SettingKeys.CountdownSeconds), settings.Get(SettingKeys.RoundOverSeconds), settings.Get(SettingKeys.RoundTimeLimitSeconds));

        public MatchSimulation Simulation { get; }
        public MatchState State { get; private set; }

        // Advances the match one update. The commands are for human seats (null for
        // a computer seat), as for MatchSimulation.Step; they are ignored when no
        // round is live.
        public void Step(float elapsed, TankCommand? commandOne, TankCommand? commandTwo)
        {
            var eventsBefore = Simulation.Events.Count;
            switch (State.Phase)
            {
                case MatchPhase.Playing:
                    Simulation.Step(elapsed, commandOne, commandTwo);
                    break;
                case MatchPhase.RoundOver:
                case MatchPhase.MatchOver:
                    Simulation.StepShells(elapsed);
                    break;
            }

            var phaseBefore = State.Phase;
            State.Update(elapsed, Simulation.Events.Skip(eventsBefore));
            if (phaseBefore == MatchPhase.RoundOver && State.Phase == MatchPhase.Countdown)
                Simulation.ResetRound(); // the next round's countdown has begun
        }

        // Begins a new match with the same seats and controllers (Play again, or a
        // demo starting its next one).
        public void StartNewMatch()
        {
            Simulation.ResetRound();
            State = NewState();
        }
    }
}

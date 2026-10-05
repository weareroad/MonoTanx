using System.Linq;

namespace MonoTanx.Core
{
    // A whole match: the simulation and the rounds rules together, so a match can
    // be played headlessly. It decides what the simulation may do in each phase
    // (nothing during the countdown, only shells in flight while a round's result
    // sinks in, everything while a round is live), feeds the rounds rules what
    // happened, and resets the world when the next round's countdown begins.
    public sealed class MatchSession
    {
        public MatchSession(MatchSimulation simulation)
        {
            Simulation = simulation;
            State = new MatchState();
        }

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
            State = new MatchState();
        }
    }
}

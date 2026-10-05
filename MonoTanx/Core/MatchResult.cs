using System;

namespace MonoTanx.Core
{
    // How a finished match ended, with the words for the end screen. The seats are
    // named from who controls them (MatchSetup.LabelOf), so nothing here assumes
    // Player 1 is human or Player 2 is the computer.
    public readonly struct MatchResult
    {
        public Seat Winner { get; }
        public int ScoreOne { get; }
        public int ScoreTwo { get; }

        // Rounds played, drawn rounds included.
        public int Rounds { get; }

        public MatchResult(Seat winner, int scoreOne, int scoreTwo, int rounds)
        {
            Winner = winner;
            ScoreOne = scoreOne;
            ScoreTwo = scoreTwo;
            Rounds = rounds;
        }

        // The result of a match that is over.
        public static MatchResult From(MatchState state)
        {
            if (state.Winner == null)
                throw new InvalidOperationException("The match is not over.");
            return new MatchResult(state.Winner.Value, state.ScoreOf(Seat.One), state.ScoreOf(Seat.Two), state.Round);
        }

        public string WinnerText(MatchSetup setup) => setup.LabelOf(Winner) + " wins!";

        public string ScoreText(MatchSetup setup) => $"{setup.LabelOf(Seat.One)} {ScoreOne} - {ScoreTwo} {setup.LabelOf(Seat.Two)}";

        public string RoundsText => Rounds == 1 ? "1 round played" : Rounds + " rounds played";
    }
}

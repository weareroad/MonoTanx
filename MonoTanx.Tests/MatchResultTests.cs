using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class MatchResultTests
{
    private static readonly MatchEvent[] Nothing = new MatchEvent[0];

    // A match the winner takes by winning every decided round, after some drawn ones.
    private static MatchState WonBy(Seat winner, int roundsToWin = 2, int drawsFirst = 0)
    {
        var state = new MatchState(roundsToWin, countdownSeconds: 1.0f, roundOverSeconds: 1.0f, roundTimeLimitSeconds: 10.0f);
        var loser = winner == Seat.One ? Seat.Two : Seat.One;
        for (var round = 0; round < drawsFirst + roundsToWin; round++)
        {
            state.Update(1.0f, Nothing); // countdown
            if (round < drawsFirst)
                state.Update(10.0f, Nothing); // a draw on the time limit
            else
                state.Update(0.1f, new[] { new MatchEvent(MatchEventKind.TankDestroyed, loser) });
            state.Update(1.0f, Nothing); // the pause
        }
        return state;
    }

    [Fact]
    public void ItCapturesTheWinnerScoreAndRoundsOfAFinishedMatch()
    {
        var state = WonBy(Seat.One, roundsToWin: 2);
        Assert.Equal(MatchPhase.MatchOver, state.Phase);

        var result = MatchResult.From(state);

        Assert.Equal(Seat.One, result.Winner);
        Assert.Equal(state.ScoreOf(Seat.One), result.ScoreOne);
        Assert.Equal(state.ScoreOf(Seat.Two), result.ScoreTwo);
        Assert.Equal(state.Round, result.Rounds);
        Assert.Equal(2, result.ScoreOne);
        Assert.Equal(0, result.ScoreTwo);
    }

    [Fact]
    public void DrawnRoundsCountAsRoundsPlayed()
    {
        var withoutDraws = MatchResult.From(WonBy(Seat.Two, roundsToWin: 1));
        var withDraws = MatchResult.From(WonBy(Seat.Two, roundsToWin: 1, drawsFirst: 2));

        Assert.Equal(withoutDraws.Rounds + 2, withDraws.Rounds);
        Assert.Equal(withoutDraws.ScoreOne, withDraws.ScoreOne);
    }

    [Fact]
    public void ItCannotDescribeAMatchThatIsNotOver()
    {
        Assert.Throws<InvalidOperationException>(() => MatchResult.From(new MatchState()));
    }

    [Fact]
    public void TheWordsNameTheSeatsFromTheirControllers()
    {
        var result = new MatchResult(Seat.Two, 1, 3, 5);

        Assert.Equal("CPU wins!", result.WinnerText(MatchSetup.OnePlayer));
        Assert.Equal("P1 1 - 3 CPU", result.ScoreText(MatchSetup.OnePlayer));
        Assert.Equal("P2 wins!", result.WinnerText(MatchSetup.TwoPlayer));
        Assert.Equal("C2 wins!", result.WinnerText(MatchSetup.Demo));
        Assert.Equal("C1 1 - 3 C2", result.ScoreText(MatchSetup.Demo));
    }

    [Fact]
    public void TheWordsWorkWithTheSeatsSwapped()
    {
        var computerFirst = new MatchSetup(PlayerControl.Computer, PlayerControl.Human);
        var result = new MatchResult(Seat.One, 3, 0, 3);

        Assert.Equal("CPU wins!", result.WinnerText(computerFirst));
        Assert.Equal("CPU 3 - 0 P2", result.ScoreText(computerFirst));
    }

    [Theory]
    [InlineData(1, "1 round played")]
    [InlineData(2, "2 rounds played")]
    [InlineData(12, "12 rounds played")]
    public void RoundsReadNaturally(int rounds, string text)
    {
        Assert.Equal(text, new MatchResult(Seat.One, 1, 0, rounds).RoundsText);
    }
}

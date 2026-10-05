using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class MatchStateTests
{
    private static readonly MatchEvent[] Nothing = new MatchEvent[0];

    private static MatchEvent Destroyed(Seat seat) => new MatchEvent(MatchEventKind.TankDestroyed, seat);

    // Short, round numbers so the tests read easily.
    private static MatchState NewState(int roundsToWin = 3) => new MatchState(roundsToWin, countdownSeconds: 3.0f, roundOverSeconds: 2.0f, roundTimeLimitSeconds: 60.0f);

    private static MatchState InPlay(int roundsToWin = 3)
    {
        var state = NewState(roundsToWin);
        state.Update(3.0f, Nothing);
        Assert.Equal(MatchPhase.Playing, state.Phase);
        state.ClearEvents();
        return state;
    }

    // Plays one decided round and its pause, ending in the next countdown (or the match's end).
    private static void WinRound(MatchState state, Seat winner)
    {
        state.Update(0.1f, new[] { Destroyed(winner == Seat.One ? Seat.Two : Seat.One) });
        state.Update(2.0f, Nothing);
        if (state.Phase == MatchPhase.Countdown)
            state.Update(3.0f, Nothing);
    }

    private static List<MatchStateEventKind> Kinds(MatchState state) => state.Events.Select(e => e.Kind).ToList();

    [Fact]
    public void ItStartsInTheCountdownOfRoundOneWithNoScore()
    {
        var state = NewState();

        Assert.Equal(MatchPhase.Countdown, state.Phase);
        Assert.Equal(1, state.Round);
        Assert.Equal(0, state.ScoreOf(Seat.One));
        Assert.Equal(0, state.ScoreOf(Seat.Two));
        Assert.Null(state.Winner);
        Assert.Equal(3.0f, state.CountdownRemaining);
    }

    [Fact]
    public void TheCountdownRunsDownThenPlayStarts()
    {
        var state = NewState();

        state.Update(1.0f, Nothing);
        Assert.Equal(MatchPhase.Countdown, state.Phase);
        Assert.Equal(2.0f, state.CountdownRemaining, 3);

        state.Update(2.0f, Nothing);
        Assert.Equal(MatchPhase.Playing, state.Phase);
        Assert.Equal(new[] { MatchStateEventKind.PlayStarted }, Kinds(state));
        Assert.Equal(0.0f, state.CountdownRemaining);
    }

    [Theory]
    [InlineData(Seat.One, Seat.Two)]
    [InlineData(Seat.Two, Seat.One)]
    public void ADestroyedTankScoresForTheOtherSeat(Seat destroyed, Seat scorer)
    {
        var state = InPlay();

        state.Update(0.1f, new[] { Destroyed(destroyed) });

        Assert.Equal(MatchPhase.RoundOver, state.Phase);
        Assert.Equal(1, state.ScoreOf(scorer));
        Assert.Equal(0, state.ScoreOf(destroyed));
        Assert.Equal(scorer, state.RoundWinner);
        var won = Assert.Single(state.Events);
        Assert.Equal(MatchStateEventKind.RoundWon, won.Kind);
        Assert.Equal(scorer, won.Seat);
    }

    [Fact]
    public void OtherSimulationEventsDoNotEndTheRound()
    {
        var state = InPlay();

        state.Update(0.1f, new[]
        {
            new MatchEvent(MatchEventKind.TankHit, Seat.One),
            new MatchEvent(MatchEventKind.ShellFired, Seat.Two),
            new MatchEvent(MatchEventKind.PickupCollected, Seat.One)
        });

        Assert.Equal(MatchPhase.Playing, state.Phase);
        Assert.Empty(state.Events);
    }

    [Fact]
    public void BothTanksDestroyedInTheSameUpdateIsADrawThatScoresNothing()
    {
        var state = InPlay();

        state.Update(0.1f, new[] { Destroyed(Seat.One), Destroyed(Seat.Two) });

        Assert.Equal(MatchPhase.RoundOver, state.Phase);
        Assert.Equal(0, state.ScoreOf(Seat.One));
        Assert.Equal(0, state.ScoreOf(Seat.Two));
        Assert.Null(state.RoundWinner);
        Assert.Equal(new[] { MatchStateEventKind.RoundDrawn }, Kinds(state));
    }

    [Fact]
    public void TheTimeLimitEndsARoundAsADraw()
    {
        var state = InPlay();

        state.Update(59.0f, Nothing);
        Assert.Equal(MatchPhase.Playing, state.Phase);
        state.Update(1.0f, Nothing);

        Assert.Equal(MatchPhase.RoundOver, state.Phase);
        Assert.Null(state.RoundWinner);
        Assert.Equal(new[] { MatchStateEventKind.RoundDrawn }, Kinds(state));
        Assert.Equal(0, state.ScoreOf(Seat.One) + state.ScoreOf(Seat.Two));
    }

    [Fact]
    public void ADestroyedTankOnTheFinalUpdateBeatsTheTimeLimit()
    {
        var state = InPlay();
        state.Update(59.0f, Nothing);

        state.Update(1.0f, new[] { Destroyed(Seat.Two) });

        Assert.Equal(Seat.One, state.RoundWinner);
    }

    [Fact]
    public void ADrawnRoundIsReplayedAsTheNextRound()
    {
        var state = InPlay();
        state.Update(60.0f, Nothing);
        state.ClearEvents();

        state.Update(2.0f, Nothing);

        Assert.Equal(MatchPhase.Countdown, state.Phase);
        Assert.Equal(2, state.Round);
        Assert.Equal(new[] { MatchStateEventKind.RoundStarting }, Kinds(state));
        Assert.Equal(0, state.ScoreOf(Seat.One) + state.ScoreOf(Seat.Two));
    }

    [Fact]
    public void TheNextRoundStartsAfterThePauseAndGoesThroughTheCountdownAgain()
    {
        var state = InPlay();
        state.Update(0.1f, new[] { Destroyed(Seat.Two) });

        state.Update(1.9f, Nothing);
        Assert.Equal(MatchPhase.RoundOver, state.Phase);
        state.Update(0.1f, Nothing);

        Assert.Equal(MatchPhase.Countdown, state.Phase);
        Assert.Equal(2, state.Round);
        Assert.Equal(3.0f, state.CountdownRemaining);
    }

    [Fact]
    public void DestroyedTanksOutsideALiveRoundAreIgnored()
    {
        var state = NewState();
        state.Update(1.0f, new[] { Destroyed(Seat.One) }); // during the countdown
        Assert.Equal(MatchPhase.Countdown, state.Phase);

        state.Update(2.0f, Nothing);
        state.Update(0.1f, new[] { Destroyed(Seat.One) }); // round decided: seat two scores
        state.Update(1.0f, new[] { Destroyed(Seat.Two) }); // a late shell during the pause counts for nothing

        Assert.Equal(1, state.ScoreOf(Seat.Two));
        Assert.Equal(0, state.ScoreOf(Seat.One));
    }

    [Theory]
    [InlineData(Seat.One)]
    [InlineData(Seat.Two)]
    public void ReachingTheTargetWinsTheMatchAfterThePause(Seat winner)
    {
        var state = InPlay();
        WinRound(state, winner);
        WinRound(state, winner == Seat.One ? Seat.Two : Seat.One); // the other seat scores in between
        WinRound(state, winner);
        state.Update(0.1f, new[] { Destroyed(winner == Seat.One ? Seat.Two : Seat.One) });
        Assert.Equal(MatchPhase.RoundOver, state.Phase);
        Assert.Null(state.Winner);
        state.ClearEvents();

        state.Update(2.0f, Nothing);

        Assert.Equal(MatchPhase.MatchOver, state.Phase);
        Assert.Equal(winner, state.Winner);
        Assert.Equal(3, state.ScoreOf(winner));
        Assert.Equal(1, state.ScoreOf(winner == Seat.One ? Seat.Two : Seat.One));
        var matchWon = Assert.Single(state.Events);
        Assert.Equal(MatchStateEventKind.MatchWon, matchWon.Kind);
        Assert.Equal(winner, matchWon.Seat);
    }

    [Fact]
    public void ADrawDoesNotCountTowardTheTarget()
    {
        var state = InPlay(roundsToWin: 2);
        WinRound(state, Seat.One);
        state.Update(60.0f, Nothing); // a drawn round
        state.Update(2.0f, Nothing);
        state.Update(3.0f, Nothing);
        Assert.Equal(MatchPhase.Playing, state.Phase);

        Assert.Equal(1, state.ScoreOf(Seat.One));
        Assert.Equal(3, state.Round);
        Assert.Null(state.Winner);
    }

    [Fact]
    public void AMatchCanBeFirstToOne()
    {
        var state = InPlay(roundsToWin: 1);

        state.Update(0.1f, new[] { Destroyed(Seat.Two) });
        state.Update(2.0f, Nothing);

        Assert.Equal(MatchPhase.MatchOver, state.Phase);
        Assert.Equal(Seat.One, state.Winner);
    }

    [Fact]
    public void ANewMatchStartsClean()
    {
        var finished = InPlay(roundsToWin: 1);
        finished.Update(0.1f, new[] { Destroyed(Seat.Two) });
        finished.Update(2.0f, Nothing);

        var fresh = new MatchState();

        Assert.Equal(MatchPhase.MatchOver, finished.Phase);
        Assert.Equal(MatchPhase.Countdown, fresh.Phase);
        Assert.Equal(0, fresh.ScoreOf(Seat.One));
        Assert.Null(fresh.Winner);
    }

    [Fact]
    public void AFinishedMatchStaysOverAndCountsHowLongItHasBeenOver()
    {
        var state = InPlay(roundsToWin: 1);
        state.Update(0.1f, new[] { Destroyed(Seat.Two) });
        state.Update(2.0f, Nothing);

        state.Update(4.0f, new[] { Destroyed(Seat.One) });

        Assert.Equal(MatchPhase.MatchOver, state.Phase);
        Assert.Equal(4.0f, state.PhaseTimer, 3);
        Assert.Equal(Seat.One, state.Winner);
    }

    [Fact]
    public void TheDefaultsComeFromTuning()
    {
        var state = new MatchState();
        state.Update(Tuning.Match.CountdownSeconds, Nothing);

        Assert.Equal(MatchPhase.Playing, state.Phase);
        state.Update(Tuning.Match.RoundTimeLimitSeconds, Nothing);
        Assert.Equal(MatchPhase.RoundOver, state.Phase);
    }

    private static MatchState InPlayWithLimit(float limitSeconds)
    {
        var state = new MatchState(3, countdownSeconds: 3.0f, roundOverSeconds: 2.0f, roundTimeLimitSeconds: limitSeconds);
        state.Update(3.0f, Nothing);
        return state;
    }

    // Plays the live round forward to this many seconds into it.
    private static void PlayTo(MatchState state, float seconds, float step = 0.05f)
    {
        while (state.Phase == MatchPhase.Playing && state.PhaseTimer < seconds - 1e-4f)
            state.Update(step, Nothing);
    }

    [Fact]
    public void NoTimerMessageOutsideALiveRound()
    {
        var state = new MatchState(3, 3.0f, 2.0f, 90.0f);

        Assert.Null(state.TimerMessage);          // countdown
        Assert.Equal(0.0f, state.RoundTimeRemaining);
        state.Update(3.0f, Nothing);
        PlayTo(state, 85.0f);
        state.Update(0.1f, new[] { Destroyed(Seat.Two) });
        Assert.Equal(MatchPhase.RoundOver, state.Phase);
        Assert.Null(state.TimerMessage);          // round over
    }

    [Fact]
    public void SixtySecondsRemainingShowsForThreeSecondsThenGoes()
    {
        var state = InPlayWithLimit(90.0f);

        PlayTo(state, 29.0f);
        Assert.Null(state.TimerMessage);
        PlayTo(state, 30.5f);                     // 59.5s left
        Assert.Equal("60s remaining", state.TimerMessage?.Text);
        Assert.False(state.TimerMessage?.IsCountdown);
        PlayTo(state, 32.5f);
        Assert.Equal("60s remaining", state.TimerMessage?.Text);
        PlayTo(state, 33.5f);
        Assert.Null(state.TimerMessage);
    }

    [Theory]
    [InlineData(60.0f)]
    [InlineData(45.0f)]
    [InlineData(20.0f)]
    public void ALimitOfSixtySecondsOrLessHasNoSixtySecondMessage(float limit)
    {
        var state = InPlayWithLimit(limit);

        for (var seconds = 0.5f; seconds < limit - 10.5f; seconds += 0.5f)
        {
            PlayTo(state, seconds);
            Assert.Null(state.TimerMessage);
        }
    }

    [Fact]
    public void TheLastTenSecondsCountDownInWholeSecondsWhilePlayContinues()
    {
        var state = InPlayWithLimit(90.0f);

        PlayTo(state, 79.5f);
        Assert.Null(state.TimerMessage);
        PlayTo(state, 80.5f);                     // 9.5s left
        Assert.Equal("10", state.TimerMessage?.Text);
        Assert.True(state.TimerMessage?.IsCountdown);
        PlayTo(state, 81.5f);
        Assert.Equal("9", state.TimerMessage?.Text);
        PlayTo(state, 88.5f);
        Assert.Equal("2", state.TimerMessage?.Text);
        PlayTo(state, 89.5f);
        Assert.Equal("1", state.TimerMessage?.Text);
        Assert.Equal(MatchPhase.Playing, state.Phase);
    }

    [Fact]
    public void AShortRoundCountsDownFromItsStart()
    {
        var state = InPlayWithLimit(8.0f);

        Assert.Equal("8", state.TimerMessage?.Text);
        PlayTo(state, 2.5f);
        Assert.Equal("6", state.TimerMessage?.Text);
    }

    [Fact]
    public void TheCountdownEndsWithTheRoundAsADraw()
    {
        var state = InPlayWithLimit(30.0f);

        PlayTo(state, 30.0f);

        Assert.Equal(MatchPhase.RoundOver, state.Phase);
        Assert.Null(state.TimerMessage);
    }

    [Fact]
    public void TheMessagesFitInsideTheDefaultRound()
    {
        Assert.True(Tuning.Match.RoundTimeLimitSeconds > Tuning.Match.TimeWarningSeconds);
        Assert.True(Tuning.Match.TimeWarningSeconds - Tuning.Match.TimeWarningShownSeconds > Tuning.Match.FinalCountdownSeconds,
            "the 60s message must be over before the final countdown starts");
    }
}

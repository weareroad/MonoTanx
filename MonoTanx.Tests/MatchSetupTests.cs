using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class MatchSetupTests
{
    private static readonly PlayerControl[] Controls = { PlayerControl.Human, PlayerControl.Computer };

    public static IEnumerable<object[]> AllSetups() =>
        from one in Controls from two in Controls select new object[] { new MatchSetup(one, two) };

    [Fact]
    public void TheNamedSetupsAssignControlToEachSeat()
    {
        Assert.Equal(PlayerControl.Human, MatchSetup.OnePlayer.PlayerOne);
        Assert.Equal(PlayerControl.Computer, MatchSetup.OnePlayer.PlayerTwo);
        Assert.Equal(PlayerControl.Human, MatchSetup.TwoPlayer.PlayerOne);
        Assert.Equal(PlayerControl.Human, MatchSetup.TwoPlayer.PlayerTwo);
        Assert.Equal(PlayerControl.Computer, MatchSetup.Demo.PlayerOne);
        Assert.Equal(PlayerControl.Computer, MatchSetup.Demo.PlayerTwo);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public void ControlOfReadsEachSeat(MatchSetup setup)
    {
        Assert.Equal(setup.PlayerOne, setup.ControlOf(Seat.One));
        Assert.Equal(setup.PlayerTwo, setup.ControlOf(Seat.Two));
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public void WithChangesOnlyTheNamedSeat(MatchSetup setup)
    {
        foreach (var control in Controls)
        {
            var changedOne = setup.With(Seat.One, control);
            Assert.Equal(control, changedOne.PlayerOne);
            Assert.Equal(setup.PlayerTwo, changedOne.PlayerTwo);

            var changedTwo = setup.With(Seat.Two, control);
            Assert.Equal(setup.PlayerOne, changedTwo.PlayerOne);
            Assert.Equal(control, changedTwo.PlayerTwo);
        }
    }

    [Fact]
    public void HumanCountIsTheNumberOfHumanSeats()
    {
        Assert.Equal(1, MatchSetup.OnePlayer.HumanCount);
        Assert.Equal(2, MatchSetup.TwoPlayer.HumanCount);
        Assert.Equal(0, MatchSetup.Demo.HumanCount);
        Assert.Equal(1, new MatchSetup(PlayerControl.Computer, PlayerControl.Human).HumanCount);
    }

    [Fact]
    public void TheHomeScreenToggleFlipsBetweenOneAndTwoPlayers()
    {
        Assert.Equal(MatchSetup.TwoPlayer, MatchSetup.OnePlayer.ToggleHumanPlayers());
        Assert.Equal(MatchSetup.OnePlayer, MatchSetup.TwoPlayer.ToggleHumanPlayers());
        Assert.Equal(MatchSetup.OnePlayer, MatchSetup.Demo.ToggleHumanPlayers()); // a demo becomes one player
    }

    [Theory]
    [InlineData(0, "Players: 0")]
    [InlineData(1, "Players: 1")]
    [InlineData(2, "Players: 2")]
    public void LabelsTheNumberOfHumanPlayers(int humans, string expected)
    {
        var setup = humans switch
        {
            0 => MatchSetup.Demo,
            1 => MatchSetup.OnePlayer,
            _ => MatchSetup.TwoPlayer
        };

        Assert.Equal(expected, setup.Label);
    }

    // The follow camera tracks the first human seat, whichever seat that is.
    [Fact]
    public void FollowSeatIsTheFirstHumanSeat()
    {
        Assert.Equal(Seat.One, MatchSetup.OnePlayer.FollowSeat);
        Assert.Equal(Seat.One, MatchSetup.TwoPlayer.FollowSeat);
        Assert.Equal(Seat.Two, new MatchSetup(PlayerControl.Computer, PlayerControl.Human).FollowSeat);
        Assert.Equal(Seat.One, MatchSetup.Demo.FollowSeat);
    }

    [Fact]
    public void OnlyASingleHumanStartsWithTheFollowCamera()
    {
        Assert.False(MatchSetup.OnePlayer.StartsInOverview);
        Assert.False(new MatchSetup(PlayerControl.Computer, PlayerControl.Human).StartsInOverview);
        Assert.True(MatchSetup.TwoPlayer.StartsInOverview);   // neither human should be favoured
        Assert.True(MatchSetup.Demo.StartsInOverview);        // nobody to follow
    }

    // The tenet: nothing may depend on which seat is which. Swapping the seats must
    // swap the answers, never change them.
    [Theory]
    [MemberData(nameof(AllSetups))]
    public void SwappingTheSeatsSwapsTheAnswers(MatchSetup setup)
    {
        var swapped = new MatchSetup(setup.PlayerTwo, setup.PlayerOne);

        Assert.Equal(setup.HumanCount, swapped.HumanCount);
        Assert.Equal(setup.StartsInOverview, swapped.StartsInOverview);
        Assert.Equal(setup.Label, swapped.Label);
        Assert.Equal(setup.ControlOf(Seat.One), swapped.ControlOf(Seat.Two));
        Assert.Equal(setup.ControlOf(Seat.Two), swapped.ControlOf(Seat.One));
        if (setup.HumanCount == 1)
            Assert.NotEqual(setup.FollowSeat, swapped.FollowSeat); // the single human's seat moved, so the camera follows it
    }

    [Fact]
    public void SetupsCompareByTheirControls()
    {
        Assert.Equal(new MatchSetup(PlayerControl.Human, PlayerControl.Computer), MatchSetup.OnePlayer);
        Assert.NotEqual(MatchSetup.OnePlayer, MatchSetup.TwoPlayer);
        Assert.Equal(MatchSetup.Demo.GetHashCode(), new MatchSetup(PlayerControl.Computer, PlayerControl.Computer).GetHashCode());
    }
}

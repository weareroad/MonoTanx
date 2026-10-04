namespace MonoTanx.Core
{
    // The two places at the table. Player 1 and Player 2 are seats; who (or what)
    // controls a seat is a separate question, see PlayerControl.
    public enum Seat
    {
        One,
        Two
    }

    // Who controls a seat. Any combination across the two seats is valid.
    public enum PlayerControl
    {
        Human,
        Computer
    }

    // Who controls each seat in a match. One player is a human against the
    // computer, two players is two humans, and a demo is two computers.
    public readonly struct MatchSetup
    {
        public PlayerControl PlayerOne { get; }
        public PlayerControl PlayerTwo { get; }

        public MatchSetup(PlayerControl playerOne, PlayerControl playerTwo)
        {
            PlayerOne = playerOne;
            PlayerTwo = playerTwo;
        }

        public static MatchSetup OnePlayer => new MatchSetup(PlayerControl.Human, PlayerControl.Computer);

        public static MatchSetup TwoPlayer => new MatchSetup(PlayerControl.Human, PlayerControl.Human);

        public static MatchSetup Demo => new MatchSetup(PlayerControl.Computer, PlayerControl.Computer);

        public PlayerControl ControlOf(Seat seat) => seat == Seat.One ? PlayerOne : PlayerTwo;

        public MatchSetup With(Seat seat, PlayerControl control) =>
            seat == Seat.One ? new MatchSetup(control, PlayerTwo) : new MatchSetup(PlayerOne, control);

        public int HumanCount =>
            (PlayerOne == PlayerControl.Human ? 1 : 0) + (PlayerTwo == PlayerControl.Human ? 1 : 0);

        // The home screen's players setting: flips between one and two players
        // (a demo becomes one player).
        public MatchSetup ToggleHumanPlayers() => HumanCount == 1 ? TwoPlayer : OnePlayer;

        public string Label => "Players: " + HumanCount;

        // What to call a seat on screen, from who controls it: a human seat is
        // "Player 1" or "Player 2"; a computer seat is just "Computer" when it is
        // the only one, and "Computer 1" or "Computer 2" when both are.
        public string LabelOf(Seat seat)
        {
            var number = seat == Seat.One ? "1" : "2";
            if (ControlOf(seat) == PlayerControl.Human)
                return "Player " + number;
            return HumanCount == 1 ? "Computer" : "Computer " + number;
        }

        // The follow camera tracks the first human seat; with no human to follow
        // (a demo) it tracks Player 1, though a demo normally uses the overview.
        public Seat FollowSeat =>
            PlayerOne == PlayerControl.Human ? Seat.One :
            PlayerTwo == PlayerControl.Human ? Seat.Two :
            Seat.One;

        // With exactly one human the follow camera suits them. With two (neither
        // should be favoured) or none (nobody to follow) the whole arena is shown.
        public bool StartsInOverview => HumanCount != 1;

        public bool Equals(MatchSetup other) => PlayerOne == other.PlayerOne && PlayerTwo == other.PlayerTwo;

        public override bool Equals(object obj) => obj is MatchSetup other && Equals(other);

        public override int GetHashCode() => ((int)PlayerOne * 31) + (int)PlayerTwo;
    }
}

namespace MonoTanx.Core
{
    public enum GameMode
    {
        // Player 1 against the computer.
        OnePlayer,

        // Two humans. Player 2 starts under human control and the whole arena is
        // shown at once so neither player is favoured by the follow camera.
        TwoPlayer
    }

    public static class GameModeExtensions
    {
        public static GameMode Toggle(this GameMode mode) =>
            mode == GameMode.OnePlayer ? GameMode.TwoPlayer : GameMode.OnePlayer;

        public static string Label(this GameMode mode) =>
            mode == GameMode.OnePlayer ? "Players: 1" : "Players: 2";
    }
}

using Microsoft.Xna.Framework;

namespace MonoTanx.Core
{
    public enum ComputerDecisionKind
    {
        // It saw a shell that would hit it and chose a move to get clear.
        Dodging,

        // A shell would have hit it but it did not notice it (the random draw, per shell).
        ShellNotNoticed,

        // It saw a shell that would hit it but no move gets clear in time.
        ShellUnavoidable,

        // It has been driving without moving and is backing away.
        Stuck
    }

    // Something the computer decided that is worth recording (it is the log's way of seeing why it
    // did or did not dodge). Observed, never fed back: it has no effect on play.
    public readonly struct ComputerDecision
    {
        public ComputerDecisionKind Kind { get; }

        // For the shell kinds: whose gun it came from, how far away it was, and how long until it would hit.
        public Seat? ShellShooter { get; }
        public float ShellDistance { get; }
        public float SecondsToHit { get; }

        // Dodging: the move chosen.
        public TankCommand Move { get; }

        // ShellNotNoticed: the draw and the chance it needed to beat.
        public float Draw { get; }
        public float Chance { get; }

        // Where the tank was and how much fuel it had.
        public Vector2 Position { get; }
        public float Fuel { get; }

        public ComputerDecision(ComputerDecisionKind kind, Vector2 position, float fuel, Seat? shellShooter = null, float shellDistance = 0.0f,
            float secondsToHit = 0.0f, TankCommand move = default, float draw = 0.0f, float chance = 0.0f)
        {
            Kind = kind;
            Position = position;
            Fuel = fuel;
            ShellShooter = shellShooter;
            ShellDistance = shellDistance;
            SecondsToHit = secondsToHit;
            Move = move;
            Draw = draw;
            Chance = chance;
        }
    }
}

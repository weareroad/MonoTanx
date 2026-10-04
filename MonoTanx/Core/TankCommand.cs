namespace MonoTanx.Core
{
    // What a controller wants a tank to do for one update: the same thing whether
    // a human (keyboard, later a gamepad) or the computer is driving. The stage
    // applies it through the usual movement and firing rules.
    public readonly struct TankCommand
    {
        // -1 turns left, +1 turns right.
        public float Turn { get; }

        // -1 reverses, +1 drives forward.
        public float Drive { get; }

        // True for the update on which the controller wants to fire.
        public bool Fire { get; }

        public TankCommand(float turn, float drive, bool fire = false)
        {
            Turn = turn;
            Drive = drive;
            Fire = fire;
        }

        public static TankCommand None => default;

        public bool IsIdle => Turn == 0.0f && Drive == 0.0f && !Fire;
    }
}

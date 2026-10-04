using Microsoft.Xna.Framework.Input;

namespace MonoTanx.Core
{
    // One keyboard layout for driving a tank, and how it becomes a TankCommand.
    // The layouts are data so they can be remapped later.
    public readonly struct SeatKeys
    {
        public Keys Left { get; }
        public Keys Right { get; }
        public Keys Forward { get; }
        public Keys Reverse { get; }
        public Keys Fire { get; }

        public SeatKeys(Keys left, Keys right, Keys forward, Keys reverse, Keys fire)
        {
            Left = left;
            Right = right;
            Forward = forward;
            Reverse = reverse;
            Fire = fire;
        }

        public static SeatKeys PlayerOne => new SeatKeys(Keys.A, Keys.D, Keys.W, Keys.S, Keys.Space);

        public static SeatKeys PlayerTwo => new SeatKeys(Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Enter);

        // Opposite keys cancel out. Fire is true only on the update the key goes
        // down, so holding it does not bypass the reload.
        public TankCommand ToCommand(KeyboardState current, KeyboardState previous)
        {
            var turn = 0.0f;
            var drive = 0.0f;
            if (current.IsKeyDown(Left)) turn -= 1.0f;
            if (current.IsKeyDown(Right)) turn += 1.0f;
            if (current.IsKeyDown(Forward)) drive += 1.0f;
            if (current.IsKeyDown(Reverse)) drive -= 1.0f;
            var fire = current.IsKeyDown(Fire) && previous.IsKeyUp(Fire);
            return new TankCommand(turn, drive, fire);
        }
    }
}

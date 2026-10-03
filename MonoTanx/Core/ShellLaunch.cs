using Microsoft.Xna.Framework;

namespace MonoTanx.Core
{
    public readonly struct ShellLaunch
    {
        public Ammunition Ammunition { get; }
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }

        public ShellLaunch(Ammunition ammunition, Vector2 position, Vector2 velocity)
        {
            Ammunition = ammunition;
            Position = position;
            Velocity = velocity;
        }
    }
}

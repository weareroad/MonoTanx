using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    public readonly struct ShellStepResult
    {
        // True when the shell has ended (expired, hit terrain, hit a tank, or reflected too often).
        public bool Removed { get; }

        // The tank that was hit, if any.
        public Player Hit { get; }

        public ShellStepResult(bool removed, Player hit)
        {
            Removed = removed;
            Hit = hit;
        }
    }

    // A projectile in flight. Contains no rendering or Game dependencies so
    // its flight, reflection and hit rules can be tested directly.
    public sealed class Shell
    {
        public Ammunition Ammunition;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public int ReflectionCount;
        public float ReflectionCooldown;

        public Shell(Ammunition ammunition, Vector2 position, Vector2 velocity)
        {
            Ammunition = ammunition;
            Position = position;
            Velocity = velocity;
        }

        // Advances the shell by the elapsed time in short sub-steps so it cannot
        // skip over thin terrain. Tanks are tested in list order.
        public ShellStepResult Step(WorldMap map, IReadOnlyList<Player> tanks, float elapsed)
        {
            Age += elapsed;
            if (Age >= Ammunition.MaxFlightDurationSeconds)
                return new ShellStepResult(true, null);

            var steps = Math.Max(1, (int)Math.Ceiling(Velocity.Length() * elapsed / Tuning.Projectile.SubStepLength));
            var stepTime = elapsed / steps;
            ReflectionCooldown = Math.Max(0.0f, ReflectionCooldown - elapsed);
            for (var step = 0; step < steps; step++)
            {
                Position += Velocity * stepTime;
                var terrain = map.GetTerrainAt(Position);
                if (terrain == TerrainKind.Reflective && ReflectionCooldown <= 0.0f)
                {
                    // Treat reflective runs as axis-aligned mirrors. A horizontal
                    // run flips Y; a vertical run flips X. Isolated tiles fall
                    // back to the velocity-based axis choice.
                    if (map.ReflectiveSurfaceIsHorizontal(Position, Velocity))
                    {
                        Velocity.Y = -Velocity.Y;
                        Position.Y += Math.Sign(Velocity.Y) * Tuning.Projectile.ReflectionNudge;
                    }
                    else
                    {
                        Velocity.X = -Velocity.X;
                        Position.X += Math.Sign(Velocity.X) * Tuning.Projectile.ReflectionNudge;
                    }
                    ReflectionCooldown = Tuning.Projectile.ReflectionCooldownSeconds;
                    if (++ReflectionCount > Tuning.Projectile.MaxReflections)
                        return new ShellStepResult(true, null);
                }
                else if (map.BlocksProjectiles(Position))
                {
                    return new ShellStepResult(true, null);
                }

                foreach (var tank in tanks)
                {
                    var hitDistance = Tuning.Projectile.CollisionRadius + tank.CollisionRadius;
                    if (Vector2.DistanceSquared(Position, tank.Position) <= hitDistance * hitDistance)
                        return new ShellStepResult(true, tank);
                }
            }

            return new ShellStepResult(false, null);
        }
    }
}

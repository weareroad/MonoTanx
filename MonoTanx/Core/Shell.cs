using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    // How a shell's step ended.
    public enum ShellFate
    {
        // Still flying.
        InFlight,

        // Ran out of flight time.
        Expired,

        // Hit a solid, non-reflective surface (wall, hill, map edge).
        HitTerrain,

        // Hit a tank.
        HitTank,

        // Reflected more times than allowed.
        TooManyReflections
    }

    public readonly struct ShellStepResult
    {
        public ShellFate Fate { get; }

        // The tank that was hit, if any.
        public Player Hit { get; }

        // How many times the shell reflected during this step.
        public int Reflections { get; }

        // True when the shell has ended, whatever the reason.
        public bool Removed => Fate != ShellFate.InFlight;

        public ShellStepResult(ShellFate fate, Player hit, int reflections)
        {
            Fate = fate;
            Hit = hit;
            Reflections = reflections;
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

        // Reflections it survives; one more removes it.
        public int MaxReflections = Tuning.Projectile.MaxReflections;
        public float ReflectionCooldown;

        // Whose gun it came from, for the log (it can still hit its own tank).
        public Seat? Shooter;

        public Shell(Ammunition ammunition, Vector2 position, Vector2 velocity)
        {
            Ammunition = ammunition;
            Position = position;
            Velocity = velocity;
        }

        // A copy in the same state, to fly forward and see where it would go without disturbing it.
        public Shell Clone() => new Shell(Ammunition, Position, Velocity)
        {
            Age = Age,
            ReflectionCount = ReflectionCount,
            MaxReflections = MaxReflections,
            ReflectionCooldown = ReflectionCooldown,
            Shooter = Shooter
        };

        // Advances the shell by the elapsed time in short sub-steps so it cannot
        // skip over thin terrain. Tanks are tested in list order.
        public ShellStepResult Step(WorldMap map, IReadOnlyList<Player> tanks, float elapsed)
        {
            Age += elapsed;
            if (Age >= Ammunition.MaxFlightDurationSeconds)
                return new ShellStepResult(ShellFate.Expired, null, 0);

            var steps = Math.Max(1, (int)Math.Ceiling(Velocity.Length() * elapsed / Tuning.Projectile.SubStepLength));
            var stepTime = elapsed / steps;
            var reflections = 0;
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
                    reflections++;
                    if (++ReflectionCount > MaxReflections)
                        return new ShellStepResult(ShellFate.TooManyReflections, null, reflections);
                }
                else if (map.BlocksProjectiles(Position))
                {
                    return new ShellStepResult(ShellFate.HitTerrain, null, reflections);
                }

                foreach (var tank in tanks)
                {
                    var hitDistance = Tuning.Projectile.CollisionRadius + tank.CollisionRadius;
                    if (Vector2.DistanceSquared(Position, tank.Position) <= hitDistance * hitDistance)
                        return new ShellStepResult(ShellFate.HitTank, tank, reflections);
                }
            }

            return new ShellStepResult(ShellFate.InFlight, null, reflections);
        }
    }
}

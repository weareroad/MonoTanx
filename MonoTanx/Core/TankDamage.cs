using Microsoft.Xna.Framework;
using System;

namespace MonoTanx.Core
{
    // What happens to a tank when a shell hits it.
    public static class TankDamage
    {
        public const float KnockbackDistance = 1.5f;
        public const float MaximumHeadingDisruptionRadians = 0.16f;

        // Reduces health (never below zero), nudges the tank away from the
        // impact if the destination is free, and disrupts its heading using the
        // supplied random source. Returns true when the tank has no health left.
        public static bool Apply(WorldMap map, Player tank, Player other, int damage, Vector2 impactVelocity, Random random)
        {
            tank.Health = Math.Max(0, tank.Health - Math.Max(0, damage));
            if (impactVelocity.LengthSquared() > 0.0f)
            {
                var knockback = Vector2.Normalize(impactVelocity) * KnockbackDistance;
                if (TankMovement.CanOccupy(map, tank, other, tank.Position + knockback))
                    tank.Position += knockback;
            }
            var disruption = ((float)random.NextDouble() * 2.0f - 1.0f) * MaximumHeadingDisruptionRadians;
            tank.Heading = MathHelper.WrapAngle(tank.Heading + disruption);
            return tank.Health == 0;
        }
    }
}

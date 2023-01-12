using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTanx.Core
{
    public static class Helpers
    {
        public static float GetMovementSpeed(int milliSeconds)
        {
            return milliSeconds / 2.38f;
        }

        // given our options and the velocity, which direction should we choose?
        public static string GetDirectionFromVelocity(Vector2 velocity, string Left, string Right, string Up, string Down, string None)
        {
            // left and right take precedence
            if (velocity.X < 0) return Left;
            else if (velocity.X > 0) return Right;
            else if (velocity.Y > 0) return Down;
            else if (velocity.Y < 0) return Up;
            return None; // fallback

        }

        public static Vector2 GetRandomVelocity(Random RandomGenerator)
        {
            int vx, vy = 0;
            do
            {
                vx = (RandomGenerator.Next(0, 3) - 1);
                vy = (RandomGenerator.Next(0, 3) - 1);
            }
            while (vx == 0 && vy == 0);
            return new Vector2(vx, vy);
        }

    }
}

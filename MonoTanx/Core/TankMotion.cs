using Microsoft.Xna.Framework;
using System;

namespace MonoTanx.Core
{
    // What a tank is doing, judged from how far it actually moved and turned.
    public enum TankMotion
    {
        Idle,
        Forward,
        Reverse,
        Turn
    }

    public static class TankMotionClassifier
    {
        // Compares a tank before and after an update. Moving wins over turning, so
        // a tank turning while it drives counts as driving. Judging from the
        // result rather than the keys works the same for a human and the computer,
        // and a tank held against a wall counts as idle.
        public static TankMotion Classify(Vector2 positionBefore, float headingBefore, Vector2 positionAfter, float headingAfter)
        {
            var moved = positionAfter - positionBefore;
            var minimum = Tuning.Audio.MotionThresholdPixels;
            if (moved.LengthSquared() > minimum * minimum)
            {
                var facing = new Vector2((float)Math.Cos(headingAfter), (float)Math.Sin(headingAfter));
                return Vector2.Dot(moved, facing) >= 0.0f ? TankMotion.Forward : TankMotion.Reverse;
            }

            var turned = Math.Abs(MathHelper.WrapAngle(headingAfter - headingBefore));
            return turned > Tuning.Audio.MotionThresholdRadians ? TankMotion.Turn : TankMotion.Idle;
        }
    }
}

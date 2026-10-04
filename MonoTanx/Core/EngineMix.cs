using Microsoft.Xna.Framework;
using System;

namespace MonoTanx.Core
{
    // Volume and pitch of one tank's engine drone over time. Both move towards
    // their targets at a fixed rate in elapsed time, so changes do not click and
    // take the same time at any frame rate. Pure, so it can be tested without an
    // audio device.
    public sealed class EngineMix
    {
        private readonly float pitchOffset;
        private float targetPitch;

        public float Volume { get; private set; }

        // -1 to +1, an octave either way.
        public float Pitch { get; private set; }

        // Player 2's engine is pitch-shifted so the two tanks can be told apart.
        public EngineMix(bool playerTwo)
        {
            pitchOffset = playerTwo ? Tuning.Audio.PlayerTwoPitchOffset : 0.0f;
            targetPitch = Pitch = ClampPitch(BasePitch(TankMotion.Forward) + pitchOffset);
        }

        public static float TargetVolume(TankMotion motion) => motion switch
        {
            TankMotion.Forward => Tuning.Audio.EngineForwardVolume,
            TankMotion.Reverse => Tuning.Audio.EngineReverseVolume,
            TankMotion.Turn => Tuning.Audio.EngineTurnVolume,
            _ => 0.0f
        };

        public static float BasePitch(TankMotion motion) => motion switch
        {
            TankMotion.Reverse => Tuning.Audio.EngineReversePitch,
            TankMotion.Turn => Tuning.Audio.EngineTurnPitch,
            _ => Tuning.Audio.EngineForwardPitch
        };

        // Moves the mix towards what the motion calls for. When the tank is idle,
        // or the game is not active (paused), the volume fades to silence and the
        // pitch stays where it was so the fade-out does not change note.
        public void Update(TankMotion motion, bool active, float elapsed)
        {
            if (active && motion != TankMotion.Idle)
                targetPitch = ClampPitch(BasePitch(motion) + pitchOffset);

            var targetVolume = active ? TargetVolume(motion) : 0.0f;
            Volume = MoveToward(Volume, targetVolume, elapsed / Tuning.Audio.EngineFadeSeconds);
            Pitch = MoveToward(Pitch, targetPitch, elapsed / Tuning.Audio.EnginePitchGlideSeconds);
        }

        private static float MoveToward(float current, float target, float maximumStep)
        {
            if (current < target) return Math.Min(target, current + maximumStep);
            if (current > target) return Math.Max(target, current - maximumStep);
            return current;
        }

        private static float ClampPitch(float pitch) => MathHelper.Clamp(pitch, -1.0f, 1.0f);
    }
}

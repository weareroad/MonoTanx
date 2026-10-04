using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;

namespace MonoTanx.Core
{
    // One tank's engine: a looping drone whose volume and pitch follow EngineMix.
    // With no sound effect (muted, no audio device, missing file) it does nothing
    // audible, so the game does not need to care.
    public sealed class EngineSound : IDisposable
    {
        private readonly EngineMix mix;
        private SoundEffectInstance instance;

        public EngineSound(SoundEffect effect, bool playerTwo)
        {
            mix = new EngineMix(playerTwo);
            if (effect == null)
                return;

            try
            {
                // It plays continuously, silent when the tank is idle.
                instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = 0.0f;
                instance.Pitch = mix.Pitch;
                instance.Play();
            }
            catch (NoAudioHardwareException)
            {
                instance = null;
            }
        }

        public void Update(TankMotion motion, bool active, float elapsed)
        {
            mix.Update(motion, active, elapsed);
            if (instance == null)
                return;

            try
            {
                instance.Volume = MathHelper.Clamp(mix.Volume, 0.0f, 1.0f);
                instance.Pitch = MathHelper.Clamp(mix.Pitch, -1.0f, 1.0f);
            }
            catch (NoAudioHardwareException)
            {
                instance = null;
            }
        }

        public void Dispose()
        {
            if (instance == null)
                return;

            try
            {
                instance.Stop();
                instance.Dispose();
            }
            catch (NoAudioHardwareException)
            {
            }
            instance = null;
        }
    }
}

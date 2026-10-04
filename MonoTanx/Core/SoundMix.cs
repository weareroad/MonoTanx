using Microsoft.Xna.Framework;

namespace MonoTanx.Core
{
    // Volume and pitch for a cue. Pure, so it can be tested without an audio device.
    public readonly struct SoundMix
    {
        public float Volume { get; }

        // -1 to +1, an octave either way (the range SoundEffect.Play accepts).
        public float Pitch { get; }

        public SoundMix(float volume, float pitch)
        {
            Volume = volume;
            Pitch = pitch;
        }

        // Player 2 hears the same sounds at a different pitch, so the two tanks
        // can be told apart (whether Player 2 is the computer or a human).
        public static SoundMix For(SoundCue cue, bool playerTwo)
        {
            var volume = cue switch
            {
                SoundCue.Fire => Tuning.Audio.FireVolume,
                SoundCue.Reload => Tuning.Audio.ReloadVolume,
                SoundCue.Explosion => Tuning.Audio.ExplosionVolume,
                SoundCue.Ping => Tuning.Audio.PingVolume,
                SoundCue.Crump => Tuning.Audio.CrumpVolume,
                SoundCue.Pickup => Tuning.Audio.PickupVolume,
                _ => 1.0f
            };
            var pitch = playerTwo ? Tuning.Audio.PlayerTwoPitchOffset : 0.0f;
            return new SoundMix(MathHelper.Clamp(volume, 0.0f, 1.0f), MathHelper.Clamp(pitch, -1.0f, 1.0f));
        }
    }
}

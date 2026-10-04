using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    // Loads the game's sounds and plays them. Audio must never stop the game
    // from running: when muted, when there is no audio device, or when a file is
    // missing, the affected sounds are simply skipped (with one console message).
    public sealed class GameAudio
    {
        private readonly Dictionary<SoundCue, SoundEffect> effects = new Dictionary<SoundCue, SoundEffect>();
        private bool disabled;

        public bool Enabled => !disabled;

        public GameAudio(ContentManager content, bool muted)
        {
            if (muted)
            {
                disabled = true;
                return;
            }

            try
            {
                SoundEffect.MasterVolume = Tuning.Audio.MasterVolume;
                foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
                {
                    try
                    {
                        effects[cue] = content.Load<SoundEffect>("Audio/" + AssetName(cue));
                    }
                    catch (ContentLoadException exception)
                    {
                        Console.Error.WriteLine($"Audio: could not load '{AssetName(cue)}' ({exception.Message}); that sound is skipped.");
                    }
                }
            }
            catch (NoAudioHardwareException)
            {
                Disable("no audio device available");
            }
        }

        // Content name of a cue's file (under Content/Audio), for example Fire -> "fire".
        public static string AssetName(SoundCue cue) => cue.ToString().ToLowerInvariant();

        public void Play(SoundCue cue, bool playerTwo = false)
        {
            if (disabled || !effects.TryGetValue(cue, out var effect))
                return;

            var mix = SoundMix.For(cue, playerTwo);
            try
            {
                effect.Play(mix.Volume, mix.Pitch, 0.0f);
            }
            catch (NoAudioHardwareException)
            {
                Disable("no audio device available");
            }
        }

        private void Disable(string reason)
        {
            disabled = true;
            Console.Error.WriteLine($"Audio disabled: {reason}.");
        }
    }
}

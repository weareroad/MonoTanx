using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class SoundMixTests
{
    [Theory]
    [InlineData(SoundCue.Fire, Tuning.Audio.FireVolume)]
    [InlineData(SoundCue.Reload, Tuning.Audio.ReloadVolume)]
    [InlineData(SoundCue.Explosion, Tuning.Audio.ExplosionVolume)]
    [InlineData(SoundCue.Ping, Tuning.Audio.PingVolume)]
    [InlineData(SoundCue.Crump, Tuning.Audio.CrumpVolume)]
    [InlineData(SoundCue.Pickup, Tuning.Audio.PickupVolume)]
    public void EachCueUsesItsTunedVolume(SoundCue cue, float expected)
    {
        Assert.Equal(expected, SoundMix.For(cue, playerTwo: false).Volume);
    }

    [Fact]
    public void PlayerOneIsAtNormalPitchAndPlayerTwoIsShifted()
    {
        foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
        {
            Assert.Equal(0.0f, SoundMix.For(cue, playerTwo: false).Pitch);
            Assert.Equal(Tuning.Audio.PlayerTwoPitchOffset, SoundMix.For(cue, playerTwo: true).Pitch);
        }
    }

    [Fact]
    public void VolumeAndPitchStayInTheRangeTheAudioApiAccepts()
    {
        foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            foreach (var playerTwo in new[] { false, true })
            {
                var mix = SoundMix.For(cue, playerTwo);
                Assert.InRange(mix.Volume, 0.0f, 1.0f);
                Assert.InRange(mix.Pitch, -1.0f, 1.0f);
            }
    }

    [Fact]
    public void TheExplosionIsTheLoudestCue()
    {
        var explosion = SoundMix.For(SoundCue.Explosion, false).Volume;

        foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            Assert.True(SoundMix.For(cue, false).Volume <= explosion);
    }

    [Fact]
    public void TheMasterVolumeIsAValidFraction()
    {
        Assert.InRange(Tuning.Audio.MasterVolume, 0.0f, 1.0f);
    }
}

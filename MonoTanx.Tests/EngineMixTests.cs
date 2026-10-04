using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class EngineMixTests
{
    private const float Step = 1.0f / 60.0f;

    private static void Run(EngineMix mix, TankMotion motion, bool active, float seconds, float step = Step)
    {
        var steps = (int)Math.Round(seconds / step);
        for (var i = 0; i < steps; i++)
            mix.Update(motion, active, step);
    }

    [Fact]
    public void StartsSilentAtTheForwardPitch()
    {
        var mix = new EngineMix(playerTwo: false);

        Assert.Equal(0.0f, mix.Volume);
        Assert.Equal(Tuning.Audio.EngineForwardPitch, mix.Pitch);
    }

    [Theory]
    [InlineData(TankMotion.Forward, Tuning.Audio.EngineForwardVolume, Tuning.Audio.EngineForwardPitch)]
    [InlineData(TankMotion.Reverse, Tuning.Audio.EngineReverseVolume, Tuning.Audio.EngineReversePitch)]
    [InlineData(TankMotion.Turn, Tuning.Audio.EngineTurnVolume, Tuning.Audio.EngineTurnPitch)]
    public void EachMotionSettlesAtItsVolumeAndPitch(TankMotion motion, float volume, float pitch)
    {
        var mix = new EngineMix(playerTwo: false);

        Run(mix, motion, active: true, seconds: 2.0f);

        Assert.Equal(volume, mix.Volume, 4);
        Assert.Equal(pitch, mix.Pitch, 4);
    }

    [Fact]
    public void TheThreeMotionsHaveDifferentPitches()
    {
        var pitches = new[] { TankMotion.Forward, TankMotion.Reverse, TankMotion.Turn }
            .Select(EngineMix.BasePitch).Distinct().ToList();

        Assert.Equal(3, pitches.Count);
    }

    [Fact]
    public void TheEnginesAreQuiet()
    {
        foreach (var motion in new[] { TankMotion.Forward, TankMotion.Reverse, TankMotion.Turn })
            Assert.InRange(EngineMix.TargetVolume(motion), 0.01f, 0.5f);
        Assert.Equal(0.0f, EngineMix.TargetVolume(TankMotion.Idle));
    }

    [Fact]
    public void ItFadesInAndOutRatherThanJumping()
    {
        var mix = new EngineMix(playerTwo: false);

        mix.Update(TankMotion.Forward, active: true, Step);
        Assert.InRange(mix.Volume, 0.0001f, Tuning.Audio.EngineForwardVolume - 0.0001f); // started, not there yet

        Run(mix, TankMotion.Forward, active: true, seconds: 2.0f);
        mix.Update(TankMotion.Idle, active: true, Step);
        Assert.InRange(mix.Volume, 0.0001f, Tuning.Audio.EngineForwardVolume - 0.0001f); // fading, not cut

        Run(mix, TankMotion.Idle, active: true, seconds: 2.0f);
        Assert.Equal(0.0f, mix.Volume);
    }

    [Fact]
    public void PausingFadesTheEngineOutWhateverTheTankWasDoing()
    {
        var mix = new EngineMix(playerTwo: false);
        Run(mix, TankMotion.Forward, active: true, seconds: 2.0f);

        Run(mix, TankMotion.Forward, active: false, seconds: 2.0f);

        Assert.Equal(0.0f, mix.Volume);
    }

    [Fact]
    public void TheFadeOutKeepsItsPitchSoTheNoteDoesNotChange()
    {
        var mix = new EngineMix(playerTwo: false);
        Run(mix, TankMotion.Reverse, active: true, seconds: 2.0f);

        Run(mix, TankMotion.Idle, active: true, seconds: 2.0f);

        Assert.Equal(Tuning.Audio.EngineReversePitch, mix.Pitch, 4);
    }

    [Fact]
    public void ThePitchGlidesBetweenNotes()
    {
        var mix = new EngineMix(playerTwo: false);
        Run(mix, TankMotion.Forward, active: true, seconds: 2.0f);

        mix.Update(TankMotion.Turn, active: true, Step);

        Assert.InRange(mix.Pitch, Tuning.Audio.EngineForwardPitch + 0.0001f, Tuning.Audio.EngineTurnPitch - 0.0001f);
    }

    [Fact]
    public void PlayerTwoIsShiftedByTheSameOffsetAtEveryNote()
    {
        foreach (var motion in new[] { TankMotion.Forward, TankMotion.Reverse, TankMotion.Turn })
        {
            var one = new EngineMix(playerTwo: false);
            var two = new EngineMix(playerTwo: true);
            Run(one, motion, active: true, seconds: 2.0f);
            Run(two, motion, active: true, seconds: 2.0f);

            Assert.Equal(one.Pitch + Tuning.Audio.PlayerTwoPitchOffset, two.Pitch, 4);
            Assert.Equal(one.Volume, two.Volume);
        }
    }

    [Fact]
    public void PitchAndVolumeNeverLeaveTheirRanges()
    {
        var mix = new EngineMix(playerTwo: true);
        var motions = new[] { TankMotion.Forward, TankMotion.Reverse, TankMotion.Turn, TankMotion.Idle };

        for (var i = 0; i < 600; i++)
        {
            mix.Update(motions[(i / 7) % motions.Length], active: i % 50 != 0, Step);
            Assert.InRange(mix.Volume, 0.0f, 1.0f);
            Assert.InRange(mix.Pitch, -1.0f, 1.0f);
        }
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void FadesTakeTheSameTimeAtAnyFrameRate(int stepsPerSecond)
    {
        var mix = new EngineMix(playerTwo: false);
        var step = 1.0f / stepsPerSecond;
        var expectedSeconds = Tuning.Audio.EngineForwardVolume * Tuning.Audio.EngineFadeSeconds;

        var elapsed = 0.0f;
        while (mix.Volume < Tuning.Audio.EngineForwardVolume)
        {
            mix.Update(TankMotion.Forward, active: true, step);
            elapsed += step;
            Assert.True(elapsed < 5.0f, "the fade never finished");
        }

        Assert.InRange(elapsed, expectedSeconds - 0.0001f, expectedSeconds + step + 0.0001f);
    }
}

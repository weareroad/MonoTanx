using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

// Every sound the game needs must exist as a valid WAV and be in the content
// pipeline. This also guards real assets dropped in to replace the placeholders.
public class AudioAssetTests
{
    private static IEnumerable<string> AssetNames()
    {
        foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            yield return GameAudio.AssetName(cue);
        yield return "engine"; // the looping engine drone
    }

    public static IEnumerable<object[]> Assets => AssetNames().Select(name => new object[] { name });

    private static string WavPath(string name) => FixturePath("content", "Audio", name + ".wav");

    [Fact]
    public void CueAssetNamesAreTheLowerCaseCueNames()
    {
        Assert.Equal("fire", GameAudio.AssetName(SoundCue.Fire));
        Assert.Equal("explosion", GameAudio.AssetName(SoundCue.Explosion));
    }

    [Theory]
    [MemberData(nameof(Assets))]
    public void TheWavExistsAndIsSixteenBitPcm(string name)
    {
        var path = WavPath(name);
        Assert.True(File.Exists(path), $"missing Content/Audio/{name}.wav");

        using var reader = new BinaryReader(File.OpenRead(path));
        Assert.Equal("RIFF", new string(reader.ReadChars(4)));
        reader.ReadInt32();
        Assert.Equal("WAVE", new string(reader.ReadChars(4)));

        var foundFormat = false;
        var foundData = false;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var chunk = new string(reader.ReadChars(4));
            var size = reader.ReadInt32();
            var next = reader.BaseStream.Position + size + (size % 2);
            if (chunk == "fmt ")
            {
                foundFormat = true;
                Assert.Equal(1, reader.ReadInt16());      // PCM
                reader.ReadInt16();                        // channels (mono or stereo are both fine)
                reader.ReadInt32();                        // sample rate
                reader.ReadInt32();                        // byte rate
                reader.ReadInt16();                        // block align
                Assert.Equal(16, reader.ReadInt16());      // bits per sample
            }
            else if (chunk == "data")
            {
                foundData = true;
                Assert.True(size > 0, "the WAV has no sample data");
            }
            reader.BaseStream.Position = next;
        }
        Assert.True(foundFormat, "no fmt chunk");
        Assert.True(foundData, "no data chunk");
    }

    [Theory]
    [MemberData(nameof(Assets))]
    public void TheContentPipelineBuildsItAsASoundEffect(string name)
    {
        var mgcb = File.ReadAllText(FixturePath("content", "Content.mgcb"));
        var marker = $"#begin Audio/{name}.wav";
        var at = mgcb.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(at >= 0, $"Content.mgcb has no entry for Audio/{name}.wav");
        var block = mgcb.Substring(at + marker.Length).Split("#begin")[0];

        Assert.Contains("/importer:WavImporter", block);
        Assert.Contains("/processor:SoundEffectProcessor", block);
        Assert.Contains("/processorParam:Quality=Best", block);
        Assert.Contains($"/build:Audio/{name}.wav", block);
    }
}

using MonoTanx.Core;
using System.IO;
using System.Linq;
using Xunit;

namespace MonoTanx.Tests;

public class SettingsTests
{
    [Fact]
    public void EveryKeyIsUniqueAndEveryDefaultIsInsideItsRange()
    {
        Assert.Equal(SettingsCatalogue.All.Count, SettingsCatalogue.All.Select(definition => definition.Key).Distinct().Count());
        foreach (var definition in SettingsCatalogue.All)
        {
            Assert.True(definition.Minimum <= definition.Default && definition.Default <= definition.Maximum, definition.Key + " default is outside its range");
            Assert.True(definition.Step > 0.0f, definition.Key + " needs a positive step");
            Assert.Equal(definition.Default, definition.Clamp(definition.Default));
            if (definition.IsWhole)
                Assert.Equal(definition.Default, (float)System.Math.Round(definition.Default));
        }
    }

    [Fact]
    public void FreshSettingsAreTheTuningDefaults()
    {
        var settings = new GameSettings();

        Assert.Empty(settings.Differences());
        Assert.Equal(Tuning.Tank.ForwardSpeed, settings.Get(SettingKeys.ForwardSpeed));
        Assert.Equal(Tuning.Ai.EngageDistanceTiles, settings.GetWhole(SettingKeys.AiEngageDistanceTiles));
        Assert.Equal(Tuning.Match.RoundTimeLimitSeconds, settings.Get(SettingKeys.RoundTimeLimitSeconds));
        Assert.Equal(1.0f, settings.Get(SettingKeys.SeatPrefixComputer + SettingKeys.ReloadSuffix));
    }

    [Fact]
    public void ValuesAreClampedAndWholeValuesRounded()
    {
        var settings = new GameSettings();

        Assert.Equal(240.0f, settings.Set(SettingKeys.ForwardSpeed, 5000.0f));
        Assert.Equal(20.0f, settings.Set(SettingKeys.ForwardSpeed, -1.0f));
        Assert.Equal(13, settings.Set(SettingKeys.Damage, 12.6f));
    }

    [Fact]
    public void FireDistanceNeverFallsBelowEngageDistance()
    {
        var settings = new GameSettings();

        settings.Set(SettingKeys.AiEngageDistanceTiles, 11);
        Assert.Equal(11, settings.GetWhole(SettingKeys.AiFireDistanceTiles));

        settings.Set(SettingKeys.AiFireDistanceTiles, 4);
        Assert.Equal(4, settings.GetWhole(SettingKeys.AiEngageDistanceTiles));
    }

    [Fact]
    public void DifferencesResetAndCloneBehave()
    {
        var settings = new GameSettings();
        settings.Set(SettingKeys.AiSkill, 0.9f);
        settings.Set(SettingKeys.Damage, 20);
        var copy = settings.Clone();

        Assert.Equal(new[] { SettingKeys.Damage, SettingKeys.AiSkill }, settings.Differences().Select(pair => pair.Key));

        settings.Reset(SettingKeys.Damage);
        Assert.True(settings.IsDefault(SettingKeys.Damage));
        Assert.Equal(20, copy.GetWhole(SettingKeys.Damage));

        settings.ResetAll();
        Assert.Empty(settings.Differences());
        Assert.Equal(0.9f, copy.Get(SettingKeys.AiSkill));
    }

    [Fact]
    public void AnUnknownKeyIsRejected()
    {
        Assert.Throws<System.ArgumentException>(() => new GameSettings().Get("nope"));
    }

    [Fact]
    public void TheFileHoldsOnlyWhatDiffersAndRoundTrips()
    {
        var settings = new GameSettings();
        settings.Set(SettingKeys.AiSkill, 0.8f);
        settings.Set(SettingKeys.AiEngageDistanceTiles, 5);
        settings.Set(SettingKeys.RoundTimeLimitSeconds, 120.0f);

        var json = SettingsFile.Serialise(settings);
        var load = SettingsFile.Parse(json);

        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"ai.skill\": 0.8", json);
        Assert.DoesNotContain("tank.forwardSpeed", json);
        Assert.Empty(load.Problems);
        Assert.Equal(settings.Differences(), load.Settings.Differences());
    }

    [Fact]
    public void AnEmptyFileOrObjectIsTheDefaults()
    {
        Assert.Empty(SettingsFile.Parse("{}").Settings.Differences());
        Assert.Empty(SettingsFile.Parse("{}").Problems);
    }

    [Fact]
    public void ABadValueIsClampedAndReported()
    {
        var load = SettingsFile.Parse("{ \"version\": 1, \"tank.forwardSpeed\": 9999 }");

        Assert.Equal(240.0f, load.Settings.Get(SettingKeys.ForwardSpeed));
        Assert.Contains(load.Problems, problem => problem.Contains("tank.forwardSpeed") && problem.Contains("240"));
    }

    [Theory]
    [InlineData("{ \"nope.nothing\": 1 }", "Unknown setting")]
    [InlineData("{ \"ai.skill\": \"high\" }", "not a number")]
    [InlineData("{ \"ai.skill\": true }", "not a number")]
    [InlineData("[1, 2]", "not a JSON object")]
    [InlineData("{ \"ai.skill\": ", "not valid JSON")]
    [InlineData("{ \"version\": 99 }", "newer")]
    public void ProblemsAreReportedAndNothingElseIsLost(string json, string expected)
    {
        var load = SettingsFile.Parse(json);

        Assert.Contains(load.Problems, problem => problem.Contains(expected));
        Assert.Empty(load.Settings.Differences());
    }

    [Fact]
    public void ValidValuesAreKeptBesideBadOnes()
    {
        var load = SettingsFile.Parse("{ \"bogus\": 1, \"ai.skill\": 0.25 }");

        Assert.Single(load.Problems);
        Assert.Equal(0.25f, load.Settings.Get(SettingKeys.AiSkill));
    }

    [Fact]
    public void SavingCreatesTheFolderAndLoadingReadsItBack()
    {
        var folder = Path.Combine(Path.GetTempPath(), "MonoTanxSettingsTest-" + System.Guid.NewGuid());
        var path = Path.Combine(folder, "inner", "settings.json");
        try
        {
            var settings = new GameSettings();
            settings.Set(SettingKeys.Damage, 30);

            Assert.True(SettingsFile.TrySave(settings, path, out var problem), problem);
            Assert.Equal(30, SettingsFile.Load(path).Settings.GetWhole(SettingKeys.Damage));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void AMissingFileIsTheDefaultsWithNoProblem()
    {
        var load = SettingsFile.Load(Path.Combine(Path.GetTempPath(), "MonoTanx-no-such-" + System.Guid.NewGuid() + ".json"));

        Assert.Empty(load.Problems);
        Assert.Empty(load.Settings.Differences());
    }

    [Fact]
    public void TheDefaultPathIsInTheUserApplicationData()
    {
        Assert.EndsWith(Path.Combine("MonoTanx", "settings.json"), SettingsFile.DefaultPath);
    }

    [Fact]
    public void TheTuningDocumentListsEveryKey()
    {
        var folder = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "MonoTanx.slnx")))
            folder = folder.Parent;
        Assert.NotNull(folder);
        var text = File.ReadAllText(Path.Combine(folder.FullName, "docs", "tuning.md"));

        foreach (var definition in SettingsCatalogue.All)
            Assert.True(text.Contains("`" + definition.Key + "`"), $"docs/tuning.md does not list {definition.Key}");
    }
}

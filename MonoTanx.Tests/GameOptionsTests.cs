using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class GameOptionsTests
{
    [Fact]
    public void NoArgumentsLeavesTheSeedUnset()
    {
        Assert.Null(GameOptions.Parse(Array.Empty<string>()).Seed);
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("0", 0)]
    [InlineData("-5", -5)]
    public void ParsesTheSeed(string value, int expected)
    {
        Assert.Equal(expected, GameOptions.Parse(new[] { "--seed", value }).Seed);
    }

    [Theory]
    [InlineData("--seed")]
    [InlineData("--seed", "abc")]
    [InlineData("--seed", "1.5")]
    public void RejectsAMissingOrInvalidSeed(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(args));
    }

    [Fact]
    public void RejectsUnknownOptions()
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--bogus" }));
    }

    [Fact]
    public void DefaultsToFullscreen()
    {
        var options = GameOptions.Parse(Array.Empty<string>());

        Assert.False(options.Windowed);
        Assert.Equal(GameOptions.DefaultScale, options.Scale);
    }

    [Fact]
    public void ParsesWindowedWithTheDefaultScale()
    {
        var options = GameOptions.Parse(new[] { "--windowed" });

        Assert.True(options.Windowed);
        Assert.Equal(2, options.Scale);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("4", 4)]
    public void ParsesTheScale(string value, int expected)
    {
        var options = GameOptions.Parse(new[] { "--windowed", "--scale", value });

        Assert.True(options.Windowed);
        Assert.Equal(expected, options.Scale);
    }

    [Fact]
    public void OptionOrderDoesNotMatter()
    {
        var options = GameOptions.Parse(new[] { "--scale", "3", "--seed", "7", "--windowed" });

        Assert.True(options.Windowed);
        Assert.Equal(3, options.Scale);
        Assert.Equal(7, options.Seed);
    }

    [Theory]
    [InlineData("--windowed", "--scale")]
    [InlineData("--windowed", "--scale", "abc")]
    [InlineData("--windowed", "--scale", "0")]
    [InlineData("--windowed", "--scale", "5")]
    [InlineData("--windowed", "--scale", "1.5")]
    public void RejectsAMissingOrOutOfRangeScale(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(args));
    }

    [Fact]
    public void RejectsScaleWithoutWindowed()
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--scale", "2" }));
    }

    [Fact]
    public void DefaultsToTheMenuAndOnePlayer()
    {
        var options = GameOptions.Parse(Array.Empty<string>());

        Assert.False(options.Test);
        Assert.Equal(MatchSetup.OnePlayer, options.Setup);
    }

    [Theory]
    [InlineData("--test")]
    [InlineData("-test")]
    public void TestSkipsTheMenu(string argument)
    {
        Assert.True(GameOptions.Parse(new[] { argument }).Test);
    }

    [Fact]
    public void TwoPlayerSelectsTheTwoPlayerMode()
    {
        Assert.Equal(MatchSetup.TwoPlayer, GameOptions.Parse(new[] { "--two-player" }).Setup);
    }

    [Fact]
    public void TestAndTwoPlayerCombineWithOtherOptionsInAnyOrder()
    {
        var options = GameOptions.Parse(new[] { "--seed", "5", "--two-player", "--windowed", "--test", "--scale", "1" });

        Assert.True(options.Test);
        Assert.Equal(MatchSetup.TwoPlayer, options.Setup);
        Assert.Equal(5, options.Seed);
        Assert.True(options.Windowed);
        Assert.Equal(1, options.Scale);
    }

    [Fact]
    public void SoundIsOnUnlessMuted()
    {
        Assert.False(GameOptions.Parse(Array.Empty<string>()).Mute);
        Assert.True(GameOptions.Parse(new[] { "--mute" }).Mute);
    }

    [Fact]
    public void MuteCombinesWithOtherOptions()
    {
        var options = GameOptions.Parse(new[] { "--test", "--mute", "--seed", "3" });

        Assert.True(options.Mute);
        Assert.True(options.Test);
        Assert.Equal(3, options.Seed);
    }

    [Fact]
    public void DemoSelectsTwoComputers()
    {
        Assert.Equal(MatchSetup.Demo, GameOptions.Parse(new[] { "--demo" }).Setup);
    }

    [Fact]
    public void DemoCombinesWithOtherOptions()
    {
        var options = GameOptions.Parse(new[] { "--test", "--demo", "--windowed", "--seed", "9" });

        Assert.True(options.Test);
        Assert.Equal(MatchSetup.Demo, options.Setup);
        Assert.Equal(9, options.Seed);
    }

    [Fact]
    public void TwoPlayerAndDemoCannotBeCombined()
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--two-player", "--demo" }));
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--demo", "--two-player" }));
    }

    [Fact]
    public void RepeatingTheSameSetupOptionIsHarmless()
    {
        Assert.Equal(MatchSetup.Demo, GameOptions.Parse(new[] { "--demo", "--demo" }).Setup);
    }

    // --help

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("/?")]
    public void AnyHelpFlagAsksForHelp(string flag)
    {
        Assert.True(GameOptions.Parse(new[] { flag }).ShowHelp);
    }

    [Fact]
    public void HelpIsOffUnlessAskedFor()
    {
        Assert.False(GameOptions.Parse(Array.Empty<string>()).ShowHelp);
        Assert.False(GameOptions.Parse(new[] { "--test", "--seed", "3" }).ShowHelp);
    }

    [Theory]
    [InlineData("--bogus", "--help")]
    [InlineData("--help", "--bogus")]
    [InlineData("--scale", "2", "--help")]
    [InlineData("--two-player", "--demo", "-h")]
    [InlineData("--seed", "--help")]
    public void HelpAlwaysWinsEvenOverInvalidOptions(params string[] args)
    {
        Assert.True(GameOptions.Parse(args).ShowHelp);
    }

    // Every option the parser accepts, with a valid way to give it. If you add an
    // option, add it here; the tests below then check the help mentions it.
    public static IEnumerable<object[]> EveryOption() => new[]
    {
        new object[] { "--test", Array.Empty<string>() },
        new object[] { "-test", Array.Empty<string>() },
        new object[] { "--two-player", Array.Empty<string>() },
        new object[] { "--demo", Array.Empty<string>() },
        new object[] { "--mute", Array.Empty<string>() },
        new object[] { "--log", Array.Empty<string>() },
        new object[] { "--seed", new[] { "1" } },
        new object[] { "--settings", new[] { "tuning.json" } },
        new object[] { "--set", new[] { "ai.skill=0.9" } },
        new object[] { "--windowed", Array.Empty<string>() },
        new object[] { "--scale", new[] { "2", "--windowed" } },
    };

    [Theory]
    [MemberData(nameof(EveryOption))]
    public void EveryAcceptedOptionIsDescribedInTheHelp(string option, string[] rest)
    {
        // it really is accepted...
        var args = new[] { option }.Concat(rest).ToArray();
        Assert.False(GameOptions.Parse(args).ShowHelp);

        // ...and the help names it
        Assert.Contains(option, GameOptions.HelpText);
    }

    [Fact]
    public void TheHelpListsNothingTheParserRejects()
    {
        // every word in the help that starts with two dashes is an option the parser accepts (or help itself)
        var words = GameOptions.HelpText.Split(new[] { ' ', '\n', '\r', ',', '.', '(', ')', ']', '[', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.StartsWith("--") && word.Length > 2)
            .Distinct();
        var accepted = EveryOption().Select(option => (string)option[0]).Concat(GameOptions.HelpFlags).ToHashSet();

        foreach (var word in words)
            Assert.True(accepted.Contains(word), $"the help mentions {word}, which the parser does not accept");
    }

    [Fact]
    public void TheHelpExplainsHowOptionsCombine()
    {
        var text = GameOptions.HelpText;

        Assert.Contains("order of options does not matter", text);
        Assert.Contains("Help always wins", text);
        Assert.Contains("--two-player with --demo", text);
        Assert.Contains("--scale without --windowed", text);
        Assert.Contains("last value wins", text);
    }

    [Fact]
    public void TheHelpShowsTheUsageAndTheHelpFlags()
    {
        Assert.Contains(GameOptions.UsageLine, GameOptions.HelpText);
        foreach (var flag in GameOptions.HelpFlags)
            Assert.Contains(flag, GameOptions.HelpText);
    }

    [Fact]
    public void WhenAnOptionIsRepeatedTheLastValueWins()
    {
        var options = GameOptions.Parse(new[] { "--seed", "1", "--seed", "2", "--windowed", "--scale", "1", "--scale", "3" });

        Assert.Equal(2, options.Seed);
        Assert.Equal(3, options.Scale);
    }

    [Theory]
    [InlineData("--seed")]
    [InlineData("--scale")]
    public void AValueOptionAtTheEndOfTheLineSaysItNeedsAValue(string option)
    {
        var exception = Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--windowed", option }));

        Assert.Contains(option, exception.Message);
        Assert.Contains("requires", exception.Message);
    }

    [Fact]
    public void ConflictsAreRejectedWithAMessageNamingBothOptions()
    {
        var exception = Assert.Throws<ArgumentException>(() => GameOptions.Parse(new[] { "--demo", "--two-player" }));

        Assert.Contains("--demo", exception.Message);
        Assert.Contains("--two-player", exception.Message);
        Assert.Contains("cannot be combined", exception.Message);
    }

    [Fact]
    public void LogEchoesTheRunLogAndIsOffByDefault()
    {
        Assert.False(GameOptions.Parse(Array.Empty<string>()).Log);
        Assert.True(GameOptions.Parse(new[] { "--test", "--log", "--seed", "3" }).Log);
    }

    [Fact]
    public void TheCommandLineIsKeptForTheRunLog()
    {
        Assert.Equal("--test --demo --seed 9", GameOptions.Parse(new[] { "--test", "--demo", "--seed", "9" }).Arguments);
        Assert.Equal("", GameOptions.Parse(Array.Empty<string>()).Arguments);
    }

    [Fact]
    public void ParsesTheSettingsPathAndRepeatedOverrides()
    {
        var options = GameOptions.Parse(new[] { "--settings", "a.json", "--set", "ai.skill=0.9", "--set", "shell.damage=20" });

        Assert.Equal("a.json", options.SettingsPath);
        Assert.Equal(new[] { "ai.skill", "shell.damage" }, options.SettingOverrides.Select(pair => pair.Key));
        Assert.Equal(0.9f, options.SettingOverrides[0].Value);
        Assert.Null(GameOptions.Parse(Array.Empty<string>()).SettingsPath);
    }

    [Theory]
    [InlineData("--set")]
    [InlineData("--set", "ai.skill")]
    [InlineData("--set", "nope=1")]
    [InlineData("--set", "ai.skill=high")]
    [InlineData("--set", "ai.skill=")]
    [InlineData("--settings")]
    public void RejectsABadSetOrSettings(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => GameOptions.Parse(args));
    }
}

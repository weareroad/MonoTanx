using MonoTanx.Core;
using Xunit;
using Xunit.Abstractions;

namespace MonoTanx.Tests;

// How two computers play each other, in numbers, over long stretches of the real
// arena. A change to the computer is judged by these: they are pinned loosely so
// that changing the balance has to be done on purpose, with docs/tuning.md updated
// (it records the numbers before and after each step).
public class ComputerBalanceTests
{
    private static readonly int[] Seeds = { 7, 42, 1234, 4242 };
    private const float SecondsPerSeed = 600.0f;

    private readonly ITestOutputHelper output;

    // measured once for the whole class: it is the slow part
    private static readonly Lazy<BalanceReport> Computers = new Lazy<BalanceReport>(() =>
        BalanceReport.Sum(Seeds.Select(seed => new MatchHarness(seed, new MatchSetup(PlayerControl.Computer, PlayerControl.Computer)).Measure(SecondsPerSeed))));

    // a computer (seat two) against a human who stands still
    private static readonly Lazy<BalanceReport> AgainstAnIdleHuman = new Lazy<BalanceReport>(() =>
        BalanceReport.Sum(Seeds.Select(seed => new MatchHarness(seed, new MatchSetup(PlayerControl.Human, PlayerControl.Computer)).Measure(SecondsPerSeed))));

    public ComputerBalanceTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private BalanceReport Report(Lazy<BalanceReport> measured, string name)
    {
        output.WriteLine(name + " " + measured.Value);
        return measured.Value;
    }

    [Fact]
    public void TwoComputersCloseInAndHitEachOtherOftenEnoughToFinishRounds()
    {
        var report = Report(Computers, "computers");

        // after stuck recovery they hit 1.7 times a minute at 10% accuracy (firing from 300 to 500px, mostly
        // missing); closing in to fire from about 100px: about 9 to 10 a minute at about 50%
        Assert.InRange(report.HitsPerMinute, 6.0f, 15.0f);
        Assert.InRange(report.Accuracy, 0.3f, 0.75f);
        Assert.True(report.ShellsFired > 0);
    }

    [Fact]
    public void TheComputersAreNoLongerStuckForLong()
    {
        // before stuck recovery about 32 seconds in every minute some tank was driving into something
        foreach (var (name, report) in new[] { ("computers", Report(Computers, "computers")), ("idle human", Report(AgainstAnIdleHuman, "idle human")) })
        {
            Assert.True(report.StuckSecondsPerMinute < 6.0f, $"{name}: stuck {report.StuckSecondsPerMinute:0.0} s/min");
            Assert.True(report.LongestStuckRunSeconds < 4.0f, $"{name}: longest stuck run {report.LongestStuckRunSeconds:0.0}s");
        }
    }

    [Fact]
    public void ARoundBetweenTwoComputersIsDecidedAboutAsOftenAsItIsDrawn()
    {
        var report = Report(Computers, "computers");

        // 5% decided before they closed in (nearly every round ran out the clock), about half now
        Assert.True(report.RoundsDecided + report.RoundsDrawn > 0);
        Assert.InRange(report.DecidedShare, 0.25f, 0.85f);
    }

    [Fact]
    public void AComputerShootsAndKillsAHumanWhoStandsStill()
    {
        var report = Report(AgainstAnIdleHuman, "idle human");

        Assert.True(report.Hits > 0);
        Assert.True(report.ShellsFired > 0);
        Assert.True(report.RoundsDecided > 0, "it never won a round against a motionless human");
    }

    [Fact]
    public void TheComputerIsNotMotionlessInViewForMostOfTheTime()
    {
        // 24% and 14% before closing in; relocating between shots (the next step) brings it down
        Assert.True(Report(Computers, "computers").MotionlessInViewShare < 0.4f);
        Assert.True(Report(AgainstAnIdleHuman, "idle human").MotionlessInViewShare < 0.4f);
    }
}

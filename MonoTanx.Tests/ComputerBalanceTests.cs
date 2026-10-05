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
    public void TwoComputersShootEachOtherAtAboutTheRateItWasMeasuredAt()
    {
        var report = Report(Computers, "computers");

        // aim error alone: 5.7 hits a minute at 34% accuracy. With stuck recovery they roam instead of
        // sitting aimed at a wall, so they hit less often (see docs/tuning.md for why): about 1.7 at about 10%.
        Assert.InRange(report.HitsPerMinute, 0.7f, 4.5f);
        Assert.InRange(report.Accuracy, 0.04f, 0.3f);
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
    public void MostRoundsStillDrawOnTheTimeLimit()
    {
        var report = Report(Computers, "computers");

        Assert.True(report.RoundsDecided + report.RoundsDrawn > 0);
        Assert.InRange(report.DecidedShare, 0.0f, 0.5f);
    }

    [Fact]
    public void AComputerStillShootsAtAHumanWhoStandsStill()
    {
        var report = Report(AgainstAnIdleHuman, "idle human");

        Assert.True(report.Hits > 0);
        Assert.True(report.ShellsFired > 0);
    }
}

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
    private static readonly int[] Seeds = { 1, 7, 42, 1234, 4242, 99999 };
    private const float SecondsPerSeed = 600.0f;

    private readonly ITestOutputHelper output;

    // measured once for the whole class: it is the slow part
    private static readonly Lazy<BalanceReport> Computers = new Lazy<BalanceReport>(() =>
        BalanceReport.Sum(Seeds.Select(seed => new MatchHarness(seed, new MatchSetup(PlayerControl.Computer, PlayerControl.Computer)).Measure(SecondsPerSeed))));

    public ComputerBalanceTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private BalanceReport MeasureAll()
    {
        output.WriteLine(Computers.Value.ToString());
        return Computers.Value;
    }

    [Fact]
    public void TwoComputersHitEachOtherAboutAsOftenAsBefore()
    {
        var report = MeasureAll();

        // before the aim error: 6.1 hits a minute at 34% accuracy; with it: about 5.5 at about 33%
        Assert.InRange(report.HitsPerMinute, 4.0f, 7.5f);
        Assert.InRange(report.Accuracy, 0.22f, 0.45f);
        Assert.True(report.ShellsFired > 0);
    }

    [Fact]
    public void TheComputersStillSpendAGreatDealOfTimeStuck()
    {
        var report = MeasureAll();

        // the baseline for stuck recovery (a separate change): today about 32 seconds in every minute
        // some tank is driving into something without moving. When recovery lands this drops and the
        // pin moves with it.
        Assert.InRange(report.StuckSecondsPerMinute, 15.0f, 60.0f);
    }

    [Fact]
    public void MostRoundsStillDrawOnTheTimeLimit()
    {
        var report = MeasureAll();

        Assert.True(report.RoundsDecided + report.RoundsDrawn > 0);
        Assert.InRange(report.DecidedShare, 0.0f, 0.5f);
    }
}

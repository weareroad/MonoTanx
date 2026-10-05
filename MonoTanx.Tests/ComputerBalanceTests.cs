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
        // missing); closing in: about 9 a minute at about 50%; scooting to closer positions: about 12 at about 60%;
        // dodging shells it can see: about 8.5 a minute at about 45%
        Assert.InRange(report.HitsPerMinute, 5.0f, 15.0f);
        Assert.InRange(report.Accuracy, 0.3f, 0.8f);
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

        // 5% decided before they closed in (nearly every round ran out the clock), 56% after scooting, about
        // 35% once they dodge (27% in this shorter run)
        Assert.True(report.RoundsDecided + report.RoundsDrawn > 0);
        Assert.InRange(report.DecidedShare, 0.12f, 0.85f);
    }

    [Fact]
    public void AComputerShootsAndHitsAHumanWhoStandsStill()
    {
        var report = Report(AgainstAnIdleHuman, "idle human");

        // it re-aims for every shot from a new position, so it hits about 40% of them (91% when it stayed put
        // and kept its first alignment), and nine hits inside a round is then out of reach: see docs/tuning.md.
        // This pins that it still shoots and hits, not that it kills.
        Assert.True(report.Hits > 0);
        Assert.InRange(report.Accuracy, 0.25f, 0.8f);
    }

    [Fact]
    public void TheComputerIsNotMotionlessInViewForMostOfTheTime()
    {
        // 24% and 14% before closing in, 20% and 12% after, about 1% with relocation between shots
        Assert.True(Report(Computers, "computers").MotionlessInViewShare < 0.1f);
        Assert.True(Report(AgainstAnIdleHuman, "idle human").MotionlessInViewShare < 0.1f);
    }
}

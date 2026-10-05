using Microsoft.Xna.Framework;
using System.Linq;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

// The rules read the settings they are given, not Tuning: a changed value changes the rule,
// and the seats' multipliers follow who controls the seat.
public class SettingsRulesTests
{
    private static Player NewTank(GameSettings settings, Seat seat, bool computer = false) =>
        new Player("Tank", "Sprites/tank", Color.White, Player.StandardAmmunition(settings), settings.GetWhole(SettingKeys.StartingShells),
            isComputerControlled: computer, settings: settings, seat: seat);

    private static GameSettings With(params (string Key, float Value)[] changes)
    {
        var settings = new GameSettings();
        foreach (var (key, value) in changes)
            settings.Set(key, value);
        return settings;
    }

    [Fact]
    public void DefaultSettingsGiveTheTuningValues()
    {
        var tank = NewTank(new GameSettings(), Seat.One);

        Assert.Equal(Tuning.Tank.ForwardSpeed, tank.MovementSpeed);
        Assert.Equal(Tuning.Tank.ReverseSpeed, tank.ReverseMovementSpeed);
        Assert.Equal(Tuning.Tank.TurnSpeed, tank.TurnSpeed);
        Assert.Equal(Tuning.Tank.ForwardFuelPerSecond, tank.ForwardFuelPerSecond);
        Assert.Equal(Tuning.Tank.ForwardFuelPerSecond * Tuning.Tank.ReverseFuelMultiplier, tank.ReverseFuelPerSecond);
        Assert.Equal(Tuning.Tank.TurnFuelPerSecond, tank.TurnFuelPerSecond);
        Assert.Equal(Tuning.Tank.MaximumHealth, tank.Health);
        Assert.Equal(Tuning.Tank.MaximumFuel, tank.Fuel);
        Assert.Equal(Tuning.Tank.CollisionRadius, tank.CollisionRadius);
        Assert.Equal(Tuning.Ai.EngageDistanceTiles, tank.PreferredCombatDistanceTiles);
        var ammunition = Player.StandardAmmunition(new GameSettings());
        Assert.Equal(Player.DefaultAmmunition.ReloadTimeSeconds, ammunition.ReloadTimeSeconds);
        Assert.Equal(Player.DefaultAmmunition.Speed, ammunition.Speed);
        Assert.Equal(Player.DefaultAmmunition.Damage, ammunition.Damage);
        Assert.Equal(Player.DefaultAmmunition.MaxFlightDurationSeconds, ammunition.MaxFlightDurationSeconds);
    }

    [Fact]
    public void TankValuesComeFromTheSettings()
    {
        var settings = With((SettingKeys.MaximumHealth, 50), (SettingKeys.MaximumFuel, 80), (SettingKeys.StartingShells, 7),
            (SettingKeys.ForwardSpeed, 120), (SettingKeys.TurnSpeed, 3), (SettingKeys.CollisionRadius, 5),
            (SettingKeys.ForwardFuelPerSecond, 8), (SettingKeys.ReverseFuelMultiplier, 3), (SettingKeys.TurnFuelPerSecond, 1),
            (SettingKeys.AiEngageDistanceTiles, 4), (SettingKeys.AiSkill, 0.9f));

        var tank = NewTank(settings, Seat.One);

        Assert.Equal(50, tank.Health);
        Assert.Equal(80.0f, tank.Fuel);
        Assert.Equal(7, tank.RemainingAmmunition);
        Assert.Equal(120.0f, tank.MovementSpeed);
        Assert.Equal(3.0f, tank.TurnSpeed);
        Assert.Equal(5.0f, tank.CollisionRadius);
        Assert.Equal(8.0f, tank.ForwardFuelPerSecond);
        Assert.Equal(24.0f, tank.ReverseFuelPerSecond);
        Assert.Equal(1.0f, tank.TurnFuelPerSecond);
        Assert.Equal(4, tank.PreferredCombatDistanceTiles);
        Assert.Equal(0.9f, tank.ComputerSkill);
    }

    [Fact]
    public void ShellValuesComeFromTheSettings()
    {
        var ammunition = Player.StandardAmmunition(With((SettingKeys.ReloadSeconds, 1.5f), (SettingKeys.MaxFlightSeconds, 2), (SettingKeys.ShellSpeed, 300), (SettingKeys.Damage, 30)));

        Assert.Equal(1.5f, ammunition.ReloadTimeSeconds);
        Assert.Equal(2.0f, ammunition.MaxFlightDurationSeconds);
        Assert.Equal(300.0f, ammunition.Speed);
        Assert.Equal(30, ammunition.Damage);
    }

    [Theory]
    [InlineData(Seat.One)]
    [InlineData(Seat.Two)]
    public void AHumanSeatUsesItsOwnMultipliersAndTheOthersDoNotApply(Seat seat)
    {
        var settings = With(
            (SettingKeys.SeatPrefixPlayerOne + SettingKeys.SpeedSuffix, 2.0f), (SettingKeys.SeatPrefixPlayerOne + SettingKeys.FuelSuffix, 3.0f), (SettingKeys.SeatPrefixPlayerOne + SettingKeys.ReloadSuffix, 0.5f),
            (SettingKeys.SeatPrefixPlayerTwo + SettingKeys.SpeedSuffix, 0.5f), (SettingKeys.SeatPrefixPlayerTwo + SettingKeys.FuelSuffix, 2.0f), (SettingKeys.SeatPrefixPlayerTwo + SettingKeys.ReloadSuffix, 2.0f),
            (SettingKeys.SeatPrefixComputer + SettingKeys.SpeedSuffix, 1.5f));
        var tank = NewTank(settings, seat);
        var one = seat == Seat.One;

        Assert.Equal(Tuning.Tank.ForwardSpeed * (one ? 2.0f : 0.5f), tank.MovementSpeed);
        Assert.Equal(Tuning.Tank.TurnSpeed * (one ? 2.0f : 0.5f), tank.TurnSpeed);
        Assert.Equal(Tuning.Tank.ForwardFuelPerSecond * (one ? 3.0f : 2.0f), tank.ForwardFuelPerSecond);
        Assert.Equal(one ? 0.5f : 2.0f, tank.ReloadMultiplier);
    }

    [Theory]
    [InlineData(Seat.One)]
    [InlineData(Seat.Two)]
    public void AComputerInEitherSeatUsesTheComputerMultipliersAndFollowsAToggle(Seat seat)
    {
        var settings = With((SettingKeys.SeatPrefixComputer + SettingKeys.SpeedSuffix, 1.5f), (SettingKeys.SeatPrefixComputer + SettingKeys.ReloadSuffix, 2.0f));
        var tank = NewTank(settings, seat, computer: true);

        Assert.Equal(Tuning.Tank.ForwardSpeed * 1.5f, tank.MovementSpeed);
        Assert.Equal(2.0f, tank.ReloadMultiplier);

        tank.IsComputerControlled = false; // the control is toggled in play (F2, F4)

        Assert.Equal(Tuning.Tank.ForwardSpeed, tank.MovementSpeed);
        Assert.Equal(1.0f, tank.ReloadMultiplier);
    }

    [Fact]
    public void ThePredictionCopyKeepsTheMultipliers()
    {
        var tank = NewTank(With((SettingKeys.SeatPrefixComputer + SettingKeys.SpeedSuffix, 1.5f), (SettingKeys.TurnFuelPerSecond, 1)), Seat.Two, computer: true);

        var copy = tank.CopyForPrediction();

        Assert.Equal(tank.MovementSpeed, copy.MovementSpeed);
        Assert.Equal(tank.TurnFuelPerSecond, copy.TurnFuelPerSecond);
    }

    [Fact]
    public void TheReloadMultiplierScalesTheReloadAfterAShot()
    {
        var tank = NewTank(With((SettingKeys.ReloadSeconds, 2), (SettingKeys.SeatPrefixPlayerOne + SettingKeys.ReloadSuffix, 1.5f)), Seat.One);

        Assert.True(tank.TryFire(8.0f, out _));

        Assert.Equal(3.0f, tank.ReloadTimer);
    }

    [Fact]
    public void TurningFuelUseFollowsTheTankNotTheConstant()
    {
        var map = TestSupport.LoadTerrainMap();
        var cheap = NewTank(With((SettingKeys.TurnFuelPerSecond, 0)), Seat.One);
        var dear = NewTank(With((SettingKeys.TurnFuelPerSecond, 4)), Seat.One);
        cheap.Position = dear.Position = TestSupport.Centre(1, 1);
        var command = new TankCommand(1.0f, 0.0f);

        Assert.Equal(0.0f, TankMovement.FuelCost(map, cheap, command, 1.0f));
        Assert.Equal(4.0f, TankMovement.FuelCost(map, dear, command, 1.0f), 3);
    }

    [Fact]
    public void KnockbackAndHeadingDisruptionAreParameters()
    {
        var map = TestSupport.LoadTerrainMap();
        var tank = TestSupport.NewPlayer(TestSupport.Centre(1, 2), heading: 0.0f);
        var other = TestSupport.NewBystander();

        TankDamage.Apply(map, tank, other, 5, new Vector2(1.0f, 0.0f), new System.Random(1), knockbackDistance: 4.0f, maximumHeadingDisruptionRadians: 0.0f);

        Assert.Equal(TestSupport.Centre(1, 2).X + 4.0f, tank.Position.X, 3);
        Assert.Equal(0.0f, tank.Heading);
    }

    [Fact]
    public void ThePickupRadiusIsAParameter()
    {
        var tank = TestSupport.NewPlayer(new Vector2(100, 100));
        var spawn = new PickupSpawn(1, PickupKind.Fuel, new Vector2(120, 100), 10, null, "x");

        Assert.False(PickupRules.InRange(tank, spawn));
        Assert.True(PickupRules.InRange(tank, spawn, 25.0f));
    }

    [Fact]
    public void ADifferentSeedAndSettingsPlayTheSameWhenTheSettingsAreTheDefaults()
    {
        var plain = new MatchHarness(42, MatchSetup.Demo).RunMatch(120);
        var explicitDefaults = new MatchHarness(42, MatchSetup.Demo, settings: new GameSettings()).RunMatch(120);

        Assert.Equal(plain.Log, explicitDefaults.Log);
    }

    [Fact]
    public void DamageChangesWhatAHitDoes()
    {
        var weak = new MatchHarness(7, MatchSetup.Demo, settings: With((SettingKeys.Damage, 1))).Run(60);
        var strong = new MatchHarness(7, MatchSetup.Demo, settings: With((SettingKeys.Damage, 40))).Run(60);

        Assert.True(weak.PlayerOne.Health + weak.PlayerTwo.Health > strong.PlayerOne.Health + strong.PlayerTwo.Health);
    }

    [Fact]
    public void ARoundTimeLimitFromTheSettingsEndsTheRound()
    {
        var harness = new MatchHarness(3, MatchSetup.Demo, settings: With((SettingKeys.CountdownSeconds, 0), (SettingKeys.RoundTimeLimitSeconds, 10), (SettingKeys.Damage, 1)));

        harness.RunMatch(30);

        Assert.True(harness.Session.State.Round > 1, "a 10 second limit should have ended the first round within 30 seconds");
    }

    [Fact]
    public void RoundsToWinAndPausesComeFromTheSettings()
    {
        var settings = With((SettingKeys.RoundsToWin, 2), (SettingKeys.CountdownSeconds, 1.5f));
        var session = new MatchHarness(1, MatchSetup.Demo, settings: settings).Session;

        Assert.Equal(1.5f, session.State.CountdownRemaining);
    }

    [Fact]
    public void ShellsLaunchedInAMatchUseTheReflectionLimit()
    {
        var harness = new MatchHarness(5, MatchSetup.Demo, settings: With((SettingKeys.MaxReflections, 2)));
        var tank = harness.PlayerOne;

        Assert.True(tank.TryFire(8.0f, out var launch));
        harness.Simulation.Launch(tank, launch);

        Assert.Equal(2, harness.Simulation.Shells[0].MaxReflections);
    }

    [Fact]
    public void TheRunLogHeaderListsOnlyWhatDiffersFromTheDefaults()
    {
        var plain = new MatchHarness(1, MatchSetup.Demo, recordLog: true);
        var tuned = new MatchHarness(1, MatchSetup.Demo, recordLog: true, settings: With((SettingKeys.AiSkill, 0.9f), (SettingKeys.Damage, 20)));

        Assert.Contains("# settings (all defaults)", plain.LogLines);
        Assert.Contains("# settings shell.damage=20 ai.skill=0.9", tuned.LogLines);
    }

    [Fact]
    public void PickupAmountsWhenTheMapSaysNothingComeFromTheSettings()
    {
        var settings = With((SettingKeys.DefaultFuelAmount, 90), (SettingKeys.DefaultAmmunitionAmount, 9));

        var spawns = new WorldMap(TestSupport.FixturePath("terrain.tmx"), settings).PickupSpawns;
        var defaults = new WorldMap(TestSupport.FixturePath("terrain.tmx")).PickupSpawns;

        Assert.Equal(Tuning.Pickups.DefaultFuelAmount, defaults[0].Amount);
        Assert.Equal(90, spawns[0].Amount);   // the map gives no amount
        Assert.Equal(8, spawns[1].Amount);    // the map gives its own
        Assert.Equal(90, spawns[2].Amount);   // an amount of 0 means "not said"
    }

    [Fact]
    public void TheComputerReadsItsGroupFromTheSettings()
    {
        static int Shots(GameSettings settings) =>
            new MatchHarness(11, MatchSetup.Demo, settings: settings).Run(90).Log.Count(entry => entry.Kind == MatchEventKind.ShellFired);

        var slow = Shots(With((SettingKeys.CountdownSeconds, 0), (SettingKeys.AiFireCooldownSeconds, 10)));
        var quick = Shots(With((SettingKeys.CountdownSeconds, 0), (SettingKeys.AiFireCooldownSeconds, 1.5f), (SettingKeys.ReloadSeconds, 1)));

        Assert.True(quick > slow, $"a shorter cooldown and reload should fire more (quick {quick}, slow {slow})");
    }

    [Fact]
    public void AComputerThatNeverNoticesShellsNeverDodges()
    {
        var blind = new MatchHarness(21, MatchSetup.Demo, recordLog: true,
            settings: With((SettingKeys.AiEvadeDetectionDistance, 0), (SettingKeys.CountdownSeconds, 0))).Run(120);

        var sighted = new MatchHarness(21, MatchSetup.Demo, recordLog: true, settings: With((SettingKeys.CountdownSeconds, 0))).Run(120);

        Assert.Contains(sighted.LogLines, line => line.Contains("DODGE "));   // so the run is long enough to dodge
        Assert.DoesNotContain(blind.LogLines, line => line.Contains("DODGE "));
    }
}

using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class MatchSimulationTests
{
    private const float Step = 1.0f / 60.0f;

    private static MatchSimulation NewSimulation(Player one, Player two, int seed = 1, WorldMap map = null)
    {
        map ??= LoadTerrainMap();
        return new MatchSimulation(map, one, two, map.PickupSpawns, new Random(seed));
    }

    private static ShellLaunch Launch(Vector2 position, Vector2 velocity) => new ShellLaunch(Player.DefaultAmmunition, position, velocity);

    private static void RunShells(MatchSimulation simulation, int steps = 120)
    {
        for (var i = 0; i < steps && simulation.Shells.Count > 0; i++)
            simulation.StepShells(Step);
    }

    private static List<MatchEventKind> Kinds(MatchSimulation simulation) => simulation.Events.Select(e => e.Kind).ToList();

    [Fact]
    public void LaunchingAShellPutsItInFlightAndReportsWhoFired()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(7, 3));
        var simulation = NewSimulation(one, two);

        simulation.Launch(two, Launch(Centre(6, 3), new Vector2(-100, 0)));

        Assert.Single(simulation.Shells);
        var fired = Assert.Single(simulation.Events);
        Assert.Equal(MatchEventKind.ShellFired, fired.Kind);
        Assert.Equal(Seat.Two, fired.Seat);
    }

    [Fact]
    public void AShellThatHitsAWallReportsItAndIsRemoved()
    {
        var simulation = NewSimulation(NewPlayer(Centre(0, 0)), NewPlayer(Centre(7, 3)));
        simulation.Launch(simulation.TankOf(Seat.One), Launch(new Vector2(40, 24), new Vector2(100, 0))); // wall at tile (3, 1)
        simulation.ClearEvents();

        RunShells(simulation);

        Assert.Empty(simulation.Shells);
        Assert.Equal(new[] { MatchEventKind.ShellHitTerrain }, Kinds(simulation));
        Assert.Null(simulation.Events[0].Seat);
    }

    [Fact]
    public void AShellThatHitsATankDamagesItAndSetsTheComputerToRetaliate()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(5, 0));
        var simulation = NewSimulation(one, two);
        simulation.Launch(one, Launch(Centre(1, 0), new Vector2(200, 0)));
        simulation.ClearEvents();

        RunShells(simulation);

        Assert.Empty(simulation.Shells);
        Assert.Equal(two.MaximumHealth - Player.DefaultAmmunition.Damage, two.Health);
        Assert.Equal(one.MaximumHealth, one.Health);
        var hit = Assert.Single(simulation.Events);
        Assert.Equal(MatchEventKind.TankHit, hit.Kind);
        Assert.Equal(Seat.Two, hit.Seat);
        Assert.Equal(Tuning.Ai.RetaliationSeconds, simulation.ControllerOf(Seat.Two).RetaliationTimer);
        Assert.Equal(0.0f, simulation.ControllerOf(Seat.One).RetaliationTimer);
    }

    [Fact]
    public void ShellsCanHitTheTankThatFiredThem()
    {
        var one = NewPlayer(Centre(3, 0));
        var simulation = NewSimulation(one, NewPlayer(Centre(7, 3)));
        simulation.Launch(one, Launch(Centre(3, 0) + new Vector2(2, 0), new Vector2(-1, 0) * 0.1f)); // starts inside its own tank
        simulation.ClearEvents();

        RunShells(simulation);

        Assert.Equal(new[] { MatchEventKind.TankHit }, Kinds(simulation));
        Assert.Equal(Seat.One, simulation.Events[0].Seat);
    }

    [Fact]
    public void ADestroyingHitReportsTheTankHitThenDestroyed()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(5, 0));
        two.Health = 1;
        var simulation = NewSimulation(one, two);
        simulation.Launch(one, Launch(Centre(1, 0), new Vector2(200, 0)));
        simulation.ClearEvents();

        RunShells(simulation);

        Assert.Equal(0, two.Health);
        Assert.Equal(new[] { MatchEventKind.TankHit, MatchEventKind.TankDestroyed }, Kinds(simulation));
        Assert.All(simulation.Events, e => Assert.Equal(Seat.Two, e.Seat));
    }

    [Fact]
    public void AShellThatReflectsThenHitsATankReportsBothInOrder()
    {
        // fired down and to the right onto the reflective tiles in row 3 (it lands at x = 50), it bounces up and to the right into the tank
        var one = NewPlayer(new Vector2(62, 36));
        var two = NewPlayer(Centre(7, 3));
        var simulation = NewSimulation(one, two);
        simulation.Launch(two, Launch(new Vector2(32, 30), new Vector2(60, 60)));
        simulation.ClearEvents();

        RunShells(simulation, 240);

        Assert.Equal(new[] { MatchEventKind.ShellReflected, MatchEventKind.TankHit }, Kinds(simulation));
        Assert.Equal(Seat.One, simulation.Events[1].Seat);
    }

    [Fact]
    public void ACollectedPickupIsReportedOnceAndDeactivated()
    {
        var one = NewPlayer(Centre(1, 2)); // the fixture's fuel pickup is at (24, 40)
        one.Fuel = 10.0f;
        var simulation = NewSimulation(one, NewPlayer(Centre(7, 3)));

        simulation.CollectPickups();
        simulation.CollectPickups();

        var collected = Assert.Single(simulation.Events);
        Assert.Equal(MatchEventKind.PickupCollected, collected.Kind);
        Assert.Equal(Seat.One, collected.Seat);
        Assert.True(one.Fuel > 10.0f);
        Assert.Contains(simulation.Pickups, p => !p.Active);
    }

    [Fact]
    public void SeatTwoCollectsPickupsToo()
    {
        var two = NewPlayer(Centre(1, 2));
        var simulation = NewSimulation(NewPlayer(Centre(7, 3)), two);

        simulation.CollectPickups();

        Assert.Equal(Seat.Two, Assert.Single(simulation.Events).Seat);
    }

    [Fact]
    public void ClearEventsEmptiesTheList()
    {
        var one = NewPlayer(Centre(1, 2));
        var simulation = NewSimulation(one, NewPlayer(Centre(7, 3)));
        simulation.CollectPickups();

        simulation.ClearEvents();

        Assert.Empty(simulation.Events);
    }

    [Fact]
    public void TheHitReactionIsReproducibleFromTheSeed()
    {
        float HeadingAfterHit(int seed)
        {
            var one = NewPlayer(Centre(0, 0));
            var two = NewPlayer(Centre(5, 0));
            var simulation = NewSimulation(one, two, seed);
            simulation.Launch(one, Launch(Centre(1, 0), new Vector2(200, 0)));
            RunShells(simulation);
            return two.Heading;
        }

        Assert.Equal(HeadingAfterHit(7), HeadingAfterHit(7));
        Assert.NotEqual(HeadingAfterHit(7), HeadingAfterHit(8));
    }

    [Fact]
    public void ControllersAndTanksBelongToTheirSeats()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(7, 3));
        var simulation = NewSimulation(one, two);

        Assert.Same(one, simulation.TankOf(Seat.One));
        Assert.Equal(Seat.Two, simulation.SeatOf(two));
        Assert.Same(one, simulation.OpponentOf(two));
        Assert.NotSame(simulation.ControllerOf(Seat.One), simulation.ControllerOf(Seat.Two));
    }

    // Step: the update as a whole

    private static MatchSimulation Human(Player one, Player two, float muzzle = 20.0f)
    {
        one.IsComputerControlled = false;
        two.IsComputerControlled = false;
        var map = LoadTerrainMap();
        return new MatchSimulation(map, one, two, new PickupSpawn[0], new Random(1), new SimulationSettings(muzzle, muzzle));
    }

    [Fact]
    public void AHumanCommandMovesTheTankAndReportsItDroveAndHowItMoved()
    {
        var one = NewPlayer(Centre(1, 0));
        var simulation = Human(one, NewPlayer(Centre(7, 3)));
        var start = one.Position;

        simulation.Step(Step, new TankCommand(0.0f, 1.0f), null);

        Assert.True(one.Position.X > start.X);
        Assert.True(simulation.Moved(Seat.One));
        Assert.False(simulation.Moved(Seat.Two));
        Assert.Equal(TankMotion.Forward, simulation.Motion(Seat.One));
        Assert.Equal(TankMotion.Idle, simulation.Motion(Seat.Two));
    }

    [Fact]
    public void FiringNeedsTheFireFlagAndShootsFromTheMuzzleOffset()
    {
        var one = NewPlayer(Centre(1, 0));
        var simulation = Human(one, NewPlayer(Centre(7, 3)), muzzle: 20.0f);

        simulation.Step(Step, new TankCommand(0.0f, 0.0f), null);
        Assert.Empty(simulation.Shells);

        simulation.Step(Step, new TankCommand(0.0f, 0.0f, fire: true), null);

        var shell = Assert.Single(simulation.Shells);
        Assert.Equal(MatchEventKind.ShellFired, Assert.Single(simulation.Events).Kind);
        Assert.Equal(one.Position.X + 20.0f + shell.Velocity.X * Step, shell.Position.X, 0.01f); // placed at the muzzle, then flown for this step
        Assert.Equal(Seat.One, simulation.Events[0].Seat);
        Assert.True(one.ReloadTimer > 0.0f);
    }

    [Fact]
    public void ATankCannotFireAgainUntilItHasReloaded()
    {
        var one = NewPlayer(Centre(1, 0));
        var simulation = Human(one, NewPlayer(Centre(7, 3)));
        var fire = new TankCommand(0.0f, 0.0f, fire: true);

        simulation.Step(Step, fire, null);
        simulation.Step(Step, fire, null);

        Assert.Single(simulation.Shells);
    }

    [Fact]
    public void FinishingAReloadIsReported()
    {
        var one = NewPlayer(Centre(1, 0));
        var simulation = Human(one, NewPlayer(Centre(7, 3)));
        simulation.Step(Step, new TankCommand(0.0f, 0.0f, fire: true), null);
        simulation.ClearEvents();

        simulation.Step(one.ReloadTimer + 0.01f, null, null);

        var reloaded = simulation.Events.Single(e => e.Kind == MatchEventKind.ReloadReady);
        Assert.Equal(Seat.One, reloaded.Seat);
    }

    [Fact]
    public void ANullCommandLeavesAHumanSeatStill()
    {
        var one = NewPlayer(Centre(1, 0));
        var simulation = Human(one, NewPlayer(Centre(7, 3)));
        var start = one.Position;

        simulation.Step(Step, null, null);

        Assert.Equal(start, one.Position);
    }

    [Fact]
    public void AComputerSeatIgnoresHumanCommandsAndDrivesItself()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(7, 0), MathHelper.Pi);
        var map = LoadTerrainMap();
        two.IsComputerControlled = true;
        var simulation = new MatchSimulation(map, one, two, new PickupSpawn[0], new Random(1));
        var start = two.Position;

        simulation.Step(Step, null, new TankCommand(0.0f, -1.0f)); // the "command" for the computer's seat is not used

        Assert.True(two.Position.X < start.X); // it pursues the distant opponent by its own rules, not the reverse command
        Assert.False(simulation.Moved(Seat.Two)); // a computer has no drive animation
    }

    [Fact]
    public void AComputerAimsAndFiresAtAnOpponentInView()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(2, 0), MathHelper.Pi); // facing seat one, 32 away, clear view
        two.IsComputerControlled = true;
        var map = LoadTerrainMap();
        var simulation = new MatchSimulation(map, one, two, new PickupSpawn[0], new Random(1), new SimulationSettings(20.0f, 20.0f));
        one.IsComputerControlled = false;

        simulation.Step(Step, null, null);

        Assert.Contains(simulation.Events, e => e.Kind == MatchEventKind.ShellFired && e.Seat == Seat.Two);
        Assert.True(simulation.ControllerOf(Seat.Two).FireTimer > 0.0f); // ShotFired was reported to the controller
    }

    [Fact]
    public void ThePlayerTwoSeatIsDrivenByCommandTwo()
    {
        var two = NewPlayer(Centre(1, 0));
        var simulation = Human(NewPlayer(Centre(7, 3)), two);
        var start = two.Position;

        simulation.Step(Step, null, new TankCommand(0.0f, 1.0f));

        Assert.True(two.Position.X > start.X);
        Assert.True(simulation.Moved(Seat.Two));
    }

    [Fact]
    public void EngineMotionIsJudgedBeforeShellsKnockATankAbout()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(5, 0));
        var simulation = Human(one, two);
        simulation.Launch(one, Launch(Centre(4, 0), new Vector2(200, 0)));
        var start = two.Position;

        simulation.Step(0.1f, null, null);

        Assert.NotEqual(start, two.Position); // the hit knocked it
        Assert.Equal(TankMotion.Idle, simulation.Motion(Seat.Two)); // but it was not driving
    }

    [Fact]
    public void SettingControlSwitchesTheSeatAndResetsItsComputer()
    {
        var simulation = Human(NewPlayer(Centre(0, 0)), NewPlayer(Centre(7, 3)));
        simulation.ControllerOf(Seat.Two).Hit();

        simulation.SetComputerControlled(Seat.Two, true);

        Assert.True(simulation.TankOf(Seat.Two).IsComputerControlled);
        Assert.Equal(0.0f, simulation.ControllerOf(Seat.Two).RetaliationTimer);
    }

    [Fact]
    public void PlacingAtTheStartPutsTheTanksInOppositeCornersFacingEachOther()
    {
        var map = new WorldMap(FixturePath("arena", "arena_01.tmx"));
        var one = NewPlayer(Vector2.Zero);
        var two = NewPlayer(Vector2.Zero);
        var simulation = new MatchSimulation(map, one, two, map.PickupSpawns, new Random(1));

        simulation.PlaceAtStart();

        Assert.True(map.CanOccupyCircle(one.Position, one.CollisionRadius));
        Assert.True(map.CanOccupyCircle(two.Position, two.CollisionRadius));
        Assert.True(one.Position.X < two.Position.X && one.Position.Y < two.Position.Y);
        Assert.Equal(Math.Atan2(two.Position.Y - one.Position.Y, two.Position.X - one.Position.X), one.Heading, 4);
        Assert.Equal(Math.Atan2(one.Position.Y - two.Position.Y, one.Position.X - two.Position.X), two.Heading, 4);
    }

    // ResetRound

    [Fact]
    public void ResettingARoundPutsEverythingBackAsAtTheStart()
    {
        var map = new WorldMap(FixturePath("arena", "arena_01.tmx"));
        var one = NewPlayer(Vector2.Zero);
        var two = NewPlayer(Vector2.Zero);
        var simulation = new MatchSimulation(map, one, two, map.PickupSpawns, new Random(1), new SimulationSettings(20.0f, 20.0f));
        simulation.PlaceAtStart();
        var startOne = (one.Position, one.Heading);
        var startTwo = (two.Position, two.Heading);
        // wreck the round: damage, move, spend, fire, collect
        one.Health = 10; two.Health = 0;
        one.Fuel = 3; two.Fuel = 0;
        foreach (var slot in one.AmmunitionSlots) slot.Remaining = 0;
        one.Position += new Vector2(40, 40);
        one.Heading = 2.0f;
        two.ReloadTimer = 2.0f;
        simulation.Launch(one, Launch(one.Position, new Vector2(100, 0)));
        foreach (var pickup in simulation.Pickups) pickup.Active = false;
        simulation.ControllerOf(Seat.One).Hit();
        simulation.ClearEvents();

        simulation.ResetRound();

        Assert.Equal(startOne, (one.Position, one.Heading));
        Assert.Equal(startTwo, (two.Position, two.Heading));
        foreach (var tank in new[] { one, two })
        {
            Assert.Equal(tank.MaximumHealth, tank.Health);
            Assert.Equal(tank.MaximumFuel, tank.Fuel);
            Assert.Equal(tank.StartingAmmunition, tank.RemainingAmmunition);
            Assert.Equal(0.0f, tank.ReloadTimer);
        }
        Assert.Empty(simulation.Shells);
        Assert.All(simulation.Pickups, p => Assert.True(p.Active));
        Assert.Equal(0.0f, simulation.ControllerOf(Seat.One).RetaliationTimer);
        Assert.False(simulation.Moved(Seat.One));
        Assert.Equal(TankMotion.Idle, simulation.Motion(Seat.One));
    }

    [Fact]
    public void ResettingARoundKeepsWhoControlsEachSeatAndTheEventsNotYetRead()
    {
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(7, 3));
        var simulation = NewSimulation(one, two);
        simulation.SetComputerControlled(Seat.One, true);
        simulation.Launch(one, Launch(Centre(1, 0), new Vector2(100, 0)));

        simulation.ResetRound();

        Assert.True(one.IsComputerControlled);
        Assert.False(two.IsComputerControlled);
        Assert.Equal(MatchEventKind.ShellFired, Assert.Single(simulation.Events).Kind);
    }

    [Fact]
    public void ARoundResetDoesNotRewindTheRandomStream()
    {
        // the heading disruption of each of two hits, with and without a reset between them
        float[] Disruptions(bool resetBetween)
        {
            var map = new WorldMap(FixturePath("arena", "arena_01.tmx"));
            var one = NewPlayer(Vector2.Zero);
            var two = NewPlayer(Vector2.Zero);
            var simulation = new MatchSimulation(map, one, two, new PickupSpawn[0], new Random(5), new SimulationSettings(20.0f, 20.0f));
            simulation.PlaceAtStart();
            var deltas = new List<float>();
            for (var hit = 0; hit < 2; hit++)
            {
                if (hit == 1 && resetBetween) simulation.ResetRound();
                two.Position = one.Position + new Vector2(60, 0);
                var before = two.Heading;
                simulation.Launch(one, Launch(one.Position + new Vector2(20, 0), new Vector2(200, 0)));
                RunShells(simulation);
                deltas.Add(MathHelper.WrapAngle(two.Heading - before));
            }
            return deltas.ToArray();
        }

        var plain = Disruptions(resetBetween: false);
        var reset = Disruptions(resetBetween: true);

        Assert.NotEqual(plain[0], plain[1]);
        Assert.Equal(plain[1], reset[1], 4); // the reset did not start the stream over
    }

    [Fact]
    public void EachSeatsComputerGetsItsOwnAimStream()
    {
        var map = LoadTerrainMap();
        var one = NewPlayer(Centre(0, 0));
        var two = NewPlayer(Centre(3, 0), MathHelper.Pi);
        var streams = new RandomStreams(8);
        one.IsComputerControlled = true;
        two.IsComputerControlled = true;
        var simulation = new MatchSimulation(map, one, two, new PickupSpawn[0], new Random(1), default, streams.CreateStream("ai-1"), streams.CreateStream("ai-2"));

        simulation.Step(Step, null, null);

        Assert.NotEqual(simulation.ControllerOf(Seat.One).AimError, simulation.ControllerOf(Seat.Two).AimError);
        Assert.True(simulation.ControllerOf(Seat.One).AimError > Tuning.Ai.AimToleranceRadians - 0.0001f);
    }
}

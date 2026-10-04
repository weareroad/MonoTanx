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
}

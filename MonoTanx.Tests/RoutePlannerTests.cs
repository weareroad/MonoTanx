using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class RoutePlannerTests
{
    private const float Radius = Tuning.Tank.CollisionRadius;

    private static WorldMap Terrain() => LoadTerrainMap();
    private static WorldMap Walled() => new WorldMap(FixturePath("walled.tmx"));
    private static WorldMap Arena() => new WorldMap(FixturePath("arena", "arena_01.tmx"));

    private static void AssertValidRoute(WorldMap map, float radius, Point start, Point goal, List<Point> route)
    {
        Assert.NotNull(route);
        Assert.Equal(start, route[0]);
        Assert.Equal(goal, route[^1]);
        for (var i = 0; i < route.Count; i++)
        {
            Assert.True(map.CanOccupyCircle(map.GetTileBounds(route[i]).Center.ToVector2(), radius), $"tile {route[i]} cannot be occupied");
            if (i > 0)
                Assert.Equal(1, Math.Abs(route[i].X - route[i - 1].X) + Math.Abs(route[i].Y - route[i - 1].Y)); // one step, no diagonals
        }
    }

    [Fact]
    public void ARouteFromATileToItselfIsThatTile()
    {
        var route = RoutePlanner.FindRoute(Terrain(), Radius, new Point(3, 2), new Point(3, 2));

        Assert.Equal(new[] { new Point(3, 2) }, route);
    }

    [Fact]
    public void AStraightRunAcrossOpenGroundIsTheShortestRoute()
    {
        var route = RoutePlanner.FindRoute(Terrain(), Radius, new Point(0, 0), new Point(7, 0));

        AssertValidRoute(Terrain(), Radius, new Point(0, 0), new Point(7, 0), route);
        Assert.Equal(8, route.Count);
        Assert.All(route, tile => Assert.Equal(0, tile.Y));
    }

    [Fact]
    public void ARouteGoesAroundBlockingTerrain()
    {
        // row 1 has water, a wall, a ravine, a hill and a reflective tile between (0,1) and (7,1)
        var route = RoutePlanner.FindRoute(Terrain(), Radius, new Point(0, 1), new Point(7, 1));

        AssertValidRoute(Terrain(), Radius, new Point(0, 1), new Point(7, 1), route);
        Assert.Equal(10, route.Count); // up (or down), across, and back: 9 steps
    }

    [Fact]
    public void ABridgeCanBeCrossed()
    {
        var route = RoutePlanner.FindRoute(Terrain(), Radius, new Point(2, 0), new Point(2, 2));

        Assert.Equal(new[] { new Point(2, 0), new Point(2, 1), new Point(2, 2) }, route);
    }

    [Theory]
    [InlineData(1, 1)]   // water
    [InlineData(3, 1)]   // wall
    [InlineData(4, 1)]   // ravine
    [InlineData(5, 1)]   // hill
    [InlineData(3, 3)]   // reflective
    public void ABlockedGoalHasNoRoute(int x, int y)
    {
        Assert.Null(RoutePlanner.FindRoute(Terrain(), Radius, new Point(0, 0), new Point(x, y)));
    }

    [Fact]
    public void AGoalSealedInByWallsHasNoRoute()
    {
        var map = Walled();

        Assert.Null(RoutePlanner.FindRoute(map, Radius, new Point(0, 0), new Point(2, 2)));
        Assert.NotNull(RoutePlanner.FindRoute(map, Radius, new Point(0, 0), new Point(5, 3)));
    }

    [Fact]
    public void AGoalOutsideTheMapHasNoRoute()
    {
        Assert.Null(RoutePlanner.FindRoute(Terrain(), Radius, new Point(0, 0), new Point(20, 20)));
    }

    [Fact]
    public void ALargerTankCannotUseTilesBesideObstacles()
    {
        var map = Terrain();
        var start = new Point(0, 0);
        var goal = new Point(0, 2); // (0,1) sits next to the water at (1,1)

        var small = RoutePlanner.FindRoute(map, 6.0f, start, goal);
        var large = RoutePlanner.FindRoute(map, 9.0f, start, goal);

        Assert.Equal(new[] { new Point(0, 0), new Point(0, 1), new Point(0, 2) }, small);
        Assert.Null(large); // at 9px the tank touches the water from (0,1), and every way across row 1 is likewise narrow
    }

    [Fact]
    public void ARouteIsTheSameEveryTime()
    {
        var map = Arena();

        var first = RoutePlanner.FindRoute(map, Radius, new Point(2, 2), new Point(50, 33));
        var second = RoutePlanner.FindRoute(map, Radius, new Point(2, 2), new Point(50, 33));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ARouteAcrossTheRealArenaIsValid()
    {
        var map = Arena();
        var start = new Point(2, 2);
        var goal = new Point(50, 33);

        var route = RoutePlanner.FindRoute(map, Radius, start, goal);

        AssertValidRoute(map, Radius, start, goal, route);
        Assert.True(route.Count >= Math.Abs(goal.X - start.X) + Math.Abs(goal.Y - start.Y) + 1); // never shorter than the straight-line steps
    }

    // ---- combat routes

    [Fact]
    public void ACombatRouteEndsOnTheRingAroundTheOpponent()
    {
        var map = Arena();
        var start = new Point(2, 2);
        var opponent = new Point(50, 33);

        var route = RoutePlanner.FindCombatRoute(map, Radius, start, opponent, 6);

        Assert.NotNull(route);
        Assert.Equal(start, route[0]);
        var end = route[^1];
        var distance = Vector2.Distance(end.ToVector2(), opponent.ToVector2());
        Assert.InRange(distance, 6 - Tuning.Ai.CombatRingToleranceTiles, 6 + Tuning.Ai.CombatRingToleranceTiles);
        AssertValidRoute(map, Radius, start, end, route);
    }

    [Fact]
    public void ACombatRouteHeadsForTheNearestRingTileToTheOpponent()
    {
        var map = Arena();
        var opponent = new Point(30, 20);

        var route = RoutePlanner.FindCombatRoute(map, Radius, new Point(2, 2), opponent, 6);

        // the ring is tried nearest-first, so a reachable route ends at the innermost distance on it
        Assert.NotNull(route);
        var distance = Vector2.Distance(route[^1].ToVector2(), opponent.ToVector2());
        Assert.InRange(distance, 5.0f, 5.5f);
    }

    [Fact]
    public void ThePreferredDistanceIsNeverBelowTheMinimumRing()
    {
        var map = Arena();
        var opponent = new Point(30, 20);

        var route = RoutePlanner.FindCombatRoute(map, Radius, new Point(2, 2), opponent, 0);

        Assert.NotNull(route);
        var distance = Vector2.Distance(route[^1].ToVector2(), opponent.ToVector2());
        Assert.InRange(distance, Tuning.Ai.MinimumCombatRingTiles - Tuning.Ai.CombatRingToleranceTiles,
            Tuning.Ai.MinimumCombatRingTiles + Tuning.Ai.CombatRingToleranceTiles);
    }

    [Fact]
    public void ACombatRouteHasNoRingToReachWhenTheRingIsOffTheMap()
    {
        // an 8x4 map cannot hold a ring 12 tiles from anything
        Assert.Null(RoutePlanner.FindCombatRoute(Terrain(), Radius, new Point(0, 0), new Point(3, 2), 12));
    }

    [Fact]
    public void ACombatRouteIsTheSameEveryTime()
    {
        var map = Arena();

        var first = RoutePlanner.FindCombatRoute(map, Radius, new Point(2, 2), new Point(40, 30), 6);
        var second = RoutePlanner.FindCombatRoute(map, Radius, new Point(2, 2), new Point(40, 30), 6);

        Assert.Equal(first, second);
    }

    // The tenet: the planner knows nothing about who is who. Planning for either
    // tank across the same ground gives the same route for the same start and goal.
    [Fact]
    public void PlanningDoesNotDependOnWhichSeatAsks()
    {
        var map = Arena();
        var a = new Point(3, 3);
        var b = new Point(55, 35);

        var forward = RoutePlanner.FindRoute(map, Radius, a, b);
        var back = RoutePlanner.FindRoute(map, Radius, b, a);

        Assert.Equal(forward.Count, back.Count); // the shortest route is as long in either direction
    }

    [Fact]
    public void AnAlternativeCombatRouteGoesToADifferentGoal()
    {
        var map = Arena();
        var start = new Point(4, 4);
        var opponent = new Point(30, 20);

        var first = RoutePlanner.FindCombatRoute(map, Radius, start, opponent, 6);
        var second = RoutePlanner.FindCombatRoute(map, Radius, start, opponent, 6, alternative: 1);
        var third = RoutePlanner.FindCombatRoute(map, Radius, start, opponent, 6, alternative: 2);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first[^1], second[^1]);
        Assert.NotEqual(second[^1], third[^1]);
        AssertValidRoute(map, Radius, start, second[^1], second);
    }

    [Fact]
    public void AnAlternativeBeyondTheReachableGoalsTakesTheLastOne()
    {
        var map = Arena();
        var start = new Point(4, 4);
        var opponent = new Point(30, 20);

        var huge = RoutePlanner.FindCombatRoute(map, Radius, start, opponent, 6, alternative: 10000);
        var alsoHuge = RoutePlanner.FindCombatRoute(map, Radius, start, opponent, 6, alternative: 20000);

        Assert.NotNull(huge);
        Assert.Equal(huge[^1], alsoHuge[^1]);
    }

    [Fact]
    public void WithNoReachableGoalAnAlternativeStillFindsNothing()
    {
        var map = Walled();

        Assert.Null(RoutePlanner.FindCombatRoute(map, Radius, new Point(0, 0), new Point(2, 2), 6, alternative: 1));
    }
}

using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class ComputerControllerTests
{
    private const float Step = 1.0f / 60.0f;
    private static readonly IReadOnlyList<PickupState> NoPickups = new List<PickupState>();

    private static WorldMap Walled() => new WorldMap(FixturePath("walled.tmx"));
    private static WorldMap Arena() => new WorldMap(FixturePath("arena", "arena_01.tmx"));

    private static PickupState FuelDrop(int id, Vector2 position, bool active = true) =>
        new PickupState(new PickupSpawn(id, PickupKind.Fuel, position, 50, null, "Sprites/fueldrop_1")) { Active = active };

    private static PickupState AmmoDrop(int id, Vector2 position) =>
        new PickupState(new PickupSpawn(id, PickupKind.Ammunition, position, 5, "standard-shell", "Sprites/ammodrop_1"));

    private static void EmptyTheGun(Player tank)
    {
        foreach (var slot in tank.AmmunitionSlots) slot.Remaining = 0;
    }

    private static float HeadingToward(Vector2 from, Vector2 to) => (float)Math.Atan2(to.Y - from.Y, to.X - from.X);

    // Two tanks on the 8x4 open row 0 (128 wide, so long range starts beyond 64).
    private static (ComputerController Controller, Player Self, Player Opponent) Duel(int selfTile, int opponentTile, float heading = 0.0f)
    {
        var map = LoadTerrainMap();
        var self = NewPlayer(Centre(selfTile, 0), heading, "Self");
        var opponent = NewPlayer(Centre(opponentTile, 0), 0.0f, "Opponent");
        return (new ComputerController(map, self, opponent), self, opponent);
    }

    [Fact]
    public void AfterBeingHitItTurnsOnTheOpponentAndDoesNotDrive()
    {
        var (controller, self, _) = Duel(0, 2, heading: MathHelper.PiOver2); // facing away; the opponent is at heading 0
        controller.Hit();

        var command = controller.PlanMove(Step, NoPickups);

        Assert.Equal(-1.0f, command.Turn);
        Assert.Equal(0.0f, command.Drive);
        Assert.False(command.Fire);
        Assert.True(controller.RetaliationTimer > 0.0f);
    }

    [Fact]
    public void RetaliationEndsAfterItsDuration()
    {
        var (controller, _, _) = Duel(0, 7);
        controller.Hit();

        controller.PlanMove(Tuning.Ai.RetaliationSeconds + 0.1f, NoPickups);

        Assert.Equal(0.0f, controller.RetaliationTimer);
    }

    [Fact]
    public void BeingHitCancelsTheFireCooldown()
    {
        var (controller, _, _) = Duel(0, 2);
        controller.ShotFired();
        Assert.True(controller.FireTimer > 0.0f);

        controller.Hit();

        Assert.Equal(0.0f, controller.FireTimer);
    }

    [Fact]
    public void ResetClearsTheTimersAndPursuit()
    {
        var (controller, _, _) = Duel(0, 7);
        controller.PlanMove(Step, NoPickups); // starts long-range pursuit
        controller.Hit();
        controller.ShotFired();

        controller.Reset();

        Assert.Equal(0.0f, controller.RetaliationTimer);
        Assert.Equal(0.0f, controller.FireTimer);
        Assert.False(controller.LongRangePursuit);
        Assert.Equal(0, controller.RouteLength);
    }

    [Fact]
    public void WithLowFuelItHeadsForAnActiveFuelPickup()
    {
        var (controller, self, _) = Duel(0, 7);
        self.Fuel = self.MaximumFuel * 0.1f;
        var pickups = new[] { FuelDrop(1, Centre(3, 0)) };

        var command = controller.PlanMove(Step, pickups);

        Assert.Equal(ComputerMode.Pickup, controller.Mode(pickups));
        Assert.Equal(0.0f, command.Turn); // already facing it
        Assert.Equal(1.0f, command.Drive);
        Assert.True(controller.RouteLength > 0);
    }

    [Fact]
    public void WithEnoughFuelAndAmmunitionItIgnoresPickups()
    {
        var (controller, _, _) = Duel(0, 7);
        var pickups = new[] { FuelDrop(1, Centre(3, 0)), AmmoDrop(2, Centre(4, 0)) };

        Assert.NotEqual(ComputerMode.Pickup, controller.Mode(pickups));
    }

    [Fact]
    public void ItOnlyWantsThePickupKindItNeeds()
    {
        var (controller, self, _) = Duel(0, 7);
        self.Fuel = self.MaximumFuel * 0.1f;

        Assert.NotEqual(ComputerMode.Pickup, controller.Mode(new[] { AmmoDrop(1, Centre(3, 0)) }));
        Assert.Equal(ComputerMode.Pickup, controller.Mode(new[] { AmmoDrop(1, Centre(3, 0)), FuelDrop(2, Centre(5, 0)) }));
    }

    [Fact]
    public void ItIgnoresPickupsAlreadyCollected()
    {
        var (controller, self, _) = Duel(0, 7);
        self.Fuel = self.MaximumFuel * 0.1f;

        Assert.NotEqual(ComputerMode.Pickup, controller.Mode(new[] { FuelDrop(1, Centre(3, 0), active: false) }));
    }

    [Fact]
    public void ItHeadsForTheNearestWantedPickup()
    {
        var (controller, self, _) = Duel(2, 7);
        self.Fuel = self.MaximumFuel * 0.1f;
        var pickups = new[] { FuelDrop(1, Centre(7, 3)), FuelDrop(2, Centre(0, 0)) };

        controller.PlanMove(Step, pickups);

        // the route ends at the nearer drop's tile (0, 0)
        Assert.Equal(3, controller.RouteLength);
    }

    [Fact]
    public void AtAWaypointItMovesOnWithoutDriving()
    {
        var (controller, self, _) = Duel(0, 7);
        self.Fuel = self.MaximumFuel * 0.1f;
        var pickups = new[] { FuelDrop(1, Centre(3, 0)) };
        controller.PlanMove(Step, pickups); // builds the route from tile 0, starting at tile 1
        Assert.Equal(1, controller.RouteIndex);
        self.Position = Centre(1, 0) - new Vector2(Tuning.Ai.WaypointReachedDistance - 1.0f, 0.0f);

        var command = controller.PlanMove(Step, pickups);

        Assert.True(command.IsIdle);
        Assert.Equal(2, controller.RouteIndex);
    }

    [Fact]
    public void WithoutAmmunitionItFleesFromTheOpponent()
    {
        var (controller, self, _) = Duel(2, 0, heading: MathHelper.Pi); // the opponent is to the left, the tank faces left
        EmptyTheGun(self);

        var command = controller.PlanMove(Step, NoPickups);

        Assert.Equal(ComputerMode.Flee, controller.Mode(NoPickups));
        Assert.Equal(1.0f, command.Drive);
        Assert.NotEqual(0.0f, command.Turn); // facing the opponent, so it turns about to face away
    }

    [Fact]
    public void FleeingDrivesStraightWhenAlreadyFacingAway()
    {
        var (controller, self, _) = Duel(2, 0, heading: 0.0f);
        EmptyTheGun(self);

        var command = controller.PlanMove(Step, NoPickups);

        Assert.Equal(0.0f, command.Turn);
        Assert.Equal(1.0f, command.Drive);
    }

    [Fact]
    public void BeyondLongRangeItDrivesStraightTowardTheOpponent()
    {
        var (controller, _, _) = Duel(0, 7);

        var command = controller.PlanMove(Step, NoPickups);

        Assert.True(controller.LongRangePursuit);
        Assert.Equal(ComputerMode.Long, controller.Mode(NoPickups));
        Assert.Equal(0.0f, command.Turn);
        Assert.Equal(1.0f, command.Drive);
    }

    [Fact]
    public void LongRangePursuitKeepsTheHeadingItStartedWith()
    {
        var (controller, self, opponent) = Duel(0, 7);
        controller.PlanMove(Step, NoPickups); // heading toward the opponent is 0
        opponent.Position = Centre(7, 3);     // the opponent moves, but the pursuit heading stays

        var command = controller.PlanMove(Step, NoPickups);

        Assert.Equal(0.0f, command.Turn);
    }

    [Fact]
    public void InRangeWithAClearViewItHoldsPosition()
    {
        var (controller, _, _) = Duel(0, 3); // 48 apart, inside the 64 long-range threshold

        var command = controller.PlanMove(Step, NoPickups);

        Assert.True(command.IsIdle);
        Assert.False(controller.LongRangePursuit);
    }

    [Fact]
    public void InRangeWithoutAViewItBuildsACombatRouteAndFollowsIt()
    {
        var map = Arena();
        var (selfPosition, opponentPosition) = FindCoveredPair(map);
        var self = NewPlayer(selfPosition, 0.0f, "Self");
        var opponent = NewPlayer(opponentPosition, 0.0f, "Opponent");
        var controller = new ComputerController(map, self, opponent);

        var command = controller.PlanMove(Step, NoPickups);

        Assert.Equal(ComputerMode.Combat, controller.Mode(NoPickups));
        Assert.True(controller.RouteLength > 1, "no combat route was built");
        Assert.Equal(1, controller.RouteIndex);
        Assert.False(command.IsIdle);
    }

    [Fact]
    public void ItDrivesOnlyWhenRoughlyFacingTheWaypointInCombat()
    {
        var map = Arena();
        var (selfPosition, opponentPosition) = FindCoveredPair(map);
        var self = NewPlayer(selfPosition, 0.0f, "Self");
        var opponent = NewPlayer(opponentPosition, 0.0f, "Opponent");
        var controller = new ComputerController(map, self, opponent);
        controller.PlanMove(Step, NoPickups);
        var waypoint = map.GetTileBounds(RoutePlanner.FindCombatRoute(map, self.CollisionRadius, map.WorldToTile(selfPosition), map.WorldToTile(opponentPosition), self.PreferredCombatDistanceTiles)[1]).Center.ToVector2();

        self.Heading = HeadingToward(selfPosition, waypoint);
        var facing = controller.PlanMove(Step, NoPickups);
        self.Heading = MathHelper.WrapAngle(HeadingToward(selfPosition, waypoint) + Tuning.Ai.DriveAngleLimitRadians + 0.2f);
        var turningAway = controller.PlanMove(Step, NoPickups);

        Assert.Equal(1.0f, facing.Drive);
        Assert.Equal(0.0f, turningAway.Drive);
        Assert.NotEqual(0.0f, turningAway.Turn);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATankThatCannotAffordTheWholeCommandDoesNothing(bool fleeing)
    {
        var (controller, self, _) = Duel(2, 0, heading: MathHelper.Pi);
        if (fleeing) EmptyTheGun(self);
        self.Fuel = 0.0f;
        controller.Hit(); // retaliation turns it, which also costs fuel

        var retaliating = controller.PlanMove(Step, NoPickups);
        controller.Reset();
        var other = controller.PlanMove(Step, NoPickups);

        Assert.True(retaliating.IsIdle);
        Assert.True(other.IsIdle);
    }

    [Fact]
    public void WhenOnlyTheTurnIsAffordableItStillDoesNothing()
    {
        var (controller, self, _) = Duel(2, 0, heading: MathHelper.Pi);
        EmptyTheGun(self);
        var turnOnly = new TankCommand(1.0f, 0.0f);
        var whole = new TankCommand(1.0f, 1.0f);
        var map = LoadTerrainMap();
        self.Fuel = (TankMovement.FuelCost(map, self, turnOnly, Step) + TankMovement.FuelCost(map, self, whole, Step)) / 2.0f;

        var command = controller.PlanMove(Step, NoPickups);

        Assert.True(command.IsIdle);
    }

    [Fact]
    public void ItFiresWhenLinedUpWithAClearViewLoadedAndNotCoolingDown()
    {
        var (controller, _, _) = Duel(0, 3);

        var command = controller.PlanAim(Step);

        Assert.True(command.Fire);
    }

    [Fact]
    public void ItTurnsInsteadOfFiringWhileOffTarget()
    {
        var (controller, self, _) = Duel(0, 3, heading: 1.0f); // well outside the aim tolerance

        var command = controller.PlanAim(Step);

        Assert.False(command.Fire);
        Assert.Equal(-1.0f, command.Turn);
        Assert.Equal(0.0f, command.Drive);
    }

    [Fact]
    public void ItFiresWhenWithinTheAimTolerance()
    {
        var (controller, self, _) = Duel(0, 3, heading: Tuning.Ai.AimToleranceRadians - 0.01f);

        var command = controller.PlanAim(Step);

        Assert.True(command.Fire);
        Assert.Equal(0.0f, command.Turn);
    }

    [Fact]
    public void ItDoesNotAimWhileTheGunIsReloading()
    {
        var (controller, self, _) = Duel(0, 3);
        self.ReloadTimer = 1.0f;

        Assert.True(controller.PlanAim(Step).IsIdle);
    }

    [Fact]
    public void ItWaitsOutTheFireCooldownThenFiresAgain()
    {
        var (controller, self, _) = Duel(0, 3);
        controller.ShotFired();
        var cooldown = self.ComputerFireCooldownSeconds + self.ComputerReactionDelaySeconds;

        Assert.True(controller.PlanAim(cooldown - 0.5f).IsIdle);
        Assert.True(controller.PlanAim(0.6f).Fire);
    }

    [Fact]
    public void ItDoesNotFireWithoutALineOfSight()
    {
        var map = Walled();
        var self = NewPlayer(Centre(0, 0), HeadingToward(Centre(0, 0), Centre(2, 2)), "Self");
        var opponent = NewPlayer(Centre(2, 2), 0.0f, "Opponent"); // in the sealed pocket
        var controller = new ComputerController(map, self, opponent);

        Assert.True(controller.PlanAim(Step).IsIdle);
    }

    [Fact]
    public void ItDoesNotTurnToAimWithoutFuel()
    {
        var (controller, self, _) = Duel(0, 3, heading: 1.0f);
        self.Fuel = 0.0f;

        Assert.True(controller.PlanAim(Step).IsIdle);
    }

    // The same situation with the seats swapped must give the same command: the
    // controller knows only "self" and "the opponent".
    [Theory]
    [InlineData("retaliate")]
    [InlineData("flee")]
    [InlineData("long")]
    [InlineData("pickup")]
    [InlineData("hold")]
    public void TheSameSituationGivesTheSameMoveForEitherSeat(string situation)
    {
        var map = LoadTerrainMap();
        var pickups = new[] { FuelDrop(1, Centre(3, 0)) };

        TankCommand Plan(bool firstSeat)
        {
            var one = NewPlayer(Centre(0, 0), MathHelper.PiOver2, "One");
            var two = NewPlayer(Centre(situation == "long" ? 7 : 3, 0), 0.0f, "Two");
            var (self, opponent) = firstSeat ? (one, two) : (two, one);
            // the second run puts the tanks in each other's places
            if (!firstSeat)
            {
                (self.Position, opponent.Position) = (opponent.Position, self.Position);
                (self.Heading, opponent.Heading) = (opponent.Heading, self.Heading);
            }
            var controller = new ComputerController(map, self, opponent);
            switch (situation)
            {
                case "retaliate": controller.Hit(); break;
                case "flee": EmptyTheGun(self); break;
                case "pickup": self.Fuel = self.MaximumFuel * 0.1f; break;
            }
            return controller.PlanMove(Step, situation == "pickup" ? pickups : NoPickups);
        }

        var first = Plan(true);
        var second = Plan(false);

        Assert.Equal(first.Turn, second.Turn);
        Assert.Equal(first.Drive, second.Drive);
        Assert.Equal(first.Fire, second.Fire);
    }

    [Fact]
    public void TheAimCommandIsTheSameForEitherSeat()
    {
        var map = LoadTerrainMap();

        TankCommand Aim(bool firstSeat)
        {
            var left = NewPlayer(Centre(0, 0), 1.0f, "Left");
            var right = NewPlayer(Centre(3, 0), 0.0f, "Right");
            var controller = firstSeat ? new ComputerController(map, left, right) : new ComputerController(map, right, left);
            // both controllers play the tank on the left, so only the labels differ
            if (!firstSeat)
            {
                (right.Position, left.Position) = (left.Position, right.Position);
                (right.Heading, left.Heading) = (left.Heading, right.Heading);
            }
            return controller.PlanAim(Step);
        }

        var first = Aim(true);
        var second = Aim(false);

        Assert.Equal(first.Turn, second.Turn);
        Assert.Equal(first.Fire, second.Fire);
    }

    // A position pair on the arena that are within long range of each other but
    // cannot see each other, so the computer must route to a combat position.
    private static (Vector2 Self, Vector2 Opponent) FindCoveredPair(WorldMap map)
    {
        var radius = Tuning.Tank.CollisionRadius;
        var threshold = map.Bounds.Width * Tuning.Ai.LongRangePursuitDistanceFraction;
        for (var sy = 0; sy < map.Bounds.Height / map.TileHeight; sy++)
            for (var sx = 0; sx < map.Bounds.Width / map.TileWidth; sx++)
                for (var oy = 0; oy < map.Bounds.Height / map.TileHeight; oy++)
                    for (var ox = 0; ox < map.Bounds.Width / map.TileWidth; ox++)
                    {
                        var self = map.GetTileBounds(new Point(sx, sy)).Center.ToVector2();
                        var opponent = map.GetTileBounds(new Point(ox, oy)).Center.ToVector2();
                        if (!map.CanOccupyCircle(self, radius) || !map.CanOccupyCircle(opponent, radius)) continue;
                        if (Vector2.Distance(self, opponent) > threshold * 0.9f || Vector2.Distance(self, opponent) < 4 * map.TileWidth) continue;
                        if (map.HasLineOfSight(self, opponent)) continue;
                        if (RoutePlanner.FindCombatRoute(map, radius, new Point(sx, sy), new Point(ox, oy), Tuning.Ai.PreferredCombatDistanceTiles) == null) continue;
                        return (self, opponent);
                    }
        throw new InvalidOperationException("the arena has no covered pair to test with");
    }
}

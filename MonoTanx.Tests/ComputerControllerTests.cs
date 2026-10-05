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

    // The 24x5 divided map's open bottom row: seat one at the left end and the opponent at the right
    // end, 336px apart, beyond long range (192px) with nothing between them.
    private static (ComputerController Controller, Player Self, Player Opponent) FarDuel()
    {
        var map = new WorldMap(FixturePath("divided.tmx"));
        var self = NewPlayer(Centre(2, 4), 0.0f, "Self");
        var opponent = NewPlayer(Centre(23, 4), 0.0f, "Opponent");
        return (new ComputerController(map, self, opponent), self, opponent);
    }

    // The same map with seat one at the left edge and the opponent the given number of tiles to its
    // right in the open row 2, in clear view (up to 10 tiles: the wall in column 11 is beyond them).
    private static (ComputerController Controller, Player Self, Player Opponent, WorldMap Map) Engagement(float tilesApart)
    {
        var map = new WorldMap(FixturePath("divided.tmx"));
        var self = NewPlayer(Centre(0, 2), 0.0f, "Self");
        var opponent = NewPlayer(Centre(0, 2) + new Vector2(tilesApart * 16.0f, 0.0f), 0.0f, "Opponent");
        Assert.True(map.HasLineOfSight(self.Position, opponent.Position), "the fixture should give a clear view");
        return (new ComputerController(map, self, opponent), self, opponent, map);
    }

    [Fact]
    public void InViewButBeyondTheHoldDistanceItClosesInInsteadOfHolding()
    {
        var (controller, self, opponent, map) = Engagement(10.0f); // 160px: past the ring, inside long range

        var command = controller.PlanMove(Step, NoPickups);

        Assert.False(command.IsIdle);
        Assert.Equal(ComputerMode.Combat, controller.Mode(NoPickups));
        var before = Vector2.Distance(self.Position, opponent.Position);
        Drive(controller, map, self, opponent, 2.0f);
        Assert.True(Vector2.Distance(self.Position, opponent.Position) < before - 20.0f, "it did not close in");
    }

    [Fact]
    public void WithinTheRingItStopsAndHolds()
    {
        var (controller, _, _, _) = Engagement(6.0f); // 96px, inside the 120px hold distance

        Assert.True(controller.PlanMove(Step, NoPickups).IsIdle);
    }

    [Fact]
    public void ItClosesInUntilItIsWithinTheHoldDistanceThenStaysPut()
    {
        var (controller, self, opponent, map) = Engagement(9.0f);

        Drive(controller, map, self, opponent, 8.0f);
        var settled = self.Position;
        Drive(controller, map, self, opponent, 2.0f);

        var holdDistance = (Tuning.Ai.EngageDistanceTiles + Tuning.Ai.CombatRingToleranceTiles + 0.5f) * 16.0f;
        Assert.True(Vector2.Distance(self.Position, opponent.Position) <= holdDistance);
        Assert.Equal(settled, self.Position);
    }

    [Fact]
    public void ItDoesNotFireFromBeyondTheFireDistanceEvenWhenLinedUp()
    {
        var (controller, _, _, _) = Engagement(Tuning.Ai.FireDistanceTiles + 1.0f);

        Assert.True(controller.PlanAim(Step).IsIdle);
    }

    [Fact]
    public void ItFiresFromInsideTheFireDistanceWhenLinedUp()
    {
        var (controller, _, _, _) = Engagement(Tuning.Ai.FireDistanceTiles - 1.0f);

        Assert.True(controller.PlanAim(Step).Fire);
    }

    [Fact]
    public void WhileStillClosingInItDoesNotTurnToAimItLeavesTheSteeringToTheMovementPhase()
    {
        var (controller, self, _, _) = Engagement(Tuning.Ai.FireDistanceTiles - 0.3f); // past the hold distance but inside the fire distance
        self.Heading = 1.0f; // well off the opponent

        var command = controller.PlanAim(Step);

        Assert.False(command.Fire);
        Assert.Equal(0.0f, command.Turn);
    }

    [Fact]
    public void OnceItHasStoppedItTurnsToAim()
    {
        var (controller, self, _, _) = Engagement(5.0f);
        self.Heading = 1.0f;

        var command = controller.PlanAim(Step);

        Assert.False(command.Fire);
        Assert.NotEqual(0.0f, command.Turn);
    }

    [Fact]
    public void WithoutAViewItNeitherTurnsToAimNorFires()
    {
        var map = Walled();
        var self = NewPlayer(Centre(0, 0), 2.0f, "Self"); // facing well away from the sealed pocket at (2, 2)
        var opponent = NewPlayer(Centre(2, 2), 0.0f, "Opponent");
        var controller = new ComputerController(map, self, opponent);

        Assert.True(controller.PlanAim(Step).IsIdle);
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
        var (controller, _, _) = FarDuel();

        var command = controller.PlanMove(Step, NoPickups);

        Assert.True(controller.LongRangePursuit);
        Assert.Equal(ComputerMode.Long, controller.Mode(NoPickups));
        Assert.Equal(0.0f, command.Turn);
        Assert.Equal(1.0f, command.Drive);
    }

    [Fact]
    public void LongRangePursuitKeepsTheHeadingItStartedWith()
    {
        var (controller, self, opponent) = FarDuel();
        controller.PlanMove(Step, NoPickups); // heading toward the opponent is 0
        opponent.Position = Centre(23, 0);    // the opponent moves, but the pursuit heading stays

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

    // Aim error

    // A computer ready to shoot an opponent in clear view 48px away, with its own random stream.
    private static (ComputerController Controller, Player Self) Shooter(Random random, float skill = 0.5f)
    {
        var map = LoadTerrainMap();
        var self = NewPlayer(Centre(0, 0), 0.0f, "Self");
        var opponent = NewPlayer(Centre(3, 0), 0.0f, "Opponent");
        self.ComputerSkill = skill;
        return (new ComputerController(map, self, opponent, random), self);
    }

    // The windows drawn for a run of consecutive shots.
    private static List<float> DrawnWindows(Random random, int shots, float skill = 0.5f)
    {
        var (controller, _) = Shooter(random, skill);
        var windows = new List<float>();
        for (var shot = 0; shot < shots; shot++)
        {
            controller.PlanAim(Step); // starts the shot: draws its window
            windows.Add(controller.AimError);
            controller.ShotFired();
            controller.PlanAim(controller.FireTimer + 0.1f); // let the cooldown pass
        }
        return windows;
    }

    [Fact]
    public void WithoutAStreamTheWindowIsTheNarrowestOne()
    {
        var (controller, self) = Shooter(random: null);

        controller.PlanAim(Step);

        Assert.Equal(self.ComputerAimToleranceRadians, controller.AimError);
    }

    [Fact]
    public void EveryShotsWindowIsWithinTheBounds()
    {
        var skill = 0.25f;
        var narrowest = Tuning.Ai.AimToleranceRadians;
        var widest = narrowest + Tuning.Ai.MaximumAimErrorRadians * (1.0f - skill);

        var windows = DrawnWindows(new Random(11), 300, skill);

        Assert.All(windows, window => Assert.InRange(window, narrowest, widest));
        Assert.True(windows.Max() > narrowest + 0.8f * (widest - narrowest), "the windows never get near the top of the range");
        Assert.True(windows.Min() < narrowest + 0.2f * (widest - narrowest), "the windows never get near the bottom of the range");
    }

    [Fact]
    public void TheSameSeedGivesTheSameWindows()
    {
        Assert.Equal(DrawnWindows(new Random(5), 20), DrawnWindows(new Random(5), 20));
        Assert.NotEqual(DrawnWindows(new Random(5), 20), DrawnWindows(new Random(6), 20));
    }

    [Fact]
    public void EachSeatsOwnStreamGivesItDifferentWindows()
    {
        var streams = new RandomStreams(42);

        var seatOne = DrawnWindows(streams.CreateStream("ai-1"), 20);
        var seatTwo = DrawnWindows(streams.CreateStream("ai-2"), 20);

        Assert.NotEqual(seatOne, seatTwo);
        Assert.Equal(seatOne, DrawnWindows(new RandomStreams(42).CreateStream("ai-1"), 20)); // and each is reproducible
    }

    [Fact]
    public void AtFullSkillThereIsNoRandomError()
    {
        var windows = DrawnWindows(new Random(3), 50, skill: 1.0f);

        Assert.All(windows, window => Assert.Equal(Tuning.Ai.AimToleranceRadians, window, 5));
    }

    [Fact]
    public void AtZeroSkillTheWindowsReachTheWidestRange()
    {
        var windows = DrawnWindows(new Random(3), 300, skill: 0.0f);

        Assert.InRange(windows.Max(), Tuning.Ai.AimToleranceRadians + 0.8f * Tuning.Ai.MaximumAimErrorRadians, Tuning.Ai.AimToleranceRadians + Tuning.Ai.MaximumAimErrorRadians);
    }

    [Fact]
    public void AShotKeepsItsWindowWhileItIsBeingAimedAndAnotherIsDrawnAfterwards()
    {
        var (controller, self) = Shooter(new Random(9));
        self.Heading = 1.0f; // well off target, so it takes several updates to turn on
        controller.PlanAim(Step);
        var window = controller.AimError;

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(window, controller.AimError);
            controller.PlanAim(Step);
        }
        Assert.Equal(window, controller.AimError);

        controller.ShotFired();
        controller.PlanAim(controller.FireTimer + 0.1f);

        Assert.NotEqual(window, controller.AimError);
    }

    [Fact]
    public void ItFiresAsSoonAsItPointsWithinTheShotsWindowAndNotBefore()
    {
        var (controller, self) = Shooter(new Random(9));
        controller.PlanAim(Step); // draws the window
        var window = controller.AimError;

        self.Heading = window + 0.01f; // just outside it
        var outside = controller.PlanAim(Step);
        self.Heading = window - 0.01f; // just inside it
        var inside = controller.PlanAim(Step);

        Assert.False(outside.Fire);
        Assert.NotEqual(0.0f, outside.Turn);
        Assert.True(inside.Fire);
    }

    // Stuck detection and recovery

    // On the 24x5 divided map (a wall down column 11 with a gap in the bottom row), seat one on the
    // left and the opponent at the far right in the same row, 336px away (and still more than 192px away at the wall): beyond long range, so
    // the computer drives straight at the opponent, straight into the wall.
    private static (ComputerController Controller, Player Self, Player Opponent, WorldMap Map) IntoTheWall(Random random = null)
    {
        var map = new WorldMap(FixturePath("divided.tmx"));
        var self = NewPlayer(Centre(2, 2), 0.0f, "Self");
        var opponent = NewPlayer(Centre(23, 2), 0.0f, "Opponent");
        return (new ComputerController(map, self, opponent, random), self, opponent, map);
    }

    // Runs until the computer has started backing away (at most 10 seconds, so a bug fails rather than hangs).
    private static void DriveUntilRecovering(ComputerController controller, WorldMap map, Player self, Player opponent)
    {
        for (var frame = 0; frame < 600 && !controller.Recovering; frame++)
            Drive(controller, map, self, opponent, Step);
        Assert.True(controller.Recovering, "it never got stuck");
    }

    // Plays the movement phase only for the given seconds, applying each command as the stage would.
    private static void Drive(ComputerController controller, WorldMap map, Player self, Player opponent, float seconds, Action<float> each = null)
    {
        for (var elapsed = 0.0f; elapsed < seconds; elapsed += Step)
        {
            TankMovement.ApplyInput(map, self, opponent, controller.PlanMove(Step, NoPickups), Step);
            each?.Invoke(elapsed);
        }
    }

    [Fact]
    public void DrivingIntoAWallWithoutMovingIsStuckAfterTheWindowAndNotBefore()
    {
        var (controller, self, opponent, map) = IntoTheWall();
        // the first window is spent driving up to the wall, so it has moved and is not stuck
        Drive(controller, map, self, opponent, Tuning.Ai.StuckWindowSeconds + 0.1f);
        Assert.Equal(0, controller.StuckCount);
        Assert.True(Vector2.Distance(self.Position, Centre(2, 2)) > 50.0f);

        // now it sits against the wall still being told to drive: stuck once a whole window has passed
        Drive(controller, map, self, opponent, Tuning.Ai.StuckWindowSeconds - 0.3f);
        Assert.Equal(0, controller.StuckCount);
        Drive(controller, map, self, opponent, 0.5f);

        Assert.Equal(1, controller.StuckCount);
    }

    [Fact]
    public void RecoveryBacksAwayTurningForTheRecoveryTimeThenSteersAgain()
    {
        var (controller, self, opponent, map) = IntoTheWall();
        DriveUntilRecovering(controller, map, self, opponent);
        var before = self.Position;
        var recoveryFrames = 0;

        while (controller.Recovering && recoveryFrames < 600)
        {
            Assert.Equal(ComputerMode.Recover, controller.Mode(NoPickups));
            Drive(controller, map, self, opponent, Step);
            recoveryFrames++;
            Assert.Equal(-1.0f, controller.LastMove.Drive);
            Assert.NotEqual(0.0f, controller.LastMove.Turn);
        }

        Assert.InRange(recoveryFrames * Step, Tuning.Ai.StuckRecoverySeconds - 0.05f, Tuning.Ai.StuckRecoverySeconds + 0.05f);
        Assert.True(Vector2.Distance(before, self.Position) > 10.0f, "it did not back away");
        Assert.NotEqual(ComputerMode.Recover, controller.Mode(NoPickups));
    }

    [Fact]
    public void AfterGettingStuckHeadingStraightAtTheOpponentItFollowsARouteAroundTheWall()
    {
        var (controller, self, opponent, map) = IntoTheWall();

        Drive(controller, map, self, opponent, 20.0f);

        // it found its way through the gap in the bottom row and past the wall (x 176 to 192, it then
        // closes in on the opponent) instead of staying wedged against the wall's face
        Assert.True(self.Position.X > 192.0f, $"never got through the gap: at {self.Position}");
        Assert.InRange(controller.StuckCount, 1, 3);
    }

    [Fact]
    public void WaitingWithAClearViewIsNotBeingStuck()
    {
        var (controller, self, opponent) = Duel(0, 3); // in range with a clear view: it holds position
        var map = LoadTerrainMap();

        Drive(controller, map, self, opponent, 8.0f);

        Assert.Equal(0, controller.StuckCount);
        Assert.False(controller.Recovering);
    }

    [Fact]
    public void WithoutFuelItCannotDriveSoItIsNotStuck()
    {
        var (controller, self, opponent, map) = IntoTheWall();
        self.Fuel = 0.0f;

        Drive(controller, map, self, opponent, 8.0f);

        Assert.Equal(0, controller.StuckCount);
        Assert.True(controller.LastMove.IsIdle);
    }

    [Fact]
    public void TurningOnTheSpotAfterAHitIsNotBeingStuck()
    {
        var (controller, self, opponent, map) = IntoTheWall();
        self.Heading = MathHelper.Pi; // facing away: retaliation turns it round without driving
        controller.Hit();

        Drive(controller, map, self, opponent, Tuning.Ai.RetaliationSeconds - 0.1f);

        Assert.Equal(0, controller.StuckCount);
    }

    [Fact]
    public void WhichSideItBacksAwayToComesFromItsOwnStream()
    {
        var sides = new HashSet<float>();
        for (var seed = 0; seed < 12; seed++)
        {
            var (controller, self, opponent, map) = IntoTheWall(new Random(seed));
            DriveUntilRecovering(controller, map, self, opponent);
            sides.Add(controller.LastMove.Turn);
        }

        Assert.Equal(new HashSet<float> { -1.0f, 1.0f }, sides);
    }

    [Fact]
    public void TheSameSeedBacksAwayTheSameWay()
    {
        float FirstSide(int seed)
        {
            var (controller, self, opponent, map) = IntoTheWall(new Random(seed));
            DriveUntilRecovering(controller, map, self, opponent);
            return controller.LastMove.Turn;
        }

        Assert.Equal(FirstSide(5), FirstSide(5));
    }

    [Fact]
    public void WhileBackingAwayTheAimPhaseDoesNotTurnTheTankBack()
    {
        var (controller, self, opponent, map) = IntoTheWall();
        DriveUntilRecovering(controller, map, self, opponent);
        self.Heading = 1.0f; // well off the opponent

        var command = controller.PlanAim(Step);

        Assert.False(command.Fire);
        Assert.Equal(0.0f, command.Turn);
    }

    [Fact]
    public void PickupSeekingNowOnlyDrivesWhenRoughlyFacingTheWaypoint()
    {
        var (controller, self, _) = Duel(0, 7);
        self.Fuel = self.MaximumFuel * 0.1f;
        self.Heading = MathHelper.Pi; // the pickup is straight ahead along the row, but it faces away
        var pickups = new[] { FuelDrop(1, Centre(3, 0)) };

        var command = controller.PlanMove(Step, pickups);

        Assert.Equal(0.0f, command.Drive);
        Assert.NotEqual(0.0f, command.Turn);
    }

    [Fact]
    public void TheStuckBehaviourIsTheSameForEitherSeat()
    {
        var map = new WorldMap(FixturePath("divided.tmx"));

        List<(float Turn, float Drive)> Moves(bool firstSeat)
        {
            var one = NewPlayer(Centre(2, 2), 0.0f, "One");
            var two = NewPlayer(Centre(23, 2), 0.0f, "Two");
            var (self, opponent) = firstSeat ? (one, two) : (two, one);
            if (!firstSeat)
            {
                (self.Position, opponent.Position) = (opponent.Position, self.Position);
                (self.Heading, opponent.Heading) = (opponent.Heading, self.Heading);
            }
            var controller = new ComputerController(map, self, opponent, new Random(4));
            var moves = new List<(float, float)>();
            Drive(controller, map, self, opponent, 6.0f, _ => moves.Add((controller.LastMove.Turn, controller.LastMove.Drive)));
            return moves;
        }

        Assert.Equal(Moves(true), Moves(false));
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
                        if (RoutePlanner.FindCombatRoute(map, radius, new Point(sx, sy), new Point(ox, oy), Tuning.Ai.EngageDistanceTiles) == null) continue;
                        return (self, opponent);
                    }
        throw new InvalidOperationException("the arena has no covered pair to test with");
    }

    [Fact]
    public void TheShotWindowIsTheSameForEitherSeat()
    {
        var map = LoadTerrainMap();

        (float Window, TankCommand Command) Aim(bool firstSeat)
        {
            var left = NewPlayer(Centre(0, 0), 0.5f, "Left");
            var right = NewPlayer(Centre(3, 0), 0.0f, "Right");
            var controller = firstSeat ? new ComputerController(map, left, right, new Random(5)) : new ComputerController(map, right, left, new Random(5));
            if (!firstSeat)
            {
                (right.Position, left.Position) = (left.Position, right.Position);
                (right.Heading, left.Heading) = (left.Heading, right.Heading);
            }
            var command = controller.PlanAim(Step);
            return (controller.AimError, command);
        }

        var first = Aim(true);
        var second = Aim(false);

        Assert.Equal(first.Window, second.Window);
        Assert.Equal(first.Command.Turn, second.Command.Turn);
        Assert.Equal(first.Command.Fire, second.Command.Fire);
    }
}

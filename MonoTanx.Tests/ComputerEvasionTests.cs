using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class ComputerEvasionTests
{
    private const float Step = 1.0f / 60.0f;
    private static readonly IReadOnlyList<PickupState> NoPickups = new List<PickupState>();

    private static WorldMap Field() => new WorldMap(FixturePath("field.tmx"));

    // An open field with seat one in the middle row and the opponent out of the way in a corner.
    private static (ComputerController Controller, Player Self, Player Opponent, WorldMap Map) Scene(Random random = null)
    {
        var map = Field();
        var self = NewPlayer(Centre(6, 4), 0.0f, "Self");
        var opponent = NewPlayer(Centre(1, 8), 0.0f, "Opponent");
        return (new ComputerController(map, self, opponent, random), self, opponent, map);
    }

    // A shell flying left along the tank's row from the given distance, already past the reaction time.
    private static Shell Incoming(Player target, float distance, float age = 0.2f, float offsetY = 0.0f) =>
        new Shell(Player.DefaultAmmunition, target.Position + new Vector2(distance, offsetY), new Vector2(-Player.DefaultAmmunition.Speed, 0.0f)) { Age = age };

    // Plays the movement phase and the shell for the given seconds. Returns whether the tank was hit.
    private static bool Fly(ComputerController controller, WorldMap map, Player self, Player opponent, Shell shell, float seconds, Action<float> each = null)
    {
        var shells = new List<Shell> { shell };
        var tanks = new[] { self, opponent };
        for (var elapsed = 0.0f; elapsed < seconds; elapsed += Step)
        {
            TankMovement.ApplyInput(map, self, opponent, controller.PlanMove(Step, NoPickups, shells), Step);
            each?.Invoke(elapsed);
            var result = shell.Step(map, tanks, Step);
            if (ReferenceEquals(result.Hit, self))
                return true;
            if (result.Removed)
                return false;
        }
        return false;
    }

    [Fact]
    public void ADodgeableShellHeadedForItMissesBecauseItMoves()
    {
        var (controller, self, opponent, map) = Scene();
        var start = self.Position;

        var hit = Fly(controller, map, self, opponent, Incoming(self, 150.0f), 2.0f);

        Assert.False(hit);
        Assert.True(Math.Abs(self.Position.Y - start.Y) > 8.0f || Math.Abs(self.Position.X - start.X) > 20.0f, "it did not move out of the way");
    }

    [Fact]
    public void WithoutLookingAtTheShellTheSameShotWouldHaveHit()
    {
        var (controller, self, opponent, map) = Scene();
        var shell = Incoming(self, 150.0f);
        var tanks = new[] { self, opponent };

        var hit = false;
        for (var elapsed = 0.0f; elapsed < 2.0f && !hit; elapsed += Step)
        {
            var result = shell.Step(map, tanks, Step);
            hit = ReferenceEquals(result.Hit, self);
            if (result.Removed) break;
        }

        Assert.True(hit, "the scenario is not a hit, so the dodge above proves nothing");
    }

    [Fact]
    public void AShellTooCloseToDodgeIsAcceptedAndCostsNoFuel()
    {
        var (controller, self, opponent, map) = Scene();
        var fuel = self.Fuel;

        var command = controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 28.0f) });

        Assert.False(controller.Evading);
        Assert.True(command.IsIdle);
        Assert.Equal(fuel, self.Fuel);
    }

    [Fact]
    public void ItDodgesAShellThatWillRebounOffTheWallAndComeBackAtIt()
    {
        var (controller, self, opponent, map) = Scene();
        // fired to the right from 128px away: it flies off toward the reflective column at x = 304, bounces,
        // and comes back along the row. Right now it is moving away.
        var shell = new Shell(Player.DefaultAmmunition, self.Position + new Vector2(128.0f, 0.0f), new Vector2(Player.DefaultAmmunition.Speed, 0.0f)) { Age = 0.2f };

        var command = controller.PlanMove(Step, NoPickups, new[] { shell });

        Assert.True(controller.Evading, "it did not see the rebound coming");
        Assert.False(command.IsIdle);
        var hit = Fly(controller, map, self, opponent, shell, 3.0f);
        Assert.False(hit);
    }

    [Fact]
    public void ItDodgesItsOwnShellComingBack()
    {
        var (controller, self, opponent, map) = Scene();
        var own = new Shell(Player.DefaultAmmunition, self.Position + new Vector2(12.0f, 0.0f), new Vector2(Player.DefaultAmmunition.Speed, 0.0f)) { Age = 0.2f };
        own.Position = self.Position + new Vector2(60.0f, 0.0f);

        var hit = Fly(controller, map, self, opponent, own, 3.0f);

        Assert.False(hit);
    }

    [Fact]
    public void WithNoFuelItDoesNotTry()
    {
        var (controller, self, _, _) = Scene();
        self.Fuel = 0.0f;

        var command = controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 150.0f) });

        Assert.False(controller.Evading);
        Assert.True(command.IsIdle);
    }

    [Fact]
    public void WithNoThreatItsBehaviourIsUnchanged()
    {
        var withShell = Scene();
        var without = Scene();
        var passing = Incoming(withShell.Self, 150.0f, offsetY: 80.0f); // flies by well clear of it

        var a = withShell.Controller.PlanMove(Step, NoPickups, new[] { passing });
        var b = without.Controller.PlanMove(Step, NoPickups);

        Assert.False(withShell.Controller.Evading);
        Assert.Equal((b.Turn, b.Drive, b.Fire), (a.Turn, a.Drive, a.Fire));
    }

    [Fact]
    public void ItDoesNotNoticeAShellUntilItHasBeenFlyingForTheReactionTime()
    {
        var (controller, self, _, _) = Scene();

        controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 150.0f, age: Tuning.Ai.EvadeReactionSeconds - 0.05f) });
        Assert.False(controller.Evading);

        controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 150.0f, age: Tuning.Ai.EvadeReactionSeconds + 0.05f) });
        Assert.True(controller.Evading);
    }

    [Fact]
    public void ItIgnoresAShellBeyondTheDetectionDistance()
    {
        var (controller, self, _, _) = Scene();

        controller.PlanMove(Step, NoPickups, new[] { Incoming(self, Tuning.Ai.EvadeDetectionDistance + 40.0f) });

        Assert.False(controller.Evading);
    }

    [Fact]
    public void DodgingComesBeforeRetaliation()
    {
        var (controller, self, _, _) = Scene();
        controller.Hit(); // retaliation would turn on the spot and not drive

        var command = controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 150.0f) });

        Assert.True(controller.Evading);
        Assert.NotEqual(0.0f, command.Drive);
    }

    [Fact]
    public void DodgingComesBeforeSeekingAPickup()
    {
        var (controller, self, _, _) = Scene();
        self.Fuel = self.MaximumFuel * 0.1f;
        var pickups = new[] { new PickupState(new PickupSpawn(1, PickupKind.Fuel, Centre(10, 4), 50, null, "Sprites/fueldrop_1")) };

        controller.PlanMove(Step, pickups, new[] { Incoming(self, 150.0f) });

        Assert.True(controller.Evading);
        Assert.Equal(ComputerMode.Evade, controller.Mode(pickups));
    }

    [Fact]
    public void WhileDodgingTheAimPhaseDoesNotTurnTheTankBack()
    {
        var map = Field();
        var self = NewPlayer(Centre(6, 4), 1.0f, "Self"); // facing well off the opponent
        var opponent = NewPlayer(Centre(6, 1), 0.0f, "Opponent"); // three tiles up, in clear view and out of the shell's way
        var controller = new ComputerController(map, self, opponent);
        controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 150.0f, offsetY: 0.0f) });
        Assert.True(controller.Evading);

        var command = controller.PlanAim(Step);

        Assert.Equal(0.0f, command.Turn);
        Assert.False(command.Fire);
    }

    [Fact]
    public void WhichShellsItNoticesComesFromItsOwnStream()
    {
        List<bool> Noticed(int seed, float skill = 0.5f)
        {
            var (controller, self, _, _) = Scene(new Random(seed));
            self.ComputerSkill = skill;
            var results = new List<bool>();
            for (var i = 0; i < 40; i++)
            {
                controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 150.0f) });
                results.Add(controller.Evading);
            }
            return results;
        }

        // each call above uses a new Shell object, so each is drawn afresh
        var seeded = Noticed(3);
        Assert.Contains(true, seeded);
        Assert.Contains(false, seeded);
        Assert.Equal(seeded, Noticed(3));
        Assert.NotEqual(seeded, Noticed(4));
        Assert.All(Noticed(3, skill: 1.0f), noticed => Assert.True(noticed));
    }

    [Fact]
    public void TheDodgeIsTheSameForEitherSeat()
    {
        var map = Field();

        TankCommand Dodge(bool firstSeat)
        {
            var one = NewPlayer(Centre(6, 4), 0.0f, "One");
            var two = NewPlayer(Centre(1, 8), 0.0f, "Two");
            var (self, opponent) = firstSeat ? (one, two) : (two, one);
            if (!firstSeat)
            {
                (self.Position, opponent.Position) = (opponent.Position, self.Position);
                (self.Heading, opponent.Heading) = (opponent.Heading, self.Heading);
            }
            var controller = new ComputerController(map, self, opponent); // no stream: it always sees the shell
            return controller.PlanMove(Step, NoPickups, new[] { Incoming(self, 150.0f) });
        }

        var first = Dodge(true);
        var second = Dodge(false);

        Assert.Equal((first.Turn, first.Drive), (second.Turn, second.Drive));
        Assert.False(first.IsIdle);
    }
}

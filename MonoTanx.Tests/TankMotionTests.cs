using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class TankMotionTests
{
    private static readonly Vector2 Origin = new Vector2(100, 100);

    [Fact]
    public void NoChangeIsIdle()
    {
        Assert.Equal(TankMotion.Idle, TankMotionClassifier.Classify(Origin, 0.5f, Origin, 0.5f));
    }

    [Fact]
    public void MovingAlongTheHeadingIsForward()
    {
        // heading 0 faces +X
        Assert.Equal(TankMotion.Forward, TankMotionClassifier.Classify(Origin, 0.0f, Origin + new Vector2(1.5f, 0), 0.0f));
    }

    [Fact]
    public void MovingAgainstTheHeadingIsReverse()
    {
        Assert.Equal(TankMotion.Reverse, TankMotionClassifier.Classify(Origin, 0.0f, Origin + new Vector2(-1.5f, 0), 0.0f));
    }

    [Theory]
    [InlineData(MathHelper.PiOver2)]      // facing down the screen
    [InlineData(-MathHelper.PiOver2)]     // facing up
    [InlineData(MathHelper.Pi)]           // facing left
    public void ForwardAndReverseFollowTheHeadingInAnyDirection(float heading)
    {
        var facing = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading));

        Assert.Equal(TankMotion.Forward, TankMotionClassifier.Classify(Origin, heading, Origin + facing * 1.5f, heading));
        Assert.Equal(TankMotion.Reverse, TankMotionClassifier.Classify(Origin, heading, Origin - facing * 1.5f, heading));
    }

    [Fact]
    public void TurningWithoutMovingIsTurn()
    {
        Assert.Equal(TankMotion.Turn, TankMotionClassifier.Classify(Origin, 0.0f, Origin, 0.04f));
        Assert.Equal(TankMotion.Turn, TankMotionClassifier.Classify(Origin, 0.04f, Origin, 0.0f));
    }

    [Fact]
    public void TurningWhileDrivingCountsAsDriving()
    {
        Assert.Equal(TankMotion.Forward, TankMotionClassifier.Classify(Origin, 0.0f, Origin + new Vector2(1.5f, 0), 0.04f));
        Assert.Equal(TankMotion.Reverse, TankMotionClassifier.Classify(Origin, 0.0f, Origin + new Vector2(-1.5f, 0), 0.04f));
    }

    [Fact]
    public void TurningAcrossTheWrapAroundPointIsASmallTurn()
    {
        // from just under +pi to just over -pi is a tiny turn, not nearly a full circle
        var before = MathHelper.Pi - 0.01f;
        var after = -MathHelper.Pi + 0.01f;

        Assert.Equal(TankMotion.Turn, TankMotionClassifier.Classify(Origin, before, Origin, after));
    }

    [Fact]
    public void ChangesBelowTheThresholdsAreIdle()
    {
        var tinyMove = new Vector2(Tuning.Audio.MotionThresholdPixels * 0.5f, 0);
        var tinyTurn = Tuning.Audio.MotionThresholdRadians * 0.5f;

        Assert.Equal(TankMotion.Idle, TankMotionClassifier.Classify(Origin, 0.0f, Origin + tinyMove, tinyTurn));
    }

    [Fact]
    public void ADrivingTankHeldAgainstAWallIsIdle()
    {
        // it did not actually move or turn, whatever keys are held
        Assert.Equal(TankMotion.Idle, TankMotionClassifier.Classify(Origin, 1.0f, Origin, 1.0f));
    }

    [Fact]
    public void ClassifiesAMovementActuallyMadeByTheMovementRules()
    {
        var map = TestSupport.LoadTerrainMap();
        var tank = TestSupport.NewPlayer(new Vector2(40, 40), heading: 0.0f);
        var other = TestSupport.NewBystander();

        var before = (tank.Position, tank.Heading);
        TankMovement.ApplyInput(map, tank, other, 0.0f, 1.0f, 1.0f / 60.0f);
        Assert.Equal(TankMotion.Forward, TankMotionClassifier.Classify(before.Position, before.Heading, tank.Position, tank.Heading));

        before = (tank.Position, tank.Heading);
        TankMovement.ApplyInput(map, tank, other, 0.0f, -1.0f, 1.0f / 60.0f);
        Assert.Equal(TankMotion.Reverse, TankMotionClassifier.Classify(before.Position, before.Heading, tank.Position, tank.Heading));

        before = (tank.Position, tank.Heading);
        TankMovement.ApplyInput(map, tank, other, 1.0f, 0.0f, 1.0f / 60.0f);
        Assert.Equal(TankMotion.Turn, TankMotionClassifier.Classify(before.Position, before.Heading, tank.Position, tank.Heading));

        before = (tank.Position, tank.Heading);
        TankMovement.ApplyInput(map, tank, other, 1.0f, 1.0f, 1.0f / 60.0f);
        Assert.Equal(TankMotion.Forward, TankMotionClassifier.Classify(before.Position, before.Heading, tank.Position, tank.Heading));
    }
}

using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class MouseMovementTests
{
    [Fact]
    public void TheFirstPositionIsNotMovement()
    {
        Assert.False(new MouseMovement().Moved(100, 200));
    }

    [Fact]
    public void ARestingPointerNeverCountsAsMovingSoTheKeyboardKeepsTheHighlight()
    {
        var pointer = new MouseMovement();
        pointer.Moved(100, 200);

        for (var frame = 0; frame < 120; frame++)
            Assert.False(pointer.Moved(100, 200));
    }

    [Fact]
    public void AnyChangeOfPositionIsMovementOnce()
    {
        var pointer = new MouseMovement();
        pointer.Moved(10, 10);

        Assert.True(pointer.Moved(11, 10));
        Assert.False(pointer.Moved(11, 10));
        Assert.True(pointer.Moved(11, 9));
        Assert.False(pointer.Moved(11, 9));
    }
}

using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class CameraViewTests
{
    // The real arena (960x640) in the real playfield (800x520).
    private static readonly Rectangle Arena = new Rectangle(0, 0, 960, 640);
    private static readonly Vector2 Playfield = new Vector2(800, 520);

    [Fact]
    public void FollowCentresTheFocusAtFullSize()
    {
        var view = CameraView.Follow(new Vector2(480, 320), Arena, Playfield);

        Assert.Equal(1.0f, view.Zoom);
        Assert.Equal(new Vector2(-80, -60), view.Offset); // camera at (80, 60)
        Assert.Equal(new Vector2(400, 260), view.WorldToView(new Vector2(480, 320))); // focus appears at the playfield centre
    }

    [Theory]
    [InlineData(10, 10, 0, 0)]          // top-left corner clamps
    [InlineData(950, 630, 160, 120)]    // bottom-right corner clamps to (960-800, 640-520)
    [InlineData(10, 630, 0, 120)]
    [InlineData(950, 10, 160, 0)]
    public void FollowClampsAtTheMapEdges(float focusX, float focusY, float cameraX, float cameraY)
    {
        var view = CameraView.Follow(new Vector2(focusX, focusY), Arena, Playfield);

        Assert.Equal(new Vector2(-cameraX, -cameraY), view.Offset);
    }

    [Fact]
    public void FollowStaysAtTheOriginWhenTheMapIsSmallerThanTheViewport()
    {
        var view = CameraView.Follow(new Vector2(100, 100), new Rectangle(0, 0, 400, 300), Playfield);

        Assert.Equal(Vector2.Zero, view.Offset);
    }

    [Fact]
    public void OverviewFitsTheWholeArenaAndCentresIt()
    {
        var view = CameraView.Overview(Arena, Playfield);

        Assert.Equal(520.0f / 640.0f, view.Zoom, 5); // height is the limiting side
        Assert.Equal(0.8125f, view.Zoom, 4);
        Assert.Equal(10.0f, view.Offset.X, 3);       // (800 - 960 * 0.8125) / 2
        Assert.Equal(0.0f, view.Offset.Y, 3);
    }

    [Fact]
    public void OverviewKeepsEveryCornerOfTheArenaInsideThePlayfield()
    {
        var view = CameraView.Overview(Arena, Playfield);

        foreach (var corner in new[] { new Vector2(0, 0), new Vector2(960, 0), new Vector2(0, 640), new Vector2(960, 640) })
        {
            var onScreen = view.WorldToView(corner);
            Assert.InRange(onScreen.X, -0.001f, Playfield.X + 0.001f);
            Assert.InRange(onScreen.Y, -0.001f, Playfield.Y + 0.001f);
        }
    }

    [Fact]
    public void OverviewUsesTheWidthWhenTheArenaIsWide()
    {
        var view = CameraView.Overview(new Rectangle(0, 0, 1600, 400), Playfield);

        Assert.Equal(0.5f, view.Zoom, 5);
        Assert.Equal(0.0f, view.Offset.X, 3);
        Assert.Equal((520.0f - 200.0f) / 2.0f, view.Offset.Y, 3); // centred vertically
    }

    [Fact]
    public void OverviewNeverMagnifiesASmallArena()
    {
        var view = CameraView.Overview(new Rectangle(0, 0, 400, 300), Playfield);

        Assert.Equal(1.0f, view.Zoom);
        Assert.Equal(new Vector2(200, 110), view.Offset); // centred at full size
    }
}

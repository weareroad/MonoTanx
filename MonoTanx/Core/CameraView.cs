using Microsoft.Xna.Framework;
using System;

namespace MonoTanx.Core
{
    // Where the world appears inside the playfield: a zoom and the position of
    // the world origin within the playfield (pixels). Pure maths, so it can be
    // tested without a graphics device.
    public readonly struct CameraView
    {
        public float Zoom { get; }
        public Vector2 Offset { get; }

        public CameraView(float zoom, Vector2 offset)
        {
            Zoom = zoom;
            Offset = offset;
        }

        public Vector2 WorldToView(Vector2 world) => world * Zoom + Offset;

        // Follows the focus point at full size, keeping it centred and clamped
        // so the view never leaves the world.
        public static CameraView Follow(Vector2 focus, Rectangle worldBounds, Vector2 viewportSize)
        {
            var maxX = MathHelper.Max(0.0f, worldBounds.Width - viewportSize.X);
            var maxY = MathHelper.Max(0.0f, worldBounds.Height - viewportSize.Y);
            var cameraX = MathHelper.Clamp(focus.X - viewportSize.X / 2.0f, 0.0f, maxX);
            var cameraY = MathHelper.Clamp(focus.Y - viewportSize.Y / 2.0f, 0.0f, maxY);
            return new CameraView(1.0f, new Vector2(-cameraX, -cameraY));
        }

        // Fits the whole world in the viewport, centred. Never magnifies, so a
        // world smaller than the viewport is shown at full size.
        public static CameraView Overview(Rectangle worldBounds, Vector2 viewportSize)
        {
            var zoom = Math.Min(1.0f, Math.Min(viewportSize.X / worldBounds.Width, viewportSize.Y / worldBounds.Height));
            var offset = new Vector2(
                (viewportSize.X - worldBounds.Width * zoom) / 2.0f,
                (viewportSize.Y - worldBounds.Height * zoom) / 2.0f);
            return new CameraView(zoom, offset);
        }
    }
}

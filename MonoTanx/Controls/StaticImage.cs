using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoTanx.Core;

namespace MonoTanx.Controls
{
    public enum StaticImageLayout { TopLeft, LeftCentre, BottomLeft, TopCentre, Centre, BottomCentre, TopRight, RightCentre, BottomRight};


    public class StaticImage : Component
    {
        private Texture2D texture;

        public Color ImageColor { get; set; } = Color.White;
        public Vector2 Position { get; set; } = Vector2.Zero;
        public Rectangle Bounds { get; set; } = Rectangle.Empty;
        public float Scale { get; set; } = 1.0f;
        public float Rotation { get; set; } = 0f;
        public bool CanRotate { get; set; } = false;
        //public Vector2 Origin { get; set; } = Vector2.Zero;
        public float Layer { get; set; } = 0f;
        public SpriteEffects SpriteEffects { get; set; } = SpriteEffects.None;

        public StaticImageLayout Layout { get; set; } = StaticImageLayout.Centre;

        public StaticImage(Texture2D texture)
        {
            this.texture = texture;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (texture == null) return;
            if (Bounds == Rectangle.Empty)
            {
                spriteBatch.Draw(texture, Position, null, ImageColor, Rotation, Vector2.Zero, Scale, SpriteEffects, Layer);
                return;
            }

            float x = 0f;
            float y = 0f;
            int width = (int)(texture.Width * Scale);
            int height = (int)(texture.Height * Scale);
            if (Layout == StaticImageLayout.TopLeft || Layout == StaticImageLayout.LeftCentre || Layout== StaticImageLayout.BottomLeft)
            {
                x = Position.X;
            }
            if (Layout == StaticImageLayout.TopLeft || Layout == StaticImageLayout.TopCentre || Layout == StaticImageLayout.TopRight)
            {
                y = Position.Y;
            }

            if (Layout == StaticImageLayout.TopRight || Layout == StaticImageLayout.RightCentre || Layout == StaticImageLayout.BottomRight)
            {
                x = Position.X + Bounds.Width - width;
            }
            if (Layout == StaticImageLayout.TopCentre || Layout == StaticImageLayout.Centre || Layout == StaticImageLayout.BottomCentre)
            {
                x = Position.X + (int)((Bounds.Width - width) / 2);
            }

            if (Layout == StaticImageLayout.LeftCentre || Layout == StaticImageLayout.Centre || Layout == StaticImageLayout.RightCentre)
            {
                y = Position.Y + (int)((Bounds.Height - height) / 2);
            }

            if (Layout == StaticImageLayout.BottomLeft || Layout == StaticImageLayout.BottomCentre || Layout == StaticImageLayout.BottomRight)
            {
                y = Position.Y + Bounds.Height - height;
            }

            if (CanRotate)
            {
                var ox = (int)texture.Width/2;
                var oy = (int)texture.Height/2;
                // so we want to work out the origin (as the centre of the scaled texture)
                spriteBatch.Draw(texture, new Vector2(x, y), null, ImageColor, Rotation, new Vector2(ox, oy), Scale, SpriteEffects, Layer);

            }
            else
            spriteBatch.Draw(texture, new Vector2(x, y), null, ImageColor, 0.0f, Vector2.Zero, Scale, SpriteEffects, Layer);
        }

        public override void Update(GameTime gameTime)
        {
        }
    }
}

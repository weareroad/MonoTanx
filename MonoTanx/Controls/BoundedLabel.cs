using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoTanx.Core;

namespace MonoTanx.Controls
{
    public enum BoundedLabelLayout { Left, Centre, Right };


    public class BoundedLabel : Component
    {
        private SpriteFont font;

        public Color PenColor { get; set; } = Color.Black;
        public Vector2 Position { get; set; } = Vector2.Zero;
        public Vector2 Bounds { get; set; } = Vector2.Zero;
        public string Text { get; set; } = "";
        public BoundedLabelLayout Layout { get; set; } = BoundedLabelLayout.Centre;

        public BoundedLabel(SpriteFont font)
        {
            this.font = font;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                float x, y;
                switch (Layout)
                {
                    case BoundedLabelLayout.Left:
                        x = Position.X;
                        y = (Position.Y + (Bounds.Y / 2)) - (font.MeasureString(Text).Y / 2);
                        spriteBatch.DrawString(font, Text, new Vector2(x, y), PenColor);
                        break;

                    case BoundedLabelLayout.Right:
                        x = (Position.X + Bounds.X) - font.MeasureString(Text).X;
                        //y = (Position.Y + Bounds.Y) - font.MeasureString(Text).Y;
                        y = (Position.Y + (Bounds.Y / 2)) - (font.MeasureString(Text).Y / 2);
                        spriteBatch.DrawString(font, Text, new Vector2(x, y), PenColor);
                        break;

                    case BoundedLabelLayout.Centre:
                    default:
                        x = (Position.X + (Bounds.X / 2)) - (font.MeasureString(Text).X / 2);
                        y = (Position.Y + (Bounds.Y / 2)) - (font.MeasureString(Text).Y / 2);
                        spriteBatch.DrawString(font, Text, new Vector2(x, y), PenColor);
                        break;
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
        }
    }
}

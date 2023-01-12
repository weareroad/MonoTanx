using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoTanx.Core;

namespace MonoTanx.Controls
{
    public class Label : Component
    {
        private SpriteFont font;

        public Color PenColor { get; set; } = Color.Black;
        public Vector2 Position { get; set; } = Vector2.Zero;
        public string Text { get; set; } = "";


        public Label(SpriteFont font)
        {
            this.font = font;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                spriteBatch.DrawString(font, Text, Position, PenColor);
            }
        }

        public override void Update(GameTime gameTime)
        {
        }
    }
}
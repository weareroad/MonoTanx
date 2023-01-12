using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Core;
using System;

namespace MonoTanx.Controls
{
    public class Button : Component
    {

        private MouseState currentMouse;
        private SpriteFont font;
        private bool isHovering;
        private MouseState previousMouse;
        private Texture2D texture;


        public event EventHandler Click;
        public bool Clicked { get; private set; }
        public Color PenColour { get; set; } = Color.Black;
        public Vector2 Position { get; set; } = Vector2.Zero;
        public string Text { get; set; } = "";
        public float Scale { get; set; } = 1.0f;

        public Rectangle Rectangle
        {
            get
            {
                return new Rectangle((int)Position.X, (int)Position.Y, texture.Width, texture.Height);
            }
        }
        public Rectangle ScaledRectangle
        {
            get
            {
                return new Rectangle((int)(Position.X * Scale), (int)(Position.Y * Scale), (int)(texture.Width * Scale), (int)(texture.Height * Scale));
            }
        }


        public Button(Texture2D texture, SpriteFont font)
        {
            this.texture = texture;
            this.font = font;
        }


        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            var colour = Color.White;

            if (isHovering)
                colour = Color.Gray;

            spriteBatch.Draw(texture, Rectangle, colour);

            if (!string.IsNullOrEmpty(Text))
            {
                var x = (Rectangle.X + (Rectangle.Width / 2)) - (font.MeasureString(Text).X / 2);
                var y = (Rectangle.Y + (Rectangle.Height / 2)) - (font.MeasureString(Text).Y / 2);

                spriteBatch.DrawString(font, Text, new Vector2(x, y), PenColour);
            }
        }


        public override void Update(GameTime gameTime)
        {
            previousMouse = currentMouse;
            currentMouse = Mouse.GetState();

            var mouseRectangle = new Rectangle(currentMouse.X, currentMouse.Y, 1, 1);

            isHovering = false;

            // compare the mouse position to the actual 'scaled' button rectangle
            if (mouseRectangle.Intersects(ScaledRectangle))
            {
                isHovering = true;

                if (currentMouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
                {
                    Click?.Invoke(this, new EventArgs());
                }
            }
        }
    }
}
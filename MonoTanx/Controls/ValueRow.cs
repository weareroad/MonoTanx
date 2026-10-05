using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Core;

namespace MonoTanx.Controls
{
    // One line of the settings page: the label on the left, the setting's default in
    // the middle and its current value on the right. The value is drawn in a different
    // colour when it is not the default. Highlighted by keyboard navigation or the
    // mouse; a left click asks for an increase and a right click for a decrease.
    public class ValueRow : Component
    {
        private readonly SpriteFont font;
        private readonly SpriteFont smallFont;
        private readonly Texture2D pixel;
        private MouseState previousMouse;

        public ValueRow(Texture2D pixel, SpriteFont font, SpriteFont smallFont)
        {
            this.pixel = pixel;
            this.font = font;
            this.smallFont = smallFont;
        }

        public Rectangle Bounds { get; set; }
        public float Scale { get; set; } = 1.0f;
        public string Label { get; set; } = "";
        public string DefaultText { get; set; } = "";
        public string ValueText { get; set; } = "";
        public bool Selected { get; set; }
        public bool IsDefault { get; set; } = true;

        // Marks a value given on the command line for this run (--set), which the page cannot change.
        public bool Overridden { get; set; }

        public bool IsHovering { get; private set; }

        // +1 if it was left-clicked this update (increase), -1 if right-clicked (decrease), otherwise 0.
        public int ClickedSide { get; private set; }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (Selected)
                spriteBatch.Draw(pixel, Bounds, new Color(40, 90, 85));
            else if (IsHovering)
                spriteBatch.Draw(pixel, Bounds, new Color(40, 44, 50));

            var middle = Bounds.Y + Bounds.Height / 2;
            var labelColour = Selected ? Color.Aquamarine : Color.White;
            spriteBatch.DrawString(font, Label, new Vector2(Bounds.X + 8, middle - font.MeasureString(Label).Y / 2), labelColour);

            var defaultText = "default " + DefaultText;
            spriteBatch.DrawString(smallFont, defaultText, new Vector2(Bounds.X + Bounds.Width * 0.66f, middle - smallFont.MeasureString(defaultText).Y / 2), Color.Gray);

            var value = (Overridden ? "*" : "") + ValueText;
            var valueColour = Overridden ? Color.Orange : IsDefault ? Color.White : Color.Yellow;
            spriteBatch.DrawString(font, value, new Vector2(Bounds.Right - 8 - font.MeasureString(value).X, middle - font.MeasureString(value).Y / 2), valueColour);
        }

        public override void Update(GameTime gameTime)
        {
            var mouse = Mouse.GetState();
            var scaled = new Rectangle((int)(Bounds.X * Scale), (int)(Bounds.Y * Scale), (int)(Bounds.Width * Scale), (int)(Bounds.Height * Scale));
            IsHovering = scaled.Contains(mouse.X, mouse.Y);
            ClickedSide = 0;
            if (IsHovering && mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
                ClickedSide = 1;
            else if (IsHovering && mouse.RightButton == ButtonState.Released && previousMouse.RightButton == ButtonState.Pressed)
                ClickedSide = -1;
            previousMouse = mouse;
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Controls;
using MonoTanx.Core;
using System;
using System.Collections.Generic;

namespace MonoTanx.Stages
{
    // The settings page: tabs for the groups of settings, a row for each setting, Reset
    // all to defaults and Back. Up/Down select, Left/Right change a value (Shift for ten
    // steps, and holding repeats), Delete puts a value back to its default, PageUp/PageDown
    // or Tab change tab, Esc goes back. The mouse hovers, clicks a tab or button, or left-clicks
    // a row to increase it and right-clicks it to decrease it. The settings are saved when the
    // page is left, and when the game is closed with it open. What it edits is
    // Tanx.StoredSettings; a match picks the changes up when it starts.
    public class SettingsStage : Stage
    {
        private const float RowHeight = 36.0f;
        private const float RowsTop = 112.0f;
        private const float RowsLeft = 60.0f;
        private const float RowsWidth = 680.0f;
        private const float TabsTop = 70.0f;

        // Holding a key: the first repeat after this long, then one every so often (seconds).
        private const float RepeatDelay = 0.4f;
        private const float RepeatInterval = 0.06f;

        private readonly SettingsPage page;
        private readonly MatchSetup setup;
        private readonly SpriteFont smallFont;
        private readonly Texture2D pixel;
        private readonly List<ValueRow> rows = new List<ValueRow>();
        private readonly Button resetButton;
        private readonly Button backButton;
        private readonly List<Rectangle> tabRectangles = new List<Rectangle>();
        private MouseState previousMouse;
        private float heldSeconds;
        private float nextRepeat;
        private int heldDirection;
        private bool saved = true;
        private string saveProblem;

        public SettingsStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content, MatchSetup setup)
          : base(game, graphicsDevice, content)
        {
            this.setup = setup;
            baseFont = content.Load<SpriteFont>("SpriteFonts/pixel-emulator");
            smallFont = content.Load<SpriteFont>("SpriteFonts/dogica");
            var buttonTexture = content.Load<Texture2D>("Controls/Button");
            pixel = new Texture2D(graphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            page = new SettingsPage(game.StoredSettings);
            previousMouse = Mouse.GetState();

            for (var index = 0; index < SettingsPage.VisibleRows; index++)
                rows.Add(new ValueRow(pixel, baseFont, smallFont)
                {
                    Bounds = new Rectangle((int)RowsLeft, (int)(RowsTop + index * RowHeight), (int)RowsWidth, (int)RowHeight),
                    Scale = game.ScreenScale
                });

            Button MakeButton(string text, float x, EventHandler click)
            {
                var button = new Button(buttonTexture, baseFont) { Position = new Vector2(x, 462), Text = text, Scale = game.ScreenScale };
                button.Click += click;
                return button;
            }
            resetButton = MakeButton("Reset all", 130, (s, e) => ResetAll());
            backButton = MakeButton("Back", 470, (s, e) => Leave());

            components = new List<Component>(rows) { resetButton, backButton };
            game.Exiting += OnGameExiting;
        }

        public override void OnLeave()
        {
            game.Exiting -= OnGameExiting;
            Persist();
        }

        private void OnGameExiting(object sender, EventArgs e) => Persist();

        private void Persist()
        {
            if (saved || !page.Changed)
                return;
            saveProblem = game.SaveSettings();
            if (saveProblem != null)
                Console.Error.WriteLine(saveProblem);
            saved = true;
        }

        private void Leave()
        {
            Persist();
            game.ChangeStage(new HomeStage(game, graphicsDevice, content, setup));
        }

        private void ResetAll()
        {
            page.ResetAll();
            saved = false;
        }

        private void Change(int direction, bool coarse)
        {
            if (page.Adjust(direction, coarse))
                saved = false;
        }

        public override void PostUpdate(GameTime gameTime)
        {
        }

        public override void Update(GameTime gameTime)
        {
            var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var keyboard = Keyboard.GetState();
            var mouse = Mouse.GetState();
            foreach (var component in components)
                component.Update(gameTime);

            // the mouse moves the highlight and clicks, so both inputs agree
            for (var index = 0; index < rows.Count; index++)
            {
                var rowIndex = page.FirstVisible + index;
                if (rowIndex >= page.Rows.Count)
                    continue;
                if (rows[index].IsHovering)
                    page.Select(rowIndex);
                if (rows[index].ClickedSide != 0)
                {
                    page.Select(rowIndex);
                    Change(rows[index].ClickedSide, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
                }
            }
            if (resetButton.IsHovering) page.Select(page.Rows.Count);
            if (backButton.IsHovering) page.Select(page.Rows.Count + 1);
            if (mouse.LeftButton == ButtonState.Released && previousMouse.LeftButton == ButtonState.Pressed)
                for (var tab = 0; tab < tabRectangles.Count; tab++)
                {
                    var rectangle = tabRectangles[tab];
                    var scaled = new Rectangle((int)(rectangle.X * game.ScreenScale), (int)(rectangle.Y * game.ScreenScale), (int)(rectangle.Width * game.ScreenScale), (int)(rectangle.Height * game.ScreenScale));
                    if (scaled.Contains(mouse.X, mouse.Y))
                        page.OpenGroup(SettingsPage.Groups[tab]);
                }
            previousMouse = mouse;

            bool Pressed(params Keys[] keys)
            {
                foreach (var key in keys)
                    if (keyboard.IsKeyDown(key) && prevKeyboardState.IsKeyUp(key))
                        return true;
                return false;
            }
            var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            if (Pressed(Keys.Up, Keys.W)) page.Previous();
            if (Pressed(Keys.Down, Keys.S)) page.Next();
            if (Pressed(Keys.PageDown) || (Pressed(Keys.Tab) && !shift)) page.NextGroup();
            if (Pressed(Keys.PageUp) || (Pressed(Keys.Tab) && shift)) page.PreviousGroup();
            if (Pressed(Keys.Delete, Keys.Back)) { page.ResetSelected(); saved = false; }

            // Left and Right change a value, repeating while held
            var direction = keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D) ? 1 : keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A) ? -1 : 0;
            if (direction == 0)
            {
                heldDirection = 0;
                heldSeconds = 0.0f;
            }
            else if (direction != heldDirection)
            {
                heldDirection = direction;
                heldSeconds = 0.0f;
                nextRepeat = RepeatDelay;
                Change(direction, shift);
            }
            else
            {
                heldSeconds += elapsed;
                while (heldSeconds >= nextRepeat)
                {
                    Change(direction, shift);
                    nextRepeat += RepeatInterval;
                }
            }

            if (Pressed(Keys.Enter, Keys.Space))
            {
                if (page.IsResetAllSelected) ResetAll();
                else if (page.IsBackSelected) Leave();
            }
            if (Pressed(Keys.Escape)) Leave();

            ShowPage();
            prevKeyboardState = keyboard;
        }

        // Copies the page's state onto the controls.
        private void ShowPage()
        {
            for (var index = 0; index < rows.Count; index++)
            {
                var rowIndex = page.FirstVisible + index;
                var row = rows[index];
                if (rowIndex >= page.Rows.Count)
                {
                    row.Label = "";
                    row.ValueText = "";
                    row.DefaultText = "";
                    row.Selected = false;
                    continue;
                }
                var definition = page.Rows[rowIndex];
                row.Label = definition.Label;
                row.ValueText = SettingsPage.Format(definition, page.Settings.Get(definition.Key));
                row.DefaultText = SettingsPage.Format(definition, definition.Default);
                row.IsDefault = page.Settings.IsDefault(definition.Key);
                row.Overridden = game.OverriddenKeys.Contains(definition.Key);
                row.Selected = rowIndex == page.SelectedIndex;
            }
            resetButton.Selected = page.IsResetAllSelected;
            backButton.Selected = page.IsBackSelected;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();

            var title = "Settings";
            spriteBatch.DrawString(baseFont, title, new Vector2((Tanx.DesignedWidth - baseFont.MeasureString(title).X) / 2, 20), Color.Aquamarine);

            DrawTabs(spriteBatch);

            // empty rows draw nothing, so the list simply ends
            foreach (var row in rows)
                if (row.Label.Length > 0)
                    row.Draw(gameTime, spriteBatch);
            if (page.CanScrollUp)
                spriteBatch.DrawString(smallFont, "^ more", new Vector2(RowsLeft + RowsWidth - 60, RowsTop - 14), Color.Gray);
            if (page.CanScrollDown)
                spriteBatch.DrawString(smallFont, "v more", new Vector2(RowsLeft + RowsWidth - 60, RowsTop + SettingsPage.VisibleRows * RowHeight + 2), Color.Gray);

            resetButton.Draw(gameTime, spriteBatch);
            backButton.Draw(gameTime, spriteBatch);

            spriteBatch.DrawString(smallFont, "Left/Right change  Shift x10  Del default  PgUp/PgDn tab  Esc back", new Vector2(40, 530), Color.Yellow);
            var note = saveProblem != null ? "Could not save: " + saveProblem
                : game.OverriddenKeys.Count > 0 ? "* given with --set for this run; changing it here has no effect until it is dropped"
                : "Click: more  Right-click: less  Applies at game start, saved on leaving";
            spriteBatch.DrawString(smallFont, note, new Vector2(40, 552), saveProblem != null ? Color.Red : Color.Gray);

            spriteBatch.End();
        }

        private void DrawTabs(SpriteBatch spriteBatch)
        {
            tabRectangles.Clear();
            var x = RowsLeft;
            foreach (var group in SettingsPage.Groups)
            {
                var text = SettingsCatalogue.TitleOf(group);
                var size = baseFont.MeasureString(text);
                var rectangle = new Rectangle((int)x - 8, (int)TabsTop - 4, (int)size.X + 16, (int)size.Y + 8);
                tabRectangles.Add(rectangle);
                if (group == page.Group)
                    spriteBatch.Draw(pixel, rectangle, new Color(40, 90, 85));
                spriteBatch.DrawString(baseFont, text, new Vector2(x, TabsTop), group == page.Group ? Color.Aquamarine : Color.White);
                x += size.X + 32;
            }
        }
    }
}

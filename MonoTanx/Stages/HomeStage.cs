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
    // The screen shown at launch (unless --test): start a game, choose one or two
    // players, or quit. Works with the mouse or the keyboard (Up/Down to move,
    // Enter/Space to choose, Left/Right to change the players setting, Esc to quit).
    public class HomeStage : Stage
    {
        private const int StartItem = 0;
        private const int PlayersItem = 1;
        private const int QuitItem = 2;

        private readonly List<Button> buttons = new List<Button>();
        private readonly MenuSelection selection;
        private readonly Button playersButton;
        private GameMode mode;

        public HomeStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
          : base(game, graphicsDevice, content)
        {
            baseFont = content.Load<SpriteFont>("SpriteFonts/pixel-emulator");
            var buttonTexture = content.Load<Texture2D>("Controls/Button");
            mode = game.Options.GameMode;

            Button MakeButton(string text, float y, EventHandler click)
            {
                var button = new Button(buttonTexture, baseFont)
                {
                    Position = new Vector2(300, y),
                    Text = text,
                    Scale = game.ScreenScale
                };
                button.Click += click;
                buttons.Add(button);
                return button;
            }

            MakeButton("Start game", 210, (s, e) => Activate(StartItem));
            playersButton = MakeButton(mode.Label(), 270, (s, e) => Activate(PlayersItem));
            MakeButton("Quit", 330, (s, e) => Activate(QuitItem));
            selection = new MenuSelection(buttons.Count);

            var bounds = new Vector2(600, 40);
            var xPos = (int)((Tanx.DesignedWidth - bounds.X) / 2);
            components = new List<Component>(buttons)
            {
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 100),
                    Bounds = bounds,
                    Text = "MonoTanx",
                    PenColor = Color.Aquamarine
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 520),
                    Bounds = bounds,
                    Text = "Arrows select  Enter choose  Esc quit",
                    PenColor = Color.Yellow
                },
            };
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();

            foreach (var component in components)
                component.Draw(gameTime, spriteBatch);

            spriteBatch.End();
        }

        public override void PostUpdate(GameTime gameTime)
        {
        }

        public override void Update(GameTime gameTime)
        {
            foreach (var component in components)
                component.Update(gameTime);

            // the mouse moves the highlight too, so both inputs agree
            for (var index = 0; index < buttons.Count; index++)
                if (buttons[index].IsHovering)
                    selection.Select(index);

            var keyboard = Keyboard.GetState();
            bool Pressed(params Keys[] keys)
            {
                foreach (var key in keys)
                    if (keyboard.IsKeyDown(key) && prevKeyboardState.IsKeyUp(key))
                        return true;
                return false;
            }

            if (Pressed(Keys.Up, Keys.W)) selection.Previous();
            if (Pressed(Keys.Down, Keys.S)) selection.Next();
            if (Pressed(Keys.Left, Keys.Right, Keys.A, Keys.D) && selection.Index == PlayersItem) Activate(PlayersItem);
            if (Pressed(Keys.Enter, Keys.Space)) Activate(selection.Index);
            if (Pressed(Keys.Escape)) game.Exit();

            for (var index = 0; index < buttons.Count; index++)
                buttons[index].Selected = index == selection.Index;

            prevKeyboardState = keyboard;
        }

        private void Activate(int item)
        {
            switch (item)
            {
                case StartItem:
                    game.ChangeStage(new GameStage(game, graphicsDevice, content, mode));
                    break;
                case PlayersItem:
                    mode = mode.Toggle();
                    playersButton.Text = mode.Label();
                    break;
                case QuitItem:
                    game.Exit();
                    break;
            }
        }
    }
}

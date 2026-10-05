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
    // The screen shown when a match has been won: who won, the score and the
    // rounds played, then play again (a new match with the same players), the home
    // screen, or quit. Works with the mouse or the keyboard (Up/Down to move,
    // Enter/Space to choose, Esc for the home screen, or to quit with --test).
    public class EndStage : Stage
    {
        private const int PlayAgainItem = 0;
        private const int HomeItem = 1;
        private const int QuitItem = 2;

        private readonly List<Button> buttons = new List<Button>();
        private readonly MenuSelection selection;
        private readonly MatchSetup setup;

        public EndStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content, MatchSetup setup, MatchResult result)
          : base(game, graphicsDevice, content)
        {
            this.setup = setup;
            baseFont = content.Load<SpriteFont>("SpriteFonts/pixel-emulator");
            var buttonTexture = content.Load<Texture2D>("Controls/Button");

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

            MakeButton("Play again", 270, (s, e) => Activate(PlayAgainItem));
            MakeButton("Home screen", 330, (s, e) => Activate(HomeItem));
            MakeButton("Quit", 390, (s, e) => Activate(QuitItem));
            selection = new MenuSelection(buttons.Count);

            var bounds = new Vector2(600, 40);
            var xPos = (int)((Tanx.DesignedWidth - bounds.X) / 2);
            BoundedLabel MakeLabel(string text, float y, Color color) => new BoundedLabel(baseFont)
            {
                Position = new Vector2(xPos, y),
                Bounds = bounds,
                Text = text,
                PenColor = color
            };
            components = new List<Component>(buttons)
            {
                MakeLabel(result.WinnerText(setup), 100, Color.Aquamarine),
                MakeLabel(result.ScoreText(setup), 160, Color.White),
                MakeLabel(result.RoundsText, 200, Color.White),
                MakeLabel(game.Options.Test ? "Arrows select  Enter choose  Esc quit" : "Arrows select  Enter choose  Esc home", 520, Color.Yellow),
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
            if (Pressed(Keys.Enter, Keys.Space)) Activate(selection.Index);
            if (Pressed(Keys.Escape)) Activate(game.Options.Test ? QuitItem : HomeItem);

            for (var index = 0; index < buttons.Count; index++)
                buttons[index].Selected = index == selection.Index;

            prevKeyboardState = keyboard;
        }

        private void Activate(int item)
        {
            switch (item)
            {
                case PlayAgainItem:
                    game.ChangeStage(new GameStage(game, graphicsDevice, content, setup));
                    break;
                case HomeItem:
                    game.ChangeStage(new HomeStage(game, graphicsDevice, content));
                    break;
                case QuitItem:
                    game.Exit();
                    break;
            }
        }
    }
}

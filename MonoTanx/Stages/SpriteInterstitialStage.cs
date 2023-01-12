using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Controls;
using MonoTanx.Core;
using System.Collections.Generic;
using static System.Net.WebRequestMethods;

namespace MonoTanx.Stages
{

    //  https://www.pngkey.com/download/u2q8a9y3q8y3t4a9_pocket-mortys-super-fan-rick-side1-rick-and/


    public class SpriteInterstitialStage : Stage
    {
        private double timeSinceStageStarted = 0.0f;
        private BoundedLabel labelTimer;
        private int CountDown = 0;
        private int CountFrom = 8;

        public SpriteInterstitialStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
          : base(game, graphicsDevice, content)
        {
            baseFont = base.content.Load<SpriteFont>("SpriteFonts/pixel-emulator");
            var bounds = new Vector2(600, 40);
            var xPos = (int)((Tanx.DesignedWidth - bounds.X) / 2);

            labelTimer = new BoundedLabel(baseFont)
            {
                Layout = BoundedLabelLayout.Right,
                Position = new Vector2(0, Tanx.DesignedHeight - 30),
                Bounds = new Vector2(Tanx.DesignedWidth, 30),
                Text = $"{CountDown}",
                PenColor = Color.Aquamarine
            };

            components = new List<Component>()
            {
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 150),
                    Bounds = bounds,
                    Text = "Red Rick (slow) - use cursor keys",
                    PenColor = Color.Red
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 190),
                    Bounds = bounds,
                    Text = "Normal Rick - use WASD",
                    PenColor = Color.White
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 230),
                    Bounds = bounds,
                    Text = "Yellow Ricks - various NPC",
                    PenColor = Color.Yellow
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 270),
                    Bounds = bounds,
                    Text = "400 Blue Ricks !! - current dev",
                    PenColor = Color.Blue
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 350),
                    Bounds = bounds,
                    Text = "(Rick asset - pocket mortys super fan, see src)",
                    PenColor = Color.Aquamarine
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 430),
                    Bounds = bounds,
                    Text = "F1 - toggle sprite debug",
                    PenColor = Color.Aquamarine
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 460),
                    Bounds = bounds,
                    Text = "F2 - spawn another NPC",
                    PenColor = Color.Aquamarine
                },
                labelTimer
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
            // remove sprites if they're not needed
        }

        public override void Update(GameTime gameTime)
        {
            var curState = Keyboard.GetState();

            if (curState.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                game.ChangeStage(new MenuStage(game, graphicsDevice, content));
            }


            timeSinceStageStarted += (float)gameTime.ElapsedGameTime.TotalSeconds;
            CountDown = (int)(CountFrom - timeSinceStageStarted);
            if (CountDown<=0)
            {
                CountDown = 0;
                game.ChangeStage(new GameStage(game, graphicsDevice, content));
                return;
            }
            labelTimer.Text = $"{CountDown}";
            foreach (var component in components)
                component.Update(gameTime);

            prevKeyboardState = curState;
        }

    }
}

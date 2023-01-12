using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Controls;
using MonoTanx.Core;
using System.Collections.Generic;

namespace MonoTanx.Stages
{
    public class InterstitialStage : Stage
    {
        private BoundedLabel labelTimer;
        private double timeSinceStageStarted = 0.0f;
        private int CountDown = 0;
        private int CountFrom = 10;
        private Stage finalStage = null;
        private List<string> text = new List<string>();
        public Stage FinalStage { get { return finalStage; } set { finalStage = value; } }
        public List<string> Text { get { return Text; } set { text = value; UpdateText(); } }
        private Vector2 labelBounds;
        private int xPos;

        public InterstitialStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content, Stage final=null, List<string> text=null)
        : base(game, graphicsDevice, content)
        {
            if (final != null)
                finalStage = final;
            if (text != null)
                this.text = text;

            baseFont = base.content.Load<SpriteFont>("SpriteFonts/pixel-emulator");
            labelBounds = new Vector2(600, 40);
            xPos = (int)((Tanx.DesignedWidth - labelBounds.X) / 2);

            labelTimer = new BoundedLabel(baseFont)
            {
                Layout = BoundedLabelLayout.Right,
                Position = new Vector2(0, Tanx.DesignedHeight - 30),
                Bounds = new Vector2(Tanx.DesignedWidth, 30),
                Text = $"{CountDown}",
                PenColor = Color.Aquamarine
            };

            components = new List<Component>();


            UpdateText();
        }

        private void UpdateText()
        {
            // remove all old components
            components.Clear();
            components.Add(labelTimer);

            int y = 0;
            int yoffset = 30;

            foreach (string s in text)
            {
                components.Add(
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, y),
                    Bounds = labelBounds,
                    Text = s,
                    PenColor = Color.White
                });
                y += yoffset;
            }
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
            //throw new System.NotImplementedException();
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
            if (CountDown <= 0)
            {
                CountDown = 0;
                if (finalStage!=null) game.ChangeStage(finalStage);
                return;
            }

            labelTimer.Text = $"{CountDown}";
            foreach (var component in components)
                component.Update(gameTime);

            prevKeyboardState = curState;
        }
    }
}

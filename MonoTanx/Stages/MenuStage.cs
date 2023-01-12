using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoTanx.Controls;
using MonoTanx.Core;
using System;
using System.Collections.Generic;

namespace MonoTanx.Stages
{

    public class MenuStage : Stage
    {

        public MenuStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
          : base(game, graphicsDevice, content)
        {
            baseFont = content.Load<SpriteFont>("SpriteFonts/pixel-emulator");

            var buttonTexture = content.Load<Texture2D>("Controls/Button");
            var bounds = new Vector2(600, 40);
            var xPos = (int)((Tanx.DesignedWidth - bounds.X) / 2);

            var spriteButton = new Button(buttonTexture, baseFont)
            {
                Position = new Vector2(300, 150),
                Text = "Sprite Test",
                Scale = game.ScreenScale
            };
            spriteButton.Click += SpriteButton_Click;


            var mapButton = new Button(buttonTexture, baseFont)
            {
                Position = new Vector2(300, 200),
                Text = "Map Test",
                Scale = game.ScreenScale
            };
            mapButton.Click += MapButton_Click;

            var aiButton = new Button(buttonTexture, baseFont)
            {
                Position = new Vector2(300, 250),
                Text = "AI Test",
                Scale = game.ScreenScale
            };
            aiButton.Click += AIButton_Click;

            var layoutButton = new Button(buttonTexture, baseFont)
            {
                Position = new Vector2(300, 300),
                Text = "Misc Layout",
                Scale = game.ScreenScale
            };
            layoutButton.Click += LayoutButton_Click;

            var quitGameButton = new Button(buttonTexture, baseFont)
            {
                Position = new Vector2(300, 400),
                Text = "Quit",
                Scale = game.ScreenScale
            };
            quitGameButton.Click += QuitGameButton_Click;

            components = new List<Component>()
            {
                spriteButton,
                mapButton,
                layoutButton,
                aiButton,
                quitGameButton,
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 0),
                    Bounds = bounds,
                    Text = "WeAreRoad MonoTanx Tech Demo",
                    PenColor = Color.Aquamarine
                },
                new BoundedLabel(baseFont)
                {
                    Position = new Vector2(xPos, 550),
                    Bounds = bounds,
                    Text = "Press [ESC] to return to menu",
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
            // remove sprites if they're not needed
        }

        public override void Update(GameTime gameTime)
        {
            foreach (var component in components)
                component.Update(gameTime);
        }

        private void QuitGameButton_Click(object sender, EventArgs e)
        {
            game.Exit();
        }

        private void SpriteButton_Click(object sender, EventArgs e)
        {
            game.ChangeStage(new SpriteInterstitialStage(game, graphicsDevice, content));
        }

        private void AIButton_Click(object sender, EventArgs e)
        {
            
            var final = new AIStage(game, graphicsDevice, content);
            List<string> text = new List<string>()
                { 
                    "here is some text",
                    "hang fire"};

            game.ChangeStage(new InterstitialStage(game, graphicsDevice, content, final, text));
            
        }

        private void LayoutButton_Click(object sender, EventArgs e)
        {
            game.ChangeStage(new LayoutStage(game, graphicsDevice, content));
        }

        private void MapButton_Click(object sender, EventArgs e)
        {
           game.ChangeStage(new MapTestStage(game, graphicsDevice, content));
        }

    }
}

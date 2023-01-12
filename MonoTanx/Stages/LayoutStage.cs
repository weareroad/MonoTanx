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

    public class LayoutStage : Stage
    {

        public LayoutStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
          : base(game, graphicsDevice, content)
        {
            baseFont = base.content.Load<SpriteFont>("SpriteFonts/pixel-emulator");
            var robsoft = base.content.Load<Texture2D>("Sprites/Robsoft-Square-256");

            components = new List<Component>()
            {
                new StaticImage(robsoft)
                {
                    Position = new Vector2(0, 0),
                    Bounds = fullScreenBounds,
                    Scale = 0.5f,
                    ImageColor = Color.Blue,
                    Layout = StaticImageLayout.TopCentre,
                    //Rotation = -1.0f * MathHelper.PiOver4,
                    //Origin = new Vector2(0,0)
                },
                new StaticImage(robsoft)
                {
                    Position = new Vector2(0, 0),
                    Bounds = fullScreenBounds,
                    Scale = 1f,
                    ImageColor = Color.Red,
                    Layout = StaticImageLayout.LeftCentre,
                    Rotation = MathHelper.PiOver4,
                    CanRotate = true
                    //Origin = new Vector2(robsoft.Width, robsoft.Height)
                },
                new StaticImage(robsoft)
                {
                    Position = new Vector2(0, 0),
                    Bounds = fullScreenBounds,
                    Scale = 1f,
                    ImageColor = Color.Yellow,
                    Layout = StaticImageLayout.BottomLeft,
                    Rotation = -1.0f * MathHelper.PiOver4,
                    CanRotate = true
                    //Origin = new Vector2(robsoft.Width/2, robsoft.Height/2)
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
            var curState = Keyboard.GetState();

            if (curState.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                game.ChangeStage(new MenuStage(game, graphicsDevice, content));
            }

            foreach (var component in components)
                component.Update(gameTime);

            prevKeyboardState = curState;
        }

    }
}

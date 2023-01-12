using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Controls;
using MonoTanx.Core;
using System.Collections.Generic;

namespace MonoTanx.Stages
{

    public class AIStage : Stage
    {
        /*
        private List<Component> components;
        private KeyboardState prevKeyboardState;
        */

        public AIStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
          : base(game, graphicsDevice, content)
        {
            baseFont = base.content.Load<SpriteFont>("SpriteFonts/pixel-emulator");
            var bounds = new Vector2(600, 40);

            var xPos = (int)((Tanx.DesignedWidth - bounds.X) / 2);

            components = new List<Component>()
            {
            new BoundedLabel(baseFont)
            {
                Position = new Vector2(xPos, 50),
                Bounds = bounds,
                Text = "nothing here yet",
                PenColor = Color.Aquamarine
            }
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoTanx.Core;

namespace MonoTanx.Stages
{
    public class MapTestStage : Stage
    {
        private SpriteBatch spriteBatch;
        private TileMapManager tileMapManager;

        public MapTestStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
        : base(game, graphicsDevice, content)
        {
            spriteBatch = new SpriteBatch(graphicsDevice);
            LoadStageResources();
        }

        private void LoadStageResources()
        {
            tileMapManager = new TileMapManager(content, "monosandpittest.tmx", "tmw_desert_spacing");

        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin(SpriteSortMode.FrontToBack);
            tileMapManager.Draw(spriteBatch, gameTime);
            spriteBatch.End();
        }

        public override void PostUpdate(GameTime gameTime)
        {
            //throw new NotImplementedException();
        }

        public override void Update(GameTime gameTime)
        {
            var curState = Keyboard.GetState();

            if (curState.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                game.ChangeStage(new MenuStage(game, graphicsDevice, content));
            }
            prevKeyboardState = curState;
        }
    }
}

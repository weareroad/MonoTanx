using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace MonoTanx.Core

{
    public abstract class Stage
    {
        protected ContentManager content;
        protected GraphicsDevice graphicsDevice;

        protected Tanx game;

        protected List<Component> components;
        protected KeyboardState prevKeyboardState;
        protected Rectangle fullScreenBounds = new Rectangle(0, 0, (int)Tanx.DesignedWidth, (int)Tanx.DesignedHeight);
        protected SpriteFont baseFont;// = content.Load<SpriteFont>("SpriteFonts/pixel-emulator");

        public abstract void Draw(GameTime gameTime, SpriteBatch spriteBatch);

        public abstract void PostUpdate(GameTime gameTime);

        public Stage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
        {
            this.game = game;
            this.graphicsDevice = graphicsDevice;
            this.content = content;
            //this.baseFont = baseFont;
        }

        public abstract void Update(GameTime gameTime);

    }
}

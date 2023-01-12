using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Core;
using MonoTanx.Stages;
using System;

namespace MonoTanx
{
    public class Tanx : Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private RenderTarget2D renderTarget;
        private Stage currentStage;
        private Stage nextStage;

        public float ScreenScale = 0.44f;
        public float TimePassedMS = 0.0f;

        // this is what we are 'designing the game at'
        //        public static float DesignedWidth = 960.0F;
        //        public static float DesignedHeight = 640.0F;
        public static float DesignedWidth = 800.0F;
        public static float DesignedHeight = 600.0F;

        /*
            Common resolutions/aspect ratios
            16:9, as in 1280x720px
            16:10, as in 1680x1050px
            5:3, as in 1280x768px
            3:2, as in 960x640 (This screen ratio is also used on iDevices.)
            4:3, as in 1024x768
        */

        public Tanx()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // lock at 60FPS
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0f);
            IsFixedTimeStep = true;

            base.Initialize();

            // this is what we want to show the screen at - we might allow the user to change this
            graphics.PreferredBackBufferWidth = 1366;// 1024;
            graphics.PreferredBackBufferHeight = 1024;// 768;
            graphics.ApplyChanges();

            // this height is the 'designed' height
            ScreenScale = 1.0f / (DesignedHeight / GraphicsDevice.Viewport.Height);

            spriteBatch = new SpriteBatch(GraphicsDevice);
            currentStage = new MenuStage(this, graphics.GraphicsDevice, Content);
            //currentStage = new MapTestStage(this, graphics.GraphicsDevice, Content);
            //currentStage = new SplashStage(this, graphics.GraphicsDevice, Content);
            //currentStage = new GameStage(this, graphics.GraphicsDevice, Content);

        }

        protected override void LoadContent()
        {
            // LoadContent happens before Initialize....
            renderTarget = new RenderTarget2D(GraphicsDevice, (int)DesignedWidth, (int)DesignedHeight);
        }

        public void ChangeStage(Stage stage)
        {
            if (stage != null)
                nextStage = stage;
        }

        protected override void Update(GameTime gameTime)
        {
            if (nextStage != null)
            {
                currentStage = nextStage;
                nextStage = null;
            }

            currentStage.Update(gameTime);
            currentStage.PostUpdate(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(renderTarget);
            GraphicsDevice.Clear(Color.Black);
            currentStage.Draw(gameTime, spriteBatch);
            GraphicsDevice.SetRenderTarget(null);


            spriteBatch.Begin();
            spriteBatch.Draw(renderTarget, Vector2.Zero, null, Color.White, 0, Vector2.Zero, ScreenScale, SpriteEffects.None, 0f);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
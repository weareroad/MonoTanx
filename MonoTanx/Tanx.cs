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

        public RandomStreams Random { get; }

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

        private readonly GameOptions options;

        public GameOptions Options => options;

        public Tanx(GameOptions options)
        {
            this.options = options;
            Random = new RandomStreams(options.Seed ?? RandomStreams.NewSeed());
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // fixed update rate
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / Tuning.Timing.UpdatesPerSecond);
            IsFixedTimeStep = true;

            base.Initialize();

            // fullscreen at the desktop resolution by default; --windowed gives an
            // integer multiple of the designed size
            if (options.Windowed)
            {
                graphics.IsFullScreen = false;
                graphics.PreferredBackBufferWidth = (int)DesignedWidth * options.Scale;
                graphics.PreferredBackBufferHeight = (int)DesignedHeight * options.Scale;
            }
            else
            {
                graphics.IsFullScreen = true;
                graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            }
            graphics.ApplyChanges();

            // this height is the 'designed' height
            ScreenScale = Math.Min(GraphicsDevice.Viewport.Width / DesignedWidth, GraphicsDevice.Viewport.Height / DesignedHeight);

            spriteBatch = new SpriteBatch(GraphicsDevice);
            // --test goes straight into a game; otherwise start at the home screen
            currentStage = options.Test
                ? new GameStage(this, graphics.GraphicsDevice, Content, options.GameMode)
                : new HomeStage(this, graphics.GraphicsDevice, Content);

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


            var scale = Math.Min(GraphicsDevice.Viewport.Width / DesignedWidth, GraphicsDevice.Viewport.Height / DesignedHeight);
            ScreenScale = scale;
            var destinationWidth = (int)(DesignedWidth * scale);
            var destinationHeight = (int)(DesignedHeight * scale);
            var destination = new Rectangle(
                (GraphicsDevice.Viewport.Width - destinationWidth) / 2,
                (GraphicsDevice.Viewport.Height - destinationHeight) / 2,
                destinationWidth,
                destinationHeight);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(renderTarget, destination, Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}

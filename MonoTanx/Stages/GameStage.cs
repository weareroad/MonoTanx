using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoTanx.Stages
{
    public class GameStage : Stage
    {
        private const bool INDEBUGMODE = false;
        private const int TESTSPRITES = 20;

        private SpriteBatch spriteBatch;
        private List<Sprite> sprites;
        private Random RandomGenerator = new Random(42);    // use a hardcoded seed so we will always see the same 'values'
        private SpriteFont debugFont;
        private Vector2 screenCentre = Vector2.Zero;
        private bool curDebug = INDEBUGMODE;

        public GameStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content)
          : base(game, graphicsDevice, content)
        {
            spriteBatch = new SpriteBatch(graphicsDevice);
            LoadStageResources();
        }

        private void LoadStageResources()
        {
            debugFont = content.Load<SpriteFont>("spritefonts/dogica");
            var sizeRefAnim = new Animation(content.Load<Texture2D>("Sprites/rickleft"), 4);
            var sizeRefWidth = sizeRefAnim.FrameWidth;
            var sizeRefHeight = sizeRefAnim.FrameHeight;
            screenCentre = new Vector2((int)(Tanx.DesignedWidth - sizeRefWidth) / 2, (int)(Tanx.DesignedHeight - sizeRefHeight) / 2);

            sprites = new List<Sprite>()
            {
                new Sprite(GetRickAnimation(), "MoveRight")
                {
                    Position = new Vector2(10, 10),
                    Speed = 3f,
                    SortDepth = 0.5f,
                    Bounds=fullScreenBounds,
                    Input = new Input()
                    {
                        Up = Keys.W,
                        Down = Keys.S,
                        Left = Keys.A,
                        Right = Keys.D,
                        Fire = Keys.R,
                        Select = Keys.T
                    },
                    DebugID = $"P1",
                    Debug = curDebug,
                    DebugFont = debugFont
                },

                new Sprite(GetRickAnimation(), "MoveLeft")
                {
                    Position = new Vector2(Tanx.DesignedWidth - sizeRefWidth - 10,
                        (int)(Tanx.DesignedHeight - sizeRefHeight) - 10),
                    Speed = 2f,
                    SortDepth = 0.5f,
                    Bounds=fullScreenBounds,
                    TintColor = Color.Red,
                    Input = new Input()
                    {
                        Up = Keys.Up,
                        Down = Keys.Down,
                        Left = Keys.Left,
                        Right = Keys.Right,
                        Fire = Keys.Enter,
                        Select = Keys.RightShift
                    },
                    DebugID = $"P2",
                    Debug = curDebug,
                    DebugFont = debugFont

                },

                new Sprite(GetRickAnimation(), "MoveUp")
                {
                    Position = screenCentre,
                    Speed = 1f,
                    SortDepth = 0.2f,
                    TintColor = Color.Yellow,
                    Bounds = fullScreenBounds,
                    PatternVelocity = new Vector2(0,-1),
                    MovementPattern = SpriteMovementPattern.PingPong,
                     DebugID = $"Bobo",
                          Debug = curDebug,
                          DebugFont = debugFont
                },
                new Sprite(GetRickAnimation(), "MoveLeft")
                {
                    Position = screenCentre,
                    SortDepth = 0.2f,
                    Speed = 3f,
                    RandomGenerator = this.RandomGenerator,
                    TintColor = Color.Blue,
                    Bounds = fullScreenBounds,
                    PatternVelocity = new Vector2(-1, 0),
                    MovementPattern = SpriteMovementPattern.RandomRicochet,
                    DebugID = $"Keith",
                    Debug = curDebug,
                    DebugFont = debugFont
                },
                new Sprite(GetRickAnimation(), "MoveRight")
                {
                    Position = screenCentre,
                    SortDepth = 0.2f,
                    Speed = 2f,
                    TintColor = Color.Yellow,
                    Bounds = fullScreenBounds,
                    PatternVelocity = new Vector2(1, -1),
                    MovementPattern = SpriteMovementPattern.Ricochet,
                    DebugID = $"Coco",
                    Debug = curDebug,
                    DebugFont = debugFont
                },
                new Sprite(GetRickAnimation(), "MoveDown")
                {
                    Position = screenCentre,
                    SortDepth = 0.2f,
                    Speed = 4f,
                    RandomGenerator = this.RandomGenerator,
                    TintColor = Color.Blue,
                    Bounds = fullScreenBounds,
                    PatternVelocity = new Vector2(0, 1),
                    MovementPattern = SpriteMovementPattern.RandomRicochet,
                    DebugID = "Ralph",
                    Debug = curDebug,
                    DebugFont = debugFont
                },
                new Sprite(GetRickAnimation(), "MoveLeft")
                {
                    Position = screenCentre,
                    SortDepth = 0.2f,
                    Speed = 3f,
                    RandomGenerator = this.RandomGenerator,
                    TintColor = Color.Blue,
                    Bounds = fullScreenBounds,
                    PatternVelocity = new Vector2(-1, 1),
                    MovementPattern = SpriteMovementPattern.RandomRicochet,
                    DebugID = "Cliff",
                    Debug = curDebug,
                    DebugFont = debugFont
                }

            };

            // now make a series of similar (random) sprites
            for (int i = 0; i < TESTSPRITES; i++)
            {
                Spawn();
            }
        }

        private void Spawn()
        { 
             var velocity = Helpers.GetRandomVelocity(RandomGenerator);
             var startAnim = Helpers.GetDirectionFromVelocity(velocity, "MoveLeft", "MoveRight", "MoveUp", "MoveDown", "MoveLeft");
             sprites.Add(
                   new Sprite(GetRickAnimation(), startAnim)
                   {
                          Position = screenCentre,
                          SortDepth = 0.2f,
                          Speed = 1f + (this.RandomGenerator.Next(3)),
                          RandomGenerator = this.RandomGenerator,
                          TintColor = Color.Blue,
                          Bounds = fullScreenBounds,
                          PatternVelocity = velocity,
                          MovementPattern = SpriteMovementPattern.RandomRicochet,
                          DebugID = $"{sprites.Count+1}",
                          Debug = curDebug,
                          DebugFont = debugFont
                   });
        }



        private Dictionary<string, Animation> GetRickAnimation()
        {
            return new Dictionary<string, Animation>()
                    {
                            {"MoveLeft", new Animation(content.Load<Texture2D>("Sprites/rickleft"), 4) { Looping=false } },
                            {"MoveRight", new Animation(content.Load<Texture2D>("Sprites/rickleft"), 4) { Looping=false, FlipMode=SpriteEffects.FlipHorizontally } },
                            {"MoveUp", new Animation(content.Load<Texture2D>("Sprites/rickback"), 4)  { Looping=false }},
                            {"MoveDown", new Animation(content.Load<Texture2D>("Sprites/rickfront"), 4) { Looping=false } },
                    };
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            graphicsDevice.Clear(Color.Black);

            // FrontToBack ensures that our 'SortDepth' settings will be observed, rather than draw/create order
            spriteBatch.Begin(SpriteSortMode.FrontToBack);
            foreach (var sprite in sprites)
                sprite.Draw(spriteBatch);
            if (curDebug)
            {
                var str = $"{sprites.Count} sprites";
                spriteBatch.DrawString(debugFont, str, new Vector2((Tanx.DesignedWidth - debugFont.MeasureString(str).X) / 2, 0), Color.Yellow,0,Vector2.Zero,1f,SpriteEffects.None,0.99f);
            }

            spriteBatch.End();
        }



        public override void PostUpdate(GameTime gameTime)
        {

        }


        public override void Update(GameTime gameTime)
        {
            foreach (var sprite in sprites)
                sprite.Update(gameTime, sprites);

            var curState = Keyboard.GetState();

            if (curState.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                game.ChangeStage(new MenuStage(game, graphicsDevice, content));
            }

            if (curState.IsKeyDown(Keys.F1) && prevKeyboardState.IsKeyUp(Keys.F1))
            {
                ToggleDebug();
            }
            if (curState.IsKeyDown(Keys.F2) && prevKeyboardState.IsKeyUp(Keys.F2))
            {
                Spawn();
            }
            prevKeyboardState = curState;
        }

        private void ToggleDebug()
        {
            curDebug = !curDebug;
            foreach (var sprite in sprites)
            {
                if (sprite.DebugFont!=null)
                {
                    sprite.Debug = curDebug;
                }
            }
        }
    }
}

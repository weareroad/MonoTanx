using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTanx.Core
{

    public enum SpriteMovementPattern { Static, PingPong, UntilBlocked, Ricochet, RandomRicochet };

    public class Sprite
    {
        private Texture2D texture = null;
        public Input Input = null;

        public Vector2 Position
        {
            get
            { return position; }
            set
            {
                position = value;
                if (animationManager != null)
                {
                    animationManager.Position = position;
                }
            }
        }

        public float Speed = 1f;
        public Vector2 Velocity = Vector2.Zero;
        public Color TintColor { get; set; } = Color.White;
        public Rectangle Bounds { get; set; }
        public float SortDepth { get; set; } = 0f;
        public Vector2 PatternVelocity { get; set; } = Vector2.Zero;
        public SpriteMovementPattern MovementPattern { get; set; } = SpriteMovementPattern.Static;
        public Random RandomGenerator { get; set; } = null;
        protected AnimationManager animationManager = null;
        protected Dictionary<string, Animation> animations;
        protected Vector2 position = Vector2.Zero;
        public string DebugID { get; set; } = "";
        public bool Debug { get; set; } = false;
        public SpriteFont DebugFont = null;
        public Color DebugColor = Color.White;

        public Sprite(Texture2D texture)
        {
            this.texture = texture;
        }

        public Sprite(Dictionary<string, Animation> animations, string StartKey="")
        {
            this.animations = animations;
            if (StartKey!="")
                animationManager = new AnimationManager(this.animations[StartKey]);
            else
                animationManager = new AnimationManager(this.animations.First().Value);
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (texture != null)
                spriteBatch.Draw(texture, Position, null, TintColor, 0, Vector2.Zero, 1.0f, SpriteEffects.None, SortDepth);
            else if (animationManager != null)
                animationManager.Draw(spriteBatch, TintColor, SortDepth);

            // debug output - use the font we were given
            if (Debug && DebugID != "" && DebugFont != null)
                spriteBatch.DrawString(DebugFont, DebugID, Position, DebugColor, 0, Vector2.Zero, 1f, SpriteEffects.None, SortDepth);
        }

        protected virtual void Move()
        {
            if (Input != null)
            {
                if (Keyboard.GetState().IsKeyDown(Input.Up) && Keyboard.GetState().IsKeyUp(Input.Down))
                    Velocity.Y = -Speed;
                else if (Keyboard.GetState().IsKeyDown(Input.Down) && Keyboard.GetState().IsKeyUp(Input.Up))
                    Velocity.Y = Speed;
                if (Keyboard.GetState().IsKeyDown(Input.Left) && Keyboard.GetState().IsKeyUp(Input.Right))
                    Velocity.X = -Speed;
                else if (Keyboard.GetState().IsKeyDown(Input.Right) && Keyboard.GetState().IsKeyUp(Input.Left))
                    Velocity.X = Speed;
            }

            if (MovementPattern!=SpriteMovementPattern.Static)
            {
                Velocity = PatternVelocity * Speed;
            }

            if (Velocity == Vector2.Zero)
            {
                animationManager.Stop();
            }
        }


        public virtual void Update(GameTime gameTime, List<Sprite> sprites)
        {
            Move();

            SetAnimations();
            animationManager.Update(gameTime);

            if (Velocity != Vector2.Zero)
            {
                float sprHeight, sprWidth;
                if (texture!=null)
                {
                    sprHeight = texture.Height;
                    sprWidth = texture.Width;
                }
                else
                {
                    sprHeight = animations.First().Value.FrameHeight;
                    sprWidth = animations.First().Value.FrameWidth;
                }

                var oldPos = Position;
                Position += Velocity;
                if (Position.X<Bounds.Left || Position.X+sprWidth>Bounds.Width || Position.Y<Bounds.Top || Position.Y+sprHeight>Bounds.Bottom)
                {
                    if (MovementPattern == SpriteMovementPattern.Ricochet)
                    {
                        var px = PatternVelocity.X;
                        var py = PatternVelocity.Y;
                        if (Position.X < Bounds.Left || Position.X + sprWidth > Bounds.Width)
                            px = px * -1;
                        if (Position.Y < Bounds.Top || Position.Y + sprHeight > Bounds.Bottom)
                            py = py * -1;
                        PatternVelocity = new Vector2(px, py);
                    }
                    else if (MovementPattern == SpriteMovementPattern.RandomRicochet)
                    {
                        //var px = PatternVelocity.X;
                        //var py = PatternVelocity.Y;
                        int flipx, flipy = 0;
                        int bounce = 1;
                        do {
                            flipx = RandomGenerator.Next(0, 2);
                            flipy = RandomGenerator.Next(0, 2);
                            bounce = RandomGenerator.Next(0, 2);
                        } while (flipx + flipy == 0);

                        if (bounce==0)
                            PatternVelocity = new Vector2(flipx, flipy);
                        else
                            PatternVelocity = new Vector2(flipx * -1, flipy * -1);
                        
                        // ok so can we do this without causing a 'headbang'?
                        var newPos = oldPos += (PatternVelocity * Speed);
                        if (newPos.X < Bounds.Left || newPos.X + sprWidth > Bounds.Width || newPos.Y < Bounds.Top || newPos.Y + sprHeight > Bounds.Bottom || newPos==oldPos)
                        {
                            Position = oldPos;
                            var px = PatternVelocity.X;
                            var py = PatternVelocity.Y;
                            if (Position.X < Bounds.Left || Position.X + sprWidth > Bounds.Width)
                                px = px * -1;
                            if (Position.Y < Bounds.Top || Position.Y + sprHeight > Bounds.Bottom)
                                py = py * -1;
                            PatternVelocity = new Vector2(px, py);
                        }

                    }


                    else if (MovementPattern==SpriteMovementPattern.PingPong)
                    {
                         PatternVelocity = PatternVelocity * -1;
                    }
                    else if (MovementPattern == SpriteMovementPattern.UntilBlocked)
                    {
                        PatternVelocity = Vector2.Zero;
                    }

                    Position = oldPos;
                }

                Velocity = Vector2.Zero;
            }

        }

        protected virtual void SetAnimations()
        {
            if (Velocity.X > 0)
            {
                animationManager.Play(animations["MoveRight"]);
            }
            else if (Velocity.X < 0)
            {
                animationManager.Play(animations["MoveLeft"]);
            }
            else if (Velocity.Y < 0)
            {
                animationManager.Play(animations["MoveUp"]);
            }
            else if (Velocity.Y > 0)
            {
                animationManager.Play(animations["MoveDown"]);
            }
        }

   
    }
}

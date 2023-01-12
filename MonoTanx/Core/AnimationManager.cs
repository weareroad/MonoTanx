using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTanx.Core
{
    public class AnimationManager
    {
        private Animation animation;
        private float timer;

        public Vector2 Position { get; set; } = Vector2.Zero;

        public AnimationManager(Animation animation)
        {
            this.animation = animation;
        }

        public void Draw(SpriteBatch spriteBatch, Color tintColor, float SortDepth = 0f)
        {
            var pos = animation.CurrentFrame * animation.FrameWidth;
            
            spriteBatch.Draw(animation.Texture, Position, 
                new Rectangle(pos, 0, animation.FrameWidth, animation.FrameHeight),
                tintColor, animation.Rotation, Vector2.Zero, animation.Scale, animation.FlipMode, SortDepth);
           
        }

        public void Play(Animation animation)
        {
            if (animation == this.animation && animation.Stopped==false) return;

            this.animation = animation;
            this.animation.Stopped = false;
            this.animation.CurrentFrame = 0;
            timer = 0f;
        }

        public void Stop()
        {
            this.animation.CurrentFrame = 0;
            this.animation.Stopped = true;
            timer = 0f;
        }

        public void Update(GameTime gameTime)
        {
            if (animation.Stopped) return;

            timer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (timer > animation.FrameSpeed)
            {
                timer = 0f;
                animation.CurrentFrame++;

                if (animation.CurrentFrame >= animation.FrameCount)
                {
                    animation.CurrentFrame = 0;
                    if (!animation.Looping)
                    {
                        animation.Stopped = true;
                    }
                }
            }
        }


    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoTanx.Core
{
    public class Animation
    {
        public int CurrentFrame { get; set; } = 0;
        public int FrameCount { get; set; } = 1;
        public int FrameHeight { get { return Texture.Height; } }
        public int FrameWidth { get { return Texture.Width / FrameCount; } }
        public float FrameSpeed { get; set; } = 0.2f;
        public bool Looping { get; set; } = true;
        public bool Stopped { get; set; } = true;

        public float Rotation { get; set; } = 0f;
        public float Scale { get; set; } = 1f;
        public SpriteEffects FlipMode { get; set; } = SpriteEffects.None;

        public Texture2D Texture { get; private set; }

        public Animation(Texture2D texture, int frameCount)
        {
            this.Texture = texture;
            this.FrameCount = frameCount;
        }

    }
}

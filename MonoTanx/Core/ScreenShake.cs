using Microsoft.Xna.Framework;
using System;

namespace MonoTanx.Core
{
    // A decaying screen shake whose timing is driven entirely by elapsed game
    // time. The random jitter is resampled at a fixed rate inside Update, so the
    // result does not depend on how often it is drawn.
    public sealed class ScreenShake
    {
        private float remaining;
        private float duration;
        private float magnitude;
        private float jitterTimer;
        private Vector2 noise;

        public bool IsActive => remaining > 0.0f && duration > 0.0f;

        // Current offset in pixels. Safe to read any number of times per frame.
        public Vector2 Offset => IsActive ? noise * magnitude * (remaining / duration) : Vector2.Zero;

        // Overlapping shakes keep the longest remaining time, the longest
        // duration and the largest magnitude until they have all died away.
        public void Start(float shakeDuration, float shakeMagnitude)
        {
            if (!IsActive)
                jitterTimer = Tuning.Shake.JitterIntervalSeconds; // resample on the next update
            remaining = Math.Max(remaining, shakeDuration);
            duration = Math.Max(duration, shakeDuration);
            magnitude = Math.Max(magnitude, shakeMagnitude);
        }

        public void Update(float elapsed, Random random)
        {
            if (!IsActive)
                return;

            jitterTimer += elapsed;
            while (jitterTimer >= Tuning.Shake.JitterIntervalSeconds)
            {
                jitterTimer -= Tuning.Shake.JitterIntervalSeconds;
                noise = new Vector2(
                    (float)random.NextDouble() * 2.0f - 1.0f,
                    (float)random.NextDouble() * 2.0f - 1.0f);
            }

            remaining = Math.Max(0.0f, remaining - elapsed);
            if (remaining <= 0.0f)
            {
                duration = 0.0f;
                magnitude = 0.0f;
                noise = Vector2.Zero;
            }
        }
    }
}

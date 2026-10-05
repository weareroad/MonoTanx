namespace MonoTanx.Core
{
    // How many times a second something happens (updates, frames), averaged over a short
    // window so the number is steady enough to read. Given the time on each tick, so it
    // needs no clock of its own and can be tested. Counting costs a few instructions, so it
    // can run all the time; only showing the number is behind the debug overlay.
    public sealed class RateMeter
    {
        private readonly double windowSeconds;
        private double windowStart;
        private int count;
        private bool started;

        public RateMeter(double windowSeconds = 0.5)
        {
            this.windowSeconds = windowSeconds;
        }

        // The rate over the last full window, per second; 0 until one has passed.
        public double PerSecond { get; private set; }

        // Records one occurrence at this time (seconds, from any steady clock).
        public void Tick(double nowSeconds)
        {
            if (!started)
            {
                started = true;
                windowStart = nowSeconds;
                return;
            }
            count++;
            var elapsed = nowSeconds - windowStart;
            if (elapsed >= windowSeconds)
            {
                PerSecond = count / elapsed;
                count = 0;
                windowStart = nowSeconds;
            }
        }
    }
}

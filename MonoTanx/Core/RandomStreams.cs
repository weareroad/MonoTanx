using System;
using System.Text;

namespace MonoTanx.Core
{
    // One master seed per run, from which independent named random streams are
    // derived. Gameplay and cosmetic randomness use separate streams so a
    // purely visual draw can never change a gameplay outcome. Pass the stream
    // (a plain Random) to whatever needs it; there is no global instance.
    public sealed class RandomStreams
    {
        public int Seed { get; }
        public Random Gameplay { get; }
        public Random Cosmetic { get; }

        public RandomStreams(int seed)
        {
            Seed = seed;
            Gameplay = CreateStream("gameplay");
            Cosmetic = CreateStream("cosmetic");
        }

        public static int NewSeed() => Random.Shared.Next();

        // The same seed and name always give the same sequence.
        public Random CreateStream(string name) => new Random(DeriveSeed(Seed, name));

        // FNV-1a over the seed and name. string.GetHashCode is randomised per
        // process, so it cannot be used here.
        private static int DeriveSeed(int seed, string name)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var b in BitConverter.GetBytes(seed))
                    hash = (hash ^ b) * 16777619u;
                foreach (var b in Encoding.UTF8.GetBytes(name))
                    hash = (hash ^ b) * 16777619u;
                return (int)hash;
            }
        }
    }
}

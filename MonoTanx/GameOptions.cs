using System;
using System.Globalization;

namespace MonoTanx
{
    public sealed class GameOptions
    {
        // Master seed for the run. Null means choose one at random.
        public int? Seed { get; private set; }

        public static GameOptions Parse(string[] args)
        {
            var options = new GameOptions();
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--seed":
                        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
                            throw new ArgumentException("--seed requires an integer value.");
                        options.Seed = seed;
                        index++;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{args[index]}'.");
                }
            }
            return options;
        }
    }
}

using MonoTanx.Core;
using System;
using System.Globalization;

namespace MonoTanx
{
    public sealed class GameOptions
    {
        // Master seed for the run. Null means choose one at random.
        public int? Seed { get; private set; }

        // Skip the menu and start a game straight away (for development and quick testing).
        public bool Test { get; private set; }

        // One or two human players. Used when a game is started from the command line.
        public GameMode GameMode { get; private set; } = GameMode.OnePlayer;

        // Run in a window instead of fullscreen.
        public bool Windowed { get; private set; }

        public const int MinimumScale = 1;
        public const int MaximumScale = 4;
        public const int DefaultScale = 2;

        // Integer multiple of the 800x600 logical surface used for the window
        // size. Only applies when Windowed.
        public int Scale { get; private set; } = DefaultScale;

        public static GameOptions Parse(string[] args)
        {
            var options = new GameOptions();
            var scaleGiven = false;
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
                    case "--test":
                    case "-test":
                        options.Test = true;
                        break;
                    case "--two-player":
                        options.GameMode = GameMode.TwoPlayer;
                        break;
                    case "--windowed":
                        options.Windowed = true;
                        break;
                    case "--scale":
                        if (index + 1 >= args.Length
                            || !int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale)
                            || scale < MinimumScale || scale > MaximumScale)
                            throw new ArgumentException($"--scale requires an integer from {MinimumScale} to {MaximumScale}.");
                        options.Scale = scale;
                        scaleGiven = true;
                        index++;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{args[index]}'.");
                }
            }
            if (scaleGiven && !options.Windowed)
                throw new ArgumentException("--scale only applies together with --windowed.");
            return options;
        }
    }
}

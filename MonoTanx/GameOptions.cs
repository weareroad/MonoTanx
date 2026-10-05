using MonoTanx.Core;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MonoTanx
{
    public sealed class GameOptions
    {
        // Master seed for the run. Null means choose one at random.
        public int? Seed { get; private set; }

        // Skip the menu and start a game straight away (for development and quick testing).
        public bool Test { get; private set; }

        // Who controls each seat. Used when a game is started from the command line.
        public MatchSetup Setup { get; private set; } = MatchSetup.OnePlayer;

        // Start with all sound off.
        public bool Mute { get; private set; }

        // Run in a window instead of fullscreen.
        public bool Windowed { get; private set; }

        public const int MinimumScale = 1;
        public const int MaximumScale = 4;
        public const int DefaultScale = 2;

        // Integer multiple of the 800x600 logical surface used for the window
        // size. Only applies when Windowed.
        public int Scale { get; private set; } = DefaultScale;

        // Asks for the help text. When any of these is given, nothing else is parsed
        // or run: help always wins, even over an invalid option.
        public static readonly string[] HelpFlags = { "--help", "-h", "-?", "/?" };

        // True when help was asked for; the caller prints HelpText and exits.
        public bool ShowHelp { get; private set; }

        // Where the parser is while it reads the arguments, so options can check each other.
        private sealed class ParseState
        {
            public bool ScaleGiven;
            public string SetupFlag;
        }

        // One command-line option. The parser and the help text both read this one
        // list, so the help cannot drift from what is actually accepted.
        private sealed class OptionSpec
        {
            public string[] Names { get; }
            public string ValueName { get; }
            public string Description { get; }
            public Action<GameOptions, ParseState, string, string> Apply { get; }

            public OptionSpec(string[] names, string valueName, string description, Action<GameOptions, ParseState, string, string> apply)
            {
                Names = names;
                ValueName = valueName;
                Description = description;
                Apply = apply;
            }
        }

        private static readonly OptionSpec[] Specs =
        {
            new OptionSpec(new[] { "--test", "-test" }, null,
                "Skip the home screen and start a game straight away (for development and quick testing).",
                (options, state, flag, value) => options.Test = true),
            new OptionSpec(new[] { "--two-player" }, null,
                "Two human players, with the whole arena in view. Preselects two players on the home screen. Cannot be combined with --demo.",
                (options, state, flag, value) => SetSetup(options, state, MatchSetup.TwoPlayer, flag)),
            new OptionSpec(new[] { "--demo" }, null,
                "Two computers play each other, with the whole arena in view; matches restart by themselves. Cannot be combined with --two-player.",
                (options, state, flag, value) => SetSetup(options, state, MatchSetup.Demo, flag)),
            new OptionSpec(new[] { "--mute" }, null,
                "Start with all sound off.",
                (options, state, flag, value) => options.Mute = true),
            new OptionSpec(new[] { "--seed" }, "<integer>",
                "Fix the run's random draws so it can be reproduced. Without it a seed is chosen at random and shown in the debug overlay (F5).",
                (options, state, flag, value) =>
                {
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
                        throw new ArgumentException("--seed requires an integer value.");
                    options.Seed = seed;
                }),
            new OptionSpec(new[] { "--windowed" }, null,
                "Run in a window instead of fullscreen.",
                (options, state, flag, value) => options.Windowed = true),
            new OptionSpec(new[] { "--scale" }, $"<{MinimumScale}-{MaximumScale}>",
                $"With --windowed, size the window as that integer multiple of 800x600 (default {DefaultScale}). Needs --windowed.",
                (options, state, flag, value) =>
                {
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale) || scale < MinimumScale || scale > MaximumScale)
                        throw new ArgumentException($"--scale requires an integer from {MinimumScale} to {MaximumScale}.");
                    options.Scale = scale;
                    state.ScaleGiven = true;
                }),
        };

        public static GameOptions Parse(string[] args)
        {
            var options = new GameOptions();
            if (args.Any(argument => HelpFlags.Contains(argument)))
            {
                options.ShowHelp = true;
                return options;
            }

            var state = new ParseState();
            for (var index = 0; index < args.Length; index++)
            {
                var flag = args[index];
                var spec = Specs.FirstOrDefault(candidate => candidate.Names.Contains(flag));
                if (spec == null)
                    throw new ArgumentException($"Unknown option '{flag}'.");
                string value = null;
                if (spec.ValueName != null)
                {
                    if (index + 1 >= args.Length)
                        throw new ArgumentException(MissingValueMessage(spec));
                    value = args[++index];
                }
                spec.Apply(options, state, flag, value);
            }
            if (state.ScaleGiven && !options.Windowed)
                throw new ArgumentException("--scale only applies together with --windowed.");
            return options;
        }

        // The error for an option that needs a value and did not get a usable one.
        private static string MissingValueMessage(OptionSpec spec) =>
            spec.Names[0] == "--scale" ? $"--scale requires an integer from {MinimumScale} to {MaximumScale}." : $"{spec.Names[0]} requires an integer value.";

        // --two-player and --demo each choose who controls the seats, so only one may be given.
        private static void SetSetup(GameOptions options, ParseState state, MatchSetup setup, string flag)
        {
            if (state.SetupFlag != null && state.SetupFlag != flag)
                throw new ArgumentException($"{state.SetupFlag} and {flag} cannot be combined.");
            state.SetupFlag = flag;
            options.Setup = setup;
        }

        public static string UsageLine =>
            "Usage: MonoTanx [--test] [--two-player | --demo] [--mute] [--seed <integer>] [--windowed [--scale <" + MinimumScale + "-" + MaximumScale + ">]] [--help]";

        // What --help prints: every option, then how conflicts and repeats are handled.
        public static string HelpText
        {
            get
            {
                var text = new StringBuilder();
                text.AppendLine("MonoTanx: a top-down tank combat game.");
                text.AppendLine();
                text.AppendLine(UsageLine);
                text.AppendLine();
                text.AppendLine("Options:");
                foreach (var spec in Specs)
                {
                    var names = string.Join(", ", spec.Names);
                    text.AppendLine("  " + (spec.ValueName == null ? names : names + " " + spec.ValueName));
                    text.AppendLine("      " + spec.Description);
                }
                text.AppendLine("  " + string.Join(", ", HelpFlags));
                text.AppendLine("      Show this help and exit.");
                text.AppendLine();
                text.AppendLine("How options combine:");
                text.AppendLine("  - The order of options does not matter.");
                text.AppendLine("  - Help always wins: if it is given, nothing else is run (even with an invalid option).");
                text.AppendLine("  - Conflicting options are rejected with a message, never silently overridden:");
                text.AppendLine("    --two-player with --demo, and --scale without --windowed.");
                text.AppendLine("  - Giving an option twice is allowed; for --seed and --scale the last value wins.");
                text.AppendLine("  - Anything not listed here is an error.");
                return text.ToString();
            }
        }
    }
}

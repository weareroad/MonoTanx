using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MonoTanx.Core
{
    // What reading a settings file produced: the settings, and a message for
    // everything in the file that was not used. The game never fails over a bad
    // file, it plays with what it could read.
    public sealed class SettingsLoad
    {
        public GameSettings Settings { get; }
        public IReadOnlyList<string> Problems { get; }

        public SettingsLoad(GameSettings settings, IReadOnlyList<string> problems)
        {
            Settings = settings;
            Problems = problems;
        }
    }

    // The settings config file: one flat JSON object keyed by setting name, holding
    // only the values that differ from the default, so it can be edited by hand.
    public static class SettingsFile
    {
        public const int CurrentVersion = 1;
        private const string VersionKey = "version";

        // Per-user application data (%AppData% on Windows, $XDG_CONFIG_HOME or ~/.config
        // elsewhere), so rebuilding or cleaning the game does not lose it.
        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonoTanx", "settings.json");

        // Reads settings from JSON text. Out-of-range values are clamped; unknown
        // keys, values that are not numbers and malformed JSON are skipped, each
        // with a message.
        public static SettingsLoad Parse(string json)
        {
            var settings = new GameSettings();
            var problems = new List<string>();

            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    problems.Add("The settings file is not a JSON object; using the defaults.");
                    return new SettingsLoad(settings, problems);
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name == VersionKey)
                    {
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var version) || version > CurrentVersion)
                            problems.Add($"The settings file is version {property.Value}, newer than this game understands ({CurrentVersion}); reading what it can.");
                        continue;
                    }

                    if (!SettingsCatalogue.TryFind(property.Name, out var definition))
                    {
                        problems.Add($"Unknown setting '{property.Name}' ignored.");
                        continue;
                    }
                    if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetSingle(out var value) || float.IsInfinity(value))
                    {
                        problems.Add($"Setting '{property.Name}' is not a number; ignored.");
                        continue;
                    }

                    var stored = settings.Set(property.Name, value);
                    if (stored != value && !(definition.IsWhole && Math.Abs(stored - value) <= 0.5f))
                        problems.Add($"Setting '{property.Name}' = {Format(value)} is outside {Format(definition.Minimum)} to {Format(definition.Maximum)}; using {Format(stored)}.");
                }
            }
            catch (JsonException exception)
            {
                problems.Add("The settings file is not valid JSON (" + exception.Message + "); using the defaults.");
                return new SettingsLoad(new GameSettings(), problems);
            }

            return new SettingsLoad(settings, problems);
        }

        // Reads the file at the path. A missing file is the defaults, with no problem.
        public static SettingsLoad Load(string path)
        {
            string json;
            try
            {
                if (!File.Exists(path))
                    return new SettingsLoad(new GameSettings(), new List<string>());
                json = File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new SettingsLoad(new GameSettings(), new List<string> { $"Could not read {path} ({exception.Message}); using the defaults." });
            }
            return Parse(json);
        }

        // The JSON for the settings that differ from the default, indented, in catalogue order.
        public static string Serialise(GameSettings settings)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber(VersionKey, CurrentVersion);
                foreach (var pair in settings.Differences())
                    writer.WriteNumber(pair.Key, Math.Round((double)pair.Value, 5));
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
        }

        // Writes the file, creating its folder. Returns false (with why) if it cannot,
        // and leaves any existing file as it was.
        public static bool TrySave(GameSettings settings, string path, out string problem)
        {
            try
            {
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, Serialise(settings));
                File.Move(temporary, path, true);
                problem = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                problem = $"Could not save the settings to {path} ({exception.Message}).";
                return false;
            }
        }

        private static string Format(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}

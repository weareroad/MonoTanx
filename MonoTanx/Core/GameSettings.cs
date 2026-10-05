using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoTanx.Core
{
    // The current value of every setting in the catalogue. Starts at the defaults
    // (from Tuning), so a fresh instance plays exactly as the game always has.
    // Values are kept within their range as they are set. A match is given a copy
    // (Clone), so it cannot change under the rules.
    public sealed class GameSettings
    {
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();

        public GameSettings()
        {
            foreach (var definition in SettingsCatalogue.All)
                values[definition.Key] = definition.Default;
        }

        public float Get(string key) => values[SettingsCatalogue.Find(key).Key];

        public int GetWhole(string key) => (int)Math.Round(Get(key));

        // Sets a value, kept within its range (and rounded if it is whole), and
        // returns the value stored. Fire distance never goes below engage
        // distance: setting one moves the other with it.
        public float Set(string key, float value)
        {
            var definition = SettingsCatalogue.Find(key);
            var stored = definition.Clamp(value);
            values[key] = stored;

            if (key == SettingKeys.AiEngageDistanceTiles && values[SettingKeys.AiFireDistanceTiles] < stored)
                values[SettingKeys.AiFireDistanceTiles] = SettingsCatalogue.Find(SettingKeys.AiFireDistanceTiles).Clamp(stored);
            else if (key == SettingKeys.AiFireDistanceTiles && values[SettingKeys.AiEngageDistanceTiles] > stored)
                values[SettingKeys.AiEngageDistanceTiles] = stored;

            return stored;
        }

        public bool IsDefault(string key) => Get(key) == SettingsCatalogue.Find(key).Default;

        public void Reset(string key) => Set(key, SettingsCatalogue.Find(key).Default);

        public void ResetAll()
        {
            foreach (var definition in SettingsCatalogue.All)
                values[definition.Key] = definition.Default;
        }

        // The settings that are not at their default, in catalogue order: what the
        // config file holds and what the run log reports.
        public IEnumerable<KeyValuePair<string, float>> Differences() =>
            SettingsCatalogue.All.Where(definition => !IsDefault(definition.Key))
                .Select(definition => new KeyValuePair<string, float>(definition.Key, values[definition.Key]));

        public GameSettings Clone()
        {
            var copy = new GameSettings();
            foreach (var pair in values)
                copy.values[pair.Key] = pair.Value;
            return copy;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoTanx.Core
{
    // The pages of the settings screen, in the order they are shown.
    public enum SettingGroup
    {
        Match,
        Tanks,
        Shells,
        Computer,
        PerSeat
    }

    // One setting that can be changed without rebuilding. The default comes from
    // Tuning (which also holds the reason for it); the range, step and label are
    // here. A whole-number setting is rounded when set.
    public sealed class SettingDefinition
    {
        public string Key { get; }
        public SettingGroup Group { get; }
        public string Label { get; }
        public bool IsWhole { get; }
        public float Minimum { get; }
        public float Maximum { get; }

        // How much one keypress changes it on the settings page.
        public float Step { get; }
        public float Default { get; }

        public SettingDefinition(string key, SettingGroup group, string label, bool isWhole, float minimum, float maximum, float step, float defaultValue)
        {
            Key = key;
            Group = group;
            Label = label;
            IsWhole = isWhole;
            Minimum = minimum;
            Maximum = maximum;
            Step = step;
            Default = defaultValue;
        }

        // The value as it will be stored: rounded if whole, then kept within the range.
        public float Clamp(float value)
        {
            if (IsWhole)
                value = (float)Math.Round(value);
            return Math.Min(Maximum, Math.Max(Minimum, value));
        }
    }

    // The keys of the settings, as written in the config file and given to --set.
    public static class SettingKeys
    {
        public const string RoundsToWin = "match.roundsToWin";
        public const string CountdownSeconds = "match.countdownSeconds";
        public const string RoundOverSeconds = "match.roundOverSeconds";
        public const string RoundTimeLimitSeconds = "match.roundTimeLimitSeconds";
        public const string MatchOverSeconds = "match.matchOverSeconds";

        public const string MaximumHealth = "tank.maximumHealth";
        public const string MaximumFuel = "tank.maximumFuel";
        public const string StartingShells = "tank.startingShells";
        public const string ForwardSpeed = "tank.forwardSpeed";
        public const string ReverseSpeed = "tank.reverseSpeed";
        public const string TurnSpeed = "tank.turnSpeed";
        public const string CollisionRadius = "tank.collisionRadius";
        public const string ForwardFuelPerSecond = "tank.forwardFuelPerSecond";
        public const string ReverseFuelMultiplier = "tank.reverseFuelMultiplier";
        public const string TurnFuelPerSecond = "tank.turnFuelPerSecond";
        public const string PickupCollectRadius = "tank.pickupCollectRadius";
        public const string DefaultFuelAmount = "tank.defaultFuelAmount";
        public const string DefaultAmmunitionAmount = "tank.defaultAmmunitionAmount";

        public const string ReloadSeconds = "shell.reloadSeconds";
        public const string MaxFlightSeconds = "shell.maxFlightSeconds";
        public const string Damage = "shell.damage";
        public const string ShellSpeed = "shell.speed";
        public const string KnockbackDistance = "shell.knockbackDistance";
        public const string MaximumHeadingDisruptionRadians = "shell.headingDisruptionRadians";
        public const string MaxReflections = "shell.maxReflections";

        public const string AiSkill = "ai.skill";
        public const string AiEngageDistanceTiles = "ai.engageDistanceTiles";
        public const string AiFireDistanceTiles = "ai.fireDistanceTiles";
        public const string AiAimToleranceRadians = "ai.aimToleranceRadians";
        public const string AiMaximumAimErrorRadians = "ai.maximumAimErrorRadians";
        public const string AiReactionDelaySeconds = "ai.reactionDelaySeconds";
        public const string AiFireCooldownSeconds = "ai.fireCooldownSeconds";
        public const string AiRetaliationSeconds = "ai.retaliationSeconds";
        public const string AiLongRangePursuitDistanceFraction = "ai.longRangePursuitDistanceFraction";
        public const string AiNeedsFuelBelowFraction = "ai.needsFuelBelowFraction";
        public const string AiNeedsAmmoBelowFraction = "ai.needsAmmoBelowFraction";
        public const string AiRelocationChoices = "ai.relocationChoices";
        public const string AiRelocationMinimumTiles = "ai.relocationMinimumTiles";
        public const string AiRelocationCloserTiles = "ai.relocationCloserTiles";
        public const string AiEvadeNoticeChanceAtSkillZero = "ai.evadeNoticeChanceAtSkillZero";
        public const string AiEvadeReactionSeconds = "ai.evadeReactionSeconds";
        public const string AiEvadeDetectionDistance = "ai.evadeDetectionDistance";
        public const string AiEvadeLookaheadSeconds = "ai.evadeLookaheadSeconds";
        public const string AiEvadeHoldSeconds = "ai.evadeHoldSeconds";
        public const string AiStuckWindowSeconds = "ai.stuckWindowSeconds";
        public const string AiStuckMinimumDistance = "ai.stuckMinimumDistance";
        public const string AiStuckRecoverySeconds = "ai.stuckRecoverySeconds";

        // Per seat: who controls a seat decides which group applies, not the seat.
        public const string SeatPrefixPlayerOne = "playerOne";
        public const string SeatPrefixPlayerTwo = "playerTwo";
        public const string SeatPrefixComputer = "computer";
        public const string SpeedSuffix = ".speedMultiplier";
        public const string FuelSuffix = ".fuelUseMultiplier";
        public const string ReloadSuffix = ".reloadMultiplier";
    }

    // Every setting that is surfaced, in page order. The settings page, the config
    // file, --set and the tests all read this one list, so they cannot drift apart.
    // Ranges keep the game playable and the rules safe (see docs/settings-spec.md).
    public static class SettingsCatalogue
    {
        public static IReadOnlyList<SettingDefinition> All { get; } = Build();

        private static readonly Dictionary<string, SettingDefinition> ByKey = All.ToDictionary(definition => definition.Key);

        public static bool TryFind(string key, out SettingDefinition definition) => ByKey.TryGetValue(key ?? "", out definition);

        public static SettingDefinition Find(string key) =>
            TryFind(key, out var definition) ? definition : throw new ArgumentException($"Unknown setting '{key}'.");

        public static IEnumerable<SettingDefinition> InGroup(SettingGroup group) => All.Where(definition => definition.Group == group);

        public static string TitleOf(SettingGroup group) =>
            group == SettingGroup.PerSeat ? "Per seat" : group == SettingGroup.Computer ? "CPU" : group.ToString();

        private static List<SettingDefinition> Build()
        {
            var list = new List<SettingDefinition>();

            void Number(SettingGroup group, string key, string label, float minimum, float maximum, float step, float defaultValue) =>
                list.Add(new SettingDefinition(key, group, label, false, minimum, maximum, step, defaultValue));

            void Whole(SettingGroup group, string key, string label, int minimum, int maximum, int step, int defaultValue) =>
                list.Add(new SettingDefinition(key, group, label, true, minimum, maximum, step, defaultValue));

            const SettingGroup match = SettingGroup.Match;
            Whole(match, SettingKeys.RoundsToWin, "Rounds to win", 1, 9, 1, Tuning.Match.RoundsToWin);
            Number(match, SettingKeys.CountdownSeconds, "Countdown (s)", 0.0f, 10.0f, 0.5f, Tuning.Match.CountdownSeconds);
            Number(match, SettingKeys.RoundOverSeconds, "Round over pause (s)", 0.0f, 10.0f, 0.5f, Tuning.Match.RoundOverSeconds);
            Number(match, SettingKeys.RoundTimeLimitSeconds, "Round time limit (s)", 10.0f, 600.0f, 5.0f, Tuning.Match.RoundTimeLimitSeconds);
            Number(match, SettingKeys.MatchOverSeconds, "Match over pause (s)", 1.0f, 30.0f, 1.0f, Tuning.Match.MatchOverSeconds);

            // Speeds are capped (240 is under three times the default) until
            // tunnelling protection exists (#49); the radius cannot exceed what
            // fits a one-tile corridor.
            const SettingGroup tanks = SettingGroup.Tanks;
            Whole(tanks, SettingKeys.MaximumHealth, "Armour", 10, 1000, 10, Tuning.Tank.MaximumHealth);
            Number(tanks, SettingKeys.MaximumFuel, "Fuel", 20.0f, 1000.0f, 10.0f, Tuning.Tank.MaximumFuel);
            Whole(tanks, SettingKeys.StartingShells, "Starting shells", 1, 200, 1, Tuning.Tank.StartingShells);
            Number(tanks, SettingKeys.ForwardSpeed, "Forward speed (px/s)", 20.0f, 240.0f, 5.0f, Tuning.Tank.ForwardSpeed);
            Number(tanks, SettingKeys.ReverseSpeed, "Reverse speed (px/s)", 10.0f, 120.0f, 5.0f, Tuning.Tank.ReverseSpeed);
            Number(tanks, SettingKeys.TurnSpeed, "Turn speed (rad/s)", 0.5f, 6.0f, 0.1f, Tuning.Tank.TurnSpeed);
            Number(tanks, SettingKeys.CollisionRadius, "Collision radius (px)", 3.0f, 7.0f, 0.5f, Tuning.Tank.CollisionRadius);
            Number(tanks, SettingKeys.ForwardFuelPerSecond, "Forward fuel use (/s)", 0.0f, 20.0f, 0.5f, Tuning.Tank.ForwardFuelPerSecond);
            Number(tanks, SettingKeys.ReverseFuelMultiplier, "Reverse fuel multiplier", 0.0f, 8.0f, 0.5f, Tuning.Tank.ReverseFuelMultiplier);
            Number(tanks, SettingKeys.TurnFuelPerSecond, "Turn fuel use (/s)", 0.0f, 5.0f, 0.05f, Tuning.Tank.TurnFuelPerSecond);
            Number(tanks, SettingKeys.PickupCollectRadius, "Pickup radius (px)", 6.0f, 32.0f, 1.0f, Tuning.Pickups.CollectRadius);
            Whole(tanks, SettingKeys.DefaultFuelAmount, "Fuel pickup amount", 5, 200, 5, Tuning.Pickups.DefaultFuelAmount);
            Whole(tanks, SettingKeys.DefaultAmmunitionAmount, "Shell pickup amount", 1, 50, 1, Tuning.Pickups.DefaultAmmunitionAmount);

            const SettingGroup shells = SettingGroup.Shells;
            Number(shells, SettingKeys.ReloadSeconds, "Reload time (s)", 0.2f, 10.0f, 0.1f, Tuning.StandardShell.ReloadSeconds);
            Number(shells, SettingKeys.MaxFlightSeconds, "Flight time (s)", 1.0f, 10.0f, 0.5f, Tuning.StandardShell.MaxFlightSeconds);
            Whole(shells, SettingKeys.Damage, "Armour lost per hit", 1, 100, 1, Tuning.StandardShell.Damage);
            Number(shells, SettingKeys.ShellSpeed, "Shell speed (px/s)", 100.0f, 500.0f, 10.0f, Tuning.StandardShell.Speed);
            Number(shells, SettingKeys.KnockbackDistance, "Knockback (px)", 0.0f, 8.0f, 0.5f, Tuning.Damage.KnockbackDistance);
            Number(shells, SettingKeys.MaximumHeadingDisruptionRadians, "Heading disruption (rad)", 0.0f, 0.6f, 0.02f, Tuning.Damage.MaximumHeadingDisruptionRadians);
            Whole(shells, SettingKeys.MaxReflections, "Max reflections", 0, 20, 1, Tuning.Projectile.MaxReflections);

            // The aim tolerance cannot go below 0.07: the computer turns up to
            // 0.04 rad a step, and a tighter window is missed over and over.
            const SettingGroup computer = SettingGroup.Computer;
            Number(computer, SettingKeys.AiSkill, "Skill", 0.0f, 1.0f, 0.05f, Tuning.Ai.Skill);
            Whole(computer, SettingKeys.AiEngageDistanceTiles, "Engage distance (tiles)", 2, 12, 1, Tuning.Ai.EngageDistanceTiles);
            Whole(computer, SettingKeys.AiFireDistanceTiles, "Fire distance (tiles)", 2, 16, 1, Tuning.Ai.FireDistanceTiles);
            Number(computer, SettingKeys.AiAimToleranceRadians, "Aim tolerance (rad)", 0.07f, 0.5f, 0.01f, Tuning.Ai.AimToleranceRadians);
            Number(computer, SettingKeys.AiMaximumAimErrorRadians, "Extra aim error (rad)", 0.0f, 1.0f, 0.05f, Tuning.Ai.MaximumAimErrorRadians);
            Number(computer, SettingKeys.AiReactionDelaySeconds, "Reaction delay (s)", 0.0f, 2.0f, 0.05f, Tuning.Ai.ReactionDelaySeconds);
            Number(computer, SettingKeys.AiFireCooldownSeconds, "Fire cooldown (s)", 0.5f, 10.0f, 0.25f, Tuning.Ai.FireCooldownSeconds);
            Number(computer, SettingKeys.AiRetaliationSeconds, "Retaliation (s)", 0.0f, 5.0f, 0.25f, Tuning.Ai.RetaliationSeconds);
            Number(computer, SettingKeys.AiLongRangePursuitDistanceFraction, "Long range pursuit", 0.1f, 1.0f, 0.05f, Tuning.Ai.LongRangePursuitDistanceFraction);
            Number(computer, SettingKeys.AiNeedsFuelBelowFraction, "Seeks fuel below", 0.0f, 1.0f, 0.05f, Tuning.Ai.NeedsFuelBelowFraction);
            Number(computer, SettingKeys.AiNeedsAmmoBelowFraction, "Seeks shells below", 0.0f, 1.0f, 0.05f, Tuning.Ai.NeedsAmmoBelowFraction);
            Whole(computer, SettingKeys.AiRelocationChoices, "Relocation choices", 1, 8, 1, Tuning.Ai.RelocationChoices);
            Whole(computer, SettingKeys.AiRelocationMinimumTiles, "Relocation min (tiles)", 1, 5, 1, Tuning.Ai.RelocationMinimumTiles);
            Whole(computer, SettingKeys.AiRelocationCloserTiles, "Relocation closer (tiles)", 0, 3, 1, Tuning.Ai.RelocationCloserTiles);
            Number(computer, SettingKeys.AiEvadeNoticeChanceAtSkillZero, "Evade notice at skill 0", 0.0f, 1.0f, 0.05f, Tuning.Ai.EvadeNoticeChanceAtSkillZero);
            Number(computer, SettingKeys.AiEvadeReactionSeconds, "Evade reaction (s)", 0.0f, 1.0f, 0.02f, Tuning.Ai.EvadeReactionSeconds);
            Number(computer, SettingKeys.AiEvadeDetectionDistance, "Evade detection (px)", 0.0f, 480.0f, 10.0f, Tuning.Ai.EvadeDetectionDistance);
            Number(computer, SettingKeys.AiEvadeLookaheadSeconds, "Evade lookahead (s)", 0.2f, 3.0f, 0.1f, Tuning.Ai.EvadeLookaheadSeconds);
            Number(computer, SettingKeys.AiEvadeHoldSeconds, "Evade hold (s)", 0.1f, 1.5f, 0.05f, Tuning.Ai.EvadeHoldSeconds);
            Number(computer, SettingKeys.AiStuckWindowSeconds, "Stuck window (s)", 0.5f, 5.0f, 0.25f, Tuning.Ai.StuckWindowSeconds);
            Number(computer, SettingKeys.AiStuckMinimumDistance, "Stuck distance (px)", 1.0f, 30.0f, 1.0f, Tuning.Ai.StuckMinimumDistance);
            Number(computer, SettingKeys.AiStuckRecoverySeconds, "Stuck recovery (s)", 0.2f, 3.0f, 0.1f, Tuning.Ai.StuckRecoverySeconds);

            // Multipliers on the tank values above, for the controller of a seat.
            // 1 leaves the value as it is, so the pacing numbers hold at the defaults.
            foreach (var (prefix, name) in new[]
            {
                (SettingKeys.SeatPrefixPlayerOne, "P1"),
                (SettingKeys.SeatPrefixPlayerTwo, "P2"),
                (SettingKeys.SeatPrefixComputer, "CPU"),
            })
            {
                Number(SettingGroup.PerSeat, prefix + SettingKeys.SpeedSuffix, name + " speed x", 0.25f, 2.0f, 0.05f, 1.0f);
                Number(SettingGroup.PerSeat, prefix + SettingKeys.FuelSuffix, name + " fuel use x", 0.0f, 4.0f, 0.05f, 1.0f);
                Number(SettingGroup.PerSeat, prefix + SettingKeys.ReloadSuffix, name + " reload x", 0.25f, 4.0f, 0.05f, 1.0f);
            }

            return list;
        }
    }
}

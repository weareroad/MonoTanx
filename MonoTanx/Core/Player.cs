using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    public sealed class Player
    {
        public static readonly Ammunition DefaultAmmunition = new Ammunition("standard-shell", "A basic shell for testing tank combat.", "placeholder-shell", "placeholder-shell-fire",
            Tuning.StandardShell.ReloadSeconds, Tuning.StandardShell.MaxFlightSeconds, Tuning.StandardShell.Speed, Tuning.StandardShell.Damage);

        // The standard shell with the settings' values (which are the same as DefaultAmmunition's at the defaults).
        public static Ammunition StandardAmmunition(GameSettings settings) => new Ammunition("standard-shell", "A basic shell for testing tank combat.", "placeholder-shell", "placeholder-shell-fire",
            settings.Get(SettingKeys.ReloadSeconds), settings.Get(SettingKeys.MaxFlightSeconds), settings.Get(SettingKeys.ShellSpeed), settings.GetWhole(SettingKeys.Damage));

        // Which of the three per-seat multiplier groups applies to a tank is decided by who
        // controls it now, not by the seat: a computer in either seat uses the computer group.
        private const int SpeedIndex = 0;
        private const int FuelIndex = 1;
        private const int ReloadIndex = 2;

        private readonly GameSettings settings;
        private readonly Seat seat;
        private readonly float baseMovementSpeed;
        private readonly float baseReverseSpeed;
        private readonly float baseTurnSpeed;
        private readonly float baseForwardFuel;
        private readonly float baseTurnFuel;
        private readonly float reverseFuelMultiplier;
        private readonly float[] humanOneMultipliers;
        private readonly float[] humanTwoMultipliers;
        private readonly float[] computerMultipliers;

        public string Name { get; }
        public string SpriteAsset { get; }
        public bool IsComputerControlled { get; set; }
        public Color Tint { get; }
        public float MaximumFuel { get; }
        public int MaximumHealth { get; }
        public float MovementSpeed => baseMovementSpeed * Multipliers[SpeedIndex];
        public float ReverseMovementSpeed => baseReverseSpeed * Multipliers[SpeedIndex];
        public float TurnSpeed => baseTurnSpeed * Multipliers[SpeedIndex];
        public float CollisionRadius { get; }
        public int PreferredCombatDistanceTiles { get; set; }
        public float LongRangePursuitDistanceFraction { get; set; }
        public float ComputerAimToleranceRadians { get; set; }
        public float ComputerSkill { get; set; }
        public float ComputerReactionDelaySeconds { get; set; }
        public float ComputerFireCooldownSeconds { get; set; }
        public float ForwardFuelPerSecond => baseForwardFuel * Multipliers[FuelIndex];
        public float ReverseFuelPerSecond => ForwardFuelPerSecond * reverseFuelMultiplier;
        public float TurnFuelPerSecond => baseTurnFuel * Multipliers[FuelIndex];

        // How much longer (or shorter) than the ammunition says this tank takes to reload.
        public float ReloadMultiplier => Multipliers[ReloadIndex];

        private float[] Multipliers => IsComputerControlled ? computerMultipliers : seat == Seat.One ? humanOneMultipliers : humanTwoMultipliers;
        public Texture2D Texture { get; set; }
        public Vector2 Position;
        public float Heading = -MathHelper.PiOver2;
        public float Fuel;
        public int Health;
        public float ReloadTimer;
        public float AnimationTimer;
        public int Frame;
        public readonly List<AmmunitionSlot> AmmunitionSlots = new List<AmmunitionSlot>(2);

        public Player(string name, string spriteAsset, Color tint, Ammunition firstAmmunition, int firstQuantity,
            Ammunition secondAmmunition = null, int secondQuantity = 0, bool isComputerControlled = false,
            GameSettings settings = null, Seat seat = Seat.One)
        {
            this.settings = settings ?? new GameSettings();
            this.seat = seat;
            settings = this.settings;
            Name = name;
            SpriteAsset = spriteAsset;
            Tint = tint;
            IsComputerControlled = isComputerControlled;
            MaximumFuel = settings.Get(SettingKeys.MaximumFuel);
            MaximumHealth = settings.GetWhole(SettingKeys.MaximumHealth);
            baseMovementSpeed = settings.Get(SettingKeys.ForwardSpeed);
            baseReverseSpeed = settings.Get(SettingKeys.ReverseSpeed);
            baseTurnSpeed = settings.Get(SettingKeys.TurnSpeed);
            CollisionRadius = settings.Get(SettingKeys.CollisionRadius);
            PreferredCombatDistanceTiles = settings.GetWhole(SettingKeys.AiEngageDistanceTiles);
            LongRangePursuitDistanceFraction = settings.Get(SettingKeys.AiLongRangePursuitDistanceFraction);
            ComputerAimToleranceRadians = settings.Get(SettingKeys.AiAimToleranceRadians);
            ComputerSkill = settings.Get(SettingKeys.AiSkill);
            ComputerReactionDelaySeconds = settings.Get(SettingKeys.AiReactionDelaySeconds);
            ComputerFireCooldownSeconds = settings.Get(SettingKeys.AiFireCooldownSeconds);
            baseForwardFuel = settings.Get(SettingKeys.ForwardFuelPerSecond);
            baseTurnFuel = settings.Get(SettingKeys.TurnFuelPerSecond);
            reverseFuelMultiplier = settings.Get(SettingKeys.ReverseFuelMultiplier);
            humanOneMultipliers = MultipliersOf(settings, SettingKeys.SeatPrefixPlayerOne);
            humanTwoMultipliers = MultipliersOf(settings, SettingKeys.SeatPrefixPlayerTwo);
            computerMultipliers = MultipliersOf(settings, SettingKeys.SeatPrefixComputer);
            AmmunitionSlots.Add(new AmmunitionSlot(firstAmmunition, firstQuantity));
            if (secondAmmunition != null)
                AmmunitionSlots.Add(new AmmunitionSlot(secondAmmunition, secondQuantity));
            ResetResources();
        }

        private static float[] MultipliersOf(GameSettings settings, string prefix) => new[]
        {
            settings.Get(prefix + SettingKeys.SpeedSuffix),
            settings.Get(prefix + SettingKeys.FuelSuffix),
            settings.Get(prefix + SettingKeys.ReloadSuffix)
        };

        // A copy of this tank's rules state (where it is, how it is heading, its fuel and its
        // speeds) to try movements on, for a computer working out where it could go. Not for play.
        public Player CopyForPrediction() => new Player(this);

        private Player(Player other)
        {
            Name = other.Name;
            SpriteAsset = other.SpriteAsset;
            Tint = other.Tint;
            IsComputerControlled = other.IsComputerControlled;
            MaximumFuel = other.MaximumFuel;
            MaximumHealth = other.MaximumHealth;
            settings = other.settings;
            seat = other.seat;
            baseMovementSpeed = other.baseMovementSpeed;
            baseReverseSpeed = other.baseReverseSpeed;
            baseTurnSpeed = other.baseTurnSpeed;
            baseForwardFuel = other.baseForwardFuel;
            baseTurnFuel = other.baseTurnFuel;
            reverseFuelMultiplier = other.reverseFuelMultiplier;
            humanOneMultipliers = other.humanOneMultipliers;
            humanTwoMultipliers = other.humanTwoMultipliers;
            computerMultipliers = other.computerMultipliers;
            CollisionRadius = other.CollisionRadius;
            Position = other.Position;
            Heading = other.Heading;
            Fuel = other.Fuel;
            Health = other.Health;
        }

        public void ResetResources()
        {
            Fuel = MaximumFuel;
            Health = MaximumHealth;
            ResetFuelAndAmmunition();
        }

        public void ResetFuelAndAmmunition()
        {
            Fuel = MaximumFuel;
            ReloadTimer = 0.0f;
            foreach (var slot in AmmunitionSlots)
                slot.Reset();
        }

        // Counts the reload down. Returns true on the update the reload finishes
        // and the tank has ammunition to fire again (the moment to play the
        // "ready" sound); false otherwise, including when it is out of shells.
        public bool TickReload(float elapsed)
        {
            var wasReloading = ReloadTimer > 0.0f;
            ReloadTimer = MathHelper.Max(0.0f, ReloadTimer - elapsed);
            return wasReloading && ReloadTimer <= 0.0f && RemainingAmmunition > 0;
        }

        // Fires along the current heading if the reload has finished and
        // ammunition remains. The caller supplies the muzzle offset from the
        // tank centre (which depends on its sprite); the shell speed comes from
        // the ammunition.
        public bool TryFire(float muzzleOffset, out ShellLaunch launch)
        {
            if (ReloadTimer > 0.0f || !TryConsumeAmmunition(out var ammunition))
            {
                launch = default;
                return false;
            }

            var direction = new Vector2((float)System.Math.Cos(Heading), (float)System.Math.Sin(Heading));
            launch = new ShellLaunch(ammunition, Position + direction * muzzleOffset, direction * ammunition.Speed);
            ReloadTimer = ammunition.ReloadTimeSeconds * ReloadMultiplier;
            return true;
        }

        public bool TryConsumeAmmunition(out Ammunition ammunition)
        {
            foreach (var slot in AmmunitionSlots)
            {
                if (slot.Remaining <= 0)
                    continue;
                slot.Remaining--;
                ammunition = slot.Ammunition;
                return true;
            }

            ammunition = null;
            return false;
        }

        public int RemainingAmmunition
        {
            get
            {
                var total = 0;
                foreach (var slot in AmmunitionSlots) total += slot.Remaining;
                return total;
            }
        }

        public int StartingAmmunition
        {
            get
            {
                var total = 0;
                foreach (var slot in AmmunitionSlots) total += slot.StartingQuantity;
                return total;
            }
        }
    }

    public sealed class AmmunitionSlot
    {
        public Ammunition Ammunition { get; }
        public int StartingQuantity { get; }
        public int Remaining { get; set; }

        public AmmunitionSlot(Ammunition ammunition, int quantity)
        {
            Ammunition = ammunition;
            StartingQuantity = quantity;
            Remaining = quantity;
        }

        public void Reset() => Remaining = StartingQuantity;
    }
}

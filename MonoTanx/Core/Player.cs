using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    public sealed class Player
    {
        public static readonly Ammunition DefaultAmmunition = new Ammunition("standard-shell", "A basic shell for testing tank combat.", "placeholder-shell", "placeholder-shell-fire", 3.0f, 5.0f, 12);
        public const float DefaultMaximumFuel = 200.0f;
        public const int DefaultMaximumHealth = 100;
        public const float DefaultForwardMovementSpeed = 90.0f;
        public const float DefaultReverseMovementSpeed = 45.0f;
        public const int DefaultPreferredCombatDistanceTiles = 6;
        public const float DefaultLongRangePursuitDistanceFraction = 0.5f;
        public const float DefaultComputerAimToleranceRadians = 0.2f;
        public const float DefaultComputerReactionDelaySeconds = 0.25f;
        public const float DefaultComputerFireCooldownSeconds = 3.0f;
        public const float DefaultTurnSpeed = 2.5f;
        public const float DefaultCollisionRadius = 6.0f;
        public const int DefaultStartingShells = 20;
        public const float DefaultForwardFuelPerSecond = 4.0f;
        public const float DefaultReverseFuelMultiplier = 2.0f;

        public string Name { get; }
        public string SpriteAsset { get; }
        public bool IsComputerControlled { get; set; }
        public Color Tint { get; }
        public float MaximumFuel { get; }
        public int MaximumHealth { get; }
        public float MovementSpeed { get; }
        public float ReverseMovementSpeed { get; }
        public float TurnSpeed { get; }
        public float CollisionRadius { get; }
        public int PreferredCombatDistanceTiles { get; set; }
        public float LongRangePursuitDistanceFraction { get; set; }
        public float ComputerAimToleranceRadians { get; set; }
        public float ComputerReactionDelaySeconds { get; set; }
        public float ComputerFireCooldownSeconds { get; set; }
        public float ForwardFuelPerSecond { get; }
        public float ReverseFuelPerSecond => ForwardFuelPerSecond * DefaultReverseFuelMultiplier;
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
            Ammunition secondAmmunition = null, int secondQuantity = 0, bool isComputerControlled = false)
        {
            Name = name;
            SpriteAsset = spriteAsset;
            Tint = tint;
            IsComputerControlled = isComputerControlled;
            MaximumFuel = DefaultMaximumFuel;
            MaximumHealth = DefaultMaximumHealth;
            MovementSpeed = DefaultForwardMovementSpeed;
            ReverseMovementSpeed = DefaultReverseMovementSpeed;
            TurnSpeed = DefaultTurnSpeed;
            CollisionRadius = DefaultCollisionRadius;
            PreferredCombatDistanceTiles = DefaultPreferredCombatDistanceTiles;
            LongRangePursuitDistanceFraction = DefaultLongRangePursuitDistanceFraction;
            ComputerAimToleranceRadians = DefaultComputerAimToleranceRadians;
            ComputerReactionDelaySeconds = DefaultComputerReactionDelaySeconds;
            ComputerFireCooldownSeconds = DefaultComputerFireCooldownSeconds;
            ForwardFuelPerSecond = DefaultForwardFuelPerSecond;
            AmmunitionSlots.Add(new AmmunitionSlot(firstAmmunition, firstQuantity));
            if (secondAmmunition != null)
                AmmunitionSlots.Add(new AmmunitionSlot(secondAmmunition, secondQuantity));
            ResetResources();
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

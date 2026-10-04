using Microsoft.Xna.Framework;

namespace MonoTanx.Core
{
    public static class PickupRules
    {
        public static bool InRange(Player player, PickupSpawn spawn)
        {
            return Vector2.DistanceSquared(player.Position, spawn.Position) <= Tuning.Pickups.CollectRadius * Tuning.Pickups.CollectRadius;
        }

        // Fuel is clamped to the tank's maximum. Ammunition goes to the matching
        // slot (or the first slot) and is not capped.
        public static void Apply(Player player, PickupSpawn spawn)
        {
            if (spawn.Kind == PickupKind.Fuel)
            {
                player.Fuel = MathHelper.Min(player.MaximumFuel, player.Fuel + spawn.Amount);
                return;
            }

            var slot = player.AmmunitionSlots.Find(x => string.IsNullOrWhiteSpace(spawn.AmmunitionId) || x.Ammunition.Id == spawn.AmmunitionId)
                ?? player.AmmunitionSlots[0];
            slot.Remaining += spawn.Amount;
        }
    }
}

using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;
using static MonoTanx.Tests.TestSupport;

namespace MonoTanx.Tests;

public class PickupRulesTests
{
    private static readonly Ammunition Small = new Ammunition("small", "", "s", "s-fire", 1.0f, 2.0f, 100.0f, 1);
    private static readonly Ammunition Large = new Ammunition("large", "", "l", "l-fire", 4.0f, 6.0f, 100.0f, 9);

    private static PickupSpawn Fuel(int amount) => new PickupSpawn(1, PickupKind.Fuel, Vector2.Zero, amount, null, "Sprites/fueldrop_1");
    private static PickupSpawn Ammo(int amount, string ammunitionId) => new PickupSpawn(2, PickupKind.Ammunition, Vector2.Zero, amount, ammunitionId, "Sprites/ammodrop_1");
    private static Player TwoSlotPlayer() => new Player("Two", "Sprites/tank", Color.White, Small, 2, Large, 3);

    [Fact]
    public void PickupIsInRangeUpToTheCollectRadius()
    {
        var player = NewPlayer(new Vector2(Tuning.Pickups.CollectRadius, 0.0f));
        Assert.True(PickupRules.InRange(player, Fuel(10)));

        player.Position = new Vector2(Tuning.Pickups.CollectRadius + 0.01f, 0.0f);
        Assert.False(PickupRules.InRange(player, Fuel(10)));
    }

    [Fact]
    public void FuelPickupAddsFuel()
    {
        var player = NewPlayer(Vector2.Zero);
        player.Fuel = 100.0f;

        PickupRules.Apply(player, Fuel(50));

        Assert.Equal(150.0f, player.Fuel);
    }

    [Fact]
    public void FuelPickupIsClampedToTheMaximum()
    {
        var player = NewPlayer(Vector2.Zero);
        player.Fuel = player.MaximumFuel - 10.0f;

        PickupRules.Apply(player, Fuel(50));

        Assert.Equal(player.MaximumFuel, player.Fuel);
    }

    [Fact]
    public void AmmunitionGoesToTheSlotWithTheMatchingId()
    {
        var player = TwoSlotPlayer();

        PickupRules.Apply(player, Ammo(4, "large"));

        Assert.Equal(2, player.AmmunitionSlots[0].Remaining);
        Assert.Equal(7, player.AmmunitionSlots[1].Remaining);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-such-ammunition")]
    public void AmmunitionWithNoOrUnknownIdGoesToTheFirstSlot(string ammunitionId)
    {
        var player = TwoSlotPlayer();

        PickupRules.Apply(player, Ammo(4, ammunitionId));

        Assert.Equal(6, player.AmmunitionSlots[0].Remaining);
        Assert.Equal(3, player.AmmunitionSlots[1].Remaining);
    }

    [Fact]
    public void AmmunitionIsNotCappedAtTheStartingQuantity()
    {
        var player = TwoSlotPlayer();

        PickupRules.Apply(player, Ammo(100, "small"));

        Assert.Equal(102, player.AmmunitionSlots[0].Remaining);
        Assert.True(player.RemainingAmmunition > player.StartingAmmunition);
    }
}

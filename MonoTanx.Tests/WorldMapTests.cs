using Microsoft.Xna.Framework;
using MonoTanx.Core;
using Xunit;

namespace MonoTanx.Tests;

public class WorldMapTests
{
    // Centre of a tile in the 8x4 fixture map.
    private static Vector2 Centre(int x, int y) => new Vector2(x * 16 + 8, y * 16 + 8);

    private static WorldMap LoadFixture() =>
        new WorldMap(Path.Combine(AppContext.BaseDirectory, "Fixtures", "terrain.tmx"));

    [Fact]
    public void ReportsBoundsAndTileSize()
    {
        var map = LoadFixture();

        Assert.Equal(new Rectangle(0, 0, 128, 64), map.Bounds);
        Assert.Equal(16, map.TileWidth);
        Assert.Equal(16, map.TileHeight);
    }

    [Fact]
    public void ConvertsBetweenWorldAndTileCoordinates()
    {
        var map = LoadFixture();

        Assert.Equal(new Point(0, 0), map.WorldToTile(new Vector2(0, 0)));
        Assert.Equal(new Point(1, 2), map.WorldToTile(new Vector2(31.9f, 32.0f)));
        Assert.Equal(new Point(-1, -1), map.WorldToTile(new Vector2(-0.1f, -0.1f)));
        Assert.Equal(new Rectangle(32, 16, 16, 16), map.GetTileBounds(new Point(2, 1)));
        Assert.True(map.IsInside(new Point(7, 3)));
        Assert.False(map.IsInside(new Point(8, 3)));
        Assert.False(map.IsInside(new Point(0, -1)));
    }

    [Theory]
    [InlineData(0, TerrainKind.Ground)]
    [InlineData(1, TerrainKind.Water)]
    [InlineData(2, TerrainKind.Bridge)]
    [InlineData(3, TerrainKind.Wall)]
    [InlineData(4, TerrainKind.Ravine)]
    [InlineData(5, TerrainKind.Hill)]
    [InlineData(6, TerrainKind.Reflective)]
    [InlineData(7, TerrainKind.Ground)]
    public void ReadsTerrainKindFromTilesetProperties(int column, TerrainKind expected)
    {
        var map = LoadFixture();

        Assert.Equal(expected, map.GetTerrainAt(Centre(column, 1)));
    }

    [Fact]
    public void UndefinedAndEmptyTilesAreGround()
    {
        var map = LoadFixture();

        Assert.Equal(TerrainKind.Ground, map.GetTerrainAt(Centre(0, 2))); // gid with no definition
        Assert.Equal(TerrainKind.Ground, map.GetTerrainAt(Centre(1, 2))); // gid 0
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(8, 0)]
    [InlineData(0, 4)]
    public void OutsideTheMapIsOutOfBounds(int tileX, int tileY)
    {
        var map = LoadFixture();

        Assert.Equal(TerrainKind.OutOfBounds, map.GetTerrainAt(new Point(tileX, tileY)));
        Assert.True(map.BlocksMovement(Centre(tileX, tileY)));
        Assert.True(map.BlocksProjectiles(Centre(tileX, tileY)));
        Assert.True(map.BlocksVision(Centre(tileX, tileY)));
    }

    [Theory]
    [InlineData(0, false, false, false)] // ground
    [InlineData(1, true, false, false)]  // water
    [InlineData(2, false, false, false)] // bridge
    [InlineData(3, true, true, true)]    // wall
    [InlineData(4, true, false, false)]  // ravine
    [InlineData(5, true, true, true)]    // hill
    [InlineData(6, true, false, true)]   // reflective
    public void TerrainBlocksMovementProjectilesAndVisionIndependently(int column, bool movement, bool projectiles, bool vision)
    {
        var map = LoadFixture();
        var position = Centre(column, 1);

        Assert.Equal(movement, map.BlocksMovement(position));
        Assert.Equal(projectiles, map.BlocksProjectiles(position));
        Assert.Equal(vision, map.BlocksVision(position));
    }

    [Fact]
    public void OnlyReflectiveTerrainReflectsProjectiles()
    {
        var map = LoadFixture();

        Assert.True(map.ReflectsProjectiles(Centre(6, 1)));
        Assert.False(map.ReflectsProjectiles(Centre(3, 1)));
        Assert.False(map.ReflectsProjectiles(Centre(0, 0)));
    }

    [Fact]
    public void TerrainMultipliersDefaultToOneAndCanBeOverridden()
    {
        var map = LoadFixture();

        Assert.Equal(1.0f, map.GetMovementSpeedMultiplier(Centre(0, 0)));
        Assert.Equal(1.0f, map.GetFuelCostMultiplier(Centre(0, 0)));
        Assert.Equal(0.5f, map.GetMovementSpeedMultiplier(Centre(7, 1)));
        Assert.Equal(2.0f, map.GetFuelCostMultiplier(Centre(7, 1)));
    }

    [Fact]
    public void CircleCanOccupyOpenGround()
    {
        var map = LoadFixture();

        Assert.True(map.CanOccupyCircle(Centre(3, 0), 6.0f));
    }

    [Fact]
    public void CircleOverlappingABlockingTileCannotBeOccupied()
    {
        var map = LoadFixture();
        var wallTopEdge = 16.0f; // wall tile at (3, 1) spans y 16..32
        var wallCentreX = 3 * 16 + 8;

        Assert.False(map.CanOccupyCircle(new Vector2(wallCentreX, wallTopEdge - 5.0f), 6.0f));
        Assert.True(map.CanOccupyCircle(new Vector2(wallCentreX, wallTopEdge - 7.0f), 6.0f));
        Assert.False(map.CanOccupyCircle(Centre(3, 1), 1.0f));
    }

    [Fact]
    public void CircleCannotLeaveTheMap()
    {
        var map = LoadFixture();

        Assert.False(map.CanOccupyCircle(new Vector2(3.0f, 8.0f), 6.0f));
        Assert.False(map.CanOccupyCircle(new Vector2(125.0f, 8.0f), 6.0f));
    }

    [Fact]
    public void BridgesAreDrivableButWaterIsNot()
    {
        var map = LoadFixture();

        Assert.True(map.CanOccupyCircle(Centre(2, 1), 4.0f));
        Assert.False(map.CanOccupyCircle(Centre(1, 1), 4.0f));
    }

    [Fact]
    public void LineOfSightIsClearAcrossOpenGround()
    {
        var map = LoadFixture();

        Assert.True(map.HasLineOfSight(Centre(0, 0), Centre(7, 0)));
        Assert.True(map.HasLineOfSight(Centre(0, 0), Centre(0, 0)));
    }

    [Fact]
    public void LineOfSightIsBlockedByWallsHillsAndReflectiveTerrain()
    {
        var map = LoadFixture();

        Assert.False(map.HasLineOfSight(Centre(2, 1), Centre(4, 1))); // through the wall at (3, 1)
        Assert.False(map.HasLineOfSight(Centre(4, 1), Centre(6, 1))); // through the hill at (5, 1)
        Assert.False(map.HasLineOfSight(Centre(6, 0), Centre(6, 2))); // through the reflective tile at (6, 1)
    }

    [Fact]
    public void LineOfSightPassesOverWaterBridgesAndRavines()
    {
        var map = LoadFixture();

        Assert.True(map.HasLineOfSight(Centre(0, 1), Centre(2, 1)));                     // over water at (1, 1)
        Assert.True(map.HasLineOfSight(new Vector2(66.0f, 24.0f), new Vector2(78.0f, 24.0f))); // within the ravine at (4, 1)
    }

    [Fact]
    public void ReflectiveRunsDecideWhichAxisFlips()
    {
        var map = LoadFixture();
        var diagonal = new Vector2(1.0f, 1.0f);

        // Horizontal run on row 3 (tiles 2..4): a hit in the middle flips Y.
        Assert.True(map.ReflectiveSurfaceIsHorizontal(Centre(3, 3), new Vector2(5.0f, 1.0f)));
        // Isolated reflective tile: the faster axis of the shell decides.
        Assert.True(map.ReflectiveSurfaceIsHorizontal(Centre(6, 1), new Vector2(1.0f, 5.0f)));
        Assert.False(map.ReflectiveSurfaceIsHorizontal(Centre(6, 1), new Vector2(5.0f, 1.0f)));
        Assert.True(map.ReflectiveSurfaceIsHorizontal(Centre(6, 1), diagonal));
    }

    [Fact]
    public void ParsesPickupObjectsAndAppliesDefaults()
    {
        var map = LoadFixture();

        Assert.Equal(3, map.PickupSpawns.Count); // the unknown "Aircraft" type is ignored

        var fuel = map.PickupSpawns[0];
        Assert.Equal(PickupKind.Fuel, fuel.Kind);
        Assert.Equal(new Vector2(24, 40), fuel.Position);
        Assert.Equal(50, fuel.Amount);
        Assert.Equal("Sprites/fueldrop_1", fuel.SpriteAsset);

        var ammunition = map.PickupSpawns[1];
        Assert.Equal(PickupKind.Ammunition, ammunition.Kind);
        Assert.Equal(8, ammunition.Amount);
        Assert.Equal("standard-shell", ammunition.AmmunitionId);
        Assert.Equal("Sprites/custom", ammunition.SpriteAsset);

        Assert.Equal(50, map.PickupSpawns[2].Amount); // Amount 0 falls back to the default
    }

    [Fact]
    public void CheckedInArenaSatisfiesTheMapContract()
    {
        var map = new WorldMap(Path.Combine(AppContext.BaseDirectory, "Fixtures", "arena", "arena_01.tmx"));

        Assert.Equal(new Rectangle(0, 0, 960, 640), map.Bounds);
        Assert.NotEmpty(map.PickupSpawns);
        Assert.All(map.PickupSpawns, spawn =>
        {
            Assert.Contains(spawn.Kind, new[] { PickupKind.Fuel, PickupKind.Ammunition });
            Assert.True(spawn.Amount > 0);
            Assert.True(map.Bounds.Contains(spawn.Position));
            Assert.False(string.IsNullOrWhiteSpace(spawn.SpriteAsset));
        });
    }
}

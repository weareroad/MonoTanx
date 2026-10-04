using Microsoft.Xna.Framework;
using MonoTanx.Core;

namespace MonoTanx.Tests;

internal static class TestSupport
{
    public static string FixturePath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Fixtures" }.Concat(parts).ToArray());

    // The 8x4 fixture map: tile (x, y) has its centre at (x * 16 + 8, y * 16 + 8).
    public static WorldMap LoadTerrainMap() => new WorldMap(FixturePath("terrain.tmx"));

    public static Vector2 Centre(int x, int y) => new Vector2(x * 16 + 8, y * 16 + 8);

    public static Player NewPlayer(Vector2 position, float heading = 0.0f, string name = "Test")
    {
        var player = new Player(name, "Sprites/tank", Color.White, Player.DefaultAmmunition, Tuning.Tank.StartingShells);
        player.Position = position;
        player.Heading = heading;
        return player;
    }

    // Parked well away from anything a test is moving.
    public static Player NewBystander() => NewPlayer(Centre(7, 3), name: "Bystander");
}

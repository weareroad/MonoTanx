using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TiledCS;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using System.Globalization;

namespace MonoTanx.Core
{
    public enum TerrainKind
    {
        Ground,
        Water,
        Bridge,
        Wall,
        Ravine,
        Hill,
        Reflective,
        OutOfBounds
    }

    public enum PickupKind { Fuel, Ammunition }

    public sealed class PickupSpawn
    {
        public int Id { get; }
        public PickupKind Kind { get; }
        public Vector2 Position { get; }
        public int Amount { get; }
        public string AmmunitionId { get; }
        public string SpriteAsset { get; }

        public PickupSpawn(int id, PickupKind kind, Vector2 position, int amount, string ammunitionId, string spriteAsset)
        {
            Id = id; Kind = kind; Position = position; Amount = amount; AmmunitionId = ammunitionId; SpriteAsset = spriteAsset;
        }
    }

    public sealed class TerrainDefinition
    {
        public TerrainKind Kind { get; }
        public float MovementSpeedMultiplier { get; }
        public float FuelCostMultiplier { get; }

        public TerrainDefinition(TerrainKind kind, float movementSpeedMultiplier = 1.0f, float fuelCostMultiplier = 1.0f)
        {
            Kind = kind;
            MovementSpeedMultiplier = MathHelper.Max(0.0f, movementSpeedMultiplier);
            FuelCostMultiplier = MathHelper.Max(0.0f, fuelCostMultiplier);
        }
    }

    public class WorldMap
    {
        private TiledMap map;
        private Dictionary<int, TerrainDefinition> terrainByGid;
        public IReadOnlyList<PickupSpawn> PickupSpawns { get; }

        public Rectangle Bounds => new Rectangle(0, 0, map.Width * map.TileWidth, map.Height * map.TileHeight);
        public int TileWidth => map.TileWidth;
        public int TileHeight => map.TileHeight;

        public WorldMap(string mapPath)
        {
            map = new TiledMap(mapPath);
            terrainByGid = LoadTerrainDefinitions(mapPath);
            PickupSpawns = LoadPickupSpawns(mapPath);
        }

        // Raw Tiled files are copied beside the built content. Resolve them
        // from the application directory so loading does not depend on the
        // process working directory chosen by an IDE or launcher.
        public static string ResolveMapPath(string contentRootDirectory, string mapName)
        {
            return Path.Combine(AppContext.BaseDirectory, contentRootDirectory, mapName);
        }

        public Point WorldToTile(Vector2 worldPosition)
        {
            return new Point(
                (int)System.Math.Floor(worldPosition.X / TileWidth),
                (int)System.Math.Floor(worldPosition.Y / TileHeight));
        }

        public Rectangle GetTileBounds(Point tile)
        {
            return new Rectangle(tile.X * TileWidth, tile.Y * TileHeight, TileWidth, TileHeight);
        }

        public bool IsInside(Point tile)
        {
            return tile.X >= 0 && tile.X < map.Width && tile.Y >= 0 && tile.Y < map.Height;
        }

        public TerrainKind GetTerrainAt(Vector2 worldPosition)
        {
            return GetTerrainAt(WorldToTile(worldPosition));
        }

        public TerrainKind GetTerrainAt(Point tile)
        {
            if (!IsInside(tile))
                return TerrainKind.OutOfBounds;

            return GetTerrainDefinitionAt(tile).Kind;
        }

        public bool BlocksMovement(Vector2 worldPosition)
        {
            return IsMovementBlocked(GetTerrainAt(worldPosition));
        }

        public bool BlocksProjectiles(Vector2 worldPosition)
        {
            return GetTerrainAt(worldPosition) is TerrainKind.Wall or TerrainKind.Hill or TerrainKind.OutOfBounds;
        }

        public bool BlocksVision(Vector2 worldPosition)
        {
            return GetTerrainAt(worldPosition) is TerrainKind.Wall or TerrainKind.Hill or TerrainKind.Reflective or TerrainKind.OutOfBounds;
        }

        public bool ReflectsProjectiles(Vector2 worldPosition)
        {
            return GetTerrainAt(worldPosition) == TerrainKind.Reflective;
        }

        public bool ReflectiveSurfaceIsHorizontal(Vector2 worldPosition, Vector2 velocity)
        {
            var tile = WorldToTile(worldPosition);
            var horizontalRun = ReflectsProjectiles(GetTileBounds(new Point(tile.X - 1, tile.Y)).Center.ToVector2())
                || ReflectsProjectiles(GetTileBounds(new Point(tile.X + 1, tile.Y)).Center.ToVector2());
            var verticalRun = ReflectsProjectiles(GetTileBounds(new Point(tile.X, tile.Y - 1)).Center.ToVector2())
                || ReflectsProjectiles(GetTileBounds(new Point(tile.X, tile.Y + 1)).Center.ToVector2());

            if (horizontalRun && !verticalRun)
                return true;
            if (verticalRun && !horizontalRun)
                return false;
            return Math.Abs(velocity.Y) >= Math.Abs(velocity.X);
        }

        public bool HasLineOfSight(Vector2 start, Vector2 end)
        {
            var distance = Vector2.Distance(start, end);
            var steps = Math.Max(1, (int)Math.Ceiling(distance / 4.0f));
            for (var step = 1; step < steps; step++)
            {
                var position = Vector2.Lerp(start, end, step / (float)steps);
                if (BlocksVision(position))
                    return false;
            }
            return true;
        }

        public float GetFuelCostMultiplier(Vector2 worldPosition)
        {
            return GetTerrainDefinitionAt(WorldToTile(worldPosition)).FuelCostMultiplier;
        }

        public float GetMovementSpeedMultiplier(Vector2 worldPosition)
        {
            return GetTerrainDefinitionAt(WorldToTile(worldPosition)).MovementSpeedMultiplier;
        }

        private TerrainDefinition GetTerrainDefinitionAt(Point tile)
        {
            if (!IsInside(tile))
                return new TerrainDefinition(TerrainKind.OutOfBounds);

            var terrainLayer = map.Layers.FirstOrDefault(x => x.type == TiledLayerType.TileLayer && x.name == "Terrain")
                ?? map.Layers.FirstOrDefault(x => x.type == TiledLayerType.TileLayer);
            if (terrainLayer == null || tile.X >= terrainLayer.width || tile.Y >= terrainLayer.height)
                return new TerrainDefinition(TerrainKind.Ground);

            var gid = terrainLayer.data[tile.Y * terrainLayer.width + tile.X];
            return gid != 0 && terrainByGid.TryGetValue(gid, out var terrain)
                ? terrain
                : new TerrainDefinition(TerrainKind.Ground);
        }

        public bool CanOccupyCircle(Vector2 center, float radius)
        {
            var minimumTile = WorldToTile(center - new Vector2(radius));
            var maximumTile = WorldToTile(center + new Vector2(radius));

            for (var y = minimumTile.Y; y <= maximumTile.Y; y++)
            {
                for (var x = minimumTile.X; x <= maximumTile.X; x++)
                {
                    var tile = new Point(x, y);
                    if (!BlocksMovement(GetTileBounds(tile), center, radius))
                        continue;

                    return false;
                }
            }

            return true;
        }

        private bool BlocksMovement(Rectangle tileBounds, Vector2 circleCenter, float radius)
        {
            var terrain = GetTerrainAt(WorldToTile(new Vector2(tileBounds.Center.X, tileBounds.Center.Y)));
            if (!IsMovementBlocked(terrain))
                return false;

            var closestPoint = new Vector2(
                MathHelper.Clamp(circleCenter.X, tileBounds.Left, tileBounds.Right),
                MathHelper.Clamp(circleCenter.Y, tileBounds.Top, tileBounds.Bottom));
            return Vector2.DistanceSquared(circleCenter, closestPoint) <= radius * radius;
        }

        private static bool IsMovementBlocked(TerrainKind terrain)
        {
            return terrain is TerrainKind.Water or TerrainKind.Wall or TerrainKind.Ravine or TerrainKind.Hill or TerrainKind.Reflective or TerrainKind.OutOfBounds;
        }

        private static Dictionary<int, TerrainDefinition> LoadTerrainDefinitions(string mapPath)
        {
            var terrainByGid = new Dictionary<int, TerrainDefinition>();
            var mapDocument = XDocument.Load(mapPath);

            foreach (var tilesetElement in mapDocument.Root?.Elements("tileset") ?? Enumerable.Empty<XElement>())
            {
                var firstGid = (int?)tilesetElement.Attribute("firstgid") ?? 1;
                var source = (string)tilesetElement.Attribute("source");
                var definition = tilesetElement;

                if (!string.IsNullOrWhiteSpace(source))
                {
                    var tilesetPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mapPath) ?? string.Empty, source));
                    definition = XDocument.Load(tilesetPath).Root;
                }

                foreach (var tileElement in definition?.Elements("tile") ?? Enumerable.Empty<XElement>())
                {
                    var localId = (int?)tileElement.Attribute("id");
                    if (!localId.HasValue)
                        continue;

                    var terrainName = (string)tileElement
                        .Element("properties")?
                        .Elements("property")
                        .FirstOrDefault(x => (string)x.Attribute("name") == "TerrainKind")?
                        .Attribute("value");

                    if (Enum.TryParse(terrainName, true, out TerrainKind terrain))
                    {
                        var movement = ReadFloatProperty(tileElement, "MovementSpeedMultiplier", 1.0f);
                        var fuel = ReadFloatProperty(tileElement, "FuelCostMultiplier", 1.0f);
                        terrainByGid[firstGid + localId.Value] = new TerrainDefinition(terrain, movement, fuel);
                    }
                }
            }

            return terrainByGid;
        }

        private static IReadOnlyList<PickupSpawn> LoadPickupSpawns(string mapPath)
        {
            var spawns = new List<PickupSpawn>();
            var document = XDocument.Load(mapPath);
            var objectLayer = document.Root?.Elements("objectgroup")
                .FirstOrDefault(x => (string)x.Attribute("name") == "Pickups");
            foreach (var objectElement in objectLayer?.Elements("object") ?? Enumerable.Empty<XElement>())
            {
                var typeName = (string)objectElement.Attribute("type") ?? (string)objectElement.Attribute("class");
                if (!Enum.TryParse(typeName, true, out PickupKind kind))
                    continue;
                var id = (int?)objectElement.Attribute("id") ?? 0;
                var x = (float?)objectElement.Attribute("x") ?? 0.0f;
                var y = (float?)objectElement.Attribute("y") ?? 0.0f;
                var amount = ReadIntProperty(objectElement, "Amount", 0);
                if (amount <= 0)
                    amount = kind == PickupKind.Fuel ? 50 : 5;
                var ammunitionId = ReadStringProperty(objectElement, "AmmunitionId", null);
                var spriteAsset = ReadStringProperty(objectElement, "SpriteAsset", null);
                if (string.IsNullOrWhiteSpace(spriteAsset))
                    spriteAsset = kind == PickupKind.Fuel ? "Sprites/fueldrop_1" : "Sprites/ammodrop_1";
                spawns.Add(new PickupSpawn(id, kind, new Vector2(x, y), amount, ammunitionId, spriteAsset));
            }
            return spawns;
        }

        private static int ReadIntProperty(XElement element, string propertyName, int defaultValue)
        {
            var value = ReadStringProperty(element, propertyName, null);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
        }

        private static string ReadStringProperty(XElement element, string propertyName, string defaultValue)
        {
            return (string)element.Element("properties")?.Elements("property")
                .FirstOrDefault(x => (string)x.Attribute("name") == propertyName)?.Attribute("value") ?? defaultValue;
        }

        private static float ReadFloatProperty(XElement tileElement, string propertyName, float defaultValue)
        {
            var value = (string)tileElement.Element("properties")?.Elements("property")
                .FirstOrDefault(x => (string)x.Attribute("name") == propertyName)?.Attribute("value");
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? MathHelper.Max(0.0f, result)
                : defaultValue;
        }
    }
}

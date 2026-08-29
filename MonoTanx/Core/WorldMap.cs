using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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
        private Dictionary<int, TiledTileset> tilesets;
        private Texture2D tilesetTexture;
        private Dictionary<int, TerrainDefinition> terrainByGid;

        public Rectangle Bounds => new Rectangle(0, 0, map.Width * map.TileWidth, map.Height * map.TileHeight);
        public int TileWidth => map.TileWidth;
        public int TileHeight => map.TileHeight;

        public WorldMap(ContentManager contentManager, string mapName, string textureName)
        {
            // Raw Tiled files are copied beside the built content. Resolve them
            // from the application directory so loading does not depend on the
            // process working directory chosen by an IDE or launcher.
            var contentDirectory = Path.Combine(AppContext.BaseDirectory, contentManager.RootDirectory);
            var mapPath = Path.Combine(contentDirectory, mapName);
            map = new TiledMap(mapPath);
            tilesets = map.GetTiledTilesets(contentDirectory + Path.DirectorySeparatorChar);
            tilesetTexture = contentManager.Load<Texture2D>(textureName);
            terrainByGid = LoadTerrainDefinitions(mapPath, contentDirectory);
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

        private static Dictionary<int, TerrainDefinition> LoadTerrainDefinitions(string mapPath, string contentDirectory)
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
                    var tilesetPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mapPath) ?? contentDirectory, source));
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

        private static float ReadFloatProperty(XElement tileElement, string propertyName, float defaultValue)
        {
            var value = (string)tileElement.Element("properties")?.Elements("property")
                .FirstOrDefault(x => (string)x.Attribute("name") == propertyName)?.Attribute("value");
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? MathHelper.Max(0.0f, result)
                : defaultValue;
        }

        private TiledSourceRect GetSourceRect(TiledMapTileset mapTileset, TiledTileset tileset, int gid)
        {
            int num = 0;
            int num2 = 0;
            for (int i = 0; i < tileset.TileCount; i++)
            {
                if (i == gid - mapTileset.firstgid)
                {
                    return new TiledSourceRect
                    {
                        x = tileset.Margin + num * (tileset.TileWidth + tileset.Spacing),
                        y = tileset.Margin + num2 * (tileset.TileHeight + tileset.Spacing),
                        width = tileset.TileWidth,
                        height = tileset.TileHeight
                    };
                }

                num++;
                if (num == tileset.Image.width / tileset.TileWidth)
                {
                    num = 0;
                    num2++;
                }
            }

            return null;
        }


        public void Draw(SpriteBatch spriteBatch)
        {
            var tileLayers = map.Layers.Where(x => x.type == TiledLayerType.TileLayer);

            foreach (var layer in tileLayers)
            {
                for (var y = 0; y < layer.height; y++)
                {
                    for (var x = 0; x < layer.width; x++)
                    {
                        var index = (y * layer.width) + x; // Assuming the default render order is used which is from right to bottom
                        var gid = layer.data[index]; // The tileset tile index
                        var tileX = x * map.TileWidth;
                        var tileY = y * map.TileHeight;

                        // Gid 0 is used to tell there is no tile set
                        if (gid == 0)
                        {
                            continue;
                        }

                        // Helper method to fetch the right TieldMapTileset instance
                        // This is a connection object Tiled uses for linking the correct tileset to the gid value using the firstgid property
                        var mapTileset = map.GetTiledMapTileset(gid);

                        // Retrieve the actual tileset based on the firstgid property of the connection object we retrieved just now
                        var tileset = tilesets[mapTileset.firstgid];

                        // Use the connection object as well as the tileset to figure out the source rectangle
                        // use my temp method because of margin and scaling properties
                        //var rect = map.GetSourceRect(mapTileset, tileset, gid);
                        var rect = GetSourceRect(mapTileset, tileset, gid);

                        // Create destination and source rectangles
                        var source = new Rectangle(rect.x, rect.y, rect.width, rect.height);
                        var destination = new Rectangle(tileX, tileY, map.TileWidth, map.TileHeight);

                        // You can use the helper methods to get useful information to generate maps
                        SpriteEffects effects = SpriteEffects.None;
                        if (map.IsTileFlippedHorizontal(layer, x, y))
                        {
                            effects |= SpriteEffects.FlipHorizontally;
                        }
                        if (map.IsTileFlippedVertical(layer, x, y))
                        {
                            effects |= SpriteEffects.FlipVertically;
                        }

                        // Render sprite at position tileX, tileY using the rect
                        spriteBatch.Draw(tilesetTexture, destination, source, Color.White, 0f, Vector2.Zero, effects, 0);
                    }
                }
            }

        }            
    }
}


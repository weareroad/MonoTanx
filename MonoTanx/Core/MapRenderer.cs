using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using TiledCS;

namespace MonoTanx.Core
{
    // Draws a Tiled map's tile layers. Holds the tileset texture so that
    // WorldMap can stay free of graphics-device dependencies.
    public sealed class MapRenderer
    {
        private readonly TiledMap map;
        private readonly Dictionary<int, TiledTileset> tilesets;
        private readonly Texture2D tilesetTexture;

        public MapRenderer(ContentManager contentManager, string mapName, string textureName)
        {
            var mapPath = WorldMap.ResolveMapPath(contentManager.RootDirectory, mapName);
            var contentDirectory = System.IO.Path.GetDirectoryName(mapPath) + System.IO.Path.DirectorySeparatorChar;
            map = new TiledMap(mapPath);
            tilesets = map.GetTiledTilesets(contentDirectory);
            tilesetTexture = contentManager.Load<Texture2D>(textureName);
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

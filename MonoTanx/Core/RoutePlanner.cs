using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    // Routes across the tile grid for a tank of a given size. Pure, so it can be
    // tested without a graphics device. Routes are lists of tiles from the start
    // to the goal inclusive, moving one tile at a time up, down, left or right.
    public static class RoutePlanner
    {
        // The shortest route (A*) over tiles a tank of this radius can occupy, or
        // null when there is none (including when the goal itself is blocked).
        public static List<Point> FindRoute(WorldMap map, float radius, Point start, Point goal)
        {
            var frontier = new PriorityQueue<Point, int>();
            var cameFrom = new Dictionary<Point, Point>();
            var costSoFar = new Dictionary<Point, int> { [start] = 0 };
            frontier.Enqueue(start, 0);
            var directions = new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };
            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current == goal) break;
                foreach (var direction in directions)
                {
                    var next = new Point(current.X + direction.X, current.Y + direction.Y);
                    if (!map.IsInside(next) || !map.CanOccupyCircle(map.GetTileBounds(next).Center.ToVector2(), radius)) continue;
                    var nextCost = costSoFar[current] + 1;
                    if (costSoFar.TryGetValue(next, out var oldCost) && oldCost <= nextCost) continue;
                    costSoFar[next] = nextCost;
                    cameFrom[next] = current;
                    frontier.Enqueue(next, nextCost + Math.Abs(goal.X - next.X) + Math.Abs(goal.Y - next.Y));
                }
            }
            if (!costSoFar.ContainsKey(goal)) return null;
            var route = new List<Point>();
            for (var current = goal; ; current = cameFrom[current]) { route.Add(current); if (current == start) break; }
            route.Reverse();
            return route;
        }

        // A route to a combat position: the nearest reachable tile to the opponent
        // on a ring around it (the preferred distance in tiles, never under the
        // minimum, give or take the tolerance). Null when no ring tile is reachable.
        // With an alternative of 1 or more it skips that many reachable ring tiles first
        // (taking the last one reachable if there are fewer), so a computer that keeps
        // getting stuck on the way to one goal can try another.
        public static List<Point> FindCombatRoute(WorldMap map, float radius, Point start, Point opponentTile, int preferredDistanceTiles, int alternative = 0)
        {
            var candidates = RingTiles(map, radius, opponentTile, preferredDistanceTiles);
            candidates.Sort((a, b) => Vector2.DistanceSquared(a.ToVector2(), opponentTile.ToVector2()).CompareTo(Vector2.DistanceSquared(b.ToVector2(), opponentTile.ToVector2())));
            List<Point> found = null;
            var skipped = 0;
            foreach (var goal in candidates)
            {
                var route = FindRoute(map, radius, start, goal);
                if (route == null) continue;
                found = route;
                if (skipped >= alternative) break;
                skipped++;
            }
            return found;
        }

        // The tiles on the combat ring around the opponent that a tank of this radius can stand on:
        // within the ring distance give or take the tolerance.
        private static List<Point> RingTiles(WorldMap map, float radius, Point opponentTile, int preferredDistanceTiles)
        {
            var ring = Math.Max(Tuning.Ai.MinimumCombatRingTiles, preferredDistanceTiles);
            var candidates = new List<Point>();
            for (var y = opponentTile.Y - ring; y <= opponentTile.Y + ring; y++)
                for (var x = opponentTile.X - ring; x <= opponentTile.X + ring; x++)
                {
                    var candidate = new Point(x, y);
                    if (!map.IsInside(candidate) || !map.CanOccupyCircle(map.GetTileBounds(candidate).Center.ToVector2(), radius)) continue;
                    var distance = Vector2.Distance(candidate.ToVector2(), opponentTile.ToVector2());
                    if (distance >= ring - Tuning.Ai.CombatRingToleranceTiles && distance <= ring + Tuning.Ai.CombatRingToleranceTiles) candidates.Add(candidate);
                }
            return candidates;
        }

        // Places to shoot from: the tiles on a ring around the opponent (the given distance, give or take
        // the ring tolerance) from which it can be seen (a clear line of sight from the tile's centre to the
        // opponent's tile) and which are no further from it than the distance itself (the near half of the
        // ring: a shot's error grows with distance), nearest the opponent first.
        public static List<Point> FindFiringPositions(WorldMap map, float radius, Point opponentTile, int preferredDistanceTiles)
        {
            var opponentCentre = map.GetTileBounds(opponentTile).Center.ToVector2();
            var positions = RingTiles(map, radius, opponentTile, preferredDistanceTiles);
            positions.RemoveAll(tile => Vector2.Distance(tile.ToVector2(), opponentTile.ToVector2()) > preferredDistanceTiles
                || !map.HasLineOfSight(map.GetTileBounds(tile).Center.ToVector2(), opponentCentre));
            positions.Sort((a, b) => Vector2.DistanceSquared(a.ToVector2(), opponentTile.ToVector2()).CompareTo(Vector2.DistanceSquared(b.ToVector2(), opponentTile.ToVector2())));
            return positions;
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoTanx.Core;
using System;
using System.Collections.Generic;

namespace MonoTanx.Stages
{
    public class GameStage : Stage
    {
        private const int HudHeight = Tuning.Presentation.HudHeight;

        private readonly WorldMap worldMap;
        private readonly MapRenderer mapRenderer;
        private readonly SpriteFont debugFont;
        private readonly Texture2D placeholderShellTexture;
        private readonly Player playerOne;
        private readonly Player playerTwo;
        private readonly Player[] tanks;
        private readonly List<Shell> shells = new List<Shell>();
        private readonly List<Pickup> pickups = new List<Pickup>();
        private readonly List<Point> playerTwoRoute = new List<Point>();
        private Vector2 cameraPosition;
        private Vector2 lastPlayerOnePosition;
        private int playerTwoRouteIndex;
        private int playerTwoPickupTargetId = -1;
        private bool playerTwoLongRangePursuit;
        private float playerTwoLongRangeHeading;
        private float playerTwoFireTimer;
        private float playerTwoRetaliationTimer;
        private readonly ScreenShake shake = new ScreenShake();
        private bool debugOverlayVisible;

        public Player Player1 => playerOne;
        public Player Player2 => playerTwo;

        public GameStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content) : base(game, graphicsDevice, content)
        {
            worldMap = new WorldMap(WorldMap.ResolveMapPath(content.RootDirectory, "arena_01.tmx"));
            mapRenderer = new MapRenderer(content, "arena_01.tmx", "arena_01");
            debugFont = content.Load<SpriteFont>("SpriteFonts/dogica");
            var tankTexture = content.Load<Texture2D>("Sprites/tank");
            var tankTwoTexture = LoadTankTwoTexture(content, tankTexture);
            placeholderShellTexture = new Texture2D(graphicsDevice, 1, 1);
            placeholderShellTexture.SetData(new[] { Color.White });
            playerOne = new Player("Player 1", "Sprites/tank", Color.White, Player.DefaultAmmunition, Tuning.Tank.StartingShells);
            playerTwo = new Player("Player 2", "Sprites/tank2", Color.LightGray, Player.DefaultAmmunition, Tuning.Tank.StartingShells, isComputerControlled: true);
            playerOne.Texture = tankTexture;
            playerTwo.Texture = tankTwoTexture;
            playerOne.Position = FindStartingPosition(true);
            playerTwo.Position = FindStartingPosition(false);
            playerOne.Heading = HeadingToward(playerOne.Position, playerTwo.Position);
            playerTwo.Heading = HeadingToward(playerTwo.Position, playerOne.Position);
            tanks = new[] { playerOne, playerTwo };
            lastPlayerOnePosition = playerOne.Position;
            foreach (var spawn in worldMap.PickupSpawns)
            {
                if (string.IsNullOrWhiteSpace(spawn.SpriteAsset))
                    continue;
                try
                {
                    pickups.Add(new Pickup(spawn, content.Load<Texture2D>(spawn.SpriteAsset)));
                }
                catch (ContentLoadException)
                {
                    // Keep malformed/unavailable pickup art from preventing the arena from loading.
                }
            }
            UpdateCamera();
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            var shakeOffset = shake.Offset;
            // Snap the world translation to whole pixels. A fractional offset makes
            // point sampling land on tile-atlas texel boundaries and pick up
            // neighbouring (empty) atlas texels, which shows as thin dark seams.
            var worldOffset = new Vector2(
                (float)Math.Round(-cameraPosition.X + shakeOffset.X),
                (float)Math.Round(-cameraPosition.Y + HudHeight + shakeOffset.Y));
            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(worldOffset.X, worldOffset.Y, 0.0f));
            mapRenderer.Draw(spriteBatch);
            DrawTank(spriteBatch, playerOne);
            DrawTank(spriteBatch, playerTwo);
            foreach (var pickup in pickups)
            {
                if (!pickup.Active) continue;
                var frameWidth = pickup.Texture.Width / Tuning.Presentation.PickupFrameCount;
                var frameHeight = pickup.Texture.Height;
                var source = new Rectangle(pickup.Frame * frameWidth, 0, frameWidth, frameHeight);
                var origin = new Vector2(frameWidth / 2.0f, frameHeight / 2.0f);
                spriteBatch.Draw(pickup.Texture, pickup.Spawn.Position, source, Color.White, 0.0f, origin, 1.0f, SpriteEffects.None, 0.45f);
            }
            foreach (var shell in shells)
            {
                var offset = Tuning.Presentation.PlaceholderShellSize / 2;
                var bounds = new Rectangle((int)shell.Position.X - offset, (int)shell.Position.Y - offset, Tuning.Presentation.PlaceholderShellSize, Tuning.Presentation.PlaceholderShellSize);
                spriteBatch.Draw(placeholderShellTexture, bounds, null, Color.Yellow, 0.0f, Vector2.Zero, SpriteEffects.None, 0.6f);
            }
            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(placeholderShellTexture, new Rectangle(0, 0, (int)Tanx.DesignedWidth, HudHeight), new Color(48, 54, 60));
            DrawPlayerOneHud(spriteBatch);
            DrawPlayerTwoHud(spriteBatch);
            spriteBatch.DrawString(debugFont, "P1 WASD/Space  P2 Cursor Keys/Enter  F1 reset P1  F2 P2 cpu/human  Esc quit", new Vector2(8.0f, Tanx.DesignedHeight - 24.0f), Color.White);
            if (debugOverlayVisible)
                DrawDebugOverlay(spriteBatch);
            spriteBatch.End();
        }

        public override void PostUpdate(GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            debugOverlayVisible = keyboard.IsKeyDown(Keys.F5);
            if (debugOverlayVisible)
            {
                prevKeyboardState = keyboard;
                return;
            }
            if (keyboard.IsKeyDown(Keys.F1) && prevKeyboardState.IsKeyUp(Keys.F1)) ResetPlayerOneResources();
            if (keyboard.IsKeyDown(Keys.F2) && prevKeyboardState.IsKeyUp(Keys.F2)) TogglePlayerTwoControl();
            UpdateTank(playerOne, keyboard, elapsed, Keys.A, Keys.D, Keys.W, Keys.S, Keys.Space);
            if (playerTwo.IsComputerControlled)
                UpdateComputerPlayer(elapsed);
            else
                UpdateTank(playerTwo, keyboard, elapsed, Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Enter);
            if (playerTwo.IsComputerControlled)
                UpdateComputerFiring(elapsed);
            UpdateShells(elapsed);
            UpdatePickupAnimations(elapsed);
            CollectPickups(playerOne);
            CollectPickups(playerTwo);
            UpdateCamera();
            shake.Update(elapsed, game.Random.Cosmetic);
            if (keyboard.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape)) game.Exit();
            prevKeyboardState = keyboard;
        }

        private void UpdateTank(Player tank, KeyboardState keyboard, float elapsed, Keys left, Keys right, Keys forwardKey, Keys reverseKey, Keys fireKey)
        {
            var turn = 0.0f; var drive = 0.0f;
            if (keyboard.IsKeyDown(left)) turn -= 1.0f;
            if (keyboard.IsKeyDown(right)) turn += 1.0f;
            if (keyboard.IsKeyDown(forwardKey)) drive += 1.0f;
            if (keyboard.IsKeyDown(reverseKey)) drive -= 1.0f;
            if (TankMovement.ApplyInput(worldMap, tank, OtherTank(tank), turn, drive, elapsed))
                UpdateTankAnimation(tank, elapsed);
            else { tank.AnimationTimer = 0.0f; tank.Frame = 0; }
            tank.TickReload(elapsed);
            if (keyboard.IsKeyDown(fireKey) && prevKeyboardState.IsKeyUp(fireKey)) TryFireShell(tank);
        }

        private void MoveTank(Player tank, Vector2 movement)
        {
            TankMovement.Move(worldMap, tank, OtherTank(tank), movement);
        }

        private Player OtherTank(Player tank) => ReferenceEquals(tank, playerOne) ? playerTwo : playerOne;

        private void CollectPickups(Player player)
        {
            foreach (var pickup in pickups)
            {
                if (!pickup.Active || !PickupRules.InRange(player, pickup.Spawn))
                    continue;
                PickupRules.Apply(player, pickup.Spawn);
                pickup.Active = false;
            }
        }

        private void UpdatePickupAnimations(float elapsed)
        {
            foreach (var pickup in pickups)
            {
                if (!pickup.Active) continue;
                pickup.AnimationTimer += elapsed;
                while (pickup.AnimationTimer >= Tuning.Presentation.PickupFrameSeconds)
                {
                    pickup.AnimationTimer -= Tuning.Presentation.PickupFrameSeconds;
                    pickup.Frame = (pickup.Frame + 1) % Tuning.Presentation.PickupFrameCount;
                }
            }
        }

        private void UpdateComputerPlayer(float elapsed)
        {
            playerTwo.TickReload(elapsed);
            playerTwoRetaliationTimer = Math.Max(0.0f, playerTwoRetaliationTimer - elapsed);
            if (playerTwoRetaliationTimer > 0.0f)
            {
                var retaliationHeading = HeadingToward(playerTwo.Position, playerOne.Position);
                var retaliationTurn = Math.Sign(MathHelper.WrapAngle(retaliationHeading - playerTwo.Heading));
                var retaliationFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
                var retaliationCost = Math.Abs(retaliationTurn) * Tuning.Tank.TurnFuelPerSecond * elapsed * retaliationFuel;
                if (playerTwo.Fuel >= retaliationCost)
                {
                    playerTwo.Heading = MathHelper.WrapAngle(playerTwo.Heading + retaliationTurn * playerTwo.TurnSpeed * elapsed);
                    playerTwo.Fuel = MathHelper.Max(0.0f, playerTwo.Fuel - retaliationCost);
                }
                return;
            }

            var pickupTarget = FindPlayerTwoPickupTarget();
            if (pickupTarget != null)
            {
                UpdateComputerPickupSeek(pickupTarget, elapsed);
                return;
            }

            if (playerTwo.RemainingAmmunition == 0)
            {
                UpdateComputerEvade(elapsed);
                return;
            }

            var playerDistance = Vector2.Distance(playerTwo.Position, playerOne.Position);
            var longRangeThreshold = worldMap.Bounds.Width * playerTwo.LongRangePursuitDistanceFraction;
            if (playerDistance <= longRangeThreshold && worldMap.HasLineOfSight(playerTwo.Position, playerOne.Position))
                return;
            if (playerDistance > longRangeThreshold)
            {
                if (!playerTwoLongRangePursuit)
                {
                    playerTwoLongRangePursuit = true;
                    playerTwoLongRangeHeading = HeadingToward(playerTwo.Position, playerOne.Position);
                    playerTwoRoute.Clear();
                    playerTwoRouteIndex = 0;
                }

                var longRangeAngle = MathHelper.WrapAngle(playerTwoLongRangeHeading - playerTwo.Heading);
                var longRangeTurn = Math.Sign(longRangeAngle);
                var longRangeFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
                var longRangeTurnCost = Math.Abs(longRangeTurn) * Tuning.Tank.TurnFuelPerSecond * elapsed * longRangeFuel;
                var longRangeDriveCost = playerTwo.ForwardFuelPerSecond * elapsed * longRangeFuel;
                if (playerTwo.Fuel >= longRangeTurnCost + longRangeDriveCost)
                {
                    playerTwo.Heading = MathHelper.WrapAngle(playerTwo.Heading + longRangeTurn * playerTwo.TurnSpeed * elapsed);
                    MoveTank(playerTwo, new Vector2((float)Math.Cos(playerTwo.Heading), (float)Math.Sin(playerTwo.Heading)) * playerTwo.MovementSpeed * worldMap.GetMovementSpeedMultiplier(playerTwo.Position) * elapsed);
                    playerTwo.Fuel = MathHelper.Max(0.0f, playerTwo.Fuel - longRangeTurnCost - longRangeDriveCost);
                }
                return;
            }

            playerTwoLongRangePursuit = false;
            if (Vector2.DistanceSquared(lastPlayerOnePosition, playerOne.Position) > Tuning.Ai.RouteRebuildDistance * Tuning.Ai.RouteRebuildDistance || playerTwoRouteIndex >= playerTwoRoute.Count)
            {
                BuildPlayerTwoRoute();
                lastPlayerOnePosition = playerOne.Position;
            }

            if (playerTwoRouteIndex >= playerTwoRoute.Count)
                return;

            var waypoint = playerTwoRoute[playerTwoRouteIndex];
            var target = worldMap.GetTileBounds(waypoint).Center.ToVector2();
            if (Vector2.DistanceSquared(playerTwo.Position, target) < Tuning.Ai.WaypointReachedDistance * Tuning.Ai.WaypointReachedDistance)
            {
                playerTwoRouteIndex++;
                return;
            }

            var desiredHeading = HeadingToward(playerTwo.Position, target);
            var angle = MathHelper.WrapAngle(desiredHeading - playerTwo.Heading);
            var turn = Math.Sign(angle);
            var drive = Math.Abs(angle) < Tuning.Ai.DriveAngleLimitRadians ? 1.0f : 0.0f;
            var terrainFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
            var turnCost = Math.Abs(turn) * Tuning.Tank.TurnFuelPerSecond * elapsed * terrainFuel;
            var driveCost = drive * playerTwo.ForwardFuelPerSecond * elapsed * terrainFuel;
            if (playerTwo.Fuel < turnCost + driveCost)
                return;
            playerTwo.Heading = MathHelper.WrapAngle(playerTwo.Heading + turn * playerTwo.TurnSpeed * elapsed);
            if (drive != 0.0f)
                MoveTank(playerTwo, new Vector2((float)Math.Cos(playerTwo.Heading), (float)Math.Sin(playerTwo.Heading)) * playerTwo.MovementSpeed * worldMap.GetMovementSpeedMultiplier(playerTwo.Position) * elapsed);
            playerTwo.Fuel = MathHelper.Max(0.0f, playerTwo.Fuel - turnCost - driveCost);
        }

        private void UpdateComputerFiring(float elapsed)
        {
            playerTwoFireTimer = Math.Max(0.0f, playerTwoFireTimer - elapsed);
            if (playerTwoFireTimer > 0.0f || playerTwo.ReloadTimer > 0.0f)
                return;

            var desiredHeading = HeadingToward(playerTwo.Position, playerOne.Position);
            var aimError = Math.Abs(MathHelper.WrapAngle(desiredHeading - playerTwo.Heading));
            var hasLineOfSight = worldMap.HasLineOfSight(playerTwo.Position, playerOne.Position);
            if (aimError > playerTwo.ComputerAimToleranceRadians)
            {
                var turn = Math.Sign(MathHelper.WrapAngle(desiredHeading - playerTwo.Heading));
                var terrainFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
                var turnCost = Math.Abs(turn) * Tuning.Tank.TurnFuelPerSecond * elapsed * terrainFuel;
                if (playerTwo.Fuel >= turnCost)
                {
                    playerTwo.Heading = MathHelper.WrapAngle(playerTwo.Heading + turn * playerTwo.TurnSpeed * elapsed);
                    playerTwo.Fuel = MathHelper.Max(0.0f, playerTwo.Fuel - turnCost);
                }
                return;
            }

            if (!hasLineOfSight)
                return;

            if (TryFireShell(playerTwo))
                playerTwoFireTimer = playerTwo.ComputerFireCooldownSeconds + playerTwo.ComputerReactionDelaySeconds;
        }

        private void UpdateComputerEvade(float elapsed)
        {
            // With no ammunition, survival takes priority over positioning:
            // continuously steer and drive away from Player 1. Collision
            // resolution will slide around map obstacles.
            var desiredHeading = HeadingToward(playerOne.Position, playerTwo.Position);
            var turn = Math.Sign(MathHelper.WrapAngle(desiredHeading - playerTwo.Heading));
            var terrainFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
            var turnCost = Math.Abs(turn) * Tuning.Tank.TurnFuelPerSecond * elapsed * terrainFuel;
            var driveCost = playerTwo.ForwardFuelPerSecond * elapsed * terrainFuel;
            if (playerTwo.Fuel < turnCost + driveCost)
                return;
            playerTwo.Heading = MathHelper.WrapAngle(playerTwo.Heading + turn * playerTwo.TurnSpeed * elapsed);
            MoveTank(playerTwo, new Vector2((float)Math.Cos(playerTwo.Heading), (float)Math.Sin(playerTwo.Heading)) * playerTwo.MovementSpeed * worldMap.GetMovementSpeedMultiplier(playerTwo.Position) * elapsed);
            playerTwo.Fuel = MathHelper.Max(0.0f, playerTwo.Fuel - turnCost - driveCost);
        }

        private Pickup FindPlayerTwoPickupTarget()
        {
            var needsFuel = playerTwo.Fuel < playerTwo.MaximumFuel * Tuning.Ai.NeedsFuelBelowFraction;
            var needsAmmo = playerTwo.RemainingAmmunition < playerTwo.StartingAmmunition * Tuning.Ai.NeedsAmmoBelowFraction;
            if (!needsFuel && !needsAmmo) return null;
            Pickup best = null;
            var bestDistance = float.MaxValue;
            foreach (var pickup in pickups)
            {
                if (!pickup.Active || (pickup.Spawn.Kind == PickupKind.Fuel ? !needsFuel : !needsAmmo)) continue;
                var distance = Vector2.DistanceSquared(playerTwo.Position, pickup.Spawn.Position);
                if (distance < bestDistance) { bestDistance = distance; best = pickup; }
            }
            return best;
        }

        private void UpdateComputerPickupSeek(Pickup target, float elapsed)
        {
            if (playerTwoPickupTargetId != target.Spawn.Id || playerTwoRouteIndex >= playerTwoRoute.Count)
            {
                playerTwoPickupTargetId = target.Spawn.Id;
                playerTwoRoute.Clear();
                playerTwoRouteIndex = 0;
                var route = FindRoute(worldMap.WorldToTile(playerTwo.Position), worldMap.WorldToTile(target.Spawn.Position));
                if (route != null) { playerTwoRoute.AddRange(route); playerTwoRouteIndex = Math.Min(1, playerTwoRoute.Count); }
            }
            if (playerTwoRouteIndex >= playerTwoRoute.Count) return;
            var waypoint = worldMap.GetTileBounds(playerTwoRoute[playerTwoRouteIndex]).Center.ToVector2();
            if (Vector2.DistanceSquared(playerTwo.Position, waypoint) < Tuning.Ai.WaypointReachedDistance * Tuning.Ai.WaypointReachedDistance) { playerTwoRouteIndex++; return; }
            var desiredHeading = HeadingToward(playerTwo.Position, waypoint);
            var turn = Math.Sign(MathHelper.WrapAngle(desiredHeading - playerTwo.Heading));
            var terrainFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
            var turnCost = Math.Abs(turn) * Tuning.Tank.TurnFuelPerSecond * elapsed * terrainFuel;
            var driveCost = playerTwo.ForwardFuelPerSecond * elapsed * terrainFuel;
            if (playerTwo.Fuel < turnCost + driveCost) return;
            playerTwo.Heading = MathHelper.WrapAngle(playerTwo.Heading + turn * playerTwo.TurnSpeed * elapsed);
            MoveTank(playerTwo, new Vector2((float)Math.Cos(playerTwo.Heading), (float)Math.Sin(playerTwo.Heading)) * playerTwo.MovementSpeed * worldMap.GetMovementSpeedMultiplier(playerTwo.Position) * elapsed);
            playerTwo.Fuel = MathHelper.Max(0.0f, playerTwo.Fuel - turnCost - driveCost);
        }

        private void BuildPlayerTwoEvadeRoute()
        {
            playerTwoRoute.Clear();
            playerTwoRouteIndex = 0;
            var start = worldMap.WorldToTile(playerTwo.Position);
            var playerTile = worldMap.WorldToTile(playerOne.Position);
            var candidates = new List<Point>();
            for (var y = 0; y < worldMap.Bounds.Height; y += worldMap.TileHeight)
                for (var x = 0; x < worldMap.Bounds.Width; x += worldMap.TileWidth)
                {
                    var candidate = new Point(x / worldMap.TileWidth, y / worldMap.TileHeight);
                    if (worldMap.CanOccupyCircle(worldMap.GetTileBounds(candidate).Center.ToVector2(), playerTwo.CollisionRadius))
                        candidates.Add(candidate);
                }
            candidates.Sort((a, b) => Vector2.DistanceSquared(b.ToVector2(), playerTile.ToVector2()).CompareTo(Vector2.DistanceSquared(a.ToVector2(), playerTile.ToVector2())));
            var attempts = Math.Min(Tuning.Ai.EvadeCandidateLimit, candidates.Count);
            for (var index = 0; index < attempts; index++)
            {
                var route = FindRoute(start, candidates[index]);
                if (route == null) continue;
                playerTwoRoute.AddRange(route);
                playerTwoRouteIndex = Math.Min(1, playerTwoRoute.Count);
                return;
            }
        }

        private void BuildPlayerTwoRoute()
        {
            playerTwoRoute.Clear();
            playerTwoRouteIndex = 0;
            var playerTile = worldMap.WorldToTile(playerOne.Position);
            var startTile = worldMap.WorldToTile(playerTwo.Position);
            var radius = Math.Max(Tuning.Ai.MinimumCombatRingTiles, playerTwo.PreferredCombatDistanceTiles);
            var candidates = new List<Point>();
            for (var y = playerTile.Y - radius; y <= playerTile.Y + radius; y++)
                for (var x = playerTile.X - radius; x <= playerTile.X + radius; x++)
                {
                    var candidate = new Point(x, y);
                    if (!worldMap.IsInside(candidate) || !worldMap.CanOccupyCircle(worldMap.GetTileBounds(candidate).Center.ToVector2(), playerTwo.CollisionRadius)) continue;
                    var distance = Vector2.Distance(candidate.ToVector2(), playerTile.ToVector2());
                    if (distance >= radius - Tuning.Ai.CombatRingToleranceTiles && distance <= radius + Tuning.Ai.CombatRingToleranceTiles) candidates.Add(candidate);
                }
            candidates.Sort((a, b) => Vector2.DistanceSquared(a.ToVector2(), playerTile.ToVector2()).CompareTo(Vector2.DistanceSquared(b.ToVector2(), playerTile.ToVector2())));
            foreach (var goal in candidates)
            {
                var route = FindRoute(startTile, goal);
                if (route != null) { playerTwoRoute.AddRange(route); playerTwoRouteIndex = Math.Min(1, playerTwoRoute.Count); return; }
            }
        }

        private List<Point> FindRoute(Point start, Point goal)
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
                    if (!worldMap.IsInside(next) || !worldMap.CanOccupyCircle(worldMap.GetTileBounds(next).Center.ToVector2(), playerTwo.CollisionRadius)) continue;
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

        private bool CanTankOccupy(Player tank, Vector2 position)
        {
            return TankMovement.CanOccupy(worldMap, tank, OtherTank(tank), position);
        }

        private Vector2 FindStartingPosition(bool topLeft)
        {
            var corner = topLeft ? new Vector2(playerOne.CollisionRadius, playerOne.CollisionRadius) : new Vector2(worldMap.Bounds.Right - playerTwo.CollisionRadius, worldMap.Bounds.Bottom - playerTwo.CollisionRadius);
            var result = corner; var bestDistance = float.MaxValue;
            for (var y = 0; y < worldMap.Bounds.Height; y += worldMap.TileHeight)
                for (var x = 0; x < worldMap.Bounds.Width; x += worldMap.TileWidth)
                {
                    var candidate = new Vector2(x + worldMap.TileWidth / 2.0f, y + worldMap.TileHeight / 2.0f);
                    if (!worldMap.CanOccupyCircle(candidate, playerOne.CollisionRadius)) continue;
                    var distance = Vector2.DistanceSquared(candidate, corner);
                    if (distance < bestDistance) { bestDistance = distance; result = candidate; }
                }
            return result;
        }

        private static float HeadingToward(Vector2 from, Vector2 to)
        {
            return (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
        }

        // Flips Player 2 between computer and human (cursor keys/Enter) control.
        // Clears the computer's working state so it starts afresh when it takes
        // control back.
        private void TogglePlayerTwoControl()
        {
            playerTwo.IsComputerControlled = !playerTwo.IsComputerControlled;
            playerTwoRoute.Clear();
            playerTwoRouteIndex = 0;
            playerTwoPickupTargetId = -1;
            playerTwoLongRangePursuit = false;
            playerTwoFireTimer = 0.0f;
            playerTwoRetaliationTimer = 0.0f;
            playerTwo.AnimationTimer = 0.0f;
            playerTwo.Frame = 0;
        }

        private void ResetPlayerOneResources()
        {
            playerOne.ResetFuelAndAmmunition();
        }

        private bool TryFireShell(Player tank)
        {
            var muzzleOffset = tank.Texture.Width / Tuning.Presentation.TankFrameCount / 2.0f + Tuning.Presentation.MuzzleClearance;
            if (!tank.TryFire(muzzleOffset, out var launch)) return false;
            shells.Add(new Shell(launch.Ammunition, launch.Position, launch.Velocity));
            if (ReferenceEquals(tank, playerOne)) StartShake(Tuning.Shake.FireDuration, Tuning.Shake.FireMagnitude);
            return true;
        }

        private void UpdateShells(float elapsed)
        {
            for (var index = shells.Count - 1; index >= 0; index--)
            {
                var shell = shells[index];
                var result = shell.Step(worldMap, tanks, elapsed);
                if (result.Hit != null)
                    DamageTank(result.Hit, shell.Ammunition.Damage, shell.Velocity);
                if (result.Removed)
                    shells.RemoveAt(index);
            }
        }

        private void DamageTank(Player tank, int damage, Vector2 impactVelocity)
        {
            var destroyed = TankDamage.Apply(worldMap, tank, OtherTank(tank), damage, impactVelocity, game.Random.Gameplay);
            StartShake(Tuning.Shake.HitDuration, Tuning.Shake.HitMagnitude);
            if (ReferenceEquals(tank, playerTwo))
            {
                playerTwoRetaliationTimer = Tuning.Ai.RetaliationSeconds;
                playerTwoFireTimer = 0.0f;
            }
            if (destroyed)
                game.Exit();
        }

        private void StartShake(float duration, float magnitude)
        {
            shake.Start(duration, magnitude);
        }

        private void UpdateCamera()
        {
            var maxX = MathHelper.Max(0.0f, worldMap.Bounds.Width - Tanx.DesignedWidth);
            var viewportHeight = Tanx.DesignedHeight - HudHeight;
            var maxY = MathHelper.Max(0.0f, worldMap.Bounds.Height - viewportHeight);
            cameraPosition.X = MathHelper.Clamp(playerOne.Position.X - Tanx.DesignedWidth / 2.0f, 0.0f, maxX);
            cameraPosition.Y = MathHelper.Clamp(playerOne.Position.Y - viewportHeight / 2.0f, 0.0f, maxY);
        }

        private static void UpdateTankAnimation(Player tank, float elapsed)
        {
            tank.AnimationTimer += elapsed;
            while (tank.AnimationTimer >= Tuning.Presentation.TankFrameSeconds) { tank.AnimationTimer -= Tuning.Presentation.TankFrameSeconds; tank.Frame = (tank.Frame + 1) % Tuning.Presentation.TankFrameCount; }
        }

        private static string ReloadText(Player tank) => tank.ReloadTimer > 0.0f ? tank.ReloadTimer.ToString("0.0") : "READY";

        private void DrawPlayerOneHud(SpriteBatch spriteBatch)
        {
            var panelWidth = (int)Tanx.DesignedWidth * 3 / 4;
            spriteBatch.DrawString(debugFont, "P1", new Vector2(8.0f, 4.0f), Color.White);
            DrawHealthBar(spriteBatch, new Rectangle(48, 8, panelWidth - 160, 12), playerOne);
            spriteBatch.DrawString(debugFont, $"{playerOne.Health}%", new Vector2(panelWidth - 112, 4.0f), Color.White);
            spriteBatch.DrawString(debugFont, $"Shells {playerOne.RemainingAmmunition}", new Vector2(8.0f, 25.0f), Color.White);
            spriteBatch.DrawString(debugFont, playerOne.ReloadTimer > 0.0f ? "!" : "", new Vector2(panelWidth - 32, 22.0f), Color.White);
            spriteBatch.DrawString(debugFont, "Fuel", new Vector2(8.0f, 52.0f), Color.White);
            DrawFuelBar(spriteBatch, new Rectangle(48, 56, panelWidth - 160, 12), playerOne);
            spriteBatch.DrawString(debugFont, $"{playerOne.Fuel / playerOne.MaximumFuel * 100.0f:0}%", new Vector2(panelWidth - 112, 52.0f), Color.White);
        }

        private void DrawPlayerTwoHud(SpriteBatch spriteBatch)
        {
            var panelX = (int)Tanx.DesignedWidth * 3 / 4;
            var panelWidth = (int)Tanx.DesignedWidth - panelX;
            spriteBatch.DrawString(debugFont, "P2", new Vector2(panelX + 8.0f, 4.0f), Color.White);
            var gaugeWidth = panelWidth - 72;
            DrawHealthBar(spriteBatch, new Rectangle(panelX + 38, 8, gaugeWidth, 12), playerTwo);
            spriteBatch.DrawString(debugFont, playerTwo.ReloadTimer > 0.0f ? "!" : "", new Vector2(panelX + panelWidth - 26, 22.0f), Color.White);
            spriteBatch.DrawString(debugFont, playerTwo.IsComputerControlled ? "CPU (F2)" : "HUMAN (F2)", new Vector2(panelX + 38.0f, 32.0f), Color.White);
            spriteBatch.DrawString(debugFont, "F", new Vector2(panelX + 8.0f, 52.0f), Color.White);
            DrawFuelBar(spriteBatch, new Rectangle(panelX + 38, 56, gaugeWidth, 12), playerTwo);
        }

        private void DrawHealthBar(SpriteBatch spriteBatch, Rectangle bounds, Player player)
        {
            spriteBatch.Draw(placeholderShellTexture, bounds, Color.Red);
            var fillWidth = (int)(bounds.Width * MathHelper.Clamp(player.Health / (float)player.MaximumHealth, 0.0f, 1.0f));
            if (fillWidth > 0)
                spriteBatch.Draw(placeholderShellTexture, new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), Color.Yellow);
        }

        private void DrawFuelBar(SpriteBatch spriteBatch, Rectangle bounds, Player player)
        {
            spriteBatch.Draw(placeholderShellTexture, bounds, Color.Red);
            var fillWidth = (int)(bounds.Width * MathHelper.Clamp(player.Fuel / player.MaximumFuel, 0.0f, 1.0f));
            if (fillWidth > 0)
                spriteBatch.Draw(placeholderShellTexture, new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), Color.Yellow);
        }

        private void DrawDebugOverlay(SpriteBatch spriteBatch)
        {
            var panel = new Rectangle(8, HudHeight + 8, 360, 128);
            spriteBatch.Draw(placeholderShellTexture, panel, Color.Black * 0.78f);
            var p1Tile = worldMap.WorldToTile(playerOne.Position);
            var p2Tile = worldMap.WorldToTile(playerTwo.Position);
            var lines = new[]
            {
                "DEBUG (hold F5)",
                $"P1 pos {playerOne.Position.X:0.00},{playerOne.Position.Y:0.00} tile {p1Tile.X},{p1Tile.Y}",
                $"P1 dir {playerOne.Heading:0.000} rad / {MathHelper.ToDegrees(playerOne.Heading):0.0} deg",
                $"P2 pos {playerTwo.Position.X:0.00},{playerTwo.Position.Y:0.00} tile {p2Tile.X},{p2Tile.Y}",
                $"P2 dir {playerTwo.Heading:0.000} rad / {MathHelper.ToDegrees(playerTwo.Heading):0.0} deg",
                $"AI mode {(FindPlayerTwoPickupTarget() != null ? "PICKUP" : playerTwo.RemainingAmmunition == 0 ? "FLEE" : playerTwoLongRangePursuit ? "LONG" : "COMBAT")} route {playerTwoRouteIndex}/{playerTwoRoute.Count}",
                $"AI fire {playerTwoFireTimer:0.00} retaliate {playerTwoRetaliationTimer:0.00} pickups {worldMap.PickupSpawns.Count}",
                $"Seed {game.Random.Seed}"
            };
            for (var index = 0; index < lines.Length; index++)
                spriteBatch.DrawString(debugFont, lines[index], new Vector2(14.0f, HudHeight + 12.0f + index * 16.0f), Color.White);
        }

        private static Texture2D LoadTankTwoTexture(ContentManager content, Texture2D fallback)
        {
            try { return content.Load<Texture2D>("Sprites/tank2"); }
            catch (ContentLoadException) { return fallback; }
        }

        private static void DrawTank(SpriteBatch spriteBatch, Player tank)
        {
            var frameWidth = tank.Texture.Width / Tuning.Presentation.TankFrameCount;
            var frameHeight = tank.Texture.Height;
            var source = new Rectangle(tank.Frame * frameWidth, 0, frameWidth, frameHeight);
            spriteBatch.Draw(tank.Texture, tank.Position, source, tank.Tint, tank.Heading - MathHelper.Pi, new Vector2(frameWidth / 2.0f, frameHeight / 2.0f), 1.0f, SpriteEffects.None, 0.5f);
        }

        private sealed class Pickup
        {
            public PickupSpawn Spawn { get; }
            public Texture2D Texture { get; }
            public bool Active { get; set; } = true;
            public float AnimationTimer;
            public int Frame;
            public Pickup(PickupSpawn spawn, Texture2D texture) { Spawn = spawn; Texture = texture; }
        }
    }
}

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
        private const int TankFrameCount = 4;
        private const float TankFrameDuration = 0.15f;
        private const float ShellCollisionRadius = 3.0f;
        private const float TurnFuelPerSecond = 0.25f;
        private const float ShellSpeed = 260.0f;
        private const int PlaceholderShellSize = 8;
        private const int PickupFrameCount = 4;
        private const float PickupFrameDuration = 0.15f;
        private const int HudHeight = 80;
        private const float PlayerOneFireShakeDuration = 0.16f;
        private const float PlayerOneFireShakeMagnitude = 1.8f;
        private const float HitShakeDuration = 0.12f;
        private const float HitShakeMagnitude = 1.0f;

        private readonly WorldMap worldMap;
        private readonly MapRenderer mapRenderer;
        private readonly SpriteFont debugFont;
        private readonly Texture2D placeholderShellTexture;
        private readonly Player playerOne;
        private readonly Player playerTwo;
        private readonly List<Shell> shells = new List<Shell>();
        private readonly List<Pickup> pickups = new List<Pickup>();
        private readonly List<Point> playerTwoRoute = new List<Point>();
        private readonly Random shakeRandom = new Random();
        private Vector2 cameraPosition;
        private float shakeTimeRemaining;
        private float shakeDuration;
        private float shakeMagnitude;
        private Vector2 lastPlayerOnePosition;
        private int playerTwoRouteIndex;
        private int playerTwoPickupTargetId = -1;
        private bool playerTwoLongRangePursuit;
        private float playerTwoLongRangeHeading;
        private float playerTwoFireTimer;
        private float playerTwoRetaliationTimer;
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
            playerOne = new Player("Player 1", "Sprites/tank", Color.White, Player.DefaultAmmunition, Player.DefaultStartingShells);
            playerTwo = new Player("Player 2", "Sprites/tank2", Color.LightGray, Player.DefaultAmmunition, Player.DefaultStartingShells, isComputerControlled: true);
            playerOne.Texture = tankTexture;
            playerTwo.Texture = tankTwoTexture;
            playerOne.Position = FindStartingPosition(true);
            playerTwo.Position = FindStartingPosition(false);
            playerOne.Heading = HeadingToward(playerOne.Position, playerTwo.Position);
            playerTwo.Heading = HeadingToward(playerTwo.Position, playerOne.Position);
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
            var shakeOffset = GetShakeOffset();
            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(-cameraPosition.X + shakeOffset.X, -cameraPosition.Y + HudHeight + shakeOffset.Y, 0.0f));
            mapRenderer.Draw(spriteBatch);
            DrawTank(spriteBatch, playerOne);
            DrawTank(spriteBatch, playerTwo);
            foreach (var pickup in pickups)
            {
                if (!pickup.Active) continue;
                var frameWidth = pickup.Texture.Width / PickupFrameCount;
                var frameHeight = pickup.Texture.Height;
                var source = new Rectangle(pickup.Frame * frameWidth, 0, frameWidth, frameHeight);
                var origin = new Vector2(frameWidth / 2.0f, frameHeight / 2.0f);
                spriteBatch.Draw(pickup.Texture, pickup.Spawn.Position, source, Color.White, 0.0f, origin, 1.0f, SpriteEffects.None, 0.45f);
            }
            foreach (var shell in shells)
            {
                var offset = PlaceholderShellSize / 2;
                var bounds = new Rectangle((int)shell.Position.X - offset, (int)shell.Position.Y - offset, PlaceholderShellSize, PlaceholderShellSize);
                spriteBatch.Draw(placeholderShellTexture, bounds, null, Color.Yellow, 0.0f, Vector2.Zero, SpriteEffects.None, 0.6f);
            }
            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(placeholderShellTexture, new Rectangle(0, 0, (int)Tanx.DesignedWidth, HudHeight), new Color(48, 54, 60));
            DrawPlayerOneHud(spriteBatch);
            DrawPlayerTwoHud(spriteBatch);
            spriteBatch.DrawString(debugFont, "P1 WASD/Space  P2 Cursor Keys/Enter  F1 reset P1  Esc quit", new Vector2(8.0f, Tanx.DesignedHeight - 24.0f), Color.White);
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
            shakeTimeRemaining = Math.Max(0.0f, shakeTimeRemaining - elapsed);
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
            var terrainFuel = worldMap.GetFuelCostMultiplier(tank.Position);
            var turnCost = Math.Abs(turn) * TurnFuelPerSecond * elapsed * terrainFuel;
            var driveRate = drive < 0.0f ? tank.ReverseFuelPerSecond : tank.ForwardFuelPerSecond;
            var driveCost = Math.Abs(drive) * driveRate * elapsed * terrainFuel;
            var canTurn = turn == 0.0f || tank.Fuel >= turnCost;
            var canDrive = drive == 0.0f || tank.Fuel >= turnCost + driveCost;
            if (canTurn) tank.Heading = MathHelper.WrapAngle(tank.Heading + turn * tank.TurnSpeed * elapsed);
            if (canDrive && drive != 0.0f)
            {
                var direction = new Vector2((float)Math.Cos(tank.Heading), (float)Math.Sin(tank.Heading));
                var speed = drive < 0.0f ? tank.ReverseMovementSpeed : tank.MovementSpeed;
                MoveTank(tank, direction * drive * speed * worldMap.GetMovementSpeedMultiplier(tank.Position) * elapsed);
                UpdateTankAnimation(tank, elapsed);
            }
            else { tank.AnimationTimer = 0.0f; tank.Frame = 0; }
            if (canTurn && canDrive) tank.Fuel = MathHelper.Max(0.0f, tank.Fuel - turnCost - driveCost);
            else if (canTurn) tank.Fuel = MathHelper.Max(0.0f, tank.Fuel - turnCost);
            tank.ReloadTimer = MathHelper.Max(0.0f, tank.ReloadTimer - elapsed);
            if (keyboard.IsKeyDown(fireKey) && prevKeyboardState.IsKeyUp(fireKey)) TryFireShell(tank);
        }

        private void MoveTank(Player tank, Vector2 movement)
        {
            var horizontal = tank.Position + new Vector2(movement.X, 0.0f);
            if (CanTankOccupy(tank, horizontal)) tank.Position = horizontal;
            var vertical = tank.Position + new Vector2(0.0f, movement.Y);
            if (CanTankOccupy(tank, vertical)) tank.Position = vertical;
        }

        private void CollectPickups(Player player)
        {
            foreach (var pickup in pickups)
            {
                if (!pickup.Active || Vector2.DistanceSquared(player.Position, pickup.Spawn.Position) > 12.0f * 12.0f)
                    continue;
                if (pickup.Spawn.Kind == PickupKind.Fuel)
                    player.Fuel = MathHelper.Min(player.MaximumFuel, player.Fuel + pickup.Spawn.Amount);
                else
                {
                    var slot = player.AmmunitionSlots.Find(x => string.IsNullOrWhiteSpace(pickup.Spawn.AmmunitionId) || x.Ammunition.Id == pickup.Spawn.AmmunitionId)
                        ?? player.AmmunitionSlots[0];
                    slot.Remaining += pickup.Spawn.Amount;
                }
                pickup.Active = false;
            }
        }

        private void UpdatePickupAnimations(float elapsed)
        {
            foreach (var pickup in pickups)
            {
                if (!pickup.Active) continue;
                pickup.AnimationTimer += elapsed;
                while (pickup.AnimationTimer >= PickupFrameDuration)
                {
                    pickup.AnimationTimer -= PickupFrameDuration;
                    pickup.Frame = (pickup.Frame + 1) % PickupFrameCount;
                }
            }
        }

        private void UpdateComputerPlayer(float elapsed)
        {
            playerTwo.ReloadTimer = Math.Max(0.0f, playerTwo.ReloadTimer - elapsed);
            playerTwoRetaliationTimer = Math.Max(0.0f, playerTwoRetaliationTimer - elapsed);
            if (playerTwoRetaliationTimer > 0.0f)
            {
                var retaliationHeading = HeadingToward(playerTwo.Position, playerOne.Position);
                var retaliationTurn = Math.Sign(MathHelper.WrapAngle(retaliationHeading - playerTwo.Heading));
                var retaliationFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
                var retaliationCost = Math.Abs(retaliationTurn) * TurnFuelPerSecond * elapsed * retaliationFuel;
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
                var longRangeTurnCost = Math.Abs(longRangeTurn) * TurnFuelPerSecond * elapsed * longRangeFuel;
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
            if (Vector2.DistanceSquared(lastPlayerOnePosition, playerOne.Position) > 1.0f || playerTwoRouteIndex >= playerTwoRoute.Count)
            {
                BuildPlayerTwoRoute();
                lastPlayerOnePosition = playerOne.Position;
            }

            if (playerTwoRouteIndex >= playerTwoRoute.Count)
                return;

            var waypoint = playerTwoRoute[playerTwoRouteIndex];
            var target = worldMap.GetTileBounds(waypoint).Center.ToVector2();
            if (Vector2.DistanceSquared(playerTwo.Position, target) < 16.0f)
            {
                playerTwoRouteIndex++;
                return;
            }

            var desiredHeading = HeadingToward(playerTwo.Position, target);
            var angle = MathHelper.WrapAngle(desiredHeading - playerTwo.Heading);
            var turn = Math.Sign(angle);
            var drive = Math.Abs(angle) < 1.1f ? 1.0f : 0.0f;
            var terrainFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
            var turnCost = Math.Abs(turn) * TurnFuelPerSecond * elapsed * terrainFuel;
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
                var turnCost = Math.Abs(turn) * TurnFuelPerSecond * elapsed * terrainFuel;
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
            var turnCost = Math.Abs(turn) * TurnFuelPerSecond * elapsed * terrainFuel;
            var driveCost = playerTwo.ForwardFuelPerSecond * elapsed * terrainFuel;
            if (playerTwo.Fuel < turnCost + driveCost)
                return;
            playerTwo.Heading = MathHelper.WrapAngle(playerTwo.Heading + turn * playerTwo.TurnSpeed * elapsed);
            MoveTank(playerTwo, new Vector2((float)Math.Cos(playerTwo.Heading), (float)Math.Sin(playerTwo.Heading)) * playerTwo.MovementSpeed * worldMap.GetMovementSpeedMultiplier(playerTwo.Position) * elapsed);
            playerTwo.Fuel = MathHelper.Max(0.0f, playerTwo.Fuel - turnCost - driveCost);
        }

        private Pickup FindPlayerTwoPickupTarget()
        {
            var needsFuel = playerTwo.Fuel < playerTwo.MaximumFuel * 0.5f;
            var needsAmmo = playerTwo.RemainingAmmunition < playerTwo.StartingAmmunition * 0.5f;
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
            if (Vector2.DistanceSquared(playerTwo.Position, waypoint) < 16.0f) { playerTwoRouteIndex++; return; }
            var desiredHeading = HeadingToward(playerTwo.Position, waypoint);
            var turn = Math.Sign(MathHelper.WrapAngle(desiredHeading - playerTwo.Heading));
            var terrainFuel = worldMap.GetFuelCostMultiplier(playerTwo.Position);
            var turnCost = Math.Abs(turn) * TurnFuelPerSecond * elapsed * terrainFuel;
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
            var attempts = Math.Min(20, candidates.Count);
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
            var radius = Math.Max(2, playerTwo.PreferredCombatDistanceTiles);
            var candidates = new List<Point>();
            for (var y = playerTile.Y - radius; y <= playerTile.Y + radius; y++)
                for (var x = playerTile.X - radius; x <= playerTile.X + radius; x++)
                {
                    var candidate = new Point(x, y);
                    if (!worldMap.IsInside(candidate) || !worldMap.CanOccupyCircle(worldMap.GetTileBounds(candidate).Center.ToVector2(), playerTwo.CollisionRadius)) continue;
                    var distance = Vector2.Distance(candidate.ToVector2(), playerTile.ToVector2());
                    if (distance >= radius - 1.0f && distance <= radius + 1.0f) candidates.Add(candidate);
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
            if (!worldMap.CanOccupyCircle(position, tank.CollisionRadius))
                return false;

            var otherTank = ReferenceEquals(tank, playerOne) ? playerTwo : playerOne;
            return Vector2.DistanceSquared(position, otherTank.Position) >
                (tank.CollisionRadius + otherTank.CollisionRadius) * (tank.CollisionRadius + otherTank.CollisionRadius);
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

        private void ResetPlayerOneResources()
        {
            playerOne.ResetFuelAndAmmunition();
        }

        private bool TryFireShell(Player tank)
        {
            if (tank.ReloadTimer > 0.0f || !tank.TryConsumeAmmunition(out var ammunition)) return false;
            var direction = new Vector2((float)Math.Cos(tank.Heading), (float)Math.Sin(tank.Heading));
            shells.Add(new Shell(ammunition, tank.Position + direction * (tank.Texture.Width / TankFrameCount / 2.0f + 4.0f), direction * ShellSpeed));
            tank.ReloadTimer = ammunition.ReloadTimeSeconds;
            if (ReferenceEquals(tank, playerOne)) StartShake(PlayerOneFireShakeDuration, PlayerOneFireShakeMagnitude);
            return true;
        }

        private void UpdateShells(float elapsed)
        {
            for (var index = shells.Count - 1; index >= 0; index--)
            {
                var shell = shells[index]; shell.Age += elapsed;
                if (shell.Age >= shell.Ammunition.MaxFlightDurationSeconds) { shells.RemoveAt(index); continue; }
                var steps = Math.Max(1, (int)Math.Ceiling(shell.Velocity.Length() * elapsed / 4.0f));
                var stepTime = elapsed / steps; var removed = false;
                shell.ReflectionCooldown = Math.Max(0.0f, shell.ReflectionCooldown - elapsed);
                for (var step = 0; step < steps; step++)
                {
                    shell.Position += shell.Velocity * stepTime;
                    var terrain = worldMap.GetTerrainAt(shell.Position);
                    if (terrain == TerrainKind.Reflective && shell.ReflectionCooldown <= 0.0f)
                    {
                        // Treat reflective runs as axis-aligned mirrors. A horizontal
                        // run flips Y; a vertical run flips X. Isolated tiles fall
                        // back to the velocity-based axis choice.
                        if (worldMap.ReflectiveSurfaceIsHorizontal(shell.Position, shell.Velocity))
                        {
                            shell.Velocity.Y = -shell.Velocity.Y;
                            shell.Position.Y += Math.Sign(shell.Velocity.Y) * 2.0f;
                        }
                        else
                        {
                            shell.Velocity.X = -shell.Velocity.X;
                            shell.Position.X += Math.Sign(shell.Velocity.X) * 2.0f;
                        }
                        shell.ReflectionCooldown = 0.08f;
                        if (++shell.ReflectionCount > 8) { removed = true; break; }
                    }
                    else if (worldMap.BlocksProjectiles(shell.Position)) { removed = true; break; }

                    if (Vector2.DistanceSquared(shell.Position, playerOne.Position) <=
                        (ShellCollisionRadius + playerOne.CollisionRadius) * (ShellCollisionRadius + playerOne.CollisionRadius))
                    {
                        DamageTank(playerOne, shell.Ammunition.Damage, shell.Velocity);
                        removed = true;
                        break;
                    }

                    if (Vector2.DistanceSquared(shell.Position, playerTwo.Position) <=
                        (ShellCollisionRadius + playerTwo.CollisionRadius) * (ShellCollisionRadius + playerTwo.CollisionRadius))
                    {
                        DamageTank(playerTwo, shell.Ammunition.Damage, shell.Velocity);
                        removed = true;
                        break;
                    }
                }
                if (removed) shells.RemoveAt(index);
            }
        }

        private void DamageTank(Player tank, int damage, Vector2 impactVelocity)
        {
            tank.Health = Math.Max(0, tank.Health - Math.Max(0, damage));
            if (impactVelocity.LengthSquared() > 0.0f)
            {
                var knockback = Vector2.Normalize(impactVelocity) * 1.5f;
                if (CanTankOccupy(tank, tank.Position + knockback))
                    tank.Position += knockback;
            }
            tank.Heading = MathHelper.WrapAngle(tank.Heading + ((float)shakeRandom.NextDouble() * 2.0f - 1.0f) * 0.16f);
            StartShake(HitShakeDuration, HitShakeMagnitude);
            if (ReferenceEquals(tank, playerTwo))
            {
                playerTwoRetaliationTimer = 1.5f;
                playerTwoFireTimer = 0.0f;
            }
            if (tank.Health == 0)
                game.Exit();
        }

        private void StartShake(float duration, float magnitude)
        {
            shakeTimeRemaining = Math.Max(shakeTimeRemaining, duration);
            shakeDuration = Math.Max(shakeDuration, duration);
            shakeMagnitude = Math.Max(shakeMagnitude, magnitude);
        }

        private Vector2 GetShakeOffset()
        {
            if (shakeTimeRemaining <= 0.0f || shakeDuration <= 0.0f)
                return Vector2.Zero;

            var strength = shakeTimeRemaining / shakeDuration;
            return new Vector2(
                ((float)shakeRandom.NextDouble() * 2.0f - 1.0f) * shakeMagnitude * strength,
                ((float)shakeRandom.NextDouble() * 2.0f - 1.0f) * shakeMagnitude * strength);
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
            while (tank.AnimationTimer >= TankFrameDuration) { tank.AnimationTimer -= TankFrameDuration; tank.Frame = (tank.Frame + 1) % TankFrameCount; }
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
            var panel = new Rectangle(8, HudHeight + 8, 360, 112);
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
                $"AI fire {playerTwoFireTimer:0.00} retaliate {playerTwoRetaliationTimer:0.00} pickups {worldMap.PickupSpawns.Count}"
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
            var frameWidth = tank.Texture.Width / TankFrameCount;
            var frameHeight = tank.Texture.Height;
            var source = new Rectangle(tank.Frame * frameWidth, 0, frameWidth, frameHeight);
            spriteBatch.Draw(tank.Texture, tank.Position, source, tank.Tint, tank.Heading - MathHelper.Pi, new Vector2(frameWidth / 2.0f, frameHeight / 2.0f), 1.0f, SpriteEffects.None, 0.5f);
        }

        private sealed class Shell
        {
            public Ammunition Ammunition; public Vector2 Position; public Vector2 Velocity; public float Age; public int ReflectionCount; public float ReflectionCooldown;
            public Shell(Ammunition ammunition, Vector2 position, Vector2 velocity) { Ammunition = ammunition; Position = position; Velocity = velocity; }
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

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
        private const float TankSpeed = 120.0f;
        private const float ReverseSpeed = TankSpeed * 0.5f;
        private const float TankTurnSpeed = 2.5f;
        private const int TankFrameCount = 4;
        private const float TankFrameDuration = 0.15f;
        private const float TankCollisionRadius = 6.0f;
        private const float ShellCollisionRadius = 3.0f;
        private const int StartingHealth = 100;
        private const float MaximumFuel = 100.0f;
        private const float TurnFuelPerSecond = 0.25f;
        private const float ForwardFuelPerSecond = 4.0f;
        private const float ReverseFuelPerSecond = ForwardFuelPerSecond * 2.0f;
        private const int StartingShells = 20;
        private const float ShellSpeed = 260.0f;
        private const int PlaceholderShellSize = 8;
        private static readonly Ammunition StandardShell = new Ammunition("standard-shell", "A basic shell for testing tank combat.", "placeholder-shell", "placeholder-shell-fire", 3.0f, 5.0f, 1);

        private readonly WorldMap worldMap;
        private readonly SpriteFont debugFont;
        private readonly Texture2D placeholderShellTexture;
        private readonly Tank playerOne;
        private readonly Tank playerTwo;
        private readonly List<Shell> shells = new List<Shell>();
        private Vector2 cameraPosition;

        public GameStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content) : base(game, graphicsDevice, content)
        {
            worldMap = new WorldMap(content, "arena_01.tmx", "arena_01");
            debugFont = content.Load<SpriteFont>("SpriteFonts/dogica");
            var tankTexture = content.Load<Texture2D>("Sprites/tank");
            var tankTwoTexture = LoadTankTwoTexture(content, tankTexture);
            placeholderShellTexture = new Texture2D(graphicsDevice, 1, 1);
            placeholderShellTexture.SetData(new[] { Color.White });
            playerOne = new Tank("Player 1", tankTexture, FindStartingPosition(true), Color.White);
            playerTwo = new Tank("Player 2", tankTwoTexture, FindStartingPosition(false), Color.LightGray);
            UpdateCamera();
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(-cameraPosition.X, -cameraPosition.Y, 0.0f));
            worldMap.Draw(spriteBatch);
            DrawTank(spriteBatch, playerOne);
            DrawTank(spriteBatch, playerTwo);
            foreach (var shell in shells)
            {
                var offset = PlaceholderShellSize / 2;
                var bounds = new Rectangle((int)shell.Position.X - offset, (int)shell.Position.Y - offset, PlaceholderShellSize, PlaceholderShellSize);
                spriteBatch.Draw(placeholderShellTexture, bounds, null, Color.Yellow, 0.0f, Vector2.Zero, SpriteEffects.None, 0.6f);
            }
            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.DrawString(debugFont, $"P1 {(int)playerOne.Position.X},{(int)playerOne.Position.Y} HP {playerOne.Health} Fuel {playerOne.Fuel:0.0} Shells {playerOne.ShellsRemaining} Reload {ReloadText(playerOne)}", new Vector2(8.0f, 8.0f), Color.White);
            spriteBatch.DrawString(debugFont, $"P2 {(int)playerTwo.Position.X},{(int)playerTwo.Position.Y} HP {playerTwo.Health} Fuel {playerTwo.Fuel:0.0} Shells {playerTwo.ShellsRemaining} Reload {ReloadText(playerTwo)}", new Vector2(8.0f, 24.0f), Color.LightGray);
            spriteBatch.DrawString(debugFont, "P1 WASD/Space  P2 Cursor Keys/Enter  F1 reset  Esc quit", new Vector2(8.0f, Tanx.DesignedHeight - 24.0f), Color.White);
            spriteBatch.End();
        }

        public override void PostUpdate(GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (keyboard.IsKeyDown(Keys.F1) && prevKeyboardState.IsKeyUp(Keys.F1)) ResetResources();
            UpdateTank(playerOne, keyboard, elapsed, Keys.A, Keys.D, Keys.W, Keys.S, Keys.Space);
            UpdateTank(playerTwo, keyboard, elapsed, Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Enter);
            UpdateShells(elapsed);
            UpdateCamera();
            if (keyboard.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape)) game.Exit();
            prevKeyboardState = keyboard;
        }

        private void UpdateTank(Tank tank, KeyboardState keyboard, float elapsed, Keys left, Keys right, Keys forwardKey, Keys reverseKey, Keys fireKey)
        {
            var turn = 0.0f; var drive = 0.0f;
            if (keyboard.IsKeyDown(left)) turn -= 1.0f;
            if (keyboard.IsKeyDown(right)) turn += 1.0f;
            if (keyboard.IsKeyDown(forwardKey)) drive += 1.0f;
            if (keyboard.IsKeyDown(reverseKey)) drive -= 1.0f;
            var terrainFuel = worldMap.GetFuelCostMultiplier(tank.Position);
            var turnCost = Math.Abs(turn) * TurnFuelPerSecond * elapsed * terrainFuel;
            var driveRate = drive < 0.0f ? ReverseFuelPerSecond : ForwardFuelPerSecond;
            var driveCost = Math.Abs(drive) * driveRate * elapsed * terrainFuel;
            var canTurn = turn == 0.0f || tank.Fuel >= turnCost;
            var canDrive = drive == 0.0f || tank.Fuel >= turnCost + driveCost;
            if (canTurn) tank.Heading = MathHelper.WrapAngle(tank.Heading + turn * TankTurnSpeed * elapsed);
            if (canDrive && drive != 0.0f)
            {
                var direction = new Vector2((float)Math.Cos(tank.Heading), (float)Math.Sin(tank.Heading));
                var speed = drive < 0.0f ? ReverseSpeed : TankSpeed;
                MoveTank(tank, direction * drive * speed * worldMap.GetMovementSpeedMultiplier(tank.Position) * elapsed);
                UpdateTankAnimation(tank, elapsed);
            }
            else { tank.AnimationTimer = 0.0f; tank.Frame = 0; }
            if (canTurn && canDrive) tank.Fuel = MathHelper.Max(0.0f, tank.Fuel - turnCost - driveCost);
            else if (canTurn) tank.Fuel = MathHelper.Max(0.0f, tank.Fuel - turnCost);
            tank.ReloadTimer = MathHelper.Max(0.0f, tank.ReloadTimer - elapsed);
            if (keyboard.IsKeyDown(fireKey) && prevKeyboardState.IsKeyUp(fireKey)) TryFireShell(tank);
        }

        private void MoveTank(Tank tank, Vector2 movement)
        {
            var horizontal = tank.Position + new Vector2(movement.X, 0.0f);
            if (CanTankOccupy(tank, horizontal)) tank.Position = horizontal;
            var vertical = tank.Position + new Vector2(0.0f, movement.Y);
            if (CanTankOccupy(tank, vertical)) tank.Position = vertical;
        }

        private bool CanTankOccupy(Tank tank, Vector2 position)
        {
            if (!worldMap.CanOccupyCircle(position, TankCollisionRadius))
                return false;

            var otherTank = ReferenceEquals(tank, playerOne) ? playerTwo : playerOne;
            return Vector2.DistanceSquared(position, otherTank.Position) >
                (TankCollisionRadius + TankCollisionRadius) * (TankCollisionRadius + TankCollisionRadius);
        }

        private Vector2 FindStartingPosition(bool topLeft)
        {
            var corner = topLeft ? new Vector2(TankCollisionRadius, TankCollisionRadius) : new Vector2(worldMap.Bounds.Right - TankCollisionRadius, worldMap.Bounds.Bottom - TankCollisionRadius);
            var result = corner; var bestDistance = float.MaxValue;
            for (var y = 0; y < worldMap.Bounds.Height; y += worldMap.TileHeight)
                for (var x = 0; x < worldMap.Bounds.Width; x += worldMap.TileWidth)
                {
                    var candidate = new Vector2(x + worldMap.TileWidth / 2.0f, y + worldMap.TileHeight / 2.0f);
                    if (!worldMap.CanOccupyCircle(candidate, TankCollisionRadius)) continue;
                    var distance = Vector2.DistanceSquared(candidate, corner);
                    if (distance < bestDistance) { bestDistance = distance; result = candidate; }
                }
            return result;
        }

        private void ResetResources()
        {
            playerOne.Fuel = MaximumFuel; playerTwo.Fuel = MaximumFuel;
            playerOne.ShellsRemaining = StartingShells; playerTwo.ShellsRemaining = StartingShells;
            playerOne.ReloadTimer = 0.0f; playerTwo.ReloadTimer = 0.0f;
        }

        private void TryFireShell(Tank tank)
        {
            if (tank.ShellsRemaining <= 0 || tank.ReloadTimer > 0.0f) return;
            var direction = new Vector2((float)Math.Cos(tank.Heading), (float)Math.Sin(tank.Heading));
            shells.Add(new Shell(StandardShell, tank.Position + direction * (tank.FrameWidth / 2.0f + 4.0f), direction * ShellSpeed));
            tank.ShellsRemaining--; tank.ReloadTimer = StandardShell.ReloadTimeSeconds;
        }

        private void UpdateShells(float elapsed)
        {
            for (var index = shells.Count - 1; index >= 0; index--)
            {
                var shell = shells[index]; shell.Age += elapsed;
                if (shell.Age >= shell.Ammunition.MaxFlightDurationSeconds) { shells.RemoveAt(index); continue; }
                var steps = Math.Max(1, (int)Math.Ceiling(shell.Velocity.Length() * elapsed / 4.0f));
                var stepTime = elapsed / steps; var removed = false;
                for (var step = 0; step < steps; step++)
                {
                    shell.Position += shell.Velocity * stepTime;
                    var terrain = worldMap.GetTerrainAt(shell.Position);
                    if (terrain == TerrainKind.Reflective)
                    {
                        var normal = Math.Abs(shell.Velocity.X) > Math.Abs(shell.Velocity.Y) ? new Vector2(-Math.Sign(shell.Velocity.X), 0.0f) : new Vector2(0.0f, -Math.Sign(shell.Velocity.Y));
                        shell.Velocity = Vector2.Reflect(shell.Velocity, normal); shell.Position += normal * 2.0f;
                        if (++shell.ReflectionCount > 8) { removed = true; break; }
                    }
                    else if (worldMap.BlocksProjectiles(shell.Position)) { removed = true; break; }

                    if (Vector2.DistanceSquared(shell.Position, playerOne.Position) <=
                        (ShellCollisionRadius + TankCollisionRadius) * (ShellCollisionRadius + TankCollisionRadius))
                    {
                        DamageTank(playerOne, shell.Ammunition.Damage);
                        removed = true;
                        break;
                    }

                    if (Vector2.DistanceSquared(shell.Position, playerTwo.Position) <=
                        (ShellCollisionRadius + TankCollisionRadius) * (ShellCollisionRadius + TankCollisionRadius))
                    {
                        DamageTank(playerTwo, shell.Ammunition.Damage);
                        removed = true;
                        break;
                    }
                }
                if (removed) shells.RemoveAt(index);
            }
        }

        private void DamageTank(Tank tank, int damage)
        {
            tank.Health = Math.Max(0, tank.Health - Math.Max(0, damage));
            if (tank.Health == 0)
                game.Exit();
        }

        private void UpdateCamera()
        {
            var maxX = MathHelper.Max(0.0f, worldMap.Bounds.Width - Tanx.DesignedWidth);
            var maxY = MathHelper.Max(0.0f, worldMap.Bounds.Height - Tanx.DesignedHeight);
            cameraPosition.X = MathHelper.Clamp(playerOne.Position.X - Tanx.DesignedWidth / 2.0f, 0.0f, maxX);
            cameraPosition.Y = MathHelper.Clamp(playerOne.Position.Y - Tanx.DesignedHeight / 2.0f, 0.0f, maxY);
        }

        private static void UpdateTankAnimation(Tank tank, float elapsed)
        {
            tank.AnimationTimer += elapsed;
            while (tank.AnimationTimer >= TankFrameDuration) { tank.AnimationTimer -= TankFrameDuration; tank.Frame = (tank.Frame + 1) % TankFrameCount; }
        }

        private static string ReloadText(Tank tank) => tank.ReloadTimer > 0.0f ? tank.ReloadTimer.ToString("0.0") : "READY";

        private static Texture2D LoadTankTwoTexture(ContentManager content, Texture2D fallback)
        {
            try { return content.Load<Texture2D>("Sprites/tank2"); }
            catch (ContentLoadException) { return fallback; }
        }

        private static void DrawTank(SpriteBatch spriteBatch, Tank tank)
        {
            var source = new Rectangle(tank.Frame * tank.FrameWidth, 0, tank.FrameWidth, tank.FrameHeight);
            spriteBatch.Draw(tank.Texture, tank.Position, source, tank.Tint, tank.Heading - MathHelper.Pi, new Vector2(tank.FrameWidth / 2.0f, tank.FrameHeight / 2.0f), 1.0f, SpriteEffects.None, 0.5f);
        }

        public sealed class Tank
        {
            public string Name { get; }
            public bool IsComputerControlled { get; set; }
            public Texture2D Texture { get; }
            public Color Tint { get; }
            public Vector2 Position;
            public float Heading = -MathHelper.PiOver2;
            public float Fuel = MaximumFuel;
            public int Health = StartingHealth;
            public int ShellsRemaining = StartingShells;
            public float ReloadTimer;
            public float AnimationTimer;
            public int Frame;
            public int FrameWidth => Texture.Width / TankFrameCount;
            public int FrameHeight => Texture.Height;
            public Tank(string name, Texture2D texture, Vector2 position, Color tint) { Name = name; Texture = texture; Position = position; Tint = tint; }
        }

        private sealed class Shell
        {
            public Ammunition Ammunition; public Vector2 Position; public Vector2 Velocity; public float Age; public int ReflectionCount;
            public Shell(Ammunition ammunition, Vector2 position, Vector2 velocity) { Ammunition = ammunition; Position = position; Velocity = velocity; }
        }
    }
}

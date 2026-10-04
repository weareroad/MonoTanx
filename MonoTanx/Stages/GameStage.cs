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
        private readonly MatchSimulation simulation;
        private readonly Dictionary<int, PickupVisual> pickupVisuals = new Dictionary<int, PickupVisual>();
        private CameraView cameraView;
        private bool overviewCamera;
        private RenderTarget2D overviewTarget;
        private readonly ScreenShake shake = new ScreenShake();
        private readonly GameAudio audio;
        private readonly EngineSound engineOne;
        private readonly EngineSound engineTwo;
        private bool debugOverlayVisible;

        public Player Player1 => playerOne;
        public Player Player2 => playerTwo;

        public GameStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content, MatchSetup setup = default) : base(game, graphicsDevice, content)
        {
            audio = new GameAudio(content, game.Options.Mute);
            engineOne = audio.CreateEngine(playerTwo: false);
            engineTwo = audio.CreateEngine(playerTwo: true);
            worldMap = new WorldMap(WorldMap.ResolveMapPath(content.RootDirectory, "arena_01.tmx"));
            mapRenderer = new MapRenderer(content, "arena_01.tmx", "arena_01");
            debugFont = content.Load<SpriteFont>("SpriteFonts/dogica");
            var tankTexture = content.Load<Texture2D>("Sprites/tank");
            var tankTwoTexture = LoadTankTwoTexture(content, tankTexture);
            placeholderShellTexture = new Texture2D(graphicsDevice, 1, 1);
            placeholderShellTexture.SetData(new[] { Color.White });
            // a default MatchSetup (both seats human) is not meaningful, so use the usual one-player game
            if (setup.Equals(default(MatchSetup)))
                setup = MatchSetup.OnePlayer;
            playerOne = new Player("Player 1", "Sprites/tank", Color.White, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
                isComputerControlled: setup.PlayerOne == PlayerControl.Computer);
            playerTwo = new Player("Player 2", "Sprites/tank2", Color.LightGray, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
                isComputerControlled: setup.PlayerTwo == PlayerControl.Computer);
            overviewCamera = setup.StartsInOverview;
            playerOne.Texture = tankTexture;
            playerTwo.Texture = tankTwoTexture;
            playerOne.Position = FindStartingPosition(true);
            playerTwo.Position = FindStartingPosition(false);
            playerOne.Heading = HeadingToward(playerOne.Position, playerTwo.Position);
            playerTwo.Heading = HeadingToward(playerTwo.Position, playerOne.Position);
            var pickupSpawns = new List<PickupSpawn>();
            foreach (var spawn in worldMap.PickupSpawns)
            {
                if (string.IsNullOrWhiteSpace(spawn.SpriteAsset))
                    continue;
                try
                {
                    pickupVisuals[spawn.Id] = new PickupVisual(content.Load<Texture2D>(spawn.SpriteAsset));
                    pickupSpawns.Add(spawn);
                }
                catch (ContentLoadException)
                {
                    // Keep malformed/unavailable pickup art from preventing the arena from loading.
                }
            }
            simulation = new MatchSimulation(worldMap, playerOne, playerTwo, pickupSpawns, game.Random.Gameplay);
            UpdateCamera();
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            var shakeOffset = shake.Offset;
            if (overviewCamera)
                DrawOverview(spriteBatch, shakeOffset);
            else
                DrawFollow(spriteBatch, shakeOffset);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(placeholderShellTexture, new Rectangle(0, 0, (int)Tanx.DesignedWidth, HudHeight), new Color(48, 54, 60));
            DrawPlayerOneHud(spriteBatch);
            DrawPlayerTwoHud(spriteBatch);
            spriteBatch.DrawString(debugFont, "P1 WASD/Space  P2 Arrows/Enter  " + (game.Options.Test ? "Esc quit" : "Esc menu"), new Vector2(8.0f, Tanx.DesignedHeight - 40.0f), Color.White);
            spriteBatch.DrawString(debugFont, "F1 refill  F2/F4 P2/P1 cpu  F3 view  F5 debug", new Vector2(8.0f, Tanx.DesignedHeight - 24.0f), Color.White);
            if (debugOverlayVisible)
                DrawDebugOverlay(spriteBatch);
            spriteBatch.End();
        }

        public override void PostUpdate(GameTime gameTime) { }

        public override void OnLeave()
        {
            engineOne.Dispose();
            engineTwo.Dispose();
        }

        public override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            var elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            debugOverlayVisible = keyboard.IsKeyDown(Keys.F5);
            if (debugOverlayVisible)
            {
                // paused: the engines fade out
                engineOne.Update(TankMotion.Idle, active: false, elapsed);
                engineTwo.Update(TankMotion.Idle, active: false, elapsed);
                prevKeyboardState = keyboard;
                return;
            }
            if (keyboard.IsKeyDown(Keys.F1) && prevKeyboardState.IsKeyUp(Keys.F1)) ResetPlayerOneResources();
            if (keyboard.IsKeyDown(Keys.F2) && prevKeyboardState.IsKeyUp(Keys.F2)) ToggleControl(Seat.Two);
            if (keyboard.IsKeyDown(Keys.F4) && prevKeyboardState.IsKeyUp(Keys.F4)) ToggleControl(Seat.One);
            if (keyboard.IsKeyDown(Keys.F3) && prevKeyboardState.IsKeyUp(Keys.F3)) overviewCamera = !overviewCamera;
            var playerOneBefore = (playerOne.Position, playerOne.Heading);
            var playerTwoBefore = (playerTwo.Position, playerTwo.Heading);
            foreach (var seat in Seats)
            {
                var tank = TankOf(seat);
                if (tank.IsComputerControlled)
                    UpdateComputerPlayer(seat, elapsed);
                else
                    UpdateTank(tank, keyboard, elapsed, KeysOf(seat));
            }
            foreach (var seat in Seats)
                if (TankOf(seat).IsComputerControlled)
                    UpdateComputerFiring(seat, elapsed);
            // judged before shells can knock a tank about
            engineOne.Update(TankMotionClassifier.Classify(playerOneBefore.Position, playerOneBefore.Heading, playerOne.Position, playerOne.Heading), active: true, elapsed);
            engineTwo.Update(TankMotionClassifier.Classify(playerTwoBefore.Position, playerTwoBefore.Heading, playerTwo.Position, playerTwo.Heading), active: true, elapsed);
            simulation.StepShells(elapsed);
            UpdatePickupAnimations(elapsed);
            simulation.CollectPickups();
            PlayEvents();
            UpdateCamera();
            shake.Update(elapsed, game.Random.Cosmetic);
            if (keyboard.IsKeyDown(Keys.Escape) && prevKeyboardState.IsKeyUp(Keys.Escape))
            {
                // --test quits straight away; otherwise go back to the home screen
                if (game.Options.Test)
                    game.Exit();
                else
                    game.ChangeStage(new HomeStage(game, graphicsDevice, content));
            }
            prevKeyboardState = keyboard;
        }

        private void UpdateTank(Player tank, KeyboardState keyboard, float elapsed, SeatKeys keys)
        {
            var command = keys.ToCommand(keyboard, prevKeyboardState);
            if (TankMovement.ApplyInput(worldMap, tank, OtherTank(tank), command, elapsed))
                UpdateTankAnimation(tank, elapsed);
            else { tank.AnimationTimer = 0.0f; tank.Frame = 0; }
            TickReload(tank, elapsed);
            if (command.Fire) TryFireShell(tank);
        }

        private static readonly Seat[] Seats = { Seat.One, Seat.Two };

        private Player TankOf(Seat seat) => seat == Seat.One ? playerOne : playerTwo;

        private ComputerController ControllerOf(Seat seat) => simulation.ControllerOf(seat);

        private static SeatKeys KeysOf(Seat seat) => seat == Seat.One ? SeatKeys.PlayerOne : SeatKeys.PlayerTwo;

        private Player OtherTank(Player tank) => ReferenceEquals(tank, playerOne) ? playerTwo : playerOne;

        // Who controls each seat right now (it can change in play with F2 and F4).
        private MatchSetup CurrentSetup => new MatchSetup(
            playerOne.IsComputerControlled ? PlayerControl.Computer : PlayerControl.Human,
            playerTwo.IsComputerControlled ? PlayerControl.Computer : PlayerControl.Human);

        // The tank the follow camera tracks, and the one whose gun shakes the screen:
        // the first human seat's. With no human there is nobody to follow or to shake for.
        private Player FollowedTank => TankOf(CurrentSetup.FollowSeat);

        // Turns what happened in the simulation into sound, shake and the end of the match.
        private void PlayEvents()
        {
            foreach (var matchEvent in simulation.Events)
            {
                var playerTwoSeat = matchEvent.Seat == Seat.Two;
                switch (matchEvent.Kind)
                {
                    case MatchEventKind.ShellFired:
                        audio.Play(SoundCue.Fire, playerTwo: playerTwoSeat);
                        if (CurrentSetup.HumanCount > 0 && ReferenceEquals(simulation.TankOf(matchEvent.Seat.Value), FollowedTank)) StartShake(Tuning.Shake.FireDuration, Tuning.Shake.FireMagnitude);
                        break;
                    case MatchEventKind.ShellReflected:
                        audio.Play(SoundCue.Ping);
                        break;
                    case MatchEventKind.ShellHitTerrain:
                        audio.Play(SoundCue.Crump);
                        break;
                    case MatchEventKind.TankHit:
                        audio.Play(SoundCue.Explosion);
                        StartShake(Tuning.Shake.HitDuration, Tuning.Shake.HitMagnitude);
                        break;
                    case MatchEventKind.PickupCollected:
                        audio.Play(SoundCue.Pickup);
                        break;
                    case MatchEventKind.TankDestroyed:
                        game.Exit();
                        break;
                }
            }
            simulation.ClearEvents();
        }

        private void UpdatePickupAnimations(float elapsed)
        {
            foreach (var pickup in simulation.Pickups)
            {
                if (!pickup.Active) continue;
                var visual = pickupVisuals[pickup.Spawn.Id];
                visual.AnimationTimer += elapsed;
                while (visual.AnimationTimer >= Tuning.Presentation.PickupFrameSeconds)
                {
                    visual.AnimationTimer -= Tuning.Presentation.PickupFrameSeconds;
                    visual.Frame = (visual.Frame + 1) % Tuning.Presentation.PickupFrameCount;
                }
            }
        }

        // The computer's movement for this seat: reload first, then the controller's
        // command through the usual movement rules (it has no drive animation).
        private void UpdateComputerPlayer(Seat seat, float elapsed)
        {
            var tank = TankOf(seat);
            TickReload(tank, elapsed);
            TankMovement.ApplyInput(worldMap, tank, OtherTank(tank), ControllerOf(seat).PlanMove(elapsed, simulation.Pickups), elapsed);
        }

        // The computer's aiming and firing, after both seats have moved.
        private void UpdateComputerFiring(Seat seat, float elapsed)
        {
            var tank = TankOf(seat);
            var controller = ControllerOf(seat);
            var command = controller.PlanAim(elapsed);
            TankMovement.ApplyInput(worldMap, tank, OtherTank(tank), command, elapsed);
            if (command.Fire && TryFireShell(tank))
                controller.ShotFired();
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

        // Flips a seat between computer and human control. Clears the computer's
        // working state so it starts afresh when it takes control back.
        private void ToggleControl(Seat seat)
        {
            var tank = TankOf(seat);
            tank.IsComputerControlled = !tank.IsComputerControlled;
            ControllerOf(seat).Reset();
            tank.AnimationTimer = 0.0f;
            tank.Frame = 0;
        }

        private void ResetPlayerOneResources()
        {
            playerOne.ResetFuelAndAmmunition();
        }

        private bool TryFireShell(Player tank)
        {
            var muzzleOffset = tank.Texture.Width / Tuning.Presentation.TankFrameCount / 2.0f + Tuning.Presentation.MuzzleClearance;
            if (!tank.TryFire(muzzleOffset, out var launch)) return false;
            simulation.Launch(tank, launch);
            return true;
        }

        private void TickReload(Player tank, float elapsed)
        {
            if (tank.TickReload(elapsed))
                audio.Play(SoundCue.Reload, playerTwo: ReferenceEquals(tank, playerTwo));
        }

        private void StartShake(float duration, float magnitude)
        {
            shake.Start(duration, magnitude);
        }

        private void UpdateCamera()
        {
            var viewport = new Vector2(Tanx.DesignedWidth, Tanx.DesignedHeight - HudHeight);
            cameraView = overviewCamera
                ? CameraView.Overview(worldMap.Bounds, viewport)
                : CameraView.Follow(FollowedTank.Position, worldMap.Bounds, viewport);
        }

        private static void UpdateTankAnimation(Player tank, float elapsed)
        {
            tank.AnimationTimer += elapsed;
            while (tank.AnimationTimer >= Tuning.Presentation.TankFrameSeconds) { tank.AnimationTimer -= Tuning.Presentation.TankFrameSeconds; tank.Frame = (tank.Frame + 1) % Tuning.Presentation.TankFrameCount; }
        }

        private static string ReloadText(Player tank) => tank.ReloadTimer > 0.0f ? tank.ReloadTimer.ToString("0.0") : "READY";

        private void DrawFollow(SpriteBatch spriteBatch, Vector2 shakeOffset)
        {
            // Snap the world translation to whole pixels. A fractional offset makes
            // point sampling land on tile-atlas texel boundaries and pick up
            // neighbouring (empty) atlas texels, which shows as thin dark seams.
            var worldOffset = new Vector2(
                (float)Math.Round(cameraView.Offset.X + shakeOffset.X),
                (float)Math.Round(cameraView.Offset.Y + HudHeight + shakeOffset.Y));
            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(worldOffset.X, worldOffset.Y, 0.0f));
            DrawWorld(spriteBatch);
            spriteBatch.End();
        }

        // Draws the whole arena at full size into an offscreen target, then
        // scales that into the playfield. (Scaling the tile atlas directly would
        // bring back the neighbouring-tile seams.)
        private void DrawOverview(SpriteBatch spriteBatch, Vector2 shakeOffset)
        {
            var bounds = worldMap.Bounds;
            if (overviewTarget == null || overviewTarget.Width != bounds.Width || overviewTarget.Height != bounds.Height)
                overviewTarget = new RenderTarget2D(graphicsDevice, bounds.Width, bounds.Height);

            var previousTargets = graphicsDevice.GetRenderTargets();
            graphicsDevice.SetRenderTarget(overviewTarget);
            graphicsDevice.Clear(Color.Black);
            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp);
            DrawWorld(spriteBatch);
            spriteBatch.End();
            graphicsDevice.SetRenderTargets(previousTargets);
            graphicsDevice.Clear(Color.Black); // the target's contents may not survive the switch

            var destination = new Rectangle(
                (int)Math.Round(cameraView.Offset.X + shakeOffset.X),
                (int)Math.Round(cameraView.Offset.Y + HudHeight + shakeOffset.Y),
                (int)Math.Round(bounds.Width * cameraView.Zoom),
                (int)Math.Round(bounds.Height * cameraView.Zoom));
            spriteBatch.Begin(samplerState: Tuning.Presentation.OverviewSmoothing ? SamplerState.LinearClamp : SamplerState.PointClamp);
            spriteBatch.Draw(overviewTarget, destination, Color.White);
            spriteBatch.End();
        }

        private void DrawWorld(SpriteBatch spriteBatch)
        {
            mapRenderer.Draw(spriteBatch);
            DrawTank(spriteBatch, playerOne);
            DrawTank(spriteBatch, playerTwo);
            foreach (var pickup in simulation.Pickups)
            {
                if (!pickup.Active) continue;
                var visual = pickupVisuals[pickup.Spawn.Id];
                var frameWidth = visual.Texture.Width / Tuning.Presentation.PickupFrameCount;
                var frameHeight = visual.Texture.Height;
                var source = new Rectangle(visual.Frame * frameWidth, 0, frameWidth, frameHeight);
                var origin = new Vector2(frameWidth / 2.0f, frameHeight / 2.0f);
                spriteBatch.Draw(visual.Texture, pickup.Spawn.Position, source, Color.White, 0.0f, origin, 1.0f, SpriteEffects.None, 0.45f);
            }
            foreach (var shell in simulation.Shells)
            {
                var offset = Tuning.Presentation.PlaceholderShellSize / 2;
                var bounds = new Rectangle((int)shell.Position.X - offset, (int)shell.Position.Y - offset, Tuning.Presentation.PlaceholderShellSize, Tuning.Presentation.PlaceholderShellSize);
                spriteBatch.Draw(placeholderShellTexture, bounds, null, Color.Yellow, 0.0f, Vector2.Zero, SpriteEffects.None, 0.6f);
            }
        }

        private void DrawPlayerOneHud(SpriteBatch spriteBatch)
        {
            var panelWidth = (int)Tanx.DesignedWidth * 3 / 4;
            spriteBatch.DrawString(debugFont, "P1", new Vector2(8.0f, 4.0f), Color.White);
            DrawHealthBar(spriteBatch, new Rectangle(48, 8, panelWidth - 160, 12), playerOne);
            spriteBatch.DrawString(debugFont, $"{playerOne.Health}%", new Vector2(panelWidth - 112, 4.0f), Color.White);
            spriteBatch.DrawString(debugFont, $"Shells {playerOne.RemainingAmmunition}", new Vector2(8.0f, 25.0f), Color.White);
            spriteBatch.DrawString(debugFont, playerOne.IsComputerControlled ? "CPU (F4)" : "HUMAN (F4)", new Vector2(130.0f, 25.0f), Color.White);
            spriteBatch.DrawString(debugFont, playerOne.ReloadTimer > 0.0f ? "!" : "", new Vector2(panelWidth - 32, 22.0f), Color.White);
            spriteBatch.DrawString(debugFont, overviewCamera ? "VIEW: OVERVIEW (F3)" : "VIEW: FOLLOW (F3)", new Vector2(260.0f, 30.0f), Color.White);
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
            var p1Tile = worldMap.WorldToTile(playerOne.Position);
            var p2Tile = worldMap.WorldToTile(playerTwo.Position);
            var lines = new List<string>
            {
                "DEBUG (hold F5)",
                $"P1 pos {playerOne.Position.X:0.00},{playerOne.Position.Y:0.00} tile {p1Tile.X},{p1Tile.Y}",
                $"P1 dir {playerOne.Heading:0.000} rad / {MathHelper.ToDegrees(playerOne.Heading):0.0} deg",
                $"P2 pos {playerTwo.Position.X:0.00},{playerTwo.Position.Y:0.00} tile {p2Tile.X},{p2Tile.Y}",
                $"P2 dir {playerTwo.Heading:0.000} rad / {MathHelper.ToDegrees(playerTwo.Heading):0.0} deg"
            };
            // the computer's working state, for each seat it controls
            foreach (var seat in Seats)
            {
                if (!TankOf(seat).IsComputerControlled)
                    continue;
                var c = ControllerOf(seat);
                var name = seat == Seat.One ? "P1" : "P2";
                var mode = c.Mode(simulation.Pickups).ToString().ToUpperInvariant();
                lines.Add($"AI {name} {mode} route {c.RouteIndex}/{c.RouteLength}");
                lines.Add($"AI {name} fire {c.FireTimer:0.00} retaliate {c.RetaliationTimer:0.00}");
            }
            lines.Add($"Seed {game.Random.Seed}  pickups {worldMap.PickupSpawns.Count}");

            var panel = new Rectangle(8, HudHeight + 8, 360, lines.Count * 16);
            spriteBatch.Draw(placeholderShellTexture, panel, Color.Black * 0.78f);
            for (var index = 0; index < lines.Count; index++)
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

        // How a pickup is drawn: its sprite and animation, kept apart from the pickup's rules state.
        private sealed class PickupVisual
        {
            public Texture2D Texture { get; }
            public float AnimationTimer;
            public int Frame;
            public PickupVisual(Texture2D texture) { Texture = texture; }
        }
    }
}

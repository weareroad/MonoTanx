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
        private readonly MatchSession session;
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

        public GameStage(Tanx game, GraphicsDevice graphicsDevice, ContentManager content, MatchSetup setup) : base(game, graphicsDevice, content)
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
            playerOne = new Player("Player 1", "Sprites/tank", Color.White, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
                isComputerControlled: setup.PlayerOne == PlayerControl.Computer);
            playerTwo = new Player("Player 2", "Sprites/tank2", Color.LightGray, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
                isComputerControlled: setup.PlayerTwo == PlayerControl.Computer);
            overviewCamera = setup.StartsInOverview;
            playerOne.Texture = tankTexture;
            playerTwo.Texture = tankTwoTexture;
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
            simulation = new MatchSimulation(worldMap, playerOne, playerTwo, pickupSpawns, game.Random.Gameplay,
                new SimulationSettings(MuzzleOffsetOf(playerOne), MuzzleOffsetOf(playerTwo)),
                game.Random.CreateStream("ai-1"), game.Random.CreateStream("ai-2"));
            simulation.PlaceAtStart();
            session = new MatchSession(simulation);
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
            DrawMatchStatus(spriteBatch);
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
            // the tanks only act while a round is live; in the pauses they stand still and the engines idle
            var stepped = session.State.Phase == MatchPhase.Playing;
            session.Step(elapsed, CommandOf(Seat.One, keyboard), CommandOf(Seat.Two, keyboard));
            foreach (var seat in Seats)
            {
                var tank = TankOf(seat);
                if (!tank.IsComputerControlled)
                {
                    if (stepped && simulation.Moved(seat)) UpdateTankAnimation(tank, elapsed);
                    else { tank.AnimationTimer = 0.0f; tank.Frame = 0; }
                }
            }
            engineOne.Update(stepped ? simulation.Motion(Seat.One) : TankMotion.Idle, active: true, elapsed);
            engineTwo.Update(stepped ? simulation.Motion(Seat.Two) : TankMotion.Idle, active: true, elapsed);
            UpdatePickupAnimations(elapsed);
            PlayEvents();
            if (UpdateMatchOver()) return;
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

        // The human's command for a seat, or null when the computer controls it.
        private TankCommand? CommandOf(Seat seat, KeyboardState keyboard)
        {
            return TankOf(seat).IsComputerControlled ? null : KeysOf(seat).ToCommand(keyboard, prevKeyboardState);
        }

        private static float MuzzleOffsetOf(Player tank)
        {
            return tank.Texture.Width / Tuning.Presentation.TankFrameCount / 2.0f + Tuning.Presentation.MuzzleClearance;
        }

        private static readonly Seat[] Seats = { Seat.One, Seat.Two };

        private Player TankOf(Seat seat) => seat == Seat.One ? playerOne : playerTwo;

        private ComputerController ControllerOf(Seat seat) => simulation.ControllerOf(seat);

        private static SeatKeys KeysOf(Seat seat) => seat == Seat.One ? SeatKeys.PlayerOne : SeatKeys.PlayerTwo;

        // Who controls each seat right now (it can change in play with F2 and F4).
        private MatchSetup CurrentSetup => new MatchSetup(
            playerOne.IsComputerControlled ? PlayerControl.Computer : PlayerControl.Human,
            playerTwo.IsComputerControlled ? PlayerControl.Computer : PlayerControl.Human);

        // The tank the follow camera tracks, and the one whose gun shakes the screen:
        // the first human seat's. With no human there is nobody to follow or to shake for.
        private Player FollowedTank => TankOf(CurrentSetup.FollowSeat);

        // Turns what happened in the simulation into sound and shake.
        private void PlayEvents()
        {
            foreach (var matchEvent in simulation.Events)
            {
                var playerTwoSeat = matchEvent.Seat == Seat.Two;
                switch (matchEvent.Kind)
                {
                    case MatchEventKind.ReloadReady:
                        audio.Play(SoundCue.Reload, playerTwo: playerTwoSeat);
                        break;
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
                }
            }
            simulation.ClearEvents();
            session.State.ClearEvents();
        }

        // Once a match is over its result stays up for a moment. Then a demo starts its
        // next match by itself and a game with a human moves on to the end screen.
        // True when the stage has been left.
        private bool UpdateMatchOver()
        {
            if (session.State.Phase != MatchPhase.MatchOver || session.State.PhaseTimer < Tuning.Match.MatchOverSeconds)
                return false;
            if (CurrentSetup.HumanCount == 0)
            {
                session.StartNewMatch();
                return false;
            }
            game.ChangeStage(new EndStage(game, graphicsDevice, content, CurrentSetup, MatchResult.From(session.State)));
            return true;
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

        // Flips a seat between computer and human control. Clears the computer's
        // working state so it starts afresh when it takes control back.
        private void ToggleControl(Seat seat)
        {
            var tank = TankOf(seat);
            simulation.SetComputerControlled(seat, !tank.IsComputerControlled);
            tank.AnimationTimer = 0.0f;
            tank.Frame = 0;
        }

        private void ResetPlayerOneResources()
        {
            simulation.ResetFuelAndAmmunition(Seat.One);
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
            if (playerOne.Health > 0) DrawTank(spriteBatch, playerOne);
            if (playerTwo.Health > 0) DrawTank(spriteBatch, playerTwo);
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

        // The score and round in the middle of the playfield's top edge, and a banner
        // for the countdown and the result of a round or the match.
        private void DrawMatchStatus(SpriteBatch spriteBatch)
        {
            var state = session.State;
            var setup = CurrentSetup;
            var score = $"{setup.LabelOf(Seat.One)} {state.ScoreOf(Seat.One)} - {state.ScoreOf(Seat.Two)} {setup.LabelOf(Seat.Two)}   Round {state.Round}";
            DrawCentred(spriteBatch, score, HudHeight + 6.0f, 1.0f, Color.White, panel: true);
            var banner = BannerText(state, setup);
            if (banner != null)
                DrawCentred(spriteBatch, banner.Value.Text, HudHeight + (Tanx.DesignedHeight - HudHeight) / 2.0f - 20.0f, banner.Value.Scale, Color.White, panel: true);
        }

        private static (string Text, float Scale)? BannerText(MatchState state, MatchSetup setup)
        {
            switch (state.Phase)
            {
                case MatchPhase.Countdown:
                    return (Math.Ceiling(state.CountdownRemaining).ToString("0"), 4.0f);
                case MatchPhase.RoundOver:
                    return (state.RoundWinner == null ? "Draw" : setup.LabelOf(state.RoundWinner.Value) + " scores", 2.0f);
                case MatchPhase.MatchOver:
                    return (setup.LabelOf(state.Winner.Value) + " wins the match", 2.0f);
                default:
                    return null;
            }
        }

        private void DrawCentred(SpriteBatch spriteBatch, string text, float y, float scale, Color color, bool panel)
        {
            var size = debugFont.MeasureString(text) * scale;
            var position = new Vector2((float)Math.Round((Tanx.DesignedWidth - size.X) / 2.0f), (float)Math.Round(y));
            if (panel)
                spriteBatch.Draw(placeholderShellTexture, new Rectangle((int)position.X - 8, (int)position.Y - 6, (int)size.X + 16, (int)size.Y + 12), Color.Black * 0.6f);
            spriteBatch.DrawString(debugFont, text, position, color, 0.0f, Vector2.Zero, scale, SpriteEffects.None, 0.0f);
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
                lines.Add($"AI {name} fire {c.FireTimer:0.00} retaliate {c.RetaliationTimer:0.00} aim +-{c.AimError:0.00}");
            }
            lines.Add($"Match {session.State.Phase} round {session.State.Round} {session.State.PhaseTimer:0.0}s score {session.State.ScoreOf(Seat.One)}-{session.State.ScoreOf(Seat.Two)}");
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

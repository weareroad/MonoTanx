using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoTanx.Core
{
    // What the computer is trying to do right now, for the debug overlay.
    public enum ComputerMode
    {
        Pickup,
        Flee,
        Long,
        Combat,

        // Dodging a shell that is about to hit.
        Evade,

        // Backing away after getting stuck.
        Recover,

        // Moving to a new firing position while the gun cools down.
        Relocate
    }

    // The computer opponent for one seat. It speaks of "self" and "the opponent",
    // never of a particular player, and produces TankCommands that the stage
    // applies through the usual movement and firing rules. It has two phases per
    // update, as the computer always had: PlanMove while the seat is updated, then
    // PlanAim in the firing pass after both seats have moved. Contains no input,
    // rendering or Game dependencies so it can be tested directly.
    public sealed class ComputerController
    {
        private readonly WorldMap map;
        private readonly Player self;
        private readonly Player opponent;
        private readonly Random random;
        private readonly GameSettings settings;
        private readonly List<Point> route = new List<Point>();
        private int routeIndex;
        private int pickupTargetId = -1;
        private readonly List<Point> relocationRoute = new List<Point>();
        private int relocationIndex;
        private bool relocationPending;
        private bool relocating;
        private bool evading;
        private TankCommand lastEvasion;
        private float lastEvasionAge = float.MaxValue;
        private readonly Dictionary<Shell, bool> noticedShells = new Dictionary<Shell, bool>();
        private readonly Dictionary<Shell, float> noticeDraws = new Dictionary<Shell, float>();
        private readonly HashSet<(Shell, ComputerDecisionKind)> reportedShells = new HashSet<(Shell, ComputerDecisionKind)>();
        private readonly List<ComputerDecision> decisions = new List<ComputerDecision>();
        private Player scratch;
        private bool longRangePursuit;
        private float longRangeHeading;
        private float fireTimer;
        private float retaliationTimer;
        private float aimError;
        private bool aimErrorDrawn;
        private Vector2 lastOpponentPosition;

        // Stuck detection: how long it has been commanded to drive, from where, and
        // the recovery that follows.
        private float drivingSeconds;
        private Vector2 drivingStart;
        private float recoveryTimer;
        private int recoverySide = 1;
        private float secondsSinceRecovery = float.MaxValue;
        private int repeatedStuck;
        private float routePursuitTimer;
        private float ignorePickupTimer;
        private int ignoredPickupId = -1;

        // The random stream is this seat's own (RandomStreams.CreateStream("ai-1") or
        // ("ai-2")). With none, the computer makes no aiming error.
        public ComputerController(WorldMap map, Player self, Player opponent, Random random = null, GameSettings settings = null)
        {
            this.settings = settings ?? new GameSettings();
            this.random = random;
            this.map = map;
            this.self = self;
            this.opponent = opponent;
            lastOpponentPosition = opponent.Position;
        }

        public int RouteIndex => routeIndex;
        public int RouteLength => route.Count;
        public float FireTimer => fireTimer;
        public float RetaliationTimer => retaliationTimer;
        public bool LongRangePursuit => longRangePursuit;

        // How many times it has been stuck, and whether it is backing away now.
        public int StuckCount { get; private set; }
        public bool Recovering => recoveryTimer > 0.0f;

        // What it has decided since the log last took it (the stage's log reads these; nothing in the
        // computer depends on them).
        public IReadOnlyList<ComputerDecision> TakeDecisions()
        {
            var taken = decisions.ToArray();
            decisions.Clear();
            return taken;
        }

        // Whether it is dodging a shell this update.
        public bool Evading => evading;

        // Whether it is moving to a new firing position after a shot.
        public bool Relocating => relocating;

        // The tile it is moving to while relocating, for the tests and the overlay.
        public Point? RelocationGoal => relocating && relocationRoute.Count > 0 ? relocationRoute[relocationRoute.Count - 1] : (Point?)null;

        // The largest error, in radians, the shot being prepared may have: it fires as
        // soon as the tank points within this of the opponent. Drawn per shot.
        public float AimError => aimError;

        // What the last PlanMove asked for, for the overlay and the tests.
        public TankCommand LastMove { get; private set; }

        // What the computer would be doing now, in the order it decides.
        public ComputerMode Mode(IReadOnlyList<PickupState> pickups)
        {
            if (evading) return ComputerMode.Evade;
            if (Recovering) return ComputerMode.Recover;
            if (relocating && self.RemainingAmmunition > 0) return ComputerMode.Relocate;
            if (FindPickupTarget(pickups) != null) return ComputerMode.Pickup;
            if (self.RemainingAmmunition == 0) return ComputerMode.Flee;
            return longRangePursuit ? ComputerMode.Long : ComputerMode.Combat;
        }

        // Called by the stage when the tank has been hit: turn on the attacker and
        // forget the fire cooldown.
        public void Hit()
        {
            retaliationTimer = settings.Get(SettingKeys.AiRetaliationSeconds);
            fireTimer = 0.0f;
            relocationPending = false;
            relocating = false; // the cooldown is cancelled, so there is nothing left to move for
        }

        // Called by the stage after a successful shot, to start the fire cooldown.
        public void ShotFired()
        {
            fireTimer = self.ComputerFireCooldownSeconds + self.ComputerReactionDelaySeconds;
            aimErrorDrawn = false; // the next shot gets its own error
            relocationPending = true; // and it moves somewhere new while the gun cools down
        }

        // Called when control changes, so the computer starts afresh.
        public void Reset()
        {
            route.Clear();
            routeIndex = 0;
            relocationRoute.Clear();
            relocationIndex = 0;
            relocationPending = false;
            relocating = false;
            evading = false;
            lastEvasionAge = float.MaxValue;
            noticedShells.Clear();
            noticeDraws.Clear();
            reportedShells.Clear();
            decisions.Clear();
            pickupTargetId = -1;
            longRangePursuit = false;
            fireTimer = 0.0f;
            retaliationTimer = 0.0f;
            aimErrorDrawn = false;
            drivingSeconds = 0.0f;
            recoveryTimer = 0.0f;
            secondsSinceRecovery = float.MaxValue;
            repeatedStuck = 0;
            routePursuitTimer = 0.0f;
            ignorePickupTimer = 0.0f;
            ignoredPickupId = -1;
            lastOpponentPosition = opponent.Position;
        }

        // The movement for this update. Does nothing at all when the tank cannot
        // afford both the turn and the drive.
        public TankCommand PlanMove(float elapsed, IReadOnlyList<PickupState> pickups, IReadOnlyList<Shell> shells = null)
        {
            secondsSinceRecovery += elapsed;
            lastEvasionAge += elapsed;
            routePursuitTimer = Math.Max(0.0f, routePursuitTimer - elapsed);
            ignorePickupTimer = Math.Max(0.0f, ignorePickupTimer - elapsed);

            // Avoiding being hit comes before everything else (while it has fuel to move)
            var evasion = shells == null ? null : PlanEvasion(elapsed, shells);
            evading = evasion.HasValue;
            if (evading)
            {
                drivingSeconds = 0.0f;
                retaliationTimer = Math.Max(0.0f, retaliationTimer - elapsed);
                LastMove = evasion.Value;
                return LastMove;
            }
            if (recoveryTimer > 0.0f)
            {
                // backing away: nothing else is decided until it is over
                recoveryTimer = Math.Max(0.0f, recoveryTimer - elapsed);
                retaliationTimer = Math.Max(0.0f, retaliationTimer - elapsed);
                LastMove = Affordable(new TankCommand(recoverySide, -1.0f), elapsed);
                if (recoveryTimer <= 0.0f)
                    EndRecovery();
                return LastMove;
            }

            var command = PlanMoveCore(elapsed, pickups);
            if (IsStuckAfter(command, elapsed))
            {
                StartRecovery();
                command = Affordable(new TankCommand(recoverySide, -1.0f), elapsed);
            }
            LastMove = command;
            return LastMove;
        }

        // Judges progress: while it is being told to drive, has it moved? Time spent not
        // driving (holding with a clear view, turning on the spot, out of fuel) does not
        // count, so waiting is not mistaken for being stuck.
        private bool IsStuckAfter(TankCommand command, float elapsed)
        {
            if (command.Drive == 0.0f)
            {
                drivingSeconds = 0.0f;
                return false;
            }
            if (drivingSeconds == 0.0f)
                drivingStart = self.Position;
            drivingSeconds += elapsed;
            if (drivingSeconds < settings.Get(SettingKeys.AiStuckWindowSeconds))
                return false;
            var moved = Vector2.Distance(drivingStart, self.Position);
            drivingSeconds = 0.0f;
            return moved < settings.Get(SettingKeys.AiStuckMinimumDistance);
        }

        private void StartRecovery()
        {
            StuckCount++;
            decisions.Add(new ComputerDecision(ComputerDecisionKind.Stuck, self.Position, self.Fuel));
            repeatedStuck = secondsSinceRecovery <= Tuning.Ai.StuckRepeatSeconds ? repeatedStuck + 1 : 1;
            // back away turning to a side picked from the seat's stream; without one, alternate sides
            recoverySide = random != null ? (random.Next(2) == 0 ? -1 : 1) : -recoverySide;
            recoveryTimer = settings.Get(SettingKeys.AiStuckRecoverySeconds);
            drivingSeconds = 0.0f;
        }

        // Backed away: plan afresh from here, and do something different if this keeps happening.
        private void EndRecovery()
        {
            secondsSinceRecovery = 0.0f;
            route.Clear();
            routeIndex = 0;
            if (longRangePursuit)
            {
                // a straight line at the opponent ran into something: follow a route for a while
                longRangePursuit = false;
                routePursuitTimer = Tuning.Ai.RoutePursuitSeconds;
            }
            if (pickupTargetId >= 0 && repeatedStuck >= 2)
            {
                ignoredPickupId = pickupTargetId;
                ignorePickupTimer = Tuning.Ai.PickupIgnoreSeconds;
            }
            pickupTargetId = -1;
        }

        private TankCommand PlanMoveCore(float elapsed, IReadOnlyList<PickupState> pickups)
        {
            retaliationTimer = Math.Max(0.0f, retaliationTimer - elapsed);
            if (retaliationTimer > 0.0f)
                return Affordable(new TankCommand(TurnToward(HeadingToward(self.Position, opponent.Position)), 0.0f), elapsed);

            var pickupTarget = FindPickupTarget(pickups);
            if (pickupTarget != null)
                return PlanPickupSeek(pickupTarget, elapsed);

            if (self.RemainingAmmunition == 0)
            {
                // With no ammunition, survival takes priority over positioning:
                // continuously steer and drive away from the opponent. Collision
                // resolution will slide around map obstacles.
                return Affordable(new TankCommand(TurnToward(HeadingToward(opponent.Position, self.Position)), 1.0f), elapsed);
            }

            var relocation = PlanRelocation(elapsed);
            if (relocation.HasValue)
                return relocation.Value;

            var distance = Vector2.Distance(self.Position, opponent.Position);
            var longRangeThreshold = map.Bounds.Width * self.LongRangePursuitDistanceFraction;
            if (distance <= HoldDistance && map.HasLineOfSight(self.Position, opponent.Position))
                return TankCommand.None; // engaged: close enough to hit, so stop and shoot
            if (distance > longRangeThreshold && routePursuitTimer <= 0.0f)
            {
                if (!longRangePursuit)
                {
                    longRangePursuit = true;
                    longRangeHeading = HeadingToward(self.Position, opponent.Position);
                    route.Clear();
                    routeIndex = 0;
                }
                return Affordable(new TankCommand(TurnToward(longRangeHeading), 1.0f), elapsed);
            }

            longRangePursuit = false;
            if (Vector2.DistanceSquared(lastOpponentPosition, opponent.Position) > Tuning.Ai.RouteRebuildDistance * Tuning.Ai.RouteRebuildDistance || routeIndex >= route.Count)
            {
                BuildCombatRoute(Math.Max(0, repeatedStuck - 1));
                lastOpponentPosition = opponent.Position;
            }

            return FollowRoute(elapsed, driveOnlyWhenFacing: true);
        }

        // The aiming and firing for this update, after both seats have moved:
        // a turn toward the opponent while it is off target, then Fire once it is
        // lined up with a clear shot, the cooldown is over and the gun is loaded.
        public TankCommand PlanAim(float elapsed)
        {
            fireTimer = Math.Max(0.0f, fireTimer - elapsed);
            if (fireTimer > 0.0f || self.ReloadTimer > 0.0f)
                return TankCommand.None;

            // no shot without a view, and none from too far for its aim to hit: keep closing in instead
            var distance = Vector2.Distance(self.Position, opponent.Position);
            if (distance > FireDistance || !map.HasLineOfSight(self.Position, opponent.Position))
                return TankCommand.None;

            // this shot's window (its largest error), drawn once and kept until it is fired
            if (!aimErrorDrawn)
            {
                aimError = DrawAimError();
                aimErrorDrawn = true;
            }
            var desiredHeading = HeadingToward(self.Position, opponent.Position);
            if (Math.Abs(MathHelper.WrapAngle(desiredHeading - self.Heading)) > aimError)
            {
                // Only turn to aim once it has stopped to shoot. While it is still closing in, or backing
                // away, the movement phase is steering it and the two would undo each other.
                if (Recovering || evading || distance > HoldDistance)
                    return TankCommand.None;
                var turn = new TankCommand(TurnToward(desiredHeading), 0.0f);
                return TankMovement.FuelCost(map, self, turn, elapsed) <= self.Fuel ? turn : TankCommand.None;
            }

            return new TankCommand(0.0f, 0.0f, fire: true);
        }

        private TankCommand PlanPickupSeek(PickupState target, float elapsed)
        {
            if (pickupTargetId != target.Spawn.Id || routeIndex >= route.Count)
            {
                pickupTargetId = target.Spawn.Id;
                route.Clear();
                routeIndex = 0;
                SetRoute(RoutePlanner.FindRoute(map, self.CollisionRadius, map.WorldToTile(self.Position), map.WorldToTile(target.Spawn.Position)));
            }
            return FollowRoute(elapsed, driveOnlyWhenFacing: true);
        }

        // Steers for the current waypoint, moving on to the next when it is reached.
        // Pickup seeking drives all the time it turns; combat only when roughly facing.
        private TankCommand FollowRoute(float elapsed, bool driveOnlyWhenFacing)
        {
            return Follow(route, ref routeIndex, elapsed, driveOnlyWhenFacing);
        }

        private TankCommand Follow(List<Point> waypoints, ref int index, float elapsed, bool driveOnlyWhenFacing)
        {
            if (index >= waypoints.Count)
                return TankCommand.None;

            var waypoint = map.GetTileBounds(waypoints[index]).Center.ToVector2();
            if (Vector2.DistanceSquared(self.Position, waypoint) < Tuning.Ai.WaypointReachedDistance * Tuning.Ai.WaypointReachedDistance)
            {
                index++;
                return TankCommand.None;
            }

            var angle = MathHelper.WrapAngle(HeadingToward(self.Position, waypoint) - self.Heading);
            var drive = !driveOnlyWhenFacing || Math.Abs(angle) < Tuning.Ai.DriveAngleLimitRadians ? 1.0f : 0.0f;
            return Affordable(new TankCommand(Math.Sign(angle), drive), elapsed);
        }

        // Looks at every shell in flight (whoever fired it, rebounds included) and, if one that it has
        // noticed would hit it where it stands, picks a move that gets clear. Null when nothing threatens
        // it, or when no move would help (a hit it cannot avoid costs no fuel to try).
        private TankCommand? PlanEvasion(float elapsed, IReadOnlyList<Shell> shells)
        {
            ForgetGoneShells(shells);
            if (shells.Count == 0)
                return null;

            var threats = new List<(Shell Shell, float SecondsToHit)>();
            foreach (var shell in shells)
            {
                if (Vector2.Distance(shell.Position, self.Position) > settings.Get(SettingKeys.AiEvadeDetectionDistance) || shell.Age < settings.Get(SettingKeys.AiEvadeReactionSeconds))
                    continue;
                var seen = Noticed(shell);
                var hitTime = FirstHitTime(TankCommand.None, new[] { shell });
                if (!hitTime.HasValue)
                    continue; // it will not hit where the tank stands: nothing to do (or to report)
                if (!seen)
                {
                    Report(ComputerDecisionKind.ShellNotNoticed, shell, hitTime.Value);
                    continue;
                }
                threats.Add((shell, hitTime.Value));
            }
            if (threats.Count == 0)
                return null;
            var threatShells = threats.Select(threat => threat.Shell).ToList();

            // try each way of moving, hold it for a moment, and keep one that gets clear of every threat
            TankCommand? best = null;
            var bestCost = float.MaxValue;
            for (var turn = -1; turn <= 1; turn++)
                for (var drive = -1; drive <= 1; drive++)
                {
                    var candidate = new TankCommand(turn, drive);
                    if (candidate.IsIdle || TankMovement.FuelCost(map, self, candidate, elapsed) > self.Fuel)
                        continue;
                    var cost = Math.Abs(turn) * 0.1f + (drive < 0 ? 0.4f : drive == 0 ? 0.3f : 0.0f);
                    if (lastEvasionAge < 0.2f && candidate.Turn == lastEvasion.Turn && candidate.Drive == lastEvasion.Drive)
                        cost -= 0.25f; // keep to the move it was making rather than dither
                    if (cost < bestCost && !FirstHitTime(candidate, threatShells).HasValue)
                    {
                        best = candidate;
                        bestCost = cost;
                    }
                }

            foreach (var (shell, secondsToHit) in threats)
                Report(best.HasValue ? ComputerDecisionKind.Dodging : ComputerDecisionKind.ShellUnavoidable, shell, secondsToHit, best ?? TankCommand.None);
            if (best.HasValue)
            {
                lastEvasion = best.Value;
                lastEvasionAge = 0.0f;
            }
            return best;
        }

        // How long until one of the shells would hit this tank if it held the command for a moment and then
        // stood still (the shells flown forward on copies with the real shell rules, the opponent where it is),
        // or null if none would. Standing still is the idle command.
        private float? FirstHitTime(TankCommand command, IEnumerable<Shell> shells)
        {
            scratch ??= self.CopyForPrediction();
            scratch.Position = self.Position;
            scratch.Heading = self.Heading;
            scratch.Fuel = self.Fuel;
            var tanks = new[] { scratch, opponent };
            var flying = shells.Select(shell => shell.Clone()).ToList();
            var step = Tuning.Ai.EvadeStepSeconds;
            var holdSteps = (int)Math.Round(settings.Get(SettingKeys.AiEvadeHoldSeconds) / step);
            var steps = (int)Math.Round(settings.Get(SettingKeys.AiEvadeLookaheadSeconds) / step);
            for (var index = 0; index < steps && flying.Count > 0; index++)
            {
                if (index < holdSteps && !command.IsIdle)
                    TankMovement.ApplyInput(map, scratch, opponent, command, step);
                for (var shell = flying.Count - 1; shell >= 0; shell--)
                {
                    var result = flying[shell].Step(map, tanks, step);
                    if (ReferenceEquals(result.Hit, scratch))
                        return (index + 1) * step;
                    if (result.Removed)
                        flying.RemoveAt(shell);
                }
            }
            return null;
        }

        // Records a decision about a shell, once for each shell and kind.
        private void Report(ComputerDecisionKind kind, Shell shell, float secondsToHit, TankCommand move = default)
        {
            if (!reportedShells.Add((shell, kind)))
                return;
            var skill = MathHelper.Clamp(self.ComputerSkill, 0.0f, 1.0f);
            var chance = settings.Get(SettingKeys.AiEvadeNoticeChanceAtSkillZero) + (1.0f - settings.Get(SettingKeys.AiEvadeNoticeChanceAtSkillZero)) * skill;
            noticeDraws.TryGetValue(shell, out var draw);
            decisions.Add(new ComputerDecision(kind, self.Position, self.Fuel, shell.Shooter, Vector2.Distance(shell.Position, self.Position), secondsToHit, move, draw, chance));
        }

        // Whether it has seen this shell: decided once, when it first comes within range, from its own
        // stream (it always sees it without one).
        private bool Noticed(Shell shell)
        {
            if (!noticedShells.TryGetValue(shell, out var seen))
            {
                var skill = MathHelper.Clamp(self.ComputerSkill, 0.0f, 1.0f);
                var chance = settings.Get(SettingKeys.AiEvadeNoticeChanceAtSkillZero) + (1.0f - settings.Get(SettingKeys.AiEvadeNoticeChanceAtSkillZero)) * skill;
                var draw = random == null ? 0.0f : (float)random.NextDouble();
                seen = random == null || draw < chance;
                noticedShells[shell] = seen;
                noticeDraws[shell] = draw;
            }
            return seen;
        }

        private void ForgetGoneShells(IReadOnlyList<Shell> shells)
        {
            if (noticedShells.Count == 0)
                return;
            var gone = noticedShells.Keys.Where(shell => !shells.Contains(shell)).ToList();
            foreach (var shell in gone)
            {
                noticedShells.Remove(shell);
                noticeDraws.Remove(shell);
                reportedShells.RemoveWhere(entry => ReferenceEquals(entry.Item1, shell));
            }
        }

        // After a shot, while the gun cools down, move to a new firing position instead of standing
        // still. Null when it is not relocating (so the usual rules decide).
        private TankCommand? PlanRelocation(float elapsed)
        {
            if (relocationPending)
            {
                relocationPending = false;
                relocating = ChooseFiringPosition();
            }
            if (!relocating)
                return null;
            // settled when it has arrived, or the gun is ready and it is time to shoot from where it is
            if (fireTimer <= 0.0f || relocationIndex >= relocationRoute.Count)
            {
                relocating = false;
                return null;
            }
            return Follow(relocationRoute, ref relocationIndex, elapsed, driveOnlyWhenFacing: true);
        }

        // Picks one of the nearest few firing positions that is a real move away, from this seat's
        // stream (the nearest without one), and plans the way there. False when there is nowhere to go.
        private bool ChooseFiringPosition()
        {
            var here = map.WorldToTile(self.Position);
            var candidates = RoutePlanner.FindFiringPositions(map, self.CollisionRadius, map.WorldToTile(opponent.Position), self.PreferredCombatDistanceTiles - settings.GetWhole(SettingKeys.AiRelocationCloserTiles))
                .Where(tile => Math.Max(Math.Abs(tile.X - here.X), Math.Abs(tile.Y - here.Y)) >= settings.GetWhole(SettingKeys.AiRelocationMinimumTiles))
                .OrderBy(tile => Vector2.DistanceSquared(tile.ToVector2(), here.ToVector2()))
                .Take(settings.GetWhole(SettingKeys.AiRelocationChoices))
                .ToList();
            if (candidates.Count == 0)
                return false;
            var first = random == null ? 0 : random.Next(candidates.Count);
            for (var offset = 0; offset < candidates.Count; offset++)
            {
                var goal = candidates[(first + offset) % candidates.Count];
                var path = RoutePlanner.FindRoute(map, self.CollisionRadius, here, goal);
                if (path == null) continue;
                relocationRoute.Clear();
                relocationRoute.AddRange(path);
                relocationIndex = Math.Min(1, relocationRoute.Count);
                return true;
            }
            return false;
        }

        private void BuildCombatRoute(int alternative)
        {
            route.Clear();
            routeIndex = 0;
            SetRoute(RoutePlanner.FindCombatRoute(map, self.CollisionRadius,
                map.WorldToTile(self.Position), map.WorldToTile(opponent.Position), self.PreferredCombatDistanceTiles, alternative));
        }

        // Adopts a new route, starting at its second tile (the first is where the tank is).
        private void SetRoute(List<Point> newRoute)
        {
            if (newRoute == null) return;
            route.AddRange(newRoute);
            routeIndex = Math.Min(1, route.Count);
        }

        // Once inside this it stops and shoots: the combat ring (the engage distance, give or take the
        // ring tolerance) plus half a tile for where in the tile it stands.
        private float HoldDistance => (self.PreferredCombatDistanceTiles + Tuning.Ai.CombatRingToleranceTiles + 0.5f) * map.TileWidth;

        // The farthest it will fire from.
        private float FireDistance => settings.Get(SettingKeys.AiFireDistanceTiles) * map.TileWidth;

        // The largest error the next shot may have: the narrowest window plus a random
        // extra, up to the maximum scaled by (1 - skill). Without a stream the window
        // is the narrowest, so there is no random error.
        private float DrawAimError()
        {
            var window = self.ComputerAimToleranceRadians;
            if (random == null) return window;
            var extra = settings.Get(SettingKeys.AiMaximumAimErrorRadians) * (1.0f - MathHelper.Clamp(self.ComputerSkill, 0.0f, 1.0f));
            return window + (float)random.NextDouble() * extra;
        }

        private PickupState FindPickupTarget(IReadOnlyList<PickupState> pickups)
        {
            var needsFuel = self.Fuel < self.MaximumFuel * settings.Get(SettingKeys.AiNeedsFuelBelowFraction);
            var needsAmmo = self.RemainingAmmunition < self.StartingAmmunition * settings.Get(SettingKeys.AiNeedsAmmoBelowFraction);
            if (!needsFuel && !needsAmmo) return null;
            PickupState best = null;
            var bestDistance = float.MaxValue;
            foreach (var pickup in pickups)
            {
                if (!pickup.Active || (pickup.Spawn.Kind == PickupKind.Fuel ? !needsFuel : !needsAmmo)) continue;
                if (ignorePickupTimer > 0.0f && pickup.Spawn.Id == ignoredPickupId) continue;
                var distance = Vector2.DistanceSquared(self.Position, pickup.Spawn.Position);
                if (distance < bestDistance) { bestDistance = distance; best = pickup; }
            }
            return best;
        }

        // The command if the tank can pay for all of it, otherwise nothing.
        private TankCommand Affordable(TankCommand command, float elapsed)
        {
            return self.Fuel >= TankMovement.FuelCost(map, self, command, elapsed) ? command : TankCommand.None;
        }

        private float TurnToward(float heading)
        {
            return Math.Sign(MathHelper.WrapAngle(heading - self.Heading));
        }

        private static float HeadingToward(Vector2 from, Vector2 to)
        {
            return (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
        }
    }
}

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    // What the computer is trying to do right now, for the debug overlay.
    public enum ComputerMode
    {
        Pickup,
        Flee,
        Long,
        Combat,

        // Backing away after getting stuck.
        Recover
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
        private readonly List<Point> route = new List<Point>();
        private int routeIndex;
        private int pickupTargetId = -1;
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
        public ComputerController(WorldMap map, Player self, Player opponent, Random random = null)
        {
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

        // The largest error, in radians, the shot being prepared may have: it fires as
        // soon as the tank points within this of the opponent. Drawn per shot.
        public float AimError => aimError;

        // What the last PlanMove asked for, for the overlay and the tests.
        public TankCommand LastMove { get; private set; }

        // What the computer would be doing now, in the order it decides.
        public ComputerMode Mode(IReadOnlyList<PickupState> pickups)
        {
            if (Recovering) return ComputerMode.Recover;
            if (FindPickupTarget(pickups) != null) return ComputerMode.Pickup;
            if (self.RemainingAmmunition == 0) return ComputerMode.Flee;
            return longRangePursuit ? ComputerMode.Long : ComputerMode.Combat;
        }

        // Called by the stage when the tank has been hit: turn on the attacker and
        // forget the fire cooldown.
        public void Hit()
        {
            retaliationTimer = Tuning.Ai.RetaliationSeconds;
            fireTimer = 0.0f;
        }

        // Called by the stage after a successful shot, to start the fire cooldown.
        public void ShotFired()
        {
            fireTimer = self.ComputerFireCooldownSeconds + self.ComputerReactionDelaySeconds;
            aimErrorDrawn = false; // the next shot gets its own error
        }

        // Called when control changes, so the computer starts afresh.
        public void Reset()
        {
            route.Clear();
            routeIndex = 0;
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
        public TankCommand PlanMove(float elapsed, IReadOnlyList<PickupState> pickups)
        {
            secondsSinceRecovery += elapsed;
            routePursuitTimer = Math.Max(0.0f, routePursuitTimer - elapsed);
            ignorePickupTimer = Math.Max(0.0f, ignorePickupTimer - elapsed);
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
            if (drivingSeconds < Tuning.Ai.StuckWindowSeconds)
                return false;
            var moved = Vector2.Distance(drivingStart, self.Position);
            drivingSeconds = 0.0f;
            return moved < Tuning.Ai.StuckMinimumDistance;
        }

        private void StartRecovery()
        {
            StuckCount++;
            repeatedStuck = secondsSinceRecovery <= Tuning.Ai.StuckRepeatSeconds ? repeatedStuck + 1 : 1;
            // back away turning to a side picked from the seat's stream; without one, alternate sides
            recoverySide = random != null ? (random.Next(2) == 0 ? -1 : 1) : -recoverySide;
            recoveryTimer = Tuning.Ai.StuckRecoverySeconds;
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

            var distance = Vector2.Distance(self.Position, opponent.Position);
            var longRangeThreshold = map.Bounds.Width * self.LongRangePursuitDistanceFraction;
            if (distance <= longRangeThreshold && map.HasLineOfSight(self.Position, opponent.Position))
                return TankCommand.None;
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

            // this shot's window (its largest error), drawn once and kept until it is fired
            if (!aimErrorDrawn)
            {
                aimError = DrawAimError();
                aimErrorDrawn = true;
            }
            var desiredHeading = HeadingToward(self.Position, opponent.Position);
            if (Math.Abs(MathHelper.WrapAngle(desiredHeading - self.Heading)) > aimError)
            {
                if (Recovering)
                    return TankCommand.None; // backing away: the aim phase must not turn the tank back
                var turn = new TankCommand(TurnToward(desiredHeading), 0.0f);
                return TankMovement.FuelCost(map, self, turn, elapsed) <= self.Fuel ? turn : TankCommand.None;
            }

            if (!map.HasLineOfSight(self.Position, opponent.Position))
                return TankCommand.None;

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
            if (routeIndex >= route.Count)
                return TankCommand.None;

            var waypoint = map.GetTileBounds(route[routeIndex]).Center.ToVector2();
            if (Vector2.DistanceSquared(self.Position, waypoint) < Tuning.Ai.WaypointReachedDistance * Tuning.Ai.WaypointReachedDistance)
            {
                routeIndex++;
                return TankCommand.None;
            }

            var angle = MathHelper.WrapAngle(HeadingToward(self.Position, waypoint) - self.Heading);
            var drive = !driveOnlyWhenFacing || Math.Abs(angle) < Tuning.Ai.DriveAngleLimitRadians ? 1.0f : 0.0f;
            return Affordable(new TankCommand(Math.Sign(angle), drive), elapsed);
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

        // The largest error the next shot may have: the narrowest window plus a random
        // extra, up to the maximum scaled by (1 - skill). Without a stream the window
        // is the narrowest, so there is no random error.
        private float DrawAimError()
        {
            var window = self.ComputerAimToleranceRadians;
            if (random == null) return window;
            var extra = Tuning.Ai.MaximumAimErrorRadians * (1.0f - MathHelper.Clamp(self.ComputerSkill, 0.0f, 1.0f));
            return window + (float)random.NextDouble() * extra;
        }

        private PickupState FindPickupTarget(IReadOnlyList<PickupState> pickups)
        {
            var needsFuel = self.Fuel < self.MaximumFuel * Tuning.Ai.NeedsFuelBelowFraction;
            var needsAmmo = self.RemainingAmmunition < self.StartingAmmunition * Tuning.Ai.NeedsAmmoBelowFraction;
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

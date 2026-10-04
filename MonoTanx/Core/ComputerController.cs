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
        Combat
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
        private readonly List<Point> route = new List<Point>();
        private int routeIndex;
        private int pickupTargetId = -1;
        private bool longRangePursuit;
        private float longRangeHeading;
        private float fireTimer;
        private float retaliationTimer;
        private Vector2 lastOpponentPosition;

        public ComputerController(WorldMap map, Player self, Player opponent)
        {
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

        // What the computer would be doing now, in the order it decides.
        public ComputerMode Mode(IReadOnlyList<PickupState> pickups)
        {
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
            lastOpponentPosition = opponent.Position;
        }

        // The movement for this update. Does nothing at all when the tank cannot
        // afford both the turn and the drive.
        public TankCommand PlanMove(float elapsed, IReadOnlyList<PickupState> pickups)
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
            if (distance > longRangeThreshold)
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
                BuildCombatRoute();
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

            var desiredHeading = HeadingToward(self.Position, opponent.Position);
            var aimError = Math.Abs(MathHelper.WrapAngle(desiredHeading - self.Heading));
            if (aimError > self.ComputerAimToleranceRadians)
            {
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
            return FollowRoute(elapsed, driveOnlyWhenFacing: false);
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

        private void BuildCombatRoute()
        {
            route.Clear();
            routeIndex = 0;
            SetRoute(RoutePlanner.FindCombatRoute(map, self.CollisionRadius,
                map.WorldToTile(self.Position), map.WorldToTile(opponent.Position), self.PreferredCombatDistanceTiles));
        }

        // Adopts a new route, starting at its second tile (the first is where the tank is).
        private void SetRoute(List<Point> newRoute)
        {
            if (newRoute == null) return;
            route.AddRange(newRoute);
            routeIndex = Math.Min(1, route.Count);
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

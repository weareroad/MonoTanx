using Microsoft.Xna.Framework;
using System;

namespace MonoTanx.Core
{
    // Movement, steering and fuel rules shared by every tank. Contains no
    // input, rendering or Game dependencies so it can be tested directly.
    public static class TankMovement
    {
        // Applies turn and drive input (each -1..1) for the elapsed time.
        // Returns true when the tank drove, so the caller can animate it.
        public static bool ApplyInput(WorldMap map, Player tank, Player other, float turn, float drive, float elapsed)
        {
            var terrainFuel = map.GetFuelCostMultiplier(tank.Position);
            var turnCost = Math.Abs(turn) * Tuning.Tank.TurnFuelPerSecond * elapsed * terrainFuel;
            var driveRate = drive < 0.0f ? tank.ReverseFuelPerSecond : tank.ForwardFuelPerSecond;
            var driveCost = Math.Abs(drive) * driveRate * elapsed * terrainFuel;
            var canTurn = turn == 0.0f || tank.Fuel >= turnCost;
            var canDrive = drive == 0.0f || tank.Fuel >= turnCost + driveCost;
            if (canTurn) tank.Heading = MathHelper.WrapAngle(tank.Heading + turn * tank.TurnSpeed * elapsed);
            var drove = canDrive && drive != 0.0f;
            if (drove)
            {
                var direction = new Vector2((float)Math.Cos(tank.Heading), (float)Math.Sin(tank.Heading));
                var speed = drive < 0.0f ? tank.ReverseMovementSpeed : tank.MovementSpeed;
                Move(map, tank, other, direction * drive * speed * map.GetMovementSpeedMultiplier(tank.Position) * elapsed);
            }
            if (canTurn && canDrive) tank.Fuel = MathHelper.Max(0.0f, tank.Fuel - turnCost - driveCost);
            else if (canTurn) tank.Fuel = MathHelper.Max(0.0f, tank.Fuel - turnCost);
            return drove;
        }

        // Moves one axis at a time so a tank slides along obstacles.
        public static void Move(WorldMap map, Player tank, Player other, Vector2 movement)
        {
            var horizontal = tank.Position + new Vector2(movement.X, 0.0f);
            if (CanOccupy(map, tank, other, horizontal)) tank.Position = horizontal;
            var vertical = tank.Position + new Vector2(0.0f, movement.Y);
            if (CanOccupy(map, tank, other, vertical)) tank.Position = vertical;
        }

        public static bool CanOccupy(WorldMap map, Player tank, Player other, Vector2 position)
        {
            if (!map.CanOccupyCircle(position, tank.CollisionRadius))
                return false;

            var minimumDistance = tank.CollisionRadius + other.CollisionRadius;
            return Vector2.DistanceSquared(position, other.Position) > minimumDistance * minimumDistance;
        }
    }
}

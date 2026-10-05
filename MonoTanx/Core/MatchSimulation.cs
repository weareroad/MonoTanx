using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MonoTanx.Core
{
    // The match without a screen: the two tanks, the shells in flight, the pickups
    // and one computer controller per seat, advanced by the usual rules and
    // reporting what happened as events. Contains no input, rendering, audio or
    // Game dependencies, so a match can run in a test. The stage gathers input,
    // calls this, then turns the events into sound and shake and draws the result.
    public sealed class MatchSimulation
    {
        private readonly WorldMap map;
        private readonly Player[] tanks;
        private readonly ComputerController[] controllers;
        private readonly Random random;
        private readonly SimulationSettings settings;
        private readonly bool[] moved = new bool[2];
        private readonly TankMotion[] motion = new TankMotion[2];
        private readonly List<Shell> shells = new List<Shell>();
        private readonly List<PickupState> pickups = new List<PickupState>();
        private readonly List<MatchEvent> events = new List<MatchEvent>();

        // The gameplay random stream (RandomStreams.Gameplay): the only randomness
        // the simulation uses, so a run is reproducible from its seed.
        public MatchSimulation(WorldMap map, Player playerOne, Player playerTwo, IEnumerable<PickupSpawn> pickupSpawns, Random random, SimulationSettings settings = default,
            Random aiRandomOne = null, Random aiRandomTwo = null)
        {
            this.map = map;
            this.random = random;
            this.settings = settings;
            tanks = new[] { playerOne, playerTwo };
            controllers = new[]
            {
                new ComputerController(map, playerOne, playerTwo, aiRandomOne),
                new ComputerController(map, playerTwo, playerOne, aiRandomTwo)
            };
            foreach (var spawn in pickupSpawns)
                pickups.Add(new PickupState(spawn));
        }

        public IReadOnlyList<Shell> Shells => shells;
        public IReadOnlyList<PickupState> Pickups => pickups;

        // What has happened since the stage last cleared the list.
        public IReadOnlyList<MatchEvent> Events => events;

        public void ClearEvents() => events.Clear();

        public Player TankOf(Seat seat) => tanks[(int)seat];

        public Seat SeatOf(Player tank) => ReferenceEquals(tank, tanks[0]) ? Seat.One : Seat.Two;

        public Player OpponentOf(Player tank) => ReferenceEquals(tank, tanks[0]) ? tanks[1] : tanks[0];

        public ComputerController ControllerOf(Seat seat) => controllers[(int)seat];

        // Puts a newly fired shell in flight.
        public void Launch(Player shooter, ShellLaunch launch)
        {
            shells.Add(new Shell(launch.Ammunition, launch.Position, launch.Velocity));
            events.Add(new MatchEvent(MatchEventKind.ShellFired, SeatOf(shooter)));
        }

        // Whether the seat's tank drove in the last Step (a computer seat never
        // counts, as it has no drive animation).
        public bool Moved(Seat seat) => moved[(int)seat];

        // How the seat's tank moved in the last Step, judged before shells could
        // knock it about: what the engine sound follows.
        public TankMotion Motion(Seat seat) => motion[(int)seat];

        // Puts both tanks at their corners of the map facing each other, and starts
        // the computers afresh.
        public void PlaceAtStart()
        {
            tanks[0].Position = FindStartingPosition(topLeft: true);
            tanks[1].Position = FindStartingPosition(topLeft: false);
            tanks[0].Heading = HeadingToward(tanks[0].Position, tanks[1].Position);
            tanks[1].Heading = HeadingToward(tanks[1].Position, tanks[0].Position);
            foreach (var controller in controllers)
                controller.Reset();
        }

        // Starts a fresh round: both tanks back at their start positions and
        // headings with full health, fuel and ammunition, no shells in flight, every
        // pickup there again and the computers afresh. Control of each seat and the
        // random stream carry on, so the same seed still gives the same matches.
        // Events already reported are left for the stage to read.
        public void ResetRound()
        {
            foreach (var tank in tanks)
                tank.ResetResources();
            shells.Clear();
            foreach (var pickup in pickups)
                pickup.Active = true;
            PlaceAtStart();
            for (var index = 0; index < 2; index++)
            {
                moved[index] = false;
                motion[index] = TankMotion.Idle;
            }
        }

        // Hands a seat to the computer or to a human, starting the computer afresh.
        public void SetComputerControlled(Seat seat, bool computer)
        {
            TankOf(seat).IsComputerControlled = computer;
            ControllerOf(seat).Reset();
        }

        public void ResetFuelAndAmmunition(Seat seat) => TankOf(seat).ResetFuelAndAmmunition();

        // Advances the match one update. The commands are for human seats (null
        // for a computer seat, which is driven by its controller). Order: each seat
        // moves in turn (a computer ticks its reload first, a human after), the
        // computers aim and fire, then shells fly and pickups are collected.
        public void Step(float elapsed, TankCommand? commandOne, TankCommand? commandTwo)
        {
            var before = new[] { (tanks[0].Position, tanks[0].Heading), (tanks[1].Position, tanks[1].Heading) };
            for (var index = 0; index < 2; index++)
            {
                var seat = (Seat)index;
                var tank = tanks[index];
                moved[index] = false;
                if (tank.IsComputerControlled)
                {
                    TickReload(tank, elapsed);
                    TankMovement.ApplyInput(map, tank, OpponentOf(tank), controllers[index].PlanMove(elapsed, pickups), elapsed);
                }
                else
                {
                    var command = (index == 0 ? commandOne : commandTwo) ?? TankCommand.None;
                    moved[index] = TankMovement.ApplyInput(map, tank, OpponentOf(tank), command, elapsed);
                    TickReload(tank, elapsed);
                    if (command.Fire) TryFire(tank);
                }
            }
            for (var index = 0; index < 2; index++)
            {
                var tank = tanks[index];
                if (!tank.IsComputerControlled) continue;
                var command = controllers[index].PlanAim(elapsed);
                TankMovement.ApplyInput(map, tank, OpponentOf(tank), command, elapsed);
                if (command.Fire && TryFire(tank))
                    controllers[index].ShotFired();
            }
            for (var index = 0; index < 2; index++)
                motion[index] = TankMotionClassifier.Classify(before[index].Item1, before[index].Item2, tanks[index].Position, tanks[index].Heading);
            StepShells(elapsed);
            CollectPickups();
        }

        private void TickReload(Player tank, float elapsed)
        {
            if (tank.TickReload(elapsed))
                events.Add(new MatchEvent(MatchEventKind.ReloadReady, SeatOf(tank)));
        }

        private bool TryFire(Player tank)
        {
            if (!tank.TryFire(settings.MuzzleOffsetOf(SeatOf(tank)), out var launch)) return false;
            Launch(tank, launch);
            return true;
        }

        private Vector2 FindStartingPosition(bool topLeft)
        {
            var one = tanks[0];
            var two = tanks[1];
            var corner = topLeft ? new Vector2(one.CollisionRadius, one.CollisionRadius) : new Vector2(map.Bounds.Right - two.CollisionRadius, map.Bounds.Bottom - two.CollisionRadius);
            var result = corner;
            var bestDistance = float.MaxValue;
            for (var y = 0; y < map.Bounds.Height; y += map.TileHeight)
                for (var x = 0; x < map.Bounds.Width; x += map.TileWidth)
                {
                    var candidate = new Vector2(x + map.TileWidth / 2.0f, y + map.TileHeight / 2.0f);
                    if (!map.CanOccupyCircle(candidate, one.CollisionRadius)) continue;
                    var distance = Vector2.DistanceSquared(candidate, corner);
                    if (distance < bestDistance) { bestDistance = distance; result = candidate; }
                }
            return result;
        }

        private static float HeadingToward(Vector2 from, Vector2 to)
        {
            return (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
        }

        // Flies every shell for the elapsed time, applying hits.
        public void StepShells(float elapsed)
        {
            for (var index = shells.Count - 1; index >= 0; index--)
            {
                var shell = shells[index];
                var result = shell.Step(map, tanks, elapsed);
                if (result.Reflections > 0)
                    events.Add(new MatchEvent(MatchEventKind.ShellReflected)); // a shell reflects at most once per update
                if (result.Fate == ShellFate.HitTerrain)
                    events.Add(new MatchEvent(MatchEventKind.ShellHitTerrain));
                if (result.Hit != null)
                    DamageTank(result.Hit, shell.Ammunition.Damage, shell.Velocity);
                if (result.Removed)
                    shells.RemoveAt(index);
            }
        }

        // Lets each tank collect any pickup it is in range of, seat one first.
        public void CollectPickups()
        {
            foreach (var tank in tanks)
                foreach (var pickup in pickups)
                {
                    if (!pickup.Active || !PickupRules.InRange(tank, pickup.Spawn))
                        continue;
                    PickupRules.Apply(tank, pickup.Spawn);
                    pickup.Active = false;
                    events.Add(new MatchEvent(MatchEventKind.PickupCollected, SeatOf(tank)));
                }
        }

        private void DamageTank(Player tank, int damage, Vector2 impactVelocity)
        {
            var seat = SeatOf(tank);
            var destroyed = TankDamage.Apply(map, tank, OpponentOf(tank), damage, impactVelocity, random);
            events.Add(new MatchEvent(MatchEventKind.TankHit, seat));
            ControllerOf(seat).Hit();
            if (destroyed)
                events.Add(new MatchEvent(MatchEventKind.TankDestroyed, seat));
        }
    }
}

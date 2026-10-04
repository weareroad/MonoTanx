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
        private readonly List<Shell> shells = new List<Shell>();
        private readonly List<PickupState> pickups = new List<PickupState>();
        private readonly List<MatchEvent> events = new List<MatchEvent>();

        // The gameplay random stream (RandomStreams.Gameplay): the only randomness
        // the simulation uses, so a run is reproducible from its seed.
        public MatchSimulation(WorldMap map, Player playerOne, Player playerTwo, IEnumerable<PickupSpawn> pickupSpawns, Random random)
        {
            this.map = map;
            this.random = random;
            tanks = new[] { playerOne, playerTwo };
            controllers = new[]
            {
                new ComputerController(map, playerOne, playerTwo),
                new ComputerController(map, playerTwo, playerOne)
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

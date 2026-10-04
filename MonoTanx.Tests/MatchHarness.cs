using Microsoft.Xna.Framework;
using MonoTanx.Core;

namespace MonoTanx.Tests;

// Runs a whole match on the real arena with no graphics device: the simulation
// stepped at the fixed rate for a number of simulated seconds from a seed, with
// every event recorded and a check called after every update. Human seats are
// driven by an optional command source (idle when none is given).
internal sealed class MatchHarness
{
    public const float Step = 1.0f / Tuning.Timing.UpdatesPerSecond;

    // 16px tank sprite: half its width plus the muzzle clearance, as the stage works it out.
    private const float MuzzleOffset = 16.0f / 2.0f + Tuning.Presentation.MuzzleClearance;

    public int Seed { get; }
    public WorldMap Map { get; }
    public Player PlayerOne { get; }
    public Player PlayerTwo { get; }
    public MatchSimulation Simulation { get; }
    public List<(int Frame, MatchEventKind Kind, Seat? Seat)> Log { get; } = new();
    public int Frame { get; private set; }

    public MatchHarness(int seed, MatchSetup setup)
    {
        Seed = seed;
        Map = new WorldMap(TestSupport.FixturePath("arena", "arena_01.tmx"));
        PlayerOne = new Player("Player 1", "Sprites/tank", Color.White, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
            isComputerControlled: setup.PlayerOne == PlayerControl.Computer);
        PlayerTwo = new Player("Player 2", "Sprites/tank2", Color.LightGray, Player.DefaultAmmunition, Tuning.Tank.StartingShells,
            isComputerControlled: setup.PlayerTwo == PlayerControl.Computer);
        var streams = new RandomStreams(seed);
        Simulation = new MatchSimulation(Map, PlayerOne, PlayerTwo, Map.PickupSpawns, streams.Gameplay,
            new SimulationSettings(MuzzleOffset, MuzzleOffset));
        Simulation.PlaceAtStart();
    }

    // Runs for the simulated seconds, calling check(harness) after each update.
    // humanCommand (if given) supplies the command for a human seat each update.
    public MatchHarness Run(float seconds, Action<MatchHarness> check = null, Func<MatchHarness, Seat, TankCommand?> humanCommand = null)
    {
        var updates = (int)Math.Round(seconds * Tuning.Timing.UpdatesPerSecond);
        for (var i = 0; i < updates; i++)
        {
            Frame++;
            Simulation.Step(Step, humanCommand?.Invoke(this, Seat.One), humanCommand?.Invoke(this, Seat.Two));
            foreach (var matchEvent in Simulation.Events)
                Log.Add((Frame, matchEvent.Kind, matchEvent.Seat));
            Simulation.ClearEvents();
            check?.Invoke(this);
        }
        return this;
    }

    public int Count(MatchEventKind kind, Seat? seat = null) =>
        Log.Count(e => e.Kind == kind && (seat == null || e.Seat == seat));

    // Everything that identifies where a match ended up.
    public string Fingerprint() => string.Join("|",
        Frame, FormatTank(PlayerOne), FormatTank(PlayerTwo), Log.Count, string.Join(",", Log.Select(e => $"{e.Frame}{e.Kind}{e.Seat}")));

    private static string FormatTank(Player tank) =>
        $"{tank.Position.X:R},{tank.Position.Y:R},{tank.Heading:R},{tank.Fuel:R},{tank.Health},{tank.RemainingAmmunition}";
}

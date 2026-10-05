namespace MonoTanx.Core
{
    // Something that happened in the match, in the order it happened. The stage
    // turns these into sounds and screen shake; the rules know nothing about either.
    public enum MatchEventKind
    {
        ShellFired,
        ReloadReady,
        ShellReflected,
        ShellHitTerrain,
        TankHit,
        PickupCollected,
        TankDestroyed,

        // The computer decided something worth recording (see ComputerDecision).
        ComputerDecision
    }

    public readonly struct MatchEvent
    {
        public MatchEventKind Kind { get; }

        // The seat the event concerns: who fired, reloaded, was hit or destroyed, or
        // collected the pickup. Null for a shell reflecting or hitting terrain.
        public Seat? Seat { get; }

        // For a hit: the seat whose shell it was, when known.
        public Seat? Source { get; }

        // For a computer decision: what it decided.
        public ComputerDecision? Decision { get; }

        public MatchEvent(MatchEventKind kind, Seat? seat = null, Seat? source = null, ComputerDecision? decision = null)
        {
            Kind = kind;
            Seat = seat;
            Source = source;
            Decision = decision;
        }
    }
}

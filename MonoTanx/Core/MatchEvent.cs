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
        TankDestroyed
    }

    public readonly struct MatchEvent
    {
        public MatchEventKind Kind { get; }

        // The seat the event concerns: who fired, reloaded, was hit or destroyed, or
        // collected the pickup. Null for a shell reflecting or hitting terrain.
        public Seat? Seat { get; }

        public MatchEvent(MatchEventKind kind, Seat? seat = null)
        {
            Kind = kind;
            Seat = seat;
        }
    }
}

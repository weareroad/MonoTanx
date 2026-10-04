namespace MonoTanx.Core
{
    // The one-shot sounds the game can play. (Engine drones are continuous and
    // handled separately.) Each maps to a file in Content/Audio named after it.
    public enum SoundCue
    {
        // A tank fires a shell.
        Fire,

        // A tank's reload has finished and it can fire again.
        Reload,

        // A shell hits a tank.
        Explosion,

        // A shell reflects off a reflective surface.
        Ping,

        // A shell hits a solid, non-reflective surface (wall, hill, map edge).
        Crump,

        // A tank collects a pickup.
        Pickup
    }
}

namespace MonoTanx.Core
{
    // A pickup in the world: where it is and what it gives (the spawn), and
    // whether it is still there to collect. The stage adds drawing to this.
    public class PickupState
    {
        public PickupSpawn Spawn { get; }
        public bool Active { get; set; } = true;

        public PickupState(PickupSpawn spawn)
        {
            Spawn = spawn;
        }
    }
}

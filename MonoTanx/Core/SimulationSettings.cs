namespace MonoTanx.Core
{
    // Presentation-derived numbers the simulation needs, given to it as plain data
    // by the stage so the rules never touch a texture.
    public readonly struct SimulationSettings
    {
        // How far in front of each tank's centre a shell appears (it depends on the tank sprite).
        public float MuzzleOffsetOne { get; }
        public float MuzzleOffsetTwo { get; }

        public SimulationSettings(float muzzleOffsetOne, float muzzleOffsetTwo)
        {
            MuzzleOffsetOne = muzzleOffsetOne;
            MuzzleOffsetTwo = muzzleOffsetTwo;
        }

        public float MuzzleOffsetOf(Seat seat) => seat == Seat.One ? MuzzleOffsetOne : MuzzleOffsetTwo;
    }
}

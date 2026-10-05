namespace MonoTanx.Core
{
    // Every gameplay and feel value in one place. Each entry says what it
    // controls, its unit, and why it has its current value. Change a value
    // here, rebuild, and check docs/tuning.md (which holds the overview and the
    // pacing numbers derived from these values).
    //
    // Per-tank values here are defaults: Player copies them into instance
    // properties, so individual players can still differ.
    //
    // Deliberately not here: the HUD pixel layout (GameStage), the logical
    // 800x600 surface (Tanx.DesignedWidth/Height) and asset names. See
    // docs/tuning.md.
    public static class Tuning
    {
        public static class Timing
        {
            // Fixed simulation and draw rate. Everything in the simulation is
            // scaled by elapsed time, so this mainly sets how smooth it looks.
            // 60 matches common displays and gives a 16.7ms step.
            public const int UpdatesPerSecond = 60;
        }

        public static class Tank
        {
            // Health a tank starts with and its maximum. Chosen as a round
            // number so health reads directly as a percentage in the HUD.
            public const int MaximumHealth = 100;

            // Fuel a tank starts with and its maximum (units). At 4 units/s
            // that is about 50 seconds of driving, roughly 4.7 arena widths,
            // so fuel matters during a match without being a constant worry.
            public const float MaximumFuel = 200.0f;

            // Forward speed on normal ground (pixels/second). The 960px arena
            // takes about 10.7 seconds to cross.
            public const float ForwardSpeed = 90.0f;

            // Reverse speed (pixels/second). Half of forward speed, so backing
            // away is possible but clearly worse than turning around.
            public const float ReverseSpeed = 45.0f;

            // Turning rate (radians/second). A full turn takes about 2.5s.
            public const float TurnSpeed = 2.5f;

            // Collision radius (pixels). The sprite is 16px, so 6 lets a tank
            // fit a one-tile (16px) corridor with 4px of play; a full 8px
            // radius would fit with none.
            public const float CollisionRadius = 6.0f;

            // Fuel used per second of forward driving (units/second).
            public const float ForwardFuelPerSecond = 4.0f;

            // Reverse fuel rate as a multiple of forward. With reverse at half
            // speed this makes reversing 4 times the fuel per pixel, a
            // deliberate penalty.
            public const float ReverseFuelMultiplier = 2.0f;

            // Fuel used per second of turning (units/second). Very low, so
            // turning is almost free (a full turn costs about 0.6 units,
            // against about 10 for driving the same 2.5s). Raise it if turning
            // should matter.
            public const float TurnFuelPerSecond = 0.25f;

            // Shells a tank starts with.
            public const int StartingShells = 20;
        }

        // The default ("standard") shell. Player.DefaultAmmunition is built
        // from these.
        public static class StandardShell
        {
            // Seconds before the tank can fire again.
            public const float ReloadSeconds = 3.0f;

            // Longest a shell stays in flight (seconds). With the speed below
            // the range is 1300px, longer than the 1154px arena diagonal.
            public const float MaxFlightSeconds = 5.0f;

            // Health removed by a hit. 12 against 100 health is 9 hits to
            // kill, which with the reload means at least 24s of firing.
            public const int Damage = 12;

            // Shell speed (pixels/second): about 2.9 times tank speed, so a
            // moving target has to be led.
            public const float Speed = 260.0f;
        }

        // How shells fly, whatever they are. Separate from StandardShell so a
        // new ammunition type does not need to redefine them.
        public static class Projectile
        {
            // Radius used when testing a shell against a tank (pixels). Added
            // to the tank radius to give a 9px hit distance.
            public const float CollisionRadius = 3.0f;

            // A shell is removed after this many reflections, so one cannot
            // bounce forever between mirrors.
            public const int MaxReflections = 8;

            // Seconds after a reflection during which the same surface cannot
            // reflect the shell again (stops double-reflection in one tile).
            public const float ReflectionCooldownSeconds = 0.08f;

            // Pixels a shell is pushed away from a mirror after reflecting.
            public const float ReflectionNudge = 2.0f;

            // Longest distance moved in one collision sub-step (pixels). Must
            // be well under a tile (16px) so fast shells cannot skip a wall.
            public const float SubStepLength = 4.0f;
        }

        public static class Damage
        {
            // Pixels a hit tank is pushed along the shell direction, if the
            // destination is free.
            public const float KnockbackDistance = 1.5f;

            // A hit turns the tank by a random angle up to this (radians,
            // about 9 degrees either way), so a hit costs a little aim.
            public const float MaximumHeadingDisruptionRadians = 0.16f;
        }

        public static class Pickups
        {
            // Distance within which a tank collects a pickup (pixels). Larger
            // than the tank radius so driving near a pickup is enough.
            public const float CollectRadius = 12.0f;

            // Used when the map does not say how much a pickup gives. Fuel
            // pickups give about 12s of driving; ammunition 5 shells.
            public const int DefaultFuelAmount = 50;
            public const int DefaultAmmunitionAmount = 5;
        }

        // Rounds, score and the match flow. See docs/match-rounds-spec.md.
        public static class Match
        {
            // Rounds a seat must win to win the match. First to 3 means a match
            // is at most 5 decided rounds, long enough for a comeback and short
            // enough to finish in one sitting. Drawn rounds are replayed and do
            // not count.
            public const int RoundsToWin = 3;

            // Pause before each round while the count runs down (seconds), so
            // both players can see where the tanks start. Nothing moves or fires.
            public const float CountdownSeconds = 3.0f;

            // Pause after a round is decided (seconds), so a hit and its
            // explosion are seen before the reset. Shells already in flight
            // finish, but no tank can act.
            public const float RoundOverSeconds = 2.0f;

            // Longest a round can last (seconds) before it is a draw. Two
            // computers can stall once their ammunition runs out, so every mode
            // needs a way to end a round. 90s covers a full tank of fuel (50s of
            // driving) and the 24s minimum it takes to kill, with room to spare.
            public const float RoundTimeLimitSeconds = 90.0f;

            // How long a finished match's result stays on screen (seconds) before
            // the next step: a demo starting its next match by itself, or a game
            // with a human leaving the match. Long enough to read who won.
            public const float MatchOverSeconds = 5.0f;
        }

        // Computer opponent. The first group are per-Player defaults; the
        // rest are used directly by GameStage.
        public static class Ai
        {
            // Distance (in tiles) it tries to keep from Player 1 when
            // closing in. About 96px, well inside shell range.
            public const int PreferredCombatDistanceTiles = 6;

            // Beyond this fraction of the map width it stops routing and just
            // drives straight at Player 1.
            public const float LongRangePursuitDistanceFraction = 0.5f;

            // The smallest error (radians, about 4 degrees) a shot can have: the
            // narrowest window around the opponent's heading in which it will
            // fire. It cannot be smaller, because the movement and aiming phases
            // each turn the tank up to 0.04 rad a step and a tighter window is
            // missed over and over (the computer then hardly ever fires).
            public const float AimToleranceRadians = 0.07f;

            // How much wider than that a shot's window can be (radians) at skill 0.
            // Each shot draws its own window uniformly between AimToleranceRadians
            // and AimToleranceRadians plus this times (1 minus skill), from the
            // seat's own random stream, and fires as soon as the tank points
            // within it, so the error of a shot is random, bounded and
            // reproducible from the seed. At the default skill the windows average
            // 0.195 rad, which matches the fixed 0.2 rad it replaced, so the
            // computer fires as often and hits about as often (see docs/tuning.md).
            public const float MaximumAimErrorRadians = 0.5f;

            // How good a computer seat is, from 0 (worst) to 1 (never misses by
            // error). Scales the aim error now and, later, how well it notices
            // incoming shells. Each seat has its own, so it can become a setting.
            public const float Skill = 0.5f;

            // Pause (seconds) after it lines up before firing.
            public const float ReactionDelaySeconds = 0.25f;

            // Minimum seconds between its shots. Cooldown plus reaction
            // gives a 3.25s cadence, slightly slower than the 3.0s reload.
            public const float FireCooldownSeconds = 3.0f;

            // After being hit it stops and turns to face Player 1 for this
            // long (seconds), then carries on.
            public const float RetaliationSeconds = 1.5f;

            // It goes looking for a pickup when fuel, or ammunition, drops
            // below this fraction of the starting amount.
            public const float NeedsFuelBelowFraction = 0.5f;
            public const float NeedsAmmoBelowFraction = 0.5f;

            // A route waypoint counts as reached within this distance (pixels).
            public const float WaypointReachedDistance = 4.0f;

            // While routing it only drives when within this angle of its
            // next waypoint (radians, about 63 degrees), otherwise it turns first.
            public const float DriveAngleLimitRadians = 1.1f;

            // Player 1 must move this far (pixels) from where the route was
            // last planned before the route is rebuilt.
            public const float RouteRebuildDistance = 1.0f;

            // The combat ring is never smaller than this many tiles, and
            // goals within this many tiles of the ring radius are accepted.
            public const int MinimumCombatRingTiles = 2;
            public const float CombatRingToleranceTiles = 1.0f;

            // How long (seconds) a tank must be commanded to drive before it is
            // judged on whether it has moved. 1.5s is 135px at full speed, so a
            // tank that is really going somewhere is far past the next value.
            public const float StuckWindowSeconds = 1.5f;

            // The least it must have moved in that window (pixels). Under this it
            // is stuck: pressed against terrain or the other tank, going nowhere.
            // Mud halves speed and a tank sliding along a wall still moves further
            // than this, so ordinary slow going is not mistaken for being stuck.
            public const float StuckMinimumDistance = 6.0f;

            // How long (seconds) the recovery lasts: reversing while turning. At
            // reverse speed (45px/s) that backs away about 36px, enough to clear a
            // wall or a corner, for about 6 fuel at the reverse rate.
            public const float StuckRecoverySeconds = 0.8f;

            // Stuck again within this long (seconds) of the last recovery counts
            // as the same problem and tries something different (another goal).
            public const float StuckRepeatSeconds = 6.0f;

            // After getting stuck driving straight at a distant opponent, how long
            // (seconds) it follows a planned route instead of a straight line.
            public const float RoutePursuitSeconds = 10.0f;

            // After getting stuck twice on the way to a pickup, how long (seconds)
            // it ignores that pickup before trying again.
            public const float PickupIgnoreSeconds = 10.0f;
        }

        public static class Vision
        {
            // Spacing of the samples taken along a line of sight (pixels).
            // Well under a tile (16px) so a thin wall cannot be stepped over.
            public const float LineOfSightStepLength = 4.0f;
        }

        public static class Shake
        {
            // Player 1 firing (seconds, pixels). Camera offsets are rounded
            // to whole pixels, so a magnitude under about 2 is rarely visible.
            public const float FireDuration = 0.16f;
            public const float FireMagnitude = 1.8f;

            // Either tank being hit. At 1.0 this rounds to no offset about
            // half the time and is barely visible; 2 to 3 would show.
            public const float HitDuration = 0.12f;
            public const float HitMagnitude = 1.0f;

            // How often the random jitter changes (seconds). Matches the
            // 60 Hz update rate; independent of how often the screen is drawn.
            public const float JitterIntervalSeconds = 1.0f / 60.0f;
        }

        // Sound mix. All cues play centred (no panning or distance fade yet).
        public static class Audio
        {
            // Overall volume (0 to 1), applied to every sound.
            public const float MasterVolume = 1.0f;

            // Volume of each one-shot cue (0 to 1). The explosion is the
            // loudest; the rest sit below it so it stands out.
            public const float FireVolume = 0.8f;
            public const float ReloadVolume = 0.6f;
            public const float ExplosionVolume = 1.0f;
            public const float PingVolume = 0.6f;
            public const float CrumpVolume = 0.7f;
            public const float PickupVolume = 0.7f;

            // Pitch shift for Player 2's sounds (-1 to +1, an octave either
            // way, so 0.15 is about 1.8 semitones up). Enough to tell the two
            // tanks apart without sounding like a different sound.
            public const float PlayerTwoPitchOffset = 0.15f;

            // Engine drone, one per tank, silent when the tank is idle. Quiet so
            // it sits under the one-shot sounds. Moving while turning plays the
            // drive sound, not the turn sound.
            public const float EngineForwardVolume = 0.25f;
            public const float EngineReverseVolume = 0.25f;
            public const float EngineTurnVolume = 0.2f;

            // Engine pitch for each motion (-1 to +1, an octave either way).
            // Forward is the drone as recorded, reverse is lower and turning is
            // higher, so the three are easy to tell apart.
            public const float EngineForwardPitch = 0.0f;
            public const float EngineReversePitch = -0.35f;
            public const float EngineTurnPitch = 0.35f;

            // Seconds for the engine volume to fade across its whole 0 to 1
            // range (so a 0.25 fade takes a quarter of this). Short enough to
            // feel immediate, long enough to avoid clicks.
            public const float EngineFadeSeconds = 0.5f;

            // Seconds for the engine pitch to glide a whole octave. Changes of
            // note are smooth rather than jumps.
            public const float EnginePitchGlideSeconds = 0.25f;

            // A tank counts as moving if it moves at least this far in one
            // update (pixels), or as turning if it turns at least this much
            // (radians). Tiny amounts from rounding do not count.
            public const float MotionThresholdPixels = 0.05f;
            public const float MotionThresholdRadians = 0.001f;
        }

        // Values tied to the art assets or the screen layout rather than to
        // gameplay.
        public static class Presentation
        {
            // Frames in the 64x16 tank sprite sheet, and seconds per frame
            // while driving.
            public const int TankFrameCount = 4;
            public const float TankFrameSeconds = 0.15f;

            // Frames in the 64x16 pickup sprite sheets, and seconds per frame.
            public const int PickupFrameCount = 4;
            public const float PickupFrameSeconds = 0.15f;

            // Drawn size of the placeholder shell (pixels). Larger than the
            // 3px collision radius so it is easy to see.
            public const int PlaceholderShellSize = 8;

            // Gap between the tank sprite edge and where a shell appears
            // (pixels), so a new shell does not overlap its own tank.
            public const float MuzzleClearance = 4.0f;

            // Whether the overview camera (F3) smooths the scaled-down arena
            // (true) or uses nearest-neighbour (false). The arena is scaled by
            // a non-integer factor, so nearest-neighbour gives uneven pixel
            // sizes; smoothing is softer but even. Try both by eye.
            public const bool OverviewSmoothing = true;

            // Height of the HUD strip at the top of the screen (pixels); five
            // 16px tiles. The playfield is the rest of the 600px surface.
            public const int HudHeight = 80;
        }
    }
}

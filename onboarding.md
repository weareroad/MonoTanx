# Engineering onboarding

MonoTanx is a self-contained MonoGame DesktopGL game targeting .NET 10. It has no backend or external services. The repository is at a playable prototype stage: a human tank and a computer-controlled tank fight in a hand-authored arena.

Start with [README.md](README.md) for the player-facing overview, controls, and build commands. This document records the engineering shape of the prototype and the assumptions that should survive future work. Forward-looking plans live in [`docs/roadmap.md`](docs/roadmap.md).

## Quick start

Run commands from the repository root:

```sh
dotnet build MonoTanx.slnx
dotnet test MonoTanx.slnx
dotnet run --project MonoTanx/MonoTanx.csproj -- --test
dotnet run --project MonoTanx/MonoTanx.csproj -- --test --seed 123   # reproducible random draws
```

## Architecture

### Application boundary

- `Program.cs` parses the command line into `GameOptions` (`--test`, `--two-player`, `--demo`, `--mute`, `--seed <integer>`, `--windowed`, `--scale <1-4>`), then constructs and runs `Tanx`, which creates the run's `RandomStreams`.
- `Tanx.cs` is the `Game` host. It runs a fixed 60 FPS step, draws the current stage to an 800×600 render target, and scales that to the window with proportional letterboxing. It also owns stage switching via `ChangeStage`.
- With `--test` the game starts directly in `GameStage`; without it, it starts at `HomeStage`: Start game, a Players: 1/2 toggle and Quit. It works with the mouse or the keyboard, and `MenuSelection` holds the highlight state. `--two-player` and `--demo` select the `MatchSetup` (who controls each seat; `--two-player` is preselected on the home screen): two humans, or two computers. With no human or two humans the overview camera is on. In a game, `Esc` returns to the home screen, or quits the application when started with `--test`. It runs fullscreen at the desktop resolution unless `--windowed` is given, which uses a window of 800×600 times `--scale` (default 2).
- Stages (`Stages/`) are the screens/game states. `Core/Stage.cs` is the base type.

### Project shape

- The logical game surface is 800×600.
- The world is a hand-authored orthogonal Tiled map. `arena_01.tmx` is 60×40 tiles at 16×16 pixels (960×640 world pixels).
- The top 80 logical pixels (five tiles) are reserved for the HUD. The camera view is the remaining playfield below the HUD.
- `Core/` holds reusable engine and gameplay code (including the testable rules in `TankMovement`, `Player`, `Shell`, `TankDamage` and `PickupRules`), `Controls/` holds UI primitives, and `Stages/` holds screen-specific behavior.

### Intended runtime structure

```text
GameStage
├── WorldMap (data and queries) and MapRenderer (drawing)
│   ├── Tiled map data
│   ├── Terrain rules
│   └── Map/object queries
├── Camera2D
├── Players
│   ├── Player simulation/state
│   ├── Human input path
│   └── Computer behavior path
├── Projectiles
├── Collision and damage resolution
├── Match state and spawning
├── Pickups and aircraft (later)
├── Visibility/fog system (later)
└── HUD
```

### Coordinate conventions

- World positions use floating-point pixel coordinates.
- The map tile grid is used for terrain and navigation queries.
- Entity positions represent their centers.
- Entity drawing converts world coordinates through the camera transform.
- HUD coordinates are display coordinates and are never camera transformed.
- Rotation is stored in radians. A tank's forward vector is derived from its heading.
- Map boundaries and collision geometry must not depend on the physical window scale.

## Design principles

- Keep simulation state independent from rendering and input devices.
- Use hand-authored Tiled maps; do not add procedural map generation yet.
- Use continuous world-pixel movement with tile-derived terrain queries.
- Configure terrain behavior as map data rather than hard-coded tile IDs.
- Keep movement, projectile, and visibility rules separate.
- Prefer small systems with narrow responsibilities over a large `GameStage`.
- Preserve fixed-step updates and deterministic behavior where practical.
- Introduce abstractions when a current feature needs them, not solely for hypothetical flexibility.

## Current implementation reference

How the current prototype behaves. Controls and the player-facing summary are in the README. Numbers quoted here are the current defaults; `Core/Tuning.cs` is the authority for every tuning value, and [`docs/tuning.md`](docs/tuning.md) explains what controls what.

### Player model

`MonoTanx.Core.Player` owns player-specific defaults and state.

Each player has:

- Name and sprite asset name.
- `IsComputerControlled` flag.
- Position, heading, animation state.
- Health: 100 maximum/starting health.
- Fuel: 200 maximum/starting fuel, displayed as a percentage.
- Movement speed: 90 pixels/second forward and 45 pixels/second in reverse on normal ground.
- Turn speed: 2.5 radians/second.
- Collision radius: 6 pixels.
- Up to two ammunition slots.

The first ammunition slot is consumed completely before the second is used. Ammunition is represented by `AmmunitionSlot` instances, each containing an ammunition type and remaining quantity.

### Movement and collision

- Movement is continuous in world pixels but validated against the tile map.
- Horizontal and vertical movement are resolved separately, allowing sliding along obstacles.
- A player cannot leave the map, enter blocked terrain, or overlap the other player.
- Fuel is consumed while turning and driving.
- Forward fuel rate is 4 units/second; reverse is 8 units/second; turning costs 0.25 units/second (defined in `GameStage`).
- Terrain can multiply movement speed and fuel cost independently.
- At zero fuel, the player cannot move or turn, but firing remains possible.

### Terrain rules

`TerrainKind` describes interaction semantics; numeric properties tune traversal.

Current movement blocking:

- Ground: traversable
- Bridge: traversable, including over water
- Water: blocked
- Wall: blocked
- Ravine: blocked
- Hill: blocked
- Reflective: blocked for tanks
- Out of bounds: blocked

Current projectile/vision behavior:

- Water and ravines do not block projectiles.
- Walls and hills stop projectiles and block vision.
- Reflective terrain reflects projectiles and blocks vision.
- Out-of-bounds stops projectiles and blocks vision.

Tileset properties are read from the external TSX:

- `TerrainKind`
- `MovementSpeedMultiplier` (float, default 1.0)
- `FuelCostMultiplier` (float, default 1.0)

This favors combinations such as a traversable bridge over water without creating enum variants such as `Ground-Muddy` or `Ground-Icy`.

### Camera and viewpoint

- The camera follows Player 1.
- Player 1 remains near the center of the playfield while the map can scroll.
- At map edges, the camera clamps and Player 1 moves away from center.
- Player 2 may be outside the visible viewport; current gameplay remains keyed to Player 1’s viewpoint.
- The world draw translation (camera plus shake) is rounded to whole pixels. A fractional translation lets point sampling pick up neighbouring texels of the tile atlas and shows as thin seams between tiles.
- `F3` toggles an overview camera that fits the whole arena in the playfield (about 0.81 zoom for the 960x640 arena in the 800x520 playfield, centred). `CameraView` (in `Core`) does the maths for both modes. The overview draws the world at full size into an offscreen target and scales that, because scaling the tile atlas directly would bring back neighbouring-tile seams. Whether the scale-up is smoothed or nearest-neighbour is `Tuning.Presentation.OverviewSmoothing`. The follow camera is the default; making the overview automatic for two-player games waits on the mode choice in #23.

### Shells and damage

The current default ammunition is a standard shell:

- 20 starting shells per player
- 3-second reload
- 5-second maximum flight time
- Damage: 12
- Shell speed: 260 pixels/second

Shells:

- Continue in the heading held at fire time.
- Are removed on expiry, out-of-bounds, or impact with walls/hills.
- Pass over water, ground, and ravines.
- Can hit either player, including the firing player after a ricochet.
- Reduce health by the ammunition type’s `Damage` value.
- A player reaching 0 health ends the game.

#### Reflective surfaces

Reflective tiles are treated as axis-aligned mirrors:

- A horizontal run of reflective tiles flips the shell’s Y velocity.
- A vertical run flips the shell’s X velocity.
- Isolated tiles fall back to a velocity-based axis choice.
- A short reflection cooldown prevents repeated reflections while a shell remains inside one tile.

On impact, the struck player receives a small valid knockback, a slight heading disruption, and a brief screen shake.

### HUD and debug overlay

The HUD is a fixed screen-space layer with a dark metallic-grey background.

Player 1 (left three quarters):

- HP percentage bar
- Shell count
- Reload `!` indicator
- Fuel percentage bar

Player 2 (right quarter):

- HP percentage bar
- Reload `!` indicator
- Fuel percentage bar

Fuel bars show remaining fuel in yellow and depleted fuel in red.

While holding `F5`, the overlay pauses the simulation and shows:

- Both player positions and tile coordinates
- Both headings in radians and degrees
- Player 2 AI mode and route progress
- AI fire cooldown and retaliation timer
- Loaded pickup count
- The run's random seed

### Player 2 computer behavior

Player 2 currently uses simple state-priority behavior:

1. Retaliation after being hit
   - Stops moving for approximately 1.5 seconds.
   - Turns toward Player 1.
   - Gets a shot opportunity as soon as it is aligned and reloaded.

2. Pickup seeking
   - If fuel is below 50% or ammunition is below half its starting quantity, seeks the nearest relevant active pickup.
   - Pickup seeking takes priority over normal combat and fleeing.

3. No-ammunition evasion
   - If no suitable pickup is available and ammunition is empty, continuously drives away from Player 1.
   - Existing collision resolution handles obstacles.

4. Long-range pursuit
   - When more than half the arena width away, chooses a heading toward Player 1 once and continues on that course without recalculating A* every frame.
   - Returns to close-range behavior once nearer.

5. Close-range combat navigation
   - Uses cached tile-grid A* routing toward a stand-off ring around Player 1.
   - Routes are rebuilt when Player 1 moves or the current route is exhausted.
   - When Player 1 is nearby and visible, Player 2 holds position while aiming rather than letting navigation overwrite its heading.

#### Player 2 firing

Player 2:

- Requires line of sight to Player 1; walls, hills, and reflective surfaces block firing.
- Uses an aim tolerance rather than perfect accuracy.
- Applies a reaction delay and fire cooldown.
- Continues correcting its heading toward Player 1 even when terrain currently blocks the shot.

AI tuning properties live on `Player`:

- `PreferredCombatDistanceTiles`
- `LongRangePursuitDistanceFraction`
- `ComputerAimToleranceRadians`
- `ComputerReactionDelaySeconds`
- `ComputerFireCooldownSeconds`

## Tiled map contract

The current map contract is intentionally small: one authored ground tile layer and one pickup object layer. Terrain behavior is attached to tile definitions in the external TSX; pickups are authored as objects and become mutable runtime entities after loading.

### Layers

Use stable names so maps can be replaced without code changes:

- A tile layer named `Terrain`, used for rendering and terrain queries. If no layer has that name, `WorldMap` falls back to the first tile layer, which is what the checked-in arena relies on.
- `Pickups`: object layer containing authored fuel and ammunition pickup spawn points.

Additional layers are not part of the current contract. Add one only when a concrete feature needs it and its query/rendering behavior is clear. Player starts currently use safe positions selected from the map; authored spawn objects are a later enhancement, not a prerequisite for the current loop.

Pickup objects use their Tiled `type`/`class` as `Fuel` or `Ammunition`, with optional `Amount`, `AmmunitionId`, and `SpriteAsset` properties. `WorldMap` parses these authored definitions into runtime data; collection state remains separate from immutable map data.

### Tile properties

Prefer independent typed properties over a single terrain enum. `TerrainKind` describes interaction categories (water, wall, ravine, hill, reflective, etc.); numeric multipliers tune traversal without creating enum variants such as `Ground-Muddy` or `Ground-Icy`. This permits combinations such as a bridge that visually crosses water but remains traversable.

Current properties:

| Property | Type | Meaning |
| --- | --- | --- |
| `TerrainKind` | enum | Supplies the current movement, projectile, vision, and reflection behavior. |
| `MovementSpeedMultiplier` | float >= 0 | Multiplies tank movement speed while occupying the tile; defaults to `1.0`. |
| `FuelCostMultiplier` | float >= 0 | Multiplies movement/turn fuel cost while occupying the tile; defaults to `1.0`. |

Potential future properties such as `Destructible` or independent blocking flags should be added only when the corresponding gameplay system requires them. They are not part of the current map contract.

Example terrain behavior:

| Terrain | Movement | Projectiles | Vision | Special |
| --- | --- | --- | --- | --- |
| Ground | Pass | Pass | Pass | None |
| Water | Block | Pass | Pass | Bridges override movement. |
| Bridge | Pass | Pass | Pass | Usually drawn above water. |
| Wall | Block | Block | Block | May later be destructible. |
| Ravine | Block | Pass | Pass | Shots can cross it. |
| Hill | Block | Block | Block | Hard tactical cover. |
| Reflective wall | Block | Reflect | Block | Uses collision normal. |

### Map objects

Current supported objects:

- `Fuel`: pickup position and optional amount/sprite metadata.
- `Ammunition`: pickup position and optional amount/ammunition/sprite metadata.

Future map-authored starts or aircraft paths should be additive. Missing pickup metadata uses documented defaults; malformed required map structure should still produce a clear load-time error naming the map or layer/object type.

Note: the checked-in arena names its single tile layer `Tile Layer 1`, so it is found by the first-tile-layer fallback. A tile with no terrain definition, or a missing layer, is treated as ground.

### Pickup objects in detail

Create an object layer named `Pickups` in the map. Place point or rectangle objects on it.

Objects should use custom classes/types:

- `Fuel`
- `Ammunition`

Useful class properties:

- `Amount` (integer)
- `AmmunitionId` (string, ammunition objects)
- `SpriteAsset` (string, e.g. `Sprites/fueldrop_1`)

If `Amount` or `SpriteAsset` is not serialized on an instance, the loader supplies defaults:

- Fuel amount: 50
- Ammunition amount: 5
- Fuel sprite: `Sprites/fueldrop_1`
- Ammunition sprite: `Sprites/ammodrop_1`

Pickup locations are authored in Tiled, but pickups are runtime entities. They animate from four-frame 64×16 sheets (four 16×16 frames), can be collected, and then become inactive/disappear. Fuel restores up to the player’s maximum. Ammunition is added to the matching slot or the first slot.

## Seats and controllers

Player 1 and Player 2 are *seats*; human and computer are *controllers*; any combination is valid (see the design tenet in `AGENTS.md`).

- `MatchSetup` (in `Core`) says who controls each seat. `OnePlayer` is human + computer, `TwoPlayer` is human + human, and `Demo` is computer + computer. Its helpers give the human count, the follow-camera seat (the first human seat) and whether the game starts in the overview (when there is not exactly one human).
- `GameStage` treats the seats symmetrically. Each tank has its own `ComputerState` (a private nested class holding its route, pickup target and timers, plus who it is and who it is playing), and every part of the computer's logic takes that state: nothing refers to "Player 1" or "Player 2" inside the AI. The state moves into `Core` as a controller in #51.
- `TankCommand` (turn, drive, fire) is what a controller wants a tank to do for one update. A human seat builds one from its keys with `SeatKeys.ToCommand` (the two keyboard layouts are data in `SeatKeys`; fire is true only on the update the key goes down), and the stage applies it with `TankMovement.ApplyInput(command)` and `Player.TryFire`. `TankMovement.FuelCost` gives what a command would cost, and `ApplyInput` charges exactly that. The computer will produce the same command once its controller moves into `Core` (#51; see `docs/ai-controller-spec.md`). `PickupState` is the `Core` view of a pickup (its spawn and whether it is still there).
- Control can change in play (`F2`, `F4`). The follow camera and the fire shake follow the first human seat as it changes, and with no human the camera is not followed.
- Sounds are per seat, not per controller: Player 2's sounds and engine are pitch-shifted whether it is human or computer.

## Audio

Design and plan: [`docs/audio-spec.md`](docs/audio-spec.md) and [`docs/audio-plan.md`](docs/audio-plan.md).

- `GameAudio` (in `Core`) loads the sounds through the content pipeline and plays them with `SoundEffect.Play`. `GameStage` calls it where things happen. The rules themselves stay silent and know nothing about audio.
- `SoundCue` lists the one-shot sounds. `SoundMix` (pure) gives each cue's volume and pitch from `Tuning.Audio`; Player 2's sounds are pitch-shifted so the two tanks can be told apart.
- Audio must never stop the game: with `--mute`, with no audio device (`NoAudioHardwareException`), or with a missing file, the affected sounds are skipped with a console message.
- Assets are WAV only (16-bit PCM, `WavImporter` and `SoundEffectProcessor`, which need no `ffmpeg`) in `Content/Audio/`, named after the cues. The current files are generated placeholders (`tools/generate_placeholder_audio.py`); `Content/Audio/README.md` explains how to replace them. `AudioAssetTests` check every cue has a valid WAV and a pipeline entry.
- All the planned sounds are in: fire, reload ready, explosion, ping, crump and pickup, plus a quiet engine drone for each tank.
- Engines: `TankMotionClassifier` judges each tank as idle, forward, reverse or turn from how far it actually moved and turned in the update (driving beats turning, and a tank held against a wall counts as idle), the same for a human or the computer. `EngineMix` (pure) moves the drone's volume and pitch towards the targets for that motion at a fixed rate in elapsed time (so it does not click and takes the same time at any frame rate). `EngineSound` plays one looping `SoundEffectInstance` per tank from that mix; Player 2's is pitch-shifted. The engines fade out under the `F5` pause overlay and are stopped and disposed through `Stage.OnLeave` when the game screen is left.
- `Shell.Step` reports why a shell ended (`ShellFate`: in flight, expired, hit terrain, hit a tank, too many reflections) and whether it reflected, and `Player.TickReload` reports the moment a reload finishes with ammunition left. `GameStage` turns those into cues: a reflection plays Ping, a solid hit Crump, a tank hit Explosion, and a finished reload Reload (Player 2's fire and reload sounds are pitch-shifted). A shell that expires makes no sound.

## Important invariants

Preserve these behaviours when changing movement, projectiles, rendering, or timing:

- Either seat can be human or computer: Player 1 and Player 2 are seats, human and computer are controllers, and any combination is valid. Rules are written in terms of "self" and "the opponent" and nothing in new code assumes Player 1 is human or Player 2 is the computer. (Some seat-specific code remains in `GameStage`; see #63 and #51.)
- Simulation state stays independent from rendering and input devices, so rules can be exercised by xUnit without a graphics device.
- Updates use a fixed 60 FPS step (`IsFixedTimeStep`, vsync on), and the simulation is time-based: every rule takes elapsed seconds, and `FrameRateIndependenceTests` run the same simulated time at 30, 60 and 120 steps per second and expect the same result for driving, reversing, turning, fuel, reload, and shell flight. New rules that depend on time should take `elapsed` and get a test like these.
- Map boundaries and collision geometry do not depend on the physical window scale.
- HUD coordinates are display coordinates and are never camera transformed.
- Terrain behavior comes from tileset properties, not hard-coded tile IDs.
- Screen shake (`ScreenShake`) runs entirely on elapsed time: its jitter is resampled at a fixed 60 Hz inside `Update`, so how often it is drawn does not change it or the random draws it consumes. It uses the cosmetic random stream.
- Debug rendering (the `F5` overlay) must not change simulation behavior. Holding `F5` pauses the whole update, including the `Esc` check.
- Fuel never goes below zero. Turning and driving each cost fuel scaled by the terrain fuel multiplier; a tank without enough fuel cannot turn or drive, but can still fire.
- Movement is resolved one axis at a time, so tanks slide along obstacles. A tank cannot enter movement-blocking terrain, leave the map, or overlap the other tank.
- Out-of-bounds counts as blocking for movement, projectiles, and vision. Missing terrain data defaults to ground.
- Firing is edge-triggered and refused while the reload timer is running or no ammunition remains. Slots are consumed in order, and firing sets the reload timer from the ammunition type.
- A shell is removed on expiry, on hitting projectile-blocking terrain, on hitting either tank (including the tank that fired it), or after more than 8 reflections.
- A pickup is collected once and then stays inactive. Fuel is clamped to the tank's maximum; ammunition is added without a cap.
- `F1` refills Player 1's fuel and ammunition only; it does not touch health or Player 2.
- `F2` and `F4` toggle Player 2 and Player 1 between computer and human control; the toggled seat's computer state (route, pursuit, timers) is cleared so it starts afresh when it takes control back.
- Reaching 0 health currently exits the game; there is no score or round state yet.
- Randomness comes from `RandomStreams`, created in `Tanx` from the master seed (`--seed <integer>`, otherwise random, shown in the `F5` overlay). The gameplay stream drives the heading disruption on a hit; the cosmetic stream drives screen shake, so visual draws never change gameplay. Streams are passed to the code that needs them, not held globally. The seed fixes random draws but not real input or frame timing, so it does not give full replay.

## Testing strategy

### Unit tests

Prioritize deterministic logic that needs no graphics device:

- Coordinate conversion.
- Terrain property parsing and queries.
- Tank command interpretation and movement.
- Collision resolution.
- Reload and ammunition state.
- Segment/tile and segment/tank intersection.
- Reflection-vector calculation.
- Visibility queries.
- Basic AI decisions from controlled world snapshots.

### Integration tests

- Load each checked-in map and validate its required layers and objects.
- Simulate a tank moving against representative terrain.
- Simulate projectile paths through passable, blocking, and reflective tiles.
- Run fixed seeded match scenarios for a bounded number of updates.

### Manual checks

- Driving and steering feel.
- Camera transitions at all four map edges and corners.
- Collision sliding and tight spaces.
- Firing feedback and reflection readability.
- HUD readability at supported window sizes.
- Match pacing and AI fairness.

Never claim manual gameplay validation unless the relevant path was actually exercised.

## Development workflow

The workflow (issue, optional spec and plan, branch, develop and test, PR, user merge, local tidy-up) is defined in [`AGENTS.md`](AGENTS.md). Specs and plans live under `docs/`; the README and this file are the authoritative summaries of the present project.

Small observations intentionally deferred from active work are recorded in [`docs/deferred-snags.md`](docs/deferred-snags.md).

## Known debt and deliberately deferred work

- Player 2 does not yet deliberately plan routes to pickups beyond nearest-target routing.
- Pickup respawn, aircraft drops, and dynamic spawning are not implemented.
- Ammunition switching is automatic only; no manual selection mechanic exists yet.
- Projectile reflection is intentionally approximate and should later use exact tile-edge normals or authored surface metadata.
- There is no projectile-vs-projectile, tank armor, score, round reset, or explicit game-over screen yet.
- Destructible terrain, fog of war, advanced AI difficulty, second-human-player mode, and fuel/ammo balancing remain future work.

## Recommended next step

See the resume point in [`docs/roadmap.md`](docs/roadmap.md): a hardening pass with focused tests, then score and round reset.

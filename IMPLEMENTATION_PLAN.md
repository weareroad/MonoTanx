# MonoTanx Implementation Plan

## Product direction

MonoTanx is a top-down, tile-map-based tank combat game inspired by the Tanks mode in Atari 2600 Combat. Two tanks navigate a hand-authored arena, use terrain tactically, and attempt to destroy one another.

The initial opponent is computer controlled. The design should later support a second human controller, smarter AI, destructible scenery, fog of war, ammunition drops, and fuel without replacing the core simulation.

## Current baseline

- .NET 9 and MonoGame DesktopGL 3.8.5.1.
- `GameStage` starts directly when the application launches.
- A Tiled map is rendered in world space through a following camera.
- Rick is temporary tank artwork.
- W/S or Up/Down move forward and reverse.
- A/D or Left/Right rotate the tank.
- Escape exits the game.
- The camera follows the player and clamps to the map boundaries.
- `OldGameStage` preserves the earlier sprite experiment.

## Design principles

- Keep simulation state independent from rendering and input devices.
- Use hand-authored Tiled maps; do not add procedural map generation yet.
- Use continuous world-pixel movement with tile-derived terrain queries.
- Configure terrain behavior as map data rather than hard-coded tile IDs.
- Keep movement, projectile, and visibility rules separate.
- Prefer small systems with narrow responsibilities over a large `GameStage`.
- Preserve fixed-step updates and deterministic behavior where practical.
- Introduce abstractions when a current feature needs them, not solely for hypothetical flexibility.

## Intended runtime structure

```text
GameStage
├── WorldMap
│   ├── Tiled map data
│   ├── Terrain rules
│   └── Map/object queries
├── Camera2D
├── Tanks
│   ├── Tank simulation
│   ├── HumanTankController
│   └── ComputerTankController
├── Projectiles
├── Collision and damage resolution
├── Match state and spawning
├── Pickups and aircraft (later)
├── Visibility/fog system (later)
└── HUD
```

## Coordinate conventions

- World positions use floating-point pixel coordinates.
- The map tile grid is used for terrain and navigation queries.
- Entity positions represent their centers.
- Entity drawing converts world coordinates through the camera transform.
- HUD coordinates are display coordinates and are never camera transformed.
- Rotation is stored in radians. A tank's forward vector is derived from its heading.
- Map boundaries and collision geometry must not depend on the physical window scale.

## Tiled map contract

### Layers

Use stable layer names so maps can be replaced without code changes:

- `Ground`: base visual terrain.
- `Terrain`: obstacles and terrain with gameplay behavior.
- `Decoration`: non-colliding visual details, if needed.
- `Spawns`: object layer containing player and opponent spawn points.
- `Markers`: optional object layer for aircraft paths, pickup zones, or AI hints.
- `Pickups`: object layer containing authored fuel and ammunition drop spawn points.

The renderer may draw multiple tile layers, but only layers or tiles carrying terrain properties participate in gameplay queries.

Pickup objects use their Tiled `type`/`class` as `Fuel` or `Ammunition`, with optional `Amount` and `AmmunitionId` properties. `WorldMap` parses these authored spawn points into runtime definitions; collection state remains separate from immutable map data.

### Tile properties

Prefer independent typed properties over a single terrain enum. `TerrainKind` describes interaction categories (water, wall, ravine, hill, reflective, etc.); numeric multipliers tune traversal without creating enum variants such as `Ground-Muddy` or `Ground-Icy`. This permits combinations such as a bridge that visually crosses water but remains traversable.

Initial properties:

| Property | Type | Meaning |
| --- | --- | --- |
| `BlocksMovement` | bool | Tanks cannot occupy the tile. |
| `BlocksProjectiles` | bool | Projectiles collide with the tile. |
| `BlocksVision` | bool | Line-of-sight and fog calculations stop at the tile. |
| `ReflectsProjectiles` | bool | A projectile reflects instead of being destroyed. |
| `Destructible` | bool | Terrain can later receive damage and change state. |
| `MovementSpeedMultiplier` | float >= 0 | Multiplies tank movement speed while occupying the tile; defaults to `1.0`. |
| `FuelCostMultiplier` | float >= 0 | Multiplies movement/turn fuel cost while occupying the tile; defaults to `1.0`. |

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

Spawn objects should have a stable `Type` or class and optional properties:

- `PlayerSpawn`: position and initial rotation.
- `OpponentSpawn`: position and initial rotation.
- Later: `DropZone`, `AircraftPath`, `PatrolPoint`, and `FuelSpawn`.

Missing required objects should produce a clear load-time error naming the map and object type.

## Core gameplay interfaces

These are target shapes, not mandatory exact signatures. Adjust them when implementation evidence suggests a simpler design.

```csharp
public readonly record struct TankCommand(
    float Throttle,
    float Steering,
    bool Fire);

public interface ITankController
{
    TankCommand GetCommand(GameTime gameTime, Tank tank, GameWorld world);
}
```

`Throttle` and `Steering` should be normalized to `-1..1`. The tank consumes commands without knowing whether they came from a keyboard, gamepad, or AI.

The map-query API should answer capabilities rather than expose tile IDs:

```csharp
bool BlocksMovement(Vector2 worldPosition);
bool BlocksProjectiles(Vector2 worldPosition);
bool BlocksVision(Vector2 worldPosition);
bool ReflectsProjectiles(Vector2 worldPosition);
Point WorldToTile(Vector2 worldPosition);
Rectangle GetTileBounds(Point tile);
```

Collision queries will probably need richer results later, including the impacted tile, contact point, and surface normal.

## Implementation phases

### Phase 1: Map and terrain foundation

Goal: make the map a dependable source of rendering, dimensions, spawn data, and terrain behavior.

#### 1.1 Refactor `TileMapManager`

Status: complete (2026-08-29). Renamed the boundary to `WorldMap`, preserved rendering and executable-relative loading, and added basic map dimensions and coordinate helpers.

- Rename it to `WorldMap` once its responsibilities go beyond rendering.
- Remove unused fields and legacy comments.
- Preserve working-directory-independent loading.
- Expose map dimensions, tile dimensions, and safe coordinate conversions.
- Split loading, drawing, and terrain queries into clear methods.
- Validate missing maps, tilesets, layers, and malformed properties with useful exceptions.

Acceptance checks:

- Existing map still renders.
- World and tile coordinate conversion is correct at corners and boundaries.
- Out-of-map queries are treated as blocked for movement and projectiles.
- Build remains warning-free.

#### 1.2 Establish the Tiled map contract

Status: in progress (2026-08-29). `WorldMap` reads `TerrainKind`, `MovementSpeedMultiplier`, and `FuelCostMultiplier` properties from external TSX XML while TiledCS remains responsible for map geometry and rendering. Missing numeric properties default to `1.0`. The checked-in map still needs the final layer names, terrain properties, and spawn objects.

- Rename the current test map to a suitable first-level name, such as `arena_01.tmx`.
- Add the agreed layer names.
- Add player and opponent spawn objects.
- Add terrain properties to the tileset.
- Keep source TMX/TSX files copied to the build output.
- Document the map contract near the map assets if Tiled metadata alone is insufficient.

Acceptance checks:

- The map loads without hard-coded spawn coordinates.
- Terrain queries return the expected values for at least one tile of each initial terrain type.
- A malformed or incomplete map fails with an actionable message.

#### 1.3 Add focused tests

- Create a .NET 9 test project and add it to the solution.
- Test coordinate conversions and terrain-property parsing.
- Test map-edge behavior.
- Keep tests independent of a graphics device where possible.

Acceptance checks:

- `dotnet test MonoTanx.sln` passes.
- Tests do not require a graphical desktop.

### Phase 2: Tank entity and controller separation

Goal: replace the temporary movement code in `GameStage` with a reusable tank simulation.

#### 2.1 Create `Tank`

- Store world position, heading, movement speed, reverse-speed modifier, turn rate, and collision bounds.
- Move tank animation state out of `GameStage`.
- Accept a `TankCommand` during update.
- Keep the temporary Rick texture behind the tank renderer or animation component.
- Do not let `Tank` read keyboard state.

Acceptance checks:

- Current driving behavior feels unchanged or deliberately improved.
- Forward, reverse, and rotation are frame-rate independent.
- Tank state can update in a unit test without loading graphics.

#### 2.2 Add `HumanTankController`

- Map W/S/A/D and cursor keys to normalized commands.
- Add fire input, initially Space or a clearly documented key.
- Handle edge-triggered actions such as firing separately from held movement input.

Acceptance checks:

- Both keyboard layouts produce identical tank commands.
- Holding fire does not bypass reload rules once firing is implemented.

#### 2.3 Add `Camera2D`

- Move follow and map-clamping logic out of `GameStage`.
- Follow the player tank center.
- Keep the tank centered while scrolling is possible.
- At map edges, clamp the camera and allow the tank to move away from display center.
- Expose a view matrix and visible world bounds.

Acceptance checks:

- No visual jump occurs when the camera begins or stops clamping.
- Maps smaller than the logical display are handled deliberately.
- Camera behavior is independent of physical window resolution.

### Phase 3: Terrain collision

Goal: tanks move smoothly but cannot enter blocked terrain or leave the map.

#### 3.1 Define tank collision shape

- Start with a circle or compact axis-aligned box smaller than the artwork.
- Keep collision shape independent of the temporary sprite dimensions.
- Visualize collision bounds behind a debug toggle.

#### 3.2 Resolve movement against tiles

- Query only tiles overlapped by the proposed collision bounds.
- Resolve X and Y movement separately initially, allowing tanks to slide along walls.
- Prevent tunnelling at the maximum supported tank speed.
- Treat out-of-map space as blocked.

Acceptance checks:

- Tank cannot cross water, walls, ravines, or hills.
- Tank can cross bridges.
- Tank slides along obstacles without becoming stuck during ordinary contact.
- Rotation near scenery does not unexpectedly teleport the tank.

### Phase 4: Projectiles and firing

Goal: deliver the first complete combat interaction.

#### 4.1 Add weapon state

Status: prototype complete (2026-08-29). `GameStage` currently provides a 20-shell stock, Space-to-fire, and a three-second reload lockout using temporary placeholder rendering.

- Add ammunition count, reload duration, and reload timer to a weapon component or tank weapon state.
- Fire from a muzzle position derived from tank position and heading.
- Decide whether an empty tank can reload from reserve ammunition or requires pickups.

#### 4.2 Add projectile entities

Status: prototype complete (2026-08-29). Shells have world position, velocity, age, bounded lifetime, and capped reflection count; they currently remain local to `GameStage` pending extraction.

- Store position, direction, speed, owner, age/range, and remaining reflections.
- Use swept movement or ray/segment checks to avoid tunnelling through tiles and tanks.
- Remove projectiles after impact, maximum lifetime/range, or exhausted reflections.

#### 4.3 Add terrain responses

- Pass over water and ravines.
- Stop on ordinary walls and hills.
- Reflect from reflective surfaces using a collision normal.
- Add a small post-reflection positional offset to prevent repeated collision with the same surface.
- Cap projectile lifetime and reflection count.

#### 4.4 Add tank hits

- Exclude the owner briefly or until the projectile clears its firing bounds.
- Resolve hits consistently and prevent one projectile causing multiple hits.
- Initially respawn or reset the round after a visible hit response.

Acceptance checks:

- Reload time cannot be bypassed by holding or tapping fire rapidly.
- Direct and reflected shots can hit tanks.
- Projectiles never remain alive indefinitely.
- Terrain behavior matches the map-property table.

### Phase 5: Match loop and first opponent

Goal: produce a small but complete playable match.

#### 5.1 Match state

- Add round start, active play, hit response, score update, and respawn states.
- Load spawn positions and headings from the map.
- Display score, ammunition, and reload state in the HUD.

#### 5.2 Basic computer controller

Start with readable, deterministic behavior rather than sophisticated pathfinding:

- Turn toward the player when there is direct line of sight.
- Fire when aligned, loaded, and not obstructed.
- Move toward or away from the player based on distance.
- Detect lack of progress and change steering direction.
- Add reaction delay and small seeded aim error for fairness.

The AI must produce `TankCommand` values through the same interface as a human controller.

Acceptance checks:

- A full round can be won or lost.
- AI never drives through blocked terrain.
- AI does not fire through hills or walls when its visibility query says the player is hidden.
- Matches are deterministic when started with the same seed and input sequence.

### Phase 6: Second human player

Goal: allow either AI or human control of tank two.

- Add a second keyboard layout or gamepad support.
- Select the controller type when creating the match.
- Keep tank rules identical regardless of controller.
- Avoid baking player-one assumptions into camera and HUD systems.

Potential camera decision to make at this phase:

- Single camera following player one.
- Shared camera that frames both tanks.
- Split screen.
- Fixed arena-sized view for local multiplayer maps.

Do not solve this decision prematurely.

### Phase 7: Destructible terrain

Goal: allow selected scenery to take damage and alter the arena.

- Represent dynamic terrain state separately from immutable source map data.
- Define damage stages and replacement tiles or overlays.
- Update movement, projectile, visibility, and AI queries immediately after destruction.
- Decide whether terrain resets each round.

Acceptance checks:

- Destroying terrain cannot leave stale collision or visibility data.
- Modified terrain renders consistently with its gameplay state.

### Phase 8: Fog of war

Goal: restrict information using terrain-aware visibility.

- Calculate visibility independently for each player.
- Hills and walls block vision; water and ravines do not.
- Decide whether previously seen terrain remains dimly remembered.
- Hide enemy tanks, projectiles, pickups, or effects according to explicit rules.
- Give AI only information it is allowed to know unless a difficulty mode explicitly cheats.

Performance should be measured before choosing ray casting, shadow casting, or another field-of-view algorithm.

### Phase 9: Fuel, ammunition drops, and aircraft

Status: fuel prototype complete (2026-08-29). `GameStage` starts with a full 100-unit tank, consumes separate tunable rates for turning, forward movement, and reverse movement, and delegates terrain cost scaling to `WorldMap.GetFuelCostMultiplier(...)`. Reverse travel is currently half forward speed and costs twice the forward fuel rate. Pickups, drops, and zero-fuel feedback remain future work.

Goal: add resource pressure and dynamic objectives.

- Consume fuel based on movement, not simply elapsed match time, unless playtesting suggests otherwise.
- Define behavior at zero fuel: unable to move but still able to rotate/fire, or fully immobilized.
- Implement ammunition and fuel pickups as world entities.
- Drive aircraft along paths defined in the map object layer.
- Telegraph drop locations and timing so drops create tactical choices.
- Use seeded scheduling for repeatable tests.

## Debugging facilities

Add these incrementally behind a single debug toggle:

- Current FPS and fixed-update timing.
- Tank collision bounds and heading vectors.
- Camera bounds and visible world rectangle.
- Tile coordinates and terrain properties under the cursor.
- Projectile traces, impact normals, and reflection count.
- AI target, steering choice, path, and line of sight.
- Fog/visibility overlay.

Debug rendering must not change simulation behavior.

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

## Definition of first playable milestone

The first playable milestone is complete when:

- One valid hand-authored arena loads through the documented map contract.
- Player and opponent spawn from map objects.
- Both tanks use the same reusable tank simulation.
- Player controls are handled through `HumanTankController`.
- A basic AI controls the opponent through `ComputerTankController`.
- Tanks collide correctly with map terrain and boundaries.
- Both tanks can fire ammunition with a visible reload delay.
- Projectiles interact correctly with water, ravines, walls, hills, and reflective surfaces.
- Hits update a score and reset the round.
- HUD shows score, ammunition, and reload state.
- Automated tests cover the core non-rendering rules.
- The solution builds without warnings.

Fuel, fog of war, aircraft, supply drops, destructible terrain, and advanced AI are explicitly outside the first playable milestone.

## Recommended next task

Start with Phase 1.2: establish the Tiled map contract on a first arena map. Do not combine map metadata work with tank refactoring in the same change.

## Model-switching workflow

To conserve tokens and make handoffs reliable:

1. Ask for one numbered task or tightly related pair of tasks at a time.
2. Have the model read `AGENTS.md` and this plan before editing.
3. Require it to inspect current code rather than assume earlier phases were completed exactly as written.
4. End each task with build/test results and a concise note about deviations from this plan.
5. Commit completed tasks separately when practical.
6. Use a stronger model for architecture changes, collision mathematics, projectile reflection, visibility, and difficult debugging.
7. Use a lighter model for mechanical extraction, renaming, simple entity fields, map metadata, HUD work, and straightforward tests.

Suggested prompt shape:

```text
Implement task 1.1 from IMPLEMENTATION_PLAN.md. Read AGENTS.md and inspect the
current repository first. Keep the change limited to that task, run the relevant
build/tests, and report any plan assumptions that did not match the code.
```

## Decision log

Record meaningful decisions here as implementation proceeds.

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-08-29 | Use hand-authored Tiled maps. | Enables deliberate, playable arenas without premature procedural-generation work. |
| 2026-08-29 | Keep world rendering inside a fixed logical render target. | The render target naturally acts as the camera viewport and separates logical resolution from window size. |
| 2026-08-29 | Separate controllers from tank simulation. | Supports human, local multiplayer, and evolving AI without duplicating tank behavior. |
| 2026-08-29 | Separate movement, projectile, and visibility terrain properties. | Ravines, water, bridges, walls, and hills require independent rule combinations. |
| 2026-08-29 | Read tile `TerrainKind` metadata directly from TSX XML for now. | Released TiledCS does not expose per-tile properties; this preserves stable package usage while keeping Tiled as the authoring source. |

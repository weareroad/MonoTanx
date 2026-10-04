# MonoTanx roadmap

## Product direction

MonoTanx is a top-down, tile-map-based tank combat game inspired by the Tanks mode in Atari 2600 Combat. Two tanks navigate a hand-authored arena, use terrain tactically, and attempt to destroy one another.

The initial opponent is computer controlled. The current prototype already demonstrates the core loop: two `Player` entities move around a hand-authored arena, spend fuel, collect pickups, and exchange shells. The plan now treats that working loop as the foundation. Later improvements should consolidate and test the existing behavior before introducing abstractions that do not yet have a demonstrated need.

## Where the current design lives

The current baseline, design principles, runtime structure, coordinate conventions, Tiled map contract, and testing strategy are documented in [`onboarding.md`](../onboarding.md); this roadmap covers what comes next.

## Core gameplay interfaces

These are optional seams, not requirements to rename or rebuild the current code. The current concrete seams are `Player`, `WorldMap`, and `GameStage`. Add command/controller interfaces only when they simplify a requested feature or make deterministic testing materially easier.

```csharp
public readonly record struct TankCommand(
    float Throttle,
    float Steering,
    bool Fire);

public interface IPlayerController
{
    TankCommand GetCommand(GameTime gameTime, Player player, WorldMap world);
}
```

`Throttle` and `Steering` should be normalized to `-1..1`. If introduced, the player consumes commands without knowing whether they came from a keyboard, gamepad, or AI. Until then, preserve the current direct input and computer-behavior paths.

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

Goal: make the map a dependable source of rendering, dimensions, pickup data, and terrain behavior.

#### 1.1 Establish `WorldMap`

Status: complete (2026-08-29). `WorldMap` replaced the old rendering-only boundary, preserves executable-relative loading, and provides map dimensions, coordinate helpers, terrain queries, collision queries, visibility queries, and pickup definitions.

- Rename it to `WorldMap` once its responsibilities go beyond rendering.
- Remove unused fields and legacy comments.
- Preserve working-directory-independent loading.
- Expose map dimensions, tile dimensions, and safe coordinate conversions.
- Keep loading, drawing, terrain queries, and pickup parsing in clear methods.
- Validate missing maps, tilesets, layers, and malformed properties with useful exceptions.

Acceptance checks:

- Existing map still renders.
- World and tile coordinate conversion is correct at corners and boundaries.
- Out-of-map queries are treated as blocked for movement and projectiles.
- Build remains warning-free.

#### 1.2 Stabilize the current Tiled map contract

Status: mostly complete (2026-09-02). The checked-in arena uses one tile layer (currently named `Tile Layer 1`) for ground/terrain and a `Pickups` object layer. `WorldMap` reads `TerrainKind`, `MovementSpeedMultiplier`, and `FuelCostMultiplier` from external TSX XML and parses Fuel/Ammunition objects. Missing numeric and pickup properties use documented defaults.

- Keep the first arena named `arena_01.tmx` and name its tile layer `Terrain` (the name `WorldMap` looks for first; otherwise it uses the first tile layer).
- Keep terrain properties in the TSX tileset and pickup definitions in the TMX object layer.
- Decide later whether authored player/opponent spawn objects add enough value to replace safe-position selection.
- Keep source TMX/TSX files copied to the build output.
- Document the map contract near the map assets if Tiled metadata alone is insufficient.

Acceptance checks:

- The map loads without requiring authored spawn objects; safe starting positions are selected from map geometry.
- Terrain queries return the expected values for at least one tile of each initial terrain type.
- A malformed or incomplete map fails with an actionable message where the current loader requires structure.

#### 1.3 Add focused tests around the working rules

Status: complete. `MonoTanx.Tests` (xUnit, in `MonoTanx.slnx`) covers `WorldMap` (via fixture maps and the checked-in arena), `Player`, `TankMovement`, `Shell`, `TankDamage`, `PickupRules`, `RandomStreams` and `GameOptions`, without a graphics device. See `hardening-spec.md` and `hardening-plan.md`.

- ~~Create a .NET 10 test project and add it to the solution without requiring a graphics device.~~ Done.
- Test coordinate conversions and terrain-property parsing.
- Test map-edge behavior.
- Keep tests independent of a graphics device where possible.

Acceptance checks:

- `dotnet test MonoTanx.slnx` passes.
- Tests do not require a graphical desktop.

### Phase 2: Consolidate the Player simulation and stage responsibilities

Goal: protect and simplify the working `Player`-based simulation before adding new gameplay systems.

#### 2.1 Consolidate `Player`

Status: foundation complete (2026-08-29). `Player` stores movement, heading, collision radius, health, fuel, ammunition, reload, animation, and computer-tuning state. Both combatants use the same entity type.

- Keep `Player` as the entity name unless a concrete need makes a rename worthwhile.
- Move only clearly reusable simulation operations out of `GameStage`; do not rewrite working behavior for abstraction alone.
- Keep rendering assets and keyboard state outside `Player` where extraction naturally reduces coupling.

Acceptance checks:

- Current driving behavior feels unchanged or deliberately improved.
- Forward, reverse, and rotation are frame-rate independent.
- Player state can update in a unit test without loading graphics.

#### 2.2 Isolate input when needed

Status: functional, not yet extracted. `GameStage` maps WASD/Space and cursor keys/Enter directly and uses edge-triggered firing.

- Preserve the current mappings and behavior.
- Extract a small human-input adapter only when a second human mode, input remapping, or testability requires it.

Acceptance checks:

- Both keyboard layouts produce identical tank commands.
- Holding fire does not bypass reload rules once firing is implemented.

#### 2.3 Consolidate camera behavior

Status: functional in `GameStage`. Follow and map-clamping behavior works at the current logical resolution.

- Move follow and map-clamping logic out of `GameStage` only if the camera becomes independently reusable or needs focused tests.
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

Status: prototype functional. `WorldMap.CanOccupyCircle` and `GameStage.MoveTank` provide circle-vs-tile collision, separate horizontal/vertical movement, sliding, and boundary blocking. Remaining work is focused on edge cases, tunnelling limits, and tests.

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

Status: functional prototype (2026-08-29). `Player` and `GameStage` provide ammunition state, edge-triggered firing, muzzle placement, and reload lockout using placeholder shell rendering.

- Add ammunition count, reload duration, and reload timer to a weapon component or tank weapon state.
- Fire from a muzzle position derived from tank position and heading.
- Decide whether an empty tank can reload from reserve ammunition or requires pickups.

#### 4.2 Add projectile entities

Status: functional prototype (2026-08-29). Shells have world position, velocity, age, bounded lifetime, tank damage, and capped reflection count; the rules now live in `Core/Shell.cs` with tests; `GameStage` keeps shell drawing and orchestration.

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

Status: playable prototype. The game launches directly into a two-player-versus-computer arena, but it is not yet a complete scored round loop.

#### 5.1 Match state

Status: partial. Active play, health reduction, HUD resource feedback, and a terminal hit response exist. Score, explicit round states, respawn/reset, and a game-over screen remain.

- Add round start, active play, hit response, score update, and respawn states.
- Load spawn positions and headings from the map.
- Display score, ammunition, and reload state in the HUD.

#### 5.2 Basic computer behavior

Status: functional prototype. The computer opponent can pursue, evade, route around terrain, seek pickups, check line of sight, aim, and fire. It is deliberately kept in `GameStage` until extracting a controller provides concrete value.

Start with readable, deterministic behavior rather than sophisticated pathfinding:

- Turn toward the player when there is direct line of sight.
- Fire when aligned, loaded, and not obstructed.
- Move toward or away from the player based on distance.
- Detect lack of progress and change steering direction.
- Add reaction delay and small seeded aim error for fairness.

If a second human mode or AI testing requires it, introduce a shared command/controller interface around the existing behavior. This is now an incremental refactoring task, not a prerequisite for continuing gameplay work.

Acceptance checks:

- A full round can be won or lost.
- AI never drives through blocked terrain.
- AI does not fire through hills or walls when its visibility query says the player is hidden.
- Matches are deterministic when started with the same seed and input sequence.

### Phase 6: Second human player

Goal: allow either AI or human control of tank two.

Status: partially present. The same `GameStage` path already supports cursor-key/Enter control when `Player 2` is not computer controlled; there is not yet a menu or match-creation choice for selecting that mode.

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

Status: fuel and static pickup prototype complete (2026-08-29). `GameStage` starts tanks with fuel, consumes separate tunable rates for turning, forward movement, and reverse movement, delegates terrain cost scaling to `WorldMap.GetFuelCostMultiplier(...)`, and collects authored Fuel/Ammunition objects. Reverse travel is currently half forward speed and costs twice the forward fuel rate. Dynamic drops, pickup respawn, and zero-fuel feedback remain future work.

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

## Definition of current playable prototype

The current playable prototype is substantially complete when:

- One hand-authored arena loads through the small documented map contract.
- Both combatants use the reusable `Player` entity.
- Human keyboard input and computer behavior drive the two players.
- Tanks collide with map terrain and boundaries.
- Both players can fire ammunition with a visible reload delay.
- Projectiles interact with passable, blocking, and reflective terrain.
- HUD shows health, ammunition/reload state, and fuel.
- Fuel and authored static pickups work.
- The solution builds without warnings.

The repository currently satisfies these functional goals as a prototype. The remaining work is hardening, testing, and match completion.

## Definition of the first complete match milestone

The first complete match milestone is complete when:

- One valid hand-authored arena loads through the documented map contract.
- Player starts are safe and repeatable; authored starts may be added later if useful.
- Both combatants use the same `Player` simulation.
- Existing human and computer control paths are covered by focused tests before optional extraction.
- Tanks collide correctly with map terrain and boundaries, including representative edge cases.
- Projectiles interact correctly with water, ravines, walls, hills, and reflective surfaces.
- Hits update a score and reset the round.
- HUD shows score, ammunition, and reload state.
- Automated tests cover the core non-rendering rules.
- The solution builds without warnings.

Fuel, fog of war, aircraft, supply drops, destructible terrain, and advanced AI are explicitly outside the first complete match milestone.

## Resume point

The playable prototype works: two `Player` entities (human and computer) drive around the hand-authored arena, spend fuel, collect pickups, and exchange shells. The solution targets .NET 10. The hardening pass (issue #16) is complete: `WorldMap` is separated from rendering (`MapRenderer`), the movement, fuel, firing, shell, damage and pickup rules live in testable `Core` classes, randomness comes from seeded `RandomStreams` (`--seed`), and `MonoTanx.Tests` covers them without a graphics device. The computer opponent, camera, shake and HUD are still in `GameStage`.

Since then: a home screen with `--test`, `--two-player`, `--windowed`, `--scale` and `--mute` launch options; a human-or-computer Player 2 (`F2`) and an overview camera (`F3`); frame-rate independence tests; every tuning value in `Core/Tuning.cs` (see `docs/tuning.md`); and audio hooks for all the planned sounds (#33). The sounds in `MonoTanx/Content/Audio/` are generated placeholders; replace them by dropping in WAVs of the same name (see the README in that folder).

The next step is score and round reset: hits update a score, the round resets, and the HUD shows score, ammunition and reload state (see "Definition of the first complete match milestone"). Add focused tests for the new rules as they are written. Extract the computer opponent from `GameStage` only when a task needs it.

## Decision log

Record meaningful decisions here as implementation proceeds.

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-08-29 | Use hand-authored Tiled maps. | Enables deliberate, playable arenas without premature procedural-generation work. |
| 2026-08-29 | Keep world rendering inside a fixed logical render target. | The render target naturally acts as the camera viewport and separates logical resolution from window size. |
| 2026-08-29 | Separate controllers from tank simulation. | Supports human, local multiplayer, and evolving AI without duplicating tank behavior. |
| 2026-08-29 | Separate movement, projectile, and visibility terrain properties. | Ravines, water, bridges, walls, and hills require independent rule combinations. |
| 2026-08-29 | Read tile `TerrainKind` metadata directly from TSX XML for now. | Released TiledCS does not expose per-tile properties; this preserves stable package usage while keeping Tiled as the authoring source. |

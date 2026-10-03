# Hardening pass: testable core rules — spec

Tracks GitHub issue #16.

## Problem

The working rules (map queries, collision, fuel, firing, projectiles, pickups) cannot be unit-tested today. `WorldMap` needs a `ContentManager` and loads and draws the tileset texture; shell, fuel, firing, damage and pickup logic are private methods in `GameStage`; firing reads `tank.Texture.Width`; hit effects use an unseeded `Random`.

## Goals

- `WorldMap` can be built in a test from a map file path, with no graphics device or content pipeline.
- Rule logic currently buried in `GameStage` lives in `Core` as small focused classes that take `WorldMap`, `Player` and elapsed time, with no input, rendering or `Game` dependencies.
- Gameplay randomness is injectable so tests can seed it.
- Focused xUnit tests cover the rules listed under "Test coverage".
- Gameplay is unchanged.

## Design decisions

- **`WorldMap` is data and queries only.** It loads the TMX/TSX from a path (as it does today, relative to the content directory) and exposes bounds, tile helpers, terrain queries, collision, line of sight, and `PickupSpawns`. Tileset texture loading and `Draw` move to a new `MapRenderer` in `Core`, constructed by `GameStage` from a `WorldMap` and a `Texture2D`.
- **Small rule classes, extracted one at a time.** Candidates: tank movement and fuel (including terrain multipliers and sliding), firing and reload, shell stepping and reflection, damage, and pickup collection. `GameStage` keeps orchestration, input mapping, the computer opponent, camera, shake and HUD. Extraction is limited to what the tests need; no behavior is rewritten for tidiness.
- **Firing does not depend on the texture.** The stage works out the muzzle offset (as it does now, from the tank sprite size) and passes it to the firing rule, preserving the current on-screen position. The rule itself knows nothing about textures.
- **Seedable randomness, injected rather than global.** Gameplay randomness comes from a `System.Random` handed to the rules that need it (initially the on-hit heading disruption); there is no static or singleton random. A run has one master seed. The game derives two separate streams from it: a gameplay stream, and a cosmetic stream for screen shake, so cosmetic draws can never shift gameplay outcomes. A `--seed <integer>` command-line option sets the master seed, and the seed is otherwise chosen at random and shown in the debug overlay so a run can be reproduced. Tests pass their own seeded `Random` directly. Streams for future systems (such as the computer opponent) are derived from the same master seed when they appear. `OldGameStage` keeps its fixed seed.
- **Test map data.** Tests use small purpose-built TMX/TSX fixtures in `MonoTanx.Tests` covering each terrain kind, multipliers, defaults and the out-of-bounds edge, plus a test that loads the checked-in `arena_01.tmx` and validates its contract.

## Out of scope

- Score, round reset and game-over.
- Extracting or changing the computer opponent.
- Changing gameplay values, feel, or the map contract (including renaming the arena's tile layer).
- Full replay determinism: `--seed` fixes random draws, but real keyboard input and frame timing still vary between runs.
- Package or framework upgrades.

## Test coverage

- Terrain: each `TerrainKind`, out-of-bounds, missing data defaulting to ground, property defaults and parsing.
- Collision: `CanOccupyCircle` against blocking terrain, edges and corners.
- Line of sight against blocking and reflective terrain.
- `Player`: reset of resources, ammunition slot order, remaining and starting totals.
- Fuel: costs for driving, reversing and turning, terrain multipliers, never below zero, no move or turn when short of fuel, firing still allowed.
- Firing: refused during reload or with no ammunition, reload set from the ammunition type.
- Shells: expiry, blocking terrain, hits on either tank including the shooter, reflection and the reflection cap.
- Damage: health clamps at zero, knockback only into a free position, seeded heading disruption.
- Randomness: the same seed reproduces the same gameplay draws, and cosmetic draws do not change them.
- Pickups: collected once, fuel clamped to maximum, ammunition added to the matching or first slot.

## Acceptance

- `dotnet build MonoTanx.slnx` has no new warnings; `dotnet test MonoTanx.slnx` passes without a graphical session.
- The game launches and plays as before, checked with a manual smoke test (driving, firing, reflection, pickups, computer opponent, `F5` overlay).
- `onboarding.md`, `AGENTS.md` boundaries and the roadmap resume point are updated to match.

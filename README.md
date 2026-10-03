# MonoTanx

MonoTanx is a top-down, tile-map-based tank combat game built with MonoGame DesktopGL and .NET 10, inspired by the Tanks mode in Atari 2600 Combat. Two tanks navigate a hand-authored arena, use terrain tactically, and try to destroy one another. Player 1 is human; Player 2 is currently computer controlled.

The project is at a playable prototype stage: the core loop works, but there is no score, round reset, or game-over screen yet. The next phase is hardening and tests, then a complete match loop (see [`docs/roadmap.md`](docs/roadmap.md)).

## The playable loop

Drive around the arena, manage your fuel and shells, collect fuel and ammunition pickups, and shoot the opposing tank. A tank reaching 0 health ends the game.

## Current systems

### Movement, fuel and terrain

- **Tank driving with sliding collision.** Tanks move continuously and collide with map terrain, the map boundary, and each other, sliding along obstacles rather than stopping dead. Reversing is half as fast as driving forward.
- **Fuel as a resource.** Turning and driving use fuel; reversing costs double. At zero fuel a tank cannot move or turn, but can still fire.
- **Terrain with different rules.** Ground and bridges are drivable. Water, ravines, walls, hills and reflective surfaces block tanks. Terrain tiles can also speed up, slow down, or change the fuel cost of travel.
- **A following camera.** The camera follows Player 1 and clamps at the map edges, with a HUD strip across the top of the screen.

### Combat

- **Shells with limited ammunition and a reload delay.** Each tank starts with 20 standard shells and has a three-second reload between shots.
- **Terrain-aware projectiles.** Shells fly over water, ground and ravines, and are stopped by walls and hills. Reflective surfaces bounce them, so a shell can come back at the tank that fired it.
- **Hit feedback.** A hit reduces health and gives the struck tank a small knockback, a heading disruption, and a brief screen shake.
- **Pickups.** Fuel and ammunition pickups are placed on the map in Tiled and collected by driving over them.

### Computer opponent

Player 2 pursues, evades and routes around terrain, seeks pickups when low on fuel or ammunition, retaliates when hit, and only fires with a clear line of sight and some aiming error.

## Build and run

From the repository root:

```sh
dotnet build MonoTanx.slnx
dotnet test MonoTanx.slnx
dotnet run --project MonoTanx/MonoTanx.csproj
```

The project targets `net10.0` and restores MonoGame and its content-pipeline tooling through NuGet. The game renders to an 800×600 logical surface and starts fullscreen, scaled proportionally with letterboxing where needed.

## Controls

| Key | Action |
|---|---|
| `W` / `S` | Drive forward / reverse |
| `A` / `D` | Turn left / right |
| `Space` | Fire |
| `F1` | Refill Player 1 fuel and ammunition (development aid) |
| `F5` (hold) | Pause and show the debug overlay |
| `Esc` | Exit |

Player 2 is computer controlled. If switched to human control in code, it uses the cursor keys to drive and turn and `Enter` to fire.

## Developer diagnostics

Holding `F5` pauses the simulation and shows both players' positions, tile coordinates and headings, Player 2's AI mode, route progress and timers, and the number of loaded pickups.

## Project layout

- `MonoTanx/` is the MonoGame DesktopGL application.
  - `Core/` holds shared engine and gameplay code (`WorldMap`, `Player`, sprites, animation).
  - `Controls/` holds UI primitives.
  - `Stages/` holds screens and game states; `GameStage` is the playable arena.
  - `Content/` is the MonoGame content-pipeline input, including the Tiled maps.
- `MonoTanx.Tests/` contains xUnit tests.
- `ArtSource/` is for editable source art.
- `docs/` holds the roadmap, specs and plans, and deferred snags.
- `onboarding.md` is the engineering reference. `AGENTS.md` is guidance for coding agents.

## Current boundary and next phase

Not yet implemented: scoring and round reset, a game-over screen, a menu or match-setup choice for a second human player, pickup respawn and dynamic drops, manual ammunition selection, destructible terrain, and fog of war. The immediate next step is a hardening pass with focused tests, followed by the first complete match loop; see [`docs/roadmap.md`](docs/roadmap.md).

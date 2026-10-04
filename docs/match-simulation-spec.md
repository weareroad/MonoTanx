# Headless match simulation — spec

Tracks GitHub issue #70. Builds on the command seam and `ComputerController` from #51. Follows the design tenet that either seat can be human or computer (`AGENTS.md`, #63).

## Problem

`GameStage` is both the presentation and the simulation. The update order, shell flight, damage, pickup collection, firing and reload ticking all run inside a class that needs a graphics device, so nothing in `Core` runs the rules together and no match can run without a window. #45 (rounds and score), #64 (demo and soak test), #66 (run log), #52 and #62 all want a match that runs headlessly, and the #51 refactor could only be proved by building two versions and diffing logs.

## Goals

- A `MatchSimulation` in `Core` that owns the two tanks, the shells, the pickups and one `ComputerController` per seat, advances them in a fixed order, and reports what happened as events.
- `GameStage` becomes orchestration: gather human input as `TankCommand`s, advance the simulation, turn events into sound and shake, draw.
- A computer versus computer match runs in an ordinary test, reproducible from its seed.
- **No change in how the game plays or sounds**, proved once more by comparing builds.

## Design decisions

- **Name and shape.** `MatchSimulation` (in `Core`, no graphics, audio or `Game` dependencies, no I/O). Constructed from a `WorldMap`, the two `Player`s, the pickup spawns to use, the gameplay `Random` (from `RandomStreams`), a `MatchSetup` and a `SimulationSettings` of plain presentation-derived data (the muzzle offset for each tank, which depends on the sprite width). The stage keeps owning how the tanks look (`Texture`, `Tint`); the simulation only reads their rules state.
- **One call per update:** `Step(elapsed, humanCommands)` where the human commands are `TankCommand?` per seat (null for a computer seat). It returns nothing; it appends to a public `Events` list that the stage reads and clears each update (so the test harness can read whole-match event logs too).
- **Events, in the order they happen**, each with the `Seat` it concerns where there is one: `ShellFired`, `ReloadReady`, `ShellReflected`, `ShellHitTerrain`, `TankHit`, `PickupCollected`, `TankDestroyed`. The stage maps them to the cues it plays today (including the Player 2 pitch shift, taken from the seat) and to screen shake (hit shake; fire shake only for the followed tank). Audio and the camera stay out of `Core`.
- **The update order is kept exactly as today:** for each seat in order, a computer seat ticks its reload and applies `PlanMove`, a human seat applies its command, ticks its reload, then fires if asked; then each computer seat's `PlanAim` pass; then (engine motion is judged here, see below) shells step, then pickups are collected for seat one then seat two.
- **Things the stage needs from inside the update**, exposed per seat rather than as callbacks:
  - `Moved(seat)`: whether the tank drove this update (the human drive animation uses it; the computer has none, as today).
  - `Motion(seat)`: the `TankMotion` classified after the movement and firing phase and before shells can knock a tank about, which is what the engine sound uses today.
- **Hit reaction** (health loss, knockback, heading disruption, the controller's `Hit()`) moves in. The seeded disruption uses the gameplay stream passed in, so a run is reproducible.
- **Tank destroyed** raises `TankDestroyed`; the simulation does not decide what happens next. Today the stage calls `game.Exit()`; #45 will replace that with rounds. The simulation keeps stepping rather than stopping, so callers choose.
- **Control changes** (`F2`/`F4`): `SetControl(seat, control)` flips the tank's `IsComputerControlled` and resets that seat's controller. `ResetFuelAndAmmunition(seat)` backs `F1`.
- **Pickups.** The simulation holds `PickupState`s built from the spawns it is given (the stage passes only those whose art loaded, as now) and the stage looks its textures up by spawn id for drawing and its animation. Collection and the controllers' view of "available" are the simulation's.
- **Starting positions** (`FindStartingPosition`, the initial headings) move in as a static helper so the headless harness starts matches the same way the game does.
- **Stays in the stage:** drawing, the camera, the HUD, screen shake, engine sound mixing, tank and pickup animation frames, `F5` pause and overlay, the human key-to-command mapping, F-key handling.

## Testing

- **Determinism:** a full computer versus computer match on the real arena for a fixed number of simulated seconds with a fixed seed gives the same event log and final state twice; a different seed differs.
- **Soak assertions over long runs and several seeds:** no tank on blocked terrain or overlapping the other, no shell outliving its flight time, fuel and health within range, ammunition never negative.
- **Event tests** on small fixtures: a shot that reflects and then hits a tank, one that hits a wall, a pickup collected, a reload finishing; events in the expected order.
- **Seat symmetry:** each of the four control combinations runs; swapping which seat is human or computer gives mirrored behaviour where the geometry is mirrored.
- **Equivalence:** the build comparison (a worktree of `main` against the branch, same seed, diffed logs) for each delivery PR, with a one-player run that exercises firing, hits, pickups and the F-key paths; and a final one-off check once the harness exists.

## Out of scope

- Rounds, score, reset and the end screen (#45, #46): the simulation reports `TankDestroyed` and keeps going.
- The run log itself (#66) and the demo mode (#64): they will read the events but are separate.
- Stuck detection and aim error (#52), settings multipliers (#62).
- Any change to gameplay, tuning or sound.

## Acceptance

- `GameStage` contains no simulation logic, only input, presentation and mapping events to sound and effects.
- A computer versus computer match runs in a test with no graphics device and is reproducible from its seed.
- The build comparison shows identical behaviour; the game sounds the same.
- `dotnet build` has no new warnings and `dotnet test` passes; played in one-player, two-player and demo modes.

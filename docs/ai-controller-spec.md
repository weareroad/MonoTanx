# Computer opponent as a controller — spec

Tracks GitHub issues #51 (extract the computer opponent) and the shared command seam from #53 (human input adapter). Follows the design tenet that either seat can be human or computer (`AGENTS.md`, #63).

## Problem

The computer's logic (about 300 lines: pursuit, evasion, pickup seeking, route planning, retaliation, aiming and firing) lives inside `GameStage` as methods over a private `ComputerState`. It is seat-agnostic since #63, but it cannot be unit tested, `GameStage` is large, and it repeats the fuel and turn maths by hand in several places instead of using `TankMovement`. The settings page (#62) needs speed, fuel and reload multipliers applied in one place for humans and the computer alike, and #52 needs to test stuck detection and aim error.

## Goals

- The computer's decisions live in `Core` as a controller that needs no graphics device, one instance per computer-controlled seat.
- The computer and the humans produce the same thing, a `TankCommand` (turn, drive, fire), and the stage applies commands through the existing `TankMovement` and `Player.TryFire` rules.
- **No change in how the computer plays**, proved by comparing builds, not just by new tests.

## Design decisions

- **`TankCommand`** (a readonly struct in `Core`): `Turn` and `Drive` in -1..1, and `Fire`. `TankMovement.ApplyInput` gains an overload that takes a command. The human path in `GameStage.UpdateTank` builds a command from the keys and uses it; nothing else about it changes. The keyboard and gamepad adapters in #53 will produce the same command later.
- **`ComputerController`** (in `Core`): created with its own tank, its opponent and the `WorldMap`; it owns the state that is now in `ComputerState` (route, route index, pickup target, long-range pursuit, fire and retaliation timers, last opponent position). No `playerOne` or `playerTwo` anywhere: it speaks of "self" and "the opponent".
  - `PlanMove(elapsed, pickups)` returns the movement command for this update.
  - `PlanAim(elapsed)` returns the aiming and firing command (a turn, and `Fire` when it wants to shoot).
  - `ShotFired()` is called by the stage after a successful shot, to start the fire cooldown; `Hit()` is called when its tank is hit (starts retaliation); `Reset()` is called when control changes.
  - Read-only properties expose what the debug overlay shows (mode, route progress, timers).
- **Two phases per update, to preserve behaviour exactly.** Today the computer decides movement in one place and aiming in another, later in the same update, after both seats have moved, and each can turn the tank. Collapsing them into one command per update would change how it plays, so the stage applies two commands, in the same order as now: the movement command while the seat is updated, then the aim command in the firing pass. (A later change can unify them deliberately.)
- **The fuel rule is kept exactly.** The computer currently does nothing at all when it cannot afford both the turn and the drive, whereas `ApplyInput` would still turn. To keep behaviour identical the controller checks the cost itself, using a new public `TankMovement.FuelCost(map, tank, command, elapsed)` that `ApplyInput` also uses, and returns an empty command when it cannot pay.
- **Reload ticking stays in the stage**, at the same points as today (the start of the computer's movement phase; after movement for a human).
- **Pickups.** The controller needs to know which pickups are available. A small `PickupState` (the `PickupSpawn` and whether it is `Active`) moves into `Core`; the stage's private `Pickup` extends it with its texture and animation. The controller takes an `IReadOnlyList<PickupState>`.
- **Route planning** (`FindRoute`, the combat ring and the escape goals) becomes a pure `RoutePlanner` in `Core`, tested on its own.
- **Debug overlay and `DamageTank`** use the controller's properties and `Hit()` instead of `ComputerState`.

## Testing

- **Characterisation by comparing builds.** The computer is deterministic when the opponent stands still. Build `main` before the change and the branch after, run both with the same seed and a stationary, indestructible opponent, log both tanks' position, heading, fuel, ammunition and health at fixed frames, and require identical logs. This is how the #63 refactor was checked (15-second and 80-second runs, all samples identical). The 80-second run reaches the pickup-seeking branch; add an ammunition-exhausted and a retaliation scenario so every branch is covered.
- **Behaviour tests on fixture maps**, one per rule: retaliation (turns to face the opponent and does not drive); pickup seeking at the fuel and ammunition thresholds; evading when out of ammunition; long-range pursuit; holding position when in range with line of sight; route following and the drive-angle limit; waypoint reached; firing gates (fire cooldown, reload, aim tolerance versus turning, line of sight); the all-or-nothing fuel rule.
- **Mirror test (the tenet).** The same geometry with the roles swapped (each tank as "self" in turn) produces the same command for the tank in the same situation.
- `RoutePlanner` tests: shortest path, blocked goal, no path, edges.
- Existing tests keep passing; `GameStage` has no remaining AI logic.

## Out of scope

- Any change to how the computer plays (stuck detection and aim error are #52).
- Human adapters and gamepad (#53, #54): this only introduces the shared `TankCommand`.
- Settings multipliers (#62) and the headless match simulation (see the plan's follow-up).

## Acceptance

- `GameStage` contains no AI decision logic, only orchestration, and the controller has unit tests that need no graphics device.
- The build comparison shows identical behaviour across the scenarios above.
- `dotnet build` has no new warnings and `dotnet test` passes; played in one-player, two-player and demo modes.

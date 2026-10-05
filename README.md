# MonoTanx

MonoTanx is a top-down, tile-map-based tank combat game built with MonoGame DesktopGL and .NET 10, inspired by the Tanks mode in Atari 2600 Combat. Two tanks navigate a hand-authored arena, use terrain tactically, and try to destroy one another. Player 1 is human; Player 2 is currently computer controlled.

The project is at a playable prototype stage: the core loop works, with rounds, a score and an end-of-match screen. There is a settings page for tuning (below). The next steps are polish (a demo mode that starts itself, more of the run log; see [`docs/roadmap.md`](docs/roadmap.md)).

## The playable loop

Drive around the arena, manage your fuel and shells, collect fuel and ammunition pickups, and shoot the opposing tank. A tank reaching 0 health loses the round: the other seat scores, the arena resets after a short pause and a 3-2-1 count, and the first to win 3 rounds wins the match, after which an end screen shows the winner and offers Play again, Home screen and Quit. A round that runs 90 seconds without a winner is a draw and is replayed. The score and round number are shown at the top of the playfield.

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
- **Sound.** Firing, reloading, shells bouncing and hitting walls or tanks, and collecting pickups all have sounds (Player 2's shots and reloads at a different pitch); each tank also has a quiet engine drone at a different pitch for forward, reverse and turning. The current sounds are generated placeholders; see `MonoTanx/Content/Audio/README.md` to replace them. `--mute` silences everything, and the game runs normally with no audio device.
- **Pickups.** Fuel and ammunition pickups are placed on the map in Tiled and collected by driving over them.

### Computer opponent

Player 2 pursues, dodges shells it sees coming (including rebounds), routes around terrain, seeks pickups when low on fuel or ammunition, retaliates when hit, backs away when it gets stuck, closes in to about six tiles before it shoots, moves to a new firing position after each shot, and only fires with a clear line of sight and some aiming error.

### Settings

The home screen has a **Settings** page for changing about 60 gameplay values without rebuilding: match rules (rounds, countdown, time limit), tanks (health, fuel, speeds, fuel use), shells (reload, damage, speed), the computer (skill, distances, aim, evasion, stuck recovery) and per-seat speed, fuel and reload multipliers. Up/Down select, Left/Right (or left-click and right-click) change a value, Shift changes it ten steps at a time, Delete resets one, Esc leaves and saves; changes apply when a game starts. Values are saved as JSON in your application data folder (`%AppData%\MonoTanx\settings.json` on Windows, `~/.config/MonoTanx/settings.json` on Linux and macOS), holding only what differs from the defaults, so it can be edited by hand. `--settings <path>` and `--set <key>=<value>` (see the launch options) keep named sets and one-off overrides; the keys, ranges and defaults are in [`docs/tuning.md`](docs/tuning.md).

## Build and run

From the repository root:

```sh
dotnet build MonoTanx.slnx
dotnet test MonoTanx.slnx
dotnet run --project MonoTanx/MonoTanx.csproj            # opens the home screen
dotnet run --project MonoTanx/MonoTanx.csproj -- --test  # straight into a game
```

### Launch options

Run `MonoTanx --help` (or `-h`, `-?`) to list these in the terminal, with how they combine.

| Option | Effect |
|---|---|
| `--test` | Skip the home screen and start a game straight away (for development and quick testing) |
| `--two-player` | Two human players: Player 2 under human control and the whole arena in view. Preselects two players on the home screen |
| `--demo` | Two computers play each other (no humans), with the whole arena in view. Cannot be combined with `--two-player`. Matches restart by themselves a few seconds after one is won |
| `--mute` | Start with all sound off |
| `--seed <integer>` | Fix the run's random draws; the seed is shown in the debug overlay |
| `--windowed` | Run in a window instead of fullscreen |
| `--scale <1-4>` | With `--windowed`, set the window to that integer multiple of 800×600 (default 2) |
| `--log` | Echo the run log to the console as the game is played (it is always written to a file; the path and the seed are printed at the start). It records shots, hits, pickups, rounds and what the computer decides: dodges, shells it did not notice, getting stuck |
| `--settings <path>` | Read (and save) the settings from this file instead of the per-user one (`%AppData%\MonoTanx\settings.json` on Windows, `~/.config/MonoTanx/settings.json` on Linux and macOS), so different sets of tuning values can be kept side by side |
| `--set <key>=<value>` | Override one setting for this run on top of the settings file, without saving it (for example `--set ai.skill=0.9`); can be repeated. Keys are the names used in the settings file |
| `--help`, `-h`, `-?`, `/?` | Print the options and exit. Always wins over everything else on the line |

Options can be given in any order. Conflicting options (`--two-player` with `--demo`, `--scale` without `--windowed`) are rejected with a message rather than one quietly overriding the other; repeating `--seed`, `--scale` or `--settings` is allowed and the last value wins, and `--set` can be repeated.

Options go after `--` when using `dotnet run`, and can be combined:

```sh
dotnet run --project MonoTanx/MonoTanx.csproj -- --test --windowed --scale 2 --seed 123
```

The project targets `net10.0` and restores MonoGame and its content-pipeline tooling through NuGet. The game renders to an 800×600 logical surface and starts fullscreen by default, scaled proportionally with letterboxing where needed.

## Home screen

Unless `--test` is given, the game opens at a home screen with **Start game**, a **Players: 1 / Players: 2** setting and **Quit**. Use the mouse, or Up/Down to move, Enter or Space to choose, Left/Right to change the players setting, and Esc to quit.

## Controls

| Key | Action |
|---|---|
| `W` / `S` | Drive forward / reverse |
| `A` / `D` | Turn left / right |
| `Space` | Fire |
| `F1` | Refill Player 1 fuel and ammunition (development aid) |
| `F2` | Switch Player 2 between computer and human control (development aid) |
| `F4` | Switch Player 1 between human and computer control (development aid) |
| `F3` | Switch between the following camera and an overview showing the whole arena (development aid) |
| `F5` (hold) | Pause and show the debug overlay |
| `Esc` | In a game: back to the home screen (quit, when started with `--test`). On the home screen: quit |

Either player can be a human or the computer. A normal game has Player 1 human and Player 2 the computer. Press `F2` to switch Player 2, or `F4` to switch Player 1, between computer and human at any time (each player's HUD panel shows `CPU` or `HUMAN`). A human Player 1 drives with `W` `A` `S` `D` and fires with `Space`; a human Player 2 uses the cursor keys and `Enter`. Switching both to the computer gives a computer-versus-computer game.

## Developer diagnostics

Holding `F5` pauses the simulation and shows both players' positions, tile coordinates and headings, Player 2's AI mode, route progress and timers, the number of loaded pickups, and the run's random seed.

## Project layout

- `MonoTanx/` is the MonoGame DesktopGL application.
  - `Core/` holds shared engine and gameplay code (`WorldMap`, `Player`, `TankMovement`, `Shell`, `Tuning`).
  - `Controls/` holds UI primitives.
  - `Stages/` holds screens and game states; `GameStage` is the playable arena.
  - `Content/` is the MonoGame content-pipeline input, including the Tiled maps.
- `MonoTanx.Tests/` contains xUnit tests.
- `ArtSource/` is for editable source art.
- `docs/` holds the roadmap, specs and plans, and deferred snags.
- `onboarding.md` is the engineering reference. `AGENTS.md` is guidance for coding agents.

## Current boundary and next phase

Not yet implemented: scoring and round reset, a game-over screen, a menu or match-setup choice for a second human player, pickup respawn and dynamic drops, manual ammunition selection, destructible terrain, and fog of war. The immediate next step is a hardening pass with focused tests, followed by the first complete match loop; see [`docs/roadmap.md`](docs/roadmap.md).

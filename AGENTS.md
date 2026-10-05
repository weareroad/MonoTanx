# AGENTS.md

## Start here

1. Read `README.md` for the current player-facing feature set and how to run the game.
2. Read `onboarding.md` for the engineering architecture, workflow, and known debt.
3. Read `docs/roadmap.md`. Its `Resume point` section records the current state and next intended work.
4. Read only the other relevant files under `docs/` for the task. Specs and plans are mostly design history, not a live backlog.
5. Check `docs/deferred-snags.md` for deliberately deferred observations.

When documentation conflicts with the implementation, verify the current behavior in code and tests, then update the authoritative summaries (`README.md` and `onboarding.md`) as part of the change.

## Project overview

MonoTanx is a small C#/.NET 10 desktop game built with MonoGame. The solution contains one executable project and one xUnit test project.

- Solution: `MonoTanx.slnx`
- Application project: `MonoTanx/MonoTanx.csproj`
- Entry point: `MonoTanx/Program.cs`
- Game host and stage switching: `MonoTanx/Tanx.cs`
- Shared engine code: `MonoTanx/Core/`
- UI controls: `MonoTanx/Controls/`
- Screens/game states: `MonoTanx/Stages/`
- MonoGame content pipeline: `MonoTanx/Content/Content.mgcb`
- Design docs: `docs/` holds specs and plans as `<feature>-spec.md` and `<feature>-plan.md` pairs, plus `docs/deferred-snags.md` for deliberately deferred observations
- Editable source art: `ArtSource/` (e.g. Aseprite files); only exported runtime assets belong in `MonoTanx/Content/`
- Unit tests: `MonoTanx.Tests/` (xUnit; parallelization is disabled in `TestAssembly.cs`)

## Working guidelines

- Make the smallest coherent change that satisfies the request.
- Preserve the existing namespace layout and brace-on-new-line C# style in files you touch.
- Follow nearby naming and formatting conventions; do not reformat unrelated legacy code.
- Keep reusable game behavior in `Core`, UI primitives in `Controls`, and screen-specific behavior in `Stages`.
- Treat asset names and paths as case-sensitive because builds may run on non-Windows systems.
- Keep editable source art in `ArtSource/` and runtime assets under `MonoTanx/Content/`; reference runtime assets with paths relative to `Content`. (Existing editable art still sitting in `Content/` will be moved over time.)
- When adding or removing a runtime asset, update `Content.mgcb` and use the content pipeline name with `Content.Load<T>()`.
- Do not edit generated `bin/`, `obj/`, or content build output.
- Do not upgrade .NET, MonoGame, or other packages unless the task explicitly calls for it.
- Preserve deterministic seeds in gameplay/demo code unless changed behavior is part of the request.
- A new command-line option goes in the `Specs` table in `GameOptions` (it drives both parsing and `--help`), in `EveryOption` in `GameOptionsTests` (a test fails if the help and the parser disagree), and in the README's launch options table.
- Put gameplay and feel values in `MonoTanx/Core/Tuning.cs` with a comment saying what each controls and why it has that value; do not add new magic numbers to rules or the stage. `docs/tuning.md` indexes them and records the derived pacing numbers (pinned by `TuningTests`) and what is deliberately kept elsewhere.

## Architectural boundaries

- `Program.cs` creates `Tanx` and runs it. `Tanx.cs` owns the MonoGame loop, the 800x600 render target and its scaling, and stage switching. Do not put gameplay rules in it.
- Stages own screen behavior. `GameStage` currently also hosts input handling, the computer opponent, projectiles, pickups, collision calls, the camera, and the HUD. Do not grow it further: put new rules and calculations in `Core` in a form that can be tested without a graphics device, and extract existing logic from `GameStage` only when a task needs it.
- `Player` holds tank state, tuning defaults, and ammunition, and owns the firing and reload rules (`TryFire`, `TickReload`). `TankMovement` owns steering, driving, fuel use, sliding collision, and tank-vs-tank overlap. `Shell` owns projectile flight, reflection, and hit detection; `TankDamage` owns health loss, knockback, and heading disruption; `PickupRules` owns collection. None of these depend on input, rendering, or `Game`. Keyboard state and rendering stay outside it. It carries presentation data (`Texture`, `Tint`) for the stage to draw, but its rules must not depend on them.
- `WorldMap` owns map loading and the terrain, collision, line-of-sight, and pickup queries. Callers ask it about capabilities (`BlocksMovement`, `HasLineOfSight`, ...) rather than reading tile IDs.
- `WorldMap` is built from a map file path and has no graphics dependencies, so tests can load it directly (see the fixtures in `MonoTanx.Tests/Fixtures/`). Tileset textures and drawing belong to `MapRenderer`; keep rendering concerns out of `WorldMap`.
- The logical canvas stays 800x600. Window scaling must not change world coordinates or gameplay.
- Debug overlays (`F5`) must not change simulation behavior.

## Terminology

Use the same words wherever a player can see them (game screens, the HUD, `--help` and messages, the run log, the settings page) and in `README.md`, `onboarding.md`, this file and any new docs, plans and specs (#100). Older specs and plans may keep their old wording.

| Say | Not |
|---|---|
| P1, P2 (a human player in that seat) | Player 1, Player 2, Human |
| CPU (a computer seat on its own); C1 and C2 when both seats are computers | Computer, Computer 1, Computer 2 |
| Armour (a tank's protection; what a shell takes off is "armour lost") | Health, HP, Damage, Shield |
| Fuel | Power |
| Shells | Ammo, Ammunition, Shots, Bullets |

Code identifiers (`Player`, `Health`, `Ammunition`, `ComputerController`, setting keys in the config file) keep their names: this is about what people read, not renaming code. `MatchSetup.LabelOf` gives a seat's label from who controls it.

## Design tenets

- **Either player (seat) can be controlled by a human or by the computer.** Player 1 and Player 2 are *seats*; human and computer are *controllers*; any combination is valid (human vs computer, human vs human, computer vs computer). In new or changed code, do not assume Player 1 is human or that Player 2 is the computer: write rules in terms of "self" and "the opponent" passed as parameters, take labels, settings and camera focus from the seat and its controller, and test with the seats swapped. This is a firm decision (see the roadmap decision log and issues #63 and #64). `GameStage` is seat-agnostic (each seat has its own computer state and control, see `MatchSetup`); the computer's logic is still inside `GameStage` and moves into `Core` as a controller in #51.

## Task workflow

Use this lifecycle for each task:

1. Agree what is being changed, usually starting from a GitHub issue.
2. For work that is likely to be complex, agree a brief spec and implementation plan in `docs/` before coding. Small, well-bounded maintenance changes do not need spec/plan files.
3. Create a focused branch for the task before development; do not commit directly to `main`.
4. Develop the change, add or update proportionate tests, run the full suite, and perform any relevant runtime smoke tests.
5. Review the finished diff with the user. Once both are happy, open a pull request with a concise summary and validation evidence.
6. The user reviews, accepts, and merges the pull request. Agents must not merge it unless explicitly asked.
7. After the merge is confirmed, tidy the local repository: return to `main`, fast-forward it, and remove the merged local feature branch. That completes the task.

## Behavioral invariants

Preserve these unless the task explicitly changes the design and updates its tests and documentation (more detail in `onboarding.md`):

- Gameplay updates scale by elapsed time and run on a fixed 60 FPS step; movement, turning, fuel use, and reload timing are frame-rate independent.
- Terrain behavior comes from tileset properties, not hard-coded tile IDs; out-of-bounds blocks movement, projectiles, and vision.
- Tanks cannot enter blocking terrain, leave the map, or overlap each other. Movement is resolved per axis so tanks slide along obstacles.
- Fuel never goes below zero. A tank without enough fuel cannot turn or drive but can still fire.
- Firing needs ammunition and a finished reload; reload time comes from the ammunition type.
- Shells can hit either tank, including the one that fired them, and are removed on expiry, blocking terrain, a hit, or too many reflections.
- Pickups are collected once; fuel is clamped to the maximum.
- Holding `F5` pauses the simulation and must not otherwise change it.
- Randomness comes from `RandomStreams`: one master seed per run (`--seed <integer>`, otherwise random and shown in the `F5` overlay) deriving separate gameplay and cosmetic streams. Pass the stream you need (a plain `Random`) to the code that uses it; do not create an unseeded `Random` or a static/global one, and keep cosmetic effects such as screen shake on the cosmetic stream.

## Change discipline

- Inspect the worktree before editing and preserve unrelated user changes.
- Prefer small, cohesive changes that follow the spec/plan, focused-test, full-suite, and smoke-test workflow.
- Do not commit generated `bin/` or `obj/` output, user settings, or save data.
- Preserve deterministic seeded behavior. Tests for seeded systems should use explicit seeds and include the seed in failure messages.
- Update `README.md`, `onboarding.md`, and relevant `docs/` when user-visible behavior, architecture, commands, or dependencies change.

## Build and validation

Run commands from the repository root.

```powershell
dotnet restore MonoTanx.slnx
dotnet build MonoTanx.slnx --no-restore
dotnet test MonoTanx.slnx --no-restore
```

Add or update focused xUnit tests for new or changed behavior, keeping testable logic free of graphics-device dependencies, and run the full suite before raising a PR. For gameplay, rendering, input, stage transitions, or content changes, also run the game when a graphical session is available:

```powershell
dotnet run --project MonoTanx/MonoTanx.csproj -- --test
```

Do not claim manual gameplay validation unless the game was actually launched and the affected path was exercised. If graphical validation is unavailable, report that limitation.

## Windows tooling notes

- `rg` is installed and is the preferred file/text search tool.
- PowerShell is the intended shell. If the PowerShell host cannot be started and `cmd` is used as a fallback, remember that characters such as `|`, `&`, `<`, and `>` are shell operators even when they are meant for a regular expression.
- Under `cmd`, prefer multiple simple `rg -e pattern` searches over a single alternation-heavy expression. Avoid patterns containing spaces or `|` unless they are escaped correctly for `cmd`.
- Use `type` under `cmd`; `Get-Content` is a PowerShell command and will not work there.
- Keep repository inspection commands simple and separate when shell quoting would obscure which command failed.

## Completion checklist

- The solution builds without new warnings or errors.
- New assets are registered in `Content.mgcb` and load through the expected content name.
- Changes stay within the appropriate project area and avoid unrelated cleanup.
- The final handoff states what was validated and calls out any untested graphical behavior.

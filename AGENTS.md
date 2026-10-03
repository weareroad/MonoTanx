# AGENTS.md

## Start here

1. Read `README.md` for the current player-facing feature set and how to run the game.
2. Read `onboarding.md` for the engineering architecture, workflow, and known debt.
3. Read only the relevant files under `docs/` for the task. Specs and plans are mostly design history, not a live backlog.
4. Check `docs/deferred-snags.md` for deliberately deferred observations.

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

## Task workflow

Use this lifecycle for each task:

1. Agree what is being changed, usually starting from a GitHub issue.
2. For work that is likely to be complex, agree a brief spec and implementation plan in `docs/` before coding. Small, well-bounded maintenance changes do not need spec/plan files.
3. Create a focused branch for the task before development; do not commit directly to `main`.
4. Develop the change, add or update proportionate tests, run the full suite, and perform any relevant runtime smoke tests.
5. Review the finished diff with the user. Once both are happy, open a pull request with a concise summary and validation evidence.
6. The user reviews, accepts, and merges the pull request. Agents must not merge it unless explicitly asked.
7. After the merge is confirmed, tidy the local repository: return to `main`, fast-forward it, and remove the merged local feature branch. That completes the task.

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
dotnet run --project MonoTanx/MonoTanx.csproj
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

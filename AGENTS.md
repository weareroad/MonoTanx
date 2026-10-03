# AGENTS.md

## Project overview

MonoTanx is a small C#/.NET 10 desktop game built with MonoGame. The solution contains one executable project and one xUnit test project.

- Solution: `MonoTanx.sln`
- Application project: `MonoTanx/MonoTanx.csproj`
- Entry point: `MonoTanx/Program.cs`
- Game host and stage switching: `MonoTanx/Tanx.cs`
- Shared engine code: `MonoTanx/Core/`
- UI controls: `MonoTanx/Controls/`
- Screens/game states: `MonoTanx/Stages/`
- MonoGame content pipeline: `MonoTanx/Content/Content.mgcb`
- Unit tests: `MonoTanx.Tests/` (xUnit; parallelization is disabled in `TestAssembly.cs`)

## Working guidelines

- Make the smallest coherent change that satisfies the request.
- Preserve the existing namespace layout and brace-on-new-line C# style in files you touch.
- Follow nearby naming and formatting conventions; do not reformat unrelated legacy code.
- Keep reusable game behavior in `Core`, UI primitives in `Controls`, and screen-specific behavior in `Stages`.
- Treat asset names and paths as case-sensitive because builds may run on non-Windows systems.
- Keep editable source assets and runtime source assets under `MonoTanx/Content/` (for example `Content/Aseprite/`); reference them with paths relative to `Content`.
- When adding or removing a runtime asset, update `Content.mgcb` and use the content pipeline name with `Content.Load<T>()`.
- Do not edit generated `bin/`, `obj/`, or content build output.
- Do not upgrade .NET, MonoGame, or other packages unless the task explicitly calls for it.
- Preserve deterministic seeds in gameplay/demo code unless changed behavior is part of the request.

## Task workflow

1. Decide what we're doing, usually from a GitHub issue.
2. Write a spec and a plan if the task is likely to be complex.
3. Create a branch for the work; do not commit directly to `main`.
4. Develop and test the code (automated tests will be added later).
5. Once the change is agreed, raise a PR.
6. The user reviews, accepts and merges the PR; do not merge it yourself.
7. After the merge, tidy up locally (switch to `main`, pull, delete the merged branch). The task is then complete.

## Build and validation

Run commands from the repository root.

```powershell
dotnet restore MonoTanx.sln
dotnet build MonoTanx.sln --no-restore
dotnet test MonoTanx.sln --no-restore
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

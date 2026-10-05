# Settings page and config file — spec

Tracks GitHub issue #62. Started now because Rob is about to run a long parameter-tuning session: the values in `Core/Tuning.cs` are `const`s, so every experiment is an edit and a rebuild. The goal is to change them from a screen (or a file) and see the effect at once. Follows the design tenet that either seat can be human or computer (`AGENTS.md`, #63).

Rob's decisions (2026-10-05): settings persist in a **JSON config file**, introduced now; **surface as many settings as possible** for now (some will be hidden again once the game is tuned); the page must make **tuning quick**.

## What it does

- A **Settings** button on the home screen opens a settings page. Values are grouped into pages (Match, Tanks, Shells, Computer, and the per-seat multipliers), each a row with a label, the current value and its default. Up/Down moves, Left/Right changes a value by its step (Shift for ten steps), Delete puts one back to its default, a **Reset to defaults** button resets everything, Esc goes back and saves. Mouse and keyboard, as on the home screen.
- Settings **apply when a match starts** (and each new match in a demo). Live change while playing is not needed: leaving the page and pressing Start is two keypresses.
- Settings are **saved as JSON** and loaded at launch. A value that has never been changed is not special: the file can hold any subset, and anything missing is the default from `Tuning`.
- The **run log records the settings that differ from the defaults** in its header, so a tuning run can be repeated and compared.

## The config file

- **Location:** per-user, in the platform's application data folder (`~/.config/MonoTanx/settings.json` on Linux, `%AppData%\MonoTanx\settings.json` on Windows), from `Environment.SpecialFolder.ApplicationData`. Per-user rather than beside the binaries because `bin/` is rebuilt and cleaned during development, which would silently lose tuning work. `--settings <path>` uses another file (several tuning sets side by side, and the headless harness).
- **Format:** one flat JSON object keyed by dotted names, written with only the values that differ from the default, in a stable order, indented so it can be edited by hand:

  ```json
  {
    "version": 1,
    "ai.skill": 0.8,
    "ai.engageDistanceTiles": 5,
    "match.roundTimeLimitSeconds": 120
  }
  ```

  Flat keys (not nested groups) so a key can be found with grep and renamed without restructuring the file. Written with `System.Text.Json`, which ships with .NET, so no new package.
- **Forgiving on read:** a missing file is all defaults; a value out of range is clamped; an unknown key, a value of the wrong type or malformed JSON is skipped. Each problem is reported (console and the run log), the game never fails to start over a config file, and a damaged file is not overwritten until the page next saves.
- **Versioned** with a `version` number so a later change of meaning can migrate rather than guess.

## The settings model (`Core`, no graphics)

- **`SettingDefinition`**: key, group, label, kind (number, whole number, on/off), minimum, maximum, step, and the default, which is read from `Tuning` so `Tuning` stays the single source of defaults and the reasons for them. One table, `SettingsCatalogue`, lists every surfaced setting; the page, the file reader/writer, the help and the tests all read it, so they cannot drift apart (the same idea as the `Specs` table in `GameOptions`).
- **`GameSettings`**: the current values, by definition. `Get`/`Set` with clamping, `IsDefault`, `ResetAll`, `Differences` (for the log), and typed read-only views the rules take: `TankSettings`, `ShellSettings`, `ComputerSettings`, `MatchSettings`. Immutable once a match starts (a match is given a copy), so a match cannot change under the rules.
- **`SettingsFile`**: load/save of `GameSettings` from a path, with the forgiving rules above; takes a path and text, so tests need no real folder.
- **Rules read the view, not `Tuning`.** Sites that today read `Tuning.X` at play time (about 35, in `Player`, `TankMovement`, `TankDamage`, `Shell`, `ComputerController`, `RoutePlanner`, `MatchState`, `MatchSession`) take the value from the settings they are given. With every setting at its default the numbers are identical, so nothing changes until a setting does (checked by build comparison, below). Values that are not gameplay stay in `Tuning` and are not surfaced: audio mix, presentation, shake, vision sampling, the fixed update rate.

## Which values are surfaced

Everything gameplay-related that is safe to change, grouped (about 60 of the 93 constants; the full list is the catalogue, and `docs/tuning.md` points at it):

| Page | Values |
|---|---|
| **Match** | rounds to win, countdown, round-over pause, round time limit, match-over pause |
| **Tanks** (all tanks) | health, fuel, starting shells, forward/reverse/turn speed, forward and turn fuel rates, reverse fuel multiplier, collision radius (limited, see below), pickup collect radius and default amounts |
| **Shells** | reload time, flight time, damage, speed, knockback, heading disruption, maximum reflections |
| **Computer** | skill, engage distance, fire distance, aim tolerance and maximum aim error, reaction delay, fire cooldown, relocation (choices, minimum, closer), evasion (notice chance, reaction, detection distance, lookahead, hold), retaliation time, needs-fuel and needs-ammo fractions, stuck window/distance/recovery |
| **Per seat** | speed, fuel-use and reload multipliers for Player 1 (human), Player 2 (human) and Computer, as in #62; the computer group covers whichever seat the computer controls (so both in a demo) |

Seat-specific values follow the **controller** of a seat, not the seat: the group is chosen from who controls the seat now, including when the control is toggled in play (`F2`), so nothing assumes Player 1 is human.

## Limits

- Every value has a **range** that keeps the game playable and the rules safe: for example speed is capped until tunnelling protection exists (#49), the collision radius cannot exceed what fits a one-tile corridor with the shell radius, durations are not negative, fractions stay 0 to 1, and `FireDistanceTiles` is not below `EngageDistanceTiles` (a pair rule, applied when the value is set: the other is moved with it). The ranges are in the catalogue next to each default, with the reason.
- The **derived pacing numbers** in `docs/tuning.md` hold only at the defaults. `TuningTests` keeps testing the defaults (from `Tuning`, not from a user's file), and the page shows a value's default beside it so a change from it is visible.
- **Randomness is untouched**: settings change values, not streams, so a run is still reproducible from its seed plus its settings (both are in the log header).

## Page design

The home screen has no number control, so this adds one reusable control to `Controls`: a **`ValueRow`** (label on the left, value and its default on the right, highlighted when selected), drawn like `Button` and navigated with `MenuSelection`. A page lists up to nine rows at a time and scrolls; the groups are tabs along the top (Left/Right on the tab row, or `PageUp`/`PageDown`). The 800x600 logical canvas and the pixel font are unchanged.

`SettingsStage` owns the page and nothing else: it edits a `GameSettings`, saves it on leaving, and hands it to `HomeStage`, which passes it to `GameStage` along with the `MatchSetup`.

## Not in this change

Presets ("easy computer"); live change while playing; per-seat computer groups (Computer 1 and 2 differing in a demo, #64); remapping keys; audio and display settings (the volume and window options stay command-line); hiding settings again (a one-line `Hidden` flag on the definition when the time comes); a settings screen for the headless harness (it takes a `GameSettings` or a `--settings` path directly).

## Decisions (Rob, 2026-10-05)

1. **Config location:** per-user application data, from `Environment.SpecialFolder.ApplicationData`. .NET resolves that to `%AppData%` on Windows and to `$XDG_CONFIG_HOME` (default `~/.config`) on Linux and macOS, so on macOS the file is at `~/.config/MonoTanx/settings.json`, not under `~/Library`. The folder is created on first save.
2. **Auto-save on leaving the page.**
3. **Five tabs** (Match, Tanks, Shells, Computer, Per seat).
4. **`--set <key>=<value>`** (repeatable) overrides one value on top of the file, for scripted comparisons. It is validated and clamped like the file, the overridden values are not written back to the file, and they appear in the log header with the other differences. It goes in the `Specs` table with `--settings`.

## Testing

Catalogue consistency (every key unique, default within range, default equal to the `Tuning` constant, step positive); `GameSettings` get/set/clamp/reset/differences and the pair rule; `SettingsFile` round trips, missing file, malformed JSON, unknown key, wrong type, out of range, version; the rules reading settings (a changed value changes the rule: damage, reload, speed, fuel, engage distance, round time limit) with the seats swapped; **defaults identical** (a seeded headless match with default `GameSettings` gives the same events as before the change); the control (`ValueRow`) value stepping and the stage's navigation as far as they can be tested without a graphics device; the log header lists only differences.

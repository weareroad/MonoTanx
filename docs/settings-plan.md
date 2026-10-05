# Settings page and config file — plan

Four PRs against #62, after the spec is agreed. The first two change no behaviour, so they are verified by comparing a build of `main` with the branch (same seed, same logs; see the build-comparison approach used for the match simulation refactor), and mutation checks that a changed setting really changes the rule.

1. **The settings model and file (no UI, no rule changes).**
   - `SettingDefinition`, `SettingsCatalogue`, `GameSettings`, `SettingsFile` in `Core`, with defaults read from `Tuning`; `--settings <path>` and `--set <key>=<value>` in the `Specs` table of `GameOptions`, `EveryOption` in `GameOptionsTests` and the README's launch options table.
   - Loaded at launch in `Tanx`, held on it; nothing reads it yet.
   - Tests: catalogue consistency (default equals the `Tuning` constant), clamping and the pair rule, file round trips and every forgiving case.
2. **The rules read the settings.**
   - Slice by area, each with its tests: tanks and shells (`Player`, `TankMovement`, `TankDamage`, `Shell`, `Ammunition`), match (`MatchState`, `MatchSession`), computer (`ComputerController`, `RoutePlanner`), per-seat multipliers by controller (including the `F2` toggle), pickups.
   - `GameStage` builds the seats from the settings it is given; `MatchHarness` takes a `GameSettings` (default: the defaults), never reading the user's file by itself.
   - Run log header lists the differences from the defaults.
   - Check: with default settings a seeded run is event-for-event identical to `main`; with each group changed the matching rule changes (and only it); seats swapped.
3. **The settings page.**
   - `ValueRow` control, `SettingsStage`, a Settings button on `HomeStage`, saving on leaving, Reset to defaults, tabs, scrolling, mouse and keyboard.
   - Hand-test on the real game (graphical session): every page, a change made and seen in play, saved and reloaded after a restart, a bad file at launch.
4. **Documentation and tuning handover.**
   - `docs/tuning.md` (the catalogue is where to tune now, and which values are not surfaced and why), `onboarding.md`, `README.md` (the page, the file, the options), roadmap resume point and decision log.
   - A short "how to run a tuning session" note: use `--settings` for named sets, `MatchHarness.Measure` for the numbers, and the log header for what was changed.

## Notes and risks

- **Many call sites.** About 35 reads of `Tuning` change; the build comparison is what makes this safe, so slice 2 goes in small, area-by-area commits.
- **Ranges are guesses until played.** Wide enough to experiment, narrow enough that nothing breaks the rules (tunnelling, the corridor fit). Widen them in the catalogue as the tuning session shows what is useful.
- **Hidden-again later.** Surfacing everything now is deliberate; the catalogue can hide values with a flag, so the file keeps working.
- **Two sources of truth if careless.** `Tuning` holds the defaults and their reasons; the catalogue only references them, and a test fails if a catalogue default drifts from its constant.

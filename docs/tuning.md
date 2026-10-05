# Tuning

Every gameplay and feel value lives in [`MonoTanx/Core/Tuning.cs`](../MonoTanx/Core/Tuning.cs), grouped by area. Each value has a comment saying what it controls, its unit, and why it has its current value. That file is the single source for the values and the reasons; this document is the index, the derived pacing numbers, and the list of things deliberately kept elsewhere.

## How to change a value

1. Edit the constant in `Tuning.cs` (and its comment, if the reason changes).
2. Run `dotnet test MonoTanx.slnx`. `TuningTests` pins the pacing figures below; if one fails, update this document and the test together.
3. Play it. These numbers are about feel, and the tests only check the arithmetic.

Per-tank values are defaults: `Player` copies them into instance properties, so individual players can still differ. The default shell's values are built into `Player.DefaultAmmunition`; the shell speed lives on `Ammunition`, so a new ammunition type can have its own.

## What controls what

| Group | Controls | Used by |
|---|---|---|
| `Timing` | Fixed update rate (60 per second) | `Tanx` |
| `Tank` | Health, fuel, speeds, turn rate, collision radius, fuel rates, starting shells | `Player`, `TankMovement`, `GameStage` |
| `StandardShell` | Reload time, flight time, damage and speed of the default shell | `Player.DefaultAmmunition` |
| `Projectile` | How shells fly: hit radius, reflection limit, cooldown and nudge, sub-step length | `Shell` |
| `Damage` | Knockback distance and random heading disruption when hit | `TankDamage` |
| `Pickups` | Collection radius and default amounts | `PickupRules`, `WorldMap` |
| `Ai` | Computer opponent behaviour: combat distance, aim, cadence, retaliation, pickup thresholds, route tolerances | `Player` defaults, `GameStage` |
| `Vision` | Line-of-sight sample spacing | `WorldMap` |
| `Audio` | Master and per-cue volumes, Player 2's pitch offset, engine volumes and pitches, fade and glide times, motion thresholds | `SoundMix`, `EngineMix`, `TankMotionClassifier` |
| `Match` | Rounds to win, countdown and round-over pauses, round time limit, demo restart pause | `MatchState`, `MatchSession` |
| `Shake` | Screen shake size, length and jitter rate | `GameStage`, `ScreenShake` |
| `Presentation` | Sprite-sheet frame counts and timing, placeholder shell size, muzzle clearance, overview smoothing, HUD height | `GameStage` |

## Pacing numbers (current values)

These follow from the values above and are checked by `TuningTests`.

- Crossing the 960px arena at 90 px/s takes about 10.7s. A full 200 fuel is about 50s of driving (about 4.7 arena widths); a 50-fuel pickup is about 12s.
- 12 damage against 100 health is 9 hits to kill. With 20 shells and a 3s reload, killing takes at least 24s of firing, about 45% of the ammunition at perfect accuracy.
- Shell speed 260 px/s for 5s is a 1300px range, longer than the 1154px arena diagonal, and about 2.9 times tank speed, so a moving target has to be led.
- Tank collision radius 6px on 16px tiles leaves 4px of play in a one-tile corridor (an 8px radius would fit with none). A shell hits within 9px of a tank centre.
- The computer fires every 3.25s (3.0s cooldown plus 0.25s reaction), slightly slower than the 3.0s reload.
- Turning costs 0.25 fuel/s against 4/s for driving, so turning is almost free (a full turn costs about 0.6 fuel against about 10 for driving the same 2.5s). Reversing is half speed at double fuel rate, 4 times the fuel per pixel.

- A round is a draw after 90s (`Match.RoundTimeLimitSeconds`): more than a full tank of driving (50s) plus the fastest possible kill (24s), pinned by `TuningTests`. A match is first to 3 rounds, so at most 5 decided rounds; drawn rounds are replayed.

## Known tuning notes

- **Most computer versus computer rounds draw on the time limit.** Over the seeds in the headless tests (computer in both seats) about three rounds in four are drawn: the computers hit often (60 to 130 hits a match) but rarely land the nine hits a kill needs before time runs out, so a whole match takes 10 to 36 minutes of simulated play. A human against the computer will differ; revisit the time limit, damage, or the computer's accuracy (#52) if demo matches feel endless.

- **Hit shake is probably too weak.** Camera offsets are rounded to whole pixels, so the 1.0px hit shake rounds to no offset about half the time. 2 to 3px would show. The 1.8px fire shake is more visible. Tune by eye.
- **Turning is nearly free.** Intentional or not, it makes fuel almost irrelevant for steering. Raise `Tank.TurnFuelPerSecond` if turning should matter.
- **Reversing is expensive** by design (4 times the fuel per pixel).
- **Computer aim:** the 0.2 rad tolerance gives a lateral miss of up to about 19px at 6 tiles but about 60px at 300px, so it hits close up and often misses at range.

## Deliberately kept elsewhere

| Value | Where | Why |
|---|---|---|
| HUD pixel layout (positions, bar sizes, overlay panel) | `GameStage` | Layout, not gameplay; it will change with real HUD art |
| Logical surface 800x600 | `Tanx.DesignedWidth/Height` | Used widely as a static; changing it is a design decision, not tuning |
| Tile size, map size | The Tiled map | Authored data |
| Terrain speed and fuel multipliers | Tileset properties in the TSX | Authored per tile in Tiled |
| `--scale` range and default | `GameOptions` | Launch option limits, not gameplay |
| Asset names and sprite paths | `GameStage`, map properties | Content, not tuning |
| Camera follow rule (centred on Player 1, clamped to the map) | `GameStage.UpdateCamera` | Behaviour rather than a number |

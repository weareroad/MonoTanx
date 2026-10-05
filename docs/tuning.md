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
| `Ai` | Computer opponent behaviour: combat distance, aim window and its random error, skill, cadence, retaliation, pickup thresholds, route tolerances | `Player` defaults, `GameStage` |
| `Vision` | Line-of-sight sample spacing | `WorldMap` |
| `Audio` | Master and per-cue volumes, Player 2's pitch offset, engine volumes and pitches, fade and glide times, motion thresholds | `SoundMix`, `EngineMix`, `TankMotionClassifier` |
| `Match` | Rounds to win, countdown and round-over pauses, round time limit, match-over pause | `MatchState`, `MatchSession` |
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
- **Computer aim:** it only fires within 8 tiles (128px). Each shot has its own firing window (its largest error), drawn from the seat's random stream between 0.07 rad and 0.07 plus 0.5 times (1 minus skill); at the default skill the windows average 0.195 rad, matching the fixed 0.2 rad they replaced. A window of 0.2 rad gives a lateral miss of up to about 19px at 6 tiles but about 60px at 300px, so it hits close up and often misses at range. The window cannot go below about 0.07 rad: movement and aiming each turn the tank up to 0.04 rad a step, and a tighter window is missed over and over (with 0.03 the computer fired a quarter as often).

## Computer balance numbers

How two computers play each other, measured with the headless harness (`MatchHarness.Measure`, in `ComputerBalanceTests`): computer in both seats on the real arena, matches restarting as in a demo. `ComputerBalanceTests` pins them loosely, so a change to the computer has to update this table on purpose. Eight seeds (1, 7, 42, 1234, 4242, 99999, 2026, 31337) of 1200 simulated seconds each, 9600s in all (the tests use fewer seeds and shorter runs, so their numbers differ slightly):

| Step | Hits a minute | Shots fired | Accuracy | Rounds decided / drawn | Stuck (seconds in each minute) |
|---|---|---|---|---|---|
| Before #52 (fixed 0.2 rad aim tolerance) | 6.1 | 2915 | 34% | 22 / 87 (20% decided) | 32.5 |
| Aim error (#52, previous step) | 5.7 | 2692 | 34% | 17 / 89 (16% decided) | 31.6 |

| Stuck recovery (#52) | 1.7 | 2746 | 10% | 5 / 92 (5% decided) | 1.8 (longest run 1.7s) |
| Closing in (#90, this step) | 9.2 | 2981 | 49% | 54 / 68 (44% decided) | 2.8 (longest run 1.7s) |

"Stuck" counts a tank that was commanded to drive for 1.5s and moved less than 6px, in either seat. Before stuck recovery that was about 32 seconds in every minute, so between them the computers spent a large share of every round pushing against something. In one 1200s run of seed 7 about 70% of those updates were in long-range pursuit (it drives a fixed heading at the opponent and never routes around anything) and about 30% in pickup seeking, where the tank drove forward all the time it turned, and a turning circle of about 36px (90px/s at 2.5 rad/s) does not fit a 16px corridor, so it pressed into corners. Recovery fixes both: it detects the lack of progress, backs away turning, and then follows a planned route for a while instead of a straight line; and pickup seeking now drives only when roughly facing the next waypoint, as combat already did (that alone took stuck pickup updates from about 18,600 to 2 in three 1200s runs).

### What the numbers mean (after stuck recovery)

Stuck recovery lowered the computers' accuracy and leaves almost every round drawn. That is a consequence of the computers now moving, not a regression in aiming, and worth understanding before the next step:

- Before, a computer commonly sat pressed against a wall at 700px or more from the opponent, its movement phase steering it onto the exact line to the opponent every update. Its shots then came out almost perfectly aimed, whatever the firing window, which is why a computer against a human standing still hit 82% of its shots (883 hits from 1081 shots). That accuracy was an accident of being stuck, not skill.
- Now it roams, so the aim window is what decides a shot. A window averages 0.195 rad, which at 300 to 500px (the computer holds position and fires from anywhere inside half the map width with a clear view) misses by 60 to 100px against a 15px target. The same computer against a still human hits 41% (437 hits from 1077 shots), and two computers hit 10%.
- Kills need nine hits and a tank has 20 shells, so a tank must hit 45% of its shots to kill a still opponent before its shells run out. At 10% a computer-versus-computer round is essentially always a draw (5% of rounds were decided), and a match to first-to-3 does not finish in hours of simulated play.

Against a human who moves, a computer has always been far less accurate than that 82% suggests. The way to make computer rounds end is not to put the stuck behaviour back but for the computer to fire from where its error can hit (close the distance first, or hold fire at range) and to spend its ammunition accordingly; see the plan's notes. Nothing about damage, ammunition or the round time limit was changed there. The next step (closing in, below) is what fixed it.

### Closing in (#90)

The computer now routes in to a combat ring `EngageDistanceTiles` (6, about 96px) from the opponent when it has a view but is further than that (it used to hold anywhere within half the map width), holds once it is inside the ring plus half a tile (120px), and fires only within `FireDistanceTiles` (8, 128px) with a clear view. Between holding and firing it only turns to aim once it has stopped; while closing in it leaves the steering to the movement phase. The measures, over the same eight seeds of 1200s, with the motionless measure added (the share of a computer tank's time in play that it stood quite still looking at the opponent with nothing to wait for: not just hit, not backing away, next shot more than a second off):

| | Hits/min | Shots | Accuracy | Rounds decided / drawn | Motionless in view |
|---|---|---|---|---|---|
| Computers, stuck recovery | 1.7 | 2746 | 10% | 5 / 92 (5%) | 24% |
| Computers, closing in | 9.2 | 2981 | 49% | 54 / 68 (44%) | 20% |
| Computer vs a still human, stuck recovery | 2.7 | 1077 | 41% | 3 / 95 (3%) | 14% |
| Computer vs a still human, closing in | 3.2 | 561 | 91% | 56 / 53 (51%) | 12% |

- **Why 6 and 8 tiles.** The distances were found by trying them (before the aim-turn fix below, so the figures are only relative). Engaging at 5 tiles and firing from 7 gave 66% accuracy and 74% of rounds decided (30 matches in 9600s), too lethal for a computer that holds still and shoots; 7 and 9 gave 42% and 44% (8 matches), 8 and 10 gave 41% and 39%. 6 and 8 keeps the ring the computer already used, and gives about 50% accuracy, about half of rounds decided, and matches that finish.
- **A deadlock found on the way.** The first version froze two computers 122px apart for the whole round: the movement phase turned each toward its route while the aim phase turned it back toward the opponent, every update, so neither drove. Fixed by letting the aim phase turn only once it has stopped to shoot (and not at all without a view), as the controller's tests now check.
- **Against a human who stands still it hits 91%** of its shots (it closes to within a few tiles), so the computer is lethal to a motionless target. Against a human who moves it will hit far less; hand-testing is the judge of whether it is too hard.
- **Still draws against a motionless human about half the time**, mostly because it goes for a pickup first when its fuel is below half (a long trip: it arrives with fuel at 80 of 200 after 60 seconds), not because it cannot hit.
- **Motionless in view is still 20% and 12%**: while engaged it holds still between shots. Relocating during the cooldown (next step) addresses that.

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

# Tuning

Every gameplay and feel value lives in [`MonoTanx/Core/Tuning.cs`](../MonoTanx/Core/Tuning.cs), grouped by area. Each value has a comment saying what it controls, its unit, and why it has its current value. That file is the single source for the values and the reasons; this document is the index, the derived pacing numbers, and the list of things deliberately kept elsewhere.

## How to change a value

1. Edit the constant in `Tuning.cs` (and its comment, if the reason changes).
2. Run `dotnet test MonoTanx.slnx`. `TuningTests` pins the pacing figures below; if one fails, update this document and the test together.
3. Play it. These numbers are about feel, and the tests only check the arithmetic.

## Changing values without rebuilding (settings)

About 60 of these values can be changed while the game runs, from the **Settings** page on the home screen or in a JSON file (design: [`settings-spec.md`](settings-spec.md)). `Tuning.cs` is still where the defaults, their units and the reasons for them live; the settings only override them, and they apply when a match starts. The catalogue of what can be changed (key, label, default, range and step) is `SettingsCatalogue`, and the reference below is generated from it; a test fails if this document misses a key.

- **The page.** Tabs for Match, Tanks, Shells, CPU and Per seat. Left/Right (or left-click / right-click) change a value a step, Shift makes it ten steps, Delete puts one back to its default, Reset all resets everything, Esc leaves and saves.
- **The file.** `settings.json` in the per-user application data folder (`%AppData%\MonoTanx` on Windows, `~/.config/MonoTanx` on Linux and macOS), holding only the values that differ from the defaults, keyed by the names below. It can be edited by hand. A bad value is clamped to its range, an unknown key or a non-number is skipped, and the game says so on the console rather than failing.
- **Named sets.** `--settings <path>` reads and saves a different file, so a tuning experiment can be kept beside the usual settings. `--set <key>=<value>` (repeatable) overrides single values for one run without saving them; those rows show `*` on the page.
- **Reproducing a run.** The run log header has a `# settings` line listing every value that differs from the defaults, next to the seed, so a run is reproducible from the seed plus those values.
- **Headless.** `MatchHarness` takes a `GameSettings` (default: the defaults) and never reads the user's file, so tests and `MatchHarness.Measure` are not affected by what is saved. Build the settings in code, or `SettingsFile.Load(path)` a named set, to measure how a change plays.
- **Multipliers per seat.** The Per seat tab has speed, fuel use and reload multipliers for P1 (human), P2 (human) and the CPU. Which group a tank uses follows **who controls it now**, so a CPU in either seat uses the CPU group, and toggling control in play switches group.
- **Pacing numbers hold at the defaults only.** `TuningTests` checks the defaults from `Tuning`, never a user's file. The page shows each default beside the value.

A short tuning session: find the value in the reference below; try it on the page; check the effect by playing and, for the computer, with `MatchHarness.Measure` on the same seeds before and after; once a value is settled, change its constant in `Tuning.cs` (with the reason in its comment) and update the pacing numbers if one moved. A settings file is a personal experiment; `Tuning.cs` is what everyone gets.

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
| `Ai` | Computer opponent behaviour: combat and firing distances, aim window and its random error, skill, relocation, evasion, stuck recovery, cadence, retaliation, pickup thresholds, route tolerances | `Player` defaults, `ComputerController` |
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
| Closing in (#90) | 9.2 | 2981 | 49% | 54 / 68 (44% decided) | 2.8 (longest run 1.7s) |
| Shoot and scoot (#90) | 11.8 | 3216 | 59% | 71 / 56 (56% decided) | 3.0 (longest run 1.7s) |
| Evasion (#85, this step) | 8.9 | 3017 | 47% | 40 / 71 (36% decided) | 2.6 (longest run 1.7s) |

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

### Shoot and scoot (#90)

After each shot, while its cooldown runs, the computer moves to a new firing position: one of the nearest three tiles on a ring one tile closer than the combat ring (so 4 to 5 tiles, 64 to 80px) from which it can see the opponent, at least two tiles from where it fired, chosen from the seat's own stream. It stops relocating when it arrives, when the gun is ready, or when it is hit (retaliation takes over), and it does not scoot once it is out of ammunition.

| | Hits/min | Accuracy | Rounds decided / drawn | Motionless in view |
|---|---|---|---|---|
| Computers, closing in | 9.2 | 49% | 54 / 68 (44%) | 20% |
| Computers, shoot and scoot | 11.8 | 59% | 71 / 56 (56%) | 2% |
| Computer vs a still human, closing in | 3.2 | 91% | 56 / 53 (51%) | 12% |
| Computer vs a still human, shoot and scoot | 1.4 | 40% | 0 / 96 (0%) | 2% |

- **Never frozen:** motionless in view fell to 2% for both. Matches between computers finish more often (13 in 9600s).
- **Why the closer ring.** The first version scooted to the combat ring (5 to 7 tiles) and accuracy fell to 20% at 5 tiles; measured against a motionless target, 57% of shots hit from 3 tiles, 48% from 4 and 20% from 5 to 6. Hence `RelocationCloserTiles`.
- **The still-human numbers fall, and that is real, not a bug.** Before scooting, a computer facing a motionless human kept the alignment of its first successful shot for every later one (it only turned if a new, narrower window demanded it), which is why it hit 91% of its shots. Scooting means it re-aims from a new position for every shot, so each shot's random error counts, and it hits 40%. A kill needs nine hits, and in a 90s round it has time for about 14 shots after the approach, so it cannot kill a motionless human in a round (0 of 96). Against computers, which also scoot and move, decided rounds went up, not down.
- **What to tune if that matters.** The skill default (0.5) sets how wide a shot's window is: at skill 0.7 the windows average 0.145 rad instead of 0.195 and accuracy at 4 to 5 tiles rises. Other dials: how close it fires from (`RelocationCloserTiles`, `EngageDistanceTiles`), damage or ammunition (nine hits from 20 shells), and the round time limit. None of these was changed here.
- **Steering is slow.** The tank steers by turning toward each route tile and drives only when roughly facing it, so it dithers and takes about 3.5s to cover three tiles; that is why the minimum relocation is two tiles, and why a relocation is sometimes cut off by the gun being ready. Better steering is a separate piece of work.

### Evasion (#85)

The computer now predicts every shell in flight (whoever fired it, its own rebounds included): it steps a copy with the real `Shell` rules for `EvadeLookaheadSeconds` (1.5s) with the tanks where they are, and if a shell it has noticed would hit it where it stands, it tries the eight ways of turning and driving (held for 0.5s, on a scratch copy of its tank with the real `TankMovement` rules) and takes one that gets clear, preferring the least change and the one it was already making. Dodging comes first, before retaliation, pickups and chasing, and the aim phase never turns the tank meanwhile. A shell it cannot get clear of is accepted and costs no fuel; with no fuel it cannot move.

It is deliberately imperfect, so a human can still hit it: only shells within `EvadeDetectionDistance` (240px) are considered; a shell must have been flying `EvadeReactionSeconds` (0.12s) before it is noticed; and each shell is noticed with a chance drawn once from the seat's stream, 0.5 at skill 0 rising to 1 at skill 1 (0.75 at the default).

| 8 seeds x 1200s | Hits/min | Accuracy | Rounds decided / drawn | Motionless in view |
|---|---|---|---|---|
| Computers, shoot and scoot | 11.8 | 59% | 71 / 56 (56%) | 2% |
| Computers, evasion | 8.9 | 47% | 40 / 71 (36%) | 2% |
| Computer vs a still human, shoot and scoot | 1.4 | 40% | 0 / 96 (0%) | 2% |
| Computer vs a still human, evasion | 1.3 | 37% | 0 / 96 (0%) | 2% |

- **Hits fall by about a quarter** (11.8 to 8.9 a minute) and the share of decided rounds from 56% to 36%, as expected: dodging is the point. The computers spend only 0.1% of their time dodging (about 2.3 dodges a minute between the two), so it is a rare, quick sidestep and not a constant wiggle.
- **A motionless human does not shoot**, so the computer only ever dodges its own shells coming back off a wall, and those numbers hardly move; the fall in kills against a still human is the shoot-and-scoot finding above.
- **What the run log shows** (`--log`, below; eight seeds of computer against computer, 1200s each, 1144 hits in all): 301 dodges, of which 213 worked and 88 failed (a hit within 1.6s); 307 shells were unavoidable (no move gets clear in time) and 201 were not noticed (the per-shell draw). The dodges start with the shell a median of 26px away and the unavoidable ones at 18px: the computers fight from 64 to 80px, a shell crosses that in about 0.2s, and the computer needs 0.12s to notice it and 0.17s to sidestep the 15px a shell needs to hit, so most close shots cannot be dodged and a dodge is usually a last-moment shuffle that works only when the shot was already marginal. Dodging mostly matters for rebounds and shots from further away (over about 110px). To dodge more, the dials are the reaction time, firing from further out (at a cost in its own accuracy), or noticing a shell when it is fired.
- **Cost.** Predicting costs about twice the simulation time per update (about 75 microseconds for both tanks against about 35), only 0.5% of a 16.7ms frame.
- **Dials for the tuning pass.** The notice chance (`EvadeNoticeChanceAtSkillZero`, and skill), the detection distance and the reaction time set how often it dodges successfully; the lookahead sets how far ahead rebounds are seen.

## Settings reference

Every setting in the catalogue, by page. Whole-number settings are rounded. Fire distance is never below engage distance: setting one moves the other with it. Ranges keep the game playable and the rules safe: speeds are capped (240 px/s forward, a little under three times the default) until tunnelling protection exists (#49), the collision radius cannot exceed what fits a one-tile corridor, and the aim tolerance cannot go below 0.07 (the computer turns up to 0.04 rad a step, so a tighter window is missed over and over).

### Match

| Key | Setting | Default | Range | Step |
|---|---|---|---|---|
| `match.roundsToWin` | Rounds to win | 3 | 1 to 9 | 1 |
| `match.countdownSeconds` | Countdown (s) | 3.0 | 0.0 to 10.0 | 0.5 |
| `match.roundOverSeconds` | Round over pause (s) | 2.0 | 0.0 to 10.0 | 0.5 |
| `match.roundTimeLimitSeconds` | Round time limit (s) | 90 | 10 to 600 | 5 |
| `match.matchOverSeconds` | Match over pause (s) | 5 | 1 to 30 | 1 |

### Tanks

| Key | Setting | Default | Range | Step |
|---|---|---|---|---|
| `tank.maximumHealth` | Armour | 100 | 10 to 1000 | 10 |
| `tank.maximumFuel` | Fuel | 200 | 20 to 1000 | 10 |
| `tank.startingShells` | Starting shells | 20 | 1 to 200 | 1 |
| `tank.forwardSpeed` | Forward speed (px/s) | 90 | 20 to 240 | 5 |
| `tank.reverseSpeed` | Reverse speed (px/s) | 45 | 10 to 120 | 5 |
| `tank.turnSpeed` | Turn speed (rad/s) | 2.5 | 0.5 to 6.0 | 0.1 |
| `tank.collisionRadius` | Collision radius (px) | 6.0 | 3.0 to 7.0 | 0.5 |
| `tank.forwardFuelPerSecond` | Forward fuel use (/s) | 4.0 | 0.0 to 20.0 | 0.5 |
| `tank.reverseFuelMultiplier` | Reverse fuel multiplier | 2.0 | 0.0 to 8.0 | 0.5 |
| `tank.turnFuelPerSecond` | Turn fuel use (/s) | 0.25 | 0.00 to 5.00 | 0.05 |
| `tank.pickupCollectRadius` | Pickup radius (px) | 12 | 6 to 32 | 1 |
| `tank.defaultFuelAmount` | Fuel pickup amount | 50 | 5 to 200 | 5 |
| `tank.defaultAmmunitionAmount` | Shell pickup amount | 5 | 1 to 50 | 1 |

### Shells

| Key | Setting | Default | Range | Step |
|---|---|---|---|---|
| `shell.reloadSeconds` | Reload time (s) | 3.0 | 0.2 to 10.0 | 0.1 |
| `shell.maxFlightSeconds` | Flight time (s) | 5.0 | 1.0 to 10.0 | 0.5 |
| `shell.damage` | Armour lost per hit | 12 | 1 to 100 | 1 |
| `shell.speed` | Shell speed (px/s) | 260 | 100 to 500 | 10 |
| `shell.knockbackDistance` | Knockback (px) | 1.5 | 0.0 to 8.0 | 0.5 |
| `shell.headingDisruptionRadians` | Heading disruption (rad) | 0.16 | 0.00 to 0.60 | 0.02 |
| `shell.maxReflections` | Max reflections | 8 | 0 to 20 | 1 |

### CPU

| Key | Setting | Default | Range | Step |
|---|---|---|---|---|
| `ai.skill` | Skill | 0.50 | 0.00 to 1.00 | 0.05 |
| `ai.engageDistanceTiles` | Engage distance (tiles) | 6 | 2 to 12 | 1 |
| `ai.fireDistanceTiles` | Fire distance (tiles) | 8 | 2 to 16 | 1 |
| `ai.aimToleranceRadians` | Aim tolerance (rad) | 0.07 | 0.07 to 0.50 | 0.01 |
| `ai.maximumAimErrorRadians` | Extra aim error (rad) | 0.50 | 0.00 to 1.00 | 0.05 |
| `ai.reactionDelaySeconds` | Reaction delay (s) | 0.25 | 0.00 to 2.00 | 0.05 |
| `ai.fireCooldownSeconds` | Fire cooldown (s) | 3.00 | 0.50 to 10.00 | 0.25 |
| `ai.retaliationSeconds` | Retaliation (s) | 1.50 | 0.00 to 5.00 | 0.25 |
| `ai.longRangePursuitDistanceFraction` | Long range pursuit | 0.50 | 0.10 to 1.00 | 0.05 |
| `ai.needsFuelBelowFraction` | Seeks fuel below | 0.50 | 0.00 to 1.00 | 0.05 |
| `ai.needsAmmoBelowFraction` | Seeks shells below | 0.50 | 0.00 to 1.00 | 0.05 |
| `ai.relocationChoices` | Relocation choices | 3 | 1 to 8 | 1 |
| `ai.relocationMinimumTiles` | Relocation min (tiles) | 2 | 1 to 5 | 1 |
| `ai.relocationCloserTiles` | Relocation closer (tiles) | 1 | 0 to 3 | 1 |
| `ai.evadeNoticeChanceAtSkillZero` | Evade notice at skill 0 | 0.50 | 0.00 to 1.00 | 0.05 |
| `ai.evadeReactionSeconds` | Evade reaction (s) | 0.12 | 0.00 to 1.00 | 0.02 |
| `ai.evadeDetectionDistance` | Evade detection (px) | 240 | 0 to 480 | 10 |
| `ai.evadeLookaheadSeconds` | Evade lookahead (s) | 1.5 | 0.2 to 3.0 | 0.1 |
| `ai.evadeHoldSeconds` | Evade hold (s) | 0.50 | 0.10 to 1.50 | 0.05 |
| `ai.stuckWindowSeconds` | Stuck window (s) | 1.50 | 0.50 to 5.00 | 0.25 |
| `ai.stuckMinimumDistance` | Stuck distance (px) | 6 | 1 to 30 | 1 |
| `ai.stuckRecoverySeconds` | Stuck recovery (s) | 0.8 | 0.2 to 3.0 | 0.1 |

### Per seat

| Key | Setting | Default | Range | Step |
|---|---|---|---|---|
| `playerOne.speedMultiplier` | P1 speed x | 1.00 | 0.25 to 2.00 | 0.05 |
| `playerOne.fuelUseMultiplier` | P1 fuel use x | 1.00 | 0.00 to 4.00 | 0.05 |
| `playerOne.reloadMultiplier` | P1 reload x | 1.00 | 0.25 to 4.00 | 0.05 |
| `playerTwo.speedMultiplier` | P2 speed x | 1.00 | 0.25 to 2.00 | 0.05 |
| `playerTwo.fuelUseMultiplier` | P2 fuel use x | 1.00 | 0.00 to 4.00 | 0.05 |
| `playerTwo.reloadMultiplier` | P2 reload x | 1.00 | 0.25 to 4.00 | 0.05 |
| `computer.speedMultiplier` | CPU speed x | 1.00 | 0.25 to 2.00 | 0.05 |
| `computer.fuelUseMultiplier` | CPU fuel use x | 1.00 | 0.00 to 4.00 | 0.05 |
| `computer.reloadMultiplier` | CPU reload x | 1.00 | 0.25 to 4.00 | 0.05 |

Not surfaced (they stay in `Tuning.cs`): the "60s remaining" mark, how long it stays up and the length of the final countdown (`Match.TimeWarningSeconds`, `TimeWarningShownSeconds`, `FinalCountdownSeconds`), the fixed update rate, the projectile sub-step, reflection cooldown and nudge, the hit radius of a shell, the computer's route tolerances (waypoint distance, drive angle, route rebuild distance), combat ring size and tolerance, evade step, stuck repeat, route pursuit and pickup ignore times, line-of-sight spacing, screen shake, all audio, and all presentation values. These are either not gameplay, or so tightly tied to other values that changing them alone would break something; any can be added to the catalogue when tuning needs it.

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

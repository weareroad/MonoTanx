# Computer opponent engagement: close in, fire from where it can hit, keep moving — spec

Tracks GitHub issue #90. Builds on the aim error and stuck recovery of #52 and the headless balance harness (#70). Comes before evasion (#85), whose tuning should be judged against a computer that actually engages. Follows the design tenet that either seat can be human or computer (`AGENTS.md`, #63): everything is written in terms of "self" and "the opponent".

## Problem

Found by hand-testing stuck recovery: with a human standing still in range, the computer stood still too, for 10 to 15 seconds, and seemed to move only when the human did. It is not stuck. It is the computer's hold rule:

- Whenever it is within `LongRangePursuitDistanceFraction` of the map width (half, about 480px) of the opponent with a clear line of sight, `ComputerController` returns no movement and fires from where it stands. Nothing closes the distance once it has a view; only losing the view, the opponent moving far enough to change its route, or needing a pickup makes it move.
- It fires from anywhere in that range. A shot's firing window averages 0.195 rad, which at 300 to 500px misses a 15px target (the 9px shell plus the 6px tank) by 60 to 100px. Its own aim can only reliably hit inside about 77px (15px divided by 0.195 rad).
- The result, measured with the harness after stuck recovery (docs/tuning.md): two computers hit 10% of their shots, and almost every computer round draws on the time limit. Kills need nine hits and a tank has 20 shells, so it needs about 45% accuracy to kill a still target. Before stuck recovery this was hidden: computers were often wedged against a wall at 700px with their movement steering them exactly onto the opponent, so their shots came out near-perfect.

## Goals

- With a motionless opponent in range, the computer closes in and shoots rather than standing still.
- It fires from a distance where its aim error can hit, and does not spend shells beyond it.
- It does not look frozen while waiting for its cooldown or reload.
- Computer-versus-computer accuracy and the share of decided rounds rise to a recorded, sensible level, so matches finish.
- Same seed, same match; seat-agnostic; each change measured with the harness and the numbers recorded.

## Design decisions (recommendations; change any in review)

### The distances, derived rather than guessed

- **Effective range.** A shot with an error of θ radians misses by `distance × tan θ`, so it can hit while that is under the 15px hit width: at the average window of 0.195 rad that is about 77px; at the widest (0.32 rad) about 47px; at the narrowest (0.07 rad) about 214px. So the computer should fight at about five tiles (80px), not at 480px.
- `Tuning.Ai.EngageDistanceTiles` (**6**, about 96px; the draft said 5): where it stops closing in and shoots. It replaces `PreferredCombatDistanceTiles` as the combat ring, so the no-view route and the approach agree; it holds once inside the ring plus half a tile (120px). `Tuning.Ai.FireDistanceTiles` (**8**, 128px; the draft said 7): the farthest it will fire from; between the hold distance and this it may shoot if it is already lined up, but it will not stop and aim from further out. *Measured:* 5 and 7 gave 66% accuracy and three rounds in four decided, too lethal; 6 and 8 gives about 50% and about half decided (see docs/tuning.md).

### Approach (PR 1)

- **Closing in.** With the opponent in view and farther than the engage distance, the computer follows a route to a tile at the engage distance from the opponent (the existing combat route, which already goes round obstacles; a straight line would run into water or ravines, which block tanks but not sight). It no longer holds still at any distance short of the engage distance, which replaces the old "within half the map width with a clear view: hold" rule. Beyond half the map width it still chases in a straight line first (and by a route after getting stuck), as now.
- **Holding.** Within the engage distance with a clear view it stops and lines up its shot. The aim phase turns the tank only once it has stopped (and not at all without a view): while it is closing in, the movement phase is steering and the two would undo each other (found as a deadlock in the first version). Holding is only while it waits to fire, which the next step makes short and varied.
- **Fire discipline.** The aim phase fires only when the opponent is within `FireDistanceTiles` as well as lined up with a clear view. Out of range it does not spend a shell; it keeps closing. Its ammunition then goes at targets it can hit.

### Keeping moving (PR 2)

- **Shoot and scoot.** After each shot, while its cooldown runs, it relocates: it picks a new firing position, a tile on a ring one tile *closer* than the combat ring (4 to 5 tiles; measured: 57% of shots hit from 3 tiles, 48% from 4, 20% from 5 to 6) that has a clear view of the opponent and is at least two tiles from where it fired, from the nearest three chosen by the seat's own random stream so it is not predictable, and drives there by the usual route. It stops relocating on arrival, when the gun is ready (it shoots from wherever it is, which is still a place with a view, since steering is slow and a three-tile trip takes about 3.5s), or when hit. When the cooldown ends it is usually already settled and shoots. Waiting without moving is then only the last moments before a shot, so it does not look frozen, even against a motionless opponent.
- The aim phase does not fire or turn while it is relocating (the cooldown is running), so it does not fight the route steering. Hit while relocating, it retaliates as now.
- The firing positions come from `RoutePlanner.FindFiringPositions` (the ring tiles with a view, nearest the opponent first), pure and tested on fixtures. Relocation does not happen with no ammunition (it flees).

### Overlay and measurement

- A new overlay mode `RELOCATE` alongside the existing ones (no `APPROACH`: Combat covers it).
- New harness measure: **seconds a computer tank stood motionless with the opponent in view and nothing it was waiting on** (not retaliating, recovering, or inside the last second before a shot). Acceptance: against a motionless human it is a small share of the time.

## Testing

- **Approach:** in range with a clear view but beyond the engage distance it moves (no longer holds) and closes in; inside the ring it holds; with no view it routes as now. (A lake between them needs no test of its own: the ring tile is on its side, so it shoots across the water.)
- **Fire discipline:** it does not fire beyond `FireDistanceTiles` even when lined up; it does inside it; it still closes in when out of range.
- **Relocation (PR 2):** after a shot it moves during the cooldown to a tile at the engage distance with a clear view, different from where it was; the choice comes from the seat's stream (reproducible, different per seat); it is settled before the cooldown ends; retaliation still takes over when hit.
- **Mirror tests:** each scenario with the seats swapped gives the same command.
- **Headless matches (the harness), recorded before and after in docs/tuning.md and pinned loosely:** accuracy, hits a minute, share of rounds decided against drawn, time motionless in view, and that matches finish. The targets: computer-versus-computer accuracy back near the 34% it had before stuck recovery, a clearly larger share of decided rounds, and motionless-in-view time under a few percent. If damage, ammunition or the round time limit need to change for rounds to end, that is reported as a finding and decided separately, not changed here.
- Soak invariants still hold over long matches on several seeds; same seed gives the same match.

## Open choices for review

- First-to-3 matches are only reachable if computers actually kill each other; the tuning knobs above are the first resort. If after tuning rounds still mostly draw, the next dials are the round time limit and damage or ammunition (a separate decision).
- Relocation picks from the nearest few firing positions. The count (default 3) is a dial for how unpredictable and how mobile it is.
- Strafing while aiming was considered and left out: the movement and aiming phases each turn the tank 0.04 rad a step and fight each other, which is why a firing window under 0.07 rad cannot work.

## Out of scope

- Evasion (#85), which comes next and overrides all of this while a shell is about to hit.
- A settings page for skill or distances (#62); changes to damage, ammunition or the round time limit; smarter tactics (cover, leading shots).

## Acceptance

- A motionless human in range no longer meets a motionless computer: it closes in, shoots, and keeps moving between shots.
- It does not fire from beyond where its error can hit.
- Recorded before-and-after numbers; computer-versus-computer matches finish; reproducible from the seed.
- `dotnet build` has no new warnings and `dotnet test` passes; played against the computer by hand.

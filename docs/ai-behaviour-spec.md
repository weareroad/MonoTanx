# Computer opponent behaviour: aim error, stuck recovery and evasion — spec

Tracks GitHub issues #52 (stuck detection and seeded aim error) and #85 (evading incoming shells). Builds on the `ComputerController` (#51) and the headless match harness (#70). Follows the design tenet that either seat can be human or computer (`AGENTS.md`, #63): everything here is written in terms of "self" and "the opponent", and each computer-controlled seat has its own state and its own random stream.

## Problem

- **Aim is a fixed tolerance, not an error.** The computer fires once it is within 0.2 rad of the opponent, so where a shot lands depends on which side it turned in from, not on any variation from shot to shot. It is neither believable nor reproducible-by-design.
- **It can stay wedged.** There is no check that it is making progress. A tank driving into a wall or a corner (pressed against terrain, or blocked by the other tank) can keep trying forever.
- **It ignores incoming shells.** It chases, retaliates and aims whatever is about to hit it. The computer's first priority should be to avoid being hit, while it has fuel, from either tank's shells and from rebounds (including its own shells coming back).
- A related observation from #45: about three computer rounds in four draw on the time limit, so how the computer moves and misses matters to how a match plays.

## Goals

- Each shot has a random aim error drawn from the seat's own seeded stream, bounded and scaled by a skill value.
- A stuck computer notices and recovers.
- The computer predicts shells that will hit it and takes evasive action ahead of everything else, but not perfectly, so a human can still hit it.
- Same seed and same inputs give the same match.
- Every change is measured with the headless harness, not just unit tested, and what changed in the balance is recorded.

## Design decisions (recommendations; change any in review)

### Per-seat AI random stream and skill

- `RandomStreams.CreateStream("ai-1")` and `("ai-2")` give each seat its own derived stream, so two computers do not share randomness and neither disturbs the gameplay or cosmetic streams. `MatchSimulation` takes the two streams (optional; with none given the controller uses no randomness: no aim error and no missed notices, which keeps the existing tests as they are). The stage passes them from `game.Random`.
- `Player.ComputerSkill` (0 to 1, default in `Tuning.Ai`) is the per-seat setting that scales aim error and the chance of noticing a shell. It sits with the other per-`Player` computer defaults (`ComputerAimToleranceRadians` and friends), so the settings page (#62) can expose it per seat later. It is not exposed in this work.

### Aim error (#52)

- Each shot draws one **window** (the largest error it may have) from the seat's stream, between `AimToleranceRadians` and `AimToleranceRadians + MaximumAimErrorRadians × (1 − skill)`, and the computer fires as soon as the tank points within that window of the opponent. The draw happens when it starts aiming and is kept until the shot is fired, so it does not re-roll every update, and the next shot draws afresh. The error of a shot is therefore random, bounded by its window and reproducible from the seed. With no stream there is no random error: the window is the narrowest.
- *Changed from the first draft.* The draft aimed at the opponent plus a signed random error and fired within a tiny tolerance of that. Measured, that fires a quarter as often (the movement phase and the aiming phase each turn the tank 0.04 rad a step, so a window under about 0.07 rad is missed over and over while the tank is following a route), which would have changed the game far more than intended. Drawing the window itself keeps the firing rate and gives the same bounded, seeded, per-shot error. The narrowest window is therefore 0.07 rad, not the small tolerance the draft assumed.
- The defaults (skill 0.5, maximum extra 0.5 rad) make the windows average 0.195 rad, the same as the fixed 0.2 rad tolerance they replace, and are chosen by measurement so computer-versus-computer hits per match stay close to today's.

### Stuck detection and recovery (#52)

- **Progress.** While the controller has been commanding a drive (and could pay for it), it tracks the position at the start of a window. If, after `StuckWindowSeconds` of commanded driving, the tank has moved less than `StuckMinimumDistance`, it is stuck. Time spent deliberately not driving (holding position in range with a clear view, retaliation or aiming turns, no fuel) does not count, so waiting is not mistaken for being stuck.
- **Recovery.** A short manoeuvre for `StuckRecoverySeconds`: reverse while turning to a side chosen from the seat's stream, then drop the route and plan afresh. If the same goal gets it stuck again straight away, it takes a different goal (the next-nearest combat ring tile, or the other pickup) instead of repeating itself.
- **Fuel.** Reversing costs fuel as usual. With too little fuel to recover, it does nothing, as today.
- The controller exposes a stuck count and a `Recovering` mode for the overlay and the tests.

### Evading incoming shells (#85)

- **Priority.** `PlanMove` becomes: evade a predicted hit; otherwise retaliate; otherwise seek a pickup; otherwise flee with no ammunition; otherwise pursue or hold, as now. Evasion needs fuel: with none, the computer cannot move, so it carries on as it would. The aim phase never turns the tank while it is evading (it may still fire if already lined up).
- **Prediction.** The controller is given the shells in flight (`PlanMove(elapsed, pickups, shells)`). For each shell, whoever fired it, it steps a copy using the real `Shell` rules (reflections and all) over `EvadeLookaheadSeconds` in coarse steps with the tanks standing where they are, and finds the time the copy first reaches the computer's tank. A new `Shell.Clone()` provides the copy. Only shells within `EvadeDetectionDistance` are considered.
- **Choosing a response.** If a hit is predicted, it tries a small set of commands (turn left, straight or right; forward, stop or reverse) held for `EvadeCommitSeconds`, simulating its own movement with the real `TankMovement` rules on a scratch copy of the tank, and picks one after which no shell predicted to hit does so, preferring the least change from what it was doing. If none survives, the hit cannot be avoided in time: it accepts it and spends no fuel on a hopeless dodge. Candidates it cannot afford are skipped.
- **Not perfect (so a human can still win).** Three limits, all in `Tuning.Ai`: the detection distance; a reaction delay (a shell must have been in flight for `EvadeReactionSeconds` before it is noticed); and a per-shell chance of never noticing it, drawn once when the shell is first seen from the seat's stream and scaled by skill. Most close shots are undodgeable anyway: a shell crosses a tank's 15px hit width in about 0.06s but a tank needs about 0.17s to clear it.
- **Cost.** A few shells times a few candidates times a few dozen coarse steps per update, only when a threat is predicted; measured in the plan and kept off the frame budget.
- **Debug overlay.** The mode line shows `EVADE` and `RECOVER` alongside `PICKUP`, `FLEE`, `LONG` and `COMBAT`.

## Testing

- **Aim error:** for a fixed seed, the sequence of errors is reproducible; every error is within the bound; different seats get different sequences; with skill 1 the error is zero; the error is held for a whole shot and redrawn after each.
- **Stuck:** scripted situations on fixture maps: a tank driving into a wall is detected after the window and recovers and reaches its goal; waiting with a clear view is not "stuck"; no fuel is not "stuck"; a second stuck on the same goal picks a different one.
- **Evasion:** scripted shells on an open fixture: a shell that will hit is dodged; one too close to dodge is not (and no fuel is spent); a shell that will rebound off a reflective tile and then hit is dodged; the computer's own rebounding shell is dodged; with no fuel it does not move; with no threat its commands are unchanged; evasion beats retaliation and pickup seeking; the seed decides which shells are noticed.
- **Mirror tests:** each scenario with the seats swapped gives the same command.
- **Headless matches (the harness):** the soak invariants still hold over long matches on several seeds; a tank is never commanded to drive while making no progress for longer than a few windows; the same seed gives the same match; and balance numbers are recorded before and after each step: hits per match, rounds decided against drawn, and stuck events. They are pinned loosely in tests and recorded in `docs/tuning.md` so a later change has to update them on purpose.
- **Equivalence:** a build comparison against `main` for the first PR with aim error switched off (skill 1) shows the same trajectories as before except for the new tolerance; later PRs are judged by the balance numbers, since they deliberately change how the computer plays.

## Out of scope

- A settings page for skill (#62) and per-seat difficulty names.
- Changing damage, reload, round time limits or other tuning (any need for that is a finding to report).
- Smarter tactics than the three here (using cover, leading shots, hunting a moving target).
- The attract mode (#64) beyond what it gets for free.

## Acceptance

- The computer no longer stays wedged against terrain, shown over long headless matches.
- Aim error is reproducible from the seed, bounded, and different per seat.
- A predicted hit is evaded ahead of retaliation and chasing while there is fuel, including rebounds and its own shells, but not perfectly.
- Balance numbers before and after are recorded; computer-versus-computer matches stay reproducible.
- `dotnet build` has no new warnings and `dotnet test` passes; played against the computer by hand.

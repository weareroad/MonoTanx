# Run log: seeing why the computer did what it did — spec

Tracks GitHub issue #66 (a run log that records the seed). This is its first slice, started because hand-testing evasion (#85) could not show whether the computer was dodging: the overlay that shows the computer's mode pauses the game, so a live decision was invisible. Follows the design tenet that either seat can be human or computer (`AGENTS.md`, #63).

## What it does

- **Always on, always a file.** Every game writes `logs/run-seed<seed>-<time>.log` beside the game's binaries (so under `bin/` while developing, which is not committed), flushed line by line so it can be followed with `tail -f`. The seed and the path are printed to the console at the start.
- **`--log` echoes it to the console** as the game is played, for watching while hand-testing.
- **The first lines** give the seed and how to re-run it, the command line, who controls each seat, and the version; **the last lines** are a summary: time played, rounds won and drawn, and for each seat shots, hits taken, dodges (and how many failed), shells not noticed, unavoidable shells and times stuck. The summary is written when the game is left or quit.
- **Each line** has the simulated time (not wall time: it stops when the game is paused), the round, the seat as `P1(human)` or `P2(cpu)` (who controls it now), a short kind, and text.

## The lines

| Kind | Meaning |
|---|---|
| `FIRE`, `HIT`, `DESTROYED`, `PICKUP` | What the simulation reports. A `HIT` says whose gun the shell came from, and if the tank had started dodging a moment before, that the dodge failed. |
| `DODGE` | The computer saw a shell that would hit it and chose a move: the shell's gun, how far away it was, how long until it would have hit, and the move (forward or reversing, turning left or right). |
| `DODGE-OK` | No hit within 1.6s of a dodge: it worked. |
| `NOT-NOTICED` | A shell would have hit it but it did not see it: the random draw and the chance it needed to beat. |
| `UNAVOIDABLE` | It saw the shell but no move gets clear in time (and says if it has no fuel). |
| `STUCK` | It has been driving without moving and is backing away, with where it was. |
| `ROUND n starts`, `ROUND n won by`, `ROUND n drawn`, `MATCH won by` | From the match rules. |

## Design

- **It only watches.** `RunLogger` (in `Core`, no I/O of its own: it is given a function that writes a line) reads the events the simulation and the match already report and writes text. It is called after each update and cannot change play; a test checks a run is identical with and without it. A run from the same seed writes the same lines (a test compares two), which is what #66 asks for.
- **The computer reports its decisions as events**, not through the logger: `ComputerController.TakeDecisions()` hands the stage-side simulation what it decided (`ComputerDecision`: kind, the shell's gun, distance and time to hit, the move, the draw and chance, where it was, its fuel), and `MatchSimulation` turns them into `MatchEvent`s. Nothing in the computer reads them back. A shell remembers whose gun it came from (`Shell.Shooter`), and a hit event carries it.
- **Each shell is reported once per kind**, when the computer first decides about it, so a shell that stays a threat for a second is one line, not sixty.
- **Failure is quiet.** A log that cannot be written is skipped (the game still runs, and says so at the start).

## Done and not done (of #66)

Done: the seed and setup first, a file per run, the summary, dodging and not-dodging decisions, stuck, shots and hits, pickups, rounds, the console option, reproducibility from the seed. Not done: a computer changing goal, anomaly detection (no progress or no hits for a long time), the game's commit rather than its version, and logging human input to replay a game.

## Testing

Line formats and the dodge follow-ups (failed, worked), the header and summary, labels following who controls a seat, rounds and matches from the match state, the events from the simulation (`Shooter`, `Source`, decisions), the same seed giving the same log, different seeds differing, logging not changing the match, and a match between computers logging its dodges. `--log` in the options tests.

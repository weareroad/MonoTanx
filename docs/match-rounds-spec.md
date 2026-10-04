# Rounds, score and the end screen — spec

Tracks GitHub issues #45 (score, rounds and reset) and #46 (the end-of-match screen). Builds on the headless `MatchSimulation` (#70). Follows the design tenet that either seat can be human or computer (`AGENTS.md`, #63).

## Problem

A hit only reduces health, and when a tank reaches zero `GameStage` calls `game.Exit()` and the application quits. There is no score, no round, nothing to win, and nowhere to go afterwards. The demo mode (#64) also needs rounds and matches that restart themselves.

## Goals

- A match is a sequence of rounds. A destroyed tank scores for the other seat, the round resets, and the first seat to reach the target wins the match.
- A new `Core` match/round state machine, testable headlessly with two computer-controlled seats as well as human ones.
- An end-of-match screen that says who won, the score and the rounds played, with Play again, Home screen and Quit.
- No stale state carries over into a new round or a new match.

## Design decisions (recommendations; change any of them in review)

- **`MatchState`** (in `Core`, no graphics, audio or input): holds the round number, each seat's score, the current phase and a phase timer. The stage feeds it each update the elapsed time and the simulation's events, and it reports what changed as its own events. It does not know about tanks.
- **Phases:** `Countdown` (a brief pause before play, with a 3-2-1 style count shown), `Playing`, `RoundOver` (a short pause to show what happened), and `MatchOver`. After `RoundOver` it goes to `Countdown` of the next round, or to `MatchOver` when a seat has reached the target.
- **Scoring:** a `TankDestroyed` event for a seat scores one round for the opposite seat. If both tanks are destroyed in the same update the round is a draw. A draw scores nothing for either seat and the round is replayed (it does not count toward the target).
- **Round time limit:** `Playing` ends in a draw when the round timer reaches a limit in `Tuning`. This is needed because two computers can stall once their ammunition is gone, and it applies to every mode so rounds always end.
- **Rounds to win:** first to **3**, in `Tuning`. A match is therefore at most 5 decided rounds; draws are replayed.
- **What carries over between rounds:** nothing. Each round starts like a fresh match: both tanks at their start positions and headings with full health, fuel and ammunition, no shells in flight, every pickup restored, controllers reset. Random numbers continue from the gameplay stream, so the same seed still gives the same match.
- **During `Countdown` and `RoundOver` the tanks cannot act.** In `Countdown` nothing is stepped. In `RoundOver` only the shells in flight are stepped, so the last shot and its explosion finish, but no tank moves, fires or collects. Engine sounds idle in both.
- **`MatchSimulation.ResetRound()`** (new, in `Core`) does the reset above in one place: tank positions and headings (`PlaceAtStart`), `ResetResources`, shells cleared, pickups reactivated, controllers reset, events cleared. A new match is a `ResetRound()` plus a fresh `MatchState`; the `GameStage` stays the same stage, so the camera, shake and audio are reused. Shake and engine state reset with it.
- **A destroyed tank is not drawn** for the rest of the round (its health is zero and the banner shows who scored).
- **Labels come from the seat and its controller**, not fixed names: a human seat is "Player 1" or "Player 2", a computer seat is "Computer" when it is the only computer and "Computer 1" or "Computer 2" when both are. The winner text, banner and HUD use `MatchSetup` for this (a new `LabelOf(seat)`), computed from the current controller because `F2` and `F4` can change it during play. Nothing assumes Player 1 is human.
- **HUD:** shows each seat's score and the round number next to the existing health, shell and fuel readouts; a centred banner shows the countdown and "<label> scores" or "Draw".
- **End screen (`EndStage`)**, in the style of `HomeStage`, with mouse and keyboard through `MenuSelection`: shows "<label> wins", the final score and the rounds played, and **Play again** (same setup and seed handling: a new match with the same `MatchSetup`), **Home screen**, and **Quit**. `Esc` behaves as in `GameStage`: it returns to the home screen, or quits when the game was started with `--test` (the end screen itself still appears with `--test`, since it is part of the game).
- **Demo (no human):** there is no end screen. After `MatchOver` the match restarts itself after a short pause (a value in `Tuning`), and `Esc` still returns to the home screen. Any-key-to-leave and the idle trigger belong to #64.
- **Audio:** the existing cues are reused; a fanfare is a nice to have and out of scope until a sound exists (#33 hooks).
- **Tuning values** (all in `Core/Tuning.cs` with comments, indexed in `docs/tuning.md`, derived numbers pinned in `TuningTests`): rounds to win, countdown seconds, round-over seconds, round time limit, match-over pause in demo.

## Testing

- `MatchState` unit tests: phase order and timing; a score for the opposite seat; a mutual kill and a time-limit expiry are draws that score nothing and replay the round; the target ends the match and names the winner; scores mirror when the seats are swapped.
- `MatchSimulation.ResetRound` tests: positions, headings, resources, shells, pickups and controllers are back to the start; the same seed gives the same sequence of rounds.
- Headless whole-match tests with the existing `MatchHarness`: two computers play a complete match to `MatchOver` on several seeds, with the soak invariants checked every update; determinism; every control combination; the round time limit ends a stalled round.
- Equivalence: a build comparison against `main` for the first round (the play is unchanged until a tank is destroyed).
- Played by hand in one-player, two-player and demo, including the end screen's options and `Esc`.

## Out of scope

- Authored spawn points (#47); the start positions stay as they are.
- The demo's idle trigger, labels and quieter mix (#64), settings (#62), gamepads (#53, #54).
- A fanfare or other new sounds.

## Acceptance

- A full match can be won or lost without the application quitting, in every mode.
- Score and round number are visible in the HUD; the end screen shows the winner, score and rounds, and Play again and Home screen both work.
- No stale state carries over (engines, shells, score, pickups).
- Round and match rules are covered by headless tests, including with two computer seats.
- `dotnet build` has no new warnings and `dotnet test` passes; checked in play.

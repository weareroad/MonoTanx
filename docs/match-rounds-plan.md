# Rounds, score and the end screen — plan

Delivered as three PRs against #45 and #46, each leaving the game playable.

1. **The rules in `Core`.**
   - Add the `Tuning` values (with comments, `docs/tuning.md` and `TuningTests`), `MatchState` with its phases and events, `MatchSession` (the simulation and the rules together), `MatchSetup.LabelOf(seat)`, and `MatchSimulation.ResetRound()`.
   - Unit tests for `MatchState` and `ResetRound`; headless whole-match tests with `MatchHarness` driving `MatchState` over the simulation (a full computer versus computer match to `MatchOver` on several seeds, a stalled round ending on the time limit, a mutual kill, determinism, every control combination).
   - No change to `GameStage`: the game plays exactly as before.
2. **`GameStage` runs the match (#45).**
   - The stage owns a `MatchSession` and calls its `Step` (which gates the simulation by phase and resets rounds); the `TankDestroyed` handler no longer calls `game.Exit()`.
   - HUD scores and round number, the countdown and round banner, the destroyed tank hidden, engines idle in the pauses, the demo restarting its match.
   - At `MatchOver` for a human game, this PR goes back to the home screen as a stand-in until the end screen exists (or restarts the match; decided in review), so nothing dead-ends.
   - Build comparison for the first round; played in all modes.
3. **The end screen (#46).**
   - `EndStage` in the style of `HomeStage` with `MenuSelection`: winner, score, rounds, Play again, Home screen, Quit; `Esc` as in `GameStage`; hooked up from `GameStage` at `MatchOver` for any game with a human.
   - Update `README.md`, `onboarding.md` (match flow and the stage's remaining role) and the roadmap resume point (the first complete match milestone).
   - Played in one-player and two-player; a demo never shows it.

## Notes

- The simulation is not stepped during `Countdown`, and only its shells are stepped during `RoundOver`. `MatchSession` owns that decision through `MatchState`'s phase, so the simulation stays unaware of rounds.
- `ResetRound()` must leave nothing behind, so the headless tests compare a reset simulation against a fresh one on the same seed's position and resource state, and run several rounds back to back.
- A draw that replays forever is possible in principle (two computers that always stall). Rounds always end by the time limit and a draw is replayed, so a match could in theory not finish. If soak tests show it happens, add a cap on draws in review rather than speculatively here.

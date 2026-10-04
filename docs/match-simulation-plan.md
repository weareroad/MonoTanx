# Headless match simulation — plan

Delivered as three PRs against #70, each leaving the game playable and playing exactly as before (checked by the build comparison each time).

1. **Shells, damage, pickups and events.**
   - Add `MatchSimulation` with the tanks, shells and pickups, the event types and the events list.
   - Move shell stepping, the hit reaction (`TankDamage`, the controller's `Hit()`) and pickup collection in; `GameStage` calls them through the simulation and maps the events to sound and shake (reflection, terrain hit, tank hit, destroyed, pickup).
   - Event tests for a reflecting shot that hits a tank, a wall hit and a pickup.
   - Firing, reload and the controllers stay in the stage for now.
2. **Firing, reload, controllers and the update order.**
   - Move firing, reload ticking, the controllers and the full update order in behind `Step(elapsed, humanCommands)`; add `Moved`, `Motion`, `SetControl`, `ResetFuelAndAmmunition` and the starting-position helper.
   - `ShellFired` and `ReloadReady` events; the stage keeps animation, engine sound and shake.
   - Tests for the update order, firing gates through the simulation and the control swap.
   - `GameStage` has no simulation logic left.
3. **The headless harness and soak tests.**
   - A test harness that runs a match for N simulated seconds from a seed and returns the event log and final state.
   - Determinism tests, soak assertions over several seeds, all four control combinations, and the seat-symmetry tests.
   - A last build comparison of a one-player run against the pre-#70 `main`.
   - Update `onboarding.md` (the simulation and the stage's remaining role) and the roadmap resume point.

## Notes

- The order of the update is the thing most likely to drift: the engine motion is judged between firing and shells, and a computer seat ticks its reload at the start of its movement phase while a human seat ticks after moving. The spec keeps both; the comparison covers them.
- #45 (rounds) sits on top of this: it consumes `TankDestroyed` and resets the simulation. The simulation should expose a way to reset itself to the start state without being rebuilt, added in #45 rather than speculatively here.

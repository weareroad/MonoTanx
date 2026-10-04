# Computer opponent as a controller — plan

Delivered as three PRs against #51, after #63 is merged. Each leaves the game playable and the computer playing exactly as before.

1. **The command seam.**
   - Add `TankCommand`, the `ApplyInput(command)` overload and `TankMovement.FuelCost`, and `PickupState` (with the stage's `Pickup` extending it).
   - The human path builds a `TankCommand` from the keys and applies it.
   - Tests for the command overload, `FuelCost` (including that `ApplyInput` charges the same amount), and the human mapping.
   - No change to the computer yet; no change in play.
2. **Route planning.**
   - Move `FindRoute`, the combat-ring goals and the escape goals into a pure `RoutePlanner` with its own tests.
   - `GameStage` calls it; the computer's behaviour is unchanged (checked by the build comparison).
3. **The controller.**
   - Add `ComputerController` and move the rest of the logic out of `GameStage`: movement, retaliation, pickup seeking, evasion, aiming and firing, and the state. The stage applies its two commands in the same order as today and reports `ShotFired()` and `Hit()`.
   - Behaviour tests per rule, the mirror test, and the build comparison across the scenarios in the spec.
   - Update `onboarding.md` (seats and controllers) and remove the "moves into Core in #51" notes.

## Follow-up (a separate issue, not part of this plan)

A headless match simulation in `Core`: tanks, shells, pickups, the controllers and the update loop, so a computer versus computer match can run without graphics. That is what #45 (the match state machine), #64 (demo and soak test) and #66 (the run log) all want, and it replaces the build-comparison technique with ordinary tests. It would move the simulation parts of `GameStage` (shell updates, damage, pickup collection, firing) behind a small interface for sounds and effects.

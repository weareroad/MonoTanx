# Computer opponent engagement — plan

Two PRs against #90, each measured with the headless harness, before evasion (#85).

1. **Approach, fire discipline and the motionless measure.**
   - First, in the harness: the "motionless in view" measure, and the numbers recorded for today's computer (accuracy, hits a minute, decided rounds, motionless time, against computers and against a motionless human).
   - `EngageDistanceTiles` (6) and `FireDistanceTiles` (8) in `Tuning.Ai` (replacing `PreferredCombatDistanceTiles` as the ring); the controller approaches by route when in view but beyond the hold distance, holds inside it, and the aim phase fires only within the fire distance with a view and turns only once stopped. (The `APPROACH` overlay mode is not added: the existing Combat mode covers it.)
   - Controller tests (approach, hold, no view, lake between, fire discipline, mirror) and the balance numbers; tune the two distances against the targets in the spec.
2. **Shoot and scoot.**
   - `RoutePlanner` returns the candidate firing positions (ring tiles with a view); after each shot the controller relocates during its cooldown to one of the nearest few, chosen from the seat's stream; `RELOCATE` overlay mode.
   - Controller tests (moves during the cooldown, lands on a viewing tile at the engage distance, seeded choice, settled before the cooldown ends, retaliation interrupts, mirror) and the balance numbers, including motionless-in-view time.
   - Update `docs/tuning.md`, `onboarding.md` (the controller's behaviour) and the roadmap.

## Notes and risks

- **Both PRs change how the computer plays**, so the judgement is the measured numbers plus hand-testing, not an equivalence check. Expect to iterate the two distances.
- **Hit-or-miss against stuck recovery.** Closer engagement means more wall and tank contact; stuck recovery (already in) and the soak invariants (no tank on blocked terrain or overlapping) are the check that it still behaves.
- **Evasion later.** A computer that moves between shots is already harder to hit; evasion will make it harder again, so the balance numbers recorded here are the baseline for that step.

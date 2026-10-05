# Computer opponent behaviour — plan

Three PRs, each leaving the game playable, each measured with the headless harness. Order matters: aim error first (small, easy to measure), stuck recovery second, evasion last because it changes the balance most.

0. **Baseline (inside PR 1, before any change).** Add a way for the harness to report balance numbers for a set of seeds (hits per match, rounds decided against drawn, how long matches take, and a count of frames where a tank was commanded to drive but stayed put). Record today's numbers in `docs/tuning.md` and pin them loosely in a test. These are what every later step is compared with.
1. **Per-seat AI stream, skill and aim error (#52).**
   - `MatchSimulation` takes the two per-seat AI streams (optional), the stage passes them from `game.Random`; `Player.ComputerSkill`; the aim error and its small firing tolerance in `Tuning.Ai`.
   - Controller tests for the error (reproducible, bounded, held for a shot, redrawn after, zero at skill 1, different per seat) and the mirror test.
   - Choose the defaults so hits per match stay near the baseline; record before and after. With the error off (window 0.2 rad, no stream) the harness reproduced the baseline numbers exactly.
2. **Stuck detection and recovery (#52).**
   - The progress window, the stuck state and the recovery manoeuvre in the controller, with the stuck count and `RECOVER` mode for the overlay; pickup seeking steers like combat (drives only when facing the waypoint).
   - Scripted fixture tests and the long-run headless check that no tank stays stuck: stuck time fell from about 32 to under 2 seconds in every minute, longest run 1.7s.
   - Result: the computers now roam instead of sitting aimed at walls, so accuracy falls (34% to 10%) and almost every computer round draws (see `docs/tuning.md`, "What the numbers mean"). Whether to fix that, by having the computer fire only where its error can hit, is for the next conversation (below), not done here.
3. **Evading incoming shells (#85).**
   - `Shell.Clone()`, the prediction, the candidate search, the priority change in `PlanMove` (now given the shells) and the aim phase's behaviour while evading; the three limits (distance, reaction time, chance of noticing) in `Tuning.Ai`.
   - Scripted tests (open fixture, rebound off a reflective tile, the computer's own rebound, undodgeable, no fuel, no threat, priority over retaliation and pickups, mirror) and the headless balance numbers: hits per match must fall by a bounded, recorded amount, and the cost per update is measured.
   - Update `onboarding.md` (the controller's priorities), `README.md` (the computer's behaviour) and the roadmap.

## Follow-up candidates found along the way

- **Fire from where the error can hit.** The computer holds position and fires from anywhere inside half the map width with a clear view; with a firing window of about 0.2 rad that is mostly wasted ammunition. Closing in first (or holding fire at range) and counting shells would make computer rounds end.
- **Ammunition against kills.** Nine hits kill and a tank has 20 shells, so it needs 45% accuracy to kill a still target; a tuning question for damage, ammunition and the round time limit.
- Both interact with evasion (#85), which will make the computer harder to hit again.

## Notes and risks

- **Balance.** Dodging and stuck recovery both change how often the computer is hit and how long rounds last. Aim error alone should be roughly neutral by construction of the defaults; evasion is not, and the limits in PR 3 are the dials that keep a human able to win. If measurement shows computer rounds still mostly draw, that is a finding to report, not something to fix by stealth (damage, reload and the round time limit stay as they are).
- **Two-phase update.** Movement and aiming are still two phases per update. Evasion lives in the movement phase and tells the aim phase to hold its turn; unifying the phases stays out of scope.
- **Determinism.** The only new randomness is the aim error, the stuck recovery's side and the per-shell notice chance, all from the seat's own stream; the existing seeded gameplay behaviour is untouched. Draws happen in a fixed order (shell list order).
- **Performance.** Evasion only simulates when a shell is predicted to hit. If the measured cost is too high, coarsen the steps or shorten the lookahead rather than skipping frames.

# Hardening pass: testable core rules — plan

Delivered as three PRs against issue #16. Each leaves the game playable and the suite green.

1. **Separate `WorldMap` data from rendering, and add map tests.**
   - Split texture loading and `Draw` into `MapRenderer`; make `WorldMap` path-only.
   - Update `GameStage` construction and drawing.
   - Add TMX/TSX test fixtures and tests for terrain, collision, line of sight and pickup parsing, plus a check of the checked-in arena.
   - Smoke test: arena renders and plays as before.

2. **Extract tank movement, fuel and firing; add `Player` tests.**
   - Move movement, sliding and fuel rules, and firing and reload rules, into focused `Core` classes.
   - Remove the texture dependency from firing.
   - Add tests for `Player` resources and ammunition, fuel, movement and firing rules.
   - Smoke test: driving, fuel drain, firing and reload.

3. **Extract shells, damage and pickups; seed randomness.**
   - Move shell stepping and reflection, damage and pickup collection into `Core`.
   - Inject `Random` for the hit heading disruption, derived from a master seed with a separate cosmetic stream for screen shake.
   - Add the `--seed <integer>` command-line option and show the seed in the debug overlay.
   - Add tests for shells, damage and pickups.
   - Smoke test: firing, reflection, hits, pickups, computer opponent.

After the last PR, update `onboarding.md`, the `AGENTS.md` boundaries and `docs/roadmap.md` (mark Phase 1.3 and the hardening step done and refresh the resume point), then pick up score and round reset.

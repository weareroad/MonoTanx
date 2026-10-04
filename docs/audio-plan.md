# Audio hooks — plan

Delivered as three PRs against issue #33. Each leaves the game playable.

1. **Plumbing and the first cue.**
   - Add `tools/generate_placeholder_audio.py` and commit its WAVs, with the `Content.mgcb` entries.
   - Add `SoundCue`, `GameAudio` (loading, play, no-device fallback), `Tuning.Audio`, and the `--mute` option with its tests.
   - Wire the **fire** cue (both tanks, computer at a different pitch).
   - Check: the game runs, a PipeWire stream appears, nothing is thrown, and `--mute` plays nothing.
2. **The remaining one-shot cues.**
   - Extend `ShellStepResult` (reflections, end reason) and `Player.TickReload` (reload just finished), with tests.
   - Wire reload ready, explosion, ping, crump and pickup.
3. **Engine loops.**
   - Add the motion classification, the engine state and volume fade, and the per-tank looping instances, with tests.
   - Handle the pause overlay and leaving the game screen.

After the last PR, update `README.md`, `onboarding.md`, `docs/tuning.md` and the roadmap resume point (mention the audio hooks and how to replace the placeholders), and tell the maintainer which files to replace with real assets.

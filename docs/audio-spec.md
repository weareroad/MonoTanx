# Audio hooks — spec

Tracks GitHub issue #33. We have no real assets yet, so this builds the hooks and proves them with generated placeholder sounds.

## Cues

| Cue | When | Notes |
|---|---|---|
| Fire | A tank fires a shell | Player 2 (computer) uses the same sound at a different pitch |
| Reload ready | A tank's reload timer reaches zero and it still has ammunition | Gun cocked; Player 2 at a different pitch |
| Explosion | A shell hits a tank (including the one that fired it) | Loud |
| Ping | A shell reflects off a reflective surface | One per reflection |
| Crump | A shell hits a non-reflective blocking surface (wall, hill, map edge) | Dull thud |
| Pickup | A tank collects a pickup | |
| Engine (forward, reverse, turn) | Continuous, per tank | Quiet drone at three pitches |

A shell that simply runs out of flight time makes no sound.

## Design decisions

- **The rules stay silent.** Nothing in the simulation depends on audio. The rules already report what happened; a few results are extended so the stage can tell what to play:
  - `ShellStepResult` also reports how many reflections happened in the step and why a shell ended (expired, hit terrain, hit a tank, too many reflections).
  - `Player.TickReload` reports when the reload has just finished.
  - A pure function classifies a tank's motion from how far it moved and turned in an update (idle, forward, reverse, turn).
- **`GameAudio` plays the cues.** One class owns the loaded `SoundEffect`s and the per-tank engine instances. `GameStage` calls it at the places where these things happen. Cues are an enum, `SoundCue`.
- **One-shots** use `SoundEffect.Play(volume, pitch, pan)`; the computer's variation is a pitch offset from `Tuning`.
- **Engines** use one looping `SoundEffectInstance` per tank, with `Pitch` and `Volume` set from the motion state. The state priority is forward or reverse over turn over idle (turning while driving plays the drive sound). Volume ramps towards its target over a short, elapsed-time-based fade so changes do not click. An idle tank is silent. The computer's engine uses a pitch offset so the two tanks can be told apart.
- **Pause and leaving.** While the simulation is paused (the `F5` overlay) the engines fade out. Leaving the game screen (`Esc` to the home screen) stops and disposes them.
- **Flat mix.** Every cue plays centred at a fixed volume; no panning or distance fade yet. Revisit once the basics work (the main downside is the computer's off-screen engine and shots being as loud as the player's in the follow camera).
- **`--mute`.** A launch option that starts with all audio off. `GameAudio` then does nothing.
- **No audio device.** If the audio device or an asset cannot be used (`NoAudioHardwareException`, a missing file), `GameAudio` disables itself with one console message and the game carries on silently.
- **Tuning.** Volumes, pitches, the computer's pitch offset, the engine pitches per state and the fade time live in `Tuning.Audio`, with comments, per `docs/tuning.md`.
- **Assets.**
  - WAV only: 16-bit PCM, built with `WavImporter` and `SoundEffectProcessor` at `Quality=Best`. This needs no `ffmpeg` (checked: zero calls during a full rebuild), so it builds the same on any machine.
  - Files live in `MonoTanx/Content/Audio/` and are named after the cues: `fire.wav`, `reload.wav`, `explosion.wav`, `ping.wav`, `crump.wav`, `pickup.wav`, `engine.wav` (a seamless loop).
  - A small Python script, `tools/generate_placeholder_audio.py`, generates synthetic placeholders so each hook is distinct. The committed WAVs are its deterministic output, so there are no licensing questions.
  - To use a real sound, replace the file with one of the same name. Record the source and licence of any real asset in `MonoTanx/Content/Audio/README.md`.

## Out of scope

- Music and menu sounds.
- Stereo panning and distance attenuation.
- A settings screen or in-game volume keys.
- Different sounds for different surfaces, and ammunition-specific sounds.
- Real audio assets (placeholders only).

## Test coverage

- Shell results: reflections reported per step, and the end reason for each case (expired, terrain, tank, too many reflections).
- Reload ready: reported once, only when ammunition remains, not repeatedly while idle.
- Motion classification: idle, forward, reverse, turn, and turn-while-driving counts as the drive state, including for a tank moved by the computer.
- Engine target selection and the volume fade (reaches the target, never overshoots, takes the same time at any step size).
- `--mute` option parsing.
- `GameAudio` itself cannot be heard in a test; it is checked by running the game (no exceptions, and PipeWire shows a playback stream) and by listening.

## Acceptance

- Each cue in the table plays at the right moment in a normal game, and the three engine sounds follow the rules above.
- `--mute` silences everything; a machine with no audio device still runs the game.
- `dotnet test` passes with no new warnings, and gameplay is unchanged.

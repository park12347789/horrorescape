# External Audio Sources

This file records externally sourced audio currently wired into the live `R`
scene loop, plus staged candidate clips already imported into the project.

## Active selections

- `Assets/Resources/Audio/Sfx/FootstepsStoneSneaker_xkeril_CC0.mp3`
  - runtime use: player walk/sprint footsteps via excerpted variants in
    `PrototypeAudioManager`
  - source page: https://freesound.org/s/677069/
  - direct file: https://cdn.freesound.org/previews/677/677069_13504080-hq.mp3
  - license: CC0
  - note: the imported file is the public preview MP3 because the original WAV
    download is login-gated on Freesound

- `Assets/Resources/Audio/Sfx/FootstepsBootsTileMono_NoxSound_CC0.mp3`
  - runtime use: shared ground enemy footstep source for
    `PrototypeEnemyAudioDriver`
  - source page: https://freesound.org/people/Nox_Sound/sounds/530588/
  - direct file: https://cdn.freesound.org/previews/530/530588_9250976-hq.mp3
  - license: CC0
  - note: the imported file is the public preview MP3 because the original WAV
    download is login-gated on Freesound

- `Assets/Resources/Audio/Sfx/GlassShatter3_GregSurr_CC0.mp3`
  - runtime use: thrown glass bottle break clip, trimmed to the first `1.0`
    second at runtime
  - source page: https://freesound.org/people/Greg_Surr/sounds/554563/
  - license: CC0
  - note: the imported file is the public preview MP3 because the original WAV
    download is login-gated on Freesound

- `Assets/Resources/Audio/Sfx/MetalStairsFootSteps_sagamusix_CC0.mp3`
  - runtime use: vent enemy continuous crawl loop and vent-node step accents
  - source page: https://freesound.org/people/sagamusix/sounds/452421/
  - license: CC0
  - note: the imported file is the public preview MP3 because the original FLAC
    download is login-gated on Freesound

- `Assets/Resources/Audio/Sfx/mechanical1_BMacZero_CC0.wav`
  - runtime use: vent enemy emerge/spawn accent when entering the playable area
  - source page: https://opengameart.org/content/mechanical-sounds
  - direct file: https://opengameart.org/sites/default/files/mechanical1.wav
  - license: CC0

- `Assets/Resources/Audio/Sfx/battery_replace_remote_cover_3s.wav`
  - runtime use: flashlight battery replacement one-shot when a stored battery
    is consumed to refill charge
  - source page: https://pixabay.com/sound-effects/opening-and-closing-remote-control-battery-cover-96496/
  - license: Pixabay Content License
  - note: trimmed from the local source MP3 to a 3 second clip starting at
    0.25s, with short edge fades to avoid clicks

## Staged selections

- `Assets/Resources/Audio/Sfx/FlickeringFluorescentLightHum_kentspublicdomain_CC0.mp3`
  - intended use: room light hum / fluorescent flicker ambience candidate
  - source page: https://freesound.org/people/kentspublicdomain/sounds/777053/
  - direct file: https://cdn.freesound.org/previews/777/777053_5583936-hq.mp3
  - license: CC0
  - note: the imported file is the public preview MP3 because the original WAV
    download is login-gated on Freesound

## License check

- Freesound pages above explicitly mark the selected sounds as `Creative Commons 0`.
- The staged `kentspublicdomain` fluorescent light loop is also explicitly
  marked `Creative Commons 0` on Freesound.
- OpenGameArt marks `Mechanical Sounds` as `CC0`.
- Pixabay marks the remote battery cover source as free to use under its
  Content License.
- As of 2026-04-23, these selections are compatible with commercial game use
  without attribution requirements, though attribution remains welcome.

## Consistency workflow

- Run `Tools/Main Escape/Audio/Write Loudness Audit Report` in the Unity editor
  after importing or replacing clips.
- Review the generated `Docs/AudioLoudnessAudit.md` report for `Peak dBFS`,
  `RMS dBFS`, asset outliers, and configured playback outliers before retuning
  runtime volume fields.
- Treat the asset report as the baseline layer.
- Use the configured playback section for the real balancing pass because it
  includes the current excerpt logic and per-event base volumes from the live
  prefabs and audio scripts.

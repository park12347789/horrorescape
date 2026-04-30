# Horror Ambient Music Candidates

These tracks were downloaded on 2026-04-12 for polish review.

All files in this folder are from OpenGameArt and were selected because the source pages list the license as `CC0`.

Recommended usage

- `CreepyAmbientLoopV2_epb9000_CC0.ogg`
  - Best fit for lobby or menu background.
  - Source: https://opengameart.org/content/creepy-ambient-loop
  - License: CC0

- `EmptyCity_yd_CC0.ogg`
  - Best fit for stealth exploration on 5F/4F.
  - Source: https://opengameart.org/content/emptycity-background-music
  - License: CC0

- `ColdSilence_Eponasoft_CC0.ogg`
  - Stronger, colder atmosphere for deeper descent or slower sections.
  - Source: https://opengameart.org/content/cold-silence
  - License: CC0

- `AmbientHorror_Techiew_CC0.ogg`
  - More aggressive ambient texture; better as a tension layer or special event loop than a full-time menu track.
  - Source: https://opengameart.org/content/ambient-horror
  - License: CC0

Integration note

- `Assets/Scripts/Audio/PrototypeAudioManager.cs` now loads the lobby/gameplay
  loops from `Assets/Resources/Audio/Music/` when those runtime music assets are
  present.
- Other tracks in this folder remain staged candidates until they are promoted
  into `Assets/Resources/Audio/Music/` and documented in
  `Docs/AudioAssetSources.md`.

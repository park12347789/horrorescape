# Audio Loudness Audit

Generated: 2026-04-14 23:35:59

Audio root: `Assets/Resources/Audio`

Use this report to compare imported clip loudness before tuning runtime volume multipliers.
RMS is the best quick comparison for perceived level. Peak shows headroom. Crest is the difference between them.
Configured playback snapshots below apply the current excerpt rules and base per-event volume fields, but they still exclude distance attenuation and shared global master/SFX mix.

Suggested workflow:
- Compare clips within the same category first, especially `Sfx` against `Sfx` and `Music` against `Music`.
- Treat clips more than about 4 dB away from their category median RMS as loudness outliers worth checking.
- Use the configured playback section to balance authored event volumes after clip selection or excerpt logic changes.

## Summary

### Sfx

- Count: 12
- Median RMS: -23.1 dB
- Loudest RMS: -13.4 dB at `Assets/Resources/Audio/Sfx/mixkit-gasping-zombie-963.wav`
- Quietest RMS: -47.3 dB at `Assets/Resources/Audio/Sfx/step_cloth4.ogg`

## RMS Outliers

### Sfx

- `Assets/Resources/Audio/Sfx/step_cloth4.ogg`: RMS -47.3 dB (-24.1 dB vs median)
- `Assets/Resources/Audio/Sfx/mixkit-gasping-zombie-963.wav`: RMS -13.4 dB (+9.7 dB vs median)
- `Assets/Resources/Audio/Sfx/alex_jauk-zombie-screaming-207590.mp3`: RMS -13.6 dB (+9.5 dB vs median)
- `Assets/Resources/Audio/Sfx/mouseclick_flashlight.wav`: RMS -17.4 dB (+5.7 dB vs median)
- `Assets/Resources/Audio/Sfx/MetalStairsFootSteps_sagamusix_CC0.mp3`: RMS -28.3 dB (-5.2 dB vs median)
- `Assets/Resources/Audio/Sfx/qubodup-hover2.wav`: RMS -27.8 dB (-4.7 dB vs median)

## Configured Playback Summary

- Count: 6
- Median effective RMS: -32.0 dB
- Loudest effective RMS: -25.8 dB at `Thrown Bottle Shatter`
- Quietest effective RMS: -36.9 dB at `Vent Node Step`

## Configured Playback Outliers

- `Thrown Bottle Shatter`: effective RMS -25.8 dB (+6.2 dB vs median)
- `Vent Node Step`: effective RMS -36.9 dB (-4.9 dB vs median)
- `Vent Crawl Loop`: effective RMS -36.1 dB (-4.1 dB vs median)

## Configured Playback Table

| Event | Source | Clip | Dur (s) | Base Vol | Clip RMS dBFS | Effective RMS dBFS | Clip Peak dBFS | Effective Peak dBFS | Notes |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Thrown Bottle Shatter | PrototypeAudioManager bottle shatter loudest 1.00s excerpt | Audit_GlassShatter_LoudestOneSecond | 1.00 | 0.50 | -19.8 dB | -25.8 dB | 0.0 dB | -6.0 dB | Base volume x0.50. |
| Enemy Spotted Scream (Ground) | Prefab `Assets/Prefabs/Enemies/MainEscape/Ground/Enemy_GroundRuntime.prefab` | alex_jauk-zombie-screaming-207590 | 2.54 | 0.18 | -13.6 dB | -28.5 dB | 0.0 dB | -14.9 dB | Base volume x0.18. |
| Enemy Spotted Scream (Vent) | Prefab `Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab` | alex_jauk-zombie-screaming-207590 | 2.54 | 0.18 | -13.6 dB | -28.5 dB | 0.0 dB | -14.9 dB | Base volume x0.18. |
| Vent Emerge | Prefab `Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab` + loudest 0.52s excerpt from vent emerge source | Audit_Vent_Emerge | 0.52 | 0.18 | -20.6 dB | -35.5 dB | 0.0 dB | -14.9 dB | Base volume x0.18. |
| Vent Crawl Loop | Prefab `Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab` + loudest 1.15s loop excerpt from vent movement source | Audit_Vent_CrawlLoop | 1.15 | 0.22 | -22.9 dB | -36.1 dB | -1.1 dB | -14.3 dB | Base volume x0.22. |
| Vent Node Step | Prefab `Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab` + loudest 0.34s excerpt from vent movement source | Audit_Vent_NodeStep | 0.34 | 0.22 | -23.7 dB | -36.9 dB | -3.9 dB | -17.1 dB | Base volume x0.22. |

## Clip Table

| Category | Clip | Dur (s) | Ch | Peak dBFS | RMS dBFS | Crest dB | Load | Compression | Mono | Normalize | Preload | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- | --- | --- |
| Music | Assets/Resources/Audio/Music/CreepyAmbientLoopV2_epb9000_CC0.ogg | 58.91 | 2 | n/a | n/a | n/a | DecompressOnLoad | Vorbis | no | yes | yes | Unity could not read sample data from this clip with the current import settings. |
| Music | Assets/Resources/Audio/Music/EmptyCity_yd_CC0.ogg | 100.00 | 2 | n/a | n/a | n/a | DecompressOnLoad | Vorbis | no | yes | yes | Unity could not read sample data from this clip with the current import settings. |
| Sfx | Assets/Resources/Audio/Sfx/mixkit-gasping-zombie-963.wav | 1.93 | 1 | -0.1 dB | -13.4 dB | 13.3 dB | DecompressOnLoad | Vorbis | yes | yes | yes |  |
| Sfx | Assets/Resources/Audio/Sfx/alex_jauk-zombie-screaming-207590.mp3 | 2.54 | 1 | 0.0 dB | -13.6 dB | 13.6 dB | DecompressOnLoad | Vorbis | yes | yes | yes |  |
| Sfx | Assets/Resources/Audio/Sfx/mouseclick_flashlight.wav | 0.11 | 1 | 0.0 dB | -17.4 dB | 17.4 dB | DecompressOnLoad | Vorbis | no | yes | no | Loaded on demand for analysis. |
| Sfx | Assets/Resources/Audio/Sfx/qubodupImpactMeat02.ogg | 0.50 | 1 | -0.5 dB | -19.3 dB | 18.9 dB | DecompressOnLoad | Vorbis | no | yes | no | Loaded on demand for analysis. |
| Sfx | Assets/Resources/Audio/Sfx/qubodupImpactMeat01.ogg | 0.50 | 1 | 0.0 dB | -22.6 dB | 22.6 dB | DecompressOnLoad | Vorbis | no | yes | no | Loaded on demand for analysis. |
| Sfx | Assets/Resources/Audio/Sfx/mechanical1_BMacZero_CC0.wav | 0.92 | 1 | 0.0 dB | -23.0 dB | 23.0 dB | DecompressOnLoad | Vorbis | no | yes | yes |  |
| Sfx | Assets/Resources/Audio/Sfx/GlassTrap_ShardStep_Mixkit172.wav | 0.64 | 1 | -1.5 dB | -23.2 dB | 21.7 dB | DecompressOnLoad | Vorbis | yes | no | yes |  |
| Sfx | Assets/Resources/Audio/Sfx/GlassShatter3_GregSurr_CC0.mp3 | 2.46 | 1 | 0.0 dB | -23.7 dB | 23.7 dB | DecompressOnLoad | Vorbis | yes | yes | yes |  |
| Sfx | Assets/Resources/Audio/Sfx/error.ogg | 3.20 | 2 | -10.9 dB | -25.0 dB | 14.1 dB | DecompressOnLoad | Vorbis | no | yes | no | Loaded on demand for analysis. |
| Sfx | Assets/Resources/Audio/Sfx/qubodup-hover2.wav | 0.05 | 2 | -6.9 dB | -27.8 dB | 20.9 dB | DecompressOnLoad | Vorbis | no | yes | no | Loaded on demand for analysis. |
| Sfx | Assets/Resources/Audio/Sfx/MetalStairsFootSteps_sagamusix_CC0.mp3 | 14.33 | 2 | -0.2 dB | -28.3 dB | 28.1 dB | DecompressOnLoad | Vorbis | no | yes | yes |  |
| Sfx | Assets/Resources/Audio/Sfx/step_cloth4.ogg | 0.19 | 2 | -29.4 dB | -47.3 dB | 17.9 dB | DecompressOnLoad | Vorbis | no | yes | no | Loaded on demand for analysis. |


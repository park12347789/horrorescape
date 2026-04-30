# Medical Foley Candidates

This folder stores candidate short-form SFX that are not yet wired into runtime code by default.

## Checked on 2026-04-23

Candidate target file name

- `zapsplat_hospital_syringe_dry_inject_plunger_push_002_46098.mp3`

Official source pages

- Asset page:
  - https://www.zapsplat.com/music/syringe-dry-no-liquid-push-plunger-down-inject-version-2/
- License page:
  - https://www.zapsplat.com/license-type/standard-license/

License status confirmed from official pages

- The asset page lists this sound under `Standard License`.
- ZapSplat `Standard License` was checked on `2026-04-23`.
- The current published `Standard License Agreement` page says it was last updated on `2025-09-29`.

Usage summary for this candidate

- Basic free users:
  - Personal, commercial, and broadcast project use is allowed.
  - Attribution to `ZapSplat` is required.
  - Free access is limited to the `mp3` version.
- Premium users:
  - The same broad project-use rights apply.
  - Attribution is not required for files downloaded during an active Premium period.
  - `mp3` and `wav` are available.

Important access rule

- ZapSplat states that sounds may only be downloaded through the user's own ZapSplat account.
- ZapSplat also states that automated access using bots, scrapers, or scripts is prohibited.

Repository handling decision

- Because of the access rule above, the sound file itself is intentionally **not bundled in this repository** by this repository.
- If the team wants to use this candidate, manually download it with the project's own ZapSplat account and place it here:
  - `Assets/Audio/Sfx/Candidates/ZapSplat/zapsplat_hospital_syringe_dry_inject_plunger_push_002_46098.mp3`

Project intent

- Candidate self-heal / syringe injection foley for medkit use.

Integration note

- The current project does not have a dedicated medkit-use `AudioClip` hook yet.
- `Assets/Scripts/Player/PlayerQuickItemController.cs` currently triggers the generic pickup SFX via `PrototypeAudioManager.TryPlayPickup()`.
- If this sound should play only for medkit use, add a dedicated medkit-use clip path or serialized field instead of replacing the shared pickup clip globally.

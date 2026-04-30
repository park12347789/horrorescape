# MainEscape Play Review Tuning Plan

- Status: design only
- Date: 2026-04-23
- Scope: playable tuning pass for the live `RMainEscape_Lobby -> RMainScene_5F -> RMainScene_4F -> RMainScene_3F -> RMainScene_2F -> RMainScene_1F -> RMainEscape_Lobby` loop

## Goal

Address the latest play-review notes with the lowest-risk changes first, while
preserving the authored scene chain and avoiding broad runtime refactors.

Reviewed play-review items:

1. background music should come down by about 25%
2. side-door interaction range and recognition feel too narrow
3. glass bottle is too strong
4. spotted-by-enemy feedback is too weak
5. vent enemy sound is too quiet

## Design Summary

This pass should prefer targeted prefab, scene, and local runtime tuning before
introducing any new framework. Four items can be improved through existing
authoring points:

- music through `PrototypeAudioManager`
- side-door feel through `CustomSideDoorClosed.prefab` and
  `MainEscapeSelfContainedDoor`
- bottle balance through `PrototypeItemCatalog`,
  `ThrowableBottleProjectile`, and support-item quantity rules
- vent loudness through `Enemy_CeilingVent.prefab`

The one item that needs a slightly broader presentation change is spotted
feedback. Even there, the safest route is still to reuse the existing
`PlayerSpotted` event and `IRThreatPanelView` path instead of adding a separate
alert UI system.

## Item 1: Background Music Down About 25%

### Current Ownership

- `Assets/Scripts/Audio/PrototypeAudioManager.cs`
  - `externalMusicVolumeMultiplier = 4.2f`
  - lobby and floor ambience both multiply music-only playback by this value
- live floor scene overrides:
  - `Assets/Scenes/RMainScene_5F.unity`
  - `Assets/Scenes/RMainScene_4F.unity`
  - `Assets/Scenes/RMainScene_3F.unity`
  - `Assets/Scenes/RMainScene_2F.unity`
  - `Assets/Scenes/RMainScene_1F.unity`

### Finding

Music is already separated from shared SFX gain through
`externalMusicVolumeMultiplier`, so this is the cleanest knob for a 25%
reduction without destabilizing footsteps, door audio, enemy cues, or UI SFX.

### Recommended Fix

- Reduce `externalMusicVolumeMultiplier` from `4.2` to `3.15`.
- Apply the same value to the script default and the serialized floor-scene
  overrides.
- Do not lower `masterVolume`, `sfxVolume`, or `ambienceVolume` for this note.

### Validation

- Lobby music should still establish tone but sit under UI/menu navigation.
- Floor music should no longer cover footstep, vent, or spotted cues.
- SFX balance should remain unchanged relative to each other.

## Item 2: Side-Door Interaction Range And Recognition

### Current Ownership

- `Assets/Prefabs/Environment/MainEscape/Doors/CustomSideDoorClosed.prefab`
  - `interactionDistance: 1.6`
  - `BoxCollider2D size: 0.9 x 1.9`
- `Assets/Scripts/Objectives/MainEscapeSelfContainedDoor.cs`
  - interaction distance uses blocker-bounds `ClosestPoint`
  - LOS point also uses blocker-bounds `ClosestPoint`
- `Assets/Scripts/Objectives/PlayerInteractionDriver.cs`
  - candidate selection rejects on LOS failure
- `Assets/Scripts/Grid/GameLayers.cs`
  - interaction blocking mask defaults to `Wall + Door`

### Finding

The feel problem is not just a small radius. Side doors are currently checked
through a strict combination of:

- blocker-bounds-based distance
- blocker-bounds-based LOS target point
- a raycast blocked by both wall and door layers

That makes doors in narrow corridors feel harder to prompt than their visuals
suggest.

### Recommended Fix

Phase 1, prefab-first and low risk:

- raise side-door `interactionDistance` from `1.6` to `1.85` or `1.9`
- widen the side-door blocker collider slightly so the effective prompt surface
  better matches the art

Phase 2, only if Phase 1 still feels strict:

- make `MainEscapeSelfContainedDoor` a little more permissive for door LOS so
  near-edge approach angles do not fail just because the ray clips adjacent
  door/wall geometry

### Validation

- prompt should appear reliably from front approach and shallow diagonal
  approach
- opening should not require pixel-precise alignment in tight corridors
- front-door behavior should remain unchanged unless intentionally tuned

## Item 3: Glass Bottle Is Too Strong

### Current Ownership

- `Assets/Scripts/Inventory/PrototypeItemCatalog.cs`
  - `throwDistance: 6.4`
  - `throwNoiseRadius: 6.8`
  - `stunDurationMin: 2`
  - `stunDurationMax: 3`
- `Assets/Scripts/Inventory/ThrowableBottleProjectile.cs`
  - `enemyProbeRadius: 0.32`
  - `enemyProbeStepDistance: 0.18`
  - `stunTargetForgivenessRadius: 0.12`
- `Assets/Scripts/Inventory/PlayerInventory.cs`
  - bottle stack cap is effectively unlimited because `MaxStackQuantity` is `0`
- `Assets/Scripts/Rebuild/Runtime/RFloorItemPlacementRuntime.cs`
  - support bottle placement gives `quantity = 2`
  - bottle spawn weight is higher than medkit and battery
- `Docs/status/MainEscapeCurrentState.md`
  - 5F also starts with 3 authored bottles

### Finding

Bottle power is stacking from multiple directions at once:

- a long direct stun window
- generous hit forgiveness on the projectile path
- unlimited inventory stacking
- support placements that award two bottles at a time
- a 5F starter loadout that already includes three bottles

This means the bottle is strong as both a disable tool and a resource economy
tool.

### Recommended Fix

Phase 1, safest balance pass:

- reduce stun from `2.0-3.0s` to `1.25-1.75s`
- reduce projectile forgiveness by tightening `enemyProbeRadius` and
  `stunTargetForgivenessRadius`
- reduce runtime support pickup quantity from `2` to `1`
- give bottles a real stack cap, preferably `3`

Phase 2, only if still too dominant:

- reduce `throwNoiseRadius` and `shatterNoiseRadius` slightly
- reduce bottle support spawn weight toward parity with medkits and batteries

Do not start by changing broad noise-system rules such as `NoiseSystem`
lifetimes or enemy-wide hearing contracts.

### Validation

- one bottle should still create a clutch escape window
- chain-stunning multiple enemies or one enemy repeatedly should feel harder
- the player should no longer accumulate a large bottle reserve by mid-run
- tutorial bottle beats should still function after runtime quantity changes

## Item 4: Spotted Feedback Is Too Weak

### Current Ownership

- `Assets/Scripts/Enemy/EnemyStateMachine.cs`
  - raises `PlayerSpotted`
  - `threatFeedbackHoldDuration: 1`
- `Assets/Scripts/Enemy/BaseOfficeVentEnemyBootstrap.cs`
  - raises `PlayerSpotted`
  - `threatFeedbackHoldDuration: 1`
- `Assets/Scripts/Audio/EnemyPlayerSpottedScreamAudio.cs`
  - direct `PlayerSpotted` audio subscriber
- `Assets/Scripts/Rebuild/UI/IRPlayerThreatHudBinder.cs`
  - polls confirmed threat sources into HUD presentation
- `Assets/Scripts/Rebuild/UI/IRThreatPanelView.cs`
  - only renders edge alpha and edge color
- `Assets/Scripts/Player/ThreatPanelPresentation.cs`
  - already has `Title` and `Detail` fields, but the current binder/view do not
    use them

### Finding

The current spotted moment has only one direct event-driven player cue:

- enemy scream SFX

The HUD path is continuous and subtle:

- it waits for confirmed threat
- it does not use `ShouldForceThreatFeedbackVisible` for early warning
- it has no text, icon, or one-shot pulse for the initial spotted moment

This makes the state readable if the player is already watching the enemy, but
weak as a reactive player-facing warning.

### Recommended Fix

Phase 1, reuse the current HUD path:

- raise enemy `spottedVolume` modestly on both ground and vent prefabs
- extend `threatFeedbackHoldDuration` from `1.0` to around `1.35-1.5`
- let the threat HUD respond to `ShouldForceThreatFeedbackVisible`, not only
  fully confirmed threat
- add a short one-shot edge pulse on `PlayerSpotted`

Phase 2, still using existing UI:

- render a minimal `SPOTTED` or `INVESTIGATING` label through the existing
  `ThreatPanelPresentation` fields

Do not introduce a second alert framework if the current threat panel can carry
the cue.

### Validation

- first spotted moment should read instantly even if the player is not staring
  at the enemy
- investigate-state warning should appear before full chase when the enemy is
  close to confirming vision
- chase readability tests should stay green after the HUD change

## Item 5: Vent Enemy Sound Is Too Quiet

### Current Ownership

- `Assets/Scripts/Audio/VentEnemyAudioDriver.cs`
  - `crawlPulseVolume: 0.22`
  - `nodeStepVolume: 0.22`
  - `transitionVolume: 0.18`
- `Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab`
  - matches those low default values
- `Assets/Scripts/Audio/EnemyPassiveAmbientAudio.cs`
  - vent passive ambient is also quiet
- `Docs/AudioLoudnessAudit.md`
  - flags vent crawl and vent node step as quieter configured playback events

### Finding

The project already has an audio audit showing vent cues landing among the
quietest authored playback events. This is a tuning problem, not a routing
problem.

### Recommended Fix

Raise vent-prefab loudness in layers:

- `crawlPulseVolume` to roughly `0.30-0.34`
- `nodeStepVolume` to roughly `0.30-0.34`
- `transitionVolume` to roughly `0.24-0.28`
- `passiveAmbientVolume` to roughly `0.055-0.065` if the vent enemy still
  feels absent between movement beats

Avoid changing distance falloff first. The complaint currently points to cue
strength, not world reach.

### Validation

- vent crawl should be readable before the emerge moment
- node transitions should stand out from general room ambience
- vent enemy should become more trackable without drowning out footsteps or
  chase cues

## Recommended Implementation Order

1. music reduction and vent loudness
2. side-door interaction prefab pass
3. bottle balance pass
4. spotted feedback pass

This order handles the cheapest high-confidence wins first, then moves to the
systems with the most player-perception impact and the highest regression risk.

## Manual Verification Checklist

- Verify lobby, 5F, and 1F music balance with default runtime options.
- Verify `CustomSideDoorClosed` from straight-on and diagonal approach in at
  least 5F and 2F.
- Verify bottle stun feel against one ground enemy and one vent enemy, then
  check total bottle availability through early floors.
- Verify the player can clearly tell the difference between investigate,
  confirmed spotted, and active chase.
- Verify vent crawl, node, and emerge cues remain audible under lowered music.

## Out Of Scope For This Pass

- scene routing changes
- package or pipeline changes
- replacing the current HUD framework
- broad noise-system rebalance
- moving authored scene data out of the current live `R` scene chain

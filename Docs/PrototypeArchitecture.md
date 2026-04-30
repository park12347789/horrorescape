# MainEscape Architecture

## Scope

This document maps current runtime ownership for the live authored loop. It is
a reference for shared systems, not a requirement that every scene-authored
feature route through a central owner.

Use this doc when you need to answer:

- which runtime system owns a responsibility
- which scene-facing objects are part of the current shared loop
- which runtime surfaces are still transitional bridges

Use these related docs for adjacent concerns:

- `Docs/RSceneRebuildManifest.md` for loop safety guidance, cleanup direction, and
  validation gates
- `Docs/SystemAuditCleanupPlan.md` for staged cleanup watchpoints
- `Docs/design/MainEscapeLiveLoopSystem.md` for player-facing system behavior
- `Docs/MainEscapeAuthoringGuide.md` for day-to-day authored scene workflow

## Canonical Scene Setup

- Build index `0`: `Assets/Scenes/RMainEscape_Lobby.unity`
- Build index `1`: `Assets/Scenes/RMainEscape_tuto.unity`
- Build index `2`: `Assets/Scenes/RMainScene_5F.unity`
- Build index `3`: `Assets/Scenes/RMainScene_4F.unity`
- Build index `4`: `Assets/Scenes/RMainScene_3F.unity`
- Build index `5`: `Assets/Scenes/RMainScene_2F.unity`
- Build index `6`: `Assets/Scenes/RMainScene_1F.unity`
- Build index `7`: `Assets/Scenes/RMainEscape_ElevatorTransition.unity`

The project is centered on the authored `RMainEscape_Lobby -> RMainScene_5F~1F -> RMainEscape_Lobby` floor route, with `RMainEscape_tuto` and `RMainEscape_ElevatorTransition` as support scenes outside the floor route arrays. The lobby scene is the public entry point, while the floor scenes own the live gameplay loop. Within those scenes, individual features should usually own their own serialized references and scene-local data.

## Ownership Map

### Lobby

- `Assets/Scripts/Rebuild/UI/IRLobbyController.cs`
  - owns start-run, quit, and last-run summary behavior

### Session persistence and scene routing

- `Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs`
  - persistent run snapshot owner across lobby and floor scenes
  - tracks current floor, floors cleared, and run outcome
- `Assets/Scripts/Rebuild/Runtime/RSceneRouter.cs`
  - loads lobby, current floor, retry, and exit-destination scenes

### Gameplay composition

- `Assets/Scripts/Rebuild/Runtime/RSceneCompositionRoot.cs`
  - owns the runtime composition graph for each floor scene
  - binds authored scene references, player runtime, HUD, fog, pickups, and floor flow
- `Assets/Scripts/Rebuild/Runtime/RFloorDirector.cs`
  - refreshes floor runtime state and floor presentation
- `Assets/Scripts/Rebuild/Runtime/RRunController.cs`
  - owns floor progression, failure/final-clear flow, and run-modal presentation
- `Assets/Scripts/Rebuild/Runtime/REncounterSpawner.cs`
  - spawns patrol, sentry, stalker, and vent enemies for the active floor

### Authored floor bridge

- `Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs`
  - still bridges authored floor roots, tilemaps, markers, routes, doors, and props into the live `R` runtime
- `Assets/Scripts/Objectives/MainEscapeRuntimeSettings.cs`
  - holds route-alignment data, shared authored names, and validation-facing defaults for the live scene chain
  - should remain a support/reference asset, not the default place for feature-specific behavior

## HUD and Run Modal Surface

- `Assets/Scripts/Rebuild/UI/IRHudCanvas.cs`
  - authored gameplay HUD anchor for inventory, quick slots, health, threat, and run modal
- `Assets/Scripts/Rebuild/UI/IRInventoryPanelView.cs`
  - inventory surface
- `Assets/Scripts/Rebuild/UI/IRQuickSlotsPanelView.cs`
  - square quick-slot layout with icon-first rendering and quantity-only display
- `Assets/Scripts/Rebuild/UI/IRHealthPanelView.cs`
  - health and flashlight summary surface
- `Assets/Scripts/Rebuild/UI/IRThreatPanelView.cs`
  - chase/readability edge presentation
- `Assets/Scripts/Rebuild/UI/IRGameClearPanelView.cs`
  - floor-clear, final-clear, and failure modal surface

## Player and Binder Layer

- `Assets/Scripts/Rebuild/Player/RPlayerRuntimeReferences.cs`
  - groups player runtime references already placed on the authored player object
- `Assets/Scripts/Rebuild/UI/IRPlayerInventoryHudBinder.cs`
- `Assets/Scripts/Rebuild/UI/IRPlayerHealthHudBinder.cs`
- `Assets/Scripts/Rebuild/UI/IRPlayerThreatHudBinder.cs`
- `Assets/Scripts/Rebuild/UI/IRPlayerQuickSlotsHudBinder.cs`

The binder layer is still coupled to player-side runtime state and should stay under watch during future refactors, but the live scene chain already routes through the `IR*` HUD surface.

## Authoring Scene Baseline

The current authored floor scenes center on:

- `RSceneRoot`
- `RSystems`
- `RGameplay`
- `RRuntime`
- `RAuthoring`

Important authored scene objects:

- `IRHudCanvas`
- `Player`
- `RFloorRuntime`
- `REncounterSpawner`
- `RFogOfWarOverlay`
- the floor-specific authored prefab/marker payload under `RAuthoring`

## Visual Readability Systems

### Fog and visibility

- `Assets/Scripts/Objectives/FlashlightFogOfWarOverlay.cs`
  - player-readable fog state
  - explored vs. unexplored visibility

### Enemy vision presentation

- `Assets/Scripts/Enemy/EnemyVisionVisualizer.cs`
  - layered enemy vision and threat-state presentation
- `Assets/Scripts/Enemy/Common/EnemyThreatVisualFeedback.cs`
  - reinforces alertness readability beyond the raw vision wedge

### Audio and noise readability

- `Assets/Scripts/Audio/PrototypeAudioManager.cs`
  - owns ambience and shared prototype audio routing
- `Assets/Scripts/Audio/PrototypePlayerAudio.cs`
  - drives player footsteps and item interaction sounds
- `Assets/Scripts/Noise/NoiseEmitter.cs`
  - movement and interaction noise source

## Operational Surface

The following editor tools are part of the current runtime-support workflow,
but this document tracks them only as part of the ownership map. Loop-safety
decisions still belong in `Docs/RSceneRebuildManifest.md`.

- `Tools/Main Escape/Validate Lobby Scene Preflight`
- `Tools/Main Escape/Validate Start Floor Runtime`
- `Tools/Main Escape/Toggle Active Scene Debug Mode`
- `Tools/Main Escape Rebuild/Cache Authored Floor References` (open authored floor scenes only)

Legacy setup tools that regenerate HUD, sentries, goal visuals, or ambient guide lights are disabled in the clean-loop branch. The live workflow assumes direct authored placement instead.

## Test Coverage

- `Assets/Tests/EditMode`
  - runtime settings
  - run session expectations
  - floor route ordering
  - authored/runtime reference checks
- `Assets/Tests/PlayMode`
  - lobby controller flow
  - loop smoke coverage
  - run-modal surface checks

## Current Direction

Read the project as a live `R` scene chain with some legacy authoring/runtime bridges still present underneath it. The next architectural cleanup steps should keep the authored scene chain stable while shrinking those bridge points, moving simple behavior back toward feature-owned Inspector references instead of adding more fallback behavior.

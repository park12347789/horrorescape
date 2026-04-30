# R Scene Rebuild Manifest

## Scope

This manifest records the live-loop safety baseline, cleanup direction, and
validation gates for the authored scenes.

Use this doc when you need to answer:

- what the checked-in live runtime path is
- what cleanup work is low risk for the current loop
- what should be checked before old paths are reduced further

Use these related docs for adjacent concerns:

- `Docs/PrototypeArchitecture.md` for runtime ownership mapping
- `Docs/SystemAuditCleanupPlan.md` for staged cleanup work
- `Docs/design/MainEscapeLiveLoopSystem.md` for player-facing behavior
- `Docs/MainEscapeAuthoringGuide.md` for practical authored-scene workflow

## Goal

Keep the live lobby, optional tutorial support scene, authored `RMainScene_5F -> 4F -> 3F -> 2F -> 1F` floor route, elevator transition support scene, and return-to-lobby loop explicit, authored, and inspectable while transitional fallback paths are reduced in controlled stages.

The cleanup direction is:

- authored scene and prefab placement stays the preferred workflow
- feature components should own their explicit Inspector references and
  scene-local data where practical
- central settings and manifests are support references for shared route data,
  validation, and recovery, not the default owner for every feature
- existing `R` and `IR` names remain useful for established runtime and HUD
  systems, but new local feature objects do not need global naming unless they
  join those systems
- missing scene-critical pieces should be investigated and fixed at the authored
  source instead of being silently recreated at runtime

## Current Live Flow

Current build routing is:

- `Assets/Scenes/RMainEscape_Lobby.unity`
- `Assets/Scenes/RMainEscape_tuto.unity`
  - optional support scene outside `RFloorSceneEntry` arrays
- `Assets/Scenes/RMainScene_5F.unity`
- `Assets/Scenes/RMainScene_4F.unity`
- `Assets/Scenes/RMainScene_3F.unity`
- `Assets/Scenes/RMainScene_2F.unity`
- `Assets/Scenes/RMainScene_1F.unity`
- `Assets/Scenes/RMainEscape_ElevatorTransition.unity`
  - interstitial support scene for floor-to-floor loading

Current status:

- `ProjectSettings/EditorBuildSettings.asset` keeps lobby at index `0`, tutorial support at `1`, five floor scenes at `2` through `6`, and elevator transition support at `7`
- `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset` still tracks the lobby and five-floor route only
- `RMainScene_5F` is still the primary authored source floor
- lower floors remain part of the live route and should be treated as downstream verification targets after structural 5F edits
- deleted detached test-bay/showcase scenes are no longer part of live routing or live runtime composition

## Current Reference Points

Treat these as the active references for the live loop:

- route data comes first from `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`; serialized scene-local routing remains compatibility/alignment data
- `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset` should stay aligned with the route graph and lobby/floor route, while support scenes such as `RMainEscape_tuto` and `RMainEscape_ElevatorTransition` stay outside `RFloorSceneEntry` arrays
- the live gameplay floor scenes are `RMainScene_5F~1F`; lobby, tutorial, and elevator transition scenes are route/support scenes with their own ownership modes
- authored floor content still bridges through `MainEscapeFloorAuthoring`, which should own authoring root and marker root names locally for floor-scene authoring
- floor runtime composition routes through:
  - `RSceneCompositionRoot`
  - `RFloorDirector`
  - `RRunController`
  - `RRunSessionController`
  - `IRHudCanvas`

## Runtime Wiring Summary

This section summarizes only the live runtime path that the rules in this
manifest are protecting. It is not intended to replace the fuller ownership map
in `Docs/PrototypeArchitecture.md`.

### Floor scene composition

`RSceneCompositionRoot` currently coordinates:

- authored roots and authored scene references
- player runtime preparation
- HUD binder wiring
- fog/runtime binding
- authored pickups and transition points
- run-session hookup for the owning floor scene via scene-local/cached session resolution

Any fallback creation of missing roots or scene-critical objects in
`RSceneCompositionRoot` is transitional compatibility scaffolding. The cleanup
direction is to verify and repair authored scene/prefab references, then shrink
those fallback paths instead of treating them as the normal composition model.

### Floor runtime

`RFloorDirector` is currently responsible for:

- resolving authored floor content into runtime state
- rebuilding floor presentation/runtime blockers
- configuring fog-of-war bounds
- handing encounter configuration to `REncounterSpawner`

### Run/session flow

`RRunSessionController` and `RRunController` currently own:

- persistent run snapshot state
- floor progression and failure/final-clear flow
- stairs/final-exit handoff
- lobby return state
- run-modal presentation triggers

## Naming Guidance

### Runtime / Gameplay

Use the existing `R` prefix when adding to the established rebuild/runtime
systems or when consistency with nearby `R*` files makes the owner clearer.
Small scene-local feature objects can use descriptive names that match their
prefab/component purpose.

### UI

Use the existing `IR` prefix when adding to the established in-run HUD and UI
surface. Simple feature-owned UI helpers can be named for the feature they
belong to, especially when they are serialized directly on that prefab or scene
object.

## Known Structural Debt

- `MainEscapeFloorAuthoring` is still the authored-scene bridge between older MainEscape naming and the live `R` runtime
- `Batch2TestRoomBootstrap` is still part of the authored/runtime floor build path and remains large
- some scene reference recovery still depends on scene-name and hierarchy-name lookup instead of fully explicit serialized wiring
- legacy `Assets/Resources/Floors/MainEscape/*.prefab` baselines remain quarantined migration/recovery prefabs, not live floor authoring truth
- `PrototypeAudioManager` and `PrototypeSceneUtility` still carry a limited prototype compatibility role; scene-local generator choices should live on `PrototypeSceneGeneratorSettings`
- a fresh Unity compile/test pass is still needed after the latest cleanup wave

## Loop Safety Guidelines

- do not create scene-critical objects just to hide a missing authored reference
- do not reintroduce deleted detached test-bay/showcase scenes into Build Settings casually
- keep HUD panels authored in the scene or prefab unless there is a deliberate rebuild workflow
- when something stops working, investigate the broken reference, prefab, or scene data first; add fallback only when it is a conscious design choice

## Validation Exit Criteria

The live loop is considered ready for further old-file cleanup only when all of these are true:

- the lobby starts the run without spawning replacement scenes or replacement objects
- the gameplay loop loads only the authored `RMainScene_5F~1F` floor scenes for floor gameplay; tutorial and elevator transition remain support scenes outside floor arrays
- detached test-bay/showcase names are absent from the live runtime path
- UI panels appear from authored scene placement
- validators and relevant Unity tests pass after the cleanup wave
- one human validation pass confirms the full `RMainEscape_Lobby -> 5F -> 1F -> RMainEscape_Lobby` route

# MainEscape Live Loop System

## System Summary

- System name: MainEscape Live Loop
- Owner: current live `R` scene chain
- Status: active prototype / cleanup-and-polish phase
- Related scenes:
  - `Assets/Scenes/RMainEscape_Lobby.unity`
  - `Assets/Scenes/RMainScene_5F.unity`
  - `Assets/Scenes/RMainScene_4F.unity`
  - `Assets/Scenes/RMainScene_3F.unity`
  - `Assets/Scenes/RMainScene_2F.unity`
  - `Assets/Scenes/RMainScene_1F.unity`
  - optional support: `Assets/Scenes/RMainEscape_tuto.unity`
  - support: `Assets/Scenes/RMainEscape_ElevatorTransition.unity`
- Related prefabs/assets:
  - `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset`
  - `Assets/Prefabs/Player.prefab`
  - authored floor content under `RAuthoring`

## Goal

Provide one stable, inspectable playable loop that starts in the lobby, runs
through the authored floor chain, and returns to the lobby with clear success or
failure feedback.

## Player-Facing Behavior

The player starts in the lobby, begins a run, descends through `5F -> 1F`,
reads the space using local light and flashlight information, avoids or
out-positions enemies, collects progression items, and eventually sees floor
clear, final clear, or failure messaging before returning to the lobby summary.

The intended feel is cautious movement through incomplete information rather
than pure action.

The lobby may also offer an optional tutorial route. The tutorial is a separate
practice scene, not floor `5F`. It teaches movement, pickup, sprint, item use,
and bottle throwing with authored visual setups. When the player exits the
tutorial elevator, the session starts a fresh 5F run so tutorial inventory does
not carry into the main route.

## Runtime Ownership

- Entry points:
  - `IRLobbyController`
  - `RRunSessionController`
  - `RSceneRouter`
  - `RTutorialSceneBootstrap`
  - `RTutorialBottleTarget`
  - `RTutorialElevatorExitInteractable`
  - `RSceneCompositionRoot`
  - `RFloorDirector`
  - `RRunController`
- State owner:
  - cross-scene run state lives in `RRunSessionController`
  - active floor runtime state is coordinated through `RSceneCompositionRoot`
    and `RFloorDirector`
- Scene or prefab dependencies:
  - authored floor roots under `RAuthoring`
  - player runtime references
  - authored pickups, doors, stairs, exits, patrol routes, and vent routes
- UI or presentation dependencies:
  - `IRHudCanvas`
  - `IR*` panel and binder layer
  - fog-of-war, enemy threat presentation, and run-modal surfaces

## Data And Authoring

- ScriptableObjects or config assets:
  - `MainEscapeRuntimeSettings`
  - `MainEscapeRuntimePrefabCatalog`
  - player and enemy sprite profiles
- Authored scene markers or roots:
  - `RSceneRoot`
  - `RSystems`
  - `RGameplay`
  - `RRuntime`
  - `RAuthoring`
  - `Tiles_ground`
  - `Tiles_wall`
  - `Doors`
  - `Visual`
  - `GameplayOverlay`
  - `InteractiveProps`
  - `AuthoringMarkers`
- Tutorial authored objects:
  - runtime composition is allowed, but it should bind to authored scene content
    rather than replace missing critical references
  - `RMainEscape_tuto` keeps a small `RTutorialBootstrap` plus an
    `RTutorialAuthoring` root; the editor menu
    `Tools/Main Escape Rebuild/Materialize Tutorial Scene Authoring` only
    repairs the tutorial tilemap contract and requested exit door pair; deleted
    authored placement objects stay deleted
  - runtime bootstrap keeps default layout materialization off unless explicitly
    re-enabled on the component, so Play Mode does not restore objects that a
    designer intentionally removed
  - Play Mode spawns one lightweight tutorial sentry named `SentryGuard_01`
    from the ground enemy prefab, using the tutorial tilemaps for a local
    `GridMapService`; this is independent of floor encounter spawning
  - the tutorial authoring root owns `RTutorialTileGrid/Tiles_ground`,
    `RTutorialTileGrid/Tiles_wall`, and
    `RTutorialTileGrid/GameplayOverlay/Tiles_movenonlywall`; `Tiles_ground` is
    seeded from the real 5F `Tiles_ground` tilemap, `Tiles_wall` uses the Wall
    layer for movement and sight blocking, and `Tiles_movenonlywall` uses the
    Prop layer for movement-only, light-passing blocking
  - tutorial pickups should use the same Main Escape item prefab family used by
    floors where possible: flashlight, battery, glass bottle, medkit, and the
    objective key visual with `MainEscapeKeyPickup`; `RTutorialInventoryPickup`
    is only a fallback when no authored world pickup exists
  - tutorial blockers, cage pieces, breakable bottle wall, the 5F door pair
    (`VexedTileBProp_01_Top (8)` plus `CustomSideDoorClosed`), lighting, and
    small visual cue objects are materialized from existing Main Escape assets,
    then given tutorial-specific colliders, disabled enemy AI, and
    exit/bottle-target behavior as needed
  - after an authored tutorial object exists, setup does not overwrite its
    transform, and automatic edit-mode materialization remains disabled so scene
    placement stays designer-owned
- Save or persistence concerns:
  - player health, flashlight state, and inventory persist across floor loads
    through `RRunSessionController`
  - tutorial pickups are intentionally discarded by starting a new run before
    loading `RMainScene_5F`
- Contract reporting:
  - `Tools/Main Escape/Report Floor Scene Contracts` is a read-only authored
    scene report for current floor ownership, fixed pickups, marker-driven
    random layers, and hidden runtime indirection. It should not save scenes or
    treat generation quotas as scene repair commands.

## Validation

- Expected invariants:
  - the live authored lobby scene and its serialized `RRunSessionController`
    routing remain the primary route contract
  - Build Settings keeps the lobby first, the optional tutorial second, the
    five floor scenes after it, and `RMainEscape_ElevatorTransition` appended
    as the final support scene
  - `MainEscapeRuntimeSettings.asset` stays aligned with the five-floor route
    and does not include interstitial support scenes in floor arrays
  - the tutorial scene stays outside `RFloorSceneEntry` arrays and exits only
    through a fresh-start handoff to `RMainScene_5F`
  - the tutorial scene does not enable floor fog, floor vision rules,
    encounter spawning, or `RSceneCompositionRoot` floor composition
  - tutorial `Prop` blockers are movement-only; tutorial `Wall` blockers stop
    movement and line of sight, matching the floor-layer contract
  - tutorial tile painting stays on the authored tutorial tilemaps and does not
    alter the 5F tilemaps
  - when `Tiles_ground` exists, generated floor placeholder rectangles stay out
    of the tutorial scene so floor editing happens on real tilemaps
  - tutorial starts the player with one health segment, no carried inventory,
    no equipped flashlight, and zero flashlight battery charge
  - 5F guaranteed starter support pickups remain fixed authored scene pickups
    for the main route. They must keep
    `suppressRuntimeManagedPickupReplacement` or equivalent protection so
    runtime-managed replacement does not absorb them.
  - Random support placement is a separate opt-in `SupportPickup` manager
    layer; it should write only under its runtime pickup root and be reported
    separately from fixed starter pickups. If the runtime root or
    `00_Pickups` does not already exist, runtime must warn and fail the apply
    pass with `apply=0` / `false` instead of auto-creating a hidden root.
    `00_Pickups` is only a recommended authored root name and is not the
    ownership gate.
  - authored references are explicit and inspectable
  - HUD panels and run-modal surfaces resolve from authored scene placement
  - lower floors remain valid after structural 5F-first changes
- Common misconfiguration risks:
  - missing serialized bindings after hierarchy changes
  - stale lower-floor scene content after 5F-driven structural edits
  - reintroduced fallback behavior hiding authored drift
- Required editor tooling or validators:
  - `Tools/Main Escape/Validate Lobby Scene Preflight`
  - `Tools/Main Escape/Validate Start Floor Runtime`
  - `Tools/Main Escape/Report Floor Scene Contracts`
  - `Tools/Main Escape Rebuild/Materialize Tutorial Scene Authoring`
  - authored reference cache/backfill support tools when references drift

## Testing

- EditMode coverage:
  - runtime contracts
  - run-session flow
  - route ordering
  - authored/runtime binding checks
- PlayMode coverage:
  - lobby controller flow
  - loop smoke tests
  - run-modal behavior
- Manual verification notes:
  - perform full lobby-to-1F-to-lobby passes after major cleanup or structural
    scene changes

## Open Questions

- How far should authored/runtime bridge reduction go before splitting heavy
  classes such as `RRunController` or `Batch2TestRoomBootstrap`?
- When should the project formalize an Addressables or broader asset-loading
  strategy ADR?
- What parts of the current readability-and-polish phase should become durable
  system docs instead of remaining in status notes?

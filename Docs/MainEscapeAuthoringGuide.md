# MainEscape Authoring Guide

## Purpose

This guide covers the current authored-scene workflow for the live `R` loop.

Use it when you want to:

- edit the live playable layout
- move pickups, transitions, guards, or authored markers
- adjust floor-specific readability, lighting, or navigation support
- update gameplay blocking over a decorated map
- validate the floor-chain scenes after an edit pass

## Working Scenes

- `Assets/Scenes/RMainScene_5F.unity`
- `Assets/Scenes/RMainScene_4F.unity`
- `Assets/Scenes/RMainScene_3F.unity`
- `Assets/Scenes/RMainScene_2F.unity`
- `Assets/Scenes/RMainScene_1F.unity`

These are the gameplay authoring scenes for the live loop.

The public playable entry scene is:

- `Assets/Scenes/RMainEscape_Lobby.unity`

That lobby scene is for start-run and last-run summary behavior. Do not author floor content there.

Support scenes are authored separately from the floor route:

- `Assets/Scenes/RMainEscape_tuto.unity`
- `Assets/Scenes/RMainEscape_ElevatorTransition.unity`

Do not add support scenes to `RFloorSceneEntry` floor route arrays.
`RMainEscape_ElevatorTransition` may carry a legacy direct-play fallback to
`RMainScene_5F`, but route-data authority remains
`RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`; floor handoff uses
the lobby/session route plus `RElevatorTransitionRequestStore`.

Current status references:

- `Docs/status/MainEscapeCurrentState.md`
- `Docs/status/ImplementationRoadmap.md`

## Scene Layout Baseline

The checked-in floor scenes currently use these scene-facing roots. Keep them
stable when editing the shared loop, but do not treat them as a template every
small feature must expand through:

- `RSceneRoot`
- `RSystems`
- `RGameplay`
- `RRuntime`
- `RAuthoring`

Common authored runtime objects currently present in the floor scenes:

- `IRHudCanvas`
- `Player`
- `RFloorRuntime`
- `REncounterSpawner`
- `RFogOfWarOverlay`
- `RSceneCompositionRoot`

The preferred source for feature behavior is the authored object or prefab that
implements that behavior. Give that component explicit Inspector references and
scene-local data whenever practical. Use `RAuthoring` and shared runtime
bridges only for systems that already depend on them or that truly cross floor,
route, or HUD boundaries.

- Missing authored references should be fixed in the scene or prefab hierarchy.
- First analyze why a reference or behavior broke before adding fallback behavior.
- `RMainScene_5F` is a useful comparison floor for structural changes; propagate confirmed shared changes down to lower floors on purpose.
- `Report Floor Scene Contracts` is the current authored-scene reference report.
  It reads the lobby-authored floor route and opened floor scene hierarchy; it
  should not be treated as a scene mutation or prefab authoring tool.
- In that report, check the explicit `RSceneMarkerPlacementManager` line first:
  `status=no-manager`, `inactive`, `missing runtimeRoot`,
  `missing SupportPickup rule/input`, `no support item ids`, or `ready`.
  Also check support marker pool `populated=`/`empty=` state and
  `legacyQuotaBalance=shortage`/`surplus`/`matched`/`empty-no-legacy-quota`
  balance.
- `Validate Floor Marker Counts` has been removed from the editor menu. Its
  generated-map quota assumptions are no longer the authored scene source of
  truth.

## Map Authoring Baseline

The current checked-in root names are used by the live authored scene chain.
`Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset` should stay
aligned with those names for validation and support code, but ordinary feature
behavior should prefer local serialized references on the owning component.
For floor authoring, `MainEscapeFloorAuthoring` is the local owner of the
authoring root and marker root name contract. Do not route new ordinary
floor-root or marker-name decisions through `RSceneCompositionRoot` fallback
creation or runtime settings unless the change truly affects route-wide
validation or recovery.

Under the authored floor root, keep these gameplay-facing roots stable:

- `Tiles_ground`
- `Tiles_wall`
- `Doors`
- `Visual`
- `GameplayOverlay`
- `InteractiveProps`
- `AuthoringMarkers`

Common child roots for the current map workflow:

- `Visual/Tiles`
- `Visual/Props`
- `GameplayOverlay/BlockAll`
- `GameplayOverlay/Tiles_movenonlywall`
- `AuthoringMarkers/ItemPlacementMarkers`
- `AuthoringMarkers/KeyPlacementMarkers`
- `AuthoringMarkers/EnemyPlacementMarkers`
- `AuthoringMarkers/ChaserPlacementMarkers`

Legacy fallback roots may still exist during migration:

- `AuthoringMarkers/PatrolSpawn`
- `AuthoringMarkers/PatrolSpawnCandidates` (optional)
- `AuthoringMarkers/SentryGuards`
- `AuthoringMarkers/SentrySpawnCandidates` (optional)
- `AuthoringMarkers/StalkerSpawn`

Meaning of each root:

- `Tiles_ground`: walkable floor tiles
- `Tiles_wall`: hard wall tiles that stop movement and block vision/light
- `Doors`: tile-backed doors used by the runtime door path
- `Visual/Tiles`: presentation-only tiles
- `Visual/Props`: presentation-only props unless a specific runtime blocker path derives from them
- `GameplayOverlay/BlockAll`: tiles that stop movement and block vision/light
- `GameplayOverlay/Tiles_movenonlywall`: tiles that stop movement but stay visually/light-open
- `AuthoringMarkers/ItemPlacementMarkers`: primary support-item marker root for batteries, glass bottles, and medkits
- `AuthoringMarkers/KeyPlacementMarkers`: primary key placement marker root
- `AuthoringMarkers/EnemyPlacementMarkers`: primary ground-enemy marker root for patrol and sentry placement
- `AuthoringMarkers/ChaserPlacementMarkers`: primary chaser marker root
- `AuthoringMarkers/DangerMarkers`: primary glass-trap candidate root; runtime selects a seeded subset from these markers
- `AuthoringMarkers/PatrolSpawn`: legacy patrol fallback root
- `AuthoringMarkers/PatrolSpawnCandidates`: legacy patrol fallback candidate root
- `AuthoringMarkers/SentryGuards`: legacy sentry fallback root
- `AuthoringMarkers/SentrySpawnCandidates`: legacy sentry fallback candidate root
- `AuthoringMarkers/StalkerSpawn`: legacy chaser fallback anchor

Older roots such as `CoverProps` and `MovementBlockers` may still exist in legacy-authored content, but they are not the preferred authoring surface for new layout work.

## Working Rules

- Prefer direct scene and prefab placement over runtime creation for scene-critical objects.
- Keep serialized references explicit, inspectable, and owned by the feature
  component or prefab that uses them whenever practical.
- Treat `Visual` and `GameplayOverlay` as separate concerns: decorate first, then paint gameplay blocking deliberately.
- For thin privacy screens or temporary dividers, prefer the `DividerScreen_*` presets in `Assets/Prefabs/MainEscapeEditing/PlaceablePresets/SolidBlockers` over painting wide `BlockAll` strips.
- `MainEscapeMovementBlockerAuthoring` and `MainEscapeSolidBlockerAuthoring` now follow the placed scene transform scale for footprint sync. Resize these blockers from the scene transform instead of expecting the serialized `footprint` field to drive the instance back to cell size.
- `MainEscapeSolidBlockerAuthoring` is the authored path that blocks movement and flashlight/fog visibility together. Plain prop blockers remain movement-only.
- New placement markers should be authored through the `ItemPlacementMarkers`, `KeyPlacementMarkers`, `EnemyPlacementMarkers`, and `ChaserPlacementMarkers` roots. Keep the older pickup/patrol/sentry/stalker roots only as fallback during migration.
- The live `RMainScene_1F~5F` chain already ships with populated `ItemPlacementMarkers` and `KeyPlacementMarkers` arrays. Do not repopulate or depend on `PickupMarkers` in those scenes.
- The checked-in `Assets/Resources/Floors/MainEscape/*.prefab` baselines are quarantined legacy migration prefabs. They may carry seeded modern marker data for recovery or comparison, but they are not the normal authoring source and should only be changed through the explicit `Tools/Main Escape Rebuild/Legacy/* Resources Floor Prefabs` workflows. Treat direct `Pickup_BatteryMarker` / `Pickup_GlassBottleMarker` authoring as migrated legacy state for that placement system, not as a general rule against local pickup ownership.
- `Pickup_Flashlight.prefab` is a separate flashlight pickup prefab. Keep its behavior and references local to that pickup unless a shared pickup system explicitly needs them.
- `RMainScene_1F~5F` now place `ScenePlacementManagers/RSceneMarkerPlacementManager_SupportPickups` under `MainEscapeFloorAuthoring` for the random support layer. Keep each manager active/enabled, point `runtimeRoot` at `InteractiveProps/00_Pickups`, and point the marker source at `AuthoringMarkers/ItemPlacementMarkers`. The serialized `runtimeRoot` reference is the apply/readiness gate; the object name alone is not.
- The current support manager rules are scene-local: 5F `5/4/2` after clamping the `9/6/3` quota to the active marker cap of `11`, 4F `8/5/6`, 3F `6/11/8`, 2F `8/9/1`, and 1F `3/14/3`.
- Once such a support manager has real placement inputs, `RFloorDirector` removes only the legacy runtime support-item plan before applying item placement. Key placement remains marker-driven through the existing key plan.
- Before placing or validating a 5F `SupportPickup` manager in the Unity Editor, use this preflight checklist:
  1. Check `git status --short` or an equivalent dirty-state view and confirm the target scene and prefab files are clean enough to trust.
  2. If `Assets/Scenes/RMainScene_5F.unity` is already dirty, do not treat that scene state as validated until the authored change is saved and rechecked.
  3. Do not rely on automatic YAML or scene-text mutation for this step; keep the authoring pass editor-driven.
  4. Use the Unity Inspector to connect and verify `runtimeRoot`, prefab references, and marker bindings directly.
- Before placing the manager, also confirm whether the 5F fixed starter pickups are intentionally still the seven direct authored `PrototypeInventoryPickup` objects under `CoverProps` with `suppressRuntimeManagedPickupReplacement=true`: 3 glass bottles, 2 flashlight batteries, and 2 medkits.
- Keep the 5F fixed starter pickups as direct authored `PrototypeInventoryPickup` objects with `suppressRuntimeManagedPickupReplacement=true` unless you are deliberately changing that authored setup. The intended set is 3 glass bottles, 2 flashlight batteries, and 2 medkits, and those objects should stay outside the `InteractiveProps/00_Pickups` + `AuthoringMarkers/ItemPlacementMarkers` random support flow.
- If support-item quota or marker-pool counts look incomplete, treat that as runtime support manager transition data, not evidence that the authored 5F fixed starter pickups are missing.
- Support manager suppression is scoped to the legacy support item plan only. Do not let it absorb fixed starter pickups or the separate flashlight pickup prefab into the random support layer.
- `DangerMarkers` now act as the glass-trap candidate pool. Fill this root with `DangerMarker` prefabs so each candidate carries a `MainEscapeDangerPlacementMarker` component.
- The scene keeps the actual trap count in `glassTrapPlacementCount`, so you can place more danger markers than the number of traps that will spawn.
- Marker prefabs are authoring helpers only. They should stay easy to duplicate in the editor, but the play build should hide their visible presentation components.
- After moving or duplicating danger markers, run `Tools/Main Escape Rebuild/Normalize Danger Markers For Open Authored Floor Scenes` or recache the authored floor references so the serialized danger marker array stays in sync.
- For fog and flashlight tuning, start from `Docs/design/MainEscapeFogRefresh.md` instead of ad hoc scene tweaks. That document is the current rollout guide for the stabilized baseline.
- After hierarchy or prefab structure changes that touch the shared loop, re-check `RSceneCompositionRoot`, `IRHudCanvas`, and player/HUD binder references. For feature-local edits, re-check the owning component's serialized references first.
- The clear/result modal is authored under `Assets/Prefabs/IRHudCanvas.prefab` at `IRPanelRoot/IRGameClearPanel`; replace its panel, elapsed-time text, and `OkButton` there or on a scene prefab instance override.
- Clear/result modal art lives under `Assets/Art/UI/ClearResult`. Use `Tools/Main Escape/Apply Clear Result UI` only when the source art needs to be re-sliced and re-applied to the HUD prefab.
- Do not rely on deleted detached-scene tooling or older showcase workflows.

## Recommended Workflow

1. Open the target floor scene.
2. If the edit is structural, make it in `RMainScene_5F` first.
3. Update the visible map with `Tiles_ground`, `Tiles_wall`, `Doors`, `Visual/Tiles`, and `Visual/Props`.
4. Update gameplay-only blocking with `GameplayOverlay/BlockAll` and `GameplayOverlay/Tiles_movenonlywall`.
5. Re-check item markers, key markers, enemy markers, chaser markers, transitions, and authored reference bindings.
6. Verify the owning feature components still point to their intended objects; for shared loop edits, also verify `RSceneCompositionRoot`, `RFloorDirector`, `RRunController`, and `IRHudCanvas`.
7. Run the lobby preflight validator and the start-floor runtime validator after meaningful structural edits.
8. Port confirmed structural changes down into lower floors intentionally instead of assuming those scenes are still aligned.

## Common Tools

- `Tools/Main Escape/Validate Lobby Scene Preflight`
- `Tools/Main Escape/Validate Start Floor Runtime`
- `Tools/Main Escape/Report Floor Scene Contracts`
- `Tools/Main Escape/Apply Clear Result UI`
- `Tools/Main Escape/Run Integrity + Recovery Prep`
- `Tools/Main Escape/Toggle Active Scene Debug Mode`
- `Tools/Main Escape Rebuild/Rebuild Live Floor Vent Routes (1F-4F)`
- `Tools/Main Escape Rebuild/Prepare R Floors For Tile Authoring (1F-5F)`
- `Tools/Main Escape Rebuild/Cache Authored Floor References` (open authored floor scenes only)
- `Tools/Main Escape Rebuild/Legacy/Cache Legacy Resources Floor Prefab References`
- `Tools/Main Escape Rebuild/Legacy/Seed Legacy Placement Markers For Resources Floor Prefabs`
- `Tools/Main Escape Rebuild/Legacy/Cleanup Legacy Placement Sources For Resources Floor Prefabs`

Use the authored-floor reference cache tool only when serialized authored references drift out of sync. It is a support tool for the currently open authored floor scenes, not the normal iteration path and not a route-wide scene sync.
Use the floor scene contract report for read-only authored-scene reference checks and manual support-manager candidate checks.
Do not use the retired floor marker count quota gate for live authored scene checks.
Use the integrity/recovery prep tool manually after suspected scene recovery drift; its automatic project-load run should stay disabled during normal editing.
Use `F1` in play mode to toggle debug mode and inspect the live vent network over the authored floor layout.
The legacy prefab cache and prefab migration tools are for older or drifted `Assets/Resources/Floors/MainEscape/*.prefab` baselines that need to be brought back to the current marker-array contract. They live under `Tools/Main Escape Rebuild/Legacy/` and should not be used as part of normal live scene authoring.
Treat runtime-created fallback roots from `RSceneCompositionRoot` as migration scaffolding only. If one appears necessary, first inspect the missing scene/prefab reference and fix the authored source when practical.

## Runtime Verification Checklist

Before considering a scene pass stable, verify:

1. player spawn and facing are correct
2. pathing is not blocked by stray wall/door/overlay tiles
3. item, key, enemy, chaser, and vent-route markers still make sense after layout edits
4. key gate, emergency stairs, and final exit interactions still match the intended floor flow
5. HUD, fog, and run-modal references still resolve from authored scene placement
6. enemy visibility and enemy-vision readability still make sense in lit and unlit spaces
7. failure, retry, and return-to-lobby flow still behave as expected
8. lower-floor scenes were either updated intentionally or consciously left untouched after a 5F-only change

## Related Files

- `Assets/Scripts/Objectives/MainEscapeRuntimeSettings.cs`
- `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset`
- `Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs`
- `Assets/Scripts/Objectives/Editor/MainEscapeRuntimeValidator.cs`
- `Assets/Scripts/Grid/Batch2TestRoomBootstrap.cs`
- `Assets/Scripts/Grid/Batch2TestRoomBootstrap.AuthoredFloors.cs`
- `Assets/Scripts/Rebuild/Runtime/RSceneCompositionRoot.cs`
- `Assets/Scripts/Rebuild/Runtime/RFloorDirector.cs`
- `Assets/Scripts/Rebuild/Runtime/RRunController.cs`
- `Assets/Scripts/Rebuild/UI/IRHudCanvas.cs`
- `Assets/Scripts/Rebuild/UI/IRLobbyController.cs`

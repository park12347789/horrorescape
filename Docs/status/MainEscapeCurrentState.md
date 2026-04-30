# MainEscape Current State

Snapshot date: 2026-04-24

This is the current route and ownership starting point for the next worker.
Read this before changing runtime code, authored floor scenes, validation, or
scene routing.

## Build Settings

- Index `0`: `Assets/Scenes/RMainEscape_Lobby.unity`
- Index `1`: `Assets/Scenes/RMainEscape_tuto.unity`
- Index `2`: `Assets/Scenes/RMainScene_5F.unity`
- Index `3`: `Assets/Scenes/RMainScene_4F.unity`
- Index `4`: `Assets/Scenes/RMainScene_3F.unity`
- Index `5`: `Assets/Scenes/RMainScene_2F.unity`
- Index `6`: `Assets/Scenes/RMainScene_1F.unity`
- Index `7`: `Assets/Scenes/RMainEscape_ElevatorTransition.unity`

## Route Ownership

- Public entry scene: `RMainEscape_Lobby`.
- Optional support scene: `RMainEscape_tuto`.
- Gameplay floor route: `RMainScene_5F -> RMainScene_4F -> RMainScene_3F -> RMainScene_2F -> RMainScene_1F`.
- Interstitial support scene: `RMainEscape_ElevatorTransition`.
- Support scenes stay outside `RFloorSceneEntry` floor route arrays.
- `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition` is the route-data
  bridge/first authority. The live authored scene chain is the playable content
  baseline, and `MainEscapeRuntimeSettings` plus Build Settings must stay
  aligned for validation and support code.

## Active Cleanup Baseline

- Preserve direct authored scene and prefab placement.
- The second decoupling round has shifted more floor behavior toward
  scene-local ownership. `MainEscapeFloorAuthoring` should own authoring root
  and marker root names locally instead of pushing ordinary floor authoring
  names back through `MainEscapeRuntimeSettings` or `RSceneCompositionRoot`.
- Treat any `RSceneCompositionRoot` fallback object/root creation as a
  transitional compatibility path. New cleanup should reduce that path after
  authored references are verified, not expand it as the normal repair model.
- Treat `Assets/Resources/Floors/MainEscape/*.prefab` as legacy/migration
  reference, not live play truth. They remain quarantined behind explicit
  `Tools/Main Escape Rebuild/Legacy/* Resources Floor Prefabs` actions.
- Normal reference-cache and placement-migration tools operate on the
  active/open authored floor scenes. They do not open every routed floor scene
  or mutate the legacy `Assets/Resources/Floors/MainEscape/*.prefab` baselines
  unless a `Tools/Main Escape Rebuild/Legacy/* Resources Floor Prefabs` action
  is invoked deliberately.
- Treat `Report Floor Scene Contracts` as the authored-scene contract report.
  It opens the lobby-authored floor route and floor scene hierarchies for a
  read-only report, including manual `RSceneMarkerPlacementManager`
  `SupportPickup` authoring candidates.
- In that report, read support placement readiness from the no-manager/manager
  status line, `status=no-manager`, `inactive`, `missing runtimeRoot`,
  `missing SupportPickup rule/input`, `no support item ids`, and `ready`
  labels, support marker pool `populated=`/`empty=` state, and
  `legacyQuotaBalance=shortage`/`surplus`/`matched`/`empty-no-legacy-quota`
  balance.
- `Validate Floor Marker Counts` has been removed from the editor menu. The
  generated-map quota view is no longer the authored-scene source-of-truth
  validation path.
- 5F fixed starter pickups are authored fixed pickups, not random support
  marker results. The current intended fixed set is 3 glass bottles, 2
  flashlight batteries, and 2 medkits.
- Those seven fixed pickups stay as direct authored `PrototypeInventoryPickup`
  objects under `CoverProps`; they should not be absorbed into
  `InteractiveProps/00_Pickups` or `AuthoringMarkers/ItemPlacementMarkers`.
- 5F fixed starter pickups keep `suppressRuntimeManagedPickupReplacement`
  enabled. That fixed-pickup contract protects them from marker migration,
  cleanup, and runtime disable paths that operate on replaceable support
  pickups.
- Marker-driven random placement should move toward scene-authored placement
  managers, with each domain owning its own data and reports instead of
  growing one oversized floor manager.
- `Pickup_Flashlight.prefab` is a separate flashlight pickup prefab. It is not
  part of the 5F fixed support pickup contract and should not be counted as a
  random support item.
- `RSceneMarkerPlacementManager` support-pickup ownership detection is
  implemented in `RFloorDirector`: an active/enabled manager under
  `MainEscapeFloorAuthoring` with a `SupportPickup` rule, prefab, requested
  count, marker candidates, resolved support item ids, and a serialized
  `runtimeRoot` suppresses only the legacy runtime support-item plan. The
  current authoring target is `InteractiveProps/00_Pickups` for the runtime
  root and `AuthoringMarkers/ItemPlacementMarkers` for the marker root, but
  the serialized `runtimeRoot` reference is still the ownership gate. The key
  placement plan remains active.
- Support-item quota and marker-pool state are runtime support manager
  transition data. They are not evidence that the authored 5F fixed starter
  pickups are missing.
- The 5F support quota ratio is `9/6/3` for battery/glass/medkit with a total
  of `18`; that total is not reduced by the seven fixed starter pickups.
- The live floor support managers read scene-local quota and marker state:
  5F is `5/4/2` after clamping `9/6/3` to the active marker cap of `11`,
  4F is `8/5/6`, 3F is `6/11/8`, 2F is `8/9/1`, and 1F is `3/14/3`.
- Support manager suppression must stay scoped to the legacy support item plan
  only. It must not absorb fixed starter pickups or the separate flashlight
  pickup prefab into the random support layer.
- `RMainScene_1F~5F` now place
  `ScenePlacementManagers/RSceneMarkerPlacementManager_SupportPickups` under
  `MainEscapeFloorAuthoring`; the contract report reads each live floor manager
  as `status=ready`. Do not assume `Assets/Resources/Floors/MainEscape` prefab
  baselines are migrated unless their prefab state is checked directly.

## Next Reads

- `Docs/design/MainEscapeContractRefactorSequence.md`
- `Docs/design/SceneIndirectionContractAudit.md`
- `Docs/RSceneRebuildManifest.md`
- `Docs/design/MainEscapeLiveLoopSystem.md`

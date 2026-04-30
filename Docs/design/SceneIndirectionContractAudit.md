# Scene Indirection Contract Audit

Date: 2026-04-23

Status: audit/design draft only. This document records findings and cleanup
direction; it does not authorize scene, prefab, ProjectSettings, package, build
settings, or runtime rewrites by itself.

## Purpose

This document records which scene objects do not own their behavior directly,
and instead work through an owner, resolver, service, runtime planner, or naming
contract.

The audit question is: when a tile, marker, pickup, enemy, exit, UI element, or
scene object appears in the hierarchy, which system actually decides what it
does at runtime?

Examples:

- A wall tile is not the only source of truth for movement or vision. Runtime
  walkability and line-of-sight queries go through `GridMapService`.
- An item marker does not spawn its pickup by itself. `MainEscapeFloorAuthoring`
  exposes marker pools, and `RFloorItemPlacementRuntime` builds a placement plan.
- A door authoring array can be empty while doors still work through visual or
  tilemap synthesis.

## Scope

Audited scenes:

- `Assets/Scenes/RMainEscape_Lobby.unity`
- `Assets/Scenes/RMainEscape_tuto.unity`
- `Assets/Scenes/RMainEscape_ElevatorTransition.unity`
- `Assets/Scenes/RMainScene_5F.unity`
- `Assets/Scenes/RMainScene_4F.unity`
- `Assets/Scenes/RMainScene_3F.unity`
- `Assets/Scenes/RMainScene_2F.unity`
- `Assets/Scenes/RMainScene_1F.unity`

Audit inputs:

- Scene YAML component inventory from
  `m_EditorClassIdentifier: Assembly-CSharp::...`
- Serialized `MainEscapeFloorAuthoring` marker arrays and quotas on 1F-5F
- Runtime code paths using `Find*`, `Resolve*`, `CacheReferencesFromHierarchy`,
  `GetComponentsInChildren`, and `GridMapService` registration

## Scene Inventory

| Scene | Primary middlemen | Runtime contract |
|---|---|---|
| `RMainEscape_Lobby` | `IRLobbyController`, `RRunSessionController`, `UiSettingsOwner` | Lobby buttons delegate to the session controller. `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition` is the route-data bridge/first authority; the lobby scene's serialized route is compatibility/alignment data. |
| `RMainEscape_tuto` | `RTutorialSceneBootstrap`, `GridMapService`, `RTutorialElevatorExitInteractable` | Tutorial does not use floor composition. The bootstrap reads `RTutorialAuthoring`, `Tiles_ground`, `Tiles_wall`, `Tiles_movenonlywall`, and tutorial markers to build a local tutorial runtime. |
| `RMainEscape_ElevatorTransition` | `RElevatorTransitionController`, `RElevatorTransitionRequestStore` | The transition scene consumes a pending request written by `RSceneRouter.LoadFloorSceneThroughElevatorTransition`; without a request it falls back to the controller's serialized `directPlayFallbackScenePath`, a legacy direct-play compatibility path pinned as migration debt. |
| `RMainScene_5F` | `RSceneCompositionRoot`, `MainEscapeFloorAuthoring`, `RFloorDirector`, `GridMapService`, `RSceneMarkerPlacementManager` | Uses the live floor contract. 5F has no active chaser marker, so chaser randomization falls back to the default chaser cell when the quota asks for one. The support pickup manager is placed under `ScenePlacementManagers` for the random support layer. |
| `RMainScene_4F` | Same live floor middlemen plus `RSceneMarkerPlacementManager` | Adds `ChaserPlacementMarkers`, `PatrolSpawn`, and `MoveOnly` roots. Door behavior depends on visual/tilemap synthesis because `doorAuthorings` slots are empty. The support pickup manager is placed under `ScenePlacementManagers`. |
| `RMainScene_3F` | Same live floor middlemen plus `RSceneMarkerPlacementManager` | Uses movement blockers, chaser markers, patrol spawn, and vent route through root/authoring contracts. The support pickup manager is placed under `ScenePlacementManagers`. |
| `RMainScene_2F` | Same live floor middlemen plus `RSceneMarkerPlacementManager` | Similar to 3F. Direct elevator exit passes through key/progression routing. The support pickup manager is placed under `ScenePlacementManagers`. |
| `RMainScene_1F` | Same live floor middlemen plus final exit and `RSceneMarkerPlacementManager` | 1F has `MainEscapeFinalExitTouchTrigger` and a final exit reference, so it carries the end-of-run exit contract instead of only upper-floor stairs/elevator routing. The support pickup manager is placed under `ScenePlacementManagers`. |

## Live Floor Shared Components

All five floor scenes contain these key contract components:

- `RSceneCompositionRoot`
- `RRunController`
- `RFloorDirector`
- `MainEscapeFloorAuthoring`
- `GridMapService`
- `REncounterSpawner`
- `IRHudCanvas`
- `NoiseSystem`
- `PrototypeAudioManager`
- `FlashlightFogOfWarOverlay`
- `RRunPlayerStateStore`
- `MainEscapeVentRouteAuthoring`
- `MainEscapeSentrySpawnAuthoring`

## Floor Marker Pools

The table below lists the active marker counts from the serialized
`MainEscapeFloorAuthoring` blocks. 5F also contains null slots in some arrays,
which matters because the inspector-visible slot count is not the same as the
usable runtime marker count.

| Floor | Support markers | Key markers | Shared enemy markers | Chaser markers | Danger markers | Support quota | Enemy quota | Trap quota |
|---|---:|---:|---:|---:|---:|---|---|---:|
| 5F | 11 active, 15 serialized slots | 1 | 4 active, 5 serialized slots | 0 active, 1 null slot | 1 | B9/G6/M3 | P0/S5/C1 | 1 |
| 4F | 29 | 1 | 8 | 3 | 14 | B8/G5/M6 | P2/S2/C1 | 2 |
| 3F | 35 | 1 | 12 | 3 | 12 | B6/G11/M8 | P3/S3/C1 | 3 |
| 2F | 27 | 1 | 12 | 3 | 10 | B8/G9/M1 | P3/S3/C1 | 4 |
| 1F | 30 | 1 | 12 | 3 | 12 | B3/G14/M3 | P4/S4/C1 | 5 |

Interpretation:

- Support quota is planner input, not a strict per-item placement guarantee.
- Runtime support placement count clamps to
  `min(active support markers, support quota total)`.
- Runtime trap placement count clamps to `min(danger markers, trap quota)`.
- Shared enemy randomization assigns patrol and sentry quota to the shared enemy
  marker pool.
- Chaser randomization uses chaser markers when available; without markers it
  keeps the existing default chaser spawn cell.
- For 5F, support-item quota and marker-pool state are runtime support manager
  transition data. They are not proof that the authored fixed starter pickups
  are missing.
- For 5F, the support quota ratio is `9/6/3` for battery/glass/medkit with a
  total of `18`, and that total is not reduced by the seven fixed starter
  pickups.
- For 5F, the current runtime random target is the active support marker cap of
  `min(18, 11) = 11`.
- For 5F, the current manager-rule direction is `5 battery / 4 glass / 2
  medkit` after clamping and scaling that ratio to the active marker cap.
- `Pickup_Flashlight.prefab` is a separate flashlight pickup prefab. It is not
  a 5F fixed support pickup target and should not be absorbed into the random
  support layer.

## Indirection Families

### 1. Naming Settings Contract

Primary owner:

- `MainEscapeRuntimeSettings`

Behavior:

- Names such as `RSystems`, `RGameplay`, `RRuntime`, `RAuthoring`,
  `ItemPlacementMarkers`, `KeyPlacementMarkers`, `EnemyPlacementMarkers`,
  `ChaserPlacementMarkers`, `DangerMarkers`, `Doors`, `MovementBlockers`,
  `PlayerStart`, `Tool`, `Transition`, `SafeRoom`, and `VentRoute` are runtime
  lookup contracts.
- `RSceneReferenceLookup` and
  `MainEscapeFloorAuthoring.CacheReferencesFromHierarchy` use these names to
  recover missing references.

Risk:

- Hierarchy names are behavior contracts. Renaming a root can silently change
  what runtime sees.
- Serialized arrays and root scans can disagree. Root scans often win.

### 2. Scene Composition Contract

Primary owner:

- `RSceneCompositionRoot`

Behavior:

- Resolves player, HUD, fog, audio, floor director, run controller, exits, and
  floor authoring for floor scenes.
- Falls back to unique component lookup or name-based lookup when serialized
  references are empty.
- Resolves floor identity through the session route and loaded scene, not only
  through local serialized values.

Risk:

- A feature component can exist in the scene but still fail if composition does
  not bind it.
- Optional fallback warnings can make the true source hard to notice unless the
  log setting is enabled.

### 3. Floor Authoring Contract

Primary owner:

- `MainEscapeFloorAuthoring`

Behavior:

- Aggregates tilemaps, player/tool/transition/safe markers, item/enemy/danger
  pools, patrol/sentry/vent route authoring, door groups, prop blockers, and
  movement blockers.
- `GetSupportItemPlacementMarkers`, `GetKeyPlacementMarkers`,
  `GetEnemyPlacementMarkers`, and `GetDangerPlacementMarkers` prefer root scans
  when roots exist, then fall back to serialized arrays.
- `BuildDoorGroups` falls back to visual/tilemap synthesis when explicit door
  authoring is absent.

Risk:

- Inspector arrays may not be the runtime source if a marker root exists.
- All floor scenes currently have empty `doorAuthorings` slots, but doors still
  work through synthesis. Looking only at door authoring arrays is misleading.

### 4. Grid And Tile Contract

Primary owner:

- `GridMapService`

Behavior:

- Combines ground, wall, door tilemaps and registered runtime cells into
  walkability and vision results.
- `BlockAll` overlay registers prop cells that block movement and vision.
- `MoveOnly` overlay and `MainEscapeMovementBlockerAuthoring` register
  movement-only cells.
- `MainEscapePropBlockerAuthoring`, `CoverPropRuntime`, and visual prop
  footprints only affect navigation when registered into `GridMapService`.

Risk:

- Visible art does not guarantee blocking.
- Move-only blockers affect movement but not vision.
- Wall/prop blockers affect both movement and vision.

### 5. Runtime Floor Build Contract

Primary owners:

- `RFloorDirector`
- `Batch2TestRoomBootstrap.AuthoredFloors`
- `RRuntimeDoorAssembler`

Behavior:

- Converts scene-resident authored floor content into an
  `OfficeFloorBuildResult`.
- The build result becomes the runtime truth for layout, map service, patrol
  route, sentry points, vent route, main door cells, and door groups.
- Door controllers are assembled from `DoorGroups`, `MainDoorCells`, and
  `GridMapService`, not by the visible door objects deciding independently.

Risk:

- Authored visual objects and runtime controller objects are not necessarily
  one-to-one.
- Missing required roots or tilemaps can fail the whole floor build.

### 6. Placement Contract

Primary owners:

- `RFloorItemPlacementRuntime`
- `RFloorTrapPlacementRuntime`
- `RAuthoredFloorEncounterRandomizationPlanner`

Behavior:

- Markers are data sources. Runtime planners choose actual item, trap, and enemy
  placements using marker pools, quotas, run seed, and prefab catalogs.
- Key markers are separate from support item markers.
- Danger markers are the runtime glass trap source.
- Enemy markers are split into shared patrol/sentry and chaser pools.

Risk:

- Placing a marker does not guarantee a spawn unless quota, planner, prefab, and
  walkability all line up.
- Marker shortages clamp or fall back instead of always failing.

### 7. Route And Progression Contract

Primary owners:

- `RRunSessionController`
- `RSceneRouter`
- `RRunRoutingSettings`
- `RRunProgressionRules`
- `MainEscapeRuntimeSettings`

Behavior:

- The current primary route source is the lobby scene's serialized
  `RRunSessionController`.
- `RRunRoutingSettings.asset` and `MainEscapeRuntimeSettings.asset` are
  alignment sources.
- Upper-floor direct elevator exit uses progression/key rules before routing to
  the next floor or the elevator transition scene.
- 1F has a final exit path.

Risk:

- Route meaning is spread across scene-local data, `RRunRoutingSettings`,
  `MainEscapeRuntimeSettings`, and Build Settings.
- Validators check alignment, but edits still need to preserve all four layers.

### 8. UI, Fog, Noise, And Audio Contract

Primary owners:

- `RSceneCompositionRoot.RuntimeBinding`
- `IRHudCanvas` and the `IR*` binder layer
- `FlashlightFogOfWarOverlay`
- `NoiseSystem`
- `PrototypeAudioManager`

Behavior:

- HUD binders receive player runtime and HUD canvas references after scene
  composition.
- Fog consumers receive the visibility service through
  `IFogOfWarOverlayConsumer`.
- Noise and audio act like scene services for multiple systems.

Risk:

- UI or visibility components can exist but still not see runtime state if
  binding fails.
- Some UI and option paths still read `MainEscapeRuntimeSettings` or
  `PlayerPrefs` directly.

## Review Cross-Check Additions

Three independent review passes checked the audit from different angles:
scene YAML, runtime C# middlemen, and Resources/editor/config contracts. These
are the additional contracts that were not explicit enough in the first draft.

### 9. Persistent Session And Options Contract

Primary owner:

- `RRunSessionController`

Behavior:

- The controller is not only route ownership. It is also a persistent global
  run state and option boundary.
- It uses singleton lookup, `DontDestroyOnLoad`, `SceneManager.sceneLoaded`, and
  delayed player runtime rebinding.
- Lobby runtime options are read and written through `PlayerPrefs`.
- `RRunController` and `IRLobbyController` now resolve through
  `RRunSessionResolver`. Runtime creation fallback remains in composition-root
  and direct-play bootstrap paths.

Risk:

- A scene can appear locally valid but still behave differently depending on the
  existing persistent session object.
- Option state can come from `PlayerPrefs`, not only scene or asset data.

### 10. Persistent Audio Contract

Primary owner:

- `PrototypeAudioManager`

Behavior:

- The audio manager is a long-lived singleton service.
- It persists across scenes, subscribes to scene-loaded events, restores volume
  through `PlayerPrefs`, and owns several `Resources` audio clip paths.

Risk:

- Scene audio behavior depends on the persistent audio manager already existing
  or being created.
- Some audio components also fall back to the first discoverable
  `WasdPlayerController`.

### 11. Interaction Registry Contract

Primary owners:

- `PlayerInteractable2D`
- `PlayerInteractionDriver`

Behavior:

- Interactables register themselves into a static active list on enable.
- The player does not query each door/pickup directly. `PlayerInteractionDriver`
  scans `PlayerInteractable2D.Active`, selects the nearest valid candidate, then
  applies line-of-sight checks.
- The default interaction blocking mask is `GameLayers.VisionBlockingMask`.
- Individual interactables can override line-of-sight blocking by implementing
  `AllowsLineOfSightBlocker`.

Risk:

- A placed interactable must be enabled and active in hierarchy to enter the
  static registry.
- Interaction can fail even when distance is valid if the vision-blocking raycast
  is blocked.
- Prompt position and text come from the shared driver, not each interactable's
  renderer alone.

### 12. Layer Name Contract

Primary owner:

- `GameLayers`
- `ProjectSettings/TagManager.asset`

Behavior:

- `Ground`, `Wall`, `Door`, and `Prop` layer names are hard runtime contracts.
- `GameLayers.VisionBlockingMask` is `Wall + Door`.
- Tutorial authoring, floor tile authoring, door assembly, fog, interaction, and
  blocker registration rely on those names resolving to valid layer indices.

Risk:

- If a layer name is removed or renamed, `GameLayers.ResolveLayer` falls back to
  layer `0`, which can make scenes look normal while collisions or visibility
  use the wrong layer.
- `Prop` is commonly movement-only unless registered into a prop-blocking path.

### 13. Runtime Resource Path Contract

Primary owners:

- `MainEscapeRuntimeSettings`
- `RRunCanonicalAssetLocator`
- `MainEscapeRuntimePrefabCatalogOverrideResolver`
- `MainEscapeTileAssetCatalog`
- `MainEscapeDoorRuntimeUtility`

Behavior:

- Canonical run assets load from fixed `Resources/MainEscape/Run/...` paths.
- Runtime settings load from `Resources/MainEscape/MainEscapeRuntimeSettings`.
- The default prefab catalog loads from
  `Resources/MainEscape/MainEscapeRuntimePrefabCatalog`.
- A scene-specific prefab catalog may load from
  `Resources/MainEscape/PrefabCatalogOverrides/<scene>/MainEscapeRuntimePrefabCatalog`.
- Canonical lobby and floor scenes are protected from prefab catalog overrides,
  but support scenes such as tutorial/elevator can still use that override path.
- Tile and door prefab utilities also use fixed `Resources` paths.

Risk:

- The hierarchy can be correct while a missing or scene-overridden catalog changes
  spawned prefabs.
- Moving assets inside `Resources` can break runtime without changing scene YAML.
- The current repository has no files under
  `Assets/Resources/MainEscape/PrefabCatalogOverrides`, but the path contract is
  active.

### 14. Direct Scene-Authored Pickup Contract

Primary scene:

- `RMainScene_5F`
- `RMainEscape_tuto`

Behavior:

- 5F has direct `PrototypeInventoryPickup` objects that opt out of runtime
  replacement with `suppressRuntimeManagedPickupReplacement`.
- Those pickups can remain visible in authored mode, so 5F loot is not purely
  marker-driven.
- The tutorial scene has direct objective pickup components such as
  `PickupFlashlightDiscoveryController` and `MainEscapeKeyPickup`.

Risk:

- Counting only marker-driven runtime pickups misses authored pickups.
- Cleanup must not delete direct pickup objects just because runtime placement
  markers exist.

### 15. Door Visual Variant Contract

Primary owner:

- `MainEscapeDoorVisualVariantOverride`
- door visual synthesis/runtime utility

Behavior:

- Door appearance is not fully owned by the visible door object.
- Floor scenes carry `MainEscapeDoorVisualVariantOverride` components that pin
  variant selection for synthesized/runtime doors.

Risk:

- Door visuals can change if the override components are removed, even if door
  cells and door groups remain valid.
- Door behavior and door appearance have separate contracts.

### 16. Authoring Clipboard And Sparse Root Contract

Observed roots:

- `VexedDecorClipboard`
- `PrefabClipboard`
- `HospitalVexed1Clipboard`
- `HospitalVexed2Clipboard`
- `HospitalTileA5Clipboard`
- `HospitalTileA4Clipboard`
- `HospitalTileCClipboard`
- `BloodGoreClipboard`
- `ClinicRoomClipboard`
- `SurgeryZombieClipboard`
- `MainEscapeWallClipboard`
- `TileClipboard`

Behavior:

- Some live scenes contain staging/clipboard roots that are not primary gameplay
  owners.
- `coverPropsRoot` is explicitly null in live floor authoring blocks, so cover
  prop behavior is being resolved through runtime registration and visual
  synthesis paths rather than a serialized root list.

Risk:

- Staging roots can be mistaken for gameplay owners.
- A cleanup pass must distinguish inactive authoring/staging containers from
  runtime blocker sources.

### 17. Final Exit Shape Contract

Primary scene:

- `RMainScene_1F`

Behavior:

- 1F endgame flow is split between `FloorEscapeTransitionPoint` and
  `MainEscapeFinalExitTouchTrigger`.
- The final exit trigger carries a direct `RRunController` reference, which is a
  different shape from upper-floor elevator/stairs interaction.

Risk:

- Treating every floor exit as the same interaction contract can miss the 1F
  trigger path.

### 18. Lobby UI Binding Contract

Primary owner:

- `IRLobbyController`
- `MainEscapeRuntimeValidator`

Behavior:

- The lobby validator requires specific serialized UI fields: session controller,
  start/options/credits/quit buttons, summary texts, footer hint, modal backdrop,
  options and credits panels, close buttons, and persistent button bindings.
- It also expects the EventSystem first-selected object to be the start button.

Risk:

- The lobby can have a canvas and buttons but still fail the hidden validator
  contract.

### 19. Editor Tool And Recovery Menu Contract

Primary owners:

- editor menu items
- `SceneRecoveryIntegrityRunner`
- migration/cache tools

Behavior:

- Recovery tooling calls exact editor menu names such as
  `Tools/Main Escape Rebuild/Cache Authored Floor References`,
  `Tools/Main Escape/Validate Lobby Scene Preflight`, and
  `Tools/Main Escape/Validate Start Floor Runtime`.
- Normal marker/cache tooling operates on open authored floor scenes. Only
  `Tools/Main Escape Rebuild/Legacy/* Resources Floor Prefabs` actions still
  hardcode `Assets/Resources/Floors/MainEscape/1F.prefab` through `5F.prefab`.
- `MainEscapeRuntimeSettings` also contains editor authoring names such as
  `MainEscapeEditingWorkspace`, `EditableFloor_MainScene_Play`,
  `AuthoringMarkers`, `SentryGuards`, `GroundCells`, `WallCells`, `DoorCells`,
  and template/preset library names used by tile authoring setup.

Risk:

- Renaming menu items can break recovery automation without compiler errors.
- Moving legacy floor prefabs or authoring workspace roots can break editor
  repair/migration tools even if live scenes still run.

### 20. Route Documentation And Tutorial Alignment Contract

Primary owners:

- `ProjectSettings/EditorBuildSettings.asset`
- `Docs/README.md`
- `RRunRoutingSettings`
- `RRunSessionController`
- `MainEscapeRuntimeValidator`

Behavior:

- The checked-in Build Settings include `RMainEscape_tuto` before 5F and append
  `RMainEscape_ElevatorTransition` after 1F.
- Older docs previously described a build index layout where 5F was index `1`
  and the elevator scene was index `6`.
- The `RRunRoutingSettings` class exposes a default `tutorialScenePath`, but
  the checked-in `RRunRoutingSettings.asset` YAML currently serializes lobby,
  elevator transition, starting floor, and floor scene entries only.
- `RRunSessionController` also serializes scene-local tutorial routing. The
  route asset now acts as a support fallback for blank tutorial/elevator paths,
  not as the primary runtime route switch.
- The current runtime-settings alignment validator focuses on lobby/start/floor
  route alignment and does not appear to validate the tutorial path with the same
  strictness.

Risk:

- Developers relying on docs can load the wrong build index.
- Tutorial route drift can slip through route alignment checks.

### 21. Shadow Startle Presentation Contract

Primary owner:

- `RShadowStartleCue`
- `RShadowStartleDirector`

Behavior:

- Shadow startle cues can manufacture missing presentation binding at runtime.
- They can add or resolve `EnemyPrefabBindings` and load a default shadow enemy
  sprite profile from `Resources`.
- The current floor scenes show no serialized shadow startle markers in the
  audited `MainEscapeFloorAuthoring` blocks, but the runtime contract exists.

Risk:

- A cue can work even when prefab binding was not fully authored, which hides
  presentation setup problems until the fallback changes.

### 22. First-Available Player Discovery Contract

Primary users:

- local audio/perception helpers
- noise panel helpers

Behavior:

- Player-dependent components now prefer explicit references or scene-local
  `RSceneReferenceLookup` resolution instead of broad player searches.
- The remaining risk is stale/missing scene-local bindings, not active-scene
  global search.

Risk:

- Additive scenes, tutorial/runtime overlap, or duplicate player objects can
  bind these helpers to the wrong player.

## Expanded Parallel Sweep Findings

After the first pass, additional review passes were assigned to narrower
areas: editor tooling, UI, physics, rendering, AI, input, persistence,
ScriptableObjects, ProjectSettings, legacy prefabs, and compile/test structure.
The findings below are additive; they are not implementation approval.

### 23. Active Editor State Contract

Primary source:

- Unity active editor state

Observed state:

- The open active scene contained `RTutorialSceneBootstrap`,
  `RTutorialAuthoring`, `RMainCamera`, and a scene-owned `Player`.
- The active scene did not contain `RSceneCompositionRoot`, `GridMapService`,
  or `IRHudCanvas`.
- The active `RTutorialSceneBootstrap` had `disableFloorRuntimeSystems`,
  `resetPlayerInventoryOnStart`, `forceNoFlashlight`, and
  `spawnTutorialSentryOnStart` enabled.
- The active editor console warned that
  `EditableFloor_MainScene_Play` is missing an explicit stalker spawn marker.

Risk:

- The tutorial scene is an intentional alternate ownership mode. It should not
  be judged by the same component presence rules as live floor scenes.
- Editor warnings can reveal authoring workspaces that are not part of the
  shipped scene loop but still affect tooling decisions.

### 24. Editor Rebuild And Mutation Contract

Primary owners:

- `SceneRecoveryIntegrityRunner`
- `RMainEscapeLobbySceneRebuilder`
- `RElevatorTransitionSceneSetup`
- `NeoDunggeunmoProFontSetup`
- marker, danger, difficulty, vent, and tutorial authoring tools

Behavior:

- Recovery automation invokes exact editor menu names and editor validation
  state.
- Lobby and elevator setup tools can recreate scenes from fixed art,
  `Resources`, and hierarchy-name assumptions.
- Font setup can run on editor load and rewrite TMP defaults, lobby scene text,
  and HUD prefab text.
- Several authoring tools can seed, normalize, delete, or regenerate floor
  markers and scene content from hardcoded scene and prefab path lists.

Risk:

- These are not passive helpers. They are editor-time ownership paths.
- A scene can be valid by hand but drift after a rebuild tool is rerun.
- Renaming menu items, roots, or command files can break recovery workflows
  without a compile error.

### 25. UI And HUD Shape Contract

Primary owners:

- `UiSettingsOwner`
- `IRHudCanvas`
- `IRPixelHudArt`
- `IRLobbyController`
- `IRAnalogNoiseUiTheme`
- `IRGameClearPanelView`
- `RTutorialSceneBootstrap`

Behavior:

- `UiSettingsOwner` can fall back to `MainEscapeRuntimeSettings` when no local
  UI settings owner exists.
- `IRHudCanvas` requires `Canvas`, `CanvasScaler`, `GraphicRaycaster`,
  `panelRoot`, and the panel references to be valid.
- Pixel-authored HUD layouts can override runtime sizing for inventory, quick
  slots, and health panels.
- The threat HUD expects a `CanvasGroup` plus four renderable edge images.
- Lobby options and credits UI are name-bound. Analog-noise theming can reparent
  summary text and CTA buttons at runtime.
- The game-clear modal can partially recover by finding a child button and the
  `Panel/ElapsedTimeText` path.
- Tutorial bootstrap disables live floor UI/runtime systems and fabricates or
  resolves its own tutorial camera, light, HUD, and runtime pieces.

Risk:

- A canvas can exist and still fail the real HUD contract.
- Authored UI hierarchy is not always the final runtime hierarchy.
- Runtime settings can change HUD behavior without changing a scene.

### 26. Grid, Fog, Vision, And Noise Contract

Primary owners:

- `GridMapService`
- `FlashlightFogOfWarOverlay`
- `VisionSensor2D`
- `FogReactiveEnemyVisibility`
- `NoiseEmitter`
- `NoiseSystem`
- `ObjectiveLoopDemo.DoorController`

Behavior:

- `GridMapService` has permissive missing-data behavior:
  `WorldToCell` can return zero, nearest-walkable resolution can give up, and
  line of sight can return visible when no ground tilemap exists.
- Door open/close depends on cached door cells and toggles paired shadow
  blockers.
- Fog overlay bypass reports everything as visible and disables its renderer.
- Fog line-of-sight prefers `GridMapService` only for compatible masks,
  otherwise it falls back to `Physics2D.Raycast`.
- Enemy vision runs range and cone checks before raycasts; co-located targets
  are detected immediately.
- Enemy visibility can be forced by any visible sampled sprite point, nearby
  point light, or threat silhouette/cone state.
- Footstep-driven noise suppresses velocity-driven noise for a short window.
- `NoiseSystem` is a singleton bus with a short-lived recent-event buffer.
- Demo door controllers maintain a static cell registry and reconcile their
  state through `GridMapService`.

Risk:

- Visibility, walkability, and door state are service results, not visual truth.
- Fallbacks can make missing floor data look playable until a stricter consumer
  reads the same scene.
- Noise and door behavior can be global even when the visible object looks local.

### 27. Settings, Catalog, And Asset Path Contract

Primary owners:

- `MainEscapeRuntimeSettings`
- `RRunRoutingSettings`
- `RRunPlayerDefaults`
- `RRunProgressionRules`
- `MainEscapeRuntimePrefabCatalog`
- `MainEscapeFloorTypes`
- tile, door, audio, player-art, and enemy-art loaders

Behavior:

- `MainEscapeRuntimeSettings.asset` is alignment/support data for hierarchy
  names, marker names, HUD sizing, analog-noise toggle, validation paths, and
  fallback floor routes. Route-data authority starts at
  `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`.
- `GetStartFloorScenePath` falls back to the canonical 5F scene path from
  `MainEscapeSceneIdentityUtility` when route data is missing.
- `RRunRoutingSettings.asset` carries the default chapter plus lobby, tutorial,
  elevator, and start-floor support defaults around the route graph; it is
  route configuration/support data, not an independent second route authority.
- `RRunPlayerDefaults.asset` and `RRunProgressionRules.asset` own default
  health, flashlight, starting inventory, key-gated direct exits, and UI text.
- `MainEscapeRuntimePrefabCatalog.asset` pins runtime player, enemy, pickup,
  and trap prefabs by serialized references.
- Non-protected support scenes can probe
  `Resources/MainEscape/PrefabCatalogOverrides/<scene>/...`.
- Floor prefab fallback paths are `Floors/MainEscape/{FloorNumber}F`.
- Tile assets load from `Tiles/MainEscape/MainEscape{Ground,Wall,Door}Tile`.
- Door prefabs and sprites load from exact `Resources/MainEscape/Door...`
  stems.
- Audio, player sprite profiles, enemy sprite profiles, and fog shader fallback
  paths are also runtime contracts.
- Ground enemy art can switch by `gameObject.name.Contains("Stalker")` when
  auto-select profile is enabled.

Risk:

- Moving or renaming assets in `Resources` can change runtime behavior without
  changing any scene YAML.
- Settings ownership is split across multiple assets that must stay aligned.
- Art-profile array order affects runtime pose fallback, not only visuals.

### 28. Scene Shape And Support Scene Contract

Primary owners:

- live floor scene roots
- tutorial scene authoring
- elevator transition scene authoring

Behavior:

- `GameplayOverlay` appears as a common live-floor root alongside `RGameplay`
  and `RRuntime`.
- Most scenes use `RMainCamera`, but the elevator transition scene uses plain
  `Main Camera`.
- Tutorial scene contains its own scene-authored `Player`, `RMainCamera`, and
  sentry-spawn bootstrap settings.
- Elevator transition scene owns its own presentation stack:
  `RElevatorTransitionController`, `ElevatorTransition_Inside_Player`,
  `ElevatorTransitionCanvas`, and `ElevatorTransition_TempLightOverlay`.

Risk:

- Support scenes are not small variants of live floor scenes. They have their
  own authored presentation and ownership contracts.
- Name fallback code needs to account for camera-name asymmetry.

### 29. Project Settings And Input Contract

Primary owners:

- `ProjectSettings/TagManager.asset`
- `ProjectSettings/EditorBuildSettings.asset`
- `Assets/InputSystem_Actions.inputactions`
- `WasdPlayerController`
- URP settings and packages

Behavior:

- Custom layers are `Ground`, `Wall`, `Door`, and `Prop`; no project-specific
  tags are defined, and only the default sorting layer exists.
- Enabled build scene order is:
  lobby, tutorial, 5F, 4F, 3F, 2F, 1F, elevator transition.
- The project template default scene is the lobby.
- The `Player` action map and action names such as `Move`, `Look`, `Sprint`,
  `Interact`, `Jump`, `Previous`, `Next`, and `ToggleFlashlight` are hardcoded
  by player input code.
- The `UI` action map exists, but some modal flows still use raw keyboard keys.
- Runtime-facing package contracts include Input System, URP, and UGUI package
  versions.

Risk:

- Renaming input maps or actions can produce quiet no-op input paths.
- UI submit/cancel support can look configured in the Input System while some
  modal flows remain keyboard-only.
- Layer names matter more to raycasts and conventions than to tags.

### 30. Persistence And Handoff Contract

Primary owners:

- `RRunSessionController`
- `RElevatorTransitionRequestStore`
- `IRLobbyController`
- editor validators and recovery tools

Behavior:

- Lobby options persist through `PlayerPrefs` keys:
  `IRLobby.MasterVolume`, `IRLobby.SfxVolume`, `IRLobby.AmbienceVolume`,
  `IRLobby.TargetFrameRate`, and `IRLobby.VSyncEnabled`.
- `PrototypeAudioManager` still honors a legacy ambience key before the first
  new write.
- Live run state is a serialized scene/persistent object snapshot, not a
  file-backed save slot.
- Floor clear handoff captures player state, unbinds runtime, records clear
  state, loads the next scene, and restores state on the next frame.
- Elevator transition uses a one-shot in-memory request store and falls back to
  session snapshot/current floor if the request is missing.
- Validators and recovery tools use `SessionState`, `EditorPrefs`, and temp
  files such as `Temp/RMainEscapeLoopSmokeReport.txt` and
  `Temp/LobbyRebuildCommand.txt`.

Risk:

- Runtime result can depend on persistent objects and prefs left over from an
  earlier scene.
- Editor validation and rebuild queues can survive in editor state even when no
  scene object shows them.

### 31. Lifecycle, Reflection, And Edit-Time Mutation Contract

Primary owners:

- `RRunSessionController`
- `MainEscapeRuntimeValidator`
- `MainEscapeMovementBlockerAuthoring`
- `MainEscapeSolidBlockerAuthoring`

Behavior:

- `RRunSessionController` subscribes to `SceneManager.sceneLoaded` in `Awake`
  and handles all already-loaded scenes at startup via `SceneManager.GetSceneAt(index)`.
- `Reset` and `OnValidate` can auto-fill missing route assets from `Resources`.
- The runtime validator uses reflection against hardcoded private field names on
  `RSceneCompositionRoot`.
- `ExecuteAlways` blocker authoring components rewrite footprint, colliders,
  layers, and generated children from `OnValidate` and `OnEnable`.

Risk:

- Touching a component in the inspector can rebind it to global assets.
- Renaming private serialized fields can weaken validation coverage.
- Edit-time authoring components can mutate scene YAML during normal editing.

### 32. Validation And Documentation Drift Contract

Primary owners:

- `MainEscapeRuntimeValidator`
- `RMainEscapeLoopSmokeBridge`
- `Docs/README.md`
- `Docs/RSceneRebuildManifest.md`

Behavior:

- Older docs previously described a build order without tutorial at index 1 and
  elevator at the final enabled slot.
- Smoke bridge refuses to run when loaded scenes are dirty.
- Some validators reopen scenes from disk, so unsaved editor changes are not
  validated.
- Marker count validation skips 5F even though authoring docs describe 5F as
  the source floor for structural changes.
- Route validation covers the main floor route more strictly than the tutorial
  route.
- Duplicate-runtime-item and key-interaction checks can be skipped when marker
  roots exist.

Risk:

- A manual checker can follow stale or historical docs and inspect the wrong
  build index if current status docs are not kept aligned.
- A dirty open scene can look validated while only the saved version was checked.
- Marker-root presence can hide duplicate pickup or key interaction drift.

### 33. AI, Chaser, And Vent Contract

Primary owners:

- `RFloorDirector`
- `MainEscapeFloorAuthoring`
- `MainEscapeEncounterSpawner`
- `REncounterSpawner`
- `MainEscapeVentRouteAuthoring`
- `BaseOfficeVentEnemyBootstrap`

Behavior:

- Shared enemy marker pools are all-or-nothing for patrol/sentry resolution; a
  small shared pool can prevent fallback to patrol or sentry candidate roots.
- Patrol quota is not a hard count. Patrol route cells can still produce patrol
  enemies when explicit patrol cells are missing.
- Chaser/stalker ownership is split: active spawn resolution prefers
  `chaserPlacementMarkers`, while some quota capture paths still look at legacy
  `stalkerSpawnMarker`.
- Live scenes can have `stalkerSpawnMarker` null while chaser marker roots are
  populated.
- Vent routes require explicit connections or specific naming heuristics such
  as `Corridor_`, `Upper_`, and `Lower_`; generated fallback is suppressed for
  authored-scene routes.

Risk:

- Enemy counts are planner outcomes, not the serialized quota alone.
- Recapturing or migrating authoring data can erase chaser quota if the legacy
  stalker marker is null.
- A visible `VentRoute` root can still fail to create a usable vent graph.

### 34. Direct Pickup And Runtime Item Plan Contract

Primary owners:

- `RFloorDirector`
- `RFloorItemPlacementRuntime`
- `RSceneMarkerPlacementManager`
- `RRunFloorStateApplier`
- `RPlacementMarkerMigrationTools`
- direct `PrototypeInventoryPickup` objects
- `MainEscapeRuntimeValidator`

Behavior:

- `RFloorDirector` applies the marker-driven item plan.
- Support item manager ownership is code-ready. The live `RMainScene_1F~5F`
  scenes have support-pickup managers placed; prefab baselines and non-support
  domains are separate follow-up work.
- Support item manager ownership requires an active and enabled
  `RSceneMarkerPlacementManager`, an eligible `SupportPickup` rule, a valid
  prefab, a positive count, at least one marker candidate, resolved support item
  ids, and a serialized `runtimeRoot`.
- The current authoring target is `InteractiveProps/00_Pickups` for
  `runtimeRoot` and `AuthoringMarkers/ItemPlacementMarkers` for the marker
  source. The manager's serialized `runtimeRoot` reference is the
  apply/readiness reference.
- When support manager ownership exists, the runtime only suppresses or filters
  the legacy support item plan. Key placement remains separate and should still
  be applied.
- Support manager suppression is scoped to the legacy support item plan only.
  It must not absorb fixed starter pickups or the separate flashlight pickup
  prefab into the random support layer.
- Direct legacy pickups are only disabled when they do not suppress runtime
  replacement.
- 5F contains direct battery, glass bottle, and medkit pickups that suppress
  runtime-managed replacement.
- Those 5F authored fixed starter pickups are direct authored
  `PrototypeInventoryPickup` objects with `suppressRuntimeManagedPickupReplacement=true`.
  They are the 3 glass bottles, 2 flashlight batteries, and 2 medkits set and
  must not be reclassified as random support items.
- `Report Floor Scene Contracts` reports support manager candidates, prefab
  catalog availability, fixed pickup state, and manager eligibility as
  read-only observations. The support section should explicitly call out
  `status=no-manager`, `inactive`, `missing runtimeRoot`,
  `missing SupportPickup rule/input`, `no support item ids`, and `ready`
  labels, plus support marker pool `populated=`/`empty=` state and
  `legacyQuotaBalance=shortage`/`surplus`/`matched`/
  `empty-no-legacy-quota` balance.
- Migration tools and the run floor state applier protect fixed pickups that
  suppress runtime-managed pickup replacement.
- Older validator duplicate checks could skip runtime battery duplication when
  support markers existed, and could skip key interaction validation when key
  markers existed.

Risk:

- A floor can contain both direct authored support pickups and runtime-spawned
  support pickups.
- Scene migration can be misread as complete if only the code-ready support
  manager gate is checked.
- Support manager suppression must remain scoped to legacy support item plans;
  if it also filters key placement, key progression can regress.

### 35. Rendering, Camera, And Fog Layer Contract

Primary owners:

- `RMainEscapeLobbySceneRebuilder`
- `FlashlightFogOfWarOverlay`
- floor cameras
- URP volume/profile assets

Behavior:

- Checked-in lobby scene uses `IRLobbyCanvas` as screen-space overlay, while the
  lobby rebuilder creates it as screen-space camera bound to `RMainCamera`.
- Fog overlay renderer uses sorting order `90`.
- Authored floor sprites already exist at higher sorting orders such as `140`,
  so some objects render above the fog overlay by contract.
- Floor cameras carry volume layer/profile data, but post-processing is disabled
  on the gameplay cameras.

Risk:

- Rebuilding the lobby can change the UI render path.
- Fog is not a guaranteed topmost visual mask.
- Volume assets can exist and be configured while remaining inert at runtime.

### 36. Physics And Trigger Contract

Primary owners:

- `NoiseFloorPanel`
- enemy prefabs
- `Physics2DSettings`
- `GameLayers`

Behavior:

- `NoiseFloorPanel` expects trigger callbacks and resolves enemy controllers
  from entering colliders.
- Enemy prefabs appear to rely on trigger colliders without an obvious root
  `Rigidbody2D`, so enemy-trigger behavior depends on where rigidbody ownership
  is actually introduced.
- The 2D layer collision matrix is effectively open; layer names do not provide
  physical collision separation.

Risk:

- Trap trigger behavior can silently fail for enemies if the required 2D physics
  body is absent.
- `Wall`, `Door`, and `Prop` layers are more important to raycasts and map
  contracts than to physical filtering.

### 37. Live Scene Versus Legacy Floor Prefab Contract

Primary sources:

- `Assets/Scenes/RMainScene_1F.unity` through `RMainScene_5F.unity`
- `Assets/Resources/Floors/MainEscape/1F.prefab` through `5F.prefab`

Behavior:

- All five live floor scenes have drifted from the legacy `Resources` floor
  prefabs.
- Live scenes carry much larger danger/support/enemy/chaser arrays, scene-only
  sentry and patrol roots, cleared `doorMarkersRoot`, and 18 null
  `doorAuthorings` slots.
- Legacy prefabs still carry smaller marker arrays, 15 populated door authoring
  references, and older quota shapes.
- 5F is especially divergent: the live scene has a null chaser slot and no
  `ChaserPlacementMarkers` root, while the legacy prefab still has one.

Risk:

- Legacy floor prefabs must not be treated as the current live authoring source.
- Tools that still load `Resources/Floors/MainEscape/*.prefab` can report or
  restore stale marker, quota, and door contracts.

### 38. Compile And Test Assembly Contract

Primary owners:

- test asmdefs
- legacy compatibility stubs
- CardioSim asmdefs
- project scripting define settings

Behavior:

- `Assets/Scripts/Perception/FlashlightFogOfWarOverlay.cs` is a path-level
  compatibility stub; the real implementation lives under `Objectives`.
- EditMode and PlayMode test asmdefs reference only `Assembly-CSharp`.
- CardioSim editor asmdef depends on its runtime asmdef by GUID.
- There are no project-wide custom scripting define symbols.

Risk:

- Moving runtime code into named assemblies can silently remove it from current
  tests unless test asmdefs are updated.
- Deleting compatibility stubs can break older generated references even when
  the real type still exists.

## Cleanup Design

The safest cleanup starts with visibility, not behavior changes.

### Phase 0 - Contract Manifest And Scene Report

Goal:

- Make the current contracts visible without changing runtime behavior.
- Replace generation-style marker validation with a report of what the authored
  scenes actually contain.

Suggested work:

- Add a `SceneContractManifest` document or editor report.
- For every live floor, report:
  - route source and expected floor number
  - required roots
  - ground, wall, door, and overlay tilemap sources
  - marker root scan counts vs serialized array counts as observations
  - authored direct pickup count and item ids
  - runtime-managed support/key/enemy/trap layer, if still enabled
  - scene marker placement manager candidates and rules, including
    `SupportPickup` eligibility
  - manager-level labels such as `status=no-manager`, inactive, missing
    `runtimeRoot`, missing `SupportPickup` rule/input, no support item ids, or
    ready
  - support marker pool `populated=`/`empty=` state and
    `legacyQuotaBalance=shortage`/`surplus`/`matched`/
    `empty-no-legacy-quota`
  - prefab catalog availability for support manager rules
  - fixed pickup state, including suppression of runtime-managed replacement
  - effective support, key, enemy, chaser, and danger ownership
  - `doorAuthorings` count vs visual synthesis door count
  - prop and movement blocker sources
  - exit contract: stairs, elevator, or final exit
  - direct pickups vs runtime-managed pickup plan
  - chaser/stalker source: chaser markers, legacy stalker marker, or fallback
  - patrol/sentry source: shared enemy pool, candidate roots, or route fallback
  - vent graph source and whether explicit connections are usable
  - fog overlay sorting order vs highest authored world sprite order
  - legacy `Resources/Floors/MainEscape/*.prefab` drift summary
- For support scenes, report:
  - tutorial ownership mode and disabled live-floor systems
  - elevator transition pending request source and fallback route
  - scene-authored presentation objects and camera naming
- For project-level state, report:
  - active Build Settings order
  - layer, input-action, and package version contracts
  - `PlayerPrefs`, `SessionState`, `EditorPrefs`, and temp-file keys
- Keep `Validate Floor Marker Counts` retired from the editor menu. Generated
  quota assumptions should not return as the authored scene rule authority.
- Marker-driven random generation should move to scene-authored
  `RSceneMarkerPlacementManager` declarations so each scene states which prefab
  is placed on which marker pool and in what count.
- Do not replace one hidden global rule with one oversized floor manager.
  Each feature should own the scene data it needs through its own component,
  snapshot section, or child/domain manager. High-level directors should
  coordinate lifecycle and ordering, while support pickups, keys, traps,
  enemies, exits, fog, UI, and similar domains keep their own reporting and
  placement logic.

### Phase 0.5 - Contract Source Classification

Goal:

- Stop mixing live authoring, legacy authoring, runtime fallback, and editor
  rebuild behavior in the same mental bucket.

Suggested source labels:

- `LiveScene`: current shipped scene YAML.
- `LegacyResourcePrefab`: `Assets/Resources/Floors/MainEscape/*.prefab`.
- `RuntimeAsset`: `MainEscapeRuntimeSettings`, routing, progression, catalog,
  art, tile, audio, and shader assets.
- `RuntimeService`: scene services such as map, noise, audio, session, fog, and
  interaction registries.
- `EditorMutation`: rebuilders, migration tools, recovery tools, and
  `ExecuteAlways` authoring components.
- `PersistentState`: `PlayerPrefs`, `SessionState`, `EditorPrefs`, temp files,
  and one-shot request stores.

Rule:

- A cleanup or validator should always state which source label it is reading
  and whether that source is authoritative for play, authoring, or recovery.

### Phase 1 - Name The Resolver Boundary

Goal:

- Continue consolidating scene-local and name-based lookup behind
  `RSceneReferenceLookup` or a future `RSceneContractResolver`; broad runtime
  `Find*` is no longer the normal session/player/noise path.

Suggested work:

- Design a small `RSceneContractResolver` facade.
- Keep using `MainEscapeRuntimeSettings` names, but make every name-based lookup
  go through one place.
- First candidates are `RSceneCompositionRoot` and `MainEscapeFloorAuthoring`.

Note:

- This affects multiple systems, so implementation should be preceded by a
  design doc update or ADR.

### Phase 2 - Consider FloorAuthoringSnapshot

Goal:

- Let runtime planners consume a stable snapshot instead of repeatedly reading
  live hierarchy and roots.

Suggested work:

- `MainEscapeFloorAuthoring` reads the scene and creates a
  `FloorAuthoringSnapshot`.
- Item, trap, and encounter planners consume snapshot collections.
- The snapshot records whether data came from root scan, serialized array, or
  fallback.

Expected benefit:

- Inspector-visible arrays and runtime source become easier to compare.
- Marker, tile, door, and exit ownership becomes easier to validate.

### Phase 3 - Sort Fallback Policy

Goal:

- Separate allowed fallbacks from critical fallbacks.

Suggested policy:

- Allowed: optional visual/debug references and warning-only compatibility paths.
- Restricted: floor route, ground/wall tilemaps, player start, transition, and
  key progression source.
- Avoid: generated fallback paths that hide critical live-floor authoring drift.

Validation:

- Fallback use should report as "warning with source", not a quiet pass.

### Phase 4 - Move Authoring Rules To User-Facing Docs

Goal:

- Designers should be able to see which owner makes tiles, markers, exits,
  items, enemies, UI, fog, and blockers work.

Suggested docs:

- `Docs/MainEscapeAuthoringGuide.md`: placement rules, roots, names, marker
  setup, and common mistakes.
- `Docs/design/MainEscapeLiveLoopSystem.md`: live loop ownership summary.
- This document: contract audit and cleanup design source.

## Do Not Apply Yet

Do not start with:

- automatic scene hierarchy rewrites
- `ProjectSettings/EditorBuildSettings.asset` changes
- `Resources` to Addressables migration
- replacing `MainEscapeFloorAuthoring`
- changing `GridMapService` walkability or vision rules
- removing door visual synthesis
- merging tutorial bootstrap into floor composition

## Open Questions

- Which runtime source should be visible first in the inspector or report:
  marker pools, blocker registration, door groups, exit routing, or UI/fog
  binding?
- Is the first cleanup goal documentation/validation, or runtime dependency
  injection cleanup?
- Is 5F's chaser quota of 1 with 0 active chaser markers intentional default
  fallback, or a missing marker?
- Should empty `doorAuthorings` plus visual synthesis be promoted to an explicit
  official door contract?

## Conclusion

The current structure is not just a bug pile. It is a multi-layer indirection
contract that preserves authored scene placement while the live loop runs.

The main problem is visibility: ownership is split across scene names,
serialized references, root scans, runtime services, and planners.

The safest next step is a contract report. After that, name the lookup boundary
around `RSceneCompositionRoot` and `MainEscapeFloorAuthoring`, then consider a
snapshot so runtime planners stop reading live hierarchy directly.

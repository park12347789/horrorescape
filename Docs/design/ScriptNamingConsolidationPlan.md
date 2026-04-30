# Script Naming Consolidation Plan

## Goal

Reduce script-name confusion before the next chapter/map expansion without
breaking authored Unity references.

This is a consolidation plan, not an immediate mass rename. Many `MonoBehaviour`
and `ScriptableObject` names are serialized into scenes, prefabs, and assets, so
renaming must happen in small validated passes.

## Current Prefix Inventory

There are currently 282 C# scripts under `Assets/Scripts`. A separate
scene/prefab/asset scan found 84 script `.meta` GUIDs referenced from Unity YAML,
so roughly one third of the script set has serialized reference exposure.

Runtime scripts under `Assets/Scripts` currently use these broad prefixes. Counts
can vary slightly by scan method because some files contain nested classes or
interface-like `I*` names:

| Prefix | Count | Current Meaning |
|---|---:|---|
| `R*` | 65 | Remake systems, including current live-loop route/session/floor composition and runtime presenters. |
| `MainEscape*` | 52 | Mid-stage Main Escape system that was paused after architectural tangling, but remains as a structural skeleton for hospital content, markers, doors, settings, and validators. |
| `IR*` | 24 | Remake interface layer. A few `I*` interfaces are counted near this group by name shape. |
| `Enemy*` | 19 | Enemy state, perception, audio/feedback contracts, sprite drivers. |
| `Player*` | 15 | Player health, flashlight, inventory-facing controls, stamina, runtime presentation. |
| `Prototype*` | 10 | Original starting-system layer from the beginning of the project, including item/audio/pickup helpers and procedural scene utility. |
| `Noise*` | 3 | Noise event bus, emitters, panels. |
| `Floor*` | 3 | Escape interactables and door assembly result. |
| `Scene*` | 4 | Scene contract data, route graph resolver, test defaults, load utility. |
| Other | 71 | Generic utilities, interfaces, domain-specific names without a shared prefix. |

## Domain Inventory

| Domain | Count | Current Role |
|---|---:|---|
| `Rebuild` | 109 | `RRun*`, `RScene*`, `RFloor*`, `RElevator*`, `IR*` UI, route/session/runtime loop. |
| `Objectives` | 64 | `MainEscape*` floor authoring, doors, markers, pickups, vents, debug/validator. |
| `Enemy` | 29 | Enemy state, vision, audio bindings, ground/vent profiles, common controllers. |
| `Player` | 18 | Movement, stamina, health, flashlight, HUD presentation, player references. |
| `Runtime` | 17 | Scene load/camera utilities and debug/fog/flashlight/UI ownership state. |
| `Audio` | 10 | Live audio manager/drivers, ambience, loudness audit. |
| `Grid` | 8 | Grid services, tile catalogs, bootstrap, vent route tools. |
| `Inventory` | 7 | Inventory, item catalog/icons, pickups, throwable bottle. |
| `Generation` | 6 | BSP/WFC/generated floor layout and generated/prototype settings. |
| `Perception` | 6 | Visibility, light, exposure, vision sensor. |
| `Noise` | 4 | Noise bus, emitters, system, floor panels. |
| `Lighting` | 2 | Flicker and runtime shadow caster configuration. |
| `Physics` | 1 | Top-down collision material utility. |
| root | 1 | `PrototypeSceneUtility`. |

The current confusion is not mainly copy-pasted duplicate logic. It is
historical coexistence: the original `Prototype*` layer, the paused-but-still
structural `MainEscape*` layer, and the newer `R*`/`IR*` remake layer all exist
at the same time.

## Project Vocabulary

These meanings are project-authoritative and should override generic naming
assumptions:

| Prefix | Meaning | Naming Rule |
|---|---|---|
| `Prototype*` | Original starting system. | Do not treat this as disposable by default. Rename only when ownership has clearly moved to a remake or domain system. |
| `MainEscape*` | Middle-stage Main Escape system that was paused because system ownership tangled, but still remains as the hospital skeleton. | Keep as authored hospital skeleton/bridge naming until the replacement owner is explicit. |
| `R*` | Remake. | Use for remake runtime systems, remake scene flow, remake floor orchestration, and remake presentation/runtime bridges. |
| `I*` | Interface contract. | Use for C# interface contracts and small dependency boundaries. |
| `IR*` | Remake interface. | Use for remake interface/UI surface pieces. Because this can look like `I` + `R`, avoid using it for non-interface gameplay runtime classes. |

## Target Naming Rules

### `R*`

Use for remake runtime infrastructure.

Allowed:

- Run/session/routing systems.
- Floor runtime composition and placement.
- Scene composition/root binding.
- Runtime bridges that exist specifically for the remake loop.

Avoid:

- Authored marker components placed as scene content.
- Generic player/enemy/noise logic that should be reusable outside the remake loop.

### `IR*`

Use for the remake interface layer.

Allowed:

- HUD views.
- HUD/lobby binders.
- Modal/input routers tied to the `IR` UI surface.
- Remake-specific interface presentation pieces.

Avoid:

- Runtime gameplay logic.
- Generic data contracts.
- Plain C# interface contracts. Those stay `I*`.

### `MainEscape*`

Use for the paused Main Escape middle-stage system that still forms the hospital
content skeleton.

Allowed:

- Scene-authored markers.
- Hospital floor authoring data.
- Door authoring/visual override components.
- Runtime settings or catalogs that are still skeleton/alignment assets.

Avoid:

- New chapter-agnostic runtime systems.
- New route authority systems. Route authority is `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`.

### `Prototype*`

Treat as the original starting system.

Allowed:

- Existing item/audio/pickup original-system pieces.
- Existing prototype scene utility.

Avoid:

- New remake systems.
- New chapter/map expansion code unless it is intentionally extending the
  original prototype layer.

### Domain Names

Use plain domain names when a system is not Main Escape-specific.

Examples:

- `Player*` for player-owned state and controls.
- `Enemy*` for enemy-owned perception/state/feedback.
- `Noise*` for the noise bus and emitters.
- `Vision*` / `Visibility*` for perception systems.

## Confusing Clusters

| Cluster | Current Names | Problem | Direction |
|---|---|---|---|
| Floor runtime | `MainEscapeFloorDirector`, `RFloorDirector`, `MainEscapeFloorAuthoring`, `RSceneResidentAuthoredFloorBuildSource` | Main Escape skeleton and remake runtime ownership coexist. | Keep `MainEscapeFloorAuthoring` as authored hospital skeleton data. Treat `RFloorDirector` as remake runtime owner. Avoid new `MainEscapeFloorDirector` work unless it is explicitly skeleton/bridge maintenance. |
| Run/session/routing | `RRunSessionController`, `RSceneRouter`, `RRunRoutingSettings`, `RouteGraphDefinition`, `MainEscapeSceneRouteResolver`, `MainEscapeRuntimeSettings` | Route authority and fallback/alignment names can read as equal owners. | Route graph is authority. `MainEscapeRuntimeSettings` and serialized scene routes are compatibility/alignment fallback. |
| HUD/UI | `IRHudCanvas`, `IRAuthoredGameplayHudView`, `IRPlayer*HudBinder`, `RRunHudPresenter`, `MainEscapeHudPresentation` | `IR`, `R`, and `MainEscape` interface/presentation names overlap. | Keep `IR*` for remake interface surface. Keep `RRunHudPresenter` for run snapshot presentation. Migrate or quarantine `MainEscapeHudPresentation` only after checking references. |
| Audio | `PrototypeAudioManager`, `PrototypePlayerAudio`, `EnemyPassiveAmbientAudio`, `RoomLightHumAudio`, `AudioScenePlayerReferenceResolver` | Original-system audio still owns production audio paths. | Do not rename `Prototype*` audio just because it says prototype. New remake audio utilities should use `R*`, `Audio*`, or domain names based on ownership. Rename only after replacing serialized references. |
| Inventory/items | `PrototypeItemCatalog`, `PrototypeItemUiIconResolver`, `PrototypeInventoryPickup`, `RTutorialInventoryPickup`, `PlayerInventory` | Original-system item data is still production-used. | Keep for now. Plan a later item-system ownership pass because pickups are serialized in prefabs/scenes. |
| Doors/objectives | `MainEscapeSelfContainedDoor`, `MainEscapeDoor*`, `FloorEscapeTransitionPoint`, `MainEscapeFinalExitTouchTrigger`, `ObjectiveLoopDemo` | Door/exit/objective names mix production and demo terms. | Treat `ObjectiveLoopDemo` as legacy/demo only. Keep serialized door names until a door-specific migration pass. |

## Concrete Rename Candidates

These are candidates, not instructions to rename immediately. Each one needs the
risk rule below applied before any file/class change.

| Current Target | Suggested Direction | Risk | Reason |
|---|---|---|---|
| `PrototypeAudioManager` | Keep until audio ownership is intentionally moved to `R*`/domain naming | High | Original-system production audio path, referenced by floor scenes. |
| `PrototypeInventoryPickup`, `PrototypeFlashlightPickup` | `MainEscapeInventoryPickup`, `FlashlightPickup` | High | Pickup prefabs/scenes can serialize these components. |
| `PrototypeEnemyAudioDriver`, `PrototypePlayerAudio` | Keep or move to domain names only after ownership changes | Medium/High | Original-system audio names have prefab and code references mixed. |
| `IRHudCanvas`, `IRLobbyController`, `IR*View` | Keep `IR*` if they remain remake interface components | High | `IR` is project vocabulary for remake interface, so rename only if a class stops being interface/UI surface. |
| `MainEscapeRuntimeSettings`, `MainEscapeRuntimePrefabCatalog` | Keep or clarify as skeleton/alignment catalogs | High | They are Main Escape skeleton/alignment assets under existing resource paths. |
| `Perception/FlashlightFogOfWarOverlay` | Legacy stub naming or removal after validation | Low | Real implementation lives elsewhere; this file is compatibility shape. |
| `ObjectiveInteraction` | `PlayerInteractable2D` | Medium | File/class naming reduces search clarity. |
| `ObjectiveLoopDemo` | Legacy isolation or split into production door/objective files | Medium | The file contains production-looking `ObjectiveManager`/`DoorController` names under a demo file. |
| `RLegacyFloorBuildPipeline` | Keep name or move to a legacy area | Medium | Name is honest; moving may be clearer than renaming. |

## Responsibility Boundaries

| Domain | Owner Names To Keep Stable | Boundary |
|---|---|---|
| Run/session/routing | `RRunSessionController`, `RSceneRouter`, `RRunSceneRouteFloorResolver`, `RouteGraphDefinition` | Session owns run state and player-state snapshots. Router executes scene loads. Route graph/floor resolver own route calculation. `MainEscapeSceneRouteResolver` remains compatibility/fallback. |
| Scene composition | `RSceneCompositionRoot`, `RSceneBindingCacheUtility`, `RSceneReferenceLookup` | Composition root binds authored references and runtime systems. It should not grow into a hidden object factory except for documented transitional scaffolding. |
| Floor authoring/runtime | `MainEscapeFloorAuthoring`, `RFloorDirector`, `REncounterSpawner` | `MainEscapeFloorAuthoring` owns authored markers/root data. `RFloorDirector` owns runtime floor orchestration. `REncounterSpawner` owns enemy spawn execution. |
| HUD/UI | `IRHudCanvas`, `IRPlayer*HudBinder`, `IR*PanelView`, `IRAuthoredGameplayHudView` | Canvas hosts UI. Binders connect runtime/player state. Panel views render. `IRAuthoredGameplayHudView` is a long-term split candidate because it still mixes view, binder, and prefab-local auto-resolve. |
| Audio/noise | `PrototypeAudioManager`, `NoiseSystem`, `INoiseEventBus`, `NoiseEmitter`, `NoiseFloorPanel` | Noise event bus and audio playback stay separate. `PrototypeAudioManager` remains original-system production audio; new remake audio APIs should not be added there unless intentionally extending that layer. |
| Items/inventory | `PlayerInventory`, `PlayerQuickItemController`, `WorldInventoryPickupBase`, `RFloorItemPlacementRuntime` | Inventory stores item stacks. Quick item controller owns use/slots. World pickup owns pickup interaction. Floor item placement owns scene placement. |
| Doors/objectives | `IMainEscapeRuntimeDoor`, `MainEscapeRuntimeDoorRegistry`, `MainEscapeSelfContainedDoor`, `MainEscapeDoor*` | Converge through common interfaces/registry/presentation-passability separation before renaming serialized door classes. |

## Do Not Rename Yet

These names are heavily tied to scene/prefab serialization, tests, or current
docs. Rename only during a dedicated Unity Editor migration pass:

- `RRunSessionController`
- `RSceneRouter`
- `RSceneCompositionRoot`
- `RRunController`
- `RFloorDirector`
- `RPlayerRuntimeReferences`
- `IRHudCanvas`
- `IRPlayer*HudBinder`
- `IRAuthoredGameplayHudView`
- `MainEscapeFloorAuthoring`
- `MainEscapeFloorDirector`
- `MainEscapeEncounterSpawner`
- `MainEscapeSelfContainedDoor`
- `FloorEscapeTransitionPoint`
- `MainEscapeElevatorExitInteractable`
- `MainEscapeKeyGatePoint`
- `MainEscapeRuntimeDoorRegistry`
- `NoiseSystem`
- `PrototypeAudioManager`
- `PlayerInventory`
- `PlayerQuickItemController`
- `WorldInventoryPickupBase`

`ObjectiveLoopDemo.cs` and its internal `DoorController` should be treated as
legacy/demo code. Prefer isolation or deletion after reference validation rather
than renaming it into the production door stack.

Also avoid broad file moves or namespace reshuffles around these high-reference
authoring/runtime marker families until Unity Editor validation is available:

- `MainEscapeVentNodeAuthoring`
- `MainEscapeAuthoringMarkerVisual`
- `MainEscapeItemPlacementMarker`
- `CoverPropRuntime`

## Rename Risk Rules

| Risk | Safe To Do | Examples |
|---|---|---|
| Low | Non-serialized static/internal utilities with no prefab/scene component references. | Pure validators, adapters, utility classes. |
| Medium | Data structs/classes and tests, or files only referenced by code. | `Scene*` data contracts, route adapters, test names. |
| High | `MonoBehaviour` and `ScriptableObject` types used by scenes, prefabs, or assets. | `RRunSessionController`, `RFloorDirector`, `MainEscapeFloorAuthoring`, `PrototypeAudioManager`, `IRHudCanvas`, pickups, doors. |

High-risk names should not be changed without:

- Reference search across scenes, prefabs, and assets.
- Unity Editor compile and console check.
- Scene/prefab validation after rename.
- A small migration note in `Docs`.

## Execution Plan

### Phase 1: Vocabulary Freeze

- Treat `Prototype*` as original-system naming, not disposable legacy naming.
- Stop adding new route ownership under `MainEscapeRuntimeSettings`.
- Use `R*` for remake runtime infrastructure.
- Use `MainEscape*` for the paused Main Escape skeleton/alignment layer.
- Use `I*` for C# interface contracts.
- Use `IR*` for remake interface/UI surface naming.

### Phase 2: Documentation And Indexing

- Add or maintain a short ownership index for each major domain.
- Mark compatibility systems as compatibility instead of silently renaming them.
- Keep `FragmentedOptionFeatureOwnershipAudit.md` as historical/rough audit unless its mojibake is repaired.

### Phase 3: Low-Risk Rename Pass

- Rename or regroup non-serialized static utilities only.
- Update tests and docs in the same pass.
- Validate with runtime/EditMode compile.

### Phase 4: Medium-Risk Domain Passes

Do one domain at a time:

- Audio naming pass.
- Inventory/item naming pass.
- Door/objective naming pass.
- Scene contract naming pass.

### Phase 5: High-Risk Unity Reference Migration

Only after Unity Editor validation is available:

- Rename serialized `MonoBehaviour`/`ScriptableObject` types.
- Preserve `.meta` GUIDs where possible.
- Open affected scenes/prefabs and check missing scripts.
- Run smoke tests.

## Immediate Next Step

Do not start a broad rename yet.

Start with a script ownership index and then pick one low-risk cluster. The best
first cluster is tests/docs/static utility naming because it reduces confusion
without touching authored scene references.

After that, use this order:

- Static/code-only utility rename pass.
- Legacy/demo isolation pass for `ObjectiveLoopDemo`-style files.
- Audio and item ownership pass with no serialized rename yet.
- Unity Editor assisted rename pass for serialized MonoBehaviours and
  ScriptableObjects after editor validation works again.

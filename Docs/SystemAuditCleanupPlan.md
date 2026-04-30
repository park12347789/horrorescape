# System Audit Cleanup Plan

This document captures the current cleanup direction after the latest repo-wide cleanup pass. The goal is still not broad feature expansion. The goal is to keep the project on one canonical runtime path and remove misleading leftovers in measured stages.

## Working Set

Read this plan together with:

- `Docs/architecture/ADR-0001-live-authored-scene-chain.md`
- `Docs/architecture/ADR-0002-controlled-resources-loading.md`
- `Docs/architecture/ADR-0003-defer-runtime-asmdef-and-namespace-rollout.md`
- `Docs/status/MainEscapeCurrentState.md`
- `Docs/status/ImplementationRoadmap.md`

This plan describes cleanup work. The ADRs describe the currently accepted
boundaries that cleanup work should not casually violate.

## Decision Anchors

Before changing cleanup direction, keep these accepted decisions in mind:

- the live authored lobby-plus-floor chain is the canonical runtime path
- existing `Resources` loading stays in place for the current stabilization
  phase, but should not expand casually
- broad runtime asmdef and namespace rollout is intentionally deferred until
  later

If a cleanup task no longer fits those anchors, update the relevant ADR or add a
new one before treating the new direction as current policy.

## Canonical Ownership

Treat the live `R` loop as the playable content baseline and runtime
verification target. Route-data authority remains
`RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`.

- Scene composition: `RSceneCompositionRoot`
- Run/session state: `RRunSessionController`
- Floor/runtime flow: `RRunController`
- Floor build and encounter setup: `RFloorDirector`
- Player runtime references: `RPlayerRuntimeReferences`
- HUD surface: `IRHudCanvas` and the `IR*` panel/binder layer
- Enemy perception: `VisionSensor2D` + `VisibilityTarget2D`
- Shared noise bus: `NoiseSystem`
- Fog-of-war: `Objectives/FlashlightFogOfWarOverlay.cs`

If a feature still lives only because of older `MainEscape*` naming or authored bridges, treat it as transitional.

## Recently Completed Cleanup

- The in-project `Legacy/R0_OldMainEscape/` workspace mirror was removed from the live tree.
- Detached enemy/lighting test bay scenes and temporary showcase scenes were removed from the checked-in workspace and Build Settings.
- Detached-scene override catalogs for the deleted bay scenes were removed.
- Detached-scene support files were removed from the checked-in runtime path:
  - `RTestBayRuntimeController`
  - `RTestBaySceneProfile`
  - `RTestBaySceneUtility`
- The old detached-scene editor setup tool and compatibility test were removed.
- Temporary analog lobby UI is no longer the checked-in default.
- The editor menu surface was trimmed to the current authored-loop tools only.
- `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition` is now treated as
  the route-data bridge/first authority, with scene-local routing and
  `MainEscapeRuntimeSettings.asset` held to compatibility, alignment, and
  support responsibilities.
- Runtime route/floor identity helpers were tightened so canonical live scene
  identity is checked before broad runtime-settings fallbacks.
- Loop smoke, preview capture, and recovery-prep flows were moved to manual-only
  editor invocation, and the dedicated audio loudness trigger bridge plus empty
  editor setup stubs were removed.
- The second decoupling round moved policy further away from central fallback
  ownership: `MainEscapeFloorAuthoring` is the local owner for floor authoring
  root and marker root names, normal authored-floor tools operate on open/live
  scenes, and legacy `Assets/Resources/Floors/MainEscape/*.prefab` mutation is
  isolated behind explicit legacy menu actions.

## Remaining Cleanup Stages

### 1. Revalidate the canonical path in Unity

- run lobby/start-floor validators after the cleanup pass
- run targeted PlayMode smoke coverage
- do one manual lobby-to-floor-chain pass

### 2. Reduce authored/runtime bridge complexity

- keep shrinking ambiguity around `MainEscapeFloorAuthoring`
- keep floor authoring root and marker root names owned locally by
  `MainEscapeFloorAuthoring`, not by `RSceneCompositionRoot` or broad runtime
  settings fallbacks
- reduce name-based scene lookup where explicit serialized wiring is practical
- treat `RSceneCompositionRoot` fallback root/object creation as transitional
  migration scaffolding to remove after authored references are verified
- avoid reintroducing detached-scene compatibility branches

### 3. Reduce prototype compatibility debt

- keep `PrototypeSceneUtility` minimal
- keep `PrototypeAudioManager` and other prototype-named utilities under watch
- remove prototype/demo leftovers only after confirming no live references remain

### 3a. Continue tool-surface reduction

- audit one-off editor rebuild/clipboard helpers against the current authored
  live loop
- prefer deleting dormant scaffolds and trigger-based wrappers over keeping
  them as placeholders
- preserve explicit menu-driven workflows where they still help iteration

### 4. Keep docs, validators, and tests aligned

- update manifests/status docs whenever live routing changes
- keep editor menu names aligned with the actual current workflow
- avoid historical docs silently becoming current-state docs

## Practical Rule

If a change touches both `MainEscape*` and `R*` code, assume the change is not finished until one side is explicitly retired, reduced to a bridge, or clearly delegated to the other.

## Current Watchpoints

- `RRunController` is still one of the heaviest classes on the canonical runtime path.
- `RSceneCompositionRoot` is cleaner than before, but any remaining hierarchy/name recovery or fallback creation should now be considered transitional.
- `Batch2TestRoomBootstrap` remains a large dependency surface in the floor runtime/build path.
- `Assets/Resources/Floors/MainEscape/*.prefab` baselines remain quarantined as legacy migration/recovery assets; do not treat them as live authored floor truth.
- The route graph, `MainEscapeRuntimeSettings.asset`, and `ProjectSettings/EditorBuildSettings.asset` must stay aligned with the lobby, optional tutorial support scene, five-floor route, and elevator transition support scene.
- Do not reintroduce detached test-bay/showcase helpers unless there is a deliberate archival recovery need.

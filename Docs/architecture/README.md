# Architecture Docs

This folder is the architecture entry point for the current `HorrorStealth`
documentation set.

For now, some architecture documents still live at the top level of `Docs/`.
Use this index as the canonical map while the repository is being cleaned up in
place.

## Recommended Reading Order

1. [ADR-0001 Live Authored Scene Chain](ADR-0001-live-authored-scene-chain.md)
2. [ADR-0002 Controlled Resources Loading](ADR-0002-controlled-resources-loading.md)
3. [ADR-0003 Defer Runtime Asmdef And Namespace Rollout](ADR-0003-defer-runtime-asmdef-and-namespace-rollout.md)
4. [ADR-0004 Explicit Regular Door Authoring](ADR-0004-explicit-regular-door-authoring.md)
5. [ADR-0005 Route Graph Assets As The Routing Bridge](ADR-0005-route-graph-assets-as-routing-bridge.md)
6. [PrototypeArchitecture](../PrototypeArchitecture.md)
7. [RSceneRebuildManifest](../RSceneRebuildManifest.md)
8. [SystemAuditCleanupPlan](../SystemAuditCleanupPlan.md)

## Active Working Set

Architecture decisions are only useful when paired with current execution
documents. For the active project phase, read these together:

- [Status README](../status/README.md)
  - current-state docs and planning entry point
- [ImplementationRoadmap](../status/ImplementationRoadmap.md)
  - near-term execution priorities
- [MainEscapeLiveLoopSystem](../design/MainEscapeLiveLoopSystem.md)
  - player-facing system summary
- [MainEscapeAuthoringGuide](../MainEscapeAuthoringGuide.md)
  - practical authored-scene workflow

## Current-State Architecture Docs

- [PrototypeArchitecture](../PrototypeArchitecture.md)
  - current runtime ownership across lobby, floor composition, run flow, and
    HUD binding
- [RSceneRebuildManifest](../RSceneRebuildManifest.md)
  - canonical authored-loop rules and rebuild direction
- [SystemAuditCleanupPlan](../SystemAuditCleanupPlan.md)
  - cleanup stages, watchpoints, and architectural risk notes
- [RFileMapping](../RFileMapping.md)
  - naming and runtime/UI file mapping during the `MainEscape*` to `R*`/`IR*`
    transition

## ADRs

- [ADR-0001 Live Authored Scene Chain](ADR-0001-live-authored-scene-chain.md)
  - records the decision that the authored lobby-plus-floor chain is the
    playable content baseline and source of truth for content placement
- [ADR-0002 Controlled Resources Loading](ADR-0002-controlled-resources-loading.md)
  - records the current loading stance while the live loop stabilizes
- [ADR-0003 Defer Runtime Asmdef And Namespace Rollout](ADR-0003-defer-runtime-asmdef-and-namespace-rollout.md)
  - records the decision to avoid a broad structure-only rollout during the
    current cleanup phase
- [ADR-0004 Explicit Regular Door Authoring](ADR-0004-explicit-regular-door-authoring.md)
  - records the decision that regular `E` doors are explicit self-contained
    prefab actors instead of visual-synthesis-owned scene objects
- [ADR-0005 Route Graph Assets As The Routing Bridge](ADR-0005-route-graph-assets-as-routing-bridge.md)
  - records the decision to route through explicit chapter/route graph assets
    while preserving the live authored hospital loop
- [ADR Template](adr-template.md)
  - use for future package, pipeline, scene-routing, or asset-loading
    decisions

## Next ADR Candidates

- long-term ownership reduction around `MainEscapeFloorAuthoring` and
  `Batch2TestRoomBootstrap`
- criteria for retiring prototype-named compatibility utilities

## Update Rule

Update this category when a document changes the long-lived structure of the
project, not just day-to-day implementation status.

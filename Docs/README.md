# MainEscape Docs

This folder tracks the current documentation set for the authored lobby,
optional tutorial support scene, five-floor route, elevator transition support
scene, and return-to-lobby flow.

## Canonical Scenes

- Build index `0`: `Assets/Scenes/RMainEscape_Lobby.unity`
- Build index `1`: `Assets/Scenes/RMainEscape_tuto.unity`
  - optional support scene; excluded from floor route arrays
- Build index `2`: `Assets/Scenes/RMainScene_5F.unity`
- Build index `3`: `Assets/Scenes/RMainScene_4F.unity`
- Build index `4`: `Assets/Scenes/RMainScene_3F.unity`
- Build index `5`: `Assets/Scenes/RMainScene_2F.unity`
- Build index `6`: `Assets/Scenes/RMainScene_1F.unity`
- Build index `7`: `Assets/Scenes/RMainEscape_ElevatorTransition.unity`
  - interstitial support scene for floor-to-floor loads; excluded from floor route arrays
- Routing authority: `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition` is the route-data bridge/first authority; the live authored scene chain is the playable content baseline, and serialized scene-local routes are compatibility/alignment fallbacks
- Feature authority: the component or prefab that implements a feature should usually own its own explicit Inspector references and scene-local data
- Alignment assets: `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset` and `ProjectSettings/EditorBuildSettings.asset`; use them for route validation and shared reference data, not as a default home for feature-specific behavior

## Start Here

- `Docs/status/MainEscapeCurrentState.md`
  - current route, Build Settings, and cleanup baseline for the next worker
- [MainEscape Contract Refactor Sequence](design/MainEscapeContractRefactorSequence.md)
  - active cleanup sequence for visible scene reference points, read-only reporting,
    snapshot boundaries, and legacy mutation quarantine
- [Scene Indirection Contract Audit](design/SceneIndirectionContractAudit.md)
  - audit of scene-visible objects that route through owners, resolvers,
    services, runtime planners, or naming assumptions
- [MainEscape Regular Door Fragility Report](design/MainEscapeRegularDoorFragilityReport.md)
  - current root-cause report and stabilization design for regular `E`
    open/close doors such as `VexedTileBProp_01_Top` and
    `CustomSideDoorClosed*`
- [MainEscape Play Review Tuning Plan](design/MainEscapePlayReviewTuningPlan.md)
  - targeted tuning design for music, side-door feel, bottle balance, spotted
    feedback, and vent enemy readability
- `Docs/status/ImplementationRoadmap.md`
  - near-term priorities after the latest validation pass
- `Docs/MainEscapeAuthoringGuide.md`
  - day-to-day authored scene workflow for the live floors
- `Docs/design/MainEscapeFogRefresh.md`
  - current fog and flashlight stabilization baseline plus map rollout steps
- `Docs/RSceneRebuildManifest.md`
  - live-loop safety guidance and rebuild-era reference notes
- `Docs/SystemAuditCleanupPlan.md`
  - remaining cleanup stages and watchpoints
  - what has already been removed and what is still transitional
- `Docs/PrototypeArchitecture.md`
  - system ownership overview for the current runtime path

## Documentation Structure

- `Docs/architecture/README.md`
  - architecture map, ADRs, and current architecture documents
- `Docs/design/README.md`
  - design, authoring workflow, and system design entry point
- `Docs/reference/README.md`
  - stable conventions, file mapping, and reusable baselines
- `Docs/checklists/README.md`
  - reusable review and migration checklists
- `Docs/status/README.md`
  - current-state status snapshots and active roadmaps

## Reusable Templates

These files are support templates and baseline guidance. They are not canonical
runtime truth for the current playable loop, but they are the preferred place
to record new decisions and repeatable review workflow:

- `Docs/architecture/adr-template.md`
  - ADR template for migration and architecture decisions
- `Docs/checklists/code-review-checklist.md`
  - review checklist for gameplay, scene, and runtime changes
- `Docs/checklists/project-setup-checklist.md`
  - setup or migration checklist for new workstreams
- `Docs/design/game-concept-template.md`
  - lightweight concept template
- `Docs/design/system-design-template.md`
  - gameplay/system design template
- `Docs/reference/unity-project-baseline.md`
  - Unity engineering baseline

## Canonical Structured Docs

- `Docs/architecture/ADR-0001-live-authored-scene-chain.md`
  - accepted decision for the live authored lobby-plus-floor runtime path
- `Docs/architecture/ADR-0002-controlled-resources-loading.md`
  - accepted loading stance for the current project phase
- `Docs/architecture/ADR-0003-defer-runtime-asmdef-and-namespace-rollout.md`
  - accepted structural deferral for runtime asmdefs and namespaces
- `Docs/architecture/ADR-0004-explicit-regular-door-authoring.md`
  - accepted decision that regular `E` doors are self-contained authored prefab
    actors
- `Docs/architecture/ADR-0005-route-graph-assets-as-routing-bridge.md`
  - accepted decision to bridge live routing through explicit chapter and route
    graph assets without adding new `Resources` load paths
- `Docs/design/MainEscapeLiveLoopSystem.md`
  - current system design summary for the live playable loop
- `Docs/design/MainEscapePlayReviewTuningPlan.md`
  - current play-review-based tuning plan for the live loop
- `Docs/design/MainEscapeShadowStartleSystem.md`
  - implemented MVP for non-combat shadow startle cues

## Notes

- Prefer direct authored scene and prefab placement over runtime regeneration whenever possible.
- When floor route ownership changes, update the route graph and keep `MainEscapeRuntimeSettings.asset` plus `EditorBuildSettings.asset` aligned; for ordinary scene-authored features, prefer local serialized references on the owning component.
- When adding support/interstitial scenes, keep the floor route arrays unchanged and document the support scene path separately.

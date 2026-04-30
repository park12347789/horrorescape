# Design Docs

This folder is the entry point for player-facing design, system design, and
authoring workflow documentation.

Some active design documents still live at the top level of `Docs/`. Use this
index as the current map during the cleanup pass.

## Current Design And Workflow Docs

- [GameDesignDocument](../GameDesignDocument.md)
  - core pillars, readability rules, enemy roles, and tone targets
- [MainEscape Live Loop System](MainEscapeLiveLoopSystem.md)
  - structured system design summary for the live lobby-to-floor-chain loop
- [Run Runtime Structure Refactor](RunRuntimeStructureRefactor.md)
  - target split for session state, player state persistence, and scene bindings
- [MainEscape Contract Refactor Sequence](MainEscapeContractRefactorSequence.md)
  - high-intensity phased plan for collapsing hidden scene/runtime contracts
- [Scene Contract And Data Separation Plan](SceneContractDataSeparationPlan.md)
  - canonical vocabulary and flexible chapter/route/scene contract plan for
    hospital, backrooms, pool, and future expansion
- [Scene Indirection Contract Audit](SceneIndirectionContractAudit.md)
  - current audit of scene objects routed through owners, resolvers, services,
    runtime planners, and naming contracts
- [MainEscape Regular Door Fragility Report](MainEscapeRegularDoorFragilityReport.md)
  - current analysis of why `VexedTileBProp_01_Top` and `CustomSideDoorClosed*`
    regress and the recommended stabilization path
- [MainEscape Play Review Tuning Plan](MainEscapePlayReviewTuningPlan.md)
  - targeted tuning plan for music, interaction feel, bottle balance, spotted
    feedback, and vent readability in the live loop
- [MainEscape Vent Network Authoring Redesign](MainEscapeVentNetworkAuthoringRedesign.md)
  - current vent-route audit and target workflow for explicit scene-authored
    vent nodes, links, and exits
- [MainEscape Shadow Startle System](MainEscapeShadowStartleSystem.md)
  - implemented MVP for one-shot non-combat shadow apparition beats
- [MainEscape Fog Refresh](MainEscapeFogRefresh.md)
  - stabilized fog and flashlight baseline, blocker semantics, and rollout
    checklist for moving the setup between floors
- [MainEscapeAuthoringGuide](../MainEscapeAuthoringGuide.md)
  - scene authoring workflow and validation steps
- [RFloorDecorWorkflow](../RFloorDecorWorkflow.md)
  - practical dressing order and root usage for decorated floors

## Templates

- [Game Concept Template](game-concept-template.md)
- [System Design Template](system-design-template.md)

## Update Rule

Update this category when a document changes the intended player experience,
authoring workflow, or subsystem behavior. Architecture-only ownership changes
belong in `Docs/architecture`.

# ADR-0003: Defer Broad Runtime Asmdef And Namespace Rollout Until After Loop Stabilization

- Status: Accepted
- Date: 2026-04-13
- Decision Makers: Project maintainers
- Related Docs:
  - `Docs/reference/unity-project-baseline.md`
  - `Docs/RSceneRebuildManifest.md`
  - `Docs/SystemAuditCleanupPlan.md`
  - `Docs/architecture/ADR-0001-live-authored-scene-chain.md`
- Affected Areas:
  - C# project structure
  - scene and prefab script references
  - compile boundaries
  - future refactor planning

## Context

The current project has almost no runtime assembly or namespace partitioning:

- `2` asmdefs under `Assets`, both for tests
- `0` namespace declarations under `Assets/Scripts`

At the same time, the live project depends heavily on authored scene and prefab
references across the active runtime path. A broad namespace or runtime asmdef
rollout would touch many scripts at once and raises the risk of missing-script
or reference-churn problems during a phase where the main priority is loop
stability.

The project still needs a documented stance, because it is easy to drift into an
accidental half-migration where only some files move and the cost is paid
without getting the full organizational benefit.

## Options Considered

1. Roll out runtime asmdefs and namespaces across the project immediately.
2. Defer the broad rollout until the live loop is more stable, while allowing
   isolated future modules to opt in deliberately.
3. Ignore the topic entirely and let structure drift without a stated rule.

## Decision

Defer a broad runtime asmdef and namespace rollout until after the current live
loop stabilization work is further along.

For the current phase:

- keep existing runtime scripts in their current assembly unless there is a
  scoped, explicit migration plan
- do not start a sweeping namespace conversion across `Assets/Scripts`
- if a new isolated editor tool, package-like module, or future subsystem truly
  benefits from an asmdef, document the boundary and keep the write scope small
- revisit broad asmdef and namespace strategy only after the authored six-scene
  loop is stable and validated

## Consequences

### Benefits

- Avoids wide churn across active gameplay, scene, and prefab references.
- Keeps current cleanup effort focused on behavior and readability rather than
  structure-only refactors.
- Makes the current deferral an intentional decision instead of passive drift.

### Tradeoffs

- Compile boundaries remain coarse for now.
- Architectural modularity improves more slowly.
- The eventual migration may still be large if the codebase keeps growing before
  the decision is revisited.

### Follow-Up

- Revisit asmdef and namespace strategy after the live loop is stable.
- Prefer ADR-backed migration planning before any broad rollout.
- If a local isolated module needs an asmdef earlier, document why that module
  is safe to separate without destabilizing scene content.

## Validation

- Runtime scene and prefab references should remain intact through the current
  cleanup phase.
- New structure-only refactors should be reviewed for churn risk against the
  authored live loop.
- Any future rollout should inventory script references and include explicit
  Unity validation before and after migration.

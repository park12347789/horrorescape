# Status Docs

Use this category for current-state summaries, active cleanup status, and
near-term execution planning.

## Decision Anchors

Status docs should be read against the current architecture decisions:

- [ADR-0001 Live Authored Scene Chain](../architecture/ADR-0001-live-authored-scene-chain.md)
- [ADR-0002 Controlled Resources Loading](../architecture/ADR-0002-controlled-resources-loading.md)
- [ADR-0003 Defer Runtime Asmdef And Namespace Rollout](../architecture/ADR-0003-defer-runtime-asmdef-and-namespace-rollout.md)
- [ADR-0004 Explicit Regular Door Authoring](../architecture/ADR-0004-explicit-regular-door-authoring.md)
- [SystemAuditCleanupPlan](../SystemAuditCleanupPlan.md)

If a status task starts conflicting with one of these decisions, update the
decision doc or create a new ADR instead of letting the roadmap drift silently.

## Current Status Docs

- [MainEscapeCurrentState](MainEscapeCurrentState.md)
  - current route, Build Settings, and cleanup baseline
- [ImplementationRoadmap](ImplementationRoadmap.md)
  - active priorities, near-term tasks, and deprioritized work

## Update Rule

Status docs are allowed to age. Keep them clearly time-scoped and focused on
what is true now, not on durable architecture decisions. When a status decision
becomes long-lived, move it into architecture, design, or reference docs.

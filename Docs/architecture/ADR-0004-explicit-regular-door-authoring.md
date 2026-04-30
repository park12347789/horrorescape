# ADR-0004 Explicit Regular Door Authoring

- Status: Accepted
- Date: 2026-04-23

## Context

The live regular `E` open/close doors had become fragile because their runtime
contract was split across authored visuals, tilemap state, and synthesized
`DoorController` instances. In practice:

- live floor scenes often fell back to visual-door synthesis instead of
  explicit authored door data
- `VexedTileBProp_01_Top` and `CustomSideDoorClosed*` could regress when sprite
  bounds, hierarchy, or loose scene copies changed
- side doors were frequently loose scene objects instead of canonical prefab
  instances
- passability and interaction truth could drift away from the visible door root

This made routine scene cleanup and patch work repeatedly break otherwise simple
regular doors.

## Decision

Regular authored doors are now explicit self-contained prefab actors.

- `VexedTileBProp_01_Top.prefab` and `CustomSideDoorClosed.prefab` are the
  canonical authored assets for the regular front-door and side-door families.
- Each prefab root owns its own interaction and runtime state through
  `MainEscapeSelfContainedDoor`, plus explicit
  `MainEscapeDoorVisualVariantOverride`, collider, and open-visual child
  wiring.
- `MainEscapeFloorAuthoring.BuildDoorGroups()` treats
  `MainEscapeSelfContainedDoor` components as authored door data before any
  visual synthesis fallback.
- `RRuntimeDoorAssembler` must skip generated door groups already claimed by a
  self-contained door root.
- `GridMapService` must defer regular-door open/closed truth to the runtime
  self-contained door registry for claimed door cells.
- Live floor scenes and the tutorial scene must use prefab instances for these
  regular door families; loose scene copies are not a valid authoring state.
- validation and edit-mode contract tests must report missing self-contained
  door components, non-prefab instances, wrong prefab source assets, and
  authored door-group coverage gaps

## Consequences

Positive:

- regular door behavior, visuals, and blocker ownership now travel with the
  prefab root
- scene patching no longer depends on sprite-bound inference for these door
  families
- replacing or moving a regular door is a prefab authoring operation instead of
  a hidden runtime reconstruction problem

Tradeoffs:

- `GridMapService` and runtime door assembly now carry an explicit compatibility
  layer for migrated self-contained doors
- visual synthesis still exists for unmigrated door-like content, so validator
  coverage remains important until legacy fallback usage is retired

## References

- [MainEscape Regular Door Fragility Report](../design/MainEscapeRegularDoorFragilityReport.md)
- [2026-04-19 MainEscape Door State Ownership Refactor](../history/2026-04-19-MainEscapeDoorStateOwnershipRefactor.md)

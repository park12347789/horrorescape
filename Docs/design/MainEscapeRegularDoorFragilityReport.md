# MainEscape Regular Door Fragility Report

Date: 2026-04-23

Status: investigation and stabilization design only. No runtime behavior was
changed in this pass.

## Scope

- Target visuals:
  - `VexedTileBProp_01_Top`
  - `CustomSideDoorClosed*`
- Target behavior:
  - regular `E` open/close doors assembled through `DoorController`
- Validation method in this environment:
  - code path inspection
  - scene/prefab YAML inspection
  - inspector reference contract review
- Not validated here:
  - live Unity playmode behavior
  - editor-time scene load and click-through verification

## Executive Summary

These doors keep breaking because the live floor scenes do not currently own
regular doors through explicit door authoring data.

The live scenes instead depend on a fallback chain:

1. infer door cells from visible sprite bounds
2. create runtime `DoorController` objects from those inferred cells
3. search the scene again to guess which visible door root belongs to those
   cells
4. infer front-door vs side-door visuals from override components, parent path,
   and legacy naming

That chain is fragile because small scene edits can change the inferred cells,
change the resolved door variant, or make the runtime controller bind the wrong
visual root.

The reason "a different door breaks every time" is that the current synthesis
path globally de-duplicates overlapping cells. When one patched door footprint
slides by even one cell, whichever visual root loses the overlap contest can
silently stop producing its own door group.

## Confirmed Current-State Facts

### 1. Live floors are using visual synthesis as the primary door contract

`MainEscapeFloorAuthoring.BuildDoorGroups()` only uses explicit
`MainEscapeDoorAuthoring` when both `doorTilemap` and `doorAuthorings` resolve
successfully. Otherwise it falls back to
`MainEscapeVisualAuthoringSynthesis.BuildVisualDoorGroups()`.

Relevant code:

- `Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs:650`
- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:119`

Confirmed in live scene YAML:

- `Assets/Scenes/RMainScene_1F.unity:98961`
- `Assets/Scenes/RMainScene_2F.unity:80995`
- `Assets/Scenes/RMainScene_3F.unity:88896`
- `Assets/Scenes/RMainScene_4F.unity:64738`
- `Assets/Scenes/RMainScene_5F.unity:59266`

All five live floor scenes currently serialize:

- `doorTilemap: {fileID: 0}`
- `doorAuthorings:` with null entries
- `doorMarkersRoot: {fileID: 0}`

### 2. Legacy `Resources/Floors/MainEscape/*.prefab` still carry explicit door data

The legacy floor prefabs still serialize populated `doorAuthorings` and
non-null `doorMarkersRoot`.

Example:

- `Assets/Resources/Floors/MainEscape/1F.prefab:27128`
- `Assets/Resources/Floors/MainEscape/1F.prefab:27150`

This means the repository currently contains two different regular-door
ownership models:

- live scenes: visual synthesis fallback
- legacy floor prefabs: explicit door authoring

That split increases patch risk because tools or recovery flows can pull stale
door assumptions from the old prefab path.

### 3. Runtime door controllers are separate objects, not the visible doors

`MainEscapeFloorDirector` creates fresh runtime `DoorController` objects from
generated door groups. It does not attach `DoorController` to the visible door
art in the scene.

Relevant code:

- `Assets/Scripts/Objectives/MainEscapeFloorDirector_Doors.cs:125`
- `Assets/Scripts/Objectives/MainEscapeDoorRuntimeUtility.cs:14`

This matters because any mismatch between:

- inferred door cells
- runtime controller position
- visual-root rebinding

causes the door logic and the visible door object to drift apart.

### 4. Visual rebinding is heuristic and overlap-based

After a runtime controller is created, `MainEscapeDoorRuntimeUtility` tries to
find the matching visible door roots by comparing inferred cells back against
the scene visuals.

Relevant code:

- `Assets/Scripts/Objectives/MainEscapeDoorRuntimeUtility.cs:43`
- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:184`

This is not a direct serialized binding. It is a second pass that guesses the
best visual root from:

- exact cell match
- subset coverage
- best overlap count
- fewest extra cells
- nearest center distance

If two nearby doors project similar cells, the rebinding result can change even
when the player-facing art still looks almost the same.

### 5. Door footprint is derived from render bounds unless explicit blocker authoring exists

`MainEscapeVisualAuthoringSynthesis.AddProjectedFootprintCells()` uses render
bounds when no authoring footprint component is present.

Relevant code:

- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:418`
- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:512`

That means all of these can change the runtime door cells:

- sprite replacement
- sprite size
- pivot differences
- object scale
- added child `SpriteRenderer`
- hidden or disabled child `SpriteRenderer`
- alpha set near zero
- wrapper hierarchy changes that alter combined bounds

### 6. Front-door vs side-door behavior still depends on override-plus-heuristic resolution

Door variant resolution is:

1. explicit `MainEscapeDoorVisualVariantOverride` on root or parent
2. fallback to legacy name/path heuristic

Relevant code:

- `Assets/Scripts/Objectives/MainEscapeDoorVisualVariantOverride.cs:27`
- `Assets/Scripts/Objectives/ObjectiveLoopDemo.cs:434`

Legacy heuristic examples:

- name contains `CustomSideDoor`
- name contains `SideDoor`
- parent name contains `sidedoor`
- name contains `VexedTileBProp_01_Top`

This remains fragile because a hierarchy cleanup can still change behavior even
without touching `DoorController`.

### 7. Front doors and side doors do not have equal authoring safety

`VexedTileBProp_01_Top` is a prefab and already carries
`MainEscapeDoorVisualVariantOverride` on the prefab root.

Relevant asset:

- `Assets/Prefabs/Environment/MainEscape/Vexed/TileBSplitDoors/VexedTileBProp_01_Top.prefab:95`

`CustomSideDoorClosed*` objects in the live floor scenes are loose scene
objects. Each copied object carries its own scene-local override component.

Examples:

- `Assets/Scenes/RMainScene_5F.unity:21549`
- `Assets/Scenes/RMainScene_5F.unity:21640`

This means side doors are more likely to regress during copy, replace, cleanup,
or scene migration work because their variant safety is not centralized in a
shared prefab.

## Root Causes

### Root Cause 1. Door ownership is implicit in scene art, not explicit in door authoring

The live scenes are not declaring regular doors as door data. They are letting
the runtime infer door data from visuals.

Why this breaks often:

- visual edits are common
- door data inference is sensitive to visual edits
- many unrelated scene patches touch visuals

### Root Cause 2. The same door is discovered twice by two different heuristics

Current flow:

1. build door groups from visual footprints
2. find visual roots again from footprint overlap

This doubles the chance of mismatch. If discovery and rebinding do not resolve
the same root, the controller can be valid while the presentation is wrong.

### Root Cause 3. Global cell de-duplication makes failures move around

`BuildVisualDoorGroups()` uses one shared `seenDoorCells` set across all doors.

Relevant code:

- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:128`
- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:162`

When two doors overlap after a patch:

- the earlier door keeps the cells
- the later door loses duplicate cells
- if it loses all unique cells, that door group disappears

This is why the broken door can appear to "move" after unrelated scene edits.

### Root Cause 4. Direct-child hierarchy under `Visual/Props/Doors` and `Visual/Props/sidedoor` is a hidden contract

`BuildVisualDoorGroups()` and `FindVisualDoorRootsForCells()` only inspect the
direct children under those two container paths.

Relevant code:

- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:23`
- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:140`
- `Assets/Scripts/Objectives/MainEscapeVisualAuthoringSynthesis.cs:218`

So the real hidden rule is:

- one direct child under `Doors` or `sidedoor` must equal one door

If someone adds:

- a grouping empty object
- a clipboard holder
- a decorator parent
- a folder-like organizer object

then the runtime can interpret that whole container as one big door footprint.

### Root Cause 5. Side-door robustness depends on scene-local manual repetition

The side-door override is not embedded in a reusable prefab contract.
It is repeated per scene object.

This is a classic "works until copied by hand" setup.

### Root Cause 6. Existing tests do not protect the live scene contract

Current tests cover synthetic class behavior such as:

- variant resolution
- synthetic footprint expectations

Examples:

- `Assets/Tests/EditMode/MainEscapeDoorVariantResolutionEditModeTests.cs`
- `Assets/Tests/EditMode/MainEscapeVisualDoorFootprintEditModeTests.cs`

What is missing:

- canonical scene coverage
- direct-child hierarchy contract checks
- duplicate footprint detection in real scenes
- per-scene count parity between visual doors and generated groups
- missing side-door override detection in live floor scenes

## High-Risk Failure Scenarios

### Scenario A. A side door is duplicated but the override is not preserved

Result:

- variant falls back to name/path heuristic
- measured footprint can change if the new parent path differs
- side door may bind front-door prefab visuals or wrong cells

### Scenario B. A helper parent is inserted under `Visual/Props/sidedoor`

Result:

- the helper becomes the direct child treated as one door root
- combined render bounds can merge multiple doors into one footprint
- one or more runtime doors disappear or bind incorrectly

### Scenario C. Sprite or scale changes alter bounds by a fraction

Result:

- inferred anchor cell moves
- overlap ranking changes
- another nearby door loses the shared-cell contest

### Scenario D. A recovery or tooling pass uses legacy `Resources/Floors/MainEscape/*.prefab`

Result:

- old `doorAuthorings` / `doorMarkersRoot` assumptions can be restored into
  today’s live scenes
- scene and prefab door contracts drift further apart

### Scenario E. Parent names or root paths are cleaned up

Result:

- `Doors` / `sidedoor` path discovery stops finding the visuals
- side-door heuristic can stop resolving
- runtime door controller falls back to generated marker visuals

## Recommended Fix Direction

This project should stop treating regular door visuals as the source from which
door gameplay data is inferred.

The safest fix is not a broader `DoorController` rewrite. The safest fix is to
move regular doors to explicit authored door data while keeping the current
runtime open/close gameplay authority.

## Phased Design

### Phase 1. Add explicit regular-door authoring on the visible door root

Create one small authoring component for regular doors and place it directly on
the visible door root or prefab root.

Suggested responsibility:

- explicit door variant
- explicit door cells or explicit footprint mode
- optional stable door id
- optional flag for main gate / locked door usage

Suggested intent:

- visible door object owns its identity
- runtime controller still owns open/close state
- runtime no longer guesses identity from bounds and names

Important note:

- Keep this as composition, not inheritance-heavy behavior
- If an interface is introduced for resolver/reader use, keep the project rule
  and name it with an `I` prefix

### Phase 2. Make build order explicit

Change `BuildDoorGroups()` priority to:

1. explicit regular-door authoring components in the live scene
2. legacy `MainEscapeDoorAuthoring` only for backward compatibility
3. visual synthesis fallback only as temporary migration support

This keeps current scenes working during rollout, but removes visual synthesis
as the main live-floor contract.

### Phase 3. Bind runtime controller to explicit authoring, not cell overlap search

`MainEscapeDoorRuntimeUtility.BindAuthoredVisualRoots()` should bind through the
explicit authoring component or stable id.

Do not keep "find the best overlap match" as the steady-state path for live
floors.

### Phase 4. Prefabize the side-door family

`CustomSideDoorClosed*` should stop being loose repeated scene objects.

Preferred direction:

- create one side-door prefab root that already contains the explicit authoring
  component and variant
- place prefab instances in scenes
- keep direct scene placement, but from a stable prefab source

This matches the project preference for authored prefab/scene placement over
runtime regeneration.

### Phase 5. Add static validation for canonical scenes

Add an editor/static validator that checks:

- every regular door visual has explicit authoring
- no duplicate door ids
- no duplicate occupied door cells
- every authored door root lives under the expected live floor hierarchy
- every generated runtime door group count matches authored door count
- every side door uses the approved prefab or approved authoring component

This validator should be able to fail before playmode and should not require
Unity runtime simulation to be useful.

### Phase 6. Retire legacy heuristics after coverage is complete

After all live floors are migrated:

- remove `ResolveLegacyHeuristic()` from the live path
- stop using render-bounds door synthesis as the normal route
- keep fallback only in migration-only tooling if absolutely needed

## Lowest-Risk Short-Term Hardening

If you want a minimal-risk stabilization before a fuller migration, do these
first:

1. Keep `DoorController` gameplay authority as-is.
2. Add explicit regular-door authoring to each live visible door.
3. Convert side doors to prefab instances.
4. Add a validator that fails on:
   - missing authoring
   - hierarchy nesting under `Doors` / `sidedoor`
   - duplicate cells
   - missing side-door variant
5. Stop accepting new door placements that rely only on visual synthesis.

This gives the highest stability gain for the smallest runtime behavior change.

## What Not To Do

- Do not add more name heuristics to fix one more special case.
- Do not rely on sprite bounds as the long-term authoritative door contract.
- Do not keep loose side-door objects as the primary reusable authoring asset.
- Do not migrate by deleting the current fallback first. Add explicit authoring
  coverage, validate, then remove fallback from the live path.

## Recommended Implementation Order

1. Add explicit regular-door authoring component.
2. Add scene validator.
3. Migrate `CustomSideDoorClosed*` to prefab-backed authored doors.
4. Migrate `VexedTileBProp_01_Top` runtime binding to explicit authoring too,
   even though its prefab override is already safer.
5. Flip build/bind order to explicit authoring first.
6. Remove live-floor dependence on visual synthesis fallback.

## Conclusion

The repeated regressions are not mainly because the open/close logic is weak.
They happen because the live floors currently treat regular doors as inferred
visual artifacts rather than explicitly authored gameplay objects.

To make these doors stop breaking, the project should keep the current
`DoorController` gameplay state path, but replace visual inference with explicit
door authoring placed on the door prefab/scene objects themselves.

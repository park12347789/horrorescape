# ADR-0005: Route Graph Assets As The Routing Bridge

- Status: Accepted
- Date: 2026-04-25
- Decision Makers: Project maintainers
- Related Docs:
  - `Docs/design/SceneContractDataSeparationPlan.md`
  - `Docs/architecture/ADR-0001-live-authored-scene-chain.md`
  - `Docs/architecture/ADR-0002-controlled-resources-loading.md`
- Affected Areas:
  - run routing
  - chapter selection data
  - scene contract data
  - Unity ScriptableObject assets

## Context

The live hospital loop still needs to remain stable, but future hospital,
backrooms, pool, and branching content should not be hardcoded through scene
names or descending floor numbers.

`ChapterDefinition` and `RouteGraphDefinition` now exist as loose data
contracts. The next step is making the current hospital loop read an actual
data asset without requiring scene edits, Build Settings changes, Addressables,
or a broad `Resources` migration.

## Options Considered

1. Put every chapter and route graph under `Assets/Resources` and load them by
   string path.
2. Add route graph references to `MainEscapeRuntimeSettings`.
3. Keep route graph assets outside `Resources`, and let the existing canonical
   `RRunRoutingSettings` resource explicitly reference the active chapter.

## Decision

Use option 3.

`RRunRoutingSettings` remains the current canonical bridge resource because the
live loop already loads it safely. It now holds an explicit `ChapterDefinition`
reference. The hospital `ChapterDefinition` points to a hospital
`RouteGraphDefinition` stored under `Assets/Data/MainEscape/Contracts`.

The run session reads the data asset first and falls back to the existing
serialized floor route and runtime-settings alignment data when the asset is
missing or invalid.

The route graph bridge is also the first authority for sessionless route
membership, floor residency checks, and scene-resident authored floor build
eligibility. When an explicit run session or configured route exists, a route
miss must not be reauthorized by canonical scene names; legacy scene-name
fallback exists only for direct-play scenes without active route data.

ScriptableObject contract classes are kept in class-name-matched script files
so Unity asset imports can resolve their `m_Script` references without relying
on multi-class file metadata.

## Consequences

### Benefits

- The current authored hospital loop stays intact.
- New route data is explicit and inspectable in Unity.
- No new `Resources.Load` path is introduced for chapter or route graph assets.
- Scene internals remain flexible; the contract is data-level, not hierarchy
  level.

### Tradeoffs

- The current bridge still converts hospital route graph nodes back into floor
  numbers for legacy floor handoff.
- Hospital route graph nodes must keep readable floor ids such as
  `hospital_5f` or `5f` until the floor-number bridge is retired.
- A stale `RRunRoutingSettings` reference can hide the new data asset path.

### Follow-Up

- Keep route graph validation active for node ids, duplicate nodes, duplicate
  scene paths, missing edge targets, and hospital-chain scene path consistency.
- Migrate serialized scene fallback fields only through a documented scene-data
  pass. `RMainEscape_ElevatorTransition` still carries a direct-play fallback
  to `RMainScene_5F`; that value is compatibility data, not the route authority.
- Move main menu chapter selection to read `ChapterDefinition` data when the
  menu chapter-start pass begins.
- Retire floor-number bridge logic after scene entry/exit contracts own routing
  end to end.

## Validation

- EditMode tests should confirm the hospital chapter asset references a valid
  route graph.
- EditMode tests should confirm `RRunRoutingSettings` references the hospital
  chapter asset.
- Runtime compile checks must keep passing.
- `RRunSessionController.SelectChapter` and `StartChapterAndLoadGameplay`
  should continue to accept explicit chapter data while preserving the existing
  default start button path.
- Sessionless direct-play systems should prefer the default chapter route graph
  before runtime-settings fallback routes.
- Route misses with active run/configured route data should fail closed instead
  of falling back to canonical scene-name authorization.
- Manual verification should still run the authored loop:
  `RMainEscape_Lobby -> RMainScene_5F -> RMainScene_4F -> RMainScene_3F -> RMainScene_2F -> RMainScene_1F -> RMainEscape_Lobby`.

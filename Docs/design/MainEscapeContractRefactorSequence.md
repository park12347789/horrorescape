# MainEscape Contract Refactor Sequence

Date: 2026-04-23

Status: design plan only. This document describes a high-intensity refactor
sequence for collapsing hidden scene/runtime contracts. It does not authorize
immediate scene, prefab, ProjectSettings, package, or runtime rewrites by
itself.

Related docs:

- `Docs/design/SceneIndirectionContractAudit.md`
- `Docs/design/RunRuntimeStructureRefactor.md`
- `Docs/design/MainEscapeLiveLoopSystem.md`
- `Docs/SystemAuditCleanupPlan.md`
- `Docs/architecture/ADR-0001-live-authored-scene-chain.md`
- `Docs/architecture/ADR-0002-controlled-resources-loading.md`
- `Docs/architecture/ADR-0003-defer-runtime-asmdef-and-namespace-rollout.md`

## Goal

Collapse the current hidden contract web into a smaller set of explicit
runtime boundaries without breaking the playable loop.

Build order is:

`RMainEscape_Lobby -> RMainEscape_tuto? -> RMainScene_5F -> 4F -> 3F -> 2F -> 1F -> RMainEscape_ElevatorTransition`

Runtime route shape is:

`RMainEscape_Lobby -> optional tutorial support -> fresh 5F run -> floor handoffs using RMainEscape_ElevatorTransition -> RMainEscape_Lobby`

The refactor should make the live authored floor scenes the clear play source,
move scattered hierarchy reads behind a snapshot boundary, quarantine editor
mutators, and retire stale legacy floor-prefab assumptions only after the live
loop stays green.

## Core Decision

This should be treated as one coordinated refactor program, not a pile of small
local fixes.

The safe high-intensity path is:

1. Lock route/source-of-truth rules.
2. Replace generation-style validation with read-only scene contract reporting.
3. Introduce a shadow-only `FloorAuthoringSnapshot`.
4. Move runtime consumers to the snapshot one domain at a time.
5. Normalize live scene data from 5F downward.
6. Quarantine and then retire legacy prefab/editor mutation paths.
7. Finish service/UI/fog/input cleanup after gameplay ownership is stable.

Do not start by deleting fallbacks or rewriting floor scenes. First make the
current contract visible enough that each removal has a gate.

## Source Labels

Every report, validator, migration, or runtime reader should name which source
it is using.

| Label | Meaning |
|---|---|
| `LiveScene` | Current shipped scene YAML under `Assets/Scenes/RMainScene_*.unity`. |
| `SupportScene` | Tutorial and elevator transition scenes with their own ownership mode. |
| `LegacyResourcePrefab` | `Assets/Resources/Floors/MainEscape/*.prefab`; archival or migration reference, not live play truth. |
| `RuntimeAsset` | Settings, routing, progression, prefab catalog, tile, audio, art, and shader assets. |
| `RuntimeService` | Scene services such as grid, fog, noise, audio, interaction, session, and HUD binding. |
| `EditorMutation` | Rebuilders, migration tools, recovery runners, cache tools, and `ExecuteAlways` components that can save or mutate scene/prefab state. |
| `PersistentState` | `PlayerPrefs`, `SessionState`, `EditorPrefs`, temp files, and one-shot request stores. |

Rule:

- A tool that can save a scene or prefab is recovery equipment, not normal
  iteration, until this refactor is complete.

## Scene-Visible Rule

The unification target is not "make every floor satisfy the old generated-map
quota rules."

The target is:

- The authored scene is the play source.
- What is visible or intentionally authored in the scene should be classified
  and consumed directly.
- Validators should report scene ownership and hidden runtime indirection; they
  should not pretend that marker quotas are map-generation requirements.
- Quotas may remain as tuning data only where runtime randomization is still an
  explicit, documented layer.
- Fixed authored pickups are fixed pickups, not random support markers that
  happened to be short.
- `Pickup_Flashlight.prefab` remains a separate flashlight pickup prefab. It is
  not part of the 5F fixed support pickup contract and should not be folded
  into the random support layer.
- Marker-driven random spawning is owned by scene-authored marker placement
  managers. A manager entry says which prefab is placed, which marker pool it
  can use, and how many instances it may create.

This means `Validate Floor Marker Counts` is not the future rule boundary. It
has been retired from the editor menu; use `Report Floor Scene Contracts` for
authored scene contract reporting.

## Local Ownership And Manager Decomposition Rule

Each gameplay feature should read the scene data it owns through its own
domain boundary. Do not keep adding behavior to a single floor script, global
validator, or catch-all manager just because that object already has access to
the scene.

Target rule:

- A feature owns the authoring data it needs to run.
- The owning feature should inspect its own markers, bindings, pickups,
  hazards, or presentation objects through a typed component or snapshot
  section.
- A high-level director may coordinate order, seed, lifecycle, and error
  reporting, but should not contain the placement, validation, balancing, and
  scene-reading logic for every domain.
- If a manager starts owning multiple unrelated functions, split it into
  smaller child/domain managers before migrating more behavior into it.
- A child/domain manager should have one clear responsibility, such as support
  pickup placement, key placement, trap placement, enemy placement, fog
  binding, exit binding, or UI binding.
- Cross-domain decisions should be expressed through small data contracts or
  reports, not by one manager reaching into another domain's internals.
- Validators should ask each domain for its report instead of reimplementing
  all domain rules in one monolithic validator.

Practical application:

- `RFloorDirector` should remain a lifecycle coordinator. It may call child
  managers in a known order, but it should not become the owner of item, trap,
  enemy, exit, fog, and UI rules.
- `RSceneMarkerPlacementManager` is a scene-authored placement executor, not a
  new universal floor brain. If support pickups, traps, and enemies need
  different balancing rules, split them into separate managers or rule
  providers instead of growing one component endlessly.
- Future `FloorAuthoringSnapshot` work should be grouped by domain sections so
  each runtime system reads the part it owns rather than browsing the whole
  scene hierarchy indirectly through a shared god object.

## Scene Marker Placement Manager Rule

Marker-based random generation should not live as a hidden global floor quota.

Target rule:

- Each scene may author one or more `RSceneMarkerPlacementManager` components.
- Each manager has placement rules.
- Each rule owns:
  - prefab
  - marker root or explicit marker list
  - count
  - seed offset
  - whether marker rotation is used
- Runtime only executes the manager's authored declarations.
- If no manager rule exists for a category, runtime should not invent that
  category from generic floor quota data.
- Fixed authored pickups, such as the 5F starter bottles, batteries, and
  medkits, are not manager-randomized items unless a designer explicitly moves
  them into a manager rule.

Initial implementation surface:

- `RSceneMarkerPlacementManager`
- `RFloorDirector.ApplySceneMarkerPlacementManagers`
- `RFloorItemPlacementRuntime`
- `RRunFloorStateApplier`
- `RPlacementMarkerMigrationTools`

Current code-ready state:

- Phase 3.5 has partial code gates for support item manager ownership.
- Scene migration is not complete; no floor should be treated as migrated only
  because the code path exists.
- Support item manager ownership requires an active and enabled
  `RSceneMarkerPlacementManager`, a `SupportPickup` rule, a valid prefab,
  a positive count, at least one marker candidate, resolved support item ids, and
  a serialized `runtimeRoot`.
- The current authoring target is `InteractiveProps/00_Pickups` for
  `runtimeRoot` and `AuthoringMarkers/ItemPlacementMarkers` for the marker
  source. The manager's serialized `runtimeRoot` reference is the
  apply/readiness reference.
- When support manager ownership is present, only the legacy support item plan
  is suppressed or filtered. Key placement remains active and must keep using
  the key placement contract.
- Support manager suppression stays scoped to the legacy support item plan.
  It must not absorb fixed starter pickups or the separate flashlight pickup
  prefab into the random support layer.
- The 5F fixed starter pickups remain the seven direct authored pickups under
  `CoverProps` and must not be moved into `InteractiveProps/00_Pickups` or
  `AuthoringMarkers/ItemPlacementMarkers`.
- `Report Floor Scene Contracts` now has a read-only surface for support
  manager candidates, prefab catalog availability, fixed pickup state, and
  manager eligibility. It should make `RSceneMarkerPlacementManager`
  `status=no-manager`, `inactive`, `missing runtimeRoot`,
  `missing SupportPickup rule/input`, `no support item ids`, and `ready`
  visible at a glance, along with support marker pool `populated=`/`empty=`
  state and `legacyQuotaBalance=shortage`/`surplus`/`matched`/
  `empty-no-legacy-quota`.
- Migration tools and the run floor state applier protect fixed pickups that
  suppress runtime-managed pickup replacement.

Current limitation:

- No live scene has been migrated to manager-authored placement yet. Existing
  item, trap, and enemy planners still exist until their responsibilities are
  retired or converted.

## Gate Order

These gates should be created or strengthened before destructive migration.

### Gate 1 - Contract Report

Purpose:

- Produce a read-only report for live floors, support scenes, legacy prefabs,
  runtime assets, and editor state.

Must report:

- scene route and expected floor number
- source label for each floor and support scene
- root names and missing required roots
- marker root counts versus serialized array counts, as observation rather than
  pass/fail generation quota
- direct pickup count and direct pickup item ids
- runtime-managed item plan, when it still exists
- scene marker placement managers and their rules
- chaser/stalker source
- patrol/sentry source
- vent route explicit connection status
- door authoring versus synthesized door groups
- final exit versus stairs/elevator exit shape
- fog overlay sorting order and high-order visible sprites
- legacy floor-prefab drift
- `PlayerPrefs`, `SessionState`, `EditorPrefs`, and temp-file state surfaces

Exit criteria:

- The report can be run without saving scenes.
- It marks `LiveScene` and `LegacyResourcePrefab` separately.
- It does not treat a fallback pass as the same thing as explicit authoring.
- It does not fail a floor only because the authored marker pool does not look
  like a generated-map quota table.

### Gate 2 - Route And Support Scene Alignment

Purpose:

- Lock the route spine before floor content changes.

Must verify:

- lobby scene remains the public entry point
- lobby `RRunSessionController` route stays aligned with
  `MainEscapeRuntimeSettings` and `RRunRoutingSettings`
- Build Settings order is lobby, tutorial, 5F, 4F, 3F, 2F, 1F, elevator
  transition
- tutorial is not part of `RFloorSceneEntry`
- elevator transition uses request-store handoff and fallback route explicitly
- docs that mention build index order are either current or clearly historical

Exit criteria:

- Route checks stay green before any floor scene migration starts.

### Gate 3 - Legacy Prefab Drift

Purpose:

- Prevent tools from restoring stale `Resources/Floors/MainEscape/*.prefab`
  contracts into live scenes.

Must verify:

- live scenes are current play source
- legacy prefabs are marked archival/migration-only
- per-floor drift summary is visible
- prefab-only migration commands are not in any automatic workflow

Exit criteria:

- No refactor step uses `LegacyResourcePrefab` as play truth.
- Any tool reading legacy prefabs says so in logs or reports.

### Gate 4 - Item And Key Ownership

Purpose:

- Catch the mixed direct-pickup and marker-driven item path before item cleanup,
  without treating random marker generation as the floor rule.

Must verify:

- support markers, key markers, quotas, and direct pickups per floor
- support-item quota and marker-pool state as runtime support manager
  transition data, not fixed-starter-missing evidence
- 5F runtime random target of `min(18, 11) = 11`, using the `9/6/3` support
  quota ratio clamped and scaled to the active support marker cap
- 5F direct battery, bottle, and medkit suppression behavior
- 5F fixed starter item set:
  - 3 glass bottles
  - 2 flashlight batteries
  - 2 medkits
- duplicate runtime item risk
- key interaction path when key markers exist
- whether random support placement is enabled for that floor at all
- whether item randomization is still driven by legacy floor quota or by a
  scene marker placement manager

Exit criteria:

- A floor cannot silently contain both direct support pickups and runtime
  support pickups without the report calling it out.
- 5F fixed starter items are reported as fixed authored pickups, not random
  support-item placements.
- `Pickup_Flashlight.prefab` is reported separately from the 5F fixed support
  pickup set.
- If random support placement remains enabled, it is reported as an explicit
  runtime layer on top of the authored scene, not as the scene's source of truth.
- A manager-authored random item rule is reported separately from legacy
  `MainEscapeFloorAuthoring` support quota.

### Gate 5 - Chaser, Enemy, And Vent

Purpose:

- Catch gameplay drift that can look playable but change enemy behavior.

Must verify:

- shared enemy pool all-or-nothing behavior
- patrol route fallback behavior
- chaser marker versus legacy stalker marker source
- 5F chaser quota with no active chaser marker
- vent route explicit connections or accepted naming heuristic
- authored-scene vent routes do not collapse to empty graphs

Exit criteria:

- Every floor has an explicit enemy source report.
- Every floor has an explicit vent graph report.

### Gate 6 - Full Live Loop

Purpose:

- Confirm the actual player route still works.

Must verify:

- lobby start
- optional tutorial fresh-start handoff
- 5F through 1F descent
- key gate/direct exit/final exit behavior
- elevator transition request and fallback
- failure, retry, clear, and return-to-lobby flow

Exit criteria:

- EditMode contract tests, PlayMode loop tests, and one manual lobby-to-1F pass
  are green after each migration wave.

## Refactor Sequence

### Phase 0 - Lockdown And ADR Prep

Intent:

- Make the refactor deliberate and reversible.

Work:

- Create or update an ADR before implementation starts, because this touches
  runtime ownership, scene authoring, validation, and editor tooling.
- Keep the current branch clean enough to identify unrelated scene/prefab
  changes.
- Disable or quarantine any auto-run recovery/editor mutation behavior.
- Establish that open dirty scenes are not trusted as validated state.
- Record current Build Settings and route asset state.

Do not:

- Rewrite scenes.
- Run marker migration tools.
- Delete legacy prefabs.
- Move runtime code into asmdefs.

Rollback checkpoint:

- No behavior change has happened yet. Revert is documentation/tool-state only.

### Phase 1 - Read-Only Contract Report

Intent:

- Turn hidden contracts into a repeatable report and retire generation-style
  marker validation as the cleanup authority.

Work:

- Add a read-only contract report path beside or inside
  `MainEscapeRuntimeValidator`.
- Report source labels and provenance for every floor domain.
- Add live-vs-legacy floor prefab drift reporting.
- Keep `Validate Floor Marker Counts` retired from the editor menu. Generated
  quota pass/fail rules are not the authored scene contract authority.
- Report 5F marker counts and fixed pickups, but do not require 5F to mimic a
  generated-map quota shape.
- Add tutorial route alignment coverage.
- Add item duplicate and key-interaction warnings that do not vanish just
  because marker roots exist.

Exit gate:

- Gate 1 and Gate 2 pass.
- The report can explain current known drift without saving any asset.

Rollback checkpoint:

- Remove the report code only; runtime remains unchanged.

### Phase 2 - Editor Mutation Quarantine

Intent:

- Stop old tools from reintroducing stale contracts while runtime is moving.

Work:

- Reclassify scene/prefab-saving tools as recovery or legacy migration.
- Separate live-scene reference cache from legacy prefab backfill in naming and
  logs.
- Keep lobby and elevator rebuilders as explicit one-shot recovery paths.
- Keep vent route batch tools manual and outside recovery auto-chains.
- Make any legacy prefab read/write path report `LegacyResourcePrefab`.

Exit gate:

- No automatic chain can mutate live scenes or floor prefabs.
- Manual tools state whether they target `LiveScene` or
  `LegacyResourcePrefab`.

Rollback checkpoint:

- Restore menu visibility or labels if needed. Runtime still unchanged.

### Phase 3 - Shadow-Only `FloorAuthoringSnapshot`

Intent:

- Name the floor authoring boundary before changing consumers.

Work:

- Introduce a snapshot model that records values and provenance.
- Snapshot should cover:
  - floor identity
  - tilemaps and overlays
  - support item markers
  - key markers
  - direct pickups
  - shared enemy markers
  - chaser markers
  - legacy stalker marker
  - danger markers and trap quota
  - patrol and sentry fallback roots
  - vent route data
  - door groups, main door cells, and synthesis source
  - exits, direct elevator, stairs, and final exit
  - prop and movement blockers
- Keep all existing consumers on live `MainEscapeFloorAuthoring` reads.
- Add comparison tests or logs that snapshot output matches current live reads.

Exit gate:

- Snapshot and current direct reads agree on all floors, including known fallback
  and drift cases.

Rollback checkpoint:

- Snapshot code is unused by runtime consumers. Remove it if wrong.

### Phase 3.5 - Scene Marker Placement Managers

Intent:

- Move marker-based random spawning out of global floor quota assumptions and
  into scene-authored manager declarations.

Work:

- Add one manager per floor scene or per authored floor shell when ready. The
  live `RMainScene_1F~5F` scenes now place the support-pickup manager under
  `MainEscapeFloorAuthoring`; prefab baselines and non-support domains are
  still separate follow-up work.
- Convert random support item placement first:
  - manager rule for glass bottle prefab
  - manager rule for flashlight battery prefab
  - manager rule for medkit prefab
  - count authored per scene
  - marker root authored per scene
- A support manager only owns random support placement when it is active and
  enabled and has at least one eligible `SupportPickup` rule with prefab,
  positive count, marker candidates, resolved support item ids, and a serialized
  `runtimeRoot`.
- The `runtimeRoot` may be the authored `00_Pickups` object, but ownership is
  tied to the serialized reference, not to the object name alone.
- Keep 5F fixed starter pickups outside manager randomization.
- Keep key placement outside support-manager suppression. Manager ownership of
  support pickups must not disable or filter the key placement path.
- Use `Report Floor Scene Contracts` to verify support manager candidates,
  prefab catalog availability, fixed pickups, and manager eligibility before
  migrating a scene.
- Convert trap and enemy randomization only after item manager behavior is
  proven.
- Remove or zero legacy random support quotas only after manager rules reproduce
  intended floor behavior.
- Do not let support-manager suppression absorb fixed starter pickups or the
  separate flashlight pickup prefab; it stays scoped to the legacy support item
  plan.

Exit gate:

- A floor's random support items are created only by its manager rules.
- Legacy support quota no longer creates hidden extra support items on that
  floor, while key placement remains unchanged.
- Fixed authored pickups that suppress runtime-managed replacement survive
  migration tools and state application.

Rollback checkpoint:

- Disable or remove the manager component and restore the previous planner path
  for that floor.

### Phase 4 - Move Gameplay Readers To Snapshot

Intent:

- Move the most consequential gameplay consumers behind one stable boundary.

Order:

1. Item and key placement.
2. Trap placement.
3. Enemy/chaser planning.
4. Vent route consumption.
5. Door group and main-door cell reading.
6. Exit resolution.

Rules:

- Keep the old live-authoring path available until each domain has passed its
  gate.
- Preserve current fallback semantics first; tighten them only in later phases.
- Door snapshot data must include synthesized door groups, not only
  `doorAuthorings`.
- 1F final exit must remain a separate exit shape.

Exit gate:

- Gate 4 and Gate 5 pass after each consumer move.
- Full loop smoke stays green after doors and exits move.

Rollback checkpoint:

- Each consumer flip should be individually reversible.

### Phase 5 - Normalize Live Scene Data From 5F Down

Intent:

- Fix the actual authored data after the report and snapshot can see it.
- Dismantle 5F tutorial-foundation assumptions while preserving a fixed minimum
  survival kit for the main route.

Order:

1. `RMainScene_5F`
2. `RMainScene_4F`
3. `RMainScene_3F`
4. `RMainScene_2F`
5. `RMainScene_1F`

Work:

- Decide the official 5F chaser policy: intentional fallback or authored chaser
  marker.
- Decide the official direct pickup policy:
  - 5F keeps a fixed starter item set, not a random support pool, for the
    minimum early-run pickup guarantee.
  - The current intended fixed set is 3 glass bottles, 2 flashlight batteries,
    and 2 medkits.
  - These pickups should be excluded from runtime-managed support randomization
    and reported separately from marker-driven support items.
  - If random support placement remains on 5F, it must be an explicit secondary
    runtime layer. It must not be inferred from a marker-count shortage.
  - Tutorial teaching/setup logic should move to `RMainEscape_tuto`; 5F should
    only own main-route fixed pickups and floor gameplay.
- Normalize serialized arrays against root scans.
- Normalize chaser/stalker fields so recache operations do not erase intended
  chaser quota.
- Normalize vent route graph shape and explicit connections.
- Normalize door visual/synthesis policy.
- Propagate only confirmed 5F structural decisions to lower floors.
- Keep 1F final exit separate from upper-floor exits.

5F fixed starter item policy:

- Treat the current 5F fixed starters as the seven direct authored scene
  pickups under `CoverProps`.
- Do not move them into the `InteractiveProps/00_Pickups` random support root
  or the `AuthoringMarkers/ItemPlacementMarkers` marker flow.
- Do not rely on scene-object names as the future contract; identify them by
  pickup component, item id, source label, and fixed-starter classification.
- Keep `suppressRuntimeManagedPickupReplacement` semantics or an equivalent
  explicit fixed-item flag until the runtime item planner can no longer replace
  or duplicate them.
- The item planner should not decide that these pickups exist. It should see
  them as authored scene pickups first. Any remaining random support placement
  must be opt-in and reported as runtime-managed.
- The report should fail or warn if these fixed pickups are missing, duplicated
  unintentionally, or counted as random support items.

Exit gate:

- Contract report shows no accidental source disagreement.
- Full loop stays green after each floor wave.

Rollback checkpoint:

- Revert one floor scene at a time, not the whole program.

### Phase 6 - Retire Legacy Fallbacks And Prefab Paths

Intent:

- Remove old recovery paths after the new source of truth is proven.

Candidate retirements:

- legacy floor prefab play fallback
- prefab marker seeding for live assumptions
- legacy pickup root fallback
- legacy stalker marker quota capture
- patrol/sentry fallback roots when modern marker roots are complete
- validator skips that treat marker roots as proof of correctness
- compatibility stubs only after generated references no longer need them

Rules:

- Retire live-scene legacy paths before deleting archival assets.
- Keep `Resources/Floors/MainEscape/*.prefab` until no runtime, validator,
  editor, or recovery path treats them as live.
- Do not start an Addressables migration as part of this phase.

Exit gate:

- Gate 1 through Gate 6 pass without legacy live fallbacks.

Rollback checkpoint:

- Legacy assets still exist until the last cleanup wave.

### Phase 7 - Service, UI, Input, And Rendering Cleanup

Intent:

- Clean visible service bindings after core floor ownership is stable.

Work:

- Reduce broad `Find*` player discovery where snapshot/composition can provide
  explicit runtime bindings.
- Keep fog and UI fallback semantics stable until floor readers are done.
- Decide whether modal input should use Input System `UI` actions instead of
  raw keyboard polling.
- Decide whether fog sorting order should become a documented gameplay layer or
  be adjusted.
- Decide whether post-processing volume data should be enabled or removed from
  expectations.
- Keep package, URP, and asmdef boundaries unchanged unless a separate ADR says
  otherwise.

Exit gate:

- HUD, fog, noise, audio, and modal behavior all bind through explicit owners or
  documented service fallbacks.

Rollback checkpoint:

- Service cleanup is separate from floor data. Revert service waves independently.

### Phase 8 - Documentation And Tool Surface Closure

Intent:

- Leave the next maintainer with one current contract map.

Work:

- Update `MainEscapeAuthoringGuide`.
- Update `MainEscapeLiveLoopSystem`.
- Update `SystemAuditCleanupPlan`.
- Update stale build-order docs.
- Mark historical docs as history when they no longer describe current state.
- Update or add ADRs for:
  - snapshot as runtime boundary
  - legacy floor prefab retirement
  - any package/loading strategy change
  - any broad asmdef or namespace change

Exit gate:

- Current docs, validators, and tests agree on the same source labels and scene
  route.

## Work Packages

| Package | Main files or areas | Can run parallel? | Must finish before |
|---|---|---:|---|
| Contract report | `MainEscapeRuntimeValidator`, tests, audit docs | Yes | Any destructive migration |
| Mutation quarantine | editor rebuild/migration/recovery tools | Yes | Scene data normalization |
| Snapshot model | `MainEscapeFloorAuthoring`, new snapshot types/tests | Partly | Runtime consumer flips |
| Item/trap migration | `RFloorItemPlacementRuntime`, `RFloorTrapPlacementRuntime`, validators | No | Enemy/door cleanup |
| Enemy/vent migration | `RFloorDirector`, encounter planners, vent route utilities | No | Door/exit cleanup |
| Door/exit migration | door synthesis, `RAuthoredExitReferenceResolver`, final exit | No | Legacy fallback retirement |
| Live scene normalization | 5F, 4F, 3F, 2F, 1F scene YAML | No | Legacy fallback retirement |
| Service/UI cleanup | fog, HUD, input, audio/noise/player discovery | Yes, after floor readers | Final polish |
| Docs/ADR closure | `Docs`, architecture ADRs | Yes | Merge/ship |

## Risk Register

| Risk | Severity | Mitigation |
|---|---|---|
| Legacy floor prefabs reintroduce stale marker/door contracts | High | Label as `LegacyResourcePrefab`, quarantine tools, drift report before edits. |
| 5F chaser fallback becomes accidental behavior change | High | Decide policy explicitly before lower-floor propagation. |
| Direct pickups plus runtime item plan create duplicate pickups | High | Item duplicate gate before item reader flip. |
| Door synthesis changes while `doorAuthorings` arrays are empty | High | Snapshot synthesized door groups and keep old path until per-floor parity. |
| 1F final exit gets flattened into generic exit flow | High | Keep final exit as its own exit shape in snapshot and tests. |
| Vent route root exists but graph is empty | Medium | Report explicit connection status and naming-heuristic usage. |
| Editor tool saves scenes/prefabs during normal iteration | High | Treat saving tools as recovery equipment. |
| Validator checks saved scene while open scene is dirty | Medium | Dirty-scene preflight and explicit saved-state reporting. |
| UI/fog cleanup causes visible blank or stale binding | Medium | Move service cleanup after gameplay readers and keep fallback parity first. |

## Stop Conditions

Pause the refactor if any of these happen:

- route gate fails
- contract report cannot tell live scene from legacy prefab source
- snapshot output cannot match current live reads
- 5F fails after item or chaser migration
- 1F final exit changes behavior
- a scene/prefab-saving editor tool runs unexpectedly
- full loop cannot return to lobby after a wave

## Recommended First Implementation Wave

The first real code wave should be conservative but high leverage:

1. Add a read-only contract report.
2. Add source labels to validator output.
3. Add live-versus-legacy floor prefab drift checks.
4. Retire or demote generation-style marker count validation.
5. Add 5F fixed starter item reporting for 3 bottles, 2 batteries, and 2
   medkits.
6. Add item duplicate and key interaction warnings that do not disappear when
   marker roots exist.
7. Add tutorial route alignment coverage.
8. Quarantine scene/prefab-saving editor tools into explicit recovery or legacy
   menus.

Only after that should the `FloorAuthoringSnapshot` implementation begin.

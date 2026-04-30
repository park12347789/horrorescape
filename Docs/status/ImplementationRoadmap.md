# MainEscape Implementation Roadmap

## Current State

The project has moved from the old prototype-scene setup to a floor-chain playable loop centered on:

- `Assets/Scenes/RMainEscape_Lobby.unity`
- `Assets/Scenes/RMainEscape_tuto.unity` as an optional support scene
- `Assets/Scenes/RMainScene_5F.unity`
- `Assets/Scenes/RMainScene_4F.unity`
- `Assets/Scenes/RMainScene_3F.unity`
- `Assets/Scenes/RMainScene_2F.unity`
- `Assets/Scenes/RMainScene_1F.unity`
- `Assets/Scenes/RMainEscape_ElevatorTransition.unity` as an interstitial support scene

The current priority is not broad feature expansion. The priority is to keep the authored `R` loop readable, stable, easy to iterate on, and clearly framed by the lobby-to-floor-chain-to-lobby loop.

The current status source of truth is:

- `Docs/status/MainEscapeCurrentState.md`

## Decision Baseline

This roadmap should be read against the current accepted architecture
decisions:

- `Docs/architecture/ADR-0001-live-authored-scene-chain.md`
  - the authored lobby/floor route is the playable content baseline, with support scenes outside floor arrays
- `Docs/architecture/ADR-0005-route-graph-assets-as-routing-bridge.md`
  - `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition` is the route-data bridge/first authority
- `Docs/architecture/ADR-0002-controlled-resources-loading.md`
  - current `Resources` loading remains valid, but should not expand casually
- `Docs/architecture/ADR-0003-defer-runtime-asmdef-and-namespace-rollout.md`
  - broad structure-only rollout is intentionally deferred
- `Docs/status/MainEscapeCurrentState.md`
  - current route, Build Settings, and cleanup baseline
- `Docs/SystemAuditCleanupPlan.md`
  - active cleanup stages and watchpoints

If a roadmap item starts requiring a different baseline, update the relevant ADR
or add a new one instead of letting the roadmap become the de facto policy
layer.

## Execution Rule

Prioritize tasks that improve the live loop without broadening the migration
surface at the same time. For the current project phase, behavior validation and
readability polish beat structure-only churn.

## Completed Foundations

- `RMainEscape_Lobby` is the build entry scene.
- `RMainScene_5F -> 1F` is the live gameplay scene chain.
- The live authored scene chain is the playable content baseline. Route data is owned first by the route graph bridge, while `MainEscapeRuntimeSettings` plus Build Settings stay aligned to the lobby, optional tutorial support scene, `RMainScene_5F~1F` floor route, elevator transition support scene, and return-to-lobby flow.
- Auxiliary editor repair/smoke/preview workflows now run from explicit menu actions instead of load-time triggers.
- Detached enemy/lighting test bay scenes, showcase scenes, and their checked-in runtime/editor support were removed from the live workflow.
- `RRunSessionController` persists run state across lobby and floor-scene loads.
- `RSceneRouter` owns lobby, gameplay, retry, and floor-transition loading behavior.
- Main gameplay runtime systems are composed through:
  - `RSceneCompositionRoot`
  - `RFloorDirector`
  - `REncounterSpawner`
  - `RRunController`
- Authored floor data still bridges through `MainEscapeFloorAuthoring`; after
  the second decoupling round, floor authoring root and marker root names should
  be owned locally there instead of through broad runtime settings or
  `RSceneCompositionRoot` fallback creation.
- The gameplay loop exposes:
  - floor-clear modal flow
  - final-clear modal flow
  - failure modal flow
  - return-to-lobby flow
- HUD readability was refreshed:
  - square quick slots
  - icon-first item presentation
  - quantity-only quick slot labels
  - clearer health, battery, and threat status surfaces
- Enemy visibility now reacts to fog and nearby local light pools.
- Enemy vision presentation has moved from a hard wedge toward layered, threat-state-driven feedback.
- Enemy chase/pathing now handles temporary blocker cells and stall recovery more aggressively.
- Walking noise emission is tuned to sit slightly below the player's main recognition radius target.
- Imported generated enemy and tile art has been processed into runtime-friendly assets and enemy sprite profiles.
- Edit mode and play mode smoke coverage exists under `Assets/Tests`.
- The latest stabilization pass finished with Unity EditMode `108 / 108`
  passing, plus a targeted PlayMode verification for the lobby summary flow.

## Active Priorities

### 1. Stability maintenance

- rerun the validator pair and focused Unity coverage after any route, fog, or
  runtime-binding change
- validate the full `RMainEscape_Lobby -> 5F -> 1F -> RMainEscape_Lobby` route
  again after meaningful authored-scene edits
- keep lower-floor drift separate from `5F`-only structural work so scene
  cleanup does not silently spread

### 2. Playtest tuning

- keep running longer play checks on the full loop after the post-cleanup validation pass
- continue tuning enemy pursuit around authored blockers and room edges
- verify that threat feedback remains readable during real pursuit, not only in isolated checks

### 3. UI and presentation polish

- replace temporary Unity-generated HUD surfaces with exported art where it improves readability
- continue refining inventory, quick slot, and run-modal readability
- keep authored `IRHudCanvas` references stable while visual polish lands

### 4. Authored-scene stability

- reduce any remaining missing-script debt in older content or imported prefabs
- reduce `RSceneCompositionRoot` fallback creation after authored references are
  verified; treat it as transitional migration support
- keep legacy `Assets/Resources/Floors/MainEscape/*.prefab` baselines
  quarantined behind explicit legacy prefab workflows
- track down the intermittent URP 2D shadow exception if it returns in later play checks
- keep validators, tests, and inspector references aligned with the current `R` scene chain

### 5. Audio depth

- expand the actual background music and effect library beyond the current tuned prototype clips
- keep footstep, interaction, enemy, and floor-transition sounds readable against ambience
- preserve the rule that walk noise should be meaningful but not over-punishing

## Near-Term Tasks

1. rerun the validator pair and key Unity smoke coverage after the next
   structural scene or runtime pass
2. do one manual full-loop verification and log any lower-floor breakage
   separately from `5F` issues
3. replace temporary HUD surfaces with exported image assets after the art pass
   is ready
4. continue tuning local light density, threat readability, and player
   guidance
5. expand the concrete BGM and SFX set now that audio routing and tuning hooks
   are in place
6. keep docs, validators, and editor menu labels aligned with the current
   lobby/floor-chain split

## Deprioritized Work

These are not the current focus unless they unblock the authored `R` loop:

- old prototype-scene showcase flows
- broad new content batches before the current floor chain reads well
- large prefab-generation workflows that reintroduce runtime creation for scene-critical objects
- re-enabling legacy setup tools that the clean-loop branch intentionally turned off

## Rule For New Work

Before adding new gameplay systems, confirm that the change improves one of these:

1. loop clarity from lobby start to lobby return
2. authored-scene runtime stability
3. iteration speed inside the live `RMainScene_5F~1F` scenes
4. player understanding of danger, objectives, UI state, or navigation

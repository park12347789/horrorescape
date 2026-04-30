# ADR-0001: Live Authored Scene Chain As The Canonical Runtime Path

- Status: Accepted
- Date: 2026-04-13
- Decision Makers: Project maintainers
- Related Docs:
  - `Docs/PrototypeArchitecture.md`
  - `Docs/RSceneRebuildManifest.md`
  - `Docs/SystemAuditCleanupPlan.md`
  - `Docs/MainEscapeAuthoringGuide.md`
- Affected Areas:
  - scene routing
  - authored scene workflow
  - runtime composition
  - validation and smoke testing
  - cleanup of legacy and detached-scene leftovers

## Context

The project previously carried prototype-era and detached-scene workflows that
made it harder to tell which scenes, prefabs, and runtime systems were truly
live.

The current repository has already converged on a single playable loop:

- `Assets/Scenes/RMainEscape_Lobby.unity`
- `Assets/Scenes/RMainEscape_tuto.unity` as an optional support scene outside
  the floor route arrays
- `Assets/Scenes/RMainScene_5F.unity`
- `Assets/Scenes/RMainScene_4F.unity`
- `Assets/Scenes/RMainScene_3F.unity`
- `Assets/Scenes/RMainScene_2F.unity`
- `Assets/Scenes/RMainScene_1F.unity`
- `Assets/Scenes/RMainEscape_ElevatorTransition.unity` as an interstitial
  support scene outside the floor route arrays

That route is split across:

- `ProjectSettings/EditorBuildSettings.asset`, which lists the lobby, support
  scenes, and floor scenes in build order
- `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset`, which tracks
  lobby/floor-route validation and support data but is not the full support
  scene list

Runtime behavior still crosses some transitional bridges such as
`MainEscapeFloorAuthoring`, but the live path is no longer the old detached
prototype flow. Support scenes may exist in Build Settings without becoming
gameplay floors. Documentation, validators, and future cleanup work need one
explicit decision to anchor against.

## Options Considered

1. Keep the authored lobby-plus-floor chain as the only canonical runtime path.
2. Treat detached scenes, showcase flows, or generated fallbacks as equal
   alternatives during cleanup.
3. Reintroduce runtime object regeneration to patch missing authored references.

## Decision

Keep the authored `RMainEscape_Lobby -> RMainScene_5F -> 4F -> 3F -> 2F -> 1F`
scene chain as the canonical runtime path for the checked-in project.

The authored scenes and their serialized references are the source of truth for
playable content placement. ADR-0005 narrows route-data authority to
`RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`; serialized floor
arrays remain compatibility/alignment data. When an authored reference is
missing, the fix should happen in the scene or prefab graph rather than by
silently recreating critical objects at runtime.

An interstitial support scene may sit between floor scenes when it preserves
that authored floor route. `RMainEscape_ElevatorTransition` is such a support
scene: it is appended after the lobby and floor scenes in Build Settings, is
referenced separately by `RRunSessionController.ElevatorTransitionScenePath`,
and is not part of the `RFloorSceneEntry` floor route arrays.

`RMainEscape_tuto` may exist as an optional lobby-selected tutorial support
scene. It is not part of the canonical floor route and must not be added to
`RFloorSceneEntry` arrays. Leaving the tutorial starts a fresh 5F run through
the elevator transition path, so tutorial pickups and key state do not persist
into the live floor chain. If present in Build Settings, it should sit after
the lobby and before the authored floor scenes while remaining outside route
graph floor nodes and serialized floor arrays. Tutorial floor tiles and pickup
practice objects are authored inside that support scene, using copied 5F tile
assets and existing Main Escape pickup, prop, enemy, light, and 5F door-pair
visuals rather than changing the 5F drop contract.

Detached test-bay, showcase, or prototype-era flows are not part of the live
path unless a future ADR explicitly restores them for a scoped purpose.

## Consequences

### Benefits

- The playable loop is easier to reason about and validate.
- Scene routing, runtime ownership, and documentation all point to the same
  chain.
- Cleanup work can remove fallback behavior without uncertainty about what is
  actually live.
- Authored scene iteration stays inspectable for layout, readability, and HUD
  binding work.

### Tradeoffs

- Transitional bridges still need care while old naming and old runtime paths
  are reduced.
- Missing serialized references fail more visibly and require explicit scene
  repair.
- Lower floors remain downstream verification targets when structural changes
  start in `RMainScene_5F`.

### Follow-Up

- Keep `Docs`, validators, and Build Settings aligned with the lobby/floor chain
  plus any explicitly documented support scenes.
- Avoid reintroducing detached-scene compatibility branches casually.
- Use future ADRs for decisions such as Addressables adoption, asmdef
  boundaries, or deeper authored/runtime bridge reduction.

## Validation

- Run the lobby preflight validator and start-floor runtime validator after
  structural scene edits.
- Keep EditMode and PlayMode smoke coverage aligned with the lobby/floor chain
  and its support scenes.
- Manually verify the full lobby-to-1F-to-lobby route after major cleanup
  waves.

# ADR-0002: Controlled Resources Loading While The Live Loop Stabilizes

- Status: Accepted
- Date: 2026-04-13
- Decision Makers: Project maintainers
- Related Docs:
  - `Docs/reference/unity-project-baseline.md`
  - `Docs/RSceneRebuildManifest.md`
  - `Docs/PrototypeArchitecture.md`
  - `Docs/MainEscapeAuthoringGuide.md`
- Affected Areas:
  - runtime asset loading
  - ScriptableObject settings access
  - enemy art and audio loading
  - door, floor, and tile content loading

## Context

The current project already depends on `Assets/Resources` as part of the live
runtime path. A quick repository check shows:

- `27` `Resources.Load` call sites under `Assets`
- live assets under `Assets/Resources/Audio`
- live assets under `Assets/Resources/Floors`
- live assets under `Assets/Resources/MainEscape`
- live assets under `Assets/Resources/Tiles`

Important runtime systems currently resolve settings, prefabs, sprite profiles,
tiles, and audio through `Resources`. The project does not currently use
Addressables, and a broad migration would touch the active authored scene chain
while the team is still prioritizing loop stability and readability.

The project still needs a clear rule, because leaving this decision implicit
makes it easy to either expand `Resources` casually or start an unstable
migration too early.

## Options Considered

1. Keep the current `Resources`-based loading model for the live loop, but
   control future expansion.
2. Start a broad Addressables migration immediately.
3. Keep adding new `Resources` paths freely until pain forces a later rewrite.

## Decision

Keep the existing `Resources`-based loading model for the current live loop.

Do not expand it casually.

For the current project phase:

- existing `Resources` dependencies remain valid
- fixes and refactors may continue using current `Resources` paths when they are
  part of the live loop
- new loading-heavy subsystems, large content sets, or async-loading needs
  should trigger a new ADR before introducing more `Resources` sprawl
- when practical, prefer explicit serialized references or catalog assets over
  inventing new string-based `Resources` lookups

## Consequences

### Benefits

- Preserves stability in the active lobby-plus-floor chain.
- Avoids a broad migration while the current priority is readability and loop
  validation.
- Makes the current loading stance explicit instead of accidental.

### Tradeoffs

- The project keeps carrying `Resources`-based technical debt for now.
- Asset-loading concerns remain more string-path-driven than a future
  Addressables or stronger catalog approach would be.
- This decision is a deferment, not a long-term ideal state.

### Follow-Up

- Track new `Resources.Load` additions during code review.
- Create a dedicated ADR before any Addressables rollout.
- Revisit the loading strategy when asset scale, async loading, or memory
  pressure becomes a real problem for the live loop.

## Validation

- Existing runtime settings, floor prefabs, tiles, doors, profiles, and audio
  should continue resolving through the current paths.
- New feature work should document why a new `Resources` path is needed.
- Any future migration plan should include scene, prefab, and test validation
  against the full six-scene live route.

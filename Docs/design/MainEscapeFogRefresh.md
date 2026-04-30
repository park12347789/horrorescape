# Main Escape Fog Refresh

## Purpose

This document records the stabilized fog and flashlight setup that came out of
the `3F` performance and light-leak pass on `2026-04-16`.

Use it when you need to:

- roll the same fix set onto another floor
- retune fog quality without reopening the original regressions
- understand which file owns which part of the behavior
- verify whether a new lighting issue belongs to fog, scene light, or blocker
  authoring

This is the current operational guide for the live authored `R` floor chain.

## What We Kept After Stabilization

The stable baseline is:

- keep the live `FlashlightFogOfWarOverlay` contract and presentation model
- move visibility line-of-sight to `GridMapService`
- keep actual scene flashlight light cheaper than gameplay reveal
- clamp the flashlight reveal origin back to the player side of nearby walls
- keep solid blockers as movement-plus-vision blockers
- keep plain prop blockers and move-only overlays as movement-only blockers
- keep flashlight shadows disabled by default
- keep automatic wall `ShadowCaster2D` authoring out of the default path

The important lesson from `3F` is that the leak fix that survived was not
"more wall shadow systems." It was "shorter scene light, safer reveal origin,
and clearer blocker registration."

## Current Fog Contract

Treat this as the current subsystem contract for the live floor chain.

### Keep the fog model simple

`FlashlightFogOfWarOverlay` should continue to behave like a simple
`Visible/Unexplored` fog system.

For current gameplay work, do not rebuild explored-memory behavior back into
the hot path. The enum may still contain `Explored`, but the live contract is:

- visible right now
- otherwise unexplored fog

### Restore fixed lights through a baked authored-light mask

Fixed readable light pools should be restored through
`AuthoredVisibilityLight2D` baked visibility, not by layering new cleanup or
freeze passes onto the fog update loop.

The intended path is:

- `FlashlightFogOfWarOverlay` rebuilds a baked authored-light visible mask from
  `AuthoredVisibilityLight2D.TrySampleStrongestReveal(...)`
- that baked mask is recalculated on reset and when the fog texture is resized
- baked visible cells are applied before player visibility checks

Current priority order is:

1. baked authored-light visible mask
2. player comfort reveal
3. flashlight reveal
4. unexplored fog

Display-side readability work should stay on the overlay material or shader.
Do not move gameplay reveal ownership out of the CPU fog contract just to make
the edge look softer.

### Real-time authored-light fallback stays secondary

If `bakeAuthoredLightVisibilityOnReset` is disabled, authored lights may still
participate as a simple fallback.

That fallback should stay narrow:

- use `AuthoredVisibilityLight2D.CouldAnyActiveLightReveal(...)` as the
  broadphase
- use `AuthoredVisibilityLight2D.TrySampleStrongestReveal(...)` as the final
  check
- do not add new freeze, stale cleanup, revision, or touched-block systems
  around that fallback

### Do not reintroduce the old complexity

Do not bring these patterns back into `FlashlightFogOfWarOverlay` as part of
lighting or readability fixes:

- freeze regions
- stale visible cleanup passes
- far-band or near-band rewrite passes
- touched-block revision tracking
- visible-confirmation revision tracking
- pending upload cleanup
- full-texture cleanup passes

Also do not treat profiler instrumentation, ad hoc runtime measurement code, or
extra debug logging as part of the normal fix path for this subsystem. The
preferred fix path is to keep the structure simple and reconnect authored light
visibility correctly.

## Ownership Map

| File | Responsibility |
| --- | --- |
| `Assets/Scripts/Objectives/MainEscapeRuntimeSettings.cs` | global runtime defaults such as frame cap, VSync policy, and default flashlight shadow state |
| `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset` | serialized runtime defaults used by the live floor chain |
| `Assets/Scripts/Player/WasdPlayerController.cs` | actual `Light2D` flashlight, presentation sprites, local offset, light order, and scene-light-only scaling |
| `Assets/Scripts/Objectives/FlashlightFogOfWarOverlay.cs` | fog texture generation, simple visible/unexplored state, baked authored-light visibility, player comfort reveal, flashlight reveal, and limited runtime throttling |
| `Assets/Resources/MainEscape/MainEscapeFogOverlaySoft.shader` and `Assets/Resources/MainEscape/MainEscapeFogOverlaySoft.mat` | prototype fog display feathering on the GPU side only; visual smoothing must not change CPU reveal ownership |
| `Assets/Scripts/Grid/GridMapService.cs` | authoritative movement and visibility cell blocking, including line-of-sight traversal |
| `Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs` | floor-side registration of wall, overlay, prop, and movement blockers into the runtime grid service |
| `Assets/Scripts/Objectives/MainEscapeSolidBlockerAuthoring.cs` | solid blocker collider fidelity and solid-blocker runtime behavior |
| `Assets/Tests/EditMode/MainEscapeFogGridLineOfSightEditModeTests.cs` | fog and line-of-sight regressions |
| `Assets/Tests/EditMode/MainEscapeRuntimeSettingsTests.cs` | frame-cap and flashlight-shadow runtime defaults |
| `Assets/Tests/EditMode/MainEscapeSolidBlockerAuthoringEditModeTests.cs` | thin wall and one-cell solid blocker collider safety |

## Stable Runtime Defaults

### Global runtime

These values are the default runtime baseline for the current playable loop:

- `targetFrameRate = 60`
- `disableVSyncForTargetFrameRate = true`
- `defaultFlashlightShadowsEnabled = false`
- `debugFlashlightShadowsEnabled = false`

Those are owned by:

- `Assets/Scripts/Objectives/MainEscapeRuntimeSettings.cs`
- `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset`

### Player flashlight scene light

The actual scene light is intentionally cheaper and tighter than the authored
gameplay reveal.

Current baseline in `WasdPlayerController`:

- `flashlightSceneLightRangeScale = 0.24`
- `flashlightSceneLightAngleScale = 0.4`
- `flashlightSceneLightIntensityScale = 0.82`
- `flashlightLocalOffset = (0, 0.12, 0)`
- `flashlight.lightOrder = -1`
- `flashlightShadowsEnabled = false`
- `showFlashlightConePresentation = false`

Important rule:

- gameplay reveal still uses the authored flashlight range and angle
- the `Light2D` shown in the scene is the reduced "scene light" version

That split is what kept visibility readable without paying for the old spill.

### Fog overlay defaults

Current baseline in `FlashlightFogOfWarOverlay`:

- `sightOriginForwardOffset = 0.08`
- `flashlightRevealAnglePadding = 8`
- `movingRefreshInterval = 0.02`
- `idleRefreshInterval = 0.08`
- `interlacedUpdateGroups = 2`
- `movingSampleStride = 2`
- `idleSampleStride = 2`
- `pixelsPerUnit = 4`
- `roiPadding = 1.25`

Runtime clamps still apply on top of serialized values:

- moving refresh interval is never allowed below `0.05`
- moving sample stride is never allowed below `2`
- idle sample stride is never allowed below `2`
- moving interlaced groups are never allowed below `2`

That means a floor can serialize slightly more aggressive values, but the hot
path still respects the safer runtime floor.

The moving fog pass should stay bounded to the current player/flashlight reveal
area plus the previous bounds for the same interlaced group. That keeps old
visible cells from lingering while avoiding a full-texture scan on every moving
refresh.

### Scene-local values currently propagated to floors

The floor scenes currently share this baseline:

- `sightOriginForwardOffset = 0.08` on `RFogOfWarOverlay` in `1F` through `5F`
- `playerComfortRevealRadius = 0.9` on `RFogOfWarOverlay` in `1F` through `5F`
- `minimumRuntimeComfortRevealRadius = 0.9` on `RFogOfWarOverlay` in `1F` through `5F`
- scene `Light2D.m_Intensity = 0.35` in `1F` through `5F`

`bakeAuthoredLightVisibilityOnReset` is intentionally mixed in the current
scene contract: `1F` through `3F` keep authored-light bake enabled, while `4F`
and `5F` keep it disabled and rely on the runtime authored-light fallback path.
Treat changing that split as a contract change, not a cleanup toggle.

`3F` keeps the same baseline plus one readability reference:

- `flashlightRevealAnglePadding = 8`
- `idleSampleStride = 2`

Use `3F` as the reference floor when you need to compare "default" versus
"confirmed on a difficult lit floor."

## Visibility Model After The Refresh

### What blocks movement and light now

- `Tiles_wall` blocks movement and visibility
- `GameplayOverlay/BlockAll` blocks movement and visibility
- `GameplayOverlay/MoveOnly` blocks movement only
- `MainEscapeSolidBlockerAuthoring` blocks movement and visibility
- plain `MainEscapePropBlockerAuthoring` blocks movement only

This is registered through `MainEscapeFloorAuthoring.RegisterPropBlockers(...)`.

The critical distinction is:

- use `solid` blockers when the object must stop the flashlight reveal
- use plain prop blockers when the object should only affect navigation

### How line-of-sight is resolved

`GridMapService.HasLineOfSight(...)` now walks cell boundaries directly instead
of stepping through repeated sample points.

That change matters because it:

- reduces hot-path physics work
- catches blocked corners more reliably than sparse sampling
- keeps wall, door, and solid-blocker logic inside one authority

### How wall-rub light leaks are prevented

The leak fix that stayed in the build is:

1. compute the desired flashlight origin from the pivot plus a very small
   forward offset
2. clamp that desired origin back toward the player if the path is blocked
3. run fog visibility from that safe origin
4. keep the visible `Light2D` itself shorter and narrower than gameplay reveal

The leak fix that did not stay in the build is:

- per-pixel wall-boundary special casing inside the fog update loop
- default wall tilemap `ShadowCaster2D` automation

Both of those were more expensive and produced worse stability.

## Rollout Checklist For Another Map

Apply this in order. Skipping straight to scene-light tuning usually recreates
the same confusion we had on `3F`.

### 1. Start from the shared runtime baseline

Confirm these are already present before touching the new floor:

- `MainEscapeRuntimeSettings` still caps to `60`
- default flashlight shadows are still off
- player prefabs still use the reduced scene-light scales and lower local
  offset

If those were reverted elsewhere, fix the shared baseline first.

### 2. Check the floor's blocker authoring before tuning fog

Confirm the target floor is using the right roots:

- `Tiles_wall`
- `GameplayOverlay/BlockAll`
- `GameplayOverlay/MoveOnly`

Then confirm the authored blockers match intent:

- use `MainEscapeSolidBlockerAuthoring` for thin dividers, one-cell walls, and
  props that must stop flashlight reveal
- use plain prop blockers for movement-only props

Do not try to compensate for wrong blocker authoring with larger fog or
lighting hacks.

### 3. Set the fog origin baseline on the floor scene

On the floor's `RFogOfWarOverlay`, start with:

- `sightOriginForwardOffset = 0.08`
- `flashlightRevealAnglePadding = 8`
- `movingRefreshInterval = 0.02`
- `idleRefreshInterval = 0.08`
- `interlacedUpdateGroups = 2`
- `movingSampleStride = 2`
- `idleSampleStride = 3`

Then use only one floor-specific change at a time:

- if the fog edge feels too coarse while idle, lower `idleSampleStride` to `2`
- if walking is still too expensive, do not raise `pixelsPerUnit` first; keep
  the texture the same and tune stride/group counts
- if the flashlight reveal still looks too narrow, widen
  `flashlightRevealAnglePadding` before increasing actual `Light2D` size

### 4. Tune scene light separately from reveal

If the scene still looks too dark or too fake:

- adjust `flashlightSceneLightRangeScale`
- adjust `flashlightSceneLightAngleScale`
- adjust `flashlightSceneLightIntensityScale`
- adjust `flashlightLocalOffset`

Do not use these changes to fix fog reveal bugs.

If the problem is "I can see around the wall in fog," the first suspects are:

- blocker registration
- wrong root placement
- origin clamp regression

If the problem is "the visible beam looks awkward," the first suspects are:

- scene-light scales
- local offset
- light order

### 5. Avoid the known dead ends

Do not reintroduce these as a first response:

- automatic wall tilemap `ShadowCaster2D` generation
- broad per-pixel wall recalculation in the fog hot path
- raising `sightOriginForwardOffset` back toward the old `0.62`
- enabling flashlight shadows by default
- using generic prop blockers for objects that should stop light

Those paths either caused frame regressions, heavy scene churn, or both.

## Manual Verification Checklist

After applying the setup to another floor, manually verify:

1. walk along a thin wall while aiming the flashlight across it
2. shine across an outside corner and confirm the far side does not pop open
3. rub against one-cell solid blockers and confirm movement does not leak
4. walk through the brightest corridor on the floor and watch frame pacing
5. check that the beam origin feels visually attached to the player sprite
6. confirm `MoveOnly` areas still block movement but do not kill lighting
7. confirm `BlockAll` areas and solid blockers stop both movement and reveal

## Automated Validation

Use these tests as the focused subsystem gate:

- `MainEscapeFogGridLineOfSightEditModeTests`
- `MainEscapeRuntimeSettingsTests`
- `MainEscapeSolidBlockerAuthoringEditModeTests`
- `AuthoredVisibilityLightEditModeTests`
- `MainEscapeDebugModeControllerEditModeTests`

Important note:

- the full EditMode suite currently contains unrelated baseline failures in
  scaffold and older runtime areas
- use the focused fog and flashlight set above to validate this subsystem until
  the broader suite is cleaned up

## Known Watchpoints

### Scene churn

Changing `ShadowCaster2D` or touching certain prefab instances can cause large
serialized diffs, especially on `3F`. Review scene diffs carefully before
committing.

### Thin visual walls

If a wall looks one-cell wide but still lets the player slip through, verify
the authoring object is a `MainEscapeSolidBlockerAuthoring` instance and not a
plain prop blocker. The solid blocker path now preserves full collider world
size even when the visual is squashed.

### Lighting versus fog confusion

Always decide which of these is wrong before tuning:

- scene light presentation
- fog reveal logic
- movement/vision blocker registration

Most wasted time during the `3F` pass came from treating those as one system.

## Related Docs

- `Docs/MainEscapeAuthoringGuide.md`
- `Docs/design/MainEscapeLiveLoopSystem.md`
- `Docs/status/MainEscapeCurrentState.md`

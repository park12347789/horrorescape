# MainEscape Shadow Startle System

## System Summary

- System name: MainEscape Shadow Startle System
- Owner: current live `RMainEscape_Lobby -> 5F -> 1F -> RMainEscape_Lobby` loop
- Status: implemented MVP
- Related scenes:
  - `Assets/Scenes/RMainScene_4F.unity`
  - `Assets/Scenes/RMainScene_3F.unity`
  - `Assets/Scenes/RMainScene_2F.unity`
- Related prefabs/assets:
  - `Assets/Resources/MainEscape/EnemyArt/GroundEnemy_ShadowHumanoid.asset`
  - `Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs`
  - `Assets/Scripts/Objectives/MainEscapeRuntimePrefabCatalog.cs`
  - `Assets/Scripts/Objectives/FlashlightFogOfWarOverlay.cs`
  - `Assets/Scripts/Enemy/Common/PlayerThreatFeedbackRegistry.cs`

## Goal

Add a non-combat shadow-humanoid presence that startles the player and reinforces
haunting without behaving like a real enemy, altering progression, or muddying
the readability of patrol, sentry, vent, and stalker roles.

## Player-Facing Behavior

The player occasionally catches a human-like shadow at the edge of the
flashlight or across a threshold, usually for less than a second. It feels like
the building is watching the run rather than actively hunting the player.

The current MVP also supports a runtime-only `footstep surprise` mode:

- on each player footstep event, roll a low-probability chance
- when the roll succeeds, spawn a one-shot silhouette just behind the player's
  facing
- default tuning uses `0.1%` chance per step

The recommended role is `Peripheral Apparition + Route Punctuation`:

- `Peripheral Apparition`
  - a brief silhouette at the edge of vision, down a dark corridor, or at the
    far side of a doorway
- `Route Punctuation`
  - a one-shot reveal that lands near a route beat such as a corner, stair
    approach, or post-objective travel beat

The shadow presence must not:

- chase, attack, block, collide, or investigate
- affect health, battery, inventory, doors, or floor progression
- enter threat HUD or enemy visibility feedback as a real danger source
- appear so long that the player reads it as a new AI enemy type

Recommended cadence for the full `5F -> 1F` descent:

- `5F`
  - optional distant hint only, or no direct reveal
- `4F`
  - first clear reveal, short and readable
- `3F`
  - optional partial reveal, reflection, or doorway pass
- `2F`
  - strongest close reveal
- `1F`
  - aftermath beat only, or no reveal if the floor already carries enough exit
    pressure

Target frequency:

- `2-3` total appearances per full run
- never more than `1` direct reveal on a floor
- at least one quiet traversal beat between appearances

## Runtime Ownership

- Entry points:
  - `Assets/Scripts/Rebuild/Runtime/RSceneCompositionRoot.cs`
  - `Assets/Scripts/Rebuild/Runtime/RFloorDirector.cs`
  - `Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs`
- State owner:
  - `RShadowStartleDirector`
  - owns per-scene trigger evaluation and cue playback
- Scene or prefab dependencies:
  - runtime-created `RShadowStartleCue`
  - `GroundEnemy_ShadowHumanoid.asset`
  - `FlashlightFogOfWarOverlay`
- UI or presentation dependencies:
  - optional one-shot SFX through marker-local `AudioSource`
  - sprite material defaults through `MainEscapeRuntimeVisualDefaults`
  - no use of enemy threat HUD binding or enemy vision presentation

Recommended implementation shape:

- Do not reuse `EnemyStateMachine` or `CeilingVentEnemyController`
- Instantiate a lightweight one-shot presentation object only when a marker
  fires
- Build that presentation object against the current ground-enemy visual spec:
  - `EnemyPrefabBindings`
  - `GroundEnemySpriteProfile`
  - `EnemySpriteDirectionUtility`
- Drive art with a small frame animation player using
  `GroundEnemy_ShadowHumanoid.asset`
- Destroy or pool-release the cue immediately after the reveal window ends

Common-spec reuse for the MVP:

- reused:
  - `EnemyPrefabBindings.VisualRoot`
  - `EnemyPrefabBindings.BodyRenderer`
  - `GroundEnemySpriteProfile` directional sprite contract
  - `EnemySpriteDirectionUtility` facing-to-frame rules
- intentionally omitted:
  - `EnemyStateMachine`
  - `VisionSensor2D`
  - `EnemyVisionVisualizer`
  - `CircleCollider2D` hitbox semantics
  - threat feedback and stun registries
  - movement / chase / search / passive enemy audio drivers

Runtime-only trigger paths in the MVP:

- authored marker cues from `ScareMarkers`
- optional `PrototypePlayerAudio` footstep hook for rare behind-player spawns
- debug preview hotkey on `RShadowStartleDirector` for instant in-play checks

## Data And Authoring

- ScriptableObjects or config assets:
  - no dedicated profile asset in MVP
  - per-cue timing, trigger, and audio tuning lives on
    `MainEscapeShadowStartleMarker`
- Authored scene markers or roots:
  - root: `RAuthoring/AuthoringMarkers/ScareMarkers`
  - marker component: `MainEscapeShadowStartleMarker`
  - child markers may be named `ShadowStartle_01`, `ShadowStartle_02`, and so
    on
- Runtime-generated objects:
  - `RShadowStartleCue`
  - `RShadowStartleDirector` scene runtime component under
    `RSceneCompositionRoot`
- Save or persistence concerns:
  - marker consumption currently resets on scene residency
  - no cross-run persistence required

Recommended marker fields:

- world position
- facing direction
- reveal variant
  - `Peripheral`
  - `Threshold`
  - `Glass`
- trigger mode
  - on enter radius
  - on crossing threshold
  - on flashlight reveal
- one-shot per run
- one-shot per scene residency
- retrigger cooldown for repeatable markers
- sorting layer name and sorting order
- optional reveal clip and volume

Recommended authoring rules:

- use a dedicated `ScareMarkers` root instead of reusing `DangerMarkers`
- keep markers off patrol routes, sentry sightline centers, vent nodes, and
  mandatory interaction hotspots
- prefer corners, doorway offsets, partition windows, and stair approaches
- pilot the system on `4F` and `2F` first before wider rollout
- default marker format for authored use:
  - component on an empty child transform under `ScareMarkers`
  - transform position is reveal spawn point
  - transform up vector is default facing when `facePlayerOnTrigger` is off
  - resource-backed sprite art comes from
    `Assets/Resources/MainEscape/EnemyArt/GroundEnemy_ShadowHumanoid.asset`

## Validation

- Expected invariants:
  - no `EnemyStateMachine` is created by the shadow startle path
  - no registration into `PlayerThreatFeedbackRegistry`
  - no change to route ordering, door state, pickups, or run outcome
  - default markers fire at most once per scene residency
  - cues disappear cleanly without leaving colliders or HUD state behind
- Common misconfiguration risks:
  - placing cues too close to core enemy teaching moments on `5F`
  - overusing the same reveal pattern and flattening tension
  - marker placement inside active enemy lanes, causing false threat reads
  - reusing danger-marker roots and mixing meanings
  - giving the cue full brightness or long dwell time so it reads like a real
    enemy
- Required editor tooling or validators:
  - extend floor/runtime validation to assert no threat-source registration from
    shadow cues
  - add authoring validation for duplicate scare markers and missing profile
    references
  - add a simple scene report listing per-floor cue count

## Testing

- EditMode coverage:
  - marker-to-runtime cue binding
  - one-shot-per-scene consumption
  - repeatable marker cooldown guards
  - no threat-registry registration
- PlayMode coverage:
  - cue fires once, plays, and disappears
  - flashlight-reveal and threshold-reveal timing
  - floor reload or run restart resets cue availability
  - enemy HUD remains unchanged during cue playback
- Manual verification notes:
  - run the full lobby-to-1F loop and confirm the shadow never alters
    progression
  - verify `4F` and `2F` pilot placements in dark, lit, and flashlight-only
    reads
  - confirm the cue still reads as non-combat when the player walks directly
    toward it
  - confirm no reveal lands during a high-load navigation or mandatory pickup
    interaction moment

## Open Questions

- Should the initial implementation support only `Peripheral` and `Threshold`
  variants, leaving `Glass` as a later extension?
- Should the cue vanish when directly lit by flashlight, or should it hold for
  a fixed short duration regardless of lighting?
- Which floor should carry the strongest close reveal first: `2F` or `3F`?

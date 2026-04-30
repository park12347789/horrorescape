# MainEscape Vent Network Authoring Redesign

Date: 2026-04-24

## Goal

Replace the current generated and inferred vent route workflow with a scene-authored
vent network that a developer can inspect and edit directly in the hierarchy.

The new workflow should let a developer define:

- the exact network shape
- the explicit links between vent nodes
- the exact exit points where the vent enemy may emerge
- per-floor differences without rebuilding routes from generated layout data

Unity Editor runtime verification is not available in this environment, so this
design is based on code structure, serialized scene data, and Inspector reference
contracts.

## Current State

Live authored scenes currently carry one `VentRoute` root each.

| Source | Route Roots | Nodes | Auto | Corridor | Room | Nodes With Serialized Links | Serialized Link Refs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `Assets/Scenes/RMainScene_5F.unity` | 1 | 28 | 18 | 6 | 4 | 3 | 4 |
| `Assets/Scenes/RMainScene_4F.unity` | 1 | 28 | 0 | 14 | 14 | 0 | 0 |
| `Assets/Scenes/RMainScene_3F.unity` | 1 | 28 | 0 | 14 | 14 | 3 | 4 |
| `Assets/Scenes/RMainScene_2F.unity` | 1 | 28 | 18 | 6 | 4 | 3 | 4 |
| `Assets/Scenes/RMainScene_1F.unity` | 1 | 28 | 18 | 6 | 4 | 3 | 4 |

Legacy floor prefabs under `Assets/Resources/Floors/MainEscape` still contain one
route root with only four corridor nodes and no serialized links. The 5F authoring
marker prefab contains four auto nodes and no serialized links. Those assets look
like stale baselines, not reliable authoring sources for the live loop.

## Current Runtime Flow

1. `MainEscapeFloorAuthoring.GetVentRouteDefinition()` resolves the `VentRoute`
   child under the authored marker root.
2. `MainEscapeVentRouteAuthoring.BuildRouteDefinition()` converts child transforms
   into node definitions.
3. It accepts three route modes at once:
   - explicit per-node `connectedNodes`
   - inferred links from names such as `Upper_`, `Lower_`, and `Corridor_`
   - child-order sequential paths when no explicit or inferred connection data is
     available
4. `RFloorDirector` or `MainEscapeFloorDirector` passes the route into
   `REncounterSpawner` or `MainEscapeEncounterSpawner`.
5. `BaseOfficeVentEnemyBootstrap.CreateRuntimeEnemy()` instantiates the vent enemy.
6. `CeilingVentEnemyController` builds an internal adjacency graph and chooses room
   or corridor nodes for noise response, player following, ambient crawling, and
   emergence.

## Root Cause Analysis

The problem is not a single missing reference. The current workflow has several
implicit graph sources that compete with manual authoring.

- `MainEscapeVentRouteBatchTools` can rebuild lower-floor routes from generated
  room and corridor data. This is the path most likely to create odd tool-authored
  placement, because it tries to infer a useful route from layout geometry instead
  of from a designer-owned vent plan.
- `MainEscapeVentRouteAuthoring.TryBuildNamedColumnConnections()` creates runtime
  links from node names and rounded world X positions. This means the final graph
  can differ from the visible `connectedNodes` data in the Inspector.
- Explicit links are not fully authoritative, because inferred named links are
  still added after explicit links are collected.
- Several live floors mix node typing styles. Some floors have many `Auto` nodes,
  some have explicit `Corridor` and `Room` node types, and 4F has no serialized
  node links at all.
- The current route model has no first-class exit concept. It only knows corridor
  and room nodes. Emergence is derived from room lookup and nearest-node decisions,
  so a developer cannot directly say "this vent exit is here."
- In authored scenes, generated fallback is suppressed. If a route fails explicit
  or inferred connection resolution, the vent enemy may end with no usable graph.

## Design Direction

Move to an explicit, scene-authored network contract:

```text
VentNetwork
  Nodes
    Node_A
    Node_B
    Node_C
  Links
    Link_A_B
    Link_B_C
  Exits
    Exit_Storage
    Exit_NurseDesk
```

The hierarchy becomes the source of truth. Editor tools may help validate or draw
the network, but they should not create the gameplay network shape by inference.

## Proposed Components

`MainEscapeVentNetworkAuthoring`

- Placed on the `VentNetwork` root.
- Owns the local network scope.
- Builds a durable `MainEscapeVentNetworkDefinition`.
- Validates duplicate node IDs, missing references, cross-network links, isolated
  nodes, and missing exits.

`MainEscapeVentNodeAuthoring`

- Placed on children under `Nodes`.
- Keeps a stable node ID.
- Stores node kind: `Transit`, `Junction`, or `ExitAnchor`.
- Stores optional authored cell override only when transform-to-cell alignment is
  not enough.
- Does not create links by child order or name.

`MainEscapeVentLinkAuthoring`

- Placed on children under `Links`.
- References exactly two nodes in the same `VentNetwork`.
- Represents one undirected graph edge.
- Provides the easiest Inspector surface for developers to inspect the network
  shape without opening every node.

`MainEscapeVentExitAuthoring`

- Placed on children under `Exits`.
- References one network node.
- References or owns an emerge anchor transform.
- Stores exit behavior flags such as `CanEmerge`, `CanRetreat`, and optional
  floor-specific weight.
- Stores optional facing direction for the first emerged scan.

If an interface is introduced later, it must keep the project rule and start with
`I`, for example `IMainEscapeVentNetworkProvider`.

## Sample Scene

`Assets/Scenes/RMainEscape_VentNetworkTest.unity` is a small isolated authoring
sample. It is not connected to the live floor loop. Open `VentNetwork_Sample` to
inspect the intended hierarchy:

- `Nodes`: five sample nodes
- `Links`: four explicit link objects
- `Exits`: three explicit emergence/retreat exit objects

## Runtime Definition

The runtime should consume an explicit immutable definition:

```csharp
MainEscapeVentNetworkDefinition
  Nodes: stable id, cell, kind
  Links: from node id, to node id
  Exits: exit id, node id, exit cell, world position, facing, behavior flags
```

`CeilingVentEnemyController` should stop guessing exits from room nodes. It should:

- use `Nodes` and `Links` for hidden crawl pathfinding
- use `Exits` for emergence and retreat decisions
- choose a noise response exit by nearest valid exit, then route through the graph
  to that exit's node
- keep floor-specific behavior in the spawner or behavior profile, not in graph
  inference

## Migration Plan

1. Freeze the rebuild workflow.
   - Mark `Tools/Main Escape Rebuild/Rebuild Live Floor Vent Routes (1F-4F)` as
     legacy or remove it from normal authoring docs.
   - Keep it only as a temporary salvage tool if needed.

2. Add explicit network data classes and validators.
   - Add edit-mode tests for duplicate IDs, broken links, isolated nodes, and
     missing exits.
   - Add a read-only report that lists node count, link count, exit count, and
     disconnected islands per scene.

3. Add a runtime adapter.
   - Either adapt `MainEscapeVentNetworkDefinition` into the existing controller
     graph or replace `MainEscapeVentRouteDefinition` inside the vent enemy path.
   - Keep `MainEscapeVentRouteDefinition` temporarily for old scenes only.

4. Migrate one floor manually.
   - Start with one non-terminal floor, preferably 3F or 4F.
   - Place `VentNetwork/Nodes`, `VentNetwork/Links`, and `VentNetwork/Exits`
     directly in the scene.
   - Verify by code inspection and serialized references that all links and exits
     stay inside the same network root.

5. Migrate the remaining live floors.
   - Do not batch regenerate the network.
   - Use the existing 28-node routes only as visual reference, not as authoritative
     structure.

6. Remove implicit route modes after all live floors migrate.
   - Remove or disable named-column inference.
   - Remove child-order sequential runtime fallback for authored scenes.
   - Keep clear warnings when an authored network is missing or invalid.

## Authoring Rules

- A playable floor should have exactly one active `VentNetwork` root.
- Every link must be represented by a visible `Link_*` object.
- Every place where the enemy can emerge must be represented by an `Exit_*` object.
- Node names are labels only. Runtime behavior must not depend on name prefixes.
- Child order is labels and organization only. Runtime behavior must not depend on
  sibling order.
- Network objects should be placed directly in the scene or floor prefab hierarchy.
  Runtime-created fallback roots are migration scaffolding only.

## Validation Checklist

- One active network root exists under the authored marker root.
- Node IDs are unique and stable.
- Links reference two valid nodes from the same network.
- Each connected island has at least one valid exit.
- Exit anchors are on walkable cells or intentionally configured as non-walkable
  ceiling-only anchors.
- At least one exit is near each intended room response area.
- No runtime path depends on `Upper_`, `Lower_`, `Corridor_`, child order, or
  generated layout rebuild output.

## Open Questions

- Should 5F keep vent-only ambient mode with exits disabled, or should it have
  authored exits that are ignored by the 5F behavior profile?
- Should exits be separate objects or a role on node objects? Separate objects are
  recommended for Inspector readability.
- Should links be edited through direct object references only, or should an editor
  scene handle create `Link_*` objects for convenience?

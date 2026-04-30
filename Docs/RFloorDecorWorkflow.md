# R Floor Decor Workflow

Use the shared `RMainScene_1F~5F` scaffold and dress each floor directly in the
live scene hierarchy. The goal is to improve readability and atmosphere without
hiding gameplay-critical blocking, route logic, or authored references.

## Recommended Order

1. Lock `Ground`, `Walls`, and `Doors` first.
2. Place the `CoverProps` objects that affect vision, movement, and routing
   before lighter set dressing.
   If a blocker should read like a narrow divider instead of a wall, use the
   `DividerScreen_*` solid-blocker presets rather than a full-width overlay.
   For `MovementBlockers` and `SolidBlockers`, adjust the placed scene transform
   scale when you need a different footprint; the authoring components now sync
   their serialized footprint from the placed instance scale.
3. Establish the floor silhouette with
   `DecorProps/00_Architecture` and `DecorProps/01_LargeSetDress`.
4. Add medium and small detail through `DecorProps/02_MediumSetDress` to
   `DecorProps/07_FX`.
5. Add `InteractiveProps` and `GoalVisuals` last so the playable route stays
   readable.
6. Use `MovementBlockers` only for gameplay correction, not as the main
   decoration tool.

## Root Guide

- `CoverProps/00_StructuralCover`
  corridor splits, wall extensions, and major line-of-sight blockers.
- `CoverProps/01_FurnitureCover`
  beds, cabinets, and other heavy furniture that shapes movement.
- `CoverProps/02_PortableCover`
  carts, boxes, and movable-feeling obstacles.
- `DecorProps/00_Architecture`
  structural shell pieces and fixed architectural elements.
- `DecorProps/01_LargeSetDress`
  large props that establish the function of the room.
- `DecorProps/02_MediumSetDress`
  desks, shelves, benches, and other medium-scale layout detail.
- `DecorProps/03_SmallClutter`
  small clutter, repeatable dressing pieces, and lived-in detail.
- `DecorProps/04_WallDress`
  posters, signs, cables, boards, and wall-mounted details.
- `DecorProps/05_CeilingDress`
  ceiling elements, lighting fixtures, and upper-space detail.
- `DecorProps/06_PracticalLighting`
  readable local light sources that support navigation and mood.
- `DecorProps/07_FX`
  smoke, leaks, grime, sparks, and other atmosphere-only finish work.
- `InteractiveProps/00_Pickups`
  pickups such as batteries, bottles, keys, and medkits.
- `InteractiveProps/01_NoiseActors`
  noise-making interactables and readable distractions.
- `InteractiveProps/02_ObjectiveSupport`
  support props that reinforce objective clarity.
- `AuthoringMarkers/DecorAnchors`
  optional anchors for recurring decor, light signage, or visual landmarks.

## Menus

- `Tools/Main Escape Rebuild/Normalize Decor Authoring In Active R Floor Scene`
- `Tools/Main Escape Rebuild/Normalize R Floor Decor Authoring Scaffold`

These menus should only normalize the contract roots and scaffold shape. They
are support tools for keeping the hierarchy consistent, not generators for the
final decor pass.

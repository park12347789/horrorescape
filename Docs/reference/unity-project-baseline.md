# Unity Project Baseline

This file captures the default engineering stance for this repository.

## Architecture

- Prefer composition over deep inheritance.
- Use ScriptableObjects for durable data and tunable values.
- Keep runtime state ownership explicit.
- Use focused service boundaries instead of god objects.

## Input

- Default to the Input System for new work.
- Keep input mappings and control choices data-driven when practical.
- Avoid scattering raw input checks across unrelated classes.

## UI

- Prefer the current authored HUD and run-modal workflow for gameplay UI in this project.
- Use UI Toolkit only when a menu-heavy or tool-like surface clearly benefits from it and the migration cost is understood.
- Keep UI reactive to state; avoid embedding gameplay rules in view code.

## Assets And Loading

- Treat `Assets/Resources` as an existing dependency, not as a pattern to expand casually.
- If a new loading strategy such as Addressables is proposed, capture the decision in an ADR first.
- Keep asset labels, folder intent, and authored/runtime ownership readable to humans.

## Performance

- Avoid allocations in `Update`, `LateUpdate`, `FixedUpdate`, and tight loops.
- Profile before introducing complexity, but do not ignore obvious hot-path issues.
- Use pooling for frequently spawned objects when lifetime patterns justify it.

## Testing

- Favor EditMode tests for pure logic and contract validation.
- Use PlayMode tests for scene wiring and integration behavior.
- Write manual verification notes for feel-heavy gameplay when automation is not enough.

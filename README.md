# HorrorStealth

HorrorStealth is a 2D top-down stealth horror prototype built in Unity 6 with URP 2D.

The current playable loop is:

1. enter `RMainEscape_Lobby`
2. optionally enter `RMainEscape_tuto` for the tutorial support scene
3. start a fresh main run
4. descend through `RMainScene_5F -> RMainScene_4F -> RMainScene_3F -> RMainScene_2F -> RMainScene_1F`, with `RMainEscape_ElevatorTransition` masking floor-to-floor loads
5. see floor-clear, final-clear, or failure messaging
6. return to `RMainEscape_Lobby`

## Current Status

- Canonical playable flow:
  - `Assets/Scenes/RMainEscape_Lobby.unity`
  - `Assets/Scenes/RMainScene_5F.unity`
  - `Assets/Scenes/RMainScene_4F.unity`
  - `Assets/Scenes/RMainScene_3F.unity`
  - `Assets/Scenes/RMainScene_2F.unity`
  - `Assets/Scenes/RMainScene_1F.unity`
- Optional tutorial support scene:
  - `Assets/Scenes/RMainEscape_tuto.unity`
  - placed after the lobby in Build Settings; not part of the floor route list
- Interstitial support scene:
  - `Assets/Scenes/RMainEscape_ElevatorTransition.unity`
  - appended after the floor scenes in Build Settings; not part of the floor route list
- Authoring baseline:
  - the live `RMainEscape_Lobby -> RMainScene_5F~1F -> RMainEscape_Lobby` scene chain
  - direct scene and prefab placement inside that chain
  - feature components own their own Inspector references and scene-local data
    wherever that keeps the workflow simpler and more inspectable
- Runtime session flow:
  - `RRunSessionController`
  - `RSceneRouter`
  - `RSceneCompositionRoot`
  - `RRunController`
- Route graph authority:
  - `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`
  - serialized scene routing data and `MainEscapeRuntimeSettings` are compatibility/alignment fallback data, not route authority
- Playable content baseline:
  - the live authored scene chain and support-scene placement
- HUD status:
  - authored `IRHudCanvas` lives in the floor scenes
  - legacy HUD/setup rebuild tools are intentionally disabled in the clean-loop branch
- Enemy status:
  - imported ground and vent enemy sprite profiles are wired for runtime animation
  - chase/pathing logic now repaths around temporary blockers more aggressively
  - threat readability is reinforced through stronger visual feedback during investigate and chase
- Validation status:
  - lobby preflight validator exists
  - start-floor runtime validator exists
  - edit mode and play mode smoke tests exist under `Assets/Tests`

## Controls

- Move: `W`, `A`, `S`, `D`
- Sprint: `Left Shift`
- Interact: `E`
- Inventory: `I`
- Quick slots: number keys bound by runtime setup
- Confirm on run modals: `Enter` or `Space`
- Retry after failure: `R`
- Return to lobby from final-clear or failure modal: `L`
- Debug mode: `F1`
- Invincibility-only debug: `F2`
- Performance overlay: `F3`

When debug mode is on in the live floor scenes, the authored vent network is drawn over the map for quick route inspection.

## Important Scenes

- `Assets/Scenes/RMainEscape_Lobby.unity`
  - start-run hub
  - last-run summary
  - quit action
- `Assets/Scenes/RMainEscape_tuto.unity`
  - optional tutorial support scene outside the floor route list
- `Assets/Scenes/RMainScene_5F.unity`
- `Assets/Scenes/RMainScene_4F.unity`
- `Assets/Scenes/RMainScene_3F.unity`
- `Assets/Scenes/RMainScene_2F.unity`
- `Assets/Scenes/RMainScene_1F.unity`
  - authored gameplay floor scenes for the live loop
- `Assets/Scenes/RMainEscape_ElevatorTransition.unity`
  - lightweight one-image floor-load cover scene for elevator handoffs

## Important Editor Menu Items

- `Tools/Main Escape/Validate Lobby Scene Preflight`
- `Tools/Main Escape/Validate Start Floor Runtime`
- `Tools/Main Escape/Report Floor Scene Contracts`
- `Tools/Main Escape/Run Integrity + Recovery Prep`
- `Tools/Main Escape/Toggle Active Scene Debug Mode`
- `Tools/Main Escape Rebuild/Rebuild Live Floor Vent Routes (1F-4F)`
- `Tools/HorrorStealth/Run Main Escape Full Loop Smoke`
- `Tools/HorrorStealth/Capture Shadow Startle Preview`
- `Tools/Main Escape Rebuild/Cache Authored Floor References` (open authored floor scenes only)
- `Tools/Main Escape Rebuild/Legacy/Cache Legacy Resources Floor Prefab References`

Legacy setup menus that rebuild HUD, goal visuals, sentry placement, or ambient helper lighting are disabled on purpose in this branch. Maintain authored scene and prefab placement directly instead of regenerating runtime objects.
Normal cache and marker migration menus now operate on the active/open authored floor scenes instead of opening the entire floor route or touching `Assets/Resources/Floors/MainEscape/*.prefab`. Use `Tools/Main Escape Rebuild/Legacy/* Resources Floor Prefabs` actions only when intentionally working on those quarantined migration prefabs.
Auxiliary editor utilities such as loop smoke, preview capture, and integrity recovery prep are manual-only in this branch; there are no load-time trigger files or auto-launch hooks for them anymore.
`Tools/Main Escape/Recovery Prep Auto Run/Enable` exists only as an explicit opt-in for recovery sessions. Keep it disabled for normal editing so Unity does not enter Play Mode for runtime validation on project load.

## Art Pipeline Notes

- Source art sheets are organized under:
  - `Assets/Art/SourceSheets`
- Imported slices and extracted runtime art live under:
  - `Assets/Art/Imported`
  - `Assets/Resources/MainEscape/EnemyArt`
- Current imported sets include:
  - hospital tile replacements
  - prop and pickup replacements
  - roamer, stalker, and vent enemy sprite profiles

## Tests

- Edit mode tests:
  - `Assets/Tests/EditMode`
- Play mode tests:
  - `Assets/Tests/PlayMode`

The tests currently focus on runtime expectations, run-session flow, blocker/pathing validation, loop smoke coverage, and run-modal behavior for the `RMainEscape_Lobby -> RMainScene_5F~1F` route.

## Documentation

- [Docs/README.md](Docs/README.md)
- [Docs/status/MainEscapeCurrentState.md](Docs/status/MainEscapeCurrentState.md)
- [Docs/architecture/README.md](Docs/architecture/README.md)
- [Docs/design/README.md](Docs/design/README.md)
- [Docs/reference/README.md](Docs/reference/README.md)
- [Docs/checklists/README.md](Docs/checklists/README.md)
- [Docs/status/README.md](Docs/status/README.md)
- [Docs/GameDesignDocument.md](Docs/GameDesignDocument.md)
- [Docs/status/ImplementationRoadmap.md](Docs/status/ImplementationRoadmap.md)
- [Docs/MainEscapeAuthoringGuide.md](Docs/MainEscapeAuthoringGuide.md)
- [Docs/PrototypeArchitecture.md](Docs/PrototypeArchitecture.md)

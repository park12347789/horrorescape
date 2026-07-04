# HorrorStealth

2D top-down stealth horror survival prototype built with Unity 6 and URP 2D.

## Portfolio Links

- Windows build: [Google Drive download](https://drive.google.com/file/d/1l9Y87HL83ZdSxiUpCX_FdfcjUABYj9h8/view?usp=drivesdk)
- Pitch deck: [HorrorStealth Survival Blueprint](https://docs.google.com/presentation/d/1qa-PLc1CUWqdpsZsKStaQqg3Q3hVvJfoBjpBPoRS2KM/edit?slide=id.p1#slide=id.p1)
- Public repository: [park12347789/horrorescape](https://github.com/park12347789/horrorescape)

> The build file is stored on Google Drive as `HorrorStealth_Windows_Portfolio_2026-07-04.zip`.
> If Drive asks for access, set the file permission to "Anyone with the link can view" in Google Drive.

## How To Play

1. Download the Windows build zip from Google Drive.
2. Unzip it.
3. Run `HorrorStealth.exe`.

## Controls

- Move: `W`, `A`, `S`, `D`
- Sprint: `Left Shift`
- Interact: `E`
- Inventory: `I`
- Quick slots: number keys
- Confirm modal: `Enter` or `Space`
- Retry after failure: `R`
- Return to lobby after clear or failure: `L`
- Debug mode: `F1`
- Invincibility debug: `F2`
- Performance overlay: `F3`

## Game Loop

The player starts in the lobby, may enter the tutorial scene, then begins a main escape run from the 5th floor. The run descends floor by floor through `5F -> 4F -> 3F -> 2F -> 1F`, using an elevator transition scene between floors. The run ends with floor-clear, final-clear, or failure messaging before returning to the lobby.

## Portfolio Highlights

- Authored multi-floor escape route using direct scene and prefab placement.
- Unity 6 URP 2D rendering setup for top-down horror readability.
- Enemy patrol, investigation, chase, and vent-route pressure.
- Inventory, pickups, quick-slot use, and run-state feedback.
- Lobby, tutorial, floor route, elevator transition, clear, and failure flow.
- Editor validation and smoke-test utilities for scene contract checks.
- Portfolio build exporter for clean Windows build generation.

## Main Scenes

- `Assets/Scenes/RMainEscape_Lobby.unity`
- `Assets/Scenes/RMainEscape_tuto.unity`
- `Assets/Scenes/RMainScene_5F.unity`
- `Assets/Scenes/RMainScene_4F.unity`
- `Assets/Scenes/RMainScene_3F.unity`
- `Assets/Scenes/RMainScene_2F.unity`
- `Assets/Scenes/RMainScene_1F.unity`
- `Assets/Scenes/RMainEscape_ElevatorTransition.unity`

## Tech

- Unity `6000.3.9f1`
- Universal Render Pipeline 2D
- Unity Input System
- uGUI and TextMesh Pro
- Unity Test Framework

## Build

The portfolio build can be generated from Unity with:

- `Tools/Portfolio/Build Windows 64-bit`

The generated local output is:

- `Builds/Portfolio/HorrorStealth_Windows/HorrorStealth.exe`
- `Builds/Portfolio/HorrorStealth_Windows_BuildReport.txt`

Build artifacts are intentionally kept out of git and distributed through Google Drive.

## Documentation

- [Docs/README.md](Docs/README.md)
- [Docs/status/MainEscapeCurrentState.md](Docs/status/MainEscapeCurrentState.md)
- [Docs/architecture/README.md](Docs/architecture/README.md)
- [Docs/design/README.md](Docs/design/README.md)
- [Docs/reference/README.md](Docs/reference/README.md)
- [Docs/checklists/README.md](Docs/checklists/README.md)
- [Docs/GameDesignDocument.md](Docs/GameDesignDocument.md)

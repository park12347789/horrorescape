# R File Mapping

## Rule

- new runtime and gameplay files start with `R`
- new UI files start with `IR`
- old files stay in place until the live `R` loop is fully validated
- the clean loop keeps authored scene placement as the source of truth
- renamed or wrapped files must not reintroduce fallback or auto-create logic

## Scene Mapping

- `Assets/Scenes/MainEscape_Lobby.unity` -> `Assets/Scenes/RMainEscape_Lobby.unity`
- old single gameplay scene routes -> `Assets/Scenes/RMainScene_5F.unity` through `Assets/Scenes/RMainScene_1F.unity`

## Canonical Source Assets

- route-data source -> `RRunRoutingSettings.DefaultChapter -> RouteGraphDefinition`
- playable content baseline -> the live authored `RMainEscape_Lobby -> RMainScene_5F~1F -> RMainEscape_Lobby` chain and support-scene placement
- route compatibility/alignment fallback -> serialized `RRunSessionController` routing and `MainEscapeRuntimeSettings.asset`
- tutorial support source -> `Assets/Scenes/RMainEscape_tuto.unity`; outside `RFloorSceneEntry` arrays
- interstitial transition source -> `Assets/Scenes/RMainEscape_ElevatorTransition.unity` plus `RRunSessionController.ElevatorTransitionScenePath`; outside `RFloorSceneEntry` arrays
- route-alignment source -> `Assets/Resources/MainEscape/MainEscapeRuntimeSettings.asset`
- build routing alignment -> `ProjectSettings/EditorBuildSettings.asset`
- authored floor bridge source -> `Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs`
- player baseline source -> `Assets/Prefabs/Player.prefab`
- objective prefab sources -> `Assets/Prefabs/Items/MainEscape/Objectives/Pickup_FloorTool.prefab`, `Assets/Prefabs/Items/MainEscape/Objectives/Transition_EmergencyStairs.prefab`, `Assets/Prefabs/Items/MainEscape/Objectives/Transition_FinalExit.prefab`
- enemy prefab sources -> `Assets/Prefabs/Enemies/MainEscape/Ground/Enemy_GroundRuntime.prefab`, `Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab`

## Current Routing

- Build Settings scene `0` -> `Assets/Scenes/RMainEscape_Lobby.unity`
- Build Settings scene `1` -> `Assets/Scenes/RMainEscape_tuto.unity`
- Build Settings scene `2` -> `Assets/Scenes/RMainScene_5F.unity`
- Build Settings scene `3` -> `Assets/Scenes/RMainScene_4F.unity`
- Build Settings scene `4` -> `Assets/Scenes/RMainScene_3F.unity`
- Build Settings scene `5` -> `Assets/Scenes/RMainScene_2F.unity`
- Build Settings scene `6` -> `Assets/Scenes/RMainScene_1F.unity`
- Build Settings scene `7` -> `Assets/Scenes/RMainEscape_ElevatorTransition.unity`
- `MainEscapeRuntimeSettings.LobbyScenePath` -> `Assets/Scenes/RMainEscape_Lobby.unity`
- `MainEscapeRuntimeSettings.GameplayScenePath` -> `Assets/Scenes/RMainScene_5F.unity`
- `MainEscapeRuntimeSettings.MainScenePath` -> `Assets/Scenes/RMainScene_5F.unity`
- `RRunSessionController.ElevatorTransitionScenePath` -> `Assets/Scenes/RMainEscape_ElevatorTransition.unity`

## Runtime / Gameplay Script Mapping

- `MainEscapeLobbyController.cs` -> `IRLobbyController.cs`
- `MainEscapeRunSessionController.cs` -> `RRunSessionController.cs`
- `MainEscapeSceneCompositionRoot.cs` -> `RSceneCompositionRoot.cs`
- `MainEscapeFloorDirector.cs` -> `RFloorDirector.cs`
- `FloorEscapeRunController.cs` -> `RRunController.cs`
- `MainEscapePlayerRuntimeReferences.cs` -> `RPlayerRuntimeReferences.cs`
- `MainEscapeEncounterSpawner.cs` -> `REncounterSpawner.cs`

## Minimal Authored Set

The live `R` loop should keep these scene-facing pieces explicit:

- `RRunSessionController`
- `IRLobbyController`
- `RSceneCompositionRoot`
- `RFloorDirector`
- `RRunController`
- `RPlayerRuntimeReferences`
- `IRHudCanvas`
- `IRLobbyCanvas`
- `IRInventoryPanelView`
- `IRHealthPanelView`
- `IRQuickSlotsPanelView`
- `IRThreatPanelView`
- `IRGameClearPanelView`

Anything not required for that loop should stay out of the critical path until it is validated.

using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "MainEscapeRuntimeSettings",
    menuName = "Main Escape/Runtime Settings")]
public sealed class MainEscapeRuntimeSettings : ScriptableObject
{
    private const string ResourcePath = "MainEscape/MainEscapeRuntimeSettings";
    private static MainEscapeRuntimeSettings cachedSettings;

    [SerializeField] private SceneIdentitySettings scenes = new();
    [SerializeField] private HierarchySettings hierarchy = new();
    [SerializeField] private InteractableSettings interactables = new();
    [SerializeField] private AuthoringSettings authoring = new();
    [SerializeField] private PerformanceSettings performance = new();
    [SerializeField] private DebugModeSettings debug = new();
    [SerializeField] private HudSettings hud = new();
    [SerializeField] private UiThemeSettings uiTheme = new();
    [SerializeField] private ValidationSettings validation = new();

    // Scene route values are retained as fallback/alignment data. Runtime route
    // owners should prefer their serialized scene-local route configuration.
    public string PrototypeSceneName => FallbackPrototypeSceneName;
    public string VentDebugSceneName => FallbackVentDebugSceneName;
    public string PrimaryAuthoredSceneName => FallbackPrimaryAuthoredSceneName;
    public string SecondaryAuthoredSceneName => FallbackSecondaryAuthoredSceneName;
    public int AuthoredFloorNumber => FallbackStartFloorNumber;
    public RFloorSceneEntry[] FloorSceneRoutes => FallbackFloorSceneRoutes;

    public string FallbackPrototypeSceneName => scenes.PrototypeSceneName;
    public string FallbackVentDebugSceneName => scenes.VentDebugSceneName;
    public string FallbackPrimaryAuthoredSceneName => scenes.PrimaryAuthoredSceneName;
    public string FallbackSecondaryAuthoredSceneName => scenes.SecondaryAuthoredSceneName;
    public int FallbackStartFloorNumber => scenes.AuthoredFloorNumber;
    public RFloorSceneEntry[] FallbackFloorSceneRoutes => scenes.FloorSceneRoutes;

    // Hierarchy names are fallback lookup aliases for legacy/authored scenes.
    // Prefer explicit Inspector references on the component that owns behavior.
    public string PrototypeRootName => ReferencePrototypeRootName;
    public string SystemsRootName => ReferenceSystemsRootName;
    public string GameplayRootName => ReferenceGameplayRootName;
    public string FloorRuntimeRootName => ReferenceFloorRuntimeRootName;
    public string NoiseSystemRootName => ReferenceNoiseSystemRootName;
    public string NoiseSystemSceneObjectName => ReferenceNoiseSystemSceneObjectName;
    public string AudioManagerRootName => ReferenceAudioManagerRootName;
    public string FogOfWarOverlayRootName => ReferenceFogOfWarOverlayRootName;
    public string MainCameraObjectName => ReferenceMainCameraObjectName;
    public string GlobalLightObjectName => ReferenceGlobalLightObjectName;

    public string ReferencePrototypeRootName => hierarchy.PrototypeRootName;
    public string ReferenceSystemsRootName => hierarchy.SystemsRootName;
    public string ReferenceGameplayRootName => hierarchy.GameplayRootName;
    public string ReferenceFloorRuntimeRootName => hierarchy.FloorRuntimeRootName;
    public string ReferenceNoiseSystemRootName => hierarchy.NoiseSystemRootName;
    public string ReferenceNoiseSystemSceneObjectName => hierarchy.NoiseSystemSceneObjectName;
    public string ReferenceAudioManagerRootName => hierarchy.AudioManagerRootName;
    public string ReferenceFogOfWarOverlayRootName => hierarchy.FogOfWarOverlayRootName;
    public string ReferenceMainCameraObjectName => hierarchy.MainCameraObjectName;
    public string ReferenceGlobalLightObjectName => hierarchy.GlobalLightObjectName;

    public string FloorToolPickupName => interactables.FloorToolPickupName;
    public string EmergencyStairsName => interactables.EmergencyStairsName;
    public string FinalExitName => interactables.FinalExitName;
    public string BatteryRuntimePickupName => interactables.BatteryRuntimePickupName;
    public string GlassBottleRuntimePickupName => interactables.GlassBottleRuntimePickupName;
    public string MedkitRuntimePickupName => interactables.MedkitRuntimePickupName;
    public string GoalVisualsRootName => interactables.GoalVisualsRootName;
    public string KeyGateVisualName => interactables.KeyGateVisualName;
    public string EmergencyStairsVisualName => interactables.EmergencyStairsVisualName;
    public string AuthoredKeyPickupName => interactables.AuthoredKeyPickupName;
    public string AuthoredBatteryPickupName => interactables.AuthoredBatteryPickupName;
    public string AuthoredGlassBottlePickupName => interactables.AuthoredGlassBottlePickupName;
    public string AuthoredMedkitPickupName => interactables.AuthoredMedkitPickupName;
    public string[] KeyPickupSearchNames => interactables.KeyPickupSearchNames;
    public string[] BatteryPickupSearchNames => interactables.BatteryPickupSearchNames;
    public string[] GlassBottlePickupSearchNames => interactables.GlassBottlePickupSearchNames;
    public string[] MedkitPickupSearchNames => interactables.MedkitPickupSearchNames;

    public string[] EditorOnlyWorkspaceRootNames => authoring.EditorOnlyWorkspaceRootNames;
    public string[] BatteryMarkerNames => authoring.BatteryMarkerNames;
    public string[] GlassBottleMarkerNames => authoring.GlassBottleMarkerNames;
    public string WorkspaceRootName => authoring.WorkspaceRootName;
    public string EditableFloorRootName => authoring.EditableFloorRootName;
    public string AuthoringMarkersRootName => authoring.AuthoringMarkersRootName;
    public string SentryGuardsRootName => authoring.SentryGuardsRootName;
    public string GroundTilemapName => authoring.GroundTilemapName;
    public string WallTilemapName => authoring.WallTilemapName;
    public string DoorTilemapName => authoring.DoorTilemapName;
    public string CoverPropsRootName => authoring.CoverPropsRootName;
    public string DecorPropsRootName => authoring.DecorPropsRootName;
    public string InteractivePropsRootName => authoring.InteractivePropsRootName;
    public string VisualRootName => authoring.VisualRootName;
    public string VisualTilesRootName => authoring.VisualTilesRootName;
    public string VisualPropsRootName => authoring.VisualPropsRootName;
    public string GameplayOverlayRootName => authoring.GameplayOverlayRootName;
    public string BlockAllOverlayRootName => authoring.BlockAllOverlayRootName;
    public string MoveOnlyOverlayRootName => authoring.MoveOnlyOverlayRootName;
    public string PickupMarkersRootName => authoring.PickupMarkersRootName;
    public string ItemPlacementMarkersRootName => authoring.ItemPlacementMarkersRootName;
    public string KeyPlacementMarkersRootName => authoring.KeyPlacementMarkersRootName;
    public string EnemyPlacementMarkersRootName => authoring.EnemyPlacementMarkersRootName;
    public string ChaserPlacementMarkersRootName => authoring.ChaserPlacementMarkersRootName;
    public string PatrolSpawnRootName => authoring.PatrolSpawnRootName;
    public string PatrolSpawnCandidatesRootName => authoring.PatrolSpawnCandidatesRootName;
    public string PatrolRouteRootName => PatrolSpawnRootName;
    public string SentrySpawnCandidatesRootName => authoring.SentrySpawnCandidatesRootName;
    public string VentRouteRootName => authoring.VentRouteRootName;
    public string DangerMarkersRootName => authoring.DangerMarkersRootName;
    public string DoorMarkersRootName => authoring.DoorMarkersRootName;
    public string MovementBlockersRootName => authoring.MovementBlockersRootName;
    public string PlayerStartMarkerName => authoring.PlayerStartMarkerName;
    public string ToolMarkerName => authoring.ToolMarkerName;
    public string TransitionMarkerName => authoring.TransitionMarkerName;
    public string StalkerSpawnMarkerName => authoring.StalkerSpawnMarkerName;
    public string SafeRoomMarkerName => authoring.SafeRoomMarkerName;
    public string CellBlockLayoutRootName => authoring.CellBlockLayoutRootName;
    public string LayoutGroundRootName => authoring.LayoutGroundRootName;
    public string LayoutWallRootName => authoring.LayoutWallRootName;
    public string LayoutDoorRootName => authoring.LayoutDoorRootName;
    public string CellBlockPresetLibraryRootName => authoring.CellBlockPresetLibraryRootName;
    public string GameplayPresetLibraryRootName => authoring.GameplayPresetLibraryRootName;
    public string TemplateLibraryRootName => authoring.TemplateLibraryRootName;
    public int SentrySpawnResolveRadius => authoring.SentrySpawnResolveRadius;

    public int TargetFrameRate => performance.TargetFrameRate;
    public bool DisableVSyncForTargetFrameRate => performance.DisableVSyncForTargetFrameRate;

    public Key DebugToggleKey => debug.DebugToggleKey;
    public Key InvincibilityOnlyToggleKey => debug.InvincibilityOnlyToggleKey;
    public Key PerformanceOverlayToggleKey => debug.PerformanceOverlayToggleKey;
    public bool StartInDebugMode => debug.StartInDebugMode;
    public bool DebugMakesPlayerInvincible => debug.DebugMakesPlayerInvincible;
    public bool DebugShowsNoisePulses => debug.DebugShowsNoisePulses;
    public bool DebugShowsVentMarkers => debug.DebugShowsVentMarkers;
    public bool DebugShowsInventoryHud => debug.DebugShowsInventoryHud;
    public bool DebugShowsStatusOverlay => debug.DebugShowsStatusOverlay;
    public bool DebugDisablesFogOfWar => debug.DebugDisablesFogOfWar;
    public float DefaultFlashlightPresentationIntensityScale => debug.DefaultFlashlightPresentationIntensityScale;
    public float DefaultFlashlightPresentationVolumeScale => debug.DefaultFlashlightPresentationVolumeScale;
    public bool DefaultFlashlightShadowsEnabled => debug.DefaultFlashlightShadowsEnabled;
    public float DebugFlashlightPresentationIntensityScale => debug.DebugFlashlightPresentationIntensityScale;
    public float DebugFlashlightPresentationVolumeScale => debug.DebugFlashlightPresentationVolumeScale;
    public bool DebugFlashlightShadowsEnabled => debug.DebugFlashlightShadowsEnabled;

    public Vector2 HudReferenceResolution => hud.ReferenceResolution;
    public float HudReferenceResolutionMatch => hud.ReferenceResolutionMatch;
    public Vector2 InventoryPanelSize => hud.InventoryPanelSize;
    public Vector2 InventoryPanelMargin => hud.InventoryPanelMargin;
    public Vector2 InventorySlotSize => hud.InventorySlotSize;
    public Vector2 InventorySlotSpacing => hud.InventorySlotSpacing;
    public int InventorySlotColumnCount => hud.InventorySlotColumnCount;
    public int InventorySlotRowCount => hud.InventorySlotRowCount;
    public Vector2 QuickSlotPanelSize => hud.QuickSlotPanelSize;
    public Vector2 QuickSlotPanelMargin => hud.QuickSlotPanelMargin;
    public Vector2 QuickSlotCardSize => hud.QuickSlotCardSize;
    public Vector2 QuickSlotCardSpacing => hud.QuickSlotCardSpacing;
    public int QuickSlotVisibleCount => hud.QuickSlotVisibleCount;
    public Vector2 HealthPanelSize => hud.HealthPanelSize;
    public Vector2 HealthPanelMargin => hud.HealthPanelMargin;
    public bool UseTemporaryAnalogNoiseUi => uiTheme.UseTemporaryAnalogNoiseUi;

    public string LobbyScenePath => FallbackLobbyScenePath;
    public string GameplayScenePath => FallbackGameplayScenePath;
    public string MainScenePath => FallbackMainScenePath;
    public string FallbackLobbyScenePath => validation.LobbyScenePath;
    public string FallbackGameplayScenePath => validation.GameplayScenePath;
    public string FallbackMainScenePath => validation.MainScenePath;
    public string GetStartFloorScenePath() => GetFallbackStartFloorScenePath();
    public string[] GetGameplayScenePaths() => GetFallbackGameplayScenePaths();
    public bool TryGetScenePathForFloor(int floorNumber, out string scenePath) => TryGetFallbackScenePathForFloor(floorNumber, out scenePath);
    public bool TryGetFloorNumberForScene(string sceneName, out int floorNumber) => TryGetFallbackFloorNumberForScene(sceneName, out floorNumber);
    public string GetFallbackStartFloorScenePath() => scenes.GetStartFloorScenePath();
    public string[] GetFallbackGameplayScenePaths() => scenes.GetGameplayScenePaths();
    public bool TryGetFallbackScenePathForFloor(int floorNumber, out string scenePath) => scenes.TryGetScenePathForFloor(floorNumber, out scenePath);
    public bool TryGetFallbackFloorNumberForScene(string sceneName, out int floorNumber) => scenes.TryGetFloorNumberForScene(sceneName, out floorNumber);
    public int ExpectedStartFloorNumber => validation.ExpectedStartFloorNumber;
    public int ExpectedEmergencyStairsDestinationFloor => validation.ExpectedEmergencyStairsDestinationFloor;
    public bool LogOptionalAuthoringFallbackWarnings => validation.LogOptionalAuthoringFallbackWarnings;

    public bool IsSupportedScene(string sceneName) => IsFallbackSupportedScene(sceneName);
    public bool IsVentDebugScene(string sceneName) => IsFallbackVentDebugScene(sceneName);
    public bool IsAuthoredScene(string sceneName) => IsFallbackAuthoredScene(sceneName);
    public bool IsFallbackSupportedScene(string sceneName) => scenes.IsSupportedScene(sceneName);
    public bool IsFallbackVentDebugScene(string sceneName) => scenes.IsVentDebugScene(sceneName);
    public bool IsFallbackAuthoredScene(string sceneName) => scenes.IsAuthoredScene(sceneName);

    public static MainEscapeRuntimeSettings Load()
    {
        if (cachedSettings != null)
        {
            return cachedSettings;
        }

        MainEscapeRuntimeSettings loadedSettings = Resources.Load<MainEscapeRuntimeSettings>(ResourcePath);

        if (loadedSettings == null)
        {
            throw new InvalidOperationException(
                $"Missing '{nameof(MainEscapeRuntimeSettings)}' resource at 'Resources/{ResourcePath}.asset'. " +
                "Fix the asset path or restore the missing asset before continuing.");
        }

        cachedSettings = loadedSettings;
        return loadedSettings;
    }

    [Serializable]
    private sealed class SceneIdentitySettings
    {
        [SerializeField] private string prototypeSceneName = string.Empty;
        [SerializeField] private string ventDebugSceneName = string.Empty;
        [SerializeField] private string[] authoredSceneNames = MainEscapeSceneIdentityUtility.GetCanonicalAuthoredSceneNames();
        [SerializeField] private int authoredFloorNumber = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
        [SerializeField] private RFloorSceneEntry[] floorSceneRoutes = MainEscapeSceneIdentityUtility.GetCanonicalFloorSceneEntries();

        public string PrototypeSceneName => prototypeSceneName?.Trim() ?? string.Empty;
        public string VentDebugSceneName => ventDebugSceneName?.Trim() ?? string.Empty;
        public string PrimaryAuthoredSceneName => GetNameAt(
            authoredSceneNames,
            0,
            MainEscapeSceneIdentityUtility.GetCanonicalAuthoredSceneNames()[0]);
        public string SecondaryAuthoredSceneName => GetNameAt(authoredSceneNames, 1, PrimaryAuthoredSceneName);
        public int AuthoredFloorNumber => authoredFloorNumber > 0
            ? authoredFloorNumber
            : MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
        public RFloorSceneEntry[] FloorSceneRoutes => SanitizeFloorSceneRoutes(floorSceneRoutes);

        public bool IsSupportedScene(string sceneName)
        {
            return IsAuthoredScene(sceneName);
        }

        public bool IsVentDebugScene(string sceneName)
        {
            return false;
        }

        public bool IsAuthoredScene(string sceneName)
        {
            return MatchesAny(sceneName, authoredSceneNames, PrimaryAuthoredSceneName);
        }

        public bool TryGetFloorNumberForScene(string sceneName, out int floorNumber)
        {
            RFloorSceneEntry[] routes = FloorSceneRoutes;

            for (int index = 0; index < routes.Length; index++)
            {
                RFloorSceneEntry route = routes[index];

                if (route.floorNumber <= 0 || string.IsNullOrWhiteSpace(route.scenePath))
                {
                    continue;
                }

                if (MainEscapeSceneIdentityUtility.MatchesScenePath(sceneName, route.scenePath))
                {
                    floorNumber = route.floorNumber;
                    return true;
                }
            }

            floorNumber = 0;
            return false;
        }

        public bool TryGetScenePathForFloor(int floorNumber, out string scenePath)
        {
            RFloorSceneEntry[] routes = FloorSceneRoutes;

            for (int index = 0; index < routes.Length; index++)
            {
                RFloorSceneEntry route = routes[index];

                if (route.floorNumber == Mathf.Max(1, floorNumber) && !string.IsNullOrWhiteSpace(route.scenePath))
                {
                    scenePath = route.scenePath.Trim();
                    return true;
                }
            }

            scenePath = string.Empty;
            return false;
        }

        public string GetStartFloorScenePath()
        {
            return TryGetScenePathForFloor(AuthoredFloorNumber, out string scenePath)
                ? scenePath
                : MainEscapeSceneIdentityUtility.GetCanonicalStartFloorScenePath();
        }

        public string[] GetGameplayScenePaths()
        {
            RFloorSceneEntry[] routes = FloorSceneRoutes;

            if (routes.Length == 0)
            {
                return Array.Empty<string>();
            }

            string[] scenePaths = new string[routes.Length];

            for (int index = 0; index < routes.Length; index++)
            {
                scenePaths[index] = routes[index].scenePath?.Trim() ?? string.Empty;
            }

            return scenePaths;
        }

        private static RFloorSceneEntry[] SanitizeFloorSceneRoutes(RFloorSceneEntry[] routes)
        {
            if (routes == null || routes.Length == 0)
            {
                return Array.Empty<RFloorSceneEntry>();
            }

            int validCount = 0;

            for (int index = 0; index < routes.Length; index++)
            {
                if (routes[index].floorNumber > 0)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return Array.Empty<RFloorSceneEntry>();
            }

            RFloorSceneEntry[] sanitized = new RFloorSceneEntry[validCount];
            int writeIndex = 0;

            for (int index = 0; index < routes.Length; index++)
            {
                RFloorSceneEntry route = routes[index];

                if (route.floorNumber > 0)
                {
                    sanitized[writeIndex++] = route;
                }
            }

            return sanitized;
        }
    }

    [Serializable]
    private sealed class HierarchySettings
    {
        [SerializeField] private string prototypeRootName = "MainEscapePrototypeRoot";
        [SerializeField] private string systemsRootName = "Systems";
        [SerializeField] private string gameplayRootName = "Gameplay";
        [SerializeField] private string floorRuntimeRootName = "FloorRuntime";
        [SerializeField] private string noiseSystemRootName = "NoiseSystem";
        [SerializeField] private string noiseSystemSceneObjectName = "PrototypeNoiseSystem";
        [SerializeField] private string audioManagerRootName = "PrototypeAudioManager";
        [SerializeField] private string fogOfWarOverlayRootName = "FogOfWarOverlay";
        [SerializeField] private string mainCameraObjectName = "Main Camera";
        [SerializeField] private string globalLightObjectName = "Global Light 2D";

        public string PrototypeRootName => DefaultIfBlank(prototypeRootName, "MainEscapePrototypeRoot");
        public string SystemsRootName => DefaultIfBlank(systemsRootName, "Systems");
        public string GameplayRootName => DefaultIfBlank(gameplayRootName, "Gameplay");
        public string FloorRuntimeRootName => DefaultIfBlank(floorRuntimeRootName, "FloorRuntime");
        public string NoiseSystemRootName => DefaultIfBlank(noiseSystemRootName, "NoiseSystem");
        public string NoiseSystemSceneObjectName => DefaultIfBlank(noiseSystemSceneObjectName, "PrototypeNoiseSystem");
        public string AudioManagerRootName => DefaultIfBlank(audioManagerRootName, "PrototypeAudioManager");
        public string FogOfWarOverlayRootName => DefaultIfBlank(fogOfWarOverlayRootName, "FogOfWarOverlay");
        public string MainCameraObjectName => DefaultIfBlank(mainCameraObjectName, "Main Camera");
        public string GlobalLightObjectName => DefaultIfBlank(globalLightObjectName, "Global Light 2D");
    }

    [Serializable]
    private sealed class InteractableSettings
    {
        [SerializeField] private string floorToolPickupName = "FloorTool";
        [SerializeField] private string emergencyStairsName = "EmergencyStairs";
        [SerializeField] private string finalExitName = "StreetExit";
        [SerializeField] private string batteryRuntimePickupName = "FlashlightBatteryPickup";
        [SerializeField] private string glassBottleRuntimePickupName = "GlassBottlePickup";
        [SerializeField] private string medkitRuntimePickupName = "MedkitPickup";
        [SerializeField] private string goalVisualsRootName = "GoalVisuals";
        [SerializeField] private string keyGateVisualName = "KeyGateVisual";
        [SerializeField] private string emergencyStairsVisualName = "EmergencyStairsVisual";
        [SerializeField] private string[] keyPickupSearchNames = { "Key" };
        [SerializeField] private string[] batteryPickupSearchNames = { "Pickup_Battery", "FlashlightBatteryPickup" };
        [SerializeField] private string[] glassBottlePickupSearchNames = { "Pickup_GlassBottle", "GlassBottlePickup" };
        [SerializeField] private string[] medkitPickupSearchNames = { "Pickup_Medkit", "MedkitPickup" };

        public string FloorToolPickupName => DefaultIfBlank(floorToolPickupName, "FloorTool");
        public string EmergencyStairsName => DefaultIfBlank(emergencyStairsName, "EmergencyStairs");
        public string FinalExitName => DefaultIfBlank(finalExitName, "StreetExit");
        public string BatteryRuntimePickupName => DefaultIfBlank(batteryRuntimePickupName, "FlashlightBatteryPickup");
        public string GlassBottleRuntimePickupName => DefaultIfBlank(glassBottleRuntimePickupName, "GlassBottlePickup");
        public string MedkitRuntimePickupName => DefaultIfBlank(medkitRuntimePickupName, "MedkitPickup");
        public string GoalVisualsRootName => DefaultIfBlank(goalVisualsRootName, "GoalVisuals");
        public string KeyGateVisualName => DefaultIfBlank(keyGateVisualName, "KeyGateVisual");
        public string EmergencyStairsVisualName => DefaultIfBlank(emergencyStairsVisualName, "EmergencyStairsVisual");
        public string AuthoredKeyPickupName => GetNameAt(keyPickupSearchNames, 0, "Key");
        public string AuthoredBatteryPickupName => GetNameAt(batteryPickupSearchNames, 0, "Pickup_Battery");
        public string AuthoredGlassBottlePickupName => GetNameAt(glassBottlePickupSearchNames, 0, "Pickup_GlassBottle");
        public string AuthoredMedkitPickupName => GetNameAt(medkitPickupSearchNames, 0, "Pickup_Medkit");
        public string[] KeyPickupSearchNames => SanitizeNames(keyPickupSearchNames, "Key");
        public string[] BatteryPickupSearchNames => SanitizeNames(batteryPickupSearchNames, "Pickup_Battery", BatteryRuntimePickupName);
        public string[] GlassBottlePickupSearchNames => SanitizeNames(glassBottlePickupSearchNames, "Pickup_GlassBottle", GlassBottleRuntimePickupName);
        public string[] MedkitPickupSearchNames => SanitizeNames(medkitPickupSearchNames, "Pickup_Medkit", MedkitRuntimePickupName);
    }

    [Serializable]
    private sealed class AuthoringSettings
    {
        [SerializeField] private string workspaceRootName = "MainEscapeEditingWorkspace";
        [SerializeField] private string editableFloorRootName = "EditableFloor";
        [SerializeField] private string authoringMarkersRootName = "AuthoringMarkers";
        [SerializeField] private string sentryGuardsRootName = "SentryGuards";
        [SerializeField] private string groundTilemapName = "Ground";
        [SerializeField] private string wallTilemapName = "Walls";
        [SerializeField] private string doorTilemapName = "Doors";
        [SerializeField] private string coverPropsRootName = "CoverProps";
        [SerializeField] private string decorPropsRootName = "DecorProps";
        [SerializeField] private string interactivePropsRootName = "InteractiveProps";
        [SerializeField] private string visualRootName = "Visual";
        [SerializeField] private string visualTilesRootName = "Tiles";
        [SerializeField] private string visualPropsRootName = "Props";
        [SerializeField] private string gameplayOverlayRootName = "GameplayOverlay";
        [SerializeField] private string blockAllOverlayRootName = "BlockAll";
        [SerializeField] private string moveOnlyOverlayRootName = "MoveOnly";
        [SerializeField] private string pickupMarkersRootName = "PickupMarkers";
        [SerializeField] private string itemPlacementMarkersRootName = "ItemPlacementMarkers";
        [SerializeField] private string keyPlacementMarkersRootName = "KeyPlacementMarkers";
        [SerializeField] private string enemyPlacementMarkersRootName = "EnemyPlacementMarkers";
        [SerializeField] private string chaserPlacementMarkersRootName = "ChaserPlacementMarkers";
        [FormerlySerializedAs("patrolRouteRootName")]
        [SerializeField] private string patrolSpawnRootName = "PatrolSpawn";
        [SerializeField] private string patrolSpawnCandidatesRootName = "PatrolSpawnCandidates";
        [SerializeField] private string sentrySpawnCandidatesRootName = "SentrySpawnCandidates";
        [SerializeField] private string ventRouteRootName = "VentRoute";
        [SerializeField] private string dangerMarkersRootName = "DangerMarkers";
        [SerializeField] private string doorMarkersRootName = "Doors";
        [SerializeField] private string movementBlockersRootName = "MovementBlockers";
        [SerializeField] private string playerStartMarkerName = "PlayerStart";
        [SerializeField] private string toolMarkerName = "Tool";
        [SerializeField] private string transitionMarkerName = "Transition";
        [SerializeField] private string stalkerSpawnMarkerName = "StalkerSpawn";
        [SerializeField] private string safeRoomMarkerName = "SafeRoom";
        [SerializeField] private string cellBlockLayoutRootName = "CellBlockLayout_Edit_This";
        [SerializeField] private string layoutGroundRootName = "GroundCells";
        [SerializeField] private string layoutWallRootName = "WallCells";
        [SerializeField] private string layoutDoorRootName = "DoorCells";
        [SerializeField] private string cellBlockPresetLibraryRootName = "CellBlockPresetLibrary_Copy_From_Here";
        [SerializeField] private string gameplayPresetLibraryRootName = "GameplayPlaceablePresetLibrary_Copy_From_Here";
        [SerializeField] private string templateLibraryRootName = "TemplateLibrary_Copy_From_Here";
        [SerializeField, Min(0)] private int sentrySpawnResolveRadius = 3;
        [SerializeField] private string[] editorOnlyWorkspaceRootNames =
        {
            "QuickStart",
            "CellBlockPresetLibrary_Copy_From_Here",
            "GameplayPlaceablePresetLibrary_Copy_From_Here"
        };

        [SerializeField] private string[] batteryMarkerNames = { "Pickup_BatteryMarker", "Battery", "BatteryMarker" };
        [SerializeField] private string[] glassBottleMarkerNames = { "Pickup_GlassBottleMarker", "GlassPanel", "GlassPanelMarker" };

        public string WorkspaceRootName => DefaultIfBlank(workspaceRootName, "MainEscapeEditingWorkspace");
        public string EditableFloorRootName => DefaultIfBlank(editableFloorRootName, "EditableFloor_MainScene_Play");
        public string AuthoringMarkersRootName => DefaultIfBlank(authoringMarkersRootName, "AuthoringMarkers");
        public string SentryGuardsRootName => DefaultIfBlank(sentryGuardsRootName, "SentryGuards");
        public string GroundTilemapName => DefaultIfBlank(groundTilemapName, "Ground");
        public string WallTilemapName => DefaultIfBlank(wallTilemapName, "Walls");
        public string DoorTilemapName => DefaultIfBlank(doorTilemapName, "Doors");
        public string CoverPropsRootName => DefaultIfBlank(coverPropsRootName, "CoverProps");
        public string DecorPropsRootName => DefaultIfBlank(decorPropsRootName, "DecorProps");
        public string InteractivePropsRootName => DefaultIfBlank(interactivePropsRootName, "InteractiveProps");
        public string VisualRootName => DefaultIfBlank(visualRootName, "Visual");
        public string VisualTilesRootName => DefaultIfBlank(visualTilesRootName, "Tiles");
        public string VisualPropsRootName => DefaultIfBlank(visualPropsRootName, "Props");
        public string GameplayOverlayRootName => DefaultIfBlank(gameplayOverlayRootName, "GameplayOverlay");
        public string BlockAllOverlayRootName => DefaultIfBlank(blockAllOverlayRootName, "BlockAll");
        public string MoveOnlyOverlayRootName => DefaultIfBlank(moveOnlyOverlayRootName, "MoveOnly");
        public string PickupMarkersRootName => DefaultIfBlank(pickupMarkersRootName, "PickupMarkers");
        public string ItemPlacementMarkersRootName => DefaultIfBlank(itemPlacementMarkersRootName, "ItemPlacementMarkers");
        public string KeyPlacementMarkersRootName => DefaultIfBlank(keyPlacementMarkersRootName, "KeyPlacementMarkers");
        public string EnemyPlacementMarkersRootName => DefaultIfBlank(enemyPlacementMarkersRootName, "EnemyPlacementMarkers");
        public string ChaserPlacementMarkersRootName => DefaultIfBlank(chaserPlacementMarkersRootName, "ChaserPlacementMarkers");
        public string PatrolSpawnRootName => DefaultIfBlank(patrolSpawnRootName, "PatrolSpawn");
        public string PatrolSpawnCandidatesRootName => DefaultIfBlank(patrolSpawnCandidatesRootName, "PatrolSpawnCandidates");
        public string SentrySpawnCandidatesRootName => DefaultIfBlank(sentrySpawnCandidatesRootName, "SentrySpawnCandidates");
        public string VentRouteRootName => DefaultIfBlank(ventRouteRootName, "VentRoute");
        public string DangerMarkersRootName => DefaultIfBlank(dangerMarkersRootName, "DangerMarkers");
        public string DoorMarkersRootName => DefaultIfBlank(doorMarkersRootName, "Doors");
        public string MovementBlockersRootName => DefaultIfBlank(movementBlockersRootName, "MovementBlockers");
        public string PlayerStartMarkerName => DefaultIfBlank(playerStartMarkerName, "PlayerStart");
        public string ToolMarkerName => DefaultIfBlank(toolMarkerName, "Tool");
        public string TransitionMarkerName => DefaultIfBlank(transitionMarkerName, "Transition");
        public string StalkerSpawnMarkerName => DefaultIfBlank(stalkerSpawnMarkerName, "StalkerSpawn");
        public string SafeRoomMarkerName => DefaultIfBlank(safeRoomMarkerName, "SafeRoom");
        public string CellBlockLayoutRootName => DefaultIfBlank(cellBlockLayoutRootName, "CellBlockLayout_Edit_This");
        public string LayoutGroundRootName => DefaultIfBlank(layoutGroundRootName, "GroundCells");
        public string LayoutWallRootName => DefaultIfBlank(layoutWallRootName, "WallCells");
        public string LayoutDoorRootName => DefaultIfBlank(layoutDoorRootName, "DoorCells");
        public string CellBlockPresetLibraryRootName => DefaultIfBlank(cellBlockPresetLibraryRootName, "CellBlockPresetLibrary_Copy_From_Here");
        public string GameplayPresetLibraryRootName => DefaultIfBlank(gameplayPresetLibraryRootName, "GameplayPlaceablePresetLibrary_Copy_From_Here");
        public string TemplateLibraryRootName => DefaultIfBlank(templateLibraryRootName, "TemplateLibrary_Copy_From_Here");
        public int SentrySpawnResolveRadius => sentrySpawnResolveRadius > 0 ? sentrySpawnResolveRadius : 3;
        public string[] EditorOnlyWorkspaceRootNames => SanitizeNames(
            editorOnlyWorkspaceRootNames,
            "QuickStart",
            "CellBlockPresetLibrary_Copy_From_Here",
            "GameplayPlaceablePresetLibrary_Copy_From_Here");

        public string[] BatteryMarkerNames => SanitizeNames(batteryMarkerNames, "Pickup_BatteryMarker", "Battery", "BatteryMarker");
        public string[] GlassBottleMarkerNames => SanitizeNames(glassBottleMarkerNames, "Pickup_GlassBottleMarker", "GlassPanel", "GlassPanelMarker");
    }

    [Serializable]
    private sealed class PerformanceSettings
    {
        [SerializeField, Min(-1)] private int targetFrameRate = 60;
        [SerializeField] private bool disableVSyncForTargetFrameRate = true;

        public int TargetFrameRate => targetFrameRate < 0 ? -1 : targetFrameRate;
        public bool DisableVSyncForTargetFrameRate => disableVSyncForTargetFrameRate;
    }

    [Serializable]
    private sealed class DebugModeSettings
    {
        [SerializeField] private Key debugToggleKey = Key.F1;
        [SerializeField] private Key invincibilityOnlyToggleKey = Key.F2;
        [SerializeField] private Key performanceOverlayToggleKey = Key.F3;
        [SerializeField] private bool startInDebugMode;
        [SerializeField] private bool debugMakesPlayerInvincible = true;
        [SerializeField] private bool debugShowsNoisePulses = true;
        [SerializeField] private bool debugShowsVentMarkers = true;
        [SerializeField] private bool debugShowsInventoryHud = true;
        [SerializeField] private bool debugShowsStatusOverlay = true;
        [SerializeField] private bool debugDisablesFogOfWar = true;
        [SerializeField, Min(0f)] private float defaultFlashlightPresentationIntensityScale = 0.76f;
        [SerializeField, Range(0f, 1f)] private float defaultFlashlightPresentationVolumeScale = 0.38f;
        [SerializeField] private bool defaultFlashlightShadowsEnabled;
        [SerializeField, Min(0f)] private float debugFlashlightPresentationIntensityScale = 1f;
        [SerializeField, Range(0f, 1f)] private float debugFlashlightPresentationVolumeScale = 0f;
        [SerializeField] private bool debugFlashlightShadowsEnabled;

        public Key DebugToggleKey => debugToggleKey;
        public Key InvincibilityOnlyToggleKey => invincibilityOnlyToggleKey;
        public Key PerformanceOverlayToggleKey => performanceOverlayToggleKey;
        public bool StartInDebugMode => startInDebugMode;
        public bool DebugMakesPlayerInvincible => debugMakesPlayerInvincible;
        public bool DebugShowsNoisePulses => debugShowsNoisePulses;
        public bool DebugShowsVentMarkers => debugShowsVentMarkers;
        public bool DebugShowsInventoryHud => debugShowsInventoryHud;
        public bool DebugShowsStatusOverlay => debugShowsStatusOverlay;
        public bool DebugDisablesFogOfWar => debugDisablesFogOfWar;
        public float DefaultFlashlightPresentationIntensityScale => Mathf.Max(0f, defaultFlashlightPresentationIntensityScale);
        public float DefaultFlashlightPresentationVolumeScale => Mathf.Clamp01(defaultFlashlightPresentationVolumeScale);
        public bool DefaultFlashlightShadowsEnabled => defaultFlashlightShadowsEnabled;
        public float DebugFlashlightPresentationIntensityScale => Mathf.Max(0f, debugFlashlightPresentationIntensityScale);
        public float DebugFlashlightPresentationVolumeScale => Mathf.Clamp01(debugFlashlightPresentationVolumeScale);
        public bool DebugFlashlightShadowsEnabled => debugFlashlightShadowsEnabled;
    }

    [Serializable]
    private sealed class HudSettings
    {
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
        [SerializeField, Range(0f, 1f)] private float referenceResolutionMatch = 0.5f;
        [SerializeField] private Vector2 inventoryPanelSize = new(364f, 430f);
        [SerializeField] private Vector2 inventoryPanelMargin = new(18f, 18f);
        [SerializeField] private Vector2 inventorySlotSize = new(58f, 58f);
        [SerializeField] private Vector2 inventorySlotSpacing = new(6f, 6f);
        [SerializeField, Min(1)] private int inventorySlotColumnCount = 5;
        [SerializeField, Min(1)] private int inventorySlotRowCount = 5;
        [SerializeField] private Vector2 quickSlotPanelSize = new(396f, 120f);
        [SerializeField] private Vector2 quickSlotPanelMargin = new(18f, 0f);
        [SerializeField] private Vector2 quickSlotCardSize = new(108f, 108f);
        [SerializeField] private Vector2 quickSlotCardSpacing = new(8f, 0f);
        [SerializeField, Min(1)] private int quickSlotVisibleCount = 3;
        [SerializeField] private Vector2 healthPanelSize = new(390f, 138f);
        [SerializeField] private Vector2 healthPanelMargin = new(18f, 18f);

        public Vector2 ReferenceResolution => SanitizeVector(referenceResolution, new Vector2(1920f, 1080f));
        public float ReferenceResolutionMatch => Mathf.Clamp01(referenceResolutionMatch);
        public Vector2 InventoryPanelSize => SanitizeVector(inventoryPanelSize, new Vector2(408f, 392f));
        public Vector2 InventoryPanelMargin => SanitizeVector(inventoryPanelMargin, new Vector2(18f, 18f));
        public Vector2 InventorySlotSize => SanitizeVector(inventorySlotSize, new Vector2(112f, 112f));
        public Vector2 InventorySlotSpacing => SanitizeVector(inventorySlotSpacing, new Vector2(12f, 12f));
        public int InventorySlotColumnCount => Mathf.Max(1, inventorySlotColumnCount == 0 ? 3 : inventorySlotColumnCount);
        public int InventorySlotRowCount => Mathf.Max(1, inventorySlotRowCount == 0 ? 2 : inventorySlotRowCount);
        public Vector2 QuickSlotPanelSize => SanitizeVector(quickSlotPanelSize, new Vector2(396f, 120f));
        public Vector2 QuickSlotPanelMargin => SanitizeVector(quickSlotPanelMargin, new Vector2(18f, 12f));
        public Vector2 QuickSlotCardSize => SanitizeVector(quickSlotCardSize, new Vector2(108f, 108f));
        public Vector2 QuickSlotCardSpacing => SanitizeVector(quickSlotCardSpacing, new Vector2(12f, 0f));
        public int QuickSlotVisibleCount => Mathf.Max(1, quickSlotVisibleCount);
        public Vector2 HealthPanelSize => SanitizeVector(healthPanelSize, new Vector2(390f, 138f));
        public Vector2 HealthPanelMargin => SanitizeVector(healthPanelMargin, new Vector2(18f, 18f));
    }

    [Serializable]
    private sealed class UiThemeSettings
    {
        [SerializeField] private bool useTemporaryAnalogNoiseUi = false;

        public bool UseTemporaryAnalogNoiseUi => useTemporaryAnalogNoiseUi;
    }

    [Serializable]
    private sealed class ValidationSettings
    {
        [SerializeField] private string lobbyScenePath = MainEscapeSceneIdentityUtility.GetCanonicalLobbyScenePath();
        [SerializeField] private string gameplayScenePath = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorScenePath();
        [SerializeField] private string mainScenePath = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorScenePath();
        [SerializeField] private int expectedStartFloorNumber = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
        [SerializeField] private int expectedEmergencyStairsDestinationFloor = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber() - 1;
        [SerializeField] private bool logOptionalAuthoringFallbackWarnings;

        public string LobbyScenePath => DefaultIfBlank(lobbyScenePath, MainEscapeSceneIdentityUtility.GetCanonicalLobbyScenePath());
        public string GameplayScenePath => DefaultIfBlank(gameplayScenePath, MainEscapeSceneIdentityUtility.GetCanonicalStartFloorScenePath());
        public string MainScenePath => DefaultIfBlank(mainScenePath, GameplayScenePath);
        public int ExpectedStartFloorNumber => expectedStartFloorNumber > 0
            ? expectedStartFloorNumber
            : MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
        public int ExpectedEmergencyStairsDestinationFloor => expectedEmergencyStairsDestinationFloor > 0
            ? expectedEmergencyStairsDestinationFloor
            : MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber() - 1;
        public bool LogOptionalAuthoringFallbackWarnings => logOptionalAuthoringFallbackWarnings;
    }

    private static string DefaultIfBlank(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static Vector2 SanitizeVector(Vector2 value, Vector2 fallback)
    {
        return value.x > 0.001f && value.y > 0.001f ? value : fallback;
    }

    private static string GetNameAt(string[] names, int index, string fallback)
    {
        if (names != null && index >= 0 && index < names.Length && !string.IsNullOrWhiteSpace(names[index]))
        {
            return names[index];
        }

        return fallback;
    }

    private static bool MatchesAny(string value, string[] names, string fallback)
    {
        string[] candidates = SanitizeNames(names, fallback);

        for (int index = 0; index < candidates.Length; index++)
        {
            if (Matches(value, candidates[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(string value, string candidate)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(candidate)
            && string.Equals(value, candidate, StringComparison.Ordinal);
    }

    private static string[] SanitizeNames(string[] preferredNames, params string[] fallbackNames)
    {
        if (HasValues(preferredNames))
        {
            return FilterEmpty(preferredNames);
        }

        return HasValues(fallbackNames)
            ? FilterEmpty(fallbackNames)
            : Array.Empty<string>();
    }

    private static bool HasValues(string[] names)
    {
        if (names == null)
        {
            return false;
        }

        for (int index = 0; index < names.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(names[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] FilterEmpty(string[] names)
    {
        if (names == null || names.Length == 0)
        {
            return Array.Empty<string>();
        }

        int validCount = 0;

        for (int index = 0; index < names.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(names[index]))
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return Array.Empty<string>();
        }

        string[] filteredNames = new string[validCount];
        int filteredIndex = 0;

        for (int index = 0; index < names.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(names[index]))
            {
                filteredNames[filteredIndex++] = names[index];
            }
        }

        return filteredNames;
    }
}

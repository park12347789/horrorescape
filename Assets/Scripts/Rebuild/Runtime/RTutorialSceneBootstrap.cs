using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;

using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RTutorialSceneBootstrap : MonoBehaviour
{
    private const string AuthoringRootName = "RTutorialAuthoring";
    private const string TutorialTileGridName = "RTutorialTileGrid";
    private const string TutorialGroundTilemapName = "Tiles_ground";
    private const string TutorialWallTilemapName = "Tiles_wall";
    private const string TutorialMoveOnlyTilemapName = "Tiles_movenonlywall";
    private const string TutorialDoorTilemapName = "Doors";
    private const string TutorialMapServiceName = "RTutorialMapService";
    private const string TutorialNoiseSystemName = "RNoiseSystem";
    private const string TutorialRuntimeEnemyRootName = "RTutorialRuntimeEnemies";
    private const string TutorialRuntimeHazardRootName = "RTutorialRuntimeHazards";
    private const string TutorialDoorInteractionsRootName = "RTutorialDoorInteractions";
    private const string TutorialWallBlockersRootName = "WallBlockers";
    private const string TutorialMoveOnlyBlockersRootName = "MoveOnlyBlockers";
    private const string TutorialVisualPropBlockersRootName = "RuntimePropBlockers";
    private const string TutorialDoorShadowBlockersRootName = "DoorShadowBlockers";
    private const string TutorialGlobalLightName = "RGlobalLight";
    private const string DirectExitElevatorVisualName = "RDirectExitElevatorVisual";
    private const string FrontDoorVisualName = "VexedTileBProp_01_Top (8)";
    private const string FrontDoorVisualNamePrefix = "VexedTileBProp_01_Top";
    private const string SideDoorVisualName = "CustomSideDoorClosed";
    private const string TutorialMoveOnlyVisualPropPrefix = "VexedTileBProp_15";
    private const string TutorialSentryMarkerName = "SentrySpawnMarker_01";
    private const string TutorialGlassTrapNamePrefix = "TutorialGlassTrap_";
    private const int BackdropSortingOrder = -20;
    private const int CueSortingOrder = 130;
#if UNITY_EDITOR
    private const string EditorSquareSpritePath = "Assets/Art/GameplaySprites/RTutorialSquareSprite.png";
    private const string EditorHudCanvasPrefabPath = "Assets/Prefabs/IRHudCanvas.prefab";
    private const string EditorAuthoredGameplayHudPrefabPath = "Assets/Prefabs/RAuthoredGameplayHudCanvas.prefab";
#endif

    private static Sprite sharedSquareSprite;
    private static bool materializingForEditMode;

    [Header("Runtime Rules")]
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private bool materializeAuthoringInEditMode;
    [SerializeField] private bool disableFloorRuntimeSystems = true;
    [SerializeField] private bool resetPlayerInventoryOnStart = true;
    [SerializeField] private bool forceNoFlashlight = true;
    [SerializeField] private bool spawnPlayerIfMissing = true;
    [SerializeField] private bool materializeDefaultTutorialLayout;
    [SerializeField, Min(1)] private int startingHealth = 1;
    [SerializeField, Range(0f, 1f)] private float startingFlashlightChargeNormalized;

    [Header("Tutorial Sentry")]
    [SerializeField] private bool spawnTutorialSentryOnStart = true;
    [SerializeField] private string tutorialSentryName = "SentryGuard_01";
    [SerializeField] private Vector2 tutorialSentrySpawnPosition = new(1.45f, 0f);
    [SerializeField] private Vector2 tutorialSentryFacingDirection = Vector2.left;

    [Header("Authored Root")]
    [SerializeField] private Transform authoringRoot;

    [Header("Default Layout")]
    [SerializeField] private Vector2 playerSpawnPosition = new(-5.8f, 0f);
    [SerializeField] private Vector2 flashlightPickupPosition = new(-4.4f, 0f);
    [SerializeField] private Vector2 medkitPickupPosition = new(-3.35f, -0.75f);
    [SerializeField] private Vector2 batteryPickupPosition = new(-3.35f, 0.75f);
    [SerializeField] private Vector2 bottlePickupPosition = new(-2.2f, 0f);
    [SerializeField] private Vector2 bottleTargetPosition = new(1.45f, 0f);
    [SerializeField] private Vector2 breakableWallPosition = new(2.65f, -1.2f);
    [SerializeField] private Vector2 keyPickupPosition = new(3.55f, 0f);
    [SerializeField] private Vector2 elevatorExitPosition = new(5.4f, 0f);
    [SerializeField] private Vector2 tutorialAreaSize = new(13.6f, 5.4f);

    private void OnValidate()
    {
        startingHealth = Mathf.Max(1, startingHealth);
        tutorialAreaSize = new Vector2(
            Mathf.Max(1f, tutorialAreaSize.x),
            Mathf.Max(1f, tutorialAreaSize.y));

    }

    private void Start()
    {
        if (Application.isPlaying && buildOnStart)
        {
            BuildTutorialScene();
        }
    }

    [ContextMenu("Materialize Tutorial Objects")]
    public void MaterializeTutorialObjectsForEditing()
    {
        Scene scene = gameObject.scene;

        if (!scene.IsValid())
        {
            return;
        }

        Transform root = EnsureAuthoringRoot(scene);
        MaterializeTutorialObjects(scene, root, spawnPlayerIfMissing);
        EnsureTutorialTileRuntime(root);
        EnsureTutorialGlassTraps(scene, root);
        EnsureTutorialGlobalLight(scene);
        WasdPlayerController playerController = ResolveOrSpawnPlayer(scene, root, spawnPlayerIfMissing);
        EnsureTutorialHudRuntime(scene, playerController);
    }

    [ContextMenu("Materialize Tutorial Glass Traps")]
    public void MaterializeTutorialGlassTrapsForEditing()
    {
        Scene scene = gameObject.scene;

        if (!scene.IsValid())
        {
            return;
        }

        Transform root = EnsureAuthoringRoot(scene);
        EnsureTutorialGlassTraps(scene, root);
    }

    [ContextMenu("Build Tutorial Scene")]
    public void BuildTutorialScene()
    {
        Scene scene = gameObject.scene;

        if (!scene.IsValid())
        {
            return;
        }

        if (disableFloorRuntimeSystems)
        {
            DisableFloorRuntimeSystems(scene);
        }

        Transform root = EnsureAuthoringRoot(scene);
        MaterializeTutorialObjects(scene, root, spawnPlayerIfMissing);
        GridMapService mapService = EnsureTutorialTileRuntime(root);
        ConfigureExistingDirectExitElevator(root);
        EnsureTutorialDoorInteractions(root, mapService);
        EnsureTutorialGlassTraps(scene, root);
        EnsureTutorialGlobalLight(scene);

        if (Application.isPlaying)
        {
            PrototypeAudioManager.EnsureExists();
            EnsureTutorialNoiseSystem(scene);
            RRunSessionController.EnsureExistsForRuntime();
        }

        WasdPlayerController playerController = ResolveOrSpawnPlayer(scene, root, spawnPlayerIfMissing);
        ConfigureTutorialPlayer(playerController);
        ConfigureCamera(playerController);
        EnsureTutorialHudRuntime(scene, playerController);

        if (Application.isPlaying)
        {
            EnsureTutorialSentry(scene, root, playerController);
        }
    }

    private void MaterializeForEditingIfAllowed()
    {
        if (!materializeAuthoringInEditMode || materializingForEditMode)
        {
            return;
        }

        Scene scene = gameObject.scene;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        materializingForEditMode = true;

        try
        {
            Transform root = EnsureAuthoringRoot(scene);
            MaterializeTutorialObjects(scene, root, spawnPlayerIfMissing);
        }
        finally
        {
            materializingForEditMode = false;
        }
    }

    private Transform EnsureAuthoringRoot(Scene scene)
    {
        if (authoringRoot != null && authoringRoot.gameObject.scene == scene)
        {
            return authoringRoot;
        }

        Transform existingRoot = RSceneReferenceLookup.FindTransformInScene(scene, AuthoringRootName);

        if (existingRoot != null)
        {
            authoringRoot = existingRoot;
            return authoringRoot;
        }

        GameObject rootObject = new(AuthoringRootName);
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        authoringRoot = rootObject.transform;
        return authoringRoot;
    }

    private void MaterializeTutorialObjects(Scene scene, Transform root, bool allowPlayerSpawn)
    {
        if (root == null)
        {
            return;
        }

        _ = ResolveOrSpawnPlayer(scene, root, allowPlayerSpawn);

        if (materializeDefaultTutorialLayout)
        {
            EnsureRectangle(
                root,
                "Backdrop",
                Vector2.zero,
                tutorialAreaSize,
                new Color(0.11f, 0.12f, 0.12f, 1f),
                BackdropSortingOrder,
                addCollider: false,
                layer: 0);

            if (!HasAuthoredGroundTilemap(root))
            {
                EnsureRectangle(
                    root,
                    "TileFloor_Base",
                    Vector2.zero,
                    tutorialAreaSize,
                    new Color(0.18f, 0.2f, 0.19f, 1f),
                    BackdropSortingOrder + 2,
                    addCollider: false,
                    layer: GameLayers.GroundIndex);
                EnsureRectangle(
                    root,
                    "FloorBand",
                    new Vector2(0f, -1.28f),
                    new Vector2(tutorialAreaSize.x, 0.18f),
                    new Color(0.27f, 0.3f, 0.28f, 1f),
                    BackdropSortingOrder + 1,
                    addCollider: false,
                    layer: GameLayers.GroundIndex);
            }

            EnsureTraversalBlockers(root);
            EnsureMovementCues(root);
            EnsureStarterPickups(root);
            EnsureBottleLesson(root);
            EnsureBreakableBottleWall(root);
            EnsureKeyPickup(root);
            EnsureElevatorExit(root);
        }
    }

    private WasdPlayerController ResolveOrSpawnPlayer(Scene scene, Transform root, bool allowSpawn)
    {
        WasdPlayerController playerController = RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>(
            scene,
            this,
            nameof(RTutorialSceneBootstrap),
            nameof(WasdPlayerController));

        if (playerController != null || !allowSpawn)
        {
            return playerController;
        }

        playerController = MainEscapePlayerSpawnUtility.SpawnPlayerFromCatalog(
            scene,
            root,
            this,
            nameof(RTutorialSceneBootstrap),
            destroyExistingPlayers: false);

        if (playerController != null)
        {
            playerController.transform.localPosition = playerSpawnPosition;
        }

        return playerController;
    }

    private static bool HasAuthoredGroundTilemap(Transform root)
    {
        Transform groundTilemap = FindChildByName(root, "Tiles_ground") ?? FindChildByName(root, "GroundTilemap");
        return groundTilemap != null && groundTilemap.GetComponent<Tilemap>() != null;
    }

    private void ConfigureTutorialPlayer(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return;
        }

        RPlayerRuntimeReferences runtimeReferences = RPlayerRuntimeReferences.Resolve(playerController);
        runtimeReferences?.EnsureRuntimeComponents();

        if (playerController.GetComponent<VisibilityTarget2D>() == null)
        {
            playerController.gameObject.AddComponent<VisibilityTarget2D>();
        }

        PlayerInventory inventory = playerController.GetComponent<PlayerInventory>();

        if (resetPlayerInventoryOnStart && inventory != null)
        {
            inventory.SetItems(Array.Empty<PlayerInventory.ItemStack>());
        }

        PlayerHealth playerHealth = playerController.GetComponent<PlayerHealth>();
        playerHealth?.SetCurrentHealth(Mathf.Max(1, startingHealth));

        PlayerFlashlightBattery flashlightBattery = playerController.GetComponent<PlayerFlashlightBattery>();
        flashlightBattery?.SetChargeNormalized(startingFlashlightChargeNormalized);

        if (!forceNoFlashlight)
        {
            return;
        }

        playerController.SetFlashlightAvailability(false);
        playerController.SetFlashlightEnabled(false);

        FlashlightStateOwner flashlightStateOwner = playerController.GetComponent<FlashlightStateOwner>();
        flashlightStateOwner?.SetFlashlightEnabledState(false);
        flashlightStateOwner?.SetChargeNormalized(startingFlashlightChargeNormalized);
    }

    private void ConfigureCamera(WasdPlayerController playerController)
    {
        Camera mainCamera = EnsureMainCamera(gameObject.scene);

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 4.6f;

        Vector3 position = playerController != null
            ? playerController.transform.position
            : (Vector3)playerSpawnPosition;
        position.z = mainCamera.transform.position.z;
        mainCamera.transform.position = position;
        mainCamera.backgroundColor = new Color(0.11f, 0.13f, 0.14f, 1f);
    }

    private static void EnsureTutorialGlobalLight(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        Light2D globalLight = FindTutorialGlobalLight(scene);

        if (globalLight == null)
        {
            GameObject lightObject = new(TutorialGlobalLightName);
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            globalLight = lightObject.AddComponent<Light2D>();
        }

        globalLight.gameObject.name = TutorialGlobalLightName;
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.color = Color.white;
        globalLight.intensity = 1f;
        globalLight.volumeIntensity = 0f;
        globalLight.shadowsEnabled = false;
        globalLight.enabled = true;
    }

    private static Light2D FindTutorialGlobalLight(Scene scene)
    {
        Light2D[] lights = RSceneReferenceLookup.FindComponentsInScene<Light2D>(scene);
        Light2D namedLight = null;

        for (int index = 0; index < lights.Length; index++)
        {
            Light2D candidate = lights[index];

            if (candidate == null)
            {
                continue;
            }

            if (candidate.lightType == Light2D.LightType.Global)
            {
                return candidate;
            }

            if (namedLight == null && string.Equals(candidate.gameObject.name, TutorialGlobalLightName, StringComparison.Ordinal))
            {
                namedLight = candidate;
            }
        }

        return namedLight;
    }

    private static void EnsureTutorialHudRuntime(Scene scene, WasdPlayerController playerController)
    {
        if (!scene.IsValid() || playerController == null)
        {
            return;
        }

        IRHudCanvas hudCanvas = FindTutorialHudCanvas(scene);

#if UNITY_EDITOR
        if (hudCanvas == null)
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorHudCanvasPrefabPath);

            if (hudPrefab != null)
            {
                GameObject hudObject = PrefabUtility.InstantiatePrefab(hudPrefab, scene) as GameObject;

                if (hudObject != null)
                {
                    hudObject.name = "IRHudCanvas";
                    hudCanvas = hudObject.GetComponent<IRHudCanvas>();
                }
            }
        }
#endif

        if (hudCanvas == null)
        {
            return;
        }

        IRAuthoredGameplayHudView authoredHudView = FindTutorialAuthoredGameplayHud(scene);

#if UNITY_EDITOR
        if (authoredHudView == null)
        {
            GameObject authoredHudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorAuthoredGameplayHudPrefabPath);

            if (authoredHudPrefab != null)
            {
                GameObject authoredHudObject = PrefabUtility.InstantiatePrefab(authoredHudPrefab, scene) as GameObject;

                if (authoredHudObject != null)
                {
                    authoredHudObject.name = "RAuthoredGameplayHudCanvas";
                    authoredHudView = authoredHudObject.GetComponent<IRAuthoredGameplayHudView>();
                }
            }
        }
#endif

        RPlayerRuntimeReferences playerRuntime = RPlayerRuntimeReferences.Resolve(playerController);
        playerRuntime?.EnsureRuntimeComponents();
        BindTutorialHudBinder(authoredHudView, playerRuntime, hudCanvas);
        RRuntimeHudBinderSet hudBinders = RRuntimePlayerInstaller.EnsureHudBinders(playerController);
        BindTutorialHudBinder(hudBinders.InventoryHudBinder, playerRuntime, hudCanvas);
        BindTutorialHudBinder(hudBinders.HealthHudBinder, playerRuntime, hudCanvas);
        BindTutorialHudBinder(hudBinders.ThreatHudBinder, playerRuntime, hudCanvas);
        BindTutorialHudBinder(hudBinders.QuickSlotsHudBinder, playerRuntime, hudCanvas);
        BindTutorialHudBinder(hudBinders.StaminaHudBinder, playerRuntime, hudCanvas);
    }

    private static IRHudCanvas FindTutorialHudCanvas(Scene scene)
    {
        return RSceneReferenceLookup.FindFirstComponentInScene<IRHudCanvas>(scene);
    }

    private static IRAuthoredGameplayHudView FindTutorialAuthoredGameplayHud(Scene scene)
    {
        return RSceneReferenceLookup.FindFirstComponentInScene<IRAuthoredGameplayHudView>(scene);
    }

    private static void BindTutorialHudBinder(
        IRebuildHudBinder binder,
        RPlayerRuntimeReferences playerRuntime,
        IRHudCanvas hudCanvas)
    {
        if (binder == null || playerRuntime == null || hudCanvas == null)
        {
            return;
        }

        binder.BindPlayerRuntime(playerRuntime);
        binder.BindHudCanvas(hudCanvas);
    }

    private static Camera EnsureMainCamera(Scene scene)
    {
        Camera mainCamera = RSceneCameraUtility.FindPreferredCameraInScene(scene);

        if (mainCamera != null && mainCamera.gameObject.scene == scene)
        {
            EnsureAudioListener(mainCamera.gameObject);
            return mainCamera;
        }

        Transform cameraTransform = RSceneReferenceLookup.FindTransformInScene(scene, "RMainCamera");
        GameObject cameraObject = cameraTransform != null ? cameraTransform.gameObject : null;

        if (cameraObject == null)
        {
            cameraObject = new GameObject("RMainCamera");

            if (scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
            }
        }

        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();

        if (camera == null)
        {
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(0f, 0f, -10f);
        EnsureAudioListener(cameraObject);
        return camera;
    }

    private static void EnsureAudioListener(GameObject cameraObject)
    {
        if (cameraObject == null)
        {
            return;
        }

        AudioListener listener = cameraObject.GetComponent<AudioListener>();

        if (listener == null
            && RSceneReferenceLookup.FindFirstComponentInScene<AudioListener>(cameraObject.scene) == null)
        {
            cameraObject.AddComponent<AudioListener>();
        }
    }

    private static void ConfigureExistingDirectExitElevator(Transform root)
    {
        Transform elevatorVisual = FindChildByName(root, DirectExitElevatorVisualName);

        if (elevatorVisual == null)
        {
            return;
        }

        MainEscapeElevatorExitInteractable floorExit = elevatorVisual.GetComponent<MainEscapeElevatorExitInteractable>();

        if (floorExit != null)
        {
            floorExit.enabled = false;
        }

        RTutorialElevatorExitInteractable tutorialExit = MainEscapeComponentUtility.GetOrAddComponent<RTutorialElevatorExitInteractable>(elevatorVisual.gameObject);
        tutorialExit.Configure(
            requiresConfiguredItem: true,
            configuredRequiredItemId: PrototypeItemCatalog.IronGateKeyItemId,
            configuredShowPromptText: false,
            configuredConsumeRequiredItemBeforeExit: false);
    }

    private static void EnsureTutorialDoorInteractions(Transform root, GridMapService mapService)
    {
        if (root == null || mapService == null)
        {
            return;
        }

        Transform interactionRoot = EnsureChild(root, TutorialDoorInteractionsRootName);
        bool keepsLegacyRuntimeDoor =
            EnsureTutorialRuntimeDoor(
                interactionRoot,
                root,
                mapService,
                "TutorialFrontDoorController",
                FrontDoorVisualName,
                FrontDoorVisualNamePrefix)
            | EnsureTutorialRuntimeDoor(
                interactionRoot,
                root,
                mapService,
                "TutorialSideDoorController",
                SideDoorVisualName,
                SideDoorVisualName);

        if (!keepsLegacyRuntimeDoor)
        {
            RemoveChildIfPresent(root, TutorialDoorInteractionsRootName);
        }
    }

    private static bool EnsureTutorialRuntimeDoor(
        Transform interactionRoot,
        Transform authoringRoot,
        GridMapService mapService,
        string controllerName,
        string visualName,
        string visualNamePrefix)
    {
        if (interactionRoot == null || authoringRoot == null || mapService == null || mapService.GroundTilemap == null)
        {
            return false;
        }

        Transform visualRoot = FindChildByName(authoringRoot, visualName) ?? FindFirstChildByNamePrefix(authoringRoot, visualNamePrefix);

        if (visualRoot == null)
        {
            RemoveChildIfPresent(interactionRoot, controllerName);
            return false;
        }

        MainEscapeSelfContainedDoor selfContainedDoor = visualRoot.GetComponent<MainEscapeSelfContainedDoor>();

        if (selfContainedDoor != null)
        {
            // Keep tutorial regular doors on the same ownership model as the live floor scenes.
            selfContainedDoor.enabled = true;
            RemoveChildIfPresent(interactionRoot, controllerName);
            return false;
        }

        Vector3Int[] doorCells = ResolveTutorialDoorCells(visualRoot, mapService.GroundTilemap);
        Transform controllerTransform = EnsureChild(interactionRoot, controllerName);
        controllerTransform.position = MainEscapeDoorRuntimeUtility.ResolveDoorCenter(mapService, doorCells);
        controllerTransform.rotation = Quaternion.identity;
        controllerTransform.localScale = Vector3.one;
        controllerTransform.gameObject.layer = GameLayers.DoorIndex;

        RTutorialSimpleDoorInteractable legacyDoor =
            controllerTransform.GetComponent<RTutorialSimpleDoorInteractable>();

        if (legacyDoor != null)
        {
            legacyDoor.enabled = false;
        }

        BoxCollider2D[] legacyRootColliders = controllerTransform.GetComponents<BoxCollider2D>();

        for (int index = 0; index < legacyRootColliders.Length; index++)
        {
            BoxCollider2D legacyCollider = legacyRootColliders[index];

            if (legacyCollider != null)
            {
                legacyCollider.enabled = false;
            }
        }

        DoorController doorController = MainEscapeComponentUtility.GetOrAddComponent<DoorController>(controllerTransform.gameObject);
        doorController.enabled = true;
        doorController.Configure(null, mapService, string.Empty, doorCells);
        Scene controllerScene = controllerTransform.gameObject.scene;
        doorController.ConfigureNoiseEventBus(NoiseEventBusResolver.Resolve(
            controllerScene,
            RSceneReferenceLookup.FindFirstComponentInScene<NoiseSystem>(controllerScene)));
        doorController.SetBuiltInVisualsEnabled(false);
        doorController.BindAuthoredVisualRoots(visualRoot);
        return true;
    }

    private static Vector3Int[] ResolveTutorialDoorCells(Transform visualRoot, Tilemap groundTilemap)
    {
        if (visualRoot == null || groundTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        MainEscapeSelfContainedDoor selfContainedDoor = visualRoot.GetComponent<MainEscapeSelfContainedDoor>();
        Vector3Int[] doorCells = selfContainedDoor != null
            ? selfContainedDoor.ResolveDoorCells(groundTilemap)
            : MainEscapeVisualAuthoringSynthesis.CollectDoorCellsForVisualRoot(visualRoot, groundTilemap);

        if (doorCells.Length > 0)
        {
            return doorCells;
        }

        return new[] { MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, visualRoot.position) };
    }

    private static void EnsureTutorialGlassTraps(Scene scene, Transform root)
    {
        if (!scene.IsValid() || root == null)
        {
            return;
        }

        MainEscapeDangerPlacementMarker[] dangerMarkers = CollectTutorialDangerMarkers(root);
        Transform hazardRoot = FindChildByName(root, TutorialRuntimeHazardRootName);

        if (dangerMarkers.Length == 0)
        {
            if (hazardRoot != null)
            {
                ClearManagedTutorialGlassTraps(hazardRoot);
            }

            return;
        }

        hazardRoot = hazardRoot != null ? hazardRoot : EnsureChild(root, TutorialRuntimeHazardRootName);
        ClearManagedTutorialGlassTraps(hazardRoot);

        MainEscapeRuntimePrefabCatalog catalog = MainEscapeRuntimePrefabCatalog.LoadForScene(scene);
        NoiseFloorPanel trapPrefab = catalog != null ? catalog.GlassTrapPanelPrefab : null;

        for (int index = 0; index < dangerMarkers.Length; index++)
        {
            MainEscapeDangerPlacementMarker marker = dangerMarkers[index];

            if (marker == null)
            {
                continue;
            }

            SpawnTutorialGlassTrap(hazardRoot, trapPrefab, marker, index);
        }
    }

    private static MainEscapeDangerPlacementMarker[] CollectTutorialDangerMarkers(Transform root)
    {
        if (root == null)
        {
            return Array.Empty<MainEscapeDangerPlacementMarker>();
        }

        MainEscapeDangerPlacementMarker[] markers = root.GetComponentsInChildren<MainEscapeDangerPlacementMarker>(true);
        List<MainEscapeDangerPlacementMarker> collectedMarkers = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeDangerPlacementMarker marker = markers[index];

            if (marker != null)
            {
                collectedMarkers.Add(marker);
            }
        }

        collectedMarkers.Sort((left, right) => string.CompareOrdinal(
            left != null ? left.PlacementId : string.Empty,
            right != null ? right.PlacementId : string.Empty));
        return collectedMarkers.ToArray();
    }

    private static void SpawnTutorialGlassTrap(
        Transform hazardRoot,
        NoiseFloorPanel trapPrefab,
        MainEscapeDangerPlacementMarker marker,
        int markerIndex)
    {
        if (hazardRoot == null || marker == null)
        {
            return;
        }

        NoiseFloorPanel trapInstance = trapPrefab != null
            ? Instantiate(trapPrefab, hazardRoot)
            : CreateFallbackTutorialGlassTrap(hazardRoot);

        if (trapInstance == null)
        {
            return;
        }

        string markerId = SanitizeObjectName(marker.PlacementId);
        trapInstance.name = $"{TutorialGlassTrapNamePrefix}{markerIndex + 1:00}_{markerId}";
        trapInstance.transform.SetParent(hazardRoot, false);
        trapInstance.transform.SetPositionAndRotation(marker.GetWorldPosition(), marker.transform.rotation);
        trapInstance.gameObject.SetActive(true);
    }

    private static NoiseFloorPanel CreateFallbackTutorialGlassTrap(Transform hazardRoot)
    {
        GameObject trapObject = new($"{TutorialGlassTrapNamePrefix}Fallback");
        trapObject.transform.SetParent(hazardRoot, false);
        NoiseFloorPanel trap = trapObject.AddComponent<NoiseFloorPanel>();
        trap.Configure(new Vector2(1.1f, 1.1f), new Color(0.9f, 0.98f, 1f, 0.1f), 5f);
        return trap;
    }

    private static void ClearManagedTutorialGlassTraps(Transform hazardRoot)
    {
        if (hazardRoot == null)
        {
            return;
        }

        for (int index = hazardRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = hazardRoot.GetChild(index);

            if (child == null || !child.name.StartsWith(TutorialGlassTrapNamePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static string SanitizeObjectName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "DangerMarker";
        }

        return rawName.Trim()
            .Replace(' ', '_')
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace("/", "_")
            .Replace("\\", "_");
    }

    private static NoiseSystem EnsureTutorialNoiseSystem(Scene scene)
    {
        NoiseSystem sceneNoiseSystem = RSceneReferenceLookup.FindFirstComponentInScene<NoiseSystem>(scene);

        if (sceneNoiseSystem != null)
        {
            if (!sceneNoiseSystem.gameObject.activeSelf)
            {
                sceneNoiseSystem.gameObject.SetActive(true);
            }

            sceneNoiseSystem.enabled = true;
            sceneNoiseSystem.SetDebugPulsesEnabled(true);
            return sceneNoiseSystem;
        }

        GameObject noiseObject = new(TutorialNoiseSystemName);
        SceneManager.MoveGameObjectToScene(noiseObject, scene);
        NoiseSystem createdNoiseSystem = noiseObject.AddComponent<NoiseSystem>();
        createdNoiseSystem.SetDebugPulsesEnabled(true);
        return createdNoiseSystem;
    }

    private void EnsureTutorialSentry(Scene scene, Transform root, WasdPlayerController playerController)
    {
        if (!spawnTutorialSentryOnStart || root == null || playerController == null)
        {
            return;
        }

        string resolvedBaseName = string.IsNullOrWhiteSpace(tutorialSentryName)
            ? "SentryGuard_01"
            : tutorialSentryName.Trim();

        VisibilityTarget2D playerTarget = playerController.GetComponent<VisibilityTarget2D>();

        if (playerTarget == null)
        {
            return;
        }

        GridMapService mapService = EnsureTutorialMapService(root);

        if (mapService == null)
        {
            return;
        }

        Transform enemyRoot = EnsureChild(root, TutorialRuntimeEnemyRootName);
        EnemyArchetype sentryArchetype = CreateTutorialSentryArchetype();
        MainEscapeEnemyPlacementMarker[] sentryMarkers = CollectTutorialSentrySpawnMarkers(root);
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog = MainEscapeRuntimePrefabCatalog.LoadForScene(scene);

        if (sentryMarkers.Length == 0)
        {
            SpawnTutorialSentry(
                scene,
                root,
                enemyRoot,
                playerTarget,
                mapService,
                sentryArchetype,
                null,
                runtimePrefabCatalog,
                resolvedBaseName);
            return;
        }

        for (int index = 0; index < sentryMarkers.Length; index++)
        {
            MainEscapeEnemyPlacementMarker sentryMarker = sentryMarkers[index];

            if (sentryMarker == null)
            {
                continue;
            }

            string sentryName = BuildTutorialSentryName(resolvedBaseName, sentryMarker, index, sentryMarkers.Length);
            SpawnTutorialSentry(
                scene,
                root,
                enemyRoot,
                playerTarget,
                mapService,
                sentryArchetype,
                sentryMarker,
                runtimePrefabCatalog,
                sentryName);
        }
    }

    private void SpawnTutorialSentry(
        Scene scene,
        Transform root,
        Transform enemyRoot,
        VisibilityTarget2D playerTarget,
        GridMapService mapService,
        EnemyArchetype sentryArchetype,
        MainEscapeEnemyPlacementMarker sentryMarker,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog,
        string sentryName)
    {
        if (enemyRoot == null || FindChildByName(enemyRoot, sentryName) != null)
        {
            return;
        }

        Vector3 spawnWorldPosition = ResolveTutorialSentrySpawnWorldPosition(root, mapService, sentryMarker);
        Vector2 facingDirection = sentryMarker != null
            ? sentryMarker.Facing
            : tutorialSentryFacingDirection.sqrMagnitude > 0.0001f
                ? tutorialSentryFacingDirection.normalized
                : Vector2.left;

        EnemyStateMachine sentry = EnemyRuntimeFactory.CreateEnemy(
            enemyRoot,
            sentryName,
            spawnWorldPosition,
            mapService,
            playerTarget,
            sentryArchetype,
            null,
            facingDirection,
            runtimePrefabCatalog: runtimePrefabCatalog);

        if (sentry == null)
        {
            return;
        }

        sentry.ConfigureExactStandGuardAnchor(spawnWorldPosition);

        if (sentry.gameObject.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(sentry.gameObject, scene);
        }
    }

    private static string BuildTutorialSentryName(
        string baseName,
        MainEscapeEnemyPlacementMarker sentryMarker,
        int markerIndex,
        int markerCount)
    {
        string resolvedBaseName = string.IsNullOrWhiteSpace(baseName) ? "SentryGuard_01" : baseName.Trim();

        if (markerCount <= 1)
        {
            return resolvedBaseName;
        }

        string markerName = sentryMarker != null && !string.IsNullOrWhiteSpace(sentryMarker.name)
            ? sentryMarker.name.Trim()
            : "Marker";
        markerName = markerName
            .Replace(' ', '_')
            .Replace("(", string.Empty)
            .Replace(")", string.Empty);
        return $"{resolvedBaseName}_{markerIndex:00}_{markerName}";
    }

    private GridMapService EnsureTutorialTileRuntime(Transform root)
    {
        Tilemap groundTilemap = FindTilemap(root, TutorialGroundTilemapName);

        if (groundTilemap == null)
        {
            return null;
        }

        Tilemap wallTilemap = FindTilemap(root, TutorialWallTilemapName);
        Tilemap moveOnlyTilemap = FindTilemap(root, TutorialMoveOnlyTilemapName);
        Tilemap doorTilemap = FindTilemap(root, TutorialDoorTilemapName);
        Grid grid = ResolveTutorialGrid(root, groundTilemap);

        if (grid == null)
        {
            return null;
        }

        doorTilemap ??= EnsureRuntimeTilemap(grid.transform, TutorialDoorTilemapName);

        ConfigureTutorialTilemap(groundTilemap, GameLayers.GroundIndex, sortingOrder: 0, enableTilemapCollider: false);
        ConfigureTutorialTilemap(wallTilemap, GameLayers.WallIndex, sortingOrder: 13, enableTilemapCollider: true);
        ConfigureTutorialTilemap(moveOnlyTilemap, GameLayers.PropIndex, sortingOrder: 18, enableTilemapCollider: false);
        ConfigureTutorialTilemap(doorTilemap, GameLayers.DoorIndex, sortingOrder: 7, enableTilemapCollider: true);
        EnsureTutorialDoorTiles(root, groundTilemap, doorTilemap);

        GridMapService mapService = grid.GetComponent<GridMapService>();

        if (mapService == null)
        {
            mapService = grid.gameObject.AddComponent<GridMapService>();
        }

        DisableLegacyTutorialMapService(root, mapService);
        mapService.Initialize(grid, groundTilemap, wallTilemap, doorTilemap, GameLayers.VisionBlockingMask);

        RemoveChildIfPresent(root, TutorialWallBlockersRootName);
        RemoveChildIfPresent(root, TutorialMoveOnlyBlockersRootName);
        RemoveChildIfPresent(root, TutorialVisualPropBlockersRootName);
        RemoveChildIfPresent(root, TutorialDoorShadowBlockersRootName);
        BuildTutorialWallBlockers(root, wallTilemap, mapService);
        BuildTutorialMoveOnlyBlockers(root, moveOnlyTilemap, mapService);
        BuildTutorialVisualPropBlockers(root, groundTilemap, mapService);
        ApplyNoFrictionToDescendantColliders(root);
        return mapService;
    }

    private GridMapService EnsureTutorialMapService(Transform root)
    {
        return EnsureTutorialTileRuntime(root);
    }

    private static Tilemap EnsureRuntimeTilemap(Transform parent, string objectName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform tilemapTransform = parent.Find(objectName) ?? EnsureChild(parent, objectName);
        Tilemap tilemap = tilemapTransform.GetComponent<Tilemap>();

        if (tilemap == null)
        {
            tilemap = tilemapTransform.gameObject.AddComponent<Tilemap>();
        }

        if (tilemapTransform.GetComponent<TilemapRenderer>() == null)
        {
            tilemapTransform.gameObject.AddComponent<TilemapRenderer>();
        }

        return tilemap;
    }

    private static void ConfigureTutorialTilemap(
        Tilemap tilemap,
        int layer,
        int sortingOrder,
        bool enableTilemapCollider)
    {
        if (tilemap == null)
        {
            return;
        }

        tilemap.gameObject.layer = layer;
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
        tilemap.color = Color.white;

        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();

        if (renderer == null)
        {
            renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
        }

        renderer.enabled = true;
        renderer.sortingOrder = sortingOrder;

        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();

        if (enableTilemapCollider)
        {
            if (tilemapCollider == null)
            {
                tilemapCollider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
            }

            tilemapCollider.enabled = true;
            tilemapCollider.isTrigger = false;
            TopDownCollisionMaterialUtility.ApplyNoFriction(tilemapCollider);
            return;
        }

        if (tilemapCollider != null)
        {
            tilemapCollider.enabled = false;
        }
    }

    private static void EnsureTutorialDoorTiles(Transform root, Tilemap groundTilemap, Tilemap doorTilemap)
    {
        if (!Application.isPlaying || root == null || groundTilemap == null || doorTilemap == null)
        {
            return;
        }

        Tile runtimeDoorTile = ScriptableObject.CreateInstance<Tile>();
        runtimeDoorTile.name = "RTutorialRuntimeDoorTile";
        runtimeDoorTile.hideFlags = HideFlags.HideAndDontSave;
        EnsureTutorialDoorTilesForVisual(root, groundTilemap, doorTilemap, runtimeDoorTile, FrontDoorVisualName, FrontDoorVisualNamePrefix);
        EnsureTutorialDoorTilesForVisual(root, groundTilemap, doorTilemap, runtimeDoorTile, SideDoorVisualName, SideDoorVisualName);

        TilemapCollider2D tilemapCollider = doorTilemap.GetComponent<TilemapCollider2D>();
        tilemapCollider?.ProcessTilemapChanges();
    }

    private static void EnsureTutorialDoorTilesForVisual(
        Transform root,
        Tilemap groundTilemap,
        Tilemap doorTilemap,
        TileBase runtimeDoorTile,
        string visualName,
        string visualNamePrefix)
    {
        Transform visualRoot = FindChildByName(root, visualName) ?? FindFirstChildByNamePrefix(root, visualNamePrefix);

        if (visualRoot == null)
        {
            return;
        }

        Vector3Int[] doorCells = ResolveTutorialDoorCells(visualRoot, groundTilemap);

        for (int index = 0; index < doorCells.Length; index++)
        {
            Vector3Int cell = doorCells[index];

            if (!doorTilemap.HasTile(cell))
            {
                doorTilemap.SetTile(cell, runtimeDoorTile);
            }
        }
    }

    private static void DisableLegacyTutorialMapService(Transform root, GridMapService activeMapService)
    {
        Transform legacyServiceTransform = FindChildByName(root, TutorialMapServiceName);
        GridMapService legacyMapService = legacyServiceTransform != null
            ? legacyServiceTransform.GetComponent<GridMapService>()
            : null;

        if (legacyMapService != null && legacyMapService != activeMapService)
        {
            legacyMapService.enabled = false;
        }
    }

    private static void BuildTutorialWallBlockers(Transform root, Tilemap wallTilemap, GridMapService mapService)
    {
        if (root == null || wallTilemap == null)
        {
            return;
        }

        Transform blockerRoot = EnsureChild(root, TutorialWallBlockersRootName);
        BoundsInt bounds = wallTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!wallTilemap.HasTile(cellPosition))
            {
                continue;
            }

            GameObject blocker = CreateTutorialCellBlocker(
                blockerRoot,
                $"WallBlocker_{cellPosition.x}_{cellPosition.y}",
                wallTilemap.GetCellCenterWorld(cellPosition),
                GameLayers.WallIndex);
            BoxCollider2D collider = blocker.GetComponent<BoxCollider2D>();

            if (!RuntimeShadowCaster2DConfigurator.TryConfigureFromCollider(blocker, collider, out _))
            {
                Debug.LogWarning($"Failed to configure tutorial wall shadow caster at {cellPosition}.", blocker);
            }

            mapService?.RegisterPropCell(cellPosition);
        }
    }

    private static void BuildTutorialMoveOnlyBlockers(Transform root, Tilemap moveOnlyTilemap, GridMapService mapService)
    {
        if (root == null || moveOnlyTilemap == null)
        {
            return;
        }

        Transform blockerRoot = EnsureChild(root, TutorialMoveOnlyBlockersRootName);
        BoundsInt bounds = moveOnlyTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!moveOnlyTilemap.HasTile(cellPosition))
            {
                continue;
            }

            CreateTutorialCellBlocker(
                blockerRoot,
                $"MoveOnlyBlocker_{cellPosition.x}_{cellPosition.y}",
                moveOnlyTilemap.GetCellCenterWorld(cellPosition),
                GameLayers.PropIndex);
            mapService?.RegisterMovementBlockingCell(cellPosition);
        }
    }

    private static void BuildTutorialVisualPropBlockers(Transform root, Tilemap groundTilemap, GridMapService mapService)
    {
        if (root == null || groundTilemap == null)
        {
            return;
        }

        Transform[] propRoots = CollectTutorialMoveOnlyVisualProps(root);

        if (propRoots.Length == 0)
        {
            return;
        }

        Transform blockerRoot = EnsureChild(root, TutorialVisualPropBlockersRootName);

        for (int index = 0; index < propRoots.Length; index++)
        {
            Transform propRoot = propRoots[index];

            if (propRoot == null)
            {
                continue;
            }

            if (TryGetRenderableBounds(propRoot, out Bounds visualBounds))
            {
                CreateTutorialBoundsBlocker(
                    blockerRoot,
                    $"PropBlocker_{index:00}_{propRoot.name}",
                    visualBounds,
                    GameLayers.PropIndex);
            }

            RegisterTutorialVisualPropCells(propRoot, groundTilemap, mapService);
        }
    }

    private static void RegisterTutorialVisualPropCells(
        Transform propRoot,
        Tilemap groundTilemap,
        GridMapService mapService)
    {
        if (propRoot == null || groundTilemap == null || mapService == null)
        {
            return;
        }

        Vector3Int[] propCells = MainEscapeVisualAuthoringSynthesis.CollectMovementBlockingPropCells(
            new[] { propRoot },
            groundTilemap);

        if (propCells.Length == 0)
        {
            Vector3 worldPosition = TryGetRenderableBounds(propRoot, out Bounds visualBounds)
                ? visualBounds.center
                : propRoot.position;
            propCells = new[]
            {
                MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, worldPosition)
            };
        }

        for (int index = 0; index < propCells.Length; index++)
        {
            mapService.RegisterMovementBlockingCell(propCells[index]);
        }
    }

    private static Transform[] CollectTutorialMoveOnlyVisualProps(Transform root)
    {
        if (root == null)
        {
            return Array.Empty<Transform>();
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        List<Transform> propRoots = new();
        HashSet<Transform> collectedProps = new();

        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];

            if (candidate == null || candidate == root || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (IsManagedTutorialRuntimeTransform(candidate))
            {
                continue;
            }

            if (!ContainsOrdinal(candidate.name, TutorialMoveOnlyVisualPropPrefix))
            {
                continue;
            }

            if (collectedProps.Add(candidate))
            {
                propRoots.Add(candidate);
            }
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer == null
                || renderer.transform == root
                || !renderer.gameObject.activeInHierarchy
                || IsManagedTutorialRuntimeTransform(renderer.transform)
                || !ContainsOrdinal(renderer.gameObject.name, TutorialMoveOnlyVisualPropPrefix))
            {
                continue;
            }

            if (collectedProps.Add(renderer.transform))
            {
                propRoots.Add(renderer.transform);
            }
        }

        return propRoots.ToArray();
    }

    private static bool IsManagedTutorialRuntimeRoot(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string name = candidate.name;
        return string.Equals(name, TutorialWallBlockersRootName, StringComparison.Ordinal)
            || string.Equals(name, TutorialMoveOnlyBlockersRootName, StringComparison.Ordinal)
            || string.Equals(name, TutorialVisualPropBlockersRootName, StringComparison.Ordinal)
            || string.Equals(name, TutorialDoorShadowBlockersRootName, StringComparison.Ordinal)
            || string.Equals(name, TutorialRuntimeEnemyRootName, StringComparison.Ordinal)
            || string.Equals(name, TutorialRuntimeHazardRootName, StringComparison.Ordinal)
            || string.Equals(name, TutorialDoorInteractionsRootName, StringComparison.Ordinal);
    }

    private static bool IsManagedTutorialRuntimeTransform(Transform candidate)
    {
        while (candidate != null)
        {
            if (IsManagedTutorialRuntimeRoot(candidate))
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }

    private static GameObject CreateTutorialCellBlocker(
        Transform parent,
        string objectName,
        Vector3 worldPosition,
        int layer)
    {
        GameObject blocker = new(objectName);
        blocker.layer = layer;
        blocker.transform.SetParent(parent, false);
        blocker.transform.position = worldPosition;

        BoxCollider2D collider = blocker.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.offset = Vector2.zero;
        collider.isTrigger = false;
        TopDownCollisionMaterialUtility.ApplyNoFriction(collider);
        return blocker;
    }

    private static GameObject CreateTutorialBoundsBlocker(
        Transform parent,
        string objectName,
        Bounds worldBounds,
        int layer)
    {
        GameObject blocker = new(objectName);
        blocker.layer = layer;
        blocker.transform.SetParent(parent, false);
        blocker.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, 0f);

        BoxCollider2D collider = blocker.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(
            Mathf.Max(0.1f, worldBounds.size.x),
            Mathf.Max(0.1f, worldBounds.size.y));
        collider.offset = Vector2.zero;
        collider.isTrigger = false;
        TopDownCollisionMaterialUtility.ApplyNoFriction(collider);
        return blocker;
    }

    private static bool TryGetRenderableBounds(Transform root, out Bounds combinedBounds)
    {
        combinedBounds = default;

        if (root == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer == null
                || !renderer.gameObject.activeInHierarchy
                || !renderer.enabled
                || renderer.sprite == null
                || renderer.color.a <= 0.01f)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void ApplyNoFrictionToDescendantColliders(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);

        for (int index = 0; index < colliders.Length; index++)
        {
            TopDownCollisionMaterialUtility.ApplyNoFriction(colliders[index]);
        }
    }

    private Vector3 ResolveTutorialSentrySpawnWorldPosition(
        Transform root,
        GridMapService mapService,
        MainEscapeEnemyPlacementMarker sentryMarker)
    {
        Vector3 preferredWorldPosition = sentryMarker != null
            ? sentryMarker.GetWorldPosition()
            : root.TransformPoint((Vector3)tutorialSentrySpawnPosition);

        if (sentryMarker != null)
        {
            return preferredWorldPosition;
        }

        return mapService.TryResolveNearestWalkableCell(preferredWorldPosition, out Vector3Int resolvedCell, 4, true)
            ? mapService.CellToWorldCenter(resolvedCell)
            : preferredWorldPosition;
    }

    private static MainEscapeEnemyPlacementMarker[] CollectTutorialSentrySpawnMarkers(Transform root)
    {
        if (root == null)
        {
            return Array.Empty<MainEscapeEnemyPlacementMarker>();
        }

        MainEscapeEnemyPlacementMarker[] markers = root.GetComponentsInChildren<MainEscapeEnemyPlacementMarker>(true);
        List<MainEscapeEnemyPlacementMarker> sentryMarkers = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeEnemyPlacementMarker marker = markers[index];

            if (marker == null)
            {
                continue;
            }

            if (marker.PlacementKind == MainEscapeEnemyPlacementKind.Sentry
                || IsTutorialSentryMarkerName(marker.name)
                || IsTutorialSentryMarkerName(marker.PlacementId))
            {
                sentryMarkers.Add(marker);
            }
        }

        return sentryMarkers.ToArray();
    }

    private static bool IsTutorialSentryMarkerName(string markerName)
    {
        return !string.IsNullOrWhiteSpace(markerName)
            && markerName.Trim().StartsWith(TutorialSentryMarkerName, StringComparison.Ordinal);
    }

    private static Grid ResolveTutorialGrid(Transform root, Tilemap groundTilemap)
    {
        Transform gridTransform = FindChildByName(root, TutorialTileGridName);
        Grid grid = gridTransform != null ? gridTransform.GetComponent<Grid>() : null;

        if (grid != null)
        {
            return grid;
        }

        return groundTilemap != null ? groundTilemap.layoutGrid as Grid : null;
    }

    private static Tilemap FindTilemap(Transform root, string tilemapName)
    {
        Transform tilemapTransform = FindChildByName(root, tilemapName);
        return tilemapTransform != null ? tilemapTransform.GetComponent<Tilemap>() : null;
    }

    private static EnemyArchetype CreateTutorialSentryArchetype()
    {
        EnemyArchetype archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
        archetype.hideFlags = HideFlags.DontSave;
        archetype.Configure(
            configuredPatrolSpeed: 0f,
            configuredInvestigateSpeed: 1.8f,
            configuredChaseSpeed: 2.7f,
            configuredVisionDistance: 5.4f,
            configuredVisionAngle: 48f,
            configuredHearingRadius: 4.5f,
            configuredPatrolWaitTime: 0.1f,
            configuredIdleWanderRadius: 1,
            configuredRepathInterval: 0.22f,
            configuredChaseMemoryDuration: 0.8f,
            configuredSearchDuration: 0.7f,
            configuredSearchRadius: 1,
            configuredIdleBehavior: EnemyIdleBehavior.StandGuard,
            configuredCaptureDistance: 0.42f,
            configuredAlertRecoveryBehavior: EnemyAlertRecoveryBehavior.SearchArea);
        return archetype;
    }

    private void EnsureTraversalBlockers(Transform root)
    {
        Color lowBlockerColor = new(0.44f, 0.52f, 0.48f, 0.82f);
        Color wallColor = new(0.23f, 0.25f, 0.27f, 1f);

        EnsureRectangle(
            root,
            "MoveOnlyBlocker_LowDesk",
            new Vector2(-1.08f, -0.95f),
            new Vector2(1.35f, 0.32f),
            lowBlockerColor,
            CueSortingOrder - 12,
            addCollider: true,
            layer: GameLayers.PropIndex);
        EnsureRectangle(
            root,
            "MoveOnlyBlocker_Cart",
            new Vector2(0.14f, 1.08f),
            new Vector2(0.42f, 1.15f),
            lowBlockerColor,
            CueSortingOrder - 12,
            addCollider: true,
            layer: GameLayers.PropIndex);
        EnsureRectangle(
            root,
            "VisionBlocker_Wall",
            new Vector2(2.46f, 1.08f),
            new Vector2(0.48f, 1.78f),
            wallColor,
            CueSortingOrder - 8,
            addCollider: true,
            layer: GameLayers.WallIndex);

        EnsureRectangle(root, "BoundaryWall_Top", new Vector2(0f, tutorialAreaSize.y * 0.5f), new Vector2(tutorialAreaSize.x, 0.18f), new Color(0.16f, 0.17f, 0.17f, 1f), CueSortingOrder - 18, addCollider: true, layer: GameLayers.WallIndex);
        EnsureRectangle(root, "BoundaryWall_Bottom", new Vector2(0f, -tutorialAreaSize.y * 0.5f), new Vector2(tutorialAreaSize.x, 0.18f), new Color(0.16f, 0.17f, 0.17f, 1f), CueSortingOrder - 18, addCollider: true, layer: GameLayers.WallIndex);
        EnsureRectangle(root, "BoundaryWall_Left", new Vector2(-tutorialAreaSize.x * 0.5f, 0f), new Vector2(0.18f, tutorialAreaSize.y), new Color(0.16f, 0.17f, 0.17f, 1f), CueSortingOrder - 18, addCollider: true, layer: GameLayers.WallIndex);
        EnsureRectangle(root, "BoundaryWall_Right", new Vector2(tutorialAreaSize.x * 0.5f, 0f), new Vector2(0.18f, tutorialAreaSize.y), new Color(0.16f, 0.17f, 0.17f, 1f), CueSortingOrder - 18, addCollider: true, layer: GameLayers.WallIndex);
    }

    private void EnsureMovementCues(Transform root)
    {
        for (int index = 0; index < 4; index++)
        {
            float x = playerSpawnPosition.x + 0.9f + (index * 0.52f);
            EnsureRectangle(
                root,
                $"MoveCue_{index:00}",
                new Vector2(x, -1.28f),
                new Vector2(0.28f, 0.08f),
                new Color(0.75f, 0.86f, 1f, 0.8f),
                CueSortingOrder,
                addCollider: false,
                layer: 0);
        }
    }

    private void EnsureStarterPickups(Transform root)
    {
        EnsurePickup(
            root,
            "FlashlightPickup",
            flashlightPickupPosition,
            PrototypeItemCatalog.FlashlightItemId,
            1,
            Color.white,
            new Vector3(0.78f, 0.78f, 1f));
        EnsurePickup(
            root,
            "MedkitPickup",
            medkitPickupPosition,
            PrototypeItemCatalog.MedkitItemId,
            1,
            new Color(0.45f, 1f, 0.66f, 1f),
            new Vector3(0.72f, 0.72f, 1f));
        EnsurePickup(
            root,
            "FlashlightBatteryPickup",
            batteryPickupPosition,
            PrototypeItemCatalog.FlashlightBatteryItemId,
            1,
            new Color(0.46f, 0.9f, 1f, 1f),
            new Vector3(0.72f, 0.72f, 1f));
    }

    private void EnsureBottleLesson(Transform root)
    {
        EnsurePickup(
            root,
            "GlassBottlePickup",
            bottlePickupPosition,
            PrototypeItemCatalog.GlassBottleItemId,
            2,
            new Color(0.72f, 0.92f, 1f, 1f),
            new Vector3(0.85f, 0.85f, 1f));

        GameObject targetObject = EnsureAuthoredObject(
            root,
            "CagedEnemyBottleTarget",
            bottleTargetPosition,
            Vector3.one,
            layer: 0,
            out _);
        _ = MainEscapeComponentUtility.GetOrAddComponent<RTutorialBottleTarget>(targetObject);

        EnsureRectangle(
            root,
            "TargetPen_Back",
            bottleTargetPosition + new Vector2(0f, 0.64f),
            new Vector2(1.8f, 0.14f),
            new Color(0.54f, 0.57f, 0.58f, 0.88f),
            CueSortingOrder - 2,
            addCollider: true,
            layer: GameLayers.PropIndex);
        EnsureRectangle(
            root,
            "TargetPen_Left",
            bottleTargetPosition + new Vector2(-0.94f, 0f),
            new Vector2(0.14f, 1.38f),
            new Color(0.54f, 0.57f, 0.58f, 0.88f),
            CueSortingOrder - 2,
            addCollider: true,
            layer: GameLayers.PropIndex);
        EnsureRectangle(
            root,
            "TargetPen_Right",
            bottleTargetPosition + new Vector2(0.94f, 0f),
            new Vector2(0.14f, 1.38f),
            new Color(0.54f, 0.57f, 0.58f, 0.88f),
            CueSortingOrder - 2,
            addCollider: true,
            layer: GameLayers.PropIndex);

        for (int index = 0; index < 5; index++)
        {
            float t = index / 4f;
            Vector2 position = Vector2.Lerp(bottlePickupPosition + new Vector2(0.65f, 0.36f), bottleTargetPosition + new Vector2(-0.42f, 0.2f), t);
            position.y += Mathf.Sin(t * Mathf.PI) * 0.44f;
            EnsureRectangle(
                root,
                $"ThrowArc_{index:00}",
                position,
                new Vector2(0.11f, 0.11f),
                new Color(0.72f, 0.92f, 1f, 0.65f),
                CueSortingOrder,
                addCollider: false,
                layer: 0);
        }
    }

    private void EnsureBreakableBottleWall(Transform root)
    {
        GameObject wallObject = EnsureAuthoredObject(
            root,
            "BottleBreakWall",
            breakableWallPosition,
            new Vector3(0.42f, 1.05f, 1f),
            GameLayers.WallIndex,
            out _);

        SpriteRenderer renderer = MainEscapeComponentUtility.GetOrAddComponent<SpriteRenderer>(wallObject);

        if (ShouldUseGeneratedRectangleVisual(renderer))
        {
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(0.56f, 0.5f, 0.42f, 1f);
            renderer.sortingOrder = CueSortingOrder - 6;
        }

        BoxCollider2D collider = MainEscapeComponentUtility.GetOrAddComponent<BoxCollider2D>(wallObject);
        collider.size = Vector2.one;
        collider.isTrigger = false;

        _ = MainEscapeComponentUtility.GetOrAddComponent<RTutorialBottleBreakWall>(wallObject);

        for (int index = 0; index < 3; index++)
        {
            EnsureRectangle(
                root,
                $"BreakWallShardCue_{index:00}",
                breakableWallPosition + new Vector2(0.58f + (index * 0.22f), -0.18f + (index * 0.18f)),
                new Vector2(0.1f, 0.1f),
                new Color(0.72f, 0.92f, 1f, 0.66f),
                CueSortingOrder,
                addCollider: false,
                layer: 0);
        }
    }

    private void EnsureKeyPickup(Transform root)
    {
        EnsurePickup(
            root,
            "IronGateKeyPickup",
            keyPickupPosition,
            PrototypeItemCatalog.IronGateKeyItemId,
            1,
            new Color(1f, 0.86f, 0.38f, 1f),
            new Vector3(0.82f, 0.82f, 1f));
    }

    private void EnsureElevatorExit(Transform root)
    {
        Transform existingExit = FindChildByName(root, DirectExitElevatorVisualName);
        GameObject exitObject = existingExit != null
            ? existingExit.gameObject
            : EnsureAuthoredObject(
                root,
                DirectExitElevatorVisualName,
                elevatorExitPosition,
                new Vector3(2.6875f, 2f, 1f),
                layer: 0,
                out _);

        SpriteRenderer renderer = MainEscapeComponentUtility.GetOrAddComponent<SpriteRenderer>(exitObject);

        bool usesGeneratedDoorVisual = ShouldUseGeneratedRectangleVisual(renderer);

        if (usesGeneratedDoorVisual)
        {
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(0.22f, 0.25f, 0.28f, 1f);
        }

        renderer.sortingOrder = CueSortingOrder - 4;

        ConfigureExistingDirectExitElevator(root);
    }

    private void EnsurePickup(
        Transform root,
        string objectName,
        Vector2 defaultPosition,
        string itemId,
        int quantity,
        Color fallbackColor,
        Vector3 defaultScale)
    {
        GameObject pickupObject = EnsureAuthoredObject(
            root,
            objectName,
            defaultPosition,
            defaultScale,
            layer: 0,
            out _);

        SpriteRenderer renderer = MainEscapeComponentUtility.GetOrAddComponent<SpriteRenderer>(pickupObject);
        WorldInventoryPickupBase authoredPickup = pickupObject.GetComponent<WorldInventoryPickupBase>();

        if (authoredPickup != null)
        {
            ConfigureAuthoredWorldPickup(authoredPickup, itemId, quantity, fallbackColor);
            renderer.sortingOrder = CueSortingOrder + 4;
            return;
        }

        RTutorialInventoryPickup pickup = MainEscapeComponentUtility.GetOrAddComponent<RTutorialInventoryPickup>(pickupObject);
        CircleCollider2D collider = MainEscapeComponentUtility.GetOrAddComponent<CircleCollider2D>(pickupObject);

        collider.isTrigger = true;
        collider.radius = 0.42f;
        pickup.Configure(itemId, PrototypeItemCatalog.GetDisplayName(itemId), Mathf.Max(1, quantity), fallbackColor);
        renderer.sortingOrder = CueSortingOrder + 4;
    }

    private static void ConfigureAuthoredWorldPickup(
        WorldInventoryPickupBase pickup,
        string itemId,
        int quantity,
        Color fallbackColor)
    {
        if (pickup is PrototypeInventoryPickup inventoryPickup)
        {
            inventoryPickup.ConfigureAuthored(
                itemId,
                PrototypeItemCatalog.GetDisplayName(itemId),
                Mathf.Max(1, quantity),
                fallbackColor);
            return;
        }

        if (pickup is MainEscapeKeyPickup keyPickup)
        {
            keyPickup.Configure(
                itemId,
                PrototypeItemCatalog.GetDisplayName(itemId),
                Mathf.Max(1, quantity));
        }
    }

    private SpriteRenderer EnsureRectangle(
        Transform root,
        string objectName,
        Vector2 defaultPosition,
        Vector2 defaultSize,
        Color color,
        int sortingOrder,
        bool addCollider,
        int layer)
    {
        GameObject rectangleObject = EnsureAuthoredObject(
            root,
            objectName,
            defaultPosition,
            new Vector3(defaultSize.x, defaultSize.y, 1f),
            layer,
            out bool created);

        SpriteRenderer renderer = rectangleObject.GetComponent<SpriteRenderer>();
        bool hasChildVisual = renderer == null && HasChildSpriteVisual(rectangleObject);

        if (created || !hasChildVisual || ShouldUseGeneratedRectangleVisual(renderer))
        {
            renderer = MainEscapeComponentUtility.GetOrAddComponent<SpriteRenderer>(rectangleObject);
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        if (addCollider)
        {
            BoxCollider2D collider = MainEscapeComponentUtility.GetOrAddComponent<BoxCollider2D>(rectangleObject);
            collider.size = Vector2.one;
            collider.isTrigger = false;
        }

        return renderer != null ? renderer : FindFirstSpriteRenderer(rectangleObject);
    }

    private static bool ShouldUseGeneratedRectangleVisual(SpriteRenderer renderer)
    {
        return renderer == null
            || renderer.sprite == null
            || string.Equals(renderer.sprite.name, "RTutorialSquareSprite", StringComparison.Ordinal);
    }

    private static bool HasChildSpriteVisual(GameObject gameObject)
    {
        SpriteRenderer[] renderers = gameObject.GetComponentsInChildren<SpriteRenderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer != null && renderer.gameObject != gameObject && renderer.sprite != null)
            {
                return true;
            }
        }

        return false;
    }

    private static SpriteRenderer FindFirstSpriteRenderer(GameObject gameObject)
    {
        SpriteRenderer[] renderers = gameObject.GetComponentsInChildren<SpriteRenderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private static GameObject EnsureAuthoredObject(
        Transform root,
        string objectName,
        Vector2 defaultLocalPosition,
        Vector3 defaultLocalScale,
        int layer,
        out bool created)
    {
        Transform existing = FindChildByName(root, objectName);

        if (existing != null)
        {
            created = false;
            existing.gameObject.layer = layer;
            return existing.gameObject;
        }

        GameObject gameObject = new(objectName);
        gameObject.layer = layer;
        gameObject.transform.SetParent(root, false);
        gameObject.transform.localPosition = defaultLocalPosition;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = defaultLocalScale;
        created = true;
        return gameObject;
    }

    private static Transform EnsureChild(Transform root, string objectName)
    {
        Transform existing = FindChildByName(root, objectName);

        if (existing != null)
        {
            return existing;
        }

        GameObject gameObject = new(objectName);
        gameObject.transform.SetParent(root, false);
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        return gameObject.transform;
    }

    private static void RemoveChildIfPresent(Transform root, string objectName)
    {
        Transform existing = FindChildByName(root, objectName);

        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            existing.gameObject.SetActive(false);
            existing.SetParent(null, false);
            Destroy(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
        }
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < children.Length; index++)
        {
            Transform child = children[index];

            if (child != null && string.Equals(child.name, objectName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindFirstChildByNamePrefix(Transform root, string objectNamePrefix)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectNamePrefix))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < children.Length; index++)
        {
            Transform child = children[index];

            if (child != null && StartsWithOrdinal(child.name, objectNamePrefix))
            {
                return child;
            }
        }

        return null;
    }

    private static bool StartsWithOrdinal(string value, string prefix)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(prefix)
            && value.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static bool ContainsOrdinal(string value, string fragment)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(fragment)
            && value.IndexOf(fragment, StringComparison.Ordinal) >= 0;
    }

    private static void DisableFloorRuntimeSystems(Scene scene)
    {
        DisableFogOverlaysInScene(scene);
        DisableComponentsInScene<RSceneCompositionRoot>(scene);
        DisableComponentsInScene<RFloorDirector>(scene);
        DisableComponentsInScene<RRunController>(scene);
        DisableComponentsInScene<MainEscapeEncounterSpawner>(scene);
        DisableComponentsInScene<RShadowStartleDirector>(scene);
        DisableComponentsInScene<VisionSensor2D>(scene);
    }

    private static void DisableFogOverlaysInScene(Scene scene)
    {
        FlashlightFogOfWarOverlay[] overlays =
            RSceneReferenceLookup.FindComponentsInScene<FlashlightFogOfWarOverlay>(scene);

        for (int index = 0; index < overlays.Length; index++)
        {
            FlashlightFogOfWarOverlay overlay = overlays[index];

            if (overlay == null)
            {
                continue;
            }

            overlay.SetBypassEnabled(true);
            overlay.enabled = false;

            SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();

            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = false;
            }
        }
    }

    private static void DisableComponentsInScene<TComponent>(Scene scene)
        where TComponent : Behaviour
    {
        TComponent[] components = RSceneReferenceLookup.FindComponentsInScene<TComponent>(scene);

        for (int index = 0; index < components.Length; index++)
        {
            TComponent component = components[index];

            if (component == null)
            {
                continue;
            }

            component.enabled = false;
        }
    }

    private static Sprite GetSquareSprite()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Sprite editorSprite = LoadOrCreateEditorSquareSprite();

            if (editorSprite != null)
            {
                return editorSprite;
            }
        }
#endif

        if (sharedSquareSprite != null)
        {
            return sharedSquareSprite;
        }

        Texture2D texture = new(8, 8, TextureFormat.RGBA32, false)
        {
            name = "RTutorialSquareSprite",
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[64];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        sharedSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
        sharedSquareSprite.name = "RTutorialSquareSprite";
        sharedSquareSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedSquareSprite;
    }

#if UNITY_EDITOR
    private static Sprite LoadOrCreateEditorSquareSprite()
    {
        Sprite existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorSquareSpritePath);

        if (existingSprite != null)
        {
            return existingSprite;
        }

        string directory = Path.GetDirectoryName(EditorSquareSpritePath);

        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Texture2D texture = new(8, 8, TextureFormat.RGBA32, false)
        {
            name = "RTutorialSquareSprite"
        };
        Color[] pixels = new Color[64];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        File.WriteAllBytes(EditorSquareSpritePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(EditorSquareSpritePath, ImportAssetOptions.ForceSynchronousImport);

        if (AssetImporter.GetAtPath(EditorSquareSpritePath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 8f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(EditorSquareSpritePath);
    }
#endif
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RTutorialBottleBreakWall : MonoBehaviour, IThrowableStunTarget
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private BoxCollider2D wallCollider;
    [SerializeField, Min(0.05f)] private float hitRadius = 0.7f;
    [SerializeField] private Color intactColor = new(0.56f, 0.5f, 0.42f, 1f);
    [SerializeField] private Color brokenColor = new(0.72f, 0.92f, 1f, 0.26f);

    private bool broken;

    public bool CanBeStunnedByThrowable => isActiveAndEnabled && !broken;
    public Vector3 ThrowableStunAimPoint => transform.position;
    public float ThrowableStunHitRadius => hitRadius;

    public bool TryApplyThrowableStun(float duration)
    {
        BreakWall();
        return false;
    }

    private void Reset()
    {
        CacheReferences();
        ApplyIntactState();
    }

    private void Awake()
    {
        CacheReferences();
        ApplyIntactState();
    }

    private void OnEnable()
    {
        ThrowableStunTargetRegistry.Register(this);
    }

    private void OnDisable()
    {
        ThrowableStunTargetRegistry.Unregister(this);
    }

    private void OnValidate()
    {
        hitRadius = Mathf.Max(0.05f, hitRadius);
        CacheReferences();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsThrowableBottle(collision != null ? collision.collider : null))
        {
            BreakWall();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsThrowableBottle(other))
        {
            BreakWall();
        }
    }

    private IEnumerator DisableAfterBreak()
    {
        yield return new WaitForSeconds(0.18f);

        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = false;
        }
    }

    private void BreakWall()
    {
        if (broken)
        {
            return;
        }

        broken = true;
        ApplyBrokenState();
        StartCoroutine(DisableAfterBreak());
    }

    private static bool IsThrowableBottle(Collider2D collider)
    {
        return collider != null
            && (collider.GetComponent<ThrowableBottleProjectile>() != null
                || (collider.attachedRigidbody != null
                    && collider.attachedRigidbody.GetComponent<ThrowableBottleProjectile>() != null));
    }

    private void CacheReferences()
    {
        bodyRenderer ??= GetComponent<SpriteRenderer>();
        wallCollider ??= GetComponent<BoxCollider2D>();
    }

    private void ApplyIntactState()
    {
        CacheReferences();
        broken = false;

        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = true;
            bodyRenderer.color = intactColor;
        }

        if (wallCollider != null)
        {
            wallCollider.enabled = true;
            wallCollider.isTrigger = false;
        }
    }

    private void ApplyBrokenState()
    {
        CacheReferences();

        if (bodyRenderer != null)
        {
            bodyRenderer.color = brokenColor;
        }

        if (wallCollider != null)
        {
            wallCollider.enabled = false;
        }
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed partial class RTutorialSimpleDoorInteractable : PlayerInteractable2D
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private BoxCollider2D blockerCollider;
    [SerializeField] private SpriteRenderer[] visualRenderers = Array.Empty<SpriteRenderer>();
    [SerializeField] private Collider2D[] visualBlockers = Array.Empty<Collider2D>();
    [SerializeField] private bool[] visualBlockerClosedStates = Array.Empty<bool>();
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.6f;
    [SerializeField, Min(0f)] private float doorNoiseRadius = 2.75f;
    [SerializeField] private Vector3 openVisualOffset = new(0.54f, 0f, 0f);
    [SerializeField] private Vector3 closedVisualLocalPosition;
    [SerializeField] private bool hasClosedVisualLocalPosition;
    [SerializeField] private bool isOpen;
    [SerializeField] private bool showPromptText;
    [SerializeField] private NoiseSystem noiseSystem;
    private INoiseEventBus noiseEventBus;

    protected override float MaxInteractionDistance => interactionDistance;

    public override Vector2 InteractionPoint => ResolveBlockerBounds().center;

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
    }

    public void Configure(
        Transform configuredVisualRoot,
        Vector2 colliderSize,
        Vector2 colliderOffset,
        Vector3 configuredOpenVisualOffset)
    {
        visualRoot = configuredVisualRoot;
        openVisualOffset = configuredOpenVisualOffset;
        gameObject.layer = GameLayers.DoorIndex;

        if (visualRoot != null)
        {
            transform.position = visualRoot.position;
            closedVisualLocalPosition = visualRoot.localPosition;
            hasClosedVisualLocalPosition = true;
        }

        CacheReferences(recaptureVisualBlockers: true);

        if (blockerCollider != null)
        {
            blockerCollider.size = new Vector2(Mathf.Max(0.05f, colliderSize.x), Mathf.Max(0.05f, colliderSize.y));
            blockerCollider.offset = colliderOffset;
            blockerCollider.isTrigger = false;
        }

        ApplyState();
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        Vector2 playerPosition = playerController.transform.position;
        Vector2 surfacePoint = ResolveInteractionSurfacePoint(playerPosition);
        return Vector2.Distance(playerPosition, surfacePoint);
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        return playerController != null
            ? ResolveInteractionSurfacePoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override bool AllowsLineOfSightBlocker(Collider2D blocker, Vector2 hitPoint, WasdPlayerController playerController)
    {
        if (blocker == null)
        {
            return false;
        }

        Transform blockerTransform = blocker.transform;
        return blocker == blockerCollider
            || blockerTransform == transform
            || blockerTransform.IsChildOf(transform)
            || (visualRoot != null && (blockerTransform == visualRoot || blockerTransform.IsChildOf(visualRoot)));
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (!showPromptText)
        {
            return string.Empty;
        }

        return isOpen ? "E Close Door" : "E Open Door";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        if (isOpen && IsActorBlockingDoorway(playerController))
        {
            PrototypeAudioManager.TryPlayDenied();
            return;
        }

        SetOpen(!isOpen, playerController);
    }

    private void Reset()
    {
        CacheReferences(recaptureVisualBlockers: true);
        CaptureClosedVisualPosition();
        ApplyState();
    }

    private void Awake()
    {
        CacheReferences(recaptureVisualBlockers: visualBlockers.Length == 0);
        CaptureClosedVisualPosition();
        ApplyState();
    }

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        doorNoiseRadius = Mathf.Max(0f, doorNoiseRadius);
        CacheReferences(recaptureVisualBlockers: false);
    }

    private void SetOpen(bool open, WasdPlayerController actor)
    {
        if (isOpen == open)
        {
            return;
        }

        isOpen = open;
        ApplyState();

        if (open)
        {
            PrototypeAudioManager.TryPlayDoorOpen();
        }
        else
        {
            PrototypeAudioManager.TryPlayDoorClose();
        }

        int emitterId = actor != null ? actor.gameObject.GetInstanceID() : 0;
        ResolveNoiseEventBus()?.TryEmitNoise(
            InteractionPoint,
            doorNoiseRadius,
            NoiseSourceType.Door,
            emitterId,
            actor != null ? NoiseEmitterAffiliation.Player : NoiseEmitterAffiliation.Neutral);
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private void CacheReferences(bool recaptureVisualBlockers)
    {
        blockerCollider ??= GetComponent<BoxCollider2D>();

        if (visualRoot == null)
        {
            visualRenderers = Array.Empty<SpriteRenderer>();
            visualBlockers = Array.Empty<Collider2D>();
            visualBlockerClosedStates = Array.Empty<bool>();
            return;
        }

        visualRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);

        if (!recaptureVisualBlockers && visualBlockers != null && visualBlockerClosedStates != null)
        {
            return;
        }

        Collider2D[] candidates = visualRoot.GetComponentsInChildren<Collider2D>(true);
        int count = 0;

        for (int index = 0; index < candidates.Length; index++)
        {
            Collider2D candidate = candidates[index];

            if (candidate != null && candidate != blockerCollider && !candidate.isTrigger)
            {
                count++;
            }
        }

        visualBlockers = new Collider2D[count];
        visualBlockerClosedStates = new bool[count];
        int writeIndex = 0;

        for (int index = 0; index < candidates.Length; index++)
        {
            Collider2D candidate = candidates[index];

            if (candidate == null || candidate == blockerCollider || candidate.isTrigger)
            {
                continue;
            }

            visualBlockers[writeIndex] = candidate;
            visualBlockerClosedStates[writeIndex] = candidate.enabled;
            writeIndex++;
        }
    }

    private void CaptureClosedVisualPosition()
    {
        if (visualRoot == null || hasClosedVisualLocalPosition)
        {
            return;
        }

        closedVisualLocalPosition = visualRoot.localPosition;
        hasClosedVisualLocalPosition = true;
    }

    private void ApplyState()
    {
        CacheReferences(recaptureVisualBlockers: false);

        if (blockerCollider != null)
        {
            blockerCollider.enabled = !isOpen;
            blockerCollider.isTrigger = false;
        }

        for (int index = 0; index < visualBlockers.Length; index++)
        {
            Collider2D visualBlocker = visualBlockers[index];

            if (visualBlocker == null)
            {
                continue;
            }

            bool closedState = visualBlockerClosedStates != null
                && index < visualBlockerClosedStates.Length
                && visualBlockerClosedStates[index];
            visualBlocker.enabled = !isOpen && closedState;
        }

        if (visualRoot != null && hasClosedVisualLocalPosition)
        {
            visualRoot.localPosition = closedVisualLocalPosition + (isOpen ? openVisualOffset : Vector3.zero);
        }
    }

    private bool IsActorBlockingDoorway(WasdPlayerController playerController)
    {
        Bounds doorwayBounds = ResolveBlockerBounds();

        if (playerController != null && doorwayBounds.Contains(playerController.transform.position))
        {
            return true;
        }

        EnemyStateMachine[] enemies = RSceneReferenceLookup.FindComponentsInScene<EnemyStateMachine>(gameObject.scene);

        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyStateMachine enemy = enemies[index];

            if (enemy != null && doorwayBounds.Contains(enemy.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 ResolveInteractionSurfacePoint(Vector2 playerPosition)
    {
        Bounds blockerBounds = ResolveBlockerBounds();

        if (blockerBounds.size.sqrMagnitude > 0.0001f)
        {
            return blockerBounds.ClosestPoint(playerPosition);
        }

        if (TryGetVisualBounds(out Bounds visualBounds))
        {
            return visualBounds.ClosestPoint(playerPosition);
        }

        return transform.position;
    }

    private Bounds ResolveBlockerBounds()
    {
        CacheReferences(recaptureVisualBlockers: false);

        if (blockerCollider == null)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Vector3 center = transform.TransformPoint(blockerCollider.offset);
        Vector3 lossyScale = transform.lossyScale;
        Vector3 size = new(
            Mathf.Abs(blockerCollider.size.x * lossyScale.x),
            Mathf.Abs(blockerCollider.size.y * lossyScale.y),
            0.1f);
        return new Bounds(center, size);
    }

    private bool TryGetVisualBounds(out Bounds combinedBounds)
    {
        CacheReferences(recaptureVisualBlockers: false);
        combinedBounds = default;
        bool hasBounds = false;

        for (int index = 0; index < visualRenderers.Length; index++)
        {
            SpriteRenderer renderer = visualRenderers[index];

            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
}

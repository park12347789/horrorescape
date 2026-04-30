using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class MainEscapeFloorDirector : MonoBehaviour, IMainEscapeDebugPresentationController, IDebugPresentationApplier, IGateAnchorReadModel
{
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private SpriteRenderer accentBackdrop;
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private MainEscapeEncounterSpawner encounterSpawner;
    [SerializeField] private DoorController mainGateDoorController;
    private readonly List<DoorController> runtimeDoorControllers = new();
    private readonly IFloorBuildPipeline floorBuildPipeline = new RLegacyFloorBuildPipeline();

    private OfficeFloorBuildResult currentBuild;

    public GridMapService CurrentMapService => currentBuild.MapService;
    public GeneratedFloorLayout CurrentLayout => currentBuild.Layout;
    public Transform CurrentFloorRoot => currentBuild.FloorRoot;
    public Vector2 CurrentWorldCenter => currentBuild.WorldCenter;
    public Vector2 CurrentWorldSize => currentBuild.WorldSize;
    public Vector3Int[] CurrentPatrolRoute => currentBuild.PatrolRoute;
    public Vector3Int CurrentStalkerSpawnCell => currentBuild.StalkerSpawnCell;
    public MainEscapeSentrySpawnPoint[] CurrentSentrySpawns => currentBuild.SentrySpawns;
    public MainEscapeVentRouteDefinition CurrentVentRoute => currentBuild.VentRoute;
    public bool DebugPresentationEnabled => encounterSpawner != null && encounterSpawner.ShowVentMarkers;
    public bool HasMainGate => currentBuild.Layout != null && currentBuild.Layout.MainDoorCells != null && currentBuild.Layout.MainDoorCells.Length > 0;

    public bool TryGetCurrentPlayerSpawnWorldPosition(out Vector3 worldPosition)
    {
        if (currentBuild.HasPlayerSpawnWorldPosition)
        {
            worldPosition = currentBuild.PlayerSpawnWorldPosition;
            return true;
        }

        worldPosition = default;
        return false;
    }

    public bool TryGetGateWorldPosition(out Vector3 worldPosition)
    {
        if (mainGateDoorController != null)
        {
            worldPosition = mainGateDoorController.transform.position;
            return true;
        }

        if (currentBuild.MapService != null
            && currentBuild.Layout != null
            && currentBuild.Layout.MainDoorCells != null
            && currentBuild.Layout.MainDoorCells.Length > 0)
        {
            Vector3 accumulatedPosition = Vector3.zero;

            for (int index = 0; index < currentBuild.Layout.MainDoorCells.Length; index++)
            {
                accumulatedPosition += currentBuild.MapService.CellToWorldCenter(currentBuild.Layout.MainDoorCells[index]);
            }

            worldPosition = accumulatedPosition / currentBuild.Layout.MainDoorCells.Length;
            return true;
        }

        worldPosition = default;
        return false;
    }

    public void Initialize(WasdPlayerController player, FlashlightFogOfWarOverlay fogOverlay, SpriteRenderer backdrop, bool enableVentMarkers = false)
    {
        playerController = player;
        fogOfWarOverlay = fogOverlay;
        accentBackdrop = backdrop;
        encounterSpawner ??= GetComponent<MainEscapeEncounterSpawner>();

        if (encounterSpawner == null)
        {
            encounterSpawner = gameObject.AddComponent<MainEscapeEncounterSpawner>();
        }

        encounterSpawner.Initialize(playerController, fogOfWarOverlay, enableVentMarkers);
    }

    public void BuildFloor(EscapeFloorDefinition floorDefinition)
    {
        if (floorDefinition == null)
        {
            return;
        }

        DestroyCurrentFloor();

        if (!floorBuildPipeline.TryBuildFloor(floorDefinition, gameObject.scene, transform, out currentBuild, out string failureReason))
        {
            Debug.LogError(string.IsNullOrWhiteSpace(failureReason)
                ? $"Failed to build MainEscape floor {floorDefinition.FloorNumber}F."
                : failureReason, this);
            return;
        }

        EnableSceneMovementBlockerColliders();
        ConfigureBackdrop(floorDefinition);
        CreateDoorControllers();
        LayerMask blockingLayers = currentBuild.MapService != null
            ? currentBuild.MapService.VisionBlockingLayers
            : GameLayers.VisionBlockingMask;
        fogOfWarOverlay?.Initialize(playerController, currentBuild.WorldCenter, currentBuild.WorldSize, blockingLayers, currentBuild.MapService);
        MainEscapeRuntimePrefabCatalog prefabCatalog = MainEscapeRuntimePrefabCatalog.LoadForScene(gameObject.scene);
        encounterSpawner?.ConfigureFloor(
            playerController,
            currentBuild.MapService,
            currentBuild.Layout,
            floorDefinition,
            currentBuild.PatrolRoute,
            currentBuild.PatrolSpawnCells,
            currentBuild.StalkerSpawnCell,
            currentBuild.SentrySpawns,
            currentBuild.VentRoute,
            prefabCatalog);
    }

    public void SetDebugPresentation(bool enabled)
    {
        encounterSpawner ??= GetComponent<MainEscapeEncounterSpawner>();
        encounterSpawner?.SetVentMarkersVisible(enabled);
    }

    public void ApplyDebugPresentation(bool enabled)
    {
        SetDebugPresentation(enabled);
    }

    private void ConfigureBackdrop(EscapeFloorDefinition floorDefinition)
    {
        if (accentBackdrop == null)
        {
            return;
        }

        accentBackdrop.transform.position = new Vector3(currentBuild.WorldCenter.x, currentBuild.WorldCenter.y, 0f);
        accentBackdrop.transform.localScale = new Vector3(currentBuild.WorldSize.x * 0.92f, currentBuild.WorldSize.y * 0.92f, 1f);
        Color accentColor = floorDefinition.AccentColor;
        accentColor.a = 0.24f;
        accentBackdrop.color = accentColor;
        accentBackdrop.sortingOrder = -4;
    }


    private void EnableSceneMovementBlockerColliders()
    {
        if (!Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        MainEscapeMovementBlockerAuthoring[] movementBlockers =
            RSceneReferenceLookup.FindComponentsInScene<MainEscapeMovementBlockerAuthoring>(gameObject.scene);

        for (int index = 0; index < movementBlockers.Length; index++)
        {
            MainEscapeMovementBlockerAuthoring blocker = movementBlockers[index];

            if (blocker == null)
            {
                continue;
            }

            BoxCollider2D collider = blocker.GetComponent<BoxCollider2D>();

            if (collider != null)
            {
                collider.enabled = true;
            }
        }
    }
}

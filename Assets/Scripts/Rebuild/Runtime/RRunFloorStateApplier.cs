using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct RRunFloorStateContext
{
    public RRunFloorStateContext(
        IFloorEscapeInteractionController interactionController,
        EscapeFloorDefinition floorDefinition,
        int currentFloorNumber,
        bool escaped,
        bool resetPlayerPosition,
        bool usesAuthoredKeyGateSequence,
        WasdPlayerController playerController,
        RPlayerRuntimeReferences playerRuntime,
        FlashlightFogOfWarOverlay fogOfWarOverlay,
        RFloorDirector floorDirector,
        FloorEscapeGoalPickup goalPickup,
        FloorEscapeTransitionPoint stairPoint,
        FloorEscapeTransitionPoint finalExitPoint,
        PrototypeInventoryPickup batteryPickup,
        PrototypeInventoryPickup bottlePickup,
        PrototypeInventoryPickup medkitPickup,
        MainEscapeKeyGatePoint keyGatePoint,
        MainEscapeEmergencyStairsPoint authoredStairsPoint)
    {
        InteractionController = interactionController;
        FloorDefinition = floorDefinition;
        CurrentFloorNumber = Mathf.Max(1, currentFloorNumber);
        Escaped = escaped;
        ResetPlayerPosition = resetPlayerPosition;
        UsesAuthoredKeyGateSequence = usesAuthoredKeyGateSequence;
        PlayerController = playerController;
        PlayerRuntime = playerRuntime;
        FogOfWarOverlay = fogOfWarOverlay;
        FloorDirector = floorDirector;
        GoalPickup = goalPickup;
        StairPoint = stairPoint;
        FinalExitPoint = finalExitPoint;
        BatteryPickup = batteryPickup;
        BottlePickup = bottlePickup;
        MedkitPickup = medkitPickup;
        KeyGatePoint = keyGatePoint;
        AuthoredStairsPoint = authoredStairsPoint;
    }

    public IFloorEscapeInteractionController InteractionController { get; }
    public EscapeFloorDefinition FloorDefinition { get; }
    public int CurrentFloorNumber { get; }
    public bool Escaped { get; }
    public bool ResetPlayerPosition { get; }
    public bool UsesAuthoredKeyGateSequence { get; }
    public WasdPlayerController PlayerController { get; }
    public RPlayerRuntimeReferences PlayerRuntime { get; }
    public FlashlightFogOfWarOverlay FogOfWarOverlay { get; }
    public RFloorDirector FloorDirector { get; }
    public FloorEscapeGoalPickup GoalPickup { get; }
    public FloorEscapeTransitionPoint StairPoint { get; }
    public FloorEscapeTransitionPoint FinalExitPoint { get; }
    public PrototypeInventoryPickup BatteryPickup { get; }
    public PrototypeInventoryPickup BottlePickup { get; }
    public PrototypeInventoryPickup MedkitPickup { get; }
    public MainEscapeKeyGatePoint KeyGatePoint { get; }
    public MainEscapeEmergencyStairsPoint AuthoredStairsPoint { get; }
}

public static class RRunFloorStateApplier
{
    private const float DefaultFlashlightOffComfortRevealScale = 2f;
    private const float DefaultFlashlightOnComfortRevealScale = 2f;

    public static bool Apply(RRunFloorStateContext context)
    {
        EscapeFloorDefinition floor = context.FloorDefinition;

        if (floor == null)
        {
            return false;
        }

        if (context.ResetPlayerPosition && context.FloorDirector == null)
        {
            Debug.LogError(
                $"{nameof(RRunFloorStateApplier)} stopped because no floor director is bound for the authored floor rebuild path.");
            return false;
        }

        if (context.ResetPlayerPosition && context.FloorDirector != null)
        {
            if (!context.FloorDirector.BuildFloor(floor))
            {
                Debug.LogError(
                    $"{nameof(RRunFloorStateApplier)} stopped because authored floor build failed for {floor.FloorNumber}F.",
                    context.FloorDirector);
                return false;
            }
        }

        GridMapService mapService = context.FloorDirector != null ? context.FloorDirector.CurrentMapService : null;
        GeneratedFloorLayout layout = context.FloorDirector != null ? context.FloorDirector.CurrentLayout : null;

        if (context.ResetPlayerPosition && context.PlayerController != null)
        {
            Vector3 playerSpawnWorldPosition = GetPlayerSpawnWorldPosition(context, floor, layout, mapService);
            PlacePlayer(context.PlayerController, context.PlayerRuntime, playerSpawnWorldPosition, Vector2.up);

            PlayerTrailRecorder trailRecorder = context.PlayerRuntime != null ? context.PlayerRuntime.TrailRecorder : null;

            if (trailRecorder != null && mapService != null)
            {
                trailRecorder.Configure(mapService);
            }
        }

        if (context.ResetPlayerPosition)
        {
            context.FogOfWarOverlay?.ResetMemory();
        }

        EnsureFloorFlashlightState(context, floor);
        ApplyFloorVisionDefaults(context);
        bool usesElevatorPropDirectExit = UsesElevatorPropDirectExit(context);

        if (context.UsesAuthoredKeyGateSequence)
        {
            context.GoalPickup?.ConfigureAuthored(context.InteractionController, floor.ToolDisplayName, floor.AccentColor);
            ConfigureAuthoredStairsAnchor(context, usesElevatorPropDirectExit);
            ConfigureLegacyStairProxy(context, floor.AccentColor, usesElevatorPropDirectExit);
            context.FinalExitPoint?.ConfigureAuthored(context.InteractionController, FloorEscapeTransitionKind.FinalExit, floor.AccentColor);
            ConfigureAuthoredSupportPickups(context);

            if (usesElevatorPropDirectExit)
            {
                DisableLegacyExitResidue(context);
            }
            else if (context.InteractionController != null && context.InteractionController.UsesDirectAuthoredExitInteraction)
            {
                AlignDirectExitToAuthoredGate(context);
            }
        }
        else if (context.CurrentFloorNumber == 1)
        {
            if (context.GoalPickup != null)
            {
                context.GoalPickup.gameObject.SetActive(false);
            }

            DisableLegacyStairProxy(context);

            if (context.FinalExitPoint != null)
            {
                context.FinalExitPoint.ConfigureAuthored(context.InteractionController, FloorEscapeTransitionKind.FinalExit, floor.AccentColor);
            }
            else
            {
                context.AuthoredStairsPoint?.Configure(context.InteractionController);
            }

            ConfigureSupportPickups(context, mapService, layout, floor);
            PrototypeAudioManager.TryPrewarmFinalExitAudio();
        }
        else
        {
            Vector3 toolPosition = GetWorldPosition(GetToolCell(floor, layout), mapService);
            Vector3 transitionPosition = GetWorldPosition(GetTransitionCell(floor, layout), mapService);

            context.GoalPickup?.Configure(context.InteractionController, toolPosition, floor.ToolDisplayName, floor.AccentColor);
            ConfigureLegacyRuntimeStairProxy(context, transitionPosition, floor.AccentColor);
            context.FinalExitPoint?.Configure(context.InteractionController, FloorEscapeTransitionKind.FinalExit, transitionPosition, floor.AccentColor);
            ConfigureSupportPickups(context, mapService, layout, floor);
        }

        PrototypeAudioManager.TrySetFloorAmbience(floor.FloorNumber, context.Escaped);
        return true;
    }

    private static void ConfigureAuthoredStairsAnchor(RRunFloorStateContext context, bool usesElevatorPropDirectExit)
    {
        if (context.AuthoredStairsPoint == null || usesElevatorPropDirectExit)
        {
            return;
        }

        context.AuthoredStairsPoint.Configure(context.InteractionController);
    }

    private static void ConfigureLegacyStairProxy(
        RRunFloorStateContext context,
        Color floorAccent,
        bool usesElevatorPropDirectExit)
    {
        if (context.StairPoint == null)
        {
            return;
        }

        if (usesElevatorPropDirectExit || context.AuthoredStairsPoint != null)
        {
            context.StairPoint.gameObject.SetActive(false);
            return;
        }

        context.StairPoint.ConfigureAuthored(
            context.InteractionController,
            FloorEscapeTransitionKind.EmergencyStairs,
            floorAccent);
    }

    private static void ConfigureLegacyRuntimeStairProxy(
        RRunFloorStateContext context,
        Vector3 transitionPosition,
        Color floorAccent)
    {
        if (context.StairPoint == null || context.AuthoredStairsPoint != null)
        {
            return;
        }

        context.StairPoint.Configure(
            context.InteractionController,
            FloorEscapeTransitionKind.EmergencyStairs,
            transitionPosition,
            floorAccent);
    }

    private static void DisableLegacyStairProxy(RRunFloorStateContext context)
    {
        if (context.StairPoint != null)
        {
            context.StairPoint.gameObject.SetActive(false);
        }
    }

    private static void EnsureFloorFlashlightState(RRunFloorStateContext context, EscapeFloorDefinition floor)
    {
        if (floor == null || floor.FloorNumber > 4)
        {
            return;
        }

        PlayerFlashlightEquipment flashlightEquipment = context.PlayerRuntime != null
            ? context.PlayerRuntime.FlashlightEquipment
            : null;

        flashlightEquipment?.EnsureFlashlightEquipped(enabled: true);
    }

    private static void ApplyFloorVisionDefaults(RRunFloorStateContext context)
    {
        if (context.PlayerController == null)
        {
            return;
        }

        context.PlayerController.SetPlayerComfortRevealScale(
            DefaultFlashlightOffComfortRevealScale,
            DefaultFlashlightOnComfortRevealScale);
    }

    private static void ConfigureAuthoredSupportPickups(RRunFloorStateContext context)
    {
        ConfigureAuthoredSupportPickup(
            context,
            context.BatteryPickup,
            PrototypeItemCatalog.FlashlightBatteryItemId,
            "Flashlight Battery",
            1,
            new Color(0.46f, 0.9f, 1f, 1f));
        ConfigureAuthoredSupportPickup(
            context,
            context.BottlePickup,
            PrototypeItemCatalog.GlassBottleItemId,
            "Glass Bottle",
            2,
            new Color(0.72f, 0.92f, 1f, 1f));
        ConfigureAuthoredSupportPickup(
            context,
            context.MedkitPickup,
            PrototypeItemCatalog.MedkitItemId,
            "Medkit",
            1,
            new Color(0.45f, 1f, 0.66f, 1f));
    }

    private static void ConfigureAuthoredSupportPickup(
        RRunFloorStateContext context,
        PrototypeInventoryPickup pickup,
        string itemId,
        string displayName,
        int quantity,
        Color baseColor)
    {
        if (IsRuntimeManagedPickupPlacement(context, itemId))
        {
            DisableLegacySupportPickup(pickup);
            return;
        }

        pickup?.ConfigureAuthored(itemId, displayName, quantity, baseColor);
    }

    private static void DisableLegacySupportPickup(PrototypeInventoryPickup pickup)
    {
        if (pickup == null || pickup.SuppressRuntimeManagedPickupReplacement)
        {
            return;
        }

        pickup.gameObject.SetActive(false);
    }

    private static void ConfigureSupportPickups(
        RRunFloorStateContext context,
        GridMapService mapService,
        GeneratedFloorLayout layout,
        EscapeFloorDefinition floor)
    {
        ConfigureGeneratedSupportPickup(
            context,
            context.BatteryPickup,
            mapService,
            layout != null ? layout.BatteryCell : default,
            PrototypeItemCatalog.FlashlightBatteryItemId,
            1,
            new Color(0.46f, 0.9f, 1f, 1f),
            "flashlight battery");
        ConfigureGeneratedSupportPickup(
            context,
            context.BottlePickup,
            mapService,
            layout != null ? layout.GlassPanelCell : default,
            PrototypeItemCatalog.GlassBottleItemId,
            2,
            new Color(0.72f, 0.92f, 1f, 1f),
            "glass bottle");

        if (context.MedkitPickup != null)
        {
            if (IsRuntimeManagedPickupPlacement(context, PrototypeItemCatalog.MedkitItemId))
            {
                DisableLegacySupportPickup(context.MedkitPickup);
                return;
            }

            context.MedkitPickup.gameObject.SetActive(false);
            Debug.LogWarning(
                $"{nameof(RRunFloorStateApplier)} disabled the runtime medkit pickup on {floor.FloorNumber}F because the generated-floor medkit fallback has been removed. Author the medkit directly in scene or add an explicit layout slot before enabling it again.",
                context.MedkitPickup);
        }
    }

    private static void ConfigureGeneratedSupportPickup(
        RRunFloorStateContext context,
        PrototypeInventoryPickup pickup,
        GridMapService mapService,
        Vector3Int authoredCell,
        string itemId,
        int quantity,
        Color baseColor,
        string pickupLabel)
    {
        if (pickup == null)
        {
            return;
        }

        if (IsRuntimeManagedPickupPlacement(context, itemId))
        {
            DisableLegacySupportPickup(pickup);
            return;
        }

        if (mapService == null || !mapService.IsWalkable(authoredCell))
        {
            pickup.gameObject.SetActive(false);
            Debug.LogWarning(
                $"{nameof(RRunFloorStateApplier)} disabled the runtime {pickupLabel} pickup because no explicit authored/generated cell is available. Clean loop no longer guesses a fallback pickup position.",
                pickup);
            return;
        }

        pickup.Configure(GetWorldPosition(authoredCell, mapService), itemId, null, quantity, baseColor);
    }

    private static bool IsRuntimeManagedPickupPlacement(RRunFloorStateContext context, string itemId)
    {
        return context.FloorDirector != null
            && context.FloorDirector.IsRuntimeManagedPickupPlacement(itemId);
    }

    private static Vector3Int GetPlayerSpawnCell(EscapeFloorDefinition floor, GeneratedFloorLayout layout)
    {
        return layout != null ? layout.PlayerStartCell : floor.PlayerSpawnCell;
    }

    private static Vector3 GetPlayerSpawnWorldPosition(
        RRunFloorStateContext context,
        EscapeFloorDefinition floor,
        GeneratedFloorLayout layout,
        GridMapService mapService)
    {
        if (context.FloorDirector != null
            && context.FloorDirector.TryGetCurrentPlayerSpawnWorldPosition(out Vector3 authoredWorldPosition))
        {
            return authoredWorldPosition;
        }

        Vector3Int spawnCell = GetPlayerSpawnCell(floor, layout);
        return GetWorldPosition(spawnCell, mapService);
    }

    private static Vector3Int GetToolCell(EscapeFloorDefinition floor, GeneratedFloorLayout layout)
    {
        return layout != null ? layout.KeyCell : floor.ToolCell;
    }

    private static Vector3Int GetTransitionCell(EscapeFloorDefinition floor, GeneratedFloorLayout layout)
    {
        return layout != null ? layout.ExitCell : floor.TransitionCell;
    }

    private static Vector3 GetWorldPosition(Vector3Int cell, GridMapService mapService)
    {
        return mapService != null ? mapService.CellToWorldCenter(cell) : new Vector3(cell.x, cell.y, 0f);
    }

    private static void PlacePlayer(
        WasdPlayerController playerController,
        RPlayerRuntimeReferences playerRuntime,
        Vector3 worldPosition,
        Vector2 facingDirection)
    {
        if (playerController == null)
        {
            return;
        }

        Rigidbody2D body = playerRuntime != null ? playerRuntime.PlayerBody : null;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = worldPosition;
        }
        else
        {
            playerController.transform.position = worldPosition;
        }

        playerController.FaceDirection(facingDirection);
        Physics2D.SyncTransforms();
        SnapMainCameraToPlayer(playerController.gameObject.scene, worldPosition);
    }

    private static void SnapMainCameraToPlayer(Scene scene, Vector3 worldPosition)
    {
        Camera mainCamera = RSceneCameraUtility.FindPreferredCameraInScene(scene);

        if (mainCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = mainCamera.transform.position;
        cameraPosition.x = worldPosition.x;
        cameraPosition.y = worldPosition.y;
        mainCamera.transform.position = cameraPosition;
    }

    private static void AlignDirectExitToAuthoredGate(RRunFloorStateContext context)
    {
        Vector3? directExitWorldPosition = ResolveDirectExitWorldPosition(context);

        if (!directExitWorldPosition.HasValue)
        {
            return;
        }

        if (context.AuthoredStairsPoint == null && context.StairPoint != null)
        {
            context.StairPoint.transform.position = directExitWorldPosition.Value;
        }

        if (context.AuthoredStairsPoint != null)
        {
            context.AuthoredStairsPoint.transform.position = directExitWorldPosition.Value;
        }
    }

    private static Vector3? ResolveDirectExitWorldPosition(RRunFloorStateContext context)
    {
        if (TryResolveGateWorldPosition(context.KeyGatePoint as IGateAnchorReadModel, out Vector3 worldPosition))
        {
            return worldPosition;
        }

        if (TryResolveGateWorldPosition(context.FloorDirector as IGateAnchorReadModel, out worldPosition))
        {
            return worldPosition;
        }

        return null;
    }

    private static bool TryResolveGateWorldPosition(IGateAnchorReadModel gateAnchorReadModel, out Vector3 worldPosition)
    {
        if (gateAnchorReadModel != null && gateAnchorReadModel.TryGetGateWorldPosition(out worldPosition))
        {
            return true;
        }

        worldPosition = default;
        return false;
    }

    private static bool UsesElevatorPropDirectExit(RRunFloorStateContext context)
    {
        return RDirectExitRouteUtility.UsesElevatorPropDirectExit(context.CurrentFloorNumber)
            && context.InteractionController != null
            && context.InteractionController.UsesDirectAuthoredExitInteraction;
    }

    private static void DisableLegacyExitResidue(RRunFloorStateContext context)
    {
        DisableLegacyStairProxy(context);

        if (context.AuthoredStairsPoint != null)
        {
            context.AuthoredStairsPoint.gameObject.SetActive(false);
        }

        if (context.KeyGatePoint != null)
        {
            context.KeyGatePoint.gameObject.SetActive(false);
        }
    }
}

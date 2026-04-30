using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[Serializable]
public struct MainEscapeSupportItemPlacementQuota
{
    [SerializeField, Min(0)] private int batteryCount;
    [SerializeField, Min(0)] private int glassBottleCount;
    [SerializeField, Min(0)] private int medkitCount;

    public MainEscapeSupportItemPlacementQuota(int configuredBatteryCount, int configuredGlassBottleCount, int configuredMedkitCount)
    {
        batteryCount = Mathf.Max(0, configuredBatteryCount);
        glassBottleCount = Mathf.Max(0, configuredGlassBottleCount);
        medkitCount = Mathf.Max(0, configuredMedkitCount);
    }

    public int BatteryCount => Mathf.Max(0, batteryCount);
    public int GlassBottleCount => Mathf.Max(0, glassBottleCount);
    public int MedkitCount => Mathf.Max(0, medkitCount);
    public int TotalCount => BatteryCount + GlassBottleCount + MedkitCount;
}

[Serializable]
public struct MainEscapeEnemyPlacementQuota
{
    [SerializeField, Min(0)] private int patrolCount;
    [SerializeField, Min(0)] private int sentryCount;
    [SerializeField, Range(0, 1)] private int chaserCount;

    public MainEscapeEnemyPlacementQuota(int configuredPatrolCount, int configuredSentryCount, int configuredChaserCount)
    {
        patrolCount = Mathf.Max(0, configuredPatrolCount);
        sentryCount = Mathf.Max(0, configuredSentryCount);
        chaserCount = Mathf.Clamp(configuredChaserCount, 0, 1);
    }

    public int PatrolCount => Mathf.Max(0, patrolCount);
    public int SentryCount => Mathf.Max(0, sentryCount);
    public int ChaserCount => Mathf.Clamp(chaserCount, 0, 1);
}

[DisallowMultipleComponent]
public sealed class MainEscapeFloorAuthoring : MonoBehaviour
{
    private static MainEscapeRuntimeSettings RuntimeSettings => MainEscapeRuntimeSettings.Load();

    [Header("Local Authoring Names")]
    [SerializeField] private string authoringMarkersRootName = "AuthoringMarkers";
    [SerializeField] private string pickupMarkersRootName = "PickupMarkers";
    [SerializeField] private string itemPlacementMarkersRootName = "ItemPlacementMarkers";
    [SerializeField] private string keyPlacementMarkersRootName = "KeyPlacementMarkers";
    [SerializeField] private string enemyPlacementMarkersRootName = "EnemyPlacementMarkers";
    [SerializeField] private string chaserPlacementMarkersRootName = "ChaserPlacementMarkers";
    [SerializeField] private string dangerMarkersRootName = "DangerMarkers";
    [SerializeField] private string scareMarkersRootName = "ScareMarkers";
    [SerializeField] private string doorMarkersRootName = "Doors";
    [SerializeField] private string sentryGuardsRootName = "SentryGuards";
    [SerializeField] private string patrolSpawnRootName = "PatrolSpawn";
    [SerializeField] private string patrolSpawnLegacyRootName = "PatrolRoute";
    [SerializeField] private string patrolSpawnCandidatesRootName = "PatrolSpawnCandidates";
    [SerializeField] private string sentrySpawnCandidatesRootName = "SentrySpawnCandidates";
    [SerializeField] private string ventRouteRootName = "VentRoute";
    [SerializeField] private string interactivePropsRootName = "InteractiveProps";
    [SerializeField] private string goalVisualsRootName = "GoalVisuals";
    [SerializeField] private string coverPropsRootName = "CoverProps";
    [SerializeField] private string movementBlockersRootName = "MovementBlockers";
    [SerializeField] private string gameplayOverlayRootName = "GameplayOverlay";
    [SerializeField] private string blockAllOverlayRootName = "BlockAll";
    [SerializeField] private string moveOnlyOverlayRootName = "MoveOnly";
    [SerializeField] private string[] playerStartMarkerSearchNames = { "PlayerStart", "PlayerStartMarker" };
    [SerializeField] private string[] toolMarkerSearchNames = { "Tool", "ToolMarker" };
    [SerializeField] private string[] transitionMarkerSearchNames = { "Transition", "TransitionMarker" };
    [SerializeField] private string[] stalkerSpawnMarkerSearchNames = { "StalkerSpawn", "StalkerSpawnMarker" };
    [SerializeField] private string[] safeRoomMarkerSearchNames = { "SafeRoom", "SafeRoomMarker" };
    [SerializeField] private string[] keyPickupMarkerSearchNames = { "Key" };
    [SerializeField] private string[] batteryMarkerSearchNames = { "Pickup_BatteryMarker", "Battery", "BatteryMarker" };
    [SerializeField] private string[] glassBottleMarkerSearchNames = { "Pickup_GlassBottleMarker", "GlassPanel", "GlassPanelMarker" };

    [Header("Core Tilemaps")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap doorTilemap;

    [Header("Markers")]
    [SerializeField] private Transform playerStartMarker;
    [SerializeField] private Transform toolMarker;
    [SerializeField] private Transform transitionMarker;
    [SerializeField] private Transform stalkerSpawnMarker;
    [SerializeField] private Transform safeRoomMarker;
    [SerializeField] private Transform keyPickupMarker;
    [SerializeField] private Transform batteryMarker;
    [SerializeField] private Transform glassPanelMarker;
    [SerializeField] private Transform[] dangerMarkers = Array.Empty<Transform>();
    [SerializeField] private MainEscapeDangerPlacementMarker[] dangerPlacementMarkers = Array.Empty<MainEscapeDangerPlacementMarker>();
    [SerializeField] private MainEscapeItemPlacementMarker[] supportItemPlacementMarkers = Array.Empty<MainEscapeItemPlacementMarker>();
    [SerializeField] private MainEscapeItemPlacementMarker[] keyPlacementMarkers = Array.Empty<MainEscapeItemPlacementMarker>();
    [SerializeField] private MainEscapeEnemyPlacementMarker[] enemyPlacementMarkers = Array.Empty<MainEscapeEnemyPlacementMarker>();
    [SerializeField] private MainEscapeEnemyPlacementMarker[] chaserPlacementMarkers = Array.Empty<MainEscapeEnemyPlacementMarker>();

    [Header("Optional Authoring Helpers")]
    [FormerlySerializedAs("patrolRoute")]
    [SerializeField] private MainEscapePatrolSpawnAuthoring patrolSpawn;
    [SerializeField] private MainEscapeSentrySpawnAuthoring sentrySpawns;
    [SerializeField] private MainEscapeVentRouteAuthoring ventRoute;
    [SerializeField] private MainEscapeDoorAuthoring[] doorAuthorings = Array.Empty<MainEscapeDoorAuthoring>();
    [SerializeField] private MainEscapePropBlockerAuthoring[] propBlockers = Array.Empty<MainEscapePropBlockerAuthoring>();
    [SerializeField] private MainEscapeMovementBlockerAuthoring[] movementBlockers = Array.Empty<MainEscapeMovementBlockerAuthoring>();

    [Header("Runtime Visibility")]
    [SerializeField] private bool hideVentAuthoringRenderersOnPlay = true;

    [Header("Authoring Roots")]
    [SerializeField] private Transform authoringMarkersRoot;
    [SerializeField] private Transform pickupMarkersRoot;
    [SerializeField] private Transform dangerMarkersRoot;
    [SerializeField] private Transform scareMarkersRoot;
    [SerializeField] private Transform doorMarkersRoot;
    [SerializeField] private Transform sentryGuardsRoot;
    [SerializeField] private Transform sentrySpawnCandidatesRoot;
    [FormerlySerializedAs("patrolRouteRoot")]
    [SerializeField] private Transform patrolSpawnRoot;
    [SerializeField] private Transform patrolSpawnCandidatesRoot;
    [SerializeField] private Transform ventRouteRoot;
    [SerializeField] private Transform interactivePropsRoot;
    [SerializeField] private Transform goalVisualsRoot;
    [SerializeField] private Transform coverPropsRoot;
    [SerializeField] private Transform movementBlockersRoot;
    [SerializeField] private Transform itemPlacementMarkersRoot;
    [SerializeField] private Transform keyPlacementMarkersRoot;
    [SerializeField] private Transform enemyPlacementMarkersRoot;
    [SerializeField] private Transform chaserPlacementMarkersRoot;
    [SerializeField] private MainEscapeShadowStartleMarker[] shadowStartleMarkers = Array.Empty<MainEscapeShadowStartleMarker>();

    [Header("Placement Quotas")]
    [SerializeField] private MainEscapeSupportItemPlacementQuota supportItemPlacementQuota = new(1, 1, 0);
    [SerializeField] private MainEscapeEnemyPlacementQuota enemyPlacementQuota = new(0, 0, 1);
    [SerializeField, Min(0)] private int glassTrapPlacementCount;

    public Grid Grid => ResolveGrid();
    public Tilemap GroundTilemap => ResolveGroundTilemap();
    public Tilemap WallTilemap => ResolveWallTilemap();
    public Tilemap DoorTilemap => ResolveDoorTilemap();
    public Transform AuthoringMarkersRoot => FindMarkersRoot();
    public Transform PickupMarkersRoot => UsesLegacyPickupMarkerFallback() ? ResolvePickupMarkersRoot() : null;
    public Transform ItemPlacementMarkersRoot => ResolveItemPlacementMarkersRoot();
    public Transform KeyPlacementMarkersRoot => ResolveKeyPlacementMarkersRoot();
    public Transform EnemyPlacementMarkersRoot => ResolveEnemyPlacementMarkersRoot();
    public Transform ChaserPlacementMarkersRoot => ResolveChaserPlacementMarkersRoot();
    public Transform DangerMarkersRoot => ResolveDangerMarkersRoot();
    public Transform VentRouteRoot => ResolveVentRouteRoot();
    public Transform InteractivePropsRoot => ResolveInteractivePropsRoot();
    public Transform GoalVisualsRoot => ResolveGoalVisualsRoot();
    public Transform KeyPickupMarker => ResolveKeyPickupMarker();
    public MainEscapeSupportItemPlacementQuota SupportItemPlacementQuota => supportItemPlacementQuota;
    public MainEscapeEnemyPlacementQuota EnemyPlacementQuota => enemyPlacementQuota;
    public int GlassTrapPlacementCount => Mathf.Max(0, glassTrapPlacementCount);

    private string AuthoringMarkersRootName => DefaultIfBlank(authoringMarkersRootName, "AuthoringMarkers");
    private string PickupMarkersRootName => DefaultIfBlank(pickupMarkersRootName, "PickupMarkers");
    private string ItemPlacementMarkersRootName => DefaultIfBlank(itemPlacementMarkersRootName, "ItemPlacementMarkers");
    private string KeyPlacementMarkersRootName => DefaultIfBlank(keyPlacementMarkersRootName, "KeyPlacementMarkers");
    private string EnemyPlacementMarkersRootName => DefaultIfBlank(enemyPlacementMarkersRootName, "EnemyPlacementMarkers");
    private string ChaserPlacementMarkersRootName => DefaultIfBlank(chaserPlacementMarkersRootName, "ChaserPlacementMarkers");
    private string DangerMarkersRootName => DefaultIfBlank(dangerMarkersRootName, "DangerMarkers");
    private string ScareMarkersRootName => DefaultIfBlank(scareMarkersRootName, "ScareMarkers");
    private string DoorMarkersRootName => DefaultIfBlank(doorMarkersRootName, "Doors");
    private string SentryGuardsRootName => DefaultIfBlank(sentryGuardsRootName, "SentryGuards");
    private string PatrolSpawnRootName => DefaultIfBlank(patrolSpawnRootName, "PatrolSpawn");
    private string PatrolSpawnLegacyRootName => DefaultIfBlank(patrolSpawnLegacyRootName, "PatrolRoute");
    private string PatrolSpawnCandidatesRootName => DefaultIfBlank(patrolSpawnCandidatesRootName, "PatrolSpawnCandidates");
    private string SentrySpawnCandidatesRootName => DefaultIfBlank(sentrySpawnCandidatesRootName, "SentrySpawnCandidates");
    private string VentRouteRootName => DefaultIfBlank(ventRouteRootName, "VentRoute");
    private string InteractivePropsRootName => DefaultIfBlank(interactivePropsRootName, "InteractiveProps");
    private string GoalVisualsRootName => DefaultIfBlank(goalVisualsRootName, "GoalVisuals");
    private string CoverPropsRootName => DefaultIfBlank(coverPropsRootName, "CoverProps");
    private string MovementBlockersRootName => DefaultIfBlank(movementBlockersRootName, "MovementBlockers");
    private string GameplayOverlayRootName => DefaultIfBlank(gameplayOverlayRootName, "GameplayOverlay");
    private string BlockAllOverlayRootName => DefaultIfBlank(blockAllOverlayRootName, "BlockAll");
    private string MoveOnlyOverlayRootName => DefaultIfBlank(moveOnlyOverlayRootName, "MoveOnly");
    private string[] PlayerStartMarkerSearchNames => SanitizeConfiguredNames(playerStartMarkerSearchNames, "PlayerStart", "PlayerStartMarker");
    private string[] ToolMarkerSearchNames => SanitizeConfiguredNames(toolMarkerSearchNames, "Tool", "ToolMarker");
    private string[] TransitionMarkerSearchNames => SanitizeConfiguredNames(transitionMarkerSearchNames, "Transition", "TransitionMarker");
    private string[] StalkerSpawnMarkerSearchNames => SanitizeConfiguredNames(stalkerSpawnMarkerSearchNames, "StalkerSpawn", "StalkerSpawnMarker");
    private string[] SafeRoomMarkerSearchNames => SanitizeConfiguredNames(safeRoomMarkerSearchNames, "SafeRoom", "SafeRoomMarker");
    private string[] KeyPickupMarkerSearchNames => SanitizeConfiguredNames(keyPickupMarkerSearchNames, "Key");
    private string[] BatteryMarkerNames => SanitizeConfiguredNames(batteryMarkerSearchNames, "Pickup_BatteryMarker", "Battery", "BatteryMarker");
    private string[] GlassBottleMarkerNames => SanitizeConfiguredNames(glassBottleMarkerSearchNames, "Pickup_GlassBottleMarker", "GlassPanel", "GlassPanelMarker");

    public void ConfigureTilemaps(Grid configuredGrid, Tilemap configuredGround, Tilemap configuredWall, Tilemap configuredDoor)
    {
        grid = configuredGrid;
        groundTilemap = configuredGround;
        wallTilemap = configuredWall;
        doorTilemap = configuredDoor;
    }

    public void ConfigureMarkers(
        Transform configuredPlayerStartMarker,
        Transform configuredToolMarker,
        Transform configuredTransitionMarker,
        Transform configuredStalkerSpawnMarker,
        Transform configuredSafeRoomMarker,
        Transform configuredBatteryMarker,
        Transform configuredGlassPanelMarker,
        Transform[] configuredDangerMarkers)
    {
        playerStartMarker = configuredPlayerStartMarker;
        toolMarker = configuredToolMarker;
        transitionMarker = configuredTransitionMarker;
        stalkerSpawnMarker = configuredStalkerSpawnMarker;
        safeRoomMarker = configuredSafeRoomMarker;
        batteryMarker = configuredBatteryMarker;
        glassPanelMarker = configuredGlassPanelMarker;
        dangerMarkers = configuredDangerMarkers ?? Array.Empty<Transform>();
    }

    public void ConfigureHelpers(
        MainEscapePatrolSpawnAuthoring configuredPatrolSpawn,
        MainEscapeSentrySpawnAuthoring configuredSentrySpawns,
        MainEscapeVentRouteAuthoring configuredVentRoute,
        MainEscapeDoorAuthoring[] configuredDoorAuthorings,
        MainEscapePropBlockerAuthoring[] configuredPropBlockers)
    {
        patrolSpawn = configuredPatrolSpawn;
        sentrySpawns = configuredSentrySpawns;
        ventRoute = configuredVentRoute;
        doorAuthorings = configuredDoorAuthorings ?? Array.Empty<MainEscapeDoorAuthoring>();
        propBlockers = configuredPropBlockers ?? Array.Empty<MainEscapePropBlockerAuthoring>();
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            HideVentAuthoringRenderersForRuntime();
        }
    }

    private void Reset()
    {
    }

    private void OnValidate()
    {
    }

    [ContextMenu("Cache References From Hierarchy")]
    public void CacheReferencesFromHierarchy()
    {
        grid = GetComponent<Grid>();
        groundTilemap = FindTilemap(RuntimeSettings.GroundTilemapName);
        wallTilemap = FindTilemap(RuntimeSettings.WallTilemapName);
        doorTilemap = FindTilemap(RuntimeSettings.DoorTilemapName);

        authoringMarkersRoot = FindChildRoot(transform, AuthoringMarkersRootName);

        Transform markersRoot = authoringMarkersRoot;
        itemPlacementMarkersRoot = markersRoot != null
            ? FindChildRoot(markersRoot, ItemPlacementMarkersRootName)
            : null;
        keyPlacementMarkersRoot = markersRoot != null
            ? FindChildRoot(markersRoot, KeyPlacementMarkersRootName)
            : null;
        enemyPlacementMarkersRoot = markersRoot != null
            ? FindChildRoot(markersRoot, EnemyPlacementMarkersRootName)
            : null;
        chaserPlacementMarkersRoot = markersRoot != null
            ? FindChildRoot(markersRoot, ChaserPlacementMarkersRootName)
            : null;
        dangerMarkersRoot = markersRoot != null
            ? FindChildRoot(markersRoot, DangerMarkersRootName)
            : null;
        scareMarkersRoot = markersRoot != null
            ? FindChildRoot(markersRoot, ScareMarkersRootName)
            : null;
        doorMarkersRoot = markersRoot != null
            ? FindChildRoot(markersRoot, DoorMarkersRootName)
            : null;
        sentryGuardsRoot = markersRoot != null
            ? FindChildRoot(markersRoot, SentryGuardsRootName)
            : null;
        patrolSpawnRoot = FindPatrolSpawnRoot(markersRoot);
        patrolSpawnCandidatesRoot = markersRoot != null
            ? FindChildRoot(markersRoot, PatrolSpawnCandidatesRootName)
            : null;
        sentrySpawnCandidatesRoot = markersRoot != null
            ? FindChildRoot(markersRoot, SentrySpawnCandidatesRootName)
            : null;
        ventRouteRoot = markersRoot != null
            ? FindChildRoot(markersRoot, VentRouteRootName)
            : null;
        interactivePropsRoot = FindChildRoot(transform, InteractivePropsRootName);
        goalVisualsRoot = interactivePropsRoot != null
            ? FindChildRoot(interactivePropsRoot, GoalVisualsRootName)
            : null;
        coverPropsRoot = null;
        movementBlockersRoot = null;
        playerStartMarker = FindMarker(markersRoot, PlayerStartMarkerSearchNames);
        toolMarker = FindMarker(markersRoot, ToolMarkerSearchNames);
        transitionMarker = FindMarker(markersRoot, TransitionMarkerSearchNames);
        stalkerSpawnMarker = FindMarker(markersRoot, StalkerSpawnMarkerSearchNames);
        safeRoomMarker = FindMarker(markersRoot, SafeRoomMarkerSearchNames);
        supportItemPlacementMarkers = CollectPlacementMarkers(ResolveItemPlacementMarkersRoot(), MainEscapeItemPlacementCategory.SupportItem);
        keyPlacementMarkers = CollectPlacementMarkers(ResolveKeyPlacementMarkersRoot(), MainEscapeItemPlacementCategory.Key);
        pickupMarkersRoot = UsesLegacyPickupMarkerFallback()
            ? FindChildRoot(transform, PickupMarkersRootName)
            : null;
        keyPickupMarker = ResolveLegacyPickupMarker(pickupMarkersRoot, KeyPickupMarkerSearchNames);
        batteryMarker = ResolveLegacyPickupMarker(pickupMarkersRoot, BatteryMarkerNames);
        glassPanelMarker = ResolveLegacyPickupMarker(pickupMarkersRoot, GlassBottleMarkerNames);
        enemyPlacementMarkers = CollectEnemyPlacementMarkers(
            ResolveEnemyPlacementMarkersRoot(),
            MainEscapeEnemyPlacementKind.Patrol,
            MainEscapeEnemyPlacementKind.Sentry,
            MainEscapeEnemyPlacementKind.Shared);
        chaserPlacementMarkers = CollectEnemyPlacementMarkers(ResolveChaserPlacementMarkersRoot(), MainEscapeEnemyPlacementKind.Chaser);
        dangerPlacementMarkers = CollectDangerPlacementMarkers(ResolveDangerMarkersRoot());
        dangerMarkers = ResolveDangerMarkerTransforms(dangerPlacementMarkers);
        patrolSpawn = ResolvePatrolSpawnAuthoring(ResolvePatrolSpawnRoot());
        sentrySpawns = ResolveSentrySpawnAuthoring(ResolveSentryGuardsRoot());
        ventRoute = ResolveVentRouteAuthoring(ResolveVentRouteRoot());
        doorAuthorings = ResolveDoorAuthorings();
        propBlockers = Array.Empty<MainEscapePropBlockerAuthoring>();
        movementBlockers = Array.Empty<MainEscapeMovementBlockerAuthoring>();
        shadowStartleMarkers = ResolveShadowStartleMarkers();
    }

    [ContextMenu("Sync Pickup Positions From Markers")]
    private void SyncPickupPositionsFromMarkers()
    {
        SyncInteractivePickupsToMarkers();
    }

    [ContextMenu("Capture Placement Quotas From Scene")]
    private void CapturePlacementQuotasFromScene()
    {
        supportItemPlacementQuota = new MainEscapeSupportItemPlacementQuota(
            CountLegacyScenePickups(PrototypeItemCatalog.FlashlightBatteryItemId),
            CountLegacyScenePickups(PrototypeItemCatalog.GlassBottleItemId),
            CountLegacyScenePickups(PrototypeItemCatalog.MedkitItemId));
        enemyPlacementQuota = new MainEscapeEnemyPlacementQuota(
            CountLegacyPatrolMarkers(),
            CountLegacySentryMarkers(),
            CountLegacyChaserMarkers());
        glassTrapPlacementCount = CountLegacyGlassTrapPanels();
    }

    public bool HasRequiredTilemaps()
    {
        return ResolveGrid() != null
            && ResolveGroundTilemap() != null
            && ResolveWallTilemap() != null;
    }

    public bool TryResolvePlayerStartWorldPosition(out Vector3 worldPosition)
        => TryResolveMarkerWorldPosition(ResolveMarker(playerStartMarker, PlayerStartMarkerSearchNames), out worldPosition);

    public bool TryResolvePlayerStartCell(out Vector3Int cell) => TryResolveMarkerCell(ResolveMarker(playerStartMarker, PlayerStartMarkerSearchNames), out cell);
    public bool TryResolveToolCell(out Vector3Int cell)
    {
        if (TryResolveMarkerCell(ResolveMarker(toolMarker, ToolMarkerSearchNames), out cell))
        {
            return true;
        }

        return TryResolveSceneObjectCell(out cell, "RFloorTool", RuntimeSettings.FloorToolPickupName);
    }
    public bool TryResolveTransitionCell(out Vector3Int cell) => TryResolveMarkerCell(ResolveMarker(transitionMarker, TransitionMarkerSearchNames), out cell);
    public bool TryResolveStalkerSpawnCell(out Vector3Int cell)
    {
        MainEscapeEnemyPlacementMarker[] chaserMarkers = GetChaserPlacementMarkers();

        if (TryResolveEnemyMarkerCell(chaserMarkers, 0, out cell))
        {
            return true;
        }

        return TryResolveMarkerCell(ResolveMarker(stalkerSpawnMarker, StalkerSpawnMarkerSearchNames), out cell);
    }
    public bool TryResolveSafeRoomCell(out Vector3Int cell)
    {
        if (TryResolveMarkerCell(ResolveMarker(safeRoomMarker, SafeRoomMarkerSearchNames), out cell))
        {
            return true;
        }

        return TryResolveSceneObjectCell(
            out cell,
            "REmergencyStairsVisual",
            RuntimeSettings.EmergencyStairsVisualName,
            "REmergencyStairs",
            RuntimeSettings.EmergencyStairsName);
    }
    public bool TryResolveKeyPickupWorldPosition(out Vector3 worldPosition)
    {
        MainEscapeItemPlacementMarker[] markers = GetKeyPlacementMarkers();

        if (markers.Length > 0)
        {
            worldPosition = markers[0].GetWorldPosition();
            return true;
        }

        return TryResolveMarkerWorldPosition(ResolveKeyPickupMarker(), out worldPosition);
    }
    public bool TryResolveBatteryCell(out Vector3Int cell)
    {
        if (TryResolveSupportItemMarkerCell(0, out cell))
        {
            return true;
        }

        if (TryResolveMarkerCell(ResolveBatteryMarker(), out cell))
        {
            return true;
        }

        return TryResolveSceneObjectCell(out cell, RuntimeSettings.AuthoredBatteryPickupName, RuntimeSettings.BatteryRuntimePickupName);
    }

    public bool TryResolveGlassPanelCell(out Vector3Int cell)
    {
        if (TryResolveSupportItemMarkerCell(1, out cell))
        {
            return true;
        }

        if (TryResolveMarkerCell(ResolveGlassPanelMarker(), out cell))
        {
            return true;
        }

        return TryResolveSceneObjectCell(out cell, RuntimeSettings.AuthoredGlassBottlePickupName, RuntimeSettings.GlassBottleRuntimePickupName);
    }

    public bool TryResolveBatteryWorldPosition(out Vector3 worldPosition)
    {
        if (TryResolveSupportItemMarkerWorldPosition(0, out worldPosition))
        {
            return true;
        }

        if (TryResolveMarkerWorldPosition(ResolveBatteryMarker(), out worldPosition))
        {
            return true;
        }

        return TryResolveSceneObjectWorldPosition(out worldPosition, RuntimeSettings.AuthoredBatteryPickupName, RuntimeSettings.BatteryRuntimePickupName);
    }

    public bool TryResolveGlassPanelWorldPosition(out Vector3 worldPosition)
    {
        if (TryResolveSupportItemMarkerWorldPosition(1, out worldPosition))
        {
            return true;
        }

        if (TryResolveMarkerWorldPosition(ResolveGlassPanelMarker(), out worldPosition))
        {
            return true;
        }

        return TryResolveSceneObjectWorldPosition(out worldPosition, RuntimeSettings.AuthoredGlassBottlePickupName, RuntimeSettings.GlassBottleRuntimePickupName);
    }

    public Vector3Int[] GetDangerCells()
    {
        return ResolveMarkers(GetDangerMarkerTransforms());
    }

    public MainEscapeDangerPlacementMarker[] GetDangerPlacementMarkers()
    {
        Transform root = ResolveDangerMarkersRoot();

        if (root != null)
        {
            return CollectDangerPlacementMarkers(root);
        }

        return dangerPlacementMarkers ?? Array.Empty<MainEscapeDangerPlacementMarker>();
    }

    public Transform[] GetDangerMarkerTransforms()
    {
        if (ResolveDangerMarkersRoot() != null)
        {
            return ResolveDangerMarkerTransforms(GetDangerPlacementMarkers());
        }

        if (dangerMarkers != null && dangerMarkers.Length > 0)
        {
            return dangerMarkers;
        }

        return ResolveDangerMarkerTransforms(GetDangerPlacementMarkers());
    }

    public bool TryResolvePatrolSpawnCell(out Vector3Int cell)
    {
        cell = default;
        Vector3Int[] markerDrivenCells = ResolveEnemyPlacementCells(MainEscapeEnemyPlacementKind.Patrol, supportCandidatesOnly: false);

        if (markerDrivenCells.Length > 0)
        {
            cell = markerDrivenCells[0];
            return true;
        }

        MainEscapePatrolSpawnAuthoring spawnAuthoring = ResolvePatrolSpawn();

        if (spawnAuthoring == null)
        {
            return false;
        }

        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();

        if (spawnAuthoring.TryGetSpawnCell(resolvedGroundTilemap, out cell))
        {
            return true;
        }

        Vector3Int[] authoredSpawnCells = spawnAuthoring.GetSpawnCells(resolvedGroundTilemap);

        if (authoredSpawnCells.Length > 0)
        {
            cell = authoredSpawnCells[0];
            return true;
        }

        return false;
    }

    public Vector3Int[] GetPatrolSpawnCells()
    {
        Vector3Int[] markerDrivenCells = ResolveEnemyPlacementCells(MainEscapeEnemyPlacementKind.Patrol, supportCandidatesOnly: false);

        if (markerDrivenCells.Length > 0)
        {
            return markerDrivenCells;
        }

        MainEscapePatrolSpawnAuthoring spawnAuthoring = ResolvePatrolSpawn();

        if (spawnAuthoring == null)
        {
            return Array.Empty<Vector3Int>();
        }

        return spawnAuthoring.GetSpawnCells(ResolveGroundTilemap());
    }

    public Vector3Int[] GetPatrolSpawnCandidateCells()
    {
        MainEscapeEnemyPlacementMarker[] patrolMarkers = GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind.Patrol);

        if (patrolMarkers.Length > 0)
        {
            return ResolveEnemyMarkerCells(patrolMarkers);
        }

        MainEscapePatrolSpawnAuthoring candidateAuthoring = ResolvePatrolSpawnAuthoring(ResolvePatrolSpawnCandidatesRoot());
        return candidateAuthoring != null
            ? candidateAuthoring.GetSpawnCells(ResolveGroundTilemap())
            : Array.Empty<Vector3Int>();
    }

    public Vector3Int[] GetPatrolRouteCells()
    {
        Vector3Int[] markerDrivenCells = ResolveEnemyPlacementCells(MainEscapeEnemyPlacementKind.Patrol, supportCandidatesOnly: false);

        if (markerDrivenCells.Length > 0)
        {
            return markerDrivenCells;
        }

        MainEscapePatrolSpawnAuthoring spawnAuthoring = ResolvePatrolSpawn();
        Transform patrolRoot = ResolvePatrolSpawnRoot();

        if (spawnAuthoring == null)
        {
            return Array.Empty<Vector3Int>();
        }

        if (!IsExplicitPatrolSpawnRoot(patrolRoot))
        {
            return spawnAuthoring.GetSpawnCells(ResolveGroundTilemap());
        }

        return TryResolvePatrolSpawnCell(out Vector3Int patrolSpawnCell)
            ? new[] { patrolSpawnCell }
            : Array.Empty<Vector3Int>();
    }

    public Vector3Int[] GetSentrySpawnCells()
    {
        MainEscapeSentrySpawnAuthoring sentrySpawnAuthoring = ResolveSentrySpawns();
        return sentrySpawnAuthoring != null ? sentrySpawnAuthoring.GetSpawnCells(ResolveGroundTilemap()) : Array.Empty<Vector3Int>();
    }

    public MainEscapeSentrySpawnPoint[] GetSentrySpawnPoints()
    {
        MainEscapeSentrySpawnPoint[] markerDrivenPoints = ResolveEnemyPlacementPoints(MainEscapeEnemyPlacementKind.Sentry);

        if (markerDrivenPoints.Length > 0)
        {
            return markerDrivenPoints;
        }

        MainEscapeSentrySpawnAuthoring sentrySpawnAuthoring = ResolveSentrySpawns();
        return sentrySpawnAuthoring != null ? sentrySpawnAuthoring.GetSpawnPoints(ResolveGroundTilemap()) : Array.Empty<MainEscapeSentrySpawnPoint>();
    }

    public MainEscapeSentrySpawnPoint[] GetSentrySpawnCandidatePoints()
    {
        MainEscapeEnemyPlacementMarker[] sentryMarkers = GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind.Sentry);

        if (sentryMarkers.Length > 0)
        {
            return ResolveEnemyMarkerSpawnPoints(sentryMarkers);
        }

        MainEscapeSentrySpawnAuthoring candidateAuthoring = ResolveSentrySpawnAuthoring(ResolveSentrySpawnCandidatesRoot());
        return candidateAuthoring != null
            ? candidateAuthoring.GetSpawnPoints(ResolveGroundTilemap())
            : Array.Empty<MainEscapeSentrySpawnPoint>();
    }

    public MainEscapeItemPlacementMarker[] GetSupportItemPlacementMarkers()
    {
        Transform root = ResolveItemPlacementMarkersRoot();

        if (root != null)
        {
            return CollectPlacementMarkers(root, MainEscapeItemPlacementCategory.SupportItem);
        }

        return supportItemPlacementMarkers ?? Array.Empty<MainEscapeItemPlacementMarker>();
    }

    public MainEscapeItemPlacementMarker[] GetKeyPlacementMarkers()
    {
        Transform root = ResolveKeyPlacementMarkersRoot();

        if (root != null)
        {
            return CollectPlacementMarkers(root, MainEscapeItemPlacementCategory.Key);
        }

        return keyPlacementMarkers ?? Array.Empty<MainEscapeItemPlacementMarker>();
    }

    public MainEscapeEnemyPlacementMarker[] GetChaserPlacementMarkers()
    {
        return GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind.Chaser);
    }

    public MainEscapeEnemyPlacementMarker[] GetSharedEnemyPlacementMarkers()
    {
        Transform root = ResolveEnemyPlacementMarkersRoot();

        if (root != null)
        {
            return CollectEnemyPlacementMarkers(
                root,
                MainEscapeEnemyPlacementKind.Patrol,
                MainEscapeEnemyPlacementKind.Sentry,
                MainEscapeEnemyPlacementKind.Shared);
        }

        return FilterEnemyPlacementMarkers(
            enemyPlacementMarkers ?? Array.Empty<MainEscapeEnemyPlacementMarker>(),
            MainEscapeEnemyPlacementKind.Patrol,
            MainEscapeEnemyPlacementKind.Sentry,
            MainEscapeEnemyPlacementKind.Shared);
    }

    public MainEscapeEnemyPlacementMarker[] GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind placementKind)
    {
        MainEscapeEnemyPlacementKind[] supportedKinds = GetSupportedPlacementKinds(placementKind);
        Transform root = placementKind == MainEscapeEnemyPlacementKind.Chaser
            ? ResolveChaserPlacementMarkersRoot()
            : ResolveEnemyPlacementMarkersRoot();

        if (root != null)
        {
            return CollectEnemyPlacementMarkers(root, supportedKinds);
        }

        MainEscapeEnemyPlacementMarker[] cachedMarkers = placementKind == MainEscapeEnemyPlacementKind.Chaser
            ? chaserPlacementMarkers
            : enemyPlacementMarkers;
        return FilterEnemyPlacementMarkers(cachedMarkers ?? Array.Empty<MainEscapeEnemyPlacementMarker>(), supportedKinds);
    }

    public bool HasSupportItemPlacementMarkers() => GetSupportItemPlacementMarkers().Length > 0;
    public bool HasKeyPlacementMarkers() => GetKeyPlacementMarkers().Length > 0;
    public bool HasEnemyPlacementMarkers() =>
        GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind.Patrol).Length > 0
        || GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind.Sentry).Length > 0
        || GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind.Chaser).Length > 0;

    public MainEscapeVentRouteDefinition GetVentRouteDefinition()
    {
        MainEscapeVentRouteAuthoring ventRouteAuthoring = ResolveVentRoute();
        return ventRouteAuthoring != null
            ? ventRouteAuthoring.BuildRouteDefinition(ResolveGroundTilemap(), GetComponent<GeneratedFloorLayout>())
            : MainEscapeVentRouteDefinition.Empty;
    }

    public void HideVentAuthoringRenderersForRuntime()
    {
        if (!hideVentAuthoringRenderersOnPlay)
        {
            return;
        }

        Transform markersRoot = FindMarkersRoot();
        DisableRendererVisuals(ResolveVentRouteRoot());
        DisableNamedVentVisualRoots(markersRoot);
        DisableVentAuthoringComponentRenderers();
    }

    public MainEscapeShadowStartleMarker[] GetShadowStartleMarkers()
    {
        return ResolveShadowStartleMarkers();
    }

    public GeneratedDoorGroupData[] BuildDoorGroups()
    {
        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();
        Tilemap resolvedDoorTilemap = ResolveDoorTilemap();
        MainEscapeDoorAuthoring[] resolvedDoorAuthorings = ResolveDoorAuthorings();
        MainEscapeSelfContainedDoor[] selfContainedDoors = GetComponentsInChildren<MainEscapeSelfContainedDoor>(true);

        List<GeneratedDoorGroupData> groups = new();
        int groupId = 0;

        if (resolvedGroundTilemap == null)
        {
            return Array.Empty<GeneratedDoorGroupData>();
        }

        if (resolvedDoorTilemap != null && resolvedDoorAuthorings != null)
        {
            for (int index = 0; index < resolvedDoorAuthorings.Length; index++)
            {
                MainEscapeDoorAuthoring authoring = resolvedDoorAuthorings[index];

                if (authoring == null)
                {
                    continue;
                }

                Vector3Int[] doorCells = authoring.GetDoorCells(resolvedGroundTilemap, resolvedDoorTilemap);

                if (doorCells.Length == 0)
                {
                    continue;
                }

                groups.Add(new GeneratedDoorGroupData(groupId++, authoring.RequiresKey, doorCells, Array.Empty<int>()));
            }
        }

        for (int index = 0; index < selfContainedDoors.Length; index++)
        {
            MainEscapeSelfContainedDoor door = selfContainedDoors[index];

            if (door == null)
            {
                continue;
            }

            Vector3Int[] doorCells = door.ResolveDoorCells(resolvedGroundTilemap);
            if (doorCells.Length == 0)
            {
                continue;
            }

            groups.Add(new GeneratedDoorGroupData(groupId++, door.RequiresKey, doorCells, Array.Empty<int>()));
        }

        if (groups.Count > 0)
        {
            return groups.ToArray();
        }

        return MainEscapeVisualAuthoringSynthesis.BuildVisualDoorGroups(transform, resolvedGroundTilemap);
    }

    public Vector3Int[] GetMainDoorCells()
    {
        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();
        Tilemap resolvedDoorTilemap = ResolveDoorTilemap();
        MainEscapeDoorAuthoring[] resolvedDoorAuthorings = ResolveDoorAuthorings();
        MainEscapeSelfContainedDoor[] selfContainedDoors = GetComponentsInChildren<MainEscapeSelfContainedDoor>(true);

        if (resolvedGroundTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        if (resolvedDoorAuthorings != null)
        {
            for (int index = 0; index < resolvedDoorAuthorings.Length; index++)
            {
                MainEscapeDoorAuthoring authoring = resolvedDoorAuthorings[index];

                if (authoring == null || !authoring.MarksMainGate)
                {
                    continue;
                }

                Vector3Int[] doorCells = authoring.GetDoorCells(resolvedGroundTilemap, resolvedDoorTilemap);

                if (doorCells.Length > 0)
                {
                    return doorCells;
                }
            }
        }

        for (int index = 0; index < selfContainedDoors.Length; index++)
        {
            MainEscapeSelfContainedDoor door = selfContainedDoors[index];

            if (door == null || !door.MarksMainGate)
            {
                continue;
            }

            Vector3Int[] doorCells = door.ResolveDoorCells(resolvedGroundTilemap);
            if (doorCells.Length > 0)
            {
                return doorCells;
            }
        }

        Transform mainGateAnchor = FindSceneObjectByNames("RKeyGateVisual", RuntimeSettings.KeyGateVisualName);

        if (mainGateAnchor != null && resolvedDoorTilemap != null)
        {
            Vector3Int[] fallbackDoorCells = FindNearestDoorCluster(resolvedGroundTilemap, resolvedDoorTilemap, mainGateAnchor.position, 3);

            if (fallbackDoorCells.Length > 0)
            {
                return fallbackDoorCells;
            }
        }

        return Array.Empty<Vector3Int>();
    }

    public void RegisterPropBlockers(GridMapService mapService)
    {
        if (mapService == null)
        {
            return;
        }

        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();

        if (resolvedGroundTilemap == null)
        {
            return;
        }

        RegisterOverlayCells(mapService.RegisterPropCells, ResolveOverlayTilemap(BlockAllOverlayRootName));
        RegisterOverlayCells(mapService.RegisterMovementBlockingCells, ResolveOverlayTilemap(MoveOnlyOverlayRootName));
        RegisterPropBlockerCells(
            mapService.RegisterPropCells,
            mapService.RegisterMovementBlockingCells,
            ResolvePropBlockers(),
            resolvedGroundTilemap);
        mapService.RegisterMovementBlockingCells(GetVisualPropCells());
        RegisterMovementBlockerCells(mapService.RegisterMovementBlockingCells, ResolveMovementBlockers(), resolvedGroundTilemap);
    }

    public Vector3Int[] GetVisualPropCells()
    {
        return MainEscapeVisualAuthoringSynthesis.CollectVisualPropCells(transform, ResolveGroundTilemap());
    }

    private bool TryResolveMarkerCell(Transform marker, out Vector3Int cell)
    {
        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();

        if (marker != null && resolvedGroundTilemap != null)
        {
            cell = MainEscapeTilemapCellUtility.WorldToCell2D(resolvedGroundTilemap, marker.position);
            return true;
        }

        cell = Vector3Int.zero;
        return false;
    }

    private static bool TryResolveMarkerWorldPosition(Transform marker, out Vector3 worldPosition)
    {
        if (marker != null)
        {
            worldPosition = marker.position;
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    private Vector3Int[] ResolveMarkers(Transform[] markers)
    {
        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();

        if (markers == null || markers.Length == 0 || resolvedGroundTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        List<Vector3Int> cells = new(markers.Length);
        HashSet<Vector3Int> seen = new();

        for (int index = 0; index < markers.Length; index++)
        {
            Transform marker = markers[index];

            if (marker == null)
            {
                continue;
            }

            Vector3Int cell = MainEscapeTilemapCellUtility.WorldToCell2D(resolvedGroundTilemap, marker.position);

            if (seen.Add(cell))
            {
                cells.Add(cell);
            }
        }

        return cells.ToArray();
    }

    private bool TryResolveSupportItemMarkerCell(int markerIndex, out Vector3Int cell)
    {
        MainEscapeItemPlacementMarker[] markers = GetSupportItemPlacementMarkers();
        return TryResolvePlacementMarkerCell(markers, markerIndex, out cell);
    }

    private bool TryResolveSupportItemMarkerWorldPosition(int markerIndex, out Vector3 worldPosition)
    {
        MainEscapeItemPlacementMarker[] markers = GetSupportItemPlacementMarkers();
        return TryResolvePlacementMarkerWorldPosition(markers, markerIndex, out worldPosition);
    }

    private bool TryResolvePlacementMarkerCell(
        MainEscapeItemPlacementMarker[] markers,
        int markerIndex,
        out Vector3Int cell)
    {
        if (markers != null && markerIndex >= 0 && markerIndex < markers.Length && markers[markerIndex] != null)
        {
            return markers[markerIndex].TryResolveCell(ResolveGroundTilemap(), out cell);
        }

        cell = Vector3Int.zero;
        return false;
    }

    private static bool TryResolvePlacementMarkerWorldPosition(
        MainEscapeItemPlacementMarker[] markers,
        int markerIndex,
        out Vector3 worldPosition)
    {
        if (markers != null && markerIndex >= 0 && markerIndex < markers.Length && markers[markerIndex] != null)
        {
            worldPosition = markers[markerIndex].GetWorldPosition();
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    private bool TryResolveEnemyMarkerCell(
        MainEscapeEnemyPlacementMarker[] markers,
        int markerIndex,
        out Vector3Int cell)
    {
        if (markers != null && markerIndex >= 0 && markerIndex < markers.Length && markers[markerIndex] != null)
        {
            return markers[markerIndex].TryResolveCell(ResolveGroundTilemap(), out cell);
        }

        cell = Vector3Int.zero;
        return false;
    }

    private Vector3Int[] ResolveEnemyPlacementCells(
        MainEscapeEnemyPlacementKind placementKind,
        bool supportCandidatesOnly)
    {
        MainEscapeEnemyPlacementMarker[] markers = GetEnemyPlacementMarkers(placementKind);

        if (markers.Length == 0)
        {
            return Array.Empty<Vector3Int>();
        }

        int desiredCount = ResolveEnemyPlacementCount(placementKind);

        if (supportCandidatesOnly || desiredCount <= 0 || desiredCount >= markers.Length)
        {
            return ResolveEnemyMarkerCells(markers);
        }

        MainEscapeEnemyPlacementMarker[] activeMarkers = new MainEscapeEnemyPlacementMarker[desiredCount];

        for (int index = 0; index < desiredCount; index++)
        {
            activeMarkers[index] = markers[index];
        }

        return ResolveEnemyMarkerCells(activeMarkers);
    }

    private MainEscapeSentrySpawnPoint[] ResolveEnemyPlacementPoints(MainEscapeEnemyPlacementKind placementKind)
    {
        MainEscapeEnemyPlacementMarker[] markers = GetEnemyPlacementMarkers(placementKind);

        if (markers.Length == 0)
        {
            return Array.Empty<MainEscapeSentrySpawnPoint>();
        }

        int desiredCount = ResolveEnemyPlacementCount(placementKind);

        if (desiredCount > 0 && desiredCount < markers.Length)
        {
            MainEscapeEnemyPlacementMarker[] activeMarkers = new MainEscapeEnemyPlacementMarker[desiredCount];

            for (int index = 0; index < desiredCount; index++)
            {
                activeMarkers[index] = markers[index];
            }

            markers = activeMarkers;
        }

        return ResolveEnemyMarkerSpawnPoints(markers);
    }

    private int ResolveEnemyPlacementCount(MainEscapeEnemyPlacementKind placementKind)
    {
        return placementKind switch
        {
            MainEscapeEnemyPlacementKind.Patrol => enemyPlacementQuota.PatrolCount,
            MainEscapeEnemyPlacementKind.Sentry => enemyPlacementQuota.SentryCount,
            MainEscapeEnemyPlacementKind.Chaser => enemyPlacementQuota.ChaserCount,
            _ => 0
        };
    }

    private MainEscapeItemPlacementMarker[] CollectPlacementMarkers(
        Transform root,
        MainEscapeItemPlacementCategory category)
    {
        if (root == null)
        {
            return Array.Empty<MainEscapeItemPlacementMarker>();
        }

        MainEscapeItemPlacementMarker[] markers = root.GetComponentsInChildren<MainEscapeItemPlacementMarker>(true);
        List<MainEscapeItemPlacementMarker> filtered = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeItemPlacementMarker marker = markers[index];

            if (marker == null || marker.Category != category || marker.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            filtered.Add(marker);
        }

        SortPlacementMarkers(filtered);
        return filtered.ToArray();
    }

    private MainEscapeEnemyPlacementMarker[] CollectEnemyPlacementMarkers(
        Transform root,
        params MainEscapeEnemyPlacementKind[] supportedKinds)
    {
        if (root == null)
        {
            return Array.Empty<MainEscapeEnemyPlacementMarker>();
        }

        MainEscapeEnemyPlacementMarker[] markers = root.GetComponentsInChildren<MainEscapeEnemyPlacementMarker>(true);
        List<MainEscapeEnemyPlacementMarker> filtered = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeEnemyPlacementMarker marker = markers[index];

            if (marker == null || marker.gameObject.scene != gameObject.scene || !SupportsPlacementKind(marker.PlacementKind, supportedKinds))
            {
                continue;
            }

            filtered.Add(marker);
        }

        SortEnemyPlacementMarkers(filtered);
        return filtered.ToArray();
    }

    private MainEscapeDangerPlacementMarker[] CollectDangerPlacementMarkers(Transform root)
    {
        if (root == null)
        {
            return Array.Empty<MainEscapeDangerPlacementMarker>();
        }

        MainEscapeDangerPlacementMarker[] markers = root.GetComponentsInChildren<MainEscapeDangerPlacementMarker>(true);
        List<MainEscapeDangerPlacementMarker> filtered = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeDangerPlacementMarker marker = markers[index];

            if (marker == null || marker.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            filtered.Add(marker);
        }

        filtered.Sort((left, right) => string.CompareOrdinal(
            left != null ? left.PlacementId : string.Empty,
            right != null ? right.PlacementId : string.Empty));
        return filtered.ToArray();
    }

    private static MainEscapeEnemyPlacementMarker[] FilterEnemyPlacementMarkers(
        MainEscapeEnemyPlacementMarker[] markers,
        params MainEscapeEnemyPlacementKind[] supportedKinds)
    {
        if (markers == null || markers.Length == 0)
        {
            return Array.Empty<MainEscapeEnemyPlacementMarker>();
        }

        List<MainEscapeEnemyPlacementMarker> filtered = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeEnemyPlacementMarker marker = markers[index];

            if (marker != null && SupportsPlacementKind(marker.PlacementKind, supportedKinds))
            {
                filtered.Add(marker);
            }
        }

        SortEnemyPlacementMarkers(filtered);
        return filtered.ToArray();
    }

    private static MainEscapeEnemyPlacementKind[] GetSupportedPlacementKinds(MainEscapeEnemyPlacementKind placementKind)
    {
        return placementKind switch
        {
            MainEscapeEnemyPlacementKind.Patrol => new[]
            {
                MainEscapeEnemyPlacementKind.Patrol,
                MainEscapeEnemyPlacementKind.Shared
            },
            MainEscapeEnemyPlacementKind.Sentry => new[]
            {
                MainEscapeEnemyPlacementKind.Sentry,
                MainEscapeEnemyPlacementKind.Shared
            },
            MainEscapeEnemyPlacementKind.Chaser => new[]
            {
                MainEscapeEnemyPlacementKind.Chaser
            },
            MainEscapeEnemyPlacementKind.Shared => new[]
            {
                MainEscapeEnemyPlacementKind.Patrol,
                MainEscapeEnemyPlacementKind.Sentry,
                MainEscapeEnemyPlacementKind.Shared
            },
            _ => Array.Empty<MainEscapeEnemyPlacementKind>()
        };
    }

    private Vector3Int[] ResolveEnemyMarkerCells(MainEscapeEnemyPlacementMarker[] markers)
    {
        if (markers == null || markers.Length == 0)
        {
            return Array.Empty<Vector3Int>();
        }

        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();

        if (resolvedGroundTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        List<Vector3Int> cells = new(markers.Length);
        HashSet<Vector3Int> seen = new();

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeEnemyPlacementMarker marker = markers[index];

            if (marker == null || !marker.TryResolveCell(resolvedGroundTilemap, out Vector3Int cell) || !seen.Add(cell))
            {
                continue;
            }

            cells.Add(cell);
        }

        return cells.ToArray();
    }

    private MainEscapeSentrySpawnPoint[] ResolveEnemyMarkerSpawnPoints(MainEscapeEnemyPlacementMarker[] markers)
    {
        if (markers == null || markers.Length == 0)
        {
            return Array.Empty<MainEscapeSentrySpawnPoint>();
        }

        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();

        if (resolvedGroundTilemap == null)
        {
            return Array.Empty<MainEscapeSentrySpawnPoint>();
        }

        List<MainEscapeSentrySpawnPoint> spawnPoints = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeEnemyPlacementMarker marker = markers[index];

            if (marker == null || !marker.TryResolveCell(resolvedGroundTilemap, out Vector3Int cell))
            {
                continue;
            }

            spawnPoints.Add(new MainEscapeSentrySpawnPoint(
                marker.PlacementId,
                cell,
                marker.Facing,
                marker.GetWorldPosition()));
        }

        return spawnPoints.ToArray();
    }

    private int CountLegacyScenePickups(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        PrototypeInventoryPickup[] pickups = GetComponentsInChildren<PrototypeInventoryPickup>(true);
        int count = 0;

        for (int index = 0; index < pickups.Length; index++)
        {
            PrototypeInventoryPickup pickup = pickups[index];

            if (pickup != null && string.Equals(pickup.ItemId, itemId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private int CountLegacyPatrolMarkers()
    {
        return ResolvePatrolSpawn() != null
            ? ResolvePatrolSpawn().GetSpawnCells(ResolveGroundTilemap()).Length
            : 0;
    }

    private int CountLegacySentryMarkers()
    {
        return ResolveSentrySpawns() != null
            ? ResolveSentrySpawns().GetSpawnPoints(ResolveGroundTilemap()).Length
            : 0;
    }

    private int CountLegacyChaserMarkers()
    {
        return ResolveMarker(stalkerSpawnMarker, StalkerSpawnMarkerSearchNames) != null ? 1 : 0;
    }

    private int CountLegacyGlassTrapPanels()
    {
        Scene scene = gameObject.scene;

        if (!scene.IsValid())
        {
            return 0;
        }

        return RSceneReferenceLookup.FindComponentsInScene<NoiseFloorPanel>(scene).Length;
    }

    private static bool SupportsPlacementKind(
        MainEscapeEnemyPlacementKind placementKind,
        MainEscapeEnemyPlacementKind[] supportedKinds)
    {
        if (supportedKinds == null || supportedKinds.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < supportedKinds.Length; index++)
        {
            if (placementKind == supportedKinds[index])
            {
                return true;
            }
        }

        return false;
    }

    private static void SortPlacementMarkers(List<MainEscapeItemPlacementMarker> markers)
    {
        if (markers == null || markers.Count <= 1)
        {
            return;
        }

        markers.Sort((left, right) => string.CompareOrdinal(
            left != null ? left.PlacementId : string.Empty,
            right != null ? right.PlacementId : string.Empty));
    }

    private static void SortEnemyPlacementMarkers(List<MainEscapeEnemyPlacementMarker> markers)
    {
        if (markers == null || markers.Count <= 1)
        {
            return;
        }

        markers.Sort((left, right) => string.CompareOrdinal(
            left != null ? left.PlacementId : string.Empty,
            right != null ? right.PlacementId : string.Empty));
    }

    private Grid ResolveGrid()
    {
        grid ??= GetComponent<Grid>();
        return grid;
    }

    private Tilemap ResolveGroundTilemap()
    {
        groundTilemap = ResolveTilemapReference(groundTilemap, RuntimeSettings.GroundTilemapName, "Tiles_ground", "Ground");
        return groundTilemap;
    }

    private Tilemap ResolveWallTilemap()
    {
        wallTilemap = ResolveTilemapReference(wallTilemap, RuntimeSettings.WallTilemapName, "Tiles_wall", "Walls");
        return wallTilemap;
    }

    private Tilemap ResolveDoorTilemap()
    {
        doorTilemap = ResolveTilemapReference(doorTilemap, RuntimeSettings.DoorTilemapName, "Doors");
        return doorTilemap;
    }

    private Transform ResolveMarker(Transform cachedMarker, params string[] markerNames)
    {
        Transform markersRoot = FindMarkersRoot();
        Transform authoredMarker = FindMarker(markersRoot, markerNames);

        if (authoredMarker != null)
        {
            return authoredMarker;
        }

        return cachedMarker != null && cachedMarker.gameObject.scene == gameObject.scene
            ? cachedMarker
            : null;
    }

    private static Transform ResolveMarkerFromRoot(Transform cachedMarker, Transform markerRoot, params string[] markerNames)
    {
        if (cachedMarker != null)
        {
            return cachedMarker;
        }

        return FindMarker(markerRoot, markerNames);
    }

    private bool TryResolveSceneObjectCell(out Vector3Int cell, params string[] objectNames)
    {
        cell = default;

        if (!TryResolveSceneObjectWorldPosition(out Vector3 worldPosition, objectNames))
        {
            return false;
        }

        Tilemap resolvedGroundTilemap = ResolveGroundTilemap();

        if (resolvedGroundTilemap != null)
        {
            cell = MainEscapeTilemapCellUtility.WorldToCell2D(resolvedGroundTilemap, worldPosition);
            return true;
        }

        Grid resolvedGrid = ResolveGrid();

        if (resolvedGrid != null)
        {
            cell = resolvedGrid.WorldToCell(worldPosition);
            return true;
        }

        return false;
    }

    private bool TryResolveSceneObjectWorldPosition(out Vector3 worldPosition, params string[] objectNames)
    {
        worldPosition = default;

        Transform sceneObject = FindSceneObjectByNames(objectNames);

        if (sceneObject == null)
        {
            return false;
        }

        worldPosition = sceneObject.position;
        return true;
    }

    private Transform FindSceneObjectByNames(params string[] objectNames)
    {
        Scene scene = gameObject.scene;

        if (!scene.IsValid())
        {
            return null;
        }

        string[] searchNames = SanitizeSearchNames(objectNames);

        if (searchNames.Length == 0)
        {
            return null;
        }

        return RSceneReferenceLookup.FindTransformInScene(scene, searchNames);
    }

    private static string[] SanitizeSearchNames(params string[] objectNames)
    {
        if (objectNames == null || objectNames.Length == 0)
        {
            return Array.Empty<string>();
        }

        List<string> names = new(objectNames.Length);

        for (int index = 0; index < objectNames.Length; index++)
        {
            string candidate = objectNames[index];

            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            bool alreadyAdded = false;

            for (int existingIndex = 0; existingIndex < names.Count; existingIndex++)
            {
                if (string.Equals(names[existingIndex], candidate, StringComparison.Ordinal))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                names.Add(candidate);
            }
        }

        return names.Count == 0 ? Array.Empty<string>() : names.ToArray();
    }

    private static string[] SanitizeConfiguredNames(string[] configuredNames, params string[] fallbackNames)
    {
        string[] names = SanitizeSearchNames(configuredNames);
        return names.Length > 0 ? names : SanitizeSearchNames(fallbackNames);
    }

    private static string DefaultIfBlank(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static Vector3Int[] FindNearestDoorCluster(Tilemap groundTilemap, Tilemap doorTilemap, Vector3 anchorWorldPosition, int searchRadius)
    {
        if (groundTilemap == null || doorTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        Vector3Int anchorCell = MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, anchorWorldPosition);
        bool foundSeed = false;
        Vector3Int seedCell = default;
        int closestDistance = int.MaxValue;

        for (int offsetX = -searchRadius; offsetX <= searchRadius; offsetX++)
        {
            for (int offsetY = -searchRadius; offsetY <= searchRadius; offsetY++)
            {
                Vector3Int candidateCell = new(anchorCell.x + offsetX, anchorCell.y + offsetY, anchorCell.z);

                if (!doorTilemap.HasTile(candidateCell))
                {
                    continue;
                }

                int distance = Mathf.Abs(offsetX) + Mathf.Abs(offsetY);

                if (!foundSeed || distance < closestDistance)
                {
                    foundSeed = true;
                    seedCell = candidateCell;
                    closestDistance = distance;
                }
            }
        }

        if (!foundSeed)
        {
            return Array.Empty<Vector3Int>();
        }

        Queue<Vector3Int> frontier = new();
        HashSet<Vector3Int> visited = new();
        List<Vector3Int> cluster = new();

        frontier.Enqueue(seedCell);
        visited.Add(seedCell);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            cluster.Add(current);

            EnqueueDoorNeighbor(current + Vector3Int.right, doorTilemap, visited, frontier);
            EnqueueDoorNeighbor(current + Vector3Int.left, doorTilemap, visited, frontier);
            EnqueueDoorNeighbor(current + Vector3Int.up, doorTilemap, visited, frontier);
            EnqueueDoorNeighbor(current + Vector3Int.down, doorTilemap, visited, frontier);
        }

        return cluster.ToArray();
    }

    private static void EnqueueDoorNeighbor(Vector3Int candidateCell, Tilemap doorTilemap, HashSet<Vector3Int> visited, Queue<Vector3Int> frontier)
    {
        if (visited.Contains(candidateCell) || !doorTilemap.HasTile(candidateCell))
        {
            return;
        }

        visited.Add(candidateCell);
        frontier.Enqueue(candidateCell);
    }

    private MainEscapePatrolSpawnAuthoring ResolvePatrolSpawn()
    {
        Transform patrolRoot = ResolvePatrolSpawnRoot();

        if (IsResolvedHelperValid(patrolSpawn, patrolRoot))
        {
            return patrolSpawn;
        }

        patrolSpawn = ResolvePatrolSpawnAuthoring(patrolRoot);

        if (patrolSpawn != null)
        {
            return patrolSpawn;
        }

        MainEscapePatrolSpawnAuthoring[] patrolAuthorings = GetComponentsInChildren<MainEscapePatrolSpawnAuthoring>(true);

        for (int index = 0; index < patrolAuthorings.Length; index++)
        {
            MainEscapePatrolSpawnAuthoring patrolAuthoring = patrolAuthorings[index];

            if (patrolAuthoring == null || patrolAuthoring.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (IsPatrolSpawnCandidateRoot(patrolAuthoring.transform))
            {
                continue;
            }

            patrolSpawn = patrolAuthoring;
            patrolSpawnRoot = patrolAuthoring.transform;
            return patrolSpawn;
        }

        return patrolSpawn;
    }

    private MainEscapeSentrySpawnAuthoring ResolveSentrySpawns()
    {
        Transform sentryRoot = ResolveSentryGuardsRoot();

        if (IsResolvedHelperValid(sentrySpawns, sentryRoot))
        {
            return sentrySpawns;
        }

        sentrySpawns = ResolveSentrySpawnAuthoring(sentryRoot);
        return sentrySpawns;
    }

    private MainEscapeVentRouteAuthoring ResolveVentRoute()
    {
        Transform ventRoot = ResolveVentRouteRoot();

        if (IsResolvedHelperValid(ventRoute, ventRoot))
        {
            return ventRoute;
        }

        ventRoute = ResolveVentRouteAuthoring(ventRoot);
        return ventRoute;
    }

    private bool IsResolvedHelperValid(Component helperComponent, Transform expectedRoot)
    {
        if (helperComponent == null || expectedRoot == null)
        {
            return false;
        }

        if (helperComponent.gameObject.scene != gameObject.scene)
        {
            return false;
        }

        return helperComponent.transform == expectedRoot;
    }

    private MainEscapeDoorAuthoring[] ResolveDoorAuthorings()
    {
        Transform doorsRoot = ResolveDoorMarkersRoot();

        if (doorsRoot != null)
        {
            doorAuthorings = doorsRoot.GetComponentsInChildren<MainEscapeDoorAuthoring>(true);
            return doorAuthorings;
        }

        return doorAuthorings ?? Array.Empty<MainEscapeDoorAuthoring>();
    }

    private MainEscapeShadowStartleMarker[] ResolveShadowStartleMarkers()
    {
        if (AreResolvedHelpersValid(shadowStartleMarkers))
        {
            return shadowStartleMarkers;
        }

        Transform scareRoot = ResolveScareMarkersRoot();
        shadowStartleMarkers = scareRoot != null
            ? scareRoot.GetComponentsInChildren<MainEscapeShadowStartleMarker>(true)
            : Array.Empty<MainEscapeShadowStartleMarker>();
        return shadowStartleMarkers;
    }

    private MainEscapePropBlockerAuthoring[] ResolvePropBlockers()
    {
        if (AreResolvedHelpersValid(propBlockers))
        {
            return propBlockers;
        }

        Transform propRoot = ResolveCoverPropsRoot();
        propBlockers = propRoot != null
            ? propRoot.GetComponentsInChildren<MainEscapePropBlockerAuthoring>(true)
            : GetComponentsInChildren<MainEscapePropBlockerAuthoring>(true);
        return propBlockers;
    }

    private MainEscapeMovementBlockerAuthoring[] ResolveMovementBlockers()
    {
        if (AreResolvedHelpersValid(movementBlockers))
        {
            return movementBlockers;
        }

        Transform blockersRoot = ResolveMovementBlockersRoot();
        movementBlockers = blockersRoot != null
            ? blockersRoot.GetComponentsInChildren<MainEscapeMovementBlockerAuthoring>(true)
            : GetComponentsInChildren<MainEscapeMovementBlockerAuthoring>(true);
        return movementBlockers;
    }

    private Transform FindMarkersRoot()
    {
        if (authoringMarkersRoot == null || authoringMarkersRoot.gameObject.scene != gameObject.scene)
        {
            authoringMarkersRoot = FindChildRoot(transform, AuthoringMarkersRootName);
        }

        return authoringMarkersRoot;
    }

    private void SyncInteractivePickupsToMarkers()
    {
        Transform interactiveRoot = ResolveInteractivePropsRoot();

        if (interactiveRoot == null)
        {
            return;
        }

        SyncInteractivePickup(interactiveRoot, RuntimeSettings.AuthoredBatteryPickupName, ResolveBatteryMarker());
        SyncInteractivePickup(interactiveRoot, RuntimeSettings.AuthoredGlassBottlePickupName, ResolveGlassPanelMarker());
    }

    private static void SyncInteractivePickup(Transform interactivePropsRoot, string pickupName, Transform marker)
    {
        if (interactivePropsRoot == null || marker == null || string.IsNullOrWhiteSpace(pickupName))
        {
            return;
        }

        Transform pickup = FindChildRoot(interactivePropsRoot, pickupName);

        if (pickup == null)
        {
            return;
        }

        pickup.position = marker.position;
    }

    private static Transform FindMarker(Transform markersRoot, params string[] markerNames)
    {
        if (markersRoot == null || markerNames == null)
        {
            return null;
        }

        for (int index = 0; index < markerNames.Length; index++)
        {
            string markerName = markerNames[index];

            if (string.IsNullOrWhiteSpace(markerName))
            {
                continue;
            }

            Transform marker = FindChildRoot(markersRoot, markerName);

            if (marker != null)
            {
                return marker;
            }
        }

        return null;
    }

    private static MainEscapeSentrySpawnAuthoring ResolveSentrySpawnAuthoring(Transform sentryRoot)
    {
        if (sentryRoot == null)
        {
            return null;
        }

        return sentryRoot.GetComponent<MainEscapeSentrySpawnAuthoring>();
    }

    private static MainEscapePatrolSpawnAuthoring ResolvePatrolSpawnAuthoring(Transform patrolRoot)
    {
        if (patrolRoot == null)
        {
            return null;
        }

        return patrolRoot.GetComponent<MainEscapePatrolSpawnAuthoring>();
    }

    private static MainEscapeVentRouteAuthoring ResolveVentRouteAuthoring(Transform ventRouteRoot)
    {
        if (ventRouteRoot == null)
        {
            return null;
        }

        return ventRouteRoot.GetComponent<MainEscapeVentRouteAuthoring>();
    }

    private Tilemap FindTilemap(params string[] objectNames)
    {
        Transform tilemapTransform = FindNamedTransform(transform, objectNames);
        return tilemapTransform != null ? tilemapTransform.GetComponent<Tilemap>() : null;
    }

    private Tilemap ResolveTilemapReference(Tilemap cachedTilemap, params string[] objectNames)
    {
        if (cachedTilemap != null
            && cachedTilemap.gameObject.activeInHierarchy
            && MatchesAnyName(cachedTilemap.name, objectNames))
        {
            return cachedTilemap;
        }

        return FindTilemap(objectNames);
    }

    private Transform ResolveInteractivePropsRoot()
    {
        interactivePropsRoot ??= FindChildRoot(transform, InteractivePropsRootName);
        return interactivePropsRoot;
    }

    private Transform ResolvePickupMarkersRoot()
    {
        if (!UsesLegacyPickupMarkerFallback())
        {
            pickupMarkersRoot = null;
            return null;
        }

        if (pickupMarkersRoot == null || pickupMarkersRoot.gameObject.scene != gameObject.scene)
        {
            pickupMarkersRoot = FindChildRoot(transform, PickupMarkersRootName);
        }

        return pickupMarkersRoot;
    }

    private Transform ResolveItemPlacementMarkersRoot()
    {
        if (itemPlacementMarkersRoot == null || itemPlacementMarkersRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            itemPlacementMarkersRoot = markersRoot != null
                ? FindChildRoot(markersRoot, ItemPlacementMarkersRootName)
                : null;
        }

        return itemPlacementMarkersRoot;
    }

    private Transform ResolveKeyPlacementMarkersRoot()
    {
        if (keyPlacementMarkersRoot == null || keyPlacementMarkersRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            keyPlacementMarkersRoot = markersRoot != null
                ? FindChildRoot(markersRoot, KeyPlacementMarkersRootName)
                : null;
        }

        return keyPlacementMarkersRoot;
    }

    private Transform ResolveEnemyPlacementMarkersRoot()
    {
        if (enemyPlacementMarkersRoot == null || enemyPlacementMarkersRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            enemyPlacementMarkersRoot = markersRoot != null
                ? FindChildRoot(markersRoot, EnemyPlacementMarkersRootName)
                : null;
        }

        return enemyPlacementMarkersRoot;
    }

    private Transform ResolveChaserPlacementMarkersRoot()
    {
        if (chaserPlacementMarkersRoot == null || chaserPlacementMarkersRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            chaserPlacementMarkersRoot = markersRoot != null
                ? FindChildRoot(markersRoot, ChaserPlacementMarkersRootName)
                : null;
        }

        return chaserPlacementMarkersRoot;
    }

    private Transform ResolveKeyPickupMarker()
    {
        keyPickupMarker = ResolveLegacyPickupMarker(ResolvePickupMarkersRoot(), KeyPickupMarkerSearchNames);
        return keyPickupMarker;
    }

    private Transform ResolveBatteryMarker()
    {
        batteryMarker = ResolveLegacyPickupMarker(ResolvePickupMarkersRoot(), BatteryMarkerNames);
        return batteryMarker;
    }

    private Transform ResolveGlassPanelMarker()
    {
        glassPanelMarker = ResolveLegacyPickupMarker(ResolvePickupMarkersRoot(), GlassBottleMarkerNames);
        return glassPanelMarker;
    }

    private bool UsesLegacyPickupMarkerFallback()
    {
        return GetSupportItemPlacementMarkers().Length == 0
            && GetKeyPlacementMarkers().Length == 0;
    }

    private static Transform ResolveLegacyPickupMarker(Transform root, params string[] markerNames)
    {
        return root != null ? FindMarker(root, markerNames) : null;
    }

    private Transform ResolveGoalVisualsRoot()
    {
        if (goalVisualsRoot == null || goalVisualsRoot.gameObject.scene != gameObject.scene)
        {
            Transform interactiveRoot = ResolveInteractivePropsRoot();
            goalVisualsRoot = interactiveRoot != null
                ? FindChildRoot(interactiveRoot, GoalVisualsRootName)
                : null;
        }

        return goalVisualsRoot;
    }

    private Transform ResolveDangerMarkersRoot()
    {
        if (dangerMarkersRoot == null || dangerMarkersRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            dangerMarkersRoot = markersRoot != null
                ? FindChildRoot(markersRoot, DangerMarkersRootName)
                : null;
        }

        return dangerMarkersRoot;
    }

    private Transform ResolveScareMarkersRoot()
    {
        if (scareMarkersRoot == null || scareMarkersRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            scareMarkersRoot = markersRoot != null
                ? FindChildRoot(markersRoot, ScareMarkersRootName)
                : null;
        }

        return scareMarkersRoot;
    }

    private Transform ResolveDoorMarkersRoot()
    {
        if (doorMarkersRoot == null || doorMarkersRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            doorMarkersRoot = markersRoot != null
                ? FindChildRoot(markersRoot, DoorMarkersRootName)
                : null;
        }

        return doorMarkersRoot;
    }

    private Transform ResolveSentryGuardsRoot()
    {
        if (sentryGuardsRoot == null || sentryGuardsRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            sentryGuardsRoot = markersRoot != null
                ? FindChildRoot(markersRoot, SentryGuardsRootName)
                : null;
        }

        return sentryGuardsRoot;
    }

    private Transform ResolveSentrySpawnCandidatesRoot()
    {
        if (sentrySpawnCandidatesRoot == null || sentrySpawnCandidatesRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            sentrySpawnCandidatesRoot = markersRoot != null
                ? FindChildRoot(markersRoot, SentrySpawnCandidatesRootName)
                : null;
        }

        return sentrySpawnCandidatesRoot;
    }

    private Transform ResolvePatrolSpawnRoot()
    {
        bool shouldResolvePatrolRoot = patrolSpawnRoot == null
            || patrolSpawnRoot.gameObject.scene != gameObject.scene
            || !IsExplicitPatrolSpawnRoot(patrolSpawnRoot);

        if (shouldResolvePatrolRoot)
        {
            Transform markersRoot = FindMarkersRoot();
            patrolSpawnRoot = FindPatrolSpawnRoot(markersRoot);

            if (patrolSpawnRoot == null)
            {
                MainEscapePatrolSpawnAuthoring fallbackPatrolSpawn = patrolSpawn;

                if (fallbackPatrolSpawn == null || fallbackPatrolSpawn.gameObject.scene != gameObject.scene)
                {
                    MainEscapePatrolSpawnAuthoring[] patrolAuthorings = GetComponentsInChildren<MainEscapePatrolSpawnAuthoring>(true);

                    for (int index = 0; index < patrolAuthorings.Length; index++)
                    {
                        MainEscapePatrolSpawnAuthoring patrolAuthoring = patrolAuthorings[index];

                        if (patrolAuthoring == null || patrolAuthoring.gameObject.scene != gameObject.scene)
                        {
                            continue;
                        }

                        fallbackPatrolSpawn = patrolAuthoring;
                        break;
                    }
                }

                patrolSpawnRoot = fallbackPatrolSpawn != null ? fallbackPatrolSpawn.transform : null;
            }
        }

        return patrolSpawnRoot;
    }

    private Transform ResolvePatrolSpawnCandidatesRoot()
    {
        if (patrolSpawnCandidatesRoot == null || patrolSpawnCandidatesRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            patrolSpawnCandidatesRoot = markersRoot != null
                ? FindChildRoot(markersRoot, PatrolSpawnCandidatesRootName)
                : null;
        }

        return patrolSpawnCandidatesRoot;
    }

    private Transform FindPatrolSpawnRoot(Transform markersRoot)
    {
        if (markersRoot == null)
        {
            return null;
        }

        return FindChildRoot(markersRoot, PatrolSpawnRootName)
            ?? FindDescendantByName(markersRoot, PatrolSpawnRootName)
            ?? FindChildRoot(markersRoot, PatrolSpawnLegacyRootName)
            ?? FindDescendantByName(markersRoot, PatrolSpawnLegacyRootName);
    }

    private bool IsExplicitPatrolSpawnRoot(Transform patrolRoot)
    {
        return patrolRoot != null
            && string.Equals(patrolRoot.name, PatrolSpawnRootName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPatrolSpawnCandidateRoot(Transform patrolRoot)
    {
        return patrolRoot != null
            && string.Equals(patrolRoot.name, PatrolSpawnCandidatesRootName, StringComparison.OrdinalIgnoreCase);
    }

    private Transform ResolveVentRouteRoot()
    {
        if (ventRouteRoot == null || ventRouteRoot.gameObject.scene != gameObject.scene)
        {
            Transform markersRoot = FindMarkersRoot();
            ventRouteRoot = markersRoot != null
                ? FindChildRoot(markersRoot, VentRouteRootName)
                : null;
        }

        return ventRouteRoot;
    }

    private void DisableNamedVentVisualRoots(Transform markersRoot)
    {
        if (markersRoot == null)
        {
            return;
        }

        for (int index = 0; index < markersRoot.childCount; index++)
        {
            Transform child = markersRoot.GetChild(index);

            if (child == null || !IsVentAuthoringVisualRootName(child.name))
            {
                continue;
            }

            DisableRendererVisuals(child);
        }
    }

    private void DisableVentAuthoringComponentRenderers()
    {
        DisableComponentRenderers(GetComponentsInChildren<MainEscapeVentNodeAuthoring>(true));
        DisableComponentRenderers(GetComponentsInChildren<MainEscapeVentLinkAuthoring>(true));
        DisableComponentRenderers(GetComponentsInChildren<MainEscapeVentExitAuthoring>(true));

        MainEscapeVentNetworkAuthoring[] networks = GetComponentsInChildren<MainEscapeVentNetworkAuthoring>(true);

        for (int index = 0; index < networks.Length; index++)
        {
            MainEscapeVentNetworkAuthoring network = networks[index];

            if (network == null || network.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            DisableRendererVisuals(network.transform);
        }
    }

    private void DisableComponentRenderers<T>(T[] components) where T : Component
    {
        if (components == null)
        {
            return;
        }

        for (int index = 0; index < components.Length; index++)
        {
            T component = components[index];

            if (component == null || component.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            DisableRendererVisuals(component.transform);
        }
    }

    private static void DisableRendererVisuals(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = false;
        }
    }

    private static bool IsVentAuthoringVisualRootName(string rootName)
    {
        return !string.IsNullOrWhiteSpace(rootName)
            && rootName.StartsWith("VentRouteVisualLines", StringComparison.OrdinalIgnoreCase);
    }

    private Transform ResolveCoverPropsRoot()
    {
        if (coverPropsRoot == null || coverPropsRoot.gameObject.scene != gameObject.scene)
        {
            coverPropsRoot = FindNamedTransform(transform, CoverPropsRootName, "CoverProps");
        }

        return coverPropsRoot;
    }

    private Transform ResolveMovementBlockersRoot()
    {
        if (movementBlockersRoot == null || movementBlockersRoot.gameObject.scene != gameObject.scene)
        {
            movementBlockersRoot = FindNamedTransform(transform, MovementBlockersRootName, "MovementBlockers");
        }

        return movementBlockersRoot;
    }

    private Transform ResolveOverlayRoot(string rootName)
    {
        string fallbackName = GetOverlayFallbackName(rootName);
        Transform overlayRoot = FindNamedTransform(transform, GameplayOverlayRootName, "GameplayOverlay");
        Transform resolvedOverlayRoot = overlayRoot != null
            ? FindNamedTransform(overlayRoot, rootName, fallbackName)
            : null;
        return resolvedOverlayRoot ?? FindNamedTransform(transform, rootName, fallbackName);
    }

    private Tilemap ResolveOverlayTilemap(string rootName)
    {
        Transform overlayRoot = ResolveOverlayRoot(rootName);
        return overlayRoot != null ? overlayRoot.GetComponent<Tilemap>() : null;
    }

    private static void RegisterOverlayCells(Action<IEnumerable<Vector3Int>> registerCells, Tilemap overlayTilemap)
    {
        if (registerCells == null || overlayTilemap == null)
        {
            return;
        }

        List<Vector3Int> occupiedCells = new();
        BoundsInt bounds = overlayTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (overlayTilemap.HasTile(cellPosition))
            {
                occupiedCells.Add(cellPosition);
            }
        }

        if (occupiedCells.Count > 0)
        {
            registerCells(occupiedCells);
        }
    }

    private static void RegisterPropBlockerCells(
        Action<IEnumerable<Vector3Int>> registerVisionAndMovementCells,
        Action<IEnumerable<Vector3Int>> registerMovementOnlyCells,
        MainEscapePropBlockerAuthoring[] blockers,
        Tilemap groundTilemap)
    {
        if ((registerVisionAndMovementCells == null && registerMovementOnlyCells == null)
            || blockers == null
            || blockers.Length == 0
            || groundTilemap == null)
        {
            return;
        }

        HashSet<Vector3Int> solidOccupiedCells = new();
        HashSet<Vector3Int> movementOnlyOccupiedCells = new();

        for (int index = 0; index < blockers.Length; index++)
        {
            MainEscapePropBlockerAuthoring blocker = blockers[index];

            if (blocker == null)
            {
                continue;
            }

            Vector3Int[] cells = blocker.GetOccupiedCells(groundTilemap);

            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                if (blocker.GetComponent<MainEscapeSolidBlockerAuthoring>() != null)
                {
                    solidOccupiedCells.Add(cells[cellIndex]);
                }
                else
                {
                    movementOnlyOccupiedCells.Add(cells[cellIndex]);
                }
            }
        }

        if (solidOccupiedCells.Count > 0 && registerVisionAndMovementCells != null)
        {
            registerVisionAndMovementCells(solidOccupiedCells);
        }

        if (movementOnlyOccupiedCells.Count > 0 && registerMovementOnlyCells != null)
        {
            registerMovementOnlyCells(movementOnlyOccupiedCells);
        }
    }

    private static void RegisterMovementBlockerCells(
        Action<IEnumerable<Vector3Int>> registerCells,
        MainEscapeMovementBlockerAuthoring[] blockers,
        Tilemap groundTilemap)
    {
        if (registerCells == null || blockers == null || blockers.Length == 0 || groundTilemap == null)
        {
            return;
        }

        HashSet<Vector3Int> occupiedCells = new();

        for (int index = 0; index < blockers.Length; index++)
        {
            MainEscapeMovementBlockerAuthoring blocker = blockers[index];

            if (blocker == null)
            {
                continue;
            }

            Vector3Int[] cells = blocker.GetOccupiedCells(groundTilemap);

            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                occupiedCells.Add(cells[cellIndex]);
            }
        }

        if (occupiedCells.Count > 0)
        {
            registerCells(occupiedCells);
        }
    }

    private bool AreResolvedHelpersValid(Component[] helpers)
    {
        if (helpers == null || helpers.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < helpers.Length; index++)
        {
            if (helpers[index] == null || helpers[index].gameObject.scene != gameObject.scene)
            {
                return false;
            }
        }

        return true;
    }

    private static Transform FindChildRoot(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);

            if (child.name == targetName)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);

            if (child.name == targetName)
            {
                return child;
            }

            Transform descendant = FindDescendantByName(child, targetName);

            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static Transform FindNamedTransform(Transform root, params string[] candidateNames)
    {
        string[] names = SanitizeSearchNames(candidateNames);

        if (root == null || names.Length == 0)
        {
            return null;
        }

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            Transform directChild = FindChildRoot(root, names[nameIndex]);

            if (directChild != null && directChild.gameObject.activeInHierarchy)
            {
                return directChild;
            }
        }

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            Transform descendant = FindDescendantByName(root, names[nameIndex]);

            if (descendant != null && descendant.gameObject.activeInHierarchy)
            {
                return descendant;
            }
        }

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            Transform directChild = FindChildRoot(root, names[nameIndex]);

            if (directChild != null)
            {
                return directChild;
            }
        }

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            Transform descendant = FindDescendantByName(root, names[nameIndex]);

            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool MatchesAnyName(string candidateName, params string[] objectNames)
    {
        string[] names = SanitizeSearchNames(objectNames);

        for (int index = 0; index < names.Length; index++)
        {
            if (string.Equals(candidateName, names[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetOverlayFallbackName(string rootName)
    {
        if (string.IsNullOrWhiteSpace(rootName))
        {
            return null;
        }

        if (string.Equals(rootName, "Tiles_movenonlywall", StringComparison.Ordinal))
        {
            return "MoveOnly";
        }

        if (string.Equals(rootName, "MoveOnly", StringComparison.Ordinal))
        {
            return "Tiles_movenonlywall";
        }

        if (string.Equals(rootName, "BlockAll", StringComparison.Ordinal))
        {
            return null;
        }

        return null;
    }

    private static Transform[] CollectChildTransforms(Transform root)
    {
        if (root == null || root.childCount == 0)
        {
            return Array.Empty<Transform>();
        }

        Transform[] children = new Transform[root.childCount];

        for (int index = 0; index < root.childCount; index++)
        {
            children[index] = root.GetChild(index);
        }

        return children;
    }

    private static Transform[] ResolveDangerMarkerTransforms(MainEscapeDangerPlacementMarker[] markers)
    {
        if (markers == null || markers.Length == 0)
        {
            return Array.Empty<Transform>();
        }

        List<Transform> transforms = new(markers.Length);

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeDangerPlacementMarker marker = markers[index];

            if (marker != null)
            {
                transforms.Add(marker.transform);
            }
        }

        return transforms.ToArray();
    }
}

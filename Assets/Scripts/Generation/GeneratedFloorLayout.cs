/*
 * File Role:
 * Stores the final generated floor metadata that the rest of the game reads at runtime.
 *
 * Runtime Use:
 * Carries key cells, exit cells, door groups, rooms, routes, danger cells, and cover prop placements.
 *
 * Study Notes:
 * This file is the contract that lets map generation stay separate from gameplay systems.
 */

using System;
using UnityEngine;

public enum GeneratedZoneType
{
    Start,
    OpenOffice,
    Facility,
    Security,
    Exit,
    Utility
}

public enum GeneratedRoomType
{
    Start,
    Explore,
    DangerCorridor,
    Key,
    Safe,
    PreExit,
    Utility,
    Exit
}

public enum GeneratedRouteType
{
    MainRoute,
    BranchRoute,
    GateApproach
}

public enum CoverPropType
{
    Planter,
    CrateStack
}

[Serializable]
public struct GeneratedZoneData
{
    [SerializeField] private GeneratedZoneType zoneType;
    [SerializeField] private RectInt bounds;
    [SerializeField] private Vector3Int centerCell;
    [SerializeField] private Vector3Int[] doorCells;

    public GeneratedZoneData(GeneratedZoneType configuredZoneType, RectInt configuredBounds, Vector3Int configuredCenterCell, Vector3Int[] configuredDoorCells)
    {
        zoneType = configuredZoneType;
        bounds = configuredBounds;
        centerCell = configuredCenterCell;
        doorCells = configuredDoorCells ?? Array.Empty<Vector3Int>();
    }

    public GeneratedZoneType ZoneType => zoneType;
    public RectInt Bounds => bounds;
    public Vector3Int CenterCell => centerCell;
    public Vector3Int[] DoorCells => doorCells ?? Array.Empty<Vector3Int>();
}

[Serializable]
public struct GeneratedRoomData
{
    [SerializeField] private int roomId;
    [SerializeField] private GeneratedRoomType roomType;
    [SerializeField] private RectInt bounds;
    [SerializeField] private Vector3Int centerCell;
    [SerializeField] private Vector3Int[] doorCells;
    [SerializeField] private int[] connectedRoomIds;

    public GeneratedRoomData(
        int configuredRoomId,
        GeneratedRoomType configuredRoomType,
        RectInt configuredBounds,
        Vector3Int configuredCenterCell,
        Vector3Int[] configuredDoorCells,
        int[] configuredConnectedRoomIds)
    {
        roomId = configuredRoomId;
        roomType = configuredRoomType;
        bounds = configuredBounds;
        centerCell = configuredCenterCell;
        doorCells = configuredDoorCells ?? Array.Empty<Vector3Int>();
        connectedRoomIds = configuredConnectedRoomIds ?? Array.Empty<int>();
    }

    public int RoomId => roomId;
    public GeneratedRoomType RoomType => roomType;
    public RectInt Bounds => bounds;
    public Vector3Int CenterCell => centerCell;
    public Vector3Int[] DoorCells => doorCells ?? Array.Empty<Vector3Int>();
    public int[] ConnectedRoomIds => connectedRoomIds ?? Array.Empty<int>();
}

[Serializable]
public struct GeneratedRouteData
{
    [SerializeField] private int fromRoomId;
    [SerializeField] private int toRoomId;
    [SerializeField] private GeneratedRouteType routeType;
    [SerializeField] private Vector3Int[] cells;

    public GeneratedRouteData(int configuredFromRoomId, int configuredToRoomId, GeneratedRouteType configuredRouteType, Vector3Int[] configuredCells)
    {
        fromRoomId = configuredFromRoomId;
        toRoomId = configuredToRoomId;
        routeType = configuredRouteType;
        cells = configuredCells ?? Array.Empty<Vector3Int>();
    }

    public int FromRoomId => fromRoomId;
    public int ToRoomId => toRoomId;
    public GeneratedRouteType RouteType => routeType;
    public Vector3Int[] Cells => cells ?? Array.Empty<Vector3Int>();
}

[Serializable]
public struct GeneratedDoorGroupData
{
    [SerializeField] private int doorGroupId;
    [SerializeField] private bool requiresKey;
    [SerializeField] private Vector3Int[] cells;
    [SerializeField] private int[] connectedRoomIds;

    public GeneratedDoorGroupData(int configuredDoorGroupId, bool configuredRequiresKey, Vector3Int[] configuredCells, int[] configuredConnectedRoomIds)
    {
        doorGroupId = configuredDoorGroupId;
        requiresKey = configuredRequiresKey;
        cells = configuredCells ?? Array.Empty<Vector3Int>();
        connectedRoomIds = configuredConnectedRoomIds ?? Array.Empty<int>();
    }

    public int DoorGroupId => doorGroupId;
    public bool RequiresKey => requiresKey;
    public Vector3Int[] Cells => cells ?? Array.Empty<Vector3Int>();
    public int[] ConnectedRoomIds => connectedRoomIds ?? Array.Empty<int>();
}

[Serializable]
public struct CoverPropPlacement
{
    [SerializeField] private CoverPropType propType;
    [SerializeField] private GeneratedZoneType zoneType;
    [SerializeField] private Vector3Int originCell;
    [SerializeField] private Vector2Int size;
    [SerializeField] private Vector3Int[] occupiedCells;

    public CoverPropPlacement(
        CoverPropType configuredPropType,
        GeneratedZoneType configuredZoneType,
        Vector3Int configuredOriginCell,
        Vector2Int configuredSize,
        Vector3Int[] configuredOccupiedCells)
    {
        propType = configuredPropType;
        zoneType = configuredZoneType;
        originCell = configuredOriginCell;
        size = configuredSize;
        occupiedCells = configuredOccupiedCells ?? Array.Empty<Vector3Int>();
    }

    public CoverPropType PropType => propType;
    public GeneratedZoneType ZoneType => zoneType;
    public Vector3Int OriginCell => originCell;
    public Vector2Int Size => size;
    public Vector3Int[] OccupiedCells => occupiedCells ?? Array.Empty<Vector3Int>();
}

[DisallowMultipleComponent]
public sealed class GeneratedFloorLayout : MonoBehaviour
{
    [SerializeField] private Vector3Int playerStartCell;
    [SerializeField] private Vector3Int keyCell;
    [SerializeField] private Vector3Int batteryCell;
    [SerializeField] private Vector3Int exitCell;
    [SerializeField] private Vector3Int patrolSpawnCell;
    [SerializeField] private Vector3Int glassPanelCell;
    [SerializeField] private Vector3Int safeRoomCell;
    [SerializeField] private Vector3Int[] mainDoorCells = Array.Empty<Vector3Int>();
    [SerializeField] private Vector3Int[] dangerCells = Array.Empty<Vector3Int>();
    [SerializeField] private GeneratedZoneData[] zones = Array.Empty<GeneratedZoneData>();
    [SerializeField] private GeneratedRoomData[] rooms = Array.Empty<GeneratedRoomData>();
    [SerializeField] private GeneratedRouteData[] routes = Array.Empty<GeneratedRouteData>();
    [SerializeField] private GeneratedDoorGroupData[] doorGroups = Array.Empty<GeneratedDoorGroupData>();
    [SerializeField] private CoverPropPlacement[] coverProps = Array.Empty<CoverPropPlacement>();

    public Vector3Int PlayerStartCell => playerStartCell;
    public Vector3Int KeyCell => keyCell;
    public Vector3Int BatteryCell => batteryCell;
    public Vector3Int ExitCell => exitCell;
    public Vector3Int PatrolSpawnCell => patrolSpawnCell;
    public Vector3Int GlassPanelCell => glassPanelCell;
    public Vector3Int SafeRoomCell => safeRoomCell;
    public Vector3Int[] MainDoorCells => mainDoorCells ?? Array.Empty<Vector3Int>();
    public Vector3Int[] DangerCells => dangerCells ?? Array.Empty<Vector3Int>();
    public GeneratedZoneData[] Zones => zones ?? Array.Empty<GeneratedZoneData>();
    public GeneratedRoomData[] Rooms => rooms ?? Array.Empty<GeneratedRoomData>();
    public GeneratedRouteData[] Routes => routes ?? Array.Empty<GeneratedRouteData>();
    public GeneratedDoorGroupData[] DoorGroups => doorGroups ?? Array.Empty<GeneratedDoorGroupData>();
    public CoverPropPlacement[] CoverProps => coverProps ?? Array.Empty<CoverPropPlacement>();

    public void Configure(
        Vector3Int configuredPlayerStartCell,
        Vector3Int configuredKeyCell,
        Vector3Int configuredBatteryCell,
        Vector3Int configuredExitCell,
        Vector3Int configuredPatrolSpawnCell,
        Vector3Int configuredGlassPanelCell,
        Vector3Int configuredSafeRoomCell,
        Vector3Int[] configuredMainDoorCells,
        Vector3Int[] configuredDangerCells,
        GeneratedZoneData[] configuredZones,
        GeneratedRoomData[] configuredRooms,
        GeneratedRouteData[] configuredRoutes,
        GeneratedDoorGroupData[] configuredDoorGroups,
        CoverPropPlacement[] configuredCoverProps)
    {
        playerStartCell = configuredPlayerStartCell;
        keyCell = configuredKeyCell;
        batteryCell = configuredBatteryCell;
        exitCell = configuredExitCell;
        patrolSpawnCell = configuredPatrolSpawnCell;
        glassPanelCell = configuredGlassPanelCell;
        safeRoomCell = configuredSafeRoomCell;
        mainDoorCells = configuredMainDoorCells ?? Array.Empty<Vector3Int>();
        dangerCells = configuredDangerCells ?? Array.Empty<Vector3Int>();
        zones = configuredZones ?? Array.Empty<GeneratedZoneData>();
        rooms = configuredRooms ?? Array.Empty<GeneratedRoomData>();
        routes = configuredRoutes ?? Array.Empty<GeneratedRouteData>();
        doorGroups = configuredDoorGroups ?? Array.Empty<GeneratedDoorGroupData>();
        coverProps = configuredCoverProps ?? Array.Empty<CoverPropPlacement>();
    }
}


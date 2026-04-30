/*
 * File Role:
 * Owns scene bootstrap and map creation for prototype and office test scenes.
 *
 * Runtime Use:
 * Chooses the active generation mode, builds tilemaps, registers doors and props, and positions scene actors.
 *
 * Study Notes:
 * This is one of the main files to study because it shows how generation data becomes a playable scene.
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static partial class Batch2TestRoomBootstrap
{
    // This file does two jobs:
    // 1. Generate or load a layout description.
    // 2. Turn that description into real tilemaps, props, doors, and scene actors.
    private const int MinRoomCount = 6;
    private const int MaxRoomCount = 10;
    private const int CorridorWidth = 2;
    private const int RoomSeparationPadding = 3;
    private const float WorldBoundsPadding = 11f;
    private static readonly Vector3Int[] CardinalOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    };
    private static Sprite tileSprite;
    private static Sprite solidBackdropSprite;
    private static TileBase groundTile;
    private static TileBase wallTile;
    private static TileBase doorTile;

    private readonly struct RoomData
    {
        public RoomData(RectInt bounds)
        {
            Bounds = bounds;
        }

        public RectInt Bounds { get; }
        public Vector3Int CenterCell => new(Bounds.xMin + (Bounds.width / 2), Bounds.yMin + (Bounds.height / 2), 0);
        public Vector3Int LeftEdgeCenterCell => new(Bounds.xMin, Bounds.yMin + (Bounds.height / 2), 0);
        public Vector3Int RightEdgeCenterCell => new(Bounds.xMax - 1, Bounds.yMin + (Bounds.height / 2), 0);
    }

    private readonly struct ZonePlan
    {
        public ZonePlan(RoomData room, GeneratedZoneType zoneType)
        {
            Room = room;
            ZoneType = zoneType;
        }

        public RoomData Room { get; }
        public GeneratedZoneType ZoneType { get; }
    }

    private readonly struct OfficeCrossCorridorCandidate
    {
        public OfficeCrossCorridorCandidate(int x, int minY, int maxY, int corridorBottomY)
        {
            X = x;
            MinY = minY;
            MaxY = maxY;
            CorridorBottomY = corridorBottomY;
        }

        public int X { get; }
        public int MinY { get; }
        public int MaxY { get; }
        public int CorridorBottomY { get; }
    }

    private readonly struct OfficeCentralBandSegment
    {
        public OfficeCentralBandSegment(int startX, int endX, int[] laneBottoms, bool[] activeLanes)
        {
            StartX = startX;
            EndX = endX;
            LaneBottoms = laneBottoms ?? Array.Empty<int>();
            ActiveLanes = activeLanes ?? Array.Empty<bool>();
        }

        public int StartX { get; }
        public int EndX { get; }
        public int[] LaneBottoms { get; }
        public bool[] ActiveLanes { get; }
    }

    private readonly struct OfficeCrossCorridorSpan
    {
        public OfficeCrossCorridorSpan(int leftX, int rightX, int minY, int maxY)
        {
            LeftX = leftX;
            RightX = rightX;
            MinY = minY;
            MaxY = maxY;
        }

        public int LeftX { get; }
        public int RightX { get; }
        public int MinY { get; }
        public int MaxY { get; }
    }

    private readonly struct OfficeCentralBandBlocker
    {
        public OfficeCentralBandBlocker(int minX, int maxX, bool actsAsCorridor)
        {
            MinX = minX;
            MaxX = maxX;
            ActsAsCorridor = actsAsCorridor;
        }

        public int MinX { get; }
        public int MaxX { get; }
        public bool ActsAsCorridor { get; }
    }

    private readonly struct OfficeCentralRoomColumn
    {
        public OfficeCentralRoomColumn(int minX, int maxX, bool hasLeftCorridor, bool hasRightCorridor)
        {
            MinX = minX;
            MaxX = maxX;
            HasLeftCorridor = hasLeftCorridor;
            HasRightCorridor = hasRightCorridor;
        }

        public int MinX { get; }
        public int MaxX { get; }
        public bool HasLeftCorridor { get; }
        public bool HasRightCorridor { get; }
        public int Width => (MaxX - MinX) + 1;
    }

    private readonly struct OfficeCentralRoomRow
    {
        public OfficeCentralRoomRow(int minY, int maxY, bool hasBottomCorridor, bool hasTopCorridor)
        {
            MinY = minY;
            MaxY = maxY;
            HasBottomCorridor = hasBottomCorridor;
            HasTopCorridor = hasTopCorridor;
        }

        public int MinY { get; }
        public int MaxY { get; }
        public bool HasBottomCorridor { get; }
        public bool HasTopCorridor { get; }
        public int Height => (MaxY - MinY) + 1;
    }

    private readonly struct OfficeLayoutConfig
    {
        public OfficeLayoutConfig(
            int targetWidthCells,
            int targetHeightCells,
            int roomCountPerRow,
            int corridorWidth,
            int segmentGap,
            int minRoomWidth,
            int maxRoomWidth,
            int minRoomHeight,
            int maxRoomHeight)
        {
            TargetWidthCells = targetWidthCells;
            TargetHeightCells = targetHeightCells;
            RoomCountPerRow = roomCountPerRow;
            CorridorWidth = corridorWidth;
            SegmentGap = segmentGap;
            MinRoomWidth = minRoomWidth;
            MaxRoomWidth = maxRoomWidth;
            MinRoomHeight = minRoomHeight;
            MaxRoomHeight = maxRoomHeight;
        }

        public int TargetWidthCells { get; }
        public int TargetHeightCells { get; }
        public int RoomCountPerRow { get; }
        public int CorridorWidth { get; }
        public int SegmentGap { get; }
        public int MinRoomWidth { get; }
        public int MaxRoomWidth { get; }
        public int MinRoomHeight { get; }
        public int MaxRoomHeight { get; }
        public int SegmentCount => 3;
        public int InternalDividerCount => Mathf.Max(0, RoomCountPerRow - SegmentCount);
        public int InterSegmentSpan => Mathf.Max(0, (SegmentCount - 1) * (SegmentGap - 1));
        public int TotalRoomWidth => Mathf.Clamp(
            TargetWidthCells - InternalDividerCount - InterSegmentSpan - 2,
            RoomCountPerRow * MinRoomWidth,
            RoomCountPerRow * MaxRoomWidth);
        public int OccupiedWidthCells => TotalRoomWidth + InternalDividerCount + InterSegmentSpan;
        public int StartX => -Mathf.RoundToInt(OccupiedWidthCells / 2f);
        public int CorridorBottomMinY => -Mathf.Max(5, TargetHeightCells / 7);
        public int CorridorBottomMaxY => Mathf.Max(5, TargetHeightCells / 7);
    }

    private readonly struct PropCandidate
    {
        public PropCandidate(CoverPropType propType, GeneratedZoneType zoneType, Vector3Int originCell, Vector2Int size)
        {
            PropType = propType;
            ZoneType = zoneType;
            OriginCell = originCell;
            Size = size;
        }

        public CoverPropType PropType { get; }
        public GeneratedZoneType ZoneType { get; }
        public Vector3Int OriginCell { get; }
        public Vector2Int Size { get; }

        public Vector3Int[] GetOccupiedCells()
        {
            Vector3Int[] cells = new Vector3Int[Size.x * Size.y];
            int index = 0;

            for (int x = 0; x < Size.x; x++)
            {
                for (int y = 0; y < Size.y; y++)
                {
                    cells[index++] = new Vector3Int(OriginCell.x + x, OriginCell.y + y, 0);
                }
            }

            return cells;
        }

        public CoverPropPlacement ToPlacement()
        {
            return new CoverPropPlacement(PropType, ZoneType, OriginCell, Size, GetOccupiedCells());
        }
    }

    private sealed class GeneratedMapData
    {
        // This is the temporary writable version of the map. Once generation finishes,
        // the data is copied into GeneratedFloorLayout for the rest of the game.
        public readonly HashSet<Vector3Int> GroundCells = new();
        public readonly HashSet<Vector3Int> ForcedWallCells = new();
        public readonly HashSet<Vector3Int> DoorCells = new();
        public readonly List<RoomData> Rooms = new();
        public readonly List<ZonePlan> ZonePlans = new();
        public readonly List<GeneratedZoneData> Zones = new();
        public readonly List<GeneratedRoomData> RoomRecords = new();
        public readonly List<GeneratedRouteData> RouteRecords = new();
        public readonly List<GeneratedDoorGroupData> DoorGroups = new();
        public readonly List<CoverPropPlacement> CoverProps = new();
        public readonly Dictionary<int, HashSet<Vector3Int>> RoomDoorCellsById = new();
        public readonly HashSet<Vector3Int> ReservedCells = new();
        public readonly HashSet<Vector3Int> PropBlockedCells = new();
        public Vector3Int PlayerStartCell;
        public Vector3Int KeyCell;
        public Vector3Int BatteryCell;
        public Vector3Int ExitCell;
        public Vector3Int PatrolSpawnCell;
        public Vector3Int GlassPanelCell;
        public Vector3Int SafeRoomCell;
        public Vector3Int[] DangerCells = Array.Empty<Vector3Int>();
        public Vector3Int[] MainDoorCells = Array.Empty<Vector3Int>();
    }

    private enum OfficeMixedStyle
    {
        Balanced,
        WingHeavy,
        ChainHeavy
    }

    public static void EnsureBuiltForActiveScene()
    {
        return;
    }

    public static OfficeFloorBuildResult BuildOfficeFloor(EscapeFloorDefinition floorDefinition, Transform parent = null)
    {
        Scene targetScene = parent != null && parent.gameObject.scene.IsValid()
            ? parent.gameObject.scene
            : ResolveLoadedSceneForFloor(floorDefinition);

        return BuildOfficeFloor(floorDefinition, targetScene, parent);
    }

    public static OfficeFloorBuildResult BuildOfficeFloor(
        EscapeFloorDefinition floorDefinition,
        Scene targetScene,
        Transform parent = null)
    {
        if (floorDefinition == null)
        {
            return default;
        }

        EnsureTiles();

        if (TryBuildSceneResidentAuthoredFloor(floorDefinition, targetScene, out OfficeFloorBuildResult authoredBuild))
        {
            return authoredBuild;
        }

        if (TryBuildAuthoredOfficeFloor(floorDefinition, parent, out OfficeFloorBuildResult authoredPrefabBuild))
        {
            Debug.LogWarning(
                $"Scene-resident authored floor for {floorDefinition.FloorNumber}F is unavailable. " +
                $"Using authored floor prefab resource fallback for the clean loop.");
            return authoredPrefabBuild;
        }

        Debug.LogError(
            $"Scene-resident authored floor is required for {floorDefinition.FloorNumber}F. " +
            $"Open the matching floor scene and author the map there instead of loading a floor prefab resource.");
        return default;
    }

    private static GeneratedMapData GenerateMapData(Scene scene)
    {
        // Generation may fail validation, so this wrapper retries with different seeds
        // before giving up and using the deterministic fallback.
        int baseSeed = PrototypeSceneUtility.TryConsumeForcedSeed(scene, out int forcedSeed)
            ? forcedSeed
            : Environment.TickCount;
        PrototypeSceneUtility.NoteUsedSeed(scene, baseSeed);

        for (int attempt = 0; attempt < 24; attempt++)
        {
            int seed = baseSeed + (attempt * 997);

            if (TryGenerateMapData(scene, seed, out GeneratedMapData generated))
            {
                return generated;
            }
        }

        Debug.LogWarning("Falling back to deterministic floor seed after procedural generation retries.");
        if (TryGenerateMapData(scene, 1337, out GeneratedMapData fallback))
        {
            return fallback;
        }

        return CreateEmergencyFallback();
    }

    private static Scene ResolveLoadedSceneForFloor(EscapeFloorDefinition floorDefinition)
    {
        if (floorDefinition == null)
        {
            return default;
        }

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            if (CanUseSceneResidentAuthoredFloor(floorDefinition, scene)
                || MainEscapeSceneIdentityUtility.MatchesCanonicalFloorScene(scene, floorDefinition.FloorNumber))
            {
                return scene;
            }
        }

        return default;
    }

    private static GeneratedMapData GenerateEscapeFloorMapData(EscapeFloorDefinition floorDefinition)
    {
        GeneratedMapData data = new();
        BuildBaseOfficeLayout(data);
        AssignEncounterCells(data);
        ApplyEscapeFloorAnchors(data, floorDefinition);
        data.SafeRoomCell = floorDefinition.PlayerSpawnCell;
        data.DangerCells = BuildOfficeDangerCells(data);

        foreach (Vector3Int doorCell in data.MainDoorCells)
        {
            data.DoorCells.Add(doorCell);
        }

        ReserveCriticalCells(data);
        ReserveCellCluster(data.ReservedCells, floorDefinition.StalkerSpawnCell, 1);

        foreach (Vector3Int patrolCell in floorDefinition.PatrolRoute)
        {
            data.ReservedCells.Add(patrolCell);
        }

        ApplyEscapeFloorVariantGeometry(data, floorDefinition);
        ApplyEscapeFloorVariantProps(data, floorDefinition);
        BuildZoneMetadata(data);
        BuildOfficeLayoutMetadata(data);

        if (!ValidateCriticalTraversal(data))
        {
            Debug.LogWarning($"MainEscape floor {floorDefinition.FloorNumber}F variant {floorDefinition.VariantId} did not fully pass traversal validation.");
        }

        return data;
    }

    private static bool TryGenerateMapData(Scene activeScene, int seed, out GeneratedMapData data)
    {
        // Scene name decides which family of generators we want to compare right now.
        data = new GeneratedMapData();
        PrototypeGeneratorMode generatorMode = PrototypeSceneUtility.GetGeneratorMode(activeScene);

        if (generatorMode == PrototypeGeneratorMode.OfficeCorridor)
        {
            return TryGenerateOfficeMapData(seed, out data);
        }

        if (generatorMode == PrototypeGeneratorMode.BaseOffice)
        {
            return TryGenerateBaseOfficeMapData(out data);
        }

        if (generatorMode == PrototypeGeneratorMode.OfficeMixed)
        {
            return TryGenerateOfficeMixedMapData(seed, out data);
        }

        if (generatorMode == PrototypeGeneratorMode.OfficeWinged)
        {
            return TryGenerateOfficeVariantMapData(seed, OfficeMixedStyle.WingHeavy, out data);
        }

        if (generatorMode == PrototypeGeneratorMode.OfficeChained)
        {
            return TryGenerateOfficeVariantMapData(seed, OfficeMixedStyle.ChainHeavy, out data);
        }

        GeneratedMapBlueprint blueprint;
        bool generated = generatorMode switch
        {
            PrototypeGeneratorMode.Hybrid => GameplayRoomGraphGenerator.TryGenerateHybrid(seed, out blueprint),
            PrototypeGeneratorMode.Bsp => GameplayBspGenerator.TryGenerate(seed, out blueprint),
            _ => GameplayRoomGraphGenerator.TryGenerate(seed, out blueprint)
        };

        if (!generated || blueprint == null)
        {
            return false;
        }

        PopulateFromBlueprint(data, blueprint);
        return data.MainDoorCells.Length > 0;
    }

    private static bool TryGenerateBaseOfficeMapData(out GeneratedMapData data)
    {
        // BaseOffice is intentionally fixed rather than random. It is the stable test bed
        // used for encounter experiments like the vent enemy.
        data = new GeneratedMapData();

        if (!BuildBaseOfficeLayout(data))
        {
            return false;
        }

        AssignEncounterCells(data);
        data.SafeRoomCell = GetOfficeSafeRoomCell(data);
        data.DangerCells = BuildOfficeDangerCells(data);

        foreach (Vector3Int doorCell in data.MainDoorCells)
        {
            data.DoorCells.Add(doorCell);
        }

        ReserveCriticalCells(data);
        BuildZoneMetadata(data);
        BuildOfficeLayoutMetadata(data);
        return ValidateCriticalTraversal(data);
    }

    private static bool TryGenerateOfficeMapData(int seed, out GeneratedMapData data)
    {
        data = new GeneratedMapData();
        System.Random random = new(seed);

        if (!TryGenerateOfficeLayout(random, data))
        {
            return false;
        }

        AssignEncounterCells(data);
        data.SafeRoomCell = GetOfficeSafeRoomCell(data);
        data.DangerCells = BuildOfficeDangerCells(data);

        foreach (Vector3Int doorCell in data.MainDoorCells)
        {
            data.DoorCells.Add(doorCell);
        }

        ReserveCriticalCells(data);
        GenerateCoverProps(random, data);
        BuildZoneMetadata(data);
        BuildOfficeLayoutMetadata(data);
        return ValidateCriticalTraversal(data);
    }

    private static bool BuildBaseOfficeLayout(GeneratedMapData data)
    {
        data.GroundCells.Clear();
        data.ForcedWallCells.Clear();
        data.DoorCells.Clear();
        data.Rooms.Clear();
        data.ZonePlans.Clear();
        data.Zones.Clear();
        data.RoomDoorCellsById.Clear();
        data.DoorGroups.Clear();
        data.CoverProps.Clear();
        data.ReservedCells.Clear();
        data.PropBlockedCells.Clear();

        FillRect(data.GroundCells, -38, 38, -1, 0);
        FillRect(data.GroundCells, -17, -16, -14, 12);
        FillRect(data.GroundCells, 10, 11, -14, 12);
        FillRect(data.GroundCells, 28, 29, 1, 12);

        RoomData startRoom = AddZoneRoom(data, new RectInt(-37, 2, 11, 11), GeneratedZoneType.Start);
        RoomData officeNorthA = AddZoneRoom(data, new RectInt(-23, 2, 10, 10), GeneratedZoneType.OpenOffice);
        RoomData officeNorthB = AddZoneRoom(data, new RectInt(-10, 2, 13, 11), GeneratedZoneType.OpenOffice);
        RoomData securityRoom = AddZoneRoom(data, new RectInt(4, 2, 13, 11), GeneratedZoneType.Security);
        RoomData preExitNorth = AddZoneRoom(data, new RectInt(23, 2, 12, 10), GeneratedZoneType.Facility);
        RoomData utilityRoom = AddZoneRoom(data, new RectInt(-36, -14, 13, 10), GeneratedZoneType.Utility);
        RoomData officeSouthA = AddZoneRoom(data, new RectInt(-21, -15, 12, 11), GeneratedZoneType.OpenOffice);
        RoomData officeSouthB = AddZoneRoom(data, new RectInt(-6, -14, 13, 10), GeneratedZoneType.OpenOffice);
        RoomData facilitySouth = AddZoneRoom(data, new RectInt(8, -15, 11, 11), GeneratedZoneType.Facility);
        RoomData exitRoom = AddZoneRoom(data, new RectInt(23, -14, 13, 10), GeneratedZoneType.Exit);

        FillRect(data.GroundCells, startRoom.Bounds.xMin, startRoom.Bounds.xMax - 1, startRoom.Bounds.yMin, startRoom.Bounds.yMax - 1);
        FillRect(data.GroundCells, officeNorthA.Bounds.xMin, officeNorthA.Bounds.xMax - 1, officeNorthA.Bounds.yMin, officeNorthA.Bounds.yMax - 1);
        FillRect(data.GroundCells, officeNorthB.Bounds.xMin, officeNorthB.Bounds.xMax - 1, officeNorthB.Bounds.yMin, officeNorthB.Bounds.yMax - 1);
        FillRect(data.GroundCells, securityRoom.Bounds.xMin, securityRoom.Bounds.xMax - 1, securityRoom.Bounds.yMin, securityRoom.Bounds.yMax - 1);
        FillRect(data.GroundCells, preExitNorth.Bounds.xMin, preExitNorth.Bounds.xMax - 1, preExitNorth.Bounds.yMin, preExitNorth.Bounds.yMax - 1);
        FillRect(data.GroundCells, utilityRoom.Bounds.xMin, utilityRoom.Bounds.xMax - 1, utilityRoom.Bounds.yMin, utilityRoom.Bounds.yMax - 1);
        FillRect(data.GroundCells, officeSouthA.Bounds.xMin, officeSouthA.Bounds.xMax - 1, officeSouthA.Bounds.yMin, officeSouthA.Bounds.yMax - 1);
        FillRect(data.GroundCells, officeSouthB.Bounds.xMin, officeSouthB.Bounds.xMax - 1, officeSouthB.Bounds.yMin, officeSouthB.Bounds.yMax - 1);
        FillRect(data.GroundCells, facilitySouth.Bounds.xMin, facilitySouth.Bounds.xMax - 1, facilitySouth.Bounds.yMin, facilitySouth.Bounds.yMax - 1);
        FillRect(data.GroundCells, exitRoom.Bounds.xMin, exitRoom.Bounds.xMax - 1, exitRoom.Bounds.yMin, exitRoom.Bounds.yMax - 1);

        AddRoomBoundaryWithDoor(data, 0, startRoom, isTopSide: true, new Vector2Int(-33, -32));
        AddRoomBoundaryWithDoor(data, 1, officeNorthA, isTopSide: true, new Vector2Int(-20, -19));
        AddRoomBoundaryWithDoor(data, 2, officeNorthB, isTopSide: true, new Vector2Int(-4, -3));
        AddRoomBoundaryWithDoor(data, 3, securityRoom, isTopSide: true, new Vector2Int(8, 9));
        AddRoomBoundaryWithDoor(data, 4, preExitNorth, isTopSide: true, new Vector2Int(28, 29));
        AddRoomBoundaryWithDoor(data, 5, utilityRoom, isTopSide: false, new Vector2Int(-31, -30));
        AddRoomBoundaryWithDoor(data, 6, officeSouthA, isTopSide: false, new Vector2Int(-16, -15));
        AddRoomBoundaryWithDoor(data, 7, officeSouthB, isTopSide: false, new Vector2Int(-1, 0));
        AddRoomBoundaryWithDoor(data, 8, facilitySouth, isTopSide: false, new Vector2Int(13, 14));
        AddRoomBoundaryWithDoor(data, 9, exitRoom, isTopSide: false, new Vector2Int(28, 29));

        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(-33, -32), roomBoundaryY: startRoom.Bounds.yMin, corridorY: 0);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(-20, -19), roomBoundaryY: officeNorthA.Bounds.yMin, corridorY: 0);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(-4, -3), roomBoundaryY: officeNorthB.Bounds.yMin, corridorY: 0);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(8, 9), roomBoundaryY: securityRoom.Bounds.yMin, corridorY: 0);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(28, 29), roomBoundaryY: preExitNorth.Bounds.yMin, corridorY: 0);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(-31, -30), roomBoundaryY: utilityRoom.Bounds.yMax - 1, corridorY: -1);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(-16, -15), roomBoundaryY: officeSouthA.Bounds.yMax - 1, corridorY: -1);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(-1, 0), roomBoundaryY: officeSouthB.Bounds.yMax - 1, corridorY: -1);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(13, 14), roomBoundaryY: facilitySouth.Bounds.yMax - 1, corridorY: -1);
        ConnectDoorSpanToMainCorridor(data.GroundCells, new Vector2Int(28, 29), roomBoundaryY: exitRoom.Bounds.yMax - 1, corridorY: -1);

        data.MainDoorCells = new[]
        {
            new Vector3Int(20, -1, 0),
            new Vector3Int(20, 0, 0)
        };
        AddTraversableDoorGroup(data, requiresKey: true, Array.Empty<int>(), data.MainDoorCells);

        AddTraversableDoorGroup(data, requiresKey: false, new[] { 3, 8 },
            new Vector3Int(10, 2, 0),
            new Vector3Int(10, 3, 0));

        AddTraversableDoorGroup(data, requiresKey: false, new[] { 4, 9 },
            new Vector3Int(28, 2, 0),
            new Vector3Int(29, 2, 0));

        AddTraversableDoorGroup(data, requiresKey: false, new[] { 1, 2 },
            new Vector3Int(-11, 6, 0),
            new Vector3Int(-11, 7, 0));

        AddTraversableDoorGroup(data, requiresKey: false, new[] { 6, 7 },
            new Vector3Int(-7, -10, 0),
            new Vector3Int(-7, -9, 0));

        return data.MainDoorCells.Length > 0 && data.ZonePlans.Count >= 10;
    }

    private static void ConnectDoorSpanToMainCorridor(HashSet<Vector3Int> groundCells, Vector2Int doorSpan, int roomBoundaryY, int corridorY)
    {
        int minY = Mathf.Min(roomBoundaryY, corridorY);
        int maxY = Mathf.Max(roomBoundaryY, corridorY);
        FillRect(groundCells, doorSpan.x, doorSpan.y, minY, maxY);
    }

    private static void ApplyEscapeFloorAnchors(GeneratedMapData data, EscapeFloorDefinition floorDefinition)
    {
        data.PlayerStartCell = floorDefinition.PlayerSpawnCell;
        data.KeyCell = floorDefinition.ToolCell;
        data.BatteryCell = floorDefinition.TransitionCell;
        data.ExitCell = floorDefinition.TransitionCell;
        data.GlassPanelCell = floorDefinition.ToolCell;
        data.PatrolSpawnCell = floorDefinition.PatrolRoute.Length > 0
            ? floorDefinition.PatrolRoute[0]
            : floorDefinition.StalkerSpawnCell;
    }

    private static void ApplyEscapeFloorVariantGeometry(GeneratedMapData data, EscapeFloorDefinition floorDefinition)
    {
        switch (floorDefinition.VariantId)
        {
            case EscapeFloorVariantId.Segmented4F:
                AddVerticalInteriorWall(data, -19, 3, 10, 6, 7);
                AddVerticalInteriorWall(data, -4, 3, 11, 8, 9);
                AddVerticalInteriorWall(data, -16, -14, -6, -11, -10);
                AddVerticalInteriorWall(data, 0, -13, -6, -8, -7);
                AddHorizontalInteriorWall(data, 24, 34, 6, 28, 29);
                break;
            case EscapeFloorVariantId.Choke3F:
                AddHorizontalInteriorWall(data, -9, 1, 6, -2, -1);
                AddHorizontalInteriorWall(data, -5, 5, -10, 1, 2);
                AddVerticalInteriorWall(data, 11, 3, 10, 5, 6);
                AddVerticalInteriorWall(data, -31, -13, -6, -10, -9);
                break;
            case EscapeFloorVariantId.Security2F:
                AddVerticalInteriorWall(data, 10, 3, 11, 5, 6);
                AddHorizontalInteriorWall(data, 9, 18, -10, 13, 14);
                AddVerticalInteriorWall(data, -30, -13, -6, -10, -9);
                AddHorizontalInteriorWall(data, 24, 34, 8, 29, 30);
                break;
            case EscapeFloorVariantId.Lobby1F:
                AddHorizontalInteriorWall(data, 24, 34, -9, 31, 32);
                AddVerticalInteriorWall(data, 27, -13, -6, -10, -9);
                AddVerticalInteriorWall(data, 30, 3, 10, 6, 7);
                AddHorizontalInteriorWall(data, -22, -14, 7, -18, -17);
                break;
        }
    }

    private static void ApplyEscapeFloorVariantProps(GeneratedMapData data, EscapeFloorDefinition floorDefinition)
    {
        AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.OpenOffice, new Vector3Int(-21, 4, 0), new Vector2Int(2, 2));
        AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.Utility, new Vector3Int(-33, -12, 0), new Vector2Int(2, 1));
        AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.Exit, new Vector3Int(30, -12, 0), new Vector2Int(2, 1));

        switch (floorDefinition.VariantId)
        {
            case EscapeFloorVariantId.Base5F:
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.Security, new Vector3Int(12, 8, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.OpenOffice, new Vector3Int(-5, 8, 0), new Vector2Int(2, 1));
                break;
            case EscapeFloorVariantId.Segmented4F:
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.OpenOffice, new Vector3Int(-8, 4, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.OpenOffice, new Vector3Int(-18, -12, 0), new Vector2Int(2, 1));
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.Facility, new Vector3Int(10, -12, 0), new Vector2Int(2, 2));
                break;
            case EscapeFloorVariantId.Choke3F:
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.OpenOffice, new Vector3Int(-1, 8, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.OpenOffice, new Vector3Int(-2, -13, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.Security, new Vector3Int(6, 4, 0), new Vector2Int(2, 1));
                break;
            case EscapeFloorVariantId.Security2F:
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.Security, new Vector3Int(13, 4, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.Facility, new Vector3Int(15, -13, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.Facility, new Vector3Int(25, 4, 0), new Vector2Int(2, 1));
                break;
            case EscapeFloorVariantId.Lobby1F:
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.Exit, new Vector3Int(24, -12, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.CrateStack, GeneratedZoneType.Exit, new Vector3Int(29, -8, 0), new Vector2Int(2, 2));
                AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.Facility, new Vector3Int(10, -12, 0), new Vector2Int(2, 1));
                AddCoverPropPlacement(data, CoverPropType.Planter, GeneratedZoneType.Facility, new Vector3Int(26, 4, 0), new Vector2Int(2, 1));
                break;
        }
    }

    private static void AddVerticalInteriorWall(GeneratedMapData data, int x, int minY, int maxY, int gapMinY, int gapMaxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            if (y >= gapMinY && y <= gapMaxY)
            {
                continue;
            }

            Vector3Int cell = new(x, y, 0);

            if (data.GroundCells.Contains(cell)
                && !data.DoorCells.Contains(cell)
                && !data.ReservedCells.Contains(cell))
            {
                data.ForcedWallCells.Add(cell);
            }
        }
    }

    private static void AddHorizontalInteriorWall(GeneratedMapData data, int minX, int maxX, int y, int gapMinX, int gapMaxX)
    {
        for (int x = minX; x <= maxX; x++)
        {
            if (x >= gapMinX && x <= gapMaxX)
            {
                continue;
            }

            Vector3Int cell = new(x, y, 0);

            if (data.GroundCells.Contains(cell)
                && !data.DoorCells.Contains(cell)
                && !data.ReservedCells.Contains(cell))
            {
                data.ForcedWallCells.Add(cell);
            }
        }
    }

    private static void AddCoverPropPlacement(
        GeneratedMapData data,
        CoverPropType propType,
        GeneratedZoneType zoneType,
        Vector3Int originCell,
        Vector2Int size)
    {
        Vector3Int[] occupiedCells = new Vector3Int[size.x * size.y];
        int index = 0;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector3Int cell = new(originCell.x + x, originCell.y + y, 0);

                if (!data.GroundCells.Contains(cell)
                    || data.ForcedWallCells.Contains(cell)
                    || data.DoorCells.Contains(cell)
                    || data.ReservedCells.Contains(cell)
                    || data.PropBlockedCells.Contains(cell))
                {
                    return;
                }

                occupiedCells[index++] = cell;
            }
        }

        foreach (Vector3Int occupiedCell in occupiedCells)
        {
            foreach (Vector3Int doorCell in data.MainDoorCells)
            {
                if (Mathf.Abs(occupiedCell.x - doorCell.x) <= 1 && Mathf.Abs(occupiedCell.y - doorCell.y) <= 1)
                {
                    return;
                }
            }
        }

        HashSet<Vector3Int> blockedCells = new(data.PropBlockedCells);

        foreach (Vector3Int occupiedCell in occupiedCells)
        {
            blockedCells.Add(occupiedCell);
        }

        if (!CanTraverse(data, blockedCells, data.PlayerStartCell, data.KeyCell)
            || !CanTraverse(data, blockedCells, data.PlayerStartCell, data.BatteryCell)
            || !CanTraverse(data, blockedCells, data.KeyCell, data.ExitCell)
            || !CanTraverse(data, blockedCells, data.PatrolSpawnCell, data.MainDoorCells[0]))
        {
            return;
        }

        data.CoverProps.Add(new CoverPropPlacement(propType, zoneType, originCell, size, occupiedCells));

        foreach (Vector3Int cell in occupiedCells)
        {
            data.PropBlockedCells.Add(cell);
        }
    }

    private static bool TryGenerateOfficeMixedMapData(int seed, out GeneratedMapData data)
    {
        return TryGenerateOfficeVariantMapData(seed, OfficeMixedStyle.Balanced, out data);
    }

    private static bool TryGenerateOfficeVariantMapData(int seed, OfficeMixedStyle style, out GeneratedMapData data)
    {
        data = new GeneratedMapData();
        System.Random random = new(seed);

        if (!TryGenerateOfficeLayout(random, data))
        {
            return false;
        }

        AddOfficeMixedExtensions(random, data, style);

        AssignEncounterCells(data);
        data.SafeRoomCell = GetOfficeSafeRoomCell(data);
        data.DangerCells = BuildOfficeDangerCells(data);

        foreach (Vector3Int doorCell in data.MainDoorCells)
        {
            data.DoorCells.Add(doorCell);
        }

        ReserveCriticalCells(data);
        GenerateCoverProps(random, data);
        BuildZoneMetadata(data);
        BuildOfficeLayoutMetadata(data);
        return ValidateCriticalTraversal(data);
    }

    private static bool TryGenerateOfficeLayout(System.Random random, GeneratedMapData data)
    {
        OfficeLayoutConfig config = GetOfficeLayoutConfig();
        data.GroundCells.Clear();
        data.ForcedWallCells.Clear();
        data.DoorCells.Clear();
        data.Rooms.Clear();
        data.ZonePlans.Clear();
        data.Zones.Clear();
        data.DoorGroups.Clear();
        data.RoomDoorCellsById.Clear();
        data.CoverProps.Clear();
        data.ReservedCells.Clear();
        data.PropBlockedCells.Clear();

        int roomCountPerRow = config.RoomCountPerRow;
        int corridorWidth = config.CorridorWidth;
        int segmentGap = config.SegmentGap;
        int startX = config.StartX;
        int totalRoomWidth = config.TotalRoomWidth;

        int firstSegmentCount = random.Next(3, 5);
        int secondSegmentCount = random.Next(3, 5);
        int thirdSegmentCount = roomCountPerRow - firstSegmentCount - secondSegmentCount;

        if (thirdSegmentCount < 2)
        {
            secondSegmentCount = Mathf.Max(3, roomCountPerRow - firstSegmentCount - 2);
            thirdSegmentCount = roomCountPerRow - firstSegmentCount - secondSegmentCount;
        }

        int[] segmentColumnCounts =
        {
            firstSegmentCount,
            secondSegmentCount,
            thirdSegmentCount
        };

        int firstCorridorBottomY = random.Next(-1, 2);
        int secondCorridorBottomY = Mathf.Clamp(
            firstCorridorBottomY + (random.NextDouble() < 0.5d ? random.Next(4, 7) : -random.Next(4, 7)),
            config.CorridorBottomMinY,
            config.CorridorBottomMaxY);

        if (Mathf.Abs(secondCorridorBottomY - firstCorridorBottomY) < 3)
        {
            secondCorridorBottomY = Mathf.Clamp(
                firstCorridorBottomY + (secondCorridorBottomY >= firstCorridorBottomY ? 4 : -4),
                config.CorridorBottomMinY,
                config.CorridorBottomMaxY);
        }

        int thirdCorridorBottomY = random.NextDouble() < 0.7d
            ? firstCorridorBottomY
            : Mathf.Clamp(
                secondCorridorBottomY + (secondCorridorBottomY > firstCorridorBottomY ? -random.Next(4, 7) : random.Next(4, 7)),
                config.CorridorBottomMinY,
                config.CorridorBottomMaxY);

        int[] corridorBottomYs =
        {
            firstCorridorBottomY,
            secondCorridorBottomY,
            thirdCorridorBottomY
        };

        List<int> sharedWidths = BuildRoomWidths(random, roomCountPerRow, totalRoomWidth, config.MinRoomWidth, config.MaxRoomWidth);
        List<int> bottomHeights = BuildRoomHeights(random, roomCountPerRow, config.MinRoomHeight, config.MaxRoomHeight);
        List<int> topHeights = BuildRoomHeights(random, roomCountPerRow, config.MinRoomHeight, config.MaxRoomHeight);
        int horizontalCorridorCount = random.Next(2, 4);
        int[] horizontalRoomGapHeights = BuildOfficeHorizontalRoomGapHeights(random, horizontalCorridorCount);
        int[] auxiliaryLaneHostSegments = BuildOfficeAuxiliaryLaneHostSegments(random, horizontalCorridorCount - 1, segmentColumnCounts.Length);
        int centralBandHeight = CalculateOfficeCentralBandHeight(corridorWidth, horizontalRoomGapHeights);
        int gateAfterIndex = firstSegmentCount + random.Next(1, secondSegmentCount);
        int gateDividerX = 0;
        int gateCorridorBottomY = 0;
        int gateWallMinY = 0;
        int gateWallMaxY = 0;
        int currentX = startX;
        int globalRoomIndex = 0;
        List<OfficeCrossCorridorCandidate> crossCorridorCandidates = new();
        List<OfficeCentralBandSegment> centralBandSegments = new();

        for (int segmentIndex = 0; segmentIndex < segmentColumnCounts.Length; segmentIndex++)
        {
            int segmentRoomCount = segmentColumnCounts[segmentIndex];
            int corridorBottomY = corridorBottomYs[segmentIndex];
            int topRoomMinY = corridorBottomY + centralBandHeight;
            int segmentStartX = currentX;
            List<int> laneBottoms = BuildOfficeHorizontalLaneBottoms(corridorBottomY, corridorWidth, horizontalRoomGapHeights);
            bool[] activeLanes = BuildOfficeHorizontalLaneActivity(laneBottoms.Count, segmentIndex, auxiliaryLaneHostSegments);

            for (int localRoomIndex = 0; localRoomIndex < segmentRoomCount; localRoomIndex++, globalRoomIndex++)
            {
                int width = sharedWidths[globalRoomIndex];
                int bottomHeight = bottomHeights[globalRoomIndex];
                int topHeight = topHeights[globalRoomIndex];
                RectInt bottomRoomBounds = new(currentX, corridorBottomY - bottomHeight, width, bottomHeight);
                RectInt topRoomBounds = new(currentX, topRoomMinY, width, topHeight);
                FillRect(data.GroundCells, bottomRoomBounds.xMin, bottomRoomBounds.xMax - 1, bottomRoomBounds.yMin, bottomRoomBounds.yMax - 1);
                FillRect(data.GroundCells, topRoomBounds.xMin, topRoomBounds.xMax - 1, topRoomBounds.yMin, topRoomBounds.yMax - 1);
                ApplyOfficeRoomShapeVariation(random, data.GroundCells, bottomRoomBounds, isTopRoom: false);
                ApplyOfficeRoomShapeVariation(random, data.GroundCells, topRoomBounds, isTopRoom: true);

                RoomData bottomRoom = AddZoneRoom(data, bottomRoomBounds, GetBottomRowZoneType(globalRoomIndex, roomCountPerRow));
                int bottomRoomId = data.ZonePlans.Count - 1;
                RoomData topRoom = AddZoneRoom(data, topRoomBounds, GetTopRowZoneType(globalRoomIndex, roomCountPerRow, gateAfterIndex));
                int topRoomId = data.ZonePlans.Count - 1;
                Vector2Int bottomDoorSpan = GetRandomDoorSpan(random, bottomRoom, preferLeftSide: globalRoomIndex >= gateAfterIndex, preferRightSide: globalRoomIndex == 0);
                Vector2Int topDoorSpan = GetRandomDoorSpan(random, topRoom, preferLeftSide: globalRoomIndex >= gateAfterIndex);

                AddHorizontalRoomBoundary(
                    data.ForcedWallCells,
                    bottomRoom,
                    isTopSide: false,
                    bottomDoorSpan);
                AddHorizontalRoomBoundary(
                    data.ForcedWallCells,
                    topRoom,
                    isTopSide: true,
                    topDoorSpan);
                RegisterDoorGroup(data, requiresKey: false, new[] { bottomRoomId }, GetBoundaryDoorCells(bottomRoom, isTopSide: false, bottomDoorSpan));
                RegisterDoorGroup(data, requiresKey: false, new[] { topRoomId }, GetBoundaryDoorCells(topRoom, isTopSide: true, topDoorSpan));

                bool hasNextRoomInSegment = localRoomIndex < segmentRoomCount - 1;

                if (hasNextRoomInSegment)
                {
                    int dividerX = bottomRoomBounds.xMax;
                    AddVerticalWallLine(data.ForcedWallCells, dividerX, bottomRoomBounds.yMin, bottomRoomBounds.yMax - 1);
                    AddVerticalWallLine(data.ForcedWallCells, dividerX, topRoomBounds.yMin, topRoomBounds.yMax - 1);
                    crossCorridorCandidates.Add(new OfficeCrossCorridorCandidate(dividerX, corridorBottomY, corridorBottomY + centralBandHeight - 1, corridorBottomY));

                    if (globalRoomIndex == gateAfterIndex - 1)
                    {
                        gateDividerX = dividerX;
                        gateCorridorBottomY = corridorBottomY;
                        gateWallMinY = bottomRoomBounds.yMin;
                        gateWallMaxY = topRoomBounds.yMax - 1;
                    }

                    currentX += width + 1;
                }
                else
                {
                    currentX += width;
                }
            }

            int segmentEndX = currentX - 1;
            CarveOfficeHorizontalCorridors(
                random,
                data.GroundCells,
                segmentStartX,
                segmentEndX,
                laneBottoms,
                activeLanes,
                segmentIndex,
                auxiliaryLaneHostSegments);
            centralBandSegments.Add(new OfficeCentralBandSegment(segmentStartX, segmentEndX, laneBottoms.ToArray(), activeLanes));

            if (segmentIndex < segmentColumnCounts.Length - 1)
            {
                int nextSegmentStartX = segmentEndX + segmentGap;
                AddOfficeSegmentConnector(data.GroundCells, segmentEndX, nextSegmentStartX, corridorBottomY, corridorBottomYs[segmentIndex + 1], corridorWidth);
                currentX = nextSegmentStartX;
            }
        }

        if (gateDividerX == 0)
        {
            return false;
        }

        AddVerticalWallLine(data.ForcedWallCells, gateDividerX, gateWallMinY, gateWallMaxY);
        data.MainDoorCells = new[]
        {
            new Vector3Int(gateDividerX, gateCorridorBottomY, 0),
            new Vector3Int(gateDividerX, gateCorridorBottomY + 1, 0)
        };

        AddTraversableDoorGroup(data, requiresKey: true, Array.Empty<int>(), data.MainDoorCells);
        List<OfficeCrossCorridorSpan> crossCorridorSpans = AddOfficeCrossCorridors(random, data, crossCorridorCandidates, gateDividerX, corridorWidth, out HashSet<int> crossCorridorXs);
        AddOfficeCentralBandRooms(random, data, centralBandSegments, crossCorridorSpans, gateDividerX, corridorWidth);
        AddOfficeInterRoomDoors(random, data, gateDividerX, crossCorridorXs);

        return data.ZonePlans.Count >= 20
            && data.Rooms.Count >= 20;
    }

    private static OfficeLayoutConfig GetOfficeLayoutConfig()
    {
        return new OfficeLayoutConfig(
            targetWidthCells: 108,
            targetHeightCells: 64,
            roomCountPerRow: 10,
            corridorWidth: 2,
            segmentGap: 5,
            minRoomWidth: 5,
            maxRoomWidth: 12,
            minRoomHeight: 11,
            maxRoomHeight: 22);
    }

    private static bool FitsOfficeLayoutTargetSize(GeneratedMapData data, OfficeLayoutConfig config)
    {
        if (data.GroundCells.Count == 0)
        {
            return false;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        foreach (Vector3Int cell in data.GroundCells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        int width = (maxX - minX) + 1;
        int height = (maxY - minY) + 1;
        int minimumTargetWidth = config.TargetWidthCells - 6;
        int minimumTargetHeight = config.TargetHeightCells - 14;

        return width <= config.TargetWidthCells
            && height <= config.TargetHeightCells
            && width >= minimumTargetWidth
            && height >= minimumTargetHeight;
    }

    private static void AddOfficeSegmentConnector(
        HashSet<Vector3Int> groundCells,
        int leftSegmentEndX,
        int rightSegmentStartX,
        int fromBottomY,
        int toBottomY,
        int corridorWidth)
    {
        int bendLeftX = leftSegmentEndX + 1;

        // Keep the elbow tight so the outside corner does not carve extra floor cells.
        CarveHorizontalCorridor(groundCells, leftSegmentEndX, bendLeftX, fromBottomY, corridorWidth);
        CarveVerticalCorridor(groundCells, fromBottomY, toBottomY, bendLeftX, corridorWidth);
        CarveHorizontalCorridor(groundCells, bendLeftX, rightSegmentStartX, toBottomY, corridorWidth);
    }

    private static void CarveOfficeHorizontalCorridors(
        System.Random random,
        HashSet<Vector3Int> groundCells,
        int segmentStartX,
        int segmentEndX,
        IReadOnlyList<int> laneBottoms,
        IReadOnlyList<bool> activeLanes,
        int segmentIndex,
        int[] auxiliaryLaneHostSegments)
    {
        if (laneBottoms == null || laneBottoms.Count == 0)
        {
            return;
        }

        // Keep one reliable central lane for traversal, then add shorter auxiliary lanes.
        CarveHorizontalCorridor(groundCells, segmentStartX, segmentEndX, laneBottoms[0], CorridorWidth);

        for (int laneIndex = 1; laneIndex < laneBottoms.Count; laneIndex++)
        {
            if (activeLanes == null || laneIndex >= activeLanes.Count || !activeLanes[laneIndex])
            {
                continue;
            }

            int segmentWidth = Mathf.Max(8, (segmentEndX - segmentStartX) + 1);
            int minLength = Mathf.Max(7, segmentWidth / 3);
            int maxLengthExclusive = Mathf.Max(minLength + 1, (segmentWidth * 2) / 3);
            int desiredLength = Mathf.Clamp(
                random.Next(minLength, maxLengthExclusive),
                7,
                segmentWidth - 2);

            int maxStartX = segmentEndX - desiredLength + 1;

            if (maxStartX <= segmentStartX)
            {
                continue;
            }

            int laneStartX = random.Next(segmentStartX, maxStartX + 1);
            int laneEndX = laneStartX + desiredLength - 1;

            if (laneEndX - laneStartX < 8)
            {
                continue;
            }

            CarveHorizontalCorridor(groundCells, laneStartX, laneEndX, laneBottoms[laneIndex], CorridorWidth);
        }
    }

    private static bool[] BuildOfficeHorizontalLaneActivity(int laneCount, int segmentIndex, int[] auxiliaryLaneHostSegments)
    {
        bool[] activeLanes = new bool[Mathf.Max(0, laneCount)];

        if (activeLanes.Length == 0)
        {
            return activeLanes;
        }

        activeLanes[0] = true;

        for (int laneIndex = 1; laneIndex < activeLanes.Length; laneIndex++)
        {
            activeLanes[laneIndex] = auxiliaryLaneHostSegments != null
                && laneIndex - 1 < auxiliaryLaneHostSegments.Length
                && auxiliaryLaneHostSegments[laneIndex - 1] == segmentIndex;
        }

        return activeLanes;
    }

    private static int[] BuildOfficeAuxiliaryLaneHostSegments(System.Random random, int auxiliaryLaneCount, int segmentCount)
    {
        if (auxiliaryLaneCount <= 0 || segmentCount <= 0)
        {
            return Array.Empty<int>();
        }

        int[] hostSegments = new int[auxiliaryLaneCount];

        for (int laneIndex = 0; laneIndex < auxiliaryLaneCount; laneIndex++)
        {
            hostSegments[laneIndex] = random.Next(0, segmentCount);
        }

        return hostSegments;
    }

    private static int[] BuildOfficeHorizontalRoomGapHeights(System.Random random, int horizontalCorridorCount)
    {
        if (horizontalCorridorCount <= 1)
        {
            return Array.Empty<int>();
        }

        int[] roomGapHeights = new int[horizontalCorridorCount - 1];

        for (int index = 0; index < roomGapHeights.Length; index++)
        {
            roomGapHeights[index] = random.Next(5, 9);
        }

        return roomGapHeights;
    }

    private static int CalculateOfficeCentralBandHeight(int corridorWidth, IReadOnlyList<int> roomGapHeights)
    {
        int height = corridorWidth;

        if (roomGapHeights == null)
        {
            return height;
        }

        for (int index = 0; index < roomGapHeights.Count; index++)
        {
            height += roomGapHeights[index] + corridorWidth;
        }

        return height;
    }

    private static List<int> BuildOfficeHorizontalLaneBottoms(int bandBottomY, int corridorWidth, IReadOnlyList<int> roomGapHeights)
    {
        int laneCount = (roomGapHeights?.Count ?? 0) + 1;
        List<int> laneBottoms = new(laneCount);
        int currentBottomY = bandBottomY;

        for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            laneBottoms.Add(currentBottomY);

            if (laneIndex < laneCount - 1)
            {
                currentBottomY += corridorWidth + roomGapHeights[laneIndex];
            }
        }

        return laneBottoms;
    }

    private static List<OfficeCrossCorridorSpan> AddOfficeCrossCorridors(
        System.Random random,
        GeneratedMapData data,
        List<OfficeCrossCorridorCandidate> candidates,
        int gateDividerX,
        int corridorWidth,
        out HashSet<int> selectedXs)
    {
        selectedXs = new HashSet<int>();
        List<OfficeCrossCorridorSpan> spans = new();

        if (candidates == null || candidates.Count == 0)
        {
            return spans;
        }

        List<OfficeCrossCorridorCandidate> eligible = new();

        foreach (OfficeCrossCorridorCandidate candidate in candidates)
        {
            if (Mathf.Abs(candidate.X - gateDividerX) <= 3)
            {
                continue;
            }

            eligible.Add(candidate);
        }

        if (eligible.Count == 0)
        {
            return spans;
        }

        Shuffle(random, eligible);
        int targetCount = Mathf.Min(random.Next(2, 4), eligible.Count);
        List<OfficeCrossCorridorCandidate> selected = new();

        foreach (OfficeCrossCorridorCandidate candidate in eligible)
        {
            bool overlaps = false;

            foreach (OfficeCrossCorridorCandidate selectedCandidate in selected)
            {
                if (Mathf.Abs(selectedCandidate.X - candidate.X) < 9)
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
            {
                continue;
            }

            selected.Add(candidate);

            if (selected.Count >= targetCount)
            {
                break;
            }
        }

        if (selected.Count == 0)
        {
            selected.Add(eligible[0]);
        }

        foreach (OfficeCrossCorridorCandidate candidate in selected)
        {
            selectedXs.Add(candidate.X);
            spans.Add(CarveOfficeCrossCorridor(random, data, candidate, corridorWidth));
        }

        return spans;
    }

    private static OfficeCrossCorridorSpan CarveOfficeCrossCorridor(
        System.Random random,
        GeneratedMapData data,
        OfficeCrossCorridorCandidate candidate,
        int corridorWidth)
    {
        int crossCorridorWidth = random.NextDouble() < 0.35d ? 3 : 2;
        int corridorLeftX = crossCorridorWidth == 3
            ? candidate.X - 1
            : (random.NextDouble() < 0.5d ? candidate.X - 1 : candidate.X);
        CarveVerticalCorridor(data.GroundCells, candidate.MinY, candidate.MaxY, corridorLeftX, crossCorridorWidth);
        return new OfficeCrossCorridorSpan(corridorLeftX, corridorLeftX + crossCorridorWidth - 1, candidate.MinY, candidate.MaxY);
    }

    private static void TryAddCrossCorridorDoor(
        System.Random random,
        GeneratedMapData data,
        int wallX,
        int minY,
        int maxY,
        int roomOffsetX)
    {
        if (maxY - minY < 1)
        {
            return;
        }

        int startY = maxY - minY < 3
            ? minY
            : random.Next(minY + 1, maxY);

        Vector3Int[] doorCells =
        {
            new(wallX, startY, 0),
            new(wallX, startY + 1, 0)
        };

        int roomId = FindRoomIdContainingCell(data, new Vector3Int(wallX + roomOffsetX, startY, 0), new Vector3Int(wallX + roomOffsetX, startY + 1, 0));
        AddTraversableDoorGroup(
            data,
            requiresKey: false,
            roomId >= 0 ? new[] { roomId } : Array.Empty<int>(),
            doorCells);
    }

    private static void AddOfficeInterRoomDoors(
        System.Random random,
        GeneratedMapData data,
        int gateDividerX,
        HashSet<int> blockedDividerXs)
    {
        for (int leftRoomId = 0; leftRoomId < data.Rooms.Count; leftRoomId++)
        {
            for (int rightRoomId = leftRoomId + 1; rightRoomId < data.Rooms.Count; rightRoomId++)
            {
                if (!TryGetSharedVerticalDivider(data.Rooms[leftRoomId], data.Rooms[rightRoomId], out int dividerX, out int overlapMinY, out int overlapMaxY))
                {
                    continue;
                }

                if (dividerX == gateDividerX || (blockedDividerXs != null && blockedDividerXs.Contains(dividerX)))
                {
                    continue;
                }

                if (random.NextDouble() > 0.42d)
                {
                    continue;
                }

                if (!TryBuildInterRoomDoorCells(random, data, dividerX, overlapMinY, overlapMaxY, out Vector3Int[] doorCells))
                {
                    continue;
                }

                AddTraversableDoorGroup(data, requiresKey: false, new[] { leftRoomId, rightRoomId }, doorCells);
            }
        }
    }

    private static void AddOfficeCentralBandRooms(
        System.Random random,
        GeneratedMapData data,
        List<OfficeCentralBandSegment> segments,
        List<OfficeCrossCorridorSpan> crossCorridorSpans,
        int gateDividerX,
        int corridorWidth)
    {
        if (segments == null || segments.Count == 0)
        {
            return;
        }

        foreach (OfficeCentralBandSegment segment in segments)
        {
            List<OfficeCentralRoomRow> roomRows = BuildOfficeCentralRoomRows(segment, corridorWidth);

            if (roomRows.Count == 0)
            {
                continue;
            }

            List<OfficeCentralRoomColumn> roomColumns = BuildOfficeCentralRoomColumns(segment, crossCorridorSpans, gateDividerX);

            if (roomColumns.Count == 0)
            {
                continue;
            }

            foreach (OfficeCentralRoomRow roomRow in roomRows)
            {
                foreach (OfficeCentralRoomColumn roomColumn in roomColumns)
                {
                    if (roomColumn.Width < 5 || roomRow.Height < 5)
                    {
                        continue;
                    }

                    RectInt roomBounds = new(
                        roomColumn.MinX,
                        roomRow.MinY,
                        roomColumn.Width,
                        roomRow.Height);

                    FillRect(data.GroundCells, roomBounds.xMin, roomBounds.xMax - 1, roomBounds.yMin, roomBounds.yMax - 1);

                    GeneratedZoneType zoneType = GetOfficeCentralBandZoneType(random, gateDividerX, roomBounds);
                    RoomData centralRoom = AddZoneRoom(data, roomBounds, zoneType);
                    int roomId = data.ZonePlans.Count - 1;

                    AddOfficeCentralRoomBoundariesAndDoors(random, data, centralRoom, roomId, roomRow, roomColumn);
                }
            }
        }
    }

    private static List<OfficeCentralRoomRow> BuildOfficeCentralRoomRows(OfficeCentralBandSegment segment, int corridorWidth)
    {
        List<OfficeCentralRoomRow> rows = new();
        int[] laneBottoms = segment.LaneBottoms ?? Array.Empty<int>();
        bool[] activeLanes = segment.ActiveLanes ?? Array.Empty<bool>();

        if (laneBottoms.Length == 0)
        {
            return rows;
        }

        int currentRowMinY = laneBottoms[0] + corridorWidth;
        int lastActiveLaneIndex = 0;

        for (int laneIndex = 1; laneIndex < laneBottoms.Length; laneIndex++)
        {
            bool laneIsActive = laneIndex < activeLanes.Length && activeLanes[laneIndex];

            if (!laneIsActive)
            {
                continue;
            }

            int rowMaxY = laneBottoms[laneIndex] - 1;

            if (rowMaxY - currentRowMinY + 1 >= 5)
            {
                rows.Add(new OfficeCentralRoomRow(currentRowMinY, rowMaxY, hasBottomCorridor: true, hasTopCorridor: true));
            }

            currentRowMinY = laneBottoms[laneIndex] + corridorWidth;
            lastActiveLaneIndex = laneIndex;
        }

        int trailingRowMaxY = laneBottoms[laneBottoms.Length - 1] + corridorWidth - 1;

        if (lastActiveLaneIndex < laneBottoms.Length - 1)
        {
            trailingRowMaxY = laneBottoms[laneBottoms.Length - 1] + corridorWidth - 1;
        }

        if (trailingRowMaxY - currentRowMinY + 1 >= 5)
        {
            rows.Add(new OfficeCentralRoomRow(currentRowMinY, trailingRowMaxY, hasBottomCorridor: true, hasTopCorridor: false));
        }

        return rows;
    }

    private static List<OfficeCentralRoomColumn> BuildOfficeCentralRoomColumns(
        OfficeCentralBandSegment segment,
        List<OfficeCrossCorridorSpan> crossCorridorSpans,
        int gateDividerX)
    {
        List<OfficeCentralBandBlocker> blockers = new();

        if (crossCorridorSpans != null)
        {
            foreach (OfficeCrossCorridorSpan span in crossCorridorSpans)
            {
                if (span.RightX < segment.StartX || span.LeftX > segment.EndX)
                {
                    continue;
                }

                blockers.Add(new OfficeCentralBandBlocker(
                    Mathf.Max(segment.StartX, span.LeftX),
                    Mathf.Min(segment.EndX, span.RightX),
                    actsAsCorridor: true));
            }
        }

        if (gateDividerX >= segment.StartX && gateDividerX <= segment.EndX)
        {
            blockers.Add(new OfficeCentralBandBlocker(gateDividerX, gateDividerX, actsAsCorridor: false));
        }

        blockers.Sort((left, right) => left.MinX.CompareTo(right.MinX));
        List<OfficeCentralRoomColumn> columns = new();
        int currentX = segment.StartX;
        bool hasLeftCorridor = false;

        foreach (OfficeCentralBandBlocker blocker in blockers)
        {
            int columnMaxX = blocker.MinX - 1;

            if (columnMaxX - currentX + 1 >= 5)
            {
                columns.Add(new OfficeCentralRoomColumn(currentX, columnMaxX, hasLeftCorridor, blocker.ActsAsCorridor));
            }

            currentX = blocker.MaxX + 1;
            hasLeftCorridor = blocker.ActsAsCorridor;
        }

        if (segment.EndX - currentX + 1 >= 5)
        {
            columns.Add(new OfficeCentralRoomColumn(currentX, segment.EndX, hasLeftCorridor, hasRightCorridor: false));
        }

        return columns;
    }

    private static GeneratedZoneType GetOfficeCentralBandZoneType(System.Random random, int gateDividerX, RectInt roomBounds)
    {
        if (roomBounds.xMax <= gateDividerX)
        {
            return random.NextDouble() < 0.7d ? GeneratedZoneType.OpenOffice : GeneratedZoneType.Facility;
        }

        if (roomBounds.xMin > gateDividerX)
        {
            return random.NextDouble() < 0.6d ? GeneratedZoneType.Facility : GeneratedZoneType.OpenOffice;
        }

        return random.NextDouble() < 0.5d ? GeneratedZoneType.OpenOffice : GeneratedZoneType.Facility;
    }

    private static void AddOfficeCentralRoomBoundariesAndDoors(
        System.Random random,
        GeneratedMapData data,
        RoomData room,
        int roomId,
        OfficeCentralRoomRow roomRow,
        OfficeCentralRoomColumn roomColumn)
    {
        bool openBottomDoor = roomRow.HasBottomCorridor && random.NextDouble() < 0.55d;
        bool openTopDoor = roomRow.HasTopCorridor && (!openBottomDoor || random.NextDouble() < 0.3d);

        if (roomRow.HasBottomCorridor)
        {
            if (openBottomDoor)
            {
                Vector2Int bottomDoorSpan = GetRandomDoorSpan(random, room);
                AddHorizontalRoomBoundary(data.ForcedWallCells, room, isTopSide: true, bottomDoorSpan);
                RegisterDoorGroup(data, requiresKey: false, new[] { roomId }, GetBoundaryDoorCells(room, isTopSide: true, bottomDoorSpan));
            }
            else
            {
                AddHorizontalRoomBoundary(data.ForcedWallCells, room, isTopSide: true);
            }
        }

        if (roomRow.HasTopCorridor)
        {
            if (openTopDoor)
            {
                Vector2Int topDoorSpan = GetRandomDoorSpan(random, room);
                AddHorizontalRoomBoundary(data.ForcedWallCells, room, isTopSide: false, topDoorSpan);
                RegisterDoorGroup(data, requiresKey: false, new[] { roomId }, GetBoundaryDoorCells(room, isTopSide: false, topDoorSpan));
            }
            else
            {
                AddHorizontalRoomBoundary(data.ForcedWallCells, room, isTopSide: false);
            }
        }

        if (roomColumn.HasLeftCorridor)
        {
            if (TryGetRandomVerticalDoorSpan(random, room.Bounds, out Vector2Int leftDoorSpan) && random.NextDouble() < 0.35d)
            {
                AddVerticalRoomBoundary(data.ForcedWallCells, room.Bounds, isLeftSide: true, leftDoorSpan);
                RegisterDoorGroup(data, requiresKey: false, new[] { roomId }, GetVerticalBoundaryDoorCells(room.Bounds, isLeftSide: true, leftDoorSpan));
            }
            else
            {
                AddVerticalRoomBoundary(data.ForcedWallCells, room.Bounds, isLeftSide: true);
            }
        }

        if (roomColumn.HasRightCorridor)
        {
            if (TryGetRandomVerticalDoorSpan(random, room.Bounds, out Vector2Int rightDoorSpan) && random.NextDouble() < 0.35d)
            {
                AddVerticalRoomBoundary(data.ForcedWallCells, room.Bounds, isLeftSide: false, rightDoorSpan);
                RegisterDoorGroup(data, requiresKey: false, new[] { roomId }, GetVerticalBoundaryDoorCells(room.Bounds, isLeftSide: false, rightDoorSpan));
            }
            else
            {
                AddVerticalRoomBoundary(data.ForcedWallCells, room.Bounds, isLeftSide: false);
            }
        }
    }

    private static bool TryGetSharedVerticalDivider(RoomData firstRoom, RoomData secondRoom, out int dividerX, out int overlapMinY, out int overlapMaxY)
    {
        RoomData leftRoom = firstRoom.Bounds.xMin <= secondRoom.Bounds.xMin ? firstRoom : secondRoom;
        RoomData rightRoom = firstRoom.Bounds.xMin <= secondRoom.Bounds.xMin ? secondRoom : firstRoom;

        dividerX = 0;
        overlapMinY = 0;
        overlapMaxY = 0;

        if (leftRoom.Bounds.xMax != rightRoom.Bounds.xMin - 1)
        {
            return false;
        }

        overlapMinY = Mathf.Max(leftRoom.Bounds.yMin + 1, rightRoom.Bounds.yMin + 1);
        overlapMaxY = Mathf.Min(leftRoom.Bounds.yMax - 2, rightRoom.Bounds.yMax - 2);

        if (overlapMaxY - overlapMinY < 1)
        {
            return false;
        }

        dividerX = leftRoom.Bounds.xMax;
        return true;
    }

    private static bool TryBuildInterRoomDoorCells(
        System.Random random,
        GeneratedMapData data,
        int dividerX,
        int overlapMinY,
        int overlapMaxY,
        out Vector3Int[] doorCells)
    {
        List<int> validStarts = new();

        for (int startY = overlapMinY; startY <= overlapMaxY - 1; startY++)
        {
            Vector3Int lowerDoorCell = new(dividerX, startY, 0);
            Vector3Int upperDoorCell = new(dividerX, startY + 1, 0);

            if (data.DoorCells.Contains(lowerDoorCell) || data.DoorCells.Contains(upperDoorCell))
            {
                continue;
            }

            bool hasLeftAccess = data.GroundCells.Contains(new Vector3Int(dividerX - 1, startY, 0))
                && data.GroundCells.Contains(new Vector3Int(dividerX - 1, startY + 1, 0));
            bool hasRightAccess = data.GroundCells.Contains(new Vector3Int(dividerX + 1, startY, 0))
                && data.GroundCells.Contains(new Vector3Int(dividerX + 1, startY + 1, 0));

            if (!hasLeftAccess || !hasRightAccess)
            {
                continue;
            }

            validStarts.Add(startY);
        }

        if (validStarts.Count == 0)
        {
            doorCells = Array.Empty<Vector3Int>();
            return false;
        }

        int selectedStartY = validStarts[random.Next(0, validStarts.Count)];
        doorCells = new[]
        {
            new Vector3Int(dividerX, selectedStartY, 0),
            new Vector3Int(dividerX, selectedStartY + 1, 0)
        };
        return true;
    }

    private static bool AddOfficeMixedExtensions(System.Random random, GeneratedMapData data, OfficeMixedStyle style)
    {
        int addedWingCount = 0;
        int gateX = data.MainDoorCells.Length > 0 ? data.MainDoorCells[0].x : 0;
        int requiredWingCount = style switch
        {
            OfficeMixedStyle.WingHeavy => 3,
            OfficeMixedStyle.ChainHeavy => 2,
            _ => 2
        };

        if (TryAddOfficeExteriorWing(
                random,
                data,
                fromTop: true,
                requireLeftOfGate: true,
                primaryZoneType: GeneratedZoneType.OpenOffice,
                secondaryZoneType: GeneratedZoneType.Facility,
                gateX,
                style))
        {
            addedWingCount++;
        }

        if (TryAddOfficeExteriorWing(
                random,
                data,
                fromTop: false,
                requireLeftOfGate: false,
                primaryZoneType: GeneratedZoneType.Facility,
                secondaryZoneType: GeneratedZoneType.OpenOffice,
                gateX,
                style))
        {
            addedWingCount++;
        }

        double thirdWingChance = style switch
        {
            OfficeMixedStyle.WingHeavy => 0.95d,
            OfficeMixedStyle.ChainHeavy => 0.8d,
            _ => 0.65d
        };

        if (random.NextDouble() < thirdWingChance
            && TryAddOfficeExteriorWing(
                random,
                data,
                fromTop: random.NextDouble() < 0.5d,
                requireLeftOfGate: random.NextDouble() < 0.5d,
                primaryZoneType: GeneratedZoneType.Facility,
                secondaryZoneType: GeneratedZoneType.Facility,
                gateX,
                style))
        {
            addedWingCount++;
        }

        if (style == OfficeMixedStyle.WingHeavy
            && random.NextDouble() < 0.55d
            && TryAddOfficeExteriorWing(
                random,
                data,
                fromTop: random.NextDouble() < 0.5d,
                requireLeftOfGate: random.NextDouble() < 0.5d,
                primaryZoneType: GeneratedZoneType.OpenOffice,
                secondaryZoneType: GeneratedZoneType.Facility,
                gateX,
                style))
        {
            addedWingCount++;
        }

        return addedWingCount >= requiredWingCount;
    }

    private static bool TryAddOfficeExteriorWing(
        System.Random random,
        GeneratedMapData data,
        bool fromTop,
        bool requireLeftOfGate,
        GeneratedZoneType primaryZoneType,
        GeneratedZoneType secondaryZoneType,
        int gateX,
        OfficeMixedStyle style)
    {
        List<ZonePlan> candidates = new();

        foreach (ZonePlan zonePlan in data.ZonePlans)
        {
            bool matchesRow = fromTop ? zonePlan.Room.CenterCell.y > 0 : zonePlan.Room.CenterCell.y < 0;
            bool matchesSide = requireLeftOfGate ? zonePlan.Room.CenterCell.x < gateX : zonePlan.Room.CenterCell.x > gateX;

            if (!matchesRow || !matchesSide)
            {
                continue;
            }

            if (zonePlan.ZoneType == GeneratedZoneType.Start
                || zonePlan.ZoneType == GeneratedZoneType.Security
                || zonePlan.ZoneType == GeneratedZoneType.Exit)
            {
                continue;
            }

            candidates.Add(zonePlan);
        }

        Shuffle(random, candidates);

        foreach (ZonePlan anchorZone in candidates)
        {
            if (TryBuildExteriorWingFromAnchor(random, data, anchorZone.Room, fromTop, primaryZoneType, secondaryZoneType, style))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildExteriorWingFromAnchor(
        System.Random random,
        GeneratedMapData data,
        RoomData anchorRoom,
        bool fromTop,
        GeneratedZoneType primaryZoneType,
        GeneratedZoneType secondaryZoneType,
        OfficeMixedStyle style)
    {
        int corridorLeftX = Mathf.Clamp(anchorRoom.CenterCell.x - 1, anchorRoom.Bounds.xMin + 1, anchorRoom.Bounds.xMax - 2);
        int corridorWidth = 2;
        int corridorLength = style switch
        {
            OfficeMixedStyle.WingHeavy => random.Next(6, 10),
            OfficeMixedStyle.ChainHeavy => random.Next(5, 8),
            _ => random.Next(4, 8)
        };
        int roomWidth = style == OfficeMixedStyle.WingHeavy ? random.Next(9, 13) : random.Next(8, 12);
        int roomHeight = style == OfficeMixedStyle.WingHeavy ? random.Next(8, 11) : random.Next(7, 10);
        int primaryRoomX = corridorLeftX - random.Next(2, Mathf.Max(3, roomWidth - 1));
        int primaryRoomY = fromTop
            ? anchorRoom.Bounds.yMax + corridorLength
            : anchorRoom.Bounds.yMin - corridorLength - roomHeight;
        RectInt primaryRoom = new(primaryRoomX, primaryRoomY, roomWidth, roomHeight);

        if (OverlapsExistingRoom(primaryRoom, data.Rooms))
        {
            return false;
        }

        if (fromTop)
        {
            FillRect(data.GroundCells, corridorLeftX, corridorLeftX + corridorWidth - 1, anchorRoom.Bounds.yMax - 2, primaryRoom.yMin + 1);
        }
        else
        {
            FillRect(data.GroundCells, corridorLeftX, corridorLeftX + corridorWidth - 1, primaryRoom.yMax - 2, anchorRoom.Bounds.yMin + 1);
        }

        FillRect(data.GroundCells, primaryRoom.xMin, primaryRoom.xMax - 1, primaryRoom.yMin, primaryRoom.yMax - 1);
        AddZoneRoom(data, primaryRoom, primaryZoneType);

        RectInt sideRoom;
        bool createdSideRoom = false;

        if (random.NextDouble() < (style == OfficeMixedStyle.Balanced ? 0.85d : 1d))
        {
            createdSideRoom = TryAddExteriorWingSideRoom(random, data, primaryRoom, secondaryZoneType, style, out sideRoom);

            if (createdSideRoom && style == OfficeMixedStyle.ChainHeavy && random.NextDouble() < 0.9d)
            {
                TryAddExteriorWingSideRoom(random, data, sideRoom, secondaryZoneType, style, out _);
            }
        }

        return true;
    }

    private static bool TryAddExteriorWingSideRoom(
        System.Random random,
        GeneratedMapData data,
        RectInt primaryRoom,
        GeneratedZoneType zoneType,
        OfficeMixedStyle style,
        out RectInt createdRoom)
    {
        createdRoom = default;
        bool branchRight = random.NextDouble() < 0.5d;
        int corridorWidth = 2;
        int corridorY = Mathf.Clamp(primaryRoom.yMin + (primaryRoom.height / 2) - 1, primaryRoom.yMin + 1, primaryRoom.yMax - 3);
        int corridorLength = style == OfficeMixedStyle.ChainHeavy ? random.Next(4, 7) : random.Next(3, 6);
        int roomWidth = style == OfficeMixedStyle.WingHeavy ? random.Next(8, 12) : random.Next(7, 11);
        int roomHeight = style == OfficeMixedStyle.WingHeavy ? random.Next(7, 10) : random.Next(6, 9);
        int sideRoomX = branchRight
            ? primaryRoom.xMax + corridorLength
            : primaryRoom.xMin - corridorLength - roomWidth;
        int sideRoomY = corridorY - random.Next(1, Mathf.Max(2, roomHeight - 2));
        RectInt sideRoom = new(sideRoomX, sideRoomY, roomWidth, roomHeight);

        if (OverlapsExistingRoom(sideRoom, data.Rooms))
        {
            return false;
        }

        if (branchRight)
        {
            FillRect(data.GroundCells, primaryRoom.xMax - 2, sideRoom.xMin + 1, corridorY, corridorY + corridorWidth - 1);
        }
        else
        {
            FillRect(data.GroundCells, sideRoom.xMax - 2, primaryRoom.xMin + 1, corridorY, corridorY + corridorWidth - 1);
        }

        FillRect(data.GroundCells, sideRoom.xMin, sideRoom.xMax - 1, sideRoom.yMin, sideRoom.yMax - 1);
        AddZoneRoom(data, sideRoom, zoneType);
        createdRoom = sideRoom;
        return true;
    }

    private static void PopulateFromBlueprint(GeneratedMapData data, GeneratedMapBlueprint blueprint)
    {
        data.GroundCells.Clear();
        data.ForcedWallCells.Clear();
        data.DoorCells.Clear();
        data.Rooms.Clear();
        data.ZonePlans.Clear();
        data.Zones.Clear();
        data.RoomRecords.Clear();
        data.RouteRecords.Clear();
        data.DoorGroups.Clear();
        data.CoverProps.Clear();
        data.ReservedCells.Clear();
        data.PropBlockedCells.Clear();

        foreach (Vector3Int groundCell in blueprint.GroundCells)
        {
            data.GroundCells.Add(groundCell);
        }

        foreach (Vector3Int wallCell in blueprint.ForcedWallCells)
        {
            data.ForcedWallCells.Add(wallCell);
        }

        foreach (Vector3Int doorCell in blueprint.DoorCells)
        {
            data.DoorCells.Add(doorCell);
        }

        data.Zones.AddRange(blueprint.Zones);
        data.RoomRecords.AddRange(blueprint.Rooms);
        data.RouteRecords.AddRange(blueprint.Routes);
        data.DoorGroups.AddRange(blueprint.DoorGroups);
        data.CoverProps.AddRange(blueprint.CoverProps);
        data.PlayerStartCell = blueprint.PlayerStartCell;
        data.KeyCell = blueprint.KeyCell;
        data.BatteryCell = blueprint.BatteryCell;
        data.ExitCell = blueprint.ExitCell;
        data.PatrolSpawnCell = blueprint.PatrolSpawnCell;
        data.GlassPanelCell = blueprint.GlassPanelCell;
        data.SafeRoomCell = blueprint.SafeRoomCell;
        data.MainDoorCells = blueprint.MainDoorCells ?? Array.Empty<Vector3Int>();
        data.DangerCells = blueprint.DangerCellSet.Count > 0 ? new List<Vector3Int>(blueprint.DangerCellSet).ToArray() : Array.Empty<Vector3Int>();
    }

    private static RoomData AddZoneRoom(GeneratedMapData data, RectInt bounds, GeneratedZoneType zoneType)
    {
        RoomData room = new(bounds);
        data.Rooms.Add(room);
        data.ZonePlans.Add(new ZonePlan(room, zoneType));
        return room;
    }

    private static Vector2Int GetDoorSpan(RoomData room, bool preferLeftSide = false, bool preferRightSide = false)
    {
        int startX;

        if (preferLeftSide)
        {
            startX = room.Bounds.xMin + 1;
        }
        else if (preferRightSide)
        {
            startX = room.Bounds.xMax - 3;
        }
        else
        {
            startX = room.CenterCell.x - 1;
        }

        startX = Mathf.Clamp(startX, room.Bounds.xMin + 1, room.Bounds.xMax - 3);
        return new Vector2Int(startX, startX + 1);
    }

    private static Vector2Int GetRandomDoorSpan(System.Random random, RoomData room, bool preferLeftSide = false, bool preferRightSide = false)
    {
        int minStart = room.Bounds.xMin + 1;
        int maxStart = room.Bounds.xMax - 3;

        if (preferLeftSide)
        {
            maxStart = Mathf.Min(maxStart, room.Bounds.xMin + Mathf.Max(1, room.Bounds.width / 2));
        }
        else if (preferRightSide)
        {
            minStart = Mathf.Max(minStart, room.Bounds.xMin + Mathf.Max(1, room.Bounds.width / 2) - 1);
        }

        if (maxStart < minStart)
        {
            return GetDoorSpan(room, preferLeftSide, preferRightSide);
        }

        int startX = random.Next(minStart, maxStart + 1);
        return new Vector2Int(startX, startX + 1);
    }

    private static Vector3Int[] GetBoundaryDoorCells(RoomData room, bool isTopSide, Vector2Int doorSpan)
    {
        int boundaryY = isTopSide ? room.Bounds.yMin : room.Bounds.yMax - 1;
        return new[]
        {
            new Vector3Int(doorSpan.x, boundaryY, 0),
            new Vector3Int(doorSpan.y, boundaryY, 0)
        };
    }

    private static Vector3Int[] GetVerticalBoundaryDoorCells(RectInt roomBounds, bool isLeftSide, Vector2Int doorSpan)
    {
        int boundaryX = isLeftSide ? roomBounds.xMin : roomBounds.xMax - 1;
        return new[]
        {
            new Vector3Int(boundaryX, doorSpan.x, 0),
            new Vector3Int(boundaryX, doorSpan.y, 0)
        };
    }

    private static void AddHorizontalRoomBoundary(HashSet<Vector3Int> forcedWalls, RoomData room, bool isTopSide, Vector2Int doorSpan)
    {
        int boundaryY = isTopSide ? room.Bounds.yMin : room.Bounds.yMax - 1;

        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            if (x == doorSpan.x || x == doorSpan.y)
            {
                continue;
            }

            forcedWalls.Add(new Vector3Int(x, boundaryY, 0));
        }
    }

    private static void AddHorizontalRoomBoundary(HashSet<Vector3Int> forcedWalls, RoomData room, bool isTopSide)
    {
        int boundaryY = isTopSide ? room.Bounds.yMin : room.Bounds.yMax - 1;

        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            forcedWalls.Add(new Vector3Int(x, boundaryY, 0));
        }
    }

    private static void AddVerticalRoomBoundary(HashSet<Vector3Int> forcedWalls, RectInt roomBounds, bool isLeftSide)
    {
        int boundaryX = isLeftSide ? roomBounds.xMin : roomBounds.xMax - 1;

        for (int y = roomBounds.yMin; y < roomBounds.yMax; y++)
        {
            forcedWalls.Add(new Vector3Int(boundaryX, y, 0));
        }
    }

    private static void AddVerticalRoomBoundary(HashSet<Vector3Int> forcedWalls, RectInt roomBounds, bool isLeftSide, Vector2Int doorSpan)
    {
        int boundaryX = isLeftSide ? roomBounds.xMin : roomBounds.xMax - 1;

        for (int y = roomBounds.yMin; y < roomBounds.yMax; y++)
        {
            if (y == doorSpan.x || y == doorSpan.y)
            {
                continue;
            }

            forcedWalls.Add(new Vector3Int(boundaryX, y, 0));
        }
    }

    private static bool TryGetRandomVerticalDoorSpan(System.Random random, RectInt roomBounds, out Vector2Int doorSpan)
    {
        int minStart = roomBounds.yMin + 1;
        int maxStart = roomBounds.yMax - 3;

        if (maxStart < minStart)
        {
            doorSpan = default;
            return false;
        }

        int startY = random.Next(minStart, maxStart + 1);
        doorSpan = new Vector2Int(startY, startY + 1);
        return true;
    }

    private static void RegisterDoorGroup(GeneratedMapData data, bool requiresKey, int[] connectedRoomIds, params Vector3Int[] doorCells)
    {
        if (doorCells == null || doorCells.Length == 0)
        {
            return;
        }

        int doorGroupId = data.DoorGroups.Count;
        data.DoorGroups.Add(new GeneratedDoorGroupData(doorGroupId, requiresKey, doorCells, connectedRoomIds ?? Array.Empty<int>()));

        foreach (Vector3Int doorCell in doorCells)
        {
            data.DoorCells.Add(doorCell);
        }

        if (connectedRoomIds == null)
        {
            return;
        }

        foreach (int roomId in connectedRoomIds)
        {
            if (roomId < 0)
            {
                continue;
            }

            if (!data.RoomDoorCellsById.TryGetValue(roomId, out HashSet<Vector3Int> roomDoorCells))
            {
                roomDoorCells = new HashSet<Vector3Int>();
                data.RoomDoorCellsById[roomId] = roomDoorCells;
            }

            foreach (Vector3Int doorCell in doorCells)
            {
                roomDoorCells.Add(doorCell);
            }
        }
    }

    private static void AddTraversableDoorGroup(GeneratedMapData data, bool requiresKey, int[] connectedRoomIds, params Vector3Int[] doorCells)
    {
        if (doorCells == null || doorCells.Length == 0)
        {
            return;
        }

        foreach (Vector3Int doorCell in doorCells)
        {
            data.ForcedWallCells.Remove(doorCell);
            data.GroundCells.Add(doorCell);
        }

        RegisterDoorGroup(data, requiresKey, connectedRoomIds, doorCells);
    }

    private static void AddRoomBoundaryWithDoor(GeneratedMapData data, int roomId, RoomData room, bool isTopSide, Vector2Int doorSpan)
    {
        AddHorizontalRoomBoundary(data.ForcedWallCells, room, isTopSide, doorSpan);
        RegisterDoorGroup(data, requiresKey: false, new[] { roomId }, GetBoundaryDoorCells(room, isTopSide, doorSpan));
    }

    private static void AddVerticalWallLine(HashSet<Vector3Int> forcedWalls, int x, int minY, int maxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            forcedWalls.Add(new Vector3Int(x, y, 0));
        }
    }

    private static List<int> BuildRoomWidths(System.Random random, int roomCount, int totalWidth, int minWidth, int maxWidth)
    {
        List<int> widths = new(roomCount);
        HashSet<int> featuredRoomIndexes = new();
        int featuredRoomCount = Mathf.Clamp(roomCount / 3, 2, 4);

        for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
        {
            widths.Add(minWidth);
        }

        while (featuredRoomIndexes.Count < featuredRoomCount)
        {
            featuredRoomIndexes.Add(random.Next(0, roomCount));
        }

        int remainingWidth = totalWidth - (roomCount * minWidth);

        while (remainingWidth > 0)
        {
            int targetIndex = random.NextDouble() < 0.45d
                ? GetRandomFeaturedRoomIndex(random, featuredRoomIndexes)
                : random.Next(0, roomCount);

            if (widths[targetIndex] >= maxWidth)
            {
                continue;
            }

            widths[targetIndex]++;
            remainingWidth--;
        }

        return widths;
    }

    private static int GetRandomFeaturedRoomIndex(System.Random random, HashSet<int> featuredRoomIndexes)
    {
        int target = random.Next(0, featuredRoomIndexes.Count);
        int current = 0;

        foreach (int roomIndex in featuredRoomIndexes)
        {
            if (current == target)
            {
                return roomIndex;
            }

            current++;
        }

        return 0;
    }

    private static List<int> BuildRoomHeights(System.Random random, int roomCount, int minHeight, int maxHeight)
    {
        List<int> heights = new(roomCount);
        HashSet<int> tallRoomIndexes = new();
        int tallRoomCount = Mathf.Clamp(roomCount / 3, 2, 4);

        while (tallRoomIndexes.Count < tallRoomCount)
        {
            tallRoomIndexes.Add(random.Next(0, roomCount));
        }

        for (int index = 0; index < roomCount; index++)
        {
            int height = tallRoomIndexes.Contains(index)
                ? random.Next(Mathf.Max(minHeight, maxHeight - 5), maxHeight + 1)
                : random.Next(minHeight, maxHeight + 1);

            if (random.NextDouble() < 0.3d)
            {
                height = Mathf.Clamp(height + random.Next(2, 4), minHeight, maxHeight);
            }
            else if (random.NextDouble() < 0.18d)
            {
                height = Mathf.Clamp(height - random.Next(1, 3), minHeight, maxHeight);
            }

            heights.Add(height);
        }

        return heights;
    }

    private static int FindRoomIdContainingCell(GeneratedMapData data, params Vector3Int[] cells)
    {
        if (cells == null || cells.Length == 0)
        {
            return -1;
        }

        for (int roomId = 0; roomId < data.Rooms.Count; roomId++)
        {
            RectInt bounds = data.Rooms[roomId].Bounds;

            foreach (Vector3Int cell in cells)
            {
                if (bounds.Contains(new Vector2Int(cell.x, cell.y)))
                {
                    return roomId;
                }
            }
        }

        return -1;
    }

    private static void ApplyOfficeRoomShapeVariation(System.Random random, HashSet<Vector3Int> groundCells, RectInt roomBounds, bool isTopRoom)
    {
        if (roomBounds.width < 6 || roomBounds.height < 6)
        {
            return;
        }

        if (random.NextDouble() < 0.4d)
        {
            CarveCornerNotch(random, groundCells, roomBounds, isTopRoom);
        }

        if (roomBounds.width >= 8 && roomBounds.height >= 7 && random.NextDouble() < 0.2d)
        {
            CarveSideInset(random, groundCells, roomBounds);
        }
    }

    private static void CarveCornerNotch(System.Random random, HashSet<Vector3Int> groundCells, RectInt roomBounds, bool isTopRoom)
    {
        int maxNotchWidth = Mathf.Min(3, roomBounds.width - 3);
        int maxNotchHeight = Mathf.Min(3, roomBounds.height - 3);

        if (maxNotchWidth < 2 || maxNotchHeight < 2)
        {
            return;
        }

        int notchWidth = random.Next(2, maxNotchWidth + 1);
        int notchHeight = random.Next(2, maxNotchHeight + 1);
        bool carveLeft = random.NextDouble() < 0.5d;
        int notchX = carveLeft ? roomBounds.xMin : roomBounds.xMax - notchWidth;
        int notchY = isTopRoom ? roomBounds.yMax - notchHeight : roomBounds.yMin;
        RemoveGroundRect(groundCells, notchX, notchX + notchWidth - 1, notchY, notchY + notchHeight - 1);
    }

    private static void CarveSideInset(System.Random random, HashSet<Vector3Int> groundCells, RectInt roomBounds)
    {
        int insetWidth = random.Next(2, Mathf.Min(4, roomBounds.width - 2));
        int insetHeight = random.Next(2, Mathf.Min(4, roomBounds.height - 2));
        bool fromLeft = random.NextDouble() < 0.5d;
        int insetX = fromLeft ? roomBounds.xMin : roomBounds.xMax - insetWidth;
        int insetY = roomBounds.yMin + random.Next(1, Mathf.Max(2, roomBounds.height - insetHeight));

        if (insetY + insetHeight >= roomBounds.yMax)
        {
            insetY = roomBounds.yMax - insetHeight - 1;
        }

        RemoveGroundRect(groundCells, insetX, insetX + insetWidth - 1, insetY, insetY + insetHeight - 1);
    }

    private static void RemoveGroundRect(HashSet<Vector3Int> groundCells, int minX, int maxX, int minY, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                groundCells.Remove(new Vector3Int(x, y, 0));
            }
        }
    }

    private static GeneratedZoneType GetBottomRowZoneType(int roomIndex, int roomCount)
    {
        if (roomIndex == 0)
        {
            return GeneratedZoneType.Start;
        }

        if (roomIndex == 2)
        {
            return GeneratedZoneType.Utility;
        }

        if (roomIndex == roomCount - 1)
        {
            return GeneratedZoneType.Exit;
        }

        return roomIndex % 3 == 0 ? GeneratedZoneType.Facility : GeneratedZoneType.OpenOffice;
    }

    private static GeneratedZoneType GetTopRowZoneType(int roomIndex, int roomCount, int gateAfterIndex)
    {
        if (roomIndex == Mathf.Clamp(gateAfterIndex, 0, roomCount - 1))
        {
            return GeneratedZoneType.Security;
        }

        if (roomIndex == roomCount - 2)
        {
            return GeneratedZoneType.Facility;
        }

        return roomIndex % 2 == 0 ? GeneratedZoneType.OpenOffice : GeneratedZoneType.Facility;
    }

    private static bool TryPlaceRooms(System.Random random, int targetRoomCount, List<RoomData> rooms)
    {
        rooms.Clear();
        const int minRoomWidth = 7;
        const int maxRoomWidth = 11;
        const int minRoomHeight = 6;
        const int maxRoomHeight = 10;
        const int minX = -26;
        const int maxX = 22;
        const int minY = -15;
        const int maxY = 15;

        for (int attempt = 0; attempt < 240 && rooms.Count < targetRoomCount; attempt++)
        {
            int width = random.Next(minRoomWidth, maxRoomWidth);
            int height = random.Next(minRoomHeight, maxRoomHeight);
            int x = random.Next(minX, maxX - width);
            int y = random.Next(minY, maxY - height);
            RectInt candidate = new(x, y, width, height);

            if (OverlapsExistingRoom(candidate, rooms))
            {
                continue;
            }

            rooms.Add(new RoomData(candidate));
        }

        return rooms.Count >= MinRoomCount;
    }

    private static bool OverlapsExistingRoom(RectInt candidate, List<RoomData> rooms)
    {
        RectInt expandedCandidate = ExpandRect(candidate, RoomSeparationPadding);

        foreach (RoomData room in rooms)
        {
            if (expandedCandidate.Overlaps(ExpandRect(room.Bounds, RoomSeparationPadding)))
            {
                return true;
            }
        }

        return false;
    }

    private static RectInt ExpandRect(RectInt rect, int padding)
    {
        return new RectInt(rect.xMin - padding, rect.yMin - padding, rect.width + (padding * 2), rect.height + (padding * 2));
    }

    private static void AssignZonePlans(System.Random random, GeneratedMapData data)
    {
        data.ZonePlans.Clear();
        int keyRoomIndex = Mathf.Max(1, data.Rooms.Count - 3);
        int batteryRoomIndex = Mathf.Min(2, data.Rooms.Count - 2);
        int exitRoomIndex = data.Rooms.Count - 1;

        for (int roomIndex = 0; roomIndex < data.Rooms.Count; roomIndex++)
        {
            GeneratedZoneType zoneType = roomIndex switch
            {
                0 => GeneratedZoneType.Start,
                _ when roomIndex == exitRoomIndex => GeneratedZoneType.Exit,
                _ when roomIndex == keyRoomIndex => GeneratedZoneType.Security,
                _ when roomIndex == batteryRoomIndex => GeneratedZoneType.Utility,
                _ => random.NextDouble() < 0.55d ? GeneratedZoneType.OpenOffice : GeneratedZoneType.Facility
            };

            data.ZonePlans.Add(new ZonePlan(data.Rooms[roomIndex], zoneType));
        }
    }

    private static void ReserveCriticalCells(GeneratedMapData data)
    {
        data.ReservedCells.Clear();
        ReserveCellCluster(data.ReservedCells, data.PlayerStartCell, 1);
        ReserveCellCluster(data.ReservedCells, data.KeyCell, 1);
        ReserveCellCluster(data.ReservedCells, data.BatteryCell, 1);
        ReserveCellCluster(data.ReservedCells, data.ExitCell, 1);
        ReserveCellCluster(data.ReservedCells, data.PatrolSpawnCell, 1);
        ReserveCellCluster(data.ReservedCells, data.GlassPanelCell, 1);
        ReserveCellCluster(data.ReservedCells, data.SafeRoomCell, 1);

        foreach (Vector3Int doorCell in data.MainDoorCells)
        {
            ReserveCellCluster(data.ReservedCells, doorCell, 1);
        }
    }

    private static void ReserveCellCluster(HashSet<Vector3Int> reservedCells, Vector3Int centerCell, int radius)
    {
        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                reservedCells.Add(new Vector3Int(centerCell.x + offsetX, centerCell.y + offsetY, 0));
            }
        }
    }

    private static void GenerateCoverProps(System.Random random, GeneratedMapData data)
    {
        data.CoverProps.Clear();
        data.PropBlockedCells.Clear();

        foreach (ZonePlan zonePlan in data.ZonePlans)
        {
            List<PropCandidate> candidates = BuildPropCandidates(zonePlan);

            if (candidates.Count == 0)
            {
                continue;
            }

            Shuffle(random, candidates);
            int targetCount = DetermineTargetPropCount(zonePlan);
            int placedCount = 0;

            foreach (PropCandidate candidate in candidates)
            {
                if (placedCount >= targetCount)
                {
                    break;
                }

                if (!CanPlacePropCandidate(data, candidate) || !WouldMaintainCriticalPaths(data, candidate))
                {
                    continue;
                }

                CoverPropPlacement placement = candidate.ToPlacement();
                data.CoverProps.Add(placement);

                foreach (Vector3Int occupiedCell in placement.OccupiedCells)
                {
                    data.PropBlockedCells.Add(occupiedCell);
                }

                placedCount++;
            }
        }
    }

    private static List<PropCandidate> BuildPropCandidates(ZonePlan zonePlan)
    {
        List<PropCandidate> candidates = new();
        RoomData room = zonePlan.Room;

        switch (zonePlan.ZoneType)
        {
            case GeneratedZoneType.Start:
                TryAddCandidate(candidates, room, zonePlan.ZoneType, CoverPropType.Planter, room.Bounds.xMin + 1, room.Bounds.yMax - 2, 1, 1);
                break;
            case GeneratedZoneType.OpenOffice:
                AddOpenOfficeCandidates(candidates, room, zonePlan.ZoneType);
                break;
            case GeneratedZoneType.Facility:
            case GeneratedZoneType.Utility:
                AddFacilityCandidates(candidates, room, zonePlan.ZoneType);
                break;
            case GeneratedZoneType.Security:
            case GeneratedZoneType.Exit:
                AddSecurityCandidates(candidates, room, zonePlan.ZoneType);
                break;
        }

        return candidates;
    }

    private static void AddOpenOfficeCandidates(List<PropCandidate> candidates, RoomData room, GeneratedZoneType zoneType)
    {
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMin + 1, room.Bounds.yMin + 1, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMin + 1, room.Bounds.yMax - 2, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMax - 2, room.Bounds.yMin + 1, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMax - 2, room.Bounds.yMax - 2, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMin + 1, room.Bounds.yMin + 2, 1, 2);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMax - 2, room.Bounds.yMax - 3, 1, 2);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMin + 2, room.CenterCell.y, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMax - 3, room.CenterCell.y - 1, 1, 1);

        if (room.Bounds.width >= 9 && room.Bounds.height >= 8)
        {
            TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.CenterCell.x - 2, room.CenterCell.y, 1, 1);
            TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.CenterCell.x + 1, room.CenterCell.y - 1, 1, 1);
        }
    }

    private static void AddFacilityCandidates(List<PropCandidate> candidates, RoomData room, GeneratedZoneType zoneType)
    {
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.Bounds.xMin + 1, room.Bounds.yMin + 1, 2, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.Bounds.xMin + 1, room.Bounds.yMax - 2, 2, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.Bounds.xMax - 2, room.Bounds.yMin + 1, 1, 2);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.Bounds.xMax - 2, room.Bounds.yMax - 2, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.Bounds.xMin + 2, room.CenterCell.y - 1, 1, 2);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.CenterCell.x, room.Bounds.yMin + 1, 1, 1);

        if (room.Bounds.width >= 9 && room.Bounds.height >= 8)
        {
            TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.CenterCell.x - 2, room.CenterCell.y - 2, 2, 1);
            TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.CenterCell.x + 1, room.CenterCell.y + 1, 1, 2);
        }
    }

    private static void AddSecurityCandidates(List<PropCandidate> candidates, RoomData room, GeneratedZoneType zoneType)
    {
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.Bounds.xMax - 2, room.Bounds.yMax - 2, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.Planter, room.Bounds.xMin + 1, room.Bounds.yMax - 2, 1, 1);
        TryAddCandidate(candidates, room, zoneType, CoverPropType.CrateStack, room.Bounds.xMin + 1, room.Bounds.yMin + 1, 1, 1);
    }

    private static void TryAddCandidate(
        List<PropCandidate> candidates,
        RoomData room,
        GeneratedZoneType zoneType,
        CoverPropType propType,
        int originX,
        int originY,
        int width,
        int height)
    {
        RectInt interior = new(room.Bounds.xMin + 1, room.Bounds.yMin + 1, room.Bounds.width - 2, room.Bounds.height - 2);

        if (width <= 0
            || height <= 0
            || interior.width <= 0
            || interior.height <= 0
            || originX < interior.xMin
            || originY < interior.yMin
            || originX + width > interior.xMax
            || originY + height > interior.yMax)
        {
            return;
        }

        PropCandidate candidate = new(propType, zoneType, new Vector3Int(originX, originY, 0), new Vector2Int(width, height));

        foreach (PropCandidate existingCandidate in candidates)
        {
            if (existingCandidate.OriginCell == candidate.OriginCell
                && existingCandidate.Size == candidate.Size
                && existingCandidate.PropType == candidate.PropType)
            {
                return;
            }
        }

        candidates.Add(candidate);
    }

    private static int DetermineTargetPropCount(ZonePlan zonePlan)
    {
        int area = zonePlan.Room.Bounds.width * zonePlan.Room.Bounds.height;

        return zonePlan.ZoneType switch
        {
            GeneratedZoneType.Start => area >= 60 ? 1 : 0,
            GeneratedZoneType.OpenOffice => Mathf.Clamp(area / 28, 1, 3),
            GeneratedZoneType.Facility => Mathf.Clamp(area / 24, 1, 3),
            GeneratedZoneType.Utility => Mathf.Clamp(area / 26, 1, 2),
            GeneratedZoneType.Security => Mathf.Clamp(area / 48, 0, 1),
            GeneratedZoneType.Exit => Mathf.Clamp(area / 56, 0, 1),
            _ => 0
        };
    }

    private static bool CanPlacePropCandidate(GeneratedMapData data, PropCandidate candidate)
    {
        Vector3Int[] occupiedCells = candidate.GetOccupiedCells();

        foreach (Vector3Int occupiedCell in occupiedCells)
        {
            if (!data.GroundCells.Contains(occupiedCell)
                || data.ForcedWallCells.Contains(occupiedCell)
                || data.ReservedCells.Contains(occupiedCell)
                || data.PropBlockedCells.Contains(occupiedCell))
            {
                return false;
            }
        }

        foreach (Vector3Int occupiedCell in occupiedCells)
        {
            foreach (Vector3Int doorCell in data.MainDoorCells)
            {
                if (Mathf.Abs(occupiedCell.x - doorCell.x) <= 1 && Mathf.Abs(occupiedCell.y - doorCell.y) <= 1)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool WouldMaintainCriticalPaths(GeneratedMapData data, PropCandidate candidate)
    {
        HashSet<Vector3Int> blockedCells = new(data.PropBlockedCells);

        foreach (Vector3Int occupiedCell in candidate.GetOccupiedCells())
        {
            blockedCells.Add(occupiedCell);
        }

        return CanTraverse(data, blockedCells, data.PlayerStartCell, data.KeyCell)
            && CanTraverse(data, blockedCells, data.PlayerStartCell, data.BatteryCell)
            && CanTraverse(data, blockedCells, data.KeyCell, data.ExitCell)
            && CanTraverse(data, blockedCells, data.PatrolSpawnCell, data.MainDoorCells[0]);
    }

    private static bool ValidateCriticalTraversal(GeneratedMapData data)
    {
        return CanTraverse(data, data.PropBlockedCells, data.PlayerStartCell, data.KeyCell)
            && CanTraverse(data, data.PropBlockedCells, data.PlayerStartCell, data.BatteryCell)
            && CanTraverse(data, data.PropBlockedCells, data.KeyCell, data.ExitCell)
            && CanTraverse(data, data.PropBlockedCells, data.PatrolSpawnCell, data.MainDoorCells[0]);
    }

    private static bool CanTraverse(GeneratedMapData data, HashSet<Vector3Int> blockedCells, Vector3Int startCell, Vector3Int goalCell)
    {
        if (!IsTraversableCell(data, blockedCells, startCell) || !IsTraversableCell(data, blockedCells, goalCell))
        {
            return false;
        }

        Queue<Vector3Int> frontier = new();
        HashSet<Vector3Int> visited = new() { startCell };
        frontier.Enqueue(startCell);

        while (frontier.Count > 0)
        {
            Vector3Int currentCell = frontier.Dequeue();

            if (currentCell == goalCell)
            {
                return true;
            }

            foreach (Vector3Int offset in CardinalOffsets)
            {
                Vector3Int neighbor = currentCell + offset;

                if (visited.Contains(neighbor) || !IsTraversableCell(data, blockedCells, neighbor))
                {
                    continue;
                }

                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
            }
        }

        return false;
    }

    private static bool IsTraversableCell(GeneratedMapData data, HashSet<Vector3Int> blockedCells, Vector3Int cellPosition)
    {
        return data.GroundCells.Contains(cellPosition)
            && !data.ForcedWallCells.Contains(cellPosition)
            && !blockedCells.Contains(cellPosition);
    }

    private static void BuildZoneMetadata(GeneratedMapData data)
    {
        data.Zones.Clear();

        foreach (ZonePlan zonePlan in data.ZonePlans)
        {
            List<Vector3Int> zoneDoorCells = new();

            foreach (Vector3Int doorCell in data.DoorCells)
            {
                if (doorCell.y >= zonePlan.Room.Bounds.yMin - 1
                    && doorCell.y <= zonePlan.Room.Bounds.yMax
                    && doorCell.x >= zonePlan.Room.Bounds.xMin - 1
                    && doorCell.x <= zonePlan.Room.Bounds.xMax)
                {
                    zoneDoorCells.Add(doorCell);
                }
            }

            data.Zones.Add(new GeneratedZoneData(
                zonePlan.ZoneType,
                zonePlan.Room.Bounds,
                zonePlan.Room.CenterCell,
                zoneDoorCells.ToArray()));
        }
    }

    private static void CarveConnection(HashSet<Vector3Int> groundCells, RoomData fromRoom, RoomData toRoom, bool buildExitGate, GeneratedMapData data)
    {
        Vector3Int startCell = fromRoom.RightEdgeCenterCell;

        if (buildExitGate)
        {
            int doorBottomY = Mathf.Clamp(toRoom.Bounds.yMin + (toRoom.Bounds.height / 2) - 1, toRoom.Bounds.yMin + 1, toRoom.Bounds.yMax - 3);
            int doorX = toRoom.Bounds.xMin - 1;
            Vector3Int corridorTarget = new(doorX - 1, doorBottomY, 0);

            CarveLShapedCorridor(groundCells, startCell, corridorTarget);
            CarveHorizontalCorridor(groundCells, corridorTarget.x, doorX, doorBottomY, CorridorWidth);
            data.MainDoorCells = new[]
            {
                new Vector3Int(doorX, doorBottomY, 0),
                new Vector3Int(doorX, doorBottomY + 1, 0)
            };
            return;
        }

        Vector3Int endCell = toRoom.LeftEdgeCenterCell;
        CarveLShapedCorridor(groundCells, startCell, endCell);
    }

    private static void CarveLShapedCorridor(HashSet<Vector3Int> groundCells, Vector3Int fromCell, Vector3Int toCell)
    {
        CarveHorizontalCorridor(groundCells, fromCell.x, toCell.x, fromCell.y, CorridorWidth);
        CarveVerticalCorridor(groundCells, fromCell.y, toCell.y, toCell.x, CorridorWidth);
    }

    private static void CarveHorizontalCorridor(HashSet<Vector3Int> groundCells, int startX, int endX, int bottomY, int width)
    {
        int minX = Math.Min(startX, endX);
        int maxX = Math.Max(startX, endX);

        for (int x = minX; x <= maxX; x++)
        {
            for (int offsetY = 0; offsetY < width; offsetY++)
            {
                groundCells.Add(new Vector3Int(x, bottomY + offsetY, 0));
            }
        }
    }

    private static void CarveVerticalCorridor(HashSet<Vector3Int> groundCells, int startY, int endY, int leftX, int width)
    {
        int minY = Math.Min(startY, endY);
        int maxY = Math.Max(startY, endY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int offsetX = 0; offsetX < width; offsetX++)
            {
                groundCells.Add(new Vector3Int(leftX + offsetX, y, 0));
            }
        }
    }

    private static void AssignEncounterCells(GeneratedMapData data)
    {
        RoomData startRoom = FindRoomByZone(data, GeneratedZoneType.Start);
        RoomData securityRoom = FindRoomByZone(data, GeneratedZoneType.Security);
        RoomData utilityRoom = FindRoomByZone(data, GeneratedZoneType.Utility);
        RoomData exitRoom = FindRoomByZone(data, GeneratedZoneType.Exit);
        RoomData openOfficeRoom = FindRoomByZone(data, GeneratedZoneType.OpenOffice);

        data.PlayerStartCell = GetInnerCell(data, startRoom, 1, 1);
        data.KeyCell = GetInnerCell(data, securityRoom, securityRoom.Bounds.width - 3, securityRoom.Bounds.height - 3);
        data.BatteryCell = GetInnerCell(data, utilityRoom, utilityRoom.Bounds.width - 3, 1);
        data.ExitCell = GetInnerCell(data, exitRoom, exitRoom.Bounds.width - 3, exitRoom.Bounds.height / 2);
        data.PatrolSpawnCell = GetInnerCell(data, exitRoom, 2, exitRoom.Bounds.height / 2);
        data.GlassPanelCell = GetInnerCell(data, openOfficeRoom, 1, 1);
    }

    private static RoomData FindRoomByZone(GeneratedMapData data, GeneratedZoneType zoneType)
    {
        foreach (ZonePlan zonePlan in data.ZonePlans)
        {
            if (zonePlan.ZoneType == zoneType)
            {
                return zonePlan.Room;
            }
        }

        return data.Rooms.Count > 0 ? data.Rooms[0] : new RoomData(new RectInt(-2, -2, 4, 4));
    }

    private static Vector3Int GetInnerCell(GeneratedMapData data, RoomData room, int offsetX, int offsetY)
    {
        int clampedX = Mathf.Clamp(room.Bounds.xMin + offsetX, room.Bounds.xMin + 1, room.Bounds.xMax - 2);
        int clampedY = Mathf.Clamp(room.Bounds.yMin + offsetY, room.Bounds.yMin + 1, room.Bounds.yMax - 2);
        Vector3Int preferredCell = new(clampedX, clampedY, 0);

        if (IsUsableInteriorCell(data, preferredCell))
        {
            return preferredCell;
        }

        int maxSearchRadius = Mathf.Max(room.Bounds.width, room.Bounds.height);

        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            for (int x = preferredCell.x - radius; x <= preferredCell.x + radius; x++)
            {
                for (int y = preferredCell.y - radius; y <= preferredCell.y + radius; y++)
                {
                    if (x <= room.Bounds.xMin
                        || x >= room.Bounds.xMax - 1
                        || y <= room.Bounds.yMin
                        || y >= room.Bounds.yMax - 1)
                    {
                        continue;
                    }

                    Vector3Int candidate = new(x, y, 0);

                    if (IsUsableInteriorCell(data, candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return room.CenterCell;
    }

    private static bool IsUsableInteriorCell(GeneratedMapData data, Vector3Int cell)
    {
        return data.GroundCells.Contains(cell)
            && !data.ForcedWallCells.Contains(cell)
            && !data.DoorCells.Contains(cell);
    }

    private static Vector3Int GetOfficeSafeRoomCell(GeneratedMapData data)
    {
        RoomData safeRoom = FindRoomByZone(data, GeneratedZoneType.OpenOffice);
        return GetInnerCell(data, safeRoom, safeRoom.Bounds.width / 2, safeRoom.Bounds.height / 2);
    }

    private static Vector3Int[] BuildOfficeDangerCells(GeneratedMapData data)
    {
        HashSet<Vector3Int> dangerCells = new();
        int gateX = data.MainDoorCells.Length > 0 ? data.MainDoorCells[0].x : 0;
        int minGateY = int.MaxValue;
        int maxGateY = int.MinValue;

        foreach (Vector3Int gateCell in data.MainDoorCells)
        {
            minGateY = Mathf.Min(minGateY, gateCell.y);
            maxGateY = Mathf.Max(maxGateY, gateCell.y);
        }

        if (minGateY == int.MaxValue)
        {
            minGateY = -1;
            maxGateY = 1;
        }

        for (int x = gateX - 6; x <= gateX + 6; x++)
        {
            for (int y = minGateY - 1; y <= maxGateY + 1; y++)
            {
                Vector3Int cell = new(x, y, 0);

                if (data.GroundCells.Contains(cell) && !data.ForcedWallCells.Contains(cell))
                {
                    dangerCells.Add(cell);
                }
            }
        }

        foreach (Vector3Int gateCell in data.MainDoorCells)
        {
            dangerCells.Add(gateCell);
        }

        return new List<Vector3Int>(dangerCells).ToArray();
    }

    private static void BuildOfficeLayoutMetadata(GeneratedMapData data)
    {
        data.RoomRecords.Clear();
        data.RouteRecords.Clear();

        if (data.ZonePlans.Count == 0)
        {
            return;
        }

        int gateX = data.MainDoorCells.Length > 0 ? data.MainDoorCells[0].x : 0;
        int safeRoomIndex = FindZonePlanIndex(data, GeneratedZoneType.OpenOffice, room => room.CenterCell.x < gateX);
        int dangerRoomIndex = FindClosestZonePlanIndex(data, gateX, room => room.CenterCell.x < gateX && room.CenterCell.y > 0f);
        int preExitRoomIndex = FindClosestZonePlanIndex(data, gateX, room => room.CenterCell.x > gateX && room.CenterCell.y < 0f);

        for (int roomIndex = 0; roomIndex < data.ZonePlans.Count; roomIndex++)
        {
            ZonePlan zonePlan = data.ZonePlans[roomIndex];
            GeneratedRoomType roomType = zonePlan.ZoneType switch
            {
                GeneratedZoneType.Start => GeneratedRoomType.Start,
                GeneratedZoneType.Utility => GeneratedRoomType.Utility,
                GeneratedZoneType.Security => GeneratedRoomType.Key,
                GeneratedZoneType.Exit => GeneratedRoomType.Exit,
                _ => GeneratedRoomType.Explore
            };

            if (roomIndex == safeRoomIndex)
            {
                roomType = GeneratedRoomType.Safe;
            }
            else if (roomIndex == dangerRoomIndex)
            {
                roomType = GeneratedRoomType.DangerCorridor;
            }
            else if (roomIndex == preExitRoomIndex)
            {
                roomType = GeneratedRoomType.PreExit;
            }

            Vector3Int[] roomDoorCells = GetRegisteredRoomDoorCells(data, roomIndex, zonePlan.Room);
            data.RoomRecords.Add(new GeneratedRoomData(
                roomIndex,
                roomType,
                zonePlan.Room.Bounds,
                zonePlan.Room.CenterCell,
                roomDoorCells,
                Array.Empty<int>()));
        }

        if (data.DoorGroups.Count == 0 && data.MainDoorCells.Length > 0)
        {
            data.DoorGroups.Add(new GeneratedDoorGroupData(0, true, data.MainDoorCells, Array.Empty<int>()));
        }

        Vector3Int gateCenter = data.MainDoorCells.Length > 0
            ? data.MainDoorCells[data.MainDoorCells.Length / 2]
            : Vector3Int.zero;

        data.RouteRecords.Add(new GeneratedRouteData(0, safeRoomIndex, GeneratedRouteType.MainRoute, BuildFallbackRouteCells(data.PlayerStartCell, gateCenter)));
        data.RouteRecords.Add(new GeneratedRouteData(safeRoomIndex, dangerRoomIndex, GeneratedRouteType.MainRoute, BuildFallbackRouteCells(gateCenter, data.KeyCell)));
        data.RouteRecords.Add(new GeneratedRouteData(dangerRoomIndex, preExitRoomIndex, GeneratedRouteType.GateApproach, BuildFallbackRouteCells(gateCenter, data.ExitCell)));
        data.RouteRecords.Add(new GeneratedRouteData(0, FindZonePlanIndex(data, GeneratedZoneType.Utility, _ => true), GeneratedRouteType.BranchRoute, BuildFallbackRouteCells(data.PlayerStartCell, data.BatteryCell)));
    }

    private static int FindZonePlanIndex(GeneratedMapData data, GeneratedZoneType zoneType, Func<RoomData, bool> predicate)
    {
        for (int index = 0; index < data.ZonePlans.Count; index++)
        {
            ZonePlan zonePlan = data.ZonePlans[index];

            if (zonePlan.ZoneType == zoneType && predicate(zonePlan.Room))
            {
                return index;
            }
        }

        for (int index = 0; index < data.ZonePlans.Count; index++)
        {
            if (data.ZonePlans[index].ZoneType == zoneType)
            {
                return index;
            }
        }

        return 0;
    }

    private static int FindClosestZonePlanIndex(GeneratedMapData data, int gateX, Func<RoomData, bool> predicate)
    {
        int selectedIndex = -1;
        int bestDistance = int.MaxValue;

        for (int index = 0; index < data.ZonePlans.Count; index++)
        {
            RoomData room = data.ZonePlans[index].Room;

            if (!predicate(room))
            {
                continue;
            }

            int distance = Mathf.Abs(room.CenterCell.x - gateX);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                selectedIndex = index;
            }
        }

        return selectedIndex >= 0 ? selectedIndex : 0;
    }

    private static bool RoomTouchesGate(RoomData room, IReadOnlyList<Vector3Int> gateCells)
    {
        foreach (Vector3Int gateCell in gateCells)
        {
            if (gateCell.x >= room.Bounds.xMin - 1
                && gateCell.x <= room.Bounds.xMax
                && gateCell.y >= room.Bounds.yMin - 1
                && gateCell.y <= room.Bounds.yMax)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3Int[] GetRegisteredRoomDoorCells(GeneratedMapData data, int roomId, RoomData room)
    {
        HashSet<Vector3Int> collected = new();

        if (data.RoomDoorCellsById.TryGetValue(roomId, out HashSet<Vector3Int> registeredDoorCells))
        {
            foreach (Vector3Int doorCell in registeredDoorCells)
            {
                collected.Add(doorCell);
            }
        }

        if (RoomTouchesGate(room, data.MainDoorCells))
        {
            foreach (Vector3Int gateCell in data.MainDoorCells)
            {
                collected.Add(gateCell);
            }
        }

        return collected.Count > 0 ? new List<Vector3Int>(collected).ToArray() : Array.Empty<Vector3Int>();
    }

    private static GeneratedMapData CreateEmergencyFallback()
    {
        GeneratedMapData data = new();
        FillRect(data.GroundCells, -8, 8, -5, 5);
        RoomData startRoom = AddZoneRoom(data, new RectInt(-8, -5, 6, 11), GeneratedZoneType.Start);
        RoomData utilityRoom = AddZoneRoom(data, new RectInt(-1, -5, 4, 5), GeneratedZoneType.Utility);
        RoomData securityRoom = AddZoneRoom(data, new RectInt(3, 1, 6, 5), GeneratedZoneType.Security);
        RoomData exitRoom = AddZoneRoom(data, new RectInt(3, -5, 6, 5), GeneratedZoneType.Exit);
        AddZoneRoom(data, new RectInt(-1, 1, 3, 5), GeneratedZoneType.OpenOffice);
        data.MainDoorCells = new[] { new Vector3Int(2, -1, 0), new Vector3Int(2, 0, 0), new Vector3Int(2, 1, 0) };
        AddHorizontalRoomBoundary(data.ForcedWallCells, startRoom, isTopSide: false, GetDoorSpan(startRoom, preferRightSide: true));
        AddHorizontalRoomBoundary(data.ForcedWallCells, utilityRoom, isTopSide: false, GetDoorSpan(utilityRoom));
        AddHorizontalRoomBoundary(data.ForcedWallCells, securityRoom, isTopSide: true, GetDoorSpan(securityRoom, preferLeftSide: true));
        AddHorizontalRoomBoundary(data.ForcedWallCells, exitRoom, isTopSide: false, GetDoorSpan(exitRoom, preferLeftSide: true));
        AddVerticalWallLine(data.ForcedWallCells, 2, -4, 4);
        foreach (Vector3Int doorCell in data.MainDoorCells)
        {
            data.ForcedWallCells.Remove(doorCell);
        }
        data.PlayerStartCell = new Vector3Int(-4, 0, 0);
        data.KeyCell = new Vector3Int(5, 3, 0);
        data.BatteryCell = new Vector3Int(5, -3, 0);
        data.ExitCell = new Vector3Int(7, -3, 0);
        data.PatrolSpawnCell = new Vector3Int(6, 0, 0);
        data.GlassPanelCell = new Vector3Int(1, 0, 0);
        data.SafeRoomCell = new Vector3Int(0, 3, 0);
        data.DangerCells = new[]
        {
            new Vector3Int(1, -1, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(2, -1, 0),
            new Vector3Int(2, 0, 0),
            new Vector3Int(2, 1, 0),
            new Vector3Int(3, -1, 0),
            new Vector3Int(3, 0, 0),
            new Vector3Int(3, 1, 0)
        };

        foreach (Vector3Int doorCell in data.MainDoorCells)
        {
            data.DoorCells.Add(doorCell);
        }

        ReserveCriticalCells(data);
        GenerateCoverProps(new System.Random(1337), data);
        BuildZoneMetadata(data);
        BuildEmergencyFallbackMetadata(data, startRoom, utilityRoom, securityRoom, exitRoom);
        return data;
    }

    private static void BuildEmergencyFallbackMetadata(GeneratedMapData data, RoomData startRoom, RoomData utilityRoom, RoomData securityRoom, RoomData exitRoom)
    {
        data.RoomRecords.Clear();
        data.RouteRecords.Clear();
        data.DoorGroups.Clear();

        RectInt safeRoomBounds = new(-1, 1, 3, 5);
        Vector3Int[] gateCells = data.MainDoorCells ?? Array.Empty<Vector3Int>();
        Vector3Int[] safeDoorCells = gateCells;
        Vector3Int[] exitDoorCells = gateCells;

        data.RoomRecords.Add(new GeneratedRoomData(0, GeneratedRoomType.Start, startRoom.Bounds, startRoom.CenterCell, Array.Empty<Vector3Int>(), new[] { 4 }));
        data.RoomRecords.Add(new GeneratedRoomData(1, GeneratedRoomType.Utility, utilityRoom.Bounds, utilityRoom.CenterCell, Array.Empty<Vector3Int>(), new[] { 4 }));
        data.RoomRecords.Add(new GeneratedRoomData(2, GeneratedRoomType.Key, securityRoom.Bounds, securityRoom.CenterCell, safeDoorCells, new[] { 4, 3 }));
        data.RoomRecords.Add(new GeneratedRoomData(3, GeneratedRoomType.Exit, exitRoom.Bounds, exitRoom.CenterCell, exitDoorCells, new[] { 2, 4 }));
        data.RoomRecords.Add(new GeneratedRoomData(4, GeneratedRoomType.Safe, safeRoomBounds, new Vector3Int(Mathf.RoundToInt(safeRoomBounds.center.x), Mathf.RoundToInt(safeRoomBounds.center.y), 0), safeDoorCells, new[] { 0, 1, 2, 3 }));

        data.RouteRecords.Add(new GeneratedRouteData(0, 4, GeneratedRouteType.MainRoute, BuildFallbackRouteCells(startRoom.CenterCell, new Vector3Int(0, 0, 0))));
        data.RouteRecords.Add(new GeneratedRouteData(4, 2, GeneratedRouteType.MainRoute, BuildFallbackRouteCells(new Vector3Int(0, 3, 0), securityRoom.CenterCell)));
        data.RouteRecords.Add(new GeneratedRouteData(2, 3, GeneratedRouteType.GateApproach, BuildFallbackRouteCells(new Vector3Int(2, 0, 0), exitRoom.CenterCell)));
        data.RouteRecords.Add(new GeneratedRouteData(1, 4, GeneratedRouteType.BranchRoute, BuildFallbackRouteCells(utilityRoom.CenterCell, new Vector3Int(0, -1, 0))));
        data.DoorGroups.Add(new GeneratedDoorGroupData(0, true, gateCells, new[] { 2, 3 }));
    }

    private static Vector3Int[] BuildFallbackRouteCells(Vector3Int fromCell, Vector3Int toCell)
    {
        List<Vector3Int> routeCells = new();
        int currentX = fromCell.x;
        int currentY = fromCell.y;
        routeCells.Add(new Vector3Int(currentX, currentY, 0));

        while (currentX != toCell.x)
        {
            currentX += currentX < toCell.x ? 1 : -1;
            routeCells.Add(new Vector3Int(currentX, currentY, 0));
        }

        while (currentY != toCell.y)
        {
            currentY += currentY < toCell.y ? 1 : -1;
            routeCells.Add(new Vector3Int(currentX, currentY, 0));
        }

        return routeCells.ToArray();
    }

    private static void ApplyGeneratedLayout(Tilemap groundTilemap, Tilemap wallTilemap, Tilemap doorTilemap, GeneratedMapData mapData)
    {
        // Paint order matters:
        // 1. Ground first so walkable space exists.
        // 2. Walls after that to seal boundaries.
        // 3. Doors last so they can replace part of a wall run.
        foreach (Vector3Int groundCell in mapData.GroundCells)
        {
            groundTilemap.SetTile(groundCell, groundTile);
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        foreach (Vector3Int cell in mapData.GroundCells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        for (int x = minX - 1; x <= maxX + 1; x++)
        {
            for (int y = minY - 1; y <= maxY + 1; y++)
            {
                Vector3Int cell = new(x, y, 0);

                if (mapData.GroundCells.Contains(cell))
                {
                    continue;
                }

                if (HasGroundNeighbor(mapData.GroundCells, cell))
                {
                    wallTilemap.SetTile(cell, wallTile);
                }
            }
        }

        for (int x = minX - 1; x <= maxX + 1; x++)
        {
            for (int y = minY - 1; y <= maxY + 1; y++)
            {
                Vector3Int cell = new(x, y, 0);

                if (mapData.GroundCells.Contains(cell)
                    || mapData.DoorCells.Contains(cell)
                    || HasGroundNeighbor(mapData.GroundCells, cell))
                {
                    continue;
                }

                if (HasDiagonalGroundCorner(mapData.GroundCells, cell))
                {
                    wallTilemap.SetTile(cell, wallTile);
                }
            }
        }

        foreach (Vector3Int wallCell in mapData.ForcedWallCells)
        {
            if (mapData.GroundCells.Contains(wallCell))
            {
                wallTilemap.SetTile(wallCell, wallTile);
            }
        }

        foreach (Vector3Int doorCell in mapData.DoorCells)
        {
            if (!mapData.GroundCells.Contains(doorCell))
            {
                groundTilemap.SetTile(doorCell, groundTile);
            }

            wallTilemap.SetTile(doorCell, null);
            doorTilemap.SetTile(doorCell, doorTile);
        }
    }

    private static void BuildCoverProps(Transform parent, GridMapService mapService, IReadOnlyList<CoverPropPlacement> placements)
    {
        if (mapService == null)
        {
            return;
        }

        mapService.ClearRegisteredProps();

        if (placements == null || placements.Count == 0)
        {
            return;
        }

        GameObject propRoot = new("CoverProps");
        propRoot.transform.SetParent(parent, false);

        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
        {
            CoverPropPlacement placement = placements[placementIndex];
            GameObject propObject = new($"{placement.PropType}_{placementIndex}");
            propObject.transform.SetParent(propRoot.transform, false);

            Vector3 worldCenter = GetPlacementWorldCenter(mapService, placement);
            CoverPropRuntime propRuntime = propObject.AddComponent<CoverPropRuntime>();
            propRuntime.Configure(placement, worldCenter, GameLayers.PropIndex, "Default", 4);
            mapService.RegisterPropCells(placement.OccupiedCells);
        }
    }

    private static Vector3 GetPlacementWorldCenter(GridMapService mapService, CoverPropPlacement placement)
    {
        Vector3 originWorldCenter = mapService.CellToWorldCenter(placement.OriginCell);
        return originWorldCenter + new Vector3((placement.Size.x - 1) * 0.5f, (placement.Size.y - 1) * 0.5f, 0f);
    }

    private static void GetGroundBounds(GeneratedMapData mapData, out Vector2 worldCenter, out Vector2 worldSize)
    {
        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        foreach (Vector3Int cell in mapData.GroundCells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        if (minX == int.MaxValue)
        {
            worldCenter = Vector2.zero;
            worldSize = Vector2.one;
            return;
        }

        float width = (maxX - minX) + WorldBoundsPadding;
        float height = (maxY - minY) + WorldBoundsPadding;
        worldCenter = new Vector2((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f);
        worldSize = new Vector2(width, height);
    }

    private static void EnsureTiles()
    {
        TileBase persistentGroundTile = MainEscapeTileAssetCatalog.LoadGroundTile();
        TileBase persistentWallTile = MainEscapeTileAssetCatalog.LoadWallTile();
        TileBase persistentDoorTile = MainEscapeTileAssetCatalog.LoadDoorTile();

        if (persistentGroundTile != null && persistentWallTile != null && persistentDoorTile != null)
        {
            groundTile = persistentGroundTile;
            wallTile = persistentWallTile;
            doorTile = persistentDoorTile;
            tileSprite = ExtractPreviewSprite(persistentGroundTile)
                ?? ExtractPreviewSprite(persistentWallTile)
                ?? ExtractPreviewSprite(persistentDoorTile)
                ?? tileSprite;
            return;
        }

        if (tileSprite == null)
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            tileSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        if (groundTile == null)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = tileSprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            groundTile = tile;
        }

        if (wallTile == null)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = tileSprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.Grid;
            wallTile = tile;
        }

        if (doorTile == null)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = tileSprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.Grid;
            doorTile = tile;
        }
    }

    private static void EnsureSolidBackdropSprite()
    {
        if (solidBackdropSprite != null)
        {
            return;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        solidBackdropSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        solidBackdropSprite.name = "MainEscapeUnifiedFloorBackdrop";
    }

    private static Sprite ExtractPreviewSprite(TileBase tile)
    {
        if (tile is Tile typedTile)
        {
            return typedTile.sprite;
        }

        return null;
    }

    private static Tilemap CreateTilemap(Transform parent, string objectName, int layer, int sortingOrder, Color tint, bool withCollider)
    {
        GameObject tilemapObject = new(objectName);
        tilemapObject.layer = layer;
        tilemapObject.transform.SetParent(parent, false);

        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemap.color = tint;
        tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);

        TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;

        if (withCollider)
        {
            TilemapCollider2D tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();
            ConfigureSmoothTilemapCollision(tilemapObject, tilemapCollider);
        }

        return tilemap;
    }

    private static void PositionSceneActors(GridMapService mapService, Vector3Int playerStartCell)
    {
        if (mapService == null)
        {
            return;
        }

        Scene scene = mapService.gameObject.scene;
        Transform player = RSceneReferenceLookup.FindTransformInScene(scene, "Player");

        if (player != null)
        {
            player.position = mapService.CellToWorldCenter(playerStartCell);
        }

        Transform focusDummy = RSceneReferenceLookup.FindTransformInScene(scene, "FocusDummy");

        if (focusDummy != null)
        {
            focusDummy.position = new Vector3(999f, 999f, 0f);
        }
    }

    private static void FillRect(HashSet<Vector3Int> cells, int minX, int maxX, int minY, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                cells.Add(new Vector3Int(x, y, 0));
            }
        }
    }

    private static bool HasGroundNeighbor(HashSet<Vector3Int> groundCells, Vector3Int cell)
    {
        return groundCells.Contains(cell + Vector3Int.left)
            || groundCells.Contains(cell + Vector3Int.right)
            || groundCells.Contains(cell + Vector3Int.up)
            || groundCells.Contains(cell + Vector3Int.down);
    }

    private static bool HasDiagonalGroundCorner(HashSet<Vector3Int> groundCells, Vector3Int cell)
    {
        bool left = groundCells.Contains(cell + Vector3Int.left);
        bool right = groundCells.Contains(cell + Vector3Int.right);
        bool up = groundCells.Contains(cell + Vector3Int.up);
        bool down = groundCells.Contains(cell + Vector3Int.down);

        return (left && up)
            || (left && down)
            || (right && up)
            || (right && down);
    }

    private static void RefreshTilemapCollider(Tilemap tilemap)
    {
        TilemapCollider2D tilemapCollider = tilemap != null ? tilemap.GetComponent<TilemapCollider2D>() : null;

        if (tilemapCollider != null)
        {
            ConfigureSmoothTilemapCollision(tilemap.gameObject, tilemapCollider);
            tilemapCollider.ProcessTilemapChanges();
        }
    }

    private static void ConfigureSmoothTilemapCollision(GameObject tilemapObject, TilemapCollider2D tilemapCollider)
    {
        if (tilemapObject == null || tilemapCollider == null)
        {
            return;
        }

        Rigidbody2D rigidbody = tilemapObject.GetComponent<Rigidbody2D>();

        if (rigidbody == null)
        {
            rigidbody = tilemapObject.AddComponent<Rigidbody2D>();
        }

        rigidbody.bodyType = RigidbodyType2D.Static;
        rigidbody.simulated = true;

        CompositeCollider2D compositeCollider = tilemapObject.GetComponent<CompositeCollider2D>();

        if (compositeCollider == null)
        {
            compositeCollider = tilemapObject.AddComponent<CompositeCollider2D>();
        }

        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
        tilemapCollider.extrusionFactor = 0.02f;

        TopDownCollisionMaterialUtility.ApplyNoFriction(tilemapCollider);
        TopDownCollisionMaterialUtility.ApplyNoFriction(compositeCollider);
    }

    private static void ApplyNoFrictionToDescendantColliders(Transform root)
    {
        if (root == null || !Application.isPlaying)
        {
            return;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);

        for (int index = 0; index < colliders.Length; index++)
        {
            TopDownCollisionMaterialUtility.ApplyNoFriction(colliders[index]);
        }
    }

    private static void BuildShadowBlockers(Transform parent, Tilemap tilemap, int layer, string rootName, GridMapService mapService)
    {
        if (tilemap == null)
        {
            return;
        }

        GameObject blockerRoot = new(rootName);
        blockerRoot.transform.SetParent(parent, false);

        BoundsInt bounds = tilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cellPosition))
            {
                continue;
            }

            GameObject blocker = new($"{rootName}_{cellPosition.x}_{cellPosition.y}");
            blocker.layer = layer;
            blocker.transform.SetParent(blockerRoot.transform, false);
            blocker.transform.position = tilemap.GetCellCenterWorld(cellPosition);

            BoxCollider2D boxCollider = blocker.AddComponent<BoxCollider2D>();
            boxCollider.size = Vector2.one;
            boxCollider.offset = Vector2.zero;
            boxCollider.isTrigger = true;

            if (!RuntimeShadowCaster2DConfigurator.TryConfigureFromCollider(blocker, boxCollider, out _))
            {
                Debug.LogWarning($"Failed to configure tile shadow blocker at {cellPosition}.", blocker);
            }

            mapService?.RegisterDoorShadowBlocker(cellPosition, blocker);
        }
    }

    private static void Shuffle<T>(System.Random random, List<T> list)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(0, index + 1);
            (list[index], list[swapIndex]) = (list[swapIndex], list[index]);
        }
    }
}


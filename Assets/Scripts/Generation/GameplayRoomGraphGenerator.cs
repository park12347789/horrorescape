/*
 * File Role:
 * Builds procedural maps from room graphs and hybrid templates.
 *
 * Runtime Use:
 * Generates anchor rooms, routes, door groups, and gameplay metadata for several prototype modes.
 *
 * Study Notes:
 * This is a large generator file, so focus on the high-level pipeline first before reading helper functions.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GeneratedMapBlueprint
{
    public readonly HashSet<Vector3Int> GroundCells = new();
    public readonly HashSet<Vector3Int> ForcedWallCells = new();
    public readonly HashSet<Vector3Int> DoorCells = new();
    public readonly HashSet<Vector3Int> DangerCellSet = new();
    public readonly HashSet<Vector3Int> ReservedCells = new();
    public readonly HashSet<Vector3Int> PropBlockedCells = new();
    public readonly List<GeneratedRoomData> Rooms = new();
    public readonly List<GeneratedRouteData> Routes = new();
    public readonly List<GeneratedDoorGroupData> DoorGroups = new();
    public readonly List<GeneratedZoneData> Zones = new();
    public readonly List<CoverPropPlacement> CoverProps = new();

    public Vector3Int PlayerStartCell;
    public Vector3Int KeyCell;
    public Vector3Int BatteryCell;
    public Vector3Int ExitCell;
    public Vector3Int PatrolSpawnCell;
    public Vector3Int GlassPanelCell;
    public Vector3Int SafeRoomCell;
    public Vector3Int[] MainDoorCells = Array.Empty<Vector3Int>();
}

public static class GameplayRoomGraphGenerator
{
    private static readonly Vector2Int[] CardinalOffsets =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

    private const int GridWidth = 9;
    private const int GridHeight = 7;
    private const int StepX = 15;
    private const int StepY = 13;
    private static readonly Vector2Int GridOrigin = new(-48, -34);

    private sealed class LogicalRoom
    {
        public int Id;
        public GeneratedRoomType Type;
        public Vector2Int Node;
        public RectInt Bounds;
        public bool IsMainRoute;
        public bool IsPostGate;
        public readonly List<int> ConnectedRoomIds = new();
        public readonly List<Vector3Int> DoorCells = new();
    }

    private sealed class MainPathPlan
    {
        public MainPathPlan(List<Vector2Int> nodes, bool isCorridorSpine, int[] safeBranchIndices, int[] utilityBranchIndices, int[] corridorAttachmentIndices)
        {
            Nodes = nodes;
            IsCorridorSpine = isCorridorSpine;
            SafeBranchIndices = safeBranchIndices ?? Array.Empty<int>();
            UtilityBranchIndices = utilityBranchIndices ?? Array.Empty<int>();
            CorridorAttachmentIndices = corridorAttachmentIndices ?? Array.Empty<int>();
        }

        public List<Vector2Int> Nodes { get; }
        public bool IsCorridorSpine { get; }
        public int[] SafeBranchIndices { get; }
        public int[] UtilityBranchIndices { get; }
        public int[] CorridorAttachmentIndices { get; }
    }

    private readonly struct LogicalEdge
    {
        public LogicalEdge(int roomAId, int roomBId, bool isMainRoute, bool isMainGate)
        {
            RoomAId = roomAId;
            RoomBId = roomBId;
            IsMainRoute = isMainRoute;
            IsMainGate = isMainGate;
        }

        public int RoomAId { get; }
        public int RoomBId { get; }
        public bool IsMainRoute { get; }
        public bool IsMainGate { get; }
    }

    private readonly struct PropCandidate
    {
        public PropCandidate(CoverPropType propType, GeneratedZoneType zoneType, Vector3Int originCell, Vector2Int size, bool isMandatory)
        {
            PropType = propType;
            ZoneType = zoneType;
            OriginCell = originCell;
            Size = size;
            IsMandatory = isMandatory;
        }

        public CoverPropType PropType { get; }
        public GeneratedZoneType ZoneType { get; }
        public Vector3Int OriginCell { get; }
        public Vector2Int Size { get; }
        public bool IsMandatory { get; }

        public Vector3Int[] GetOccupiedCells()
        {
            Vector3Int[] occupiedCells = new Vector3Int[Size.x * Size.y];
            int cellIndex = 0;

            for (int x = 0; x < Size.x; x++)
            {
                for (int y = 0; y < Size.y; y++)
                {
                    occupiedCells[cellIndex++] = new Vector3Int(OriginCell.x + x, OriginCell.y + y, 0);
                }
            }

            return occupiedCells;
        }

        public CoverPropPlacement ToPlacement()
        {
            return new CoverPropPlacement(PropType, ZoneType, OriginCell, Size, GetOccupiedCells());
        }
    }

    public static bool TryGenerate(int seed, out GeneratedMapBlueprint blueprint)
    {
        return GameplayWfcGenerator.TryGenerate(seed, out blueprint);
    }

    public static bool TryGenerateHybrid(int seed, out GeneratedMapBlueprint blueprint)
    {
        return TryGenerateSingle(seed, out blueprint);
    }

    private static bool TryGenerateSingle(int seed, out GeneratedMapBlueprint blueprint)
    {
        System.Random random = new(seed);
        blueprint = new GeneratedMapBlueprint();
        int targetRoomCount = random.Next(18, 23);
        Dictionary<int, LogicalRoom> roomsById = new();
        Dictionary<Vector2Int, int> roomIdByNode = new();
        List<LogicalEdge> edges = new();

        if (!BuildLogicalGraph(random, targetRoomCount, roomsById, roomIdByNode, edges))
        {
            return false;
        }

        if (!AssignBounds(random, roomsById))
        {
            return false;
        }

        FillRooms(blueprint, roomsById);
        BuildRoutesAndDoors(blueprint, roomsById, edges);
        AddSightlineBreakers(random, blueprint, roomsById);
        AssignObjectiveCells(blueprint, roomsById);
        ReserveCriticalCells(blueprint);
        GenerateCoverProps(random, blueprint, roomsById);
        BuildMetadata(blueprint, roomsById);
        return ValidateBlueprint(blueprint, roomsById);
    }

    private static bool BuildLogicalGraph(
        System.Random random,
        int targetRoomCount,
        Dictionary<int, LogicalRoom> roomsById,
        Dictionary<Vector2Int, int> roomIdByNode,
        List<LogicalEdge> edges)
    {
        MainPathPlan mainPathPlan = BuildMainPath(random);
        List<Vector2Int> mainPath = mainPathPlan.Nodes;

        if (mainPath.Count < 9)
        {
            return false;
        }

        int preGateMaxX = int.MinValue;
        int postGateMinX = int.MaxValue;

        for (int roomIndex = 0; roomIndex <= 6; roomIndex++)
        {
            preGateMaxX = Mathf.Max(preGateMaxX, mainPath[roomIndex].x);
        }

        for (int roomIndex = 7; roomIndex < mainPath.Count; roomIndex++)
        {
            postGateMinX = Mathf.Min(postGateMinX, mainPath[roomIndex].x);
        }

        GeneratedRoomType[] mainTypes =
        {
            GeneratedRoomType.Start,
            GeneratedRoomType.Explore,
            GeneratedRoomType.DangerCorridor,
            GeneratedRoomType.Explore,
            GeneratedRoomType.Key,
            GeneratedRoomType.Explore,
            GeneratedRoomType.DangerCorridor,
            GeneratedRoomType.PreExit,
            GeneratedRoomType.Exit
        };

        for (int roomIndex = 0; roomIndex < mainPath.Count; roomIndex++)
        {
            CreateRoom(roomsById, roomIdByNode, mainPath[roomIndex], mainTypes[roomIndex], isMainRoute: true, isPostGate: roomIndex >= 7);
        }

        for (int roomIndex = 0; roomIndex < mainPath.Count - 1; roomIndex++)
        {
            int roomAId = roomIdByNode[mainPath[roomIndex]];
            int roomBId = roomIdByNode[mainPath[roomIndex + 1]];
            ConnectRooms(roomsById, edges, roomAId, roomBId, isMainRoute: true, isMainGate: roomIndex == 6);
        }

        if (!TryAddBranchRoomToMainPath(random, roomsById, roomIdByNode, edges, mainPath, preGateMaxX, postGateMinX, mainPathPlan.SafeBranchIndices, GeneratedRoomType.Safe))
        {
            return false;
        }

        if (!TryAddBranchRoomToMainPath(random, roomsById, roomIdByNode, edges, mainPath, preGateMaxX, postGateMinX, mainPathPlan.UtilityBranchIndices, GeneratedRoomType.Utility))
        {
            return false;
        }

        if (mainPathPlan.IsCorridorSpine)
        {
            EnsureCorridorAttachments(random, roomsById, roomIdByNode, edges, mainPath, preGateMaxX, postGateMinX, mainPathPlan.CorridorAttachmentIndices, 4);
        }

        while (roomsById.Count < targetRoomCount)
        {
            if (!TryAddProceduralBranch(random, roomsById, roomIdByNode, edges, targetRoomCount, preGateMaxX, postGateMinX))
            {
                break;
            }
        }

        return roomsById.Count >= 18;
    }

    private static MainPathPlan BuildMainPath(System.Random random)
    {
        int startY = random.Next(1, GridHeight - 2);
        int firstBendY = PickDifferentLane(random, startY);
        int secondBendY = PickFollowupLane(random, firstBendY, startY);
        int templateIndex = random.Next(6);

        List<Vector2Int> path = templateIndex switch
        {
            0 => new List<Vector2Int>
            {
                new(0, startY),
                new(1, startY),
                new(1, firstBendY),
                new(2, firstBendY),
                new(3, firstBendY),
                new(4, firstBendY),
                new(4, secondBendY),
                new(5, secondBendY),
                new(6, secondBendY)
            },
            1 => new List<Vector2Int>
            {
                new(0, startY),
                new(1, startY),
                new(2, startY),
                new(2, firstBendY),
                new(3, firstBendY),
                new(3, secondBendY),
                new(4, secondBendY),
                new(5, secondBendY),
                new(6, secondBendY)
            },
            2 => new List<Vector2Int>
            {
                new(0, startY),
                new(0, firstBendY),
                new(1, firstBendY),
                new(2, firstBendY),
                new(3, firstBendY),
                new(4, firstBendY),
                new(4, secondBendY),
                new(5, secondBendY),
                new(6, secondBendY)
            },
            3 => new List<Vector2Int>
            {
                new(0, startY),
                new(1, startY),
                new(1, firstBendY),
                new(2, firstBendY),
                new(2, secondBendY),
                new(3, secondBendY),
                new(4, secondBendY),
                new(5, secondBendY),
                new(6, secondBendY)
            },
            4 => new List<Vector2Int>
            {
                new(0, startY),
                new(1, startY),
                new(2, startY),
                new(3, startY),
                new(3, firstBendY),
                new(4, firstBendY),
                new(5, firstBendY),
                new(5, secondBendY),
                new(6, secondBendY)
            },
            _ => new List<Vector2Int>
            {
                new(0, startY),
                new(1, startY),
                new(2, startY),
                new(3, startY),
                new(4, startY),
                new(5, startY),
                new(6, startY),
                new(7, startY),
                new(8, startY)
            }
        };

        bool isCorridorSpine = templateIndex == 5;
        int[] safeBranchIndices = isCorridorSpine ? new[] { 1, 2, 3, 4 } : new[] { 1, 2, 3 };
        int[] utilityBranchIndices = isCorridorSpine ? new[] { 3, 4, 5, 6 } : new[] { 2, 3, 4, 5 };
        int[] corridorAttachmentIndices = isCorridorSpine ? new[] { 1, 2, 3, 4, 5, 6 } : Array.Empty<int>();

        if (ContainsDuplicateNodes(path))
        {
            return new MainPathPlan(
                new List<Vector2Int>
                {
                    new(0, startY),
                    new(1, startY),
                    new(1, firstBendY),
                    new(2, firstBendY),
                    new(3, firstBendY),
                    new(4, firstBendY),
                    new(4, secondBendY),
                    new(5, secondBendY),
                    new(6, secondBendY)
                },
                false,
                new[] { 1, 2, 3 },
                new[] { 2, 3, 4, 5 },
                Array.Empty<int>());
        }

        return new MainPathPlan(path, isCorridorSpine, safeBranchIndices, utilityBranchIndices, corridorAttachmentIndices);
    }

    private static int PickDifferentLane(System.Random random, int currentLane)
    {
        int nextLane = currentLane;

        while (nextLane == currentLane)
        {
            nextLane = random.Next(1, GridHeight - 1);
        }

        return nextLane;
    }

    private static int PickFollowupLane(System.Random random, int currentLane, int fallbackLane)
    {
        if (random.NextDouble() < 0.4d)
        {
            return fallbackLane;
        }

        return PickDifferentLane(random, currentLane);
    }

    private static bool ContainsDuplicateNodes(List<Vector2Int> path)
    {
        HashSet<Vector2Int> visited = new();

        foreach (Vector2Int node in path)
        {
            if (!visited.Add(node))
            {
                return true;
            }
        }

        return false;
    }

    private static int CreateRoom(
        Dictionary<int, LogicalRoom> roomsById,
        Dictionary<Vector2Int, int> roomIdByNode,
        Vector2Int node,
        GeneratedRoomType roomType,
        bool isMainRoute,
        bool isPostGate)
    {
        int roomId = roomsById.Count;
        LogicalRoom room = new()
        {
            Id = roomId,
            Type = roomType,
            Node = node,
            IsMainRoute = isMainRoute,
            IsPostGate = isPostGate
        };
        roomsById[roomId] = room;
        roomIdByNode[node] = roomId;
        return roomId;
    }

    private static void ConnectRooms(
        Dictionary<int, LogicalRoom> roomsById,
        List<LogicalEdge> edges,
        int roomAId,
        int roomBId,
        bool isMainRoute,
        bool isMainGate)
    {
        if (!roomsById[roomAId].ConnectedRoomIds.Contains(roomBId))
        {
            roomsById[roomAId].ConnectedRoomIds.Add(roomBId);
        }

        if (!roomsById[roomBId].ConnectedRoomIds.Contains(roomAId))
        {
            roomsById[roomBId].ConnectedRoomIds.Add(roomAId);
        }

        edges.Add(new LogicalEdge(roomAId, roomBId, isMainRoute, isMainGate));
    }

    private static bool TryAddBranchRoom(
        System.Random random,
        Dictionary<int, LogicalRoom> roomsById,
        Dictionary<Vector2Int, int> roomIdByNode,
        List<LogicalEdge> edges,
        int parentRoomId,
        GeneratedRoomType roomType,
        int preGateMaxX,
        int postGateMinX)
    {
        LogicalRoom parentRoom = roomsById[parentRoomId];
        List<Vector2Int> candidates = GetFreeNeighborNodes(parentRoom.Node, roomIdByNode, parentRoom.IsPostGate, preGateMaxX, postGateMinX);
        Shuffle(random, candidates);

        if (candidates.Count == 0)
        {
            return false;
        }

        int childRoomId = CreateRoom(roomsById, roomIdByNode, candidates[0], roomType, isMainRoute: false, isPostGate: parentRoom.IsPostGate);
        ConnectRooms(roomsById, edges, parentRoomId, childRoomId, isMainRoute: false, isMainGate: false);
        return true;
    }

    private static bool TryAddBranchRoomToMainPath(
        System.Random random,
        Dictionary<int, LogicalRoom> roomsById,
        Dictionary<Vector2Int, int> roomIdByNode,
        List<LogicalEdge> edges,
        IReadOnlyList<Vector2Int> mainPath,
        int preGateMaxX,
        int postGateMinX,
        IReadOnlyList<int> preferredIndices,
        GeneratedRoomType roomType)
    {
        List<int> parentRoomIds = new();

        for (int index = 0; index < preferredIndices.Count; index++)
        {
            int routeIndex = preferredIndices[index];

            if (routeIndex < 0 || routeIndex >= mainPath.Count)
            {
                continue;
            }

            if (roomIdByNode.TryGetValue(mainPath[routeIndex], out int roomId))
            {
                parentRoomIds.Add(roomId);
            }
        }

        Shuffle(random, parentRoomIds);

        foreach (int parentRoomId in parentRoomIds)
        {
            if (TryAddBranchRoom(random, roomsById, roomIdByNode, edges, parentRoomId, roomType, preGateMaxX, postGateMinX))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryAddProceduralBranch(
        System.Random random,
        Dictionary<int, LogicalRoom> roomsById,
        Dictionary<Vector2Int, int> roomIdByNode,
        List<LogicalEdge> edges,
        int targetRoomCount,
        int preGateMaxX,
        int postGateMinX)
    {
        List<int> candidateParents = new();

        foreach (KeyValuePair<int, LogicalRoom> pair in roomsById)
        {
            if (pair.Value.Type == GeneratedRoomType.Exit)
            {
                continue;
            }

            if (GetFreeNeighborNodes(pair.Value.Node, roomIdByNode, pair.Value.IsPostGate, preGateMaxX, postGateMinX).Count > 0)
            {
                candidateParents.Add(pair.Key);
            }
        }

        if (candidateParents.Count == 0)
        {
            return false;
        }

        Shuffle(random, candidateParents);
        int parentId = candidateParents[0];
        LogicalRoom parentRoom = roomsById[parentId];
        GeneratedRoomType roomType = ChooseBranchRoomType(random, roomsById, parentRoom, targetRoomCount);
        return TryAddBranchRoom(random, roomsById, roomIdByNode, edges, parentId, roomType, preGateMaxX, postGateMinX);
    }

    private static void EnsureCorridorAttachments(
        System.Random random,
        Dictionary<int, LogicalRoom> roomsById,
        Dictionary<Vector2Int, int> roomIdByNode,
        List<LogicalEdge> edges,
        IReadOnlyList<Vector2Int> mainPath,
        int preGateMaxX,
        int postGateMinX,
        IReadOnlyList<int> attachmentIndices,
        int desiredAttachmentCount)
    {
        List<int> indices = new();

        for (int index = 0; index < attachmentIndices.Count; index++)
        {
            indices.Add(attachmentIndices[index]);
        }

        Shuffle(random, indices);
        int createdCount = 0;

        foreach (int routeIndex in indices)
        {
            if (createdCount >= desiredAttachmentCount)
            {
                break;
            }

            if (routeIndex < 0 || routeIndex >= mainPath.Count || !roomIdByNode.TryGetValue(mainPath[routeIndex], out int parentRoomId))
            {
                continue;
            }

            if (TryAddBranchRoom(random, roomsById, roomIdByNode, edges, parentRoomId, GeneratedRoomType.Explore, preGateMaxX, postGateMinX))
            {
                createdCount++;
            }
        }
    }

    private static GeneratedRoomType ChooseBranchRoomType(
        System.Random random,
        Dictionary<int, LogicalRoom> roomsById,
        LogicalRoom parentRoom,
        int targetRoomCount)
    {
        int dangerRoomCount = 0;
        int exploreRoomCount = 0;

        foreach (LogicalRoom room in roomsById.Values)
        {
            if (room.Type == GeneratedRoomType.DangerCorridor)
            {
                dangerRoomCount++;
            }

            if (room.Type == GeneratedRoomType.Explore)
            {
                exploreRoomCount++;
            }
        }

        if (!parentRoom.IsPostGate && dangerRoomCount < 4 && random.NextDouble() < 0.18d)
        {
            return GeneratedRoomType.DangerCorridor;
        }

        if (parentRoom.IsPostGate && random.NextDouble() < 0.18d)
        {
            return GeneratedRoomType.PreExit;
        }

        if (exploreRoomCount < targetRoomCount - 5 || random.NextDouble() < 0.72d)
        {
            return GeneratedRoomType.Explore;
        }

        return parentRoom.IsPostGate ? GeneratedRoomType.PreExit : GeneratedRoomType.DangerCorridor;
    }

    private static List<Vector2Int> GetFreeNeighborNodes(Vector2Int node, Dictionary<Vector2Int, int> roomIdByNode, bool isPostGate, int preGateMaxX, int postGateMinX)
    {
        List<Vector2Int> freeNodes = new();

        foreach (Vector2Int offset in CardinalOffsets)
        {
            Vector2Int candidate = node + offset;

            if (candidate.x < 0 || candidate.x >= GridWidth || candidate.y < 0 || candidate.y >= GridHeight)
            {
                continue;
            }

            if (isPostGate && candidate.x < postGateMinX)
            {
                continue;
            }

            if (!isPostGate && candidate.x > preGateMaxX)
            {
                continue;
            }

            if (!roomIdByNode.ContainsKey(candidate))
            {
                freeNodes.Add(candidate);
            }
        }

        return freeNodes;
    }

    private static bool AssignBounds(System.Random random, Dictionary<int, LogicalRoom> roomsById)
    {
        List<LogicalRoom> orderedRooms = new(roomsById.Values);
        orderedRooms.Sort((left, right) => left.Id.CompareTo(right.Id));

        foreach (LogicalRoom room in orderedRooms)
        {
            Vector2Int center = new(
                GridOrigin.x + (room.Node.x * StepX) + random.Next(-1, 2),
                GridOrigin.y + (room.Node.y * StepY) + random.Next(-1, 2));
            Vector2Int size = GetRoomSize(random, room);
            room.Bounds = new RectInt(center.x - (size.x / 2), center.y - (size.y / 2), size.x, size.y);
        }

        for (int leftIndex = 0; leftIndex < orderedRooms.Count; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < orderedRooms.Count; rightIndex++)
            {
                if (ExpandRect(orderedRooms[leftIndex].Bounds, 1).Overlaps(ExpandRect(orderedRooms[rightIndex].Bounds, 1)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static RectInt ExpandRect(RectInt rect, int padding)
    {
        return new RectInt(rect.xMin - padding, rect.yMin - padding, rect.width + (padding * 2), rect.height + (padding * 2));
    }

    private static Vector2Int GetRoomSize(System.Random random, LogicalRoom room)
    {
        return room.Type switch
        {
            GeneratedRoomType.DangerCorridor => new Vector2Int(random.Next(6, 9), random.Next(5, 7)),
            GeneratedRoomType.Safe => new Vector2Int(random.Next(8, 11), random.Next(7, 9)),
            GeneratedRoomType.Exit => new Vector2Int(random.Next(8, 11), random.Next(7, 9)),
            GeneratedRoomType.PreExit => new Vector2Int(random.Next(7, 10), random.Next(6, 8)),
            _ => new Vector2Int(random.Next(7, 11), random.Next(6, 9))
        };
    }

    private static void FillRooms(GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
            {
                for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                {
                    blueprint.GroundCells.Add(new Vector3Int(x, y, 0));
                }
            }
        }
    }

    private static void BuildRoutesAndDoors(
        GeneratedMapBlueprint blueprint,
        Dictionary<int, LogicalRoom> roomsById,
        List<LogicalEdge> edges)
    {
        int doorGroupId = 0;

        foreach (LogicalEdge edge in edges)
        {
            LogicalRoom roomA = roomsById[edge.RoomAId];
            LogicalRoom roomB = roomsById[edge.RoomBId];

            if (roomA.Node.x == roomB.Node.x)
            {
                BuildVerticalConnection(blueprint, roomA, roomB, edge, ref doorGroupId);
            }
            else
            {
                BuildHorizontalConnection(blueprint, roomA, roomB, edge, ref doorGroupId);
            }
        }
    }

    private static void BuildHorizontalConnection(
        GeneratedMapBlueprint blueprint,
        LogicalRoom roomA,
        LogicalRoom roomB,
        LogicalEdge edge,
        ref int doorGroupId)
    {
        LogicalRoom leftRoom = roomA.Bounds.center.x <= roomB.Bounds.center.x ? roomA : roomB;
        LogicalRoom rightRoom = leftRoom == roomA ? roomB : roomA;

        int minDoorBottom = Mathf.Max(leftRoom.Bounds.yMin + 1, rightRoom.Bounds.yMin + 1);
        int maxDoorBottom = Mathf.Min(leftRoom.Bounds.yMax - 3, rightRoom.Bounds.yMax - 3);
        int doorBottom = Mathf.Clamp(Mathf.RoundToInt((leftRoom.Bounds.center.y + rightRoom.Bounds.center.y) * 0.5f) - 1, minDoorBottom, maxDoorBottom);
        int corridorStartX = leftRoom.Bounds.xMax;
        int corridorEndX = rightRoom.Bounds.xMin - 1;

        List<Vector3Int> routeCells = new();

        for (int x = corridorStartX; x <= corridorEndX; x++)
        {
            for (int y = doorBottom; y <= doorBottom + 1; y++)
            {
                Vector3Int cell = new(x, y, 0);
                blueprint.GroundCells.Add(cell);
                routeCells.Add(cell);
            }
        }

        Vector3Int[] leftDoorCells =
        {
            new Vector3Int(corridorStartX, doorBottom, 0),
            new Vector3Int(corridorStartX, doorBottom + 1, 0)
        };
        Vector3Int[] rightDoorCells =
        {
            new Vector3Int(corridorEndX, doorBottom, 0),
            new Vector3Int(corridorEndX, doorBottom + 1, 0)
        };

        AddRouteRecord(blueprint, leftRoom, rightRoom, edge, routeCells.ToArray());

        if (edge.IsMainGate)
        {
            int gateX = Mathf.Clamp((corridorStartX + corridorEndX) / 2, corridorStartX, corridorEndX);
            Vector3Int[] gateCells =
            {
                new Vector3Int(gateX, doorBottom, 0),
                new Vector3Int(gateX, doorBottom + 1, 0)
            };

            RegisterDoorGroup(blueprint, ref doorGroupId, requiresKey: true, leftRoom.Id, rightRoom.Id, gateCells);
            blueprint.MainDoorCells = gateCells;
            leftRoom.DoorCells.AddRange(gateCells);
            rightRoom.DoorCells.AddRange(gateCells);
            return;
        }

        RegisterDoorGroup(blueprint, ref doorGroupId, requiresKey: false, leftRoom.Id, rightRoom.Id, leftDoorCells);
        RegisterDoorGroup(blueprint, ref doorGroupId, requiresKey: false, leftRoom.Id, rightRoom.Id, rightDoorCells);
        leftRoom.DoorCells.AddRange(leftDoorCells);
        rightRoom.DoorCells.AddRange(rightDoorCells);
    }

    private static void BuildVerticalConnection(
        GeneratedMapBlueprint blueprint,
        LogicalRoom roomA,
        LogicalRoom roomB,
        LogicalEdge edge,
        ref int doorGroupId)
    {
        LogicalRoom bottomRoom = roomA.Bounds.center.y <= roomB.Bounds.center.y ? roomA : roomB;
        LogicalRoom topRoom = bottomRoom == roomA ? roomB : roomA;

        int minDoorLeft = Mathf.Max(bottomRoom.Bounds.xMin + 1, topRoom.Bounds.xMin + 1);
        int maxDoorLeft = Mathf.Min(bottomRoom.Bounds.xMax - 3, topRoom.Bounds.xMax - 3);
        int doorLeft = Mathf.Clamp(Mathf.RoundToInt((bottomRoom.Bounds.center.x + topRoom.Bounds.center.x) * 0.5f) - 1, minDoorLeft, maxDoorLeft);
        int corridorStartY = bottomRoom.Bounds.yMax;
        int corridorEndY = topRoom.Bounds.yMin - 1;

        List<Vector3Int> routeCells = new();

        for (int y = corridorStartY; y <= corridorEndY; y++)
        {
            for (int x = doorLeft; x <= doorLeft + 1; x++)
            {
                Vector3Int cell = new(x, y, 0);
                blueprint.GroundCells.Add(cell);
                routeCells.Add(cell);
            }
        }

        Vector3Int[] bottomDoorCells =
        {
            new Vector3Int(doorLeft, corridorStartY, 0),
            new Vector3Int(doorLeft + 1, corridorStartY, 0)
        };
        Vector3Int[] topDoorCells =
        {
            new Vector3Int(doorLeft, corridorEndY, 0),
            new Vector3Int(doorLeft + 1, corridorEndY, 0)
        };

        AddRouteRecord(blueprint, bottomRoom, topRoom, edge, routeCells.ToArray());

        if (edge.IsMainGate)
        {
            int gateY = Mathf.Clamp((corridorStartY + corridorEndY) / 2, corridorStartY, corridorEndY);
            Vector3Int[] gateCells =
            {
                new Vector3Int(doorLeft, gateY, 0),
                new Vector3Int(doorLeft + 1, gateY, 0)
            };

            RegisterDoorGroup(blueprint, ref doorGroupId, requiresKey: true, bottomRoom.Id, topRoom.Id, gateCells);
            blueprint.MainDoorCells = gateCells;
            bottomRoom.DoorCells.AddRange(gateCells);
            topRoom.DoorCells.AddRange(gateCells);
            return;
        }

        RegisterDoorGroup(blueprint, ref doorGroupId, requiresKey: false, bottomRoom.Id, topRoom.Id, bottomDoorCells);
        RegisterDoorGroup(blueprint, ref doorGroupId, requiresKey: false, bottomRoom.Id, topRoom.Id, topDoorCells);
        bottomRoom.DoorCells.AddRange(bottomDoorCells);
        topRoom.DoorCells.AddRange(topDoorCells);
    }

    private static void AddRouteRecord(GeneratedMapBlueprint blueprint, LogicalRoom roomA, LogicalRoom roomB, LogicalEdge edge, Vector3Int[] routeCells)
    {
        GeneratedRouteType routeType = edge.IsMainGate
            ? GeneratedRouteType.GateApproach
            : edge.IsMainRoute
                ? GeneratedRouteType.MainRoute
                : GeneratedRouteType.BranchRoute;

        blueprint.Routes.Add(new GeneratedRouteData(roomA.Id, roomB.Id, routeType, routeCells));

        if (edge.IsMainGate || roomA.Type == GeneratedRoomType.DangerCorridor || roomB.Type == GeneratedRoomType.DangerCorridor || roomA.Type == GeneratedRoomType.PreExit || roomB.Type == GeneratedRoomType.PreExit)
        {
            foreach (Vector3Int routeCell in routeCells)
            {
                blueprint.DangerCellSet.Add(routeCell);
            }
        }
    }

    private static void RegisterDoorGroup(
        GeneratedMapBlueprint blueprint,
        ref int doorGroupId,
        bool requiresKey,
        int roomAId,
        int roomBId,
        Vector3Int[] cells)
    {
        blueprint.DoorGroups.Add(new GeneratedDoorGroupData(doorGroupId++, requiresKey, cells, new[] { roomAId, roomBId }));

        foreach (Vector3Int cell in cells)
        {
            blueprint.DoorCells.Add(cell);
            blueprint.GroundCells.Add(cell);
        }
    }

    private static void AddSightlineBreakers(System.Random random, GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            if (room.Type == GeneratedRoomType.DangerCorridor || room.Bounds.width < 7 || room.Bounds.height < 6)
            {
                continue;
            }

            Vector2 roomCenter = room.Bounds.center;
            int centerX = Mathf.RoundToInt(roomCenter.x);
            int centerY = Mathf.RoundToInt(roomCenter.y);
            Vector3Int origin = random.NextDouble() < 0.5d
                ? new Vector3Int(centerX - 1, centerY - 1, 0)
                : new Vector3Int(centerX, centerY - 1, 0);
            int width = random.NextDouble() < 0.5d ? 2 : 1;
            int height = width == 2 ? 2 : 3;
            TryAddForcedWallCluster(blueprint.ForcedWallCells, origin, width, height, room);
        }
    }

    private static void TryAddForcedWallCluster(HashSet<Vector3Int> forcedWalls, Vector3Int origin, int width, int height, LogicalRoom room)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int cell = new(origin.x + x, origin.y + y, 0);

                if (cell.x <= room.Bounds.xMin + 1
                    || cell.x >= room.Bounds.xMax - 2
                    || cell.y <= room.Bounds.yMin + 1
                    || cell.y >= room.Bounds.yMax - 2)
                {
                    continue;
                }

                forcedWalls.Add(cell);
            }
        }
    }

    private static void AssignObjectiveCells(GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        LogicalRoom startRoom = FindRoomByType(roomsById, GeneratedRoomType.Start);
        LogicalRoom keyRoom = FindRoomByType(roomsById, GeneratedRoomType.Key);
        LogicalRoom utilityRoom = FindRoomByType(roomsById, GeneratedRoomType.Utility);
        LogicalRoom safeRoom = FindRoomByType(roomsById, GeneratedRoomType.Safe);
        LogicalRoom exitRoom = FindRoomByType(roomsById, GeneratedRoomType.Exit);
        LogicalRoom patrolRoom = FindRoomByType(roomsById, GeneratedRoomType.PreExit);
        Vector2 safeRoomCenter = safeRoom.Bounds.center;
        Vector2 patrolRoomCenter = patrolRoom.Bounds.center;

        blueprint.PlayerStartCell = FindRoomAnchorCell(blueprint, startRoom, new Vector2Int(startRoom.Bounds.xMin + 2, Mathf.RoundToInt(startRoom.Bounds.center.y)));
        blueprint.KeyCell = FindRoomAnchorCell(blueprint, keyRoom, new Vector2Int(keyRoom.Bounds.xMax - 2, keyRoom.Bounds.yMax - 2));
        blueprint.BatteryCell = FindRoomAnchorCell(blueprint, utilityRoom, new Vector2Int(utilityRoom.Bounds.xMax - 2, utilityRoom.Bounds.yMin + 2));
        blueprint.SafeRoomCell = FindRoomAnchorCell(blueprint, safeRoom, new Vector2Int(Mathf.RoundToInt(safeRoomCenter.x), Mathf.RoundToInt(safeRoomCenter.y)));
        blueprint.ExitCell = FindRoomAnchorCell(blueprint, exitRoom, new Vector2Int(exitRoom.Bounds.xMax - 2, Mathf.RoundToInt(exitRoom.Bounds.center.y)));
        blueprint.PatrolSpawnCell = FindRoomAnchorCell(blueprint, patrolRoom, new Vector2Int(Mathf.RoundToInt(patrolRoomCenter.x), Mathf.RoundToInt(patrolRoomCenter.y)));
        blueprint.GlassPanelCell = FindGlassPanelCell(blueprint, roomsById);
    }

    private static LogicalRoom FindRoomByType(Dictionary<int, LogicalRoom> roomsById, GeneratedRoomType roomType)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            if (room.Type == roomType)
            {
                return room;
            }
        }

        foreach (LogicalRoom room in roomsById.Values)
        {
            return room;
        }

        return null;
    }

    private static Vector3Int FindGlassPanelCell(GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        foreach (Vector3Int dangerCell in blueprint.DangerCellSet)
        {
            if (!blueprint.DoorCells.Contains(dangerCell) && !blueprint.ForcedWallCells.Contains(dangerCell))
            {
                return dangerCell;
            }
        }

        LogicalRoom dangerRoom = FindRoomByType(roomsById, GeneratedRoomType.DangerCorridor);
        return dangerRoom != null
            ? FindRoomAnchorCell(blueprint, dangerRoom, new Vector2Int(Mathf.RoundToInt(dangerRoom.Bounds.center.x), Mathf.RoundToInt(dangerRoom.Bounds.center.y)))
            : blueprint.PlayerStartCell;
    }

    private static void ReserveCriticalCells(GeneratedMapBlueprint blueprint)
    {
        ReserveCellCluster(blueprint.ReservedCells, blueprint.PlayerStartCell, 1);
        ReserveCellCluster(blueprint.ReservedCells, blueprint.KeyCell, 1);
        ReserveCellCluster(blueprint.ReservedCells, blueprint.BatteryCell, 1);
        ReserveCellCluster(blueprint.ReservedCells, blueprint.ExitCell, 1);
        ReserveCellCluster(blueprint.ReservedCells, blueprint.PatrolSpawnCell, 1);
        ReserveCellCluster(blueprint.ReservedCells, blueprint.GlassPanelCell, 1);
        ReserveCellCluster(blueprint.ReservedCells, blueprint.SafeRoomCell, 1);

        foreach (GeneratedDoorGroupData doorGroup in blueprint.DoorGroups)
        {
            foreach (Vector3Int cell in doorGroup.Cells)
            {
                ReserveCellCluster(blueprint.ReservedCells, cell, 1);
            }
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

    private static void GenerateCoverProps(System.Random random, GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        List<PropCandidate> mandatoryCandidates = new();
        List<PropCandidate> optionalCandidates = new();

        foreach (LogicalRoom room in roomsById.Values)
        {
            BuildPropCandidates(room, mandatoryCandidates, optionalCandidates);
        }

        Shuffle(random, optionalCandidates);

        foreach (PropCandidate candidate in mandatoryCandidates)
        {
            TryPlaceCoverProp(blueprint, candidate, requireSuccess: true);
        }

        foreach (PropCandidate candidate in optionalCandidates)
        {
            TryPlaceCoverProp(blueprint, candidate, requireSuccess: false);
        }
    }

    private static void BuildPropCandidates(LogicalRoom room, List<PropCandidate> mandatoryCandidates, List<PropCandidate> optionalCandidates)
    {
        GeneratedZoneType zoneType = MapRoomTypeToZone(room.Type);
        Vector2 roomCenter = room.Bounds.center;
        int centerX = Mathf.RoundToInt(roomCenter.x);
        int centerY = Mathf.RoundToInt(roomCenter.y);

        switch (room.Type)
        {
            case GeneratedRoomType.DangerCorridor:
            case GeneratedRoomType.PreExit:
                AddPropCandidate(mandatoryCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(centerX - 1, centerY - 1, 0), new Vector2Int(1, 2));
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1));
                break;
            case GeneratedRoomType.Safe:
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMin + 1, 0), new Vector2Int(1, 1));
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMax - 2, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1));
                break;
            case GeneratedRoomType.Utility:
            case GeneratedRoomType.Key:
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMin + 1, 0), new Vector2Int(2, 1));
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(room.Bounds.xMax - 2, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1));
                break;
            default:
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMin + 1, 0), new Vector2Int(1, 1));
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMax - 2, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1));
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(centerX - 1, centerY - 1, 0), new Vector2Int(1, 1));
                break;
        }
    }

    private static void AddPropCandidate(List<PropCandidate> targetList, LogicalRoom room, GeneratedZoneType zoneType, CoverPropType propType, Vector3Int originCell, Vector2Int size)
    {
        if (originCell.x < room.Bounds.xMin + 1
            || originCell.y < room.Bounds.yMin + 1
            || originCell.x + size.x > room.Bounds.xMax - 1
            || originCell.y + size.y > room.Bounds.yMax - 1)
        {
            return;
        }

        targetList.Add(new PropCandidate(propType, zoneType, originCell, size, isMandatory: false));
    }

    private static bool TryPlaceCoverProp(GeneratedMapBlueprint blueprint, PropCandidate candidate, bool requireSuccess)
    {
        Vector3Int[] occupiedCells = candidate.GetOccupiedCells();

        foreach (Vector3Int occupiedCell in occupiedCells)
        {
            if (!blueprint.GroundCells.Contains(occupiedCell)
                || blueprint.ForcedWallCells.Contains(occupiedCell)
                || blueprint.ReservedCells.Contains(occupiedCell)
                || blueprint.PropBlockedCells.Contains(occupiedCell)
                || blueprint.DoorCells.Contains(occupiedCell))
            {
                return !requireSuccess;
            }
        }

        HashSet<Vector3Int> nextBlockedCells = new(blueprint.PropBlockedCells);

        foreach (Vector3Int occupiedCell in occupiedCells)
        {
            nextBlockedCells.Add(occupiedCell);
        }

        if (!CanTraverse(blueprint, nextBlockedCells, gateClosed: true, blueprint.PlayerStartCell, blueprint.KeyCell)
            || !CanTraverse(blueprint, nextBlockedCells, gateClosed: true, blueprint.PlayerStartCell, blueprint.BatteryCell)
            || !CanTraverse(blueprint, nextBlockedCells, gateClosed: false, blueprint.KeyCell, blueprint.ExitCell))
        {
            return !requireSuccess;
        }

        CoverPropPlacement placement = candidate.ToPlacement();
        blueprint.CoverProps.Add(placement);

        foreach (Vector3Int occupiedCell in placement.OccupiedCells)
        {
            blueprint.PropBlockedCells.Add(occupiedCell);
        }

        return true;
    }

    private static void BuildMetadata(GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        blueprint.Rooms.Clear();
        blueprint.Zones.Clear();
        List<LogicalRoom> orderedRooms = new(roomsById.Values);
        orderedRooms.Sort((left, right) => left.Id.CompareTo(right.Id));

        foreach (LogicalRoom room in orderedRooms)
        {
            room.ConnectedRoomIds.Sort();
            Vector3Int[] doorCells = room.DoorCells.ToArray();
            Vector3Int roomAnchorCell = FindRoomAnchorCell(blueprint, room, new Vector2Int(Mathf.RoundToInt(room.Bounds.center.x), Mathf.RoundToInt(room.Bounds.center.y)));
            blueprint.Rooms.Add(new GeneratedRoomData(room.Id, room.Type, room.Bounds, roomAnchorCell, doorCells, room.ConnectedRoomIds.ToArray()));
            blueprint.Zones.Add(new GeneratedZoneData(MapRoomTypeToZone(room.Type), room.Bounds, roomAnchorCell, doorCells));
        }
    }

    private static Vector3Int FindRoomAnchorCell(GeneratedMapBlueprint blueprint, LogicalRoom room, Vector2Int preferredCell)
    {
        if (room == null)
        {
            return Vector3Int.zero;
        }

        Vector2 roomCenter = room.Bounds.center;
        Vector3Int bestCell = new(Mathf.RoundToInt(roomCenter.x), Mathf.RoundToInt(roomCenter.y), 0);
        int bestScore = int.MaxValue;
        bool foundWalkableCell = false;

        for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
        {
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                Vector3Int candidate = new(x, y, 0);

                if (!blueprint.GroundCells.Contains(candidate)
                    || blueprint.ForcedWallCells.Contains(candidate)
                    || blueprint.DoorCells.Contains(candidate)
                    || blueprint.PropBlockedCells.Contains(candidate))
                {
                    continue;
                }

                int borderPenalty = x <= room.Bounds.xMin + 1
                    || x >= room.Bounds.xMax - 2
                    || y <= room.Bounds.yMin + 1
                    || y >= room.Bounds.yMax - 2
                    ? 2
                    : 0;
                int distanceScore = Mathf.Abs(preferredCell.x - x) + Mathf.Abs(preferredCell.y - y) + borderPenalty;

                if (!foundWalkableCell || distanceScore < bestScore)
                {
                    bestCell = candidate;
                    bestScore = distanceScore;
                    foundWalkableCell = true;
                }
            }
        }

        return bestCell;
    }

    private static GeneratedZoneType MapRoomTypeToZone(GeneratedRoomType roomType)
    {
        return roomType switch
        {
            GeneratedRoomType.Start => GeneratedZoneType.Start,
            GeneratedRoomType.Key => GeneratedZoneType.Security,
            GeneratedRoomType.PreExit => GeneratedZoneType.Security,
            GeneratedRoomType.Exit => GeneratedZoneType.Exit,
            GeneratedRoomType.Utility => GeneratedZoneType.Utility,
            GeneratedRoomType.DangerCorridor => GeneratedZoneType.Facility,
            _ => GeneratedZoneType.OpenOffice
        };
    }

    private static bool ValidateBlueprint(GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        if (roomsById.Count < 18 || roomsById.Count > 22)
        {
            return false;
        }

        if (blueprint.MainDoorCells.Length == 0
            || !CanTraverse(blueprint, blueprint.PropBlockedCells, gateClosed: true, blueprint.PlayerStartCell, blueprint.KeyCell)
            || !CanTraverse(blueprint, blueprint.PropBlockedCells, gateClosed: true, blueprint.PlayerStartCell, blueprint.BatteryCell)
            || CanTraverse(blueprint, blueprint.PropBlockedCells, gateClosed: true, blueprint.PlayerStartCell, blueprint.ExitCell)
            || !CanTraverse(blueprint, blueprint.PropBlockedCells, gateClosed: false, blueprint.KeyCell, blueprint.ExitCell))
        {
            return false;
        }

        LogicalRoom safeRoom = FindRoomByType(roomsById, GeneratedRoomType.Safe);

        if (safeRoom == null)
        {
            return false;
        }

        foreach (Vector3Int dangerCell in blueprint.DangerCellSet)
        {
            if (safeRoom.Bounds.Contains((Vector2Int)dangerCell))
            {
                return false;
            }
        }

        int dangerCoverCount = 0;

        foreach (CoverPropPlacement placement in blueprint.CoverProps)
        {
            foreach (Vector3Int occupiedCell in placement.OccupiedCells)
            {
                if (blueprint.DangerCellSet.Contains(occupiedCell) || IsNearDangerCell(blueprint.DangerCellSet, occupiedCell))
                {
                    dangerCoverCount++;
                    break;
                }
            }
        }

        return dangerCoverCount >= 2;
    }

    private static bool IsNearDangerCell(HashSet<Vector3Int> dangerCells, Vector3Int occupiedCell)
    {
        foreach (Vector2Int offset in CardinalOffsets)
        {
            if (dangerCells.Contains(occupiedCell + (Vector3Int)offset))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanTraverse(GeneratedMapBlueprint blueprint, HashSet<Vector3Int> blockedProps, bool gateClosed, Vector3Int startCell, Vector3Int goalCell)
    {
        if (!IsTraversable(blueprint, blockedProps, gateClosed, startCell) || !IsTraversable(blueprint, blockedProps, gateClosed, goalCell))
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

            foreach (Vector2Int offset in CardinalOffsets)
            {
                Vector3Int neighbor = currentCell + (Vector3Int)offset;

                if (visited.Contains(neighbor) || !IsTraversable(blueprint, blockedProps, gateClosed, neighbor))
                {
                    continue;
                }

                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
            }
        }

        return false;
    }

    private static bool IsTraversable(GeneratedMapBlueprint blueprint, HashSet<Vector3Int> blockedProps, bool gateClosed, Vector3Int cell)
    {
        if (!blueprint.GroundCells.Contains(cell)
            || blueprint.ForcedWallCells.Contains(cell)
            || blockedProps.Contains(cell))
        {
            return false;
        }

        if (gateClosed)
        {
            foreach (Vector3Int gateCell in blueprint.MainDoorCells)
            {
                if (gateCell == cell)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Shuffle<T>(System.Random random, List<T> list)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (list[index], list[swapIndex]) = (list[swapIndex], list[index]);
        }
    }
}


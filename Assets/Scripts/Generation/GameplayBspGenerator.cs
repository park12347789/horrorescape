/*
 * File Role:
 * Creates a procedural layout using binary space partitioning rules.
 *
 * Runtime Use:
 * Splits space into rooms and connectors, then converts the result into shared map metadata.
 *
 * Study Notes:
 * Study this to compare BSP generation with the graph and WFC approaches in the project.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameplayBspGenerator
{
    private static readonly Vector3Int[] CardinalOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    };

    private const int FloorMinX = -60;
    private const int FloorMinY = -42;
    private const int FloorWidth = 120;
    private const int FloorHeight = 84;
    private const int MinLeafWidth = 12;
    private const int MinLeafHeight = 10;
    private const int MinRoomWidth = 7;
    private const int MinRoomHeight = 6;
    private const int CorridorWidth = 2;
    private const int SplitAttempts = 64;

    private sealed class BspNode
    {
        public RectInt Bounds;
        public BspNode Left;
        public BspNode Right;
        public int RoomId = -1;

        public bool IsLeaf => Left == null && Right == null;
    }

    private sealed class LogicalRoom
    {
        public int Id;
        public RectInt Bounds;
        public GeneratedRoomType RoomType = GeneratedRoomType.Explore;
        public bool IsMainRoute;
        public bool IsPostGate;
        public readonly List<int> ConnectedRoomIds = new();
        public readonly List<Vector3Int> DoorCells = new();
    }

    private sealed class LogicalEdge
    {
        public int RoomAId;
        public int RoomBId;
        public bool IsMainRoute;
        public bool IsMainGate;
    }

    private readonly struct DoorSegment
    {
        public DoorSegment(Vector3Int[] cells, bool isVertical)
        {
            Cells = cells ?? Array.Empty<Vector3Int>();
            IsVertical = isVertical;
        }

        public Vector3Int[] Cells { get; }
        public bool IsVertical { get; }
        public Vector3Int AnchorCell => Cells.Length > 0 ? Cells[0] : Vector3Int.zero;
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
            int index = 0;

            for (int x = 0; x < Size.x; x++)
            {
                for (int y = 0; y < Size.y; y++)
                {
                    occupiedCells[index++] = new Vector3Int(OriginCell.x + x, OriginCell.y + y, 0);
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
        System.Random random = new(seed);

        for (int attempt = 0; attempt < 48; attempt++)
        {
            if (TryGenerateSingle(random.Next(), out blueprint))
            {
                return true;
            }
        }

        blueprint = null;
        return false;
    }

    private static bool TryGenerateSingle(int seed, out GeneratedMapBlueprint blueprint)
    {
        System.Random random = new(seed);
        blueprint = new GeneratedMapBlueprint();
        int targetRoomCount = random.Next(18, 23);

        if (!TryBuildTree(random, targetRoomCount, out BspNode root, out List<BspNode> leaves))
        {
            return false;
        }

        Dictionary<int, LogicalRoom> roomsById = CreateRooms(random, leaves);

        if (roomsById.Count < 18 || roomsById.Count > 22)
        {
            return false;
        }

        List<LogicalEdge> edges = new();
        BuildTreeConnections(root, roomsById, edges);
        AddLoopConnections(random, roomsById, edges);

        if (!AssignGameplayRoles(roomsById, edges, out List<int> mainPath))
        {
            return false;
        }

        FillRooms(blueprint, roomsById);
        BuildRoutesAndDoors(random, blueprint, roomsById, edges);
        AssignObjectiveCells(blueprint, roomsById);
        ReserveCriticalCells(blueprint);
        AddSightlineBreakers(random, blueprint, roomsById);
        GenerateCoverProps(random, blueprint, roomsById);
        BuildMetadata(blueprint, roomsById);
        return ValidateBlueprint(blueprint, roomsById, mainPath);
    }

    private static bool TryBuildTree(System.Random random, int targetRoomCount, out BspNode root, out List<BspNode> leaves)
    {
        root = new BspNode { Bounds = new RectInt(FloorMinX, FloorMinY, FloorWidth, FloorHeight) };
        leaves = new List<BspNode> { root };

        for (int splitIndex = 0; splitIndex < SplitAttempts && leaves.Count < targetRoomCount; splitIndex++)
        {
            BspNode leafToSplit = GetLargestSplittableLeaf(leaves);

            if (leafToSplit == null || !TrySplitLeaf(random, leafToSplit, out BspNode leftChild, out BspNode rightChild))
            {
                break;
            }

            leaves.Remove(leafToSplit);
            leafToSplit.Left = leftChild;
            leafToSplit.Right = rightChild;
            leaves.Add(leftChild);
            leaves.Add(rightChild);
        }

        return leaves.Count >= 18 && leaves.Count <= 22;
    }

    private static BspNode GetLargestSplittableLeaf(List<BspNode> leaves)
    {
        BspNode bestLeaf = null;
        int bestArea = int.MinValue;

        foreach (BspNode leaf in leaves)
        {
            if (!CanSplit(leaf.Bounds))
            {
                continue;
            }

            int area = leaf.Bounds.width * leaf.Bounds.height;

            if (area > bestArea)
            {
                bestArea = area;
                bestLeaf = leaf;
            }
        }

        return bestLeaf;
    }

    private static bool CanSplit(RectInt bounds)
    {
        return bounds.width >= (MinLeafWidth * 2) || bounds.height >= (MinLeafHeight * 2);
    }

    private static bool TrySplitLeaf(System.Random random, BspNode leaf, out BspNode leftChild, out BspNode rightChild)
    {
        leftChild = null;
        rightChild = null;

        bool splitHorizontally = ChooseSplitOrientation(random, leaf.Bounds);

        if (!TryCreateChildren(random, leaf.Bounds, splitHorizontally, out leftChild, out rightChild)
            && !TryCreateChildren(random, leaf.Bounds, !splitHorizontally, out leftChild, out rightChild))
        {
            return false;
        }

        return true;
    }

    private static bool ChooseSplitOrientation(System.Random random, RectInt bounds)
    {
        if (bounds.width >= bounds.height + 6)
        {
            return false;
        }

        if (bounds.height >= bounds.width + 6)
        {
            return true;
        }

        return random.NextDouble() < 0.5d;
    }

    private static bool TryCreateChildren(System.Random random, RectInt bounds, bool splitHorizontally, out BspNode leftChild, out BspNode rightChild)
    {
        leftChild = null;
        rightChild = null;

        if (splitHorizontally)
        {
            int splitMin = bounds.yMin + MinLeafHeight;
            int splitMax = bounds.yMax - MinLeafHeight;

            if (splitMax <= splitMin)
            {
                return false;
            }

            int splitY = random.Next(splitMin, splitMax);
            leftChild = new BspNode { Bounds = new RectInt(bounds.xMin, bounds.yMin, bounds.width, splitY - bounds.yMin) };
            rightChild = new BspNode { Bounds = new RectInt(bounds.xMin, splitY, bounds.width, bounds.yMax - splitY) };
            return true;
        }

        int verticalSplitMin = bounds.xMin + MinLeafWidth;
        int verticalSplitMax = bounds.xMax - MinLeafWidth;

        if (verticalSplitMax <= verticalSplitMin)
        {
            return false;
        }

        int splitX = random.Next(verticalSplitMin, verticalSplitMax);
        leftChild = new BspNode { Bounds = new RectInt(bounds.xMin, bounds.yMin, splitX - bounds.xMin, bounds.height) };
        rightChild = new BspNode { Bounds = new RectInt(splitX, bounds.yMin, bounds.xMax - splitX, bounds.height) };
        return true;
    }

    private static Dictionary<int, LogicalRoom> CreateRooms(System.Random random, List<BspNode> leaves)
    {
        Dictionary<int, LogicalRoom> roomsById = new();

        for (int leafIndex = 0; leafIndex < leaves.Count; leafIndex++)
        {
            BspNode leaf = leaves[leafIndex];
            RectInt roomBounds = CreateRoomBounds(random, leaf.Bounds);

            if (roomBounds.width < MinRoomWidth || roomBounds.height < MinRoomHeight)
            {
                continue;
            }

            leaf.RoomId = roomsById.Count;
            roomsById[leaf.RoomId] = new LogicalRoom
            {
                Id = leaf.RoomId,
                Bounds = roomBounds
            };
        }

        return roomsById;
    }

    private static RectInt CreateRoomBounds(System.Random random, RectInt leafBounds)
    {
        int maxInsetX = Mathf.Max(2, leafBounds.width / 5);
        int maxInsetY = Mathf.Max(2, leafBounds.height / 5);
        int leftInset = random.Next(1, maxInsetX + 1);
        int rightInset = random.Next(1, maxInsetX + 1);
        int bottomInset = random.Next(1, maxInsetY + 1);
        int topInset = random.Next(1, maxInsetY + 1);

        int width = Mathf.Max(MinRoomWidth, leafBounds.width - leftInset - rightInset);
        int height = Mathf.Max(MinRoomHeight, leafBounds.height - bottomInset - topInset);
        int xMin = leafBounds.xMin + leftInset;
        int yMin = leafBounds.yMin + bottomInset;

        if (xMin + width > leafBounds.xMax - 1)
        {
            xMin = leafBounds.xMax - width - 1;
        }

        if (yMin + height > leafBounds.yMax - 1)
        {
            yMin = leafBounds.yMax - height - 1;
        }

        return new RectInt(xMin, yMin, width, height);
    }

    private static void BuildTreeConnections(BspNode root, Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        ConnectSubtree(root, roomsById, edges);
    }

    private static List<int> ConnectSubtree(BspNode node, Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        if (node == null)
        {
            return new List<int>();
        }

        if (node.IsLeaf)
        {
            return node.RoomId >= 0 && roomsById.ContainsKey(node.RoomId)
                ? new List<int> { node.RoomId }
                : new List<int>();
        }

        List<int> leftRooms = ConnectSubtree(node.Left, roomsById, edges);
        List<int> rightRooms = ConnectSubtree(node.Right, roomsById, edges);

        if (leftRooms.Count > 0 && rightRooms.Count > 0)
        {
            FindClosestRoomPair(roomsById, leftRooms, rightRooms, out int leftRoomId, out int rightRoomId);
            AddEdge(edges, roomsById, leftRoomId, rightRoomId);
        }

        leftRooms.AddRange(rightRooms);
        return leftRooms;
    }

    private static void FindClosestRoomPair(Dictionary<int, LogicalRoom> roomsById, List<int> leftRooms, List<int> rightRooms, out int leftRoomId, out int rightRoomId)
    {
        leftRoomId = leftRooms[0];
        rightRoomId = rightRooms[0];
        int bestDistance = int.MaxValue;

        foreach (int leftCandidate in leftRooms)
        {
            foreach (int rightCandidate in rightRooms)
            {
                Vector2Int leftCenter = GetRectCenterCell(roomsById[leftCandidate].Bounds);
                Vector2Int rightCenter = GetRectCenterCell(roomsById[rightCandidate].Bounds);
                int distance = Mathf.Abs(leftCenter.x - rightCenter.x) + Mathf.Abs(leftCenter.y - rightCenter.y);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    leftRoomId = leftCandidate;
                    rightRoomId = rightCandidate;
                }
            }
        }
    }

    private static void AddLoopConnections(System.Random random, Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        Dictionary<int, HashSet<int>> adjacency = BuildAdjacency(edges);
        List<(int roomAId, int roomBId, int score)> candidates = new();
        List<int> roomIds = new(roomsById.Keys);

        for (int leftIndex = 0; leftIndex < roomIds.Count; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < roomIds.Count; rightIndex++)
            {
                int roomAId = roomIds[leftIndex];
                int roomBId = roomIds[rightIndex];

                if (adjacency.TryGetValue(roomAId, out HashSet<int> connectedRooms) && connectedRooms.Contains(roomBId))
                {
                    continue;
                }

                RectInt boundsA = roomsById[roomAId].Bounds;
                RectInt boundsB = roomsById[roomBId].Bounds;
                int xGap = GetAxisGap(boundsA.xMin, boundsA.xMax, boundsB.xMin, boundsB.xMax);
                int yGap = GetAxisGap(boundsA.yMin, boundsA.yMax, boundsB.yMin, boundsB.yMax);

                if (xGap > 12 || yGap > 12)
                {
                    continue;
                }

                candidates.Add((roomAId, roomBId, xGap + yGap));
            }
        }

        candidates.Sort((left, right) => left.score.CompareTo(right.score));
        int extraConnectionBudget = random.Next(2, 5);

        foreach (var candidate in candidates)
        {
            if (extraConnectionBudget <= 0)
            {
                break;
            }

            int roomAId = candidate.roomAId;
            int roomBId = candidate.roomBId;

            if (adjacency.TryGetValue(roomAId, out HashSet<int> roomAConnections) && roomAConnections.Contains(roomBId))
            {
                continue;
            }

            AddEdge(edges, roomsById, roomAId, roomBId);

            if (!adjacency.TryGetValue(roomAId, out roomAConnections))
            {
                roomAConnections = new HashSet<int>();
                adjacency[roomAId] = roomAConnections;
            }

            if (!adjacency.TryGetValue(roomBId, out HashSet<int> roomBConnections))
            {
                roomBConnections = new HashSet<int>();
                adjacency[roomBId] = roomBConnections;
            }

            roomAConnections.Add(roomBId);
            roomBConnections.Add(roomAId);
            extraConnectionBudget--;
        }
    }

    private static int GetAxisGap(int minA, int maxA, int minB, int maxB)
    {
        if (maxA < minB)
        {
            return minB - maxA;
        }

        if (maxB < minA)
        {
            return minA - maxB;
        }

        return 0;
    }

    private static void AddEdge(List<LogicalEdge> edges, Dictionary<int, LogicalRoom> roomsById, int roomAId, int roomBId)
    {
        foreach (LogicalEdge edge in edges)
        {
            if ((edge.RoomAId == roomAId && edge.RoomBId == roomBId) || (edge.RoomAId == roomBId && edge.RoomBId == roomAId))
            {
                return;
            }
        }

        edges.Add(new LogicalEdge
        {
            RoomAId = roomAId,
            RoomBId = roomBId
        });

        if (!roomsById[roomAId].ConnectedRoomIds.Contains(roomBId))
        {
            roomsById[roomAId].ConnectedRoomIds.Add(roomBId);
        }

        if (!roomsById[roomBId].ConnectedRoomIds.Contains(roomAId))
        {
            roomsById[roomBId].ConnectedRoomIds.Add(roomAId);
        }
    }

    private static bool AssignGameplayRoles(Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges, out List<int> mainPath)
    {
        mainPath = FindDiameterPath(roomsById, edges);

        if (mainPath.Count < 7)
        {
            return false;
        }

        if (GetRectCenterCell(roomsById[mainPath[0]].Bounds).x > GetRectCenterCell(roomsById[mainPath[mainPath.Count - 1]].Bounds).x)
        {
            mainPath.Reverse();
        }

        HashSet<int> mainPathSet = new(mainPath);
        Dictionary<int, HashSet<int>> adjacency = BuildAdjacency(edges);
        NormalizeRoleIndices(mainPath.Count, out int dangerAIndex, out int keyIndex, out int dangerBIndex, out int preExitIndex);

        for (int index = 0; index < mainPath.Count; index++)
        {
            LogicalRoom room = roomsById[mainPath[index]];
            room.IsMainRoute = true;
            room.RoomType = GeneratedRoomType.Explore;
        }

        roomsById[mainPath[0]].RoomType = GeneratedRoomType.Start;
        roomsById[mainPath[dangerAIndex]].RoomType = GeneratedRoomType.DangerCorridor;
        roomsById[mainPath[keyIndex]].RoomType = GeneratedRoomType.Key;
        roomsById[mainPath[dangerBIndex]].RoomType = GeneratedRoomType.DangerCorridor;
        roomsById[mainPath[preExitIndex]].RoomType = GeneratedRoomType.PreExit;
        roomsById[mainPath[mainPath.Count - 1]].RoomType = GeneratedRoomType.Exit;

        int gateRoomAId = mainPath[preExitIndex];
        int gateRoomBId = mainPath[mainPath.Count - 1];

        foreach (LogicalEdge edge in edges)
        {
            edge.IsMainRoute = IsConsecutiveMainPathEdge(mainPath, edge.RoomAId, edge.RoomBId);
            edge.IsMainGate = (edge.RoomAId == gateRoomAId && edge.RoomBId == gateRoomBId)
                || (edge.RoomAId == gateRoomBId && edge.RoomBId == gateRoomAId);
        }

        MarkPostGateRooms(roomsById, adjacency, gateRoomAId, gateRoomBId);
        PruneCrossGateEdges(roomsById, edges);
        adjacency = BuildAdjacency(edges);
        RebuildConnectedRoomIds(roomsById, edges);

        if (!TryAssignBranchRoomType(roomsById, adjacency, mainPath, mainPathSet, 1, keyIndex, GeneratedRoomType.Safe)
            || !TryAssignBranchRoomType(roomsById, adjacency, mainPath, mainPathSet, dangerAIndex, dangerBIndex, GeneratedRoomType.Utility))
        {
            return false;
        }

        return true;
    }

    private static void NormalizeRoleIndices(int pathLength, out int dangerAIndex, out int keyIndex, out int dangerBIndex, out int preExitIndex)
    {
        dangerAIndex = Mathf.Clamp(pathLength / 4, 1, pathLength - 5);
        keyIndex = Mathf.Clamp(pathLength / 2, dangerAIndex + 2, pathLength - 4);
        dangerBIndex = Mathf.Clamp((pathLength * 3) / 4, keyIndex + 1, pathLength - 3);
        preExitIndex = pathLength - 2;

        if (dangerBIndex >= preExitIndex)
        {
            dangerBIndex = Mathf.Max(keyIndex + 1, preExitIndex - 1);
        }
    }

    private static bool IsConsecutiveMainPathEdge(List<int> mainPath, int roomAId, int roomBId)
    {
        for (int index = 0; index < mainPath.Count - 1; index++)
        {
            int leftRoomId = mainPath[index];
            int rightRoomId = mainPath[index + 1];

            if ((leftRoomId == roomAId && rightRoomId == roomBId) || (leftRoomId == roomBId && rightRoomId == roomAId))
            {
                return true;
            }
        }

        return false;
    }

    private static void MarkPostGateRooms(Dictionary<int, LogicalRoom> roomsById, Dictionary<int, HashSet<int>> adjacency, int gateRoomAId, int gateRoomBId)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            room.IsPostGate = false;
        }

        Queue<int> frontier = new();
        HashSet<int> visited = new() { gateRoomBId };
        frontier.Enqueue(gateRoomBId);

        while (frontier.Count > 0)
        {
            int roomId = frontier.Dequeue();
            roomsById[roomId].IsPostGate = true;

            if (!adjacency.TryGetValue(roomId, out HashSet<int> connectedRooms))
            {
                continue;
            }

            foreach (int connectedRoomId in connectedRooms)
            {
                bool isGateEdge = (roomId == gateRoomAId && connectedRoomId == gateRoomBId) || (roomId == gateRoomBId && connectedRoomId == gateRoomAId);

                if (isGateEdge || !visited.Add(connectedRoomId))
                {
                    continue;
                }

                frontier.Enqueue(connectedRoomId);
            }
        }
    }

    private static void PruneCrossGateEdges(Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        for (int edgeIndex = edges.Count - 1; edgeIndex >= 0; edgeIndex--)
        {
            LogicalEdge edge = edges[edgeIndex];

            if (edge.IsMainGate)
            {
                continue;
            }

            bool roomAIsPostGate = roomsById[edge.RoomAId].IsPostGate;
            bool roomBIsPostGate = roomsById[edge.RoomBId].IsPostGate;

            if (roomAIsPostGate != roomBIsPostGate)
            {
                edges.RemoveAt(edgeIndex);
            }
        }
    }

    private static void RebuildConnectedRoomIds(Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            room.ConnectedRoomIds.Clear();
        }

        foreach (LogicalEdge edge in edges)
        {
            roomsById[edge.RoomAId].ConnectedRoomIds.Add(edge.RoomBId);
            roomsById[edge.RoomBId].ConnectedRoomIds.Add(edge.RoomAId);
        }
    }

    private static bool TryAssignBranchRoomType(
        Dictionary<int, LogicalRoom> roomsById,
        Dictionary<int, HashSet<int>> adjacency,
        List<int> mainPath,
        HashSet<int> mainPathSet,
        int minMainIndex,
        int maxMainIndex,
        GeneratedRoomType roomType)
    {
        List<int> candidates = new();

        for (int index = Mathf.Max(0, minMainIndex); index <= Mathf.Min(maxMainIndex, mainPath.Count - 1); index++)
        {
            int mainRoomId = mainPath[index];

            if (!adjacency.TryGetValue(mainRoomId, out HashSet<int> connectedRooms))
            {
                continue;
            }

            foreach (int connectedRoomId in connectedRooms)
            {
                if (!mainPathSet.Contains(connectedRoomId) && roomsById[connectedRoomId].RoomType == GeneratedRoomType.Explore)
                {
                    if ((roomType == GeneratedRoomType.Safe || roomType == GeneratedRoomType.Utility) && roomsById[connectedRoomId].IsPostGate)
                    {
                        continue;
                    }

                    candidates.Add(connectedRoomId);
                }
            }
        }

        if (candidates.Count == 0)
        {
            foreach (KeyValuePair<int, LogicalRoom> pair in roomsById)
            {
                if (!mainPathSet.Contains(pair.Key)
                    && pair.Value.RoomType == GeneratedRoomType.Explore
                    && ((roomType != GeneratedRoomType.Safe && roomType != GeneratedRoomType.Utility) || !pair.Value.IsPostGate))
                {
                    candidates.Add(pair.Key);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        int candidateId = candidates[0];
        int bestDistance = int.MaxValue;

        foreach (int connectedRoomId in candidates)
        {
            Vector2Int candidateCenter = GetRectCenterCell(roomsById[connectedRoomId].Bounds);
            int distance = Mathf.Abs(candidateCenter.x - GetRectCenterCell(roomsById[mainPath[minMainIndex]].Bounds).x)
                + Mathf.Abs(candidateCenter.y - GetRectCenterCell(roomsById[mainPath[minMainIndex]].Bounds).y);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                candidateId = connectedRoomId;
            }
        }

        roomsById[candidateId].RoomType = roomType;
        return true;
    }

    private static List<int> FindDiameterPath(Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        Dictionary<int, HashSet<int>> adjacency = BuildAdjacency(edges);
        int seedRoomId = FindLeftmostRoomId(roomsById);
        int farthestFromSeed = FindFarthestRoom(seedRoomId, adjacency, out _);
        int farthestFromA = FindFarthestRoom(farthestFromSeed, adjacency, out Dictionary<int, int> parentByRoom);
        return BuildPath(parentByRoom, farthestFromSeed, farthestFromA);
    }

    private static int FindLeftmostRoomId(Dictionary<int, LogicalRoom> roomsById)
    {
        int selectedRoomId = -1;
        int minX = int.MaxValue;

        foreach (KeyValuePair<int, LogicalRoom> pair in roomsById)
        {
            int centerX = GetRectCenterCell(pair.Value.Bounds).x;

            if (centerX < minX)
            {
                minX = centerX;
                selectedRoomId = pair.Key;
            }
        }

        return selectedRoomId;
    }

    private static int FindFarthestRoom(int startRoomId, Dictionary<int, HashSet<int>> adjacency, out Dictionary<int, int> parentByRoom)
    {
        parentByRoom = new Dictionary<int, int>();
        Dictionary<int, int> distanceByRoom = new() { [startRoomId] = 0 };
        Queue<int> frontier = new();
        frontier.Enqueue(startRoomId);
        int farthestRoomId = startRoomId;

        while (frontier.Count > 0)
        {
            int currentRoomId = frontier.Dequeue();

            if (distanceByRoom[currentRoomId] > distanceByRoom[farthestRoomId])
            {
                farthestRoomId = currentRoomId;
            }

            if (!adjacency.TryGetValue(currentRoomId, out HashSet<int> connectedRooms))
            {
                continue;
            }

            foreach (int connectedRoomId in connectedRooms)
            {
                if (distanceByRoom.ContainsKey(connectedRoomId))
                {
                    continue;
                }

                distanceByRoom[connectedRoomId] = distanceByRoom[currentRoomId] + 1;
                parentByRoom[connectedRoomId] = currentRoomId;
                frontier.Enqueue(connectedRoomId);
            }
        }

        return farthestRoomId;
    }

    private static List<int> BuildPath(Dictionary<int, int> parentByRoom, int startRoomId, int endRoomId)
    {
        List<int> path = new() { endRoomId };
        int currentRoomId = endRoomId;

        while (currentRoomId != startRoomId && parentByRoom.TryGetValue(currentRoomId, out int parentRoomId))
        {
            currentRoomId = parentRoomId;
            path.Add(currentRoomId);
        }

        path.Reverse();
        return path;
    }

    private static Dictionary<int, HashSet<int>> BuildAdjacency(List<LogicalEdge> edges)
    {
        Dictionary<int, HashSet<int>> adjacency = new();

        foreach (LogicalEdge edge in edges)
        {
            if (!adjacency.TryGetValue(edge.RoomAId, out HashSet<int> roomAConnections))
            {
                roomAConnections = new HashSet<int>();
                adjacency[edge.RoomAId] = roomAConnections;
            }

            if (!adjacency.TryGetValue(edge.RoomBId, out HashSet<int> roomBConnections))
            {
                roomBConnections = new HashSet<int>();
                adjacency[edge.RoomBId] = roomBConnections;
            }

            roomAConnections.Add(edge.RoomBId);
            roomBConnections.Add(edge.RoomAId);
        }

        return adjacency;
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

    private static void BuildRoutesAndDoors(System.Random random, GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        int doorGroupId = 0;

        foreach (LogicalEdge edge in edges)
        {
            LogicalRoom roomA = roomsById[edge.RoomAId];
            LogicalRoom roomB = roomsById[edge.RoomBId];
            DoorSegment doorA = GetDoorSegment(roomA.Bounds, roomB.Bounds);
            DoorSegment doorB = GetDoorSegment(roomB.Bounds, roomA.Bounds);
            List<Vector3Int> routeCells = BuildCorridorCells(doorA, doorB, random);

            foreach (Vector3Int routeCell in routeCells)
            {
                blueprint.GroundCells.Add(routeCell);
            }

            GeneratedRouteType routeType = edge.IsMainGate
                ? GeneratedRouteType.GateApproach
                : edge.IsMainRoute
                    ? GeneratedRouteType.MainRoute
                    : GeneratedRouteType.BranchRoute;
            blueprint.Routes.Add(new GeneratedRouteData(edge.RoomAId, edge.RoomBId, routeType, routeCells.ToArray()));

            if (edge.IsMainGate)
            {
                Vector3Int[] gateCells = CreateGateCells(doorA, doorB);
                RegisterDoorGroup(blueprint, ref doorGroupId, true, edge.RoomAId, edge.RoomBId, gateCells);
                roomA.DoorCells.AddRange(gateCells);
                roomB.DoorCells.AddRange(gateCells);
                blueprint.MainDoorCells = gateCells;
            }
            else
            {
                RegisterDoorGroup(blueprint, ref doorGroupId, false, edge.RoomAId, edge.RoomBId, doorA.Cells);
                RegisterDoorGroup(blueprint, ref doorGroupId, false, edge.RoomAId, edge.RoomBId, doorB.Cells);
                roomA.DoorCells.AddRange(doorA.Cells);
                roomB.DoorCells.AddRange(doorB.Cells);
            }

            if (edge.IsMainGate || roomA.RoomType == GeneratedRoomType.DangerCorridor || roomB.RoomType == GeneratedRoomType.DangerCorridor || roomA.RoomType == GeneratedRoomType.PreExit || roomB.RoomType == GeneratedRoomType.PreExit)
            {
                foreach (Vector3Int routeCell in routeCells)
                {
                    blueprint.DangerCellSet.Add(routeCell);
                }
            }
        }
    }

    private static DoorSegment GetDoorSegment(RectInt fromRoom, RectInt toRoom)
    {
        Vector2Int fromCenter = GetRectCenterCell(fromRoom);
        Vector2Int toCenter = GetRectCenterCell(toRoom);
        int deltaX = toCenter.x - fromCenter.x;
        int deltaY = toCenter.y - fromCenter.y;

        if (Mathf.Abs(deltaX) >= Mathf.Abs(deltaY))
        {
            int doorwayX = deltaX >= 0 ? fromRoom.xMax - 1 : fromRoom.xMin;
            int doorBottom = Mathf.Clamp(Mathf.RoundToInt((fromCenter.y + toCenter.y) * 0.5f) - 1, fromRoom.yMin + 1, fromRoom.yMax - 3);
            return new DoorSegment(
                new[]
                {
                    new Vector3Int(doorwayX, doorBottom, 0),
                    new Vector3Int(doorwayX, doorBottom + 1, 0)
                },
                true);
        }

        int doorwayY = deltaY >= 0 ? fromRoom.yMax - 1 : fromRoom.yMin;
        int doorLeft = Mathf.Clamp(Mathf.RoundToInt((fromCenter.x + toCenter.x) * 0.5f) - 1, fromRoom.xMin + 1, fromRoom.xMax - 3);
        return new DoorSegment(
            new[]
            {
                new Vector3Int(doorLeft, doorwayY, 0),
                new Vector3Int(doorLeft + 1, doorwayY, 0)
            },
            false);
    }

    private static List<Vector3Int> BuildCorridorCells(DoorSegment doorA, DoorSegment doorB, System.Random random)
    {
        HashSet<Vector3Int> routeCellSet = new();
        Vector3Int startCell = doorA.AnchorCell;
        Vector3Int endCell = doorB.AnchorCell;

        if (random.NextDouble() < 0.5d)
        {
            CarveHorizontal(routeCellSet, startCell.x, endCell.x, startCell.y);
            CarveVertical(routeCellSet, startCell.y, endCell.y, endCell.x);
        }
        else
        {
            CarveVertical(routeCellSet, startCell.y, endCell.y, startCell.x);
            CarveHorizontal(routeCellSet, startCell.x, endCell.x, endCell.y);
        }

        foreach (Vector3Int cell in doorA.Cells)
        {
            routeCellSet.Add(cell);
        }

        foreach (Vector3Int cell in doorB.Cells)
        {
            routeCellSet.Add(cell);
        }

        return new List<Vector3Int>(routeCellSet);
    }

    private static void CarveHorizontal(HashSet<Vector3Int> cells, int startX, int endX, int bottomY)
    {
        int minX = Mathf.Min(startX, endX);
        int maxX = Mathf.Max(startX, endX);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = bottomY; y < bottomY + CorridorWidth; y++)
            {
                cells.Add(new Vector3Int(x, y, 0));
            }
        }
    }

    private static void CarveVertical(HashSet<Vector3Int> cells, int startY, int endY, int leftX)
    {
        int minY = Mathf.Min(startY, endY);
        int maxY = Mathf.Max(startY, endY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = leftX; x < leftX + CorridorWidth; x++)
            {
                cells.Add(new Vector3Int(x, y, 0));
            }
        }
    }

    private static Vector3Int[] CreateGateCells(DoorSegment doorA, DoorSegment doorB)
    {
        if (doorA.IsVertical)
        {
            int gateX = (doorA.AnchorCell.x + doorB.AnchorCell.x) / 2;
            int gateBottom = (doorA.AnchorCell.y + doorB.AnchorCell.y) / 2;
            return new[]
            {
                new Vector3Int(gateX, gateBottom, 0),
                new Vector3Int(gateX, gateBottom + 1, 0)
            };
        }

        int gateLeft = (doorA.AnchorCell.x + doorB.AnchorCell.x) / 2;
        int gateY = (doorA.AnchorCell.y + doorB.AnchorCell.y) / 2;
        return new[]
        {
            new Vector3Int(gateLeft, gateY, 0),
            new Vector3Int(gateLeft + 1, gateY, 0)
        };
    }

    private static void RegisterDoorGroup(GeneratedMapBlueprint blueprint, ref int doorGroupId, bool requiresKey, int roomAId, int roomBId, Vector3Int[] cells)
    {
        blueprint.DoorGroups.Add(new GeneratedDoorGroupData(doorGroupId++, requiresKey, cells, new[] { roomAId, roomBId }));

        foreach (Vector3Int cell in cells)
        {
            blueprint.DoorCells.Add(cell);
            blueprint.GroundCells.Add(cell);
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

        blueprint.PlayerStartCell = FindRoomAnchorCell(blueprint, startRoom, GetRectCenterCell(startRoom.Bounds));
        blueprint.KeyCell = FindRoomAnchorCell(blueprint, keyRoom, GetRectCenterCell(keyRoom.Bounds));
        blueprint.BatteryCell = FindRoomAnchorCell(blueprint, utilityRoom, GetRectCenterCell(utilityRoom.Bounds));
        blueprint.SafeRoomCell = FindRoomAnchorCell(blueprint, safeRoom, GetRectCenterCell(safeRoom.Bounds));
        blueprint.ExitCell = FindRoomAnchorCell(blueprint, exitRoom, GetRectCenterCell(exitRoom.Bounds));
        blueprint.PatrolSpawnCell = FindRoomAnchorCell(blueprint, patrolRoom, GetRectCenterCell(patrolRoom.Bounds));
        blueprint.GlassPanelCell = FindDangerAnchorCell(blueprint, roomsById);
    }

    private static LogicalRoom FindRoomByType(Dictionary<int, LogicalRoom> roomsById, GeneratedRoomType roomType)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            if (room.RoomType == roomType)
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

    private static Vector3Int FindDangerAnchorCell(GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        foreach (Vector3Int dangerCell in blueprint.DangerCellSet)
        {
            if (!blueprint.DoorCells.Contains(dangerCell))
            {
                return dangerCell;
            }
        }

        LogicalRoom dangerRoom = FindRoomByType(roomsById, GeneratedRoomType.DangerCorridor);
        return dangerRoom != null ? FindRoomAnchorCell(blueprint, dangerRoom, GetRectCenterCell(dangerRoom.Bounds)) : blueprint.PlayerStartCell;
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
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                reservedCells.Add(new Vector3Int(centerCell.x + x, centerCell.y + y, 0));
            }
        }
    }

    private static void AddSightlineBreakers(System.Random random, GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            if (room.RoomType == GeneratedRoomType.DangerCorridor || room.Bounds.width < 9 || room.Bounds.height < 8)
            {
                continue;
            }

            Vector2Int centerCell = GetRectCenterCell(room.Bounds);
            int width = random.NextDouble() < 0.5d ? 2 : 3;
            int height = random.NextDouble() < 0.5d ? 2 : 3;
            Vector3Int originCell = new(centerCell.x - (width / 2), centerCell.y - (height / 2), 0);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3Int cell = new(originCell.x + x, originCell.y + y, 0);

                    if (cell.x <= room.Bounds.xMin + 1 || cell.x >= room.Bounds.xMax - 2 || cell.y <= room.Bounds.yMin + 1 || cell.y >= room.Bounds.yMax - 2)
                    {
                        continue;
                    }

                    if (blueprint.ReservedCells.Contains(cell) || blueprint.DoorCells.Contains(cell))
                    {
                        continue;
                    }

                    blueprint.ForcedWallCells.Add(cell);
                }
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
            TryPlaceCoverProp(blueprint, candidate, true);
        }

        foreach (PropCandidate candidate in optionalCandidates)
        {
            TryPlaceCoverProp(blueprint, candidate, false);
        }
    }

    private static void BuildPropCandidates(LogicalRoom room, List<PropCandidate> mandatoryCandidates, List<PropCandidate> optionalCandidates)
    {
        GeneratedZoneType zoneType = MapRoomTypeToZone(room.RoomType);
        Vector2Int center = GetRectCenterCell(room.Bounds);

        switch (room.RoomType)
        {
            case GeneratedRoomType.DangerCorridor:
            case GeneratedRoomType.PreExit:
                AddPropCandidate(mandatoryCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(center.x - 1, center.y - 1, 0), new Vector2Int(1, 2), true);
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1), false);
                break;
            case GeneratedRoomType.Safe:
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMin + 1, 0), new Vector2Int(1, 1), false);
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMax - 2, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1), false);
                break;
            case GeneratedRoomType.Utility:
            case GeneratedRoomType.Key:
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMin + 1, 0), new Vector2Int(2, 1), false);
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.CrateStack, new Vector3Int(room.Bounds.xMax - 2, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1), false);
                break;
            default:
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMin + 1, room.Bounds.yMin + 1, 0), new Vector2Int(1, 1), false);
                AddPropCandidate(optionalCandidates, room, zoneType, CoverPropType.Planter, new Vector3Int(room.Bounds.xMax - 2, room.Bounds.yMax - 2, 0), new Vector2Int(1, 1), false);
                break;
        }
    }

    private static void AddPropCandidate(List<PropCandidate> targetList, LogicalRoom room, GeneratedZoneType zoneType, CoverPropType propType, Vector3Int originCell, Vector2Int size, bool isMandatory)
    {
        if (originCell.x < room.Bounds.xMin + 1
            || originCell.y < room.Bounds.yMin + 1
            || originCell.x + size.x > room.Bounds.xMax - 1
            || originCell.y + size.y > room.Bounds.yMax - 1)
        {
            return;
        }

        targetList.Add(new PropCandidate(propType, zoneType, originCell, size, isMandatory));
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

        HashSet<Vector3Int> blockedPropCells = new(blueprint.PropBlockedCells);

        foreach (Vector3Int occupiedCell in occupiedCells)
        {
            blockedPropCells.Add(occupiedCell);
        }

        if (!CanTraverse(blueprint, blockedPropCells, true, blueprint.PlayerStartCell, blueprint.KeyCell)
            || !CanTraverse(blueprint, blockedPropCells, true, blueprint.PlayerStartCell, blueprint.BatteryCell)
            || !CanTraverse(blueprint, blockedPropCells, false, blueprint.KeyCell, blueprint.ExitCell))
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
            Vector3Int roomAnchor = FindRoomAnchorCell(blueprint, room, GetRectCenterCell(room.Bounds));
            Vector3Int[] roomDoorCells = room.DoorCells.ToArray();
            blueprint.Rooms.Add(new GeneratedRoomData(room.Id, room.RoomType, room.Bounds, roomAnchor, roomDoorCells, room.ConnectedRoomIds.ToArray()));
            blueprint.Zones.Add(new GeneratedZoneData(MapRoomTypeToZone(room.RoomType), room.Bounds, roomAnchor, roomDoorCells));
        }
    }

    private static Vector3Int FindRoomAnchorCell(GeneratedMapBlueprint blueprint, LogicalRoom room, Vector2Int preferredCell)
    {
        Vector3Int bestCell = new(preferredCell.x, preferredCell.y, 0);
        int bestScore = int.MaxValue;
        bool foundCell = false;

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

                int score = Mathf.Abs(preferredCell.x - x) + Mathf.Abs(preferredCell.y - y);

                if (!foundCell || score < bestScore)
                {
                    bestCell = candidate;
                    bestScore = score;
                    foundCell = true;
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

    private static bool ValidateBlueprint(GeneratedMapBlueprint blueprint, Dictionary<int, LogicalRoom> roomsById, List<int> mainPath)
    {
        if (roomsById.Count < 18 || roomsById.Count > 22 || blueprint.MainDoorCells.Length == 0)
        {
            return false;
        }

        if (!CanTraverse(blueprint, blueprint.PropBlockedCells, true, blueprint.PlayerStartCell, blueprint.KeyCell)
            || !CanTraverse(blueprint, blueprint.PropBlockedCells, true, blueprint.PlayerStartCell, blueprint.BatteryCell)
            || CanTraverse(blueprint, blueprint.PropBlockedCells, true, blueprint.PlayerStartCell, blueprint.ExitCell)
            || !CanTraverse(blueprint, blueprint.PropBlockedCells, false, blueprint.KeyCell, blueprint.ExitCell))
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
            if (safeRoom.Bounds.Contains(new Vector2Int(dangerCell.x, dangerCell.y)))
            {
                return false;
            }
        }

        return blueprint.CoverProps.Count > 0
            && mainPath.Count >= 7
            && HasRoomType(roomsById, GeneratedRoomType.Key)
            && HasRoomType(roomsById, GeneratedRoomType.Safe)
            && HasRoomType(roomsById, GeneratedRoomType.Utility)
            && HasRoomType(roomsById, GeneratedRoomType.PreExit)
            && HasRoomType(roomsById, GeneratedRoomType.Exit);
    }

    private static bool HasRoomType(Dictionary<int, LogicalRoom> roomsById, GeneratedRoomType roomType)
    {
        foreach (LogicalRoom room in roomsById.Values)
        {
            if (room.RoomType == roomType)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanTraverse(GeneratedMapBlueprint blueprint, HashSet<Vector3Int> blockedPropCells, bool gateClosed, Vector3Int startCell, Vector3Int goalCell)
    {
        if (!IsTraversable(blueprint, blockedPropCells, gateClosed, startCell) || !IsTraversable(blueprint, blockedPropCells, gateClosed, goalCell))
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

                if (visited.Contains(neighbor) || !IsTraversable(blueprint, blockedPropCells, gateClosed, neighbor))
                {
                    continue;
                }

                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
            }
        }

        return false;
    }

    private static bool IsTraversable(GeneratedMapBlueprint blueprint, HashSet<Vector3Int> blockedPropCells, bool gateClosed, Vector3Int cell)
    {
        if (!blueprint.GroundCells.Contains(cell)
            || blueprint.ForcedWallCells.Contains(cell)
            || blockedPropCells.Contains(cell))
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

    private static Vector2Int GetRectCenterCell(RectInt rect)
    {
        Vector2 center = rect.center;
        return new Vector2Int(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
    }

    private static void Shuffle<T>(System.Random random, List<T> list)
    {
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);

            if (swapIndex == index)
            {
                continue;
            }

            T temp = list[index];
            list[index] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }
}


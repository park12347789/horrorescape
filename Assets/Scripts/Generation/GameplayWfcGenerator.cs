/*
 * File Role:
 * Creates procedural layouts with a wave-function-collapse style module solver.
 *
 * Runtime Use:
 * Chooses compatible room modules, propagates constraints, and then converts the result into game data.
 *
 * Study Notes:
 * Best read after the graph generator so the shared metadata output format already feels familiar.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameplayWfcGenerator
{
    private static readonly Vector3Int[] CardinalOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    };

    private static readonly ModuleDefinition[] Modules =
    {
        new("Empty", false, false, false, false, 4, true),
        new("DeadN", true, false, false, false, 1, false),
        new("DeadE", false, true, false, false, 1, false),
        new("DeadS", false, false, true, false, 1, false),
        new("DeadW", false, false, false, true, 1, false),
        new("StraightNS", true, false, true, false, 3, false),
        new("StraightEW", false, true, false, true, 3, false),
        new("CornerNE", true, true, false, false, 4, false),
        new("CornerNW", true, false, false, true, 4, false),
        new("CornerSE", false, true, true, false, 4, false),
        new("CornerSW", false, false, true, true, 4, false),
        new("TNoN", false, true, true, true, 2, false),
        new("TNoE", true, false, true, true, 2, false),
        new("TNoS", true, true, false, true, 2, false),
        new("TNoW", true, true, true, false, 2, false),
        new("Cross", true, true, true, true, 1, false)
    };

    private const int GridWidth = 5;
    private const int GridHeight = 5;
    private const int TargetRoomMin = 18;
    private const int TargetRoomMax = 22;
    private const int SlotWidth = 24;
    private const int SlotHeight = 18;
    private const int RoomInsetMin = 2;
    private const int RoomMinWidth = 10;
    private const int RoomMaxWidth = 16;
    private const int RoomMinHeight = 8;
    private const int RoomMaxHeight = 12;
    private const int CorridorWidth = 2;
    private static readonly Vector2Int GridOrigin = new(-54, -40);

    private enum Direction
    {
        North,
        East,
        South,
        West
    }

    private readonly struct ModuleDefinition
    {
        public ModuleDefinition(string name, bool north, bool east, bool south, bool west, int weight, bool isEmpty)
        {
            Name = name;
            North = north;
            East = east;
            South = south;
            West = west;
            Weight = weight;
            IsEmpty = isEmpty;
        }

        public string Name { get; }
        public bool North { get; }
        public bool East { get; }
        public bool South { get; }
        public bool West { get; }
        public int Weight { get; }
        public bool IsEmpty { get; }
    }

    private sealed class LogicalRoom
    {
        public int Id;
        public Vector2Int Node;
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

        for (int attempt = 0; attempt < 64; attempt++)
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
        blueprint = new GeneratedMapBlueprint();
        System.Random random = new(seed);

        if (!TryCollapseWave(random, out int[,] collapsedModules))
        {
            return false;
        }

        BuildRoomsAndEdges(random, collapsedModules, out Dictionary<int, LogicalRoom> roomsById, out List<LogicalEdge> edges);

        if (roomsById.Count < TargetRoomMin || roomsById.Count > TargetRoomMax || !IsConnectedGraph(roomsById, edges))
        {
            return false;
        }

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

    private static bool TryCollapseWave(System.Random random, out int[,] collapsedModules)
    {
        HashSet<int>[,] possibilities = new HashSet<int>[GridWidth, GridHeight];

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                possibilities[x, y] = new HashSet<int>();

                for (int moduleIndex = 0; moduleIndex < Modules.Length; moduleIndex++)
                {
                    possibilities[x, y].Add(moduleIndex);
                }
            }
        }

        Vector2Int[] forcedOpenCells =
        {
            new(GridWidth / 2, GridHeight / 2),
            new(GridWidth / 2, GridHeight / 2 - 1),
            new(GridWidth / 2 - 1, GridHeight / 2)
        };

        foreach (Vector2Int forcedCell in forcedOpenCells)
        {
            if (IsInside(forcedCell))
            {
                possibilities[forcedCell.x, forcedCell.y].Remove(0);
            }
        }

        Queue<Vector2Int> pending = new();

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                ApplyBoundaryConstraints(possibilities[x, y], x, y);

                if (possibilities[x, y].Count == 0)
                {
                    collapsedModules = null;
                    return false;
                }

                pending.Enqueue(new Vector2Int(x, y));
            }
        }

        if (!PropagateConstraints(possibilities, pending))
        {
            collapsedModules = null;
            return false;
        }

        while (TrySelectLowestEntropyCell(random, possibilities, out Vector2Int selectedCell))
        {
            int chosenModule = ChooseWeightedModule(random, possibilities[selectedCell.x, selectedCell.y]);
            possibilities[selectedCell.x, selectedCell.y].Clear();
            possibilities[selectedCell.x, selectedCell.y].Add(chosenModule);

            pending.Clear();
            pending.Enqueue(selectedCell);

            if (!PropagateConstraints(possibilities, pending))
            {
                collapsedModules = null;
                return false;
            }
        }

        collapsedModules = new int[GridWidth, GridHeight];
        int activeRoomCount = 0;

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                int chosen = GetOnlyValue(possibilities[x, y]);
                collapsedModules[x, y] = chosen;

                if (!Modules[chosen].IsEmpty)
                {
                    activeRoomCount++;
                }
            }
        }

        return activeRoomCount >= TargetRoomMin && activeRoomCount <= TargetRoomMax;
    }

    private static void ApplyBoundaryConstraints(HashSet<int> cellOptions, int x, int y)
    {
        List<int> removals = new();

        foreach (int moduleIndex in cellOptions)
        {
            ModuleDefinition module = Modules[moduleIndex];

            if ((x == 0 && module.West)
                || (x == GridWidth - 1 && module.East)
                || (y == 0 && module.South)
                || (y == GridHeight - 1 && module.North))
            {
                removals.Add(moduleIndex);
            }
        }

        foreach (int removal in removals)
        {
            cellOptions.Remove(removal);
        }
    }

    private static bool PropagateConstraints(HashSet<int>[,] possibilities, Queue<Vector2Int> pending)
    {
        while (pending.Count > 0)
        {
            Vector2Int sourceCell = pending.Dequeue();
            HashSet<int> sourceOptions = possibilities[sourceCell.x, sourceCell.y];

            foreach (Direction direction in Enum.GetValues(typeof(Direction)))
            {
                Vector2Int neighborCell = sourceCell + ToOffset(direction);

                if (!IsInside(neighborCell))
                {
                    continue;
                }

                HashSet<int> neighborOptions = possibilities[neighborCell.x, neighborCell.y];
                List<int> removals = new();

                foreach (int neighborModuleIndex in neighborOptions)
                {
                    bool hasCompatibleSource = false;

                    foreach (int sourceModuleIndex in sourceOptions)
                    {
                        if (AreModulesCompatible(sourceModuleIndex, neighborModuleIndex, direction))
                        {
                            hasCompatibleSource = true;
                            break;
                        }
                    }

                    if (!hasCompatibleSource)
                    {
                        removals.Add(neighborModuleIndex);
                    }
                }

                if (removals.Count == 0)
                {
                    continue;
                }

                foreach (int removal in removals)
                {
                    neighborOptions.Remove(removal);
                }

                if (neighborOptions.Count == 0)
                {
                    return false;
                }

                pending.Enqueue(neighborCell);
            }
        }

        return true;
    }

    private static bool TrySelectLowestEntropyCell(System.Random random, HashSet<int>[,] possibilities, out Vector2Int selectedCell)
    {
        List<Vector2Int> candidates = new();
        int bestEntropy = int.MaxValue;

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                int count = possibilities[x, y].Count;

                if (count <= 1)
                {
                    continue;
                }

                if (count < bestEntropy)
                {
                    bestEntropy = count;
                    candidates.Clear();
                    candidates.Add(new Vector2Int(x, y));
                }
                else if (count == bestEntropy)
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        if (candidates.Count == 0)
        {
            selectedCell = default;
            return false;
        }

        selectedCell = candidates[random.Next(candidates.Count)];
        return true;
    }

    private static int ChooseWeightedModule(System.Random random, HashSet<int> options)
    {
        int totalWeight = 0;

        foreach (int option in options)
        {
            totalWeight += Modules[option].Weight;
        }

        int roll = random.Next(totalWeight);

        foreach (int option in options)
        {
            roll -= Modules[option].Weight;

            if (roll < 0)
            {
                return option;
            }
        }

        foreach (int option in options)
        {
            return option;
        }

        return 0;
    }

    private static int GetOnlyValue(HashSet<int> options)
    {
        foreach (int option in options)
        {
            return option;
        }

        return 0;
    }

    private static bool AreModulesCompatible(int sourceIndex, int neighborIndex, Direction direction)
    {
        return GetConnector(Modules[sourceIndex], direction) == GetConnector(Modules[neighborIndex], Opposite(direction));
    }

    private static bool GetConnector(ModuleDefinition module, Direction direction)
    {
        return direction switch
        {
            Direction.North => module.North,
            Direction.East => module.East,
            Direction.South => module.South,
            _ => module.West
        };
    }

    private static Direction Opposite(Direction direction)
    {
        return direction switch
        {
            Direction.North => Direction.South,
            Direction.East => Direction.West,
            Direction.South => Direction.North,
            _ => Direction.East
        };
    }

    private static Vector2Int ToOffset(Direction direction)
    {
        return direction switch
        {
            Direction.North => new Vector2Int(0, 1),
            Direction.East => new Vector2Int(1, 0),
            Direction.South => new Vector2Int(0, -1),
            _ => new Vector2Int(-1, 0)
        };
    }

    private static bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < GridWidth && cell.y >= 0 && cell.y < GridHeight;
    }

    private static void BuildRoomsAndEdges(
        System.Random random,
        int[,] collapsedModules,
        out Dictionary<int, LogicalRoom> roomsById,
        out List<LogicalEdge> edges)
    {
        roomsById = new Dictionary<int, LogicalRoom>();
        edges = new List<LogicalEdge>();
        int[,] roomIdByCell = new int[GridWidth, GridHeight];

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                roomIdByCell[x, y] = -1;

                ModuleDefinition module = Modules[collapsedModules[x, y]];

                if (module.IsEmpty)
                {
                    continue;
                }

                int roomId = roomsById.Count;
                roomIdByCell[x, y] = roomId;
                roomsById[roomId] = new LogicalRoom
                {
                    Id = roomId,
                    Node = new Vector2Int(x, y),
                    Bounds = CreateRoomBounds(random, x, y)
                };
            }
        }

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                int roomId = roomIdByCell[x, y];

                if (roomId < 0)
                {
                    continue;
                }

                ModuleDefinition module = Modules[collapsedModules[x, y]];

                if (module.East && x + 1 < GridWidth && roomIdByCell[x + 1, y] >= 0 && Modules[collapsedModules[x + 1, y]].West)
                {
                    AddEdge(edges, roomsById, roomId, roomIdByCell[x + 1, y]);
                }

                if (module.North && y + 1 < GridHeight && roomIdByCell[x, y + 1] >= 0 && Modules[collapsedModules[x, y + 1]].South)
                {
                    AddEdge(edges, roomsById, roomId, roomIdByCell[x, y + 1]);
                }
            }
        }
    }

    private static RectInt CreateRoomBounds(System.Random random, int gridX, int gridY)
    {
        int slotX = GridOrigin.x + (gridX * SlotWidth);
        int slotY = GridOrigin.y + (gridY * SlotHeight);
        int maxWidth = Mathf.Min(RoomMaxWidth, SlotWidth - (RoomInsetMin * 2) - 1);
        int maxHeight = Mathf.Min(RoomMaxHeight, SlotHeight - (RoomInsetMin * 2) - 1);
        int width = random.Next(RoomMinWidth, maxWidth + 1);
        int height = random.Next(RoomMinHeight, maxHeight + 1);
        int xMin = slotX + random.Next(RoomInsetMin, SlotWidth - width - RoomInsetMin);
        int yMin = slotY + random.Next(RoomInsetMin, SlotHeight - height - RoomInsetMin);
        return new RectInt(xMin, yMin, width, height);
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

        roomsById[roomAId].ConnectedRoomIds.Add(roomBId);
        roomsById[roomBId].ConnectedRoomIds.Add(roomAId);
    }

    private static bool IsConnectedGraph(Dictionary<int, LogicalRoom> roomsById, List<LogicalEdge> edges)
    {
        if (roomsById.Count == 0)
        {
            return false;
        }

        Dictionary<int, HashSet<int>> adjacency = BuildAdjacency(edges);
        int startRoomId = -1;

        foreach (int roomId in roomsById.Keys)
        {
            startRoomId = roomId;
            break;
        }

        Queue<int> frontier = new();
        HashSet<int> visited = new() { startRoomId };
        frontier.Enqueue(startRoomId);

        while (frontier.Count > 0)
        {
            int roomId = frontier.Dequeue();

            if (!adjacency.TryGetValue(roomId, out HashSet<int> connectedRooms))
            {
                continue;
            }

            foreach (int connectedRoomId in connectedRooms)
            {
                if (visited.Add(connectedRoomId))
                {
                    frontier.Enqueue(connectedRoomId);
                }
            }
        }

        return visited.Count == roomsById.Count;
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

        foreach (LogicalRoom room in roomsById.Values)
        {
            room.RoomType = GeneratedRoomType.Explore;
            room.IsMainRoute = false;
            room.IsPostGate = false;
        }

        for (int index = 0; index < mainPath.Count; index++)
        {
            roomsById[mainPath[index]].IsMainRoute = true;
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

        if (!TryAssignRoomType(roomsById, adjacency, mainPath, mainPathSet, 1, keyIndex, GeneratedRoomType.Safe)
            || !TryAssignRoomType(roomsById, adjacency, mainPath, mainPathSet, dangerAIndex, dangerBIndex, GeneratedRoomType.Utility))
        {
            return false;
        }

        return true;
    }

    private static void NormalizeRoleIndices(int pathLength, out int dangerAIndex, out int keyIndex, out int dangerBIndex, out int preExitIndex)
    {
        dangerAIndex = Mathf.Clamp(pathLength / 4, 1, pathLength - 5);
        keyIndex = Mathf.Clamp(pathLength / 2, dangerAIndex + 1, pathLength - 4);
        dangerBIndex = Mathf.Clamp((pathLength * 3) / 4, keyIndex + 1, pathLength - 3);
        preExitIndex = pathLength - 2;

        if (dangerBIndex >= preExitIndex)
        {
            dangerBIndex = Mathf.Max(keyIndex + 1, preExitIndex - 1);
        }
    }

    private static bool TryAssignRoomType(
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
                if (mainPathSet.Contains(connectedRoomId) || roomsById[connectedRoomId].RoomType != GeneratedRoomType.Explore)
                {
                    continue;
                }

                if ((roomType == GeneratedRoomType.Safe || roomType == GeneratedRoomType.Utility) && roomsById[connectedRoomId].IsPostGate)
                {
                    continue;
                }

                candidates.Add(connectedRoomId);
            }
        }

        if (candidates.Count == 0)
        {
            for (int index = Mathf.Max(1, minMainIndex); index <= Mathf.Min(maxMainIndex, mainPath.Count - 2); index++)
            {
                int roomId = mainPath[index];

                if (roomsById[roomId].RoomType == GeneratedRoomType.Explore)
                {
                    candidates.Add(roomId);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        int candidateId = candidates[0];
        int bestDistance = int.MaxValue;
        Vector2Int anchorCenter = GetRectCenterCell(roomsById[mainPath[Mathf.Clamp(minMainIndex, 0, mainPath.Count - 1)]].Bounds);

        foreach (int connectedRoomId in candidates)
        {
            Vector2Int candidateCenter = GetRectCenterCell(roomsById[connectedRoomId].Bounds);
            int distance = Mathf.Abs(candidateCenter.x - anchorCenter.x) + Mathf.Abs(candidateCenter.y - anchorCenter.y);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                candidateId = connectedRoomId;
            }
        }

        roomsById[candidateId].RoomType = roomType;
        return true;
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
            DoorSegment doorA = GetDoorSegment(roomA, roomB);
            DoorSegment doorB = GetDoorSegment(roomB, roomA);
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

            if (edge.IsMainGate
                || roomA.RoomType == GeneratedRoomType.DangerCorridor
                || roomB.RoomType == GeneratedRoomType.DangerCorridor
                || roomA.RoomType == GeneratedRoomType.PreExit
                || roomB.RoomType == GeneratedRoomType.PreExit)
            {
                foreach (Vector3Int routeCell in routeCells)
                {
                    blueprint.DangerCellSet.Add(routeCell);
                }
            }
        }
    }

    private static DoorSegment GetDoorSegment(LogicalRoom fromRoom, LogicalRoom toRoom)
    {
        Vector2Int delta = toRoom.Node - fromRoom.Node;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            int doorwayX = delta.x >= 0 ? fromRoom.Bounds.xMax - 1 : fromRoom.Bounds.xMin;
            int doorBottom = Mathf.Clamp(
                Mathf.RoundToInt((GetRectCenterCell(fromRoom.Bounds).y + GetRectCenterCell(toRoom.Bounds).y) * 0.5f) - 1,
                fromRoom.Bounds.yMin + 1,
                fromRoom.Bounds.yMax - 3);
            return new DoorSegment(
                new[]
                {
                    new Vector3Int(doorwayX, doorBottom, 0),
                    new Vector3Int(doorwayX, doorBottom + 1, 0)
                },
                true);
        }

        int doorwayY = delta.y >= 0 ? fromRoom.Bounds.yMax - 1 : fromRoom.Bounds.yMin;
        int doorLeft = Mathf.Clamp(
            Mathf.RoundToInt((GetRectCenterCell(fromRoom.Bounds).x + GetRectCenterCell(toRoom.Bounds).x) * 0.5f) - 1,
            fromRoom.Bounds.xMin + 1,
            fromRoom.Bounds.xMax - 3);
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

        if (startRoom == null || keyRoom == null || utilityRoom == null || safeRoom == null || exitRoom == null || patrolRoom == null)
        {
            return;
        }

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
            if (room.RoomType == GeneratedRoomType.DangerCorridor || room.Bounds.width < 10 || room.Bounds.height < 9)
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
        if (roomsById.Count < TargetRoomMin || roomsById.Count > TargetRoomMax || blueprint.MainDoorCells.Length == 0)
        {
            return false;
        }

        if (!HasRoomType(roomsById, GeneratedRoomType.Key)
            || !HasRoomType(roomsById, GeneratedRoomType.Safe)
            || !HasRoomType(roomsById, GeneratedRoomType.Utility)
            || !HasRoomType(roomsById, GeneratedRoomType.PreExit)
            || !HasRoomType(roomsById, GeneratedRoomType.Exit))
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

        return blueprint.CoverProps.Count > 0 && mainPath.Count >= 7;
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


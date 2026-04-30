#if UNITY_EDITOR
using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MainEscapeVentRouteBatchTools
{
    private static readonly string[] LiveLowerFloorScenePaths =
    {
        "Assets/Scenes/RMainScene_1F.unity",
        "Assets/Scenes/RMainScene_2F.unity",
        "Assets/Scenes/RMainScene_3F.unity",
        "Assets/Scenes/RMainScene_4F.unity"
    };

    private readonly struct PlannedVentNode
    {
        public PlannedVentNode(string name, MainEscapeVentNodeType nodeType, Vector3Int cell)
        {
            Name = name;
            NodeType = nodeType;
            Cell = cell;
        }

        public string Name { get; }
        public MainEscapeVentNodeType NodeType { get; }
        public Vector3Int Cell { get; }
    }

    [MenuItem("Tools/Main Escape Rebuild/Rebuild Live Floor Vent Routes (1F-4F)")]
    private static void RebuildLiveLowerFloorVentRoutesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        RebuildLiveLowerFloorVentRoutes();
    }

    public static void RebuildLiveLowerFloorVentRoutes()
    {
        string originalScenePath = SceneManager.GetActiveScene().path;

        try
        {
            for (int index = 0; index < LiveLowerFloorScenePaths.Length; index++)
            {
                string scenePath = LiveLowerFloorScenePaths[index];
                RebuildSceneVentRoute(scenePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MainEscapeVentRouteBatchTools] Rebuilt live lower-floor vent routes for 1F-4F.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }
        }
    }

    private static void RebuildSceneVentRoute(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        MainEscapeFloorAuthoring floorAuthoring = FindFloorAuthoring(scene);

        if (floorAuthoring == null)
        {
            throw new InvalidOperationException($"Could not find {nameof(MainEscapeFloorAuthoring)} in '{scenePath}'.");
        }

        GeneratedFloorLayout layout = floorAuthoring.GetComponent<GeneratedFloorLayout>();
        Tilemap groundTilemap = floorAuthoring.GroundTilemap;
        MainEscapeVentRouteAuthoring ventRoute = ResolveVentRouteAuthoring(floorAuthoring);

        if (layout == null)
        {
            throw new InvalidOperationException($"Could not find {nameof(GeneratedFloorLayout)} for '{scenePath}'.");
        }

        if (groundTilemap == null)
        {
            throw new InvalidOperationException($"Could not resolve ground tilemap for '{scenePath}'.");
        }

        if (ventRoute == null)
        {
            throw new InvalidOperationException($"Could not resolve VentRoute authoring root for '{scenePath}'.");
        }

        List<PlannedVentNode> plannedNodes = BuildPlannedNodes(layout, groundTilemap);

        if (plannedNodes.Count < 3)
        {
            throw new InvalidOperationException($"Vent route rebuild produced too few nodes for '{scenePath}'.");
        }

        ApplyNodes(ventRoute, groundTilemap, plannedNodes);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[MainEscapeVentRouteBatchTools] Rebuilt {plannedNodes.Count} vent nodes in '{scenePath}'.", ventRoute);
    }

    private static MainEscapeFloorAuthoring FindFloorAuthoring(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            MainEscapeFloorAuthoring authoring = roots[rootIndex].GetComponentInChildren<MainEscapeFloorAuthoring>(true);

            if (authoring != null)
            {
                return authoring;
            }
        }

        return null;
    }

    private static MainEscapeVentRouteAuthoring ResolveVentRouteAuthoring(MainEscapeFloorAuthoring floorAuthoring)
    {
        Transform ventRouteRoot = floorAuthoring.VentRouteRoot;
        return ventRouteRoot != null ? ventRouteRoot.GetComponent<MainEscapeVentRouteAuthoring>() : null;
    }

    private static List<PlannedVentNode> BuildPlannedNodes(GeneratedFloorLayout layout, Tilemap groundTilemap)
    {
        List<GeneratedRoomData> upperRooms = new();
        List<GeneratedRoomData> lowerRooms = new();
        List<Vector3Int> corridorCells = CollectCorridorCells(layout);
        int corridorBandY = ResolveCorridorBandY(layout, corridorCells);

        for (int index = 0; index < layout.Rooms.Length; index++)
        {
            GeneratedRoomData room = layout.Rooms[index];

            if (room.CenterCell.y >= corridorBandY)
            {
                upperRooms.Add(room);
            }
            else
            {
                lowerRooms.Add(room);
            }
        }

        upperRooms.Sort(CompareRoomsByXThenY);
        lowerRooms.Sort(CompareRoomsByXThenY);

        int upperBandY = ResolveRoomBandY(upperRooms, corridorBandY, useUpperBand: true);
        int lowerBandY = ResolveRoomBandY(lowerRooms, corridorBandY, useUpperBand: false);
        HashSet<Vector3Int> usedCells = new();
        SortedSet<int> corridorXs = new();
        List<PlannedVentNode> plannedNodes = new();

        BuildRoomNodes(
            upperRooms,
            "Upper",
            upperBandY,
            corridorBandY,
            groundTilemap,
            usedCells,
            corridorXs,
            plannedNodes);

        BuildRoomNodes(
            lowerRooms,
            "Lower",
            lowerBandY,
            corridorBandY,
            groundTilemap,
            usedCells,
            corridorXs,
            plannedNodes);

        corridorXs.Add(ResolveCorridorEndpointX(layout, corridorCells, useMinimumX: true));
        corridorXs.Add(ResolveCorridorEndpointX(layout, corridorCells, useMinimumX: false));

        int corridorIndex = 0;

        foreach (int corridorX in corridorXs)
        {
            Vector3Int corridorCell = ResolveCorridorCell(corridorCells, groundTilemap, usedCells, corridorX, corridorBandY);
            plannedNodes.Add(new PlannedVentNode($"Corridor_{corridorIndex:00}", MainEscapeVentNodeType.Corridor, corridorCell));
            corridorIndex++;
        }

        plannedNodes.Sort(CompareNodesForAuthoringOrder);
        return plannedNodes;
    }

    private static void BuildRoomNodes(
        List<GeneratedRoomData> rooms,
        string prefix,
        int roomBandY,
        int corridorBandY,
        Tilemap groundTilemap,
        ISet<Vector3Int> usedCells,
        ISet<int> corridorXs,
        ICollection<PlannedVentNode> plannedNodes)
    {
        for (int index = 0; index < rooms.Count; index++)
        {
            GeneratedRoomData room = rooms[index];
            int connectorX = ResolveRoomConnectorX(room, corridorBandY);
            Vector3Int roomCell = ResolveRoomCell(room, groundTilemap, usedCells, connectorX, roomBandY);
            corridorXs.Add(roomCell.x);
            plannedNodes.Add(new PlannedVentNode($"{prefix}_{index:00}", MainEscapeVentNodeType.Room, roomCell));
        }
    }

    private static int CompareRoomsByXThenY(GeneratedRoomData left, GeneratedRoomData right)
    {
        int xCompare = left.CenterCell.x.CompareTo(right.CenterCell.x);
        return xCompare != 0 ? xCompare : left.CenterCell.y.CompareTo(right.CenterCell.y);
    }

    private static int CompareNodesForAuthoringOrder(PlannedVentNode left, PlannedVentNode right)
    {
        int leftRank = ResolveNodeRank(left.Name);
        int rightRank = ResolveNodeRank(right.Name);
        int rankCompare = leftRank.CompareTo(rightRank);

        if (rankCompare != 0)
        {
            return rankCompare;
        }

        return string.CompareOrdinal(left.Name, right.Name);
    }

    private static int ResolveNodeRank(string nodeName)
    {
        if (nodeName.StartsWith("Upper_", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (nodeName.StartsWith("Corridor_", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static List<Vector3Int> CollectCorridorCells(GeneratedFloorLayout layout)
    {
        List<Vector3Int> corridorCells = new();

        if (layout == null || layout.Routes == null)
        {
            return corridorCells;
        }

        for (int routeIndex = 0; routeIndex < layout.Routes.Length; routeIndex++)
        {
            GeneratedRouteData route = layout.Routes[routeIndex];
            Vector3Int[] routeCells = route.Cells;

            if (routeCells == null || routeCells.Length == 0)
            {
                continue;
            }

            for (int cellIndex = 0; cellIndex < routeCells.Length; cellIndex++)
            {
                Vector3Int cell = routeCells[cellIndex];

                if (!IsInsideRoom(layout.Rooms, cell))
                {
                    corridorCells.Add(cell);
                }
            }
        }

        return corridorCells;
    }

    private static bool IsInsideRoom(GeneratedRoomData[] rooms, Vector3Int cell)
    {
        if (rooms == null)
        {
            return false;
        }

        Vector2Int point = new(cell.x, cell.y);

        for (int index = 0; index < rooms.Length; index++)
        {
            if (rooms[index].Bounds.Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveCorridorBandY(GeneratedFloorLayout layout, List<Vector3Int> corridorCells)
    {
        if (corridorCells != null && corridorCells.Count > 0)
        {
            List<int> sortedYs = new(corridorCells.Count);

            for (int index = 0; index < corridorCells.Count; index++)
            {
                sortedYs.Add(corridorCells[index].y);
            }

            sortedYs.Sort();
            return sortedYs[sortedYs.Count / 2];
        }

        List<int> doorYs = new();

        if (layout != null && layout.Rooms != null)
        {
            for (int roomIndex = 0; roomIndex < layout.Rooms.Length; roomIndex++)
            {
                Vector3Int[] doorCells = layout.Rooms[roomIndex].DoorCells;

                for (int doorIndex = 0; doorIndex < doorCells.Length; doorIndex++)
                {
                    doorYs.Add(doorCells[doorIndex].y);
                }
            }
        }

        if (doorYs.Count > 0)
        {
            doorYs.Sort();
            return doorYs[doorYs.Count / 2];
        }

        return layout != null ? layout.PlayerStartCell.y : 0;
    }

    private static int ResolveRoomBandY(
        IReadOnlyList<GeneratedRoomData> rooms,
        int corridorBandY,
        bool useUpperBand)
    {
        if (rooms == null || rooms.Count == 0)
        {
            return corridorBandY + (useUpperBand ? 3 : -3);
        }

        int intersectionMin = int.MinValue;
        int intersectionMax = int.MaxValue;

        for (int index = 0; index < rooms.Count; index++)
        {
            RectInt bounds = rooms[index].Bounds;
            int roomMinY = bounds.yMin + 1;
            int roomMaxY = Mathf.Max(roomMinY, bounds.yMax - 2);
            intersectionMin = Mathf.Max(intersectionMin, roomMinY);
            intersectionMax = Mathf.Min(intersectionMax, roomMaxY);
        }

        if (intersectionMin <= intersectionMax)
        {
            return useUpperBand ? intersectionMax : intersectionMin;
        }

        int fallbackSum = 0;

        for (int index = 0; index < rooms.Count; index++)
        {
            RectInt bounds = rooms[index].Bounds;
            int roomMinY = bounds.yMin + 1;
            int roomMaxY = Mathf.Max(roomMinY, bounds.yMax - 2);
            fallbackSum += useUpperBand ? roomMaxY : roomMinY;
        }

        return Mathf.RoundToInt(fallbackSum / (float)rooms.Count);
    }

    private static int ResolveRoomConnectorX(GeneratedRoomData room, int corridorBandY)
    {
        int interiorMinX = room.Bounds.xMin + 1;
        int interiorMaxX = Mathf.Max(interiorMinX, room.Bounds.xMax - 2);
        int preferredX = room.CenterCell.x;
        Vector3Int[] doorCells = room.DoorCells;

        if (doorCells != null && doorCells.Length > 0)
        {
            Vector3Int bestDoor = doorCells[0];
            int bestScore = int.MaxValue;

            for (int index = 0; index < doorCells.Length; index++)
            {
                Vector3Int doorCell = doorCells[index];
                int score = (Mathf.Abs(doorCell.y - corridorBandY) * 10) + Mathf.Abs(doorCell.x - room.CenterCell.x);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestDoor = doorCell;
                }
            }

            preferredX = bestDoor.x;
        }

        return Mathf.Clamp(preferredX, interiorMinX, interiorMaxX);
    }

    private static Vector3Int ResolveRoomCell(
        GeneratedRoomData room,
        Tilemap groundTilemap,
        ISet<Vector3Int> usedCells,
        int targetX,
        int targetY)
    {
        int minX = room.Bounds.xMin + 1;
        int maxX = Mathf.Max(minX, room.Bounds.xMax - 2);
        int minY = room.Bounds.yMin + 1;
        int maxY = Mathf.Max(minY, room.Bounds.yMax - 2);
        int clampedTargetX = Mathf.Clamp(targetX, minX, maxX);
        int clampedTargetY = Mathf.Clamp(targetY, minY, maxY);
        Vector3Int bestCell = new(clampedTargetX, clampedTargetY, 0);
        int bestScore = int.MaxValue;
        bool found = false;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3Int candidate = new(x, y, 0);

                if ((usedCells != null && usedCells.Contains(candidate)) || !IsUsableFloorCell(groundTilemap, candidate))
                {
                    continue;
                }

                int score = (Mathf.Abs(x - clampedTargetX) * 3) + (Mathf.Abs(y - clampedTargetY) * 8);

                if (!found || score < bestScore)
                {
                    bestCell = candidate;
                    bestScore = score;
                    found = true;
                }
            }
        }

        if (!found)
        {
            bestCell = new(clampedTargetX, clampedTargetY, 0);
        }

        usedCells?.Add(bestCell);
        return bestCell;
    }

    private static int ResolveCorridorEndpointX(
        GeneratedFloorLayout layout,
        IReadOnlyList<Vector3Int> corridorCells,
        bool useMinimumX)
    {
        if (corridorCells != null && corridorCells.Count > 0)
        {
            int selectedX = corridorCells[0].x;

            for (int index = 1; index < corridorCells.Count; index++)
            {
                int candidateX = corridorCells[index].x;

                if (useMinimumX ? candidateX < selectedX : candidateX > selectedX)
                {
                    selectedX = candidateX;
                }
            }

            return selectedX;
        }

        if (layout != null && layout.Rooms != null && layout.Rooms.Length > 0)
        {
            int selectedX = useMinimumX ? int.MaxValue : int.MinValue;

            for (int index = 0; index < layout.Rooms.Length; index++)
            {
                RectInt bounds = layout.Rooms[index].Bounds;
                int candidateX = useMinimumX ? bounds.xMin + 1 : bounds.xMax - 2;

                if (useMinimumX)
                {
                    selectedX = Mathf.Min(selectedX, candidateX);
                }
                else
                {
                    selectedX = Mathf.Max(selectedX, candidateX);
                }
            }

            if (selectedX != int.MaxValue && selectedX != int.MinValue)
            {
                return selectedX;
            }
        }

        return layout != null ? layout.PlayerStartCell.x : 0;
    }

    private static Vector3Int ResolveCorridorCell(
        IReadOnlyList<Vector3Int> corridorCells,
        Tilemap groundTilemap,
        ISet<Vector3Int> usedCells,
        int targetX,
        int targetY)
    {
        Vector3Int bestCell = new(targetX, targetY, 0);
        int bestScore = int.MaxValue;
        bool found = false;

        if (corridorCells != null)
        {
            for (int index = 0; index < corridorCells.Count; index++)
            {
                Vector3Int candidate = corridorCells[index];

                if ((usedCells != null && usedCells.Contains(candidate)) || !IsUsableFloorCell(groundTilemap, candidate))
                {
                    continue;
                }

                int score = (Mathf.Abs(candidate.x - targetX) * 3) + (Mathf.Abs(candidate.y - targetY) * 8);

                if (!found || score < bestScore)
                {
                    bestCell = candidate;
                    bestScore = score;
                    found = true;
                }
            }
        }

        if (!found)
        {
            for (int radius = 0; radius <= 6; radius++)
            {
                for (int y = targetY - radius; y <= targetY + radius; y++)
                {
                    for (int x = targetX - radius; x <= targetX + radius; x++)
                    {
                        Vector3Int candidate = new(x, y, 0);

                        if ((usedCells != null && usedCells.Contains(candidate)) || !IsUsableFloorCell(groundTilemap, candidate))
                        {
                            continue;
                        }

                        int score = (Mathf.Abs(x - targetX) * 3) + (Mathf.Abs(y - targetY) * 8);

                        if (!found || score < bestScore)
                        {
                            bestCell = candidate;
                            bestScore = score;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    break;
                }
            }
        }

        usedCells?.Add(bestCell);
        return bestCell;
    }

    private static bool IsUsableFloorCell(Tilemap groundTilemap, Vector3Int cell)
    {
        return groundTilemap != null && groundTilemap.HasTile(cell);
    }

    private static void ApplyNodes(
        MainEscapeVentRouteAuthoring ventRoute,
        Tilemap groundTilemap,
        IReadOnlyList<PlannedVentNode> plannedNodes)
    {
        Transform routeRoot = ventRoute.transform;

        for (int index = routeRoot.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(routeRoot.GetChild(index).gameObject);
        }

        ventRoute.Configure(configuredLoopPath: false);
        EditorUtility.SetDirty(ventRoute);

        for (int index = 0; index < plannedNodes.Count; index++)
        {
            PlannedVentNode plannedNode = plannedNodes[index];
            GameObject nodeObject = new(plannedNode.Name);
            nodeObject.transform.SetParent(routeRoot, false);
            nodeObject.transform.position = groundTilemap.GetCellCenterWorld(plannedNode.Cell);

            MainEscapeVentNodeAuthoring nodeAuthoring = nodeObject.AddComponent<MainEscapeVentNodeAuthoring>();
            nodeAuthoring.Configure(plannedNode.NodeType);
            nodeAuthoring.ClearConnections();
            EditorUtility.SetDirty(nodeAuthoring);
        }
    }
}
#endif

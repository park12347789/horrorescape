using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MainEscapeVentNodeType
{
    Auto,
    Corridor,
    Room
}

[Serializable]
public readonly struct MainEscapeVentNodeDefinition
{
    public MainEscapeVentNodeDefinition(Vector3Int cell, bool isCorridor, int roomId, bool allowsSurfaceAccess = true)
    {
        Cell = cell;
        IsCorridor = isCorridor;
        RoomId = roomId;
        AllowsSurfaceAccess = allowsSurfaceAccess;
    }

    public Vector3Int Cell { get; }
    public bool IsCorridor { get; }
    public int RoomId { get; }
    public bool AllowsSurfaceAccess { get; }
}

[Serializable]
public readonly struct MainEscapeVentConnectionDefinition
{
    public MainEscapeVentConnectionDefinition(int fromIndex, int toIndex)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }

    public int FromIndex { get; }
    public int ToIndex { get; }
}

[Serializable]
public readonly struct MainEscapeVentRouteDefinition
{
    public MainEscapeVentRouteDefinition(MainEscapeVentNodeDefinition[] nodes, bool loopPath)
        : this(nodes, Array.Empty<MainEscapeVentConnectionDefinition>(), false, loopPath)
    {
    }

    public MainEscapeVentRouteDefinition(
        MainEscapeVentNodeDefinition[] nodes,
        MainEscapeVentConnectionDefinition[] connections,
        bool usesExplicitConnections,
        bool loopPath)
    {
        Nodes = nodes ?? Array.Empty<MainEscapeVentNodeDefinition>();
        Connections = connections ?? Array.Empty<MainEscapeVentConnectionDefinition>();
        UsesExplicitConnections = usesExplicitConnections;
        LoopPath = loopPath;
    }

    public MainEscapeVentNodeDefinition[] Nodes { get; }
    public MainEscapeVentConnectionDefinition[] Connections { get; }
    public bool UsesExplicitConnections { get; }
    public bool LoopPath { get; }
    public bool IsValid => Nodes != null && Nodes.Length > 0;

    public static MainEscapeVentRouteDefinition Empty => new(
        Array.Empty<MainEscapeVentNodeDefinition>(),
        Array.Empty<MainEscapeVentConnectionDefinition>(),
        false,
        false);
}

[DisallowMultipleComponent]
public sealed class MainEscapeVentRouteAuthoring : MonoBehaviour
{
    [SerializeField] private bool loopPath;

    public bool LoopPath => loopPath;

    public void Configure(bool configuredLoopPath)
    {
        loopPath = configuredLoopPath;
    }

    public MainEscapeVentRouteDefinition BuildRouteDefinition(Tilemap tilemap, GeneratedFloorLayout layout)
    {
        if (tilemap == null || transform.childCount == 0)
        {
            return MainEscapeVentRouteDefinition.Empty;
        }

        List<MainEscapeVentNodeDefinition> nodes = new(transform.childCount);
        Dictionary<Transform, int> nodeIndexByTransform = new(transform.childCount);
        HashSet<Vector3Int> seen = new();
        bool usesExplicitConnections = false;

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform nodeTransform = transform.GetChild(index);
            Vector3Int cell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, nodeTransform.position);

            if (!seen.Add(cell))
            {
                continue;
            }

            MainEscapeVentNodeAuthoring nodeAuthoring = nodeTransform.GetComponent<MainEscapeVentNodeAuthoring>();
            MainEscapeVentNodeType nodeType = ResolveNodeType(nodeTransform, nodeAuthoring);

            if (nodeAuthoring != null)
            {
                usesExplicitConnections |= nodeAuthoring.HasExplicitConnections;
            }

            int roomId = ResolveRoomId(cell, layout);
            bool isCorridor = ResolveIsCorridor(nodeType, roomId);
            bool allowsSurfaceAccess = ResolveAllowsSurfaceAccess(nodeTransform);
            nodes.Add(new MainEscapeVentNodeDefinition(cell, isCorridor, isCorridor ? -1 : roomId, allowsSurfaceAccess));
            nodeIndexByTransform[nodeTransform] = nodes.Count - 1;
        }

        List<MainEscapeVentConnectionDefinition> connections = new();
        HashSet<long> seenConnections = new();

        if (usesExplicitConnections)
        {
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform nodeTransform = transform.GetChild(index);

                if (!nodeIndexByTransform.TryGetValue(nodeTransform, out int fromIndex))
                {
                    continue;
                }

                MainEscapeVentNodeAuthoring nodeAuthoring = nodeTransform.GetComponent<MainEscapeVentNodeAuthoring>();

                if (nodeAuthoring == null || !nodeAuthoring.HasExplicitConnections)
                {
                    continue;
                }

                IReadOnlyList<MainEscapeVentNodeAuthoring> connectedNodes = nodeAuthoring.ConnectedNodes;

                for (int connectionIndex = 0; connectionIndex < connectedNodes.Count; connectionIndex++)
                {
                    MainEscapeVentNodeAuthoring connectedNode = connectedNodes[connectionIndex];

                    if (connectedNode == null || connectedNode.transform.parent != transform)
                    {
                        continue;
                    }

                    if (!nodeIndexByTransform.TryGetValue(connectedNode.transform, out int toIndex) || toIndex == fromIndex)
                    {
                        continue;
                    }

                    int minIndex = Math.Min(fromIndex, toIndex);
                    int maxIndex = Math.Max(fromIndex, toIndex);
                    long connectionKey = ((long)minIndex << 32) | (uint)maxIndex;

                    if (seenConnections.Add(connectionKey))
                    {
                        connections.Add(new MainEscapeVentConnectionDefinition(minIndex, maxIndex));
                    }
                }
            }
        }

        if (AllowImplicitNamedConnections()
            && TryBuildNamedColumnConnections(nodeIndexByTransform, out MainEscapeVentConnectionDefinition[] inferredConnections))
        {
            for (int index = 0; index < inferredConnections.Length; index++)
            {
                MainEscapeVentConnectionDefinition inferredConnection = inferredConnections[index];
                int minIndex = Math.Min(inferredConnection.FromIndex, inferredConnection.ToIndex);
                int maxIndex = Math.Max(inferredConnection.FromIndex, inferredConnection.ToIndex);
                long connectionKey = ((long)minIndex << 32) | (uint)maxIndex;

                if (seenConnections.Add(connectionKey))
                {
                    connections.Add(inferredConnection);
                }
            }

            usesExplicitConnections = connections.Count > 0;
        }

        return nodes.Count > 0
            ? new MainEscapeVentRouteDefinition(nodes.ToArray(), connections.ToArray(), usesExplicitConnections, loopPath)
            : MainEscapeVentRouteDefinition.Empty;
    }

    private bool AllowImplicitNamedConnections()
    {
        return true;
    }

    private static bool ResolveIsCorridor(MainEscapeVentNodeType nodeType, int roomId)
    {
        return nodeType switch
        {
            MainEscapeVentNodeType.Corridor => true,
            MainEscapeVentNodeType.Room => false,
            _ => roomId < 0
        };
    }

    private static int ResolveRoomId(Vector3Int cell, GeneratedFloorLayout layout)
    {
        if (layout == null || layout.Rooms == null)
        {
            return -1;
        }

        Vector2Int point = new(cell.x, cell.y);

        for (int index = 0; index < layout.Rooms.Length; index++)
        {
            GeneratedRoomData room = layout.Rooms[index];

            if (room.Bounds.Contains(point))
            {
                return room.RoomId;
            }
        }

        return -1;
    }

    private static MainEscapeVentNodeType ResolveNodeType(Transform nodeTransform, MainEscapeVentNodeAuthoring nodeAuthoring)
    {
        if (nodeAuthoring != null && nodeAuthoring.NodeType != MainEscapeVentNodeType.Auto)
        {
            return nodeAuthoring.NodeType;
        }

        if (nodeTransform == null)
        {
            return MainEscapeVentNodeType.Auto;
        }

        string nodeName = nodeTransform.name ?? string.Empty;

        if (nodeName.StartsWith("Corridor_", StringComparison.OrdinalIgnoreCase)
            || nodeName.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase))
        {
            return MainEscapeVentNodeType.Corridor;
        }

        if (nodeName.StartsWith("Upper_", StringComparison.OrdinalIgnoreCase)
            || nodeName.StartsWith("Lower_", StringComparison.OrdinalIgnoreCase))
        {
            return MainEscapeVentNodeType.Room;
        }

        return MainEscapeVentNodeType.Auto;
    }

    private static bool ResolveAllowsSurfaceAccess(Transform nodeTransform)
    {
        if (nodeTransform == null)
        {
            return false;
        }

        string nodeName = nodeTransform.name ?? string.Empty;
        return nodeName.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryBuildNamedColumnConnections(
        Dictionary<Transform, int> nodeIndexByTransform,
        out MainEscapeVentConnectionDefinition[] connections)
    {
        connections = Array.Empty<MainEscapeVentConnectionDefinition>();

        if (nodeIndexByTransform == null || nodeIndexByTransform.Count == 0)
        {
            return false;
        }

        if (TryBuildNamedGridConnections(nodeIndexByTransform, out connections))
        {
            return true;
        }

        SortedDictionary<float, int> corridorNodes = new();
        Dictionary<float, int> upperNodes = new();
        Dictionary<float, int> lowerNodes = new();

        foreach ((Transform nodeTransform, int nodeIndex) in nodeIndexByTransform)
        {
            if (nodeTransform == null)
            {
                continue;
            }

            float x = RoundToHalf(nodeTransform.position.x);
            string nodeName = nodeTransform.name ?? string.Empty;

            if (nodeName.StartsWith("Corridor_", StringComparison.OrdinalIgnoreCase))
            {
                corridorNodes[x] = nodeIndex;
            }
            else if (nodeName.StartsWith("Upper_", StringComparison.OrdinalIgnoreCase))
            {
                upperNodes[x] = nodeIndex;
            }
            else if (nodeName.StartsWith("Lower_", StringComparison.OrdinalIgnoreCase))
            {
                lowerNodes[x] = nodeIndex;
            }
        }

        if (corridorNodes.Count == 0 || (upperNodes.Count == 0 && lowerNodes.Count == 0))
        {
            return false;
        }

        List<MainEscapeVentConnectionDefinition> resolvedConnections = new();
        HashSet<long> seenConnections = new();
        int previousCorridorIndex = -1;

        foreach ((float _, int corridorIndex) in corridorNodes)
        {
            if (previousCorridorIndex >= 0)
            {
                AddConnection(previousCorridorIndex, corridorIndex, resolvedConnections, seenConnections);
            }

            previousCorridorIndex = corridorIndex;
        }

        foreach ((float x, int upperIndex) in upperNodes)
        {
            if (corridorNodes.TryGetValue(x, out int corridorIndex))
            {
                AddConnection(upperIndex, corridorIndex, resolvedConnections, seenConnections);
            }
        }

        foreach ((float x, int lowerIndex) in lowerNodes)
        {
            if (corridorNodes.TryGetValue(x, out int corridorIndex))
            {
                AddConnection(lowerIndex, corridorIndex, resolvedConnections, seenConnections);
            }
        }

        AddAdjacentLaneConnections(upperNodes, resolvedConnections, seenConnections);
        AddAdjacentLaneConnections(lowerNodes, resolvedConnections, seenConnections);
        AddCrossLaneConnections(upperNodes, lowerNodes, corridorNodes, resolvedConnections, seenConnections);
        AddSecondaryCorridorConnections(upperNodes, corridorNodes, resolvedConnections, seenConnections);
        AddSecondaryCorridorConnections(lowerNodes, corridorNodes, resolvedConnections, seenConnections);
        AddExpressCorridorConnections(corridorNodes, resolvedConnections, seenConnections);

        connections = resolvedConnections.ToArray();
        return connections.Length > 0;
    }

    private static bool TryBuildNamedGridConnections(
        Dictionary<Transform, int> nodeIndexByTransform,
        out MainEscapeVentConnectionDefinition[] connections)
    {
        connections = Array.Empty<MainEscapeVentConnectionDefinition>();
        SortedDictionary<int, SortedDictionary<int, int>> rowNodes = new();
        SortedDictionary<int, SortedDictionary<int, int>> columnNodes = new();

        foreach ((Transform nodeTransform, int nodeIndex) in nodeIndexByTransform)
        {
            if (nodeTransform == null
                || !TryParseGridNodeName(nodeTransform.name, out int row, out int column))
            {
                continue;
            }

            if (!rowNodes.TryGetValue(row, out SortedDictionary<int, int> rowEntries))
            {
                rowEntries = new SortedDictionary<int, int>();
                rowNodes[row] = rowEntries;
            }

            if (!columnNodes.TryGetValue(column, out SortedDictionary<int, int> columnEntries))
            {
                columnEntries = new SortedDictionary<int, int>();
                columnNodes[column] = columnEntries;
            }

            rowEntries[column] = nodeIndex;
            columnEntries[row] = nodeIndex;
        }

        if (rowNodes.Count == 0 || columnNodes.Count == 0)
        {
            return false;
        }

        List<MainEscapeVentConnectionDefinition> resolvedConnections = new();
        HashSet<long> seenConnections = new();

        foreach (SortedDictionary<int, int> rowEntries in rowNodes.Values)
        {
            AddOrderedGridConnections(rowEntries, resolvedConnections, seenConnections);
        }

        foreach (SortedDictionary<int, int> columnEntries in columnNodes.Values)
        {
            AddOrderedGridConnections(columnEntries, resolvedConnections, seenConnections);
        }

        connections = resolvedConnections.ToArray();
        return connections.Length > 0;
    }

    private static bool TryParseGridNodeName(string nodeName, out int row, out int column)
    {
        row = -1;
        column = -1;

        if (string.IsNullOrWhiteSpace(nodeName)
            || !nodeName.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] parts = nodeName.Split('_');

        for (int index = 0; index < parts.Length; index++)
        {
            string part = parts[index];

            if (part.Length < 2)
            {
                continue;
            }

            if ((part[0] == 'R' || part[0] == 'r')
                && int.TryParse(part.Substring(1), out int parsedRow))
            {
                row = parsedRow;
            }
            else if ((part[0] == 'C' || part[0] == 'c')
                && int.TryParse(part.Substring(1), out int parsedColumn))
            {
                column = parsedColumn;
            }
        }

        return row >= 0 && column >= 0;
    }

    private static void AddOrderedGridConnections(
        SortedDictionary<int, int> orderedNodes,
        List<MainEscapeVentConnectionDefinition> connections,
        HashSet<long> seenConnections)
    {
        int previousIndex = -1;

        foreach (int nodeIndex in orderedNodes.Values)
        {
            if (previousIndex >= 0)
            {
                AddConnection(previousIndex, nodeIndex, connections, seenConnections);
            }

            previousIndex = nodeIndex;
        }
    }

    private static void AddAdjacentLaneConnections(
        Dictionary<float, int> laneNodes,
        List<MainEscapeVentConnectionDefinition> connections,
        HashSet<long> seenConnections)
    {
        if (laneNodes.Count < 2)
        {
            return;
        }

        List<KeyValuePair<float, int>> orderedNodes = new(laneNodes);
        orderedNodes.Sort((left, right) => left.Key.CompareTo(right.Key));
        const float maxAdjacentGap = 12.5f;

        for (int index = 1; index < orderedNodes.Count; index++)
        {
            KeyValuePair<float, int> previous = orderedNodes[index - 1];
            KeyValuePair<float, int> current = orderedNodes[index];

            if (Mathf.Abs(current.Key - previous.Key) > maxAdjacentGap)
            {
                continue;
            }

            AddConnection(previous.Value, current.Value, connections, seenConnections);
        }
    }

    private static void AddSecondaryCorridorConnections(
        Dictionary<float, int> laneNodes,
        SortedDictionary<float, int> corridorNodes,
        List<MainEscapeVentConnectionDefinition> connections,
        HashSet<long> seenConnections)
    {
        if (laneNodes.Count == 0 || corridorNodes.Count < 2)
        {
            return;
        }

        const float secondaryCorridorTolerance = 1.1f;

        foreach ((float laneX, int laneIndex) in laneNodes)
        {
            float bestDistance = float.MaxValue;
            int bestCorridorIndex = -1;

            foreach ((float corridorX, int corridorIndex) in corridorNodes)
            {
                float distance = Mathf.Abs(corridorX - laneX);

                if (distance < 0.001f || distance > secondaryCorridorTolerance || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestCorridorIndex = corridorIndex;
            }

            if (bestCorridorIndex >= 0)
            {
                AddConnection(laneIndex, bestCorridorIndex, connections, seenConnections);
            }
        }
    }

    private static void AddCrossLaneConnections(
        Dictionary<float, int> upperNodes,
        Dictionary<float, int> lowerNodes,
        SortedDictionary<float, int> corridorNodes,
        List<MainEscapeVentConnectionDefinition> connections,
        HashSet<long> seenConnections)
    {
        if (upperNodes.Count == 0 || lowerNodes.Count == 0)
        {
            return;
        }

        const float directCrossLaneTolerance = 1.1f;
        const float sharedCorridorTolerance = 4.5f;

        foreach ((float upperX, int upperIndex) in upperNodes)
        {
            float bestDistance = float.MaxValue;
            int bestLowerIndex = -1;

            foreach ((float lowerX, int lowerIndex) in lowerNodes)
            {
                float distance = Mathf.Abs(upperX - lowerX);

                if (distance > directCrossLaneTolerance || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestLowerIndex = lowerIndex;
            }

            if (bestLowerIndex >= 0)
            {
                AddConnection(upperIndex, bestLowerIndex, connections, seenConnections);
            }
        }

        if (corridorNodes.Count == 0)
        {
            return;
        }

        foreach ((float upperX, int upperIndex) in upperNodes)
        {
            int nearestCorridorIndex = FindNearestNodeIndexByX(corridorNodes, upperX, sharedCorridorTolerance);

            if (nearestCorridorIndex < 0)
            {
                continue;
            }

            foreach ((float lowerX, int lowerIndex) in lowerNodes)
            {
                int lowerCorridorIndex = FindNearestNodeIndexByX(corridorNodes, lowerX, sharedCorridorTolerance);

                if (lowerCorridorIndex >= 0 && lowerCorridorIndex == nearestCorridorIndex)
                {
                    AddConnection(upperIndex, lowerIndex, connections, seenConnections);
                }
            }
        }
    }

    private static void AddExpressCorridorConnections(
        SortedDictionary<float, int> corridorNodes,
        List<MainEscapeVentConnectionDefinition> connections,
        HashSet<long> seenConnections)
    {
        if (corridorNodes.Count < 4)
        {
            return;
        }

        List<KeyValuePair<float, int>> orderedCorridors = new(corridorNodes);
        orderedCorridors.Sort((left, right) => left.Key.CompareTo(right.Key));
        const float minExpressSpan = 8f;
        const float maxExpressSpan = 18.5f;

        for (int index = 0; index < orderedCorridors.Count; index++)
        {
            KeyValuePair<float, int> origin = orderedCorridors[index];

            for (int candidateIndex = index + 2; candidateIndex < orderedCorridors.Count; candidateIndex++)
            {
                KeyValuePair<float, int> destination = orderedCorridors[candidateIndex];
                float span = Mathf.Abs(destination.Key - origin.Key);

                if (span < minExpressSpan)
                {
                    continue;
                }

                if (span > maxExpressSpan)
                {
                    break;
                }

                AddConnection(origin.Value, destination.Value, connections, seenConnections);
                break;
            }
        }
    }

    private static int FindNearestNodeIndexByX(
        SortedDictionary<float, int> nodesByX,
        float targetX,
        float maxDistance)
    {
        float bestDistance = float.MaxValue;
        int bestIndex = -1;

        foreach ((float nodeX, int nodeIndex) in nodesByX)
        {
            float distance = Mathf.Abs(nodeX - targetX);

            if (distance > maxDistance || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestIndex = nodeIndex;
        }

        return bestIndex;
    }

    private static void AddConnection(
        int a,
        int b,
        List<MainEscapeVentConnectionDefinition> connections,
        HashSet<long> seenConnections)
    {
        if (a < 0 || b < 0 || a == b)
        {
            return;
        }

        int minIndex = Math.Min(a, b);
        int maxIndex = Math.Max(a, b);
        long connectionKey = ((long)minIndex << 32) | (uint)maxIndex;

        if (seenConnections.Add(connectionKey))
        {
            connections.Add(new MainEscapeVentConnectionDefinition(minIndex, maxIndex));
        }
    }

    private static float RoundToHalf(float value)
    {
        return (float)Math.Round(value * 2f, MidpointRounding.AwayFromZero) * 0.5f;
    }
}

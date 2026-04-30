using System;

using UnityEngine;

public readonly struct SceneRouteGraphSnapshot
{
    public SceneRouteGraphSnapshot(
        string chapterId,
        string routeGraphId,
        string startNodeId,
        SceneNodeDefinition[] sceneNodes,
        RouteEdgeDefinition[] edges)
    {
        ChapterId = chapterId?.Trim() ?? string.Empty;
        RouteGraphId = routeGraphId?.Trim() ?? string.Empty;
        StartNodeId = startNodeId?.Trim() ?? string.Empty;
        SceneNodes = sceneNodes ?? Array.Empty<SceneNodeDefinition>();
        Edges = edges ?? Array.Empty<RouteEdgeDefinition>();
    }

    public string ChapterId { get; }
    public string RouteGraphId { get; }
    public string StartNodeId { get; }
    public SceneNodeDefinition[] SceneNodes { get; }
    public RouteEdgeDefinition[] Edges { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(ChapterId)
        && !string.IsNullOrWhiteSpace(RouteGraphId)
        && !string.IsNullOrWhiteSpace(StartNodeId)
        && SceneNodes.Length > 0;
}

public static class HospitalRouteGraphAdapter
{
    public const string ChapterId = "hospital";
    public const string RouteGraphId = "hospital_default";
    public const string DescentExitId = "descent";

    public static SceneRouteGraphSnapshot Build(int startingFloorNumber, RFloorSceneEntry[] floorScenes)
    {
        RFloorSceneEntry[] routes = CloneValidRoutes(floorScenes);

        if (routes.Length == 0)
        {
            return new SceneRouteGraphSnapshot(
                ChapterId,
                RouteGraphId,
                string.Empty,
                Array.Empty<SceneNodeDefinition>(),
                Array.Empty<RouteEdgeDefinition>());
        }

        SortDescending(routes);
        string startNodeId = BuildNodeId(ResolveStartFloorNumber(startingFloorNumber, routes));
        SceneNodeDefinition[] nodes = BuildNodes(routes);
        RouteEdgeDefinition[] edges = BuildFixedDescentEdges(routes);

        return new SceneRouteGraphSnapshot(ChapterId, RouteGraphId, startNodeId, nodes, edges);
    }

    public static string BuildNodeId(int floorNumber)
    {
        return $"hospital_{Mathf.Max(1, floorNumber)}f";
    }

    public static bool TryReadFloorNumber(SceneNodeDefinition sceneNode, out int floorNumber)
    {
        if (TryReadFloorNumber(sceneNode.chapterLocalLevelId, out floorNumber))
        {
            return true;
        }

        return TryReadFloorNumber(sceneNode.nodeId, out floorNumber);
    }

    private static bool TryReadFloorNumber(string value, out int floorNumber)
    {
        string normalizedValue = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(normalizedValue))
        {
            floorNumber = 0;
            return false;
        }

        int endIndex = normalizedValue.EndsWith("f", StringComparison.Ordinal)
            ? normalizedValue.Length - 1
            : normalizedValue.Length;
        int startIndex = endIndex - 1;

        while (startIndex >= 0 && char.IsDigit(normalizedValue[startIndex]))
        {
            startIndex--;
        }

        startIndex++;

        if (startIndex >= endIndex
            || !int.TryParse(normalizedValue.Substring(startIndex, endIndex - startIndex), out floorNumber))
        {
            floorNumber = 0;
            return false;
        }

        floorNumber = Mathf.Max(1, floorNumber);
        return true;
    }

    private static SceneNodeDefinition[] BuildNodes(RFloorSceneEntry[] routes)
    {
        SceneNodeDefinition[] nodes = new SceneNodeDefinition[routes.Length];

        for (int index = 0; index < routes.Length; index++)
        {
            int floorNumber = Mathf.Max(1, routes[index].floorNumber);
            nodes[index] = new SceneNodeDefinition
            {
                nodeId = BuildNodeId(floorNumber),
                scenePath = routes[index].scenePath,
                kind = SceneNodeKind.Level,
                chapterLocalLevelId = $"{floorNumber}f",
                tags = new[] { ChapterId, $"{floorNumber}f" }
            };
        }

        return nodes;
    }

    private static RouteEdgeDefinition[] BuildFixedDescentEdges(RFloorSceneEntry[] routes)
    {
        if (routes.Length <= 1)
        {
            return Array.Empty<RouteEdgeDefinition>();
        }

        RouteEdgeDefinition[] edges = new RouteEdgeDefinition[routes.Length - 1];

        for (int index = 0; index < routes.Length - 1; index++)
        {
            edges[index] = new RouteEdgeDefinition
            {
                fromNodeId = BuildNodeId(routes[index].floorNumber),
                exitId = DescentExitId,
                toNodeId = BuildNodeId(routes[index + 1].floorNumber),
                policy = RouteEdgePolicy.Fixed,
                weight = 1f
            };
        }

        return edges;
    }

    private static int ResolveStartFloorNumber(int startingFloorNumber, RFloorSceneEntry[] routes)
    {
        int normalizedStart = Mathf.Max(1, startingFloorNumber);

        for (int index = 0; index < routes.Length; index++)
        {
            if (routes[index].floorNumber == normalizedStart)
            {
                return normalizedStart;
            }
        }

        return routes[0].floorNumber;
    }

    private static RFloorSceneEntry[] CloneValidRoutes(RFloorSceneEntry[] floorScenes)
    {
        if (floorScenes == null || floorScenes.Length == 0)
        {
            return Array.Empty<RFloorSceneEntry>();
        }

        int validCount = 0;

        for (int index = 0; index < floorScenes.Length; index++)
        {
            if (floorScenes[index].floorNumber > 0 && !string.IsNullOrWhiteSpace(floorScenes[index].scenePath))
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return Array.Empty<RFloorSceneEntry>();
        }

        RFloorSceneEntry[] routes = new RFloorSceneEntry[validCount];
        int writeIndex = 0;

        for (int index = 0; index < floorScenes.Length; index++)
        {
            RFloorSceneEntry route = floorScenes[index];

            if (route.floorNumber <= 0 || string.IsNullOrWhiteSpace(route.scenePath))
            {
                continue;
            }

            routes[writeIndex] = new RFloorSceneEntry(route.floorNumber, route.scenePath.Trim());
            writeIndex++;
        }

        return routes;
    }

    private static void SortDescending(RFloorSceneEntry[] routes)
    {
        Array.Sort(routes, static (left, right) => right.floorNumber.CompareTo(left.floorNumber));
    }
}

using System;

using UnityEngine;

public static class RouteGraphFloorRouteAdapter
{
    public static bool TryBuildSnapshot(ChapterDefinition chapterDefinition, out SceneRouteGraphSnapshot snapshot)
    {
        if (chapterDefinition == null || chapterDefinition.StartRouteGraph == null || !chapterDefinition.StartRouteGraph.IsValid)
        {
            snapshot = default;
            return false;
        }

        RouteGraphDefinition routeGraph = chapterDefinition.StartRouteGraph;
        snapshot = new SceneRouteGraphSnapshot(
            chapterDefinition.ChapterId,
            routeGraph.RouteGraphId,
            routeGraph.StartNodeId,
            routeGraph.SceneNodes,
            routeGraph.Edges);
        return snapshot.IsValid;
    }

    public static bool TryResolveStartingFloorNumber(ChapterDefinition chapterDefinition, out int floorNumber)
    {
        if (!TryBuildSnapshot(chapterDefinition, out SceneRouteGraphSnapshot snapshot)
            || !TryFindNode(snapshot.SceneNodes, snapshot.StartNodeId, out SceneNodeDefinition startNode))
        {
            floorNumber = 0;
            return false;
        }

        return HospitalRouteGraphAdapter.TryReadFloorNumber(startNode, out floorNumber);
    }

    public static bool TryBuildFloorSceneEntries(ChapterDefinition chapterDefinition, out RFloorSceneEntry[] floorScenes)
    {
        if (!TryBuildSnapshot(chapterDefinition, out SceneRouteGraphSnapshot snapshot))
        {
            floorScenes = Array.Empty<RFloorSceneEntry>();
            return false;
        }

        SceneNodeDefinition[] sceneNodes = snapshot.SceneNodes;
        RFloorSceneEntry[] entries = new RFloorSceneEntry[sceneNodes.Length];
        int entryCount = 0;

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            SceneNodeDefinition sceneNode = sceneNodes[index];

            if (sceneNode.kind != SceneNodeKind.Level
                || string.IsNullOrWhiteSpace(sceneNode.scenePath)
                || !HospitalRouteGraphAdapter.TryReadFloorNumber(sceneNode, out int floorNumber))
            {
                continue;
            }

            entries[entryCount++] = new RFloorSceneEntry(floorNumber, sceneNode.scenePath.Trim());
        }

        if (entryCount == 0)
        {
            floorScenes = Array.Empty<RFloorSceneEntry>();
            return false;
        }

        if (entryCount != entries.Length)
        {
            Array.Resize(ref entries, entryCount);
        }

        Array.Sort(entries, static (left, right) => right.floorNumber.CompareTo(left.floorNumber));
        floorScenes = entries;
        return true;
    }

    public static bool TryResolveSceneNodeIdForFloor(
        ChapterDefinition chapterDefinition,
        int floorNumber,
        out string sceneNodeId)
    {
        if (!TryBuildSnapshot(chapterDefinition, out SceneRouteGraphSnapshot snapshot))
        {
            sceneNodeId = string.Empty;
            return false;
        }

        int normalizedFloorNumber = Mathf.Max(1, floorNumber);
        SceneNodeDefinition[] sceneNodes = snapshot.SceneNodes;

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            SceneNodeDefinition sceneNode = sceneNodes[index];

            if (HospitalRouteGraphAdapter.TryReadFloorNumber(sceneNode, out int nodeFloorNumber)
                && nodeFloorNumber == normalizedFloorNumber
                && !string.IsNullOrWhiteSpace(sceneNode.nodeId))
            {
                sceneNodeId = sceneNode.nodeId.Trim();
                return true;
            }
        }

        sceneNodeId = string.Empty;
        return false;
    }

    public static bool TryResolveNextFloorNumber(
        ChapterDefinition chapterDefinition,
        int currentFloorNumber,
        string exitId,
        int runSeed,
        out int destinationFloorNumber)
    {
        if (!TryBuildSnapshot(chapterDefinition, out SceneRouteGraphSnapshot snapshot)
            || !TryResolveSceneNodeIdForFloor(chapterDefinition, currentFloorNumber, out string currentSceneNodeId))
        {
            destinationFloorNumber = 0;
            return false;
        }

        bool resolved = SceneRouteGraphResolver.TryResolveNextSceneNode(
            snapshot.SceneNodes,
            snapshot.Edges,
            snapshot.StartNodeId,
            currentSceneNodeId,
            exitId,
            runSeed,
            null,
            null,
            out SceneNodeDefinition destinationNode,
            out _);

        if (resolved && HospitalRouteGraphAdapter.TryReadFloorNumber(destinationNode, out destinationFloorNumber))
        {
            return true;
        }

        destinationFloorNumber = 0;
        return false;
    }

    private static bool TryFindNode(SceneNodeDefinition[] sceneNodes, string nodeId, out SceneNodeDefinition node)
    {
        string normalizedNodeId = nodeId?.Trim() ?? string.Empty;

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            if (string.Equals(sceneNodes[index].nodeId, normalizedNodeId, StringComparison.Ordinal))
            {
                node = sceneNodes[index];
                return node.IsValid;
            }
        }

        node = default;
        return false;
    }
}

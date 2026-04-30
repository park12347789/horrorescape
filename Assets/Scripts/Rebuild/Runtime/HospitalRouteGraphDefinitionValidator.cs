using System;

public static class HospitalRouteGraphDefinitionValidator
{
    private static readonly int StartFloorNumber = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
    private static readonly int EndFloorNumber = MainEscapeSceneIdentityUtility.GetCanonicalTerminalFloorNumber();

    public static bool TryValidateHospitalChain(
        ChapterDefinition chapter,
        RouteGraphDefinition routeGraph,
        out string[] errors)
    {
        var collector = new ErrorCollector();

        if (chapter == null)
        {
            collector.Add("Hospital chapter asset is missing.");
        }
        else
        {
            if (!string.Equals(chapter.ChapterId, HospitalRouteGraphAdapter.ChapterId, StringComparison.Ordinal))
            {
                collector.Add($"Hospital chapter id must be '{HospitalRouteGraphAdapter.ChapterId}'.");
            }

            if (routeGraph != null && !ReferenceEquals(chapter.StartRouteGraph, routeGraph))
            {
                collector.Add("Hospital chapter does not reference the hospital route graph.");
            }
        }

        if (routeGraph == null)
        {
            collector.Add("Hospital route graph asset is missing.");
            errors = collector.ToArray();
            return false;
        }

        if (!RouteGraphDefinitionValidator.TryValidate(routeGraph, out string[] routeGraphErrors))
        {
            for (int index = 0; index < routeGraphErrors.Length; index++)
            {
                collector.Add(routeGraphErrors[index]);
            }
        }

        ValidateIdentity(routeGraph, ref collector);
        ValidateNodes(routeGraph.SceneNodes, ref collector);
        ValidateEdges(routeGraph.Edges, ref collector);

        errors = collector.ToArray();
        return errors.Length == 0;
    }

    private static void ValidateIdentity(RouteGraphDefinition routeGraph, ref ErrorCollector collector)
    {
        string expectedStartNodeId = HospitalRouteGraphAdapter.BuildNodeId(StartFloorNumber);

        if (!string.Equals(routeGraph.RouteGraphId, HospitalRouteGraphAdapter.RouteGraphId, StringComparison.Ordinal))
        {
            collector.Add($"Hospital route graph id must be '{HospitalRouteGraphAdapter.RouteGraphId}'.");
        }

        if (!string.Equals(routeGraph.StartNodeId, expectedStartNodeId, StringComparison.Ordinal))
        {
            collector.Add($"Hospital route graph start node must be '{expectedStartNodeId}'.");
        }
    }

    private static void ValidateNodes(SceneNodeDefinition[] sceneNodes, ref ErrorCollector collector)
    {
        int expectedCount = StartFloorNumber - EndFloorNumber + 1;

        if (sceneNodes.Length != expectedCount)
        {
            collector.Add($"Hospital route graph must define exactly {expectedCount} floor nodes.");
        }

        for (int floorNumber = StartFloorNumber; floorNumber >= EndFloorNumber; floorNumber--)
        {
            string expectedNodeId = HospitalRouteGraphAdapter.BuildNodeId(floorNumber);
            string expectedScenePath = MainEscapeSceneIdentityUtility.GetCanonicalFloorScenePath(floorNumber);

            if (!TryFindNode(sceneNodes, expectedNodeId, out SceneNodeDefinition sceneNode))
            {
                collector.Add($"Hospital route graph is missing node '{expectedNodeId}'.");
                continue;
            }

            if (!string.Equals(sceneNode.scenePath?.Trim(), expectedScenePath, StringComparison.Ordinal))
            {
                collector.Add($"Hospital node '{expectedNodeId}' must use scene path '{expectedScenePath}'.");
            }

            if (!string.Equals(sceneNode.chapterLocalLevelId?.Trim(), $"{floorNumber}f", StringComparison.Ordinal))
            {
                collector.Add($"Hospital node '{expectedNodeId}' must use chapter local level id '{floorNumber}f'.");
            }
        }
    }

    private static void ValidateEdges(RouteEdgeDefinition[] edges, ref ErrorCollector collector)
    {
        int expectedCount = StartFloorNumber - EndFloorNumber;

        if (edges.Length != expectedCount)
        {
            collector.Add($"Hospital route graph must define exactly {expectedCount} descent edges.");
        }

        for (int floorNumber = StartFloorNumber; floorNumber > EndFloorNumber; floorNumber--)
        {
            string expectedFromNodeId = HospitalRouteGraphAdapter.BuildNodeId(floorNumber);
            string expectedToNodeId = HospitalRouteGraphAdapter.BuildNodeId(floorNumber - 1);

            if (!HasFixedDescentEdge(edges, expectedFromNodeId, expectedToNodeId))
            {
                collector.Add($"Hospital route graph is missing descent edge '{expectedFromNodeId}' to '{expectedToNodeId}'.");
            }
        }
    }

    private static bool TryFindNode(
        SceneNodeDefinition[] sceneNodes,
        string nodeId,
        out SceneNodeDefinition sceneNode)
    {
        for (int index = 0; index < sceneNodes.Length; index++)
        {
            if (string.Equals(sceneNodes[index].nodeId?.Trim(), nodeId, StringComparison.Ordinal))
            {
                sceneNode = sceneNodes[index];
                return true;
            }
        }

        sceneNode = default;
        return false;
    }

    private static bool HasFixedDescentEdge(
        RouteEdgeDefinition[] edges,
        string fromNodeId,
        string toNodeId)
    {
        for (int index = 0; index < edges.Length; index++)
        {
            RouteEdgeDefinition edge = edges[index];

            if (string.Equals(edge.fromNodeId?.Trim(), fromNodeId, StringComparison.Ordinal)
                && string.Equals(edge.exitId?.Trim(), HospitalRouteGraphAdapter.DescentExitId, StringComparison.Ordinal)
                && string.Equals(edge.toNodeId?.Trim(), toNodeId, StringComparison.Ordinal)
                && edge.policy == RouteEdgePolicy.Fixed)
            {
                return true;
            }
        }

        return false;
    }

    private struct ErrorCollector
    {
        private string[] errors;
        private int count;

        public void Add(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return;
            }

            errors ??= new string[4];

            if (count >= errors.Length)
            {
                Array.Resize(ref errors, errors.Length * 2);
            }

            errors[count++] = error;
        }

        public string[] ToArray()
        {
            if (count == 0)
            {
                return Array.Empty<string>();
            }

            string[] result = new string[count];
            Array.Copy(errors, result, count);
            return result;
        }
    }
}

using System;

public static class RouteGraphDefinitionValidator
{
    public static bool TryValidate(RouteGraphDefinition routeGraph, out string[] errors)
    {
        var collector = new ErrorCollector();

        if (routeGraph == null)
        {
            collector.Add("Route graph asset is missing.");
            errors = collector.ToArray();
            return false;
        }

        ValidateRouteGraphIdentity(routeGraph, ref collector);
        ValidateSceneNodes(routeGraph.SceneNodes, routeGraph.StartNodeId, ref collector);
        ValidateEdges(routeGraph.SceneNodes, routeGraph.Edges, ref collector);

        errors = collector.ToArray();
        return errors.Length == 0;
    }

    private static void ValidateRouteGraphIdentity(RouteGraphDefinition routeGraph, ref ErrorCollector collector)
    {
        if (string.IsNullOrWhiteSpace(routeGraph.RouteGraphId))
        {
            collector.Add("Route graph id is empty.");
        }

        if (string.IsNullOrWhiteSpace(routeGraph.StartNodeId))
        {
            collector.Add("Start node id is empty.");
        }
    }

    private static void ValidateSceneNodes(
        SceneNodeDefinition[] sceneNodes,
        string startNodeId,
        ref ErrorCollector collector)
    {
        if (sceneNodes == null || sceneNodes.Length == 0)
        {
            collector.Add("Route graph has no scene nodes.");
            return;
        }

        bool hasStartNode = false;

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            SceneNodeDefinition sceneNode = sceneNodes[index];
            string nodeId = sceneNode.nodeId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nodeId))
            {
                collector.Add($"Scene node at index {index} has no node id.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(sceneNode.scenePath))
            {
                collector.Add($"Scene node '{nodeId}' has no scene path.");
            }
            else
            {
                string scenePath = sceneNode.scenePath.Trim();

                for (int compareIndex = index + 1; compareIndex < sceneNodes.Length; compareIndex++)
                {
                    if (string.Equals(scenePath, sceneNodes[compareIndex].scenePath?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        collector.Add($"Scene path '{scenePath}' is duplicated.");
                        break;
                    }
                }
            }

            if (string.Equals(nodeId, startNodeId?.Trim() ?? string.Empty, StringComparison.Ordinal))
            {
                hasStartNode = true;
            }

            for (int compareIndex = index + 1; compareIndex < sceneNodes.Length; compareIndex++)
            {
                if (string.Equals(nodeId, sceneNodes[compareIndex].nodeId?.Trim(), StringComparison.Ordinal))
                {
                    collector.Add($"Scene node id '{nodeId}' is duplicated.");
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(startNodeId) && !hasStartNode)
        {
            collector.Add($"Start node '{startNodeId.Trim()}' does not exist in scene nodes.");
        }
    }

    private static void ValidateEdges(
        SceneNodeDefinition[] sceneNodes,
        RouteEdgeDefinition[] edges,
        ref ErrorCollector collector)
    {
        if (edges == null)
        {
            return;
        }

        for (int index = 0; index < edges.Length; index++)
        {
            RouteEdgeDefinition edge = edges[index];

            if (!edge.IsValid)
            {
                collector.Add($"Route edge at index {index} is incomplete.");
                continue;
            }

            if (!ContainsNode(sceneNodes, edge.fromNodeId))
            {
                collector.Add($"Route edge '{edge.fromNodeId}:{edge.exitId}' references missing from node '{edge.fromNodeId}'.");
            }

            if (!ContainsNode(sceneNodes, edge.toNodeId))
            {
                collector.Add($"Route edge '{edge.fromNodeId}:{edge.exitId}' references missing target node '{edge.toNodeId}'.");
            }

            if (edge.policy == RouteEdgePolicy.WeightedRandom && edge.weight < 0f)
            {
                collector.Add($"Weighted route edge '{edge.fromNodeId}:{edge.exitId}' has a negative weight.");
            }
        }
    }

    private static bool ContainsNode(SceneNodeDefinition[] sceneNodes, string nodeId)
    {
        string normalizedNodeId = nodeId?.Trim() ?? string.Empty;

        if (sceneNodes == null || string.IsNullOrEmpty(normalizedNodeId))
        {
            return false;
        }

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            if (string.Equals(sceneNodes[index].nodeId?.Trim(), normalizedNodeId, StringComparison.Ordinal))
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

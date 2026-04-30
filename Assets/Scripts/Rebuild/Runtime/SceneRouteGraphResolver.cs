using System;

public static class SceneRouteGraphResolver
{
    public static bool TryResolveNextSceneNode(
        RouteGraphDefinition graph,
        string currentNodeId,
        string exitId,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out SceneNodeDefinition nextNode)
    {
        return TryResolveNextSceneNode(
            graph,
            currentNodeId,
            exitId,
            runSeed,
            grantedProfileEventIds,
            activeRunFlags,
            out nextNode,
            out _);
    }

    public static bool TryResolveNextSceneNode(
        RouteGraphDefinition graph,
        string currentNodeId,
        string exitId,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out SceneNodeDefinition nextNode,
        out RouteEdgeDefinition resolvedEdge)
    {
        nextNode = default;
        resolvedEdge = default;

        if (!TryResolveNextRouteEdge(
                graph,
                currentNodeId,
                exitId,
                runSeed,
                grantedProfileEventIds,
                activeRunFlags,
                out resolvedEdge))
        {
            return false;
        }

        return graph.TryGetSceneNode(resolvedEdge.toNodeId, out nextNode);
    }

    public static bool TryResolveNextSceneNode(
        SceneNodeDefinition[] sceneNodes,
        RouteEdgeDefinition[] edges,
        string startNodeId,
        string currentNodeId,
        string exitId,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out SceneNodeDefinition nextNode,
        out RouteEdgeDefinition resolvedEdge)
    {
        nextNode = default;
        resolvedEdge = default;

        if (!TryResolveNextRouteEdge(
                edges,
                startNodeId,
                currentNodeId,
                exitId,
                runSeed,
                grantedProfileEventIds,
                activeRunFlags,
                out resolvedEdge))
        {
            return false;
        }

        return TryGetSceneNode(sceneNodes, resolvedEdge.toNodeId, out nextNode);
    }

    public static bool TryResolveNextRouteEdge(
        RouteGraphDefinition graph,
        string currentNodeId,
        string exitId,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out RouteEdgeDefinition resolvedEdge)
    {
        resolvedEdge = default;

        if (graph == null)
        {
            return false;
        }

        return TryResolveNextRouteEdge(
            graph.Edges,
            graph.StartNodeId,
            currentNodeId,
            exitId,
            runSeed,
            grantedProfileEventIds,
            activeRunFlags,
            out resolvedEdge);
    }

    public static bool TryResolveNextRouteEdge(
        RouteEdgeDefinition[] edges,
        string startNodeId,
        string currentNodeId,
        string exitId,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out RouteEdgeDefinition resolvedEdge)
    {
        resolvedEdge = default;

        string normalizedNodeId = string.IsNullOrWhiteSpace(currentNodeId)
            ? startNodeId
            : currentNodeId.Trim();
        string normalizedExitId = exitId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedNodeId) || string.IsNullOrWhiteSpace(normalizedExitId))
        {
            return false;
        }

        if (TrySelectEdge(
                edges,
                normalizedNodeId,
                normalizedExitId,
                RouteEdgePolicy.Conditional,
                runSeed,
                grantedProfileEventIds,
                activeRunFlags,
                out resolvedEdge))
        {
            return true;
        }

        if (TrySelectEdge(
                edges,
                normalizedNodeId,
                normalizedExitId,
                RouteEdgePolicy.WeightedRandom,
                runSeed,
                grantedProfileEventIds,
                activeRunFlags,
                out resolvedEdge))
        {
            return true;
        }

        if (TrySelectEdge(
                edges,
                normalizedNodeId,
                normalizedExitId,
                RouteEdgePolicy.Choice,
                runSeed,
                grantedProfileEventIds,
                activeRunFlags,
                out resolvedEdge))
        {
            return true;
        }

        if (TrySelectEdge(
                edges,
                normalizedNodeId,
                normalizedExitId,
                RouteEdgePolicy.Loop,
                runSeed,
                grantedProfileEventIds,
                activeRunFlags,
                out resolvedEdge))
        {
            return true;
        }

        return TrySelectEdge(
            edges,
            normalizedNodeId,
            normalizedExitId,
            RouteEdgePolicy.Fixed,
            runSeed,
            grantedProfileEventIds,
            activeRunFlags,
            out resolvedEdge);
    }

    private static bool TryGetSceneNode(SceneNodeDefinition[] sceneNodes, string nodeId, out SceneNodeDefinition sceneNode)
    {
        string normalizedNodeId = nodeId?.Trim() ?? string.Empty;

        if (sceneNodes != null && !string.IsNullOrEmpty(normalizedNodeId))
        {
            for (int index = 0; index < sceneNodes.Length; index++)
            {
                SceneNodeDefinition candidate = sceneNodes[index];

                if (string.Equals(candidate.nodeId?.Trim(), normalizedNodeId, StringComparison.Ordinal))
                {
                    sceneNode = candidate;
                    return candidate.IsValid;
                }
            }
        }

        sceneNode = default;
        return false;
    }

    private static bool TrySelectEdge(
        RouteEdgeDefinition[] edges,
        string fromNodeId,
        string exitId,
        RouteEdgePolicy policy,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out RouteEdgeDefinition resolvedEdge)
    {
        resolvedEdge = default;

        if (edges == null || edges.Length == 0)
        {
            return false;
        }

        return policy == RouteEdgePolicy.WeightedRandom
            ? TrySelectWeightedEdge(edges, fromNodeId, exitId, runSeed, grantedProfileEventIds, activeRunFlags, out resolvedEdge)
            : TrySelectBranchEdge(edges, fromNodeId, exitId, policy, runSeed, grantedProfileEventIds, activeRunFlags, out resolvedEdge);
    }

    private static bool TrySelectBranchEdge(
        RouteEdgeDefinition[] edges,
        string fromNodeId,
        string exitId,
        RouteEdgePolicy policy,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out RouteEdgeDefinition resolvedEdge)
    {
        resolvedEdge = default;
        int eligibleCount = CountEligibleEdges(edges, fromNodeId, exitId, policy, grantedProfileEventIds, activeRunFlags);

        if (eligibleCount <= 0)
        {
            return false;
        }

        int selectedIndex = policy == RouteEdgePolicy.Fixed && eligibleCount == 1
            ? 0
            : PositiveModulo(StableHash(runSeed, fromNodeId, exitId, policy.ToString()), eligibleCount);

        return TryGetEligibleEdgeAt(
            edges,
            fromNodeId,
            exitId,
            policy,
            grantedProfileEventIds,
            activeRunFlags,
            selectedIndex,
            out resolvedEdge);
    }

    private static bool TrySelectWeightedEdge(
        RouteEdgeDefinition[] edges,
        string fromNodeId,
        string exitId,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out RouteEdgeDefinition resolvedEdge)
    {
        resolvedEdge = default;
        float totalWeight = 0f;

        for (int index = 0; index < edges.Length; index++)
        {
            RouteEdgeDefinition edge = edges[index];

            if (!IsEligibleEdge(edge, fromNodeId, exitId, RouteEdgePolicy.WeightedRandom, grantedProfileEventIds, activeRunFlags)
                || edge.weight <= 0f)
            {
                continue;
            }

            totalWeight += edge.weight;
        }

        if (totalWeight <= 0f)
        {
            return TrySelectBranchEdge(
                edges,
                fromNodeId,
                exitId,
                RouteEdgePolicy.WeightedRandom,
                runSeed,
                grantedProfileEventIds,
                activeRunFlags,
                out resolvedEdge);
        }

        float selectedPoint = StableUnitFloat(runSeed, fromNodeId, exitId, nameof(RouteEdgePolicy.WeightedRandom)) * totalWeight;
        float accumulatedWeight = 0f;

        for (int index = 0; index < edges.Length; index++)
        {
            RouteEdgeDefinition edge = edges[index];

            if (!IsEligibleEdge(edge, fromNodeId, exitId, RouteEdgePolicy.WeightedRandom, grantedProfileEventIds, activeRunFlags)
                || edge.weight <= 0f)
            {
                continue;
            }

            accumulatedWeight += edge.weight;

            if (selectedPoint < accumulatedWeight)
            {
                resolvedEdge = edge;
                return true;
            }
        }

        return false;
    }

    private static int CountEligibleEdges(
        RouteEdgeDefinition[] edges,
        string fromNodeId,
        string exitId,
        RouteEdgePolicy policy,
        string[] grantedProfileEventIds,
        string[] activeRunFlags)
    {
        int count = 0;

        for (int index = 0; index < edges.Length; index++)
        {
            if (IsEligibleEdge(edges[index], fromNodeId, exitId, policy, grantedProfileEventIds, activeRunFlags))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryGetEligibleEdgeAt(
        RouteEdgeDefinition[] edges,
        string fromNodeId,
        string exitId,
        RouteEdgePolicy policy,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        int selectedIndex,
        out RouteEdgeDefinition resolvedEdge)
    {
        int currentIndex = 0;

        for (int index = 0; index < edges.Length; index++)
        {
            RouteEdgeDefinition edge = edges[index];

            if (!IsEligibleEdge(edge, fromNodeId, exitId, policy, grantedProfileEventIds, activeRunFlags))
            {
                continue;
            }

            if (currentIndex == selectedIndex)
            {
                resolvedEdge = edge;
                return true;
            }

            currentIndex++;
        }

        resolvedEdge = default;
        return false;
    }

    private static bool IsEligibleEdge(
        RouteEdgeDefinition edge,
        string fromNodeId,
        string exitId,
        RouteEdgePolicy policy,
        string[] grantedProfileEventIds,
        string[] activeRunFlags)
    {
        return edge.IsValid
            && edge.policy == policy
            && string.Equals(edge.fromNodeId?.Trim(), fromNodeId, StringComparison.Ordinal)
            && string.Equals(edge.exitId?.Trim(), exitId, StringComparison.Ordinal)
            && HasRequiredIdentifier(edge.requiredProfileEventId, grantedProfileEventIds)
            && HasRequiredIdentifier(edge.requiredRunFlag, activeRunFlags);
    }

    private static bool HasRequiredIdentifier(string requiredId, string[] availableIds)
    {
        string normalizedRequiredId = requiredId?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(normalizedRequiredId))
        {
            return true;
        }

        if (availableIds == null || availableIds.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < availableIds.Length; index++)
        {
            if (string.Equals(availableIds[index]?.Trim(), normalizedRequiredId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static float StableUnitFloat(int seed, string fromNodeId, string exitId, string salt)
    {
        const int bucketCount = 1000000;
        return PositiveModulo(StableHash(seed, fromNodeId, exitId, salt), bucketCount) / (float)bucketCount;
    }

    private static int StableHash(int seed, string fromNodeId, string exitId, string salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            AppendInt(ref hash, seed);
            AppendString(ref hash, fromNodeId);
            AppendString(ref hash, exitId);
            AppendString(ref hash, salt);
            return (int)(hash & 0x7fffffffu);
        }
    }

    private static void AppendInt(ref uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }
    }

    private static void AppendString(ref uint hash, string value)
    {
        string normalizedValue = value ?? string.Empty;

        unchecked
        {
            for (int index = 0; index < normalizedValue.Length; index++)
            {
                hash ^= normalizedValue[index];
                hash *= 16777619u;
            }
        }
    }

    private static int PositiveModulo(int value, int modulus)
    {
        if (modulus <= 0)
        {
            return 0;
        }

        return value % modulus;
    }
}

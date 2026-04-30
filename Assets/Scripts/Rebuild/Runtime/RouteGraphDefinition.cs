using System;

using UnityEngine;

[CreateAssetMenu(
    fileName = "RouteGraphDefinition",
    menuName = "Main Escape/Contracts/Route Graph Definition")]
public sealed class RouteGraphDefinition : ScriptableObject
{
    [SerializeField] private string routeGraphId = "hospital_default";
    [SerializeField] private string startNodeId = "hospital_5f";
    [SerializeField] private SceneNodeDefinition[] sceneNodes = Array.Empty<SceneNodeDefinition>();
    [SerializeField] private RouteEdgeDefinition[] edges = Array.Empty<RouteEdgeDefinition>();

    public string RouteGraphId => routeGraphId?.Trim() ?? string.Empty;
    public string StartNodeId => startNodeId?.Trim() ?? string.Empty;
    public SceneNodeDefinition[] SceneNodes => sceneNodes ?? Array.Empty<SceneNodeDefinition>();
    public RouteEdgeDefinition[] Edges => edges ?? Array.Empty<RouteEdgeDefinition>();
    public bool IsValid => !string.IsNullOrWhiteSpace(RouteGraphId)
        && TryGetSceneNode(StartNodeId, out _);

    public bool TryGetSceneNode(string nodeId, out SceneNodeDefinition node)
    {
        string normalizedNodeId = nodeId?.Trim() ?? string.Empty;
        SceneNodeDefinition[] nodes = SceneNodes;

        for (int index = 0; index < nodes.Length; index++)
        {
            if (string.Equals(nodes[index].nodeId, normalizedNodeId, StringComparison.Ordinal))
            {
                node = nodes[index];
                return node.IsValid;
            }
        }

        node = default;
        return false;
    }

    public RouteEdgeDefinition[] GetEdgesFrom(string nodeId)
    {
        string normalizedNodeId = nodeId?.Trim() ?? string.Empty;
        RouteEdgeDefinition[] sourceEdges = Edges;
        int count = 0;

        for (int index = 0; index < sourceEdges.Length; index++)
        {
            if (string.Equals(sourceEdges[index].fromNodeId, normalizedNodeId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Array.Empty<RouteEdgeDefinition>();
        }

        RouteEdgeDefinition[] matches = new RouteEdgeDefinition[count];
        int writeIndex = 0;

        for (int index = 0; index < sourceEdges.Length; index++)
        {
            if (string.Equals(sourceEdges[index].fromNodeId, normalizedNodeId, StringComparison.Ordinal))
            {
                matches[writeIndex] = sourceEdges[index];
                writeIndex++;
            }
        }

        return matches;
    }
}

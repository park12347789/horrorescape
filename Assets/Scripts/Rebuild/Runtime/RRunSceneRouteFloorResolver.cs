using UnityEngine;
using UnityEngine.SceneManagement;

public static class RRunSceneRouteFloorResolver
{
    public static bool TryResolveNextFloorNumber(
        int startingFloorNumber,
        RFloorSceneEntry[] floorScenes,
        int currentFloorNumber,
        int runSeed,
        out int destinationFloorNumber)
    {
        return TryResolveNextFloorNumber(
            startingFloorNumber,
            floorScenes,
            currentFloorNumber,
            HospitalRouteGraphAdapter.DescentExitId,
            runSeed,
            out destinationFloorNumber);
    }

    public static bool TryResolveNextFloorNumber(
        int startingFloorNumber,
        RFloorSceneEntry[] floorScenes,
        int currentFloorNumber,
        string exitId,
        int runSeed,
        out int destinationFloorNumber)
    {
        SceneRouteGraphSnapshot snapshot = HospitalRouteGraphAdapter.Build(startingFloorNumber, floorScenes);

        if (!snapshot.IsValid)
        {
            destinationFloorNumber = 0;
            return false;
        }

        bool resolved = SceneRouteGraphResolver.TryResolveNextSceneNode(
            snapshot.SceneNodes,
            snapshot.Edges,
            snapshot.StartNodeId,
            HospitalRouteGraphAdapter.BuildNodeId(Mathf.Max(1, currentFloorNumber)),
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

    public static bool TryResolveFloorNumberForScene(
        int startingFloorNumber,
        RFloorSceneEntry[] floorScenes,
        Scene scene,
        out int floorNumber)
    {
        if (!scene.IsValid())
        {
            floorNumber = 0;
            return false;
        }

        return TryResolveFloorNumberForScenePath(
            startingFloorNumber,
            floorScenes,
            MainEscapeSceneIdentityUtility.GetScenePathOrName(scene),
            out floorNumber);
    }

    public static bool TryResolveFloorNumberForScenePath(
        int startingFloorNumber,
        RFloorSceneEntry[] floorScenes,
        string scenePathOrName,
        out int floorNumber)
    {
        SceneRouteGraphSnapshot snapshot = HospitalRouteGraphAdapter.Build(startingFloorNumber, floorScenes);

        if (!snapshot.IsValid || string.IsNullOrWhiteSpace(scenePathOrName))
        {
            floorNumber = 0;
            return false;
        }

        SceneNodeDefinition[] sceneNodes = snapshot.SceneNodes;

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            SceneNodeDefinition sceneNode = sceneNodes[index];

            if (!ScenePathMatches(scenePathOrName, sceneNode.scenePath)
                || !HospitalRouteGraphAdapter.TryReadFloorNumber(sceneNode, out floorNumber))
            {
                continue;
            }

            return true;
        }

        floorNumber = 0;
        return false;
    }

    public static bool TryResolveScenePathForFloor(
        int startingFloorNumber,
        RFloorSceneEntry[] floorScenes,
        int floorNumber,
        out string scenePath)
    {
        SceneRouteGraphSnapshot snapshot = HospitalRouteGraphAdapter.Build(startingFloorNumber, floorScenes);

        if (!snapshot.IsValid)
        {
            scenePath = string.Empty;
            return false;
        }

        int normalizedFloorNumber = Mathf.Max(1, floorNumber);
        SceneNodeDefinition[] sceneNodes = snapshot.SceneNodes;

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            SceneNodeDefinition sceneNode = sceneNodes[index];

            if (!HospitalRouteGraphAdapter.TryReadFloorNumber(sceneNode, out int nodeFloorNumber)
                || nodeFloorNumber != normalizedFloorNumber
                || string.IsNullOrWhiteSpace(sceneNode.scenePath))
            {
                continue;
            }

            scenePath = sceneNode.scenePath.Trim();
            return true;
        }

        scenePath = string.Empty;
        return false;
    }

    private static bool ScenePathMatches(string scenePathOrName, string configuredScenePath)
    {
        string normalizedScenePathOrName = scenePathOrName?.Trim() ?? string.Empty;
        string normalizedConfiguredScenePath = configuredScenePath?.Trim() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(normalizedScenePathOrName)
            && !string.IsNullOrWhiteSpace(normalizedConfiguredScenePath)
            && (string.Equals(normalizedScenePathOrName, normalizedConfiguredScenePath, System.StringComparison.Ordinal)
                || MainEscapeSceneIdentityUtility.MatchesScenePath(normalizedScenePathOrName, normalizedConfiguredScenePath));
    }
}

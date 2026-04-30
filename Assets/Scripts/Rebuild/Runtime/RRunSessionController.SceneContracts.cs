public sealed partial class RRunSessionController
{
    private SceneRouteGraphSnapshot BuildRouteGraphSnapshot()
    {
        if (RouteGraphFloorRouteAdapter.TryBuildSnapshot(
                GetCurrentChapterDefinition(),
                out SceneRouteGraphSnapshot routeGraphSnapshot))
        {
            return routeGraphSnapshot;
        }

        return HospitalRouteGraphAdapter.Build(StartingFloorNumber, GetConfiguredFloorScenes());
    }

    private string GetCurrentSceneNodeId()
    {
        int floorNumber = currentFloorNumber > 0 ? currentFloorNumber : StartingFloorNumber;
        if (RouteGraphFloorRouteAdapter.TryResolveSceneNodeIdForFloor(
                GetCurrentChapterDefinition(),
                floorNumber,
                out string sceneNodeId))
        {
            return sceneNodeId;
        }

        return HospitalRouteGraphAdapter.BuildNodeId(floorNumber);
    }

    private bool TryResolveRouteExitFloorNumber(int currentFloorNumber, out int destinationFloorNumber)
    {
        if (RouteGraphFloorRouteAdapter.TryResolveNextFloorNumber(
                GetCurrentChapterDefinition(),
                currentFloorNumber,
                HospitalRouteGraphAdapter.DescentExitId,
                RunRandomizationSeed,
                out destinationFloorNumber))
        {
            return true;
        }

        return RRunSceneRouteFloorResolver.TryResolveNextFloorNumber(
            StartingFloorNumber,
            GetConfiguredFloorScenes(),
            currentFloorNumber,
            RunRandomizationSeed,
            out destinationFloorNumber);
    }
}

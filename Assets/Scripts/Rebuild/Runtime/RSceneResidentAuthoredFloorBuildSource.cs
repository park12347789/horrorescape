using UnityEngine.SceneManagement;

public sealed class RSceneResidentAuthoredFloorBuildSource : ISceneResidentFloorBuildSource
{
    private readonly MainEscapeFloorAuthoring floorAuthoring;
    private readonly Scene targetScene;
    private readonly bool hasTargetScene;

    public RSceneResidentAuthoredFloorBuildSource()
    {
    }

    public RSceneResidentAuthoredFloorBuildSource(Scene targetScene)
    {
        this.targetScene = targetScene;
        hasTargetScene = true;
    }

    public RSceneResidentAuthoredFloorBuildSource(MainEscapeFloorAuthoring floorAuthoring)
    {
        this.floorAuthoring = floorAuthoring;
    }

    public bool TryBuildSceneResidentFloor(EscapeFloorDefinition floorDefinition, out OfficeFloorBuildResult buildResult)
    {
        if (floorAuthoring != null)
        {
            return Batch2TestRoomBootstrap.TryBuildSceneResidentAuthoredFloor(
                floorDefinition,
                floorAuthoring,
                out buildResult);
        }

        if (hasTargetScene)
        {
            return Batch2TestRoomBootstrap.TryBuildSceneResidentAuthoredFloor(
                floorDefinition,
                targetScene,
                out buildResult);
        }

        return Batch2TestRoomBootstrap.TryBuildSceneResidentAuthoredFloor(
            floorDefinition,
            out buildResult);
    }

    public bool TryBuildSceneResidentFloor(
        EscapeFloorDefinition floorDefinition,
        Scene targetScene,
        out OfficeFloorBuildResult buildResult)
    {
        return Batch2TestRoomBootstrap.TryBuildSceneResidentAuthoredFloor(
            floorDefinition,
            targetScene,
            out buildResult);
    }

    public bool TryBuildSceneResidentFloor(
        EscapeFloorDefinition floorDefinition,
        MainEscapeFloorAuthoring floorAuthoring,
        out OfficeFloorBuildResult buildResult)
    {
        return Batch2TestRoomBootstrap.TryBuildSceneResidentAuthoredFloor(
            floorDefinition,
            floorAuthoring,
            out buildResult);
    }
}

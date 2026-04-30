using UnityEngine;
using UnityEngine.SceneManagement;

public interface IFloorBuildPipeline
{
    bool TryBuildFloor(
        EscapeFloorDefinition floorDefinition,
        Scene scene,
        Transform runtimeParent,
        out OfficeFloorBuildResult buildResult,
        out string failureReason);
}

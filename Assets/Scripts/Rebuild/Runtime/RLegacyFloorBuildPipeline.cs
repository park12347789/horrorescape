using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RLegacyFloorBuildPipeline : IFloorBuildPipeline
{
    private readonly IFloorSceneResidencyPolicy floorSceneResidencyPolicy = new RRuntimeSettingsFloorResidencyPolicy();
    private readonly ISceneResidentFloorBuildSource sceneResidentFloorBuildSource = new RSceneResidentAuthoredFloorBuildSource();
    private readonly IGeneratedFloorBuildSource generatedFloorBuildSource = new RGeneratedOfficeFloorBuildSource();

    public bool TryBuildFloor(
        EscapeFloorDefinition floorDefinition,
        Scene scene,
        Transform runtimeParent,
        out OfficeFloorBuildResult buildResult,
        out string failureReason)
    {
        buildResult = default;
        failureReason = string.Empty;

        if (floorDefinition == null)
        {
            failureReason = "Floor definition is missing.";
            return false;
        }

        if (floorSceneResidencyPolicy.RequiresSceneResidentAuthoring(floorDefinition, scene))
        {
            if (sceneResidentFloorBuildSource.TryBuildSceneResidentFloor(floorDefinition, out buildResult))
            {
                return buildResult.IsValid;
            }

            failureReason =
                $"Scene-resident authored floor {floorDefinition.FloorNumber}F was unavailable in scene '{scene.name}'. " +
                "The live authored floor chain now fails fast instead of falling back to generated or prefab content.";
            buildResult = default;
            return false;
        }

        buildResult = generatedFloorBuildSource.BuildGeneratedFloor(floorDefinition, runtimeParent);

        if (buildResult.IsValid)
        {
            return true;
        }

        failureReason = $"Failed to build rebuild floor {floorDefinition.FloorNumber}F.";
        return false;
    }
}

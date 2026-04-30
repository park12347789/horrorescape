using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RGeneratedOfficeFloorBuildSource : IGeneratedFloorBuildSource
{
    public OfficeFloorBuildResult BuildGeneratedFloor(EscapeFloorDefinition floorDefinition, Transform runtimeParent)
    {
        if (runtimeParent == null)
        {
            return Batch2TestRoomBootstrap.BuildOfficeFloor(floorDefinition);
        }

        Scene targetScene = runtimeParent.gameObject.scene;

        if (!targetScene.IsValid())
        {
            return Batch2TestRoomBootstrap.BuildOfficeFloor(floorDefinition, runtimeParent);
        }

        return Batch2TestRoomBootstrap.BuildOfficeFloor(floorDefinition, targetScene, runtimeParent);
    }
}

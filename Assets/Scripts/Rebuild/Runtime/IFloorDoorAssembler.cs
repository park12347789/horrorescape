using UnityEngine;

public interface IFloorDoorAssembler
{
    FloorDoorAssemblyResult AssembleDoors(OfficeFloorBuildResult buildResult, Transform runtimeParent);
    void DestroyDoors(Transform runtimeParent);
}

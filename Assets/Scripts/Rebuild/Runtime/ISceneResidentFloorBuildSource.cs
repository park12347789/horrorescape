public interface ISceneResidentFloorBuildSource
{
    bool TryBuildSceneResidentFloor(EscapeFloorDefinition floorDefinition, out OfficeFloorBuildResult buildResult);
}

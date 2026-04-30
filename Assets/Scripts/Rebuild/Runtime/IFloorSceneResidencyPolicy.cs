using UnityEngine.SceneManagement;

public interface IFloorSceneResidencyPolicy
{
    bool RequiresSceneResidentAuthoring(EscapeFloorDefinition floorDefinition, Scene scene);
}

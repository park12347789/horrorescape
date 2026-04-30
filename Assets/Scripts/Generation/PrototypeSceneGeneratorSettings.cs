using UnityEngine;

[DisallowMultipleComponent]
public sealed class PrototypeSceneGeneratorSettings : MonoBehaviour
{
    [SerializeField] private PrototypeGeneratorMode generatorMode = PrototypeGeneratorMode.Wfc;
    [SerializeField] private bool useOverviewCamera;

    public PrototypeGeneratorMode GeneratorMode => generatorMode;
    public bool UseOverviewCamera => useOverviewCamera;

    public void Configure(PrototypeGeneratorMode mode, bool overviewCamera)
    {
        generatorMode = mode;
        useOverviewCamera = overviewCamera;
    }
}

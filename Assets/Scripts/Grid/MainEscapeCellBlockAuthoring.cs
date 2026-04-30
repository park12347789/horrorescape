using UnityEngine;

public enum MainEscapeCellBlockKind
{
    Ground,
    Wall,
    Door
}

[DisallowMultipleComponent]
public sealed class MainEscapeCellBlockAuthoring : MonoBehaviour
{
    [SerializeField] private MainEscapeCellBlockKind blockKind;

    public MainEscapeCellBlockKind BlockKind => blockKind;

    public void Configure(MainEscapeCellBlockKind configuredBlockKind)
    {
        blockKind = configuredBlockKind;
        ApplyLayerFromKind();
    }

    private void Reset()
    {
        ApplyLayerFromKind();
    }

    private void OnValidate()
    {
        ApplyLayerFromKind();
    }

    private void ApplyLayerFromKind()
    {
        gameObject.layer = blockKind switch
        {
            MainEscapeCellBlockKind.Ground => GameLayers.GroundIndex,
            MainEscapeCellBlockKind.Wall => GameLayers.WallIndex,
            MainEscapeCellBlockKind.Door => GameLayers.DoorIndex,
            _ => 0
        };
    }
}

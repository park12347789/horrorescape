using UnityEngine;

public interface IPlayerVisionQuery2D
{
    bool BypassEnabled { get; }
    LayerMask VisibilityBlockingLayers { get; }
    FogVisibilityState GetStateAtWorldPoint(Vector2 worldPoint);
    bool IsWorldPointVisible(Vector2 worldPoint);
    float SampleFlashlightVisibility(Vector2 worldPoint, float distancePadding, bool ignoreDoorLayer);
}

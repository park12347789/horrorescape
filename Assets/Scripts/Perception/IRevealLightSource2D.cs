using UnityEngine;

public interface IRevealLightSource2D
{
    bool IsRevealEnabled { get; }
    bool AffectsExposure { get; }
    bool TrySampleReveal(Vector2 worldPoint, int blockingMask, float raycastPadding, out float revealStrength);
    float SampleExposureMultiplier(Vector2 worldPoint, int blockingMask, float raycastPadding);
}

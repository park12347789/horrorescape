using UnityEngine;

public interface IPlayerThreatFeedbackSource
{
    bool IsConfirmedThreat { get; }
    bool IsActivelyPursuingPlayer { get; }
    bool ShouldForceThreatFeedbackVisible { get; }
    float ThreatIntensityNormalized { get; }
    Vector3 ThreatWorldPosition { get; }
    SpriteRenderer ThreatMarkerRenderer { get; }
}

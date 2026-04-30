using UnityEngine;

public static class EnemyVisionConfirmationUtility
{
    public static VisionSensor2D.VisionReading BuildConfirmedReading(
        VisionSensor2D.VisionReading rawReading,
        float deltaTime,
        ref float detectionMeter,
        float buildDuration,
        float decayDuration,
        float confirmedThreshold)
    {
        bool hasClearVisual = rawReading.InRange
            && rawReading.InAngle
            && !rawReading.IsOccluded
            && rawReading.DetectionStrength > 0f;

        if (hasClearVisual)
        {
            float buildFactor = Mathf.Lerp(0.35f, 1f, rawReading.DetectionStrength);
            float buildStep = deltaTime * (buildFactor / Mathf.Max(0.05f, buildDuration));
            detectionMeter = Mathf.MoveTowards(detectionMeter, 1f, buildStep);
        }
        else
        {
            float decayStep = deltaTime / Mathf.Max(0.05f, decayDuration);
            detectionMeter = Mathf.MoveTowards(detectionMeter, 0f, decayStep);
        }

        bool confirmedVisual = hasClearVisual && detectionMeter >= confirmedThreshold;
        float effectiveDetectionStrength = hasClearVisual
            ? Mathf.Max(rawReading.DetectionStrength, detectionMeter)
            : detectionMeter;

        return new VisionSensor2D.VisionReading(
            confirmedVisual,
            rawReading.InRange,
            rawReading.InAngle,
            rawReading.IsOccluded,
            rawReading.Distance,
            rawReading.Angle,
            rawReading.ExposureMultiplier,
            Mathf.Clamp01(effectiveDetectionStrength));
    }
}

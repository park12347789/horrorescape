/*
 * File Role:
 * Calculates 2D line-of-sight using range, angle, occlusion, and exposure multipliers.
 *
 * Runtime Use:
 * Returns a detailed VisionReading instead of a simple yes/no result so callers can react differently.
 *
 * Study Notes:
 * This is one of the main perception files to study alongside VisibilityTarget2D.
 */

using UnityEngine;

[DisallowMultipleComponent]
public sealed class VisionSensor2D : MonoBehaviour
{
    // VisionReading is intentionally verbose so callers can reuse one calculation for
    // debug UI, AI state changes, and balancing checks.
    public readonly struct VisionReading
    {
        public VisionReading(bool canSee, bool inRange, bool inAngle, bool isOccluded, float distance, float angle, float exposureMultiplier, float detectionStrength)
        {
            CanSee = canSee;
            InRange = inRange;
            InAngle = inAngle;
            IsOccluded = isOccluded;
            Distance = distance;
            Angle = angle;
            ExposureMultiplier = exposureMultiplier;
            DetectionStrength = detectionStrength;
        }

        public bool CanSee { get; }
        public bool InRange { get; }
        public bool InAngle { get; }
        public bool IsOccluded { get; }
        public float Distance { get; }
        public float Angle { get; }
        public float ExposureMultiplier { get; }
        public float DetectionStrength { get; }
    }

    [SerializeField] private Transform viewOrigin;
    [SerializeField, Min(0f)] private float viewDistance = 7f;
    [SerializeField, Range(0f, 360f)] private float viewAngle = 65f;
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private bool useExposureMultiplier = true;

    public Transform ViewOrigin => viewOrigin != null ? viewOrigin : transform;
    public float ViewDistance => viewDistance;
    public float ViewAngle => viewAngle;
    public LayerMask ObstructionMask => obstructionMask;
    public bool UseExposureMultiplier
    {
        get => useExposureMultiplier;
        set => useExposureMultiplier = value;
    }

    public void Configure(Transform origin, float distance, float angle, LayerMask mask)
    {
        // Generators and enemy bootstraps call this repeatedly to keep the sensor synced
        // with runtime-created pivots and layer masks.
        viewOrigin = origin;
        viewDistance = distance;
        viewAngle = angle;
        obstructionMask = mask;
    }

    public bool CanSee(VisibilityTarget2D target)
    {
        return GetReading(target).CanSee;
    }

    public float GetDetectionStrength(VisibilityTarget2D target)
    {
        return GetReading(target).DetectionStrength;
    }

    public VisionReading GetReading(VisibilityTarget2D target)
    {
        // The method is written as a step-by-step pipeline:
        // 1. Build a direction to the target.
        // 2. Check range.
        // 3. Check the view cone angle.
        // 4. Raycast for occlusion.
        // 5. Convert the result into a detection strength.
        if (target == null)
        {
            return default;
        }

        Transform originTransform = ViewOrigin;
        Vector2 origin = originTransform.position;
        Vector2 targetPoint = target.AimPoint;
        Vector2 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;

        if (distance <= 0.0001f)
        {
            // If the target is effectively on top of the origin we skip angle/raycast math
            // and return the strongest possible reading.
            float immediateExposureMultiplier = useExposureMultiplier ? target.GetExposureMultiplier() : 1f;
            return new VisionReading(true, true, true, false, 0f, 0f, immediateExposureMultiplier, 1f);
        }

        Vector2 direction = toTarget / distance;
        float halfAngle = viewAngle * 0.5f;
        float angle = Vector2.Angle(originTransform.up, direction);
        bool inRange = distance <= viewDistance;
        bool inAngle = angle <= halfAngle;
        bool isOccluded = false;

        if (inRange && inAngle)
        {
            // A single raycast is enough for the current prototype because targets expose
            // one important aim point rather than a full body volume.
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, obstructionMask);
            isOccluded = hit.collider != null;
        }

        float exposureMultiplier = 1f;
        float detectionStrength = 0f;

        if (inRange && inAngle && !isOccluded)
        {
            exposureMultiplier = useExposureMultiplier ? target.GetExposureMultiplier() : 1f;

            // Distance and angle both matter, but distance is weighted a bit higher so
            // targets near the sensor feel more reliably visible.
            float distanceFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, viewDistance));
            float angleFactor = halfAngle <= 0.001f ? 1f : 1f - Mathf.Clamp01(angle / halfAngle);
            float baseStrength = (distanceFactor * 0.6f) + (angleFactor * 0.4f);
            detectionStrength = Mathf.Clamp01(baseStrength * exposureMultiplier);
        }

        bool canSee = detectionStrength > 0f;
        return new VisionReading(canSee, inRange, inAngle, isOccluded, distance, angle, exposureMultiplier, detectionStrength);
    }
}


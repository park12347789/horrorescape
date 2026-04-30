/*
 * File Role:
 * Marks an object as something that can be seen by a VisionSensor2D.
 *
 * Runtime Use:
 * Provides the aim point and exposure multiplier used when calculating detection strength.
 *
 * Study Notes:
 * Read this together with VisionSensor2D because the two scripts form a matching pair.
 */

using UnityEngine;

[DisallowMultipleComponent]
public sealed class VisibilityTarget2D : MonoBehaviour
{
    [SerializeField] private Vector2 aimOffset = new(0f, 0.3f);
    [SerializeField, Min(0f)] private float baseExposureMultiplier = 1f;
    [Header("Debug")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform meterTransform;
    [SerializeField] private SpriteRenderer meterRenderer;

    public Vector2 AimPoint => (Vector2)transform.position + aimOffset;

    public float GetExposureMultiplier()
    {
        float multiplier = baseExposureMultiplier;
        Vector2 worldPosition = transform.position;

        foreach (ExposureZone2D zone in ExposureZone2D.ActiveZones)
        {
            if (zone != null && zone.Contains(worldPosition))
            {
                multiplier *= zone.ExposureMultiplier;
            }
        }

        multiplier *= AuthoredVisibilityLight2D.SampleStrongestExposureMultiplier(
            worldPosition,
            GameLayers.VisionBlockingMask.value,
            0.04f);

        return Mathf.Clamp(multiplier, 0f, 2.5f);
    }

    public void BindDebugRenderers(SpriteRenderer newBodyRenderer, Transform newMeterTransform, SpriteRenderer newMeterRenderer)
    {
        bodyRenderer = newBodyRenderer;
        meterTransform = newMeterTransform;
        meterRenderer = newMeterRenderer;
    }

    public void ApplyDebugState(VisionSensor2D.VisionReading reading)
    {
        if (bodyRenderer != null)
        {
            Color stateColor;

            if (reading.CanSee)
            {
                stateColor = Color.Lerp(new Color(0.32f, 0.37f, 0.43f, 1f), new Color(0.38f, 1f, 0.53f, 1f), reading.DetectionStrength);
            }
            else if (reading.IsOccluded)
            {
                stateColor = new Color(1f, 0.35f, 0.35f, 1f);
            }
            else
            {
                stateColor = new Color(0.55f, 0.58f, 0.62f, 1f);
            }

            if (reading.ExposureMultiplier > 1.01f)
            {
                stateColor = Color.Lerp(stateColor, new Color(1f, 0.84f, 0.3f, 1f), 0.35f);
            }
            else if (reading.ExposureMultiplier < 0.99f)
            {
                stateColor = Color.Lerp(stateColor, new Color(0.36f, 0.65f, 1f, 1f), 0.45f);
            }

            bodyRenderer.color = stateColor;
        }

        if (meterTransform != null)
        {
            float height = Mathf.Lerp(0.06f, 1.2f, reading.DetectionStrength);
            meterTransform.localScale = new Vector3(0.18f, height, 1f);
        }

        if (meterRenderer != null)
        {
            meterRenderer.color = reading.CanSee
                ? Color.Lerp(new Color(0.38f, 0.62f, 0.25f, 1f), new Color(0.38f, 1f, 0.53f, 1f), reading.DetectionStrength)
                : (reading.IsOccluded ? new Color(1f, 0.35f, 0.35f, 1f) : new Color(0.42f, 0.45f, 0.5f, 1f));
        }
    }
}


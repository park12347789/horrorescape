/*
 * File Role:
 * Defines an authored 2D light source that can both tint local fixture visuals
 * and participate in gameplay visibility queries.
 *
 * Runtime Use:
 * Register this on placed light prefabs so fog-of-war, enemy readability, and
 * player exposure all agree on which spaces are lit.
 *
 * Study Notes:
 * This is the bridge between URP Light2D presentation and the stealth
 * visibility rules used elsewhere in the project.
 */

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum AuthoredLightColorPreset
{
    Custom = 0,
    FluorescentCool = 1,
    WarmWhite = 2,
    HospitalMint = 3,
    EmergencyRed = 4,
    SodiumAmber = 5
}

[DisallowMultipleComponent]
public sealed class AuthoredVisibilityLight2D : MonoBehaviour, IRevealLightSource2D
{
    private static readonly List<AuthoredVisibilityLight2D> ActiveLightsInternal = new();

    [SerializeField] private Light2D lightSource;
    [SerializeField] private ExposureZone2D exposureZone;
    [SerializeField] private SpriteRenderer fixtureRenderer;
    [SerializeField] private SpriteRenderer[] glowRenderers = System.Array.Empty<SpriteRenderer>();
    [Header("Authoring Locks")]
    [SerializeField] private bool lockLightSourceToAuthoring = true;
    [SerializeField] private AuthoredLightColorPreset colorPreset = AuthoredLightColorPreset.Custom;
    [SerializeField] private Color lightColor = new(1f, 0.9f, 0.76f, 1f);
    [SerializeField] private Color fixtureColor = new(0.26f, 0.23f, 0.2f, 1f);
    [SerializeField, Min(0f)] private float lightIntensity = 0.9f;
    [SerializeField, Min(0f)] private float lightFalloff = 0.82f;
    [SerializeField, Min(0f)] private float volumeIntensity = 0.3f;
    [SerializeField, Min(0f)] private float pointLightOuterRadius = 3.8f;
    [SerializeField, Min(0f)] private float pointLightInnerRadius = 0.65f;
    [SerializeField, Range(0f, 360f)] private float pointLightOuterAngle = 180f;
    [SerializeField, Range(0f, 360f)] private float pointLightInnerAngle = 112f;
    [SerializeField] private bool shadowsEnabled;
    [SerializeField, Range(0, 9)] private int lightOrder = 5;
    [Header("Gameplay Reveal")]
    [SerializeField] private bool revealInFog = true;
    [SerializeField] private bool affectExposure = true;
    [SerializeField, Min(1f)] private float exposureMultiplier = 1.25f;
    [SerializeField] private Vector2 exposureZoneSize = new(2.8f, 2.8f);
    [SerializeField] private Color exposureZoneDebugColor = new(1f, 0.82f, 0.3f, 0.12f);
    [SerializeField, Min(0f)] private float revealRadiusPadding = 0.18f;
    [SerializeField, Range(0f, 24f)] private float revealAnglePadding = 8f;
    [SerializeField] private bool useOcclusion = true;

    public static IReadOnlyList<AuthoredVisibilityLight2D> ActiveLights => ActiveLightsInternal;

    public bool IsRevealEnabled => revealInFog && IsOperational();
    public bool AffectsExposure => affectExposure && IsOperational();
    public Light2D LightSource => lightSource;
    public AuthoredLightColorPreset ColorPreset => colorPreset;
    public Color ActiveLightColor
    {
        get
        {
            ResolvePresentationColors(out Color resolvedLightColor, out _);
            return resolvedLightColor;
        }
    }

    public Color ActiveFixtureColor
    {
        get
        {
            ResolvePresentationColors(out _, out Color resolvedFixtureColor);
            return resolvedFixtureColor;
        }
    }

    private void Awake()
    {
        RefreshCachedReferences();
        ApplyPresentation();
    }

    private void OnEnable()
    {
        RefreshCachedReferences();
        RegisterActiveLight();
        ApplyPresentation();
    }

    private void OnDisable()
    {
        ActiveLightsInternal.Remove(this);
    }

    private void Reset()
    {
        RefreshCachedReferences();
        ApplyPresentation();
    }

    private void OnValidate()
    {
        RefreshCachedReferences();
        ApplyPresentation();
    }

    public bool TrySampleReveal(Vector2 worldPoint, int blockingMask, float raycastPadding, out float revealStrength)
    {
        revealStrength = 0f;

        if (!IsRevealEnabled || lightSource == null)
        {
            return false;
        }

        if (UsesFreeformRevealShape())
        {
            return TrySampleFreeformReveal(worldPoint, blockingMask, raycastPadding, out revealStrength);
        }

        Vector2 origin = lightSource.transform.position;
        Vector2 toPoint = worldPoint - origin;
        float distance = toPoint.magnitude;
        float outerRadius = Mathf.Max(0.05f, GetEffectiveOuterRadius() + Mathf.Max(0f, revealRadiusPadding));

        if (distance > outerRadius)
        {
            return false;
        }

        if (distance > 0.0001f)
        {
            Vector2 direction = toPoint / distance;

            if (!IsPointWithinLightAngle(direction, distance))
            {
                return false;
            }

            if (useOcclusion && blockingMask != 0)
            {
                float castDistance = Mathf.Max(0f, distance - Mathf.Max(0f, raycastPadding));
                RaycastHit2D hit = Physics2D.Raycast(origin, direction, castDistance, blockingMask);

                if (hit.collider != null)
                {
                    return false;
                }
            }
        }

        float innerRadius = GetEffectiveInnerRadius();
        float distanceStrength = distance <= innerRadius
            ? 1f
            : 1f - Mathf.Clamp01((distance - innerRadius) / Mathf.Max(0.001f, outerRadius - innerRadius));
        distanceStrength = Mathf.SmoothStep(0f, 1f, distanceStrength);

        float angleStrength = 1f;

        if (distance > 0.0001f)
        {
            float outerAngle = GetEffectiveOuterAngle();

            if (outerAngle < 359.5f)
            {
                float halfInner = GetEffectiveInnerAngle() * 0.5f;
                float halfOuter = (outerAngle * 0.5f) + Mathf.Max(0f, revealAnglePadding);
                float angleToPoint = Vector2.Angle(lightSource.transform.up, toPoint / distance);
                angleStrength = angleToPoint <= halfInner
                    ? 1f
                    : 1f - Mathf.Clamp01((angleToPoint - halfInner) / Mathf.Max(0.001f, halfOuter - halfInner));
                angleStrength = Mathf.SmoothStep(0f, 1f, angleStrength);
            }
        }

        revealStrength = Mathf.Clamp01(distanceStrength * angleStrength * GetRevealIntensityFactor());
        return revealStrength > 0f;
    }

    public float SampleExposureMultiplier(Vector2 worldPoint, int blockingMask, float raycastPadding)
    {
        if (!AffectsExposure || !TrySampleReveal(worldPoint, blockingMask, raycastPadding, out float revealStrength))
        {
            return 1f;
        }

        return Mathf.Lerp(1f, Mathf.Max(1f, exposureMultiplier), revealStrength);
    }

    public static bool TrySampleStrongestReveal(Vector2 worldPoint, int blockingMask, float raycastPadding, out float revealStrength)
    {
        revealStrength = 0f;

        for (int index = 0; index < ActiveLightsInternal.Count; index++)
        {
            AuthoredVisibilityLight2D light = ActiveLightsInternal[index];

            if (light == null || !light.TrySampleReveal(worldPoint, blockingMask, raycastPadding, out float sampledRevealStrength))
            {
                continue;
            }

            if (sampledRevealStrength > revealStrength)
            {
                revealStrength = sampledRevealStrength;
            }
        }

        return revealStrength > 0f;
    }

    public static bool CouldAnyActiveLightReveal(Vector2 worldPoint)
    {
        for (int index = 0; index < ActiveLightsInternal.Count; index++)
        {
            AuthoredVisibilityLight2D light = ActiveLightsInternal[index];

            if (light == null || !light.CouldRevealAt(worldPoint))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public static float SampleStrongestExposureMultiplier(Vector2 worldPoint, int blockingMask, float raycastPadding)
    {
        float strongestMultiplier = 1f;

        for (int index = 0; index < ActiveLightsInternal.Count; index++)
        {
            AuthoredVisibilityLight2D light = ActiveLightsInternal[index];

            if (light == null)
            {
                continue;
            }

            float sampledMultiplier = light.SampleExposureMultiplier(worldPoint, blockingMask, raycastPadding);

            if (sampledMultiplier > strongestMultiplier)
            {
                strongestMultiplier = sampledMultiplier;
            }
        }

        return strongestMultiplier;
    }

    public void SetColorPreset(AuthoredLightColorPreset preset)
    {
        colorPreset = preset;
        ApplyPresentation();
    }

    public void SetCustomColors(Color newLightColor, Color newFixtureColor)
    {
        colorPreset = AuthoredLightColorPreset.Custom;
        lightColor = newLightColor;
        fixtureColor = newFixtureColor;
        ApplyPresentation();
    }

    private void RefreshCachedReferences()
    {
        lightSource ??= GetComponentInChildren<Light2D>(true);
        exposureZone ??= GetComponent<ExposureZone2D>();

        if (fixtureRenderer == null)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            for (int index = 0; index < renderers.Length; index++)
            {
                SpriteRenderer renderer = renderers[index];

                if (renderer != null && renderer.gameObject != gameObject)
                {
                    fixtureRenderer = renderer;
                    break;
                }
            }
        }
    }

    private void RegisterActiveLight()
    {
        if (!ActiveLightsInternal.Contains(this))
        {
            ActiveLightsInternal.Add(this);
        }
    }

    private void ApplyPresentation()
    {
        ResolvePresentationColors(out Color resolvedLightColor, out Color resolvedFixtureColor);
        Color appliedLightColor = resolvedLightColor;

        if (lightSource != null)
        {
            if (lockLightSourceToAuthoring)
            {
                lightSource.lightType = Light2D.LightType.Point;
                lightSource.color = resolvedLightColor;
                lightSource.intensity = lightIntensity;
                lightSource.falloffIntensity = lightFalloff;
                lightSource.pointLightOuterRadius = pointLightOuterRadius;
                lightSource.pointLightInnerRadius = Mathf.Min(pointLightInnerRadius, pointLightOuterRadius);
                lightSource.pointLightOuterAngle = pointLightOuterAngle;
                lightSource.pointLightInnerAngle = Mathf.Min(pointLightInnerAngle, pointLightOuterAngle);
                lightSource.volumetricEnabled = volumeIntensity > 0.001f;
                lightSource.volumeIntensity = volumeIntensity;
                lightSource.shadowsEnabled = shadowsEnabled;
                lightSource.shadowIntensity = shadowsEnabled ? 1f : 0f;
                lightSource.shadowVolumeIntensity = shadowsEnabled ? 1f : 0f;
                lightSource.volumetricShadowsEnabled = shadowsEnabled;
                lightSource.lightOrder = lightOrder;
            }

            appliedLightColor = lightSource.color;
        }

        if (fixtureRenderer != null)
        {
            Color tintedFixtureColor = resolvedFixtureColor;
            tintedFixtureColor.a = fixtureRenderer.color.a > 0.001f ? fixtureRenderer.color.a : resolvedFixtureColor.a;
            fixtureRenderer.color = tintedFixtureColor;
        }

        if (glowRenderers != null)
        {
            for (int index = 0; index < glowRenderers.Length; index++)
            {
                SpriteRenderer renderer = glowRenderers[index];

                if (renderer != null)
                {
                    Color glowColor = appliedLightColor;
                    glowColor.a = renderer.color.a > 0.001f ? renderer.color.a : appliedLightColor.a;
                    renderer.color = glowColor;
                }
            }
        }

        if (exposureZone != null)
        {
            float debugAlpha = exposureZoneDebugColor.a > 0.001f ? exposureZoneDebugColor.a : 0.12f;
            Color debugColor = new(appliedLightColor.r, appliedLightColor.g, appliedLightColor.b, debugAlpha);
            exposureZone.Configure(exposureZoneSize, exposureMultiplier, debugColor);
            exposureZone.enabled = false;
        }
    }

    private void ResolvePresentationColors(out Color resolvedLightColor, out Color resolvedFixtureColor)
    {
        switch (colorPreset)
        {
            case AuthoredLightColorPreset.FluorescentCool:
                resolvedLightColor = new Color(0.8f, 0.92f, 1f, 1f);
                resolvedFixtureColor = new Color(0.19f, 0.2f, 0.22f, 1f);
                return;
            case AuthoredLightColorPreset.WarmWhite:
                resolvedLightColor = new Color(1f, 0.86f, 0.62f, 1f);
                resolvedFixtureColor = new Color(0.28f, 0.24f, 0.2f, 1f);
                return;
            case AuthoredLightColorPreset.HospitalMint:
                resolvedLightColor = new Color(0.76f, 1f, 0.9f, 1f);
                resolvedFixtureColor = new Color(0.18f, 0.24f, 0.22f, 1f);
                return;
            case AuthoredLightColorPreset.EmergencyRed:
                resolvedLightColor = new Color(1f, 0.34f, 0.28f, 1f);
                resolvedFixtureColor = new Color(0.25f, 0.14f, 0.14f, 1f);
                return;
            case AuthoredLightColorPreset.SodiumAmber:
                resolvedLightColor = new Color(1f, 0.76f, 0.4f, 1f);
                resolvedFixtureColor = new Color(0.26f, 0.21f, 0.14f, 1f);
                return;
            default:
                resolvedLightColor = lightColor;
                resolvedFixtureColor = fixtureColor;
                return;
        }
    }

    private bool IsOperational()
    {
        return isActiveAndEnabled
            && lightSource != null
            && lightSource.isActiveAndEnabled
            && GetEffectiveLightIntensity() > 0.01f
            && GetEffectiveOuterRadius() > 0.05f;
    }

    private bool CouldRevealAt(Vector2 worldPoint)
    {
        if (!IsRevealEnabled || lightSource == null)
        {
            return false;
        }

        Vector2 origin = lightSource.transform.position;
        float broadphaseRadius = GetEffectiveOuterRadius();

        if (UsesFreeformRevealShape())
        {
            broadphaseRadius += GetEffectiveFreeformFalloff();
        }

        broadphaseRadius += Mathf.Max(0f, revealRadiusPadding);

        return (worldPoint - origin).sqrMagnitude <= broadphaseRadius * broadphaseRadius;
    }

    private bool TrySampleFreeformReveal(Vector2 worldPoint, int blockingMask, float raycastPadding, out float revealStrength)
    {
        revealStrength = 0f;

        Vector2 origin = lightSource.transform.position;
        Vector2 toPoint = worldPoint - origin;
        float distance = toPoint.magnitude;
        float outerFalloff = Mathf.Max(0.001f, GetEffectiveFreeformFalloff() + Mathf.Max(0f, revealRadiusPadding));
        float outerRadius = Mathf.Max(0.05f, GetEffectiveOuterRadius() + outerFalloff);

        if (distance > outerRadius)
        {
            return false;
        }

        if (distance > 0.0001f && useOcclusion && blockingMask != 0)
        {
            float castDistance = Mathf.Max(0f, distance - Mathf.Max(0f, raycastPadding));

            if (castDistance > 0f)
            {
                RaycastHit2D hit = Physics2D.Raycast(origin, toPoint / distance, castDistance, blockingMask);

                if (hit.collider != null)
                {
                    return false;
                }
            }
        }

        EvaluateFreeformShape(worldPoint, out bool isInsideShape, out float edgeDistance);

        if (isInsideShape)
        {
            revealStrength = 1f;
            revealStrength *= GetRevealIntensityFactor();
            return revealStrength > 0f;
        }

        if (edgeDistance > outerFalloff)
        {
            return false;
        }

        revealStrength = 1f - Mathf.Clamp01(edgeDistance / outerFalloff);
        revealStrength = Mathf.SmoothStep(0f, 1f, revealStrength);
        revealStrength *= GetRevealIntensityFactor();
        return revealStrength > 0f;
    }

    private void EvaluateFreeformShape(Vector2 worldPoint, out bool isInsideShape, out float edgeDistance)
    {
        isInsideShape = false;
        edgeDistance = float.PositiveInfinity;

        Vector3[] shapePath = lightSource.shapePath;

        if (shapePath == null || shapePath.Length < 3)
        {
            return;
        }

        Transform lightTransform = lightSource.transform;
        Vector2 previousPoint = lightTransform.TransformPoint(shapePath[shapePath.Length - 1]);

        for (int index = 0; index < shapePath.Length; index++)
        {
            Vector2 currentPoint = lightTransform.TransformPoint(shapePath[index]);

            bool crossesScanline = (currentPoint.y > worldPoint.y) != (previousPoint.y > worldPoint.y);

            if (crossesScanline)
            {
                float scanlineRatio = (worldPoint.y - currentPoint.y) / (previousPoint.y - currentPoint.y);
                float edgeX = currentPoint.x + ((previousPoint.x - currentPoint.x) * scanlineRatio);

                if (worldPoint.x < edgeX)
                {
                    isInsideShape = !isInsideShape;
                }
            }

            edgeDistance = Mathf.Min(edgeDistance, DistanceToSegment(worldPoint, previousPoint, currentPoint));
            previousPoint = currentPoint;
        }
    }

    private bool UsesFreeformRevealShape()
    {
        return lightSource != null
            && lightSource.lightType == Light2D.LightType.Freeform
            && lightSource.shapePath != null
            && lightSource.shapePath.Length >= 3;
    }

    private float GetEffectiveFreeformFalloff()
    {
        if (lightSource == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, lightSource.shapeLightFalloffSize) * GetLightTransformMaxScale();
    }

    private bool IsPointWithinLightAngle(Vector2 direction, float distance)
    {
        float outerAngle = GetEffectiveOuterAngle();

        if (outerAngle >= 359.5f || distance <= 0.0001f)
        {
            return true;
        }

        float signedAngle = Vector2.Angle(lightSource.transform.up, direction);
        return signedAngle <= (outerAngle * 0.5f) + Mathf.Max(0f, revealAnglePadding);
    }

    private float GetEffectiveLightIntensity()
    {
        if (lightSource != null && !lockLightSourceToAuthoring)
        {
            return Mathf.Max(0f, lightSource.intensity);
        }

        return Mathf.Max(0f, lightIntensity);
    }

    private float GetEffectiveOuterRadius()
    {
        if (UsesFreeformRevealShape())
        {
            return GetFreeformOuterRadius();
        }

        if (lightSource != null && !lockLightSourceToAuthoring)
        {
            return Mathf.Max(0f, lightSource.pointLightOuterRadius);
        }

        return Mathf.Max(0f, pointLightOuterRadius);
    }

    private float GetEffectiveInnerRadius()
    {
        if (lightSource != null && !lockLightSourceToAuthoring)
        {
            return Mathf.Clamp(lightSource.pointLightInnerRadius, 0f, GetEffectiveOuterRadius());
        }

        return Mathf.Clamp(pointLightInnerRadius, 0f, GetEffectiveOuterRadius());
    }

    private float GetEffectiveOuterAngle()
    {
        if (lightSource != null && !lockLightSourceToAuthoring)
        {
            return Mathf.Clamp(lightSource.pointLightOuterAngle, 0f, 360f);
        }

        return Mathf.Clamp(pointLightOuterAngle, 0f, 360f);
    }

    private float GetEffectiveInnerAngle()
    {
        if (lightSource != null && !lockLightSourceToAuthoring)
        {
            return Mathf.Clamp(lightSource.pointLightInnerAngle, 0f, GetEffectiveOuterAngle());
        }

        return Mathf.Clamp(pointLightInnerAngle, 0f, GetEffectiveOuterAngle());
    }

    private float GetRevealIntensityFactor()
    {
        float baseIntensity = Mathf.Max(0.001f, lightIntensity);
        return Mathf.Clamp01(GetEffectiveLightIntensity() / baseIntensity);
    }

    private float GetFreeformOuterRadius()
    {
        if (lightSource == null || lightSource.shapePath == null)
        {
            return 0f;
        }

        float maxDistance = 0f;
        Vector3[] shapePath = lightSource.shapePath;

        for (int index = 0; index < shapePath.Length; index++)
        {
            maxDistance = Mathf.Max(maxDistance, ((Vector2)shapePath[index]).magnitude);
        }

        return maxDistance * GetLightTransformMaxScale();
    }

    private float GetLightTransformMaxScale()
    {
        if (lightSource == null)
        {
            return 1f;
        }

        Vector3 lossyScale = lightSource.transform.lossyScale;
        return Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), 0.0001f);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;

        if (segmentLengthSquared <= 0.000001f)
        {
            return Vector2.Distance(point, start);
        }

        float projection = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSquared);
        Vector2 closestPoint = start + (segment * projection);
        return Vector2.Distance(point, closestPoint);
    }
}

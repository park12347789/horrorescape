using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

internal static class RuntimePointLight2DCache
{
    private const float MinimumLightRefreshInterval = 0.08f;
    private static readonly List<Light2D> CachedPointLights = new();
    private static float nextRefreshTime = float.NegativeInfinity;
    private static int cacheRevision;
    private static ulong lastCacheSignature;
    private static bool cacheInitialized;
    private static Scene cachedScene;

    public static int CachedCount => CachedPointLights.Count;
    public static int CacheRevision => cacheRevision;

    public static IReadOnlyList<Light2D> GetLights(float refreshInterval)
    {
        return GetLights(default, refreshInterval);
    }

    public static IReadOnlyList<Light2D> GetLights(Scene scene, float refreshInterval)
    {
        float now = Application.isPlaying ? Time.unscaledTime : 0f;

        if (!cacheInitialized || cachedScene != scene || now >= nextRefreshTime)
        {
            Refresh(scene, now, refreshInterval);
        }

        return CachedPointLights;
    }

    private static void Refresh(Scene scene, float now, float refreshInterval)
    {
        Light2D[] allLights = scene.IsValid()
            ? RSceneReferenceLookup.FindComponentsInScene<Light2D>(scene)
            : System.Array.Empty<Light2D>();
        CachedPointLights.Clear();
        cachedScene = scene;
        ulong cacheSignature = 17uL;
        int lightCount = 0;
        ulong lightIdXor = 0uL;
        ulong lightIdSum = 0uL;
        ulong lightIdMix = 0uL;

        for (int index = 0; index < allLights.Length; index++)
        {
            Light2D light = allLights[index];

            if (light == null
                || !light.isActiveAndEnabled
                || (scene.IsValid() && light.gameObject.scene != scene)
                || light.lightType != Light2D.LightType.Point
                || light.intensity <= 0.01f
                || light.pointLightOuterRadius <= 0.05f)
            {
                continue;
            }

            CachedPointLights.Add(light);
            lightCount++;

            unchecked
            {
                ulong lightSignature = ((ulong)(uint)light.GetInstanceID() * 397uL) ^ (uint)light.lightOrder;
                lightIdXor ^= lightSignature;
                lightIdSum += lightSignature;
                lightIdMix += (lightSignature * 31uL) ^ (lightSignature >> 16);
            }
        }

        unchecked
        {
            cacheSignature = (cacheSignature * 31uL) + (uint)lightCount;
            cacheSignature = (cacheSignature * 31uL) + lightIdXor;
            cacheSignature = (cacheSignature * 31uL) + lightIdSum;
            cacheSignature = (cacheSignature * 31uL) + lightIdMix;
        }

        if (!cacheInitialized || cacheSignature != lastCacheSignature)
        {
            cacheRevision++;
            lastCacheSignature = cacheSignature;
        }

        cacheInitialized = true;
        nextRefreshTime = now + Mathf.Max(MinimumLightRefreshInterval, refreshInterval);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FogReactiveSpriteTint : MonoBehaviour, IFogOfWarOverlayConsumer
{
    [SerializeField, FormerlySerializedAs("fogOfWarOverlay")] private MonoBehaviour fogVisibilityServiceSource;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.02f)] private float visibilityRefreshInterval = 0.08f;
    [SerializeField, Min(0f)] private float movementRefreshThreshold = 0.02f;
    [SerializeField] private float exploredBrightness = 0.12f;
    [SerializeField] private float hiddenBrightness = 0.01f;

    private Color sourceColor;
    private Color exploredColor;
    private Color hiddenColor;
    private FogVisibilityState lastAppliedState = (FogVisibilityState)(-1);
    private float nextVisibilityRefreshTime;
    private Vector3 lastSamplePosition;
    private bool hasSamplePosition;
    private IFogVisibilityService fogVisibilityService;

    public void Initialize(IFogVisibilityService boundFogVisibilityService)
    {
        SetFogVisibilityService(boundFogVisibilityService);
        spriteRenderer ??= GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            sourceColor = spriteRenderer.color;
            RebuildTintCache();
        }
    }

    public void Initialize(FlashlightFogOfWarOverlay overlay)
    {
        Initialize((IFogVisibilityService)overlay);
    }

    public void BindFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        SetFogVisibilityService(boundFogVisibilityService);
    }

    public void BindFogOfWarOverlay(FlashlightFogOfWarOverlay overlay)
    {
        BindFogVisibilityService(overlay);
    }

    private void Awake()
    {
        RefreshFogVisibilityServiceReference();
        spriteRenderer ??= GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            sourceColor = spriteRenderer.color;
            RebuildTintCache();
        }
    }

    public void SetSourceColor(Color color)
    {
        sourceColor = color;
        RebuildTintCache();

        if (spriteRenderer != null
            && fogVisibilityService != null
            && fogVisibilityService.GetStateAtWorldPoint(spriteRenderer.bounds.center) == FogVisibilityState.Visible)
        {
            spriteRenderer.color = sourceColor;
            lastAppliedState = FogVisibilityState.Visible;
        }
    }

    private void LateUpdate()
    {
        if (fogVisibilityService == null || spriteRenderer == null)
        {
            return;
        }

        if (!ShouldRefreshVisibilitySample())
        {
            return;
        }

        FogVisibilityState state = fogVisibilityService.GetStateAtWorldPoint(spriteRenderer.bounds.center);
        StampVisibilitySample();

        switch (state)
        {
            case FogVisibilityState.Visible:
                ApplyTintIfChanged(state, sourceColor);
                break;
            case FogVisibilityState.Explored:
                ApplyTintIfChanged(state, exploredColor);
                break;
            default:
                ApplyTintIfChanged(state, hiddenColor);
                break;
        }
    }

    private void ApplyTintIfChanged(FogVisibilityState state, Color targetColor)
    {
        if (lastAppliedState == state && spriteRenderer.color == targetColor)
        {
            return;
        }

        spriteRenderer.color = targetColor;
        lastAppliedState = state;
    }

    private void RebuildTintCache()
    {
        exploredColor = ToGrayscale(sourceColor, exploredBrightness);
        hiddenColor = ToGrayscale(sourceColor, hiddenBrightness);
        lastAppliedState = (FogVisibilityState)(-1);
        hasSamplePosition = false;
    }

    private static Color ToGrayscale(Color color, float brightness)
    {
        float gray = (color.r * 0.299f) + (color.g * 0.587f) + (color.b * 0.114f);
        float value = Mathf.Clamp01(gray * brightness);
        return new Color(value, value, value, color.a);
    }

    private void RefreshFogVisibilityServiceReference()
    {
        fogVisibilityService = fogVisibilityServiceSource as IFogVisibilityService;
    }

    private void SetFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        fogVisibilityService = boundFogVisibilityService;
        fogVisibilityServiceSource = boundFogVisibilityService as MonoBehaviour;
        hasSamplePosition = false;
    }

    private bool ShouldRefreshVisibilitySample()
    {
        if (!hasSamplePosition)
        {
            return true;
        }

        float threshold = Mathf.Max(0f, movementRefreshThreshold);

        if (threshold > 0f && (transform.position - lastSamplePosition).sqrMagnitude >= threshold * threshold)
        {
            return true;
        }

        return Time.unscaledTime >= nextVisibilityRefreshTime;
    }

    private void StampVisibilitySample()
    {
        hasSamplePosition = true;
        lastSamplePosition = transform.position;
        nextVisibilityRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, visibilityRefreshInterval);
    }
}

[DisallowMultipleComponent]
public sealed class FogReactiveEnemyVisibility : MonoBehaviour, IFogOfWarOverlayConsumer
{
    private const float VisibilityRefreshPhaseSpread = 0.65f;

    [SerializeField, FormerlySerializedAs("fogOfWarOverlay")] private MonoBehaviour fogVisibilityServiceSource;
    [SerializeField, Min(0.05f)] private float rendererRefreshInterval = 0.25f;
    [SerializeField, Min(0.02f)] private float visibilityRefreshInterval = 0.05f;
    [SerializeField, Min(0.02f)] private float threatVisibilityRefreshInterval = 0.033f;
    [SerializeField, Min(0f)] private float visibilityMovementRefreshThreshold = 0.05f;
    [SerializeField, Min(0.08f)] private float localLightRefreshInterval = 0.08f;
    [SerializeField, Min(0f)] private float sampleInset = 0.12f;
    [SerializeField, Min(0f)] private float localLightRadiusPadding = 0.2f;
    [SerializeField, Range(0f, 24f)] private float localLightAnglePadding = 10f;
    [SerializeField, Min(0)] private int localLightMaxOrder = 9;
    [SerializeField, Range(0f, 1f)] private float investigateSilhouetteAlpha = 0.38f;
    [SerializeField, Range(0f, 1f)] private float confirmedSilhouetteAlpha = 0.62f;
    [SerializeField] private SpriteRenderer[] controlledSpriteRenderers;
    [SerializeField] private MeshRenderer[] controlledMeshRenderers;

    private float nextRendererRefreshTime;
    private float nextVisibilityRefreshTime;
    private float nextThreatVisibilityRefreshTime;
    private Vector3 lastVisibilitySamplePosition;
    private float nextLocalLightRefreshTime;
    private readonly System.Collections.Generic.List<Light2D> localLights = new();
    private IPlayerThreatFeedbackSource threatFeedbackSource;
    private int lastLocalLightCacheRevision = -1;
    private ulong lastLocalLightSignature;
    private int lastLocalLightMaxOrder = int.MinValue;
    private bool rendererCacheDirty = true;
    private bool localLightCacheDirty = true;
    private bool hasVisibilitySample;
    private bool cachedVisibility = true;
    private readonly List<SpriteRenderer> controlledSpriteRendererCache = new();
    private readonly List<MeshRenderer> controlledMeshRendererCache = new();
    private readonly List<bool> controlledMeshRendererIsVisionVisualizerCache = new();
    private readonly List<SpriteRenderer> spriteRendererScratch = new();
    private readonly List<EnemyVisionVisualizer> visionVisualizerScratch = new();
    private readonly List<MonoBehaviour> behaviourScratch = new();
    private bool staggerNextVisibilityRefresh = true;
    private bool hasVisibilityRefreshPhase;
    private float visibilityRefreshPhase;
    private IFogVisibilityService fogVisibilityService;
    private bool appliedVisibilityDirty = true;
    private bool hasAppliedVisibilityState;
    private bool lastAppliedVisibility;
    private bool lastAppliedThreatMarkerVisible;
    private bool lastAppliedThreatSilhouetteVisible;
    private bool lastAppliedThreatVisionConeVisible;
    private float lastAppliedSilhouetteAlpha;
    private SpriteRenderer lastAppliedForcedThreatMarker;

    public void Initialize(IFogVisibilityService boundFogVisibilityService)
    {
        SetFogVisibilityService(boundFogVisibilityService);
        RefreshControlledRenderers(force: true);
        RefreshLocalLights(force: true);
        cachedVisibility = EvaluateVisibility();
        StampVisibilitySample();
        ApplyVisibility(cachedVisibility);
    }

    public void Initialize(FlashlightFogOfWarOverlay overlay)
    {
        Initialize((IFogVisibilityService)overlay);
    }

    public void BindFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        SetFogVisibilityService(boundFogVisibilityService);
    }

    public void BindFogOfWarOverlay(FlashlightFogOfWarOverlay overlay)
    {
        BindFogVisibilityService(overlay);
    }

    private void Awake()
    {
        RefreshFogVisibilityServiceReference();
        EnsureVisibilityRefreshPhase();
    }

    private void LateUpdate()
    {
        long performanceSample = MainEscapePerformanceTracker.BeginSample(MainEscapePerformanceSampleId.FogReactiveEnemyVisibility);

        try
        {
            RefreshControlledRenderers(force: false);
            RefreshLocalLights(force: false);

            if (ShouldRefreshVisibilitySample())
            {
                cachedVisibility = EvaluateVisibility();
                StampVisibilitySample();
            }

            ApplyVisibility(cachedVisibility);
        }
        finally
        {
            MainEscapePerformanceTracker.EndSample(MainEscapePerformanceSampleId.FogReactiveEnemyVisibility, performanceSample);
        }
    }

    private void OnTransformChildrenChanged()
    {
        rendererCacheDirty = true;
        localLightCacheDirty = true;
        appliedVisibilityDirty = true;
        hasVisibilitySample = false;
        staggerNextVisibilityRefresh = true;
    }

    private bool ShouldRefreshVisibilitySample()
    {
        if (!hasVisibilitySample || fogVisibilityService == null)
        {
            return true;
        }

        if (HasActiveThreatPresentation())
        {
            return Time.unscaledTime >= nextThreatVisibilityRefreshTime;
        }

        float threshold = Mathf.Max(0f, visibilityMovementRefreshThreshold);

        if (threshold > 0f && (transform.position - lastVisibilitySamplePosition).sqrMagnitude >= threshold * threshold)
        {
            return true;
        }

        return Time.unscaledTime >= nextVisibilityRefreshTime;
    }

    private void StampVisibilitySample()
    {
        hasVisibilitySample = true;
        lastVisibilitySamplePosition = transform.position;
        float regularInterval = Mathf.Max(0.02f, visibilityRefreshInterval);
        float threatInterval = Mathf.Max(0.02f, threatVisibilityRefreshInterval);
        float phaseOffset = staggerNextVisibilityRefresh
            ? EnsureVisibilityRefreshPhase() * VisibilityRefreshPhaseSpread
            : 0f;
        staggerNextVisibilityRefresh = false;
        nextVisibilityRefreshTime = Time.unscaledTime + regularInterval + (regularInterval * phaseOffset);
        nextThreatVisibilityRefreshTime = Time.unscaledTime + threatInterval + (threatInterval * phaseOffset);
    }

    private float EnsureVisibilityRefreshPhase()
    {
        if (hasVisibilityRefreshPhase)
        {
            return visibilityRefreshPhase;
        }

        unchecked
        {
            uint hash = (uint)GetInstanceID();
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            visibilityRefreshPhase = (hash & 0xffffu) / 65535f;
        }

        hasVisibilityRefreshPhase = true;
        return visibilityRefreshPhase;
    }

    private bool HasActiveThreatPresentation()
    {
        ResolveThreatFeedbackSource();
        return threatFeedbackSource != null
            && (threatFeedbackSource.IsConfirmedThreat
                || threatFeedbackSource.ShouldForceThreatFeedbackVisible
                || threatFeedbackSource.ThreatIntensityNormalized > 0.001f);
    }

    private void RefreshControlledRenderers(bool force)
    {
        if (!force
            && !rendererCacheDirty
            && Time.unscaledTime < nextRendererRefreshTime)
        {
            return;
        }

        ResolveThreatFeedbackSource();

        controlledSpriteRendererCache.Clear();

        if (controlledSpriteRenderers != null)
        {
            for (int index = 0; index < controlledSpriteRenderers.Length; index++)
            {
                AddControlledSpriteRenderer(controlledSpriteRenderers[index]);
            }
        }

        spriteRendererScratch.Clear();
        GetComponentsInChildren(true, spriteRendererScratch);

        for (int index = 0; index < spriteRendererScratch.Count; index++)
        {
            AddControlledSpriteRenderer(spriteRendererScratch[index]);
        }

        controlledMeshRendererCache.Clear();
        controlledMeshRendererIsVisionVisualizerCache.Clear();

        if (controlledMeshRenderers != null)
        {
            for (int index = 0; index < controlledMeshRenderers.Length; index++)
            {
                AddControlledMeshRenderer(controlledMeshRenderers[index], isVisionVisualizer: false);
            }
        }

        visionVisualizerScratch.Clear();
        GetComponentsInChildren(true, visionVisualizerScratch);

        for (int index = 0; index < visionVisualizerScratch.Count; index++)
        {
            MeshRenderer meshRenderer = visionVisualizerScratch[index] != null
                ? visionVisualizerScratch[index].GetComponent<MeshRenderer>()
                : null;
            AddControlledMeshRenderer(meshRenderer, isVisionVisualizer: meshRenderer != null);
        }

        nextRendererRefreshTime = Time.unscaledTime + rendererRefreshInterval;
        rendererCacheDirty = false;
        appliedVisibilityDirty = true;
    }

    private void AddControlledSpriteRenderer(SpriteRenderer renderer)
    {
        if (renderer == null || controlledSpriteRendererCache.Contains(renderer))
        {
            return;
        }

        controlledSpriteRendererCache.Add(renderer);
    }

    private void AddControlledMeshRenderer(MeshRenderer renderer, bool isVisionVisualizer)
    {
        if (renderer == null)
        {
            return;
        }

        int existingIndex = controlledMeshRendererCache.IndexOf(renderer);

        if (existingIndex >= 0)
        {
            if (isVisionVisualizer && existingIndex < controlledMeshRendererIsVisionVisualizerCache.Count)
            {
                controlledMeshRendererIsVisionVisualizerCache[existingIndex] = true;
            }

            return;
        }

        controlledMeshRendererCache.Add(renderer);
        controlledMeshRendererIsVisionVisualizerCache.Add(isVisionVisualizer);
    }

    private void ResolveThreatFeedbackSource()
    {
        if (threatFeedbackSource != null)
        {
            return;
        }

        behaviourScratch.Clear();
        GetComponents(behaviourScratch);

        for (int index = 0; index < behaviourScratch.Count; index++)
        {
            if (behaviourScratch[index] is IPlayerThreatFeedbackSource source)
            {
                threatFeedbackSource = source;
                return;
            }
        }
    }

    private void RefreshLocalLights(bool force)
    {
        float now = Time.unscaledTime;

        if (!force
            && !localLightCacheDirty
            && localLightMaxOrder == lastLocalLightMaxOrder
            && now < nextLocalLightRefreshTime)
        {
            return;
        }

        IReadOnlyList<Light2D> lights = RuntimePointLight2DCache.GetLights(gameObject.scene, localLightRefreshInterval);
        int cacheRevision = RuntimePointLight2DCache.CacheRevision;
        ulong localLightSignature = ComputeLocalLightSignature(lights);

        if (!force
            && !localLightCacheDirty
            && localLightMaxOrder == lastLocalLightMaxOrder
            && cacheRevision == lastLocalLightCacheRevision
            && localLightSignature == lastLocalLightSignature)
        {
            nextLocalLightRefreshTime = now + localLightRefreshInterval;
            return;
        }

        localLights.Clear();

        for (int index = 0; index < lights.Count; index++)
        {
            Light2D light = lights[index];

            if (ShouldIncludeLocalLight(light))
            {
                localLights.Add(light);
            }
        }

        lastLocalLightCacheRevision = cacheRevision;
        lastLocalLightSignature = localLightSignature;
        lastLocalLightMaxOrder = localLightMaxOrder;
        localLightCacheDirty = false;
        nextLocalLightRefreshTime = now + localLightRefreshInterval;
    }

    private bool EvaluateVisibility()
    {
        if (fogVisibilityService == null)
        {
            return true;
        }

        if (IsVisibleAtPoint(transform.position))
        {
            return true;
        }

        if (TryBuildSpriteBounds(out Bounds bounds))
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float inset = Mathf.Max(0f, sampleInset);

            if (IsVisibleAtPoint(center))
            {
                return true;
            }

            if (IsVisibleAtPoint(new Vector2(center.x, bounds.max.y - inset))
                || IsVisibleAtPoint(new Vector2(center.x, bounds.min.y + inset))
                || IsVisibleAtPoint(new Vector2(bounds.min.x + inset, center.y))
                || IsVisibleAtPoint(new Vector2(bounds.max.x - inset, center.y)))
            {
                return true;
            }

            if (extents.sqrMagnitude > 0.001f)
            {
                if (IsVisibleAtPoint(new Vector2(bounds.min.x + inset, bounds.max.y - inset))
                    || IsVisibleAtPoint(new Vector2(bounds.max.x - inset, bounds.max.y - inset))
                    || IsVisibleAtPoint(new Vector2(bounds.min.x + inset, bounds.min.y + inset))
                    || IsVisibleAtPoint(new Vector2(bounds.max.x - inset, bounds.min.y + inset)))
                {
                    return true;
                }
            }
        }

        if (controlledSpriteRendererCache.Count > 0)
        {
            for (int index = 0; index < controlledSpriteRendererCache.Count; index++)
            {
                SpriteRenderer spriteRenderer = controlledSpriteRendererCache[index];

                if (spriteRenderer != null && IsVisibleAtPoint(spriteRenderer.bounds.center))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryBuildSpriteBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (controlledSpriteRendererCache.Count > 0)
        {
            for (int index = 0; index < controlledSpriteRendererCache.Count; index++)
            {
                SpriteRenderer spriteRenderer = controlledSpriteRendererCache[index];

                if (spriteRenderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = spriteRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(spriteRenderer.bounds);
                }
            }
        }

        return hasBounds;
    }

    private bool IsVisibleAtPoint(Vector2 worldPoint)
    {
        return IsVisibleInFog(worldPoint) || IsVisibleInLocalLight(worldPoint);
    }

    private bool IsVisibleInFog(Vector2 worldPoint)
    {
        return fogVisibilityService != null && fogVisibilityService.IsWorldPointVisible(worldPoint);
    }

    private void RefreshFogVisibilityServiceReference()
    {
        fogVisibilityService = fogVisibilityServiceSource as IFogVisibilityService;
    }

    private void SetFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        fogVisibilityService = boundFogVisibilityService;
        fogVisibilityServiceSource = boundFogVisibilityService as MonoBehaviour;
        hasVisibilitySample = false;
        staggerNextVisibilityRefresh = true;
        appliedVisibilityDirty = true;
        EnsureVisibilityRefreshPhase();
    }

    private bool IsVisibleInLocalLight(Vector2 worldPoint)
    {
        if (localLights == null || localLights.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < localLights.Count; index++)
        {
            if (IsPointLitByLocalLight(localLights[index], worldPoint))
            {
                return true;
            }
        }

        return false;
    }

    private ulong ComputeLocalLightSignature(IReadOnlyList<Light2D> lights)
    {
        int includedCount = 0;
        ulong includedXor = 0uL;
        ulong includedSum = 0uL;
        ulong includedMix = 0uL;

        unchecked
        {
            for (int index = 0; index < lights.Count; index++)
            {
                Light2D light = lights[index];

                if (!ShouldIncludeLocalLight(light))
                {
                    continue;
                }

                ulong lightSignature = (uint)light.GetInstanceID();
                includedCount++;
                includedXor ^= lightSignature;
                includedSum += lightSignature;
                includedMix += (lightSignature * 31uL) ^ (lightSignature >> 16);
            }

            ulong hash = 17uL;
            hash = (hash * 31uL) + (uint)includedCount;
            hash = (hash * 31uL) + includedXor;
            hash = (hash * 31uL) + includedSum;
            hash = (hash * 31uL) + includedMix;
            return hash;
        }
    }

    private bool ShouldIncludeLocalLight(Light2D light)
    {
        return light != null
            && light.isActiveAndEnabled
            && light.lightType == Light2D.LightType.Point
            && light.lightOrder <= localLightMaxOrder
            && light.intensity > 0.01f
            && light.pointLightOuterRadius > 0.05f
            && !light.transform.IsChildOf(transform);
    }

    private bool IsPointLitByLocalLight(Light2D light, Vector2 worldPoint)
    {
        if (light == null || !light.isActiveAndEnabled)
        {
            return false;
        }

        Vector2 lightPosition = light.transform.position;
        Vector2 toPoint = worldPoint - lightPosition;
        float distance = toPoint.magnitude;
        float outerRadius = light.pointLightOuterRadius + Mathf.Max(0f, localLightRadiusPadding);

        if (distance > outerRadius)
        {
            return false;
        }

        if (distance <= light.pointLightInnerRadius + (localLightRadiusPadding * 0.5f))
        {
            return true;
        }

        float outerAngle = Mathf.Clamp(light.pointLightOuterAngle, 0f, 360f);

        if (outerAngle >= 359.5f || distance <= 0.0001f)
        {
            return true;
        }

        Vector2 forward = light.transform.up;
        float signedAngle = Vector2.Angle(forward, toPoint / distance);
        return signedAngle <= (outerAngle * 0.5f) + Mathf.Max(0f, localLightAnglePadding);
    }

    private void ApplyVisibility(bool isVisible)
    {
        EnemyThreatVisualFeedbackProfile threatProfile = EnemyThreatVisualFeedback.Evaluate(this, ref threatFeedbackSource);
        SpriteRenderer forcedThreatMarker = threatProfile.MarkerRenderer;
        bool showForcedThreatMarker = threatProfile.ShouldForceVisible;
        bool showThreatSilhouette = !isVisible && threatProfile.ShouldForceVisible;
        bool showThreatVisionCone = !isVisible && threatProfile.ShowVisionConeInFog;
        float silhouetteAlpha = threatProfile.IsConfirmedThreat
            ? Mathf.Max(confirmedSilhouetteAlpha, threatProfile.SilhouetteAlpha)
            : Mathf.Max(investigateSilhouetteAlpha, threatProfile.SilhouetteAlpha);
        silhouetteAlpha = Mathf.Clamp01(silhouetteAlpha);

        if (!appliedVisibilityDirty
            && hasAppliedVisibilityState
            && lastAppliedVisibility == isVisible
            && lastAppliedThreatMarkerVisible == showForcedThreatMarker
            && lastAppliedThreatSilhouetteVisible == showThreatSilhouette
            && lastAppliedThreatVisionConeVisible == showThreatVisionCone
            && lastAppliedForcedThreatMarker == forcedThreatMarker
            && Mathf.Approximately(lastAppliedSilhouetteAlpha, silhouetteAlpha))
        {
            return;
        }

        if (controlledSpriteRendererCache.Count > 0)
        {
            for (int index = 0; index < controlledSpriteRendererCache.Count; index++)
            {
                SpriteRenderer spriteRenderer = controlledSpriteRendererCache[index];

                if (spriteRenderer != null)
                {
                    bool rendererVisible = isVisible
                        || (showForcedThreatMarker && spriteRenderer == forcedThreatMarker)
                        || showThreatSilhouette;
                    if (spriteRenderer.enabled != rendererVisible)
                    {
                        spriteRenderer.enabled = rendererVisible;
                    }

                    if (rendererVisible)
                    {
                        Color color = spriteRenderer.color;
                        float targetAlpha = isVisible || spriteRenderer == forcedThreatMarker
                            ? 1f
                            : silhouetteAlpha;

                        if (!Mathf.Approximately(color.a, targetAlpha))
                        {
                            spriteRenderer.color = new Color(color.r, color.g, color.b, targetAlpha);
                        }
                    }
                }
            }
        }

        if (controlledMeshRendererCache.Count > 0)
        {
            for (int index = 0; index < controlledMeshRendererCache.Count; index++)
            {
                MeshRenderer meshRenderer = controlledMeshRendererCache[index];

                if (meshRenderer != null)
                {
                    if (index < controlledMeshRendererIsVisionVisualizerCache.Count
                        && controlledMeshRendererIsVisionVisualizerCache[index])
                    {
                        bool visualizerTargetEnabled = isVisible || showThreatVisionCone;

                        if (meshRenderer.enabled != visualizerTargetEnabled)
                        {
                            meshRenderer.enabled = visualizerTargetEnabled;
                        }

                        continue;
                    }

                    bool targetEnabled = isVisible || showThreatVisionCone;

                    if (meshRenderer.enabled != targetEnabled)
                    {
                        meshRenderer.enabled = targetEnabled;
                    }
                }
            }
        }

        hasAppliedVisibilityState = true;
        appliedVisibilityDirty = false;
        lastAppliedVisibility = isVisible;
        lastAppliedThreatMarkerVisible = showForcedThreatMarker;
        lastAppliedThreatSilhouetteVisible = showThreatSilhouette;
        lastAppliedThreatVisionConeVisible = showThreatVisionCone;
        lastAppliedSilhouetteAlpha = silhouetteAlpha;
        lastAppliedForcedThreatMarker = forcedThreatMarker;
    }
}

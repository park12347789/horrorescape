/*
 * File Role:
 * Draws the visible red field-of-view cone used by enemies.
 *
 * Runtime Use:
 * Rebuilds a mesh from raycasts so the cone is cut by walls and doors.
 *
 * Study Notes:
 * Useful when studying how gameplay vision rules are turned into readable debug visuals.
 */

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class EnemyVisionVisualizer : MonoBehaviour, IFogOfWarOverlayConsumer
{
    private const int MinimumSegmentCount = 20;
    private const int MinimumReadableSortingOrder = 140;
    private const float MinimumReadableFillAlpha = 1f;
    private const float MinimumReadableInvestigateAlpha = 1f;
    private const float MinimumReadableChaseAlpha = 1f;
    private const float MinimumReadableDensityMultiplier = 6.2f;
    private const float MinimumReadableCenterDensity = 0.96f;
    private const float MinimumReadableNearDensityMin = 0.9f;
    private const float MinimumReadableMidDensityMin = 0.92f;
    private const float MinimumReadableFarDensityMin = 0.74f;
    private const float MinimumReadableFarDensityMax = 1f;
    private const float MinimumReadableFlashlightInfluenceFloor = 1f;
    private const float MinimumReadableFlashlightDensityBoost = 6.2f;

    [SerializeField, Min(MinimumSegmentCount)] private int segmentCount = 24;
    [SerializeField] private Color fillColor = new(1f, 0.18f, 0.12f, 0.84f);
    [SerializeField] private Color investigateFillColor = new(1f, 0.84f, 0.22f, 0.92f);
    [SerializeField] private Color chaseFillColor = new(1f, 0.24f, 0.16f, 1f);
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 108;
    [SerializeField, Min(0f)] private float obstaclePadding = 0.02f;
    [SerializeField, Min(0.05f)] private float lightRefreshInterval = 0.35f;
    [SerializeField, Min(0f)] private float lightRadiusPadding = 0.22f;
    [SerializeField, Range(0f, 24f)] private float lightAnglePadding = 10f;
    [SerializeField, Range(0f, 1f)] private float unlitAlphaScale = 0.2f;
    [SerializeField, FormerlySerializedAs("fogOfWarOverlay")] private MonoBehaviour fogVisibilityServiceSource;
    [SerializeField] private bool revealOnlyWithPlayerFlashlight = true;
    [SerializeField, Min(0f)] private float densityMultiplier = 3.8f;
    [SerializeField, Range(0f, 1f)] private float centerDensity = 0.82f;
    [SerializeField, Range(0f, 1f)] private float nearDensityMin = 0.72f;
    [SerializeField, Range(0f, 1f)] private float nearDensityMax = 1f;
    [SerializeField, Range(0f, 1f)] private float midDensityMin = 0.72f;
    [SerializeField, Range(0f, 1f)] private float midDensityMax = 1f;
    [SerializeField, Range(0f, 1f)] private float farDensityMin = 0.5f;
    [SerializeField, Range(0f, 1f)] private float farDensityMax = 0.96f;
    [SerializeField, Min(0.1f)] private float threatPulseSpeed = 7.6f;
    [SerializeField, Min(1f)] private float threatDensityMultiplier = 1.38f;
    [SerializeField, Range(0f, 1f)] private float investigateBlendStrength = 0.46f;
    [SerializeField, Range(0f, 1f)] private float chaseBlendStrength = 0.74f;
    [SerializeField, Range(0f, 0.75f)] private float alphaPulseStrength = 0.3f;
    [SerializeField, Range(0f, 1f)] private float flashlightVisibleAlphaFloor = 1f;
    [SerializeField, Range(0f, 1f)] private float flashlightVisibleInfluenceFloor = 0.95f;
    [SerializeField, Min(1f)] private float flashlightVisibleDensityBoost = 4.4f;
    [SerializeField, Min(0.08f)] private float rebuildInterval = 0.08f;
    [SerializeField, Min(0f)] private float movementRefreshThreshold = 0.05f;
    [SerializeField, Range(0f, 45f)] private float rotationRefreshThreshold = 3f;
    [SerializeField] private WasdPlayerController playerController;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh visionMesh;
    private Material visionMaterial;
    private static Texture2D sharedConeTexture;
    private Transform castOrigin;
    private LayerMask obstructionMask;
    private static float nextLightCacheRefreshTime = float.NegativeInfinity;
    private static readonly Light2D[] CachedFlashlightOnly = new Light2D[1];
    private static IReadOnlyList<Light2D> cachedPointLights = System.Array.Empty<Light2D>();
    private static bool pointLightCacheInitialized;
    private Vector3[] vertexBuffer;
    private Vector2[] uvBuffer;
    private Color[] colorBuffer;
    private int[] triangleBuffer;
    private int bufferedSegmentCount = -1;
    private float configuredDistance;
    private float configuredAngle;
    private float nextRebuildTime;
    private Vector3 lastOriginPosition;
    private Quaternion lastOriginRotation = Quaternion.identity;
    private bool hasPreviousOriginSample;
    private bool configDirty = true;
    private IPlayerThreatFeedbackSource threatFeedbackSource;
    private Color presentedFillColor;
    private float presentedDensityMultiplier = 1f;
    private WasdPlayerController cachedPlayerController;
    private IFogVisibilityService fogVisibilityService;

    public void BindFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        SetFogVisibilityService(boundFogVisibilityService);
        configDirty = true;
    }

    public void BindFogOfWarOverlay(FlashlightFogOfWarOverlay overlay)
    {
        BindFogVisibilityService(overlay);
    }

    public void BindPlayerController(WasdPlayerController boundPlayerController)
    {
        SetPlayerController(boundPlayerController);
        configDirty = true;
    }

    public void Configure(
        float distance,
        float angle,
        string configuredSortingLayerName,
        int configuredSortingOrder,
        Transform configuredCastOrigin,
        LayerMask configuredObstructionMask)
    {
        EnsureComponents();
        bool configChanged = !Mathf.Approximately(configuredDistance, distance)
            || !Mathf.Approximately(configuredAngle, angle)
            || castOrigin != configuredCastOrigin
            || obstructionMask != configuredObstructionMask
            || !string.Equals(sortingLayerName, configuredSortingLayerName)
            || sortingOrder != configuredSortingOrder;
        configuredDistance = distance;
        configuredAngle = angle;
        sortingLayerName = string.IsNullOrWhiteSpace(configuredSortingLayerName) ? "Default" : configuredSortingLayerName;
        sortingOrder = configuredSortingOrder;
        castOrigin = configuredCastOrigin != null ? configuredCastOrigin : transform;
        obstructionMask = configuredObstructionMask;
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = Mathf.Max(sortingOrder, MinimumReadableSortingOrder);
        meshRenderer.enabled = visionMaterial != null;

        if (visionMaterial != null)
        {
            presentedFillColor = ResolveReadableFillColor(fillColor, MinimumReadableFillAlpha);
            presentedDensityMultiplier = ResolveReadableDensityMultiplier();
            visionMaterial.color = presentedFillColor;
        }

        configDirty |= configChanged;
        TryRefreshMesh(force: !Application.isPlaying || configChanged);
    }

    public void SetRevealOnlyWithPlayerFlashlight(bool shouldRevealOnlyWithPlayerFlashlight)
    {
        EnsureComponents();
        if (revealOnlyWithPlayerFlashlight == shouldRevealOnlyWithPlayerFlashlight)
        {
            return;
        }

        revealOnlyWithPlayerFlashlight = shouldRevealOnlyWithPlayerFlashlight;
        configDirty = true;
        TryRefreshMesh(force: true);
    }

    public void SetPresentationVisible(bool visible)
    {
        EnsureComponents();

        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible && visionMaterial != null;
        }

        if (!visible && visionMesh != null)
        {
            visionMesh.Clear();
        }

        hasPreviousOriginSample = false;
        nextRebuildTime = 0f;
        configDirty = true;
    }

    private void Awake()
    {
        RefreshFogVisibilityServiceReference();
        EnsureComponents();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        TryRefreshMesh(force: false);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            Destroy(visionMesh);
            Destroy(visionMaterial);
            return;
        }

        DestroyImmediate(visionMesh);
        DestroyImmediate(visionMaterial);
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (visionMesh == null)
        {
            visionMesh = new Mesh
            {
                name = "EnemyVisionVisualizerMesh"
            };
            visionMesh.MarkDynamic();
        }

        if (meshFilter.sharedMesh != visionMesh)
        {
            meshFilter.sharedMesh = visionMesh;
        }

        EnsureConeTexture();

        if (visionMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            shader ??= Shader.Find("Universal Render Pipeline/Unlit");
            shader ??= Shader.Find("Sprites/Default");
            visionMaterial = shader != null ? new Material(shader) : null;

            if (visionMaterial != null)
            {
                visionMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        ApplyConeTexture();

        if (visionMaterial != null && meshRenderer.sharedMaterial != visionMaterial)
        {
            meshRenderer.sharedMaterial = visionMaterial;
        }

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = Mathf.Max(sortingOrder, MinimumReadableSortingOrder);
        meshRenderer.enabled = visionMaterial != null;
        presentedFillColor = ResolveReadableFillColor(fillColor, MinimumReadableFillAlpha);
        presentedDensityMultiplier = ResolveReadableDensityMultiplier();
    }

    private void RebuildMesh(float distance, float angle)
    {
        if (visionMesh == null)
        {
            return;
        }

        RefreshPointLightCache();
        RefreshThreatPresentation();

        int safeSegments = Mathf.Max(MinimumSegmentCount, segmentCount);
        EnsureBufferCapacity(safeSegments);
        int verticesPerRing = safeSegments + 1;
        float resolvedCenterDensity = Mathf.Max(centerDensity, MinimumReadableCenterDensity);
        float resolvedNearDensityMin = Mathf.Max(nearDensityMin, MinimumReadableNearDensityMin);
        float resolvedNearDensityMax = Mathf.Max(nearDensityMax, 1f);
        float resolvedMidDensityMin = Mathf.Max(midDensityMin, MinimumReadableMidDensityMin);
        float resolvedMidDensityMax = Mathf.Max(midDensityMax, 1f);
        float resolvedFarDensityMin = Mathf.Max(farDensityMin, MinimumReadableFarDensityMin);
        float resolvedFarDensityMax = Mathf.Max(farDensityMax, MinimumReadableFarDensityMax);

        vertexBuffer[0] = Vector3.zero;
        uvBuffer[0] = new Vector2(0.5f, 0.02f);
        colorBuffer[0] = BuildVertexColor(presentedFillColor.a * resolvedCenterDensity * presentedDensityMultiplier);
        float halfAngle = angle * 0.5f;
        Vector2 worldOrigin = castOrigin != null ? castOrigin.position : transform.position;
        Quaternion worldRotation = castOrigin != null ? castOrigin.rotation : transform.rotation;

        for (int index = 0; index <= safeSegments; index++)
        {
            float t = index / (float)safeSegments;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            Vector2 localDirection = new(Mathf.Sin(currentAngle), Mathf.Cos(currentAngle));
            Vector2 worldDirection = worldRotation * localDirection;
            float visibleDistance = distance;

            if (obstructionMask.value != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(worldOrigin, worldDirection, distance, obstructionMask);

                if (hit.collider != null)
                {
                    visibleDistance = Mathf.Max(0f, hit.distance - obstaclePadding);
                }
            }

            float sideWeight = Mathf.Clamp01(Mathf.Sin((index / (float)safeSegments) * Mathf.PI));
            sideWeight = Mathf.Pow(sideWeight, 0.68f);
            int nearIndex = 1 + index;
            int midIndex = 1 + verticesPerRing + index;
            int farIndex = 1 + (verticesPerRing * 2) + index;
            Vector2 nearWorldPoint = worldOrigin + (worldDirection * visibleDistance * 0.18f);
            Vector2 midWorldPoint = worldOrigin + (worldDirection * visibleDistance * 0.62f);
            Vector2 farWorldPoint = worldOrigin + (worldDirection * visibleDistance);
            float nearLightInfluence = SamplePointLightInfluence(nearWorldPoint);
            float midLightInfluence = SamplePointLightInfluence(midWorldPoint);
            float farLightInfluence = SamplePointLightInfluence(farWorldPoint);

            vertexBuffer[nearIndex] = new Vector3(localDirection.x * visibleDistance * 0.18f, localDirection.y * visibleDistance * 0.18f, 0f);
            vertexBuffer[midIndex] = new Vector3(localDirection.x * visibleDistance * 0.62f, localDirection.y * visibleDistance * 0.62f, 0f);
            vertexBuffer[farIndex] = new Vector3(localDirection.x * visibleDistance, localDirection.y * visibleDistance, 0f);
            uvBuffer[nearIndex] = new Vector2(t, 0.2f);
            uvBuffer[midIndex] = new Vector2(t, 0.62f);
            uvBuffer[farIndex] = new Vector2(t, 1f);

            colorBuffer[nearIndex] = BuildVertexColor(presentedFillColor.a * Mathf.Lerp(resolvedNearDensityMin, resolvedNearDensityMax, sideWeight) * nearLightInfluence * presentedDensityMultiplier);
            colorBuffer[midIndex] = BuildVertexColor(presentedFillColor.a * Mathf.Lerp(resolvedMidDensityMin, resolvedMidDensityMax, sideWeight) * midLightInfluence * presentedDensityMultiplier);
            colorBuffer[farIndex] = BuildVertexColor(presentedFillColor.a * Mathf.Lerp(resolvedFarDensityMin, resolvedFarDensityMax, sideWeight) * farLightInfluence * presentedDensityMultiplier);
        }

        visionMesh.Clear();
        visionMesh.vertices = vertexBuffer;
        visionMesh.uv = uvBuffer;
        visionMesh.colors = colorBuffer;
        visionMesh.triangles = triangleBuffer;
        visionMesh.RecalculateBounds();
    }

    private void RefreshThreatPresentation()
    {
        if (visionMaterial == null)
        {
            return;
        }

        EnemyThreatVisualFeedbackProfile threatProfile = EnemyThreatVisualFeedback.Evaluate(this, ref threatFeedbackSource, threatPulseSpeed);
        Color baseFillColor = ResolveReadableFillColor(fillColor, MinimumReadableFillAlpha);
        Color investigateColor = ResolveReadableFillColor(investigateFillColor, MinimumReadableInvestigateAlpha);
        Color chaseColor = ResolveReadableFillColor(chaseFillColor, MinimumReadableChaseAlpha);
        Color targetColor = baseFillColor;
        float targetDensityMultiplier = ResolveReadableDensityMultiplier();

        if (threatProfile.ShouldHighlight)
        {
            float blendStrength = threatProfile.IsConfirmedThreat ? chaseBlendStrength : investigateBlendStrength;
            Color accentColor = threatProfile.IsConfirmedThreat ? chaseColor : investigateColor;
            float pulsedIntensity = Mathf.Lerp(1f - alphaPulseStrength, 1f + alphaPulseStrength, threatProfile.Pulse);
            targetColor = Color.Lerp(baseFillColor, accentColor, Mathf.Clamp01(blendStrength * threatProfile.ReadableIntensity));
            targetColor.a = Mathf.Clamp01(targetColor.a * pulsedIntensity);
            targetDensityMultiplier = Mathf.Lerp(
                ResolveReadableDensityMultiplier(),
                Mathf.Max(threatDensityMultiplier, MinimumReadableDensityMultiplier * 1.24f),
                threatProfile.ReadableIntensity);
        }

        if (revealOnlyWithPlayerFlashlight)
        {
            targetColor.a = Mathf.Max(targetColor.a, Mathf.Max(flashlightVisibleAlphaFloor, MinimumReadableChaseAlpha));
            targetDensityMultiplier = Mathf.Max(
                targetDensityMultiplier,
                ResolveReadableDensityMultiplier() * Mathf.Max(flashlightVisibleDensityBoost, MinimumReadableFlashlightDensityBoost));
        }

        presentedFillColor = targetColor;
        presentedDensityMultiplier = targetDensityMultiplier;
        visionMaterial.color = presentedFillColor;
    }

    private static void AddQuad(int[] triangles, ref int cursor, int innerA, int innerB, int outerA, int outerB)
    {
        AddTriangle(triangles, ref cursor, innerA, outerA, innerB);
        AddTriangle(triangles, ref cursor, innerB, outerA, outerB);
    }

    private static void AddTriangle(int[] triangles, ref int cursor, int a, int b, int c)
    {
        triangles[cursor++] = a;
        triangles[cursor++] = b;
        triangles[cursor++] = c;
    }

    private static Color BuildVertexColor(float alpha)
    {
        return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
    }

    private void TryRefreshMesh(bool force)
    {
        if (visionMesh == null)
        {
            return;
        }

        if (!force && meshRenderer != null && !meshRenderer.isVisible)
        {
            hasPreviousOriginSample = false;
            return;
        }

        Transform origin = castOrigin != null ? castOrigin : transform;
        Vector3 currentOriginPosition = origin.position;
        Quaternion currentOriginRotation = origin.rotation;
        float movementThresholdSquared = movementRefreshThreshold * movementRefreshThreshold;
        bool moved = !hasPreviousOriginSample
            || (lastOriginPosition - currentOriginPosition).sqrMagnitude >= movementThresholdSquared;
        bool rotated = !hasPreviousOriginSample
            || Quaternion.Angle(lastOriginRotation, currentOriginRotation) >= rotationRefreshThreshold;

        if (!force
            && !configDirty
            && !moved
            && !rotated
            && Time.unscaledTime < nextRebuildTime)
        {
            return;
        }

        long performanceSample = MainEscapePerformanceTracker.BeginSample(MainEscapePerformanceSampleId.EnemyVisionMesh);

        try
        {
            RebuildMesh(configuredDistance, configuredAngle);
        }
        finally
        {
            MainEscapePerformanceTracker.EndSample(MainEscapePerformanceSampleId.EnemyVisionMesh, performanceSample);
        }

        lastOriginPosition = currentOriginPosition;
        lastOriginRotation = currentOriginRotation;
        hasPreviousOriginSample = true;
        configDirty = false;
        nextRebuildTime = Time.unscaledTime + Mathf.Max(0f, rebuildInterval);
    }

    private void EnsureBufferCapacity(int safeSegments)
    {
        int verticesPerRing = safeSegments + 1;
        int requiredVertexCount = 1 + (verticesPerRing * 3);

        if (vertexBuffer == null || vertexBuffer.Length != requiredVertexCount)
        {
            vertexBuffer = new Vector3[requiredVertexCount];
        }

        if (colorBuffer == null || colorBuffer.Length != requiredVertexCount)
        {
            colorBuffer = new Color[requiredVertexCount];
        }

        if (uvBuffer == null || uvBuffer.Length != requiredVertexCount)
        {
            uvBuffer = new Vector2[requiredVertexCount];
        }

        if (triangleBuffer != null && bufferedSegmentCount == safeSegments)
        {
            return;
        }

        triangleBuffer = new int[safeSegments * 15];
        int triangleCursor = 0;

        for (int triangleIndex = 0; triangleIndex < safeSegments; triangleIndex++)
        {
            int nearA = 1 + triangleIndex;
            int nearB = nearA + 1;
            int midA = 1 + verticesPerRing + triangleIndex;
            int midB = midA + 1;
            int farA = 1 + (verticesPerRing * 2) + triangleIndex;
            int farB = farA + 1;

            AddTriangle(triangleBuffer, ref triangleCursor, 0, nearA, nearB);
            AddQuad(triangleBuffer, ref triangleCursor, nearA, nearB, midA, midB);
            AddQuad(triangleBuffer, ref triangleCursor, midA, midB, farA, farB);
        }

        bufferedSegmentCount = safeSegments;
    }

    private float SamplePointLightInfluence(Vector2 worldPoint)
    {
        bool visibleInPlayerSight = !revealOnlyWithPlayerFlashlight || IsVisibleInBoundFog(worldPoint);

        if (!visibleInPlayerSight)
        {
            return 0f;
        }

        if (cachedPointLights == null || cachedPointLights.Count == 0)
        {
            return revealOnlyWithPlayerFlashlight
                ? 1f
                : Mathf.Clamp01(unlitAlphaScale);
        }

        float strongestInfluence = revealOnlyWithPlayerFlashlight ? 0f : Mathf.Clamp01(unlitAlphaScale);

        for (int index = 0; index < cachedPointLights.Count; index++)
        {
            float influence = CalculatePointLightInfluence(cachedPointLights[index], worldPoint);

            if (influence > strongestInfluence)
            {
                strongestInfluence = influence;
            }
        }

        if (revealOnlyWithPlayerFlashlight)
        {
            return Mathf.Max(flashlightVisibleInfluenceFloor, MinimumReadableFlashlightInfluenceFloor, strongestInfluence);
        }

        return strongestInfluence;
    }

    private float CalculatePointLightInfluence(Light2D light, Vector2 worldPoint)
    {
        if (light == null || !light.isActiveAndEnabled)
        {
            return 0f;
        }

        Vector2 lightPosition = light.transform.position;
        Vector2 toPoint = worldPoint - lightPosition;
        float distance = toPoint.magnitude;
        float outerRadius = light.pointLightOuterRadius + Mathf.Max(0f, lightRadiusPadding);

        if (distance > outerRadius)
        {
            return 0f;
        }

        float innerRadius = Mathf.Max(0f, light.pointLightInnerRadius);
        float distanceInfluence = distance <= innerRadius
            ? 1f
            : 1f - Mathf.Clamp01((distance - innerRadius) / Mathf.Max(0.001f, outerRadius - innerRadius));
        distanceInfluence = Mathf.SmoothStep(0f, 1f, distanceInfluence);

        float outerAngle = Mathf.Clamp(light.pointLightOuterAngle, 0f, 360f);

        if (outerAngle >= 359.5f || distance <= 0.0001f)
        {
            return distanceInfluence;
        }

        float innerAngle = Mathf.Clamp(light.pointLightInnerAngle, 0f, outerAngle);
        float halfOuter = (outerAngle * 0.5f) + Mathf.Max(0f, lightAnglePadding);
        float halfInner = innerAngle * 0.5f;
        float angleToPoint = Vector2.Angle(light.transform.up, toPoint / distance);

        if (angleToPoint > halfOuter)
        {
            return 0f;
        }

        float angleInfluence = angleToPoint <= halfInner
            ? 1f
            : 1f - Mathf.Clamp01((angleToPoint - halfInner) / Mathf.Max(0.001f, halfOuter - halfInner));
        angleInfluence = Mathf.SmoothStep(0f, 1f, angleInfluence);
        return distanceInfluence * angleInfluence;
    }

    private bool IsVisibleInBoundFog(Vector2 worldPoint)
    {
        if (fogVisibilityService == null)
        {
            return true;
        }

        return fogVisibilityService.IsWorldPointVisible(worldPoint);
    }

    private void EnsureConeTexture()
    {
        if (sharedConeTexture != null)
        {
            return;
        }

        const int width = 128;
        const int height = 128;
        sharedConeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "EnemyVisionConeTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float tipFade = Mathf.SmoothStep(0f, 0.12f, v);

            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float centeredU = Mathf.Abs((u - 0.5f) * 2f);
                float edgeBand = Mathf.SmoothStep(0.48f, 0.96f, centeredU);
                float centerLine = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.5f) * 11f);
                centerLine = Mathf.SmoothStep(0f, 1f, centerLine);
                float stripe = Mathf.Repeat((u * 4.2f) + (v * 6.5f), 1f);
                float stripeMask = stripe > 0.28f && stripe < 0.55f ? 1f : 0f;
                stripeMask *= Mathf.SmoothStep(0.08f, 0.28f, v);
                float alpha = Mathf.Clamp01((1.18f + (edgeBand * 1.68f) + (centerLine * 1.16f) + (stripeMask * 0.74f)) * tipFade);
                pixels[(y * width) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        sharedConeTexture.SetPixels(pixels);
        sharedConeTexture.Apply(false, true);
    }

    private void ApplyConeTexture()
    {
        if (visionMaterial == null || sharedConeTexture == null)
        {
            return;
        }

        visionMaterial.mainTexture = sharedConeTexture;

        if (visionMaterial.HasProperty("_MainTex"))
        {
            visionMaterial.SetTexture("_MainTex", sharedConeTexture);
        }

        if (visionMaterial.HasProperty("_BaseMap"))
        {
            visionMaterial.SetTexture("_BaseMap", sharedConeTexture);
        }
    }

    private void RefreshPointLightCache()
    {
        float now = Application.isPlaying ? Time.unscaledTime : 0f;

        if (pointLightCacheInitialized && now < nextLightCacheRefreshTime)
        {
            return;
        }

        if (revealOnlyWithPlayerFlashlight)
        {
            cachedPlayerController = ResolvePlayerController();
            Light2D flashlight = cachedPlayerController != null ? cachedPlayerController.FlashlightLight : null;
            if (flashlight != null && flashlight.isActiveAndEnabled)
            {
                CachedFlashlightOnly[0] = flashlight;
                cachedPointLights = CachedFlashlightOnly;
            }
            else
            {
                CachedFlashlightOnly[0] = null;
                cachedPointLights = System.Array.Empty<Light2D>();
            }

            pointLightCacheInitialized = true;
            nextLightCacheRefreshTime = now + Mathf.Max(0.05f, lightRefreshInterval);
            return;
        }

        cachedPointLights = RuntimePointLight2DCache.GetLights(lightRefreshInterval);
        pointLightCacheInitialized = true;
        nextLightCacheRefreshTime = now + Mathf.Max(0.05f, lightRefreshInterval);
    }

    private static Color ResolveReadableFillColor(Color color, float minimumAlpha)
    {
        color.a = Mathf.Max(color.a, minimumAlpha);
        return color;
    }

    private float ResolveReadableDensityMultiplier()
    {
        return Mathf.Max(densityMultiplier, MinimumReadableDensityMultiplier);
    }

    private WasdPlayerController ResolvePlayerController()
    {
        if (playerController != null
            && playerController.gameObject.scene == gameObject.scene)
        {
            return playerController;
        }

        SetPlayerController(RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>(
            gameObject.scene,
            this,
            nameof(EnemyVisionVisualizer),
            nameof(playerController)));

        return playerController;
    }

    private void SetPlayerController(WasdPlayerController boundPlayerController)
    {
        playerController = boundPlayerController != null
            && boundPlayerController.gameObject.scene == gameObject.scene
            ? boundPlayerController
            : null;
    }

    private void RefreshFogVisibilityServiceReference()
    {
        fogVisibilityService = fogVisibilityServiceSource as IFogVisibilityService;
    }

    private void SetFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        fogVisibilityService = boundFogVisibilityService;
        fogVisibilityServiceSource = boundFogVisibilityService as MonoBehaviour;
    }
}


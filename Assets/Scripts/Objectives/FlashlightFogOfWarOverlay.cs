using UnityEngine;

public enum FogVisibilityState
{
    Unexplored,
    Explored,
    Visible
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(50)]
public sealed class FlashlightFogOfWarOverlay : MonoBehaviour, IFogVisibilityService, IFogBypassDebugApplier
{
    private const float HardMinimumComfortRevealRadius = 0.9f;
    private const float MinimumMovingRefreshInterval = 0.05f;
    // Keep some coarse lower bounds for performance, but still allow the scene-tuned
    // quality values to meaningfully round out flashlight silhouettes.
    private const int MinimumMovingSampleStride = 2;
    private const int MinimumIdleSampleStride = 2;
    private const int MinimumMovingInterlacedGroups = 2;
    private const float RapidTurnQualityAngleThreshold = 16f;
    private const int RapidTurnSampleStride = 1;
    private const string OverlayMaterialResourcePath = "MainEscape/MainEscapeFogOverlaySoft";
    private static Material sharedOverlayMaterial;

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private GridMapService gridMapService;
    [SerializeField] private SpriteRenderer overlayRenderer;
    [SerializeField] private Vector2 worldCenter = Vector2.zero;
    [SerializeField] private Vector2 worldSize = new(23.5f, 17.5f);
    [SerializeField, Min(2)] private int pixelsPerUnit = 4;
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float unexploredAlpha = 0.88f;
    [SerializeField, Range(1, 6)] private int darknessFilterLayers = 2;
    [SerializeField, Min(0f)] private float sightOriginForwardOffset = 0.08f;
    [SerializeField, Min(0f)] private float closeRevealRadius = 0.24f;
    [SerializeField, Min(0f)] private float playerComfortRevealRadius = 0.9f;
    [SerializeField, Min(0f)] private float minimumRuntimeComfortRevealRadius = 0.9f;
    [SerializeField, Min(0f)] private float flashlightOffComfortRevealRadius = 0.3f;
    [SerializeField, Min(0f)] private float directComfortRevealRadius = 0.7f;
    [SerializeField, Min(0f)] private float raycastPadding = 0.04f;
    [SerializeField, Range(0f, 1f)] private float edgeSoftness = 0.12f;
    [SerializeField, Range(0f, 30f)] private float flashlightRevealAnglePadding = 10f;
    [SerializeField, Range(0.5f, 4f)] private float idleQualityBoost = 1.2f;
    [SerializeField, Range(0.5f, 2f)] private float idleEdgeSoftnessMultiplier = 1.1f;
    [SerializeField, Range(0.5f, 2f)] private float idleComfortBlendStrength = 1.05f;
    [SerializeField, Min(0f)] private float roiPadding = 1.25f;
    [SerializeField] private LayerMask visibilityBlockingLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float movingRefreshInterval = 0.05f;
    [SerializeField, Min(0f)] private float idleRefreshInterval = 0.12f;
    [SerializeField, Min(0f)] private float movementRefreshThreshold = 0.08f;
    [SerializeField, Range(0f, 45f)] private float rotationRefreshThreshold = 3.5f;
    [SerializeField, Min(1)] private int interlacedUpdateGroups = 2;
    [SerializeField, Min(1)] private int movingSampleStride = 3;
    [SerializeField, Min(1)] private int idleSampleStride = 4;
    [SerializeField] private bool bakeAuthoredLightVisibilityOnReset;
    [SerializeField] private bool bypassEnabled;

    private Texture2D fogTexture;
    private Sprite fogSprite;
    private Color32[] pixels;
    private bool[] bakedVisibleCells;
    private Vector2[] sampleWorldPoints;
    private FogVisibilityState[] visibilityStates;
    private int textureWidth;
    private int textureHeight;
    private float nextRefreshTime;
    private int currentUpdateGroup;
    private Vector2 lastSamplePlayerPosition;
    private Vector2 lastSampleForward = Vector2.up;
    private bool hasPreviousSample;
    private bool forceFullRefresh = true;
    private int[] playerLineOfSightCacheRevisions;
    private bool[] playerLineOfSightCacheValues;
    private int[] flashlightLineOfSightCacheRevisions;
    private bool[] flashlightLineOfSightCacheValues;
    private int activeLineOfSightCacheRevision = 1;
    private Vector2 lastLineOfSightCachePlayerPosition;
    private Vector2 lastLineOfSightCacheOrigin;
    private Vector2 lastLineOfSightCacheForward = Vector2.up;
    private bool hasLineOfSightCachePose;
    private Rect[] partialRefreshBoundsByGroup;
    private bool[] partialRefreshBoundsValidByGroup;
    private int flashlightQueryCacheFrame = -1;
    private WasdPlayerController flashlightQueryCacheController;
    private Transform flashlightQueryCachePivot;
    private Vector2 flashlightQueryCachePlayerPosition;
    private Vector2 flashlightQueryCachePivotPosition;
    private Vector2 flashlightQueryCachePivotUp = Vector2.up;
    private Vector2 flashlightQueryCacheAimDirection = Vector2.up;
    private float flashlightQueryCacheEffectiveRange;
    private float flashlightQueryCacheEffectiveOuterAngle;
    private float flashlightQueryCacheVisibilityScale;
    private int flashlightQueryCacheBlockingMaskValue;
    private float flashlightQueryCacheSightOriginForwardOffset;
    private float flashlightQueryCacheRaycastPadding;
    private float flashlightQueryCacheRevealAnglePadding;
    private Vector2 flashlightQueryCacheOrigin;
    private Vector2 flashlightQueryCacheForward = Vector2.up;
    private float flashlightQueryCacheHalfAngle;
    private float flashlightQueryCacheCosineHalfAngle;
    private bool hasUploadedFogTextureData;

    public bool BypassEnabled => bypassEnabled;
    public LayerMask VisibilityBlockingLayers => visibilityBlockingLayers;

    private void Awake()
    {
        EnsureOverlayRenderer();
    }

    private void OnEnable()
    {
        EnsureOverlayRenderer();

        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = !bypassEnabled;
        }

        forceFullRefresh = true;
        nextRefreshTime = 0f;
        currentUpdateGroup = 0;
        hasPreviousSample = false;
        ResetPartialRefreshBoundsCache();
        InvalidateFlashlightQueryCache();
    }

    private void OnDisable()
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        ReleaseTextureResources();
    }

    public void Initialize(WasdPlayerController controller, Vector2 center, Vector2 size, LayerMask blockingLayers, GridMapService mapService = null)
    {
        playerController = controller;
        gridMapService = mapService;
        worldCenter = center;
        worldSize = size;
        visibilityBlockingLayers = blockingLayers;
        EnsureOverlayRenderer();

        if (overlayRenderer == null)
        {
            return;
        }

        overlayRenderer.sortingOrder = 90;
        overlayRenderer.color = Color.white;
        overlayRenderer.enabled = !bypassEnabled;
        nextRefreshTime = 0f;
        currentUpdateGroup = 0;
        hasPreviousSample = false;
        forceFullRefresh = true;
        ResetPartialRefreshBoundsCache();
        ResetLineOfSightCaches();
        InvalidateFlashlightQueryCache();
        EnsureTexture();
        ResetMemory();
    }

    public void SetBypassEnabled(bool enabled)
    {
        bypassEnabled = enabled;
        EnsureOverlayRenderer();

        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = !bypassEnabled;
        }

        if (!bypassEnabled)
        {
            forceFullRefresh = true;
            nextRefreshTime = 0f;
            ResetPartialRefreshBoundsCache();
            ResetLineOfSightCaches();
            InvalidateFlashlightQueryCache();
        }
    }

    public void ApplyFogBypass(bool enabled)
    {
        SetBypassEnabled(enabled);
    }

    public void ResetMemory()
    {
        EnsureTexture();

        if (visibilityStates == null)
        {
            return;
        }

        nextRefreshTime = 0f;
        currentUpdateGroup = 0;
        hasPreviousSample = false;
        forceFullRefresh = true;
        ResetPartialRefreshBoundsCache();
        ResetLineOfSightCaches();
        InvalidateFlashlightQueryCache();
        ResetFogToBaseline();
    }

    private void Update()
    {
        long performanceSample = MainEscapePerformanceTracker.BeginSample(MainEscapePerformanceSampleId.FogOverlay);

        try
        {
            EnsureOverlayRenderer();

            if (bypassEnabled)
            {
                if (overlayRenderer != null)
                {
                    overlayRenderer.enabled = false;
                }

                return;
            }

            if (playerController == null)
            {
                return;
            }

            EnsureTexture();

            if (fogTexture == null || pixels == null || visibilityStates == null)
            {
                return;
            }

            Transform flashlightPivot = playerController.FlashlightPivot;
            Vector2 playerPosition = playerController.transform.position;
            Vector2 desiredOrigin = flashlightPivot != null
                ? (Vector2)flashlightPivot.position
                : playerPosition;
            Vector2 forward = flashlightPivot != null
                ? (Vector2)flashlightPivot.up
                : playerController.AimDirection;

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector2.up;
            }

            forward.Normalize();
            desiredOrigin += forward * sightOriginForwardOffset;
            bool useRapidTurnQuality = ShouldUseRapidTurnQuality(forward);

            bool significantMotion = HasSignificantVisionMotion(playerPosition, forward);
            float refreshInterval = significantMotion
                ? Mathf.Max(MinimumMovingRefreshInterval, movingRefreshInterval)
                : idleRefreshInterval;

            if (!forceFullRefresh
                && refreshInterval > 0f
                && Time.unscaledTime < nextRefreshTime
                && !useRapidTurnQuality)
            {
                return;
            }

            Vector2 origin = ResolveFlashlightVisionOrigin(playerPosition, desiredOrigin, visibilityBlockingLayers.value);
            RefreshLineOfSightCachePose(playerPosition, origin, forward);

            float maxDistance = Mathf.Max(closeRevealRadius, playerController.EffectiveFlashlightRange);
            float maxDistanceSquared = maxDistance * maxDistance;
            float halfAngle = ResolveFlashlightRevealHalfAngle(playerController.EffectiveFlashlightOuterAngle);
            float cosineHalfAngle = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            float playerComfortRevealScale = Mathf.Max(0f, playerController.EffectivePlayerComfortRevealScale);
            float scaledComfortRevealRadius = playerComfortRevealRadius * playerComfortRevealScale;
            float comfortRevealRadius = playerController.IsFlashlightSwitchOn
                ? Mathf.Max(
                    closeRevealRadius,
                    HardMinimumComfortRevealRadius,
                    minimumRuntimeComfortRevealRadius,
                    scaledComfortRevealRadius)
                : Mathf.Max(
                    closeRevealRadius,
                    flashlightOffComfortRevealRadius,
                    scaledComfortRevealRadius);
            float comfortRevealRadiusSquared = comfortRevealRadius * comfortRevealRadius;
            float directComfortRadius = ResolveDirectComfortRevealRadius(comfortRevealRadius);
            float directComfortRadiusSquared = directComfortRadius * directComfortRadius;
            int groupCount = significantMotion
                ? Mathf.Max(MinimumMovingInterlacedGroups, interlacedUpdateGroups)
                : 1;
            int sampleStride = significantMotion
                ? Mathf.Max(MinimumMovingSampleStride, movingSampleStride)
                : Mathf.Max(MinimumIdleSampleStride, idleSampleStride);
            if (useRapidTurnQuality)
            {
                groupCount = 1;
                sampleStride = RapidTurnSampleStride;
            }

            bool updateWholeTexture = forceFullRefresh;
            int activeGroup = updateWholeTexture ? 0 : Mathf.Abs(currentUpdateGroup) % groupCount;
            Rect currentPartialRefreshBounds = default;
            Rect refreshBounds = default;
            if (!updateWholeTexture)
            {
                currentPartialRefreshBounds = BuildRefreshBounds(playerPosition, origin, maxDistance, comfortRevealRadius);
                refreshBounds = ResolveRefreshBoundsForGroup(activeGroup, groupCount, currentPartialRefreshBounds);
            }

            float qualityScale = significantMotion ? 1f : Mathf.Max(1f, idleQualityBoost);
            float revealEdgeSoftness = significantMotion
                ? edgeSoftness
                : Mathf.Clamp01(edgeSoftness * Mathf.Max(1f, idleEdgeSoftnessMultiplier));
            float comfortBlendStrength = significantMotion
                ? 1f
                : Mathf.Max(1f, idleComfortBlendStrength);

            bool pixelsChanged = UpdateFogPixels(
                updateWholeTexture,
                activeGroup,
                groupCount,
                sampleStride,
                refreshBounds,
                playerPosition,
                origin,
                forward,
                maxDistance,
                maxDistanceSquared,
                halfAngle,
                cosineHalfAngle,
                comfortRevealRadius,
                comfortRevealRadiusSquared,
                directComfortRadiusSquared,
                revealEdgeSoftness,
                qualityScale,
                comfortBlendStrength);

            if (pixelsChanged)
            {
                UploadFogTexture();
            }

            if (!updateWholeTexture)
            {
                StorePartialRefreshBounds(activeGroup, groupCount, currentPartialRefreshBounds);
                currentUpdateGroup = (currentUpdateGroup + 1) % groupCount;
            }
            else
            {
                currentUpdateGroup = 0;
                ResetPartialRefreshBoundsCache();
            }

            lastSamplePlayerPosition = playerPosition;
            lastSampleForward = forward;
            hasPreviousSample = true;
            forceFullRefresh = false;
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
        }
        finally
        {
            MainEscapePerformanceTracker.EndSample(MainEscapePerformanceSampleId.FogOverlay, performanceSample);
        }
    }

    private bool UpdateFogPixels(
        bool updateWholeTexture,
        int activeGroup,
        int groupCount,
        int sampleStride,
        Rect refreshBounds,
        Vector2 playerPosition,
        Vector2 origin,
        Vector2 forward,
        float maxDistance,
        float maxDistanceSquared,
        float halfAngle,
        float cosineHalfAngle,
        float comfortRevealRadius,
        float comfortRevealRadiusSquared,
        float directComfortRevealRadiusSquared,
        float revealEdgeSoftness,
        float qualityScale,
        float comfortBlendStrength)
    {
        int stride = Mathf.Max(1, sampleStride);
        bool pixelsChanged = false;
        long updatePixelsSample = MainEscapePerformanceTracker.BeginSample(MainEscapePerformanceSampleId.FogUpdatePixels);

        try
        {
            RectInt pixelRefreshBounds = updateWholeTexture
                ? new RectInt(0, 0, textureWidth, textureHeight)
                : ResolvePixelRefreshBounds(refreshBounds, stride);

            if (pixelRefreshBounds.width <= 0 || pixelRefreshBounds.height <= 0)
            {
                return false;
            }

            int startY = pixelRefreshBounds.yMin;
            int endY = pixelRefreshBounds.yMax;
            int startX = pixelRefreshBounds.xMin;
            int endX = pixelRefreshBounds.xMax;

            for (int y = startY; y < endY; y += stride)
            {
                if (!updateWholeTexture && !ShouldProcessInterlacedBlockRow(y, stride, groupCount, activeGroup))
                {
                    continue;
                }

                int blockHeight = Mathf.Min(stride, textureHeight - y);

                for (int x = startX; x < endX; x += stride)
                {
                    int blockWidth = Mathf.Min(stride, textureWidth - x);
                    int sampleX = Mathf.Min(textureWidth - 1, x + (blockWidth / 2));
                    int sampleY = Mathf.Min(textureHeight - 1, y + (blockHeight / 2));
                    int sampleIndex = sampleX + (sampleY * textureWidth);
                    Vector2 samplePoint = sampleWorldPoints != null && sampleIndex < sampleWorldPoints.Length
                        ? sampleWorldPoints[sampleIndex]
                        : GetWorldPoint(sampleX, sampleY);

                    if (!updateWholeTexture && !ContainsWorldPoint(refreshBounds, samplePoint))
                    {
                        continue;
                    }

                    float visibilityStrength = 0f;
                    FogVisibilityState state = FogVisibilityState.Unexplored;

                    if (TryComputeVisibility(
                        playerPosition,
                        origin,
                        forward,
                        sampleIndex,
                        maxDistance,
                        maxDistanceSquared,
                        halfAngle,
                        cosineHalfAngle,
                        comfortRevealRadius,
                        comfortRevealRadiusSquared,
                        directComfortRevealRadiusSquared,
                        revealEdgeSoftness,
                        qualityScale,
                        comfortBlendStrength,
                        samplePoint,
                        out visibilityStrength))
                    {
                        state = FogVisibilityState.Visible;
                    }

                    float blockBlendRadius = ResolveBlockBlendRadius(x, y, blockWidth, blockHeight, sampleX, sampleY);

                    if (state != FogVisibilityState.Visible || blockBlendRadius <= 0.0001f)
                    {
                        Color32 uniformPixel = ResolveUniformBlockPixel(visibilityStrength, state);
                        pixelsChanged |= ApplyUniformBlock(x, y, blockWidth, blockHeight, state, uniformPixel);
                        continue;
                    }

                    for (int offsetY = 0; offsetY < blockHeight; offsetY++)
                    {
                        int pixelY = y + offsetY;
                        int rowStartIndex = pixelY * textureWidth;

                        for (int offsetX = 0; offsetX < blockWidth; offsetX++)
                        {
                            int pixelX = x + offsetX;
                            int index = rowStartIndex + x + offsetX;
                            float pixelAlpha = ResolveInterpolatedBlockAlpha(
                                visibilityStrength,
                                state,
                                pixelX,
                                pixelY,
                                sampleX,
                                sampleY,
                                blockBlendRadius);
                            float resolvedAlpha = ApplyDarknessLayers(pixelAlpha, state);
                            Color32 nextPixel = new(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(resolvedAlpha) * 255f));
                            pixelsChanged |= ApplyResolvedPixel(index, state, nextPixel);
                        }
                    }
                }
            }
        }
        finally
        {
            MainEscapePerformanceTracker.EndSample(MainEscapePerformanceSampleId.FogUpdatePixels, updatePixelsSample);
        }

        return pixelsChanged;
    }

    private RectInt ResolvePixelRefreshBounds(Rect worldBounds, int stride)
    {
        if (textureWidth <= 0 || textureHeight <= 0 || worldSize.x <= 0f || worldSize.y <= 0f)
        {
            return new RectInt(0, 0, textureWidth, textureHeight);
        }

        float minWorldX = worldCenter.x - (worldSize.x * 0.5f);
        float minWorldY = worldCenter.y - (worldSize.y * 0.5f);
        float normalizedMinX = (worldBounds.xMin - minWorldX) / worldSize.x;
        float normalizedMaxX = (worldBounds.xMax - minWorldX) / worldSize.x;
        float normalizedMinY = (worldBounds.yMin - minWorldY) / worldSize.y;
        float normalizedMaxY = (worldBounds.yMax - minWorldY) / worldSize.y;

        int safeStride = Mathf.Max(1, stride);
        int xMin = Mathf.Clamp(Mathf.FloorToInt(normalizedMinX * textureWidth) - safeStride, 0, textureWidth);
        int xMax = Mathf.Clamp(Mathf.CeilToInt(normalizedMaxX * textureWidth) + safeStride, 0, textureWidth);
        int yMin = Mathf.Clamp(Mathf.FloorToInt(normalizedMinY * textureHeight) - safeStride, 0, textureHeight);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(normalizedMaxY * textureHeight) + safeStride, 0, textureHeight);

        xMin = AlignDownToStride(xMin, safeStride);
        yMin = AlignDownToStride(yMin, safeStride);

        if (xMax <= xMin || yMax <= yMin)
        {
            return new RectInt(0, 0, 0, 0);
        }

        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static int AlignDownToStride(int value, int stride)
    {
        if (stride <= 1)
        {
            return value;
        }

        return value - (value % stride);
    }

    private Rect BuildRefreshBounds(Vector2 playerPosition, Vector2 origin, float maxDistance, float comfortRevealRadius)
    {
        float revealRadius = Mathf.Max(maxDistance, comfortRevealRadius) + Mathf.Max(0f, roiPadding);
        Vector2 min = Vector2.Min(playerPosition, origin) - (Vector2.one * revealRadius);
        Vector2 max = Vector2.Max(playerPosition, origin) + (Vector2.one * revealRadius);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Rect ResolveRefreshBoundsForGroup(int activeGroup, int groupCount, Rect currentRefreshBounds)
    {
        int safeGroupCount = Mathf.Max(1, groupCount);
        Rect resolvedRefreshBounds = currentRefreshBounds;

        if (partialRefreshBoundsByGroup != null
            && partialRefreshBoundsValidByGroup != null
            && partialRefreshBoundsByGroup.Length != safeGroupCount)
        {
            int previousGroupCount = Mathf.Min(partialRefreshBoundsByGroup.Length, partialRefreshBoundsValidByGroup.Length);

            for (int index = 0; index < previousGroupCount; index++)
            {
                if (!partialRefreshBoundsValidByGroup[index])
                {
                    continue;
                }

                resolvedRefreshBounds = CombineRefreshBounds(resolvedRefreshBounds, partialRefreshBoundsByGroup[index]);
            }
        }

        EnsurePartialRefreshBoundsCache(safeGroupCount);

        if (partialRefreshBoundsByGroup == null
            || partialRefreshBoundsValidByGroup == null
            || activeGroup < 0
            || activeGroup >= partialRefreshBoundsByGroup.Length
            || activeGroup >= partialRefreshBoundsValidByGroup.Length
            || !partialRefreshBoundsValidByGroup[activeGroup])
        {
            return resolvedRefreshBounds;
        }

        return CombineRefreshBounds(resolvedRefreshBounds, partialRefreshBoundsByGroup[activeGroup]);
    }

    private void StorePartialRefreshBounds(int activeGroup, int groupCount, Rect currentRefreshBounds)
    {
        EnsurePartialRefreshBoundsCache(groupCount);

        if (partialRefreshBoundsByGroup == null
            || partialRefreshBoundsValidByGroup == null
            || activeGroup < 0
            || activeGroup >= partialRefreshBoundsByGroup.Length
            || activeGroup >= partialRefreshBoundsValidByGroup.Length)
        {
            return;
        }

        partialRefreshBoundsByGroup[activeGroup] = currentRefreshBounds;
        partialRefreshBoundsValidByGroup[activeGroup] = true;
    }

    private void EnsurePartialRefreshBoundsCache(int groupCount)
    {
        int safeGroupCount = Mathf.Max(1, groupCount);

        if (partialRefreshBoundsByGroup != null
            && partialRefreshBoundsValidByGroup != null
            && partialRefreshBoundsByGroup.Length == safeGroupCount
            && partialRefreshBoundsValidByGroup.Length == safeGroupCount)
        {
            return;
        }

        partialRefreshBoundsByGroup = new Rect[safeGroupCount];
        partialRefreshBoundsValidByGroup = new bool[safeGroupCount];
    }

    private void ResetPartialRefreshBoundsCache()
    {
        if (partialRefreshBoundsValidByGroup == null)
        {
            return;
        }

        System.Array.Clear(partialRefreshBoundsValidByGroup, 0, partialRefreshBoundsValidByGroup.Length);
    }

    private static Rect CombineRefreshBounds(Rect first, Rect second)
    {
        return Rect.MinMaxRect(
            Mathf.Min(first.xMin, second.xMin),
            Mathf.Min(first.yMin, second.yMin),
            Mathf.Max(first.xMax, second.xMax),
            Mathf.Max(first.yMax, second.yMax));
    }

    private static bool ContainsWorldPoint(Rect bounds, Vector2 worldPoint)
    {
        return worldPoint.x >= bounds.xMin
            && worldPoint.x <= bounds.xMax
            && worldPoint.y >= bounds.yMin
            && worldPoint.y <= bounds.yMax;
    }

    private static bool ShouldProcessInterlacedBlockRow(int pixelY, int sampleStride, int groupCount, int activeGroup)
    {
        int safeStride = Mathf.Max(1, sampleStride);
        int safeGroupCount = Mathf.Max(1, groupCount);
        int blockRow = Mathf.Max(0, pixelY) / safeStride;
        return (blockRow % safeGroupCount) == Mathf.Abs(activeGroup) % safeGroupCount;
    }

    private bool TryComputeVisibility(
        Vector2 playerPosition,
        Vector2 origin,
        Vector2 forward,
        int sampleIndex,
        float maxDistance,
        float maxDistanceSquared,
        float halfAngle,
        float cosineHalfAngle,
        float comfortRevealRadius,
        float comfortRevealRadiusSquared,
        float directComfortRevealRadiusSquared,
        float revealEdgeSoftness,
        float qualityScale,
        float comfortBlendStrength,
        Vector2 samplePoint,
        out float visibilityStrength)
    {
        if (IsBakedVisible(sampleIndex))
        {
            visibilityStrength = 1f;
            return true;
        }

        if (TryComputePlayerReveal(
            playerPosition,
            samplePoint,
            sampleIndex,
            comfortRevealRadius,
            comfortRevealRadiusSquared,
            directComfortRevealRadiusSquared,
            comfortBlendStrength,
            out visibilityStrength))
        {
            return true;
        }

        if (TryComputeFlashlightReveal(
            origin,
            forward,
            maxDistance,
            maxDistanceSquared,
            halfAngle,
            cosineHalfAngle,
            revealEdgeSoftness,
            qualityScale,
            samplePoint,
            visibilityBlockingLayers.value,
            sampleIndex,
            out visibilityStrength))
        {
            return true;
        }

        if (TryComputeAuthoredLightFallback(samplePoint, out visibilityStrength))
        {
            return true;
        }

        visibilityStrength = 0f;
        return false;
    }

    private bool ApplyUniformBlock(
        int blockX,
        int blockY,
        int blockWidth,
        int blockHeight,
        FogVisibilityState state,
        Color32 pixel)
    {
        bool pixelsChanged = false;

        for (int offsetY = 0; offsetY < blockHeight; offsetY++)
        {
            int rowStartIndex = (blockY + offsetY) * textureWidth;

            for (int offsetX = 0; offsetX < blockWidth; offsetX++)
            {
                int index = rowStartIndex + blockX + offsetX;
                pixelsChanged |= ApplyResolvedPixel(index, state, pixel);
            }
        }

        return pixelsChanged;
    }

    private Color32 ResolveUniformBlockPixel(float sampledVisibilityStrength, FogVisibilityState state)
    {
        float pixelAlpha = Mathf.Lerp(unexploredAlpha, visibleAlpha, sampledVisibilityStrength);
        float resolvedAlpha = ApplyDarknessLayers(pixelAlpha, state);
        return new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(resolvedAlpha) * 255f));
    }

    private float ResolveInterpolatedBlockAlpha(
        float sampledVisibilityStrength,
        FogVisibilityState state,
        int pixelX,
        int pixelY,
        int sampleX,
        int sampleY,
        float blockBlendRadius)
    {
        if (state != FogVisibilityState.Visible || blockBlendRadius <= 0.0001f)
        {
            return Mathf.Lerp(unexploredAlpha, visibleAlpha, sampledVisibilityStrength);
        }

        float sampleCenterX = sampleX + 0.5f;
        float sampleCenterY = sampleY + 0.5f;
        float pixelCenterX = pixelX + 0.5f;
        float pixelCenterY = pixelY + 0.5f;
        float deltaX = pixelCenterX - sampleCenterX;
        float deltaY = pixelCenterY - sampleCenterY;
        float distance = Mathf.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        float normalizedDistance = Mathf.Clamp01(distance / blockBlendRadius);
        float edgeAttenuation = Mathf.Lerp(1f, 0.72f, Mathf.SmoothStep(0f, 1f, normalizedDistance));
        float attenuatedVisibilityStrength = sampledVisibilityStrength * edgeAttenuation;
        return Mathf.Lerp(unexploredAlpha, visibleAlpha, attenuatedVisibilityStrength);
    }

    private static float ResolveBlockBlendRadius(
        int blockX,
        int blockY,
        int blockWidth,
        int blockHeight,
        int sampleX,
        int sampleY)
    {
        float sampleCenterX = sampleX + 0.5f;
        float sampleCenterY = sampleY + 0.5f;
        float minPixelCenterX = blockX + 0.5f;
        float maxPixelCenterX = blockX + blockWidth - 0.5f;
        float minPixelCenterY = blockY + 0.5f;
        float maxPixelCenterY = blockY + blockHeight - 0.5f;
        float maxDeltaX = Mathf.Max(
            Mathf.Abs(minPixelCenterX - sampleCenterX),
            Mathf.Abs(maxPixelCenterX - sampleCenterX));
        float maxDeltaY = Mathf.Max(
            Mathf.Abs(minPixelCenterY - sampleCenterY),
            Mathf.Abs(maxPixelCenterY - sampleCenterY));
        return Mathf.Sqrt((maxDeltaX * maxDeltaX) + (maxDeltaY * maxDeltaY));
    }

    private bool TryComputePlayerReveal(
        Vector2 playerPosition,
        Vector2 samplePoint,
        int sampleIndex,
        float comfortRevealRadius,
        float comfortRevealRadiusSquared,
        float directComfortRevealRadiusSquared,
        float comfortBlendStrength,
        out float visibilityStrength)
    {
        if (comfortRevealRadius <= 0f)
        {
            visibilityStrength = 0f;
            return false;
        }

        Vector2 toSample = samplePoint - playerPosition;
        float distanceSquared = toSample.sqrMagnitude;

        if (distanceSquared > comfortRevealRadiusSquared)
        {
            visibilityStrength = 0f;
            return false;
        }

        float distance = 0f;

        if (distanceSquared > 0.0001f)
        {
            distance = Mathf.Sqrt(distanceSquared);
            if (distanceSquared > directComfortRevealRadiusSquared)
            {
                Vector2 direction = toSample / distance;
                if (!HasCachedLineOfSight(
                    playerCache: true,
                    sampleIndex,
                    playerPosition,
                    samplePoint,
                    direction,
                    distance,
                    visibilityBlockingLayers.value))
                {
                    visibilityStrength = 0f;
                    return false;
                }
            }
        }

        float normalizedDistance = 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, comfortRevealRadius));
        float softenedStrength = Mathf.SmoothStep(0f, 1f, normalizedDistance);
        softenedStrength = Mathf.Pow(softenedStrength, 1f / Mathf.Max(0.01f, comfortBlendStrength));
        visibilityStrength = Mathf.Lerp(0.18f, 1f, softenedStrength);
        return true;
    }

    private bool TryComputeFlashlightReveal(
        Vector2 origin,
        Vector2 forward,
        float maxDistance,
        float maxDistanceSquared,
        float halfAngle,
        float cosineHalfAngle,
        float revealEdgeSoftness,
        float qualityScale,
        Vector2 samplePoint,
        int blockingMask,
        int sampleIndex,
        out float visibilityStrength)
    {
        Vector2 toSample = samplePoint - origin;
        float distanceSquared = toSample.sqrMagnitude;
        float boostedCloseRevealRadius = closeRevealRadius * Mathf.Max(1f, qualityScale * 0.9f);

        if (distanceSquared <= boostedCloseRevealRadius * boostedCloseRevealRadius)
        {
            visibilityStrength = 1f;
            return true;
        }

        if (distanceSquared <= 0.0001f || distanceSquared > maxDistanceSquared)
        {
            visibilityStrength = 0f;
            return false;
        }

        float distance = Mathf.Sqrt(distanceSquared);
        Vector2 direction = toSample / distance;

        if (Vector2.Dot(forward, direction) < cosineHalfAngle)
        {
            visibilityStrength = 0f;
            return false;
        }

        float angle = Vector2.Angle(forward, direction);

        if (angle > halfAngle)
        {
            visibilityStrength = 0f;
            return false;
        }

        if (blockingMask != 0)
        {
            if (!HasCachedLineOfSight(
                playerCache: false,
                sampleIndex,
                origin,
                samplePoint,
                direction,
                distance,
                blockingMask))
            {
                visibilityStrength = 0f;
                return false;
            }
        }

        float distanceFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, maxDistance));
        float angleFactor = 1f - Mathf.Clamp01(angle / Mathf.Max(0.001f, halfAngle));
        distanceFactor = Mathf.Pow(distanceFactor, 1f / Mathf.Max(1f, qualityScale));
        angleFactor = Mathf.Pow(angleFactor, 1f / Mathf.Max(1f, qualityScale));
        float coneStrength = distanceFactor * angleFactor;
        float softenedStrength = Mathf.Clamp01((coneStrength - revealEdgeSoftness) / Mathf.Max(0.001f, 1f - revealEdgeSoftness));
        visibilityStrength = Mathf.SmoothStep(0.04f, 1f, softenedStrength);
        return true;
    }

    private bool TryComputeAuthoredLightFallback(Vector2 samplePoint, out float visibilityStrength)
    {
        visibilityStrength = 0f;

        if (bakeAuthoredLightVisibilityOnReset)
        {
            return false;
        }

        if (!AuthoredVisibilityLight2D.TrySampleStrongestReveal(
                samplePoint,
                visibilityBlockingLayers.value,
                raycastPadding,
                out _))
        {
            return false;
        }

        visibilityStrength = 1f;
        return true;
    }

    private bool IsBakedVisible(int sampleIndex)
    {
        return bakedVisibleCells != null
            && sampleIndex >= 0
            && sampleIndex < bakedVisibleCells.Length
            && bakedVisibleCells[sampleIndex];
    }

    private void ResetFogToBaseline()
    {
        bool pixelsChanged = FillTexture(unexploredAlpha);
        RebuildBakedAuthoredLightVisibility();

        if (ApplyBakedAuthoredLightVisibility())
        {
            pixelsChanged = true;
        }

        if (pixelsChanged || !hasUploadedFogTextureData)
        {
            UploadFogTexture();
        }
    }

    private void RebuildBakedAuthoredLightVisibility()
    {
        if (bakedVisibleCells == null)
        {
            return;
        }

        System.Array.Clear(bakedVisibleCells, 0, bakedVisibleCells.Length);

        if (!bakeAuthoredLightVisibilityOnReset
            || sampleWorldPoints == null
            || sampleWorldPoints.Length != bakedVisibleCells.Length)
        {
            return;
        }

        for (int index = 0; index < sampleWorldPoints.Length; index++)
        {
            if (AuthoredVisibilityLight2D.TrySampleStrongestReveal(
                    sampleWorldPoints[index],
                    visibilityBlockingLayers.value,
                    raycastPadding,
                    out _))
            {
                bakedVisibleCells[index] = true;
            }
        }
    }

    private bool ApplyBakedAuthoredLightVisibility()
    {
        if (!bakeAuthoredLightVisibilityOnReset
            || bakedVisibleCells == null
            || pixels == null
            || visibilityStates == null)
        {
            return false;
        }

        float resolvedAlpha = ApplyDarknessLayers(visibleAlpha, FogVisibilityState.Visible);
        byte visibleAlphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(resolvedAlpha) * 255f);
        Color32 visiblePixel = new(0, 0, 0, visibleAlphaByte);
        bool pixelsChanged = false;

        for (int index = 0; index < bakedVisibleCells.Length; index++)
        {
            if (!bakedVisibleCells[index])
            {
                continue;
            }

            pixelsChanged |= ApplyResolvedPixel(index, FogVisibilityState.Visible, visiblePixel);
        }

        return pixelsChanged;
    }

    public FogVisibilityState GetStateAtWorldPoint(Vector2 worldPoint)
    {
        if (bypassEnabled)
        {
            return FogVisibilityState.Visible;
        }

        if (visibilityStates == null || textureWidth <= 0 || textureHeight <= 0)
        {
            return FogVisibilityState.Unexplored;
        }

        if (!TryGetPixelIndex(worldPoint, out int index))
        {
            return FogVisibilityState.Unexplored;
        }

        return visibilityStates[index];
    }

    public bool IsWorldPointVisible(Vector2 worldPoint)
    {
        return GetStateAtWorldPoint(worldPoint) == FogVisibilityState.Visible;
    }

    public float SampleFlashlightVisibility(Vector2 worldPoint, float distancePadding, bool ignoreDoorLayer)
    {
        if (!TryResolveFlashlightQuery(
                out WasdPlayerController activePlayerController,
                out Vector2 origin,
                out Vector2 forward,
                out float effectiveRange,
                out float halfAngle,
                out float cosineHalfAngle,
                out float visibilityScale))
        {
            return 0f;
        }

        float maxDistance = Mathf.Max(0.1f, effectiveRange + Mathf.Max(0f, distancePadding));
        float maxDistanceSquared = maxDistance * maxDistance;
        int blockingMask = ResolveVisibilityBlockingMask(ignoreDoorLayer);

        if (!TryComputeFlashlightReveal(
            origin,
            forward,
            maxDistance,
            maxDistanceSquared,
            halfAngle,
            cosineHalfAngle,
            edgeSoftness,
            1f,
            worldPoint,
            blockingMask,
            -1,
            out float visibilityStrength))
        {
            return 0f;
        }

        return visibilityStrength * visibilityScale;
    }

    private bool ApplyResolvedPixel(int index, FogVisibilityState state, Color32 pixel)
    {
        bool changed = visibilityStates[index] != state || !AreColorsEqual(pixels[index], pixel);

        if (!changed)
        {
            return false;
        }

        visibilityStates[index] = state;
        pixels[index] = pixel;
        return true;
    }

    private static bool AreColorsEqual(Color32 left, Color32 right)
    {
        return left.r == right.r
            && left.g == right.g
            && left.b == right.b
            && left.a == right.a;
    }

    private bool FillTexture(float alpha)
    {
        if (pixels == null || visibilityStates == null)
        {
            return false;
        }

        byte alphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
        Color32 fillPixel = new(0, 0, 0, alphaByte);
        bool pixelsChanged = false;

        for (int index = 0; index < pixels.Length; index++)
        {
            pixelsChanged |= ApplyResolvedPixel(index, FogVisibilityState.Unexplored, fillPixel);
        }

        return pixelsChanged;
    }

    private void UploadFogTexture()
    {
        if (fogTexture == null || pixels == null)
        {
            return;
        }

        long uploadSample = MainEscapePerformanceTracker.BeginSample(MainEscapePerformanceSampleId.FogUpload);
        fogTexture.SetPixelData(pixels, 0);
        fogTexture.Apply(false, false);
        hasUploadedFogTextureData = true;
        MainEscapePerformanceTracker.EndSample(MainEscapePerformanceSampleId.FogUpload, uploadSample);
    }

    private void RebuildSamplePointCache()
    {
        if (sampleWorldPoints == null || sampleWorldPoints.Length != textureWidth * textureHeight)
        {
            return;
        }

        for (int y = 0; y < textureHeight; y++)
        {
            int rowStartIndex = y * textureWidth;

            for (int x = 0; x < textureWidth; x++)
            {
                sampleWorldPoints[rowStartIndex + x] = GetWorldPoint(x, y);
            }
        }
    }

    private Vector2 GetWorldPoint(int pixelX, int pixelY)
    {
        if (textureWidth <= 0 || textureHeight <= 0)
        {
            return worldCenter;
        }

        float minX = worldCenter.x - (worldSize.x * 0.5f);
        float minY = worldCenter.y - (worldSize.y * 0.5f);
        float normalizedX = (pixelX + 0.5f) / textureWidth;
        float normalizedY = (pixelY + 0.5f) / textureHeight;
        return new Vector2(
            minX + (normalizedX * worldSize.x),
            minY + (normalizedY * worldSize.y));
    }

    private float ResolveDirectComfortRevealRadius(float comfortRevealRadius)
    {
        if (comfortRevealRadius <= 0f)
        {
            return 0f;
        }

        return Mathf.Min(
            comfortRevealRadius,
            Mathf.Max(closeRevealRadius, directComfortRevealRadius));
    }

    private int ResolveVisibilityBlockingMask(bool ignoreDoorLayer)
    {
        int blockingMask = visibilityBlockingLayers.value;

        if (!ignoreDoorLayer)
        {
            return blockingMask;
        }

        int doorLayerIndex = GameLayers.DoorIndex;

        if (doorLayerIndex < 0 || doorLayerIndex >= 32)
        {
            return blockingMask;
        }

        return blockingMask & ~(1 << doorLayerIndex);
    }

    private void ResetLineOfSightCaches()
    {
        hasLineOfSightCachePose = false;
        activeLineOfSightCacheRevision = 1;

        if (playerLineOfSightCacheRevisions != null)
        {
            System.Array.Clear(playerLineOfSightCacheRevisions, 0, playerLineOfSightCacheRevisions.Length);
        }

        if (flashlightLineOfSightCacheRevisions != null)
        {
            System.Array.Clear(flashlightLineOfSightCacheRevisions, 0, flashlightLineOfSightCacheRevisions.Length);
        }
    }

    private void InvalidateFlashlightQueryCache()
    {
        flashlightQueryCacheFrame = -1;
        flashlightQueryCacheController = null;
        flashlightQueryCachePivot = null;
        flashlightQueryCacheForward = Vector2.up;
    }

    private bool TryResolveFlashlightQuery(
        out WasdPlayerController activePlayerController,
        out Vector2 origin,
        out Vector2 forward,
        out float effectiveRange,
        out float halfAngle,
        out float cosineHalfAngle,
        out float visibilityScale)
    {
        activePlayerController = playerController;
        origin = default;
        forward = Vector2.up;
        effectiveRange = 0f;
        halfAngle = 0f;
        cosineHalfAngle = 0f;
        visibilityScale = 0f;

        if (activePlayerController == null || !activePlayerController.IsFlashlightSwitchOn)
        {
            InvalidateFlashlightQueryCache();
            return false;
        }

        Transform flashlightPivot = activePlayerController.FlashlightPivot;
        Vector2 playerPosition = activePlayerController.transform.position;
        Vector2 pivotPosition = flashlightPivot != null ? (Vector2)flashlightPivot.position : playerPosition;
        Vector2 pivotUp = flashlightPivot != null ? (Vector2)flashlightPivot.up : Vector2.up;
        Vector2 aimDirection = activePlayerController.AimDirection;
        effectiveRange = activePlayerController.EffectiveFlashlightRange;
        float effectiveOuterAngle = activePlayerController.EffectiveFlashlightOuterAngle;
        visibilityScale = Mathf.Clamp01(activePlayerController.FlashlightVisibilityScale);
        int blockingMaskValue = visibilityBlockingLayers.value;
        float cachedSightOriginForwardOffset = sightOriginForwardOffset;
        float cachedRaycastPadding = raycastPadding;
        float revealAnglePadding = flashlightRevealAnglePadding;

        bool canReuseCachedQuery = flashlightQueryCacheFrame == Time.frameCount
            && flashlightQueryCacheController == activePlayerController
            && flashlightQueryCachePivot == flashlightPivot
            && flashlightQueryCachePlayerPosition == playerPosition
            && flashlightQueryCachePivotPosition == pivotPosition
            && flashlightQueryCachePivotUp == pivotUp
            && flashlightQueryCacheAimDirection == aimDirection
            && Mathf.Approximately(flashlightQueryCacheEffectiveRange, effectiveRange)
            && Mathf.Approximately(flashlightQueryCacheEffectiveOuterAngle, effectiveOuterAngle)
            && Mathf.Approximately(flashlightQueryCacheVisibilityScale, visibilityScale)
            && flashlightQueryCacheBlockingMaskValue == blockingMaskValue
            && Mathf.Approximately(flashlightQueryCacheSightOriginForwardOffset, cachedSightOriginForwardOffset)
            && Mathf.Approximately(flashlightQueryCacheRaycastPadding, cachedRaycastPadding)
            && Mathf.Approximately(flashlightQueryCacheRevealAnglePadding, revealAnglePadding);

        if (canReuseCachedQuery)
        {
            origin = flashlightQueryCacheOrigin;
            forward = flashlightQueryCacheForward;
            halfAngle = flashlightQueryCacheHalfAngle;
            cosineHalfAngle = flashlightQueryCacheCosineHalfAngle;
            return true;
        }

        if (!TryResolveFlashlightPose(out _, out origin, out forward))
        {
            InvalidateFlashlightQueryCache();
            return false;
        }

        halfAngle = ResolveFlashlightRevealHalfAngle(effectiveOuterAngle);
        cosineHalfAngle = Mathf.Cos(halfAngle * Mathf.Deg2Rad);

        flashlightQueryCacheFrame = Time.frameCount;
        flashlightQueryCacheController = activePlayerController;
        flashlightQueryCachePivot = flashlightPivot;
        flashlightQueryCachePlayerPosition = playerPosition;
        flashlightQueryCachePivotPosition = pivotPosition;
        flashlightQueryCachePivotUp = pivotUp;
        flashlightQueryCacheAimDirection = aimDirection;
        flashlightQueryCacheEffectiveRange = effectiveRange;
        flashlightQueryCacheEffectiveOuterAngle = effectiveOuterAngle;
        flashlightQueryCacheVisibilityScale = visibilityScale;
        flashlightQueryCacheBlockingMaskValue = blockingMaskValue;
        flashlightQueryCacheSightOriginForwardOffset = cachedSightOriginForwardOffset;
        flashlightQueryCacheRaycastPadding = cachedRaycastPadding;
        flashlightQueryCacheRevealAnglePadding = revealAnglePadding;
        flashlightQueryCacheOrigin = origin;
        flashlightQueryCacheForward = forward;
        flashlightQueryCacheHalfAngle = halfAngle;
        flashlightQueryCacheCosineHalfAngle = cosineHalfAngle;
        return true;
    }

    private void RefreshLineOfSightCachePose(Vector2 playerPosition, Vector2 origin, Vector2 forward)
    {
        if (!hasLineOfSightCachePose)
        {
            lastLineOfSightCachePlayerPosition = playerPosition;
            lastLineOfSightCacheOrigin = origin;
            lastLineOfSightCacheForward = forward;
            hasLineOfSightCachePose = true;
            return;
        }

        bool shouldInvalidate = HasMovedBeyondThreshold(lastLineOfSightCachePlayerPosition, playerPosition, 0.14f)
            || HasMovedBeyondThreshold(lastLineOfSightCacheOrigin, origin, 0.14f)
            || HasRotatedBeyondThreshold(lastLineOfSightCacheForward, forward, 5f);

        if (shouldInvalidate)
        {
            AdvanceLineOfSightCacheRevision();
        }

        lastLineOfSightCachePlayerPosition = playerPosition;
        lastLineOfSightCacheOrigin = origin;
        lastLineOfSightCacheForward = forward;
    }

    private bool HasCachedLineOfSight(
        bool playerCache,
        int sampleIndex,
        Vector2 origin,
        Vector2 samplePoint,
        Vector2 direction,
        float distance,
        int blockingMask)
    {
        if (sampleIndex < 0)
        {
            return HasVisibilityLineOfSight(origin, samplePoint, direction, distance, blockingMask);
        }

        int[] revisions = playerCache
            ? playerLineOfSightCacheRevisions
            : flashlightLineOfSightCacheRevisions;
        bool[] values = playerCache
            ? playerLineOfSightCacheValues
            : flashlightLineOfSightCacheValues;

        if (revisions == null
            || values == null
            || sampleIndex >= revisions.Length
            || sampleIndex >= values.Length)
        {
            return HasVisibilityLineOfSight(origin, samplePoint, direction, distance, blockingMask);
        }

        if (revisions[sampleIndex] != activeLineOfSightCacheRevision)
        {
            values[sampleIndex] = HasVisibilityLineOfSight(origin, samplePoint, direction, distance, blockingMask);
            revisions[sampleIndex] = activeLineOfSightCacheRevision;
        }

        return values[sampleIndex];
    }

    private void AdvanceLineOfSightCacheRevision()
    {
        if (activeLineOfSightCacheRevision == int.MaxValue)
        {
            ResetLineOfSightCaches();
            return;
        }

        activeLineOfSightCacheRevision++;
    }

    private static bool HasMovedBeyondThreshold(Vector2 previous, Vector2 current, float threshold)
    {
        float minimumThreshold = Mathf.Max(0f, threshold);
        float minimumThresholdSquared = minimumThreshold * minimumThreshold;

        if (minimumThresholdSquared <= 0f)
        {
            return (previous - current).sqrMagnitude > 0.000001f;
        }

        return (previous - current).sqrMagnitude >= minimumThresholdSquared;
    }

    private static bool HasRotatedBeyondThreshold(Vector2 previous, Vector2 current, float threshold)
    {
        Vector2 normalizedPrevious = previous.sqrMagnitude > 0.0001f ? previous.normalized : Vector2.up;
        Vector2 normalizedCurrent = current.sqrMagnitude > 0.0001f ? current.normalized : Vector2.up;

        if (threshold <= 0f)
        {
            return Vector2.Dot(normalizedPrevious, normalizedCurrent) < 0.9999f;
        }

        float rotationDotThreshold = Mathf.Cos(threshold * Mathf.Deg2Rad);
        return Vector2.Dot(normalizedPrevious, normalizedCurrent) <= rotationDotThreshold;
    }

    private bool HasSignificantVisionMotion(Vector2 playerPosition, Vector2 forward)
    {
        if (!hasPreviousSample)
        {
            return true;
        }

        float movementThresholdSquared = movementRefreshThreshold * movementRefreshThreshold;

        if ((lastSamplePlayerPosition - playerPosition).sqrMagnitude >= movementThresholdSquared)
        {
            return true;
        }

        float rotationDotThreshold = Mathf.Cos(rotationRefreshThreshold * Mathf.Deg2Rad);
        return Vector2.Dot(lastSampleForward, forward) <= rotationDotThreshold;
    }

    private bool ShouldUseRapidTurnQuality(Vector2 forward)
    {
        if (!hasPreviousSample)
        {
            return false;
        }

        return Vector2.Angle(lastSampleForward, forward) >= RapidTurnQualityAngleThreshold;
    }

    private void EnsureTexture()
    {
        EnsureOverlayRenderer();

        if (overlayRenderer == null)
        {
            return;
        }

        int desiredWidth = Mathf.Max(8, Mathf.RoundToInt(worldSize.x * pixelsPerUnit));
        int desiredHeight = Mathf.Max(8, Mathf.RoundToInt(worldSize.y * pixelsPerUnit));

        bool hasValidTexture = fogTexture != null
            && fogSprite != null
            && pixels != null
            && bakedVisibleCells != null
            && sampleWorldPoints != null
            && visibilityStates != null
            && playerLineOfSightCacheRevisions != null
            && playerLineOfSightCacheValues != null
            && flashlightLineOfSightCacheRevisions != null
            && flashlightLineOfSightCacheValues != null
            && desiredWidth == textureWidth
            && desiredHeight == textureHeight;

        if (hasValidTexture)
        {
            if (overlayRenderer.sprite != fogSprite)
            {
                overlayRenderer.sprite = fogSprite;
            }

            return;
        }

        ReleaseTextureResources();
        textureWidth = desiredWidth;
        textureHeight = desiredHeight;

        fogTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        hasUploadedFogTextureData = false;

        fogSprite = Sprite.Create(
            fogTexture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);

        fogSprite.name = "FlashlightFogOfWarSprite";
        overlayRenderer.sprite = fogSprite;
        transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        transform.localScale = Vector3.one;

        pixels = new Color32[textureWidth * textureHeight];
        bakedVisibleCells = new bool[textureWidth * textureHeight];
        sampleWorldPoints = new Vector2[textureWidth * textureHeight];
        visibilityStates = new FogVisibilityState[textureWidth * textureHeight];
        playerLineOfSightCacheRevisions = new int[textureWidth * textureHeight];
        playerLineOfSightCacheValues = new bool[textureWidth * textureHeight];
        flashlightLineOfSightCacheRevisions = new int[textureWidth * textureHeight];
        flashlightLineOfSightCacheValues = new bool[textureWidth * textureHeight];
        RebuildSamplePointCache();
        ResetLineOfSightCaches();
        ResetPartialRefreshBoundsCache();
        forceFullRefresh = true;
        ResetFogToBaseline();
    }

    private void ReleaseTextureResources()
    {
        if (overlayRenderer != null && overlayRenderer.sprite == fogSprite)
        {
            overlayRenderer.sprite = null;
        }

        if (fogSprite != null)
        {
            DestroyRuntimeObject(fogSprite);
            fogSprite = null;
        }

        if (fogTexture != null)
        {
            DestroyRuntimeObject(fogTexture);
            fogTexture = null;
        }

        pixels = null;
        bakedVisibleCells = null;
        sampleWorldPoints = null;
        visibilityStates = null;
        textureWidth = 0;
        textureHeight = 0;
        hasUploadedFogTextureData = false;
        playerLineOfSightCacheRevisions = null;
        playerLineOfSightCacheValues = null;
        flashlightLineOfSightCacheRevisions = null;
        flashlightLineOfSightCacheValues = null;
        hasLineOfSightCachePose = false;
        partialRefreshBoundsByGroup = null;
        partialRefreshBoundsValidByGroup = null;
        InvalidateFlashlightQueryCache();
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }

    private bool TryResolveFlashlightPose(out WasdPlayerController activePlayerController, out Vector2 origin, out Vector2 forward)
    {
        activePlayerController = playerController;
        origin = default;
        forward = Vector2.up;

        if (activePlayerController == null || !activePlayerController.IsFlashlightSwitchOn)
        {
            return false;
        }

        Transform flashlightPivot = activePlayerController.FlashlightPivot;
        Vector2 playerPosition = activePlayerController.transform.position;
        Vector2 desiredOrigin = flashlightPivot != null ? (Vector2)flashlightPivot.position : playerPosition;
        forward = flashlightPivot != null ? (Vector2)flashlightPivot.up : activePlayerController.AimDirection;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = activePlayerController.AimDirection;
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.up;
        }

        forward.Normalize();
        desiredOrigin += forward * sightOriginForwardOffset;
        origin = ResolveFlashlightVisionOrigin(playerPosition, desiredOrigin, visibilityBlockingLayers.value);
        return true;
    }

    private Vector2 ResolveFlashlightVisionOrigin(Vector2 playerPosition, Vector2 desiredOrigin, int blockingMask)
    {
        Vector2 offset = desiredOrigin - playerPosition;

        if (offset.sqrMagnitude <= 0.0001f || blockingMask == 0)
        {
            return desiredOrigin;
        }

        float distance = offset.magnitude;
        Vector2 direction = offset / distance;
        float castDistance = Mathf.Max(0f, distance - raycastPadding);

        if (castDistance <= 0f)
        {
            return desiredOrigin;
        }

        RaycastHit2D hit = Physics2D.Raycast(playerPosition, direction, castDistance, blockingMask);

        if (hit.collider == null)
        {
            return desiredOrigin;
        }

        float safeDistance = Mathf.Max(0f, hit.distance - Mathf.Max(raycastPadding, 0.01f));
        return playerPosition + (direction * safeDistance);
    }

    private bool TryGetPixelIndex(Vector2 worldPoint, out int index)
    {
        float minX = worldCenter.x - (worldSize.x * 0.5f);
        float minY = worldCenter.y - (worldSize.y * 0.5f);
        float normalizedX = (worldPoint.x - minX) / worldSize.x;
        float normalizedY = (worldPoint.y - minY) / worldSize.y;

        if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
        {
            index = 0;
            return false;
        }

        int pixelX = Mathf.Clamp(Mathf.FloorToInt(normalizedX * textureWidth), 0, textureWidth - 1);
        int pixelY = Mathf.Clamp(Mathf.FloorToInt(normalizedY * textureHeight), 0, textureHeight - 1);
        index = pixelX + (pixelY * textureWidth);
        return true;
    }

    private float ApplyDarknessLayers(float alpha, FogVisibilityState state)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        int layers = state == FogVisibilityState.Visible ? 1 : darknessFilterLayers;
        return 1f - Mathf.Pow(1f - clampedAlpha, Mathf.Max(1, layers));
    }

    private float ResolveFlashlightRevealHalfAngle(float effectiveOuterAngle)
    {
        float widenedOuterAngle = effectiveOuterAngle + Mathf.Max(0f, flashlightRevealAnglePadding);
        return Mathf.Max(0.01f, widenedOuterAngle * 0.5f);
    }

    private void EnsureOverlayRenderer()
    {
        if (overlayRenderer == null)
        {
            overlayRenderer = GetComponent<SpriteRenderer>();
        }

        if (overlayRenderer != null)
        {
            overlayRenderer.sharedMaterial = GetOverlayMaterial();
        }
    }

    private static Material GetOverlayMaterial()
    {
        if (sharedOverlayMaterial != null)
        {
            return sharedOverlayMaterial;
        }

        sharedOverlayMaterial = Resources.Load<Material>(OverlayMaterialResourcePath);

        if (sharedOverlayMaterial != null)
        {
            return sharedOverlayMaterial;
        }

        Shader overlayShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (overlayShader == null)
        {
            overlayShader = Shader.Find("Sprites/Default");
        }

        if (overlayShader == null)
        {
            return null;
        }

        sharedOverlayMaterial = new Material(overlayShader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedOverlayMaterial;
    }

    private bool HasVisibilityLineOfSight(
        Vector2 origin,
        Vector2 samplePoint,
        Vector2 direction,
        float distance,
        int blockingMask)
    {
        if (blockingMask == 0)
        {
            return true;
        }

        if (CanUseGridLineOfSight(blockingMask, out bool ignoreClosedDoors))
        {
            return gridMapService.HasLineOfSight(origin, samplePoint, ignoreClosedDoors);
        }

        float castDistance = Mathf.Max(0f, distance - raycastPadding);
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, castDistance, blockingMask);
        return hit.collider == null;
    }

    private bool CanUseGridLineOfSight(int blockingMask, out bool ignoreClosedDoors)
    {
        ignoreClosedDoors = false;

        if (gridMapService == null)
        {
            return false;
        }

        int mapBlockingMask = gridMapService.VisionBlockingLayers.value;

        if (blockingMask == mapBlockingMask)
        {
            return true;
        }

        int doorLayerIndex = GameLayers.DoorIndex;

        if (doorLayerIndex < 0 || doorLayerIndex >= 32)
        {
            return false;
        }

        int doorLayerBit = 1 << doorLayerIndex;

        if ((mapBlockingMask & doorLayerBit) == 0)
        {
            return false;
        }

        if (blockingMask == (mapBlockingMask & ~doorLayerBit))
        {
            ignoreClosedDoors = true;
            return true;
        }

        return false;
    }
}

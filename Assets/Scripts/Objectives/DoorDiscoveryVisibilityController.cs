using System;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class DoorDiscoveryVisibilityController : MonoBehaviour, IFogOfWarOverlayConsumer
{
    private const int DoorAboveFogSortingOrder = 110;
    private const float FrontFaceSampleOffset = 0.08f;
    private const float FrontFaceTangentOffsetMin = 0.08f;
    private const float FrontFaceTangentOffsetMax = 0.34f;
    private const float SceneRequirementRetryIntervalSeconds = 0.25f;
    [SerializeField, FormerlySerializedAs("fogOfWarOverlay")] private MonoBehaviour fogVisibilityServiceSource;
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private SpriteRenderer[] controlledRenderers = Array.Empty<SpriteRenderer>();
    [SerializeField, Min(0f)] private float visibilityPollInterval = 0.12f;
    [SerializeField, Min(0f)] private float movementRefreshThreshold = 0.1f;
    private Vector3 lastSamplePosition;
    private float nextPollTime;
    private float nextSceneRequirementRefreshTime;
    private string cachedSceneIdentity = string.Empty;
    private bool hasSample;
    private bool requiresFlashlightDiscovery;
    private bool renderersCached;
    private bool controlledColliderCached;
    private Collider2D controlledCollider;
    private IFogVisibilityService fogVisibilityService;

    public void Initialize(SpriteRenderer renderer, Color baseColor)
    {
        Initialize(renderer != null ? new[] { renderer } : Array.Empty<SpriteRenderer>());
    }

    public void Initialize(SpriteRenderer[] renderers)
    {
        controlledRenderers = FilterRenderers(renderers);
        renderersCached = true;
        ApplyCurrentState();
    }

    public void BindFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        SetFogVisibilityService(boundFogVisibilityService);
        ApplyCurrentState();
    }

    public void BindFogOfWarOverlay(FlashlightFogOfWarOverlay overlay)
    {
        BindFogVisibilityService(overlay);
    }

    public void Configure(WasdPlayerController boundPlayerController, IFogVisibilityService boundFogVisibilityService)
    {
        SetPlayerController(boundPlayerController);
        SetFogVisibilityService(boundFogVisibilityService);
        ApplyCurrentState();
    }

    public void BindPlayerController(WasdPlayerController boundPlayerController)
    {
        SetPlayerController(boundPlayerController);
        ApplyCurrentState();
    }

    public void SetDiscoveredColor(Color color)
    {
        ApplyCurrentState();
    }

    public void SetUndiscoveredColor(Color color)
    {
        ApplyCurrentState();
    }

    public void ResetDiscovery()
    {
        hasSample = false;
        nextPollTime = 0f;
        ApplyCurrentState();
    }

    private void Awake()
    {
        RefreshSceneDiscoveryRequirement();
        RefreshFogVisibilityServiceReference();
        controlledColliderCached = false;
        EnsureControlledRenderers();
    }

    private void OnEnable()
    {
        RefreshSceneDiscoveryRequirement();
        RefreshFogVisibilityServiceReference();
        controlledColliderCached = false;
        EnsureControlledRenderers();
        hasSample = false;
        nextPollTime = 0f;
        ApplyCurrentState();
    }

    private void OnTransformChildrenChanged()
    {
        renderersCached = false;
        controlledColliderCached = false;
        hasSample = false;
    }

    private void LateUpdate()
    {
        if (!ShouldRefresh())
        {
            return;
        }

        ApplyCurrentState();
    }

    private bool ShouldRefresh()
    {
        if (!RequiresFlashlightDiscovery())
        {
            return !hasSample;
        }

        if (!hasSample || Time.unscaledTime >= nextPollTime)
        {
            return true;
        }

        float movementThresholdSquared = movementRefreshThreshold * movementRefreshThreshold;
        return (transform.position - lastSamplePosition).sqrMagnitude >= movementThresholdSquared;
    }

    private void ApplyCurrentState()
    {
        EnsureControlledRenderers();

        if (controlledRenderers == null || controlledRenderers.Length == 0)
        {
            return;
        }

        if (!RequiresFlashlightDiscovery())
        {
            ApplyRenderVisibility(true);
            StampSample();
            return;
        }

        if (IsDebugVisibilityBypassed())
        {
            ApplyRenderVisibility(true);
            StampSample();
            return;
        }

        ApplyRenderVisibility(IsVisibleInFlashlight());

        StampSample();
    }

    private void StampSample()
    {
        bool firstSample = !hasSample;
        float pollInterval = Mathf.Max(0f, visibilityPollInterval);
        lastSamplePosition = transform.position;
        nextPollTime = Time.unscaledTime + pollInterval + (firstSample ? ResolvePollPhaseOffset(pollInterval) : 0f);
        hasSample = true;
    }

    private float ResolvePollPhaseOffset(float pollInterval)
    {
        if (pollInterval <= 0f)
        {
            return 0f;
        }

        return Mathf.Repeat(Mathf.Abs(GetInstanceID()) * 0.00137f, pollInterval);
    }

    private bool RequiresFlashlightDiscovery()
    {
        RefreshSceneDiscoveryRequirement();
        return requiresFlashlightDiscovery;
    }

    private bool IsVisibleInFlashlight()
    {
        if (!RequiresFlashlightDiscovery())
        {
            return true;
        }

        if (fogVisibilityService == null)
        {
            return false;
        }

        if (!TryGetControlledBounds(out Bounds bounds))
        {
            return false;
        }

        Vector2 samplePoint = bounds.center;

        if (IsSamplePointVisible(samplePoint))
        {
            return true;
        }

        WasdPlayerController playerController = ResolvePlayerController();

        if (playerController == null)
        {
            return false;
        }

        Vector2 playerPosition = playerController.transform.position;
        Vector2 frontCenter = bounds.ClosestPoint(playerPosition);
        Vector2 towardPlayer = playerPosition - frontCenter;

        if (towardPlayer.sqrMagnitude <= 0.0001f)
        {
            towardPlayer = playerPosition - (Vector2)bounds.center;
        }

        if (towardPlayer.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        towardPlayer.Normalize();
        Vector2 frontSamplePoint = frontCenter + (towardPlayer * FrontFaceSampleOffset);

        if (IsSamplePointVisible(frontSamplePoint))
        {
            return true;
        }

        Vector2 tangent = new(-towardPlayer.y, towardPlayer.x);
        float tangentOffset = Mathf.Clamp(
            Mathf.Max(bounds.extents.x, bounds.extents.y) * 0.45f,
            FrontFaceTangentOffsetMin,
            FrontFaceTangentOffsetMax);

        Vector2 frontPositiveSample = frontSamplePoint + (tangent * tangentOffset);
        Vector2 frontNegativeSample = frontSamplePoint - (tangent * tangentOffset);

        return IsSamplePointVisible(frontPositiveSample)
            || IsSamplePointVisible(frontNegativeSample)
            || IsVisibleFromPlayerFlashlight(samplePoint)
            || IsVisibleFromPlayerFlashlight(frontSamplePoint)
            || IsVisibleFromPlayerFlashlight(frontPositiveSample)
            || IsVisibleFromPlayerFlashlight(frontNegativeSample);
    }

    private bool IsDebugVisibilityBypassed()
    {
        return fogVisibilityService != null && fogVisibilityService.BypassEnabled;
    }

    private bool IsSamplePointVisible(Vector2 samplePoint)
    {
        return fogVisibilityService != null
            && fogVisibilityService.IsWorldPointVisible(samplePoint);
    }

    private bool IsVisibleFromPlayerFlashlight(Vector2 samplePoint)
    {
        return fogVisibilityService != null
            && fogVisibilityService.SampleFlashlightVisibility(samplePoint, 0f, ignoreDoorLayer: true) > 0f;
    }

    private void RefreshFogVisibilityServiceReference()
    {
        if (fogVisibilityServiceSource is IFogVisibilityService boundFogVisibilityService
            && fogVisibilityServiceSource != null
            && fogVisibilityServiceSource.gameObject.scene == gameObject.scene)
        {
            SetFogVisibilityService(boundFogVisibilityService);
            return;
        }

        SetFogVisibilityService(RSceneReferenceLookup.FindUniqueComponentInScene<FlashlightFogOfWarOverlay>(
            gameObject.scene,
            this,
            nameof(DoorDiscoveryVisibilityController),
            nameof(fogVisibilityServiceSource)));
    }

    private void SetFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        fogVisibilityService = boundFogVisibilityService;
        fogVisibilityServiceSource = boundFogVisibilityService as MonoBehaviour;
    }

    private void RefreshSceneDiscoveryRequirement()
    {
        Scene scene = gameObject.scene;
        string sceneIdentity = MainEscapeSceneIdentityUtility.GetSceneIdentity(scene);

        if (sceneIdentity == cachedSceneIdentity)
        {
            if (requiresFlashlightDiscovery
                || !Application.isPlaying
                || Time.unscaledTime < nextSceneRequirementRefreshTime)
            {
                return;
            }
        }

        cachedSceneIdentity = sceneIdentity;
        requiresFlashlightDiscovery = RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(scene);
        nextSceneRequirementRefreshTime = !requiresFlashlightDiscovery && Application.isPlaying
            ? Time.unscaledTime + SceneRequirementRetryIntervalSeconds
            : 0f;
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
            nameof(DoorDiscoveryVisibilityController),
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

    private void EnsureControlledRenderers()
    {
        if (renderersCached && controlledRenderers != null)
        {
            return;
        }

        controlledRenderers = FilterRenderers(controlledRenderers != null && controlledRenderers.Length > 0
            ? controlledRenderers
            : GetComponentsInChildren<SpriteRenderer>(true));
        renderersCached = true;
    }

    private void ApplyRenderVisibility(bool enabled)
    {
        for (int index = 0; index < controlledRenderers.Length; index++)
        {
            SpriteRenderer renderer = controlledRenderers[index];

            if (renderer == null)
            {
                continue;
            }

            if (enabled)
            {
                ApplyAboveFogSorting(renderer);
            }

            if (renderer.enabled != enabled)
            {
                renderer.enabled = enabled;
            }
        }
    }

    private static void ApplyAboveFogSorting(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, DoorAboveFogSortingOrder);
        renderer.maskInteraction = SpriteMaskInteraction.None;
    }

    private bool TryGetControlledBounds(out Bounds bounds)
    {
        Collider2D doorCollider = ResolveControlledCollider();

        if (doorCollider != null)
        {
            bounds = doorCollider.bounds;
            return bounds.size.sqrMagnitude > 0.0001f;
        }

        bounds = default;
        bool hasBounds = false;

        for (int index = 0; index < controlledRenderers.Length; index++)
        {
            SpriteRenderer renderer = controlledRenderers[index];

            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds.min);
            bounds.Encapsulate(renderer.bounds.max);
        }

        return hasBounds;
    }

    private Collider2D ResolveControlledCollider()
    {
        if (!controlledColliderCached)
        {
            controlledCollider = GetComponent<Collider2D>();
            controlledColliderCached = true;
        }

        return controlledCollider;
    }

    private static SpriteRenderer[] FilterRenderers(SpriteRenderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return Array.Empty<SpriteRenderer>();
        }

        int validCount = 0;

        for (int index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null && renderers[index].sprite != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return Array.Empty<SpriteRenderer>();
        }

        SpriteRenderer[] filtered = new SpriteRenderer[validCount];
        int writeIndex = 0;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            filtered[writeIndex++] = renderer;
        }

        return filtered;
    }
}

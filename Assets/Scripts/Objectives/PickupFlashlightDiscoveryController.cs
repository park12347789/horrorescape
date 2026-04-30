using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PickupFlashlightDiscoveryController : MonoBehaviour, IFogOfWarOverlayConsumer
{
    private const float SceneRequirementRetryIntervalSeconds = 0.25f;

    [SerializeField, FormerlySerializedAs("fogOfWarOverlay")] private MonoBehaviour fogVisibilityServiceSource;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color sourceColor = Color.white;
    [SerializeField, Min(0f)] private float visibilityPollInterval = 0.12f;
    [SerializeField, Min(0f)] private float movementRefreshThreshold = 0.1f;

    private Vector3 lastSamplePosition;
    private float nextPollTime;
    private float nextSceneRequirementRefreshTime;
    private string cachedSceneIdentity = string.Empty;
    private bool hasSample;
    private bool requiresFlashlightDiscovery;
    private IFogVisibilityService fogVisibilityService;

    public void Initialize(SpriteRenderer renderer, Color baseColor)
    {
        spriteRenderer = renderer != null ? renderer : GetComponent<SpriteRenderer>();
        sourceColor = baseColor;

        ForceRefresh();
    }

    public void BindFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        SetFogVisibilityService(boundFogVisibilityService);
        ForceRefresh();
    }

    public void BindFogOfWarOverlay(FlashlightFogOfWarOverlay overlay)
    {
        BindFogVisibilityService(overlay);
    }

    public void SetSourceColor(Color baseColor)
    {
        sourceColor = baseColor;

        if (spriteRenderer != null && (spriteRenderer.enabled || !RequiresFlashlightDiscovery()))
        {
            spriteRenderer.color = sourceColor;
        }
    }

    public void ResetDiscovery()
    {
        ForceRefresh();
    }

    private void Awake()
    {
        RefreshSceneDiscoveryRequirement();
        RefreshFogVisibilityServiceReference();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        RefreshSceneDiscoveryRequirement();
        RefreshFogVisibilityServiceReference();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        hasSample = false;
        nextPollTime = 0f;
        ForceRefresh();
    }

    private void OnDisable()
    {
        hasSample = false;
        nextPollTime = 0f;
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

        if (Time.unscaledTime >= nextPollTime)
        {
            return true;
        }

        if (!hasSample)
        {
            return true;
        }

        float movementThresholdSquared = movementRefreshThreshold * movementRefreshThreshold;
        return (transform.position - lastSamplePosition).sqrMagnitude >= movementThresholdSquared;
    }

    private void ForceRefresh()
    {
        nextPollTime = 0f;
        hasSample = false;
        ApplyCurrentState();
    }

    private void ApplyCurrentState()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            return;
        }

        if (!RequiresFlashlightDiscovery())
        {
            ApplyRendererState(true, sourceColor);
            StampSample();
            return;
        }

        if (IsDebugVisibilityBypassed())
        {
            ApplyRendererState(true, sourceColor);
            StampSample();
            return;
        }

        bool currentlyVisible = IsVisibleToPlayer();
        ApplyRendererEnabled(currentlyVisible);

        if (currentlyVisible)
        {
            ApplyRendererColor(sourceColor);
        }

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

    private bool IsVisibleToPlayer()
    {
        if (!RequiresFlashlightDiscovery())
        {
            return true;
        }

        if (fogVisibilityService == null)
        {
            return false;
        }

        Vector2 samplePoint = spriteRenderer != null
            ? (Vector2)spriteRenderer.bounds.center
            : (Vector2)transform.position;
        return fogVisibilityService.IsWorldPointVisible(samplePoint)
            || fogVisibilityService.SampleFlashlightVisibility(samplePoint, 0f, ignoreDoorLayer: true) > 0f;
    }

    private bool IsDebugVisibilityBypassed()
    {
        return fogVisibilityService != null && fogVisibilityService.BypassEnabled;
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
            nameof(PickupFlashlightDiscoveryController),
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

    private void ApplyRendererState(bool enabled, Color color)
    {
        ApplyRendererEnabled(enabled);

        if (enabled)
        {
            ApplyRendererColor(color);
        }
    }

    private void ApplyRendererEnabled(bool enabled)
    {
        if (spriteRenderer.enabled != enabled)
        {
            spriteRenderer.enabled = enabled;
        }
    }

    private void ApplyRendererColor(Color color)
    {
        if (spriteRenderer.color != color)
        {
            spriteRenderer.color = color;
        }
    }
}

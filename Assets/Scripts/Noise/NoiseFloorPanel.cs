/*
 * File Role:
 * Implements the scattered-glass floor trap that makes noise when crossed.
 *
 * Runtime Use:
 * Creates a loud floor event only while the player is actually moving across the panel.
 *
 * Study Notes:
 * Useful when studying event-based sound triggers instead of constant area effects.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[SelectionBase]
public sealed class NoiseFloorPanel : MonoBehaviour, IFogOfWarOverlayConsumer
{
    private const string ReflectionRendererName = "GlassReflection";
    private const float EditorPreviewReflectionAlpha = 0.06f;
    private const float DefaultSharedActorVisualPulseInterval = 1.45f;

    [SerializeField] private Vector2 panelSize = new(1.1f, 1.1f);
    [SerializeField] private Color panelColor = new(0.9f, 0.98f, 1f, 0.1f);
    [SerializeField] private Color frameColor = new(0.82f, 0.97f, 1f, 0.48f);
    [SerializeField] private Color reflectionColor = new(0.94f, 0.99f, 1f, 0.96f);
    [SerializeField, Min(0f)] private float noiseRadius = 5f;
    [SerializeField, Min(0.05f)] private float stepNoiseInterval = 0.24f;
    [SerializeField, Min(0f)] private float sharedActorVisualPulseInterval = DefaultSharedActorVisualPulseInterval;
    [SerializeField, Min(0.01f)] private float minimumMovementSpeed = 0.2f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 11;
    [SerializeField, Range(0f, 1f)] private float idleReflectionAlpha = 0.015f;
    [SerializeField, Range(0f, 1f)] private float flashlightReflectionAlpha = 0.92f;
    [SerializeField, Min(0.1f)] private float reflectionPulseSpeed = 3.2f;
    [SerializeField, Min(0.1f)] private float reflectionLerpSpeed = 5.6f;
    [SerializeField, Min(0f)] private float flashlightReachPadding = 0.5f;
    [SerializeField, Min(0f)] private float flashlightRaycastPadding = 0.04f;
    [SerializeField, Min(0.02f)] private float reflectionInfluenceRefreshInterval = 0.06f;
    [SerializeField, Min(0f)] private float reflectionMovementRefreshThreshold = 0.05f;
    [SerializeField, FormerlySerializedAs("fogOfWarOverlay")] private MonoBehaviour fogVisibilityServiceSource;
    [SerializeField] private WasdPlayerController explicitPlayerController;
    [SerializeField] private NoiseSystem noiseSystem;
    [SerializeField] private SpriteRenderer reflectionRenderer;

    private static Sprite sharedPanelSprite;
    private static Sprite sharedReflectionSprite;
    // Shared across authored glass panels so visual throttling follows the actor, not one panel instance.
    private static readonly Dictionary<int, float> nextSharedVisualPulseTimesByActorId = new();
    private readonly Dictionary<int, ActorCacheEntry> actorCacheByColliderId = new();
    private readonly Dictionary<int, float> nextAllowedStepTimes = new();
    private readonly Dictionary<int, Vector2> lastObservedActorPositions = new();
    private readonly Dictionary<int, float> lastObservedActorTimes = new();
    private SpriteRenderer spriteRenderer;
    private WasdPlayerController playerController;
    private INoiseEventBus noiseEventBus;
    private float presentedReflectionInfluence;
    private IFogVisibilityService fogVisibilityService;
    private bool runtimeSetupApplied;
    private bool hasReflectionInfluenceSample;
    private bool lastReflectionSampleFlashlightOn;
    private float cachedTargetReflectionInfluence;
    private float nextReflectionInfluenceRefreshTime;
    private Vector2 lastReflectionSamplePlayerPosition;
    private Vector2 lastReflectionSampleAimDirection;
#if UNITY_EDITOR
    private bool editorRefreshQueued;
#endif

    private void Reset()
    {
        RefreshFogVisibilityServiceReference();
        EnsureSetup();
    }

    private void OnEnable()
    {
        RefreshFogVisibilityServiceReference();
        EnsureSetup();
    }

    private void OnDisable()
    {
        actorCacheByColliderId.Clear();
        nextAllowedStepTimes.Clear();
        lastObservedActorPositions.Clear();
        lastObservedActorTimes.Clear();
        runtimeSetupApplied = false;
        InvalidateReflectionInfluenceSample();

#if UNITY_EDITOR
        CancelQueuedEditorRefresh();
#endif
    }

    private void OnValidate()
    {
        RefreshFogVisibilityServiceReference();
        panelSize.x = Mathf.Max(0.1f, panelSize.x);
        panelSize.y = Mathf.Max(0.1f, panelSize.y);
        noiseRadius = Mathf.Max(0f, noiseRadius);
        stepNoiseInterval = Mathf.Max(0.05f, stepNoiseInterval);
        sharedActorVisualPulseInterval = Mathf.Max(0f, sharedActorVisualPulseInterval);
        minimumMovementSpeed = Mathf.Max(0.01f, minimumMovementSpeed);
        reflectionPulseSpeed = Mathf.Max(0.1f, reflectionPulseSpeed);
        reflectionLerpSpeed = Mathf.Max(0.1f, reflectionLerpSpeed);
        flashlightReachPadding = Mathf.Max(0f, flashlightReachPadding);
        flashlightRaycastPadding = Mathf.Max(0f, flashlightRaycastPadding);
        reflectionInfluenceRefreshInterval = Mathf.Max(0.02f, reflectionInfluenceRefreshInterval);
        reflectionMovementRefreshThreshold = Mathf.Max(0f, reflectionMovementRefreshThreshold);
        InvalidateReflectionInfluenceSample();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorRefresh();
            return;
        }
#endif

        if (isActiveAndEnabled)
        {
            EnsureSetup();
            UpdateReflectionPresentation();
        }
    }

    public void Configure(Vector2 size, Color color, float radius)
    {
        panelSize = size;
        panelColor = color;
        noiseRadius = Mathf.Max(0f, radius);
        EnsureSetup();
    }

    public void ConfigurePlayerController(WasdPlayerController configuredPlayerController)
    {
        explicitPlayerController = configuredPlayerController;
        playerController = configuredPlayerController;
        InvalidateReflectionInfluenceSample();
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
    }

    public void BindFogVisibilityService(IFogVisibilityService boundFogVisibilityService)
    {
        fogVisibilityService = boundFogVisibilityService;
        fogVisibilityServiceSource = boundFogVisibilityService as MonoBehaviour;
        InvalidateReflectionInfluenceSample();
    }

    public void BindFogOfWarOverlay(FlashlightFogOfWarOverlay overlay)
    {
        BindFogVisibilityService(overlay);
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureSetup();
            UpdateReflectionPresentation();
            return;
        }
#endif

        if (!runtimeSetupApplied)
        {
            EnsureSetup();
        }

        UpdateReflectionPresentation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryEmit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryEmit(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        int colliderId = other.GetInstanceID();
        actorCacheByColliderId.Remove(colliderId);

        if (!TryResolveActorId(other, out int actorId))
        {
            actorId = other.attachedRigidbody != null ? other.attachedRigidbody.GetInstanceID() : other.GetInstanceID();
        }

        nextAllowedStepTimes.Remove(actorId);
        lastObservedActorPositions.Remove(actorId);
        lastObservedActorTimes.Remove(actorId);
    }

    private void TryEmit(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (!TryResolveActor(other, out int actorId, out Vector2 actorWorldPosition, out float speed, out NoiseEmitterAffiliation emitterAffiliation))
        {
            return;
        }

        if (speed < minimumMovementSpeed)
        {
            return;
        }

        if (nextAllowedStepTimes.TryGetValue(actorId, out float nextAllowedTime) && Time.time < nextAllowedTime)
        {
            return;
        }

        bool allowVisualPulse = ShouldRequestSharedLoudFloorVisualPulse(actorId);
        INoiseEventBus eventBus = ResolveNoiseEventBus();
        eventBus?.TryEmitNoise(
            actorWorldPosition,
            noiseRadius,
            NoiseSourceType.LoudFloor,
            actorId,
            emitterAffiliation,
            allowDebugPulse: allowVisualPulse);
        PrototypeAudioManager.TryPlayNoiseTrap(Mathf.InverseLerp(minimumMovementSpeed, 3.1f, speed));
        nextAllowedStepTimes[actorId] = Time.time + stepNoiseInterval;
    }

    private bool ShouldRequestSharedLoudFloorVisualPulse(int actorId)
    {
        float visualInterval = Mathf.Max(0f, sharedActorVisualPulseInterval);

        if (visualInterval <= 0f)
        {
            return true;
        }

        float currentTime = Time.time;

        if (nextSharedVisualPulseTimesByActorId.TryGetValue(actorId, out float nextAllowedTime)
            && currentTime < nextAllowedTime)
        {
            return false;
        }

        nextSharedVisualPulseTimesByActorId[actorId] = currentTime + visualInterval;
        return true;
    }

    private bool TryResolveActor(
        Collider2D other,
        out int actorId,
        out Vector2 actorWorldPosition,
        out float speed,
        out NoiseEmitterAffiliation emitterAffiliation)
    {
        actorId = 0;
        actorWorldPosition = Vector2.zero;
        speed = 0f;
        emitterAffiliation = NoiseEmitterAffiliation.Neutral;

        if (!TryResolveActorEntry(other, out ActorCacheEntry entry))
        {
            return false;
        }

        actorId = entry.ActorId;
        actorWorldPosition = entry.ActorTransform.position;
        emitterAffiliation = entry.Affiliation;

        if (entry.Player != null)
        {
            speed = entry.Player.Velocity.magnitude;
        }
        else
        {
            speed = ResolveActorMovementSpeed(actorId, actorWorldPosition, entry.Body);
        }

        return true;
    }

    private bool TryResolveActorId(Collider2D other, out int actorId)
    {
        actorId = 0;

        if (!TryResolveActorEntry(other, out ActorCacheEntry entry))
        {
            return false;
        }

        actorId = entry.ActorId;
        return true;
    }

    private bool TryResolveActorEntry(Collider2D other, out ActorCacheEntry entry)
    {
        entry = default;

        if (other == null)
        {
            return false;
        }

        int colliderId = other.GetInstanceID();

        if (actorCacheByColliderId.TryGetValue(colliderId, out entry) && entry.IsValid)
        {
            return true;
        }

        WasdPlayerController resolvedPlayer = ResolvePlayerActor(other);

        if (resolvedPlayer != null)
        {
            entry = ActorCacheEntry.ForPlayer(resolvedPlayer);
            actorCacheByColliderId[colliderId] = entry;
            return true;
        }

        EnemyStateMachine groundEnemy = ResolveGroundEnemyActor(other);

        if (groundEnemy != null)
        {
            entry = ActorCacheEntry.ForEnemy(groundEnemy.transform, groundEnemy.GetComponent<Rigidbody2D>());
            actorCacheByColliderId[colliderId] = entry;
            return true;
        }

        CeilingVentEnemyController ventEnemy = ResolveVentEnemyActor(other);

        if (ventEnemy != null)
        {
            entry = ActorCacheEntry.ForEnemy(ventEnemy.transform, ventEnemy.GetComponent<Rigidbody2D>());
            actorCacheByColliderId[colliderId] = entry;
            return true;
        }

        actorCacheByColliderId.Remove(colliderId);
        return false;
    }

    private float ResolveActorMovementSpeed(int actorId, Vector2 currentPosition, Rigidbody2D actorBody)
    {
        float currentTime = Time.time;

        if (actorBody != null)
        {
            lastObservedActorPositions[actorId] = currentPosition;
            lastObservedActorTimes[actorId] = currentTime;
            return actorBody.linearVelocity.magnitude;
        }

        if (!lastObservedActorPositions.TryGetValue(actorId, out Vector2 previousPosition)
            || !lastObservedActorTimes.TryGetValue(actorId, out float previousTime))
        {
            lastObservedActorPositions[actorId] = currentPosition;
            lastObservedActorTimes[actorId] = currentTime;
            return 0f;
        }

        lastObservedActorPositions[actorId] = currentPosition;
        lastObservedActorTimes[actorId] = currentTime;

        float deltaTime = currentTime - previousTime;

        if (deltaTime <= 0.0001f)
        {
            return 0f;
        }

        return Vector2.Distance(previousPosition, currentPosition) / deltaTime;
    }

    private static WasdPlayerController ResolvePlayerActor(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        WasdPlayerController resolvedPlayer = other.GetComponent<WasdPlayerController>();

        if (resolvedPlayer != null)
        {
            return resolvedPlayer;
        }

        if (other.attachedRigidbody != null)
        {
            resolvedPlayer = other.attachedRigidbody.GetComponent<WasdPlayerController>();

            if (resolvedPlayer != null)
            {
                return resolvedPlayer;
            }
        }

        return other.GetComponentInParent<WasdPlayerController>();
    }

    private static EnemyStateMachine ResolveGroundEnemyActor(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        EnemyStateMachine groundEnemy = other.GetComponent<EnemyStateMachine>();

        if (groundEnemy != null)
        {
            return groundEnemy;
        }

        if (other.attachedRigidbody != null)
        {
            groundEnemy = other.attachedRigidbody.GetComponent<EnemyStateMachine>();

            if (groundEnemy != null)
            {
                return groundEnemy;
            }
        }

        return other.GetComponentInParent<EnemyStateMachine>();
    }

    private static CeilingVentEnemyController ResolveVentEnemyActor(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        CeilingVentEnemyController ventEnemy = other.GetComponent<CeilingVentEnemyController>();

        if (ventEnemy != null)
        {
            return ventEnemy;
        }

        if (other.attachedRigidbody != null)
        {
            ventEnemy = other.attachedRigidbody.GetComponent<CeilingVentEnemyController>();

            if (ventEnemy != null)
            {
                return ventEnemy;
            }
        }

        return other.GetComponentInParent<CeilingVentEnemyController>();
    }

    private void EnsureSetup()
    {
        if (GameLayers.GroundIndex >= 0)
        {
            gameObject.layer = GameLayers.GroundIndex;
        }

        transform.localScale = Vector3.one;

        spriteRenderer ??= GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = ResolvePanelSprite();
        spriteRenderer.color = panelColor;
        spriteRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName) ? "Default" : sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        spriteRenderer.size = panelSize;
        spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
        spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        spriteRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        spriteRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        reflectionRenderer = ResolveReflectionRenderer();

        if (reflectionRenderer != null)
        {
            reflectionRenderer.gameObject.layer = gameObject.layer;
            reflectionRenderer.sprite = ResolveReflectionSprite();
            reflectionRenderer.color = ResolveReflectionColor(idleReflectionAlpha);
            reflectionRenderer.sortingLayerName = spriteRenderer.sortingLayerName;
            reflectionRenderer.sortingOrder = sortingOrder + 1;
            reflectionRenderer.drawMode = SpriteDrawMode.Sliced;
            reflectionRenderer.size = new Vector2(
                Mathf.Max(0.1f, panelSize.x * 0.98f),
                Mathf.Max(0.1f, panelSize.y * 0.98f));
            reflectionRenderer.maskInteraction = SpriteMaskInteraction.None;
            reflectionRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            reflectionRenderer.receiveShadows = false;
            reflectionRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            reflectionRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();

        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.size = panelSize;
        triggerCollider.offset = Vector2.zero;
        runtimeSetupApplied = Application.isPlaying;
    }

#if UNITY_EDITOR
    private void QueueEditorRefresh()
    {
        if (editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = true;
        EditorApplication.delayCall += ApplyQueuedEditorRefresh;
    }

    private void CancelQueuedEditorRefresh()
    {
        if (!editorRefreshQueued)
        {
            return;
        }

        EditorApplication.delayCall -= ApplyQueuedEditorRefresh;
        editorRefreshQueued = false;
    }

    private void ApplyQueuedEditorRefresh()
    {
        EditorApplication.delayCall -= ApplyQueuedEditorRefresh;
        editorRefreshQueued = false;

        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        EnsureSetup();
        UpdateReflectionPresentation();
    }
#endif

    private SpriteRenderer ResolveReflectionRenderer()
    {
        if (reflectionRenderer != null)
        {
            return reflectionRenderer;
        }

        Transform reflectionTransform = transform.Find(ReflectionRendererName);

        if (reflectionTransform == null)
        {
            GameObject reflectionObject = new(ReflectionRendererName);
            reflectionTransform = reflectionObject.transform;
            reflectionTransform.SetParent(transform, false);
            reflectionTransform.localPosition = Vector3.zero;
            reflectionTransform.localRotation = Quaternion.identity;
            reflectionTransform.localScale = Vector3.one;
        }

        reflectionRenderer = reflectionTransform.GetComponent<SpriteRenderer>();

        if (reflectionRenderer == null)
        {
            reflectionRenderer = reflectionTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        return reflectionRenderer;
    }

    private Sprite ResolvePanelSprite()
    {
        return spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.sprite
            : GetSharedPanelSprite();
    }

    private Sprite ResolveReflectionSprite()
    {
        return reflectionRenderer != null && reflectionRenderer.sprite != null
            ? reflectionRenderer.sprite
            : GetSharedReflectionSprite();
    }

    private void UpdateReflectionPresentation()
    {
        if (reflectionRenderer == null || spriteRenderer == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            spriteRenderer.color = panelColor;
            reflectionRenderer.color = ResolveReflectionColor(Mathf.Max(idleReflectionAlpha, EditorPreviewReflectionAlpha));
            return;
        }

        float targetInfluence = ResolveTargetReflectionInfluence();
        presentedReflectionInfluence = Mathf.MoveTowards(
            presentedReflectionInfluence,
            targetInfluence,
            Time.unscaledDeltaTime * reflectionLerpSpeed);

        float pulse = 1f + (Mathf.Sin(Time.unscaledTime * reflectionPulseSpeed) * 0.08f * presentedReflectionInfluence);
        float reflectionAlpha = Mathf.Lerp(idleReflectionAlpha, flashlightReflectionAlpha, presentedReflectionInfluence) * pulse;
        reflectionRenderer.color = ResolveReflectionColor(reflectionAlpha);
        spriteRenderer.color = Color.Lerp(panelColor, ResolveLitPanelColor(), presentedReflectionInfluence * 0.3f);
    }

    private float ResolveTargetReflectionInfluence()
    {
        if (ShouldRefreshReflectionInfluenceSample())
        {
            cachedTargetReflectionInfluence = EvaluateFlashlightInfluence();
            StampReflectionInfluenceSample();
        }

        return cachedTargetReflectionInfluence;
    }

    private bool ShouldRefreshReflectionInfluenceSample()
    {
        playerController = ResolvePlayerController(playerController, explicitPlayerController, gameObject);

        if (!hasReflectionInfluenceSample || Time.unscaledTime >= nextReflectionInfluenceRefreshTime)
        {
            return true;
        }

        if (playerController == null)
        {
            return false;
        }

        bool flashlightOn = playerController.IsFlashlightSwitchOn;

        if (flashlightOn != lastReflectionSampleFlashlightOn)
        {
            return true;
        }

        Vector2 playerPosition = playerController.transform.position;
        float movementThresholdSqr = reflectionMovementRefreshThreshold * reflectionMovementRefreshThreshold;

        if (movementThresholdSqr > 0f && (playerPosition - lastReflectionSamplePlayerPosition).sqrMagnitude >= movementThresholdSqr)
        {
            return true;
        }

        Vector2 aimDirection = ResolveFlashlightForward(playerController);
        return (aimDirection - lastReflectionSampleAimDirection).sqrMagnitude >= 0.004f;
    }

    private void StampReflectionInfluenceSample()
    {
        hasReflectionInfluenceSample = true;
        nextReflectionInfluenceRefreshTime = Time.unscaledTime + reflectionInfluenceRefreshInterval;

        if (playerController == null)
        {
            lastReflectionSampleFlashlightOn = false;
            lastReflectionSamplePlayerPosition = Vector2.zero;
            lastReflectionSampleAimDirection = Vector2.up;
            return;
        }

        lastReflectionSampleFlashlightOn = playerController.IsFlashlightSwitchOn;
        lastReflectionSamplePlayerPosition = playerController.transform.position;
        lastReflectionSampleAimDirection = ResolveFlashlightForward(playerController);
    }

    private void InvalidateReflectionInfluenceSample()
    {
        hasReflectionInfluenceSample = false;
        nextReflectionInfluenceRefreshTime = 0f;
    }

    private float EvaluateFlashlightInfluence()
    {
        playerController = ResolvePlayerController(playerController, explicitPlayerController, gameObject);

        if (playerController == null || !playerController.IsFlashlightSwitchOn)
        {
            return 0f;
        }

        Vector2 center = transform.position;
        Vector2 halfSize = new(panelSize.x * 0.28f, panelSize.y * 0.28f);
        float influence = 0f;

        influence = Mathf.Max(influence, EvaluateSampleInfluence(playerController, center));
        influence = Mathf.Max(influence, EvaluateSampleInfluence(playerController, center + new Vector2(halfSize.x, 0f)));
        influence = Mathf.Max(influence, EvaluateSampleInfluence(playerController, center + new Vector2(-halfSize.x, 0f)));
        influence = Mathf.Max(influence, EvaluateSampleInfluence(playerController, center + new Vector2(0f, halfSize.y)));
        influence = Mathf.Max(influence, EvaluateSampleInfluence(playerController, center + new Vector2(0f, -halfSize.y)));
        return influence;
    }

    private float EvaluateSampleInfluence(WasdPlayerController controller, Vector2 samplePoint)
    {
        if (controller == null)
        {
            return 0f;
        }

        if (fogVisibilityService != null)
        {
            return fogVisibilityService.SampleFlashlightVisibility(samplePoint, flashlightReachPadding, ignoreDoorLayer: true);
        }

        Transform flashlightPivot = controller.FlashlightPivot;
        Vector2 origin = flashlightPivot != null
            ? (Vector2)flashlightPivot.position
            : (Vector2)controller.transform.position;
        Vector2 forward = ResolveFlashlightForward(controller);

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = controller.AimDirection;
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector2.up;
        }

        forward.Normalize();
        Vector2 toSample = samplePoint - origin;
        float distance = toSample.magnitude;

        if (distance <= 0.0001f)
        {
            return 1f;
        }

        float maxDistance = Mathf.Max(0.1f, controller.EffectiveFlashlightRange + flashlightReachPadding);

        if (distance > maxDistance)
        {
            return 0f;
        }

        float halfAngle = Mathf.Max(0.01f, controller.EffectiveFlashlightOuterAngle * 0.5f);
        float normalizedAngle = Vector2.Angle(forward, toSample / distance);

        if (normalizedAngle > halfAngle)
        {
            return 0f;
        }

        int maskBits = fogVisibilityService != null
            ? fogVisibilityService.VisibilityBlockingLayers.value
            : GameLayers.VisionBlockingMask.value;
        int doorLayer = GameLayers.DoorIndex;

        if (doorLayer >= 0 && doorLayer < 32)
        {
            maskBits &= ~(1 << doorLayer);
        }

        if (maskBits != 0)
        {
            float castDistance = Mathf.Max(0f, distance - flashlightRaycastPadding);
            RaycastHit2D hit = Physics2D.Raycast(origin, toSample / distance, castDistance, maskBits);

            if (hit.collider != null)
            {
                return 0f;
            }
        }

        float distanceFactor = 1f - Mathf.Clamp01(distance / maxDistance);
        float angleFactor = 1f - Mathf.Clamp01(normalizedAngle / halfAngle);
        float flashlightFactor = Mathf.Clamp01(controller.FlashlightVisibilityScale);
        return Mathf.SmoothStep(0f, 1f, distanceFactor * angleFactor) * flashlightFactor;
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private static WasdPlayerController ResolvePlayerController(
        WasdPlayerController current,
        WasdPlayerController explicitPlayer,
        GameObject owner)
    {
        return AudioScenePlayerReferenceResolver.ResolveCurrentOrSceneFallback(
            explicitPlayer != null ? explicitPlayer : current,
            owner);
    }

    private static Vector2 ResolveFlashlightForward(WasdPlayerController controller)
    {
        if (controller == null)
        {
            return Vector2.up;
        }

        Transform flashlightPivot = controller.FlashlightPivot;
        Vector2 forward = flashlightPivot != null
            ? (Vector2)flashlightPivot.up
            : controller.AimDirection;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = controller.AimDirection;
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            return Vector2.up;
        }

        forward.Normalize();
        return forward;
    }

    private void RefreshFogVisibilityServiceReference()
    {
        fogVisibilityService = fogVisibilityServiceSource as IFogVisibilityService;
        InvalidateReflectionInfluenceSample();
    }

    private readonly struct ActorCacheEntry
    {
        private ActorCacheEntry(
            int actorId,
            Transform actorTransform,
            Rigidbody2D body,
            WasdPlayerController player,
            NoiseEmitterAffiliation affiliation)
        {
            ActorId = actorId;
            ActorTransform = actorTransform;
            Body = body;
            Player = player;
            Affiliation = affiliation;
        }

        public int ActorId { get; }
        public Transform ActorTransform { get; }
        public Rigidbody2D Body { get; }
        public WasdPlayerController Player { get; }
        public NoiseEmitterAffiliation Affiliation { get; }
        public bool IsValid => ActorTransform != null;

        public static ActorCacheEntry ForPlayer(WasdPlayerController player)
        {
            return new ActorCacheEntry(
                player.gameObject.GetInstanceID(),
                player.transform,
                null,
                player,
                NoiseEmitterAffiliation.Player);
        }

        public static ActorCacheEntry ForEnemy(Transform actorTransform, Rigidbody2D body)
        {
            return new ActorCacheEntry(
                actorTransform.gameObject.GetInstanceID(),
                actorTransform,
                body,
                null,
                NoiseEmitterAffiliation.Enemy);
        }
    }

    private Color ResolveLitPanelColor()
    {
        return new Color(
            Mathf.Lerp(panelColor.r, frameColor.r, 0.28f),
            Mathf.Lerp(panelColor.g, frameColor.g, 0.28f),
            Mathf.Lerp(panelColor.b, frameColor.b, 0.28f),
            Mathf.Lerp(panelColor.a, Mathf.Clamp01(panelColor.a + 0.08f), 0.28f));
    }

    private Color ResolveReflectionColor(float alpha)
    {
        return new Color(reflectionColor.r, reflectionColor.g, reflectionColor.b, Mathf.Clamp01(alpha));
    }

    private static float SampleShardMask(float nx, float ny)
    {
        float mask = 0f;
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.2f, 0.22f), new Vector2(0.1f, 0.06f), -26f, -0.18f, 0.74f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.4f, 0.3f), new Vector2(0.08f, 0.045f), 18f, 0.12f, 0.52f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.64f, 0.24f), new Vector2(0.11f, 0.055f), -12f, 0.2f, 0.68f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.78f, 0.44f), new Vector2(0.09f, 0.05f), 34f, -0.24f, 0.56f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.56f, 0.52f), new Vector2(0.13f, 0.06f), -38f, 0.17f, 0.8f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.31f, 0.56f), new Vector2(0.07f, 0.04f), 42f, -0.12f, 0.48f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.18f, 0.68f), new Vector2(0.095f, 0.05f), -4f, 0.08f, 0.66f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.46f, 0.74f), new Vector2(0.12f, 0.065f), 21f, -0.22f, 0.76f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.73f, 0.73f), new Vector2(0.1f, 0.055f), -18f, 0.14f, 0.58f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.86f, 0.18f), new Vector2(0.05f, 0.03f), 29f, -0.15f, 0.42f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.12f, 0.45f), new Vector2(0.045f, 0.024f), -33f, 0.1f, 0.4f));
        mask = Mathf.Max(mask, SampleShard(nx, ny, new Vector2(0.88f, 0.61f), new Vector2(0.045f, 0.025f), 11f, -0.08f, 0.46f));
        return Mathf.Clamp01(mask);
    }

    private static float SampleShard(float nx, float ny, Vector2 center, Vector2 size, float angleDegrees, float skew, float facetBias)
    {
        Vector2 local = RotatePoint(new Vector2(nx - center.x, ny - center.y), angleDegrees);
        local.x += local.y * skew;
        float normalizedX = Mathf.Abs(local.x) / Mathf.Max(0.0001f, size.x);
        float normalizedY = Mathf.Abs(local.y) / Mathf.Max(0.0001f, size.y);
        float body = 1f - normalizedX - normalizedY;

        if (body <= 0f)
        {
            return 0f;
        }

        float shard = Mathf.SmoothStep(0f, 1f, body);
        float facetA = Mathf.Clamp01(1f - Mathf.Abs((local.x * 0.92f) - (local.y * 0.34f)) / Mathf.Max(0.02f, size.x * 0.42f));
        float facetB = Mathf.Clamp01(1f - Mathf.Abs((local.x * -0.46f) - (local.y * 0.94f)) / Mathf.Max(0.02f, size.y * 0.38f));
        float ridge = Mathf.Clamp01(1f - Mathf.Abs((local.x * 0.24f) + (local.y * 1.12f)) / Mathf.Max(0.02f, size.y * 0.3f));
        float facets = Mathf.Max(facetA * facetBias, facetB * Mathf.Lerp(0.46f, 0.84f, 1f - facetBias));
        facets = Mathf.Max(facets, ridge * 0.36f);
        return shard * Mathf.Lerp(0.7f, 1f, facets);
    }

    private static float SampleReflectionGlint(float nx, float ny)
    {
        float glint = 0f;
        glint = Mathf.Max(glint, SampleGlintOnShard(nx, ny, new Vector2(0.2f, 0.22f), new Vector2(0.1f, 0.06f), -26f, 0.06f));
        glint = Mathf.Max(glint, SampleGlintOnShard(nx, ny, new Vector2(0.64f, 0.24f), new Vector2(0.11f, 0.055f), -12f, 0.048f));
        glint = Mathf.Max(glint, SampleGlintOnShard(nx, ny, new Vector2(0.56f, 0.52f), new Vector2(0.13f, 0.06f), -38f, 0.054f));
        glint = Mathf.Max(glint, SampleGlintOnShard(nx, ny, new Vector2(0.46f, 0.74f), new Vector2(0.12f, 0.065f), 21f, 0.052f));
        glint = Mathf.Max(glint, SampleGlintOnShard(nx, ny, new Vector2(0.73f, 0.73f), new Vector2(0.1f, 0.055f), -18f, 0.046f));
        glint = Mathf.Max(glint, SampleGlintOnShard(nx, ny, new Vector2(0.31f, 0.56f), new Vector2(0.07f, 0.04f), 42f, 0.04f));
        return Mathf.Clamp01(glint);
    }

    private static float SampleGlintOnShard(float nx, float ny, Vector2 center, Vector2 size, float angleDegrees, float bandWidth)
    {
        Vector2 local = RotatePoint(new Vector2(nx - center.x, ny - center.y), angleDegrees);
        float normalizedX = Mathf.Abs(local.x) / Mathf.Max(0.0001f, size.x);
        float normalizedY = Mathf.Abs(local.y) / Mathf.Max(0.0001f, size.y);
        float body = 1f - normalizedX - normalizedY;

        if (body <= 0f)
        {
            return 0f;
        }

        float shardMask = Mathf.SmoothStep(0f, 1f, body);
        float diagonalA = Mathf.Clamp01(1f - Mathf.Abs(local.x - (local.y * 0.62f)) / Mathf.Max(0.01f, bandWidth));
        float diagonalB = Mathf.Clamp01(1f - Mathf.Abs(local.x + (local.y * 0.4f)) / Mathf.Max(0.01f, bandWidth * 0.74f)) * 0.44f;
        return shardMask * Mathf.Max(diagonalA, diagonalB);
    }

    private static Vector2 RotatePoint(Vector2 value, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            (value.x * cosine) - (value.y * sine),
            (value.x * sine) + (value.y * cosine));
    }

    private static Sprite GetSharedPanelSprite()
    {
        if (sharedPanelSprite != null)
        {
            return sharedPanelSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "NoiseFloorGlassSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)(size - 1);
                float shardMask = SampleShardMask(nx, ny);
                float edgeFade = Mathf.Clamp01(Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny)) / 0.08f);
                float bodyNoise = Mathf.Lerp(0.84f, 1.06f, 0.5f + (0.5f * Mathf.Sin(((nx * 11.2f) + (ny * 9.4f)) * Mathf.PI)));
                float alpha = Mathf.Clamp01(shardMask * edgeFade * bodyNoise);
                pixels[x + (y * size)] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedPanelSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0u,
            SpriteMeshType.FullRect,
            Vector4.zero);
        sharedPanelSprite.name = "NoiseFloorPanelSprite";
        return sharedPanelSprite;
    }

    private static Sprite GetSharedReflectionSprite()
    {
        if (sharedReflectionSprite != null)
        {
            return sharedReflectionSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "NoiseFloorGlassReflectionSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)(size - 1);
                float glint = SampleReflectionGlint(nx, ny);
                float sparkle = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0.68f, 0.28f)) / 0.12f) * 0.22f;
                float alpha = Mathf.Clamp01((glint * 0.9f) + sparkle);
                pixels[x + (y * size)] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedReflectionSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0u,
            SpriteMeshType.FullRect,
            Vector4.zero);
        sharedReflectionSprite.name = "NoiseFloorPanelReflectionSprite";
        return sharedReflectionSprite;
    }
}


/*
 * File Role:
 * Implements top-down movement, sprinting, camera follow, and flashlight aiming.
 *
 * Runtime Use:
 * Consumes the input actions used by the current prototype player character.
 *
 * Study Notes:
 * This is the main player study file because it touches input, physics, camera, and lighting.
 */

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStamina))]
public sealed class WasdPlayerController : MonoBehaviour
{
    private const string FlashlightBeamObjectName = "FlashlightBeamVisual";
    private const string FlashlightHazeObjectName = "FlashlightHazeVisual";
    private const string FlashlightSoftSpillObjectName = "FlashlightSoftSpillVisual";
    private const string FlashlightSourceGlowObjectName = "FlashlightSourceGlowVisual";
    private const string PlayerComfortAuraObjectName = "PlayerComfortAuraVisual";
    private const int FlashlightBeamSortOffset = -1;
    private const int FlashlightHazeSortOffset = -2;
    private const int FlashlightSoftSpillSortOffset = 0;
    private const int FlashlightSourceGlowSortOffset = 1;
    private const int PlayerComfortAuraSortOffset = -3;
    private const float FlashlightBeamPixelsPerUnit = 100f;
    private static Sprite sharedFlashlightBeamSprite;
    private static Sprite sharedFlashlightHazeSprite;
    private static Sprite sharedRadialGlowSprite;
    private static Material sharedLightPresentationMaterial;

    // These fields are intentionally grouped by gameplay concern so it is easy to map
    // the inspector values back to movement, camera, and flashlight behavior.
    [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
    [SerializeField, Min(1f)] private float sprintMultiplier = 1.65f;
    [SerializeField] private PlayerStamina stamina;
    [SerializeField, Min(0f)] private float cameraFollowSharpness = 10f;
    [SerializeField] private Vector3 cameraOffset = new(0f, 0f, -10f);
    [Header("Flashlight")]
    [SerializeField] private Color flashlightColor = new(1f, 0.95f, 0.82f, 1f);
    [SerializeField, Min(0f)] private float flashlightRange = 14f;
    [SerializeField, Min(0f)] private float flashlightInnerRadius = 1.75f;
    [SerializeField, Range(0f, 360f)] private float flashlightOuterAngle = 50f;
    [SerializeField, Range(0.08f, 1f)] private float flashlightRangeBatteryFloorScale = 0.24f;
    [SerializeField, Range(0f, 360f)] private float flashlightInnerAngle = 38f;
    [SerializeField] private bool showFlashlightConePresentation;
    [SerializeField, Min(0f)] private float flashlightIntensity = 1.6f;
    [SerializeField, Range(0f, 1f)] private float flashlightFalloff = 0.55f;
    [SerializeField, Range(0.15f, 1f)] private float flashlightSceneLightRangeScale = 0.24f;
    [SerializeField, Range(0.15f, 1f)] private float flashlightSceneLightAngleScale = 0.4f;
    [SerializeField, Range(0f, 1f)] private float flashlightSceneLightIntensityScale = 0.82f;
    [SerializeField, Range(0f, 1f)] private float flashlightVolumeIntensity = 0f;
    [SerializeField] private Color flashlightBeamColor = new(1f, 0.93f, 0.75f, 0f);
    [SerializeField, Min(0f)] private float flashlightBeamLengthScale = 0.94f;
    [SerializeField, Min(0f)] private float flashlightBeamWidthScale = 0.74f;
    [SerializeField, Min(0f)] private float flashlightBeamSourceOffset = 0.18f;
    [SerializeField, Min(0f)] private float flashlightBeamPulseSpeed = 1.7f;
    [SerializeField, Range(0f, 0.25f)] private float flashlightBeamPulseStrength = 0.05f;
    [SerializeField] private Color flashlightHazeColor = new(0.82f, 0.92f, 1f, 0.06f);
    [SerializeField, Min(0f)] private float flashlightHazeLengthScale = 0.72f;
    [SerializeField, Min(0f)] private float flashlightHazeWidthScale = 0.6f;
    [SerializeField, Min(0f)] private float flashlightHazeSourceOffset = 0.05f;
    [SerializeField] private Sprite flashlightHazeSprite;
    [SerializeField] private Color flashlightSoftSpillColor = new(0.9f, 0.96f, 1f, 0.1f);
    [SerializeField, Min(0f)] private float flashlightSoftSpillScale = 1.4f;
    [SerializeField, Min(0f)] private float flashlightSoftSpillSourceOffset = 0.28f;
    [SerializeField] private Sprite flashlightSoftSpillSprite;
    [SerializeField] private Color flashlightSourceGlowColor = new(0.86f, 0.95f, 1f, 0.24f);
    [SerializeField, Min(0f)] private float flashlightSourceGlowScale = 0.68f;
    [SerializeField] private Sprite flashlightSourceGlowSprite;
    [SerializeField] private Color playerComfortAuraColor = new(0.56f, 0.72f, 0.8f, 0.045f);
    [SerializeField, Min(0f)] private float playerComfortAuraRadius = 0.56f;
    [SerializeField, Min(0f)] private float playerComfortAuraVerticalScale = 0.58f;
    [SerializeField] private float playerComfortAuraYOffset = -0.08f;
    [SerializeField] private Sprite playerComfortAuraSprite;
    [SerializeField] private Vector3 flashlightLocalOffset = new(0f, 0.12f, 0f);

    private Rigidbody2D body;
    private CapsuleCollider2D bodyCollider;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction interactAction;
    private InputAction jumpAction;
    private InputAction previousAction;
    private InputAction nextAction;
    private InputAction toggleFlashlightAction;
    private Camera cachedCamera;
    [SerializeField] private Transform flashlightPivot;
    [SerializeField] private Light2D flashlight;
    private SpriteRenderer flashlightBeamRenderer;
    private SpriteRenderer flashlightHazeRenderer;
    private SpriteRenderer flashlightSoftSpillRenderer;
    private SpriteRenderer flashlightSourceGlowRenderer;
    private SpriteRenderer playerComfortAuraRenderer;
    private SpriteRenderer bodyRenderer;
    private float flashlightPresentationIntensityScale = 1f;
    private float flashlightPresentationVolumeScale = 1f;
    private float flashlightBatteryIntensityScale = 1f;
    private float flashlightBatteryVolumeScale = 1f;
    private float flashlightOnComfortRevealScale = 1f;
    private float flashlightOffComfortRevealScale = 1f;
    private bool flashlightShadowsEnabled;
    private bool flashlightAvailable = true;
    private bool flashlightEnabledState = true;
    private Vector2 aimDirection = Vector2.up;
    private Vector2 currentMoveInput;
    private Vector2 lastMoveInputDirection = Vector2.up;

    public Transform FlashlightPivot => flashlightPivot != null ? flashlightPivot : transform;
    public Light2D FlashlightLight => flashlight;
    public float FlashlightRange => flashlightRange;
    public float FlashlightOuterAngle => flashlightOuterAngle;
    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
    public bool SprintHeld => sprintAction != null && sprintAction.IsPressed();
    public bool IsSprinting => stamina != null
        ? stamina.IsSprinting
        : SprintHeld && currentMoveInput.sqrMagnitude > 0.0001f;
    public IStaminaSource Stamina => stamina;
    public bool HasFlashlight => flashlightAvailable;
    public bool IsFlashlightSwitchOn => flashlightAvailable && flashlightEnabledState;
    public Vector2 AimDirection => aimDirection.sqrMagnitude > 0.0001f
        ? aimDirection
        : lastMoveInputDirection.sqrMagnitude > 0.0001f
            ? lastMoveInputDirection.normalized
            : Vector2.up;
    public float FlashlightPresentationIntensityScale => flashlightPresentationIntensityScale;
    public float FlashlightPresentationVolumeScale => flashlightPresentationVolumeScale;
    public float FlashlightVisibilityScale => IsFlashlightSwitchOn
        ? Mathf.Clamp01(flashlightPresentationIntensityScale * flashlightBatteryIntensityScale)
        : 0f;
    public float EffectiveFlashlightRange => IsFlashlightSwitchOn
        ? flashlightRange * Mathf.Lerp(flashlightRangeBatteryFloorScale, 1f, FlashlightVisibilityScale)
        : 0f;
    public float EffectiveFlashlightOuterAngle => IsFlashlightSwitchOn
        ? Mathf.Lerp(18f, flashlightOuterAngle, FlashlightVisibilityScale)
        : 0f;
    public float EffectivePlayerComfortRevealScale => IsFlashlightSwitchOn
        ? flashlightOnComfortRevealScale
        : flashlightOffComfortRevealScale;
    public float FlashlightOnComfortRevealScale => flashlightOnComfortRevealScale;
    public float FlashlightOffComfortRevealScale => flashlightOffComfortRevealScale;
    public bool FlashlightShadowsEnabled => flashlightShadowsEnabled;

    private void Awake()
    {
        // Cache long-lived component references once. The rest of the file assumes these
        // objects already exist and only refreshes dynamic references like the scene camera.
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CapsuleCollider2D>();
        playerInput = GetComponent<PlayerInput>();
        stamina = GetComponent<PlayerStamina>();
        cachedCamera = ResolveCachedSceneCamera();
        CacheActions();
        ConfigurePhysicsBody();
        ResetFacingState();
        EnsureFlashlight();
    }

    private void OnEnable()
    {
        playerInput.ActivateInput();
        cachedCamera = ResolveCachedSceneCamera();
        stamina ??= GetComponent<PlayerStamina>();
        CacheActions();
        ConfigurePhysicsBody();
        ResetFacingState();
        EnsureFlashlight();
    }

    private void Update()
    {
        CacheMovementInput();

        // Rotation and flashlight pose are visual updates, so they run every frame rather
        // than in FixedUpdate.
        UpdateLookRotation();
        UpdateFlashlightPose();
    }

    private void FixedUpdate()
    {
        // Movement uses physics time so that Rigidbody2D velocity changes stay stable even
        // when the frame rate changes.
        Vector2 movement = currentMoveInput;

        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        bool hasMovementInput = movement.sqrMagnitude > 0.0001f;
        bool sprintActive = SprintHeld && hasMovementInput;

        stamina ??= GetComponent<PlayerStamina>();

        if (stamina != null)
        {
            sprintActive = stamina.TickSprintIntent(SprintHeld, hasMovementInput, Time.fixedDeltaTime);
        }

        float currentSpeed = moveSpeed;

        if (sprintActive)
        {
            currentSpeed *= sprintMultiplier;
        }

        body.linearVelocity = movement * currentSpeed;
    }

    private void LateUpdate()
    {
        UpdateCameraFollow();
    }

    private void OnDisable()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        currentMoveInput = Vector2.zero;
        stamina?.CancelSprint();
    }

    private void Reset()
    {
        Rigidbody2D cachedBody = GetComponent<Rigidbody2D>();
        cachedBody.gravityScale = 0f;
        cachedBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        cachedBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        cachedBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        cachedBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
    }

    private void ConfigurePhysicsBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (body != null)
        {
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        bodyCollider ??= GetComponent<CapsuleCollider2D>();
        TopDownCollisionMaterialUtility.ApplyNoFriction(bodyCollider);
    }

    private void CacheActions()
    {
        // The controller prefers the current action map, but falls back to global lookup
        // so the prefab still works if the Input System asset is reorganized.
        if (playerInput == null || playerInput.actions == null)
        {
            moveAction = null;
            lookAction = null;
            sprintAction = null;
            interactAction = null;
            jumpAction = null;
            previousAction = null;
            nextAction = null;
            toggleFlashlightAction = null;
            return;
        }

        string actionMapName = string.IsNullOrWhiteSpace(playerInput.defaultActionMap)
            ? "Player"
            : playerInput.defaultActionMap;

        InputActionMap actionMap = playerInput.actions.FindActionMap(actionMapName, false);
        moveAction = actionMap?.FindAction("Move", false) ?? playerInput.actions.FindAction("Move", false);
        lookAction = actionMap?.FindAction("Look", false) ?? playerInput.actions.FindAction("Look", false);
        sprintAction = actionMap?.FindAction("Sprint", false) ?? playerInput.actions.FindAction("Sprint", false);
        interactAction = actionMap?.FindAction("Interact", false) ?? playerInput.actions.FindAction("Interact", false);
        jumpAction = actionMap?.FindAction("Jump", false) ?? playerInput.actions.FindAction("Jump", false);
        previousAction = actionMap?.FindAction("Previous", false) ?? playerInput.actions.FindAction("Previous", false);
        nextAction = actionMap?.FindAction("Next", false) ?? playerInput.actions.FindAction("Next", false);
        toggleFlashlightAction = actionMap?.FindAction("ToggleFlashlight", false) ?? playerInput.actions.FindAction("ToggleFlashlight", false);
    }

    private void UpdateLookRotation()
    {
        // The player always faces the most recent valid look direction. If there is no
        // meaningful input this frame, we keep the previous facing.
        if (!TryGetLookDirection(out Vector2 lookDirection))
        {
            return;
        }

        aimDirection = lookDirection;
    }

    private bool TryGetLookDirection(out Vector2 lookDirection)
    {
        lookDirection = Vector2.zero;

        if (lookAction == null)
        {
            return false;
        }

        Vector2 rawLook = lookAction.ReadValue<Vector2>();

        if (rawLook.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        // Mouse look uses absolute screen position, while gamepad stick look uses a
        // direction vector. This branch normalizes both into the same world-space result.
        bool usingMouseScheme = string.Equals(playerInput.currentControlScheme, "Keyboard&Mouse");
        bool usingPointerPosition = rawLook.sqrMagnitude > 4f
            || usingMouseScheme;

        if (usingPointerPosition)
        {
            Camera lookCamera = ResolveCachedSceneCamera();

            if (lookCamera == null)
            {
                return false;
            }

            Vector2 pointerPosition = rawLook;

            if (usingMouseScheme && Mouse.current != null)
            {
                pointerPosition = Mouse.current.position.ReadValue();
            }

            float depthToPlayerPlane = Mathf.Abs(lookCamera.transform.position.z - transform.position.z);
            Vector3 screenPoint = new(pointerPosition.x, pointerPosition.y, Mathf.Max(0.01f, depthToPlayerPlane));
            Vector3 worldPoint = lookCamera.ScreenToWorldPoint(screenPoint);
            lookDirection = (Vector2)(worldPoint - transform.position);
        }
        else
        {
            lookDirection = rawLook;
        }

        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        lookDirection.Normalize();
        return true;
    }

    private void UpdateCameraFollow()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // Legacy overview prototype scenes keep a fixed camera, so the player controller
        // should not pull the camera back onto itself there.
        if (PrototypeSceneUtility.UseOverviewCamera(gameObject.scene))
        {
            return;
        }

        Camera followCamera = ResolveCachedSceneCamera();

        if (followCamera == null)
        {
            return;
        }

        Vector3 targetPosition = transform.position + cameraOffset;

        if (cameraFollowSharpness <= 0f)
        {
            followCamera.transform.position = targetPosition;
            return;
        }

        float blend = 1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime);
        followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, targetPosition, blend);
    }

    private Camera ResolveCachedSceneCamera()
    {
        Scene scene = gameObject.scene;

        if (cachedCamera != null && cachedCamera.gameObject.scene == scene)
        {
            return cachedCamera;
        }

        cachedCamera = RSceneCameraUtility.FindPreferredCameraInScene(scene);
        return cachedCamera;
    }

    private void EnsureFlashlight()
    {
        // The flashlight is created in code so the player prefab stays lightweight and
        // can be dropped into any test scene without extra manual hierarchy setup.
        bodyRenderer ??= GetComponent<SpriteRenderer>();

        if (flashlightPivot == null)
        {
            flashlight ??= GetComponentInChildren<Light2D>(true);

            if (flashlight != null)
            {
                flashlightPivot = flashlight.transform;
            }
            else
            {
                GameObject flashlightObject = new("FlashlightPivot");
                flashlightPivot = flashlightObject.transform;
                flashlightPivot.SetParent(transform, false);
            }
        }

        flashlightPivot.localScale = Vector3.one;

        if (flashlight == null)
        {
            flashlight = flashlightPivot.GetComponent<Light2D>();

            if (flashlight == null)
            {
                flashlight = flashlightPivot.gameObject.AddComponent<Light2D>();
            }
        }

        EnsureFlashlightBeam();
        EnsureFlashlightHaze();
        EnsureFlashlightSourceGlow();
        ApplyFlashlightLightState();
        ApplyFlashlightPresentation();
        UpdateFlashlightPose();
    }

    private void ApplyFlashlightLightState()
    {
        if (flashlight == null)
        {
            return;
        }

        bool flashlightActive = IsFlashlightSwitchOn;
        bool flashlightOwned = HasFlashlight;
        float intensity = flashlightActive
            ? flashlightIntensity * flashlightPresentationIntensityScale * flashlightBatteryIntensityScale
            : 0f;
        float volumeIntensity = flashlightActive
            ? flashlightVolumeIntensity * flashlightPresentationVolumeScale * flashlightBatteryVolumeScale
            : 0f;
        bool shadowsActive = flashlightShadowsEnabled;
        float sceneLightRange = flashlightRange * flashlightSceneLightRangeScale;
        float sceneLightInnerRadius = Mathf.Min(sceneLightRange, flashlightInnerRadius * flashlightSceneLightRangeScale);
        float sceneLightOuterAngle = flashlightOuterAngle * flashlightSceneLightAngleScale;
        float sceneLightInnerAngle = Mathf.Min(sceneLightOuterAngle, flashlightInnerAngle * flashlightSceneLightAngleScale);

        flashlight.lightType = Light2D.LightType.Point;
        flashlight.enabled = flashlightOwned;
        flashlight.color = flashlightColor;
        flashlight.intensity = intensity * flashlightSceneLightIntensityScale;
        flashlight.falloffIntensity = flashlightFalloff;
        // Keep fog/gameplay reveal driven by the authored flashlight values, but clamp the
        // actual scene light so it does not spill unrealistically far past thin walls.
        flashlight.pointLightInnerRadius = sceneLightInnerRadius;
        flashlight.pointLightOuterRadius = sceneLightRange;
        flashlight.pointLightInnerAngle = sceneLightInnerAngle;
        flashlight.pointLightOuterAngle = sceneLightOuterAngle;
        flashlight.volumetricEnabled = volumeIntensity > 0.001f;
        flashlight.volumeIntensity = volumeIntensity;
        flashlight.shadowsEnabled = shadowsActive;
        flashlight.shadowIntensity = shadowsActive ? 1f : 0f;
        flashlight.shadowSoftness = 0f;
        flashlight.shadowVolumeIntensity = shadowsActive ? 1f : 0f;
        flashlight.volumetricShadowsEnabled = shadowsActive;
        flashlight.lightOrder = -1;
    }

    private void RefreshFlashlightRuntimePresentation(bool updatePose = false)
    {
        if (flashlight == null || flashlightPivot == null)
        {
            EnsureFlashlight();
            return;
        }

        ApplyFlashlightLightState();

        if (updatePose)
        {
            UpdateFlashlightPose();
            return;
        }

        ApplyFlashlightPresentation();
    }

    private void EnsureFlashlightBeam()
    {
        if (flashlightPivot == null)
        {
            return;
        }

        if (flashlightBeamRenderer == null)
        {
            Transform existingBeam = flashlightPivot.Find(FlashlightBeamObjectName);

            if (existingBeam == null)
            {
                GameObject beamObject = new(FlashlightBeamObjectName);
                existingBeam = beamObject.transform;
                existingBeam.SetParent(flashlightPivot, false);
            }

            flashlightBeamRenderer = existingBeam.GetComponent<SpriteRenderer>();

            if (flashlightBeamRenderer == null)
            {
                flashlightBeamRenderer = existingBeam.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        ConfigurePresentationRenderer(flashlightBeamRenderer, GetSharedFlashlightBeamSprite());
    }

    private void EnsureFlashlightHaze()
    {
        if (flashlightPivot == null)
        {
            return;
        }

        if (flashlightHazeRenderer == null)
        {
            flashlightHazeRenderer = ResolveOrCreateSpriteRenderer(flashlightPivot, FlashlightHazeObjectName);
        }

        ConfigurePresentationRenderer(
            flashlightHazeRenderer,
            flashlightHazeSprite != null ? flashlightHazeSprite : GetSharedFlashlightHazeSprite());
    }

    private void EnsureFlashlightSoftSpill()
    {
        if (flashlightPivot == null)
        {
            return;
        }

        if (flashlightSoftSpillRenderer == null)
        {
            flashlightSoftSpillRenderer = ResolveOrCreateSpriteRenderer(flashlightPivot, FlashlightSoftSpillObjectName);
        }

        ConfigurePresentationRenderer(
            flashlightSoftSpillRenderer,
            flashlightSoftSpillSprite != null ? flashlightSoftSpillSprite : GetSharedRadialGlowSprite());
    }

    private void EnsureFlashlightSourceGlow()
    {
        if (flashlightPivot == null)
        {
            return;
        }

        if (flashlightSourceGlowRenderer == null)
        {
            flashlightSourceGlowRenderer = ResolveOrCreateSpriteRenderer(flashlightPivot, FlashlightSourceGlowObjectName);
        }

        ConfigurePresentationRenderer(
            flashlightSourceGlowRenderer,
            flashlightSourceGlowSprite != null ? flashlightSourceGlowSprite : GetSharedRadialGlowSprite());
    }

    private void EnsurePlayerComfortAura()
    {
        if (playerComfortAuraRenderer == null)
        {
            playerComfortAuraRenderer = ResolveOrCreateSpriteRenderer(transform, PlayerComfortAuraObjectName);
        }

        ConfigurePresentationRenderer(
            playerComfortAuraRenderer,
            playerComfortAuraSprite != null ? playerComfortAuraSprite : GetSharedRadialGlowSprite());
    }

    private static SpriteRenderer ResolveOrCreateSpriteRenderer(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);

        if (child == null)
        {
            GameObject childObject = new(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<SpriteRenderer>();
        }

        return renderer;
    }

    private static void ConfigurePresentationRenderer(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = sprite;
        Material presentationMaterial = GetSharedLightPresentationMaterial();

        if (presentationMaterial != null)
        {
            renderer.sharedMaterial = presentationMaterial;
        }

        renderer.maskInteraction = SpriteMaskInteraction.None;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private void ApplyFlashlightPresentation()
    {
        DisableAmbientRadialPresentation();

        if (!showFlashlightConePresentation)
        {
            if (flashlightBeamRenderer != null)
            {
                flashlightBeamRenderer.enabled = false;
            }

            if (flashlightHazeRenderer != null)
            {
                flashlightHazeRenderer.enabled = false;
            }
        }

        ApplyFlashlightBeamPresentation();
        ApplyFlashlightHazePresentation();
        ApplyFlashlightSourceGlowPresentation();
    }

    private void DisableAmbientRadialPresentation()
    {
        if (flashlightSoftSpillRenderer != null)
        {
            flashlightSoftSpillRenderer.enabled = false;
        }

        if (playerComfortAuraRenderer != null)
        {
            playerComfortAuraRenderer.enabled = false;
        }
    }

    private void ApplyFlashlightBeamPresentation()
    {
        if (flashlightBeamRenderer == null || flashlightPivot == null)
        {
            return;
        }

        if (!showFlashlightConePresentation || !IsFlashlightSwitchOn)
        {
            flashlightBeamRenderer.enabled = false;
            return;
        }

        float visibilityScale = Mathf.Clamp01(FlashlightVisibilityScale);
        float pulse = ComputeFlashlightPulseMultiplier();

        Color beamColor = flashlightBeamColor;
        beamColor.a = Mathf.Clamp01(beamColor.a * Mathf.Lerp(0.36f, 1f, visibilityScale) * pulse);

        if (beamColor.a <= 0.01f)
        {
            flashlightBeamRenderer.enabled = false;
            return;
        }

        flashlightBeamRenderer.enabled = true;
        flashlightBeamRenderer.color = beamColor;

        float beamLength = Mathf.Max(0.35f, flashlightRange * flashlightBeamLengthScale * Mathf.Lerp(0.4f, 1f, visibilityScale));
        float halfAngleRadians = Mathf.Max(0.05f, flashlightOuterAngle * 0.5f * Mathf.Deg2Rad);
        float beamWidth = Mathf.Max(
            flashlightInnerRadius * 1.18f,
            Mathf.Tan(halfAngleRadians) * beamLength * flashlightBeamWidthScale);
        GetInverseAbsScale(flashlightPivot.lossyScale, out float inverseScaleX, out float inverseScaleY);
        Transform beamTransform = flashlightBeamRenderer.transform;
        beamTransform.localPosition = new Vector3(
            0f,
            (flashlightBeamSourceOffset + (beamLength * 0.18f)) * inverseScaleY,
            0f);
        beamTransform.localRotation = Quaternion.identity;
        beamTransform.localScale = new Vector3(beamWidth * inverseScaleX, beamLength * inverseScaleY, 1f);

        if (bodyRenderer != null)
        {
            flashlightBeamRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            flashlightBeamRenderer.sortingOrder = bodyRenderer.sortingOrder + FlashlightBeamSortOffset;
        }
    }

    private void ApplyFlashlightHazePresentation()
    {
        if (flashlightHazeRenderer == null || flashlightPivot == null)
        {
            return;
        }

        if (!showFlashlightConePresentation || !IsFlashlightSwitchOn)
        {
            flashlightHazeRenderer.enabled = false;
            return;
        }

        float visibilityScale = Mathf.Clamp01(FlashlightVisibilityScale);
        float pulse = ComputeFlashlightPulseMultiplier();
        Color hazeColor = Color.Lerp(flashlightHazeColor, flashlightColor, 0.38f);
        hazeColor.a = Mathf.Clamp01(flashlightHazeColor.a * Mathf.Lerp(0.28f, 1f, visibilityScale) * pulse);

        if (hazeColor.a <= 0.01f)
        {
            flashlightHazeRenderer.enabled = false;
            return;
        }

        flashlightHazeRenderer.enabled = true;
        flashlightHazeRenderer.color = hazeColor;

        float hazeLength = Mathf.Max(0.4f, flashlightRange * flashlightHazeLengthScale * Mathf.Lerp(0.46f, 1f, visibilityScale));
        float halfAngleRadians = Mathf.Max(0.05f, flashlightOuterAngle * 0.5f * Mathf.Deg2Rad);
        float hazeWidth = Mathf.Max(
            flashlightInnerRadius * 1.42f,
            Mathf.Tan(halfAngleRadians) * hazeLength * flashlightHazeWidthScale);

        GetInverseAbsScale(flashlightPivot.lossyScale, out float inverseScaleX, out float inverseScaleY);
        Transform hazeTransform = flashlightHazeRenderer.transform;
        hazeTransform.localPosition = new Vector3(
            0f,
            (flashlightHazeSourceOffset + (hazeLength * 0.12f)) * inverseScaleY,
            0f);
        hazeTransform.localRotation = Quaternion.identity;
        hazeTransform.localScale = new Vector3(hazeWidth * inverseScaleX, hazeLength * inverseScaleY, 1f);

        if (bodyRenderer != null)
        {
            flashlightHazeRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            flashlightHazeRenderer.sortingOrder = bodyRenderer.sortingOrder + FlashlightHazeSortOffset;
        }
    }

    private void ApplyFlashlightSourceGlowPresentation()
    {
        if (flashlightSourceGlowRenderer == null || flashlightPivot == null)
        {
            return;
        }

        if (!IsFlashlightSwitchOn)
        {
            flashlightSourceGlowRenderer.enabled = false;
            return;
        }

        float visibilityScale = Mathf.Clamp01(FlashlightVisibilityScale);
        float pulse = ComputeFlashlightPulseMultiplier();
        Color sourceGlowColor = Color.Lerp(flashlightSourceGlowColor, flashlightColor, 0.32f);
        sourceGlowColor.a = Mathf.Clamp01(flashlightSourceGlowColor.a * Mathf.Lerp(0.35f, 1f, visibilityScale) * pulse);

        if (sourceGlowColor.a <= 0.01f)
        {
            flashlightSourceGlowRenderer.enabled = false;
            return;
        }

        flashlightSourceGlowRenderer.enabled = true;
        flashlightSourceGlowRenderer.color = sourceGlowColor;

        float glowScale = Mathf.Max(0.24f, flashlightInnerRadius * flashlightSourceGlowScale * Mathf.Lerp(0.85f, 1.08f, visibilityScale));
        GetInverseAbsScale(flashlightPivot.lossyScale, out float inverseScaleX, out float inverseScaleY);
        Transform sourceTransform = flashlightSourceGlowRenderer.transform;
        sourceTransform.localPosition = new Vector3(0f, flashlightBeamSourceOffset * 0.42f * inverseScaleY, 0f);
        sourceTransform.localRotation = Quaternion.identity;
        sourceTransform.localScale = new Vector3(glowScale * inverseScaleX, glowScale * inverseScaleY, 1f);

        if (bodyRenderer != null)
        {
            flashlightSourceGlowRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            flashlightSourceGlowRenderer.sortingOrder = bodyRenderer.sortingOrder + FlashlightSourceGlowSortOffset;
        }
    }

    private void ApplyFlashlightSoftSpillPresentation()
    {
        if (flashlightSoftSpillRenderer == null || flashlightPivot == null)
        {
            return;
        }

        if (!IsFlashlightSwitchOn)
        {
            flashlightSoftSpillRenderer.enabled = false;
            return;
        }

        float visibilityScale = Mathf.Clamp01(FlashlightVisibilityScale);
        float pulse = ComputeFlashlightPulseMultiplier();
        Color spillColor = Color.Lerp(flashlightSoftSpillColor, flashlightColor, 0.4f);
        spillColor.a = Mathf.Clamp01(flashlightSoftSpillColor.a * Mathf.Lerp(0.34f, 1f, visibilityScale) * pulse);

        if (spillColor.a <= 0.01f)
        {
            flashlightSoftSpillRenderer.enabled = false;
            return;
        }

        flashlightSoftSpillRenderer.enabled = true;
        flashlightSoftSpillRenderer.color = spillColor;

        float spillScale = Mathf.Max(0.42f, flashlightInnerRadius * flashlightSoftSpillScale * Mathf.Lerp(0.92f, 1.18f, visibilityScale));
        GetInverseAbsScale(flashlightPivot.lossyScale, out float inverseScaleX, out float inverseScaleY);
        Transform spillTransform = flashlightSoftSpillRenderer.transform;
        spillTransform.localPosition = new Vector3(0f, flashlightSoftSpillSourceOffset * inverseScaleY, 0f);
        spillTransform.localRotation = Quaternion.identity;
        spillTransform.localScale = new Vector3(spillScale * inverseScaleX, spillScale * inverseScaleY, 1f);

        if (bodyRenderer != null)
        {
            flashlightSoftSpillRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            flashlightSoftSpillRenderer.sortingOrder = bodyRenderer.sortingOrder + FlashlightSoftSpillSortOffset;
        }
    }

    private void ApplyPlayerComfortAuraPresentation()
    {
        if (playerComfortAuraRenderer == null)
        {
            return;
        }

        float comfortScale = Mathf.Max(0f, EffectivePlayerComfortRevealScale);

        if (comfortScale <= 0.01f)
        {
            playerComfortAuraRenderer.enabled = false;
            return;
        }

        float onOffAlphaScale = IsFlashlightSwitchOn ? 1f : 0.62f;
        Color auraColor = Color.Lerp(playerComfortAuraColor, flashlightColor, 0.12f);
        auraColor.a = Mathf.Clamp01(playerComfortAuraColor.a * Mathf.Lerp(0.45f, 1f, Mathf.Min(1f, comfortScale)) * onOffAlphaScale);

        if (auraColor.a <= 0.01f)
        {
            playerComfortAuraRenderer.enabled = false;
            return;
        }

        playerComfortAuraRenderer.enabled = true;
        playerComfortAuraRenderer.color = auraColor;

        float auraRadius = Mathf.Max(0.3f, playerComfortAuraRadius * Mathf.Max(0.65f, comfortScale));
        GetInverseAbsScale(transform.lossyScale, out float inverseScaleX, out float inverseScaleY);
        Transform auraTransform = playerComfortAuraRenderer.transform;
        auraTransform.localPosition = new Vector3(0f, playerComfortAuraYOffset * inverseScaleY, 0f);
        auraTransform.localRotation = Quaternion.identity;
        auraTransform.localScale = new Vector3(
            auraRadius * 2f * inverseScaleX,
            auraRadius * 2f * playerComfortAuraVerticalScale * inverseScaleY,
            1f);

        if (bodyRenderer != null)
        {
            playerComfortAuraRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            playerComfortAuraRenderer.sortingOrder = bodyRenderer.sortingOrder + PlayerComfortAuraSortOffset;
        }
    }

    private float ComputeFlashlightPulseMultiplier()
    {
        float pulse = 1f;

        if (Application.isPlaying
            && flashlightBeamPulseStrength > 0.0001f
            && flashlightBeamPulseSpeed > 0.0001f)
        {
            pulse += Mathf.Sin(Time.unscaledTime * flashlightBeamPulseSpeed) * flashlightBeamPulseStrength;
        }

        return Mathf.Max(0.1f, pulse);
    }

    private static void GetInverseAbsScale(Vector3 lossyScale, out float inverseScaleX, out float inverseScaleY)
    {
        inverseScaleX = Mathf.Approximately(lossyScale.x, 0f) ? 1f : 1f / Mathf.Abs(lossyScale.x);
        inverseScaleY = Mathf.Approximately(lossyScale.y, 0f) ? 1f : 1f / Mathf.Abs(lossyScale.y);
    }

    private static Sprite GetSharedFlashlightBeamSprite()
    {
        if (sharedFlashlightBeamSprite != null)
        {
            return sharedFlashlightBeamSprite;
        }

        const int textureWidth = 128;
        const int textureHeight = 256;
        Texture2D texture = new(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "FlashlightBeamSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[textureWidth * textureHeight];

        for (int y = 0; y < textureHeight; y++)
        {
            float v = y / (float)(textureHeight - 1);
            float beamHalfWidth = Mathf.Lerp(0.08f, 0.92f, Mathf.Pow(v, 0.82f));
            float tailFade = 1f - (Mathf.SmoothStep(0.82f, 1f, v) * 0.72f);
            float sourceGlow = 1f - Mathf.SmoothStep(0.1f, 0.42f, v);

            for (int x = 0; x < textureWidth; x++)
            {
                float u = x / (float)(textureWidth - 1);
                float centeredU = Mathf.Abs((u - 0.5f) * 2f);

                if (centeredU > beamHalfWidth)
                {
                    pixels[(y * textureWidth) + x] = Color.clear;
                    continue;
                }

                float edgeFade = 1f - Mathf.Clamp01(centeredU / Mathf.Max(0.001f, beamHalfWidth));
                edgeFade = Mathf.SmoothStep(0f, 1f, edgeFade);
                float centerGlow = 1f - Mathf.Abs(u - 0.5f) * 2f;
                centerGlow = Mathf.SmoothStep(0f, 1f, centerGlow);
                float alpha = (0.1f + (edgeFade * 0.44f) + (centerGlow * 0.28f) + (sourceGlow * 0.24f)) * tailFade;
                pixels[(y * textureWidth) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedFlashlightBeamSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0f),
            FlashlightBeamPixelsPerUnit);
        sharedFlashlightBeamSprite.name = "FlashlightBeamSprite";
        sharedFlashlightBeamSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedFlashlightBeamSprite;
    }

    private static Sprite GetSharedFlashlightHazeSprite()
    {
        if (sharedFlashlightHazeSprite != null)
        {
            return sharedFlashlightHazeSprite;
        }

        const int textureWidth = 128;
        const int textureHeight = 256;
        Texture2D texture = new(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "FlashlightHazeSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[textureWidth * textureHeight];

        for (int y = 0; y < textureHeight; y++)
        {
            float v = y / (float)(textureHeight - 1);
            float hazeHalfWidth = Mathf.Lerp(0.16f, 0.98f, Mathf.Pow(v, 0.92f));
            float tailFade = 1f - (Mathf.SmoothStep(0.72f, 1f, v) * 0.58f);
            float sourceGlow = 1f - Mathf.SmoothStep(0.04f, 0.3f, v);

            for (int x = 0; x < textureWidth; x++)
            {
                float u = x / (float)(textureWidth - 1);
                float centeredU = Mathf.Abs((u - 0.5f) * 2f);

                if (centeredU > hazeHalfWidth)
                {
                    pixels[(y * textureWidth) + x] = Color.clear;
                    continue;
                }

                float edgeFade = 1f - Mathf.Clamp01(centeredU / Mathf.Max(0.001f, hazeHalfWidth));
                edgeFade = Mathf.SmoothStep(0f, 1f, edgeFade);
                float centerGlow = 1f - Mathf.Pow(centeredU, 1.85f);
                float alpha = (0.04f + (edgeFade * 0.2f) + (centerGlow * 0.12f) + (sourceGlow * 0.18f)) * tailFade;
                pixels[(y * textureWidth) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedFlashlightHazeSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0f),
            FlashlightBeamPixelsPerUnit);
        sharedFlashlightHazeSprite.name = "FlashlightHazeSprite";
        sharedFlashlightHazeSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedFlashlightHazeSprite;
    }

    private static Sprite GetSharedRadialGlowSprite()
    {
        if (sharedRadialGlowSprite != null)
        {
            return sharedRadialGlowSprite;
        }

        const int textureSize = 128;
        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "RadialGlowSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            float v = ((y / (float)(textureSize - 1)) - 0.5f) * 2f;

            for (int x = 0; x < textureSize; x++)
            {
                float u = ((x / (float)(textureSize - 1)) - 0.5f) * 2f;
                float distance = Mathf.Sqrt((u * u) + (v * v));

                if (distance >= 1f)
                {
                    pixels[(y * textureSize) + x] = Color.clear;
                    continue;
                }

                float radial = 1f - distance;
                radial = Mathf.SmoothStep(0f, 1f, radial);
                float core = 1f - Mathf.SmoothStep(0f, 0.36f, distance);
                float alpha = (radial * 0.42f) + (core * 0.28f);
                pixels[(y * textureSize) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedRadialGlowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            FlashlightBeamPixelsPerUnit);
        sharedRadialGlowSprite.name = "RadialGlowSprite";
        sharedRadialGlowSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedRadialGlowSprite;
    }

    private static Material GetSharedLightPresentationMaterial()
    {
        if (sharedLightPresentationMaterial != null)
        {
            return sharedLightPresentationMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        shader ??= Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        sharedLightPresentationMaterial = new Material(shader)
        {
            name = "LightPresentationMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedLightPresentationMaterial;
    }

    public void SetFlashlightPresentation(float intensityScale, float volumeScale)
    {
        float resolvedIntensityScale = Mathf.Max(0f, intensityScale);
        float resolvedVolumeScale = Mathf.Clamp01(volumeScale);

        if (flashlightPresentationIntensityScale == resolvedIntensityScale
            && flashlightPresentationVolumeScale == resolvedVolumeScale)
        {
            return;
        }

        flashlightPresentationIntensityScale = resolvedIntensityScale;
        flashlightPresentationVolumeScale = resolvedVolumeScale;
        RefreshFlashlightRuntimePresentation();
    }

    public void SetFlashlightBatteryScale(float intensityScale, float volumeScale)
    {
        float resolvedIntensityScale = Mathf.Max(0f, intensityScale);
        float resolvedVolumeScale = Mathf.Clamp01(volumeScale);

        if (flashlightBatteryIntensityScale == resolvedIntensityScale
            && flashlightBatteryVolumeScale == resolvedVolumeScale)
        {
            return;
        }

        flashlightBatteryIntensityScale = resolvedIntensityScale;
        flashlightBatteryVolumeScale = resolvedVolumeScale;
        RefreshFlashlightRuntimePresentation();
    }

    public void SetPlayerComfortRevealScale(float flashlightOffScale, float flashlightOnScale)
    {
        flashlightOffComfortRevealScale = Mathf.Max(0f, flashlightOffScale);
        flashlightOnComfortRevealScale = Mathf.Max(0f, flashlightOnScale);
        DisableAmbientRadialPresentation();
    }

    public void SetFlashlightShadowEnabled(bool enabled)
    {
        if (flashlightShadowsEnabled == enabled)
        {
            return;
        }

        flashlightShadowsEnabled = enabled;
        RefreshFlashlightRuntimePresentation();
    }

    public void SetFlashlightAvailability(bool available)
    {
        bool nextFlashlightEnabledState = available
            ? flashlightEnabledState
            : false;

        if (flashlightAvailable == available
            && flashlightEnabledState == nextFlashlightEnabledState)
        {
            return;
        }

        flashlightAvailable = available;
        flashlightEnabledState = nextFlashlightEnabledState;
        RefreshFlashlightRuntimePresentation(updatePose: true);
    }

    public void SetFlashlightEnabled(bool enabled)
    {
        bool nextFlashlightEnabledState = flashlightAvailable && enabled;

        if (flashlightEnabledState == nextFlashlightEnabledState)
        {
            return;
        }

        flashlightEnabledState = nextFlashlightEnabledState;
        RefreshFlashlightRuntimePresentation(updatePose: true);
    }

    private void UpdateFlashlightPose()
    {
        // The root stays axis-aligned so the collider does not rotate into walls.
        if (flashlightPivot == null)
        {
            return;
        }

        Vector2 forward = AimDirection;
        Vector2 right = new(forward.y, -forward.x);
        Vector2 planarOffset = (right * flashlightLocalOffset.x) + (forward * flashlightLocalOffset.y);
        flashlightPivot.position = transform.position + new Vector3(planarOffset.x, planarOffset.y, flashlightLocalOffset.z);
        flashlightPivot.up = new Vector3(forward.x, forward.y, 0f);
        flashlightPivot.localScale = Vector3.one;
        ApplyFlashlightPresentation();
    }

    public void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }

        aimDirection = direction.normalized;
        UpdateFlashlightPose();
    }

    public bool ConsumeInteractPressedThisFrame()
    {
        return interactAction != null && interactAction.WasPressedThisFrame();
    }

    public bool ConsumeThrowablePressedThisFrame()
    {
        return (jumpAction != null && jumpAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
    }

    public bool ConsumeFlashlightTogglePressedThisFrame()
    {
        return (toggleFlashlightAction != null && toggleFlashlightAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
    }

    public bool ConsumeQuickSlot1PressedThisFrame()
    {
        return (previousAction != null && previousAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame);
    }

    public bool ConsumeQuickSlot2PressedThisFrame()
    {
        return (nextAction != null && nextAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame);
    }

    private void CacheMovementInput()
    {
        currentMoveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        if (currentMoveInput.sqrMagnitude > 1f)
        {
            currentMoveInput.Normalize();
        }

        if (currentMoveInput.sqrMagnitude > 0.0001f)
        {
            lastMoveInputDirection = currentMoveInput;
        }
    }

    private void ResetFacingState()
    {
        currentMoveInput = Vector2.zero;
        aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.up;
        lastMoveInputDirection = lastMoveInputDirection.sqrMagnitude > 0.0001f
            ? lastMoveInputDirection.normalized
            : Vector2.up;
    }
}


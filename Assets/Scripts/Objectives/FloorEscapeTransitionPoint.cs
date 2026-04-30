using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum FloorEscapeTransitionKind
{
    EmergencyStairs,
    FinalExit
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FloorEscapeTransitionPoint : PlayerInteractable2D
{
    private const int SortingOrder = 140;

    [FormerlySerializedAs("controller")]
    [SerializeField] private MonoBehaviour rebuildControllerSource;
    [SerializeField] private FloorEscapeTransitionKind transitionKind;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;
    [SerializeField] private Color lockedColor = new(0.78f, 0.28f, 0.25f, 1f);
    [SerializeField] private Color unlockedColor = new(0.3f, 0.86f, 0.95f, 1f);
    [SerializeField] private Color clearedColor = new(0.46f, 0.96f, 0.54f, 1f);
    [SerializeField] private Color accentColor = Color.white;

    private IFloorEscapeInteractionController interactionController;
    private bool hasRenderedState;
    private bool renderedVisible;
    private bool renderedUnlocked;
    private bool renderedEscaped;

    protected override float MaxInteractionDistance => interactionDistance;

    private void Awake()
    {
        CacheController();
        CacheSpriteRenderer();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void Reset()
    {
        CacheController();
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void OnValidate()
    {
        CacheController();
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void Update()
    {
        RefreshState();
    }

    public void Configure(IFloorEscapeInteractionController owner, FloorEscapeTransitionKind kind, Vector3 worldPosition, Color floorAccent)
    {
        BindController(owner);
        ConfigureInternal(kind, worldPosition, floorAccent);
    }

    public void ConfigureAuthored(IFloorEscapeInteractionController owner, FloorEscapeTransitionKind kind, Color floorAccent)
    {
        BindController(owner);
        CacheController();
        transitionKind = kind;
        accentColor = floorAccent;
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisual();
        RefreshState();
    }

    public bool TryGetInteractionController(out IFloorEscapeInteractionController controller)
    {
        CacheController();
        controller = interactionController;
        return controller != null;
    }

    private void ConfigureInternal(FloorEscapeTransitionKind kind, Vector3 worldPosition, Color floorAccent)
    {
        CacheController();
        transitionKind = kind;
        accentColor = floorAccent;
        CacheSpriteRenderer();
        transform.position = worldPosition;
        transform.localScale = kind == FloorEscapeTransitionKind.EmergencyStairs
            ? new Vector3(1.75f, 1f, 1f)
            : new Vector3(2.4f, 1.1f, 1f);
        gameObject.name = kind == FloorEscapeTransitionKind.EmergencyStairs ? "EmergencyStairs" : "StreetExit";
        InvalidateRenderedState();
        RefreshVisual();
        gameObject.SetActive(true);
        RefreshState();
    }

    public void RefreshState()
    {
        CacheController();
        CacheSpriteRenderer();
        bool visible = interactionController != null && interactionController.IsTransitionVisible(transitionKind);

        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }

        if (!visible || spriteRenderer == null || interactionController == null)
        {
            StoreRenderedState(visible, false, false);
            return;
        }

        bool escaped = interactionController.IsEscaped;
        bool unlocked = interactionController.IsTransitionUnlocked(transitionKind);

        if (hasRenderedState
            && renderedVisible
            && renderedUnlocked == unlocked
            && renderedEscaped == escaped
            && spriteRenderer.sprite != null)
        {
            return;
        }

        ApplyVisualState(unlocked, escaped);
        StoreRenderedState(true, unlocked, escaped);
    }

    public override bool CanInteract(WasdPlayerController playerController)
    {
        return CanInteractAtDistance(playerController, GetInteractionDistance(playerController));
    }

    public override bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        return base.CanInteractAtDistance(playerController, interactionDistance)
            && interactionController != null
            && interactionController.IsTransitionVisible(transitionKind);
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        CacheSpriteRenderer();

        if (spriteRenderer == null)
        {
            return base.GetInteractionDistance(playerController);
        }

        Vector2 playerPosition = playerController.transform.position;
        Vector2 interactionSurfacePoint = spriteRenderer.bounds.size.sqrMagnitude > 0.0001f
            ? spriteRenderer.bounds.ClosestPoint(playerPosition)
            : (Vector2)transform.position;
        return Vector2.Distance(playerPosition, interactionSurfacePoint);
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return InteractionPoint;
        }

        CacheSpriteRenderer();
        return spriteRenderer != null && spriteRenderer.bounds.size.sqrMagnitude > 0.0001f
            ? spriteRenderer.bounds.ClosestPoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (interactionController == null)
        {
            return "E Interact";
        }

        if (!interactionController.IsTransitionUnlocked(transitionKind))
        {
            return transitionKind == FloorEscapeTransitionKind.EmergencyStairs
                ? interactionController.UsesDirectAuthoredExitInteraction
                    ? "Need Iron Gate Key"
                    : "Recover tool to unlock stairs"
                : "Need Iron Gate Key";
        }

        return transitionKind == FloorEscapeTransitionKind.EmergencyStairs
            ? interactionController.UsesDirectAuthoredExitInteraction
                ? "E Use Exit"
                : "E Use Emergency Stairs"
            : "E Leave Through Exit";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        if (interactionController == null)
        {
            return;
        }

        if (transitionKind == FloorEscapeTransitionKind.EmergencyStairs)
        {
            interactionController.TryUseEmergencyStairs();
        }
        else
        {
            interactionController.TryUseFinalExit();
        }

        RefreshState();
    }

    private void RefreshVisual()
    {
        CacheSpriteRenderer();

        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(spriteRenderer);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);
        bool isUnlockedPreview = interactionController == null
            ? transitionKind == FloorEscapeTransitionKind.FinalExit
            : interactionController.IsTransitionUnlocked(transitionKind);
        ApplyVisualState(isUnlockedPreview, interactionController != null && interactionController.IsEscaped);
    }

    private void ApplyVisualState(bool unlocked, bool escaped)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(spriteRenderer);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);
        Color targetColor = escaped
            ? clearedColor
            : unlocked
                ? Color.Lerp(unlockedColor, accentColor, 0.3f)
                : lockedColor;

        if (spriteRenderer.color != targetColor)
        {
            spriteRenderer.color = targetColor;
        }

        ApplySceneMaterial(spriteRenderer);
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, SortingOrder);
    }

    private void CacheSpriteRenderer()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
    }

    private void CacheController()
    {
        interactionController = rebuildControllerSource as IFloorEscapeInteractionController;
    }

    private void BindController(IFloorEscapeInteractionController owner)
    {
        rebuildControllerSource = owner as MonoBehaviour;
    }

    private void StoreRenderedState(bool visible, bool unlocked, bool escaped)
    {
        hasRenderedState = true;
        renderedVisible = visible;
        renderedUnlocked = unlocked;
        renderedEscaped = escaped;
    }

    private void InvalidateRenderedState()
    {
        hasRenderedState = false;
    }

    private void RefreshVisualDeferredIfNeeded()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= RefreshVisualInEditor;
            EditorApplication.delayCall += RefreshVisualInEditor;
            return;
        }
#endif

        RefreshVisual();
    }

    private static void ApplySceneMaterial(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(renderer);
    }

#if UNITY_EDITOR
    private void RefreshVisualInEditor()
    {
        if (this == null)
        {
            return;
        }

        CacheSpriteRenderer();
        RefreshVisual();
    }
#endif
}

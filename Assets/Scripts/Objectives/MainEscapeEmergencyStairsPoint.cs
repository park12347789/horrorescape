using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MainEscapeEmergencyStairsPoint : PlayerInteractable2D
{
    private const int SortingOrder = 145;

    [FormerlySerializedAs("controller")]
    [SerializeField] private MonoBehaviour rebuildControllerSource;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.6f;
    [SerializeField] private Color lockedColor = new(0.84f, 0.34f, 0.28f, 1f);
    [SerializeField] private Color readyColor = new(0.38f, 0.9f, 0.98f, 1f);
    [SerializeField] private Color clearedColor = new(0.44f, 0.96f, 0.58f, 1f);

    private IFloorEscapeInteractionController interactionController;
    private bool hasRenderedState;
    private bool renderedVisible;
    private bool renderedUnlocked;
    private bool renderedEscaped;

    protected override float MaxInteractionDistance => interactionDistance;

    private void Awake()
    {
        CacheController();
        CacheRenderer();
        RefreshVisual();
    }

    private void Update()
    {
        RefreshState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheController();
        CacheRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void Reset()
    {
        CacheController();
        CacheRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void OnValidate()
    {
        CacheController();
        CacheRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    public void Configure(IFloorEscapeInteractionController owner)
    {
        BindController(owner);
        CacheController();
        CacheRenderer();
        InvalidateRenderedState();
        RefreshState();
    }

    public void RefreshState()
    {
        CacheController();
        CacheRenderer();
        bool visible = interactionController != null && interactionController.IsAuthoredStairsVisible;

        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }

        if (!visible)
        {
            StoreRenderedState(false, false, false);
            return;
        }

        bool escaped = interactionController.IsEscaped;
        bool unlocked = interactionController.IsTransitionUnlocked(FloorEscapeTransitionKind.EmergencyStairs);

        if (hasRenderedState
            && renderedVisible
            && renderedUnlocked == unlocked
            && renderedEscaped == escaped)
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
            && interactionController.IsAuthoredStairsVisible;
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        CacheRenderer();

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

        CacheRenderer();
        return spriteRenderer != null && spriteRenderer.bounds.size.sqrMagnitude > 0.0001f
            ? spriteRenderer.bounds.ClosestPoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override bool AllowsLineOfSightBlocker(Collider2D blocker, Vector2 hitPoint, WasdPlayerController playerController)
    {
        return interactionController != null
            && interactionController.IsTransitionUnlocked(FloorEscapeTransitionKind.EmergencyStairs);
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (interactionController == null)
        {
            return "E Interact";
        }

        if (interactionController.UsesDirectAuthoredExitInteraction)
        {
            return interactionController.IsTransitionUnlocked(FloorEscapeTransitionKind.EmergencyStairs)
                ? "E Use Exit"
                : "Need Iron Gate Key";
        }

        if (interactionController.UsesFinalClearPanelShortcut)
        {
            return "E Open Clear Panel";
        }

        if (interactionController.IsTransitionUnlocked(FloorEscapeTransitionKind.EmergencyStairs))
        {
            return "E Use Emergency Stairs";
        }

        return interactionController.IsAuthoredGateVisible && !interactionController.IsAuthoredGateUnlocked
            ? "Unlock Iron Gate First"
            : "Recover tool to unlock stairs";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        interactionController?.TryUseEmergencyStairs();
        RefreshState();
    }

    private void RefreshVisual()
    {
        CacheController();
        CacheRenderer();

        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, SortingOrder);

        if (interactionController == null)
        {
            return;
        }

        ApplyVisualState(
            interactionController.IsTransitionUnlocked(FloorEscapeTransitionKind.EmergencyStairs),
            interactionController.IsEscaped);
    }

    private void ApplyVisualState(bool unlocked, bool escaped)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, SortingOrder);

        Color targetColor = escaped
            ? clearedColor
            : unlocked
                ? readyColor
                : lockedColor;

        if (spriteRenderer.color != targetColor)
        {
            spriteRenderer.color = targetColor;
        }
    }

    private void CacheRenderer()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        spriteRenderer ??= GetComponentInChildren<SpriteRenderer>(true);
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

#if UNITY_EDITOR
    private void RefreshVisualInEditor()
    {
        if (this == null)
        {
            return;
        }

        CacheRenderer();
        RefreshVisual();
    }
#endif
}

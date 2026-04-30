using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MainEscapeKeyGatePoint : PlayerInteractable2D, IGateAnchorReadModel
{
    private const int SortingOrder = 145;

    [FormerlySerializedAs("controller")]
    [SerializeField] private MonoBehaviour rebuildControllerSource;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D blockerCollider;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.6f;
    [SerializeField] private Color lockedColor = new(0.86f, 0.28f, 0.24f, 1f);
    [SerializeField] private Color readyColor = new(0.96f, 0.78f, 0.24f, 1f);
    [SerializeField] private Color unlockedColor = new(0.42f, 0.92f, 0.58f, 1f);

    private IFloorEscapeInteractionController interactionController;
    private bool hasRenderedState;
    private bool renderedVisible;
    private bool renderedUnlocked;
    private bool renderedReadyToUnlock;

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
        CacheCollider();
        InvalidateRenderedState();
        RefreshState();
    }

    public void RefreshState()
    {
        CacheController();
        CacheRenderer();
        bool visible = interactionController != null
            && interactionController.IsAuthoredGateVisible
            && (interactionController.RequiresAuthoredGateInteraction
                ? !interactionController.IsAuthoredGateUnlocked
                : true);
        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }

        if (!visible)
        {
            StoreRenderedState(false, false, false);
            return;
        }

        bool unlocked = ResolveUnlockedState();
        bool readyToUnlock = interactionController.RequiresAuthoredGateInteraction && interactionController.HasAuthoredGateKey;

        if (hasRenderedState
            && renderedVisible
            && renderedUnlocked == unlocked
            && renderedReadyToUnlock == readyToUnlock)
        {
            return;
        }

        ApplyVisualState(unlocked, readyToUnlock);
        ApplyColliderState(unlocked);
        StoreRenderedState(true, unlocked, readyToUnlock);
    }

    public bool TryGetGateWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = transform.position;
        return true;
    }

    public override bool CanInteract(WasdPlayerController playerController)
    {
        return CanInteractAtDistance(playerController, GetInteractionDistance(playerController));
    }

    public override bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        return base.CanInteractAtDistance(playerController, interactionDistance)
            && interactionController != null
            && interactionController.IsAuthoredGateVisible
            && interactionController.RequiresAuthoredGateInteraction
            && !interactionController.IsAuthoredGateUnlocked;
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        CacheCollider();

        if (blockerCollider == null)
        {
            return base.GetInteractionDistance(playerController);
        }

        Vector2 playerPosition = playerController.transform.position;
        Vector2 interactionSurfacePoint = blockerCollider.enabled
            ? blockerCollider.ClosestPoint(playerPosition)
            : (Vector2)transform.position;
        return Vector2.Distance(playerPosition, interactionSurfacePoint);
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return InteractionPoint;
        }

        CacheCollider();
        return blockerCollider != null
            ? blockerCollider.ClosestPoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (interactionController == null)
        {
            return "E Interact";
        }

        if (interactionController.IsAuthoredGateUnlocked)
        {
            return "Iron Gate Open";
        }

        if (!interactionController.RequiresAuthoredGateInteraction)
        {
            return interactionController.IsTransitionUnlocked(FloorEscapeTransitionKind.EmergencyStairs)
                ? "Stairs Gate Open"
                : "Recover tool to open stairs gate";
        }

        return interactionController.HasAuthoredGateKey
            ? "E Unlock Iron Gate"
            : "Need Iron Gate Key";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        if (interactionController != null && interactionController.RequiresAuthoredGateInteraction)
        {
            interactionController.TryUnlockKeyGate();
        }

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

        bool unlocked = ResolveUnlockedState();
        bool readyToUnlock = interactionController.RequiresAuthoredGateInteraction && interactionController.HasAuthoredGateKey;

        ApplyVisualState(unlocked, readyToUnlock);
        RefreshCollider();
    }

    private void ApplyVisualState(bool unlocked, bool readyToUnlock)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, SortingOrder);

        Color targetColor = unlocked
            ? unlockedColor
            : readyToUnlock
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

    private void CacheCollider()
    {
        if (blockerCollider == null)
        {
            blockerCollider = GetComponent<BoxCollider2D>();
        }

        if (blockerCollider == null)
        {
            blockerCollider = GetComponentInChildren<BoxCollider2D>(true);
        }

        if (blockerCollider != null)
        {
            blockerCollider.isTrigger = false;
        }
    }

    private void RefreshCollider()
    {
        CacheCollider();

        if (blockerCollider == null)
        {
            return;
        }

        if (interactionController == null)
        {
            blockerCollider.enabled = true;
            return;
        }

        ApplyColliderState(ResolveUnlockedState());
    }

    private void ApplyColliderState(bool unlocked)
    {
        CacheCollider();

        if (blockerCollider == null)
        {
            return;
        }

        bool shouldEnable = !unlocked;

        if (blockerCollider.enabled != shouldEnable)
        {
            blockerCollider.enabled = shouldEnable;
        }
    }

    private bool ResolveUnlockedState()
    {
        return interactionController != null
            && (interactionController.RequiresAuthoredGateInteraction
                ? interactionController.IsAuthoredGateUnlocked
                : interactionController.IsTransitionUnlocked(FloorEscapeTransitionKind.EmergencyStairs));
    }

    private void StoreRenderedState(bool visible, bool unlocked, bool readyToUnlock)
    {
        hasRenderedState = true;
        renderedVisible = visible;
        renderedUnlocked = unlocked;
        renderedReadyToUnlock = readyToUnlock;
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

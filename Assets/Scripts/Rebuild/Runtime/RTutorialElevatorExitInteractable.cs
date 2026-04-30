using UnityEngine;

[DisallowMultipleComponent]
public sealed class RTutorialElevatorExitInteractable : PlayerInteractable2D
{
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.75f;
    [SerializeField] private bool requiresItem = true;
    [SerializeField] private string requiredItemId = PrototypeItemCatalog.IronGateKeyItemId;
    [SerializeField] private bool consumeRequiredItemBeforeExit;
    [SerializeField] private bool showPromptText = true;
    [SerializeField] private string readyPrompt = "E Use Elevator";
    [SerializeField] private string missingItemPrompt = "Need Iron Gate Key";

    protected override float MaxInteractionDistance => interactionDistance;

    public override Vector2 InteractionPoint => interactionAnchor != null
        ? interactionAnchor.position
        : transform.position;

    public void Configure(
        bool requiresConfiguredItem,
        string configuredRequiredItemId,
        bool configuredShowPromptText,
        bool configuredConsumeRequiredItemBeforeExit)
    {
        requiresItem = requiresConfiguredItem;
        requiredItemId = string.IsNullOrWhiteSpace(configuredRequiredItemId)
            ? PrototypeItemCatalog.IronGateKeyItemId
            : configuredRequiredItemId;
        showPromptText = configuredShowPromptText;
        consumeRequiredItemBeforeExit = configuredConsumeRequiredItemBeforeExit;
        CacheVisuals();
    }

    private void Reset()
    {
        CacheVisuals();
    }

    private void Awake()
    {
        CacheVisuals();
    }

    private void OnValidate()
    {
        CacheVisuals();
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        return Vector2.Distance(
            playerController.transform.position,
            ResolveInteractionSurfacePoint(playerController.transform.position));
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        return playerController != null
            ? ResolveInteractionSurfacePoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (!showPromptText)
        {
            return string.Empty;
        }

        return HasRequiredItem(playerController) ? readyPrompt : missingItemPrompt;
    }

    public override void Interact(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return;
        }

        PlayerInventory inventory = playerController.GetComponent<PlayerInventory>();

        if (!HasRequiredItem(inventory))
        {
            PrototypeAudioManager.TryPlayDenied();
            return;
        }

        if (consumeRequiredItemBeforeExit && requiresItem && inventory != null)
        {
            inventory.RemoveItem(requiredItemId);
        }

        RRunSessionController sessionController = ResolveSceneSessionController();

        if (sessionController == null)
        {
            Debug.LogError($"{nameof(RTutorialElevatorExitInteractable)} cannot leave the tutorial because no {nameof(RRunSessionController)} is available.", this);
            PrototypeAudioManager.TryPlayDenied();
            return;
        }

        RPlayerRuntimeReferences runtime = RPlayerRuntimeReferences.Resolve(playerController);
        sessionController.UnbindGameplayRuntime(runtime);
        sessionController.StartNewRunFromTutorialAndLoadStartFloor();
    }

    private RRunSessionController ResolveSceneSessionController()
    {
        return RRunSessionResolver.ResolveForContext(this);
    }

    private bool HasRequiredItem(WasdPlayerController playerController)
    {
        return HasRequiredItem(playerController != null ? playerController.GetComponent<PlayerInventory>() : null);
    }

    private bool HasRequiredItem(PlayerInventory inventory)
    {
        return !requiresItem
            || string.IsNullOrWhiteSpace(requiredItemId)
            || (inventory != null && inventory.HasItem(requiredItemId));
    }

    private void CacheVisuals()
    {
        if (interactionAnchor == null)
        {
            interactionAnchor = transform;
        }

        targetRenderer ??= interactionAnchor.GetComponentInChildren<SpriteRenderer>(true);
    }

    private Vector2 ResolveInteractionSurfacePoint(Vector2 playerPosition)
    {
        CacheVisuals();

        if (targetRenderer != null && targetRenderer.bounds.size.sqrMagnitude > 0.0001f)
        {
            return targetRenderer.bounds.ClosestPoint(playerPosition);
        }

        Collider2D exitCollider = GetComponent<Collider2D>();

        if (exitCollider != null && exitCollider.enabled)
        {
            return exitCollider.ClosestPoint(playerPosition);
        }

        return InteractionPoint;
    }
}

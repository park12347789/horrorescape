using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeElevatorExitInteractable : PlayerInteractable2D
{
    [SerializeField] private MonoBehaviour rebuildControllerSource;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Transform interactionAnchor;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.75f;

    private IFloorEscapeInteractionController interactionController;

    protected override float MaxInteractionDistance => interactionDistance;

    private void Awake()
    {
        CacheController();
        CacheVisuals();
    }

    private void OnValidate()
    {
        CacheController();
        CacheVisuals();
    }

    public void Configure(IFloorEscapeInteractionController owner, SpriteRenderer visualRenderer, Transform anchor)
    {
        rebuildControllerSource = owner as MonoBehaviour;

        if (visualRenderer != null)
        {
            targetRenderer = visualRenderer;
        }

        if (anchor != null)
        {
            interactionAnchor = anchor;
        }

        CacheController();
        CacheVisuals();
    }

    public override bool CanInteract(WasdPlayerController playerController)
    {
        return CanInteractAtDistance(playerController, GetInteractionDistance(playerController));
    }

    public override bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        return base.CanInteractAtDistance(playerController, interactionDistance)
            && interactionController != null
            && interactionController.UsesDirectAuthoredExitInteraction
            && !interactionController.IsEscaped;
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        return Vector2.Distance(playerController.transform.position, ResolveInteractionSurfacePoint(playerController.transform.position));
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return InteractionPoint;
        }

        return ResolveInteractionSurfacePoint(playerController.transform.position);
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (interactionController == null)
        {
            return "E Use Elevator";
        }

        return interactionController.HasAuthoredGateKey
            ? "E Use Elevator"
            : "Need Iron Gate Key";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        interactionController?.TryUseEmergencyStairs();
    }

    private void CacheController()
    {
        interactionController = rebuildControllerSource as IFloorEscapeInteractionController;
    }

    private void CacheVisuals()
    {
        if (interactionAnchor == null)
        {
            interactionAnchor = transform.parent != null ? transform.parent : transform;
        }

        targetRenderer ??= interactionAnchor != null
            ? interactionAnchor.GetComponentInChildren<SpriteRenderer>(true)
            : null;
    }

    private Vector2 ResolveInteractionSurfacePoint(Vector2 playerPosition)
    {
        CacheVisuals();

        if (targetRenderer != null && targetRenderer.bounds.size.sqrMagnitude > 0.0001f)
        {
            return targetRenderer.bounds.ClosestPoint(playerPosition);
        }

        return interactionAnchor != null
            ? (Vector2)interactionAnchor.position
            : (Vector2)transform.position;
    }
}

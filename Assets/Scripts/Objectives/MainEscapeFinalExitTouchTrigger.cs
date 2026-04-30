using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class MainEscapeFinalExitTouchTrigger : MonoBehaviour
{
    [SerializeField] private RRunController runController;
    [SerializeField] private float retryCooldownSeconds = 0.35f;

    private float nextAttemptTime;
    private float nextReferenceResolveTime;

    private void Awake()
    {
        ConfigureTriggerCollider();
    }

    private void OnValidate()
    {
        ConfigureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHandleTouch(other);
    }

    private void ConfigureTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void TryHandleTouch(Collider2D other)
    {
        if (Time.time < nextAttemptTime)
        {
            return;
        }

        if (!TryResolvePlayer(other, out _))
        {
            return;
        }

        if (!TryResolveRunController(force: false) || runController.IsEscaped)
        {
            return;
        }

        bool didEscape = runController.TryUseFinalExit();
        nextAttemptTime = Time.time + retryCooldownSeconds;

        if (didEscape)
        {
            enabled = false;
        }
    }

    private bool TryResolveRunController(bool force)
    {
        if (IsSceneReference(runController))
        {
            return true;
        }

        runController = null;

        if (!force && Application.isPlaying && Time.time < nextReferenceResolveTime)
        {
            return false;
        }

        if (TryResolveRunControllerFromTransitionPoint(out runController))
        {
            return true;
        }

        runController = RSceneReferenceLookup.FindFirstComponentInScene<RRunController>(gameObject.scene);

        if (runController == null && Application.isPlaying)
        {
            nextReferenceResolveTime = Time.time + retryCooldownSeconds;
        }

        return runController != null;
    }

    private bool TryResolveRunControllerFromTransitionPoint(out RRunController resolvedRunController)
    {
        resolvedRunController = null;
        FloorEscapeTransitionPoint transitionPoint = GetComponent<FloorEscapeTransitionPoint>();

        if (transitionPoint == null
            || !transitionPoint.TryGetInteractionController(out IFloorEscapeInteractionController interactionController))
        {
            return false;
        }

        resolvedRunController = interactionController as RRunController;
        return IsSceneReference(resolvedRunController);
    }

    private bool IsSceneReference(Component component)
    {
        return component != null && component.gameObject.scene == gameObject.scene;
    }

    private static bool TryResolvePlayer(Collider2D other, out WasdPlayerController playerController)
    {
        playerController = null;

        if (other == null)
        {
            return false;
        }

        playerController = other.GetComponent<WasdPlayerController>();

        if (playerController != null)
        {
            return true;
        }

        playerController = other.GetComponentInParent<WasdPlayerController>();
        return playerController != null;
    }
}

using UnityEngine;

public sealed partial class RSceneCompositionRoot
{
    [Header("Run Refactor")]
    [SerializeField] private MonoBehaviour playerStateStoreSource;

    private RRunSceneBindings BuildRunSceneBindings()
    {
        return RRunSceneBindings.Create(
            playerController,
            playerRuntime,
            playerRuntime != null ? playerRuntime.Inventory : null,
            goalPickup,
            ResolveLegacyEmergencyStairsProxyBinding(),
            finalExitPoint,
            batteryPickup,
            bottlePickup,
            medkitPickup,
            keyGatePoint,
            authoredStairsPoint,
            fogOfWarOverlay,
            accentBackdrop,
            floorDirector,
            hudCanvas as MonoBehaviour);
    }

    private FloorEscapeTransitionPoint ResolveLegacyEmergencyStairsProxyBinding()
    {
        // Canonical authored floors route stairs through MainEscapeEmergencyStairsPoint.
        // Keep forwarding the legacy proxy only when no authored owner is bound.
        return authoredStairsPoint == null ? emergencyStairsPoint : null;
    }

    private void BindOptionalPlayerStateStore()
    {
        if (runSessionController == null)
        {
            return;
        }

        MonoBehaviour resolvedSource = ResolvePlayerStateStoreSource();

        if (resolvedSource != null)
        {
            runSessionController.BindPlayerStateStore(resolvedSource);
        }
    }

    private MonoBehaviour ResolvePlayerStateStoreSource()
    {
        if (playerStateStoreSource != null)
        {
            if (playerStateStoreSource is IRunPlayerStateStore)
            {
                return playerStateStoreSource;
            }

            Debug.LogError(
                $"{nameof(RSceneCompositionRoot)} has a player state store source that does not implement {nameof(IRunPlayerStateStore)}.",
                playerStateStoreSource);
            return null;
        }

        if (runSessionController != null)
        {
            MonoBehaviour[] sessionBehaviours = runSessionController.GetComponents<MonoBehaviour>();

            for (int index = 0; index < sessionBehaviours.Length; index++)
            {
                if (sessionBehaviours[index] is IRunPlayerStateStore)
                {
                    playerStateStoreSource = sessionBehaviours[index];
                    return playerStateStoreSource;
                }
            }
        }

        MonoBehaviour[] localBehaviours = GetComponents<MonoBehaviour>();

        for (int index = 0; index < localBehaviours.Length; index++)
        {
            if (localBehaviours[index] is IRunPlayerStateStore)
            {
                playerStateStoreSource = localBehaviours[index];
                return playerStateStoreSource;
            }
        }

        return null;
    }
}

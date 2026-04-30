using UnityEngine;
using UnityEngine.SceneManagement;

public static class RRunSessionResolver
{
    public static RRunSessionController ResolveForContext(
        MonoBehaviour context,
        RRunSessionController assignedSessionController = null)
    {
        return ResolveForScene(
            context != null ? context.gameObject.scene : default,
            assignedSessionController);
    }

    public static RRunSessionController ResolveForScene(
        Scene scene,
        RRunSessionController assignedSessionController = null)
    {
        if (assignedSessionController != null)
        {
            return assignedSessionController;
        }

        RRunSessionController sceneSessionController =
            RSceneReferenceLookup.FindFirstComponentInScene<RRunSessionController>(scene);

        if (sceneSessionController != null)
        {
            return sceneSessionController;
        }

        return RRunSessionController.TryGetCachedInstance(out RRunSessionController cachedSessionController)
            ? cachedSessionController
            : null;
    }
}

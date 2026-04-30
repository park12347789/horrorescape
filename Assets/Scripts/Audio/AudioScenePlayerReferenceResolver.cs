using UnityEngine;
using UnityEngine.SceneManagement;

public static class AudioScenePlayerReferenceResolver
{
    public static WasdPlayerController ResolveCurrentOrSceneFallback(
        WasdPlayerController current,
        GameObject owner)
    {
        if (IsUsableForOwnerScene(current, owner))
        {
            return current;
        }

        return FindFirstPlayerInOwnerScene(owner);
    }

    public static bool IsUsableForOwnerScene(WasdPlayerController playerController, GameObject owner)
    {
        if (playerController == null || owner == null)
        {
            return false;
        }

        Scene ownerScene = owner.scene;
        return ownerScene.IsValid()
            && ownerScene.isLoaded
            && playerController.gameObject.scene == ownerScene;
    }

    private static WasdPlayerController FindFirstPlayerInOwnerScene(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        Scene ownerScene = owner.scene;

        if (!ownerScene.IsValid() || !ownerScene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = ownerScene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            WasdPlayerController playerController = roots[index].GetComponentInChildren<WasdPlayerController>(false);

            if (playerController != null)
            {
                return playerController;
            }
        }

        return null;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainEscapePlayerSpawnUtility
{
    public static WasdPlayerController SpawnPlayerFromCatalog(
        Scene scene,
        Transform parent,
        UnityEngine.Object logContext,
        string ownerLabel,
        bool destroyExistingPlayers)
    {
        MainEscapeRuntimePrefabCatalog catalog = MainEscapeRuntimePrefabCatalog.LoadForScene(scene);
        GameObject playerPrefab = catalog != null ? catalog.PlayerPrefab : null;

        if (playerPrefab == null)
        {
            Debug.LogError(
                $"{ownerLabel} requires an authored player prefab in {nameof(MainEscapeRuntimePrefabCatalog)}.",
                catalog != null ? (UnityEngine.Object)catalog : logContext);
            return null;
        }

        WasdPlayerController[] existingPlayers = destroyExistingPlayers
            ? RSceneReferenceLookup.FindComponentsInScene<WasdPlayerController>(scene)
            : null;

        GameObject playerInstance = UnityEngine.Object.Instantiate(playerPrefab, parent, false);
        playerInstance.name = "Player";
        playerInstance.transform.localPosition = Vector3.zero;
        playerInstance.transform.localRotation = Quaternion.identity;

        if (parent == null && scene.IsValid() && playerInstance.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(playerInstance, scene);
        }

        if (destroyExistingPlayers && existingPlayers != null)
        {
            for (int index = 0; index < existingPlayers.Length; index++)
            {
                WasdPlayerController existingPlayer = existingPlayers[index];

                if (existingPlayer == null || existingPlayer.gameObject == playerInstance)
                {
                    continue;
                }

                UnityEngine.Object.Destroy(existingPlayer.gameObject);
            }
        }

        return playerInstance.GetComponent<WasdPlayerController>();
    }
}

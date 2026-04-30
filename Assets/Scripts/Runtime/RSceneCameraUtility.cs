using UnityEngine;
using UnityEngine.SceneManagement;

public static class RSceneCameraUtility
{
    private const string MainCameraTag = "MainCamera";

    public static Camera FindPreferredCameraInScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        Camera[] cameras = RSceneReferenceLookup.FindComponentsInScene<Camera>(scene);
        Camera firstEnabledCamera = null;
        Camera firstCamera = null;

        for (int index = 0; index < cameras.Length; index++)
        {
            Camera candidate = cameras[index];

            if (candidate == null || candidate.gameObject.scene != scene)
            {
                continue;
            }

            firstCamera ??= candidate;

            if (!candidate.isActiveAndEnabled)
            {
                continue;
            }

            firstEnabledCamera ??= candidate;

            if (candidate.CompareTag(MainCameraTag))
            {
                return candidate;
            }
        }

        return firstEnabledCamera != null ? firstEnabledCamera : firstCamera;
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MainEscapeDebugModeTools
{
    [MenuItem("Tools/Main Escape/Toggle Active Scene Debug Mode")]
    private static void ToggleActiveSceneDebugMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter play mode before toggling MainEscape debug mode.");
            return;
        }

        MainEscapeDebugModeController controller = Object.FindFirstObjectByType<MainEscapeDebugModeController>();

        if (controller == null)
        {
            Debug.LogError("Could not find MainEscapeDebugModeController in the active scene.");
            return;
        }

        controller.SetDebugModeEnabled(!controller.DebugModeEnabled);
    }
}
#endif

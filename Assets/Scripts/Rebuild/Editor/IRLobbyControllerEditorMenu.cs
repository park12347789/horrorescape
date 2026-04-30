using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IRLobbyControllerEditorMenu
{
    [MenuItem("Tools/Main Escape/Materialize Lobby Modal UI")]
    private static void MaterializeLobbyModalUiMenu()
    {
        IRLobbyController controller =
            RSceneReferenceLookup.FindFirstComponentInScene<IRLobbyController>(EditorSceneManager.GetActiveScene());

        if (controller == null)
        {
            Debug.LogError($"{nameof(IRLobbyController)} could not be found in the open scene.");
            return;
        }

        controller.MaterializeLobbyModalUiForAuthoring();
    }
}

using UnityEngine;

public static class MainEscapeWorkspaceUtility
{
    public static void DisableEditorOnlyWorkspaceRoots(Transform workspaceRoot, string[] editorOnlyRootNames)
    {
        if (workspaceRoot == null || editorOnlyRootNames == null || editorOnlyRootNames.Length == 0)
        {
            return;
        }

        for (int index = 0; index < editorOnlyRootNames.Length; index++)
        {
            string rootName = editorOnlyRootNames[index];

            if (string.IsNullOrWhiteSpace(rootName))
            {
                continue;
            }

            Transform candidate = RSceneReferenceLookup.FindDirectChild(workspaceRoot, rootName);

            if (candidate == null || !candidate.gameObject.activeSelf)
            {
                continue;
            }

            candidate.gameObject.SetActive(false);
        }
    }
}

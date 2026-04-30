using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoadUtility
{
    public static void LoadSceneByPathOrName(string scenePath, string errorPrefix)
    {
        LoadSceneByPathOrName(scenePath, errorPrefix, null);
    }

    public static void LoadSceneByPathOrName(string scenePath, string errorPrefix, string emptyPathErrorMessage)
    {
        if (TryResolveSceneBuildIndex(scenePath, errorPrefix, emptyPathErrorMessage, out int buildIndex))
        {
            SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
        }
    }

    public static AsyncOperation LoadSceneAsyncByPathOrName(string scenePath, string errorPrefix, string emptyPathErrorMessage)
    {
        return TryResolveSceneBuildIndex(scenePath, errorPrefix, emptyPathErrorMessage, out int buildIndex)
            ? SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single)
            : null;
    }

    public static bool TryResolveSceneBuildIndex(string scenePath, string errorPrefix, string emptyPathErrorMessage, out int buildIndex)
    {
        Time.timeScale = 1f;
        buildIndex = -1;
        string normalizedScenePath = scenePath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedScenePath))
        {
            Debug.LogError(string.IsNullOrWhiteSpace(emptyPathErrorMessage)
                ? $"{errorPrefix} could not resolve scene from path '{scenePath}'."
                : emptyPathErrorMessage);
            return false;
        }

        buildIndex = SceneUtility.GetBuildIndexByScenePath(normalizedScenePath);

        if (buildIndex >= 0)
        {
            return true;
        }

        string sceneName = Path.GetFileNameWithoutExtension(normalizedScenePath);
        Debug.LogError($"{errorPrefix} could not resolve scene path '{normalizedScenePath}' in build settings{(string.IsNullOrWhiteSpace(sceneName) ? "." : $" for scene '{sceneName}'.")}");
        return false;
    }

    public static void ReloadScene(
        Scene scene,
        bool includeScenePath,
        string reloadErrorMessage,
        string loadSceneErrorPrefix,
        string emptyPathErrorMessage = null)
    {
        Time.timeScale = 1f;

        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
            return;
        }

        if (includeScenePath && !string.IsNullOrWhiteSpace(scene.path))
        {
            LoadSceneByPathOrName(scene.path, loadSceneErrorPrefix, emptyPathErrorMessage);
            return;
        }

        Debug.LogError(reloadErrorMessage);
    }
}

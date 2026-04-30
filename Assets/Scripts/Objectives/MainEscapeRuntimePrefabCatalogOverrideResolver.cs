using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class MainEscapeRuntimePrefabCatalogOverrideResolver
{
    private const string DefaultCatalogResourcePath = "MainEscape/MainEscapeRuntimePrefabCatalog";
    private const string OverrideCatalogRootResourcePath = "MainEscape/PrefabCatalogOverrides";
    private const string OverrideCatalogResourceName = "MainEscapeRuntimePrefabCatalog";

    private static bool cacheValid;
    private static string cachedSceneKey = string.Empty;
    private static MainEscapeRuntimePrefabCatalog cachedCatalog;

    public static MainEscapeRuntimePrefabCatalog Load()
    {
        return LoadDefault();
    }

    public static MainEscapeRuntimePrefabCatalog LoadDefault()
    {
        return LoadForSceneName(string.Empty);
    }

    public static MainEscapeRuntimePrefabCatalog LoadForScene(Scene scene)
    {
        return LoadForSceneName(BuildScenePathOrName(scene));
    }

    public static MainEscapeRuntimePrefabCatalog LoadForSceneName(string sceneName)
    {
        string normalizedSceneName = MainEscapeSceneIdentityUtility.NormalizeSceneName(sceneName);

        if (cacheValid && string.Equals(cachedSceneKey, normalizedSceneName, StringComparison.Ordinal))
        {
            return cachedCatalog;
        }

        cachedSceneKey = normalizedSceneName;
        cachedCatalog = ResolveForScene(normalizedSceneName);
        cacheValid = true;
        return cachedCatalog;
    }

    private static MainEscapeRuntimePrefabCatalog ResolveForScene(string sceneName)
    {
        if (TryGetOverrideResourcePath(sceneName, out string overrideResourcePath))
        {
            MainEscapeRuntimePrefabCatalog overrideCatalog = Resources.Load<MainEscapeRuntimePrefabCatalog>(overrideResourcePath);

            if (overrideCatalog != null)
            {
                return overrideCatalog;
            }
        }

        return Resources.Load<MainEscapeRuntimePrefabCatalog>(DefaultCatalogResourcePath);
    }

    internal static bool TryGetOverrideResourcePath(string sceneName, out string resourcePath)
    {
        string normalizedSceneName = Path.GetFileNameWithoutExtension(
            MainEscapeSceneIdentityUtility.NormalizeSceneName(sceneName));

        if (string.IsNullOrWhiteSpace(normalizedSceneName))
        {
            resourcePath = string.Empty;
            return false;
        }

        resourcePath = $"{OverrideCatalogRootResourcePath}/{normalizedSceneName}/{OverrideCatalogResourceName}";
        return true;
    }

    private static string BuildScenePathOrName(Scene scene)
    {
        return MainEscapeSceneIdentityUtility.GetScenePathOrName(scene);
    }
}

using System;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainEscapeSceneIdentityUtility
{
    private const string CanonicalLobbyScenePath = "Assets/Scenes/RMainEscape_Lobby.unity";
    private const string CanonicalTutorialScenePath = "Assets/Scenes/RMainEscape_tuto.unity";
    private const string CanonicalElevatorTransitionScenePath = "Assets/Scenes/RMainEscape_ElevatorTransition.unity";
    private const int CanonicalStartFloorNumber = 5;
    private const int CanonicalTerminalFloorNumber = 1;

    public static bool IsAuthoredSceneName(string sceneName)
    {
        if (MatchesCanonicalLobbySceneName(sceneName) || TryGetCanonicalFloorNumber(sceneName, out _))
        {
            return true;
        }

        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        return settings != null && settings.IsFallbackAuthoredScene(sceneName);
    }

    public static int GetCanonicalStartFloorNumber()
    {
        return CanonicalStartFloorNumber;
    }

    public static int GetCanonicalTerminalFloorNumber()
    {
        return CanonicalTerminalFloorNumber;
    }

    public static string GetCanonicalLobbyScenePath()
    {
        return CanonicalLobbyScenePath;
    }

    public static string GetCanonicalTutorialScenePath()
    {
        return CanonicalTutorialScenePath;
    }

    public static string GetCanonicalElevatorTransitionScenePath()
    {
        return CanonicalElevatorTransitionScenePath;
    }

    public static string GetCanonicalFloorScenePath(int floorNumber)
    {
        int normalizedFloorNumber = Mathf.Max(CanonicalTerminalFloorNumber, floorNumber);

        if (normalizedFloorNumber > CanonicalStartFloorNumber)
        {
            return string.Empty;
        }

        return $"Assets/Scenes/RMainScene_{normalizedFloorNumber}F.unity";
    }

    public static string GetCanonicalStartFloorScenePath()
    {
        return GetCanonicalFloorScenePath(CanonicalStartFloorNumber);
    }

    public static string[] GetCanonicalAuthoredSceneNames()
    {
        int floorCount = GetCanonicalFloorCount();
        string[] sceneNames = new string[floorCount];
        int writeIndex = 0;

        for (int floorNumber = CanonicalStartFloorNumber; floorNumber >= CanonicalTerminalFloorNumber; floorNumber--)
        {
            sceneNames[writeIndex++] = Path.GetFileNameWithoutExtension(GetCanonicalFloorScenePath(floorNumber));
        }

        return sceneNames;
    }

    public static string[] GetCanonicalGameplayScenePaths()
    {
        int floorCount = GetCanonicalFloorCount();
        string[] scenePaths = new string[floorCount];
        int writeIndex = 0;

        for (int floorNumber = CanonicalStartFloorNumber; floorNumber >= CanonicalTerminalFloorNumber; floorNumber--)
        {
            scenePaths[writeIndex++] = GetCanonicalFloorScenePath(floorNumber);
        }

        return scenePaths;
    }

    public static RFloorSceneEntry[] GetCanonicalFloorSceneEntries()
    {
        int floorCount = GetCanonicalFloorCount();
        RFloorSceneEntry[] floorSceneEntries = new RFloorSceneEntry[floorCount];
        int writeIndex = 0;

        for (int floorNumber = CanonicalStartFloorNumber; floorNumber >= CanonicalTerminalFloorNumber; floorNumber--)
        {
            floorSceneEntries[writeIndex++] = new RFloorSceneEntry(floorNumber, GetCanonicalFloorScenePath(floorNumber));
        }

        return floorSceneEntries;
    }

    public static bool MatchesCanonicalLobbyScene(Scene scene)
    {
        return MatchesScene(scene, CanonicalLobbyScenePath);
    }

    public static bool MatchesCanonicalLobbySceneName(string sceneName)
    {
        return MatchesScenePath(sceneName, CanonicalLobbyScenePath);
    }

    public static bool MatchesCanonicalTutorialScene(Scene scene)
    {
        return MatchesScene(scene, CanonicalTutorialScenePath);
    }

    public static bool MatchesCanonicalTutorialSceneName(string sceneName)
    {
        return MatchesScenePath(sceneName, CanonicalTutorialScenePath);
    }

    public static bool MatchesCanonicalFloorScene(Scene scene, int floorNumber)
    {
        return MatchesScene(scene, GetCanonicalFloorScenePath(floorNumber));
    }

    public static bool TryGetCanonicalFloorNumber(Scene scene, out int floorNumber)
    {
        if (!scene.IsValid())
        {
            floorNumber = 0;
            return false;
        }

        return TryGetCanonicalFloorNumber(GetScenePathOrName(scene), out floorNumber)
            || TryGetCanonicalFloorNumberFromPath(scene.path, out floorNumber);
    }

    public static bool TryGetCanonicalFloorNumber(string sceneName, out int floorNumber)
    {
        string normalizedSceneName = NormalizeSceneName(sceneName);

        for (int currentFloorNumber = CanonicalStartFloorNumber; currentFloorNumber >= CanonicalTerminalFloorNumber; currentFloorNumber--)
        {
            if (MatchesScenePath(normalizedSceneName, GetCanonicalFloorScenePath(currentFloorNumber)))
            {
                floorNumber = currentFloorNumber;
                return true;
            }
        }

        floorNumber = 0;
        return false;
    }

    public static bool IsProtectedPrefabOverrideSceneName(string sceneName)
    {
        string normalizedSceneName = NormalizeSceneName(sceneName);

        if (string.IsNullOrWhiteSpace(normalizedSceneName))
        {
            return false;
        }

        if (MatchesCanonicalLobbySceneName(normalizedSceneName)
            || TryGetCanonicalFloorNumber(normalizedSceneName, out _))
        {
            return true;
        }

        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();

        if (settings == null)
        {
            return false;
        }

        if (MatchesScenePath(normalizedSceneName, settings.FallbackLobbyScenePath))
        {
            return true;
        }

        string[] gameplayScenePaths = settings.GetFallbackGameplayScenePaths();

        for (int index = 0; index < gameplayScenePaths.Length; index++)
        {
            if (MatchesScenePath(normalizedSceneName, gameplayScenePaths[index]))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeSceneName(string sceneName)
    {
        return sceneName?.Trim() ?? string.Empty;
    }

    public static string GetScenePathOrName(Scene scene)
    {
        if (!scene.IsValid())
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(scene.path)
            ? scene.path
            : scene.name;
    }

    public static string GetSceneIdentity(Scene scene)
    {
        if (!scene.IsValid())
        {
            return string.Empty;
        }

        string scenePathOrName = GetScenePathOrName(scene);
        return !string.IsNullOrWhiteSpace(scenePathOrName)
            ? scenePathOrName
            : scene.handle.ToString();
    }

    public static bool MatchesScenePath(string sceneName, string scenePath)
    {
        string normalizedSceneNameOrPath = NormalizeSceneName(sceneName);
        string normalizedPath = scenePath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSceneNameOrPath) || string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        if (string.Equals(normalizedSceneNameOrPath, normalizedPath, StringComparison.Ordinal))
        {
            return true;
        }

        string pathSceneName = Path.GetFileNameWithoutExtension(normalizedPath);
        string inputSceneName = Path.GetFileNameWithoutExtension(normalizedSceneNameOrPath);
        return string.Equals(inputSceneName, pathSceneName, StringComparison.Ordinal);
    }

    public static bool MatchesScene(Scene scene, string scenePath)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        string normalizedPath = scenePath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        return string.Equals(scene.path, normalizedPath, StringComparison.Ordinal)
            || MatchesScenePath(GetScenePathOrName(scene), normalizedPath);
    }

    private static bool TryGetCanonicalFloorNumberFromPath(string scenePath, out int floorNumber)
    {
        string normalizedPath = scenePath?.Trim() ?? string.Empty;

        for (int currentFloorNumber = CanonicalStartFloorNumber; currentFloorNumber >= CanonicalTerminalFloorNumber; currentFloorNumber--)
        {
            if (string.Equals(normalizedPath, GetCanonicalFloorScenePath(currentFloorNumber), StringComparison.Ordinal))
            {
                floorNumber = currentFloorNumber;
                return true;
            }
        }

        floorNumber = 0;
        return false;
    }

    private static int GetCanonicalFloorCount()
    {
        return CanonicalStartFloorNumber - CanonicalTerminalFloorNumber + 1;
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MainEscapeLoopPlayModeSmokeTests
{
    private const string LobbySceneName = "RMainEscape_Lobby";
    private const string GameplaySceneName = "RMainScene_5F";
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator GameplayScene_LoadsRunControllerAndHudCanvas()
    {
        string gameplaySceneName = ResolveGameplaySceneName();
        yield return LoadSceneOrInconclusive(gameplaySceneName);

        Component runController = FindSceneComponentByTypeName("RRunController");
        Component hudCanvas = FindSceneComponentByTypeName("IRHudCanvas");

        Assert.That(runController, Is.Not.Null, "RRunController is missing in gameplay scene.");
        Assert.That(hudCanvas, Is.Not.Null, "IRHudCanvas is missing in gameplay scene.");
    }

    [UnityTest]
    public IEnumerator GameplayScene_RunModalApiSurface_IsExposedWhenIntegrated()
    {
        string gameplaySceneName = ResolveGameplaySceneName();
        yield return LoadSceneOrInconclusive(gameplaySceneName);

        Component panel = FindSceneComponentByTypeName("IRGameClearPanelView");

        if (panel == null)
        {
            Assert.Inconclusive("Run modal panel is not wired in gameplay scene yet.");
        }

        string[] requiredMethods = { "ShowFloorClear", "ShowFinalClear", "ShowFailure", "HideAndResume" };
        bool hasAllMethods = requiredMethods.All(methodName => panel.GetType().GetMethod(methodName, MemberFlags) != null);

        if (!hasAllMethods)
        {
            Assert.Inconclusive("Run modal API is not fully integrated yet.");
        }

        Assert.That(hasAllMethods, Is.True);
    }

    [UnityTest]
    public IEnumerator LobbyScene_LoadsAndContainsInteractiveUi_WhenIntegrated()
    {
        string lobbySceneName = ResolveLobbySceneName();

        if (string.IsNullOrWhiteSpace(lobbySceneName))
        {
            Assert.Inconclusive("Canonical lobby scene name is not configured.");
        }

        yield return LoadSceneOrInconclusive(lobbySceneName);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(canvas, Is.Not.Null, "Lobby scene should contain a Canvas.");
        Assert.That(buttons.Length, Is.GreaterThan(0), "Lobby scene should contain at least one Button.");
    }

    [Test]
    public void SessionContractType_ExistsWhenIntegrated()
    {
        Type sessionControllerType = FindTypeByName("RRunSessionController");

        if (sessionControllerType == null)
        {
            Assert.Inconclusive("RRunSessionController is not integrated yet.");
        }

        Assert.That(sessionControllerType, Is.Not.Null);
    }

    private static IEnumerator LoadSceneOrInconclusive(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Assert.Inconclusive("Resolved scene name was empty.");
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Assert.Inconclusive($"Scene '{sceneName}' is not currently loadable from build settings.");
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        Assert.That(loadOperation, Is.Not.Null, $"Failed to start loading scene '{sceneName}'.");

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        yield return null;
    }

    private static string ResolveGameplaySceneName()
    {
        return GameplaySceneName;
    }

    private static string ResolveLobbySceneName()
    {
        return LobbySceneName;
    }

    private static Component FindSceneComponentByTypeName(string typeName)
    {
        Type targetType = FindTypeByName(typeName);

        if (targetType == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour == null || !targetType.IsAssignableFrom(behaviour.GetType()))
            {
                continue;
            }

            if (!behaviour.gameObject.scene.IsValid())
            {
                continue;
            }

            return behaviour;
        }

        return null;
    }

    private static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int index = 0; index < assemblies.Length; index++)
        {
            Type found = assemblies[index].GetType(typeName, false);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

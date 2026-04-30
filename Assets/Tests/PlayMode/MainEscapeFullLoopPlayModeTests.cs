using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class MainEscapeFullLoopPlayModeTests
{
    private const string LobbySceneName = "RMainEscape_Lobby";
    private const string ElevatorTransitionSceneName = "RMainEscape_ElevatorTransition";
    private const float SceneLoadTimeoutSeconds = 8f;
    private const float RuntimeBindTimeoutSeconds = 6f;
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator Lobby_RunProgressesFrom5FTo1F_ThenReturnsToLobby()
    {
        yield return LoadSceneOrFail(LobbySceneName, SceneLoadTimeoutSeconds);

        Component sessionController = null;
        yield return WaitForComponentOrFail(
            "RRunSessionController",
            RuntimeBindTimeoutSeconds,
            includeInactive: true,
            resolved => sessionController = resolved);

        Invoke(sessionController, "StartNewRunAndLoadGameplay");

        for (int floorNumber = 5; floorNumber >= 2; floorNumber--)
        {
            yield return WaitForFloorSceneOrFail(floorNumber, SceneLoadTimeoutSeconds);

            Component runController = null;
            Component inventory = null;
            yield return WaitForGameplayRuntimeOrFail(
                RuntimeBindTimeoutSeconds,
                resolvedRunController => runController = resolvedRunController,
                resolvedInventory => inventory = resolvedInventory);

            Component floorDirector = FindComponentByTypeName("RFloorDirector", includeInactive: true);
            Assert.That(floorDirector, Is.Not.Null, $"{floorNumber}F floor director was not bound.");
            Assert.That(ReadProperty(floorDirector, "CurrentMapService"), Is.Not.Null, $"{floorNumber}F authored floor build did not produce a map service.");
            Assert.That(ReadProperty(floorDirector, "CurrentLayout"), Is.Not.Null, $"{floorNumber}F authored floor build did not produce a layout.");

            Assert.That(ReadIntProperty(runController, "CurrentFloorNumber"), Is.EqualTo(floorNumber), $"{floorNumber}F run controller did not bind the expected floor.");
            Assert.That(
                InvokeBool(inventory, "AddItem", ReadStaticStringField("PrototypeItemCatalog", "IronGateKeyItemId"), "Iron Gate Key", 1),
                Is.True,
                $"{floorNumber}F could not inject the authored Iron Gate Key for loop verification.");

            bool usesDirectAuthoredExit = ReadBoolProperty(runController, "UsesDirectAuthoredExitInteraction");

            if (usesDirectAuthoredExit && ReadBoolProperty(floorDirector, "HasMainGate"))
            {
                AssertDirectExitMainGateStartsOpen(floorDirector, floorNumber);
            }

            if (!usesDirectAuthoredExit)
            {
                Assert.That(ReadBoolProperty(floorDirector, "HasMainGate"), Is.True, $"{floorNumber}F key-gate floor is missing an authored main gate contract.");
                Assert.That(InvokeBool(runController, "TryUnlockKeyGate"), Is.True, $"{floorNumber}F key gate did not unlock.");
                yield return null;
            }

            yield return null;
            Assert.That(InvokeBool(runController, "TryUseEmergencyStairs"), Is.True, $"{floorNumber}F emergency stairs did not transition to the next floor.");
            yield return WaitForSceneOrFail(ElevatorTransitionSceneName, SceneLoadTimeoutSeconds);
        }

        yield return WaitForFloorSceneOrFail(1, SceneLoadTimeoutSeconds);

        Component finalRunController = null;
        Component finalInventory = null;
        yield return WaitForGameplayRuntimeOrFail(
            RuntimeBindTimeoutSeconds,
            resolvedRunController => finalRunController = resolvedRunController,
            resolvedInventory => finalInventory = resolvedInventory);

        Assert.That(ReadIntProperty(finalRunController, "CurrentFloorNumber"), Is.EqualTo(1), "Final floor run controller did not bind 1F.");
        Assert.That(
            InvokeBool(finalInventory, "AddItem", ReadStaticStringField("PrototypeItemCatalog", "IronGateKeyItemId"), "Iron Gate Key", 1),
            Is.True,
            "1F could not inject the authored Iron Gate Key for final exit verification.");
        Assert.That(InvokeBool(finalRunController, "TryUseFinalExit"), Is.True, "1F final exit did not complete the run.");
        yield return null;

        object snapshot = ReadProperty(sessionController, "Snapshot");
        Assert.That(ReadBoolProperty(snapshot, "HasCompletedRun"), Is.True, "Run session did not mark the full loop as completed.");
        Assert.That(ReadBoolProperty(snapshot, "WasSuccessful"), Is.True, "Run session did not mark the full loop as successful.");

        Invoke(sessionController, "ReturnToLobby");
        yield return WaitForSceneOrFail(LobbySceneName, SceneLoadTimeoutSeconds);
    }

    private static IEnumerator LoadSceneOrFail(string sceneName, float timeoutSeconds)
    {
        Assert.That(string.IsNullOrWhiteSpace(sceneName), Is.False, "Resolved scene name was empty.");
        Assert.That(Application.CanStreamedLevelBeLoaded(sceneName), Is.True, $"Scene '{sceneName}' is not loadable from build settings.");

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        Assert.That(loadOperation, Is.Not.Null, $"Failed to start loading scene '{sceneName}'.");

        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;

        while (!loadOperation.isDone && Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        Assert.That(loadOperation.isDone, Is.True, $"Timed out loading scene '{sceneName}'.");
        yield return null;
    }

    private static IEnumerator WaitForSceneOrFail(string sceneName, float timeoutSeconds)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutAt)
        {
            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() && string.Equals(activeScene.name, sceneName, StringComparison.Ordinal))
            {
                yield return null;
                yield break;
            }

            yield return null;
        }

        Assert.Fail($"Timed out waiting for scene '{sceneName}'.");
    }

    private static IEnumerator WaitForFloorSceneOrFail(int floorNumber, float timeoutSeconds)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutAt)
        {
            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.IsValid() && IsCanonicalFloorScene(activeScene, floorNumber))
            {
                yield return null;
                yield break;
            }

            yield return null;
        }

        Assert.Fail($"Timed out waiting for floor '{floorNumber}F'.");
    }

    private static IEnumerator WaitForGameplayRuntimeOrFail(
        float timeoutSeconds,
        Action<Component> onRunControllerResolved,
        Action<Component> onInventoryResolved)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutAt)
        {
            Component runController = FindComponentByTypeName("RRunController", includeInactive: true);
            Component inventory = FindComponentByTypeName("PlayerInventory", includeInactive: true);

            if (runController != null && inventory != null)
            {
                onRunControllerResolved?.Invoke(runController);
                onInventoryResolved?.Invoke(inventory);
                yield return null;
                yield break;
            }

            yield return null;
        }

        Assert.Fail("Timed out waiting for gameplay runtime bindings.");
    }

    private static IEnumerator WaitForComponentOrFail(
        string typeName,
        float timeoutSeconds,
        bool includeInactive,
        Action<Component> onResolved)
    {
        float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;

        while (Time.realtimeSinceStartup < timeoutAt)
        {
            Component component = FindComponentByTypeName(typeName, includeInactive);

            if (component != null)
            {
                onResolved?.Invoke(component);
                yield return null;
                yield break;
            }

            yield return null;
        }

        Assert.Fail($"Timed out waiting for component '{typeName}'.");
    }

    private static bool IsCanonicalFloorScene(Scene scene, int floorNumber)
    {
        string expectedSceneName = $"RMainScene_{Mathf.Max(1, floorNumber)}F";
        string expectedScenePath = $"Assets/Scenes/{expectedSceneName}.unity";
        return string.Equals(scene.name, expectedSceneName, StringComparison.Ordinal)
            || string.Equals(scene.path, expectedScenePath, StringComparison.Ordinal);
    }

    private static Component FindComponentByTypeName(string typeName, bool includeInactive)
    {
        Type targetType = FindTypeByName(typeName);

        if (targetType == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour != null && targetType.IsAssignableFrom(behaviour.GetType()))
            {
                return behaviour;
            }
        }

        return null;
    }

    private static bool InvokeBool(object instance, string methodName, params object[] arguments)
    {
        return Invoke(instance, methodName, arguments) is bool value && value;
    }

    private static object Invoke(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, MemberFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName}() is missing.");
        return method.Invoke(instance, arguments);
    }

    private static string ReadStaticStringField(string typeName, string fieldName)
    {
        Type resolvedType = FindTypeByName(typeName);
        Assert.That(resolvedType, Is.Not.Null, $"{typeName} type is missing.");

        FieldInfo field = resolvedType.GetField(fieldName, StaticMemberFlags);
        Assert.That(field, Is.Not.Null, $"{typeName}.{fieldName} is missing.");
        return field.GetValue(null) as string;
    }

    private static object ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, MemberFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance);
    }

    private static int ReadIntProperty(object instance, string propertyName)
    {
        object value = ReadProperty(instance, propertyName);
        return value != null ? Convert.ToInt32(value) : 0;
    }

    private static bool ReadBoolProperty(object instance, string propertyName)
    {
        return ReadProperty(instance, propertyName) is bool value && value;
    }

    private static void AssertDirectExitMainGateStartsOpen(object floorDirector, int floorNumber)
    {
        object layout = ReadProperty(floorDirector, "CurrentLayout");
        object mapService = ReadProperty(floorDirector, "CurrentMapService");
        Array mainDoorCells = ReadProperty(layout, "MainDoorCells") as Array;

        Assert.That(layout, Is.Not.Null, $"{floorNumber}F direct-exit floor is missing its layout when validating main gate state.");
        Assert.That(mapService, Is.Not.Null, $"{floorNumber}F direct-exit floor is missing its map service when validating main gate state.");
        Assert.That(mainDoorCells, Is.Not.Null, $"{floorNumber}F direct-exit floor did not expose main gate cells.");
        Assert.That(mainDoorCells.Length, Is.GreaterThan(0), $"{floorNumber}F direct-exit floor reported a main gate but exposed no main gate cells.");

        for (int index = 0; index < mainDoorCells.Length; index++)
        {
            object mainDoorCell = mainDoorCells.GetValue(index);
            Assert.That(
                InvokeBool(mapService, "IsDoorOpen", mainDoorCell),
                Is.True,
                $"{floorNumber}F direct-exit floor left its main gate closed at cell {mainDoorCell}.");
        }
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

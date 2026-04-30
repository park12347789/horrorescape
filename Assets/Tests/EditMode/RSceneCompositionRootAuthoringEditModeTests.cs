using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RSceneCompositionRootAuthoringEditModeTests
{
    private const string StartFloorScenePath = "Assets/Scenes/RMainScene_5F.unity";
    private static readonly string[] FloorScenePaths =
    {
        "Assets/Scenes/RMainScene_5F.unity",
        "Assets/Scenes/RMainScene_4F.unity",
        "Assets/Scenes/RMainScene_3F.unity",
        "Assets/Scenes/RMainScene_2F.unity",
        "Assets/Scenes/RMainScene_1F.unity"
    };
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void StartFloorScene_CompositionRoot_HasRequiredAuthoredReferences()
    {
        EditorSceneManager.OpenScene(StartFloorScenePath, OpenSceneMode.Single);

        Type compositionRootType = FindTypeByName("RSceneCompositionRoot");
        Assert.That(compositionRootType, Is.Not.Null, $"{StartFloorScenePath} is missing RSceneCompositionRoot type.");

        UnityEngine.Object[] compositionRoots = Resources.FindObjectsOfTypeAll(compositionRootType);
        Assert.That(compositionRoots, Is.Not.Null.And.Not.Empty, $"{StartFloorScenePath} is missing RSceneCompositionRoot.");

        UnityEngine.Object compositionRoot = compositionRoots[0];
        MethodInfo cacheMethod = compositionRootType.GetMethod("CacheSceneReferences", InstanceFlags);
        Assert.That(cacheMethod, Is.Not.Null, $"{compositionRootType.Name}.CacheSceneReferences is missing.");
        cacheMethod.Invoke(compositionRoot, null);

        List<string> requiredFieldNames = BuildRequiredFieldNames(compositionRootType, compositionRoot);

        for (int index = 0; index < requiredFieldNames.Count; index++)
        {
            string fieldName = requiredFieldNames[index];
            FieldInfo field = compositionRootType.GetField(fieldName, InstanceFlags);

            Assert.That(field, Is.Not.Null, $"{compositionRootType.Name} is missing serialized field '{fieldName}'.");
            Assert.That(
                field.GetValue(compositionRoot) as UnityEngine.Object,
                Is.Not.Null,
                $"{compositionRootType.Name} is missing authored reference '{fieldName}' in {StartFloorScenePath}.");
        }

        Type catalogType = FindTypeByName("MainEscapeRuntimePrefabCatalog");
        Assert.That(catalogType, Is.Not.Null, "MainEscapeRuntimePrefabCatalog type is missing.");

        MethodInfo loadMethod = catalogType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static);
        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimePrefabCatalog.Load is missing.");

        UnityEngine.Object catalog = loadMethod.Invoke(null, null) as UnityEngine.Object;
        Assert.That(catalog, Is.Not.Null, "MainEscapeRuntimePrefabCatalog asset is missing from Resources.");

        PropertyInfo playerPrefabProperty = catalogType.GetProperty("PlayerPrefab", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(playerPrefabProperty, Is.Not.Null, "MainEscapeRuntimePrefabCatalog.PlayerPrefab property is missing.");
        Assert.That(
            playerPrefabProperty.GetValue(catalog) as UnityEngine.Object,
            Is.Not.Null,
            "MainEscapeRuntimePrefabCatalog is missing the authored player prefab reference.");
    }

    [Test]
    public void FloorScenes_CompositionRoot_AssignsPlayerStateStoreSource()
    {
        Type compositionRootType = FindTypeByName("RSceneCompositionRoot");
        Assert.That(compositionRootType, Is.Not.Null, "RSceneCompositionRoot type is missing.");

        for (int index = 0; index < FloorScenePaths.Length; index++)
        {
            string scenePath = FloorScenePaths[index];
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            UnityEngine.Object[] compositionRoots = Resources.FindObjectsOfTypeAll(compositionRootType);
            Assert.That(compositionRoots, Is.Not.Null.And.Not.Empty, $"{scenePath} is missing RSceneCompositionRoot.");

            UnityEngine.Object compositionRoot = compositionRoots[0];
            MethodInfo cacheMethod = compositionRootType.GetMethod("CacheSceneReferences", InstanceFlags);
            Assert.That(cacheMethod, Is.Not.Null, $"{compositionRootType.Name}.CacheSceneReferences is missing.");
            cacheMethod.Invoke(compositionRoot, null);

            FieldInfo playerStateStoreField = compositionRootType.GetField("playerStateStoreSource", InstanceFlags);
            Assert.That(playerStateStoreField, Is.Not.Null, $"{compositionRootType.Name} is missing serialized field 'playerStateStoreSource'.");
            Assert.That(
                playerStateStoreField.GetValue(compositionRoot) as UnityEngine.Object,
                Is.Not.Null,
                $"{compositionRootType.Name} is missing authored reference 'playerStateStoreSource' in {scenePath}.");
        }
    }

    [Test]
    public void FloorScenes_CompositionRoot_UsesSceneFloorWhenSessionControllerIsUnbound()
    {
        Type compositionRootType = FindTypeByName("RSceneCompositionRoot");
        Assert.That(compositionRootType, Is.Not.Null, "RSceneCompositionRoot type is missing.");

        MethodInfo cacheMethod = compositionRootType.GetMethod("CacheSceneReferences", InstanceFlags);
        Assert.That(cacheMethod, Is.Not.Null, $"{compositionRootType.Name}.CacheSceneReferences is missing.");

        MethodInfo resolveBoundFloorMethod = compositionRootType.GetMethod("ResolveBoundFloorNumber", InstanceFlags);
        Assert.That(resolveBoundFloorMethod, Is.Not.Null, $"{compositionRootType.Name}.ResolveBoundFloorNumber is missing.");

        for (int index = 0; index < FloorScenePaths.Length; index++)
        {
            string scenePath = FloorScenePaths[index];
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            UnityEngine.Object[] compositionRoots = Resources.FindObjectsOfTypeAll(compositionRootType);
            Assert.That(compositionRoots, Is.Not.Null.And.Not.Empty, $"{scenePath} is missing RSceneCompositionRoot.");

            UnityEngine.Object compositionRoot = compositionRoots[0];
            cacheMethod.Invoke(compositionRoot, null);

            object runController = GetFieldValue(compositionRootType, compositionRoot, "runController");
            Assert.That(runController, Is.Not.Null, $"{scenePath} is missing a bound runController reference.");

            int expectedFloorNumber = GetPropertyValue(runController, "CurrentFloorNumber", 0);
            Assert.That(expectedFloorNumber, Is.GreaterThan(0), $"{scenePath} did not resolve a valid scene floor number.");

            int resolvedFloorNumber = resolveBoundFloorMethod.Invoke(compositionRoot, null) is int value ? value : 0;
            Assert.That(
                resolvedFloorNumber,
                Is.EqualTo(expectedFloorNumber),
                $"{compositionRootType.Name} should prefer the authored scene floor number in {scenePath} when no session controller is bound.");
        }
    }

    [Test]
    public void FloorScenes_CompositionRoot_LeavesLegacyEmergencyStairsProxyUnassigned()
    {
        Type compositionRootType = FindTypeByName("RSceneCompositionRoot");
        Assert.That(compositionRootType, Is.Not.Null, "RSceneCompositionRoot type is missing.");

        FieldInfo emergencyStairsField = compositionRootType.GetField("emergencyStairsPoint", InstanceFlags);
        Assert.That(emergencyStairsField, Is.Not.Null, $"{compositionRootType.Name} is missing serialized field 'emergencyStairsPoint'.");

        for (int index = 0; index < FloorScenePaths.Length; index++)
        {
            string scenePath = FloorScenePaths[index];
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            UnityEngine.Object[] compositionRoots = Resources.FindObjectsOfTypeAll(compositionRootType);
            Assert.That(compositionRoots, Is.Not.Null.And.Not.Empty, $"{scenePath} is missing RSceneCompositionRoot.");
            Assert.That(
                emergencyStairsField.GetValue(compositionRoots[0]) as UnityEngine.Object,
                Is.Null,
                $"{compositionRootType.Name} should leave legacy emergency stairs proxy unassigned in {scenePath}.");
        }
    }

    [Test]
    public void RunSceneBindings_TreatsLegacyStairProxyAsOptional()
    {
        Type legacyStairProxyType = FindTypeByName("FloorEscapeTransitionPoint");
        Type authoredStairsPointType = FindTypeByName("MainEscapeEmergencyStairsPoint");
        Type bindingsType = FindTypeByName("RRunSceneBindings");
        Assert.That(legacyStairProxyType, Is.Not.Null, "FloorEscapeTransitionPoint type is missing.");
        Assert.That(authoredStairsPointType, Is.Not.Null, "MainEscapeEmergencyStairsPoint type is missing.");
        Assert.That(bindingsType, Is.Not.Null, "RRunSceneBindings type is missing.");

        MethodInfo createMethod = bindingsType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.That(createMethod, Is.Not.Null, "RRunSceneBindings.Create is missing.");

        GameObject legacyStairProxyRoot = new("LegacyStairProxy");
        GameObject authoredStairsRoot = new("AuthoredStairs");

        try
        {
            Component legacyStairProxy = legacyStairProxyRoot.AddComponent(legacyStairProxyType);
            Component authoredStairsPoint = authoredStairsRoot.AddComponent(authoredStairsPointType);

            object legacyOnlyBindings = createMethod.Invoke(null, new object[]
            {
                null,
                null,
                null,
                null,
                legacyStairProxy,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            });
            Assert.That(
                GetPropertyValue(legacyOnlyBindings, "HasLegacyTransitionProxy", false),
                Is.True,
                "RRunSceneBindings should still expose the legacy stair proxy separately for compatibility paths.");
            Assert.That(
                GetPropertyValue<object>(legacyOnlyBindings, "LegacyStairProxy", null),
                Is.Not.Null,
                "RRunSceneBindings should expose the legacy stair proxy through the explicit compatibility alias.");
            Assert.That(
                GetPropertyValue(legacyOnlyBindings, "HasAnyProgressionAnchor", false),
                Is.False,
                "RRunSceneBindings should not treat the legacy stair proxy as a required authored progression anchor.");

            object authoredBindings = createMethod.Invoke(null, new object[]
            {
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                authoredStairsPoint,
                null,
                null,
                null,
                null
            });
            Assert.That(
                GetPropertyValue(authoredBindings, "HasLegacyTransitionProxy", false),
                Is.False,
                "RRunSceneBindings should report no legacy stair proxy when only the authored stairs anchor is assigned.");
            Assert.That(
                GetPropertyValue<object>(authoredBindings, "LegacyStairProxy", null),
                Is.Null,
                "RRunSceneBindings should keep the explicit legacy stair proxy alias empty when only the authored stairs anchor is assigned.");
            Assert.That(
                GetPropertyValue(authoredBindings, "HasAnyProgressionAnchor", false),
                Is.True,
                "RRunSceneBindings should accept authored stairs anchors even when the legacy stair proxy is omitted.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(authoredStairsRoot);
            UnityEngine.Object.DestroyImmediate(legacyStairProxyRoot);
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

    private static List<string> BuildRequiredFieldNames(Type compositionRootType, UnityEngine.Object compositionRoot)
    {
        List<string> requiredFieldNames = new()
        {
            "systemsRoot",
            "gameplayRoot",
            "runtimeRoot",
            "authoringWorkspaceRoot",
            "floorAuthoring",
            "floorDirector",
            "runController",
            "playerStateStoreSource",
            "noiseSystem",
            "audioManager",
            "fogOfWarOverlay",
            "hudCanvas",
            "bottlePickup",
            "keyPickup"
        };

        object floorAuthoring = GetFieldValue(compositionRootType, compositionRoot, "floorAuthoring");

        if (InvokeBooleanMethod(floorAuthoring, "HasSupportItemPlacementMarkers"))
        {
            requiredFieldNames.Remove("bottlePickup");
        }

        if (InvokeBooleanMethod(floorAuthoring, "HasKeyPlacementMarkers"))
        {
            requiredFieldNames.Remove("keyPickup");
        }

        object runController = GetFieldValue(compositionRootType, compositionRoot, "runController");
        int currentFloorNumber = GetPropertyValue(runController, "CurrentFloorNumber", 1);
        bool usesDirectAuthoredExit = currentFloorNumber > 1
            && GetPropertyValue(runController, "UsesDirectAuthoredExitInteraction", false);

        if (currentFloorNumber <= 1)
        {
            requiredFieldNames.Add("finalExitPoint");
        }
        else if (usesDirectAuthoredExit && UsesElevatorPropDirectExit(currentFloorNumber))
        {
            requiredFieldNames.Add("elevatorExitPoint");
        }
        else
        {
            requiredFieldNames.Add("authoredStairsPoint");

            if (!usesDirectAuthoredExit)
            {
                requiredFieldNames.Add("keyGatePoint");
            }
        }

        return requiredFieldNames;
    }

    private static bool InvokeBooleanMethod(object owner, string methodName)
    {
        if (owner == null)
        {
            return false;
        }

        MethodInfo method = owner.GetType().GetMethod(methodName, InstanceFlags);

        if (method == null)
        {
            return false;
        }

        return method.Invoke(owner, null) is bool result && result;
    }

    private static object GetFieldValue(Type ownerType, object owner, string fieldName)
    {
        FieldInfo field = ownerType.GetField(fieldName, InstanceFlags);
        return field != null ? field.GetValue(owner) : null;
    }

    private static T GetPropertyValue<T>(object owner, string propertyName, T fallback)
    {
        if (owner == null)
        {
            return fallback;
        }

        PropertyInfo property = owner.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        if (property == null)
        {
            return fallback;
        }

        object value = property.GetValue(owner);
        return value is T typedValue ? typedValue : fallback;
    }

    private static bool UsesElevatorPropDirectExit(int floorNumber)
    {
        return Mathf.Max(1, floorNumber) >= 2;
    }
}

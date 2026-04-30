using System;
using System.Linq;
using System.Reflection;

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public sealed class RElevatorTransitionRoutingEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const string LobbyScenePath = "Assets/Scenes/RMainEscape_Lobby.unity";
    private const string TutorialScenePath = "Assets/Scenes/RMainEscape_tuto.unity";
    private const string TransitionScenePath = "Assets/Scenes/RMainEscape_ElevatorTransition.unity";
    private static readonly string[] CanonicalFloorScenePaths =
    {
        "Assets/Scenes/RMainScene_5F.unity",
        "Assets/Scenes/RMainScene_4F.unity",
        "Assets/Scenes/RMainScene_3F.unity",
        "Assets/Scenes/RMainScene_2F.unity",
        "Assets/Scenes/RMainScene_1F.unity"
    };
    private static readonly string[] CanonicalBuildScenePaths =
    {
        LobbyScenePath,
        TutorialScenePath,
        "Assets/Scenes/RMainScene_5F.unity",
        "Assets/Scenes/RMainScene_4F.unity",
        "Assets/Scenes/RMainScene_3F.unity",
        "Assets/Scenes/RMainScene_2F.unity",
        "Assets/Scenes/RMainScene_1F.unity",
        TransitionScenePath
    };

    [Test]
    public void RunSessionController_DefaultSerializedConfiguration_ExposesElevatorTransitionScene()
    {
        Type controllerType = FindTypeByName("RRunSessionController");
        Assert.That(controllerType, Is.Not.Null, "RRunSessionController type is missing.");

        GameObject controllerObject = new("RRunSessionController");
        try
        {
            object controller = controllerObject.AddComponent(controllerType);
            Assert.That(ReadProperty(controller, "ElevatorTransitionScenePath"), Is.EqualTo(TransitionScenePath));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void ElevatorTransitionRequestStore_ConsumesRequestOnce()
    {
        Type requestType = FindTypeByName("RElevatorTransitionRequest");
        Type storeType = FindTypeByName("RElevatorTransitionRequestStore");
        Assert.That(requestType, Is.Not.Null, "RElevatorTransitionRequest type is missing.");
        Assert.That(storeType, Is.Not.Null, "RElevatorTransitionRequestStore type is missing.");

        object request = Activator.CreateInstance(
            requestType,
            "Assets/Scenes/RMainScene_4F.unity",
            5,
            4,
            "UnitTest");

        storeType.GetMethod("Set", StaticFlags)?.Invoke(null, new[] { request });

        object[] firstConsumeArguments = { null };
        bool firstConsumed = storeType.GetMethod("TryConsume", StaticFlags)?.Invoke(null, firstConsumeArguments) is bool firstValue && firstValue;
        Assert.That(firstConsumed, Is.True);

        object consumedRequest = firstConsumeArguments[0];
        Assert.That(ReadProperty(consumedRequest, "TargetScenePath"), Is.EqualTo("Assets/Scenes/RMainScene_4F.unity"));
        Assert.That(ReadProperty(consumedRequest, "SourceFloorNumber"), Is.EqualTo(5));
        Assert.That(ReadProperty(consumedRequest, "DestinationFloorNumber"), Is.EqualTo(4));

        object[] secondConsumeArguments = { null };
        bool secondConsumed = storeType.GetMethod("TryConsume", StaticFlags)?.Invoke(null, secondConsumeArguments) is bool secondValue && secondValue;
        Assert.That(secondConsumed, Is.False);
    }

    [Test]
    public void SceneRouter_ExposesElevatorTransitionFloorLoadMethod()
    {
        Type routerType = FindTypeByName("RSceneRouter");
        Type controllerType = FindTypeByName("RRunSessionController");
        Assert.That(routerType, Is.Not.Null, "RSceneRouter type is missing.");
        Assert.That(controllerType, Is.Not.Null, "RRunSessionController type is missing.");

        MethodInfo method = routerType.GetMethod(
            "LoadFloorSceneThroughElevatorTransition",
            StaticFlags,
            null,
            new[] { controllerType, typeof(int), typeof(int) },
            null);
        Assert.That(method, Is.Not.Null, "RSceneRouter should expose transition-aware floor loading.");
        Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
    }

    [Test]
    public void BuildSettings_ListsSupportScenesOutsideCanonicalFloorRoute()
    {
        string[] scenePaths = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray();

        Assert.That(
            scenePaths.Take(CanonicalBuildScenePaths.Length).ToArray(),
            Is.EqualTo(CanonicalBuildScenePaths),
            "Build Settings should be lobby, tutorial support, floor scenes, then elevator transition support.");
        Assert.That(
            scenePaths.Where(scenePath => CanonicalFloorScenePaths.Contains(scenePath)).ToArray(),
            Is.EqualTo(CanonicalFloorScenePaths),
            "Support scenes can be in Build Settings, but only the five floor scenes should form the canonical floor route.");
    }

    [Test]
    public void RoutingSettingsAsset_ReferencesSupportScenesOutsideFloorRouteArray()
    {
        object routingSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/MainEscape/Run/RRunRoutingSettings.asset");
        Assert.That(routingSettings, Is.Not.Null, "RRunRoutingSettings asset is missing.");

        Assert.That(ReadProperty(routingSettings, "TutorialScenePath"), Is.EqualTo(TutorialScenePath));
        Assert.That(ReadProperty(routingSettings, "ElevatorTransitionScenePath"), Is.EqualTo(TransitionScenePath));

        Array floorScenes = ReadProperty(routingSettings, "FloorScenes") as Array;
        Assert.That(floorScenes, Is.Not.Null, "RRunRoutingSettings.FloorScenes is missing.");

        string[] floorScenePaths = floorScenes
            .Cast<object>()
            .Select(route => ReadFieldOrProperty(route, "scenePath") as string)
            .ToArray();
        Assert.That(floorScenePaths, Is.EqualTo(CanonicalFloorScenePaths));
        Assert.That(floorScenePaths, Does.Not.Contain(TutorialScenePath));
        Assert.That(floorScenePaths, Does.Not.Contain(TransitionScenePath));
    }

    [Test]
    public void ElevatorTransitionSceneAsset_ExistsAsSupportScene()
    {
        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TransitionScenePath);
        Assert.That(scene, Is.Not.Null, $"Missing transition scene asset at '{TransitionScenePath}'.");
    }

    [Test]
    public void ElevatorTransitionScene_IsSupportSceneWithoutFloorRuntimeOwnership()
    {
        string previousScenePath = SceneManager.GetActiveScene().path;

        try
        {
            EditorSceneManager.OpenScene(TransitionScenePath, OpenSceneMode.Single);

            Assert.That(FindSceneComponentCount("RElevatorTransitionController"), Is.EqualTo(1));
            Assert.That(FindSceneComponentCount("MainEscapeFloorAuthoring"), Is.EqualTo(0));
            Assert.That(FindSceneComponentCount("RSceneCompositionRoot"), Is.EqualTo(0));
            Assert.That(FindSceneComponentCount("RRunController"), Is.EqualTo(0));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }
    }

    [Test]
    public void ElevatorTransitionController_UsesSessionResolverInsteadOfDirectSingleton()
    {
        Type controllerType = FindTypeByName("RElevatorTransitionController");
        Type sessionControllerType = FindTypeByName("RRunSessionController");
        Assert.That(controllerType, Is.Not.Null, "RElevatorTransitionController type is missing.");
        Assert.That(sessionControllerType, Is.Not.Null, "RRunSessionController type is missing.");

        MethodInfo fallbackResolver = controllerType.GetMethod(
            "ResolveFallbackTargetScenePath",
            InstanceFlags,
            null,
            new[] { sessionControllerType },
            null);
        FieldInfo directPlayFallback = controllerType.GetField("directPlayFallbackScenePath", InstanceFlags);
        Assert.That(fallbackResolver, Is.Not.Null);
        Assert.That(directPlayFallback, Is.Not.Null);

        FormerlySerializedAsAttribute formerlySerializedAs =
            directPlayFallback.GetCustomAttribute<FormerlySerializedAsAttribute>();
        TooltipAttribute tooltip = directPlayFallback.GetCustomAttribute<TooltipAttribute>();

        Assert.That(formerlySerializedAs, Is.Not.Null);
        Assert.That(formerlySerializedAs.oldName, Is.EqualTo("fallbackScenePath"));
        Assert.That(tooltip, Is.Not.Null);
        Assert.That(tooltip.tooltip, Does.Contain("no pending elevator request"));
        Assert.That(tooltip.tooltip, Does.Contain("no resolvable run session"));

        string source = System.IO.File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RElevatorTransitionController.cs");

        Assert.That(source, Does.Contain("RRunSessionResolver.ResolveForContext(this)"));
        Assert.That(source, Does.Not.Contain("PrototypeAudioManager.TryPlayElevator"));
        Assert.That(source, Does.Not.Contain("PrototypeAudioManager.TryStopElevatorRideNoise"));
        Assert.That(source, Does.Not.Contain("fallbackScenePath = \"Assets/Scenes/RMainScene_5F.unity\""));
        Assert.That(source, Does.Not.Contain("RRunSessionController.Instance"));
    }

    [Test]
    public void LobbyScene_RunSessionController_ResolvesElevatorTransitionScene()
    {
        string previousScenePath = SceneManager.GetActiveScene().path;

        try
        {
            EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            MonoBehaviour sessionController = FindSceneComponent("RRunSessionController");

            Assert.That(sessionController, Is.Not.Null, "Lobby scene is missing RRunSessionController.");
            Assert.That(ReadProperty(sessionController, "ElevatorTransitionScenePath"), Is.EqualTo(TransitionScenePath));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }
    }

    [Test]
    public void TransitionAndLobbySerializedRouteDebt_IsReadableThroughCurrentFields()
    {
        WithOpenScene(TransitionScenePath, () =>
        {
            MonoBehaviour transitionController = FindSceneComponent("RElevatorTransitionController");
            Assert.That(transitionController, Is.Not.Null, "Transition scene is missing RElevatorTransitionController.");

            SerializedObject serializedTransition = new(transitionController);
            serializedTransition.UpdateIfRequiredOrScript();
            SerializedProperty directPlayFallback = serializedTransition.FindProperty("directPlayFallbackScenePath");

            Assert.That(directPlayFallback, Is.Not.Null, "directPlayFallbackScenePath should be readable through the current field name.");
            Assert.That(directPlayFallback.propertyType, Is.EqualTo(SerializedPropertyType.String));
            Assert.That(directPlayFallback.stringValue, Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        });

        WithOpenScene(LobbyScenePath, () =>
        {
            MonoBehaviour sessionController = FindSceneComponent("RRunSessionController");
            Assert.That(sessionController, Is.Not.Null, "Lobby scene is missing RRunSessionController.");

            SerializedObject serializedSession = new(sessionController);
            serializedSession.UpdateIfRequiredOrScript();
            SerializedProperty useSceneLocalRoutingOverrides = serializedSession.FindProperty("useSceneLocalRoutingOverrides");
            SerializedProperty routingSettings = serializedSession.FindProperty("routingSettings");
            SerializedProperty floorScenes = serializedSession.FindProperty("floorScenes");

            Assert.That(useSceneLocalRoutingOverrides, Is.Not.Null);
            Assert.That(useSceneLocalRoutingOverrides.boolValue, Is.True);
            Assert.That(routingSettings, Is.Not.Null);
            Assert.That(routingSettings.objectReferenceValue, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(routingSettings.objectReferenceValue),
                Is.EqualTo("Assets/Resources/MainEscape/Run/RRunRoutingSettings.asset"));
            Assert.That(floorScenes, Is.Not.Null);
            Assert.That(floorScenes.isArray, Is.True);
            Assert.That(floorScenes.arraySize, Is.EqualTo(CanonicalFloorScenePaths.Length));

            for (int index = 0; index < floorScenes.arraySize; index++)
            {
                SerializedProperty floorScene = floorScenes.GetArrayElementAtIndex(index);
                Assert.That(floorScene.FindPropertyRelative("floorNumber").intValue, Is.EqualTo(5 - index));
                Assert.That(floorScene.FindPropertyRelative("scenePath").stringValue, Is.EqualTo(CanonicalFloorScenePaths[index]));
            }
        });
    }

    private static object ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance);
    }

    private static object ReadFieldOrProperty(object instance, string memberName)
    {
        FieldInfo field = instance.GetType().GetField(memberName, InstanceFlags);

        if (field != null)
        {
            return field.GetValue(instance);
        }

        PropertyInfo property = instance.GetType().GetProperty(memberName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{memberName} member is missing.");
        return property.GetValue(instance);
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

    private static int FindSceneComponentCount(string typeName)
    {
        Type targetType = FindTypeByName(typeName);
        Assert.That(targetType, Is.Not.Null, $"{typeName} type is missing.");
        return FindSceneComponents(targetType).Length;
    }

    private static MonoBehaviour FindSceneComponent(string typeName)
    {
        Type targetType = FindTypeByName(typeName);
        Assert.That(targetType, Is.Not.Null, $"{typeName} type is missing.");
        MonoBehaviour[] matches = FindSceneComponents(targetType);
        return matches.Length > 0 ? matches[0] : null;
    }

    private static void WithOpenScene(string scenePath, Action action)
    {
        string previousScenePath = SceneManager.GetActiveScene().path;

        try
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            action();
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }
    }

    private static MonoBehaviour[] FindSceneComponents(Type targetType)
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        System.Collections.Generic.List<MonoBehaviour> matches = new();

        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour != null && targetType.IsAssignableFrom(behaviour.GetType()))
            {
                matches.Add(behaviour);
            }
        }

        return matches.ToArray();
    }
}

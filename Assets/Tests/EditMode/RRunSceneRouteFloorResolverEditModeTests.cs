using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

public sealed class RRunSceneRouteFloorResolverEditModeTests
{
    [Test]
    public void TryResolveNextFloorNumber_UsesRouteGraphEdges()
    {
        bool resolved = TryResolveNextFloorNumber(
            5,
            CreateRouteArray(
                (5, "Assets/Scenes/RMainScene_5F.unity"),
                (3, "Assets/Scenes/RMainScene_3F.unity"),
                (1, "Assets/Scenes/RMainScene_1F.unity")),
            5,
            1337,
            out int destinationFloorNumber);

        Assert.That(resolved, Is.True);
        Assert.That(destinationFloorNumber, Is.EqualTo(3));
    }

    [Test]
    public void TryResolveNextFloorNumber_ReturnsFalseWhenRouteHasNoExit()
    {
        bool resolved = TryResolveNextFloorNumber(
            5,
            CreateRouteArray(
                (5, "Assets/Scenes/RMainScene_5F.unity"),
                (3, "Assets/Scenes/RMainScene_3F.unity")),
            3,
            1337,
            out int destinationFloorNumber);

        Assert.That(resolved, Is.False);
        Assert.That(destinationFloorNumber, Is.EqualTo(0));
    }

    [Test]
    public void TryResolveFloorNumberForScenePath_UsesRouteGraphNodePaths()
    {
        bool resolved = TryResolveFloorNumberForScenePath(
            5,
            CreateRouteArray(
                (5, "Assets/Scenes/RMainScene_5F.unity"),
                (3, "Assets/Scenes/RMainScene_3F.unity")),
            "Assets/Scenes/RMainScene_3F.unity",
            out int floorNumber);

        Assert.That(resolved, Is.True);
        Assert.That(floorNumber, Is.EqualTo(3));
    }

    [Test]
    public void TryResolveFloorNumberForScenePath_AlsoAcceptsSceneName()
    {
        bool resolved = TryResolveFloorNumberForScenePath(
            5,
            CreateRouteArray(
                (5, "Assets/Scenes/RMainScene_5F.unity"),
                (3, "Assets/Scenes/RMainScene_3F.unity")),
            "RMainScene_5F",
            out int floorNumber);

        Assert.That(resolved, Is.True);
        Assert.That(floorNumber, Is.EqualTo(5));
    }

    [Test]
    public void TryResolveScenePathForFloor_UsesRouteGraphNodePaths()
    {
        bool resolved = TryResolveScenePathForFloor(
            5,
            CreateRouteArray(
                (5, "Assets/Scenes/RMainScene_5F.unity"),
                (3, "Assets/Scenes/RMainScene_3F.unity")),
            3,
            out string scenePath);

        Assert.That(resolved, Is.True);
        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/RMainScene_3F.unity"));
    }

    [Test]
    public void RuntimeSettingsFloorResidencyPolicy_AcceptsRouteDrivenNonCanonicalScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(2, (2, scene.name));
        ScriptableObject routingSettings = CreateRoutingSettings(2);

        try
        {
            object policy = CreateResidencyPolicy(settings, routingSettings);
            object floorDefinition = GetFloorDefinition(2);

            Assert.That(
                InvokeRequiresSceneResidentAuthoring(policy, floorDefinition, scene),
                Is.True,
                "A noncanonical scene should fall back to runtime route data when routing settings cannot build a route graph.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void RuntimeSettingsFloorResidencyPolicy_PrefersRoutingSettingsRouteGraph()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject runtimeSettings = CreateRuntimeSettings(3, (3, "Assets/Scenes/RuntimeSettingsOnly3F.unity"));
        ScriptableObject routingSettings = CreateRoutingSettings(2, (2, scene.name));

        try
        {
            object policy = CreateResidencyPolicy(runtimeSettings, routingSettings);
            object floorDefinition = GetFloorDefinition(2);

            Assert.That(
                InvokeRequiresSceneResidentAuthoring(policy, floorDefinition, scene),
                Is.True,
                "Sessionless residency should use the RRunRoutingSettings.DefaultChapter route graph before runtime settings fallback routes.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(runtimeSettings);
        }
    }

    [Test]
    public void RuntimeSettingsFloorResidencyPolicy_RejectsRoutingSettingsRouteMiss_BeforeRuntimeSettingsFallback()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject runtimeSettings = CreateRuntimeSettings(2, (2, scene.name));
        ScriptableObject routingSettings = CreateRoutingSettings(4, (4, "Assets/Scenes/RouteGraphOnly4F.unity"));

        try
        {
            object policy = CreateResidencyPolicy(runtimeSettings, routingSettings);
            object floorDefinition = GetFloorDefinition(2);

            Assert.That(
                InvokeRequiresSceneResidentAuthoring(policy, floorDefinition, scene),
                Is.False,
                "A configured route graph miss should fail closed instead of being reauthorized by runtime settings fallback routes.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(runtimeSettings);
        }
    }

    [Test]
    public void RuntimeSettingsFloorResidencyPolicy_RejectsUnmappedNonCanonicalScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(3, (3, "Assets/Scenes/SomeOtherRouteDriven3F.unity"));
        ScriptableObject routingSettings = CreateRoutingSettings(3);

        try
        {
            object policy = CreateResidencyPolicy(settings, routingSettings);
            object floorDefinition = GetFloorDefinition(2);

            Assert.That(
                InvokeRequiresSceneResidentAuthoring(policy, floorDefinition, scene),
                Is.False,
                "A noncanonical scene should not be accepted when route data does not map it to the requested floor.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void RuntimeSettingsFloorResidencyPolicy_RejectsCanonicalSceneWhenRuntimeSettingsRouteMisses()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/RMainScene_5F.unity", OpenSceneMode.Single);
        ScriptableObject settings = CreateRuntimeSettings(4, (4, "Assets/Scenes/RMainScene_4F.unity"));
        ScriptableObject routingSettings = CreateRoutingSettings(4);

        try
        {
            object policy = CreateResidencyPolicy(settings, routingSettings);
            object floorDefinition = GetFloorDefinition(5);

            Assert.That(
                InvokeRequiresSceneResidentAuthoring(policy, floorDefinition, scene),
                Is.False,
                "Runtime settings route data should prevent canonical scene names from reauthorizing a route miss.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void MainEscapeSceneRouteResolver_UsesRuntimeSettingsFallbackRoutesBeforeCanonicalNames()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(2, (2, scene.name));

        try
        {
            bool resolved = InvokeTryResolveFloorNumber(settings, CreateRouteArray(), scene, out int floorNumber);

            Assert.That(resolved, Is.True);
            Assert.That(floorNumber, Is.EqualTo(2));
        }
        finally
        {
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void MainEscapeSceneRouteResolver_RejectsSceneWhenExplicitRouteMisses()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(2, (2, "Assets/Scenes/SomeOtherRouteDriven2F.unity"));

        try
        {
            bool resolved = InvokeTryResolveFloorNumber(settings, CreateRouteArray(), scene, out int floorNumber);

            Assert.That(resolved, Is.False);
            Assert.That(floorNumber, Is.EqualTo(0));
        }
        finally
        {
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void MainEscapeSceneRouteResolver_RejectsCanonicalSceneWhenConfiguredRouteMisses()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/RMainScene_5F.unity", OpenSceneMode.Single);
        ScriptableObject settings = CreateRuntimeSettings(5, (5, "Assets/Scenes/RMainScene_5F.unity"));

        try
        {
            Array configuredRoutes = CreateRouteArray((4, "Assets/Scenes/RMainScene_4F.unity"));
            bool resolved = InvokeTryResolveFloorNumber(settings, configuredRoutes, scene, out int floorNumber);

            Assert.That(resolved, Is.False);
            Assert.That(floorNumber, Is.EqualTo(0));
        }
        finally
        {
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void MainEscapeSceneRouteResolver_ConfiguredRoutesOverrideSettingsInFloorResolution()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(4, (4, "Assets/Scenes/SomeOtherRouteDriven4F.unity"));

        try
        {
            bool resolved = InvokeTryResolveFloorNumber(settings, CreateRouteArray((2, scene.name)), scene, out int floorNumber);

            Assert.That(resolved, Is.True);
            Assert.That(floorNumber, Is.EqualTo(2));
        }
        finally
        {
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void MainEscapeSceneRouteResolver_SceneMatchesFloor_UsesRuntimeSettingsFallbackRoutes()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(4, (4, scene.name));

        try
        {
            Assert.That(InvokeSceneMatchesFloor(settings, scene, 4), Is.True);
            Assert.That(InvokeSceneMatchesFloor(settings, scene, 3), Is.False);
        }
        finally
        {
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void MainEscapeSceneRouteResolver_SceneMatchesFloor_ConfiguredRoutesOverrideSettingsFallback()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(4, (4, scene.name));

        try
        {
            Array configuredRoutes = CreateRouteArray((2, scene.name));

            Assert.That(InvokeSceneMatchesFloor(settings, configuredRoutes, scene, 2), Is.True);
            Assert.That(InvokeSceneMatchesFloor(settings, configuredRoutes, scene, 4), Is.False);
        }
        finally
        {
            UnityObject.DestroyImmediate(settings);
        }
    }

    private static bool TryResolveNextFloorNumber(
        int startingFloorNumber,
        Array floorScenes,
        int currentFloorNumber,
        int runSeed,
        out int destinationFloorNumber)
    {
        Type resolverType = MainEscapeReflectionTestHelper.RequireType("RRunSceneRouteFloorResolver");
        MethodInfo method = FindDefaultResolveMethod(resolverType, floorScenes.GetType());
        object[] arguments =
        {
            startingFloorNumber,
            floorScenes,
            currentFloorNumber,
            runSeed,
            null
        };

        bool resolved = method.Invoke(null, arguments) is bool value && value;
        destinationFloorNumber = arguments[4] is int floorNumber ? floorNumber : 0;
        return resolved;
    }

    private static MethodInfo FindDefaultResolveMethod(Type resolverType, Type routeArrayType)
    {
        MethodInfo[] methods = resolverType.GetMethods(MainEscapeReflectionTestHelper.StaticMemberFlags);

        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo method = methods[index];
            ParameterInfo[] parameters = method.GetParameters();

            if (method.Name == "TryResolveNextFloorNumber"
                && parameters.Length == 5
                && parameters[1].ParameterType == routeArrayType)
            {
                return method;
            }
        }

        Assert.Fail("RRunSceneRouteFloorResolver.TryResolveNextFloorNumber default overload is missing.");
        return null;
    }

    private static bool TryResolveFloorNumberForScenePath(
        int startingFloorNumber,
        Array floorScenes,
        string scenePathOrName,
        out int floorNumber)
    {
        Type resolverType = MainEscapeReflectionTestHelper.RequireType("RRunSceneRouteFloorResolver");
        MethodInfo method = resolverType.GetMethod(
            "TryResolveFloorNumberForScenePath",
            MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "RRunSceneRouteFloorResolver.TryResolveFloorNumberForScenePath is missing.");
        object[] arguments =
        {
            startingFloorNumber,
            floorScenes,
            scenePathOrName,
            null
        };

        bool resolved = method.Invoke(null, arguments) is bool value && value;
        floorNumber = arguments[3] is int resolvedFloorNumber ? resolvedFloorNumber : 0;
        return resolved;
    }

    private static bool TryResolveScenePathForFloor(
        int startingFloorNumber,
        Array floorScenes,
        int floorNumber,
        out string scenePath)
    {
        Type resolverType = MainEscapeReflectionTestHelper.RequireType("RRunSceneRouteFloorResolver");
        MethodInfo method = resolverType.GetMethod(
            "TryResolveScenePathForFloor",
            MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "RRunSceneRouteFloorResolver.TryResolveScenePathForFloor is missing.");
        object[] arguments =
        {
            startingFloorNumber,
            floorScenes,
            floorNumber,
            null
        };

        bool resolved = method.Invoke(null, arguments) is bool value && value;
        scenePath = arguments[3] as string ?? string.Empty;
        return resolved;
    }

    private static Array CreateRouteArray(params (int FloorNumber, string ScenePath)[] routes)
    {
        Type routeType = MainEscapeReflectionTestHelper.RequireType("RFloorSceneEntry");
        Array routeArray = Array.CreateInstance(routeType, routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            routeArray.SetValue(
                Activator.CreateInstance(routeType, routes[index].FloorNumber, routes[index].ScenePath),
                index);
        }

        return routeArray;
    }

    private static ScriptableObject CreateRuntimeSettings(int startingFloorNumber, params (int FloorNumber, string ScenePath)[] routes)
    {
        Type settingsType = MainEscapeReflectionTestHelper.RequireType("MainEscapeRuntimeSettings");
        var settings = (ScriptableObject)ScriptableObject.CreateInstance(settingsType);
        FieldInfo scenesField = settingsType.GetField("scenes", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(scenesField, Is.Not.Null, "MainEscapeRuntimeSettings.scenes is missing.");
        object sceneSettings = scenesField.GetValue(settings);
        Assert.That(sceneSettings, Is.Not.Null, "MainEscapeRuntimeSettings.scenes is not initialized.");
        SetField(sceneSettings, "authoredFloorNumber", startingFloorNumber);
        SetField(sceneSettings, "floorSceneRoutes", CreateRouteArray(routes));
        return settings;
    }

    private static ScriptableObject CreateRoutingSettings(int startingFloorNumber, params (int FloorNumber, string ScenePath)[] routes)
    {
        Type routingSettingsType = MainEscapeReflectionTestHelper.RequireType("RRunRoutingSettings");
        Type chapterType = MainEscapeReflectionTestHelper.RequireType("ChapterDefinition");
        Type routeGraphType = MainEscapeReflectionTestHelper.RequireType("RouteGraphDefinition");

        var routingSettings = (ScriptableObject)ScriptableObject.CreateInstance(routingSettingsType);
        var chapter = (ScriptableObject)ScriptableObject.CreateInstance(chapterType);
        var routeGraph = (ScriptableObject)ScriptableObject.CreateInstance(routeGraphType);

        SetField(routeGraph, "routeGraphId", "test_route_graph");
        SetField(routeGraph, "startNodeId", $"test_{startingFloorNumber}f");
        SetField(routeGraph, "sceneNodes", CreateSceneNodeArray(routes));
        SetField(routeGraph, "edges", Array.CreateInstance(MainEscapeReflectionTestHelper.RequireType("RouteEdgeDefinition"), 0));
        SetField(chapter, "chapterId", "test_chapter");
        SetField(chapter, "startRouteGraph", routeGraph);
        SetField(routingSettings, "defaultChapter", chapter);
        SetField(routingSettings, "lobbyScenePath", "Assets/Scenes/RMainEscape_Lobby.unity");
        return routingSettings;
    }

    private static Array CreateSceneNodeArray(params (int FloorNumber, string ScenePath)[] routes)
    {
        Type sceneNodeType = MainEscapeReflectionTestHelper.RequireType("SceneNodeDefinition");
        Array nodes = Array.CreateInstance(sceneNodeType, routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            object node = Activator.CreateInstance(sceneNodeType);
            string floorId = $"{routes[index].FloorNumber}f";
            SetField(node, "nodeId", $"test_{floorId}");
            SetField(node, "scenePath", routes[index].ScenePath);
            SetField(node, "chapterLocalLevelId", floorId);
            nodes.SetValue(node, index);
        }

        return nodes;
    }

    private static object CreateResidencyPolicy(ScriptableObject settings)
    {
        Type policyType = MainEscapeReflectionTestHelper.RequireType("RRuntimeSettingsFloorResidencyPolicy");
        ConstructorInfo constructor = policyType.GetConstructor(
            MainEscapeReflectionTestHelper.InstanceMemberFlags,
            null,
            new[] { settings.GetType() },
            null);
        Assert.That(constructor, Is.Not.Null, "RRuntimeSettingsFloorResidencyPolicy test settings constructor is missing.");
        return constructor.Invoke(new object[] { settings });
    }

    private static object CreateResidencyPolicy(ScriptableObject settings, ScriptableObject routingSettings)
    {
        Type policyType = MainEscapeReflectionTestHelper.RequireType("RRuntimeSettingsFloorResidencyPolicy");
        ConstructorInfo constructor = policyType.GetConstructor(
            MainEscapeReflectionTestHelper.InstanceMemberFlags,
            null,
            new[] { settings.GetType(), routingSettings.GetType() },
            null);
        Assert.That(constructor, Is.Not.Null, "RRuntimeSettingsFloorResidencyPolicy routing settings constructor is missing.");
        return constructor.Invoke(new object[] { settings, routingSettings });
    }

    private static void DestroyRoutingSettings(ScriptableObject routingSettings)
    {
        if (routingSettings == null)
        {
            return;
        }

        object chapter = MainEscapeReflectionTestHelper.GetPropertyValue(routingSettings, "DefaultChapter");
        object routeGraph = chapter != null ? MainEscapeReflectionTestHelper.GetPropertyValue(chapter, "StartRouteGraph") : null;

        UnityObject.DestroyImmediate(routingSettings);

        if (chapter is UnityObject chapterObject)
        {
            UnityObject.DestroyImmediate(chapterObject);
        }

        if (routeGraph is UnityObject routeGraphObject)
        {
            UnityObject.DestroyImmediate(routeGraphObject);
        }
    }

    private static object GetFloorDefinition(int floorNumber)
    {
        Type catalogType = MainEscapeReflectionTestHelper.RequireType("MainEscapeFloorCatalog");
        MethodInfo method = catalogType.GetMethod("TryGetFloorByNumber", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "MainEscapeFloorCatalog.TryGetFloorByNumber is missing.");
        object[] arguments = { floorNumber, null };
        bool resolved = method.Invoke(null, arguments) is bool value && value;
        Assert.That(resolved, Is.True, $"Floor {floorNumber} definition is missing.");
        return arguments[1];
    }

    private static bool InvokeRequiresSceneResidentAuthoring(object policy, object floorDefinition, Scene scene)
    {
        MethodInfo method = policy.GetType().GetMethod(
            "RequiresSceneResidentAuthoring",
            MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "RRuntimeSettingsFloorResidencyPolicy.RequiresSceneResidentAuthoring is missing.");
        return method.Invoke(policy, new[] { floorDefinition, scene }) is bool value && value;
    }

    private static bool InvokeTryResolveFloorNumber(
        ScriptableObject settings,
        Array configuredScenes,
        Scene scene,
        out int floorNumber)
    {
        Type resolverType = MainEscapeReflectionTestHelper.RequireType("MainEscapeSceneRouteResolver");
        MethodInfo method = resolverType.GetMethod("TryResolveFloorNumber", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "MainEscapeSceneRouteResolver.TryResolveFloorNumber is missing.");
        object[] arguments = { settings, configuredScenes, scene, null };
        bool resolved = method.Invoke(null, arguments) is bool value && value;
        floorNumber = arguments[3] is int resolvedFloorNumber ? resolvedFloorNumber : 0;
        return resolved;
    }

    private static bool InvokeSceneMatchesFloor(ScriptableObject settings, Scene scene, int floorNumber)
    {
        Type resolverType = MainEscapeReflectionTestHelper.RequireType("MainEscapeSceneRouteResolver");
        MethodInfo method = FindSceneMatchesFloorMethod(resolverType, parameterCount: 3);
        Assert.That(method, Is.Not.Null, "MainEscapeSceneRouteResolver.SceneMatchesFloor is missing.");
        return method.Invoke(null, new object[] { settings, scene, floorNumber }) is bool value && value;
    }

    private static bool InvokeSceneMatchesFloor(ScriptableObject settings, Array configuredRoutes, Scene scene, int floorNumber)
    {
        Type resolverType = MainEscapeReflectionTestHelper.RequireType("MainEscapeSceneRouteResolver");
        MethodInfo method = FindSceneMatchesFloorMethod(resolverType, parameterCount: 4);
        Assert.That(method, Is.Not.Null, "MainEscapeSceneRouteResolver.SceneMatchesFloor configured-route overload is missing.");
        return method.Invoke(null, new object[] { settings, configuredRoutes, scene, floorNumber }) is bool value && value;
    }

    private static MethodInfo FindSceneMatchesFloorMethod(Type resolverType, int parameterCount)
    {
        MethodInfo[] methods = resolverType.GetMethods(MainEscapeReflectionTestHelper.StaticMemberFlags);

        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo method = methods[index];

            if (method.Name == "SceneMatchesFloor" && method.GetParameters().Length == parameterCount)
            {
                return method;
            }
        }

        return null;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
    }
}

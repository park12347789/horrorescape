using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

public sealed class RSceneRouteMembershipUtilityEditModeTests
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void IsManagedGameplayOrAuthoredScene_AcceptsExplicitRouteDrivenScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        GameObject sessionObject = new("RouteMembership_Test_RunSessionController");

        try
        {
            Component sessionController = sessionObject.AddComponent(RequireType("RRunSessionController"));
            ConfigureSessionRoute(sessionController, 2, (2, scene.name));

            Assert.That(
                InvokeIsManagedGameplayOrAuthoredScene(scene, sessionController),
                Is.True,
                "A noncanonical scene should be accepted when it is explicitly present in the session route.");
        }
        finally
        {
            UnityObject.DestroyImmediate(sessionObject);
        }
    }

    [Test]
    public void IsManagedGameplayOrAuthoredScene_RejectsUnroutedNonCanonicalScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        GameObject sessionObject = new("RouteMembership_Test_RunSessionController");

        try
        {
            Component sessionController = sessionObject.AddComponent(RequireType("RRunSessionController"));
            ConfigureSessionRoute(sessionController, 2, (2, "Assets/Scenes/SomeOtherRouteDriven2F.unity"));

            Assert.That(
                InvokeIsManagedGameplayOrAuthoredScene(scene, sessionController),
                Is.False,
                "A noncanonical scene should still be rejected when neither route data nor authored-name fallback matches.");
        }
        finally
        {
            UnityObject.DestroyImmediate(sessionObject);
        }
    }

    [Test]
    public void IsManagedGameplayOrAuthoredScene_AcceptsRuntimeSettingsRouteDrivenScene_WhenNoSessionExists()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(2, (2, scene.name));
        ScriptableObject routingSettings = CreateRoutingSettings(2);

        try
        {
            Assert.That(
                InvokeIsManagedGameplayOrAuthoredScene(scene, null, settings, routingSettings),
                Is.True,
                "A direct-play scene should fall back to runtime settings route data when routing settings cannot build a route graph.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void IsManagedGameplayOrAuthoredScene_PrefersRoutingSettingsRouteGraph_WhenNoSessionExists()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject runtimeSettings = CreateRuntimeSettings(3, (3, "Assets/Scenes/RuntimeSettingsOnly3F.unity"));
        ScriptableObject routingSettings = CreateRoutingSettings(2, (2, scene.name));

        try
        {
            Assert.That(
                InvokeIsManagedGameplayOrAuthoredScene(scene, null, runtimeSettings, routingSettings),
                Is.True,
                "Sessionless route membership should use RRunRoutingSettings.DefaultChapter route graph before runtime settings fallback routes.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(runtimeSettings);
        }
    }

    [Test]
    public void IsManagedGameplayOrAuthoredScene_RejectsRoutingSettingsRouteMiss_BeforeRuntimeSettingsFallback()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject runtimeSettings = CreateRuntimeSettings(2, (2, scene.name));
        ScriptableObject routingSettings = CreateRoutingSettings(4, (4, "Assets/Scenes/RouteGraphOnly4F.unity"));

        try
        {
            Assert.That(
                InvokeIsManagedGameplayOrAuthoredScene(scene, null, runtimeSettings, routingSettings),
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
    public void IsManagedGameplayOrAuthoredScene_RejectsRuntimeSettingsRouteMiss_WhenNoSessionExists()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Scene scene = SceneManager.GetActiveScene();
        ScriptableObject settings = CreateRuntimeSettings(2, (2, "Assets/Scenes/SomeOtherRouteDriven2F.unity"));
        ScriptableObject routingSettings = CreateRoutingSettings(2);

        try
        {
            Assert.That(
                InvokeIsManagedGameplayOrAuthoredScene(scene, null, settings, routingSettings),
                Is.False,
                "Runtime settings route data should be authoritative before legacy authored scene-name fallback.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(settings);
        }
    }

    [Test]
    public void IsManagedGameplayOrAuthoredScene_RejectsCanonicalGameplayScene_WhenRuntimeSettingsRouteMisses()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/RMainScene_5F.unity", OpenSceneMode.Single);
        ScriptableObject settings = CreateRuntimeSettings(4, (4, "Assets/Scenes/RMainScene_4F.unity"));
        ScriptableObject routingSettings = CreateRoutingSettings(4);

        try
        {
            Assert.That(
                InvokeIsManagedGameplayOrAuthoredScene(scene, null, settings, routingSettings),
                Is.False,
                "Runtime settings route data should prevent canonical floor scene names from reauthorizing a route miss.");
        }
        finally
        {
            DestroyRoutingSettings(routingSettings);
            UnityObject.DestroyImmediate(settings);
        }
    }

    private static bool InvokeIsManagedGameplayOrAuthoredScene(Scene scene, Component sessionController)
    {
        Type utilityType = RequireType("RSceneRouteMembershipUtility");
        MethodInfo method = FindMembershipMethod(utilityType, parameterCount: 2);
        Assert.That(method, Is.Not.Null, "RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene is missing.");
        return method.Invoke(null, new object[] { scene, sessionController }) is bool value && value;
    }

    private static bool InvokeIsManagedGameplayOrAuthoredScene(Scene scene, Component sessionController, ScriptableObject settings)
    {
        Type utilityType = RequireType("RSceneRouteMembershipUtility");
        MethodInfo method = FindMembershipMethod(utilityType, parameterCount: 3);
        Assert.That(method, Is.Not.Null, "RSceneRouteMembershipUtility injected settings overload is missing.");
        return method.Invoke(null, new object[] { scene, sessionController, settings }) is bool value && value;
    }

    private static bool InvokeIsManagedGameplayOrAuthoredScene(
        Scene scene,
        Component sessionController,
        ScriptableObject settings,
        ScriptableObject routingSettings)
    {
        Type utilityType = RequireType("RSceneRouteMembershipUtility");
        MethodInfo method = FindMembershipMethod(utilityType, parameterCount: 4);
        Assert.That(method, Is.Not.Null, "RSceneRouteMembershipUtility injected routing settings overload is missing.");
        return method.Invoke(null, new object[] { scene, sessionController, settings, routingSettings }) is bool value && value;
    }

    private static MethodInfo FindMembershipMethod(Type utilityType, int parameterCount)
    {
        MethodInfo[] methods = utilityType.GetMethods(StaticFlags);

        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo method = methods[index];

            if (method.Name == "IsManagedGameplayOrAuthoredScene"
                && method.GetParameters().Length == parameterCount)
            {
                return method;
            }
        }

        return null;
    }

    private static void ConfigureSessionRoute(Component sessionController, int startingFloorNumber, params (int FloorNumber, string ScenePath)[] routes)
    {
        SetField(sessionController, "startingFloorNumber", startingFloorNumber);
        SetField(sessionController, "floorScenes", CreateRouteEntriesArray(routes));
    }

    private static Array CreateRouteEntriesArray(params (int FloorNumber, string ScenePath)[] routes)
    {
        Type routeType = RequireType("RFloorSceneEntry");
        Array entries = Array.CreateInstance(routeType, routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            entries.SetValue(Activator.CreateInstance(routeType, routes[index].FloorNumber, routes[index].ScenePath), index);
        }

        return entries;
    }

    private static ScriptableObject CreateRuntimeSettings(int startingFloorNumber, params (int FloorNumber, string ScenePath)[] routes)
    {
        Type settingsType = RequireType("MainEscapeRuntimeSettings");
        var settings = (ScriptableObject)ScriptableObject.CreateInstance(settingsType);
        FieldInfo scenesField = settingsType.GetField("scenes", InstanceFlags);
        Assert.That(scenesField, Is.Not.Null, "MainEscapeRuntimeSettings.scenes is missing.");
        object sceneSettings = scenesField.GetValue(settings);
        Assert.That(sceneSettings, Is.Not.Null, "MainEscapeRuntimeSettings.scenes is not initialized.");
        SetObjectField(sceneSettings, "authoredFloorNumber", startingFloorNumber);
        SetObjectField(sceneSettings, "floorSceneRoutes", CreateRouteEntriesArray(routes));
        return settings;
    }

    private static ScriptableObject CreateRoutingSettings(int startingFloorNumber, params (int FloorNumber, string ScenePath)[] routes)
    {
        Type routingSettingsType = RequireType("RRunRoutingSettings");
        Type chapterType = RequireType("ChapterDefinition");
        Type routeGraphType = RequireType("RouteGraphDefinition");

        var routingSettings = (ScriptableObject)ScriptableObject.CreateInstance(routingSettingsType);
        var chapter = (ScriptableObject)ScriptableObject.CreateInstance(chapterType);
        var routeGraph = (ScriptableObject)ScriptableObject.CreateInstance(routeGraphType);

        SetObjectField(routeGraph, "routeGraphId", "test_route_graph");
        SetObjectField(routeGraph, "startNodeId", $"test_{startingFloorNumber}f");
        SetObjectField(routeGraph, "sceneNodes", CreateSceneNodeArray(routes));
        SetObjectField(routeGraph, "edges", Array.CreateInstance(RequireType("RouteEdgeDefinition"), 0));
        SetObjectField(chapter, "chapterId", "test_chapter");
        SetObjectField(chapter, "startRouteGraph", routeGraph);
        SetObjectField(routingSettings, "defaultChapter", chapter);
        SetObjectField(routingSettings, "lobbyScenePath", "Assets/Scenes/RMainEscape_Lobby.unity");
        return routingSettings;
    }

    private static Array CreateSceneNodeArray(params (int FloorNumber, string ScenePath)[] routes)
    {
        Type sceneNodeType = RequireType("SceneNodeDefinition");
        Array nodes = Array.CreateInstance(sceneNodeType, routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            object node = Activator.CreateInstance(sceneNodeType);
            string floorId = $"{routes[index].FloorNumber}f";
            SetObjectField(node, "nodeId", $"test_{floorId}");
            SetObjectField(node, "scenePath", routes[index].ScenePath);
            SetObjectField(node, "chapterLocalLevelId", floorId);
            nodes.SetValue(node, index);
        }

        return nodes;
    }

    private static void DestroyRoutingSettings(ScriptableObject routingSettings)
    {
        if (routingSettings == null)
        {
            return;
        }

        object chapter = ReadObjectProperty(routingSettings, "DefaultChapter");
        object routeGraph = chapter != null ? ReadObjectProperty(chapter, "StartRouteGraph") : null;

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

    private static object ReadObjectProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{target.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(target);
    }

    private static void SetField(Component target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
    }

    private static void SetObjectField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
    }

    private static Type RequireType(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(resolved, Is.Not.Null, $"{typeName} type is missing.");
        return resolved;
    }
}

using System;
using System.Reflection;
using NUnit.Framework;

public sealed class RRunSceneConfigurationUtilityTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [Test]
    public void ResolveNextFloorNumber_ReturnsZero_WhenRoutesAreMissing()
    {
        int nextFloor = InvokeResolveNextFloorNumber(5, null);

        Assert.That(nextFloor, Is.EqualTo(0));
    }

    [Test]
    public void ResolveNextFloorNumber_UsesConfiguredLowerFloor()
    {
        Array routes = CreateRouteEntriesArray(
            (5, "Assets/Scenes/RMainScene_5F.unity"),
            (4, "Assets/Scenes/RMainScene_4F.unity"),
            (2, "Assets/Scenes/RMainScene_2F.unity"));

        int nextFloor = InvokeResolveNextFloorNumber(5, routes);

        Assert.That(nextFloor, Is.EqualTo(4));
    }

    [Test]
    public void ResolveLobbyScenePath_PrefersSceneLocalPath_WhenLegacyFlagIsFalse()
    {
        object settings = LoadRuntimeSettings();

        string scenePath = InvokeResolveLobbyScenePath(
            settings,
            "Assets/Scenes/OverrideLobby.unity",
            preferSceneLocal: false);

        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/OverrideLobby.unity"));
    }

    [Test]
    public void ResolveStartingFloorNumber_PrefersSceneLocalFloor_WhenLegacyFlagIsFalse()
    {
        object settings = LoadRuntimeSettings();

        int floorNumber = InvokeResolveStartingFloorNumber(
            settings,
            3,
            preferSceneLocal: false);

        Assert.That(floorNumber, Is.EqualTo(3));
    }

    [Test]
    public void ResolveFloorScenes_PrefersSceneLocalRoutes_WhenLegacyFlagIsFalse()
    {
        object settings = LoadRuntimeSettings();
        Array routes = CreateRouteEntriesArray(
            (3, "Assets/Scenes/Override3F.unity"),
            (2, "Assets/Scenes/Override2F.unity"));

        Array resolvedRoutes = InvokeResolveFloorScenes(settings, routes, preferSceneLocal: false);

        Assert.That(resolvedRoutes, Is.Not.Null);
        Assert.That(resolvedRoutes.Length, Is.EqualTo(2));
        Assert.That(ReadRouteFloorNumber(resolvedRoutes.GetValue(0)), Is.EqualTo(3));
        Assert.That(ReadRouteScenePath(resolvedRoutes.GetValue(0)), Is.EqualTo("Assets/Scenes/Override3F.unity"));
    }

    private static int InvokeResolveNextFloorNumber(int currentFloorNumber, object routes)
    {
        Type utilityType = FindTypeByName("RRunSceneConfigurationUtility");
        Assert.That(utilityType, Is.Not.Null, "RRunSceneConfigurationUtility type is missing.");

        MethodInfo method = utilityType.GetMethod("ResolveNextFloorNumber", StaticFlags);
        Assert.That(method, Is.Not.Null, "RRunSceneConfigurationUtility.ResolveNextFloorNumber is missing.");

        return Convert.ToInt32(method.Invoke(null, new[] { (object)currentFloorNumber, routes }));
    }

    private static string InvokeResolveLobbyScenePath(object settings, string sceneLocalPath, bool preferSceneLocal)
    {
        Type utilityType = FindTypeByName("RRunSceneConfigurationUtility");
        Assert.That(utilityType, Is.Not.Null, "RRunSceneConfigurationUtility type is missing.");

        MethodInfo method = utilityType.GetMethod("ResolveLobbyScenePath", StaticFlags);
        Assert.That(method, Is.Not.Null, "RRunSceneConfigurationUtility.ResolveLobbyScenePath is missing.");

        return method.Invoke(null, new[] { settings, sceneLocalPath, preferSceneLocal }) as string;
    }

    private static int InvokeResolveStartingFloorNumber(object settings, int sceneLocalFloorNumber, bool preferSceneLocal)
    {
        Type utilityType = FindTypeByName("RRunSceneConfigurationUtility");
        Assert.That(utilityType, Is.Not.Null, "RRunSceneConfigurationUtility type is missing.");

        MethodInfo method = utilityType.GetMethod("ResolveStartingFloorNumber", StaticFlags);
        Assert.That(method, Is.Not.Null, "RRunSceneConfigurationUtility.ResolveStartingFloorNumber is missing.");

        return Convert.ToInt32(method.Invoke(null, new[] { settings, sceneLocalFloorNumber, preferSceneLocal }));
    }

    private static Array InvokeResolveFloorScenes(object settings, object routes, bool preferSceneLocal)
    {
        Type utilityType = FindTypeByName("RRunSceneConfigurationUtility");
        Assert.That(utilityType, Is.Not.Null, "RRunSceneConfigurationUtility type is missing.");

        MethodInfo method = utilityType.GetMethod("ResolveFloorScenes", StaticFlags);
        Assert.That(method, Is.Not.Null, "RRunSceneConfigurationUtility.ResolveFloorScenes is missing.");

        return method.Invoke(null, new[] { settings, routes, preferSceneLocal }) as Array;
    }

    private static object LoadRuntimeSettings()
    {
        Type settingsType = FindTypeByName("MainEscapeRuntimeSettings");
        Assert.That(settingsType, Is.Not.Null, "MainEscapeRuntimeSettings type is missing.");

        MethodInfo loadMethod = settingsType.GetMethod("Load", StaticFlags);
        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimeSettings.Load is missing.");
        return loadMethod.Invoke(null, null);
    }

    private static int ReadRouteFloorNumber(object route)
    {
        FieldInfo field = route.GetType().GetField("floorNumber");
        Assert.That(field, Is.Not.Null, "RFloorSceneEntry.floorNumber is missing.");
        return Convert.ToInt32(field.GetValue(route));
    }

    private static string ReadRouteScenePath(object route)
    {
        FieldInfo field = route.GetType().GetField("scenePath");
        Assert.That(field, Is.Not.Null, "RFloorSceneEntry.scenePath is missing.");
        return field.GetValue(route) as string;
    }

    private static Array CreateRouteEntriesArray(params (int floorNumber, string scenePath)[] routes)
    {
        Type routeType = FindTypeByName("RFloorSceneEntry");
        Assert.That(routeType, Is.Not.Null, "RFloorSceneEntry type is missing.");

        Array entries = Array.CreateInstance(routeType, routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            entries.SetValue(Activator.CreateInstance(routeType, routes[index].floorNumber, routes[index].scenePath), index);
        }

        return entries;
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

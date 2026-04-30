using System;
using System.Reflection;
using NUnit.Framework;

public sealed class MainEscapeRuntimeSettingsTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Test]
    public void RuntimeSettings_RouteAlignment_MatchesLobbyAndFloorSceneChain()
    {
        object settings = LoadRuntimeSettings();

        Assert.That(settings, Is.Not.Null);
        Assert.That(ReadStringProperty(settings, "LobbyScenePath"), Is.EqualTo("Assets/Scenes/RMainEscape_Lobby.unity"));
        Assert.That(ReadStringProperty(settings, "GameplayScenePath"), Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        Assert.That(ReadStringProperty(settings, "MainScenePath"), Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        Assert.That(InvokeInstanceMethod(settings, "GetStartFloorScenePath") as string, Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));

        AssertFloorRoute(settings, 5, "Assets/Scenes/RMainScene_5F.unity");
        AssertFloorRoute(settings, 4, "Assets/Scenes/RMainScene_4F.unity");
        AssertFloorRoute(settings, 3, "Assets/Scenes/RMainScene_3F.unity");
        AssertFloorRoute(settings, 2, "Assets/Scenes/RMainScene_2F.unity");
        AssertFloorRoute(settings, 1, "Assets/Scenes/RMainScene_1F.unity");

        Array routes = ReadProperty(settings, "FloorSceneRoutes") as Array;
        Assert.That(routes, Is.Not.Null);
        Assert.That(routes.Length, Is.EqualTo(5));
    }

    [Test]
    public void RuntimeSettings_FallbackRouteApi_MirrorsAlignmentData()
    {
        object settings = LoadRuntimeSettings();

        Assert.That(ReadStringProperty(settings, "FallbackLobbyScenePath"), Is.EqualTo("Assets/Scenes/RMainEscape_Lobby.unity"));
        Assert.That(ReadIntProperty(settings, "FallbackStartFloorNumber"), Is.EqualTo(5));
        Assert.That(InvokeInstanceMethod(settings, "GetFallbackStartFloorScenePath") as string, Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        Assert.That(TryGetFallbackScenePathForFloor(settings, 4, out string scenePath), Is.True);
        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/RMainScene_4F.unity"));

        Array fallbackRoutes = ReadProperty(settings, "FallbackFloorSceneRoutes") as Array;
        Assert.That(fallbackRoutes, Is.Not.Null);
        Assert.That(fallbackRoutes.Length, Is.EqualTo(5));
    }

    [Test]
    public void RuntimeSettings_DebugHotkeys_SeparateFullDebugAndInvincibilityOnly()
    {
        object settings = LoadRuntimeSettings();

        Assert.That(settings, Is.Not.Null);
        Assert.That(ReadProperty(settings, "DebugToggleKey").ToString(), Is.EqualTo("F1"));
        Assert.That(ReadProperty(settings, "InvincibilityOnlyToggleKey").ToString(), Is.EqualTo("F2"));
        Assert.That(ReadProperty(settings, "PerformanceOverlayToggleKey").ToString(), Is.EqualTo("F3"));
        Assert.That(ReadProperty(settings, "DebugToggleKey"), Is.Not.EqualTo(ReadProperty(settings, "InvincibilityOnlyToggleKey")));
        Assert.That(ReadProperty(settings, "PerformanceOverlayToggleKey"), Is.Not.EqualTo(ReadProperty(settings, "DebugToggleKey")));
        Assert.That(ReadProperty(settings, "PerformanceOverlayToggleKey"), Is.Not.EqualTo(ReadProperty(settings, "InvincibilityOnlyToggleKey")));
    }

    [Test]
    public void RuntimeSettings_Defaults_To60FpsCap_WithVSyncDisabledForCap()
    {
        object settings = LoadRuntimeSettings();

        Assert.That(settings, Is.Not.Null);
        Assert.That(ReadIntProperty(settings, "TargetFrameRate"), Is.EqualTo(60));
        Assert.That(ReadProperty(settings, "DisableVSyncForTargetFrameRate"), Is.EqualTo(true));
    }

    [Test]
    public void RuntimeSettings_DefaultFlashlightShadows_AreDisabled_ForRuntimeStability()
    {
        object settings = LoadRuntimeSettings();

        Assert.That(settings, Is.Not.Null);
        Assert.That(ReadProperty(settings, "DefaultFlashlightShadowsEnabled"), Is.EqualTo(false));
        Assert.That(ReadProperty(settings, "DebugFlashlightShadowsEnabled"), Is.EqualTo(false));
    }

    [Test]
    public void FloorCatalog_IsOrderedFromFiveToOne()
    {
        Type catalogType = FindTypeByName("MainEscapeFloorCatalog");
        Assert.That(catalogType, Is.Not.Null, "MainEscapeFloorCatalog type is missing.");

        PropertyInfo allFloors = catalogType.GetProperty("AllFloors", StaticFlags);
        Assert.That(allFloors, Is.Not.Null, "MainEscapeFloorCatalog.AllFloors is missing.");

        Array floors = allFloors.GetValue(null) as Array;
        Assert.That(floors, Is.Not.Null);
        Assert.That(floors.Length, Is.EqualTo(5));
        Assert.That(ReadIntProperty(floors.GetValue(0), "FloorNumber"), Is.EqualTo(5));
        Assert.That(ReadIntProperty(floors.GetValue(1), "FloorNumber"), Is.EqualTo(4));
        Assert.That(ReadIntProperty(floors.GetValue(2), "FloorNumber"), Is.EqualTo(3));
        Assert.That(ReadIntProperty(floors.GetValue(3), "FloorNumber"), Is.EqualTo(2));
        Assert.That(ReadIntProperty(floors.GetValue(4), "FloorNumber"), Is.EqualTo(1));
        Assert.That(ReadProperty(floors.GetValue(4), "VariantId").ToString(), Is.EqualTo("Lobby1F"));
    }

    private static object LoadRuntimeSettings()
    {
        Type settingsType = FindTypeByName("MainEscapeRuntimeSettings");
        Assert.That(settingsType, Is.Not.Null, "MainEscapeRuntimeSettings type is missing.");

        MethodInfo loadMethod = settingsType.GetMethod("Load", StaticFlags);
        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimeSettings.Load is missing.");
        return loadMethod.Invoke(null, null);
    }

    private static void AssertFloorRoute(object settings, int floorNumber, string expectedScenePath)
    {
        Assert.That(TryGetScenePathForFloor(settings, floorNumber, out string scenePath), Is.True, $"{floorNumber}F scene path is missing.");
        Assert.That(scenePath, Is.EqualTo(expectedScenePath), $"{floorNumber}F scene path is incorrect.");
    }

    private static bool TryGetScenePathForFloor(object settings, int floorNumber, out string scenePath)
    {
        MethodInfo method = settings.GetType().GetMethod("TryGetScenePathForFloor", InstanceFlags);
        Assert.That(method, Is.Not.Null, "MainEscapeRuntimeSettings.TryGetScenePathForFloor is missing.");

        object[] arguments = { floorNumber, null };
        bool resolved = method.Invoke(settings, arguments) is bool value && value;
        scenePath = arguments[1] as string ?? string.Empty;
        return resolved;
    }

    private static bool TryGetFallbackScenePathForFloor(object settings, int floorNumber, out string scenePath)
    {
        MethodInfo method = settings.GetType().GetMethod("TryGetFallbackScenePathForFloor", InstanceFlags);
        Assert.That(method, Is.Not.Null, "MainEscapeRuntimeSettings.TryGetFallbackScenePathForFloor is missing.");

        object[] arguments = { floorNumber, null };
        bool resolved = method.Invoke(settings, arguments) is bool value && value;
        scenePath = arguments[1] as string ?? string.Empty;
        return resolved;
    }

    private static string ReadStringProperty(object instance, string propertyName)
    {
        return ReadProperty(instance, propertyName) as string;
    }

    private static int ReadIntProperty(object instance, string propertyName)
    {
        return Convert.ToInt32(ReadProperty(instance, propertyName));
    }

    private static object ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance);
    }

    private static object InvokeInstanceMethod(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        return method.Invoke(instance, arguments);
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

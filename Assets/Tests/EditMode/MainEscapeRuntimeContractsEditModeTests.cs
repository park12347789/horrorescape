using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public sealed class MainEscapeRuntimeContractsEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void RuntimeSettings_Alignment_StartFloorScenePath_ResolvesToExistingSceneAsset()
    {
        object settings = LoadRuntimeSettings();

        Assert.That(settings, Is.Not.Null, "MainEscapeRuntimeSettings.Load() returned null.");
        string mainScenePath = InvokeInstanceMethod(settings, "GetStartFloorScenePath") as string;
        Assert.That(mainScenePath, Is.Not.Null.And.Not.Empty, "Start floor scene path is empty.");
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(mainScenePath),
            Is.Not.Null,
            $"Start floor scene asset was not found at '{mainScenePath}'.");
    }

    [Test]
    public void FloorCatalog_IsUniqueAndStrictlyDescendingFromFiveToOne()
    {
        Type floorCatalogType = FindTypeByName("MainEscapeFloorCatalog");
        Assert.That(floorCatalogType, Is.Not.Null, "MainEscapeFloorCatalog type is missing.");

        PropertyInfo allFloorsProperty = floorCatalogType.GetProperty("AllFloors", StaticMemberFlags);
        Assert.That(allFloorsProperty, Is.Not.Null, "MainEscapeFloorCatalog.AllFloors property is missing.");

        Array floors = allFloorsProperty.GetValue(null) as Array;

        Assert.That(floors, Is.Not.Null);
        Assert.That(floors.Length, Is.EqualTo(5), "Expected a 5-floor loop (5F -> 1F).");

        int[] floorNumbers = floors
            .Cast<object>()
            .Select(floor => Convert.ToInt32(floor.GetType().GetProperty("FloorNumber", MemberFlags)?.GetValue(floor)))
            .ToArray();

        Assert.That(floorNumbers.Distinct().Count(), Is.EqualTo(floorNumbers.Length), "Floor numbers must be unique.");

        int[] expected = { 5, 4, 3, 2, 1 };
        CollectionAssert.AreEqual(expected, floorNumbers, "Floor order must remain 5F -> 1F.");
    }

    [Test]
    public void RuntimeSettings_Alignment_ExposesLobbyAndGameplayPaths_WhenLobbyLoopIsIntegrated()
    {
        object settings = LoadRuntimeSettings();
        string lobbyPath = ReadStringProperty(settings, "LobbyScenePath");
        string gameplayPath = ReadStringProperty(settings, "GameplayScenePath");

        Assert.That(lobbyPath, Is.Not.Null.And.Not.Empty, "LobbyScenePath is empty.");
        Assert.That(gameplayPath, Is.Not.Null.And.Not.Empty, "GameplayScenePath is empty.");
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(lobbyPath),
            Is.Not.Null,
            $"Lobby scene asset was not found at '{lobbyPath}'.");
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(gameplayPath),
            Is.Not.Null,
            $"Gameplay scene asset was not found at '{gameplayPath}'.");
    }

    [Test]
    public void RuntimeSettings_Alignment_ExposesFloorSceneRoutes_InDescendingOrder()
    {
        object settings = LoadRuntimeSettings();
        Array routes = ReadProperty(settings, "FloorSceneRoutes") as Array;

        Assert.That(routes, Is.Not.Null, "FloorSceneRoutes should not be null.");
        Assert.That(routes.Length, Is.EqualTo(5), "Expected a 5-floor scene route list.");
        CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1 }, routes.Cast<object>().Select(route => Convert.ToInt32(ReadFieldOrProperty(route, "floorNumber"))).ToArray());
        Assert.That(TryGetScenePathForFloor(settings, 5, out string startScenePath), Is.True);
        Assert.That(startScenePath, Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
    }

    [Test]
    public void RSessionContracts_ExistAndExposeSnapshotStateMembers()
    {
        Type controllerType = FindTypeByName("RRunSessionController");
        Type snapshotType = FindTypeByName("RRunSnapshot");
        Type outcomeType = FindTypeByName("RRunOutcome");

        Assert.That(controllerType, Is.Not.Null, "RRunSessionController type is missing.");
        Assert.That(snapshotType, Is.Not.Null, "RRunSnapshot type is missing.");
        Assert.That(outcomeType, Is.Not.Null, "RRunOutcome type is missing.");
        Assert.That(HasReadableMember(snapshotType, "CurrentFloorNumber"), Is.True, "Snapshot should expose current floor.");
        Assert.That(HasReadableMember(snapshotType, "FloorsCleared"), Is.True, "Snapshot should expose cleared floor count.");
        Assert.That(
            HasReadableMember(snapshotType, "RunStarted") || HasReadableMember(snapshotType, "HasActiveRun"),
            Is.True,
            "Snapshot should expose whether a run has started.");
        Assert.That(HasReadableMember(snapshotType, "Outcome"), Is.True, "Snapshot should expose final outcome.");
    }

    private static bool HasReadableMember(Type targetType, string memberName)
    {
        return targetType.GetProperty(memberName, MemberFlags) != null
            || targetType.GetField(memberName, MemberFlags) != null;
    }

    private static object LoadRuntimeSettings()
    {
        Type settingsType = FindTypeByName("MainEscapeRuntimeSettings");
        Assert.That(settingsType, Is.Not.Null, "MainEscapeRuntimeSettings type is missing.");

        MethodInfo loadMethod = settingsType.GetMethod("Load", StaticMemberFlags);
        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimeSettings.Load() method is missing.");

        return loadMethod.Invoke(null, null);
    }

    private static bool TryGetScenePathForFloor(object settings, int floorNumber, out string scenePath)
    {
        MethodInfo method = settings.GetType().GetMethod("TryGetScenePathForFloor", MemberFlags);
        Assert.That(method, Is.Not.Null, "MainEscapeRuntimeSettings.TryGetScenePathForFloor() is missing.");

        object[] arguments = { floorNumber, null };
        bool resolved = method.Invoke(settings, arguments) is bool value && value;
        scenePath = arguments[1] as string ?? string.Empty;
        return resolved;
    }

    private static string ReadStringProperty(object instance, string propertyName)
    {
        return ReadFieldOrProperty(instance, propertyName) as string;
    }

    private static object ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, MemberFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} property is missing.");
        return property.GetValue(instance);
    }

    private static object ReadFieldOrProperty(object instance, string memberName)
    {
        PropertyInfo property = instance.GetType().GetProperty(memberName, MemberFlags);

        if (property != null)
        {
            return property.GetValue(instance);
        }

        FieldInfo field = instance.GetType().GetField(memberName, MemberFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{memberName} member is missing.");
        return field.GetValue(instance);
    }

    private static object InvokeInstanceMethod(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, MemberFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName}() is missing.");
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

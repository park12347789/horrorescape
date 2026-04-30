using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RRunSessionControllerTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [TearDown]
    public void TearDown()
    {
        GameObject controllerObject = GameObject.Find("RRunSessionController");

        if (controllerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void BeginNewRun_SeedsInProgressSnapshot()
    {
        object controller = EnsureController();
        Invoke(controller, "BeginNewRun");

        object snapshot = ReadProperty(controller, "Snapshot");
        Assert.That(ReadProperty(snapshot, "RunStarted"), Is.EqualTo(true));
        Assert.That(ReadProperty(snapshot, "CurrentFloorNumber"), Is.EqualTo(5));
        Assert.That(ReadProperty(snapshot, "FloorsCleared"), Is.EqualTo(0));
        Assert.That(ReadProperty(snapshot, "Outcome").ToString(), Is.EqualTo("InProgress"));
    }

    [Test]
    public void RecordFloorClear_UpdatesProgress()
    {
        object controller = EnsureController();
        Invoke(controller, "BeginNewRun");
        Invoke(controller, "RecordFloorClear", 5, 4);

        object snapshot = ReadProperty(controller, "Snapshot");
        Assert.That(ReadProperty(snapshot, "CurrentFloorNumber"), Is.EqualTo(4));
        Assert.That(ReadProperty(snapshot, "FloorsCleared"), Is.EqualTo(1));
        Assert.That(ReadProperty(snapshot, "Outcome").ToString(), Is.EqualTo("InProgress"));
    }

    [Test]
    public void RecordFailure_CapturesOutcomeAndSource()
    {
        object controller = EnsureController();
        Invoke(controller, "BeginNewRun");
        Invoke(controller, "RecordFailure", 3, "SentryGuard_01");

        object snapshot = ReadProperty(controller, "Snapshot");
        Assert.That(ReadProperty(snapshot, "Outcome").ToString(), Is.EqualTo("Failed"));
        Assert.That(ReadProperty(snapshot, "CurrentFloorNumber"), Is.EqualTo(3));
        Assert.That(ReadProperty(snapshot, "FailureSource"), Is.EqualTo("SentryGuard_01"));
        Assert.That(ReadProperty(snapshot, "SummaryTitle"), Is.EqualTo("Last Descent Failed"));
    }

    [Test]
    public void PlayerStatePersistenceContracts_Exist()
    {
        Type persistenceType = FindTypeByName("RRunPlayerStatePersistence");
        Type stateType = FindTypeByName("RRunSavedPlayerState");
        Type storeType = FindTypeByName("RRunPlayerStateStore");
        Type storeContractType = FindTypeByName("IRunPlayerStateStore");
        Type controllerType = FindTypeByName("RRunSessionController");

        Assert.That(persistenceType, Is.Not.Null, "RRunPlayerStatePersistence type is missing.");
        Assert.That(stateType, Is.Not.Null, "RRunSavedPlayerState type is missing.");
        Assert.That(storeType, Is.Not.Null, "RRunPlayerStateStore type is missing.");
        Assert.That(storeContractType, Is.Not.Null, "IRunPlayerStateStore type is missing.");
        Assert.That(controllerType, Is.Not.Null, "RRunSessionController type is missing.");
        Assert.That(storeContractType.IsAssignableFrom(storeType), Is.True, "RRunPlayerStateStore should implement IRunPlayerStateStore.");
        Assert.That(storeContractType.IsAssignableFrom(controllerType), Is.False, "RRunSessionController should orchestrate persistence, not implement the store contract directly.");
        Assert.That(persistenceType.GetMethod("Capture", BindingFlags.Static | BindingFlags.Public), Is.Not.Null);
        Assert.That(persistenceType.GetMethod("TryRestore", BindingFlags.Static | BindingFlags.Public), Is.Not.Null);
        Assert.That(persistenceType.GetMethod("Clone", BindingFlags.Static | BindingFlags.Public), Is.Not.Null);
        Assert.That(persistenceType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public), Is.Not.Null);
    }

    [Test]
    public void ResolvePlayerStateStore_DoesNotFallBackToSessionController()
    {
        object controller = EnsureController();
        Type controllerType = controller.GetType();
        Type storeContractType = FindTypeByName("IRunPlayerStateStore");
        Assert.That(storeContractType, Is.Not.Null, "IRunPlayerStateStore type is missing.");

        MethodInfo resolveMethod = controllerType.GetMethod("ResolvePlayerStateStore", InstanceFlags);
        Assert.That(resolveMethod, Is.Not.Null, $"{controllerType.Name}.ResolvePlayerStateStore is missing.");

        object resolvedStore = resolveMethod.Invoke(controller, null);

        Assert.That(resolvedStore, Is.Not.Null, "ResolvePlayerStateStore should always return a usable store.");
        Assert.That(storeContractType.IsInstanceOfType(resolvedStore), Is.True, "ResolvePlayerStateStore should return an IRunPlayerStateStore implementation.");
        Assert.That(ReferenceEquals(resolvedStore, controller), Is.False, "ResolvePlayerStateStore should not fall back to the session controller itself.");
    }

    [Test]
    public void PlayerStatePersistence_RestoresCapturedInventoryItems()
    {
        GameObject playerObject = new("PlayerRuntime");

        try
        {
            Type inventoryType = FindTypeByName("PlayerInventory");
            Type runtimeType = FindTypeByName("RPlayerRuntimeReferences");
            Type persistenceType = FindTypeByName("RRunPlayerStatePersistence");
            Assert.That(inventoryType, Is.Not.Null, "PlayerInventory type is missing.");
            Assert.That(runtimeType, Is.Not.Null, "RPlayerRuntimeReferences type is missing.");
            Assert.That(persistenceType, Is.Not.Null, "RRunPlayerStatePersistence type is missing.");

            Component inventory = playerObject.AddComponent(inventoryType);
            Component runtime = playerObject.AddComponent(runtimeType);
            Invoke(runtime, "CacheExistingReferences");

            string flashlightItemId = ReadStaticStringField("PrototypeItemCatalog", "FlashlightItemId");
            string bottleItemId = ReadStaticStringField("PrototypeItemCatalog", "GlassBottleItemId");
            string medkitItemId = ReadStaticStringField("PrototypeItemCatalog", "MedkitItemId");

            Assert.That(InvokeBool(inventory, "AddItem", flashlightItemId, "Flashlight", 1), Is.True);
            Assert.That(InvokeBool(inventory, "AddItem", bottleItemId, "Glass Bottle", 2), Is.True);
            Assert.That(InvokeBool(inventory, "AddItem", medkitItemId, "Medkit", 1), Is.True);

            object capturedState = InvokeStatic(
                persistenceType,
                "Capture",
                runtime,
                InvokeStatic(persistenceType, "Clear"));

            ClearInventory(inventory);
            Assert.That(InvokeInt(inventory, "GetQuantity", flashlightItemId), Is.EqualTo(0));
            Assert.That(InvokeInt(inventory, "GetQuantity", bottleItemId), Is.EqualTo(0));
            Assert.That(InvokeInt(inventory, "GetQuantity", medkitItemId), Is.EqualTo(0));

            bool restored = InvokeStatic(persistenceType,
                "TryRestore",
                runtime,
                capturedState,
                InvokeStatic(persistenceType, "Clear")) is bool value && value;

            Assert.That(restored, Is.True);
            Assert.That(InvokeInt(inventory, "GetQuantity", flashlightItemId), Is.EqualTo(1));
            Assert.That(InvokeInt(inventory, "GetQuantity", bottleItemId), Is.EqualTo(2));
            Assert.That(InvokeInt(inventory, "GetQuantity", medkitItemId), Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void LegacyStaticPlayerStateFallbackFields_AreRemoved()
    {
        Type controllerType = FindTypeByName("RRunSessionController");
        Assert.That(controllerType, Is.Not.Null, "RRunSessionController type is missing.");

        Assert.That(controllerType.GetField("hasStaticSavedPlayerState", InstanceFlags), Is.Null);
        Assert.That(controllerType.GetField("staticSavedPlayerHealth", InstanceFlags), Is.Null);
        Assert.That(controllerType.GetField("staticSavedFlashlightChargeNormalized", InstanceFlags), Is.Null);
        Assert.That(controllerType.GetField("staticSavedFlashlightEnabled", InstanceFlags), Is.Null);
        Assert.That(controllerType.GetField("staticSavedInventoryItems", InstanceFlags), Is.Null);
    }

    [Test]
    public void SceneRouting_DefaultSerializedConfiguration_MatchesCanonicalSceneChain()
    {
        object controller = EnsureController();

        Assert.That(ReadProperty(controller, "LobbyScenePath"), Is.EqualTo("Assets/Scenes/RMainEscape_Lobby.unity"));
        Assert.That(ReadProperty(controller, "StartingFloorNumber"), Is.EqualTo(5));

        for (int floorNumber = 5; floorNumber >= 1; floorNumber--)
        {
            Assert.That(InvokeWithOutString(controller, "TryGetScenePathForFloor", floorNumber, out string scenePath), Is.True);
            Assert.That(scenePath, Is.EqualTo($"Assets/Scenes/RMainScene_{floorNumber}F.unity"));
        }
    }

    [Test]
    public void SceneRouting_UsesSceneLocalRoutes_WhenLegacyOverrideFlagIsDisabled()
    {
        object controller = EnsureController();

        SetField(controller, "lobbyScenePath", "Assets/Scenes/OverrideLobby.unity");
        SetField(controller, "startingFloorNumber", 3);
        SetField(controller, "floorScenes", CreateRouteEntriesArray(
            (3, "Assets/Scenes/Override3F.unity"),
            (2, "Assets/Scenes/Override2F.unity")));
        SetField(controller, "useSceneLocalRoutingOverrides", false);

        Assert.That(ReadProperty(controller, "LobbyScenePath"), Is.EqualTo("Assets/Scenes/OverrideLobby.unity"));
        Assert.That(ReadProperty(controller, "StartingFloorNumber"), Is.EqualTo(3));

        bool resolved = InvokeWithOutString(controller, "TryGetScenePathForFloor", 3, out string scenePath);
        Assert.That(resolved, Is.True);
        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/Override3F.unity"));
    }

    [Test]
    public void SceneRouting_FallsBackToRuntimeSettingsAlignment_WhenSceneLocalRoutesAreMissing()
    {
        object controller = EnsureController();

        SetField(controller, "lobbyScenePath", string.Empty);
        SetField(controller, "startingFloorNumber", 0);
        SetField(controller, "floorScenes", CreateRouteEntriesArray());
        SetField(controller, "useSceneLocalRoutingOverrides", false);

        Assert.That(ReadProperty(controller, "LobbyScenePath"), Is.EqualTo("Assets/Scenes/RMainEscape_Lobby.unity"));
        Assert.That(ReadProperty(controller, "StartingFloorNumber"), Is.EqualTo(5));

        bool resolved = InvokeWithOutString(controller, "TryGetScenePathForFloor", 5, out string scenePath);
        Assert.That(resolved, Is.True);
        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
    }

    [Test]
    public void SceneRouting_PrefersSceneLocalSerializedRoutes_WhenEnabled()
    {
        object controller = EnsureController();

        SetField(controller, "lobbyScenePath", "Assets/Scenes/OverrideLobby.unity");
        SetField(controller, "startingFloorNumber", 3);
        SetField(controller, "floorScenes", CreateRouteEntriesArray(
            (3, "Assets/Scenes/Override3F.unity"),
            (2, "Assets/Scenes/Override2F.unity")));
        SetField(controller, "useSceneLocalRoutingOverrides", true);

        Assert.That(ReadProperty(controller, "LobbyScenePath"), Is.EqualTo("Assets/Scenes/OverrideLobby.unity"));
        Assert.That(ReadProperty(controller, "StartingFloorNumber"), Is.EqualTo(3));
        Assert.That(InvokeWithOutString(controller, "TryGetScenePathForFloor", 3, out string scenePath), Is.True);
        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/Override3F.unity"));
    }

    [Test]
    public void SceneContracts_BuildsHospitalRouteGraphSnapshot_FromConfiguredRoutes()
    {
        object controller = EnsureController();

        object snapshot = InvokeReturn(controller, "BuildRouteGraphSnapshot");

        Assert.That(ReadProperty(snapshot, "ChapterId"), Is.EqualTo("hospital"));
        Assert.That(ReadProperty(snapshot, "RouteGraphId"), Is.EqualTo("hospital_default"));
        Assert.That(ReadProperty(snapshot, "StartNodeId"), Is.EqualTo("hospital_5f"));
        Assert.That(ReadProperty(snapshot, "IsValid"), Is.EqualTo(true));
        Assert.That(ReadProperty(snapshot, "SceneNodes") as Array, Has.Length.EqualTo(5));
        Assert.That(ReadProperty(snapshot, "Edges") as Array, Has.Length.EqualTo(4));
        Assert.That(InvokeReturn(controller, "GetCurrentSceneNodeId"), Is.EqualTo("hospital_5f"));
    }

    [Test]
    public void SceneContracts_RoutingSettingsChapterGraphOverridesConflictingSceneLocalRoutes()
    {
        object controller = EnsureController();
        UnityEngine.Object routingSettings = LoadAsset("RRunRoutingSettings", "Assets/Resources/MainEscape/Run/RRunRoutingSettings.asset");

        SetField(controller, "routingSettings", routingSettings);
        SetField(controller, "startingFloorNumber", 3);
        SetField(controller, "floorScenes", CreateRouteEntriesArray(
            (3, "Assets/Scenes/Override3F.unity"),
            (2, "Assets/Scenes/Override2F.unity")));

        Assert.That(ReadProperty(controller, "StartingFloorNumber"), Is.EqualTo(5));
        Assert.That(InvokeWithOutString(controller, "TryGetScenePathForFloor", 5, out string scenePath), Is.True);
        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        Assert.That(InvokeReturn(controller, "ResolveNextFloorNumber", 5), Is.EqualTo(4));
        Assert.That(InvokeReturn(controller, "GetCurrentSceneNodeId"), Is.EqualTo("hospital_5f"));
    }

    [Test]
    public void SceneContracts_SelectedChapterOverridesConflictingSceneLocalRoutes()
    {
        object controller = EnsureController();
        UnityEngine.Object chapter = LoadAsset("ChapterDefinition", "Assets/Data/MainEscape/Contracts/HospitalChapter.asset");

        SetField(controller, "startingFloorNumber", 3);
        SetField(controller, "floorScenes", CreateRouteEntriesArray(
            (3, "Assets/Scenes/Override3F.unity"),
            (2, "Assets/Scenes/Override2F.unity")));
        Invoke(controller, "SelectChapter", chapter);

        Assert.That(ReadProperty(controller, "StartingFloorNumber"), Is.EqualTo(5));
        Assert.That(InvokeWithOutString(controller, "TryGetScenePathForFloor", 5, out string scenePath), Is.True);
        Assert.That(scenePath, Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        Assert.That(InvokeReturn(controller, "ResolveNextFloorNumber", 5), Is.EqualTo(4));
    }

    [Test]
    public void SceneContracts_ResolveNextFloorNumber_UsesHospitalRouteGraph()
    {
        object controller = EnsureController();

        SetField(controller, "startingFloorNumber", 5);
        SetField(controller, "floorScenes", CreateRouteEntriesArray(
            (5, "Assets/Scenes/RMainScene_5F.unity"),
            (3, "Assets/Scenes/RMainScene_3F.unity"),
            (1, "Assets/Scenes/RMainScene_1F.unity")));

        Assert.That(InvokeReturn(controller, "ResolveNextFloorNumber", 5), Is.EqualTo(3));
        Assert.That(InvokeReturn(controller, "ResolveNextFloorNumber", 3), Is.EqualTo(1));
        Assert.That(InvokeReturn(controller, "ResolveNextFloorNumber", 1), Is.EqualTo(0));
    }

    [Test]
    public void SceneContracts_CurrentHospitalSceneNodeId_FollowsRunProgress()
    {
        object controller = EnsureController();

        Invoke(controller, "BeginNewRun");
        Invoke(controller, "RecordFloorClear", 5, 4);

        Assert.That(InvokeReturn(controller, "GetCurrentSceneNodeId"), Is.EqualTo("hospital_4f"));
    }

    private static object EnsureController()
    {
        Type controllerType = FindTypeByName("RRunSessionController");
        Assert.That(controllerType, Is.Not.Null, "RRunSessionController type is missing.");

        GameObject controllerObject = new("RRunSessionController");
        return controllerObject.AddComponent(controllerType);
    }

    private static UnityEngine.Object LoadAsset(string typeName, string assetPath)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
        Assert.That(asset, Is.Not.Null, $"{assetPath} could not be loaded as {typeName}.");
        return asset;
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

    private static object ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance);
    }

    private static void Invoke(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        method.Invoke(instance, arguments);
    }

    private static object InvokeReturn(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        return method.Invoke(instance, arguments);
    }

    private static object InvokeStatic(Type type, string methodName, params object[] arguments)
    {
        MethodInfo method = type.GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} is missing.");
        return method.Invoke(null, arguments);
    }

    private static bool InvokeBool(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        return method.Invoke(instance, arguments) is bool value && value;
    }

    private static int InvokeInt(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        return method.Invoke(instance, arguments) is int value ? value : 0;
    }

    private static void ClearInventory(Component inventory)
    {
        Type itemStackType = inventory.GetType().GetNestedType("ItemStack", BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(itemStackType, Is.Not.Null, "PlayerInventory.ItemStack type is missing.");
        Array emptyItems = Array.CreateInstance(itemStackType, 0);
        Invoke(inventory, "SetItems", emptyItems);
    }

    private static string ReadStaticStringField(string typeName, string fieldName)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");
        FieldInfo field = type.GetField(fieldName, StaticFlags);
        Assert.That(field, Is.Not.Null, $"{typeName}.{fieldName} is missing.");
        return field.GetValue(null) as string ?? string.Empty;
    }

    private static bool InvokeWithOutString(object instance, string methodName, int floorNumber, out string scenePath)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");

        object[] arguments = { floorNumber, null };
        bool result = method.Invoke(instance, arguments) is bool value && value;
        scenePath = arguments[1] as string ?? string.Empty;
        return result;
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} is missing.");
        field.SetValue(instance, value);
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

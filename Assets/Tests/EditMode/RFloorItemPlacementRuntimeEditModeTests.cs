using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityObject = UnityEngine.Object;

public sealed class RFloorItemPlacementRuntimeEditModeTests
{
    [Test]
    public void WithoutPlacementCategory_RemovesSupportPlacementsOnly()
    {
        object supportPlacement = CreatePlacement(
            FlashlightBatteryItemId(),
            SupportItemCategory(),
            "support_marker");
        object keyPlacement = CreatePlacement(
            IronGateKeyItemId(),
            KeyCategory(),
            "key_marker");
        object plan = CreatePlacementPlan(supportPlacement, keyPlacement);

        object filteredPlan = InvokePlanMethod(plan, "WithoutPlacementCategory", SupportItemCategory());
        Array placements = GetPlanPlacements(filteredPlan);

        Assert.That(placements, Has.Length.EqualTo(1));
        Assert.That(GetPlacementCategory(placements.GetValue(0)), Is.EqualTo(KeyCategory()));
        Assert.That(GetPlacementItemId(placements.GetValue(0)), Is.EqualTo(IronGateKeyItemId()));
        Assert.That(GetPlacementMarkerId(placements.GetValue(0)), Is.EqualTo("key_marker"));
    }

    [Test]
    public void WithoutPlacementCategory_WhenSupportOnly_ReturnsEmptyPlan()
    {
        object plan = CreatePlacementPlan(
            CreatePlacement(
                FlashlightBatteryItemId(),
                SupportItemCategory(),
                "battery_marker"),
            CreatePlacement(
                GlassBottleItemId(),
                SupportItemCategory(),
                "bottle_marker"));

        object filteredPlan = InvokePlanMethod(plan, "WithoutPlacementCategory", SupportItemCategory());

        Assert.That(GetPlanHasPlacements(filteredPlan), Is.False);
        Assert.That(GetPlanPlacements(filteredPlan), Is.Empty);
    }

    [Test]
    public void WithoutItemIds_RemovesOnlyOwnedSupportItemId_AndKeepsOtherSupportAndKey()
    {
        object batteryPlacement = CreatePlacement(
            FlashlightBatteryItemId(),
            SupportItemCategory(),
            "battery_marker");
        object bottlePlacement = CreatePlacement(
            GlassBottleItemId(),
            SupportItemCategory(),
            "bottle_marker");
        object keyPlacement = CreatePlacement(
            IronGateKeyItemId(),
            KeyCategory(),
            "key_marker");
        object plan = CreatePlacementPlan(batteryPlacement, bottlePlacement, keyPlacement);
        HashSet<string> ownedItemIds = new() { FlashlightBatteryItemId() };

        object filteredPlan = InvokePlanMethod(plan, "WithoutItemIds", ownedItemIds);
        Array placements = GetPlanPlacements(filteredPlan);

        Assert.That(placements, Has.Length.EqualTo(2));
        Assert.That(GetPlacementItemId(placements.GetValue(0)), Is.EqualTo(GlassBottleItemId()));
        Assert.That(GetPlacementItemId(placements.GetValue(1)), Is.EqualTo(IronGateKeyItemId()));
    }

    [Test]
    public void ApplyPlan_WhenAuthoredRuntimePickupRootMissing_ReturnsFalse_AndDoesNotCreateRoot()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject floorObject = new("FloorRoot");
        Component floorAuthoring = MainEscapeReflectionTestHelper.AddComponent(floorObject, "MainEscapeFloorAuthoring");
        ScriptableObject catalog = MainEscapeReflectionTestHelper.CreateScriptableObject("MainEscapeRuntimePrefabCatalog");
        object controller = Activator.CreateInstance(MainEscapeReflectionTestHelper.RequireType("RAuthoredFloorRuntimeItemPlacementController"));
        object plan = CreatePlacementPlan(
            CreatePlacement(
                FlashlightBatteryItemId(),
                SupportItemCategory(),
                "battery_marker"));
        HashSet<string> managedItemIds = new() { FlashlightBatteryItemId() };

        try
        {
            Assert.That(FindTransformRecursive(floorObject.transform, "00_Pickups"), Is.Null);
            LogAssert.Expect(
                LogType.Warning,
                $"RAuthoredFloorRuntimeItemPlacementController could not find authored runtime pickup root '00_Pickups' in scene '{scene.name}'; item placement was not applied to avoid mutating the authored scene hierarchy.");

            bool applied = InvokeControllerApplyPlan(
                controller,
                scene,
                floorObject.transform,
                floorAuthoring,
                catalog,
                plan,
                managedItemIds);

            Assert.That(applied, Is.False);
            Assert.That(FindTransformRecursive(floorObject.transform, "00_Pickups"), Is.Null);
            Assert.That(FindSceneTransform(scene, "00_Pickups"), Is.Null);
        }
        finally
        {
            UnityObject.DestroyImmediate(catalog);
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    [Test]
    public void CollectSceneMarkerSupportPickupItemIds_RequiresActiveRuntimeRootedSupportRule()
    {
        GameObject floorObject = new("FloorAuthoring");
        GameObject managerObject = new("PlacementManager");
        GameObject runtimeRoot = new("SceneManagedPickups");
        GameObject markerRoot = new("SupportMarkers");
        GameObject markerCandidate = new("SupportMarker_01");
        GameObject supportPrefab = CreateSupportPickupPrefab(
            "BatterySupportPickupPrefab",
            FlashlightBatteryItemId());

        try
        {
            Component floorAuthoring = MainEscapeReflectionTestHelper.AddComponent(floorObject, "MainEscapeFloorAuthoring");
            managerObject.transform.SetParent(floorObject.transform);
            runtimeRoot.transform.SetParent(managerObject.transform);
            markerRoot.transform.SetParent(managerObject.transform);
            markerCandidate.transform.SetParent(markerRoot.transform);
            Component placementManager = MainEscapeReflectionTestHelper.AddComponent(managerObject, "RSceneMarkerPlacementManager");
            MainEscapeReflectionTestHelper.SetFieldValue(placementManager, "runtimeRoot", runtimeRoot.transform);
            ConfigurePlacementRule(
                placementManager,
                SupportPickupPlacementKind(),
                supportPrefab,
                1,
                markerRoot.transform);

            HashSet<string> ownedItemIds = InvokeCollectSceneMarkerSupportPickupItemIds(floorAuthoring);

            Assert.That(ownedItemIds, Does.Contain(FlashlightBatteryItemId()));

            ((Behaviour)placementManager).enabled = false;

            Assert.That(InvokeCollectSceneMarkerSupportPickupItemIds(floorAuthoring), Is.Empty);
        }
        finally
        {
            UnityObject.DestroyImmediate(supportPrefab);
            UnityObject.DestroyImmediate(managerObject);
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    [Test]
    public void CollectSceneMarkerSupportPickupItemIds_WhenSupportRuleHasNoMarkerCandidates_ReturnsEmpty()
    {
        GameObject floorObject = new("FloorAuthoring");
        GameObject managerObject = new("PlacementManager");
        GameObject runtimeRoot = new("SceneManagedPickups");
        GameObject markerRoot = new("EmptySupportMarkers");
        GameObject supportPrefab = CreateSupportPickupPrefab(
            "BatterySupportPickupPrefab",
            FlashlightBatteryItemId());

        try
        {
            Component floorAuthoring = MainEscapeReflectionTestHelper.AddComponent(floorObject, "MainEscapeFloorAuthoring");
            managerObject.transform.SetParent(floorObject.transform);
            runtimeRoot.transform.SetParent(managerObject.transform);
            markerRoot.transform.SetParent(managerObject.transform);
            Component placementManager = MainEscapeReflectionTestHelper.AddComponent(managerObject, "RSceneMarkerPlacementManager");
            MainEscapeReflectionTestHelper.SetFieldValue(placementManager, "runtimeRoot", runtimeRoot.transform);
            ConfigurePlacementRule(
                placementManager,
                SupportPickupPlacementKind(),
                supportPrefab,
                1,
                markerRoot.transform);

            Assert.That(InvokeCollectSceneMarkerSupportPickupItemIds(floorAuthoring), Is.Empty);
        }
        finally
        {
            UnityObject.DestroyImmediate(supportPrefab);
            UnityObject.DestroyImmediate(managerObject);
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    [Test]
    public void CollectSceneMarkerSupportPickupItemIds_WhenRuntimeRootMissing_ReturnsEmpty_AndApplyDoesNotCreateRoot()
    {
        GameObject floorObject = new("FloorAuthoring");
        GameObject managerObject = new("PlacementManager");
        GameObject markerRoot = new("SupportMarkers");
        GameObject markerCandidate = new("SupportMarker_01");
        GameObject supportPrefab = CreateSupportPickupPrefab(
            "BatterySupportPickupPrefab",
            FlashlightBatteryItemId());

        try
        {
            Component floorAuthoring = MainEscapeReflectionTestHelper.AddComponent(floorObject, "MainEscapeFloorAuthoring");
            managerObject.transform.SetParent(floorObject.transform);
            markerRoot.transform.SetParent(managerObject.transform);
            markerCandidate.transform.SetParent(markerRoot.transform);
            Component placementManager = MainEscapeReflectionTestHelper.AddComponent(managerObject, "RSceneMarkerPlacementManager");
            ConfigurePlacementRule(
                placementManager,
                SupportPickupPlacementKind(),
                supportPrefab,
                1,
                markerRoot.transform);

            Assert.That(InvokeCollectSceneMarkerSupportPickupItemIds(floorAuthoring), Is.Empty);
            LogAssert.Expect(
                LogType.Warning,
                $"RSceneMarkerPlacementManager on '{managerObject.name}' has no runtimeRoot assigned; marker placements were not applied to avoid mutating the authored scene hierarchy.");

            int appliedCount = InvokePlacementManagerApply(placementManager, 123);

            Assert.That(appliedCount, Is.Zero);
            Assert.That(managerObject.transform.Find($"{managerObject.name}_RuntimeMarkerPlacements"), Is.Null);
        }
        finally
        {
            UnityObject.DestroyImmediate(supportPrefab);
            UnityObject.DestroyImmediate(managerObject);
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    [Test]
    public void CollectSceneMarkerSupportPickupItemIds_WhenSupportPrefabHasNoItemId_ReturnsEmpty()
    {
        GameObject floorObject = new("FloorAuthoring");
        GameObject managerObject = new("PlacementManager");
        GameObject runtimeRoot = new("SceneManagedPickups");
        GameObject markerRoot = new("SupportMarkers");
        GameObject markerCandidate = new("SupportMarker_01");
        GameObject supportPrefab = CreateSupportPickupPrefab("UnconfiguredSupportPickupPrefab", string.Empty);

        try
        {
            Component floorAuthoring = MainEscapeReflectionTestHelper.AddComponent(floorObject, "MainEscapeFloorAuthoring");
            managerObject.transform.SetParent(floorObject.transform);
            runtimeRoot.transform.SetParent(managerObject.transform);
            markerRoot.transform.SetParent(managerObject.transform);
            markerCandidate.transform.SetParent(markerRoot.transform);
            Component placementManager = MainEscapeReflectionTestHelper.AddComponent(managerObject, "RSceneMarkerPlacementManager");
            MainEscapeReflectionTestHelper.SetFieldValue(placementManager, "runtimeRoot", runtimeRoot.transform);
            ConfigurePlacementRule(
                placementManager,
                SupportPickupPlacementKind(),
                supportPrefab,
                1,
                markerRoot.transform);

            Assert.That(InvokeCollectSceneMarkerSupportPickupItemIds(floorAuthoring), Is.Empty);
        }
        finally
        {
            UnityObject.DestroyImmediate(supportPrefab);
            UnityObject.DestroyImmediate(managerObject);
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    private static object CreatePlacement(
        string itemId,
        object placementCategory,
        string markerId)
    {
        Type placementType = MainEscapeReflectionTestHelper.RequireType("RFloorRuntimeItemPlacement");

        return Activator.CreateInstance(
            placementType,
            itemId,
            MainEscapeReflectionTestHelper.CatalogDisplayName(itemId, itemId),
            1,
            Color.white,
            Vector3.zero,
            markerId,
            placementCategory);
    }

    private static object CreatePlacementPlan(params object[] placements)
    {
        Type placementType = MainEscapeReflectionTestHelper.RequireType("RFloorRuntimeItemPlacement");
        Type planType = MainEscapeReflectionTestHelper.RequireType("RFloorItemPlacementPlan");
        Array typedPlacements = Array.CreateInstance(placementType, placements.Length);

        for (int index = 0; index < placements.Length; index++)
        {
            typedPlacements.SetValue(placements[index], index);
        }

        return Activator.CreateInstance(planType, new object[] { typedPlacements });
    }

    private static object InvokePlanMethod(object plan, string methodName, params object[] arguments)
    {
        MethodInfo method = plan.GetType().GetMethod(methodName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"RFloorItemPlacementPlan.{methodName} is missing.");
        return method.Invoke(plan, arguments);
    }

    private static bool InvokeControllerApplyPlan(
        object controller,
        Scene scene,
        Transform floorRoot,
        Component floorAuthoring,
        ScriptableObject catalog,
        object plan,
        IReadOnlyCollection<string> runtimeManagedItemIds)
    {
        MethodInfo method = controller.GetType().GetMethod("ApplyPlan", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "RAuthoredFloorRuntimeItemPlacementController.ApplyPlan is missing.");
        return method.Invoke(
            controller,
            new object[] { scene, floorRoot, floorAuthoring, catalog, plan, runtimeManagedItemIds }) is bool applied && applied;
    }

    private static HashSet<string> InvokeCollectSceneMarkerSupportPickupItemIds(Component floorAuthoring)
    {
        MethodInfo method = MainEscapeReflectionTestHelper.RequireType("RFloorDirector").GetMethod(
            "CollectSceneMarkerSupportPickupItemIds",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, "RFloorDirector is missing scene marker support ownership collection.");
        object result = method.Invoke(null, new object[] { floorAuthoring });
        Assert.That(result, Is.TypeOf<HashSet<string>>());
        return (HashSet<string>)result;
    }

    private static void ConfigurePlacementRule(
        Component placementManager,
        object placementKind,
        GameObject prefab,
        int count,
        Transform markerRoot)
    {
        Type ruleType = placementManager.GetType().GetNestedType("PlacementRule", BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(ruleType, Is.Not.Null, "RSceneMarkerPlacementManager.PlacementRule is missing.");

        object rule = Activator.CreateInstance(ruleType);
        MainEscapeReflectionTestHelper.SetFieldValue(rule, "placementKind", placementKind);
        MainEscapeReflectionTestHelper.SetFieldValue(rule, "prefab", prefab);
        MainEscapeReflectionTestHelper.SetFieldValue(rule, "markerRoot", markerRoot);
        MainEscapeReflectionTestHelper.SetFieldValue(rule, "count", count);

        Array rules = Array.CreateInstance(ruleType, 1);
        rules.SetValue(rule, 0);
        MainEscapeReflectionTestHelper.SetFieldValue(placementManager, "placementRules", rules);
    }

    private static GameObject CreateSupportPickupPrefab(string name, string itemId)
    {
        GameObject prefab = new(name);
        Component pickup = MainEscapeReflectionTestHelper.AddComponent(prefab, "PrototypeInventoryPickup");
        MainEscapeReflectionTestHelper.SetFieldValue(pickup, "itemId", itemId);
        return prefab;
    }

    private static int InvokePlacementManagerApply(Component placementManager, int runSeed)
    {
        MethodInfo method = placementManager.GetType().GetMethod("Apply", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "RSceneMarkerPlacementManager.Apply is missing.");
        return method.Invoke(placementManager, new object[] { runSeed }) is int appliedCount ? appliedCount : 0;
    }

    private static Array GetPlanPlacements(object plan)
    {
        return MainEscapeReflectionTestHelper.GetPropertyValue(plan, "Placements") as Array;
    }

    private static bool GetPlanHasPlacements(object plan)
    {
        return MainEscapeReflectionTestHelper.GetPropertyValue<bool>(plan, "HasPlacements");
    }

    private static string GetPlacementItemId(object placement)
    {
        return MainEscapeReflectionTestHelper.GetPropertyValue<string>(placement, "ItemId");
    }

    private static string GetPlacementMarkerId(object placement)
    {
        return MainEscapeReflectionTestHelper.GetPropertyValue<string>(placement, "MarkerId");
    }

    private static object GetPlacementCategory(object placement)
    {
        return MainEscapeReflectionTestHelper.GetPropertyValue(placement, "PlacementCategory");
    }

    private static object SupportItemCategory()
    {
        return MainEscapeReflectionTestHelper.EnumValue("MainEscapeItemPlacementCategory", "SupportItem");
    }

    private static object KeyCategory()
    {
        return MainEscapeReflectionTestHelper.EnumValue("MainEscapeItemPlacementCategory", "Key");
    }

    private static object SupportPickupPlacementKind()
    {
        return MainEscapeReflectionTestHelper.EnumValue("RSceneMarkerPlacementKind", "SupportPickup");
    }

    private static string FlashlightBatteryItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("FlashlightBatteryItemId");
    }

    private static string GlassBottleItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("GlassBottleItemId");
    }

    private static string MedkitItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("MedkitItemId");
    }

    private static string IronGateKeyItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("IronGateKeyItemId");
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            Transform found = FindTransformRecursive(rootObjects[index].transform, objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindTransformRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, objectName, StringComparison.Ordinal))
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindTransformRecursive(root.GetChild(index), objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

internal static class MainEscapeReflectionTestHelper
{
    public const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static Type RequireType(string typeName)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");
        return type;
    }

    public static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp-Editor")
            ?? Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] candidateTypes;

            try
            {
                candidateTypes = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                candidateTypes = exception.Types;
            }

            if (candidateTypes == null)
            {
                continue;
            }

            for (int typeIndex = 0; typeIndex < candidateTypes.Length; typeIndex++)
            {
                Type candidate = candidateTypes[typeIndex];

                if (candidate != null && (candidate.Name == typeName || candidate.FullName == typeName))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static Component AddComponent(GameObject owner, string typeName)
    {
        Type componentType = RequireType(typeName);
        Assert.That(typeof(Component).IsAssignableFrom(componentType), Is.True, $"{typeName} should be a Unity component type.");
        return owner.AddComponent(componentType);
    }

    public static ScriptableObject CreateScriptableObject(string typeName)
    {
        Type assetType = RequireType(typeName);
        Assert.That(typeof(ScriptableObject).IsAssignableFrom(assetType), Is.True, $"{typeName} should be a ScriptableObject type.");
        return ScriptableObject.CreateInstance(assetType);
    }

    public static object EnumValue(string enumTypeName, string enumValueName)
    {
        Type enumType = RequireType(enumTypeName);
        Assert.That(enumType.IsEnum, Is.True, $"{enumTypeName} should be an enum type.");
        return Enum.Parse(enumType, enumValueName);
    }

    public static string CatalogItemId(string fieldName)
    {
        Type catalogType = RequireType("PrototypeItemCatalog");
        FieldInfo field = catalogType.GetField(fieldName, StaticMemberFlags);
        Assert.That(field, Is.Not.Null, $"PrototypeItemCatalog.{fieldName} is missing.");
        return field.GetValue(null) as string;
    }

    public static string CatalogDisplayName(string itemId, string fallback)
    {
        Type catalogType = RequireType("PrototypeItemCatalog");
        MethodInfo method = catalogType.GetMethod("GetDisplayName", StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "PrototypeItemCatalog.GetDisplayName is missing.");
        return method.Invoke(null, new object[] { itemId, fallback }) as string;
    }

    public static T GetPropertyValue<T>(object owner, string propertyName)
    {
        PropertyInfo property = owner?.GetType().GetProperty(propertyName, InstanceMemberFlags);
        Assert.That(property, Is.Not.Null, $"{owner?.GetType().Name}.{propertyName} is missing.");
        object value = property.GetValue(owner);
        return value is T typedValue ? typedValue : default;
    }

    public static object GetPropertyValue(object owner, string propertyName)
    {
        PropertyInfo property = owner?.GetType().GetProperty(propertyName, InstanceMemberFlags);
        Assert.That(property, Is.Not.Null, $"{owner?.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(owner);
    }

    public static void SetFieldValue(object owner, string fieldName, object value)
    {
        FieldInfo field = FindFieldInHierarchy(owner?.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"{owner?.GetType().Name}.{fieldName} is missing.");
        field.SetValue(owner, value);
    }

    public static object GetFieldValue(object owner, string fieldName)
    {
        FieldInfo field = FindFieldInHierarchy(owner?.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"{owner?.GetType().Name}.{fieldName} is missing.");
        return field.GetValue(owner);
    }

    public static FieldInfo FindFieldInHierarchy(Type type, string fieldName)
    {
        Type currentType = type;

        while (currentType != null)
        {
            FieldInfo field = currentType.GetField(fieldName, InstanceMemberFlags);

            if (field != null)
            {
                return field;
            }

            currentType = currentType.BaseType;
        }

        return null;
    }
}

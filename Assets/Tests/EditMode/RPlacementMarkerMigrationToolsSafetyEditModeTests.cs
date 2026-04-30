using System;
using System.Reflection;

using NUnit.Framework;

using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

public sealed class RPlacementMarkerMigrationToolsSafetyEditModeTests
{
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void CollectSupportItemPickups_SceneOverload_ExcludesSuppressedAuthoredFixedPickup()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        try
        {
            Component retainedPickup = CreatePickup(
                "RuntimeManagedBattery",
                null,
                FlashlightBatteryItemId(),
                suppressRuntimeManagedReplacement: false,
                position: new Vector3(2f, 1f, 0f));
            Component suppressedPickup = CreatePickup(
                "FixedStarterBattery",
                null,
                FlashlightBatteryItemId(),
                suppressRuntimeManagedReplacement: true,
                position: new Vector3(1f, 1f, 0f));

            SceneManager.MoveGameObjectToScene(retainedPickup.gameObject, testScene);
            SceneManager.MoveGameObjectToScene(suppressedPickup.gameObject, testScene);

            Array collectedPickups = InvokeCollectSupportItemPickups(testScene);

            Assert.That(
                collectedPickups,
                Has.Length.EqualTo(1),
                "Scene migration collection should ignore authored fixed pickups that suppress runtime-managed replacement.");
            Assert.That(
                collectedPickups.GetValue(0),
                Is.SameAs(retainedPickup),
                "Scene migration collection should still retain regular support pickups for marker seeding and cleanup.");
        }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }

            if (testScene.IsValid() && testScene.isLoaded)
            {
                EditorSceneManager.CloseScene(testScene, true);
            }
        }
    }

    [Test]
    public void CollectSupportItemPickups_PrefabRootOverload_ExcludesSuppressedAuthoredFixedPickup()
    {
        GameObject prefabRoot = new("PrefabRoot");

        try
        {
            Component retainedPickup = CreatePickup(
                "RuntimeManagedGlassBottle",
                prefabRoot.transform,
                GlassBottleItemId(),
                suppressRuntimeManagedReplacement: false,
                position: new Vector3(2f, 1f, 0f));
            CreatePickup(
                "FixedStarterMedkit",
                prefabRoot.transform,
                MedkitItemId(),
                suppressRuntimeManagedReplacement: true,
                position: new Vector3(1f, 1f, 0f));

            Array collectedPickups = InvokeCollectSupportItemPickups(prefabRoot);

            Assert.That(
                collectedPickups,
                Has.Length.EqualTo(1),
                "Prefab migration collection should ignore authored fixed pickups that suppress runtime-managed replacement.");
            Assert.That(
                collectedPickups.GetValue(0),
                Is.SameAs(retainedPickup),
                "Prefab migration collection should still retain regular support pickups for marker seeding and cleanup.");
        }
        finally
        {
            Object.DestroyImmediate(prefabRoot);
        }
    }

    private static Component CreatePickup(
        string name,
        Transform parent,
        string itemId,
        bool suppressRuntimeManagedReplacement,
        Vector3 position)
    {
        GameObject pickupObject = new(name);

        if (parent != null)
        {
            pickupObject.transform.SetParent(parent, false);
        }

        pickupObject.transform.position = position;

        Component pickup = MainEscapeReflectionTestHelper.AddComponent(pickupObject, "PrototypeInventoryPickup");
        MainEscapeReflectionTestHelper.SetFieldValue(pickup, "itemId", itemId);
        MainEscapeReflectionTestHelper.SetFieldValue(pickup, "suppressRuntimeManagedPickupReplacement", suppressRuntimeManagedReplacement);
        return pickup;
    }

    private static Array InvokeCollectSupportItemPickups(Scene scene)
    {
        object value = InvokeMigrationToolMethod(
            "CollectSupportItemPickups",
            new[] { typeof(Scene) },
            new object[] { scene });

        Assert.That(value, Is.InstanceOf<Array>());
        Assert.That(value.GetType().GetElementType()?.Name, Is.EqualTo("PrototypeInventoryPickup"));
        return (Array)value;
    }

    private static Array InvokeCollectSupportItemPickups(GameObject prefabRoot)
    {
        object value = InvokeMigrationToolMethod(
            "CollectSupportItemPickups",
            new[] { typeof(GameObject) },
            new object[] { prefabRoot });

        Assert.That(value, Is.InstanceOf<Array>());
        Assert.That(value.GetType().GetElementType()?.Name, Is.EqualTo("PrototypeInventoryPickup"));
        return (Array)value;
    }

    private static object InvokeMigrationToolMethod(string methodName, Type[] parameterTypes, object[] arguments)
    {
        Type toolType = MainEscapeReflectionTestHelper.RequireType("RPlacementMarkerMigrationTools");

        MethodInfo method = toolType.GetMethod(methodName, StaticMemberFlags, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"RPlacementMarkerMigrationTools.{methodName} overload is missing.");
        return method.Invoke(null, arguments);
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
}

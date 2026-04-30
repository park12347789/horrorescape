using System;
using System.Collections.Generic;

using NUnit.Framework;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

public sealed class MainEscapeAuthoredPickupContractEditModeTests
{
    private const string StartFloorScenePath = "Assets/Scenes/RMainScene_5F.unity";
    private const string ItemPlacementMarkersRootName = "ItemPlacementMarkers";
    private const string RuntimePickupRootName = "00_Pickups";

    [Test]
    public void FiveF_FixedAuthoredStarterPickups_ArePrototypeInventoryPickups_NotRandomMarkerSupportPlacements()
    {
        OpenStartFloorScene();
        List<Component> starterPickups = FindStarterSupportPickups();

        Assert.That(
            starterPickups,
            Has.Count.EqualTo(7),
            "5F fixed authored starter pickup contract should be seven PrototypeInventoryPickup instances, not random marker support placements.");

        StarterPickupExpectation[] expectations = StarterPickupExpectations();

        for (int index = 0; index < expectations.Length; index++)
        {
            StarterPickupExpectation expectation = expectations[index];
            int actualCount = CountByItemId(starterPickups, expectation.ItemId);

            Assert.That(
                actualCount,
                Is.EqualTo(expectation.ExpectedCount),
                $"5F fixed authored starter pickup contract should include {expectation.ExpectedCount} {expectation.Label} PrototypeInventoryPickup instances; do not satisfy this with random marker support placement.");
        }
    }

    [Test]
    public void FiveF_FixedAuthoredStarterPickups_SuppressRuntimeReplacement_AndStayAlwaysVisible()
    {
        OpenStartFloorScene();
        List<Component> starterPickups = FindStarterSupportPickups();

        Assert.That(
            starterPickups,
            Has.Count.EqualTo(7),
            "5F fixed authored starter pickup contract should stay on explicit PrototypeInventoryPickup objects before checking replacement and visibility flags.");

        for (int index = 0; index < starterPickups.Count; index++)
        {
            Component pickup = starterPickups[index];
            string pickupPath = GetHierarchyPath(pickup.transform);

            Assert.That(
                MainEscapeReflectionTestHelper.GetPropertyValue<bool>(pickup, "SuppressRuntimeManagedPickupReplacement"),
                Is.True,
                $"{pickupPath} must keep suppressRuntimeManagedPickupReplacement=true so the fixed authored starter pickup is not absorbed by marker-driven runtime support placement.");
            Assert.That(
                ReadSerializedBool(pickup, "alwaysVisibleInAuthoredScene"),
                Is.True,
                $"{pickupPath} must keep alwaysVisibleInAuthoredScene=true so the fixed authored starter pickup remains visible in the authored 5F scene.");
        }
    }

    [Test]
    public void FiveF_FixedAuthoredStarterPickups_AreNotChildrenOfRandomMarkerRoots_OrRuntimePickupRoot()
    {
        OpenStartFloorScene();
        Scene scene = SceneManager.GetSceneByPath(StartFloorScenePath);
        Transform itemPlacementMarkersRoot = FindSceneTransform(scene, ItemPlacementMarkersRootName);
        Transform runtimePickupRoot = FindSceneTransform(scene, RuntimePickupRootName);
        List<Component> starterPickups = FindStarterSupportPickups();
        Type placementMarkerType = MainEscapeReflectionTestHelper.RequireType("MainEscapeItemPlacementMarker");

        Assert.That(itemPlacementMarkersRoot, Is.Not.Null, $"{StartFloorScenePath} should expose {ItemPlacementMarkersRootName} for the random marker support placement layer.");
        Assert.That(runtimePickupRoot, Is.Not.Null, $"{StartFloorScenePath} should expose {RuntimePickupRootName} for runtime-managed pickups.");

        for (int index = 0; index < starterPickups.Count; index++)
        {
            Component pickup = starterPickups[index];
            string pickupPath = GetHierarchyPath(pickup.transform);

            Assert.That(
                pickup.transform.IsChildOf(itemPlacementMarkersRoot),
                Is.False,
                $"{pickupPath} is part of the fixed authored 5F starter pickup contract and must not be represented as a random marker support placement under {ItemPlacementMarkersRootName}.");
            Assert.That(
                pickup.transform.IsChildOf(runtimePickupRoot),
                Is.False,
                $"{pickupPath} is part of the fixed authored 5F starter pickup contract and must not be absorbed into the {RuntimePickupRootName} runtime root.");
            Assert.That(
                pickup.GetComponent(placementMarkerType),
                Is.Null,
                $"{pickupPath} must remain a PrototypeInventoryPickup, not a MainEscapeItemPlacementMarker support placement.");
        }
    }

    private static void OpenStartFloorScene()
    {
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(StartFloorScenePath),
            Is.Not.Null,
            $"Missing start floor scene asset at '{StartFloorScenePath}'.");

        EditorSceneManager.OpenScene(StartFloorScenePath, OpenSceneMode.Single);
    }

    private static List<Component> FindStarterSupportPickups()
    {
        Type pickupType = MainEscapeReflectionTestHelper.RequireType("PrototypeInventoryPickup");
        Object[] pickups = Object.FindObjectsByType(
            pickupType,
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<Component> starterPickups = new();

        for (int index = 0; index < pickups.Length; index++)
        {
            Component pickup = pickups[index] as Component;

            if (pickup == null
                || pickup.gameObject.scene.path != StartFloorScenePath
                || !IsStarterSupportItem(MainEscapeReflectionTestHelper.GetPropertyValue<string>(pickup, "ItemId")))
            {
                continue;
            }

            starterPickups.Add(pickup);
        }

        return starterPickups;
    }

    private static bool IsStarterSupportItem(string itemId)
    {
        return string.Equals(itemId, GlassBottleItemId(), StringComparison.Ordinal)
            || string.Equals(itemId, FlashlightBatteryItemId(), StringComparison.Ordinal)
            || string.Equals(itemId, MedkitItemId(), StringComparison.Ordinal);
    }

    private static int CountByItemId(List<Component> pickups, string itemId)
    {
        int count = 0;

        for (int index = 0; index < pickups.Count; index++)
        {
            Component pickup = pickups[index];
            string pickupItemId = pickup != null
                ? MainEscapeReflectionTestHelper.GetPropertyValue<string>(pickup, "ItemId")
                : string.Empty;

            if (pickup != null && string.Equals(pickupItemId, itemId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static StarterPickupExpectation[] StarterPickupExpectations()
    {
        return new[]
        {
            new StarterPickupExpectation(GlassBottleItemId(), "glass bottle", 3),
            new StarterPickupExpectation(FlashlightBatteryItemId(), "flashlight battery", 2),
            new StarterPickupExpectation(MedkitItemId(), "medkit", 2)
        };
    }

    private static string GlassBottleItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("GlassBottleItemId");
    }

    private static string FlashlightBatteryItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("FlashlightBatteryItemId");
    }

    private static string MedkitItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("MedkitItemId");
    }

    private static bool ReadSerializedBool(UnityEngine.Object owner, string propertyName)
    {
        SerializedObject serializedObject = new(owner);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        Assert.That(property, Is.Not.Null, $"{owner.name} is missing serialized bool property '{propertyName}'.");
        return property.boolValue;
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

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<missing transform>";
        }

        Stack<string> names = new();
        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private readonly struct StarterPickupExpectation
    {
        public StarterPickupExpectation(string itemId, string label, int expectedCount)
        {
            ItemId = itemId;
            Label = label;
            ExpectedCount = expectedCount;
        }

        public string ItemId { get; }
        public string Label { get; }
        public int ExpectedCount { get; }
    }
}

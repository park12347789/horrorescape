using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class RRunFloorStateApplierSupportPickupEditModeTests
{
    [Test]
    public void ConfigureAuthoredSupportPickups_DisablesOnlyRuntimeManagedItemId()
    {
        GameObject directorObject = new("FloorDirector");
        GameObject batteryObject = new("BatteryLegacyPickup");
        GameObject bottleObject = new("BottleLegacyPickup");

        try
        {
            Component floorDirector = MainEscapeReflectionTestHelper.AddComponent(directorObject, "RFloorDirector");
            AddRuntimeManagedPickupItemId(floorDirector, FlashlightBatteryItemId());
            Component batteryPickup = MainEscapeReflectionTestHelper.AddComponent(batteryObject, "PrototypeInventoryPickup");
            Component bottlePickup = MainEscapeReflectionTestHelper.AddComponent(bottleObject, "PrototypeInventoryPickup");
            batteryPickup.gameObject.SetActive(true);
            bottlePickup.gameObject.SetActive(true);

            object context = CreateContext(floorDirector, batteryPickup, bottlePickup, null);

            InvokeConfigureAuthoredSupportPickups(context);

            Assert.That(
                batteryPickup.gameObject.activeSelf,
                Is.False,
                "Only the battery legacy pickup should be disabled when the floor director reports battery as runtime-managed.");
            Assert.That(
                bottlePickup.gameObject.activeSelf,
                Is.True,
                "A bottle legacy pickup must remain configurable when only battery is runtime-managed.");
            Assert.That(
                MainEscapeReflectionTestHelper.GetPropertyValue<string>(bottlePickup, "ItemId"),
                Is.EqualTo(GlassBottleItemId()));
        }
        finally
        {
            UnityObject.DestroyImmediate(bottleObject);
            UnityObject.DestroyImmediate(batteryObject);
            UnityObject.DestroyImmediate(directorObject);
        }
    }

    [Test]
    public void ConfigureAuthoredSupportPickups_PreservesSuppressedRuntimeManagedPickup()
    {
        GameObject directorObject = new("FloorDirector");
        GameObject suppressedObject = new("SuppressedSupportPickup");

        try
        {
            Component floorDirector = MainEscapeReflectionTestHelper.AddComponent(directorObject, "RFloorDirector");
            AddRuntimeManagedPickupItemId(floorDirector, FlashlightBatteryItemId());
            Component suppressedPickup = MainEscapeReflectionTestHelper.AddComponent(suppressedObject, "PrototypeInventoryPickup");
            SetSuppressRuntimeManagedPickupReplacement(suppressedPickup, true);
            suppressedPickup.gameObject.SetActive(true);

            object context = CreateContext(floorDirector, suppressedPickup, null, null);

            InvokeConfigureAuthoredSupportPickups(context);

            Assert.That(
                suppressedPickup.gameObject.activeSelf,
                Is.True,
                "A fixed authored support pickup that suppresses runtime-managed replacement must stay active even if it is accidentally wired to a runtime-managed legacy support pickup slot.");
        }
        finally
        {
            UnityObject.DestroyImmediate(suppressedObject);
            UnityObject.DestroyImmediate(directorObject);
        }
    }

    private static object CreateContext(
        Component floorDirector,
        Component batteryPickup,
        Component bottlePickup,
        Component medkitPickup)
    {
        return Activator.CreateInstance(
            MainEscapeReflectionTestHelper.RequireType("RRunFloorStateContext"),
            null,
            null,
            5,
            false,
            false,
            true,
            null,
            null,
            null,
            floorDirector,
            null,
            null,
            null,
            batteryPickup,
            bottlePickup,
            medkitPickup,
            null,
            null);
    }

    private static void InvokeConfigureAuthoredSupportPickups(object context)
    {
        MethodInfo method = MainEscapeReflectionTestHelper.RequireType("RRunFloorStateApplier").GetMethod(
            "ConfigureAuthoredSupportPickups",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, "RRunFloorStateApplier is missing authored support pickup configuration logic.");
        method.Invoke(null, new object[] { context });
    }

    private static void AddRuntimeManagedPickupItemId(Component floorDirector, string itemId)
    {
        object value = MainEscapeReflectionTestHelper.GetFieldValue(floorDirector, "runtimeManagedPickupItemIds");
        Assert.That(value, Is.InstanceOf<System.Collections.Generic.HashSet<string>>());
        ((System.Collections.Generic.HashSet<string>)value).Add(itemId);
    }

    private static void SetSuppressRuntimeManagedPickupReplacement(Component pickup, bool suppress)
    {
        MainEscapeReflectionTestHelper.SetFieldValue(pickup, "suppressRuntimeManagedPickupReplacement", suppress);
    }

    private static string FlashlightBatteryItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("FlashlightBatteryItemId");
    }

    private static string GlassBottleItemId()
    {
        return MainEscapeReflectionTestHelper.CatalogItemId("GlassBottleItemId");
    }
}

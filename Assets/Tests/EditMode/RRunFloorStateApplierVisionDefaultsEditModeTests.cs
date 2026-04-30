using System;

using NUnit.Framework;

using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class RRunFloorStateApplierVisionDefaultsEditModeTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void ApplyFloorVisionDefaults_UsesSharedComfortRevealScaleAcrossMainFloors(int floorNumber)
    {
        GameObject playerObject = new("Player");

        try
        {
            Component playerController = MainEscapeReflectionTestHelper.AddComponent(playerObject, "WasdPlayerController");

            Assert.That(
                TryGetFloorByNumber(floorNumber, out object floor),
                Is.True,
                $"{floorNumber}F floor definition should exist.");

            object context = Activator.CreateInstance(
                MainEscapeReflectionTestHelper.RequireType("RRunFloorStateContext"),
                null,
                floor,
                floorNumber,
                false,
                false,
                false,
                playerController,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            InvokeApply(context);

            Assert.That(
                MainEscapeReflectionTestHelper.GetPropertyValue<float>(playerController, "FlashlightOffComfortRevealScale"),
                Is.EqualTo(2f),
                $"{floorNumber}F should keep the expanded comfort reveal radius when the flashlight is off.");
            Assert.That(
                MainEscapeReflectionTestHelper.GetPropertyValue<float>(playerController, "FlashlightOnComfortRevealScale"),
                Is.EqualTo(2f),
                $"{floorNumber}F should keep the expanded comfort reveal radius when the flashlight is on.");
        }
        finally
        {
            UnityObject.DestroyImmediate(playerObject);
        }
    }

    private static bool TryGetFloorByNumber(int floorNumber, out object floor)
    {
        Type catalogType = MainEscapeReflectionTestHelper.RequireType("MainEscapeFloorCatalog");
        Type floorType = MainEscapeReflectionTestHelper.RequireType("EscapeFloorDefinition");
        Type byRefFloorType = floorType.MakeByRefType();
        floor = null;

        System.Reflection.MethodInfo method = catalogType.GetMethod(
            "TryGetFloorByNumber",
            MainEscapeReflectionTestHelper.StaticMemberFlags,
            null,
            new[] { typeof(int), byRefFloorType },
            null);

        Assert.That(method, Is.Not.Null, "MainEscapeFloorCatalog.TryGetFloorByNumber is missing.");

        object[] arguments = { floorNumber, null };
        bool resolved = method.Invoke(null, arguments) is bool result && result;
        floor = arguments[1];
        return resolved;
    }

    private static void InvokeApply(object context)
    {
        System.Reflection.MethodInfo method = MainEscapeReflectionTestHelper.RequireType("RRunFloorStateApplier").GetMethod(
            "Apply",
            MainEscapeReflectionTestHelper.StaticMemberFlags);

        Assert.That(method, Is.Not.Null, "RRunFloorStateApplier.Apply is missing.");
        _ = method.Invoke(null, new[] { context });
    }
}

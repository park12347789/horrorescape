using NUnit.Framework;
using UnityEngine;
using System;
using System.Reflection;

public sealed class PlayerInventoryStackLimitEditModeTests
{
    [Test]
    public void AddItem_CapsMedkitsAndBatteriesAtThree()
    {
        GameObject owner = new("PlayerInventoryStackLimit_Test");

        try
        {
            Component inventory = MainEscapeReflectionTestHelper.AddComponent(owner, "PlayerInventory");
            string medkitItemId = MainEscapeReflectionTestHelper.CatalogItemId("MedkitItemId");
            string batteryItemId = MainEscapeReflectionTestHelper.CatalogItemId("FlashlightBatteryItemId");

            Assert.That(AddItem(inventory, medkitItemId, "Medkit", 1), Is.True);
            Assert.That(AddItem(inventory, medkitItemId, "Medkit", 1), Is.True);
            Assert.That(AddItem(inventory, medkitItemId, "Medkit", 1), Is.True);
            Assert.That(AddItem(inventory, medkitItemId, "Medkit", 1), Is.False);
            Assert.That(GetQuantity(inventory, medkitItemId), Is.EqualTo(3));

            Assert.That(AddItem(inventory, batteryItemId, "Flashlight Battery", 5), Is.True);
            Assert.That(GetQuantity(inventory, batteryItemId), Is.EqualTo(3));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void AddItem_AllowsUncappedPrototypeItemsPastThree()
    {
        GameObject owner = new("PlayerInventoryUncappedItem_Test");

        try
        {
            Component inventory = MainEscapeReflectionTestHelper.AddComponent(owner, "PlayerInventory");
            string glassBottleItemId = MainEscapeReflectionTestHelper.CatalogItemId("GlassBottleItemId");

            Assert.That(AddItem(inventory, glassBottleItemId, "Glass Bottle", 5), Is.True);

            Assert.That(GetQuantity(inventory, glassBottleItemId), Is.EqualTo(5));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void SetItems_ClampsLimitedStacksFromSerializedState()
    {
        GameObject owner = new("PlayerInventorySetItemsLimit_Test");

        try
        {
            Component inventory = MainEscapeReflectionTestHelper.AddComponent(owner, "PlayerInventory");
            string medkitItemId = MainEscapeReflectionTestHelper.CatalogItemId("MedkitItemId");

            SetItems(inventory, medkitItemId, "Medkit", 9);

            Assert.That(GetQuantity(inventory, medkitItemId), Is.EqualTo(3));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static bool AddItem(Component inventory, string itemId, string displayName, int quantity)
    {
        MethodInfo method = inventory.GetType().GetMethod("AddItem", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "PlayerInventory.AddItem is missing.");
        return method.Invoke(inventory, new object[] { itemId, displayName, quantity }) is bool added && added;
    }

    private static int GetQuantity(Component inventory, string itemId)
    {
        MethodInfo method = inventory.GetType().GetMethod("GetQuantity", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "PlayerInventory.GetQuantity is missing.");
        return method.Invoke(inventory, new object[] { itemId }) is int quantity ? quantity : 0;
    }

    private static void SetItems(Component inventory, string itemId, string displayName, int quantity)
    {
        Type stackType = inventory.GetType().GetNestedType("ItemStack", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(stackType, Is.Not.Null, "PlayerInventory.ItemStack is missing.");

        object stack = Activator.CreateInstance(stackType);
        SetField(stack, "itemId", itemId);
        SetField(stack, "displayName", displayName);
        SetField(stack, "quantity", quantity);

        Array stacks = Array.CreateInstance(stackType, 1);
        stacks.SetValue(stack, 0);

        MethodInfo method = inventory.GetType().GetMethod("SetItems", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "PlayerInventory.SetItems is missing.");
        method.Invoke(inventory, new object[] { stacks });
    }

    private static void SetField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{fieldName} is missing.");
        field.SetValue(owner, value);
    }
}

using System.Reflection;

using NUnit.Framework;
using UnityEngine;

public sealed class PlayerQuickItemThrowableCooldownEditModeTests
{
    [Test]
    public void TryThrowEquippedItem_BlocksRepeatedThrowableUseDuringCooldown()
    {
        GameObject player = new("PlayerQuickItemThrowableCooldown_Test");
        player.SetActive(false);

        try
        {
            Collider2D playerCollider = player.AddComponent<BoxCollider2D>();
            Component playerController = MainEscapeReflectionTestHelper.AddComponent(player, "WasdPlayerController");
            Component inventory = MainEscapeReflectionTestHelper.AddComponent(player, "PlayerInventory");
            Component quickItems = MainEscapeReflectionTestHelper.AddComponent(player, "PlayerQuickItemController");
            string glassBottleItemId = MainEscapeReflectionTestHelper.CatalogItemId("GlassBottleItemId");

            AddItem(inventory, glassBottleItemId, "Glass Bottle", 2);
            MainEscapeReflectionTestHelper.SetFieldValue(quickItems, "playerController", playerController);
            MainEscapeReflectionTestHelper.SetFieldValue(quickItems, "inventory", inventory);
            MainEscapeReflectionTestHelper.SetFieldValue(quickItems, "playerCollider", playerCollider);
            MainEscapeReflectionTestHelper.SetFieldValue(quickItems, "equippedThrowableItemId", glassBottleItemId);

            InvokeTryThrowEquippedItem(quickItems);
            InvokeTryThrowEquippedItem(quickItems);

            Assert.That(GetQuantity(inventory, glassBottleItemId), Is.EqualTo(1));
            Assert.That(MainEscapeReflectionTestHelper.GetPropertyValue<bool>(quickItems, "IsThrowableUseOnCooldown"), Is.True);
        }
        finally
        {
            DestroySpawnedBottles();
            Object.DestroyImmediate(player);
        }
    }

    private static void InvokeTryThrowEquippedItem(Component quickItems)
    {
        MethodInfo method = quickItems.GetType().GetMethod(
            "TryThrowEquippedItem",
            MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "PlayerQuickItemController.TryThrowEquippedItem is missing.");
        method.Invoke(quickItems, null);
    }

    private static void AddItem(Component inventory, string itemId, string displayName, int quantity)
    {
        MethodInfo method = inventory.GetType().GetMethod("AddItem", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "PlayerInventory.AddItem is missing.");
        bool added = method.Invoke(inventory, new object[] { itemId, displayName, quantity }) is bool result && result;
        Assert.That(added, Is.True);
    }

    private static int GetQuantity(Component inventory, string itemId)
    {
        MethodInfo method = inventory.GetType().GetMethod("GetQuantity", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, "PlayerInventory.GetQuantity is missing.");
        return method.Invoke(inventory, new object[] { itemId }) is int quantity ? quantity : 0;
    }

    private static void DestroySpawnedBottles()
    {
        System.Type projectileType = MainEscapeReflectionTestHelper.RequireType("ThrowableBottleProjectile");
        Object[] bottles = Object.FindObjectsByType(projectileType, FindObjectsSortMode.None);

        for (int index = 0; index < bottles.Length; index++)
        {
            if (bottles[index] is Component component)
            {
                Object.DestroyImmediate(component.gameObject);
                continue;
            }

            if (bottles[index] != null)
            {
                Object.DestroyImmediate(bottles[index]);
            }
        }
    }
}

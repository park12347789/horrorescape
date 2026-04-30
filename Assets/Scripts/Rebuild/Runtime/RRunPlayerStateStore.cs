using System;

using UnityEngine;

internal static class RFlashlightStateOwnerUtility
{
    public static void CaptureState(
        RPlayerRuntimeReferences runtime,
        float previousChargeNormalized,
        bool previousFlashlightEnabled,
        out float chargeNormalized,
        out bool flashlightEnabled)
    {
        chargeNormalized = previousChargeNormalized;
        flashlightEnabled = previousFlashlightEnabled;

        if (runtime == null)
        {
            return;
        }

        IFlashlightStateReadModel flashlightState = runtime.FlashlightState;

        if (flashlightState != null)
        {
            chargeNormalized = flashlightState.ChargeNormalized;
            flashlightEnabled = flashlightState.IsFlashlightEnabled;
            return;
        }

        if (runtime.FlashlightBattery != null)
        {
            chargeNormalized = runtime.FlashlightBattery.ChargeNormalized;
        }

        if (runtime.FlashlightEquipment != null)
        {
            flashlightEnabled = runtime.FlashlightEquipment.IsFlashlightEnabled;
        }
    }

    public static bool TryRestoreState(RPlayerRuntimeReferences runtime, float chargeNormalized, bool flashlightEnabled)
    {
        if (runtime == null)
        {
            return false;
        }

        if (runtime.FlashlightStateOwner != null)
        {
            runtime.FlashlightStateOwner.SetChargeNormalized(chargeNormalized);
            runtime.FlashlightStateOwner.SetFlashlightEnabledState(flashlightEnabled);
            return true;
        }

        bool usedLegacyFacade = false;

        if (runtime.FlashlightBattery != null)
        {
            runtime.FlashlightBattery.SetChargeNormalized(chargeNormalized);
            usedLegacyFacade = true;
        }

        if (runtime.FlashlightEquipment != null)
        {
            runtime.FlashlightEquipment.SetFlashlightEnabledState(flashlightEnabled);
            usedLegacyFacade = true;
        }

        return usedLegacyFacade;
    }

    public static bool TryApplyConfiguredCharge(RPlayerRuntimeReferences runtime, float chargeNormalized)
    {
        if (runtime == null)
        {
            return false;
        }

        if (runtime.FlashlightStateOwner != null)
        {
            runtime.FlashlightStateOwner.SetChargeNormalized(chargeNormalized);
            return true;
        }

        if (runtime.FlashlightBattery != null)
        {
            runtime.FlashlightBattery.SetChargeNormalized(chargeNormalized);
            return true;
        }

        return false;
    }

    public static bool TryApplyConfiguredEnabledState(RPlayerRuntimeReferences runtime, bool enabled)
    {
        if (runtime == null)
        {
            return false;
        }

        FlashlightStateOwner flashlightStateOwner = runtime.FlashlightStateOwner;

        if (flashlightStateOwner != null)
        {
            return flashlightStateOwner.HasFlashlight
                ? flashlightStateOwner.SetFlashlightEnabledState(enabled)
                : flashlightStateOwner.EnsureFlashlightEquipped(enabled);
        }

        PlayerFlashlightEquipment flashlightEquipment = runtime.FlashlightEquipment;

        if (flashlightEquipment == null)
        {
            return false;
        }

        return flashlightEquipment.HasFlashlight
            ? flashlightEquipment.SetFlashlightEnabledState(enabled)
            : flashlightEquipment.EnsureFlashlightEquipped(enabled);
    }
}

internal static class RRunPlayerStateStoreUtility
{
    public static RRunPlayerStateSnapshot CreateDefault()
    {
        return RRunPlayerStateSnapshot.CreateDefault();
    }

    public static RRunPlayerStateSnapshot Capture(
        RPlayerRuntimeReferences runtime,
        RRunPlayerStateSnapshot previousSnapshot = null)
    {
        RRunPlayerStateSnapshot previousState = previousSnapshot ?? CreateDefault();

        if (runtime == null)
        {
            return previousState.Clone();
        }

        PlayerInventory inventory = runtime.Inventory;
        RFlashlightStateOwnerUtility.CaptureState(
            runtime,
            previousState.FlashlightChargeNormalized,
            previousState.FlashlightEnabled,
            out float chargeNormalized,
            out bool flashlightEnabled);

        return RRunPlayerStateSnapshot.Create(
            hasState: true,
            playerHealth: runtime.PlayerHealth != null
                ? runtime.PlayerHealth.CurrentHealth
                : previousState.PlayerHealth,
            flashlightChargeNormalized: chargeNormalized,
            flashlightEnabled: flashlightEnabled,
            inventoryItems: inventory != null
                ? CaptureInventoryItems(inventory)
                : CloneInventoryItems(previousState.InventoryItems),
            cloneInventoryItems: false);
    }

    public static bool TryRestore(
        RPlayerRuntimeReferences runtime,
        RRunPlayerStateSnapshot primarySnapshot,
        RRunPlayerStateSnapshot fallbackSnapshot = null)
    {
        if (runtime == null)
        {
            return false;
        }

        RRunPlayerStateSnapshot primaryState = primarySnapshot ?? CreateDefault();
        RRunPlayerStateSnapshot fallbackState = fallbackSnapshot ?? CreateDefault();
        bool shouldUseFallback = !primaryState.HasState && fallbackState.HasState;

        if (!primaryState.HasState && !shouldUseFallback)
        {
            return false;
        }

        RRunPlayerStateSnapshot resolvedState = shouldUseFallback ? fallbackState : primaryState;
        RestoreInventory(runtime.Inventory, resolvedState.InventoryItems);
        runtime.PlayerHealth?.SetCurrentHealth(resolvedState.PlayerHealth);
        RestoreFlashlightState(runtime, resolvedState);
        return true;
    }

    private static void RestoreInventory(PlayerInventory inventory, RRunSavedInventoryItem[] savedInventoryItems)
    {
        if (inventory == null)
        {
            return;
        }

        RRunSavedInventoryItem[] inventoryItems = savedInventoryItems ?? Array.Empty<RRunSavedInventoryItem>();
        PlayerInventory.ItemStack[] restoredItems = new PlayerInventory.ItemStack[inventoryItems.Length];

        for (int index = 0; index < inventoryItems.Length; index++)
        {
            RRunSavedInventoryItem item = inventoryItems[index];
            restoredItems[index] = new PlayerInventory.ItemStack
            {
                itemId = item.itemId,
                displayName = item.displayName,
                quantity = item.quantity
            };
        }

        inventory.SetItems(restoredItems);
    }

    private static void RestoreFlashlightState(RPlayerRuntimeReferences runtime, RRunPlayerStateSnapshot state)
    {
        if (runtime == null)
        {
            return;
        }

        RFlashlightStateOwnerUtility.TryRestoreState(
            runtime,
            state.FlashlightChargeNormalized,
            state.FlashlightEnabled);
    }

    private static RRunSavedInventoryItem[] CaptureInventoryItems(PlayerInventory inventory)
    {
        RRunSavedInventoryItem[] capturedItems = new RRunSavedInventoryItem[inventory.Items.Count];

        for (int index = 0; index < inventory.Items.Count; index++)
        {
            PlayerInventory.ItemStack item = inventory.Items[index];
            capturedItems[index] = new RRunSavedInventoryItem
            {
                itemId = item.itemId,
                displayName = item.displayName,
                quantity = item.quantity
            };
        }

        return capturedItems;
    }

    private static RRunSavedInventoryItem[] CloneInventoryItems(RRunSavedInventoryItem[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<RRunSavedInventoryItem>();
        }

        RRunSavedInventoryItem[] clone = new RRunSavedInventoryItem[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }
}

[DisallowMultipleComponent]
public sealed class RRunPlayerStateStore : MonoBehaviour, IRunPlayerStateStore
{
    public RRunPlayerStateSnapshot CreateDefault()
    {
        return RRunPlayerStateStoreUtility.CreateDefault();
    }

    public RRunPlayerStateSnapshot Capture(RPlayerRuntimeReferences runtime, RRunPlayerStateSnapshot previousSnapshot = null)
    {
        return RRunPlayerStateStoreUtility.Capture(runtime, previousSnapshot);
    }

    public bool TryRestore(
        RPlayerRuntimeReferences runtime,
        RRunPlayerStateSnapshot primarySnapshot,
        RRunPlayerStateSnapshot fallbackSnapshot = null)
    {
        return RRunPlayerStateStoreUtility.TryRestore(runtime, primarySnapshot, fallbackSnapshot);
    }
}

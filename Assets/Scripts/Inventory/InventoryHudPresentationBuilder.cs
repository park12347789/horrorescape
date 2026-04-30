using System;
using System.Collections.Generic;

using UnityEngine;

internal static class InventoryHudPresentationBuilder
{
    private const string ShortcutPromptPrefix = "I close";

    public static InventoryPanelPresentation Build(
        IUiSettingsReadModel uiSettings,
        PlayerInventory inventory,
        PlayerFlashlightBattery flashlightBattery,
        PlayerQuickItemController quickItems,
        int visibleSlotCount = -1)
    {
        IUiSettingsReadModel resolvedUiSettings = uiSettings
            ?? UiSettingsOwner.Resolve(quickItems)
            ?? UiSettingsOwner.Resolve(flashlightBattery)
            ?? UiSettingsOwner.Resolve(inventory);
        int slotCount = visibleSlotCount > 0
            ? visibleSlotCount
            : resolvedUiSettings.InventoryVisibleSlotCount;

        return new InventoryPanelPresentation(
            BuildBatterySummary(inventory, flashlightBattery),
            BuildShortcutSummaryFromReadModel(resolvedUiSettings, quickItems),
            BuildSlotViews(inventory, quickItems, slotCount));
    }

    public static InventoryPanelPresentation Build(
        PlayerInventory inventory,
        PlayerFlashlightBattery flashlightBattery,
        PlayerQuickItemController quickItems)
    {
        return Build(null, inventory, flashlightBattery, quickItems);
    }

    public static string BuildBatterySummary(PlayerInventory inventory, PlayerFlashlightBattery flashlightBattery)
    {
        int spareBatteries = flashlightBattery != null
            ? flashlightBattery.StoredBatteryCount
            : inventory != null
                ? inventory.GetQuantity(PrototypeItemCatalog.FlashlightBatteryItemId)
                : 0;

        return $"Cells x{spareBatteries}";
    }

    public static string BuildShortcutSummary(MainEscapeRuntimeSettings runtimeSettings, PlayerQuickItemController quickItems)
    {
        return BuildShortcutSummaryCore(null, quickItems, runtimeSettings);
    }

    private static string BuildShortcutSummaryFromReadModel(IUiSettingsReadModel uiSettings, PlayerQuickItemController quickItems)
    {
        return BuildShortcutSummaryCore(uiSettings, quickItems, null);
    }

    private static string BuildShortcutSummaryCore(
        IUiSettingsReadModel uiSettings,
        PlayerQuickItemController quickItems,
        MainEscapeRuntimeSettings runtimeSettings)
    {
        List<string> parts = new() { ShortcutPromptPrefix };
        IUiSettingsReadModel resolvedUiSettings = uiSettings
            ?? UiSettingsOwner.Resolve(quickItems);
        int minimumVisibleCount = resolvedUiSettings != null
            ? resolvedUiSettings.QuickSlotVisibleCount
            : runtimeSettings != null
                ? runtimeSettings.QuickSlotVisibleCount
                : 1;

        if (quickItems != null)
        {
            int visibleCount = Mathf.Max(quickItems.GetConfiguredSlotCount(), minimumVisibleCount);

            for (int index = 0; index < visibleCount; index++)
            {
                if (!quickItems.TryGetSlotViewAt(index, out PlayerQuickItemController.QuickSlotView slotView))
                {
                    continue;
                }

                if (slotView.Quantity <= 0)
                {
                    continue;
                }

                parts.Add($"{GetShortcutKeyLabel(slotView)} {GetShortcutLabel(slotView.DisplayName)}");
            }
        }

        return string.Join("  |  ", parts);
    }

    public static InventorySlotPresentation[] BuildSlotViews(
        PlayerInventory inventory,
        PlayerQuickItemController quickItems,
        int maxSlots)
    {
        List<InventorySlotPresentation> views = new(maxSlots);
        HashSet<string> representedItemIds = new(StringComparer.Ordinal);
        Dictionary<string, PlayerQuickItemController.QuickSlotView> quickSlotViewsByItemId = new(StringComparer.Ordinal);

        if (quickItems != null)
        {
            int quickSlotCount = quickItems.GetConfiguredSlotCount();

            for (int index = 0; index < quickSlotCount; index++)
            {
                if (!quickItems.TryGetSlotViewAt(index, out PlayerQuickItemController.QuickSlotView slotView))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(slotView.ItemId) || quickSlotViewsByItemId.ContainsKey(slotView.ItemId))
                {
                    continue;
                }

                quickSlotViewsByItemId.Add(slotView.ItemId, slotView);
            }
        }

        if (inventory != null)
        {
            for (int index = 0; index < inventory.Items.Count && views.Count < maxSlots; index++)
            {
                PlayerInventory.ItemStack stack = inventory.Items[index];

                if (stack == null
                    || stack.quantity <= 0
                    || string.IsNullOrWhiteSpace(stack.itemId)
                    || representedItemIds.Contains(stack.itemId))
                {
                    continue;
                }

                PrototypeItemCatalog.TryGetDefinition(stack.itemId, out PrototypeItemDefinition definition);
                bool hasQuickSlot = quickSlotViewsByItemId.TryGetValue(stack.itemId, out PlayerQuickItemController.QuickSlotView slotView);
                views.Add(new InventorySlotPresentation(
                    stack.itemId,
                    PrototypeItemCatalog.GetDisplayName(stack.itemId, stack.displayName),
                    stack.quantity,
                    hasQuickSlot ? slotView.SlotNumber : 0,
                    hasQuickSlot && slotView.Equipped,
                    hasQuickSlot,
                    definition.UseKind));
                representedItemIds.Add(stack.itemId);
            }
        }

        while (views.Count < maxSlots)
        {
            views.Add(default);
        }

        return views.ToArray();
    }

    private static string GetShortcutLabel(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Item";
        }

        if (string.Equals(displayName, "Flashlight Battery", StringComparison.OrdinalIgnoreCase))
        {
            return "Cells";
        }

        if (string.Equals(displayName, "Flashlight", StringComparison.OrdinalIgnoreCase))
        {
            return "Light";
        }

        if (string.Equals(displayName, "Glass Bottle", StringComparison.OrdinalIgnoreCase))
        {
            return "Bottle";
        }

        string trimmed = displayName.Trim();
        int firstSpace = trimmed.IndexOf(' ');
        string label = firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
        return label.Length <= 7 ? label : label[..7];
    }

    private static string GetShortcutKeyLabel(PlayerQuickItemController.QuickSlotView slotView)
    {
        return slotView.UseKind == PrototypeItemUseKind.Throwable ? "Space" : slotView.SlotNumber.ToString();
    }
}

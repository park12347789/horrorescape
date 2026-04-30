public readonly struct InventorySlotPresentation
{
    public InventorySlotPresentation(
        string itemId,
        string displayName,
        int quantity,
        int quickSlotNumber,
        bool equipped,
        bool configuredSlot,
        PrototypeItemUseKind useKind)
    {
        ItemId = itemId ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName;
        Quantity = quantity < 0 ? 0 : quantity;
        QuickSlotNumber = quickSlotNumber;
        Equipped = equipped;
        ConfiguredSlot = configuredSlot;
        UseKind = useKind;
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public int Quantity { get; }
    public int QuickSlotNumber { get; }
    public bool Equipped { get; }
    public bool ConfiguredSlot { get; }
    public PrototypeItemUseKind UseKind { get; }
}

public readonly struct QuickSlotPresentation
{
    public QuickSlotPresentation(
        int slotNumber,
        string itemId,
        string displayName,
        int quantity,
        bool equipped,
        bool configuredSlot,
        PrototypeItemUseKind useKind)
    {
        SlotNumber = slotNumber;
        ItemId = itemId ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName;
        Quantity = quantity < 0 ? 0 : quantity;
        Equipped = equipped;
        ConfiguredSlot = configuredSlot;
        UseKind = useKind;
    }

    public int SlotNumber { get; }
    public string ItemId { get; }
    public string DisplayName { get; }
    public int Quantity { get; }
    public bool Equipped { get; }
    public bool ConfiguredSlot { get; }
    public PrototypeItemUseKind UseKind { get; }
}

public readonly struct QuickSlotPanelPresentation
{
    public QuickSlotPanelPresentation(QuickSlotPresentation[] slots)
    {
        Slots = slots ?? System.Array.Empty<QuickSlotPresentation>();
    }

    public QuickSlotPresentation[] Slots { get; }
}

public readonly struct InventoryPanelPresentation
{
    public InventoryPanelPresentation(string batterySummary, string infoSummary, InventorySlotPresentation[] slots)
    {
        BatterySummary = batterySummary ?? string.Empty;
        InfoSummary = infoSummary ?? string.Empty;
        Slots = slots ?? System.Array.Empty<InventorySlotPresentation>();
    }

    public string BatterySummary { get; }
    public string InfoSummary { get; }
    public InventorySlotPresentation[] Slots { get; }
}

public readonly struct HealthPanelPresentation
{
    public HealthPanelPresentation(
        int currentHealth,
        int maxHealth,
        float healthNormalized,
        float recoveryNormalized,
        float flashlightChargeNormalized,
        int storedBatteryCount)
    {
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        HealthNormalized = healthNormalized;
        RecoveryNormalized = recoveryNormalized;
        FlashlightChargeNormalized = flashlightChargeNormalized;
        StoredBatteryCount = storedBatteryCount < 0 ? 0 : storedBatteryCount;
    }

    public int CurrentHealth { get; }
    public int MaxHealth { get; }
    public float HealthNormalized { get; }
    public float RecoveryNormalized { get; }
    public float FlashlightChargeNormalized { get; }
    public int StoredBatteryCount { get; }
}

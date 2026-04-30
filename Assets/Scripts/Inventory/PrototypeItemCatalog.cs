using System;

public enum PrototypeItemUseKind
{
    Passive,
    Instant,
    Throwable
}

public readonly struct PrototypeItemDefinition
{
    public PrototypeItemDefinition(
        string itemId,
        string displayName,
        PrototypeItemUseKind useKind,
        int healAmount = 0,
        float flashlightChargeAmount = 0f,
        float throwSpeed = 0f,
        float throwDistance = 0f,
        float throwNoiseRadius = 0f,
        float stunDurationMin = 0f,
        float stunDurationMax = 0f,
        int maxStackQuantity = 0)
    {
        ItemId = itemId ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName;
        UseKind = useKind;
        HealAmount = Math.Max(0, healAmount);
        FlashlightChargeAmount = Math.Max(0f, flashlightChargeAmount);
        ThrowSpeed = Math.Max(0f, throwSpeed);
        ThrowDistance = Math.Max(0f, throwDistance);
        ThrowNoiseRadius = Math.Max(0f, throwNoiseRadius);
        StunDurationMin = Math.Max(0f, stunDurationMin);
        StunDurationMax = Math.Max(StunDurationMin, stunDurationMax);
        MaxStackQuantity = Math.Max(0, maxStackQuantity);
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public PrototypeItemUseKind UseKind { get; }
    public int HealAmount { get; }
    public float FlashlightChargeAmount { get; }
    public float ThrowSpeed { get; }
    public float ThrowDistance { get; }
    public float ThrowNoiseRadius { get; }
    public float StunDurationMin { get; }
    public float StunDurationMax { get; }
    public int MaxStackQuantity { get; }
}

public static class PrototypeItemCatalog
{
    public const string IronGateKeyItemId = "prototype.objective.iron_gate_key";
    public const string FlashlightItemId = "prototype.tool.flashlight";
    public const string GlassBottleItemId = "prototype.throwable.glass_bottle";
    public const string MedkitItemId = "prototype.heal.medkit";
    public const string FlashlightBatteryItemId = "prototype.resource.flashlight_battery";

    public static bool TryGetDefinition(string itemId, out PrototypeItemDefinition definition)
    {
        switch (itemId)
        {
            case IronGateKeyItemId:
                definition = new PrototypeItemDefinition(
                    itemId,
                    "Iron Gate Key",
                    PrototypeItemUseKind.Passive);
                return true;
            case FlashlightItemId:
                definition = new PrototypeItemDefinition(
                    itemId,
                    "Flashlight",
                    PrototypeItemUseKind.Passive);
                return true;
            case GlassBottleItemId:
                definition = new PrototypeItemDefinition(
                    itemId,
                    "Glass Bottle",
                    PrototypeItemUseKind.Throwable,
                    throwSpeed: 11.5f,
                    throwDistance: 6.4f,
                    throwNoiseRadius: 6.8f,
                    stunDurationMin: 2f,
                    stunDurationMax: 2f);
                return true;
            case MedkitItemId:
                definition = new PrototypeItemDefinition(
                    itemId,
                    "Medkit",
                    PrototypeItemUseKind.Instant,
                    healAmount: 1,
                    maxStackQuantity: 3);
                return true;
            case FlashlightBatteryItemId:
                definition = new PrototypeItemDefinition(
                    itemId,
                    "Flashlight Battery",
                    PrototypeItemUseKind.Passive,
                    flashlightChargeAmount: 1f,
                    maxStackQuantity: 3);
                return true;
            default:
                definition = new PrototypeItemDefinition(itemId, itemId, PrototypeItemUseKind.Passive);
                return false;
        }
    }

    public static string GetDisplayName(string itemId, string fallback = null)
    {
        return TryGetDefinition(itemId, out PrototypeItemDefinition definition)
            ? definition.DisplayName
            : (string.IsNullOrWhiteSpace(fallback) ? itemId : fallback);
    }
}

using System;

public enum IRUiItemSemantic
{
    Unknown,
    Flashlight,
    GlassBottle,
    Medkit,
    FlashlightBattery
}

public static class IRUiItemSemanticUtility
{
    public static IRUiItemSemantic Resolve(string itemId)
    {
        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightItemId, StringComparison.Ordinal))
        {
            return IRUiItemSemantic.Flashlight;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal))
        {
            return IRUiItemSemantic.GlassBottle;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal))
        {
            return IRUiItemSemantic.Medkit;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal))
        {
            return IRUiItemSemantic.FlashlightBattery;
        }

        return IRUiItemSemantic.Unknown;
    }
}

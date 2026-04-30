using System;

using UnityEngine;

[Serializable]
public sealed class RRunPlayerStateSnapshot
{
    [SerializeField] private bool hasState;
    [SerializeField, Min(1)] private int playerHealth = 3;
    [SerializeField, Range(0f, 1f)] private float flashlightChargeNormalized = 1f;
    [SerializeField] private bool flashlightEnabled;
    [SerializeField] private RRunSavedInventoryItem[] inventoryItems = Array.Empty<RRunSavedInventoryItem>();

    public bool HasState => hasState;
    public int PlayerHealth => playerHealth;
    public float FlashlightChargeNormalized => flashlightChargeNormalized;
    public bool FlashlightEnabled => flashlightEnabled;
    public RRunSavedInventoryItem[] InventoryItems => inventoryItems ?? Array.Empty<RRunSavedInventoryItem>();

    public static RRunPlayerStateSnapshot CreateDefault()
    {
        return Create(
            hasState: false,
            playerHealth: 3,
            flashlightChargeNormalized: 1f,
            flashlightEnabled: false,
            inventoryItems: Array.Empty<RRunSavedInventoryItem>(),
            cloneInventoryItems: false);
    }

    public static RRunPlayerStateSnapshot FromLegacy(RRunSavedPlayerState legacyState)
    {
        return Create(
            legacyState.hasState,
            legacyState.playerHealth,
            legacyState.flashlightChargeNormalized,
            legacyState.flashlightEnabled,
            legacyState.inventoryItems);
    }

    public RRunSavedPlayerState ToLegacyState()
    {
        return new RRunSavedPlayerState
        {
            hasState = hasState,
            playerHealth = playerHealth,
            flashlightChargeNormalized = flashlightChargeNormalized,
            flashlightEnabled = flashlightEnabled,
            inventoryItems = CloneInventoryItems(inventoryItems)
        };
    }

    internal RRunPlayerStateSnapshot Clone()
    {
        return Create(
            hasState,
            playerHealth,
            flashlightChargeNormalized,
            flashlightEnabled,
            inventoryItems);
    }

    internal static RRunPlayerStateSnapshot Create(
        bool hasState,
        int playerHealth,
        float flashlightChargeNormalized,
        bool flashlightEnabled,
        RRunSavedInventoryItem[] inventoryItems,
        bool cloneInventoryItems = true)
    {
        return new RRunPlayerStateSnapshot
        {
            hasState = hasState,
            playerHealth = playerHealth,
            flashlightChargeNormalized = flashlightChargeNormalized,
            flashlightEnabled = flashlightEnabled,
            inventoryItems = cloneInventoryItems
                ? CloneInventoryItems(inventoryItems)
                : inventoryItems ?? Array.Empty<RRunSavedInventoryItem>()
        };
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

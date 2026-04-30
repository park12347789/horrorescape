using System;

using UnityEngine;

[CreateAssetMenu(
    fileName = "RRunPlayerDefaults",
    menuName = "Main Escape/Run Player Defaults")]
public sealed class RRunPlayerDefaults : ScriptableObject
{
    [Serializable]
    public struct StartingInventoryItem
    {
        public string itemId;
        public string displayName;
        public int quantity;
    }

    [SerializeField, Min(1)] private int defaultHealth = 3;
    [SerializeField, Range(0f, 1f)] private float defaultFlashlightChargeNormalized = 1f;
    [SerializeField] private bool flashlightEnabledByDefault = true;
    [SerializeField] private StartingInventoryItem[] startingItems = Array.Empty<StartingInventoryItem>();

    public int DefaultHealth => defaultHealth;
    public float DefaultFlashlightChargeNormalized => defaultFlashlightChargeNormalized;
    public bool FlashlightEnabledByDefault => flashlightEnabledByDefault;
    public StartingInventoryItem[] StartingItems => startingItems ?? Array.Empty<StartingInventoryItem>();
}

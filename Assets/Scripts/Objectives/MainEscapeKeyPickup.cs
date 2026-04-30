using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeKeyPickup : WorldInventoryPickupBase
{
    private static readonly Color DefaultKeyColor = new(1f, 0.82f, 0.24f, 1f);

    public void Configure(string configuredItemId, string displayName, int configuredQuantity = 1)
    {
        ConfigurePickupDefinition(
            string.IsNullOrWhiteSpace(configuredItemId)
                ? PrototypeItemCatalog.IronGateKeyItemId
                : configuredItemId,
            displayName,
            configuredQuantity,
            DefaultKeyColor);
    }

    protected override SpriteRenderer ResolveSpriteRenderer()
    {
        SpriteRenderer localRenderer = GetComponent<SpriteRenderer>();

        if (localRenderer != null)
        {
            return localRenderer;
        }

        return GetComponentInChildren<SpriteRenderer>(true);
    }
}

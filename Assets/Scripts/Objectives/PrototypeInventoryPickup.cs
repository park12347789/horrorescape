using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class PrototypeInventoryPickup : WorldInventoryPickupBase
{
    private static readonly Vector3 MainScenePickupScale = new(0.9f, 0.9f, 1f);
    [SerializeField] private bool alwaysVisibleInAuthoredScene;

    protected override bool ApplyMainSceneScale => true;
    protected override bool UseDiscoveryVisibility => !alwaysVisibleInAuthoredScene && base.UseDiscoveryVisibility;
    protected override Vector3 MainSceneScale => MainScenePickupScale;

    public void Configure(Vector3 worldPosition, string configuredItemId, string displayName, int configuredQuantity, Color baseColor)
    {
        ConfigurePickupDefinition(
            configuredItemId,
            displayName,
            configuredQuantity,
            baseColor,
            worldPosition,
            activate: true);
    }

    public void ConfigureAuthored(string configuredItemId, string displayName, int configuredQuantity, Color baseColor)
    {
        ConfigurePickupDefinition(
            configuredItemId,
            displayName,
            configuredQuantity,
            baseColor,
            activate: true);
    }
}

using UnityEngine;

public interface IUiSettingsReadModel
{
    Vector2 HudReferenceResolution { get; }
    float HudReferenceResolutionMatch { get; }
    Vector2 InventoryPanelSize { get; }
    Vector2 InventoryPanelMargin { get; }
    Vector2 InventorySlotSize { get; }
    Vector2 InventorySlotSpacing { get; }
    int InventorySlotColumnCount { get; }
    int InventorySlotRowCount { get; }
    int InventoryVisibleSlotCount { get; }
    Vector2 QuickSlotPanelSize { get; }
    Vector2 QuickSlotPanelMargin { get; }
    Vector2 QuickSlotCardSize { get; }
    Vector2 QuickSlotCardSpacing { get; }
    int QuickSlotVisibleCount { get; }
    Vector2 HealthPanelSize { get; }
    Vector2 HealthPanelMargin { get; }
    bool UseTemporaryAnalogNoiseUi { get; }
}

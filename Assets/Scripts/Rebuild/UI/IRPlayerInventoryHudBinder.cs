using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class IRPlayerInventoryHudBinder : MonoBehaviour, IRebuildHudBinder
{
    [SerializeField] private bool visible;
    [SerializeField] private IRHudCanvas hudCanvas;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;

    private PlayerInventory inventory;
    private PlayerFlashlightBattery flashlightBattery;
    private PlayerQuickItemController quickItems;
    private IRInventoryPanelView panelView;
    private PlayerInventory subscribedInventory;
    private PlayerFlashlightBattery subscribedFlashlightBattery;
    private PlayerQuickItemController subscribedQuickItems;
    private int lastRenderedStoredBatteryCount = int.MinValue;

    public bool Visible => visible;

    private void Awake()
    {
        CacheRebuildDependencies();
        ResolveRebuildPanelView();
        SubscribeRebuildDependencies();
        RefreshRebuildView();
    }

    private void OnValidate()
    {
        CacheRebuildDependencies();
        ResolveRebuildPanelView();
    }

    private void Update()
    {
        if (hudCanvas != null && hudCanvas.SuppressLegacyGameplayPanels)
        {
            hudCanvas.ApplyLegacyGameplayPanelSuppression();
            return;
        }

        if (Keyboard.current != null && Keyboard.current[Key.I].wasPressedThisFrame)
        {
            visible = !visible;
            RefreshRebuildView();
        }
    }

    private void OnEnable()
    {
        SubscribeRebuildDependencies();
        RefreshRebuildView();
    }

    private void OnDisable()
    {
        UnsubscribeRebuildDependencies();
        panelView?.SetVisible(false);
    }

    private void OnDestroy()
    {
        UnsubscribeRebuildDependencies();
    }

    public void SetVisible(bool enabled)
    {
        visible = enabled;
        RefreshRebuildView();
    }

    public void BindHudCanvas(IRHudCanvas canvas)
    {
        hudCanvas = canvas;
        ResolveRebuildPanelView();
        RefreshRebuildView();
    }

    public void BindPlayerRuntime(RPlayerRuntimeReferences runtime)
    {
        UnsubscribeRebuildDependencies();
        playerRuntime = runtime;
        CacheRebuildDependencies();
        SubscribeRebuildDependencies();
        RefreshRebuildView();
    }

    private void CacheRebuildDependencies()
    {
        RPlayerRuntimeReferences runtime = playerRuntime != null ? playerRuntime : GetComponent<RPlayerRuntimeReferences>();
        inventory = runtime != null ? runtime.Inventory : GetComponent<PlayerInventory>();
        flashlightBattery = runtime != null ? runtime.FlashlightBattery : GetComponent<PlayerFlashlightBattery>();
        quickItems = runtime != null ? runtime.QuickItems : GetComponent<PlayerQuickItemController>();
    }

    private void ResolveRebuildPanelView()
    {
        panelView = hudCanvas != null ? hudCanvas.InventoryPanel : null;
    }

    private void SubscribeRebuildDependencies()
    {
        if (inventory != null && subscribedInventory != inventory)
        {
            inventory.Changed += HandleRebuildPresentationChanged;
            subscribedInventory = inventory;
        }

        if (flashlightBattery != null && subscribedFlashlightBattery != flashlightBattery)
        {
            flashlightBattery.Changed += HandleBatteryPresentationChanged;
            subscribedFlashlightBattery = flashlightBattery;
        }

        if (quickItems != null && subscribedQuickItems != quickItems)
        {
            quickItems.Changed += HandleRebuildPresentationChanged;
            subscribedQuickItems = quickItems;
        }
    }

    private void UnsubscribeRebuildDependencies()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.Changed -= HandleRebuildPresentationChanged;
            subscribedInventory = null;
        }

        if (subscribedFlashlightBattery != null)
        {
            subscribedFlashlightBattery.Changed -= HandleBatteryPresentationChanged;
            subscribedFlashlightBattery = null;
        }

        if (subscribedQuickItems != null)
        {
            subscribedQuickItems.Changed -= HandleRebuildPresentationChanged;
            subscribedQuickItems = null;
        }
    }

    private void HandleRebuildPresentationChanged()
    {
        if (!visible)
        {
            return;
        }

        RefreshRebuildView();
    }

    private void HandleBatteryPresentationChanged()
    {
        if (!visible)
        {
            return;
        }

        int storedBatteryCount = ResolveStoredBatteryCount();

        if (storedBatteryCount == lastRenderedStoredBatteryCount)
        {
            return;
        }

        RefreshRebuildView();
    }

    private void RefreshRebuildView()
    {
        if (panelView == null || inventory == null)
        {
            return;
        }

        if (hudCanvas != null && hudCanvas.SuppressLegacyGameplayPanels)
        {
            visible = false;
            panelView.SetVisible(false);
            hudCanvas.ApplyLegacyGameplayPanelSuppression();
            return;
        }

        panelView.SetVisible(visible);

        if (!visible)
        {
            return;
        }

        panelView.Render(BuildRebuildPresentation());
        lastRenderedStoredBatteryCount = ResolveStoredBatteryCount();
    }

    private int ResolveStoredBatteryCount()
    {
        if (flashlightBattery != null)
        {
            return flashlightBattery.StoredBatteryCount;
        }

        return inventory != null
            ? inventory.GetQuantity(PrototypeItemCatalog.FlashlightBatteryItemId)
            : 0;
    }

    private InventoryPanelPresentation BuildRebuildPresentation()
    {
        IUiSettingsReadModel uiSettings = hudCanvas != null ? hudCanvas.UiSettings : UiSettingsOwner.Resolve(this);
        int visibleSlotCount = panelView != null && panelView.SlotCapacity > 0
            ? Mathf.Min(uiSettings.InventoryVisibleSlotCount, panelView.SlotCapacity)
            : uiSettings.InventoryVisibleSlotCount;
        return InventoryHudPresentationBuilder.Build(uiSettings, inventory, flashlightBattery, quickItems, visibleSlotCount);
    }
}

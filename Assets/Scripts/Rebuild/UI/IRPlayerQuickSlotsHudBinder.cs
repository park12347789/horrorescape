using UnityEngine;

[DisallowMultipleComponent]
public sealed class IRPlayerQuickSlotsHudBinder : MonoBehaviour, IRebuildHudBinder
{
    [SerializeField] private IRHudCanvas hudCanvas;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;

    private PlayerQuickItemController quickItems;
    private PlayerInventory inventory;
    private IRQuickSlotsPanelView panelView;
    private PlayerQuickItemController subscribedQuickItems;
    private PlayerInventory subscribedInventory;
    private QuickSlotPresentation[] slotPresentationBuffer = System.Array.Empty<QuickSlotPresentation>();

    private void Awake()
    {
        CacheDependencies();
        ResolvePanelView();
        SubscribeToDependencies();
        RefreshView();
    }

    private void OnValidate()
    {
        CacheDependencies();
        ResolvePanelView();
    }

    private void OnEnable()
    {
        SubscribeToDependencies();
        RefreshView();
    }

    private void OnDisable()
    {
        UnsubscribeFromDependencies();
    }

    private void OnDestroy()
    {
        UnsubscribeFromDependencies();
    }

    public void BindHudCanvas(IRHudCanvas canvas)
    {
        hudCanvas = canvas;
        ResolvePanelView();
        RefreshView();
    }

    public void BindPlayerRuntime(RPlayerRuntimeReferences runtime)
    {
        UnsubscribeFromDependencies();
        playerRuntime = runtime;
        CacheDependencies();
        SubscribeToDependencies();
        RefreshView();
    }

    private void CacheDependencies()
    {
        RPlayerRuntimeReferences runtime = playerRuntime != null ? playerRuntime : GetComponent<RPlayerRuntimeReferences>();
        quickItems = runtime != null ? runtime.QuickItems : GetComponent<PlayerQuickItemController>();
        inventory = runtime != null ? runtime.Inventory : GetComponent<PlayerInventory>();
    }

    private void ResolvePanelView()
    {
        panelView = hudCanvas != null ? hudCanvas.QuickSlotsPanel : null;
    }

    private void SubscribeToDependencies()
    {
        if (quickItems != null && subscribedQuickItems != quickItems)
        {
            quickItems.Changed += HandlePresentationChanged;
            subscribedQuickItems = quickItems;
        }

        if (inventory != null && subscribedInventory != inventory)
        {
            inventory.Changed += HandlePresentationChanged;
            subscribedInventory = inventory;
        }
    }

    private void UnsubscribeFromDependencies()
    {
        if (subscribedQuickItems != null)
        {
            subscribedQuickItems.Changed -= HandlePresentationChanged;
            subscribedQuickItems = null;
        }

        if (subscribedInventory != null)
        {
            subscribedInventory.Changed -= HandlePresentationChanged;
            subscribedInventory = null;
        }
    }

    private void HandlePresentationChanged()
    {
        RefreshView();
    }

    private void RefreshView()
    {
        if (panelView == null || quickItems == null)
        {
            return;
        }

        if (hudCanvas != null && hudCanvas.SuppressLegacyGameplayPanels)
        {
            hudCanvas.ApplyLegacyGameplayPanelSuppression();
            return;
        }

        IUiSettingsReadModel uiSettings = hudCanvas != null ? hudCanvas.UiSettings : UiSettingsOwner.Resolve(this);
        int minimumVisibleCount = uiSettings.QuickSlotVisibleCount;
        int slotCount = Mathf.Max(minimumVisibleCount, quickItems.GetConfiguredSlotCount());

        if (panelView.SlotCapacity > 0)
        {
            slotCount = Mathf.Min(slotCount, panelView.SlotCapacity);
        }

        EnsureSlotPresentationBuffer(slotCount);

        for (int index = 0; index < slotCount; index++)
        {
            if (quickItems.TryGetSlotViewAt(index, out PlayerQuickItemController.QuickSlotView slotView))
            {
                int displayedQuantity = ResolveDisplayedQuantity(slotView);
                slotPresentationBuffer[index] = new QuickSlotPresentation(
                    slotView.SlotNumber,
                    slotView.ItemId,
                    slotView.DisplayName,
                    displayedQuantity,
                    slotView.Equipped,
                    true,
                    slotView.UseKind);
                continue;
            }

            slotPresentationBuffer[index] = new QuickSlotPresentation(
                index + 1,
                string.Empty,
                string.Empty,
                0,
                false,
                false,
                PrototypeItemUseKind.Passive);
        }

        panelView.Render(new QuickSlotPanelPresentation(slotPresentationBuffer));
    }

    private void EnsureSlotPresentationBuffer(int slotCount)
    {
        int safeSlotCount = Mathf.Max(0, slotCount);

        if (slotPresentationBuffer.Length == safeSlotCount)
        {
            return;
        }

        slotPresentationBuffer = new QuickSlotPresentation[safeSlotCount];
    }

    private int ResolveDisplayedQuantity(PlayerQuickItemController.QuickSlotView slotView)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(slotView.ItemId))
        {
            return slotView.Quantity;
        }

        return inventory.GetQuantity(slotView.ItemId);
    }
}

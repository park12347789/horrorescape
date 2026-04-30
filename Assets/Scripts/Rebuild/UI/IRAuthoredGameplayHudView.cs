using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IRAuthoredGameplayHudView : MonoBehaviour, IRebuildHudBinder
{
    private const int HealthTraceStateCount = 3;
    private const int HealthTraceSegmentCount = 4;
    private const int HealthHeartCount = 3;
    private const int BatteryCellCount = 10;
    private const int InventorySlotCount = 12;
    private const int StockIndicatorCount = 3;

    [Header("Runtime")]
    [SerializeField] private IRHudCanvas hudCanvas;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField] private bool autoResolveImages = true;
    [SerializeField] private bool hideLegacyHealthAndShortcutPanels = true;
    [SerializeField] private bool hideWhenRuntimeMissing = true;
    [SerializeField] private StaminaHudView staminaHudView;

    [Header("Health")]
    [SerializeField] private Image hpFrameImage;
    [SerializeField] private Image hpHeartFilledImage;
    [SerializeField] private Image hpHeartEmptyImage;
    [SerializeField] private Image[] hpHeartFilledImages = new Image[HealthHeartCount];
    [SerializeField] private Image[] hpHeartEmptyImages = new Image[HealthHeartCount];
    [SerializeField] private Image[] hpTraceImages = new Image[HealthTraceSegmentCount];
    [SerializeField, Min(0f)] private float hpTraceScrollSpeed = 76f;
    [SerializeField, Min(0f)] private float hpTraceSegmentGap;
    [SerializeField] private Color hpStableColor = new(0.92f, 1f, 0.9f, 1f);
    [SerializeField] private Color hpWoundedColor = new(1f, 0.78f, 0.28f, 1f);
    [SerializeField] private Color hpCriticalColor = new(1f, 0.24f, 0.18f, 1f);
    [SerializeField] private Color hpEmptyHeartColor = new(0.22f, 0.08f, 0.08f, 0.72f);

    [Header("Battery")]
    [SerializeField] private Image batteryFrameImage;
    [SerializeField] private Image batteryBackGaugeImage;
    [SerializeField] private Image[] batteryCellImages = new Image[BatteryCellCount];
    [SerializeField] private TextMeshProUGUI batteryPercentText;
    [SerializeField] private bool autoLayoutBatteryTenStepGauge;
    [SerializeField, Min(1f)] private float batteryCellWidth = 16f;
    [SerializeField, Min(1f)] private float batteryCellHeight = 52f;
    [SerializeField, Min(0f)] private float batteryCellSpacing = 7f;
    [SerializeField] private Color batteryHighColor = new(0.34f, 1f, 0.74f, 1f);
    [SerializeField] private Color batteryMediumColor = new(1f, 0.86f, 0.28f, 1f);
    [SerializeField] private Color batteryLowColor = new(1f, 0.28f, 0.16f, 1f);
    [SerializeField] private Color batteryEmptyCellColor = new(0.06f, 0.08f, 0.08f, 0.22f);

    [Header("Inventory And Shortcuts")]
    [SerializeField] private bool inventoryVisible;
    [SerializeField] private Image inventoryPanelImage;
    [SerializeField] private Sprite inventoryOccupiedSlotBackgroundSprite;
    [SerializeField] private Image[] inventorySlotBackgroundImages = new Image[InventorySlotCount];
    [SerializeField] private Image[] inventorySlotIconImages = new Image[InventorySlotCount];
    [SerializeField] private TextMeshProUGUI[] inventorySlotQuantityTexts = new TextMeshProUGUI[InventorySlotCount];
    [SerializeField, Min(1f)] private float inventorySlotBackgroundSize = 70f;
    [SerializeField, Min(1f)] private float inventorySlotItemIconSize = 52f;
    [SerializeField, Min(1f)] private float inventorySlotQuantityFontSize = 30f;
    [SerializeField] private Color inventorySlotQuantityColor = new(0.84f, 0.8f, 0.68f, 1f);
    [SerializeField] private TextMeshProUGUI glassBottleCountText;
    [SerializeField] private Image shortcutBackplateImage;
    [SerializeField] private Image[] medkitStockImages = new Image[StockIndicatorCount];
    [SerializeField] private Image[] batteryStockImages = new Image[StockIndicatorCount];
    [SerializeField] private Color medkitStockColor = new(1f, 0.96f, 0.96f, 1f);
    [SerializeField] private Color batteryStockColor = new(0.96f, 1f, 0.86f, 1f);

    private readonly Sprite[] hpTraceStateSprites = new Sprite[HealthTraceStateCount];
    private readonly RectTransform[] hpTraceRects = new RectTransform[HealthTraceSegmentCount];
    private readonly Vector2[] hpTraceBasePositions = new Vector2[HealthTraceSegmentCount];

    private PlayerHealth playerHealth;
    private PlayerFlashlightBattery flashlightBattery;
    private PlayerInventory inventory;
    private PlayerQuickItemController quickItems;
    private PlayerStamina playerStamina;
    private PlayerHealth subscribedPlayerHealth;
    private PlayerFlashlightBattery subscribedFlashlightBattery;
    private PlayerInventory subscribedInventory;
    private PlayerQuickItemController subscribedQuickItems;
    private Canvas authoredCanvas;
    private bool hpTraceDefaultsCached;
    private float hpTraceOffset;
    private int lastTraceStateIndex = -1;
    private int lastHealth = int.MinValue;
    private int lastMaxHealth = int.MinValue;
    private int lastBatteryCellCount = int.MinValue;
    private int lastBatteryColorTier = int.MinValue;
    private int lastBatteryPercent = int.MinValue;
    private int lastMedkitStock = int.MinValue;
    private int lastBatteryStock = int.MinValue;
    private int lastInventorySignature = int.MinValue;
    private bool lastInventoryVisible;
    private int lastGlassBottleCount = int.MinValue;

    private void Awake()
    {
        ResolveAuthoredImagesIfNeeded();
        CacheHealthTraceDefaults();
        ApplyBatteryCellLayout();
        CacheRuntimeDependencies();
        SubscribeRuntimeDependencies();
        RefreshAll(force: true);
    }

    private void OnValidate()
    {
        ResolveAuthoredImagesIfNeeded();
        ApplyBatteryCellLayout();
        hpTraceDefaultsCached = false;
    }

    private void OnEnable()
    {
        CacheRuntimeDependencies();
        SubscribeRuntimeDependencies();
        RefreshAll(force: true);
    }

    private void OnDisable()
    {
        UnsubscribeRuntimeDependencies();
    }

    private void OnDestroy()
    {
        UnsubscribeRuntimeDependencies();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[Key.I].wasPressedThisFrame)
        {
            inventoryVisible = !inventoryVisible;
            RenderAuthoredInventory(force: true);
        }

        AnimateHealthTrace();
    }

    public void BindHudCanvas(IRHudCanvas canvas)
    {
        hudCanvas = canvas;
        ApplyLegacyHudPanelVisibility();
        RefreshAll(force: true);
    }

    public void BindPlayerRuntime(RPlayerRuntimeReferences runtime)
    {
        UnsubscribeRuntimeDependencies();
        playerRuntime = runtime;
        CacheRuntimeDependencies();
        SubscribeRuntimeDependencies();
        RefreshAll(force: true);
    }

    private void CacheRuntimeDependencies()
    {
        if (!IsSceneReference(hudCanvas))
        {
            hudCanvas = null;
        }

        if (!IsSceneReference(playerRuntime))
        {
            playerRuntime = null;
        }

        RPlayerRuntimeReferences runtime = playerRuntime != null
            ? playerRuntime
            : GetComponent<RPlayerRuntimeReferences>();

        playerHealth = runtime != null ? runtime.PlayerHealth : GetComponent<PlayerHealth>();
        flashlightBattery = runtime != null ? runtime.FlashlightBattery : GetComponent<PlayerFlashlightBattery>();
        inventory = runtime != null ? runtime.Inventory : GetComponent<PlayerInventory>();
        quickItems = runtime != null ? runtime.QuickItems : GetComponent<PlayerQuickItemController>();
        PlayerStamina runtimeStamina = runtime != null ? runtime.Stamina : null;
        playerStamina = runtimeStamina != null ? runtimeStamina : GetComponent<PlayerStamina>();

        if (staminaHudView == null || staminaHudView.transform == null || !staminaHudView.transform.IsChildOf(transform))
        {
            staminaHudView = GetComponentInChildren<StaminaHudView>(true);
        }

        staminaHudView?.Bind(playerStamina);
    }

    private bool IsSceneReference(Component component)
    {
        return component != null && component.gameObject.scene == gameObject.scene;
    }

    private void SubscribeRuntimeDependencies()
    {
        if (playerHealth != null && subscribedPlayerHealth != playerHealth)
        {
            playerHealth.Changed += HandleRuntimePresentationChanged;
            subscribedPlayerHealth = playerHealth;
        }

        if (flashlightBattery != null && subscribedFlashlightBattery != flashlightBattery)
        {
            flashlightBattery.Changed += HandleRuntimePresentationChanged;
            subscribedFlashlightBattery = flashlightBattery;
        }

        if (inventory != null && subscribedInventory != inventory)
        {
            inventory.Changed += HandleRuntimePresentationChanged;
            subscribedInventory = inventory;
        }

        if (quickItems != null && subscribedQuickItems != quickItems)
        {
            quickItems.Changed += HandleRuntimePresentationChanged;
            subscribedQuickItems = quickItems;
        }
    }

    private void UnsubscribeRuntimeDependencies()
    {
        if (subscribedPlayerHealth != null)
        {
            subscribedPlayerHealth.Changed -= HandleRuntimePresentationChanged;
            subscribedPlayerHealth = null;
        }

        if (subscribedFlashlightBattery != null)
        {
            subscribedFlashlightBattery.Changed -= HandleRuntimePresentationChanged;
            subscribedFlashlightBattery = null;
        }

        if (subscribedInventory != null)
        {
            subscribedInventory.Changed -= HandleRuntimePresentationChanged;
            subscribedInventory = null;
        }

        if (subscribedQuickItems != null)
        {
            subscribedQuickItems.Changed -= HandleRuntimePresentationChanged;
            subscribedQuickItems = null;
        }
    }

    private void HandleRuntimePresentationChanged()
    {
        RefreshAll(force: false);
    }

    private void RefreshAll(bool force)
    {
        if (!ApplyRuntimeVisibility())
        {
            return;
        }

        ApplyLegacyHudPanelVisibility();
        RenderHealth(force);
        RenderBattery(force);
        RenderShortcutStocks(force);
        RenderGlassBottleCount(force);
        RenderAuthoredInventory(force);
    }

    private bool ApplyRuntimeVisibility()
    {
        if (!hideWhenRuntimeMissing)
        {
            SetAuthoredCanvasEnabled(true);
            return true;
        }

        bool hasRuntime = playerHealth != null
            || flashlightBattery != null
            || inventory != null
            || quickItems != null
            || playerStamina != null;
        SetAuthoredCanvasEnabled(hasRuntime);
        return hasRuntime;
    }

    private void SetAuthoredCanvasEnabled(bool visible)
    {
        authoredCanvas ??= GetComponent<Canvas>();

        if (authoredCanvas != null && authoredCanvas.enabled != visible)
        {
            authoredCanvas.enabled = visible;
        }
    }

    private void ApplyLegacyHudPanelVisibility()
    {
        if (hudCanvas == null)
        {
            return;
        }

        bool legacyPanelVisible = !hideLegacyHealthAndShortcutPanels;

        if (hudCanvas.HealthPanel != null && hudCanvas.HealthPanel.PanelRoot != null)
        {
            hudCanvas.HealthPanel.PanelRoot.gameObject.SetActive(legacyPanelVisible);
        }

        if (hudCanvas.QuickSlotsPanel != null && hudCanvas.QuickSlotsPanel.PanelRoot != null)
        {
            hudCanvas.QuickSlotsPanel.PanelRoot.gameObject.SetActive(legacyPanelVisible);
        }

        if (hudCanvas.InventoryPanel != null && hudCanvas.InventoryPanel.PanelRoot != null)
        {
            hudCanvas.InventoryPanel.PanelRoot.gameObject.SetActive(legacyPanelVisible);
        }

        if (hudCanvas.StaminaPanel != null && hudCanvas.StaminaPanel != staminaHudView)
        {
            hudCanvas.StaminaPanel.gameObject.SetActive(staminaHudView == null);
        }
    }

    private void RenderHealth(bool force)
    {
        int maxHealth = playerHealth != null ? Mathf.Max(1, playerHealth.MaxHealth) : 1;
        int currentHealth = playerHealth != null ? Mathf.Clamp(playerHealth.CurrentHealth, 0, maxHealth) : 0;

        if (!force && currentHealth == lastHealth && maxHealth == lastMaxHealth)
        {
            return;
        }

        lastHealth = currentHealth;
        lastMaxHealth = maxHealth;

        float normalized = Mathf.Clamp01(currentHealth / (float)maxHealth);
        Color healthColor = ResolveHealthColor(currentHealth, maxHealth);

        if (hpFrameImage != null)
        {
            hpFrameImage.color = Color.white;
            hpFrameImage.raycastTarget = false;
        }

        RenderHeartImages(currentHealth, maxHealth);

        ApplyHealthTraceState(ResolveHealthTraceState(currentHealth, maxHealth), healthColor, force);
    }

    private void RenderHeartImages(int currentHealth, int maxHealth)
    {
        EnsureImageArraySizes();

        int visibleEmptyHeartCount = Mathf.Clamp(maxHealth, 0, HealthHeartCount);
        int visibleFilledHeartCount = Mathf.Clamp(currentHealth, 0, HealthHeartCount);

        for (int index = 0; index < HealthHeartCount; index++)
        {
            Image emptyHeart = hpHeartEmptyImages[index];

            if (emptyHeart != null)
            {
                emptyHeart.gameObject.SetActive(index < visibleEmptyHeartCount);
                emptyHeart.type = Image.Type.Simple;
                emptyHeart.color = hpEmptyHeartColor;
                emptyHeart.raycastTarget = false;
            }

            Image filledHeart = hpHeartFilledImages[index];

            if (filledHeart != null)
            {
                filledHeart.gameObject.SetActive(index < visibleFilledHeartCount);
                filledHeart.type = Image.Type.Simple;
                filledHeart.fillAmount = 1f;
                filledHeart.color = Color.white;
                filledHeart.raycastTarget = false;
            }
        }
    }

    private void RenderBattery(bool force)
    {
        float normalized = flashlightBattery != null ? Mathf.Clamp01(flashlightBattery.ChargeNormalized) : 0f;
        int batteryPercent = Mathf.Clamp(Mathf.RoundToInt(normalized * 100f), 0, 100);
        int filledCellCount = normalized <= 0.001f
            ? 0
            : Mathf.Clamp(Mathf.CeilToInt(normalized * BatteryCellCount), 0, BatteryCellCount);
        int colorTier = ResolveBatteryColorTier(normalized);

        if (!force
            && filledCellCount == lastBatteryCellCount
            && colorTier == lastBatteryColorTier
            && batteryPercent == lastBatteryPercent)
        {
            return;
        }

        lastBatteryCellCount = filledCellCount;
        lastBatteryColorTier = colorTier;
        lastBatteryPercent = batteryPercent;
        Color filledColor = ResolveBatteryColor(normalized);

        if (batteryFrameImage != null)
        {
            batteryFrameImage.color = Color.white;
            batteryFrameImage.raycastTarget = false;
        }

        if (batteryBackGaugeImage != null)
        {
            batteryBackGaugeImage.color = Color.white;
            batteryBackGaugeImage.raycastTarget = false;
        }

        for (int index = 0; index < batteryCellImages.Length; index++)
        {
            Image cellImage = batteryCellImages[index];

            if (cellImage == null)
            {
                continue;
            }

            bool filled = index < filledCellCount;
            cellImage.gameObject.SetActive(filled);
            cellImage.color = Color.white;
            cellImage.raycastTarget = false;
        }

        if (batteryPercentText != null)
        {
            batteryPercentText.text = $"{batteryPercent:00}%";
            batteryPercentText.color = filledColor;
            batteryPercentText.raycastTarget = false;
        }
    }

    private void RenderShortcutStocks(bool force)
    {
        int medkitStock = ClampVisualStock(inventory != null
            ? inventory.GetQuantity(PrototypeItemCatalog.MedkitItemId)
            : 0);
        int batteryStock = ClampVisualStock(ResolveStoredBatteryCount());

        if (!force && medkitStock == lastMedkitStock && batteryStock == lastBatteryStock)
        {
            return;
        }

        lastMedkitStock = medkitStock;
        lastBatteryStock = batteryStock;

        if (inventoryPanelImage != null)
        {
            inventoryPanelImage.raycastTarget = false;
        }

        if (shortcutBackplateImage != null)
        {
            shortcutBackplateImage.color = Color.white;
            shortcutBackplateImage.raycastTarget = false;
        }

        ApplyStockIndicators(medkitStockImages, medkitStock, medkitStockColor);
        ApplyStockIndicators(batteryStockImages, batteryStock, batteryStockColor);
    }

    private void RenderGlassBottleCount(bool force)
    {
        int glassBottleCount = inventory != null
            ? inventory.GetQuantity(PrototypeItemCatalog.GlassBottleItemId)
            : 0;

        if (!force && glassBottleCount == lastGlassBottleCount)
        {
            return;
        }

        lastGlassBottleCount = glassBottleCount;

        if (glassBottleCountText != null)
        {
            glassBottleCountText.text = glassBottleCount.ToString("00");
            glassBottleCountText.raycastTarget = false;
        }
    }

    private void RenderAuthoredInventory(bool force)
    {
        EnsureImageArraySizes();
        EnsureInventorySlotWidgets();

        int inventorySignature = ResolveInventorySignature();

        if (!force
            && inventorySignature == lastInventorySignature
            && inventoryVisible == lastInventoryVisible)
        {
            return;
        }

        lastInventorySignature = inventorySignature;
        lastInventoryVisible = inventoryVisible;

        if (inventoryPanelImage != null)
        {
            inventoryPanelImage.gameObject.SetActive(inventoryVisible);
            inventoryPanelImage.raycastTarget = false;
        }

        if (!inventoryVisible)
        {
            SetInventorySlotsVisible(false);
            return;
        }

        InventoryPanelPresentation presentation = InventoryHudPresentationBuilder.Build(
            hudCanvas != null ? hudCanvas.UiSettings : UiSettingsOwner.Resolve(this),
            inventory,
            flashlightBattery,
            quickItems,
            InventorySlotCount);

        for (int index = 0; index < InventorySlotCount; index++)
        {
            InventorySlotPresentation slot = index < presentation.Slots.Length
                ? presentation.Slots[index]
                : default;
            bool hasItem = !string.IsNullOrWhiteSpace(slot.ItemId) && slot.Quantity > 0;
            Image iconImage = inventorySlotIconImages[index];
            Image backgroundImage = inventorySlotBackgroundImages[index];

            if (backgroundImage != null)
            {
                bool showBackground = inventoryVisible && hasItem && inventoryOccupiedSlotBackgroundSprite != null;
                backgroundImage.gameObject.SetActive(showBackground);
                backgroundImage.raycastTarget = false;
                backgroundImage.preserveAspect = true;
                backgroundImage.overrideSprite = inventoryOccupiedSlotBackgroundSprite;
                backgroundImage.color = showBackground ? Color.white : Color.clear;
            }

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(inventoryVisible && hasItem);
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;
                ConfigureInventoryItemIconRect(iconImage.rectTransform);

                if (hasItem && PrototypeItemUiIconResolver.TryResolve(gameObject.scene, slot.ItemId, slot.DisplayName, out PrototypeItemUiIcon icon))
                {
                    iconImage.overrideSprite = icon.Sprite;
                    iconImage.color = icon.Tint.a > 0.001f ? icon.Tint : Color.white;
                }
                else
                {
                    iconImage.overrideSprite = null;
                    iconImage.color = ResolveInventoryFallbackColor(slot.ItemId, slot.UseKind);
                }
            }

            TextMeshProUGUI quantityText = inventorySlotQuantityTexts[index];

            if (quantityText != null)
            {
                bool showQuantity = hasItem && slot.Quantity > 0;
                quantityText.gameObject.SetActive(inventoryVisible && showQuantity);
                quantityText.text = showQuantity ? slot.Quantity.ToString() : string.Empty;
                ConfigureInventoryQuantityText(quantityText, iconImage != null ? iconImage.rectTransform : null);
                quantityText.raycastTarget = false;
            }
        }
    }

    private void EnsureInventorySlotWidgets()
    {
        if (inventoryPanelImage == null)
        {
            return;
        }

        RectTransform parent = inventoryPanelImage.rectTransform;

        if (parent == null)
        {
            return;
        }

        EnsureImageArraySizes();

        for (int index = 0; index < InventorySlotCount; index++)
        {
            Image iconImage = inventorySlotIconImages[index];

            if (iconImage == null)
            {
                continue;
            }

            RectTransform iconRect = iconImage.rectTransform;
            inventorySlotBackgroundImages[index] = EnsureInventorySlotBackground(index, parent, iconRect, inventorySlotBackgroundImages[index]);
            inventorySlotQuantityTexts[index] = EnsureInventorySlotQuantityText(index, parent, iconRect, inventorySlotQuantityTexts[index]);
            OrderInventorySlotWidgets(inventorySlotBackgroundImages[index], iconImage, inventorySlotQuantityTexts[index]);
        }
    }

    private Image EnsureInventorySlotBackground(int index, RectTransform parent, RectTransform referenceRect, Image currentImage)
    {
        if (referenceRect == null)
        {
            return currentImage;
        }

        Image resolvedImage = currentImage != null
            ? currentImage
            : ResolveImageByName($"R5F_Inventory_SlotBackground_{index:00}");

        if (resolvedImage == null)
        {
            GameObject backgroundObject = new($"R5F_Inventory_SlotBackground_{index:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            resolvedImage = backgroundObject.GetComponent<Image>();
        }

        RectTransform backgroundRect = resolvedImage.rectTransform;
        CopySlotAnchor(referenceRect, backgroundRect);
        backgroundRect.anchoredPosition = referenceRect.anchoredPosition;
        backgroundRect.sizeDelta = Vector2.one * inventorySlotBackgroundSize;
        resolvedImage.overrideSprite = inventoryOccupiedSlotBackgroundSprite;
        resolvedImage.preserveAspect = true;
        resolvedImage.raycastTarget = false;
        resolvedImage.gameObject.SetActive(false);
        return resolvedImage;
    }

    private TextMeshProUGUI EnsureInventorySlotQuantityText(int index, RectTransform parent, RectTransform referenceRect, TextMeshProUGUI currentText)
    {
        if (referenceRect == null)
        {
            return currentText;
        }

        TextMeshProUGUI resolvedText = currentText != null
            ? currentText
            : ResolveTextByName($"R5F_Inventory_SlotQuantity_{index:00}");

        if (resolvedText == null)
        {
            GameObject textObject = new($"R5F_Inventory_SlotQuantity_{index:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            resolvedText = textObject.GetComponent<TextMeshProUGUI>();
            CopyInventoryQuantityTextStyle(resolvedText);
        }

        ConfigureInventoryQuantityText(resolvedText, referenceRect);
        resolvedText.gameObject.SetActive(false);
        return resolvedText;
    }

    private void ConfigureInventoryItemIconRect(RectTransform iconRect)
    {
        if (iconRect == null)
        {
            return;
        }

        iconRect.sizeDelta = Vector2.one * inventorySlotItemIconSize;
    }

    private void ConfigureInventoryQuantityText(TextMeshProUGUI quantityText, RectTransform referenceRect)
    {
        if (quantityText == null || referenceRect == null)
        {
            return;
        }

        RectTransform textRect = quantityText.rectTransform;
        CopySlotAnchor(referenceRect, textRect);
        textRect.pivot = new Vector2(1f, 0f);
        textRect.anchoredPosition = referenceRect.anchoredPosition + new Vector2(
            (inventorySlotBackgroundSize * 0.5f) - 4f,
            (-inventorySlotBackgroundSize * 0.5f) + 2f);
        textRect.sizeDelta = new Vector2(34f, 32f);
        quantityText.alignment = TextAlignmentOptions.BottomRight;
        quantityText.fontSize = inventorySlotQuantityFontSize;
        quantityText.enableAutoSizing = false;
        quantityText.textWrappingMode = TextWrappingModes.NoWrap;
        quantityText.overflowMode = TextOverflowModes.Overflow;
        quantityText.color = inventorySlotQuantityColor;
        quantityText.raycastTarget = false;
    }

    private void CopyInventoryQuantityTextStyle(TextMeshProUGUI quantityText)
    {
        if (quantityText == null)
        {
            return;
        }

        TextMeshProUGUI styleSource = glassBottleCountText != null
            ? glassBottleCountText
            : batteryPercentText;

        if (styleSource == null)
        {
            return;
        }

        quantityText.font = styleSource.font;
        quantityText.fontSharedMaterial = styleSource.fontSharedMaterial;
    }

    private static void CopySlotAnchor(RectTransform source, RectTransform destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;
        destination.localScale = Vector3.one;
        destination.localRotation = Quaternion.identity;
    }

    private static void OrderInventorySlotWidgets(Image backgroundImage, Image iconImage, TextMeshProUGUI quantityText)
    {
        if (iconImage == null)
        {
            return;
        }

        RectTransform iconRect = iconImage.rectTransform;
        int targetIndex = iconRect.GetSiblingIndex();

        if (backgroundImage != null)
        {
            backgroundImage.rectTransform.SetSiblingIndex(targetIndex);
            targetIndex = backgroundImage.rectTransform.GetSiblingIndex() + 1;
        }

        iconRect.SetSiblingIndex(targetIndex);

        if (quantityText != null)
        {
            quantityText.rectTransform.SetSiblingIndex(iconRect.GetSiblingIndex() + 1);
        }
    }

    private void ApplyHealthTraceState(int stateIndex, Color stateColor, bool force)
    {
        CacheHealthTraceDefaults();

        if (!force && stateIndex == lastTraceStateIndex)
        {
            return;
        }

        lastTraceStateIndex = stateIndex;
        Sprite stateSprite = hpTraceStateSprites[stateIndex];

        for (int index = 0; index < hpTraceImages.Length; index++)
        {
            Image traceImage = hpTraceImages[index];

            if (traceImage == null)
            {
                continue;
            }

            traceImage.gameObject.SetActive(true);
            traceImage.overrideSprite = stateSprite;
            traceImage.color = stateColor;
            traceImage.maskable = true;
            traceImage.raycastTarget = false;
        }
    }

    private void AnimateHealthTrace()
    {
        if (hpTraceScrollSpeed <= 0.001f)
        {
            return;
        }

        CacheHealthTraceDefaults();

        if (!HasAnyHealthTraceRect())
        {
            return;
        }

        float stride = ResolveHealthTraceReferenceStride();
        float totalSpan = stride * hpTraceRects.Length;

        if (totalSpan <= 0.001f)
        {
            return;
        }

        hpTraceOffset = Mathf.Repeat(hpTraceOffset + (Time.unscaledDeltaTime * hpTraceScrollSpeed), totalSpan);
        float originX = hpTraceBasePositions[0].x;
        float originY = hpTraceBasePositions[0].y;

        for (int index = 0; index < hpTraceRects.Length; index++)
        {
            RectTransform traceRect = hpTraceRects[index];

            if (traceRect == null)
            {
                continue;
            }

            float localX = Mathf.Repeat((index * stride) - hpTraceOffset, totalSpan);

            if (localX > totalSpan - stride)
            {
                localX -= totalSpan;
            }

            traceRect.anchoredPosition = new Vector2(originX + localX, originY);
        }
    }

    private void CacheHealthTraceDefaults()
    {
        if (hpTraceDefaultsCached)
        {
            return;
        }

        EnsureImageArraySizes();

        for (int index = 0; index < HealthTraceSegmentCount; index++)
        {
            Image traceImage = hpTraceImages[index];
            hpTraceRects[index] = traceImage != null ? traceImage.rectTransform : null;
        }

        ApplyHealthTraceViewportFromStateA();

        for (int index = 0; index < HealthTraceSegmentCount; index++)
        {
            hpTraceBasePositions[index] = hpTraceRects[index] != null ? hpTraceRects[index].anchoredPosition : Vector2.zero;
        }

        for (int index = 0; index < HealthTraceStateCount; index++)
        {
            Image traceImage = index < hpTraceImages.Length ? hpTraceImages[index] : null;
            hpTraceStateSprites[index] = traceImage != null
                ? (traceImage.overrideSprite != null ? traceImage.overrideSprite : traceImage.sprite)
                : null;
        }

        hpTraceDefaultsCached = true;
    }

    private void ApplyHealthTraceViewportFromStateA()
    {
        RectTransform referenceRect = hpTraceRects.Length > 0 ? hpTraceRects[0] : null;

        if (referenceRect == null)
        {
            return;
        }

        RectTransform viewportRect = referenceRect.parent as RectTransform;

        if (viewportRect == null)
        {
            return;
        }

        Vector2 referenceOffset = referenceRect.anchoredPosition;
        Vector2 referenceVisualSize = ResolveScaledRectSize(referenceRect);

        if (referenceVisualSize.x <= 1f || referenceVisualSize.y <= 1f)
        {
            return;
        }

        if (referenceOffset.sqrMagnitude > 0.001f)
        {
            viewportRect.anchoredPosition += referenceOffset;

            for (int index = 0; index < hpTraceRects.Length; index++)
            {
                RectTransform traceRect = hpTraceRects[index];

                if (traceRect == null)
                {
                    continue;
                }

                traceRect.anchoredPosition -= referenceOffset;
            }
        }

        viewportRect.sizeDelta = referenceVisualSize;

        RectMask2D viewportMask = viewportRect.GetComponent<RectMask2D>();

        if (viewportMask == null)
        {
            viewportMask = viewportRect.gameObject.AddComponent<RectMask2D>();
        }

        viewportMask.padding = Vector4.zero;
        viewportMask.softness = Vector2Int.zero;

        for (int index = 1; index < hpTraceRects.Length; index++)
        {
            RectTransform traceRect = hpTraceRects[index];

            if (traceRect == null)
            {
                continue;
            }

            traceRect.anchorMin = referenceRect.anchorMin;
            traceRect.anchorMax = referenceRect.anchorMax;
            traceRect.pivot = referenceRect.pivot;
            traceRect.sizeDelta = referenceRect.sizeDelta;
            traceRect.localScale = referenceRect.localScale;
            traceRect.localRotation = Quaternion.identity;
        }
    }

    private bool HasAnyHealthTraceRect()
    {
        for (int index = 0; index < hpTraceRects.Length; index++)
        {
            if (hpTraceRects[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    private float ResolveHealthTraceReferenceStride()
    {
        RectTransform referenceRect = hpTraceRects.Length > 0 ? hpTraceRects[0] : null;

        if (referenceRect == null)
        {
            return 1f;
        }

        return Mathf.Max(1f, ResolveScaledRectSize(referenceRect).x + hpTraceSegmentGap);
    }

    private static Vector2 ResolveScaledRectSize(RectTransform rect)
    {
        if (rect == null)
        {
            return Vector2.zero;
        }

        return new Vector2(
            rect.rect.width * Mathf.Abs(rect.localScale.x),
            rect.rect.height * Mathf.Abs(rect.localScale.y));
    }

    private void ApplyBatteryCellLayout()
    {
        if (!autoLayoutBatteryTenStepGauge)
        {
            return;
        }

        EnsureImageArraySizes();
        RectTransform firstCellRect = batteryCellImages.Length > 0 && batteryCellImages[0] != null
            ? batteryCellImages[0].rectTransform
            : null;

        if (firstCellRect == null)
        {
            return;
        }

        Vector2 startPosition = firstCellRect.anchoredPosition;

        for (int index = 0; index < batteryCellImages.Length; index++)
        {
            Image cellImage = batteryCellImages[index];

            if (cellImage == null)
            {
                continue;
            }

            RectTransform cellRect = cellImage.rectTransform;
            cellRect.anchorMin = new Vector2(0f, 1f);
            cellRect.anchorMax = new Vector2(0f, 1f);
            cellRect.pivot = new Vector2(0f, 1f);
            cellRect.anchoredPosition = new Vector2(
                startPosition.x + (index * (batteryCellWidth + batteryCellSpacing)),
                startPosition.y);
            cellRect.sizeDelta = new Vector2(batteryCellWidth, batteryCellHeight);
        }
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

    private static int ResolveHealthTraceState(int currentHealth, int maxHealth)
    {
        if (currentHealth <= 0)
        {
            return 2;
        }

        float normalized = Mathf.Clamp01(currentHealth / (float)Mathf.Max(1, maxHealth));

        if (normalized <= 0.34f)
        {
            return 2;
        }

        return normalized <= 0.67f ? 1 : 0;
    }

    private Color ResolveHealthColor(int currentHealth, int maxHealth)
    {
        int stateIndex = ResolveHealthTraceState(currentHealth, maxHealth);

        switch (stateIndex)
        {
            case 0:
                return hpStableColor;
            case 1:
                return hpWoundedColor;
            default:
                return hpCriticalColor;
        }
    }

    private Color ResolveBatteryColor(float normalized)
    {
        switch (ResolveBatteryColorTier(normalized))
        {
            case 0:
                return batteryLowColor;
            case 1:
                return batteryMediumColor;
            default:
                return batteryHighColor;
        }
    }

    private static int ResolveBatteryColorTier(float normalized)
    {
        if (normalized <= 0.25f)
        {
            return 0;
        }

        return normalized <= 0.55f ? 1 : 2;
    }

    private static int ClampVisualStock(int quantity)
    {
        return Mathf.Clamp(quantity, 0, StockIndicatorCount);
    }

    private void SetInventorySlotsVisible(bool visible)
    {
        for (int index = 0; index < inventorySlotBackgroundImages.Length; index++)
        {
            if (inventorySlotBackgroundImages[index] != null)
            {
                inventorySlotBackgroundImages[index].gameObject.SetActive(visible);
            }
        }

        for (int index = 0; index < inventorySlotIconImages.Length; index++)
        {
            if (inventorySlotIconImages[index] != null)
            {
                inventorySlotIconImages[index].gameObject.SetActive(visible);
            }
        }

        for (int index = 0; index < inventorySlotQuantityTexts.Length; index++)
        {
            if (inventorySlotQuantityTexts[index] != null)
            {
                inventorySlotQuantityTexts[index].gameObject.SetActive(visible);
            }
        }
    }

    private int ResolveInventorySignature()
    {
        unchecked
        {
            int hash = inventoryVisible ? 397 : 113;

            if (inventory != null)
            {
                for (int index = 0; index < inventory.Items.Count; index++)
                {
                    PlayerInventory.ItemStack stack = inventory.Items[index];

                    if (stack == null)
                    {
                        continue;
                    }

                    hash = (hash * 31) + (!string.IsNullOrWhiteSpace(stack.itemId) ? stack.itemId.GetHashCode() : 0);
                    hash = (hash * 31) + Mathf.Max(0, stack.quantity);
                }
            }

            if (quickItems != null)
            {
                int configuredSlotCount = quickItems.GetConfiguredSlotCount();
                hash = (hash * 31) + configuredSlotCount;

                for (int index = 0; index < configuredSlotCount; index++)
                {
                    if (!quickItems.TryGetSlotViewAt(index, out PlayerQuickItemController.QuickSlotView slotView))
                    {
                        continue;
                    }

                    hash = (hash * 31) + slotView.SlotNumber;
                    hash = (hash * 31) + (!string.IsNullOrWhiteSpace(slotView.ItemId) ? slotView.ItemId.GetHashCode() : 0);
                    hash = (hash * 31) + slotView.Quantity;
                    hash = (hash * 31) + (slotView.Equipped ? 1 : 0);
                }
            }

            return hash;
        }
    }

    private static Color ResolveInventoryFallbackColor(string itemId, PrototypeItemUseKind useKind)
    {
        if (string.Equals(itemId, PrototypeItemCatalog.MedkitItemId, System.StringComparison.Ordinal))
        {
            return new Color(0.86f, 0.18f, 0.16f, 0.96f);
        }

        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, System.StringComparison.Ordinal))
        {
            return new Color(0.98f, 0.72f, 0.18f, 0.96f);
        }

        if (string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, System.StringComparison.Ordinal))
        {
            return new Color(0.42f, 0.68f, 0.9f, 0.96f);
        }

        return useKind == PrototypeItemUseKind.Throwable
            ? new Color(0.82f, 0.52f, 0.18f, 0.96f)
            : new Color(0.82f, 0.82f, 0.76f, 0.96f);
    }

    private static void ApplyStockIndicators(Image[] stockImages, int visibleCount, Color visibleColor)
    {
        if (stockImages == null)
        {
            return;
        }

        for (int index = 0; index < stockImages.Length; index++)
        {
            Image stockImage = stockImages[index];

            if (stockImage == null)
            {
                continue;
            }

            bool visible = index < visibleCount;
            stockImage.gameObject.SetActive(visible);
            stockImage.color = visibleColor;
            stockImage.raycastTarget = false;
        }
    }

    private void ResolveAuthoredImagesIfNeeded()
    {
        if (!autoResolveImages)
        {
            EnsureImageArraySizes();
            return;
        }

        EnsureImageArraySizes();

        hpFrameImage = ResolveImage(hpFrameImage, "ui_image_0_HP_Frame");
        batteryFrameImage = ResolveImage(batteryFrameImage, "ui_image_1_Battery_Frame");
        inventoryPanelImage = ResolveImage(inventoryPanelImage, "ui_image_2_Inventory_Panel");
        for (int index = 0; index < inventorySlotBackgroundImages.Length; index++)
        {
            inventorySlotBackgroundImages[index] = ResolveImage(
                inventorySlotBackgroundImages[index],
                $"R5F_Inventory_SlotBackground_{index:00}");
        }

        hpTraceImages[0] = ResolveImage(hpTraceImages[0], "ui_image_3_HP_Trace_StateA");
        hpHeartFilledImages[0] = ResolveImage(hpHeartFilledImages[0] != null ? hpHeartFilledImages[0] : hpHeartFilledImage, "ui_image_4_HP_HeartFilled");
        hpHeartFilledImages[1] = ResolveImage(hpHeartFilledImages[1], "ui_image_4_HP_HeartFilled (1)");
        hpHeartFilledImages[2] = ResolveImage(hpHeartFilledImages[2], "ui_image_4_HP_HeartFilled (2)");
        hpHeartFilledImage = hpHeartFilledImages[0];
        hpHeartEmptyImages[0] = ResolveImage(hpHeartEmptyImages[0] != null ? hpHeartEmptyImages[0] : hpHeartEmptyImage, "ui_image_5_HP_HeartEmpty");
        hpHeartEmptyImages[1] = ResolveImage(hpHeartEmptyImages[1], "ui_image_5_HP_HeartEmpty (1)");
        hpHeartEmptyImages[2] = ResolveImage(hpHeartEmptyImages[2], "ui_image_5_HP_HeartEmpty (2)");
        hpHeartEmptyImage = hpHeartEmptyImages[0];
        batteryBackGaugeImage = ResolveImage(batteryBackGaugeImage, "ui_image_6_Battery_BackGauge");
        for (int index = 0; index < batteryCellImages.Length; index++)
        {
            batteryCellImages[index] = ResolveBatteryCellImage(batteryCellImages[index], index);
        }

        hpTraceImages[1] = ResolveImage(hpTraceImages[1], "ui_image_11_HP_Trace_StateB");
        medkitStockImages[0] = ResolveImage(medkitStockImages[0], "ui_image_12_Shortcut_Visual_0");
        medkitStockImages[1] = ResolveImage(medkitStockImages[1], "ui_image_13_Shortcut_Visual_1");
        medkitStockImages[2] = ResolveImage(medkitStockImages[2], "ui_image_14_Shortcut_Visual_2");
        batteryStockImages[0] = ResolveImage(batteryStockImages[0], "ui_image_15_Shortcut_Visual_3");
        batteryStockImages[1] = ResolveImage(batteryStockImages[1], "ui_image_16_Shortcut_Visual_4");
        batteryStockImages[2] = ResolveImage(batteryStockImages[2], "ui_image_17_Shortcut_Visual_5");
        shortcutBackplateImage = ResolveImage(shortcutBackplateImage, "ui_image_18_Shortcut_Backplate");
        hpTraceImages[2] = ResolveImage(hpTraceImages[2], "ui_image_19_HP_Trace_StateC");
        hpTraceImages[3] = ResolveImage(hpTraceImages[3], "R5F_HP_Trace_LoopSegment_03");
        batteryPercentText = ResolveText(batteryPercentText, "battery_percent");

        for (int index = 0; index < inventorySlotIconImages.Length; index++)
        {
            inventorySlotIconImages[index] = ResolveImage(
                inventorySlotIconImages[index],
                $"R5F_Inventory_SlotIcon_{index:00}");
            inventorySlotQuantityTexts[index] = ResolveText(
                inventorySlotQuantityTexts[index],
                $"R5F_Inventory_SlotQuantity_{index:00}");
        }

        glassBottleCountText = ResolveText(glassBottleCountText, "Numberbottles");
    }

    private Image ResolveBatteryCellImage(Image currentImage, int index)
    {
        string authoredCellName = index == 0
            ? "ui_image_8_Battery_Cell_1"
            : $"ui_image_8_Battery_Cell_1 ({index})";
        Image authoredCell = ResolveImageByName(authoredCellName);

        if (authoredCell != null)
        {
            return authoredCell;
        }

        string legacyCellName = index switch
        {
            0 => "ui_image_7_Battery_Cell_0",
            1 => "ui_image_8_Battery_Cell_1",
            2 => "ui_image_9_Battery_Cell_2",
            3 => "ui_image_10_Battery_Cell_3",
            _ => $"R5F_Battery_ChargeBar_{index:00}"
        };

        return ResolveImageByName(legacyCellName) ?? currentImage;
    }

    private void EnsureImageArraySizes()
    {
        EnsureImageArraySize(ref hpTraceImages, HealthTraceSegmentCount);
        EnsureImageArraySize(ref hpHeartFilledImages, HealthHeartCount);
        EnsureImageArraySize(ref hpHeartEmptyImages, HealthHeartCount);
        EnsureImageArraySize(ref batteryCellImages, BatteryCellCount);
        EnsureImageArraySize(ref inventorySlotBackgroundImages, InventorySlotCount);
        EnsureImageArraySize(ref inventorySlotIconImages, InventorySlotCount);
        EnsureTextArraySize(ref inventorySlotQuantityTexts, InventorySlotCount);
        EnsureImageArraySize(ref medkitStockImages, StockIndicatorCount);
        EnsureImageArraySize(ref batteryStockImages, StockIndicatorCount);
    }

    private static void EnsureImageArraySize(ref Image[] images, int length)
    {
        if (images != null && images.Length == length)
        {
            return;
        }

        Image[] resized = new Image[length];

        if (images != null)
        {
            int copyLength = Mathf.Min(images.Length, length);

            for (int index = 0; index < copyLength; index++)
            {
                resized[index] = images[index];
            }
        }

        images = resized;
    }

    private static void EnsureTextArraySize(ref TextMeshProUGUI[] texts, int length)
    {
        if (texts != null && texts.Length == length)
        {
            return;
        }

        TextMeshProUGUI[] resized = new TextMeshProUGUI[length];

        if (texts != null)
        {
            int copyLength = Mathf.Min(texts.Length, length);

            for (int index = 0; index < copyLength; index++)
            {
                resized[index] = texts[index];
            }
        }

        texts = resized;
    }

    private Image ResolveImage(Image currentImage, string childName)
    {
        if (currentImage != null && currentImage.transform != null && currentImage.transform.IsChildOf(transform))
        {
            return currentImage;
        }

        return ResolveImageByName(childName) ?? currentImage;
    }

    private Image ResolveImageByName(string childName)
    {
        Transform child = FindChildRecursive(transform, childName);
        return child != null && child.TryGetComponent(out Image image) ? image : null;
    }

    private TextMeshProUGUI ResolveText(TextMeshProUGUI currentText, string childName)
    {
        if (currentText != null && currentText.transform != null && currentText.transform.IsChildOf(transform))
        {
            return currentText;
        }

        Transform child = FindChildRecursive(transform, childName);
        return child != null && child.TryGetComponent(out TextMeshProUGUI text) ? text : currentText;
    }

    private TextMeshProUGUI ResolveTextByName(string childName)
    {
        Transform child = FindChildRecursive(transform, childName);
        return child != null && child.TryGetComponent(out TextMeshProUGUI text) ? text : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name.Trim(), childName.Trim(), System.StringComparison.Ordinal))
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform match = FindChildRecursive(root.GetChild(index), childName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}

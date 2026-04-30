using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IRInventoryPanelView : MonoBehaviour
{
    private const float InventoryHeaderHeight = 108f;

    [Serializable]
    public sealed class SlotWidget
    {
        public RectTransform root;
        public Image frame;
        public Image icon;
        public TextMeshProUGUI label;
        public TextMeshProUGUI quantity;
        public Image quickSlotBadge;
        public TextMeshProUGUI quickSlotText;
        public Image equippedBorder;
    }

    [SerializeField] private MonoBehaviour uiSettingsReadModelSource;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TextMeshProUGUI batterySummaryText;
    [SerializeField] private TextMeshProUGUI infoSummaryText;
    [SerializeField] private SlotWidget[] slotWidgets = Array.Empty<SlotWidget>();

    public RectTransform PanelRoot => panelRoot;
    public TextMeshProUGUI BatterySummaryText => batterySummaryText;
    public TextMeshProUGUI InfoSummaryText => infoSummaryText;
    public SlotWidget[] SlotWidgets => slotWidgets ?? Array.Empty<SlotWidget>();
    public int SlotCapacity => slotWidgets != null ? slotWidgets.Length : 0;

    private void Awake()
    {
        ResolveUiSettings();
        ValidateBindings();
        ApplyLayout(ResolveUiSettings(), slotWidgets.Length);
    }

    private void OnValidate()
    {
        ResolveUiSettings();
        ValidateBindings();
    }

    public void Configure(RectTransform configuredPanelRoot, TextMeshProUGUI configuredBatterySummaryText, TextMeshProUGUI configuredInfoSummaryText, SlotWidget[] configuredSlotWidgets)
    {
        panelRoot = configuredPanelRoot;
        batterySummaryText = configuredBatterySummaryText;
        infoSummaryText = configuredInfoSummaryText;
        slotWidgets = configuredSlotWidgets ?? Array.Empty<SlotWidget>();

        for (int index = 0; index < slotWidgets.Length; index++)
        {
            if (slotWidgets[index]?.icon != null)
            {
                slotWidgets[index].icon.preserveAspect = true;
            }
        }

        ResolveUiSettings();
        ValidateBindings();
        ApplyLayout(ResolveUiSettings(), slotWidgets.Length);
    }

    public void SetVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.gameObject.activeSelf != visible)
        {
            panelRoot.gameObject.SetActive(visible);
        }
    }

    public void Render(in InventoryPanelPresentation presentation)
    {
        IUiSettingsReadModel uiSettings = ResolveUiSettings();
        ApplyLayout(uiSettings, presentation.Slots.Length);
        bool useAuthoredPixelLayout = IRPixelHudArt.HasAuthoredInventoryLayout(this);

        if (!useAuthoredPixelLayout)
        {
            IRAnalogNoiseUiTheme.ApplyInventoryPanelTheme(panelRoot, batterySummaryText, infoSummaryText);
        }

        if (batterySummaryText != null)
        {
            batterySummaryText.text = presentation.BatterySummary;
        }

        if (infoSummaryText != null)
        {
            infoSummaryText.text = presentation.InfoSummary;
        }

        int slotCount = Mathf.Min(slotWidgets.Length, presentation.Slots.Length);

        for (int index = 0; index < slotWidgets.Length; index++)
        {
            SlotWidget widget = slotWidgets[index];
            bool slotVisible = index < slotCount;

            if (widget?.root != null)
            {
                widget.root.gameObject.SetActive(slotVisible);
            }

            if (!slotVisible)
            {
                continue;
            }

            InventorySlotPresentation slot = presentation.Slots[index];
            RenderSlot(gameObject.scene, widget, slot, !string.IsNullOrWhiteSpace(slot.ItemId), useAuthoredPixelLayout);
        }
    }

    public void ApplyLayout(IUiSettingsReadModel uiSettings, int visibleSlotCount)
    {
        if (uiSettings == null || panelRoot == null)
        {
            return;
        }

        if (IRPixelHudArt.HasAuthoredInventoryLayout(this))
        {
            return;
        }

        panelRoot.anchorMin = new Vector2(1f, 1f);
        panelRoot.anchorMax = new Vector2(1f, 1f);
        panelRoot.pivot = new Vector2(1f, 1f);
        panelRoot.anchoredPosition = new Vector2(-uiSettings.InventoryPanelMargin.x, -uiSettings.InventoryPanelMargin.y);
        panelRoot.sizeDelta = uiSettings.InventoryPanelSize;

        int columnCount = Mathf.Max(1, uiSettings.InventorySlotColumnCount);
        Vector2 slotSize = uiSettings.InventorySlotSize;
        Vector2 slotSpacing = uiSettings.InventorySlotSpacing;
        int layoutSlotCount = visibleSlotCount > 0 ? visibleSlotCount : slotWidgets.Length;

        for (int index = 0; index < slotWidgets.Length; index++)
        {
            RectTransform slotRoot = slotWidgets[index]?.root;

            if (slotRoot == null)
            {
                continue;
            }

            int column = index % columnCount;
            int row = index / columnCount;
            slotRoot.anchorMin = new Vector2(0f, 1f);
            slotRoot.anchorMax = new Vector2(0f, 1f);
            slotRoot.pivot = new Vector2(0f, 1f);
            slotRoot.anchoredPosition = new Vector2(
                uiSettings.InventoryPanelMargin.x + (column * (slotSize.x + slotSpacing.x)),
                -(uiSettings.InventoryPanelMargin.y + InventoryHeaderHeight + (row * (slotSize.y + slotSpacing.y))));
            slotRoot.sizeDelta = slotSize;

            if (index >= layoutSlotCount)
            {
                slotRoot.gameObject.SetActive(false);
            }
        }
    }

    private void ValidateBindings()
    {
        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(IRInventoryPanelView)} is missing its panel root reference.", this);
        }
    }

    private static void RenderSlot(UnityEngine.SceneManagement.Scene scene, SlotWidget widget, InventorySlotPresentation slot, bool hasItem, bool useAuthoredPixelLayout)
    {
        if (widget == null || widget.root == null)
        {
            return;
        }

        if (useAuthoredPixelLayout)
        {
            RenderPixelSlot(scene, widget, slot, hasItem);
            return;
        }

        bool hasQuantity = hasItem && slot.Quantity > 0;
        IRUiItemSemantic itemSemantic = IRUiItemSemanticUtility.Resolve(slot.ItemId);
        Color emptyFrameColor = new(0.11f, 0.1f, 0.1f, 0.98f);
        Color emptyIconColor = new(0.16f, 0.17f, 0.18f, 0.28f);
        Color occupiedFrameColor = slot.Equipped
            ? new Color(0.2f, 0.18f, 0.14f, 1f)
            : hasQuantity
                ? new Color(0.15f, 0.13f, 0.11f, 0.98f)
                : new Color(0.12f, 0.12f, 0.11f, 0.94f);

        if (widget.frame != null)
        {
            widget.frame.color = hasItem ? occupiedFrameColor : emptyFrameColor;
        }

        PrototypeItemUiIcon resolvedIcon = default;
        bool hasResolvedIcon = hasItem && PrototypeItemUiIconResolver.TryResolve(scene, slot.ItemId, slot.DisplayName, out resolvedIcon);

        if (widget.icon != null)
        {
            widget.icon.preserveAspect = true;
            widget.icon.overrideSprite = hasResolvedIcon ? resolvedIcon.Sprite : null;
            widget.icon.color = hasItem
                ? hasResolvedIcon
                    ? GetReadableIconTint(itemSemantic, resolvedIcon.Tint, hasQuantity)
                    : GetItemColor(itemSemantic, slot.UseKind, slot.Quantity > 0)
                : emptyIconColor;
        }

        if (widget.label != null)
        {
            bool showLabelFallback = hasItem && !hasResolvedIcon;
            widget.label.text = showLabelFallback ? GetShortLabel(slot.DisplayName) : string.Empty;
            widget.label.gameObject.SetActive(showLabelFallback);
            widget.label.color = hasItem
                ? new Color(0.98f, 0.98f, 1f, hasQuantity ? 0.96f : 0.72f)
                : new Color(0.72f, 0.74f, 0.76f, 0.54f);
        }

        if (widget.quantity != null)
        {
            widget.quantity.text = hasItem && slot.Quantity > 0 ? $"x{slot.Quantity}" : string.Empty;
            widget.quantity.color = hasItem
                ? new Color(0.98f, 0.92f, 0.78f, hasQuantity ? 1f : 0.64f)
                : new Color(0.74f, 0.76f, 0.8f, 0.35f);
        }

        if (widget.quickSlotBadge != null)
        {
            bool showBadge = hasItem && slot.QuickSlotNumber > 0;
            widget.quickSlotBadge.gameObject.SetActive(showBadge);

            if (showBadge)
            {
                widget.quickSlotBadge.color = new Color(0.2f, 0.26f, 0.34f, 0.97f);
            }
        }

        if (widget.quickSlotText != null)
        {
            bool showBadgeText = hasItem && slot.QuickSlotNumber > 0;
            widget.quickSlotText.gameObject.SetActive(showBadgeText);

            if (showBadgeText)
            {
                widget.quickSlotText.text = slot.QuickSlotNumber.ToString();
                widget.quickSlotText.color = new Color(0.95f, 0.98f, 1f, 1f);
            }
        }

        if (widget.equippedBorder != null)
        {
            bool showBorder = hasItem && slot.Equipped;
            widget.equippedBorder.gameObject.SetActive(showBorder);
            widget.equippedBorder.color = new Color(0.98f, 0.84f, 0.34f, 0f);

            Outline outline = widget.equippedBorder.GetComponent<Outline>();

            if (outline != null)
            {
                outline.effectColor = new Color(0.98f, 0.84f, 0.34f, showBorder ? 0.94f : 0f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }

        IRAnalogNoiseUiTheme.ApplyInventorySlotTheme(
            widget.frame,
            widget.icon,
            widget.label,
            widget.quantity,
            widget.quickSlotBadge,
            widget.quickSlotText,
            widget.equippedBorder,
            hasItem,
            hasQuantity,
            hasItem && slot.Equipped,
            itemSemantic);
    }

    private static void RenderPixelSlot(UnityEngine.SceneManagement.Scene scene, SlotWidget widget, InventorySlotPresentation slot, bool hasItem)
    {
        bool hasQuantity = hasItem && slot.Quantity > 0;
        IRUiItemSemantic itemSemantic = IRUiItemSemanticUtility.Resolve(slot.ItemId);
        Color accentColor = GetItemColor(itemSemantic, slot.UseKind, hasQuantity);

        if (widget.frame != null)
        {
            widget.frame.sprite = IRPixelHudArt.WhiteSprite;
            widget.frame.color = slot.Equipped
                ? new Color(accentColor.r * 0.35f, accentColor.g * 0.35f, accentColor.b * 0.35f, 0.22f)
                : new Color(0f, 0f, 0f, hasItem ? 0.12f : 0.04f);
        }

        PrototypeItemUiIcon resolvedIcon = default;
        bool hasResolvedIcon = hasItem && PrototypeItemUiIconResolver.TryResolve(scene, slot.ItemId, slot.DisplayName, out resolvedIcon);

        if (widget.icon != null)
        {
            widget.icon.preserveAspect = true;
            widget.icon.overrideSprite = hasResolvedIcon ? resolvedIcon.Sprite : null;
            widget.icon.color = hasItem
                ? hasResolvedIcon
                    ? GetReadableIconTint(itemSemantic, resolvedIcon.Tint, hasQuantity)
                    : GetItemColor(itemSemantic, slot.UseKind, slot.Quantity > 0)
                : Color.clear;
            widget.icon.gameObject.SetActive(hasResolvedIcon);
        }

        if (widget.label != null)
        {
            bool showLabelFallback = hasItem && !hasResolvedIcon;
            widget.label.text = showLabelFallback ? GetShortLabel(slot.DisplayName) : string.Empty;
            widget.label.gameObject.SetActive(showLabelFallback);
            widget.label.color = new Color(0.97f, 0.93f, 0.82f, 0.94f);
        }

        if (widget.quantity != null)
        {
            widget.quantity.text = hasQuantity ? $"x{slot.Quantity}" : string.Empty;
            widget.quantity.color = new Color(0.98f, 0.94f, 0.8f, hasQuantity ? 1f : 0f);
        }

        if (widget.quickSlotBadge != null)
        {
            widget.quickSlotBadge.gameObject.SetActive(false);
        }

        if (widget.quickSlotText != null)
        {
            widget.quickSlotText.gameObject.SetActive(false);
        }

        if (widget.equippedBorder != null)
        {
            bool showBorder = hasItem && slot.Equipped;
            widget.equippedBorder.gameObject.SetActive(showBorder);
            widget.equippedBorder.sprite = IRPixelHudArt.WhiteSprite;
            widget.equippedBorder.color = Color.clear;

            Outline outline = widget.equippedBorder.GetComponent<Outline>();

            if (outline != null)
            {
                outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, showBorder ? 0.92f : 0f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
    }

    private static string GetShortLabel(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        if (string.Equals(displayName, "Flashlight Battery", StringComparison.OrdinalIgnoreCase))
        {
            return "CELL";
        }

        if (string.Equals(displayName, "Flashlight", StringComparison.OrdinalIgnoreCase))
        {
            return "LITE";
        }

        if (string.Equals(displayName, "Glass Bottle", StringComparison.OrdinalIgnoreCase))
        {
            return "BTL";
        }

        if (displayName.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "KEY";
        }

        string trimmed = displayName.Trim();

        if (trimmed.Length <= 4)
        {
            return trimmed.ToUpperInvariant();
        }

        string[] parts = trimmed.Split(' ');

        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        return trimmed.Substring(0, Mathf.Min(3, trimmed.Length)).ToUpperInvariant();
    }

    private static Color GetItemColor(IRUiItemSemantic itemSemantic, PrototypeItemUseKind useKind, bool hasQuantity)
    {
        Color baseColor = itemSemantic switch
        {
            IRUiItemSemantic.Flashlight => new Color(0.98f, 0.9f, 0.46f, hasQuantity ? 1f : 0.45f),
            IRUiItemSemantic.GlassBottle => new Color(0.38f, 0.62f, 0.82f, hasQuantity ? 1f : 0.45f),
            IRUiItemSemantic.Medkit => new Color(0.82f, 0.2f, 0.18f, hasQuantity ? 1f : 0.45f),
            IRUiItemSemantic.FlashlightBattery => new Color(0.95f, 0.72f, 0.2f, hasQuantity ? 1f : 0.45f),
            _ => new Color(0.58f, 0.56f, 0.78f, hasQuantity ? 1f : 0.45f)
        };

        if (useKind == PrototypeItemUseKind.Instant)
        {
            return Color.Lerp(baseColor, new Color(0.82f, 0.16f, 0.12f, baseColor.a), 0.4f);
        }

        if (useKind == PrototypeItemUseKind.Throwable
            && itemSemantic != IRUiItemSemantic.GlassBottle)
        {
            return Color.Lerp(baseColor, new Color(0.88f, 0.52f, 0.18f, baseColor.a), 0.35f);
        }

        return baseColor;
    }

    private static Color GetReadableIconTint(IRUiItemSemantic itemSemantic, Color sourceTint, bool hasQuantity)
    {
        if (itemSemantic == IRUiItemSemantic.GlassBottle)
        {
            return new Color(0.94f, 0.98f, 1f, hasQuantity ? 0.98f : 0.45f);
        }

        Color normalized = Color.Lerp(Color.white, sourceTint, 0.1f);
        normalized.a = hasQuantity ? 0.98f : 0.45f;
        return normalized;
    }

    private IUiSettingsReadModel ResolveUiSettings()
    {
        IUiSettingsReadModel uiSettings = UiSettingsOwner.Resolve(this, uiSettingsReadModelSource);

        if (uiSettingsReadModelSource == null && uiSettings is MonoBehaviour behaviour)
        {
            uiSettingsReadModelSource = behaviour;
        }

        return uiSettings;
    }
}

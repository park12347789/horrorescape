using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IRQuickSlotsPanelView : MonoBehaviour
{
    [Serializable]
    public sealed class SlotWidget
    {
        public RectTransform root;
        public Image frame;
        public Image accent;
        public Image icon;
        public TextMeshProUGUI keyText;
        public TextMeshProUGUI itemLabel;
        public TextMeshProUGUI quantityText;
        public Image equippedBorder;
    }

    [SerializeField] private MonoBehaviour uiSettingsReadModelSource;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private SlotWidget[] slotWidgets = Array.Empty<SlotWidget>();

    public RectTransform PanelRoot => panelRoot;
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

    public void Configure(RectTransform configuredPanelRoot, SlotWidget[] configuredSlotWidgets)
    {
        panelRoot = configuredPanelRoot;
        slotWidgets = configuredSlotWidgets ?? Array.Empty<SlotWidget>();
        ResolveUiSettings();
        ValidateBindings();
        ApplyLayout(ResolveUiSettings(), slotWidgets.Length);
    }

    public void Render(in QuickSlotPanelPresentation presentation)
    {
        IUiSettingsReadModel uiSettings = ResolveUiSettings();
        ApplyLayout(uiSettings, presentation.Slots.Length);
        bool useAuthoredPixelLayout = IRPixelHudArt.HasAuthoredQuickSlotsLayout(this);

        if (!useAuthoredPixelLayout)
        {
            IRAnalogNoiseUiTheme.ApplyQuickSlotsPanelTheme(panelRoot);
        }

        if (panelRoot != null && !panelRoot.gameObject.activeSelf)
        {
            panelRoot.gameObject.SetActive(true);
        }

        for (int index = 0; index < slotWidgets.Length; index++)
        {
            bool hasSlot = index < presentation.Slots.Length;
            SlotWidget widget = slotWidgets[index];

            if (widget?.root != null)
            {
                widget.root.gameObject.SetActive(hasSlot);
            }

            if (!hasSlot)
            {
                continue;
            }

            QuickSlotPresentation slot = presentation.Slots[index];
            RenderSlot(gameObject.scene, widget, slot, true, useAuthoredPixelLayout);
        }
    }

    public void ApplyLayout(IUiSettingsReadModel uiSettings, int visibleSlotCount)
    {
        if (uiSettings == null || panelRoot == null)
        {
            return;
        }

        if (IRPixelHudArt.HasAuthoredQuickSlotsLayout(this))
        {
            return;
        }

        panelRoot.anchorMin = new Vector2(0.5f, 0f);
        panelRoot.anchorMax = new Vector2(0.5f, 0f);
        panelRoot.pivot = new Vector2(0.5f, 0f);
        panelRoot.anchoredPosition = new Vector2(0f, uiSettings.QuickSlotPanelMargin.y);
        panelRoot.sizeDelta = uiSettings.QuickSlotPanelSize;

        int layoutSlotCount = Mathf.Clamp(visibleSlotCount > 0 ? visibleSlotCount : slotWidgets.Length, 1, Mathf.Max(1, slotWidgets.Length));
        Vector2 cardSize = uiSettings.QuickSlotCardSize;
        Vector2 cardSpacing = uiSettings.QuickSlotCardSpacing;
        float totalWidth = (layoutSlotCount * cardSize.x) + ((layoutSlotCount - 1) * cardSpacing.x);
        float startX = -0.5f * totalWidth;

        for (int index = 0; index < slotWidgets.Length; index++)
        {
            RectTransform slotRoot = slotWidgets[index]?.root;

            if (slotRoot == null)
            {
                continue;
            }

            slotRoot.anchorMin = new Vector2(0.5f, 0.5f);
            slotRoot.anchorMax = new Vector2(0.5f, 0.5f);
            slotRoot.pivot = new Vector2(0f, 0.5f);
            slotRoot.anchoredPosition = new Vector2(startX + (index * (cardSize.x + cardSpacing.x)), 0f);
            slotRoot.sizeDelta = cardSize;
        }
    }

    private void ValidateBindings()
    {
        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(IRQuickSlotsPanelView)} is missing its panel root reference.", this);
        }
    }

    private static void RenderSlot(UnityEngine.SceneManagement.Scene scene, SlotWidget widget, QuickSlotPresentation slot, bool hasSlot, bool useAuthoredPixelLayout)
    {
        if (widget == null || widget.root == null)
        {
            return;
        }

        if (useAuthoredPixelLayout)
        {
            RenderPixelSlot(scene, widget, slot, hasSlot);
            return;
        }

        bool configured = hasSlot && slot.ConfiguredSlot;
        bool hasQuantity = slot.Quantity > 0;
        IRUiItemSemantic itemSemantic = IRUiItemSemanticUtility.Resolve(slot.ItemId);
        Color frameColor = configured
            ? hasQuantity
                ? new Color(0.13f, 0.16f, 0.2f, 0.98f)
                : new Color(0.11f, 0.13f, 0.16f, 0.96f)
            : new Color(0.08f, 0.09f, 0.11f, 0.72f);
        Color accentColor = configured
            ? GetItemAccentColor(itemSemantic, slot.UseKind, hasQuantity)
            : new Color(0.2f, 0.24f, 0.3f, 0.38f);

        if (configured && itemSemantic == IRUiItemSemantic.GlassBottle)
        {
            accentColor = new Color(0.34f, 0.54f, 0.72f, hasQuantity ? 0.96f : 0.42f);
        }

        if (configured && slot.Equipped)
        {
            frameColor = new Color(0.18f, 0.21f, 0.26f, 1f);
        }

        if (widget.frame != null)
        {
            widget.frame.color = frameColor;
        }

        if (widget.accent != null)
        {
            widget.accent.color = accentColor;
        }

        PrototypeItemUiIcon resolvedIcon = default;
        bool hasResolvedIcon = configured && PrototypeItemUiIconResolver.TryResolve(scene, slot.ItemId, slot.DisplayName, out resolvedIcon);

        if (widget.icon != null)
        {
            widget.icon.preserveAspect = false;
            widget.icon.overrideSprite = hasResolvedIcon ? resolvedIcon.Sprite : null;
            widget.icon.color = hasResolvedIcon
                ? GetReadableIconTint(itemSemantic, resolvedIcon.Tint, hasQuantity)
                : Color.clear;
            widget.icon.gameObject.SetActive(hasResolvedIcon);
        }

        if (widget.keyText != null)
        {
            widget.keyText.text = string.Empty;
            widget.keyText.gameObject.SetActive(false);
        }

        if (widget.itemLabel != null)
        {
            widget.itemLabel.text = string.Empty;
            widget.itemLabel.gameObject.SetActive(false);
        }

        if (widget.quantityText != null)
        {
            widget.quantityText.gameObject.SetActive(configured && hasQuantity);
            widget.quantityText.text = configured && hasQuantity ? $"x{Mathf.Max(0, slot.Quantity)}" : string.Empty;
            widget.quantityText.color = configured
                ? new Color(0.98f, 0.92f, 0.76f, hasQuantity ? 1f : 0.64f)
                : new Color(0.7f, 0.72f, 0.75f, 0.4f);

            if (configured && hasQuantity)
            {
                widget.quantityText.rectTransform.SetAsLastSibling();
            }
        }

        if (widget.equippedBorder != null)
        {
            bool showBorder = configured && slot.Equipped;
            widget.equippedBorder.gameObject.SetActive(showBorder);
            widget.equippedBorder.color = new Color(0.98f, 0.82f, 0.34f, 0f);

            Outline outline = widget.equippedBorder.GetComponent<Outline>();

            if (outline != null)
            {
                outline.effectColor = new Color(0.98f, 0.84f, 0.34f, showBorder ? 0.96f : 0f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        IRAnalogNoiseUiTheme.ApplyQuickSlotTheme(
            widget.frame,
            widget.accent,
            widget.icon,
            widget.keyText,
            widget.itemLabel,
            widget.quantityText,
            widget.equippedBorder,
            configured,
            hasQuantity,
            configured && slot.Equipped,
            itemSemantic);
    }

    private static void RenderPixelSlot(UnityEngine.SceneManagement.Scene scene, SlotWidget widget, QuickSlotPresentation slot, bool hasSlot)
    {
        bool configured = hasSlot && slot.ConfiguredSlot;
        bool hasItem = configured && !string.IsNullOrWhiteSpace(slot.ItemId);
        bool hasQuantity = hasItem && slot.Quantity > 0;
        IRUiItemSemantic itemSemantic = IRUiItemSemanticUtility.Resolve(slot.ItemId);
        Color accentColor = configured
            ? GetItemAccentColor(itemSemantic, slot.UseKind, hasQuantity)
            : new Color(0.38f, 0.34f, 0.3f, 0.42f);

        if (widget.frame != null)
        {
            widget.frame.sprite = IRPixelHudArt.WhiteSprite;
            widget.frame.color = slot.Equipped
                ? new Color(0f, 0f, 0f, 0.22f)
                : new Color(0f, 0f, 0f, hasItem ? 0.12f : 0.06f);
        }

        if (widget.accent != null)
        {
            widget.accent.sprite = IRPixelHudArt.WhiteSprite;
            widget.accent.color = accentColor;
        }

        PrototypeItemUiIcon resolvedIcon = default;
        bool hasResolvedIcon = hasItem && PrototypeItemUiIconResolver.TryResolve(scene, slot.ItemId, slot.DisplayName, out resolvedIcon);

        if (widget.icon != null)
        {
            widget.icon.preserveAspect = true;
            widget.icon.overrideSprite = hasResolvedIcon ? resolvedIcon.Sprite : null;
            widget.icon.color = hasResolvedIcon
                ? GetReadableIconTint(itemSemantic, resolvedIcon.Tint, hasQuantity)
                : Color.clear;
            widget.icon.gameObject.SetActive(hasResolvedIcon);
        }

        if (widget.keyText != null)
        {
            widget.keyText.gameObject.SetActive(true);
            string keyLabel = GetQuickSlotKeyLabel(slot, configured);
            widget.keyText.text = keyLabel;
            widget.keyText.fontSize = keyLabel.Length > 2 ? 18f : 24f;
            widget.keyText.color = new Color(0.97f, 0.92f, 0.8f, configured ? 1f : 0.72f);
        }

        if (widget.itemLabel != null)
        {
            bool showLabelFallback = hasItem && !hasResolvedIcon;
            widget.itemLabel.gameObject.SetActive(showLabelFallback);
            widget.itemLabel.text = showLabelFallback ? GetShortFallbackLabel(slot.DisplayName) : string.Empty;
            widget.itemLabel.color = new Color(0.93f, 0.9f, 0.82f, 0.94f);
        }

        if (widget.quantityText != null)
        {
            widget.quantityText.gameObject.SetActive(hasQuantity);
            widget.quantityText.text = hasQuantity ? $"x{Mathf.Max(0, slot.Quantity)}" : string.Empty;
            widget.quantityText.color = new Color(0.98f, 0.94f, 0.8f, hasQuantity ? 1f : 0f);

            if (hasQuantity)
            {
                RepairPixelQuantityTextIfNeeded(widget.quantityText);
                widget.quantityText.rectTransform.SetAsLastSibling();
            }
        }

        if (widget.equippedBorder != null)
        {
            bool showBorder = configured && slot.Equipped;
            widget.equippedBorder.gameObject.SetActive(showBorder);
            widget.equippedBorder.sprite = IRPixelHudArt.WhiteSprite;
            widget.equippedBorder.color = Color.clear;

            Outline outline = widget.equippedBorder.GetComponent<Outline>();

            if (outline != null)
            {
                outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, showBorder ? 0.96f : 0f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
    }

    private static string GetShortFallbackLabel(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        string trimmed = displayName.Trim();

        if (trimmed.Length <= 3)
        {
            return trimmed.ToUpperInvariant();
        }

        string[] parts = trimmed.Split(' ');

        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        return trimmed.Substring(0, 3).ToUpperInvariant();
    }

    private static string GetQuickSlotKeyLabel(QuickSlotPresentation slot, bool configured)
    {
        if (!configured)
        {
            return Mathf.Max(1, slot.SlotNumber).ToString();
        }

        return slot.UseKind == PrototypeItemUseKind.Throwable ? "SPACE" : Mathf.Max(1, slot.SlotNumber).ToString();
    }

    private static Color GetItemAccentColor(IRUiItemSemantic itemSemantic, PrototypeItemUseKind useKind, bool hasQuantity)
    {
        Color baseColor = itemSemantic switch
        {
            IRUiItemSemantic.Flashlight => new Color(0.98f, 0.9f, 0.46f, hasQuantity ? 1f : 0.42f),
            IRUiItemSemantic.GlassBottle => new Color(0.38f, 0.62f, 0.82f, hasQuantity ? 1f : 0.42f),
            IRUiItemSemantic.Medkit => new Color(0.84f, 0.2f, 0.2f, hasQuantity ? 1f : 0.42f),
            IRUiItemSemantic.FlashlightBattery => new Color(0.95f, 0.75f, 0.2f, hasQuantity ? 1f : 0.42f),
            _ => new Color(0.62f, 0.52f, 0.76f, hasQuantity ? 1f : 0.42f)
        };

        if (useKind == PrototypeItemUseKind.Instant)
        {
            return Color.Lerp(baseColor, new Color(0.88f, 0.24f, 0.18f, baseColor.a), 0.32f);
        }

        if (useKind == PrototypeItemUseKind.Throwable
            && itemSemantic != IRUiItemSemantic.GlassBottle)
        {
            return Color.Lerp(baseColor, new Color(0.92f, 0.56f, 0.18f, baseColor.a), 0.24f);
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

    private static void RepairPixelQuantityTextIfNeeded(TextMeshProUGUI quantityText)
    {
        if (quantityText == null
            || string.IsNullOrEmpty(quantityText.text))
        {
            return;
        }

        RectTransform rectTransform = quantityText.rectTransform;
        bool changed = false;

        if (quantityText.overflowMode != TextOverflowModes.Overflow)
        {
            quantityText.overflowMode = TextOverflowModes.Overflow;
            changed = true;
        }

        if (quantityText.textWrappingMode != TextWrappingModes.NoWrap)
        {
            quantityText.textWrappingMode = TextWrappingModes.NoWrap;
            changed = true;
        }

        if (quantityText.margin != Vector4.zero)
        {
            quantityText.margin = Vector4.zero;
            changed = true;
        }

        if (rectTransform.localScale != Vector3.one)
        {
            rectTransform.localScale = Vector3.one;
            changed = true;
        }

        quantityText.ForceMeshUpdate();

        if (!quantityText.isTextTruncated && !quantityText.isTextOverflowing)
        {
            return;
        }

        Vector2 targetSize = new Vector2(Mathf.Max(rectTransform.sizeDelta.x, 56f), Mathf.Max(rectTransform.sizeDelta.y, 20f));

        if (rectTransform.sizeDelta != targetSize)
        {
            rectTransform.sizeDelta = targetSize;
            changed = true;
        }

        Vector2 defaultAnchoredPosition = new Vector2(-10f, 12f);

        if (Vector2.Distance(rectTransform.anchoredPosition, defaultAnchoredPosition) > 0.01f)
        {
            rectTransform.anchoredPosition = defaultAnchoredPosition;
            changed = true;
        }

        if (changed)
        {
            quantityText.ForceMeshUpdate();
        }
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

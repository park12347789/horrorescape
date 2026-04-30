using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class IRPixelHudArt
{
    private static Sprite whiteSprite;
    private static Texture2D whiteTexture;

    public static bool IsHealthLayoutAvailable => false;
    public static bool IsQuickSlotsLayoutAvailable => false;
    public static bool IsInventoryLayoutAvailable => false;
    public static Sprite WhiteSprite => GetWhiteSprite();

    public static bool HasAuthoredHealthLayout(IRHealthPanelView view)
    {
        if (view == null)
        {
            return false;
        }

        Image healthTrackImage = GetTrackImage(view, "HealthTrack");
        Image batteryTrackImage = GetTrackImage(view, "BatteryTrack");
        return HasAssignedSprite(healthTrackImage) && HasAssignedSprite(batteryTrackImage);
    }

    public static bool HasAuthoredQuickSlotsLayout(IRQuickSlotsPanelView view)
    {
        return HasAssignedSprite(view?.PanelRoot != null ? view.PanelRoot.GetComponent<Image>() : null);
    }

    public static bool HasAuthoredInventoryLayout(IRInventoryPanelView view)
    {
        return HasAssignedSprite(view?.PanelRoot != null ? view.PanelRoot.GetComponent<Image>() : null);
    }

    public static bool TryGetHealthPanelSprite(out Sprite sprite) => TryResolveRetiredPixelHudSprite(out sprite);
    public static bool TryGetBatteryPanelSprite(out Sprite sprite) => TryResolveRetiredPixelHudSprite(out sprite);
    public static bool TryGetQuickSlotsPanelSprite(out Sprite sprite) => TryResolveRetiredPixelHudSprite(out sprite);
    public static bool TryGetInventoryPanelSprite(out Sprite sprite) => TryResolveRetiredPixelHudSprite(out sprite);

    public static bool TryApplyHealthLayout(IRHealthPanelView view, IUiSettingsReadModel uiSettings)
    {
        if (view?.PanelRoot == null
            || uiSettings == null
            || !TryGetHealthPanelSprite(out Sprite healthSprite)
            || !TryGetBatteryPanelSprite(out Sprite batterySprite))
        {
            return false;
        }

        float barWidth = Mathf.Max(uiSettings.HealthPanelSize.x * 1.55f, 520f);
        float healthHeight = GetHeightForWidth(healthSprite, barWidth);
        float batteryHeight = GetHeightForWidth(batterySprite, barWidth);
        float gap = Mathf.Max(10f, healthHeight * 0.12f);

        ConfigureFixedRect(
            view.PanelRoot,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(uiSettings.HealthPanelMargin.x + 8f, -(uiSettings.HealthPanelMargin.y + 6f)),
            new Vector2(barWidth, healthHeight + gap + batteryHeight));

        HidePanelRootImage(view.PanelRoot);

        RectTransform healthTrack = GetTrackRect(view, "HealthTrack");
        RectTransform batteryTrack = GetTrackRect(view, "BatteryTrack");

        if (healthTrack != null)
        {
            ConfigureFixedRect(
                healthTrack,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(barWidth, healthHeight));
            ApplyPanelSprite(healthTrack.GetComponent<Image>(), healthSprite);
        }

        if (batteryTrack != null)
        {
            ConfigureFixedRect(
                batteryTrack,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, -(healthHeight + gap)),
                new Vector2(barWidth, batteryHeight));
            ApplyPanelSprite(batteryTrack.GetComponent<Image>(), batterySprite);
        }

        RectTransform healthFillArea = EnsureFillArea(
            healthTrack,
            "IRPixelHealthFillArea",
            barWidth * 0.29f,
            0f,
            barWidth * 0.67f,
            Mathf.Max(26f, healthHeight * 0.38f),
            preserveExistingRect: true);
        RectTransform batteryFillArea = EnsureFillArea(
            batteryTrack,
            "IRPixelBatteryFillArea",
            barWidth * 0.61f,
            0f,
            barWidth * 0.34f,
            Mathf.Max(18f, batteryHeight * 0.3f),
            preserveExistingRect: true);

        PruneDuplicateNamedDescendants(healthFillArea, "IRPixelHealthFillArea");
        PruneDuplicateNamedDescendants(batteryFillArea, "IRPixelBatteryFillArea");

        EnsureBaseLine(healthFillArea, "IRPixelHealthBaseLine");
        EnsureBaseLine(batteryFillArea, "IRPixelBatteryBaseLine");

        PrepareFillGraphic(view.FillImage, healthFillArea);
        PrepareFillGraphic(view.BatteryFillImage, batteryFillArea);
        PrepareFillGraphic(view.PulseOverlay, healthFillArea, lastSibling: true);
        EnsureHeartbeatGraphic(healthFillArea);

        if (view.ValueText != null)
        {
            view.ValueText.gameObject.SetActive(false);
        }

        if (view.BatteryValueText != null)
        {
            view.BatteryValueText.gameObject.SetActive(true);
            if (batteryTrack != null && view.BatteryValueText.rectTransform.parent != batteryTrack)
            {
                view.BatteryValueText.rectTransform.SetParent(batteryTrack, false);
            }

            ConfigureFixedRect(
                view.BatteryValueText.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(barWidth * 0.17f, 0f),
                new Vector2(barWidth * 0.32f, 34f));
            ConfigureText(view.BatteryValueText, 22f, TextAlignmentOptions.Left);
            view.BatteryValueText.rectTransform.SetAsLastSibling();
        }

        if (view.DividerRoot != null)
        {
            view.DividerRoot.gameObject.SetActive(false);
        }

        return true;
    }

    public static bool ApplyHealthVisuals(IRHealthPanelView view, in HealthPanelPresentation presentation)
    {
        if (view == null
            || !HasAuthoredHealthLayout(view))
        {
            return false;
        }

        float batteryNormalized = Mathf.Clamp01(presentation.FlashlightChargeNormalized);
        float healthNormalized = presentation.MaxHealth <= 0
            ? 0f
            : Mathf.Clamp01(presentation.CurrentHealth / (float)presentation.MaxHealth);
        Color healthColor = EvaluateHealthStageColor(presentation.CurrentHealth, presentation.MaxHealth);
        Color batteryColor = EvaluateBatteryColor(batteryNormalized);

        if (view.BatteryValueText != null)
        {
            view.BatteryValueText.text = $"{Mathf.RoundToInt(batteryNormalized * 100f)}% x{presentation.StoredBatteryCount}";
            view.BatteryValueText.color = batteryNormalized <= 0.2f
                ? new Color(1f, 0.82f, 0.48f, 1f)
                : new Color(0.97f, 0.93f, 0.82f, 1f);
        }

        if (view.BatteryFillImage != null)
        {
            ApplyHorizontalFill(view.BatteryFillImage, batteryNormalized);
            view.BatteryFillImage.color = batteryColor;
            SetBaseLineColor(view.BatteryFillImage.rectTransform.parent as RectTransform, new Color(0.22f, 0.32f, 0.18f, 0.42f));
        }

        if (view.ValueText != null)
        {
            view.ValueText.gameObject.SetActive(false);
            view.ValueText.text = string.Empty;
        }

        if (view.FillImage != null)
        {
            view.FillImage.enabled = false;
            view.FillImage.color = Color.clear;
            SetBaseLineColor(view.FillImage.rectTransform.parent as RectTransform, new Color(healthColor.r, healthColor.g, healthColor.b, 0.22f));
        }

        IRHudHeartbeatGraphic heartbeatGraphic = TryGetExistingHeartbeatGraphic(view.FillImage != null
            ? view.FillImage.rectTransform.parent as RectTransform
            : GetTrackRect(view, "HealthTrack"));

        if (heartbeatGraphic != null)
        {
            heartbeatGraphic.Configure(
                healthColor,
                Mathf.Lerp(2.4f, 3.6f, 1f - healthNormalized),
                EvaluateHeartbeatAmplitude(presentation.CurrentHealth, presentation.MaxHealth),
                EvaluateHeartbeatSpeed(presentation.CurrentHealth, presentation.MaxHealth));
        }

        if (view.PulseOverlay != null)
        {
            view.PulseOverlay.enabled = false;
            view.PulseOverlay.color = Color.clear;
        }

        return true;
    }

    public static bool TryApplyQuickSlotsLayout(IRQuickSlotsPanelView view, IUiSettingsReadModel uiSettings, int visibleSlotCount)
    {
        if (view?.PanelRoot == null
            || uiSettings == null
            || !TryGetQuickSlotsPanelSprite(out Sprite panelSprite))
        {
            return false;
        }

        float panelWidth = Mathf.Max(uiSettings.QuickSlotPanelSize.x * 1.32f, 520f);
        float panelHeight = GetHeightForWidth(panelSprite, panelWidth);

        ConfigureFixedRect(
            view.PanelRoot,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, Mathf.Max(28f, uiSettings.QuickSlotPanelMargin.y + 24f)),
            new Vector2(panelWidth, panelHeight));
        ApplyPanelSprite(view.PanelRoot.GetComponent<Image>(), panelSprite);

        IRQuickSlotsPanelView.SlotWidget[] slotWidgets = view.SlotWidgets;
        int slotCount = Mathf.Clamp(
            visibleSlotCount > 0 ? visibleSlotCount : slotWidgets.Length,
            1,
            Mathf.Max(1, slotWidgets.Length));
        float leftPad = panelWidth * 0.04f;
        float rightPad = panelWidth * 0.04f;
        float topPad = panelHeight * 0.05f;
        float bottomPad = panelHeight * 0.06f;
        float cellWidth = (panelWidth - leftPad - rightPad) / slotCount;
        float cellHeight = panelHeight - topPad - bottomPad;

        for (int index = 0; index < slotWidgets.Length; index++)
        {
            IRQuickSlotsPanelView.SlotWidget widget = slotWidgets[index];

            if (widget?.root == null)
            {
                continue;
            }

            ConfigureFixedRect(
                widget.root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(leftPad + (index * cellWidth), -topPad),
                new Vector2(cellWidth, cellHeight));

            ConfigureQuickSlotWidget(widget, cellWidth, cellHeight);
        }

        return true;
    }

    public static bool TryApplyInventoryLayout(IRInventoryPanelView view, IUiSettingsReadModel uiSettings, int visibleSlotCount)
    {
        if (view?.PanelRoot == null
            || uiSettings == null
            || !TryGetInventoryPanelSprite(out Sprite panelSprite))
        {
            return false;
        }

        float panelWidth = Mathf.Max(uiSettings.InventoryPanelSize.x * 1.28f, 520f);
        float panelHeight = GetHeightForWidth(panelSprite, panelWidth);

        ConfigureFixedRect(
            view.PanelRoot,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-(uiSettings.InventoryPanelMargin.x + 12f), -(uiSettings.InventoryPanelMargin.y + 12f)),
            new Vector2(panelWidth, panelHeight));
        ApplyPanelSprite(view.PanelRoot.GetComponent<Image>(), panelSprite);

        if (view.BatterySummaryText != null)
        {
            view.BatterySummaryText.gameObject.SetActive(true);
            ConfigureFixedRect(
                view.BatterySummaryText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -16f),
                new Vector2(panelWidth * 0.26f, 22f));
            ConfigureText(view.BatterySummaryText, 15f, TextAlignmentOptions.Left);
        }

        if (view.InfoSummaryText != null)
        {
            view.InfoSummaryText.gameObject.SetActive(true);
            ConfigureFixedRect(
                view.InfoSummaryText.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -16f),
                new Vector2(panelWidth * 0.64f, 22f));
            ConfigureText(view.InfoSummaryText, 14f, TextAlignmentOptions.Right);
        }

        IRInventoryPanelView.SlotWidget[] slotWidgets = view.SlotWidgets;
        int slotCount = Mathf.Clamp(
            visibleSlotCount > 0 ? visibleSlotCount : slotWidgets.Length,
            1,
            Mathf.Max(1, slotWidgets.Length));
        int columnCount = Mathf.Min(3, Mathf.Max(1, slotCount));
        int rowCount = Mathf.CeilToInt(slotCount / (float)columnCount);
        float leftPad = panelWidth * 0.035f;
        float rightPad = panelWidth * 0.035f;
        float topPad = panelHeight * 0.17f;
        float bottomPad = panelHeight * 0.05f;
        float cellWidth = (panelWidth - leftPad - rightPad) / columnCount;
        float cellHeight = (panelHeight - topPad - bottomPad) / Mathf.Max(1, rowCount);

        for (int index = 0; index < slotWidgets.Length; index++)
        {
            IRInventoryPanelView.SlotWidget widget = slotWidgets[index];

            if (widget?.root == null)
            {
                continue;
            }

            int column = index % columnCount;
            int row = index / columnCount;
            ConfigureFixedRect(
                widget.root,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(leftPad + (column * cellWidth), -(topPad + (row * cellHeight))),
                new Vector2(cellWidth, cellHeight));

            ConfigureInventorySlotWidget(widget, cellWidth, cellHeight);
        }

        return true;
    }

    private static void ConfigureQuickSlotWidget(IRQuickSlotsPanelView.SlotWidget widget, float cellWidth, float cellHeight)
    {
        ConfigureSlotFrame(widget.frame, widget.root, 0.05f);

        if (widget.accent != null)
        {
            SetGraphicSprite(widget.accent, GetWhiteSprite());
            ConfigureFixedRect(
                widget.accent.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 12f),
                new Vector2(cellWidth * 0.7f, 9f));
        }

        ConfigureSlotIcon(widget.icon, cellWidth, cellHeight, 0f, -8f, 0.58f);
        ConfigureSlotText(widget.keyText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(Mathf.Min(cellWidth - 20f, 92f), 30f), 24f, TextAlignmentOptions.Center);
        ConfigureSlotText(widget.itemLabel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(cellWidth - 24f, 18f), 16f, TextAlignmentOptions.Center);
        ConfigureSlotText(widget.quantityText, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 12f), new Vector2(56f, 20f), 17f, TextAlignmentOptions.Right);

        if (widget.equippedBorder != null)
        {
            SetGraphicSprite(widget.equippedBorder, GetWhiteSprite());
            Stretch(widget.equippedBorder.rectTransform);
        }
    }

    private static void ConfigureInventorySlotWidget(IRInventoryPanelView.SlotWidget widget, float cellWidth, float cellHeight)
    {
        ConfigureSlotFrame(widget.frame, widget.root, 0.04f);
        ConfigureSlotIcon(widget.icon, cellWidth, cellHeight, 0f, -10f, 0.56f);
        ConfigureSlotText(widget.label, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(cellWidth - 28f, 18f), 15f, TextAlignmentOptions.Center);
        ConfigureSlotText(widget.quantity, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(56f, 18f), 16f, TextAlignmentOptions.Right);

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
            SetGraphicSprite(widget.equippedBorder, GetWhiteSprite());
            Stretch(widget.equippedBorder.rectTransform);
        }
    }

    private static void ConfigureSlotFrame(Image frame, RectTransform slotRoot, float alpha)
    {
        if (frame == null)
        {
            return;
        }

        SetGraphicSprite(frame, GetWhiteSprite());

        if (frame.rectTransform != slotRoot)
        {
            Stretch(frame.rectTransform);
        }

        frame.color = new Color(1f, 1f, 1f, alpha);
        frame.raycastTarget = false;
    }

    private static void ConfigureSlotIcon(Image icon, float cellWidth, float cellHeight, float x, float y, float sizeRatio)
    {
        if (icon == null)
        {
            return;
        }

        icon.preserveAspect = true;
        ConfigureFixedRect(
            icon.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(x, y),
            Vector2.one * Mathf.Min(cellWidth, cellHeight) * sizeRatio);
    }

    private static void ConfigureSlotText(TextMeshProUGUI text, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        if (text == null)
        {
            return;
        }

        ConfigureFixedRect(text.rectTransform, anchor, pivot, anchoredPosition, size);
        ConfigureText(text, fontSize, alignment);
    }

    private static void ConfigureText(TextMeshProUGUI text, float fontSize, TextAlignmentOptions alignment)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
    }

    private static void HidePanelRootImage(RectTransform panelRoot)
    {
        Image image = panelRoot.GetComponent<Image>();

        if (image == null)
        {
            return;
        }

        image.enabled = false;
        image.color = Color.clear;
    }

    private static void ApplyPanelSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
        {
            return;
        }

        image.enabled = true;
        image.sprite = sprite;
        image.overrideSprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static RectTransform EnsureFillArea(RectTransform track, string name, float left, float y, float width, float height, bool preserveExistingRect = false)
    {
        if (track == null)
        {
            return null;
        }

        RectTransform area = EnsureChildRect(track, name);

        if (!preserveExistingRect || !HasMeaningfulAuthoredRect(area))
        {
            ConfigureFixedRect(
                area,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(left, y),
                new Vector2(width, height));
        }

        return area;
    }

    private static Image EnsureBaseLine(RectTransform parent, string name)
    {
        RectTransform baseLineRect = EnsureChildRect(parent, name);
        baseLineRect.SetAsFirstSibling();
        Stretch(baseLineRect);
        Image baseLine = GetOrAddImage(baseLineRect);
        SetGraphicSprite(baseLine, GetWhiteSprite());
        baseLine.color = new Color(1f, 1f, 1f, 0.18f);
        return baseLine;
    }

    private static void SetBaseLineColor(RectTransform fillArea, Color color)
    {
        if (fillArea == null)
        {
            return;
        }

        Image baseLine = fillArea.GetComponentInChildren<Image>(true);

        if (baseLine != null && baseLine.transform != fillArea)
        {
            baseLine.color = color;
        }
    }

    private static IRHudHeartbeatGraphic EnsureHeartbeatGraphic(RectTransform parent)
    {
        if (parent == null)
        {
            return null;
        }

        RectTransform graphicRect = EnsureChildRect(parent, "IRPixelHeartbeatLine");
        Stretch(graphicRect);
        graphicRect.SetAsLastSibling();

        if (!graphicRect.TryGetComponent<CanvasRenderer>(out _))
        {
            graphicRect.gameObject.AddComponent<CanvasRenderer>();
        }

        IRHudHeartbeatGraphic graphic = graphicRect.GetComponent<IRHudHeartbeatGraphic>();

        if (graphic == null)
        {
            graphic = graphicRect.gameObject.AddComponent<IRHudHeartbeatGraphic>();
        }

        graphic.raycastTarget = false;
        return graphic;
    }

    private static IRHudHeartbeatGraphic TryGetExistingHeartbeatGraphic(RectTransform parent)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find("IRPixelHeartbeatLine");

        if (child != null && child.TryGetComponent(out IRHudHeartbeatGraphic heartbeatGraphic))
        {
            return heartbeatGraphic;
        }

        return parent.GetComponentInChildren<IRHudHeartbeatGraphic>(true);
    }

    private static void PrepareFillGraphic(Image image, RectTransform parent, bool lastSibling = false)
    {
        if (image == null || parent == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;

        if (rect.parent != parent)
        {
            rect.SetParent(parent, false);
        }

        SetGraphicSprite(image, GetWhiteSprite());
        Stretch(rect);

        if (lastSibling)
        {
            rect.SetAsLastSibling();
        }
    }

    private static void SetGraphicSprite(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.enabled = true;
        image.sprite = sprite;
        image.overrideSprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private static Image GetOrAddImage(RectTransform rect)
    {
        Image image = rect.GetComponent<Image>();
        return image != null ? image : rect.gameObject.AddComponent<Image>();
    }

    private static bool HasMeaningfulAuthoredRect(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        return rect.sizeDelta.x > 1f || rect.sizeDelta.y > 1f || rect.anchoredPosition.sqrMagnitude > 0.01f;
    }

    private static RectTransform EnsureChildRect(RectTransform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(name);

        if (existing is RectTransform existingRect)
        {
            return existingRect;
        }

        GameObject child = new(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void ConfigureFixedRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void ApplyHorizontalFill(Image image, float normalized)
    {
        if (image == null)
        {
            return;
        }

        RectTransform fillRect = image.rectTransform;

        if (fillRect == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalized);
        image.type = Image.Type.Simple;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(clamped, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private static Color EvaluateHealthStageColor(int currentHealth, int maxHealth)
    {
        int clampedHealth = Mathf.Clamp(currentHealth, 0, Mathf.Max(1, maxHealth));

        if (clampedHealth <= 1)
        {
            return new Color(0.9f, 0.24f, 0.18f, 1f);
        }

        if (clampedHealth == 2)
        {
            return new Color(0.94f, 0.82f, 0.28f, 1f);
        }

        return new Color(0.43f, 0.86f, 0.35f, 1f);
    }

    private static float EvaluateHeartbeatSpeed(int currentHealth, int maxHealth)
    {
        int clampedHealth = Mathf.Clamp(currentHealth, 0, Mathf.Max(1, maxHealth));

        if (clampedHealth <= 1)
        {
            return 0.9f;
        }

        if (clampedHealth == 2)
        {
            return 0.68f;
        }

        return 0.48f;
    }

    private static float EvaluateHeartbeatAmplitude(int currentHealth, int maxHealth)
    {
        int clampedHealth = Mathf.Clamp(currentHealth, 0, Mathf.Max(1, maxHealth));

        if (clampedHealth <= 1)
        {
            return 1f;
        }

        if (clampedHealth == 2)
        {
            return 0.92f;
        }

        return 0.84f;
    }

    private static Color EvaluateBatteryColor(float normalized)
    {
        return Color.Lerp(
            new Color(0.26f, 0.42f, 0.2f, 0.9f),
            new Color(0.52f, 0.88f, 0.36f, 1f),
            normalized);
    }

    private static float GetHeightForWidth(Sprite sprite, float width)
    {
        return width * (sprite.rect.height / sprite.rect.width);
    }

    private static bool TryResolveRetiredPixelHudSprite(out Sprite sprite)
    {
        sprite = null;
        return false;
    }

    private static bool HasAssignedSprite(Image image)
    {
        return image != null && (image.sprite != null || image.overrideSprite != null);
    }

    private static void PruneDuplicateNamedDescendants(RectTransform root, string duplicateName)
    {
        if (root == null || string.IsNullOrWhiteSpace(duplicateName))
        {
            return;
        }

        RectTransform[] descendants = root.GetComponentsInChildren<RectTransform>(true);

        for (int index = 0; index < descendants.Length; index++)
        {
            RectTransform descendant = descendants[index];

            if (descendant == null
                || descendant == root
                || !string.Equals(descendant.name, duplicateName, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(descendant.gameObject);
            }
            else
            {
                Object.DestroyImmediate(descendant.gameObject);
            }
        }
    }

    private static RectTransform GetTrackRect(IRHealthPanelView view, string trackName)
    {
        if (view?.PanelRoot == null || string.IsNullOrWhiteSpace(trackName))
        {
            return null;
        }

        Transform trackTransform = view.PanelRoot.Find(trackName);
        return trackTransform as RectTransform;
    }

    private static Image GetTrackImage(IRHealthPanelView view, string trackName)
    {
        return GetTrackRect(view, trackName)?.GetComponent<Image>();
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply(false, true);
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }
}

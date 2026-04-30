using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class IRAnalogNoiseUiTheme
{
    private const string HudBackdropName = "IRAnalogNoiseBackdrop";
    private const string HudScanlineName = "IRAnalogNoiseScanlines";
    private const string LobbyThreatStripName = "IRAnalogNoiseLobbyThreatStrip";
    private const string LobbyHeaderName = "IRAnalogNoiseLobbyHeader";
    private const string LobbySubheaderName = "IRAnalogNoiseLobbySubheader";
    private const string LobbySystemTagName = "IRAnalogNoiseLobbySystemTag";
    private const string LobbyStatusPlateName = "IRAnalogNoiseStatusPlate";
    private const string LobbySummaryLabelName = "IRAnalogNoiseLobbySummaryLabel";
    private const string LobbySummaryStatusName = "IRAnalogNoiseLobbySummaryStatus";
    private const string LobbySummaryDetailName = "IRAnalogNoiseLobbySummaryDetail";
    private const string LobbyHeartbeatPanelName = "IRAnalogNoiseLobbyHeartbeatPanel";
    private const string LobbyHeartbeatGlowName = "IRAnalogNoiseLobbyHeartbeatGlow";
    private const string LobbyHeartbeatLabelName = "IRAnalogNoiseLobbyHeartbeatLabel";
    private const string LobbyHeartbeatStatusName = "IRAnalogNoiseLobbyHeartbeatStatus";
    private const string LobbyDirectivePanelName = "IRAnalogNoiseLobbyDirectivePanel";
    private const string LobbyDirectiveLabelName = "IRAnalogNoiseLobbyDirectiveLabel";
    private const string LobbyDirectiveBodyName = "IRAnalogNoiseLobbyDirectiveBody";
    private const string LobbyDirectiveTagsName = "IRAnalogNoiseLobbyDirectiveTags";
    private const string LobbyRoutePanelName = "IRAnalogNoiseLobbyRoutePanel";
    private const string LobbyRouteHeaderName = "IRAnalogNoiseLobbyRouteHeader";
    private const string LobbyRouteSubheaderName = "IRAnalogNoiseLobbyRouteSubheader";
    private const string LobbyRouteStatusName = "IRAnalogNoiseLobbyRouteStatus";
    private const string LobbyRouteDetailName = "IRAnalogNoiseLobbyRouteDetail";
    private const string LobbyRouteBuildName = "IRAnalogNoiseLobbyRouteBuild";
    private const string LobbyRouteProgressName = "IRAnalogNoiseLobbyRouteProgress";
    private const string LobbyRouteRowPrefix = "IRAnalogNoiseLobbyRouteRow_";
    private const string LobbyRouteLinePrefix = "IRAnalogNoiseLobbyRouteLine_";
    private const string LobbyCtaDockName = "IRAnalogNoiseLobbyCtaDock";
    private const string LobbyCtaCaptionName = "IRAnalogNoiseLobbyCtaCaption";
    private const string LobbyPromptName = "IRAnalogNoisePrompt";
    private const string ModalScanlineName = "IRAnalogNoiseModalScanlines";
    private const float ThinOutline = 1f;

    private static Sprite panelSprite;
    private static Sprite monitorSprite;
    private static Sprite slotSprite;
    private static Sprite buttonSprite;
    private static Texture2D backdropTexture;
    private static Texture2D scanlineTexture;
    private static Texture2D threatTexture;

    public static readonly Color BackdropColor = new(0f, 0f, 0f, 0.86f);
    public static readonly Color PanelColor = new(0.05f, 0.05f, 0.05f, 0.9f);
    public static readonly Color BorderColor = new(0.31f, 0.28f, 0.24f, 0.95f);
    public static readonly Color GlassColor = new(0.03f, 0.035f, 0.04f, 0.94f);
    public static readonly Color AmberColor = new(0.88f, 0.7f, 0.34f, 1f);
    public static readonly Color AmberSoftColor = new(0.74f, 0.6f, 0.28f, 0.92f);
    public static readonly Color WarningRed = new(0.8f, 0.24f, 0.22f, 1f);
    public static readonly Color WarningRedSoft = new(0.54f, 0.17f, 0.16f, 0.92f);
    public static readonly Color CyanColor = new(0.45f, 0.82f, 0.86f, 1f);
    public static readonly Color CyanSoftColor = new(0.31f, 0.62f, 0.67f, 0.92f);
    public static readonly Color TextColor = new(0.86f, 0.83f, 0.74f, 1f);
    public static readonly Color MutedTextColor = new(0.67f, 0.65f, 0.58f, 0.94f);
    public static readonly Color BlackButtonColor = new(0.05f, 0.05f, 0.05f, 0.96f);

    public static bool IsEnabled(Component context)
    {
        return IsEnabled(UiSettingsOwner.Resolve(context));
    }

    public static bool IsEnabled(IUiSettingsReadModel readModel)
    {
        return readModel != null && readModel.UseTemporaryAnalogNoiseUi;
    }

    public static void ApplyHudCanvasTheme(IRHudCanvas hudCanvas)
    {
        if (!IsEnabled(hudCanvas) || hudCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = hudCanvas.transform as RectTransform;
        RectTransform panelRoot = hudCanvas.PanelRoot;

        if (canvasRect == null || panelRoot == null)
        {
            return;
        }

        EnsureBackdrop(canvasRect, panelRoot.GetSiblingIndex());
        StylePanelRoot(panelRoot, GetPanelSprite(), PanelColor);
    }

    public static void ApplyLobbyTheme(IRLobbyController controller)
    {
        if (!IsEnabled(controller) || controller == null)
        {
            return;
        }

        RectTransform canvasRect = controller.GetComponentInParent<Canvas>()?.transform as RectTransform;
        RectTransform panelRect = controller.PanelRoot;

        if (canvasRect == null || panelRect == null)
        {
            return;
        }

        EnsureBackdrop(canvasRect, panelRect.GetSiblingIndex());

        RRunSnapshot snapshot = controller.RunSessionController != null
            ? controller.RunSessionController.Snapshot
            : default;
        int entryFloorNumber = controller.RunSessionController != null
            ? controller.RunSessionController.StartingFloorNumber
            : 5;
        int terminalFloorNumber = controller.RunSessionController != null
            ? controller.RunSessionController.TerminalFloorNumber
            : 1;
        int routeFloorCount = controller.RunSessionController != null
            ? controller.RunSessionController.RouteFloorCount
            : Mathf.Max(1, entryFloorNumber);
        string gameplaySceneName = controller.RunSessionController != null
            ? CompactSceneName(controller.RunSessionController.GameplayScenePath)
            : "SCENE UNBOUND";
        string statusLabel = ResolveLobbyStatusLabel(snapshot);
        string statusDetail = ResolveLobbyStatusDetail(snapshot, entryFloorNumber, terminalFloorNumber, routeFloorCount);
        string directiveText = ResolveDirectiveText(snapshot, entryFloorNumber, terminalFloorNumber);
        string directiveTags = ResolveDirectiveTags(snapshot);
        string ctaCaption = ResolveCtaCaption(snapshot, entryFloorNumber);

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1280f, 808f);
        panelRect.anchoredPosition = new Vector2(0f, -2f);

        StylePanelRoot(panelRect, GetMonitorSprite(), new Color(0.155f, 0.145f, 0.13f, 0.99f));
        EnsureScanlineOverlay(panelRect, ModalScanlineName, 0.035f);

        RawImage threatStrip = EnsureRawImage(panelRect, LobbyThreatStripName, GetThreatTexture(), new Color(1f, 1f, 1f, 0.72f));
        ConfigureRect(threatStrip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 20f));

        TextMeshProUGUI headerText = EnsureText(
            panelRect,
            LobbyHeaderName,
            controller.ResolveThemeFont(),
            "EMERGENCY ROUTE",
            62f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        RectTransform headerRect = headerText.rectTransform;
        ConfigureRect(headerRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -76f), new Vector2(760f, 68f));
        StyleText(headerText, AmberColor, 1f, 2f, true);

        TextMeshProUGUI subheaderText = EnsureText(
            panelRect,
            LobbySubheaderName,
            controller.ResolveThemeFont(),
            "R-LOOP SURVIVAL TERMINAL // SUBMISSION BUILD",
            17f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        RectTransform subheaderRect = subheaderText.rectTransform;
        ConfigureRect(subheaderRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -38f), new Vector2(760f, 24f));
        StyleText(subheaderText, CyanColor, 0.94f, 2f, false);

        TextMeshProUGUI systemTagText = EnsureText(
            panelRect,
            LobbySystemTagName,
            controller.ResolveThemeFont(),
            $"TARGET SCENE // {gameplaySceneName}",
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Right);
        RectTransform systemTagRect = systemTagText.rectTransform;
        ConfigureRect(systemTagRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-58f, -48f), new Vector2(420f, 22f));
        StyleText(systemTagText, TextColor, 0.9f, 1f, false);

        RectTransform statusPlate = EnsureImage(panelRect, LobbyStatusPlateName, GetPanelSprite(), new Color(0.05f, 0.05f, 0.052f, 0.99f)).rectTransform;
        ConfigureRect(statusPlate, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -148f), new Vector2(820f, 342f));
        EnsureScanlineOverlay(statusPlate, ModalScanlineName, 0.055f);

        Image statusAccent = EnsureImage(statusPlate, "IRAnalogNoiseLobbyStatusAccent", GetGaugeSprite(), AmberColor);
        ConfigureRect(statusAccent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(12f, -24f));

        TextMeshProUGUI summaryLabelText = EnsureText(
            statusPlate,
            LobbySummaryLabelName,
            controller.ResolveThemeFont(),
            "MISSION BRIEF",
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(summaryLabelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -24f), new Vector2(220f, 20f));
        StyleText(summaryLabelText, CyanColor, 0.9f, 1f, false);

        TextMeshProUGUI summaryStatusText = EnsureText(
            statusPlate,
            LobbySummaryStatusName,
            controller.ResolveThemeFont(),
            statusLabel,
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Right);
        ConfigureRect(summaryStatusText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -24f), new Vector2(220f, 24f));
        StyleText(summaryStatusText, snapshot.Outcome == RRunOutcome.Failed ? WarningRed : AmberColor, 1f, 1f, false);

        TextMeshProUGUI summaryDetailText = EnsureText(
            statusPlate,
            LobbySummaryDetailName,
            controller.ResolveThemeFont(),
            statusDetail,
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.TopRight);
        RectTransform summaryDetailRect = summaryDetailText.rectTransform;
        ConfigureRect(summaryDetailRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -58f), new Vector2(252f, 50f));
        summaryDetailText.textWrappingMode = TextWrappingModes.Normal;
        summaryDetailText.lineSpacing = -8f;
        StyleText(summaryDetailText, TextColor, 0.72f, 0f, false);

        TextMeshProUGUI summaryTitleText = controller.SummaryTitleText;
        if (summaryTitleText != null)
        {
            summaryTitleText.rectTransform.SetParent(statusPlate, false);
            ConfigureRect(summaryTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(34f, -78f), new Vector2(-300f, 50f));
            summaryTitleText.fontSize = 34f;
            StyleText(summaryTitleText, AmberColor, 1f, 1f, true);
        }

        TextMeshProUGUI summaryBodyText = controller.SummaryBodyText;
        if (summaryBodyText != null)
        {
            summaryBodyText.rectTransform.SetParent(statusPlate, false);
            ConfigureRect(summaryBodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(34f, -138f), new Vector2(-72f, 110f));
            summaryBodyText.textWrappingMode = TextWrappingModes.Normal;
            summaryBodyText.lineSpacing = -2f;
            summaryBodyText.fontSize = 22f;
            StyleText(summaryBodyText, TextColor, 0.95f, 0f, false);
        }

        ApplyHeartbeatPreview(statusPlate, controller);

        RectTransform directivePanel = EnsureImage(panelRect, LobbyDirectivePanelName, GetPanelSprite(), new Color(0.042f, 0.046f, 0.05f, 0.99f)).rectTransform;
        ConfigureRect(directivePanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -518f), new Vector2(820f, 152f));

        TextMeshProUGUI directiveLabelText = EnsureText(
            directivePanel,
            LobbyDirectiveLabelName,
            controller.ResolveThemeFont(),
            "FIELD DIRECTIVES",
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(directiveLabelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(240f, 20f));
        StyleText(directiveLabelText, CyanColor, 0.88f, 1f, false);

        TextMeshProUGUI directiveBodyText = EnsureText(
            directivePanel,
            LobbyDirectiveBodyName,
            controller.ResolveThemeFont(),
            directiveText,
            20f,
            FontStyles.Bold,
            TextAlignmentOptions.TopLeft);
        RectTransform directiveBodyRect = directiveBodyText.rectTransform;
        ConfigureRect(directiveBodyRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(28f, -54f), new Vector2(-56f, 84f));
        directiveBodyText.textWrappingMode = TextWrappingModes.Normal;
        directiveBodyText.lineSpacing = -2f;
        StyleText(directiveBodyText, TextColor, 0.92f, 0f, false);

        TextMeshProUGUI directiveTagsText = EnsureText(
            directivePanel,
            LobbyDirectiveTagsName,
            controller.ResolveThemeFont(),
            directiveTags,
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(directiveTagsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(28f, 16f), new Vector2(-56f, 18f));
        StyleText(directiveTagsText, AmberSoftColor, 0.9f, 1f, false);

        RectTransform routePanel = EnsureImage(panelRect, LobbyRoutePanelName, GetPanelSprite(), new Color(0.048f, 0.05f, 0.053f, 0.995f)).rectTransform;
        ConfigureRect(routePanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-58f, -148f), new Vector2(330f, 522f));
        EnsureScanlineOverlay(routePanel, ModalScanlineName, 0.05f);

        TextMeshProUGUI routeHeaderText = EnsureText(
            routePanel,
            LobbyRouteHeaderName,
            controller.ResolveThemeFont(),
            "DESCENT ROUTE",
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(routeHeaderText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(240f, 32f));
        StyleText(routeHeaderText, AmberColor, 1f, 1f, true);

        TextMeshProUGUI routeSubheaderText = EnsureText(
            routePanel,
            LobbyRouteSubheaderName,
            controller.ResolveThemeFont(),
            ResolveRouteSubheader(entryFloorNumber, terminalFloorNumber),
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(routeSubheaderText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -64f), new Vector2(-48f, 18f));
        StyleText(routeSubheaderText, TextColor, 0.7f, 1f, false);

        int visibleRowIndex = 0;

        for (int floorNumber = 5; floorNumber >= 1; floorNumber--)
        {
            string rowName = $"{LobbyRouteRowPrefix}{floorNumber}";
            RectTransform rowRect = EnsureImage(routePanel, rowName, GetSlotSprite(), Color.clear).rectTransform;
            bool rowVisible = floorNumber <= entryFloorNumber && floorNumber >= terminalFloorNumber;
            rowRect.gameObject.SetActive(rowVisible);

            Image routeLine = null;

            if (floorNumber > 1)
            {
                routeLine = EnsureImage(routePanel, $"{LobbyRouteLinePrefix}{floorNumber}", GetGaugeSprite(), Color.clear);
            }

            bool lineVisible = rowVisible && floorNumber > terminalFloorNumber && routeLine != null;

            if (routeLine != null)
            {
                routeLine.gameObject.SetActive(lineVisible);
            }

            if (!rowVisible)
            {
                continue;
            }

            ResolveRouteRowVisual(
                snapshot,
                floorNumber,
                entryFloorNumber,
                terminalFloorNumber,
                out string rowState,
                out Color rowFill,
                out Color accentColor,
                out Color labelColor);
            ConfigureRect(rowRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -108f - (visibleRowIndex * 66f)), new Vector2(282f, 52f));
            rowRect.GetComponent<Image>().color = rowFill;

            Image accent = EnsureImage(rowRect, $"{rowName}_Accent", GetGaugeSprite(), accentColor);
            ConfigureRect(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(10f, 30f));

            TextMeshProUGUI rowLabelText = EnsureText(
                rowRect,
                $"{rowName}_Label",
                controller.ResolveThemeFont(),
                $"FLOOR 0{floorNumber}",
                19f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            ConfigureRect(rowLabelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(144f, 24f));
            StyleText(rowLabelText, labelColor, 1f, 1f, false);

            TextMeshProUGUI rowStateText = EnsureText(
                rowRect,
                $"{rowName}_State",
                controller.ResolveThemeFont(),
                rowState,
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Right);
            ConfigureRect(rowStateText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(112f, 20f));
            StyleText(rowStateText, accentColor, 1f, 1f, false);

            if (routeLine != null && lineVisible)
            {
                routeLine.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.44f);
                ConfigureRect(routeLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -166f - (visibleRowIndex * 66f)), new Vector2(4f, 16f));
            }

            visibleRowIndex++;
        }

        TextMeshProUGUI routeStatusText = EnsureText(
            routePanel,
            LobbyRouteStatusName,
            controller.ResolveThemeFont(),
            ResolveThreatLabel(snapshot),
            15f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(routeStatusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 72f), new Vector2(-48f, 18f));
        StyleText(routeStatusText, snapshot.Outcome == RRunOutcome.Failed ? WarningRed : CyanColor, 0.95f, 1f, false);

        TextMeshProUGUI routeDetailText = EnsureText(
            routePanel,
            LobbyRouteDetailName,
            controller.ResolveThemeFont(),
            ResolveRouteDetail(entryFloorNumber, terminalFloorNumber),
            13f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(routeDetailText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 46f), new Vector2(-48f, 16f));
        StyleText(routeDetailText, TextColor, 0.62f, 1f, false);

        TextMeshProUGUI routeBuildText = EnsureText(
            routePanel,
            LobbyRouteBuildName,
            controller.ResolveThemeFont(),
            $"TARGET SCENE // {gameplaySceneName}",
            13f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(routeBuildText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(-48f, 16f));
        StyleText(routeBuildText, AmberSoftColor, 0.88f, 1f, false);

        TextMeshProUGUI routeProgressText = EnsureText(
            routePanel,
            LobbyRouteProgressName,
            controller.ResolveThemeFont(),
            ResolveRouteProgressText(snapshot, routeFloorCount),
            13f,
            FontStyles.Bold,
            TextAlignmentOptions.Right);
        ConfigureRect(routeProgressText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 22f), new Vector2(120f, 16f));
        StyleText(routeProgressText, TextColor, 0.84f, 1f, false);

        RectTransform ctaDock = EnsureImage(panelRect, LobbyCtaDockName, GetPanelSprite(), new Color(0.055f, 0.055f, 0.05f, 0.99f)).rectTransform;
        ConfigureRect(ctaDock, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1164f, 138f));

        TextMeshProUGUI ctaCaptionText = EnsureText(
            ctaDock,
            LobbyCtaCaptionName,
            controller.ResolveThemeFont(),
            ctaCaption,
            32f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(ctaCaptionText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -20f), new Vector2(520f, 34f));
        StyleText(ctaCaptionText, snapshot.Outcome == RRunOutcome.Failed ? WarningRed : AmberColor, 1f, 2f, true);

        TextMeshProUGUI footerHintText = controller.FooterHintText;
        if (footerHintText != null)
        {
            footerHintText.rectTransform.SetParent(ctaDock, false);
            ConfigureRect(footerHintText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 16f), new Vector2(600f, 18f));
            footerHintText.alignment = TextAlignmentOptions.Left;
            footerHintText.fontSize = 14f;
            StyleText(footerHintText, TextColor, 0.62f, 1f, false);
        }

        if (controller.StartRunButton != null)
        {
            controller.StartRunButton.transform.SetParent(ctaDock, false);
        }

        if (controller.QuitButton != null)
        {
            controller.QuitButton.transform.SetParent(ctaDock, false);
        }

        StyleLobbyButton(controller.StartRunButton, true, new Vector2(336f, 24f));
        StyleLobbyButton(controller.QuitButton, false, new Vector2(548f, 24f));

        TextMeshProUGUI promptText = EnsureText(
            ctaDock,
            LobbyPromptName,
            controller.ResolveThemeFont(),
            "ENTER / SPACE / S TO LAUNCH     ESC / Q TO BACK OUT",
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        RectTransform promptRect = promptText.rectTransform;
        ConfigureRect(promptRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 52f), new Vector2(640f, 18f));
        StyleText(promptText, AmberSoftColor, 0.95f, 1f, false);
    }

    public static void ApplyGameClearTheme(
        RectTransform panelRoot,
        Image backdropImage,
        Image panelImage,
        TextMeshProUGUI titleText,
        TextMeshProUGUI bodyText,
        TextMeshProUGUI promptText,
        IRRunModalMode mode)
    {
        if (!IsEnabled(panelRoot))
        {
            return;
        }

        if (backdropImage != null)
        {
            backdropImage.color = new Color(0f, 0f, 0f, 0.9f);
        }

        if (panelImage != null)
        {
            panelImage.sprite = GetMonitorSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = mode == IRRunModalMode.Failure
                ? new Color(0.17f, 0.07f, 0.07f, 0.97f)
                : new Color(0.13f, 0.12f, 0.11f, 0.97f);
        }

        EnsureScanlineOverlay(panelRoot, ModalScanlineName, 0.2f);
        StyleText(titleText, mode == IRRunModalMode.Failure ? WarningRed : AmberColor, 0.95f, 3f, true);
        StyleText(bodyText, mode == IRRunModalMode.Failure ? new Color(0.93f, 0.78f, 0.76f, 1f) : TextColor, 0.9f, 1f, false);
        StyleText(promptText, mode == IRRunModalMode.Failure ? new Color(0.93f, 0.7f, 0.66f, 1f) : AmberSoftColor, 0.8f, 2f, false);
    }

    public static void ApplyHealthPanelTheme(IRHealthPanelView view, in HealthPanelPresentation presentation)
    {
        if (!IsEnabled(view) || view == null)
        {
            return;
        }

        StylePanelRoot(view.PanelRoot, GetPanelSprite(), new Color(0.06f, 0.06f, 0.05f, 0.92f));
        StyleGaugeTrack(view.BatteryFillImage);
        StyleGaugeTrack(view.FillImage);

        float batteryNormalized = Mathf.Clamp01(presentation.FlashlightChargeNormalized);
        float healthNormalized = presentation.MaxHealth <= 0 ? 0f : Mathf.Clamp01(presentation.CurrentHealth / (float)presentation.MaxHealth);

        StyleText(view.BatteryValueText, batteryNormalized <= 0.2f ? WarningRed : AmberColor, 0.92f, 1.5f, false);
        StyleText(view.ValueText, presentation.CurrentHealth <= 1 ? WarningRed : AmberColor, 0.92f, 1.5f, false);

        if (view.BatteryFillImage != null)
        {
            view.BatteryFillImage.sprite = GetGaugeSprite();
            view.BatteryFillImage.type = Image.Type.Sliced;
            view.BatteryFillImage.color = Color.Lerp(new Color(0.47f, 0.24f, 0.09f, 1f), AmberColor, batteryNormalized);
        }

        if (view.FillImage != null)
        {
            view.FillImage.sprite = GetGaugeSprite();
            view.FillImage.type = Image.Type.Sliced;
            view.FillImage.color = Color.Lerp(new Color(0.35f, 0.1f, 0.09f, 1f), WarningRed, healthNormalized);
        }

        if (view.PulseOverlay != null)
        {
            view.PulseOverlay.sprite = GetGaugeSprite();
            view.PulseOverlay.type = Image.Type.Sliced;
            view.PulseOverlay.color = new Color(1f, 0.9f, 0.74f, presentation.RecoveryNormalized > 0.001f ? 0.22f : 0f);
        }
    }

    public static void ApplyInventoryPanelTheme(RectTransform panelRoot, TextMeshProUGUI batterySummaryText, TextMeshProUGUI infoSummaryText)
    {
        if (!IsEnabled(panelRoot))
        {
            return;
        }

        StylePanelRoot(panelRoot, GetPanelSprite(), new Color(0.05f, 0.05f, 0.05f, 0.94f));
        StyleText(batterySummaryText, AmberColor, 0.92f, 1.5f, false);
        StyleText(infoSummaryText, MutedTextColor, 0.78f, 1f, false);
    }

    public static void ApplyInventorySlotTheme(
        Image frame,
        Image icon,
        TextMeshProUGUI label,
        TextMeshProUGUI quantity,
        Image quickSlotBadge,
        TextMeshProUGUI quickSlotText,
        Image equippedBorder,
        bool hasItem,
        bool hasQuantity,
        bool equipped,
        IRUiItemSemantic itemSemantic)
    {
        if (!IsEnabled(frame != null ? frame.transform as Component : icon))
        {
            return;
        }

        if (frame != null)
        {
            frame.sprite = GetSlotSprite();
            frame.type = Image.Type.Sliced;
            frame.color = equipped
                ? new Color(0.16f, 0.13f, 0.11f, 0.97f)
                : hasItem
                    ? new Color(0.08f, 0.08f, 0.07f, 0.95f)
                    : new Color(0.05f, 0.05f, 0.05f, 0.9f);
        }

        if (icon != null && hasItem)
        {
            icon.color = itemSemantic == IRUiItemSemantic.Medkit
                ? new Color(0.95f, 0.82f, 0.82f, hasQuantity ? 0.98f : 0.44f)
                : itemSemantic == IRUiItemSemantic.Flashlight
                    ? new Color(1f, 0.94f, 0.72f, hasQuantity ? 0.98f : 0.44f)
                : itemSemantic == IRUiItemSemantic.FlashlightBattery
                    ? new Color(1f, 0.88f, 0.58f, hasQuantity ? 0.98f : 0.44f)
                    : new Color(0.84f, 0.88f, 0.92f, hasQuantity ? 0.98f : 0.44f);
        }

        StyleText(label, TextColor, hasItem ? 0.86f : 0.34f, 1f, false);
        StyleText(quantity, AmberColor, hasItem && hasQuantity ? 1f : 0.3f, 1.5f, false);

        if (quickSlotBadge != null)
        {
            quickSlotBadge.sprite = GetSlotSprite();
            quickSlotBadge.type = Image.Type.Sliced;
            quickSlotBadge.color = new Color(0.06f, 0.06f, 0.06f, 0.96f);
        }

        StyleText(quickSlotText, AmberSoftColor, 0.9f, 0f, false);

        if (equippedBorder != null)
        {
            equippedBorder.sprite = GetSlotSprite();
            equippedBorder.type = Image.Type.Sliced;
        }
    }

    public static void ApplyQuickSlotsPanelTheme(RectTransform panelRoot)
    {
        if (!IsEnabled(panelRoot))
        {
            return;
        }

        StylePanelRoot(panelRoot, GetPanelSprite(), new Color(0.05f, 0.05f, 0.05f, 0.94f));
    }

    public static void ApplyQuickSlotTheme(
        Image frame,
        Image accent,
        Image icon,
        TextMeshProUGUI keyText,
        TextMeshProUGUI itemLabel,
        TextMeshProUGUI quantityText,
        Image equippedBorder,
        bool configured,
        bool hasQuantity,
        bool equipped,
        IRUiItemSemantic itemSemantic)
    {
        if (!IsEnabled(frame != null ? frame.transform as Component : icon))
        {
            return;
        }

        if (frame != null)
        {
            frame.sprite = GetSlotSprite();
            frame.type = Image.Type.Sliced;
            frame.color = equipped
                ? new Color(0.14f, 0.11f, 0.09f, 1f)
                : configured
                    ? new Color(0.07f, 0.07f, 0.06f, 0.96f)
                    : new Color(0.04f, 0.04f, 0.04f, 0.78f);
        }

        if (accent != null)
        {
            accent.sprite = GetGaugeSprite();
            accent.type = Image.Type.Sliced;
            accent.color = itemSemantic == IRUiItemSemantic.Medkit
                ? WarningRedSoft
                : itemSemantic == IRUiItemSemantic.Flashlight
                    ? new Color(0.92f, 0.78f, 0.42f, configured && hasQuantity ? 0.96f : 0.42f)
                : itemSemantic == IRUiItemSemantic.FlashlightBattery
                    ? AmberSoftColor
                    : new Color(0.27f, 0.27f, 0.23f, configured && hasQuantity ? 0.92f : 0.36f);
        }

        if (icon != null && configured)
        {
            icon.color = itemSemantic == IRUiItemSemantic.Medkit
                ? new Color(0.95f, 0.82f, 0.82f, hasQuantity ? 0.98f : 0.42f)
                : itemSemantic == IRUiItemSemantic.Flashlight
                    ? new Color(1f, 0.94f, 0.72f, hasQuantity ? 0.98f : 0.42f)
                : itemSemantic == IRUiItemSemantic.FlashlightBattery
                    ? new Color(0.98f, 0.88f, 0.58f, hasQuantity ? 0.98f : 0.42f)
                    : new Color(0.86f, 0.9f, 0.95f, hasQuantity ? 0.98f : 0.42f);
        }

        StyleText(keyText, MutedTextColor, 0.6f, 0f, false);
        StyleText(itemLabel, MutedTextColor, 0.66f, 0.8f, false);
        StyleText(quantityText, AmberColor, configured && hasQuantity ? 1f : 0.32f, 1.5f, false);

        if (equippedBorder != null)
        {
            equippedBorder.sprite = GetSlotSprite();
            equippedBorder.type = Image.Type.Sliced;
        }
    }

    public static void ApplyThreatPanelTheme(RectTransform panelRoot, CanvasGroup canvasGroup, RawImage top, RawImage bottom, RawImage left, RawImage right)
    {
        if (!IsEnabled(panelRoot))
        {
            return;
        }

        if (panelRoot != null)
        {
            EnsureScanlineOverlay(panelRoot, HudScanlineName, 0.06f);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Max(canvasGroup.alpha, 0f);
        }

        ApplyThreatEdge(top);
        ApplyThreatEdge(bottom);
        ApplyThreatEdge(left);
        ApplyThreatEdge(right);
    }

    public static void StyleText(TextMeshProUGUI text, Color color, float alphaMultiplier, float spacing, bool outline)
    {
        if (text == null)
        {
            return;
        }

        Color applied = color;
        applied.a *= Mathf.Clamp01(alphaMultiplier);
        text.color = applied;
        text.characterSpacing = spacing;
        text.fontStyle = FontStyles.Bold;

        Outline outlineComponent = EnsureOutline(text);
        outlineComponent.enabled = outline;
        outlineComponent.effectDistance = new Vector2(ThinOutline, -ThinOutline);
        outlineComponent.effectColor = outline ? new Color(0f, 0f, 0f, 0.88f) : Color.clear;

        Shadow shadow = EnsureShadow(text);
        shadow.effectDistance = new Vector2(0f, -1f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
    }

    private static string ResolveLobbyStatusLabel(in RRunSnapshot snapshot)
    {
        return snapshot.Outcome switch
        {
            RRunOutcome.Cleared => "ROUTE VERIFIED",
            RRunOutcome.Failed => "CONTACT LOST",
            RRunOutcome.InProgress when snapshot.HasActiveRun => "LIVE RUN",
            _ => "READY TO DEPLOY"
        };
    }

    private static string ResolveLobbyStatusDetail(
        in RRunSnapshot snapshot,
        int entryFloorNumber,
        int terminalFloorNumber,
        int routeFloorCount)
    {
        return snapshot.Outcome switch
        {
            RRunOutcome.Cleared => terminalFloorNumber <= 1
                ? $"{entryFloorNumber}F TO STREET VERIFIED\nSUBMISSION ROUTE CONFIRMED"
                : $"{entryFloorNumber}F TO {terminalFloorNumber}F VERIFIED\nSUBMISSION ROUTE CONFIRMED",
            RRunOutcome.Failed => string.IsNullOrWhiteSpace(snapshot.FailureSource)
                ? $"LAST CONTACT {Mathf.Max(1, snapshot.CurrentFloorNumber)}F\nRESET AND REDEPLOY"
                : $"LAST CONTACT {Mathf.Max(1, snapshot.CurrentFloorNumber)}F\n{snapshot.FailureSource.ToUpperInvariant()}",
            RRunOutcome.InProgress when snapshot.HasActiveRun => $"CURRENT FLOOR {Mathf.Max(1, snapshot.CurrentFloorNumber)}F\n{Mathf.Clamp(snapshot.FloorsCleared, 0, routeFloorCount)} FLOORS SECURED",
            _ => terminalFloorNumber <= 1
                ? $"ENTRY FLOOR {entryFloorNumber}F\nSTREET EXIT LOCKED"
                : $"ENTRY FLOOR {entryFloorNumber}F\n{terminalFloorNumber}F EXTRACTION LOCKED"
        };
    }

    private static string ResolveDirectiveText(in RRunSnapshot snapshot, int entryFloorNumber, int terminalFloorNumber)
    {
        string routeDescriptor = terminalFloorNumber <= 1
            ? $"{entryFloorNumber}F TO STREET"
            : $"{entryFloorNumber}F TO {terminalFloorNumber}F";

        return snapshot.Outcome switch
        {
            RRunOutcome.Cleared => "EXTRACTION WINDOW IS OPEN.\nREVIEW THE DESCENT, LOCK THE BUILD, AND PREPARE THE NEXT PASS.",
            RRunOutcome.Failed => "LAST DESCENT BROKE UNDER CONTACT.\nCUT THE NOISE, RECLAIM THE ROUTE, AND PUSH THE STAIRS FAST.",
            RRunOutcome.InProgress when snapshot.HasActiveRun => "A LIVE RUN IS STILL BOUND TO THIS TERMINAL.\nRECOVER THE CURRENT FLOOR AND KEEP THE EXIT SIGNAGE IN SIGHT.",
            _ => $"DESCEND FROM {routeDescriptor} WITHOUT LOSING THE ROUTE.\nUSE LIGHT SPARINGLY AND TREAT EVERY CORNER LIKE A LISTEN CHECK."
        };
    }

    private static string ResolveDirectiveTags(in RRunSnapshot snapshot)
    {
        return snapshot.Outcome switch
        {
            RRunOutcome.Cleared => "SUBMISSION READY // ROUTE VERIFIED // STREET OPEN",
            RRunOutcome.Failed => "NOISE DISCIPLINE // CONTACT RECOVERY // FAST REDEPLOY",
            RRunOutcome.InProgress when snapshot.HasActiveRun => "LIVE TELEMETRY // FLOOR HOLD // BATTERY TRIAGE",
            _ => "STAIRWELL PRIORITY // LOW-LIGHT ADVANCE // EVADE OVER FIGHT"
        };
    }

    private static string ResolveThreatLabel(in RRunSnapshot snapshot)
    {
        return snapshot.Outcome switch
        {
            RRunOutcome.Cleared => "THREAT STATE // LOW",
            RRunOutcome.Failed => "THREAT STATE // BREACHED",
            RRunOutcome.InProgress when snapshot.HasActiveRun => "THREAT STATE // TRACKING",
            _ => "THREAT STATE // UNKNOWN"
        };
    }

    private static string ResolveRouteProgressText(in RRunSnapshot snapshot, int routeFloorCount)
    {
        int securedFloors = snapshot.WasSuccessful
            ? Mathf.Max(1, routeFloorCount)
            : Mathf.Clamp(snapshot.FloorsCleared, 0, Mathf.Max(1, routeFloorCount));
        return $"{securedFloors}/{Mathf.Max(1, routeFloorCount)} SECURED";
    }

    private static string ResolveCtaCaption(in RRunSnapshot snapshot, int entryFloorNumber)
    {
        return snapshot.Outcome switch
        {
            RRunOutcome.Cleared => "RUN ARCHIVED // READY FOR REVIEW",
            RRunOutcome.Failed => "REDEPLOY IMMEDIATELY",
            RRunOutcome.InProgress when snapshot.HasActiveRun => "RESUME LIVE DESCENT",
            _ => $"LAUNCH SUBMISSION ROUTE // {entryFloorNumber}F"
        };
    }

    private static string ResolveRouteSubheader(int entryFloorNumber, int terminalFloorNumber)
    {
        return terminalFloorNumber <= 1
            ? $"SUBMISSION ROUTE // {entryFloorNumber}F ENTRY // STREET EXTRACTION"
            : $"SUBMISSION ROUTE // {entryFloorNumber}F ENTRY // {terminalFloorNumber}F EXTRACTION";
    }

    private static string ResolveRouteDetail(int entryFloorNumber, int terminalFloorNumber)
    {
        return terminalFloorNumber <= 1
            ? $"ENTRY {entryFloorNumber}F // MOVE BY STAIRWELL // EXIT STREET"
            : $"ENTRY {entryFloorNumber}F // MOVE BY STAIRWELL // EXIT {terminalFloorNumber}F";
    }

    private static string CompactSceneName(string gameplayScenePath)
    {
        if (string.IsNullOrWhiteSpace(gameplayScenePath))
        {
            return "SCENE UNBOUND";
        }

        return Path.GetFileNameWithoutExtension(gameplayScenePath).ToUpperInvariant();
    }

    private static void ResolveRouteRowVisual(
        in RRunSnapshot snapshot,
        int floorNumber,
        int entryFloorNumber,
        int terminalFloorNumber,
        out string rowState,
        out Color rowFill,
        out Color accentColor,
        out Color labelColor)
    {
        int routePosition = Mathf.Max(1, entryFloorNumber - floorNumber + 1);
        bool clearedFloor = snapshot.WasSuccessful || snapshot.FloorsCleared >= routePosition;
        bool failureFloor = snapshot.Outcome == RRunOutcome.Failed && Mathf.Max(1, snapshot.CurrentFloorNumber) == floorNumber;
        bool activeFloor = snapshot.Outcome == RRunOutcome.InProgress && snapshot.HasActiveRun && Mathf.Max(1, snapshot.CurrentFloorNumber) == floorNumber;
        bool idleEntryFloor = !snapshot.RunStarted && floorNumber == entryFloorNumber;
        bool exitFloor = floorNumber == terminalFloorNumber;

        rowState = "LOCKED";
        rowFill = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        accentColor = new Color(0.22f, 0.22f, 0.2f, 0.75f);
        labelColor = MutedTextColor;

        if (snapshot.WasSuccessful && exitFloor)
        {
            rowState = "EXIT OPEN";
            rowFill = new Color(0.06f, 0.11f, 0.11f, 0.96f);
            accentColor = CyanColor;
            labelColor = TextColor;
            return;
        }

        if (failureFloor)
        {
            rowState = "CONTACT";
            rowFill = new Color(0.12f, 0.06f, 0.06f, 0.96f);
            accentColor = WarningRed;
            labelColor = new Color(0.96f, 0.82f, 0.8f, 1f);
            return;
        }

        if (activeFloor)
        {
            rowState = "CURRENT";
            rowFill = new Color(0.12f, 0.09f, 0.06f, 0.97f);
            accentColor = AmberColor;
            labelColor = TextColor;
            return;
        }

        if (clearedFloor)
        {
            rowState = exitFloor ? "OPEN" : "SECURED";
            rowFill = new Color(0.06f, 0.1f, 0.1f, 0.95f);
            accentColor = CyanSoftColor;
            labelColor = TextColor;
            return;
        }

        if (idleEntryFloor)
        {
            rowState = "ENTRY";
            rowFill = new Color(0.1f, 0.08f, 0.06f, 0.96f);
            accentColor = AmberSoftColor;
            labelColor = TextColor;
            return;
        }

        if (exitFloor)
        {
            rowState = terminalFloorNumber <= 1 ? "STREET" : "EXIT";
            rowFill = new Color(0.06f, 0.1f, 0.1f, 0.95f);
            accentColor = CyanSoftColor;
            labelColor = TextColor;
            return;
        }
    }

    private static void StyleLobbyButton(Button button, bool primary, Vector2 anchoredPosition)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(primary ? 236f : 176f, primary ? 64f : 54f);
            rect.anchoredPosition = anchoredPosition;
        }

        Image buttonImage = button.GetComponent<Image>();

        if (buttonImage != null)
        {
            buttonImage.sprite = GetButtonSprite();
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = primary ? new Color(0.58f, 0.19f, 0.15f, 0.99f) : new Color(0.045f, 0.045f, 0.045f, 0.96f);
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        StyleText(label, primary ? new Color(1f, 0.91f, 0.84f, 1f) : AmberColor, 0.98f, 3f, false);
        if (label != null)
        {
            label.fontSize = primary ? 24f : 19f;
        }
    }

    private static void StyleGaugeTrack(Image fillImage)
    {
        if (fillImage == null)
        {
            return;
        }

        Image track = fillImage.transform.parent != null ? fillImage.transform.parent.GetComponent<Image>() : null;

        if (track != null)
        {
            track.sprite = GetPanelSprite();
            track.type = Image.Type.Sliced;
            track.color = new Color(0.07f, 0.07f, 0.06f, 0.96f);
        }
    }

    private static void ApplyHeartbeatPreview(RectTransform statusPlate, IRLobbyController controller)
    {
        if (statusPlate == null || controller == null)
        {
            return;
        }

        IRHudHeartbeatGraphic heartbeatPreview = FindNamedComponent<IRHudHeartbeatGraphic>(controller.PanelRoot, "IRLobbyHeartbeatPreview");

        if (heartbeatPreview == null)
        {
            return;
        }

        SetNamedObjectActive(controller.transform.root, "RLobbyCardioSimPreview", false);

        RectTransform heartbeatPanel = EnsureImage(
            statusPlate,
            LobbyHeartbeatPanelName,
            GetSlotSprite(),
            new Color(0.045f, 0.03f, 0.032f, 0.98f)).rectTransform;
        ConfigureRect(
            heartbeatPanel,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(34f, 26f),
            new Vector2(-68f, 112f));

        Image heartbeatPanelImage = heartbeatPanel.GetComponent<Image>();
        if (heartbeatPanelImage != null)
        {
            heartbeatPanelImage.type = Image.Type.Sliced;
        }

        Outline panelOutline = EnsureOutline(heartbeatPanelImage);
        panelOutline.effectColor = new Color(0.22f, 0.08f, 0.08f, 0.92f);
        panelOutline.effectDistance = new Vector2(1f, -1f);
        panelOutline.enabled = true;

        Shadow panelShadow = EnsureShadow(heartbeatPanelImage);
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
        panelShadow.effectDistance = new Vector2(0f, -1f);
        panelShadow.enabled = true;

        RawImage glow = EnsureRawImage(
            heartbeatPanel,
            LobbyHeartbeatGlowName,
            GetThreatTexture(),
            new Color(0.8f, 0.16f, 0.14f, 0.09f));
        StretchWithOffsets(glow.rectTransform, new Vector2(14f, 12f), new Vector2(-14f, -12f));
        glow.uvRect = new Rect(0f, 0f, 3f, 1f);

        TextMeshProUGUI heartbeatLabelText = EnsureText(
            heartbeatPanel,
            LobbyHeartbeatLabelName,
            controller.ResolveThemeFont(),
            "CARDIO PREVIEW",
            15f,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
        ConfigureRect(
            heartbeatLabelText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -10f),
            new Vector2(220f, 18f));
        StyleText(heartbeatLabelText, WarningRed, 0.96f, 1f, false);

        TextMeshProUGUI heartbeatStatusText = EnsureText(
            heartbeatPanel,
            LobbyHeartbeatStatusName,
            controller.ResolveThemeFont(),
            "CARDIOSIM LOOP // PASSIVE TRACE",
            13f,
            FontStyles.Bold,
            TextAlignmentOptions.Right);
        ConfigureRect(
            heartbeatStatusText.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-18f, -12f),
            new Vector2(280f, 16f));
        StyleText(heartbeatStatusText, TextColor, 0.64f, 1f, false);

        IRHudHeartbeatGraphic heartbeatTrace = EnsureHeartbeatTraceGraphic(heartbeatPanel);
        RectTransform heartbeatRect = heartbeatTrace.rectTransform;
        StretchWithOffsets(heartbeatRect, new Vector2(18f, 14f), new Vector2(-18f, -30f));

        heartbeatTrace.color = new Color(0.86f, 0.18f, 0.16f, 1f);
        heartbeatTrace.Configure(heartbeatTrace.color, 3.4f, 0.76f, 0.72f);
        heartbeatTrace.raycastTarget = false;
        heartbeatPreview.raycastTarget = false;
    }

    private static void StylePanelRoot(RectTransform panelRoot, Sprite sprite, Color color)
    {
        if (panelRoot == null)
        {
            return;
        }

        Image panelImage = panelRoot.GetComponent<Image>();

        if (panelImage == null)
        {
            return;
        }

        panelImage.sprite = sprite;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = color;
    }

    private static Image EnsureImage(RectTransform parent, string objectName, Sprite sprite, Color color)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(Image));

        if (existing == null)
        {
            imageObject.transform.SetParent(parent, false);
        }

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI EnsureText(RectTransform parent, string objectName, TMP_FontAsset font, string content, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));

        if (existing == null)
        {
            textObject.transform.SetParent(parent, false);
        }

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void EnsureBackdrop(RectTransform canvasRect, int siblingIndex)
    {
        RawImage backdrop = EnsureRawImage(canvasRect, HudBackdropName, GetBackdropTexture(), new Color(1f, 1f, 1f, 0.94f));
        StretchFull(backdrop.rectTransform);
        backdrop.rectTransform.SetSiblingIndex(Mathf.Max(0, siblingIndex));

        RawImage scanlines = EnsureRawImage(canvasRect, HudScanlineName, GetScanlineTexture(), new Color(1f, 1f, 1f, 0.12f));
        StretchFull(scanlines.rectTransform);
        scanlines.rectTransform.SetSiblingIndex(Mathf.Max(0, siblingIndex + 1));
        scanlines.color = Color.clear;
        scanlines.enabled = false;
    }

    private static void EnsureScanlineOverlay(RectTransform parent, string objectName, float alpha)
    {
        if (parent == null)
        {
            return;
        }

        RawImage overlay = EnsureRawImage(parent, objectName, GetScanlineTexture(), new Color(1f, 1f, 1f, alpha));
        StretchFull(overlay.rectTransform);
        overlay.rectTransform.SetAsFirstSibling();
        overlay.color = Color.clear;
        overlay.enabled = false;
    }

    private static RawImage EnsureRawImage(RectTransform parent, string objectName, Texture texture, Color color)
    {
        Transform existing = parent.Find(objectName);
        GameObject rawImageObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(RawImage));

        if (existing == null)
        {
            rawImageObject.transform.SetParent(parent, false);
        }

        RawImage rawImage = rawImageObject.GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.color = color;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    private static IRHudHeartbeatGraphic EnsureHeartbeatTraceGraphic(RectTransform parent)
    {
        const string objectName = "IRAnalogNoiseLobbyHeartbeatTrace";
        Transform existing = parent.Find(objectName);
        GameObject traceObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(IRHudHeartbeatGraphic));

        if (existing == null)
        {
            traceObject.transform.SetParent(parent, false);
        }

        return traceObject.GetComponent<IRHudHeartbeatGraphic>();
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchWithOffsets(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static T FindNamedComponent<T>(RectTransform root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);

        for (int index = 0; index < components.Length; index++)
        {
            if (components[index] != null && components[index].name == objectName)
            {
                return components[index];
            }
        }

        return null;
    }

    private static void SetNamedObjectActive(Transform root, string objectName, bool active)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];

            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            candidate.gameObject.SetActive(active);
            return;
        }
    }

    private static void ApplyThreatEdge(RawImage edge)
    {
        if (edge == null)
        {
            return;
        }

        edge.texture = GetThreatTexture();
        edge.color = Color.white;
    }

    private static Outline EnsureOutline(Graphic graphic)
    {
        Outline outline = graphic.GetComponent<Outline>();

        if (outline == null)
        {
            outline = graphic.gameObject.AddComponent<Outline>();
        }

        return outline;
    }

    private static Shadow EnsureShadow(Graphic graphic)
    {
        Shadow shadow = graphic.GetComponent<Shadow>();

        if (shadow == null)
        {
            shadow = graphic.gameObject.AddComponent<Shadow>();
        }

        return shadow;
    }

    private static Sprite GetPanelSprite()
    {
        if (panelSprite == null)
        {
            Texture2D texture = CreatePanelTexture(40, 40, new Color32(18, 18, 18, 255), new Color32(84, 74, 64, 255));
            panelSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(10f, 10f, 10f, 10f));
        }

        return panelSprite;
    }

    private static Sprite GetMonitorSprite()
    {
        if (monitorSprite == null)
        {
            Texture2D texture = CreateMonitorTexture(56, 56);
            monitorSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));
        }

        return monitorSprite;
    }

    private static Sprite GetSlotSprite()
    {
        if (slotSprite == null)
        {
            Texture2D texture = CreatePanelTexture(28, 28, new Color32(14, 14, 14, 255), new Color32(108, 96, 82, 255));
            slotSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
        }

        return slotSprite;
    }

    private static Sprite GetButtonSprite()
    {
        if (buttonSprite == null)
        {
            Texture2D texture = CreateButtonTexture(36, 20);
            buttonSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
        }

        return buttonSprite;
    }

    private static Sprite GetGaugeSprite()
    {
        return GetButtonSprite();
    }

    private static Texture2D GetBackdropTexture()
    {
        if (backdropTexture == null)
        {
            backdropTexture = new Texture2D(160, 90, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            Color[] pixels = new Color[backdropTexture.width * backdropTexture.height];

            for (int y = 0; y < backdropTexture.height; y++)
            {
                for (int x = 0; x < backdropTexture.width; x++)
                {
                    float nx = x / (float)(backdropTexture.width - 1);
                    float ny = y / (float)(backdropTexture.height - 1);
                    float vignette = Mathf.Clamp01(1f - (Mathf.Abs(nx - 0.5f) * 1.7f + Mathf.Abs(ny - 0.5f) * 1.5f));
                    float grain = ((x * 13 + y * 29) % 17) / 16f;
                    float circuit = (x % 32 == 0 || y % 24 == 0) ? 0.12f : 0f;
                    float alpha = Mathf.Lerp(0.88f, 0.98f, vignette);
                    Color color = Color.Lerp(new Color(0.012f, 0.012f, 0.012f, alpha), new Color(0.03f, 0.028f, 0.024f, alpha), grain * 0.08f + circuit);
                    pixels[(y * backdropTexture.width) + x] = color;
                }
            }

            backdropTexture.SetPixels(pixels);
            backdropTexture.Apply(false, true);
        }

        return backdropTexture;
    }

    private static Texture2D GetScanlineTexture()
    {
        if (scanlineTexture == null)
        {
            scanlineTexture = new Texture2D(4, 16, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            Color[] pixels = new Color[scanlineTexture.width * scanlineTexture.height];

            for (int y = 0; y < scanlineTexture.height; y++)
            {
                float alpha = y % 4 == 0 ? 0.22f : y % 2 == 0 ? 0.08f : 0.02f;

                for (int x = 0; x < scanlineTexture.width; x++)
                {
                    pixels[(y * scanlineTexture.width) + x] = new Color(1f, 0.94f, 0.82f, alpha);
                }
            }

            scanlineTexture.SetPixels(pixels);
            scanlineTexture.Apply(false, true);
        }

        return scanlineTexture;
    }

    private static Texture2D GetThreatTexture()
    {
        if (threatTexture == null)
        {
            threatTexture = new Texture2D(32, 8, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            Color[] pixels = new Color[threatTexture.width * threatTexture.height];

            for (int y = 0; y < threatTexture.height; y++)
            {
                for (int x = 0; x < threatTexture.width; x++)
                {
                    bool stripe = x % 5 == 0;
                    float pulse = 0.42f + 0.12f * Mathf.Sin((x + y) * 0.5f);
                    pixels[(y * threatTexture.width) + x] = stripe
                        ? new Color(1f, 0.2f, 0.16f, 0.7f + pulse * 0.2f)
                        : new Color(1f, 0.45f, 0.18f, 0.26f + pulse * 0.12f);
                }
            }

            threatTexture.SetPixels(pixels);
            threatTexture.Apply(false, true);
        }

        return threatTexture;
    }

    private static Texture2D CreatePanelTexture(int width, int height, Color32 fillColor, Color32 borderColor)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool outer = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                bool innerBorder = x < 5 || y < 5 || x >= width - 5 || y >= height - 5;
                bool cornerWear = (x < 4 && y < 4) || (x >= width - 4 && y < 4) || (x < 4 && y >= height - 4) || (x >= width - 4 && y >= height - 4);
                bool corrosion = ((x * 19 + y * 7) % 23) <= 1;
                Color pixel = fillColor;

                if (innerBorder)
                {
                    pixel = Color.Lerp(fillColor, borderColor, outer ? 1f : 0.55f);
                }

                if (cornerWear)
                {
                    pixel = Color.Lerp(pixel, new Color(0.24f, 0.22f, 0.19f, 1f), 0.2f);
                }

                if (corrosion && !outer)
                {
                    pixel = Color.Lerp(pixel, AmberSoftColor, 0.08f);
                }

                pixels[(y * width) + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateMonitorTexture(int width, int height)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool outer = x < 3 || y < 3 || x >= width - 3 || y >= height - 3;
                bool bezel = x < 10 || y < 10 || x >= width - 10 || y >= height - 10;
                bool glass = x >= 11 && y >= 11 && x < width - 11 && y < height - 11;
                bool wear = ((x * 11 + y * 17) % 41) <= 1;
                Color pixel = outer
                    ? new Color(0.34f, 0.31f, 0.27f, 1f)
                    : bezel
                        ? new Color(0.21f, 0.2f, 0.18f, 1f)
                        : new Color(0.08f, 0.08f, 0.07f, 1f);

                if (glass)
                {
                    float scan = y % 5 == 0 ? 0.06f : 0f;
                    pixel = Color.Lerp(GlassColor, new Color(0.16f, 0.14f, 0.11f, 1f), scan);
                }

                if (wear)
                {
                    pixel = Color.Lerp(pixel, AmberSoftColor, 0.11f);
                }

                pixels[(y * width) + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateButtonTexture(int width, int height)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool outer = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
                bool border = x < 4 || y < 4 || x >= width - 4 || y >= height - 4;
                bool highlight = y < 6 && x > 4 && x < width - 4;
                Color pixel = outer
                    ? new Color(0.18f, 0.16f, 0.14f, 1f)
                    : border
                        ? new Color(0.41f, 0.35f, 0.24f, 1f)
                        : new Color(0.09f, 0.09f, 0.08f, 1f);

                if (highlight)
                {
                    pixel = Color.Lerp(pixel, AmberSoftColor, 0.12f);
                }

                pixels[(y * width) + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}

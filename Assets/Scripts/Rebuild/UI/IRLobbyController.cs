using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public sealed class IRLobbyController : MonoBehaviour
{
    private enum LobbyModal
    {
        None,
        Options,
        Credits
    }

    private sealed class MenuEntry
    {
        public MenuEntry(
            Button button,
            TextMeshProUGUI label,
            Color idleColor,
            Color selectedMinimumColor,
            Color selectedPeakColor)
        {
            Button = button;
            Label = label;
            IdleColor = idleColor;
            SelectedMinimumColor = selectedMinimumColor;
            SelectedPeakColor = selectedPeakColor;
            BaseScale = label != null ? label.rectTransform.localScale : Vector3.one;
            Shadow = label != null ? EnsureShadow(label) : null;
            Outline = label != null ? EnsureOutline(label) : null;
        }

        public Button Button { get; }
        public TextMeshProUGUI Label { get; }
        public Color IdleColor { get; }
        public Color SelectedMinimumColor { get; }
        public Color SelectedPeakColor { get; }
        public Vector3 BaseScale { get; }
        public Shadow Shadow { get; }
        public Outline Outline { get; }
    }

    private static readonly int[] FrameRateOptions = { 30, 60, 120, -1 };
    private static readonly Color MenuIdleColor = new(0.78f, 0.78f, 0.76f, 0.7f);
    private static readonly Color MenuSelectedWarmMin = new(0.94f, 0.9f, 0.84f, 0.96f);
    private static readonly Color MenuSelectedWarmMax = new(1f, 0.97f, 0.9f, 1f);
    private static readonly Color MenuSelectedAlertMin = new(0.86f, 0.34f, 0.3f, 0.94f);
    private static readonly Color MenuSelectedAlertMax = new(1f, 0.63f, 0.58f, 1f);

    [SerializeField] private MonoBehaviour uiSettingsReadModelSource;
    [SerializeField] private RRunSessionController runSessionController;
    [SerializeField] private ChapterDefinition startChapter;
    [SerializeField] private Button startRunButton;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TextMeshProUGUI summaryTitleText;
    [SerializeField] private TextMeshProUGUI summaryBodyText;
    [SerializeField] private TextMeshProUGUI footerHintText;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private RectTransform modalBackdrop;
    [SerializeField] private RectTransform optionsPanel;
    [SerializeField] private RectTransform creditsPanel;
    [SerializeField] private Button optionsCloseButton;
    [SerializeField] private Button creditsCloseButton;
    private RectTransform resolvedPanelRoot;

    private readonly List<MenuEntry> menuEntries = new();
    private Slider masterVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider ambienceVolumeSlider;
    private TextMeshProUGUI masterVolumeValueText;
    private TextMeshProUGUI sfxVolumeValueText;
    private TextMeshProUGUI ambienceVolumeValueText;
    private Button fpsCycleButton;
    private TextMeshProUGUI fpsCycleButtonText;
    private Button vSyncToggleButton;
    private TextMeshProUGUI vSyncToggleButtonText;
    private LobbyModal activeModal;
    private Button lastMenuSelection;
    private float masterVolumeValue = 0.92f;
    private float sfxVolumeValue = 1f;
    private float ambienceVolumeValue = 0.05f;
    private int frameRateOptionIndex = 1;
    private bool vSyncEnabled;
    private bool suppressOptionCallbacks;

    public Button StartRunButton => startRunButton;
    public Button TutorialButton => tutorialButton;
    public Button OptionsButton => optionsButton;
    public Button CreditsButton => creditsButton;
    public Button QuitButton => quitButton;
    public TextMeshProUGUI SummaryTitleText => summaryTitleText;
    public TextMeshProUGUI SummaryBodyText => summaryBodyText;
    public TextMeshProUGUI FooterHintText => footerHintText;
    public RRunSessionController RunSessionController => runSessionController;
    public IUiSettingsReadModel UiSettings => ResolveUiSettingsReadModel();
    public bool IsOptionsModalOpen => activeModal == LobbyModal.Options;
    public bool IsCreditsModalOpen => activeModal == LobbyModal.Credits;
    public RectTransform PanelRoot => resolvedPanelRoot != null
        ? resolvedPanelRoot
        : summaryTitleText != null
            ? summaryTitleText.transform.parent as RectTransform
            : transform as RectTransform;

    private void Awake()
    {
        ResolveUiSettingsReadModel();
        ResolveSessionController(logErrorIfMissing: true);
        RefreshSummary();

        if (Application.isPlaying)
        {
            InitializeRuntimeLobbyUi(selectDefault: true);
        }
    }

    private void OnEnable()
    {
        ResolveUiSettingsReadModel();
        ResolveSessionController(logErrorIfMissing: false);
        RefreshSummary();

        if (Application.isPlaying)
        {
            InitializeRuntimeLobbyUi(selectDefault: activeModal == LobbyModal.None);
        }
    }

    private void OnDisable()
    {
        SaveOptionValues(flushToDisk: true);
        BindRunSessionController(null);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureSelection();
        UpdateMenuVisuals();
        HandleKeyboardShortcuts();
    }

    public void Configure(
        RRunSessionController configuredRunSessionController,
        Button configuredStartRunButton,
        Button configuredQuitButton,
        TextMeshProUGUI configuredSummaryTitleText,
        TextMeshProUGUI configuredSummaryBodyText,
        TextMeshProUGUI configuredFooterHintText)
    {
        runSessionController = configuredRunSessionController;
        startRunButton = configuredStartRunButton;
        quitButton = configuredQuitButton;
        summaryTitleText = configuredSummaryTitleText;
        summaryBodyText = configuredSummaryBodyText;
        footerHintText = configuredFooterHintText;
        resolvedPanelRoot = configuredSummaryTitleText != null
            ? configuredSummaryTitleText.transform.parent as RectTransform
            : resolvedPanelRoot;
        ResolveSessionController(logErrorIfMissing: true);
        RefreshSummary();

        if (Application.isPlaying)
        {
            InitializeRuntimeLobbyUi(selectDefault: true);
        }
    }

    public void RefreshSummary()
    {
        ResolveSessionController(logErrorIfMissing: false);
        bool useLobbyThemeFormatting = IRAnalogNoiseUiTheme.IsEnabled(this);

        if (summaryTitleText != null)
        {
            string summaryTitle = runSessionController != null
                ? runSessionController.Snapshot.SummaryTitle
                : "Deployment Ready";
            summaryTitleText.text = IRLobbySummaryFormatter.FormatTitle(summaryTitle, useLobbyThemeFormatting);
        }

        if (summaryBodyText != null)
        {
            string summaryBody = runSessionController != null
                ? runSessionController.Snapshot.SummaryBody
                : "Press Play to launch the authored emergency route.";
            summaryBodyText.text = IRLobbySummaryFormatter.FormatBody(summaryBody, useLobbyThemeFormatting);
        }

        if (footerHintText != null)
        {
            footerHintText.text = IRLobbySummaryFormatter.FormatFooter(
                runSessionController != null ? runSessionController.GameplayScenePath : string.Empty,
                useLobbyThemeFormatting);
        }
    }

    public TMP_FontAsset ResolveThemeFont()
    {
        if (summaryTitleText != null && summaryTitleText.font != null)
        {
            return summaryTitleText.font;
        }

        if (summaryBodyText != null && summaryBodyText.font != null)
        {
            return summaryBodyText.font;
        }

        if (footerHintText != null && footerHintText.font != null)
        {
            return footerHintText.font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    public void StartRun()
    {
        RRunSessionController resolvedSessionController = ResolveSessionController(logErrorIfMissing: true);

        if (resolvedSessionController == null)
        {
            Debug.LogError($"{nameof(IRLobbyController)} cannot start a run because no {nameof(RRunSessionController)} is assigned.", this);
            return;
        }

        if (startChapter != null)
        {
            resolvedSessionController.StartChapterAndLoadGameplay(startChapter);
            return;
        }

        resolvedSessionController.StartNewRunAndLoadGameplay();
    }

    public void StartTutorial()
    {
        RRunSessionController resolvedSessionController = ResolveSessionController(logErrorIfMissing: true);

        if (resolvedSessionController == null)
        {
            Debug.LogError($"{nameof(IRLobbyController)} cannot start the tutorial because no {nameof(RRunSessionController)} is assigned.", this);
            return;
        }

        resolvedSessionController.LoadTutorialScene();
    }

    public void OpenOptionsModal()
    {
        lastMenuSelection = IsTrackedMenuButton(optionsButton) ? optionsButton : lastMenuSelection;
        activeModal = LobbyModal.Options;
        RefreshOptionControls();
        SetModalVisualState();
        SelectDefaultActionButton();
    }

    public void OpenCreditsModal()
    {
        lastMenuSelection = IsTrackedMenuButton(creditsButton) ? creditsButton : lastMenuSelection;
        activeModal = LobbyModal.Credits;
        SetModalVisualState();
        SelectDefaultActionButton();
    }

    public void CloseActiveModal()
    {
        if (activeModal == LobbyModal.None)
        {
            return;
        }

        activeModal = LobbyModal.None;
        SetModalVisualState();
        SaveOptionValues(flushToDisk: true);
        SelectDefaultActionButton();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void InitializeRuntimeLobbyUi(bool selectDefault)
    {
        PrototypeAudioManager.EnsureExists();
        PrototypeAudioManager.TryApplySceneAmbienceForScene(gameObject.scene);
        CleanupTransientLobbyThemeArtifacts();
        IRAnalogNoiseUiTheme.ApplyLobbyTheme(this);
        EnsureRuntimeLobbyUi();
        BindButtons();
        LoadOptionValues();
        ApplyOptionValuesToRuntime(refreshUi: true);

        if (selectDefault)
        {
            SelectDefaultActionButton();
        }
    }

    private void EnsureRuntimeLobbyUi()
    {
        RectTransform panelRoot = PanelRoot;

        if (panelRoot == null)
        {
            return;
        }

        ResolveAuthoredModalUi();

        if (!ValidateAuthoredLobbyUi(logErrors: true))
        {
            return;
        }

        ResolveMenuEntries();
        ConfigureMainMenuNavigation();
        ConfigureModalNavigation();
    }

    private void ResolveMenuEntries()
    {
        menuEntries.Clear();

        RegisterMenuEntry(startRunButton, FindMenuLabel(startRunButton), MenuSelectedWarmMin, MenuSelectedWarmMax);
        RegisterMenuEntry(tutorialButton, FindMenuLabel(tutorialButton), MenuSelectedWarmMin, MenuSelectedWarmMax);
        RegisterMenuEntry(optionsButton, FindMenuLabel(optionsButton), MenuSelectedAlertMin, MenuSelectedAlertMax);
        RegisterMenuEntry(creditsButton, FindMenuLabel(creditsButton), MenuSelectedAlertMin, MenuSelectedAlertMax);
        RegisterMenuEntry(quitButton, FindMenuLabel(quitButton), MenuSelectedAlertMin, MenuSelectedAlertMax);

        if (!IsTrackedMenuButton(lastMenuSelection))
        {
            lastMenuSelection = startRunButton != null
                ? startRunButton
                : tutorialButton != null
                    ? tutorialButton
                    : optionsButton != null
                        ? optionsButton
                        : creditsButton != null
                            ? creditsButton
                            : quitButton;
        }
    }

    private void RegisterMenuEntry(
        Button button,
        TextMeshProUGUI label,
        Color selectedMinimumColor,
        Color selectedPeakColor)
    {
        if (button == null || label == null)
        {
            return;
        }

        menuEntries.Add(new MenuEntry(
            button,
            label,
            MenuIdleColor,
            selectedMinimumColor,
            selectedPeakColor));
    }

    private void ResolveAuthoredModalUi()
    {
        masterVolumeSlider ??= FindNamedSlider("IRMasterVolumeRow_Slider");
        sfxVolumeSlider ??= FindNamedSlider("IRSfxVolumeRow_Slider");
        ambienceVolumeSlider ??= FindNamedSlider("IRAmbienceVolumeRow_Slider");

        masterVolumeValueText ??= FindNamedText("IRMasterVolumeRow_Value");
        sfxVolumeValueText ??= FindNamedText("IRSfxVolumeRow_Value");
        ambienceVolumeValueText ??= FindNamedText("IRAmbienceVolumeRow_Value");

        fpsCycleButton ??= FindNamedButton("IRFrameRateRow_Button");
        fpsCycleButtonText ??= FindNamedText("IRFrameRateRow_Button_Text");

        vSyncToggleButton ??= FindNamedButton("IRVSyncRow_Button");
        vSyncToggleButtonText ??= FindNamedText("IRVSyncRow_Button_Text");

        optionsCloseButton ??= FindNamedButton("IRLobbyOptionsCloseButton");
        creditsCloseButton ??= FindNamedButton("IRLobbyCreditsCloseButton");
        SetModalVisualState();
    }

    private bool ValidateAuthoredLobbyUi(bool logErrors)
    {
        bool isValid = true;

        isValid &= RequireReference(uiSettingsReadModelSource, nameof(uiSettingsReadModelSource), logErrors);
        isValid &= RequireReference(startRunButton, nameof(startRunButton), logErrors);
        isValid &= RequireReference(optionsButton, nameof(optionsButton), logErrors);
        isValid &= RequireReference(creditsButton, nameof(creditsButton), logErrors);
        isValid &= RequireReference(quitButton, nameof(quitButton), logErrors);
        isValid &= RequireReference(summaryTitleText, nameof(summaryTitleText), logErrors);
        isValid &= RequireReference(summaryBodyText, nameof(summaryBodyText), logErrors);
        isValid &= RequireReference(footerHintText, nameof(footerHintText), logErrors);
        isValid &= RequireReference(modalBackdrop, nameof(modalBackdrop), logErrors);
        isValid &= RequireReference(optionsPanel, nameof(optionsPanel), logErrors);
        isValid &= RequireReference(creditsPanel, nameof(creditsPanel), logErrors);
        isValid &= RequireReference(optionsCloseButton, nameof(optionsCloseButton), logErrors);
        isValid &= RequireReference(creditsCloseButton, nameof(creditsCloseButton), logErrors);
        return isValid;
    }

    private bool RequireReference(UnityEngine.Object target, string fieldName, bool logErrors)
    {
        if (target != null)
        {
            return true;
        }

        if (logErrors)
        {
            Debug.LogError($"{nameof(IRLobbyController)} is missing its authored `{fieldName}` reference in the lobby scene.", this);
        }

        return false;
    }

    private void BuildOptionsPanel(RectTransform parent)
    {
        TMP_FontAsset font = ResolveThemeFont();
        CreateModalHeader(parent, font, "OPTIONS");

        masterVolumeSlider = CreateSliderRow(
            parent,
            font,
            "IRMasterVolumeRow",
            "MASTER",
            -102f,
            out masterVolumeValueText);
        sfxVolumeSlider = CreateSliderRow(
            parent,
            font,
            "IRSfxVolumeRow",
            "SFX",
            -170f,
            out sfxVolumeValueText);
        ambienceVolumeSlider = CreateSliderRow(
            parent,
            font,
            "IRAmbienceVolumeRow",
            "AMBIENCE",
            -238f,
            out ambienceVolumeValueText);

        fpsCycleButton = CreateButtonRow(
            parent,
            font,
            "IRFrameRateRow",
            "FPS LIMIT",
            -306f,
            out fpsCycleButtonText);
        vSyncToggleButton = CreateButtonRow(
            parent,
            font,
            "IRVSyncRow",
            "V-SYNC",
            -374f,
            out vSyncToggleButtonText);

        TextMeshProUGUI helperText = CreateText(
            "IRLobbyOptionsHint",
            parent,
            font,
            "Changes apply immediately and are saved locally on this machine.",
            18f,
            FontStyles.Normal,
            new Color(0.76f, 0.74f, 0.7f, 0.6f),
            TextAlignmentOptions.Center);
        ConfigureRect(
            helperText.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 76f),
            new Vector2(620f, 22f));

        optionsCloseButton = CreateControlButton(
            parent,
            font,
            "IRLobbyOptionsCloseButton",
            "CLOSE",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 28f),
            new Vector2(188f, 52f),
            out _);
    }

    private void BuildCreditsPanel(RectTransform parent)
    {
        TMP_FontAsset font = ResolveThemeFont();
        CreateModalHeader(parent, font, "CREDITS");

        TextMeshProUGUI bodyText = CreateText(
            "IRLobbyCreditsBody",
            parent,
            font,
            "EXIT: WARD\n\n" +
            "Built around the authored lobby -> floor descent -> lobby loop.\n" +
            "Lobby reference art lives in Assets/Art/UI/Lobby.\n" +
            "Audio source notes live in Docs/AudioAssetSources.md.\n\n" +
            "This modal is lightweight on purpose so the playable route stays stable.",
            24f,
            FontStyles.Bold,
            new Color(0.9f, 0.88f, 0.82f, 0.94f),
            TextAlignmentOptions.TopLeft);
        ConfigureRect(
            bodyText.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(42f, -88f),
            new Vector2(-84f, 190f));
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.lineSpacing = -8f;

        creditsCloseButton = CreateControlButton(
            parent,
            font,
            "IRLobbyCreditsCloseButton",
            "CLOSE",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 28f),
            new Vector2(188f, 52f),
            out _);
    }

    private RectTransform CreateModalPanel(
        string objectName,
        Transform parent,
        Vector2 sizeDelta)
    {
        RectTransform panel = CreateRect(
            objectName,
            parent,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f),
            sizeDelta);

        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.075f, 0.07f, 0.96f);
        panelImage.raycastTarget = true;

        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.42f, 0.35f, 0.3f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(8f, -8f);

        RectTransform innerBorder = CreateRect(
            objectName + "_InnerBorder",
            panel,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        Image innerBorderImage = innerBorder.gameObject.AddComponent<Image>();
        innerBorderImage.color = Color.clear;
        innerBorderImage.raycastTarget = false;
        Outline innerOutline = innerBorder.gameObject.AddComponent<Outline>();
        innerOutline.effectColor = new Color(0.23f, 0.2f, 0.18f, 0.88f);
        innerOutline.effectDistance = new Vector2(1f, -1f);
        return panel;
    }

    private void CreateModalHeader(
        RectTransform parent,
        TMP_FontAsset font,
        string headerText)
    {
        TextMeshProUGUI header = CreateText(
            parent.name + "_Header",
            parent,
            font,
            headerText,
            34f,
            FontStyles.Bold,
            new Color(0.95f, 0.92f, 0.88f, 0.98f),
            TextAlignmentOptions.Center);
        ConfigureRect(
            header.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -34f),
            new Vector2(320f, 34f));

        CreateDivider(parent, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(420f, 2f));
    }

    private Slider CreateSliderRow(
        RectTransform parent,
        TMP_FontAsset font,
        string objectName,
        string labelText,
        float anchoredY,
        out TextMeshProUGUI valueText)
    {
        RectTransform row = CreateRect(
            objectName,
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(44f, anchoredY),
            new Vector2(672f, 52f));

        TextMeshProUGUI label = CreateText(
            objectName + "_Label",
            row,
            font,
            labelText,
            24f,
            FontStyles.Bold,
            new Color(0.84f, 0.82f, 0.76f, 0.96f),
            TextAlignmentOptions.Left);
        ConfigureRect(
            label.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(170f, 30f));

        Slider slider = CreateSegmentedSlider(
            objectName + "_Slider",
            row,
            new Vector2(196f, 0f),
            new Vector2(324f, 28f));

        valueText = CreateText(
            objectName + "_Value",
            row,
            font,
            "0%",
            23f,
            FontStyles.Bold,
            new Color(0.92f, 0.9f, 0.86f, 0.96f),
            TextAlignmentOptions.Right);
        ConfigureRect(
            valueText.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(112f, 28f));
        return slider;
    }

    private Button CreateButtonRow(
        RectTransform parent,
        TMP_FontAsset font,
        string objectName,
        string labelText,
        float anchoredY,
        out TextMeshProUGUI buttonText)
    {
        RectTransform row = CreateRect(
            objectName,
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(44f, anchoredY),
            new Vector2(672f, 52f));

        TextMeshProUGUI label = CreateText(
            objectName + "_Label",
            row,
            font,
            labelText,
            24f,
            FontStyles.Bold,
            new Color(0.84f, 0.82f, 0.76f, 0.96f),
            TextAlignmentOptions.Left);
        ConfigureRect(
            label.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(170f, 30f));

        Button button = CreateControlButton(
            row,
            font,
            objectName + "_Button",
            string.Empty,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(280f, 42f),
            out buttonText);
        return button;
    }

    private Button CreateControlButton(
        RectTransform parent,
        TMP_FontAsset font,
        string objectName,
        string textValue,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        out TextMeshProUGUI text)
    {
        RectTransform buttonRect = CreateRect(
            objectName,
            parent,
            anchorMin,
            anchorMax,
            pivot,
            anchoredPosition,
            sizeDelta);
        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.color = new Color(0.14f, 0.12f, 0.11f, 0.98f);
        image.raycastTarget = true;

        Outline outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.33f, 0.28f, 0.24f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = image;
        button.colors = CreateSelectableColors();

        text = CreateText(
            objectName + "_Text",
            buttonRect,
            font,
            textValue,
            24f,
            FontStyles.Bold,
            new Color(0.94f, 0.92f, 0.88f, 0.98f),
            TextAlignmentOptions.Center);
        ConfigureRect(
            text.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        return button;
    }

    private Slider CreateSegmentedSlider(
        string objectName,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        RectTransform sliderRect = CreateRect(
            objectName,
            parent,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            anchoredPosition,
            sizeDelta);
        Image background = sliderRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.98f);
        background.raycastTarget = true;

        Outline outline = sliderRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.17f, 0.15f, 0.14f, 0.92f);
        outline.effectDistance = new Vector2(1f, -1f);

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.ColorTint;
        slider.targetGraphic = background;
        slider.colors = CreateSelectableColors();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        RectTransform fillArea = CreateRect(
            "Fill Area",
            sliderRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        SetStretch(fillArea, new Vector2(3f, 3f), new Vector2(-3f, -3f));

        RectTransform fill = CreateRect(
            "Fill",
            fillArea,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.66f, 0.2f, 0.19f, 0.96f);
        fillImage.raycastTarget = false;
        slider.fillRect = fill;

        RectTransform handleSlideArea = CreateRect(
            "Handle Slide Area",
            sliderRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        SetStretch(handleSlideArea, new Vector2(0f, 0f), new Vector2(0f, 0f));

        RectTransform handle = CreateRect(
            "Handle",
            handleSlideArea,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(12f, 24f));
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.96f, 0.88f, 0.82f, 0.01f);
        handleImage.raycastTarget = true;
        slider.handleRect = handle;

        for (int index = 1; index < 16; index++)
        {
            RectTransform divider = CreateRect(
                $"Divider_{index}",
                sliderRect,
                new Vector2(index / 16f, 0.5f),
                new Vector2(index / 16f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(2f, 22f));
            Image dividerImage = divider.gameObject.AddComponent<Image>();
            dividerImage.color = new Color(0f, 0f, 0f, 0.58f);
            dividerImage.raycastTarget = false;
        }

        return slider;
    }

    private void CreateDivider(
        RectTransform parent,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        RectTransform divider = CreateRect(
            parent.name + "_Divider_" + parent.childCount,
            parent,
            anchor,
            anchor,
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            sizeDelta);
        Image dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = new Color(0.35f, 0.28f, 0.24f, 0.78f);
        dividerImage.raycastTarget = false;
    }

    private void SetModalVisualState()
    {
        bool showBackdrop = activeModal != LobbyModal.None && modalBackdrop != null;

        if (modalBackdrop != null)
        {
            modalBackdrop.gameObject.SetActive(showBackdrop);
            modalBackdrop.SetAsLastSibling();
        }

        if (optionsPanel != null)
        {
            optionsPanel.gameObject.SetActive(activeModal == LobbyModal.Options);
        }

        if (creditsPanel != null)
        {
            creditsPanel.gameObject.SetActive(activeModal == LobbyModal.Credits);
        }
    }

    private void BindButtons()
    {
        BindButtonClick(startRunButton, StartRun, nameof(StartRun));
        BindButtonClick(tutorialButton, StartTutorial, nameof(StartTutorial));
        BindButtonClick(optionsButton, OpenOptionsModal, nameof(OpenOptionsModal));
        BindButtonClick(creditsButton, OpenCreditsModal, nameof(OpenCreditsModal));
        BindButtonClick(quitButton, QuitGame, nameof(QuitGame));

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
            masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        }

        if (ambienceVolumeSlider != null)
        {
            ambienceVolumeSlider.onValueChanged.RemoveListener(HandleAmbienceVolumeChanged);
            ambienceVolumeSlider.onValueChanged.AddListener(HandleAmbienceVolumeChanged);
        }

        if (fpsCycleButton != null)
        {
            fpsCycleButton.onClick.RemoveListener(CycleFrameRateOption);
            fpsCycleButton.onClick.AddListener(CycleFrameRateOption);
        }

        if (vSyncToggleButton != null)
        {
            vSyncToggleButton.onClick.RemoveListener(ToggleVSync);
            vSyncToggleButton.onClick.AddListener(ToggleVSync);
        }

        if (optionsCloseButton != null)
        {
            optionsCloseButton.onClick.RemoveListener(CloseActiveModal);
            optionsCloseButton.onClick.AddListener(CloseActiveModal);
        }

        if (creditsCloseButton != null)
        {
            creditsCloseButton.onClick.RemoveListener(CloseActiveModal);
            creditsCloseButton.onClick.AddListener(CloseActiveModal);
        }

    }

    private void BindButtonClick(Button button, UnityAction action, string methodName)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);

        if (HasPersistentButtonClick(button, this, methodName))
        {
            return;
        }

        button.onClick.AddListener(action);
    }

    private static bool HasPersistentButtonClick(Button button, UnityEngine.Object target, string methodName)
    {
        if (button == null || target == null || string.IsNullOrWhiteSpace(methodName))
        {
            return false;
        }

        int persistentCount = button.onClick.GetPersistentEventCount();

        for (int index = 0; index < persistentCount; index++)
        {
            if (button.onClick.GetPersistentTarget(index) != target)
            {
                continue;
            }

            if (!string.Equals(button.onClick.GetPersistentMethodName(index), methodName, StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void HandleMasterVolumeChanged(float value)
    {
        if (suppressOptionCallbacks)
        {
            return;
        }

        masterVolumeValue = Mathf.Clamp01(value);
        CommitOptionChanges(updateUi: true);
    }

    private void HandleSfxVolumeChanged(float value)
    {
        if (suppressOptionCallbacks)
        {
            return;
        }

        sfxVolumeValue = Mathf.Clamp01(value);
        CommitOptionChanges(updateUi: true);
    }

    private void HandleAmbienceVolumeChanged(float value)
    {
        if (suppressOptionCallbacks)
        {
            return;
        }

        ambienceVolumeValue = Mathf.Clamp01(value);
        CommitOptionChanges(updateUi: true);
    }

    private void CycleFrameRateOption()
    {
        frameRateOptionIndex = (frameRateOptionIndex + 1) % FrameRateOptions.Length;
        CommitOptionChanges(updateUi: true);
    }

    private void ToggleVSync()
    {
        vSyncEnabled = !vSyncEnabled;
        CommitOptionChanges(updateUi: true);
    }

    private void CommitOptionChanges(bool updateUi)
    {
        ApplyOptionValuesToRuntime(refreshUi: updateUi);
        SaveOptionValues(flushToDisk: false);
    }

    private void LoadOptionValues()
    {
        PrototypeAudioManager audioManager = PrototypeAudioManager.EnsureExists();
        RLobbyRuntimeOptionsSnapshot snapshot = RLobbyRuntimeOptions.Load(audioManager, TryLoadRuntimeSettings());
        masterVolumeValue = snapshot.MasterVolume;
        sfxVolumeValue = snapshot.SfxVolume;
        ambienceVolumeValue = snapshot.AmbienceVolume;
        frameRateOptionIndex = ResolveFrameRateOptionIndex(snapshot.TargetFrameRate);
        vSyncEnabled = snapshot.VSyncEnabled;
    }

    private void SaveOptionValues(bool flushToDisk)
    {
        RLobbyRuntimeOptions.Save(BuildRuntimeOptionsSnapshot(), flushToDisk);
    }

    private void ApplyOptionValuesToRuntime(bool refreshUi)
    {
        RLobbyRuntimeOptions.ApplyAllToRuntime(
            BuildRuntimeOptionsSnapshot(),
            PrototypeAudioManager.EnsureExists());

        if (refreshUi)
        {
            RefreshOptionControls();
        }
    }

    private RLobbyRuntimeOptionsSnapshot BuildRuntimeOptionsSnapshot()
    {
        return new RLobbyRuntimeOptionsSnapshot(
            masterVolumeValue,
            sfxVolumeValue,
            ambienceVolumeValue,
            FrameRateOptions[Mathf.Clamp(frameRateOptionIndex, 0, FrameRateOptions.Length - 1)],
            vSyncEnabled);
    }

    private void RefreshOptionControls()
    {
        suppressOptionCallbacks = true;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(masterVolumeValue);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(sfxVolumeValue);
        }

        if (ambienceVolumeSlider != null)
        {
            ambienceVolumeSlider.SetValueWithoutNotify(ambienceVolumeValue);
        }

        suppressOptionCallbacks = false;
        RefreshOptionValueTexts();
    }

    private void RefreshOptionValueTexts()
    {
        if (masterVolumeValueText != null)
        {
            masterVolumeValueText.text = $"{Mathf.RoundToInt(masterVolumeValue * 100f)}%";
        }

        if (sfxVolumeValueText != null)
        {
            sfxVolumeValueText.text = $"{Mathf.RoundToInt(sfxVolumeValue * 100f)}%";
        }

        if (ambienceVolumeValueText != null)
        {
            ambienceVolumeValueText.text = $"{Mathf.RoundToInt(ambienceVolumeValue * 100f)}%";
        }

        if (fpsCycleButtonText != null)
        {
            int frameRate = FrameRateOptions[Mathf.Clamp(frameRateOptionIndex, 0, FrameRateOptions.Length - 1)];
            fpsCycleButtonText.text = frameRate > 0 ? $"{frameRate} FPS" : "UNLIMITED";
        }

        if (vSyncToggleButtonText != null)
        {
            vSyncToggleButtonText.text = vSyncEnabled ? "ON" : "OFF";
        }
    }

    private void ConfigureMainMenuNavigation()
    {
        List<Selectable> selectables = new();

        for (int index = 0; index < menuEntries.Count; index++)
        {
            if (menuEntries[index].Button != null)
            {
                selectables.Add(menuEntries[index].Button);
            }
        }

        ConfigureVerticalNavigation(selectables);
    }

    private void ConfigureModalNavigation()
    {
        List<Selectable> optionsSelectables = new();

        if (masterVolumeSlider != null)
        {
            optionsSelectables.Add(masterVolumeSlider);
        }

        if (sfxVolumeSlider != null)
        {
            optionsSelectables.Add(sfxVolumeSlider);
        }

        if (ambienceVolumeSlider != null)
        {
            optionsSelectables.Add(ambienceVolumeSlider);
        }

        if (fpsCycleButton != null)
        {
            optionsSelectables.Add(fpsCycleButton);
        }

        if (vSyncToggleButton != null)
        {
            optionsSelectables.Add(vSyncToggleButton);
        }

        if (optionsCloseButton != null)
        {
            optionsSelectables.Add(optionsCloseButton);
        }

        ConfigureVerticalNavigation(optionsSelectables);

        if (creditsCloseButton != null)
        {
            Navigation navigation = creditsCloseButton.navigation;
            navigation.mode = Navigation.Mode.None;
            creditsCloseButton.navigation = navigation;
        }

    }

    private static void ConfigureVerticalNavigation(IReadOnlyList<Selectable> selectables)
    {
        for (int index = 0; index < selectables.Count; index++)
        {
            Selectable selectable = selectables[index];

            if (selectable == null)
            {
                continue;
            }

            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = index > 0 ? selectables[index - 1] : selectables[selectables.Count - 1];
            navigation.selectOnDown = index < selectables.Count - 1 ? selectables[index + 1] : selectables[0];
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            selectable.navigation = navigation;
        }
    }

    private void EnsureSelection()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return;
        }

        GameObject selectedGameObject = eventSystem.currentSelectedGameObject;
        Button selectedMenuButton = ResolveSelectedMenuButton(selectedGameObject);

        if (selectedMenuButton != null)
        {
            lastMenuSelection = selectedMenuButton;
        }

        if (selectedGameObject != null)
        {
            return;
        }

        SelectDefaultActionButton();
    }

    private void HandleKeyboardShortcuts()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (activeModal != LobbyModal.None)
            {
                CloseActiveModal();
            }
            else
            {
                QuitGame();
            }

            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame && activeModal == LobbyModal.None)
        {
            QuitGame();
        }
    }

    private void SelectDefaultActionButton()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Selectable target = ResolveDefaultSelection();

        if (target == null || !target.IsInteractable() || !target.gameObject.activeInHierarchy)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        target.Select();
    }

    private Selectable ResolveDefaultSelection()
    {
        return activeModal switch
        {
            LobbyModal.Options => masterVolumeSlider != null
                ? masterVolumeSlider
                : sfxVolumeSlider != null
                    ? sfxVolumeSlider
                    : ambienceVolumeSlider != null
                        ? ambienceVolumeSlider
                        : fpsCycleButton != null
                            ? fpsCycleButton
                            : vSyncToggleButton != null
                                ? vSyncToggleButton
                                : optionsCloseButton,
            LobbyModal.Credits => creditsCloseButton,
            _ => IsTrackedMenuButton(lastMenuSelection)
                ? lastMenuSelection
                : startRunButton != null
                    ? startRunButton
                    : tutorialButton != null
                        ? tutorialButton
                        : optionsButton != null
                            ? optionsButton
                            : creditsButton != null
                                ? creditsButton
                                : quitButton
        };
    }

    private void UpdateMenuVisuals()
    {
        if (menuEntries.Count == 0)
        {
            return;
        }

        Button highlightedButton = activeModal != LobbyModal.None
            ? lastMenuSelection
            : ResolveSelectedMenuButton(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null);

        if (!IsTrackedMenuButton(highlightedButton))
        {
            highlightedButton = lastMenuSelection;
        }

        float pulse = 0.5f + (Mathf.Sin(Time.unscaledTime * 5.4f) * 0.5f);

        for (int index = 0; index < menuEntries.Count; index++)
        {
            MenuEntry entry = menuEntries[index];

            if (entry.Label == null)
            {
                continue;
            }

            bool selected = entry.Button == highlightedButton;
            Color textColor = selected
                ? Color.Lerp(entry.SelectedMinimumColor, entry.SelectedPeakColor, pulse)
                : entry.IdleColor;
            entry.Label.color = textColor;
            entry.Label.characterSpacing = selected ? Mathf.Lerp(1.5f, 3f, pulse) : 0f;
            entry.Label.rectTransform.localScale = selected
                ? entry.BaseScale * Mathf.Lerp(1f, 1.04f, pulse)
                : entry.BaseScale;

            if (entry.Shadow != null)
            {
                entry.Shadow.enabled = true;
                entry.Shadow.effectDistance = selected ? new Vector2(0f, -2f) : new Vector2(0f, -1f);
                entry.Shadow.effectColor = selected
                    ? new Color(textColor.r * 0.75f, 0.1f, 0.1f, Mathf.Lerp(0.32f, 0.82f, pulse))
                    : new Color(0f, 0f, 0f, 0.42f);
            }

            if (entry.Outline != null)
            {
                entry.Outline.enabled = selected;
                entry.Outline.effectDistance = new Vector2(1f, -1f);
                entry.Outline.effectColor = selected
                    ? new Color(textColor.r, 0.14f, 0.14f, Mathf.Lerp(0.08f, 0.38f, pulse))
                    : Color.clear;
            }
        }
    }

    private Button ResolveSelectedMenuButton(GameObject selectedGameObject)
    {
        if (selectedGameObject == null)
        {
            return null;
        }

        for (int index = 0; index < menuEntries.Count; index++)
        {
            Button button = menuEntries[index].Button;

            if (button == null)
            {
                continue;
            }

            if (selectedGameObject == button.gameObject || selectedGameObject.transform.IsChildOf(button.transform))
            {
                return button;
            }
        }

        return null;
    }

    private bool IsTrackedMenuButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        for (int index = 0; index < menuEntries.Count; index++)
        {
            if (menuEntries[index].Button == button)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleSnapshotChanged(RRunSnapshot snapshot)
    {
        RefreshSummary();
    }

    private RRunSessionController ResolveSessionController(bool logErrorIfMissing)
    {
        RRunSessionController resolvedSessionController = runSessionController;

        if (resolvedSessionController == null)
        {
            resolvedSessionController = RRunSessionResolver.ResolveForContext(this);
        }

        BindRunSessionController(resolvedSessionController);

        if (runSessionController == null && logErrorIfMissing)
        {
            Debug.LogError($"{nameof(IRLobbyController)} is missing its {nameof(RRunSessionController)} reference.", this);
        }

        if (resolvedPanelRoot == null)
        {
            resolvedPanelRoot = summaryTitleText != null
                ? summaryTitleText.transform.parent as RectTransform
                : transform as RectTransform;
        }

        return runSessionController;
    }

    private void BindRunSessionController(RRunSessionController resolvedSessionController)
    {
        if (runSessionController == resolvedSessionController)
        {
            return;
        }

        if (Application.isPlaying && runSessionController != null)
        {
            runSessionController.SnapshotChanged -= HandleSnapshotChanged;
        }

        runSessionController = resolvedSessionController;

        if (Application.isPlaying && runSessionController != null)
        {
            runSessionController.SnapshotChanged += HandleSnapshotChanged;
        }
    }

    private void CleanupTransientLobbyThemeArtifacts()
    {
        RestoreTransientLobbyThemeReferences();
        RemoveChildrenWithPrefix(PanelRoot, "IRAnalogNoise");

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = parentCanvas != null ? parentCanvas.transform as RectTransform : null;

        if (canvasRect != null && canvasRect != PanelRoot)
        {
            RemoveChildrenWithPrefix(canvasRect, "IRAnalogNoise");
        }
    }

    private void RestoreTransientLobbyThemeReferences()
    {
        IRHudHeartbeatGraphic heartbeatPreview = FindNamedComponent<IRHudHeartbeatGraphic>("IRLobbyHeartbeatPreview");

        if (heartbeatPreview != null && heartbeatPreview.transform.parent != PanelRoot && PanelRoot != null)
        {
            heartbeatPreview.rectTransform.SetParent(PanelRoot, false);
            heartbeatPreview.rectTransform.localScale = Vector3.one;
            heartbeatPreview.rectTransform.localRotation = Quaternion.identity;
        }

        if (!IRAnalogNoiseUiTheme.IsEnabled(this))
        {
            SetNamedObjectActive(transform.root, "RLobbyCardioSimPreview", true);
        }
    }

    private static void RemoveChildrenWithPrefix(RectTransform parent, string prefix)
    {
        if (parent == null || string.IsNullOrEmpty(prefix))
        {
            return;
        }

        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = parent.GetChild(childIndex);

            if (child != null && child.name.StartsWith(prefix, StringComparison.Ordinal))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private IUiSettingsReadModel ResolveUiSettingsReadModel()
    {
        if (uiSettingsReadModelSource is IUiSettingsReadModel readModel)
        {
            return readModel;
        }

        IUiSettingsReadModel resolvedReadModel = UiSettingsOwner.Resolve(this, uiSettingsReadModelSource);

        if (uiSettingsReadModelSource == null && resolvedReadModel is MonoBehaviour behaviour)
        {
            uiSettingsReadModelSource = behaviour;
        }

        return resolvedReadModel;
    }

    private TextMeshProUGUI FindMenuLabel(Button button)
    {
        if (button == null)
        {
            return null;
        }

        TextMeshProUGUI directText = button.GetComponent<TextMeshProUGUI>();
        return directText != null ? directText : button.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private TextMeshProUGUI FindNamedText(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        RectTransform panelRoot = PanelRoot;
        Transform found = panelRoot != null ? panelRoot.Find(objectName) : null;

        if (found != null)
        {
            return found.GetComponent<TextMeshProUGUI>();
        }

        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);

        for (int index = 0; index < texts.Length; index++)
        {
            if (string.Equals(texts[index].name, objectName, StringComparison.Ordinal))
            {
                return texts[index];
            }
        }

        return null;
    }

    private RectTransform FindNamedRect(string objectName)
    {
        return FindNamedComponent<RectTransform>(objectName);
    }

    private Button FindNamedButton(string objectName)
    {
        return FindNamedComponent<Button>(objectName);
    }

    private Slider FindNamedSlider(string objectName)
    {
        return FindNamedComponent<Slider>(objectName);
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        RectTransform panelRoot = PanelRoot;
        Transform found = panelRoot != null ? panelRoot.Find(objectName) : null;

        if (found != null)
        {
            return found.GetComponent<T>();
        }

        T[] components = GetComponentsInChildren<T>(true);

        for (int index = 0; index < components.Length; index++)
        {
            if (string.Equals(components[index].name, objectName, StringComparison.Ordinal))
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

    private static MainEscapeRuntimeSettings TryLoadRuntimeSettings()
    {
        try
        {
            return MainEscapeRuntimeSettings.Load();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static int ResolveFrameRateOptionIndex(int configuredFrameRate)
    {
        int resolvedFrameRate = configuredFrameRate < 0 ? -1 : configuredFrameRate;

        for (int index = 0; index < FrameRateOptions.Length; index++)
        {
            if (FrameRateOptions[index] == resolvedFrameRate)
            {
                return index;
            }
        }

        int closestIndex = 0;
        int smallestDifference = int.MaxValue;

        for (int index = 0; index < FrameRateOptions.Length; index++)
        {
            if (FrameRateOptions[index] < 0)
            {
                continue;
            }

            int difference = Mathf.Abs(FrameRateOptions[index] - Mathf.Max(30, resolvedFrameRate));

            if (difference < smallestDifference)
            {
                smallestDifference = difference;
                closestIndex = index;
            }
        }

        return closestIndex;
    }

    private static ColorBlock CreateSelectableColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.14f, 0.12f, 0.11f, 0.98f);
        colors.highlightedColor = new Color(0.24f, 0.16f, 0.15f, 1f);
        colors.pressedColor = new Color(0.19f, 0.14f, 0.13f, 1f);
        colors.selectedColor = new Color(0.26f, 0.17f, 0.16f, 1f);
        colors.disabledColor = new Color(0.14f, 0.12f, 0.11f, 0.42f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private static RectTransform CreateRect(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        TMP_FontAsset fontAsset,
        string textValue,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(
            objectName,
            parent,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.text = textValue;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
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

    private static Outline EnsureOutline(Graphic graphic)
    {
        Outline outline = graphic.GetComponent<Outline>();

        if (outline == null)
        {
            outline = graphic.gameObject.AddComponent<Outline>();
        }

        return outline;
    }

#if UNITY_EDITOR
    public void MaterializeLobbyModalUiForAuthoring()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop play mode before materializing lobby modal UI.", this);
            return;
        }

        resolvedPanelRoot = null;
        ResolveMenuEntries();

        RectTransform panelRoot = PanelRoot;

        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(IRLobbyController)} could not resolve {nameof(PanelRoot)} for modal authoring.", this);
            return;
        }

        CreateMissingModalUiForAuthoring(panelRoot);
        ResolveAuthoredModalUi();

        if (!ValidateAuthoredLobbyUi(logErrors: true))
        {
            return;
        }

        activeModal = LobbyModal.None;
        SetModalVisualState();

        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void CreateMissingModalUiForAuthoring(RectTransform panelRoot)
    {
        if (modalBackdrop == null)
        {
            modalBackdrop = CreateRect(
                "IRLobbyModalBackdrop",
                panelRoot,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            Image backdropImage = modalBackdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.56f);
            backdropImage.raycastTarget = true;
        }

        if (optionsPanel == null)
        {
            optionsPanel = CreateModalPanel(
                "IRLobbyOptionsPanel",
                modalBackdrop,
                new Vector2(760f, 468f));
            BuildOptionsPanel(optionsPanel);
        }

        if (creditsPanel == null)
        {
            creditsPanel = CreateModalPanel(
                "IRLobbyCreditsPanel",
                modalBackdrop,
                new Vector2(700f, 360f));
            BuildCreditsPanel(creditsPanel);
        }
    }
#endif
}

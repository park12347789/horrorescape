using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum IRRunModalMode
{
    None,
    FloorClear,
    FinalClear,
    Failure,
    Custom
}

[DisallowMultipleComponent]
public sealed class IRGameClearPanelView : MonoBehaviour
{
    [SerializeField] private RRunSessionController runSessionController;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Image backdropImage;
    [SerializeField] private Image panelImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI elapsedTimeText;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private Button okButton;
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField] private bool useAuthoredVisuals;
    [SerializeField] private bool hideLegacyGameplayPanelsWhileShowing = true;
    [SerializeField] private bool showElapsedTimeOnClear = true;
    [SerializeField] private bool showing;
    [SerializeField] private IRRunModalMode mode;

    private bool inputSuspended;
    private bool okButtonBound;
    private float cachedTimeScale = 1f;
    private string nextFloorPrompt = string.Empty;

    public bool IsShowing => showing;
    public IRRunModalMode Mode => mode;
    public bool IsFailureModal => showing && mode == IRRunModalMode.Failure;

    private void Awake()
    {
        CacheViewReferences();
        ValidateBindings();
        ApplyVisualDefaults();

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(showing);
        }
    }

    private void OnValidate()
    {
        CacheViewReferences();
        ValidateBindings();
    }

    private void Update()
    {
        if (!showing)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        ExecuteAction(IRGameClearPanelInputRouter.ResolveAction(
            mode,
            IRGameClearPanelInputRouter.WasConfirmPressed(keyboard),
            keyboard != null && keyboard.rKey.wasPressedThisFrame,
            keyboard != null && keyboard.lKey.wasPressedThisFrame));
    }

    private void OnEnable()
    {
        BindOkButton();
    }

    private void OnDisable()
    {
        UnbindOkButton();
    }

    public void Configure(
        RRunSessionController configuredRunSessionController,
        RectTransform configuredPanelRoot,
        Image configuredBackdropImage,
        Image configuredPanelImage,
        TextMeshProUGUI configuredTitleText,
        TextMeshProUGUI configuredBodyText,
        TextMeshProUGUI configuredPromptText)
    {
        Configure(
            configuredRunSessionController,
            configuredPanelRoot,
            configuredBackdropImage,
            configuredPanelImage,
            configuredTitleText,
            configuredBodyText,
            null,
            configuredPromptText,
            null);
    }

    public void Configure(
        RRunSessionController configuredRunSessionController,
        RectTransform configuredPanelRoot,
        Image configuredBackdropImage,
        Image configuredPanelImage,
        TextMeshProUGUI configuredTitleText,
        TextMeshProUGUI configuredBodyText,
        TextMeshProUGUI configuredElapsedTimeText,
        TextMeshProUGUI configuredPromptText,
        Button configuredOkButton)
    {
        runSessionController = configuredRunSessionController;
        panelRoot = configuredPanelRoot;
        backdropImage = configuredBackdropImage;
        panelImage = configuredPanelImage;
        titleText = configuredTitleText;
        bodyText = configuredBodyText;
        elapsedTimeText = configuredElapsedTimeText;
        promptText = configuredPromptText;
        okButton = configuredOkButton;
        CacheViewReferences();
        ValidateBindings();
        ApplyVisualDefaults();
        BindOkButton();

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(showing);
        }
    }

    private void ExecuteAction(IRRunModalAction action)
    {
        switch (action)
        {
            case IRRunModalAction.HideAndResume:
                HideAndResume();
                break;
            case IRRunModalAction.RetryCurrentRun:
                RetryCurrentRun();
                break;
            case IRRunModalAction.ReturnToLobby:
                ReturnToLobby();
                break;
        }
    }

    public void BindSessionController(RRunSessionController sessionController)
    {
        runSessionController = sessionController;
    }

    public void BindPlayer(WasdPlayerController player)
    {
        playerController = player;
        playerRuntime = player != null ? player.GetComponent<RPlayerRuntimeReferences>() : null;

        if (playerRuntime != null)
        {
            playerRuntime.CacheExistingReferences();
        }
        else if (player != null)
        {
            Debug.LogError($"{nameof(IRGameClearPanelView)} could not resolve RPlayerRuntimeReferences from the assigned player.", this);
        }

        CachePlayerDependencies();
    }

    public void ShowFloorClear(int clearedFloorNumber, int destinationFloorNumber)
    {
        nextFloorPrompt = destinationFloorNumber > 0 ? $"{destinationFloorNumber}F" : "the next floor";
        ShowInternal(
            IRRunModalMode.FloorClear,
            "\uD074\uB9AC\uC5B4",
            $"{clearedFloorNumber}F route secured.\nProceed to {nextFloorPrompt}.",
            "OK",
            pauseGameplay: true);
    }

    public void ShowFinalClear()
    {
        ShowInternal(
            IRRunModalMode.FinalClear,
            "\uD074\uB9AC\uC5B4",
            "Extraction point reached.\nRoute clear confirmed.",
            "OK",
            pauseGameplay: true);
    }

    public void ShowFailure(string caughtBy)
    {
        ShowInternal(
            IRRunModalMode.Failure,
            "\uC0AC\uB9DD",
            "\uC801\uC5D0\uAC8C \uBD99\uC7A1\uD614\uC2B5\uB2C8\uB2E4.\n\uD604\uC7AC \uCE35\uC5D0\uC11C \uB2E4\uC2DC \uC2DC\uC791\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
            "R \uC7AC\uC2DC\uB3C4    Enter / L \uB85C\uBE44",
            pauseGameplay: true);
    }

    public void Show(string title, string body, string prompt)
    {
        ShowInternal(
            IRRunModalMode.Custom,
            title,
            body,
            prompt,
            pauseGameplay: true);
    }

    public void HideAndResume()
    {
        if (!showing)
        {
            return;
        }

        HidePanel();
        ResumeGameplay();
    }

    public void RetryCurrentRun()
    {
        HidePanel();
        IRGameClearPanelSessionActions.TryRetryCurrentRun(this, runSessionController);
    }

    public void ReturnToLobby()
    {
        HidePanel();
        IRGameClearPanelSessionActions.TryReturnToLobby(this, runSessionController);
    }

    private void SuspendGameplay()
    {
        CachePlayerDependencies();
        inputSuspended = IRGameClearPanelGameplayGate.Suspend(
            inputSuspended,
            playerController,
            playerRuntime,
            ref cachedTimeScale);
    }

    private void ResumeGameplay()
    {
        inputSuspended = IRGameClearPanelGameplayGate.Resume(
            inputSuspended,
            playerController,
            playerRuntime,
            cachedTimeScale);
    }

    private void ShowInternal(
        IRRunModalMode modalMode,
        string title,
        string body,
        string prompt,
        bool pauseGameplay)
    {
        mode = modalMode;
        ApplyVisualDefaults(modalMode);
        HideLegacyGameplayPanels();

        if (titleText != null)
        {
            titleText.text = string.IsNullOrWhiteSpace(title) ? "Status" : title;
        }

        if (bodyText != null)
        {
            bodyText.text = string.IsNullOrWhiteSpace(body) ? "No message." : body;
        }

        if (promptText != null)
        {
            promptText.text = string.IsNullOrWhiteSpace(prompt) ? "Enter : Continue" : prompt;
        }

        ApplyAuthoredLayoutVisibility(modalMode);
        ApplyElapsedTimeText(modalMode);
        showing = true;

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        if (pauseGameplay)
        {
            SuspendGameplay();
        }
    }

    private void HidePanel()
    {
        showing = false;
        mode = IRRunModalMode.None;
        nextFloorPrompt = string.Empty;

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(false);
        }
    }

    private void ApplyVisualDefaults(IRRunModalMode currentMode = IRRunModalMode.None)
    {
        if (useAuthoredVisuals)
        {
            ApplyAuthoredVisualDefaults(currentMode);
            return;
        }

        if (backdropImage != null)
        {
            backdropImage.color = new Color(0.02f, 0.01f, 0.01f, 0.82f);
        }

        if (panelImage != null)
        {
            panelImage.color = new Color(0.14f, 0.09f, 0.07f, 0.98f);
        }

        IRAnalogNoiseUiTheme.ApplyGameClearTheme(panelRoot, backdropImage, panelImage, titleText, bodyText, promptText, currentMode);
    }

    private void ApplyAuthoredVisualDefaults(IRRunModalMode currentMode)
    {
        bool isFailureMode = currentMode == IRRunModalMode.Failure;

        if (backdropImage != null)
        {
            backdropImage.color = new Color(0f, 0f, 0f, isFailureMode ? 0.88f : 0.82f);
        }

        if (panelImage != null)
        {
            panelImage.color = isFailureMode
                ? new Color(1f, 0.88f, 0.84f, 1f)
                : Color.white;
            panelImage.raycastTarget = false;
        }

        StyleAuthoredText(
            titleText,
            isFailureMode ? new Color(0.92f, 0.2f, 0.16f, 1f) : new Color(0.96f, 0.91f, 0.72f, 1f),
            isFailureMode ? 56f : 58f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        StyleAuthoredText(
            bodyText,
            isFailureMode ? new Color(0.9f, 0.78f, 0.7f, 1f) : new Color(0.89f, 0.84f, 0.7f, 1f),
            24f,
            FontStyles.Normal,
            TextAlignmentOptions.Center);
        StyleAuthoredText(
            promptText,
            isFailureMode ? new Color(0.84f, 0.62f, 0.54f, 0.94f) : new Color(0.84f, 0.75f, 0.56f, 0.9f),
            18f,
            FontStyles.Normal,
            TextAlignmentOptions.Center);
    }

    private void CacheViewReferences()
    {
        panelRoot ??= GetComponent<RectTransform>();
        backdropImage ??= GetComponent<Image>();
        okButton ??= GetComponentInChildren<Button>(true);

        if (elapsedTimeText == null)
        {
            Transform elapsedTransform = transform.Find("Panel/ElapsedTimeText");
            elapsedTimeText = elapsedTransform != null ? elapsedTransform.GetComponent<TextMeshProUGUI>() : null;
        }
    }

    private void OnDestroy()
    {
        UnbindOkButton();

        if (inputSuspended)
        {
            Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
        }
    }

    private void ValidateBindings()
    {
        runSessionController = IRGameClearPanelSessionActions.ResolveSessionControllerForScene(runSessionController, gameObject.scene);
    }

    private void CachePlayerDependencies()
    {
        if (playerController == null)
        {
            return;
        }

        if (playerRuntime == null)
        {
            Debug.LogWarning($"{nameof(IRGameClearPanelView)} has no RPlayerRuntimeReferences bound.", this);
        }
    }

    private void BindOkButton()
    {
        if (okButton == null || okButtonBound)
        {
            return;
        }

        okButton.onClick.AddListener(HandleOkButtonClicked);
        okButtonBound = true;
    }

    private void UnbindOkButton()
    {
        if (okButton == null || !okButtonBound)
        {
            return;
        }

        okButton.onClick.RemoveListener(HandleOkButtonClicked);
        okButtonBound = false;
    }

    private void HandleOkButtonClicked()
    {
        if (!showing)
        {
            return;
        }

        ExecuteAction(IRGameClearPanelInputRouter.ResolveAction(
            mode,
            confirmPressed: true,
            retryPressed: false,
            returnToLobbyPressed: false));
    }

    private void ApplyElapsedTimeText(IRRunModalMode modalMode)
    {
        if (elapsedTimeText == null)
        {
            return;
        }

        bool shouldShowElapsedTime = showElapsedTimeOnClear
            && (modalMode == IRRunModalMode.FloorClear || modalMode == IRRunModalMode.FinalClear);
        elapsedTimeText.gameObject.SetActive(shouldShowElapsedTime);
        elapsedTimeText.text = shouldShowElapsedTime
            ? $"\uAC78\uB9B0 \uC2DC\uAC04 {RRunSessionController.FormatElapsedRunTime(ResolveElapsedRunSeconds())}"
            : string.Empty;
    }

    private void ApplyAuthoredLayoutVisibility(IRRunModalMode modalMode)
    {
        if (!useAuthoredVisuals)
        {
            return;
        }

        bool isClearMode = modalMode == IRRunModalMode.FloorClear || modalMode == IRRunModalMode.FinalClear;

        if (bodyText != null)
        {
            bodyText.gameObject.SetActive(!isClearMode && !string.IsNullOrWhiteSpace(bodyText.text));
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(!isClearMode && !string.IsNullOrWhiteSpace(promptText.text));
        }

        if (okButton != null)
        {
            okButton.gameObject.SetActive(isClearMode);
        }
    }

    private float ResolveElapsedRunSeconds()
    {
        return runSessionController != null ? runSessionController.GetElapsedRunSeconds() : 0f;
    }

    private void HideLegacyGameplayPanels()
    {
        if (!hideLegacyGameplayPanelsWhileShowing)
        {
            return;
        }

        IRHudCanvas hudCanvas = GetComponentInParent<IRHudCanvas>();
        hudCanvas?.SetLegacyGameplayPanelsVisible(false);
    }

    private static void StyleAuthoredText(
        TextMeshProUGUI text,
        Color color,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        if (text == null)
        {
            return;
        }

        text.color = color;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.raycastTarget = false;
    }
}

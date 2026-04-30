using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public sealed class IRHudCanvas : MonoBehaviour
{
    [SerializeField] private MonoBehaviour uiSettingsReadModelSource;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private IRInventoryPanelView inventoryPanel;
    [SerializeField] private IRQuickSlotsPanelView quickSlotsPanel;
    [SerializeField] private IRHealthPanelView healthPanel;
    [SerializeField] private IRThreatPanelView threatPanel;
    [SerializeField] private StaminaHudView staminaPanel;
    [SerializeField] private IRGameClearPanelView gameClearPanel;
    [SerializeField] private bool suppressLegacyGameplayPanels = true;

    public RectTransform PanelRoot => panelRoot;
    public IRInventoryPanelView InventoryPanel => inventoryPanel;
    public IRQuickSlotsPanelView QuickSlotsPanel => quickSlotsPanel;
    public IRHealthPanelView HealthPanel => healthPanel;
    public IRThreatPanelView ThreatPanel => threatPanel;
    public StaminaHudView StaminaPanel => staminaPanel;
    public IRGameClearPanelView GameClearPanel => gameClearPanel;
    public bool SuppressLegacyGameplayPanels => suppressLegacyGameplayPanels;
    public IUiSettingsReadModel UiSettings => ResolveUiSettings();

    private void Awake()
    {
        ValidateBindings();
        ApplyLayout();
        IRAnalogNoiseUiTheme.ApplyHudCanvasTheme(this);
        ApplyLegacyGameplayPanelSuppression();
    }

    private void OnValidate()
    {
        ValidateBindings();
        ResolveUiSettings();
    }

    public void Configure(
        Canvas configuredCanvas,
        CanvasScaler configuredCanvasScaler,
        GraphicRaycaster configuredGraphicRaycaster,
        RectTransform configuredPanelRoot,
        IRInventoryPanelView configuredInventoryPanel,
        IRQuickSlotsPanelView configuredQuickSlotsPanel,
        IRHealthPanelView configuredHealthPanel,
        IRThreatPanelView configuredThreatPanel,
        StaminaHudView configuredStaminaPanel,
        IRGameClearPanelView configuredGameClearPanel)
    {
        canvas = configuredCanvas;
        canvasScaler = configuredCanvasScaler;
        graphicRaycaster = configuredGraphicRaycaster;
        panelRoot = configuredPanelRoot;
        inventoryPanel = configuredInventoryPanel;
        quickSlotsPanel = configuredQuickSlotsPanel;
        healthPanel = configuredHealthPanel;
        threatPanel = configuredThreatPanel;
        staminaPanel = configuredStaminaPanel;
        gameClearPanel = configuredGameClearPanel;
        ValidateBindings();
        ApplyLayout();
        IRAnalogNoiseUiTheme.ApplyHudCanvasTheme(this);
        ApplyLegacyGameplayPanelSuppression();
    }

    public string GetLayoutSummary()
    {
        return canvasScaler == null
            ? $"screen={Screen.width}x{Screen.height}"
            : $"CanvasScaler ref={canvasScaler.referenceResolution} match={canvasScaler.matchWidthOrHeight:0.00} screen={Screen.width}x{Screen.height}";
    }

    public void ApplyLegacyGameplayPanelSuppression()
    {
        if (!suppressLegacyGameplayPanels)
        {
            return;
        }

        SetLegacyGameplayPanelsVisible(false);
    }

    public void SetLegacyGameplayPanelsVisible(bool visible)
    {
        bool resolvedVisible = visible && !suppressLegacyGameplayPanels;
        SetPanelRootVisible(healthPanel != null ? healthPanel.PanelRoot : null, resolvedVisible);
        SetPanelRootVisible(quickSlotsPanel != null ? quickSlotsPanel.PanelRoot : null, resolvedVisible);
        SetPanelRootVisible(inventoryPanel != null ? inventoryPanel.PanelRoot : null, resolvedVisible);
    }

    private void ValidateBindings()
    {
        if (canvas == null)
        {
            Debug.LogError($"{nameof(IRHudCanvas)} is missing a Canvas reference.", this);
        }

        if (canvasScaler == null)
        {
            Debug.LogError($"{nameof(IRHudCanvas)} is missing a CanvasScaler reference.", this);
        }

        if (graphicRaycaster == null)
        {
            Debug.LogError($"{nameof(IRHudCanvas)} is missing a GraphicRaycaster reference.", this);
        }

        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(IRHudCanvas)} is missing its panel root reference.", this);
        }

        if (!suppressLegacyGameplayPanels)
        {
            if (inventoryPanel == null)
            {
                Debug.LogError($"{nameof(IRHudCanvas)} is missing its inventory panel reference.", this);
            }

            if (quickSlotsPanel == null)
            {
                Debug.LogError($"{nameof(IRHudCanvas)} is missing its quick slots panel reference.", this);
            }

            if (healthPanel == null)
            {
                Debug.LogError($"{nameof(IRHudCanvas)} is missing its health panel reference.", this);
            }
        }

        if (threatPanel == null)
        {
            Debug.LogError($"{nameof(IRHudCanvas)} is missing its threat panel reference.", this);
        }

        if (staminaPanel == null)
        {
            Debug.LogError($"{nameof(IRHudCanvas)} is missing its stamina panel reference.", this);
        }

        if (gameClearPanel == null)
        {
            Debug.LogError($"{nameof(IRHudCanvas)} is missing its game clear panel reference.", this);
        }
    }

    private void ApplyLayout()
    {
        IUiSettingsReadModel uiSettings = ResolveUiSettings();

        if (canvasScaler != null)
        {
            canvasScaler.referenceResolution = uiSettings.HudReferenceResolution;
            canvasScaler.matchWidthOrHeight = uiSettings.HudReferenceResolutionMatch;
        }

        inventoryPanel?.ApplyLayout(uiSettings, uiSettings.InventoryVisibleSlotCount);
        quickSlotsPanel?.ApplyLayout(uiSettings, uiSettings.QuickSlotVisibleCount);
        healthPanel?.ApplyLayout(uiSettings);
        staminaPanel?.ApplyLayout();
    }

    private IUiSettingsReadModel ResolveUiSettings()
    {
        IUiSettingsReadModel readModel = UiSettingsOwner.Resolve(this, uiSettingsReadModelSource);

        if (uiSettingsReadModelSource == null && readModel is MonoBehaviour behaviour)
        {
            uiSettingsReadModelSource = behaviour;
        }

        return readModel;
    }

    private static void SetPanelRootVisible(RectTransform root, bool visible)
    {
        if (root != null && root.gameObject.activeSelf != visible)
        {
            root.gameObject.SetActive(visible);
        }
    }
}

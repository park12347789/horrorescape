using UnityEngine;

[DisallowMultipleComponent]
public sealed class IRPlayerHealthHudBinder : MonoBehaviour, IRebuildHudBinder
{
    [SerializeField] private IRHudCanvas hudCanvas;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;

    private PlayerHealth playerHealth;
    private PlayerFlashlightBattery flashlightBattery;
    private IRHealthPanelView panelView;
    private PlayerHealth subscribedPlayerHealth;
    private PlayerFlashlightBattery subscribedFlashlightBattery;
    private bool refreshDuringRecovery;

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

    private void Update()
    {
        if (!refreshDuringRecovery || playerHealth == null)
        {
            return;
        }

        RefreshView();
        refreshDuringRecovery = playerHealth.RecoveryNormalized > 0.001f;
    }

    private void OnEnable()
    {
        SubscribeToDependencies();
        RefreshView();
    }

    private void OnDisable()
    {
        UnsubscribeFromDependencies();
        refreshDuringRecovery = false;
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
        playerHealth = runtime != null ? runtime.PlayerHealth : GetComponent<PlayerHealth>();
        flashlightBattery = runtime != null ? runtime.FlashlightBattery : GetComponent<PlayerFlashlightBattery>();
    }

    private void ResolvePanelView()
    {
        panelView = hudCanvas != null ? hudCanvas.HealthPanel : null;
    }

    private void SubscribeToDependencies()
    {
        if (playerHealth != null && subscribedPlayerHealth != playerHealth)
        {
            playerHealth.Changed += HandlePresentationChanged;
            subscribedPlayerHealth = playerHealth;
        }

        if (flashlightBattery != null && subscribedFlashlightBattery != flashlightBattery)
        {
            flashlightBattery.Changed += HandlePresentationChanged;
            subscribedFlashlightBattery = flashlightBattery;
        }
    }

    private void UnsubscribeFromDependencies()
    {
        if (subscribedPlayerHealth != null)
        {
            subscribedPlayerHealth.Changed -= HandlePresentationChanged;
            subscribedPlayerHealth = null;
        }

        if (subscribedFlashlightBattery != null)
        {
            subscribedFlashlightBattery.Changed -= HandlePresentationChanged;
            subscribedFlashlightBattery = null;
        }
    }

    private void HandlePresentationChanged()
    {
        RefreshView();
        refreshDuringRecovery = playerHealth != null && playerHealth.RecoveryNormalized > 0.001f;
    }

    private void RefreshView()
    {
        if (panelView == null || playerHealth == null)
        {
            return;
        }

        if (hudCanvas != null && hudCanvas.SuppressLegacyGameplayPanels)
        {
            hudCanvas.ApplyLegacyGameplayPanelSuppression();
            return;
        }

        panelView.Render(new HealthPanelPresentation(
            playerHealth.CurrentHealth,
            playerHealth.MaxHealth,
            playerHealth.HealthNormalized,
            playerHealth.RecoveryNormalized,
            flashlightBattery != null ? flashlightBattery.ChargeNormalized : 0f,
            flashlightBattery != null ? flashlightBattery.StoredBatteryCount : 0));
    }
}

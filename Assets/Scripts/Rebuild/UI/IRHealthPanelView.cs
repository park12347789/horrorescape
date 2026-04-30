using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IRHealthPanelView : MonoBehaviour
{
    [SerializeField] private MonoBehaviour uiSettingsReadModelSource;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TextMeshProUGUI batteryValueText;
    [SerializeField] private Image batteryFillImage;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image pulseOverlay;
    [SerializeField] private RectTransform dividerRoot;
    [SerializeField] private List<Image> dividers = new();

    public RectTransform PanelRoot => panelRoot;
    public TextMeshProUGUI BatteryValueText => batteryValueText;
    public Image BatteryFillImage => batteryFillImage;
    public TextMeshProUGUI ValueText => valueText;
    public Image FillImage => fillImage;
    public Image PulseOverlay => pulseOverlay;
    public RectTransform DividerRoot => dividerRoot;
    public List<Image> Dividers => dividers;

    private void Awake()
    {
        ResolveUiSettings();
        ValidateBindings();
        ApplyLayout(ResolveUiSettings());
    }

    private void OnValidate()
    {
        ResolveUiSettings();
        ValidateBindings();
    }

    public void Configure(
        RectTransform configuredPanelRoot,
        TextMeshProUGUI configuredBatteryValueText,
        Image configuredBatteryFillImage,
        TextMeshProUGUI configuredValueText,
        Image configuredFillImage,
        Image configuredPulseOverlay,
        RectTransform configuredDividerRoot,
        List<Image> configuredDividers)
    {
        panelRoot = configuredPanelRoot;
        batteryValueText = configuredBatteryValueText;
        batteryFillImage = configuredBatteryFillImage;
        valueText = configuredValueText;
        fillImage = configuredFillImage;
        pulseOverlay = configuredPulseOverlay;
        dividerRoot = configuredDividerRoot;
        dividers = configuredDividers ?? new List<Image>();
        ResolveUiSettings();
        ValidateBindings();
        ApplyLayout(ResolveUiSettings());
    }

    public void Render(in HealthPanelPresentation presentation)
    {
        ApplyLayout(ResolveUiSettings());

        if (IRPixelHudArt.ApplyHealthVisuals(this, presentation))
        {
            UpdateDividers(0);
            return;
        }

        float batteryNormalized = Mathf.Clamp01(presentation.FlashlightChargeNormalized);
        float healthNormalized = presentation.MaxHealth <= 0
            ? 0f
            : Mathf.Clamp01(presentation.CurrentHealth / (float)presentation.MaxHealth);

        if (batteryValueText != null)
        {
            batteryValueText.text = $"BAT {Mathf.RoundToInt(batteryNormalized * 100f)}%  x{presentation.StoredBatteryCount}";
            batteryValueText.color = batteryNormalized <= 0.2f
                ? new Color(1f, 0.62f, 0.36f, 1f)
                : new Color(0.96f, 0.98f, 1f, 1f);
        }

        if (batteryFillImage != null)
        {
            ApplyHorizontalFill(batteryFillImage, batteryNormalized);
            batteryFillImage.color = Color.Lerp(
                new Color(0.95f, 0.36f, 0.16f, 1f),
                new Color(1f, 0.86f, 0.22f, 1f),
                batteryNormalized);
        }

        if (valueText != null)
        {
            valueText.text = $"HP {presentation.CurrentHealth}/{presentation.MaxHealth}";
            valueText.color = presentation.CurrentHealth <= 1
                ? new Color(1f, 0.62f, 0.5f, 1f)
                : new Color(0.96f, 0.98f, 1f, 1f);
        }

        if (fillImage != null)
        {
            ApplyHorizontalFill(fillImage, healthNormalized);
            fillImage.color = presentation.CurrentHealth <= 1
                ? new Color(0.98f, 0.28f, 0.18f, 1f)
                : new Color(0.86f, 0.2f, 0.2f, 1f);
        }

        if (pulseOverlay != null)
        {
            float pulse = 0.45f + Mathf.PingPong(Time.time * 2.8f, 0.45f);
            pulseOverlay.color = new Color(1f, 0.94f, 0.82f, presentation.RecoveryNormalized > 0.001f ? pulse * 0.55f : 0f);
        }

        UpdateDividers(presentation.MaxHealth);
        IRAnalogNoiseUiTheme.ApplyHealthPanelTheme(this, presentation);
    }

    public void ApplyLayout(IUiSettingsReadModel uiSettings)
    {
        if (uiSettings == null || panelRoot == null)
        {
            return;
        }

        if (IRPixelHudArt.HasAuthoredHealthLayout(this))
        {
            return;
        }

        panelRoot.anchorMin = new Vector2(0f, 1f);
        panelRoot.anchorMax = new Vector2(0f, 1f);
        panelRoot.pivot = new Vector2(0f, 1f);
        panelRoot.anchoredPosition = new Vector2(uiSettings.HealthPanelMargin.x, -uiSettings.HealthPanelMargin.y);
        panelRoot.sizeDelta = uiSettings.HealthPanelSize;
    }

    private void ValidateBindings()
    {
        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(IRHealthPanelView)} is missing its panel root reference.", this);
        }
    }

    private void UpdateDividers(int maxHealth)
    {
        for (int index = 0; index < dividers.Count; index++)
        {
            Image divider = dividers[index];

            if (divider == null)
            {
                continue;
            }

            bool visible = index < Mathf.Max(0, maxHealth - 1);
            divider.gameObject.SetActive(visible);
        }
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

using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UiSettingsOwner : MonoBehaviour, IUiSettingsReadModel
{
    [SerializeField] private MainEscapeRuntimeSettings runtimeSettings;

    // Keeps authored scenes functional even if a local owner is temporarily missing.
    private static readonly IUiSettingsReadModel FallbackReadModel = new MainEscapeRuntimeSettingsReadModel();

    public MainEscapeRuntimeSettings RuntimeSettings => ResolveSettings();

    public Vector2 HudReferenceResolution => ResolveSettings().HudReferenceResolution;
    public float HudReferenceResolutionMatch => ResolveSettings().HudReferenceResolutionMatch;
    public Vector2 InventoryPanelSize => ResolveSettings().InventoryPanelSize;
    public Vector2 InventoryPanelMargin => ResolveSettings().InventoryPanelMargin;
    public Vector2 InventorySlotSize => ResolveSettings().InventorySlotSize;
    public Vector2 InventorySlotSpacing => ResolveSettings().InventorySlotSpacing;
    public int InventorySlotColumnCount => ResolveSettings().InventorySlotColumnCount;
    public int InventorySlotRowCount => ResolveSettings().InventorySlotRowCount;
    public int InventoryVisibleSlotCount => Mathf.Max(1, InventorySlotColumnCount * InventorySlotRowCount);
    public Vector2 QuickSlotPanelSize => ResolveSettings().QuickSlotPanelSize;
    public Vector2 QuickSlotPanelMargin => ResolveSettings().QuickSlotPanelMargin;
    public Vector2 QuickSlotCardSize => ResolveSettings().QuickSlotCardSize;
    public Vector2 QuickSlotCardSpacing => ResolveSettings().QuickSlotCardSpacing;
    public int QuickSlotVisibleCount => ResolveSettings().QuickSlotVisibleCount;
    public Vector2 HealthPanelSize => ResolveSettings().HealthPanelSize;
    public Vector2 HealthPanelMargin => ResolveSettings().HealthPanelMargin;
    public bool UseTemporaryAnalogNoiseUi => ResolveSettings().UseTemporaryAnalogNoiseUi;

    private void OnValidate()
    {
        runtimeSettings ??= TryLoadSettings();
    }

    public static UiSettingsOwner ResolveOwner(Component context, UiSettingsOwner explicitOwner = null)
    {
        if (explicitOwner != null)
        {
            return explicitOwner;
        }

        if (context == null)
        {
            return null;
        }

        UiSettingsOwner localOwner = context.GetComponent<UiSettingsOwner>();

        if (localOwner != null)
        {
            return localOwner;
        }

        return context.GetComponentInParent<UiSettingsOwner>(true);
    }

    public static IUiSettingsReadModel ResolveReadModel(Component context, UiSettingsOwner explicitOwner = null)
    {
        UiSettingsOwner owner = ResolveOwner(context, explicitOwner);
        return owner != null ? owner : FallbackReadModel;
    }

    public static IUiSettingsReadModel Resolve(Component context, MonoBehaviour explicitSource = null)
    {
        if (explicitSource is IUiSettingsReadModel readModel)
        {
            return readModel;
        }

        return ResolveReadModel(context);
    }

    private MainEscapeRuntimeSettings ResolveSettings()
    {
        runtimeSettings ??= TryLoadSettings();

        if (runtimeSettings == null)
        {
            throw new InvalidOperationException(
                $"{nameof(UiSettingsOwner)} could not resolve a {nameof(MainEscapeRuntimeSettings)} asset.");
        }

        return runtimeSettings;
    }

    private static MainEscapeRuntimeSettings TryLoadSettings()
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

    private sealed class MainEscapeRuntimeSettingsReadModel : IUiSettingsReadModel
    {
        private MainEscapeRuntimeSettings Settings => MainEscapeRuntimeSettings.Load();

        public Vector2 HudReferenceResolution => Settings.HudReferenceResolution;
        public float HudReferenceResolutionMatch => Settings.HudReferenceResolutionMatch;
        public Vector2 InventoryPanelSize => Settings.InventoryPanelSize;
        public Vector2 InventoryPanelMargin => Settings.InventoryPanelMargin;
        public Vector2 InventorySlotSize => Settings.InventorySlotSize;
        public Vector2 InventorySlotSpacing => Settings.InventorySlotSpacing;
        public int InventorySlotColumnCount => Settings.InventorySlotColumnCount;
        public int InventorySlotRowCount => Settings.InventorySlotRowCount;
        public int InventoryVisibleSlotCount => Mathf.Max(1, InventorySlotColumnCount * InventorySlotRowCount);
        public Vector2 QuickSlotPanelSize => Settings.QuickSlotPanelSize;
        public Vector2 QuickSlotPanelMargin => Settings.QuickSlotPanelMargin;
        public Vector2 QuickSlotCardSize => Settings.QuickSlotCardSize;
        public Vector2 QuickSlotCardSpacing => Settings.QuickSlotCardSpacing;
        public int QuickSlotVisibleCount => Settings.QuickSlotVisibleCount;
        public Vector2 HealthPanelSize => Settings.HealthPanelSize;
        public Vector2 HealthPanelMargin => Settings.HealthPanelMargin;
        public bool UseTemporaryAnalogNoiseUi => Settings.UseTemporaryAnalogNoiseUi;
    }
}

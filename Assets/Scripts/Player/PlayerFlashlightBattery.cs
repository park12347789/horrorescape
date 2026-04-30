using System;

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WasdPlayerController))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(FlashlightStateOwner))]
public sealed class PlayerFlashlightBattery : MonoBehaviour
{
    [SerializeField, Min(5f)] private float maxChargeSeconds = 75f;
    [SerializeField, Min(0f)] private float emptyIntensityScale = 0.02f;
    [SerializeField, Range(0f, 1f)] private float emptyVolumeScale = 0f;
    [SerializeField, Range(0.2f, 1f)] private float batteryVisibilityCurve = 0.4f;
    [SerializeField] private float currentChargeSeconds;

    private FlashlightStateOwner flashlightStateOwner;
    private int publishedChargePercent = int.MinValue;
    private int publishedStoredBatteryCount = int.MinValue;

    public event Action Changed;

    internal float EmptyIntensityScaleConfig => emptyIntensityScale;
    internal float EmptyVolumeScaleConfig => emptyVolumeScale;
    internal float BatteryVisibilityCurveConfig => batteryVisibilityCurve;

    public float ChargeNormalized => flashlightStateOwner != null
        ? flashlightStateOwner.ChargeNormalized
        : ResolveFallbackChargeNormalized();

    public int StoredBatteryCount => flashlightStateOwner != null
        ? flashlightStateOwner.StoredBatteryCount
        : 0;

    public bool IsFullCharge => flashlightStateOwner != null
        ? flashlightStateOwner.IsFullCharge
        : currentChargeSeconds >= ResolveMaxChargeSeconds() - 0.01f;

    private void Awake()
    {
        ResolveDependencies();
        ResolveInitialChargeSeconds();
        PublishChanged(force: true);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        BindOwnerEvents();
        PublishChanged(force: true);
    }

    private void OnDisable()
    {
        UnbindOwnerEvents();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        currentChargeSeconds = Mathf.Clamp(currentChargeSeconds, 0f, ResolveMaxChargeSeconds());
    }

    public void RefillCharge(float normalizedAmount)
    {
        ResolveDependencies();

        if (flashlightStateOwner != null)
        {
            flashlightStateOwner.RefillCharge(normalizedAmount);
            return;
        }

        float resolvedMaxChargeSeconds = ResolveMaxChargeSeconds();
        currentChargeSeconds = Mathf.Clamp(
            currentChargeSeconds + (Mathf.Clamp01(normalizedAmount) * resolvedMaxChargeSeconds),
            0f,
            resolvedMaxChargeSeconds);
        PublishChanged(force: true);
    }

    public bool TryUseStoredBattery()
    {
        ResolveDependencies();
        return flashlightStateOwner != null && flashlightStateOwner.TryUseStoredBattery();
    }

    public void SetChargeNormalized(float normalizedCharge)
    {
        ResolveDependencies();

        if (flashlightStateOwner != null)
        {
            flashlightStateOwner.SetChargeNormalized(normalizedCharge);
            return;
        }

        currentChargeSeconds = Mathf.Clamp01(normalizedCharge) * ResolveMaxChargeSeconds();
        PublishChanged(force: true);
    }

    internal float ResolveMaxChargeSeconds()
    {
        maxChargeSeconds = Mathf.Max(5f, maxChargeSeconds);
        return maxChargeSeconds;
    }

    internal float ResolveInitialChargeSeconds()
    {
        float resolvedMaxChargeSeconds = ResolveMaxChargeSeconds();

        if (currentChargeSeconds <= 0f || currentChargeSeconds > resolvedMaxChargeSeconds)
        {
            currentChargeSeconds = resolvedMaxChargeSeconds;
        }

        return currentChargeSeconds;
    }

    internal void SetLegacyCurrentChargeSecondsFromOwner(float chargeSeconds)
    {
        currentChargeSeconds = Mathf.Clamp(chargeSeconds, 0f, ResolveMaxChargeSeconds());
    }

    internal static int ResolveDisplayChargePercent(float chargeNormalized)
    {
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(chargeNormalized) * 100f), 0, 100);
    }

    private void ResolveDependencies()
    {
        flashlightStateOwner ??= GetComponent<FlashlightStateOwner>();

        if (flashlightStateOwner == null && Application.isPlaying)
        {
            flashlightStateOwner = gameObject.AddComponent<FlashlightStateOwner>();
        }
    }

    private void BindOwnerEvents()
    {
        if (flashlightStateOwner == null)
        {
            return;
        }

        flashlightStateOwner.Changed -= HandleOwnerChanged;
        flashlightStateOwner.Changed += HandleOwnerChanged;
    }

    private void UnbindOwnerEvents()
    {
        if (flashlightStateOwner == null)
        {
            return;
        }

        flashlightStateOwner.Changed -= HandleOwnerChanged;
    }

    private void HandleOwnerChanged()
    {
        PublishChanged();
    }

    private float ResolveFallbackChargeNormalized()
    {
        float resolvedMaxChargeSeconds = ResolveMaxChargeSeconds();
        return resolvedMaxChargeSeconds <= 0.01f
            ? 0f
            : Mathf.Clamp01(currentChargeSeconds / resolvedMaxChargeSeconds);
    }

    private void PublishChanged(bool force = false)
    {
        float charge = ChargeNormalized;
        int chargePercent = ResolveDisplayChargePercent(charge);
        int storedBatteryCount = StoredBatteryCount;

        if (!force
            && publishedChargePercent == chargePercent
            && publishedStoredBatteryCount == storedBatteryCount)
        {
            return;
        }

        publishedChargePercent = chargePercent;
        publishedStoredBatteryCount = storedBatteryCount;
        Changed?.Invoke();
    }
}

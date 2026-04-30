using System;

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WasdPlayerController))]
public sealed class FlashlightStateOwner : MonoBehaviour, IFlashlightStateReadModel
{
    private const float BatteryDrainRateMultiplier = 0.5f;
    private const float DefaultMaxChargeSeconds = 75f;
    private const float DefaultEmptyIntensityScale = 0.02f;
    private const float DefaultEmptyVolumeScale = 0f;
    private const float DefaultBatteryVisibilityCurve = 0.4f;

    private WasdPlayerController playerController;
    private PlayerInventory inventory;
    private PlayerFlashlightEquipment equipmentFacade;
    private PlayerFlashlightBattery batteryFacade;
    private PlayerInventory subscribedInventory;
    private bool hasFlashlight;
    private bool flashlightEnabled;
    private float currentChargeSeconds;
    private bool chargeInitialized;
    private bool suppressInventoryEvents;
    private bool publishedHasFlashlight;
    private bool publishedFlashlightEnabled;
    private int publishedChargePercent = int.MinValue;
    private int publishedStoredBatteryCount = int.MinValue;

    public event Action Changed;

    public bool HasFlashlight => hasFlashlight;
    public bool IsFlashlightEnabled => hasFlashlight && flashlightEnabled;
    public float ChargeNormalized
    {
        get
        {
            float maxChargeSeconds = ResolveMaxChargeSeconds();
            return maxChargeSeconds <= 0.01f
                ? 0f
                : Mathf.Clamp01(currentChargeSeconds / maxChargeSeconds);
        }
    }

    public int StoredBatteryCount => inventory != null
        ? inventory.GetQuantity(PrototypeItemCatalog.FlashlightBatteryItemId)
        : 0;

    public bool IsFullCharge => currentChargeSeconds >= ResolveMaxChargeSeconds() - 0.01f;

    private void Awake()
    {
        ResolveDependencies();
        EnsureChargeInitialized();
        EnsureStartingOwnership();
        RefreshState(forceNotify: true, applyConfiguredEnabledOnAcquire: true);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        EnsureChargeInitialized();
        EnsureStartingOwnership();
        RefreshState(forceNotify: true, applyConfiguredEnabledOnAcquire: true);
    }

    private void OnDisable()
    {
        UnbindInventoryEvents();
    }

    private void Update()
    {
        EnsureDependenciesResolved();

        if (playerController == null)
        {
            return;
        }

        bool flashlightStateChanged = false;
        bool batteryPresentationChanged = false;
        bool playFlashlightToggleOnSound = false;

        if (playerController.ConsumeFlashlightTogglePressedThisFrame())
        {
            bool enabledAfterToggle = !IsFlashlightEnabled;
            bool toggleChanged = SetFlashlightEnabledStateWithoutNotify(enabledAfterToggle);
            flashlightStateChanged |= toggleChanged;
            playFlashlightToggleOnSound = ShouldPlayFlashlightToggleOnSound(toggleChanged, enabledAfterToggle);
        }

        if (playerController.IsFlashlightSwitchOn)
        {
            float previousChargeSeconds = currentChargeSeconds;
            currentChargeSeconds = Mathf.Max(0f, currentChargeSeconds - (Time.deltaTime * BatteryDrainRateMultiplier));

            if (!Mathf.Approximately(previousChargeSeconds, currentChargeSeconds))
            {
                MirrorChargeToFacade();
                batteryPresentationChanged = true;
            }
        }

        if (currentChargeSeconds <= 0.001f)
        {
            batteryPresentationChanged |= TryConsumeStoredBatteryWithoutNotify();
        }

        if (!flashlightStateChanged && !batteryPresentationChanged)
        {
            return;
        }

        if (flashlightStateChanged)
        {
            ApplyFlashlightState();
        }

        if (playFlashlightToggleOnSound)
        {
            equipmentFacade?.TryPlayFlashlightToggleOnSound();
        }

        ApplyBatteryPresentation();
        PublishChanged();
    }

    public bool GrantFlashlight()
    {
        EnsureDependenciesResolved();

        if (inventory == null)
        {
            return false;
        }

        suppressInventoryEvents = true;

        try
        {
            if (!inventory.AddItem(
                    PrototypeItemCatalog.FlashlightItemId,
                    PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.FlashlightItemId),
                    1))
            {
                return false;
            }
        }
        finally
        {
            suppressInventoryEvents = false;
        }

        RefreshState(forceNotify: true, applyConfiguredEnabledOnAcquire: true);
        return true;
    }

    public bool EnsureFlashlightEquipped(bool enabled)
    {
        EnsureDependenciesResolved();

        if (inventory == null)
        {
            return false;
        }

        if (!inventory.HasItem(PrototypeItemCatalog.FlashlightItemId))
        {
            suppressInventoryEvents = true;

            try
            {
                inventory.AddItem(
                    PrototypeItemCatalog.FlashlightItemId,
                    PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.FlashlightItemId),
                    1);
            }
            finally
            {
                suppressInventoryEvents = false;
            }
        }

        RefreshState(forceNotify: false, applyConfiguredEnabledOnAcquire: false);

        if (!hasFlashlight)
        {
            return false;
        }

        SetFlashlightEnabledStateWithoutNotify(enabled);
        ApplyFlashlightState();
        ApplyBatteryPresentation();
        PublishChanged(force: true);
        return true;
    }

    public bool SetFlashlightEnabledState(bool enabled)
    {
        EnsureDependenciesResolved();

        if (!SetFlashlightEnabledStateWithoutNotify(enabled))
        {
            return false;
        }

        ApplyFlashlightState();
        ApplyBatteryPresentation();
        PublishChanged(force: true);
        return true;
    }

    public void RefillCharge(float normalizedAmount)
    {
        EnsureDependenciesResolved();
        EnsureChargeInitialized();
        float maxChargeSeconds = ResolveMaxChargeSeconds();
        currentChargeSeconds = Mathf.Clamp(
            currentChargeSeconds + (Mathf.Clamp01(normalizedAmount) * maxChargeSeconds),
            0f,
            maxChargeSeconds);
        MirrorChargeToFacade();
        ApplyBatteryPresentation();
        PublishChanged(force: true);
    }

    public bool TryUseStoredBattery()
    {
        EnsureDependenciesResolved();

        if (inventory == null || IsFullCharge)
        {
            return false;
        }

        if (!TryConsumeStoredBatteryWithoutNotify())
        {
            return false;
        }

        ApplyBatteryPresentation();
        PublishChanged(force: true);
        return true;
    }

    public void SetChargeNormalized(float normalizedCharge)
    {
        EnsureDependenciesResolved();
        EnsureChargeInitialized();
        currentChargeSeconds = Mathf.Clamp01(normalizedCharge) * ResolveMaxChargeSeconds();
        MirrorChargeToFacade();
        ApplyBatteryPresentation();
        PublishChanged(force: true);
    }

    private void EnsureDependenciesResolved()
    {
        if (playerController == null || inventory == null || equipmentFacade == null || batteryFacade == null)
        {
            ResolveDependencies();
        }
    }

    private void ResolveDependencies()
    {
        playerController ??= GetComponent<WasdPlayerController>();
        equipmentFacade ??= GetComponent<PlayerFlashlightEquipment>();
        batteryFacade ??= GetComponent<PlayerFlashlightBattery>();

        PlayerInventory resolvedInventory = inventory != null
            ? inventory
            : GetComponent<PlayerInventory>();

        if (ReferenceEquals(resolvedInventory, inventory))
        {
            return;
        }

        UnbindInventoryEvents();
        inventory = resolvedInventory;
        BindInventoryEvents();
    }

    private void BindInventoryEvents()
    {
        if (inventory == null || ReferenceEquals(subscribedInventory, inventory))
        {
            return;
        }

        inventory.Changed += HandleInventoryChanged;
        subscribedInventory = inventory;
    }

    private void UnbindInventoryEvents()
    {
        if (subscribedInventory == null)
        {
            return;
        }

        subscribedInventory.Changed -= HandleInventoryChanged;
        subscribedInventory = null;
    }

    private void EnsureStartingOwnership()
    {
        if (equipmentFacade == null || !equipmentFacade.StartWithFlashlightConfig || inventory == null)
        {
            return;
        }

        if (inventory.HasItem(PrototypeItemCatalog.FlashlightItemId))
        {
            return;
        }

        suppressInventoryEvents = true;

        try
        {
            inventory.AddItem(
                PrototypeItemCatalog.FlashlightItemId,
                PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.FlashlightItemId),
                1);
        }
        finally
        {
            suppressInventoryEvents = false;
        }
    }

    private void EnsureChargeInitialized()
    {
        ResolveDependencies();

        if (chargeInitialized)
        {
            ClampChargeToConfiguredBounds();
            return;
        }

        currentChargeSeconds = batteryFacade != null
            ? batteryFacade.ResolveInitialChargeSeconds()
            : ResolveMaxChargeSeconds();
        chargeInitialized = true;
        MirrorChargeToFacade();
    }

    private void HandleInventoryChanged()
    {
        if (suppressInventoryEvents)
        {
            return;
        }

        RefreshState(forceNotify: true, applyConfiguredEnabledOnAcquire: true);
    }

    private void RefreshState(bool forceNotify, bool applyConfiguredEnabledOnAcquire)
    {
        EnsureDependenciesResolved();
        EnsureChargeInitialized();

        bool previouslyOwned = hasFlashlight;
        bool previouslyEnabled = IsFlashlightEnabled;
        hasFlashlight = inventory != null && inventory.HasItem(PrototypeItemCatalog.FlashlightItemId);

        if (!hasFlashlight)
        {
            flashlightEnabled = false;
        }
        else if (!previouslyOwned && applyConfiguredEnabledOnAcquire)
        {
            flashlightEnabled = equipmentFacade != null && equipmentFacade.StartEnabledConfig;
        }

        ClampChargeToConfiguredBounds();
        bool flashlightStateChanged = previouslyOwned != hasFlashlight || previouslyEnabled != IsFlashlightEnabled;

        if (forceNotify || flashlightStateChanged)
        {
            ApplyFlashlightState();
        }

        ApplyBatteryPresentation();

        if (forceNotify || flashlightStateChanged)
        {
            PublishChanged(forceNotify);
        }
    }

    private bool SetFlashlightEnabledStateWithoutNotify(bool enabled)
    {
        EnsureDependenciesResolved();
        bool previouslyEnabled = IsFlashlightEnabled;

        if (!hasFlashlight)
        {
            return false;
        }

        flashlightEnabled = enabled;
        return previouslyEnabled != IsFlashlightEnabled;
    }

    private static bool ShouldPlayFlashlightToggleOnSound(bool flashlightStateChanged, bool enabledAfterToggle)
    {
        return flashlightStateChanged && enabledAfterToggle;
    }

    private bool TryConsumeStoredBatteryWithoutNotify()
    {
        EnsureDependenciesResolved();

        if (inventory == null)
        {
            return false;
        }

        suppressInventoryEvents = true;

        try
        {
            if (!inventory.RemoveItem(PrototypeItemCatalog.FlashlightBatteryItemId))
            {
                return false;
            }
        }
        finally
        {
            suppressInventoryEvents = false;
        }

        currentChargeSeconds = ResolveMaxChargeSeconds();
        MirrorChargeToFacade();
        PrototypeAudioManager.TryPlayBatteryReplace();
        return true;
    }

    private void ClampChargeToConfiguredBounds()
    {
        currentChargeSeconds = Mathf.Clamp(currentChargeSeconds, 0f, ResolveMaxChargeSeconds());
        MirrorChargeToFacade();
    }

    private void ApplyFlashlightState()
    {
        if (playerController == null)
        {
            return;
        }

        playerController.SetFlashlightAvailability(hasFlashlight);
        playerController.SetFlashlightEnabled(flashlightEnabled);
    }

    private void ApplyBatteryPresentation()
    {
        if (playerController == null)
        {
            return;
        }

        float charge = ChargeNormalized;
        float visibilityCharge = Mathf.Pow(Mathf.Clamp01(charge), ResolveBatteryVisibilityCurve());
        float intensityScale = Mathf.Lerp(ResolveEmptyIntensityScale(), 1f, visibilityCharge);
        float volumeScale = Mathf.Lerp(ResolveEmptyVolumeScale(), 1f, charge);
        playerController.SetFlashlightBatteryScale(intensityScale, volumeScale);
    }

    private float ResolveMaxChargeSeconds()
    {
        return batteryFacade != null
            ? batteryFacade.ResolveMaxChargeSeconds()
            : DefaultMaxChargeSeconds;
    }

    private float ResolveEmptyIntensityScale()
    {
        return batteryFacade != null
            ? batteryFacade.EmptyIntensityScaleConfig
            : DefaultEmptyIntensityScale;
    }

    private float ResolveEmptyVolumeScale()
    {
        return batteryFacade != null
            ? batteryFacade.EmptyVolumeScaleConfig
            : DefaultEmptyVolumeScale;
    }

    private float ResolveBatteryVisibilityCurve()
    {
        return batteryFacade != null
            ? batteryFacade.BatteryVisibilityCurveConfig
            : DefaultBatteryVisibilityCurve;
    }

    private void MirrorChargeToFacade()
    {
        batteryFacade?.SetLegacyCurrentChargeSecondsFromOwner(currentChargeSeconds);
    }

    private void PublishChanged(bool force = false)
    {
        bool hasFlashlightNow = HasFlashlight;
        bool flashlightEnabledNow = IsFlashlightEnabled;
        int chargePercent = PlayerFlashlightBattery.ResolveDisplayChargePercent(ChargeNormalized);
        int storedBatteryCount = StoredBatteryCount;

        if (!force
            && publishedHasFlashlight == hasFlashlightNow
            && publishedFlashlightEnabled == flashlightEnabledNow
            && publishedChargePercent == chargePercent
            && publishedStoredBatteryCount == storedBatteryCount)
        {
            return;
        }

        publishedHasFlashlight = hasFlashlightNow;
        publishedFlashlightEnabled = flashlightEnabledNow;
        publishedChargePercent = chargePercent;
        publishedStoredBatteryCount = storedBatteryCount;
        Changed?.Invoke();
    }
}

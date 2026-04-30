using System;

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(WasdPlayerController))]
[RequireComponent(typeof(FlashlightStateOwner))]
public sealed class PlayerFlashlightEquipment : MonoBehaviour
{
    private const string FlashlightToggleOnResourcePath = "Audio/Sfx/mouseclick_flashlight";

    [SerializeField] private bool startWithFlashlight;
    [SerializeField] private bool startEnabled;

    private FlashlightStateOwner flashlightStateOwner;
    private AudioSource toggleAudioSource;
    private AudioClip flashlightToggleOnClip;
    private bool publishedHasFlashlight;
    private bool publishedFlashlightEnabled;

    public event Action Changed;

    internal bool StartWithFlashlightConfig => startWithFlashlight;
    internal bool StartEnabledConfig => startEnabled;

    public bool HasFlashlight => flashlightStateOwner != null && flashlightStateOwner.HasFlashlight;
    public bool IsFlashlightEnabled => flashlightStateOwner != null && flashlightStateOwner.IsFlashlightEnabled;

    private void Awake()
    {
        ResolveDependencies();
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

    public void ToggleFlashlight()
    {
        ResolveDependencies();

        if (flashlightStateOwner == null)
        {
            return;
        }

        bool enableAfterToggle = !IsFlashlightEnabled;

        if (!flashlightStateOwner.SetFlashlightEnabledState(enableAfterToggle))
        {
            return;
        }

        if (enableAfterToggle)
        {
            TryPlayFlashlightToggleOnSound();
        }
    }

    public bool GrantFlashlight()
    {
        ResolveDependencies();
        return flashlightStateOwner != null && flashlightStateOwner.GrantFlashlight();
    }

    public bool EnsureFlashlightEquipped(bool enabled)
    {
        ResolveDependencies();
        return flashlightStateOwner != null && flashlightStateOwner.EnsureFlashlightEquipped(enabled);
    }

    public bool SetFlashlightEnabledState(bool enabled)
    {
        ResolveDependencies();
        return flashlightStateOwner != null && flashlightStateOwner.SetFlashlightEnabledState(enabled);
    }

    public bool TryPlayFlashlightToggleOnSound()
    {
        EnsureToggleAudio();

        if (toggleAudioSource == null || flashlightToggleOnClip == null)
        {
            return false;
        }

        toggleAudioSource.PlayOneShot(flashlightToggleOnClip, 0.65f);
        return true;
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

    private void PublishChanged(bool force = false)
    {
        bool hasFlashlightNow = HasFlashlight;
        bool flashlightEnabledNow = IsFlashlightEnabled;

        if (!force
            && publishedHasFlashlight == hasFlashlightNow
            && publishedFlashlightEnabled == flashlightEnabledNow)
        {
            return;
        }

        publishedHasFlashlight = hasFlashlightNow;
        publishedFlashlightEnabled = flashlightEnabledNow;
        Changed?.Invoke();
    }

    private void EnsureToggleAudio()
    {
        flashlightToggleOnClip ??= Resources.Load<AudioClip>(FlashlightToggleOnResourcePath);

        if (toggleAudioSource != null)
        {
            return;
        }

        GameObject audioRoot = new("FlashlightToggleAudio");
        audioRoot.transform.SetParent(transform, false);
        toggleAudioSource = audioRoot.AddComponent<AudioSource>();
        toggleAudioSource.playOnAwake = false;
        toggleAudioSource.loop = false;
        toggleAudioSource.spatialBlend = 0f;
        toggleAudioSource.dopplerLevel = 0f;
        toggleAudioSource.rolloffMode = AudioRolloffMode.Linear;
        toggleAudioSource.reverbZoneMix = 0f;
    }
}

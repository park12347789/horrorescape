using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerStamina : MonoBehaviour, IStaminaSource
{
    private const float MinimumSprintStamina = 0.01f;

    [SerializeField] private StaminaConfig config;
    [SerializeField, Min(1f)] private float fallbackMaxStamina = 100f;
    [SerializeField, Min(0f)] private float fallbackSprintDrainPerSecond = 18f;
    [SerializeField, Min(0f)] private float fallbackRecoverPerSecond = 14f;
    [SerializeField, Min(0f)] private float fallbackRecoverDelay = 1f;
    [SerializeField, Min(0f)] private float fallbackExhaustedLockDuration = 1.6f;
    [SerializeField, Min(0f)] private float fallbackExhaustedRecoverDelay = 0.35f;
    [SerializeField, Min(0f)] private float fallbackMinimumSprintStartStamina = 4f;
    [SerializeField] private bool startFull = true;

    private float currentStamina;
    private float recoverDelayRemaining;
    private float exhaustedRemaining;
    private bool isDraining;
    private bool isRecovering;
    private bool initialized;

    public event Action Changed;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => ResolveMaxStamina();
    public float NormalizedStamina => MaxStamina > 0f ? Mathf.Clamp01(currentStamina / MaxStamina) : 0f;
    public float StaminaNormalized => NormalizedStamina;
    public bool CanSprint => !IsExhausted && currentStamina >= ResolveMinimumSprintStartStamina();
    public bool IsDraining => isDraining;
    public bool IsRecovering => isRecovering;
    public bool IsExhausted => exhaustedRemaining > 0f;
    public bool IsSprinting => isDraining;
    public bool ShouldShowStaminaHud => isDraining || isRecovering || IsExhausted || NormalizedStamina < 0.999f;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        InitializeIfNeeded();
        PublishChanged();
    }

    public void Tick(bool wantsSprint, bool sprinting, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        float previousStamina = currentStamina;
        bool previousDraining = isDraining;
        bool previousRecovering = isRecovering;
        bool previousExhausted = IsExhausted;

        float maxStamina = MaxStamina;
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        isDraining = sprinting && wantsSprint && currentStamina > 0f;

        if (isDraining)
        {
            currentStamina = Mathf.Max(0f, currentStamina - (ResolveSprintDrainPerSecond() * deltaTime));
            recoverDelayRemaining = ResolveRecoverDelay();

            if (currentStamina <= MinimumSprintStamina)
            {
                currentStamina = 0f;
                isDraining = false;
                exhaustedRemaining = ResolveExhaustedLockDuration();
                recoverDelayRemaining = ResolveRecoverDelay() + ResolveExhaustedRecoverDelay();
            }
        }
        else
        {
            AdvanceExhaustion(deltaTime);
            AdvanceRecoveryDelay(deltaTime);
            RecoverIfReady(deltaTime, maxStamina);
        }

        isRecovering = !isDraining
            && !IsExhausted
            && recoverDelayRemaining <= 0f
            && currentStamina < maxStamina;

        if (!Mathf.Approximately(previousStamina, currentStamina)
            || previousDraining != isDraining
            || previousRecovering != isRecovering
            || previousExhausted != IsExhausted)
        {
            PublishChanged();
        }
    }

    public bool TickSprintIntent(bool sprintHeld, bool hasMovementInput, float deltaTime)
    {
        bool wantsSprint = sprintHeld && hasMovementInput;
        bool canContinueSprint = isDraining && currentStamina > MinimumSprintStamina;
        bool canStartSprint = !isDraining && CanSprint;
        bool sprinting = wantsSprint && !IsExhausted && (canContinueSprint || canStartSprint);

        Tick(wantsSprint, sprinting, deltaTime);
        return isDraining;
    }

    public void CancelSprint()
    {
        if (!isDraining)
        {
            return;
        }

        isDraining = false;
        PublishChanged();
    }

    public void Refill()
    {
        currentStamina = MaxStamina;
        recoverDelayRemaining = 0f;
        exhaustedRemaining = 0f;
        isDraining = false;
        isRecovering = false;
        PublishChanged();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            currentStamina = Mathf.Clamp(currentStamina, 0f, MaxStamina);
            return;
        }

        float maxStamina = MaxStamina;

        if (startFull && currentStamina <= 0f)
        {
            currentStamina = maxStamina;
        }
        else
        {
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }

        initialized = true;
    }

    private void AdvanceExhaustion(float deltaTime)
    {
        if (exhaustedRemaining <= 0f)
        {
            return;
        }

        exhaustedRemaining = Mathf.Max(0f, exhaustedRemaining - deltaTime);
    }

    private void AdvanceRecoveryDelay(float deltaTime)
    {
        if (IsExhausted || recoverDelayRemaining <= 0f)
        {
            return;
        }

        recoverDelayRemaining = Mathf.Max(0f, recoverDelayRemaining - deltaTime);
    }

    private void RecoverIfReady(float deltaTime, float maxStamina)
    {
        if (IsExhausted || recoverDelayRemaining > 0f || currentStamina >= maxStamina)
        {
            return;
        }

        currentStamina = Mathf.Min(maxStamina, currentStamina + (ResolveRecoverPerSecond() * deltaTime));
    }

    private float ResolveMaxStamina()
    {
        return config != null ? config.MaxStamina : Mathf.Max(1f, fallbackMaxStamina);
    }

    private float ResolveSprintDrainPerSecond()
    {
        return config != null ? config.SprintDrainPerSecond : Mathf.Max(0f, fallbackSprintDrainPerSecond);
    }

    private float ResolveRecoverPerSecond()
    {
        return config != null ? config.RecoverPerSecond : Mathf.Max(0f, fallbackRecoverPerSecond);
    }

    private float ResolveRecoverDelay()
    {
        return config != null ? config.RecoverDelay : Mathf.Max(0f, fallbackRecoverDelay);
    }

    private float ResolveExhaustedLockDuration()
    {
        return config != null ? config.ExhaustedLockDuration : Mathf.Max(0f, fallbackExhaustedLockDuration);
    }

    private float ResolveExhaustedRecoverDelay()
    {
        return config != null ? config.ExhaustedRecoverDelay : Mathf.Max(0f, fallbackExhaustedRecoverDelay);
    }

    private float ResolveMinimumSprintStartStamina()
    {
        float configuredMinimum = config != null
            ? config.MinimumSprintStartStamina
            : Mathf.Max(0f, fallbackMinimumSprintStartStamina);
        return Mathf.Max(MinimumSprintStamina, configuredMinimum);
    }

    private void PublishChanged()
    {
        Changed?.Invoke();
    }

    private void OnValidate()
    {
        fallbackMaxStamina = Mathf.Max(1f, fallbackMaxStamina);
        fallbackSprintDrainPerSecond = Mathf.Max(0f, fallbackSprintDrainPerSecond);
        fallbackRecoverPerSecond = Mathf.Max(0f, fallbackRecoverPerSecond);
        fallbackRecoverDelay = Mathf.Max(0f, fallbackRecoverDelay);
        fallbackExhaustedLockDuration = Mathf.Max(0f, fallbackExhaustedLockDuration);
        fallbackExhaustedRecoverDelay = Mathf.Max(0f, fallbackExhaustedRecoverDelay);
        fallbackMinimumSprintStartStamina = Mathf.Max(0f, fallbackMinimumSprintStartStamina);
        currentStamina = Mathf.Clamp(currentStamina, 0f, MaxStamina);
    }
}

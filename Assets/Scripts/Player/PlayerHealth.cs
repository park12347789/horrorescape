using System;

using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCaughtState))]
public sealed class PlayerHealth : MonoBehaviour, IInvincibilityDebugApplier
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField, Min(0f)] private float damageRecoveryDuration = 1.1f;
    [SerializeField] private bool invincible;
    [SerializeField] private int currentHealth;
    [SerializeField, FormerlySerializedAs("runController"), FormerlySerializedAs("rebuildRunControllerSource")]
    private MonoBehaviour runControllerSource;

    private IRunStateController runStateController;

    private PlayerCaughtState caughtState;
    private PlayerDamageFeedback damageFeedback;
    private float recoverUntilTime;

    public event Action Changed;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsInvincible => invincible;
    public bool IsFullHealth => currentHealth >= maxHealth;
    public bool CanTakeDamage => Time.time >= recoverUntilTime;
    public float HealthNormalized => maxHealth <= 0 ? 0f : Mathf.Clamp01(currentHealth / (float)maxHealth);
    public float RecoveryNormalized => damageRecoveryDuration <= 0.001f
        ? 0f
        : Mathf.Clamp01((recoverUntilTime - Time.time) / damageRecoveryDuration);

    private void Awake()
    {
        caughtState = GetComponent<PlayerCaughtState>();
        damageFeedback = GetComponent<PlayerDamageFeedback>();
        maxHealth = Mathf.Max(1, maxHealth);
        CacheRunController();

        if (currentHealth <= 0 || currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        NotifyChanged();
    }

    public bool TryApplyDamage(int amount, string sourceName, Vector2? sourceWorldPosition = null)
    {
        if (amount <= 0 || caughtState == null || caughtState.IsCaught || !CanTakeDamage || HasEscapedRun())
        {
            return false;
        }

        if (invincible)
        {
            recoverUntilTime = Time.time + damageRecoveryDuration;
            NotifyChanged();
            return true;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        recoverUntilTime = Time.time + damageRecoveryDuration;
        damageFeedback ??= GetComponent<PlayerDamageFeedback>();

        if (damageFeedback == null)
        {
            damageFeedback = gameObject.AddComponent<PlayerDamageFeedback>();
        }

        damageFeedback?.PlayHit(sourceWorldPosition ?? (Vector2)transform.position);

        if (currentHealth <= 0)
        {
            caughtState.TryCapture(sourceName);
        }

        NotifyChanged();
        return true;
    }

    public bool TryHeal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth)
        {
            return false;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        NotifyChanged();
        return true;
    }

    public string GetHudSummary()
    {
        return $"HP {currentHealth}/{maxHealth}";
    }

    public void SetInvincible(bool enabled)
    {
        invincible = enabled;

        if (invincible)
        {
            currentHealth = Mathf.Max(currentHealth, maxHealth);
        }

        NotifyChanged();
    }

    public void ApplyInvincibility(bool enabled)
    {
        SetInvincible(enabled);
    }

    public void SetCurrentHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 1, maxHealth);
        recoverUntilTime = 0f;
        NotifyChanged();
    }

    public void BindRunController(IRunStateController controller)
    {
        runStateController = controller;
        runControllerSource = controller as MonoBehaviour;
    }

    private bool HasEscapedRun()
    {
        return runStateController != null && runStateController.IsEscaped;
    }

    private void OnValidate()
    {
        CacheRunController();
    }

    private void CacheRunController()
    {
        runStateController = runControllerSource as IRunStateController;
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}

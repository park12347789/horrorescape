using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDisruptionController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer facingMarkerRenderer;
    [SerializeField] private EnemyVisionVisualizer visionVisualizer;
    [SerializeField] private Transform stunAnchor;
    [SerializeField, Min(0f)] private float stunHeightOffset = 0.92f;
    [SerializeField] private int stunSortingOrder = 24;
    [SerializeField] private Color stunBodyColor = new(0.58f, 0.74f, 1f, 1f);
    [SerializeField] private Color stunMarkerColor = new(0.9f, 0.96f, 1f, 1f);

    private EnemyStunEffect stunEffect;
    private float stunUntilTime;
    private float attackRecoverUntilTime;

    public bool IsStunned => Time.time < stunUntilTime;
    public float StunUntilTime => stunUntilTime;
    public bool IsAttackRecovering => Time.time < attackRecoverUntilTime;

    public void Configure(
        SpriteRenderer configuredBodyRenderer,
        SpriteRenderer configuredFacingMarkerRenderer,
        EnemyVisionVisualizer configuredVisionVisualizer,
        Transform configuredStunAnchor = null,
        int? configuredStunSortingOrder = null,
        float configuredStunHeightOffset = 0.92f,
        Color? configuredStunBodyColor = null,
        Color? configuredStunMarkerColor = null)
    {
        bodyRenderer = configuredBodyRenderer;
        facingMarkerRenderer = configuredFacingMarkerRenderer;
        visionVisualizer = configuredVisionVisualizer;
        stunAnchor = configuredStunAnchor != null
            ? configuredStunAnchor
            : bodyRenderer != null
                ? bodyRenderer.transform
                : transform;
        stunSortingOrder = configuredStunSortingOrder ?? (bodyRenderer != null ? bodyRenderer.sortingOrder + 6 : 24);
        stunHeightOffset = Mathf.Max(0f, configuredStunHeightOffset);

        if (configuredStunBodyColor.HasValue)
        {
            stunBodyColor = configuredStunBodyColor.Value;
        }

        if (configuredStunMarkerColor.HasValue)
        {
            stunMarkerColor = configuredStunMarkerColor.Value;
        }

        if (stunEffect != null)
        {
            stunEffect.Configure(stunAnchor, stunSortingOrder, stunHeightOffset);
        }
    }

    public void BeginNormalFrame(bool reactivateVisionVisualizer = true)
    {
        if (reactivateVisionVisualizer && visionVisualizer != null && !visionVisualizer.gameObject.activeSelf)
        {
            visionVisualizer.gameObject.SetActive(true);
        }

        HideStunEffect();
    }

    public void HideStunEffect()
    {
        if (stunEffect != null)
        {
            stunEffect.SetVisible(false);
        }
    }

    public void BeginAttackRecovery(float duration, Action onRecoveryStarted = null)
    {
        attackRecoverUntilTime = Mathf.Max(attackRecoverUntilTime, Time.time + Mathf.Max(0f, duration));
        onRecoveryStarted?.Invoke();
    }

    public bool TryApplyStun(float duration, Action onStunApplied = null)
    {
        if (duration <= 0f)
        {
            return false;
        }

        stunUntilTime = Mathf.Max(stunUntilTime, Time.time + duration);
        onStunApplied?.Invoke();
        EnsureStunEffect();
        stunEffect?.SetVisible(true);
        return true;
    }

    public bool UpdateWhileStunned(Action onWhileStunned = null, Action onAfterPresentation = null)
    {
        if (!IsStunned)
        {
            return false;
        }

        onWhileStunned?.Invoke();

        if (visionVisualizer != null && visionVisualizer.gameObject.activeSelf)
        {
            visionVisualizer.gameObject.SetActive(false);
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.color = stunBodyColor;
        }

        if (facingMarkerRenderer != null)
        {
            facingMarkerRenderer.color = stunMarkerColor;
        }

        EnsureStunEffect();
        stunEffect?.SetVisible(true);
        onAfterPresentation?.Invoke();
        return true;
    }

    private void OnDisable()
    {
        HideStunEffect();
    }

    private void EnsureStunEffect()
    {
        if (stunEffect == null)
        {
            stunEffect = GetComponent<EnemyStunEffect>();
        }

        if (stunEffect == null)
        {
            stunEffect = gameObject.AddComponent<EnemyStunEffect>();
        }

        Transform anchor = stunAnchor != null
            ? stunAnchor
            : bodyRenderer != null
                ? bodyRenderer.transform
                : transform;
        stunEffect.Configure(anchor, stunSortingOrder, stunHeightOffset);
    }
}

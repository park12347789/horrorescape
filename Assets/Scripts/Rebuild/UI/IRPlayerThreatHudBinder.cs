using UnityEngine;

public readonly struct RThreatHudPresentationState
{
    public RThreatHudPresentationState(float displayedIntensity, ThreatPanelPresentation presentation)
    {
        DisplayedIntensity = displayedIntensity;
        Presentation = presentation;
    }

    public float DisplayedIntensity { get; }
    public ThreatPanelPresentation Presentation { get; }
}

public static class RThreatHudPresenter
{
    public const float VisibleThreshold = 0.16f;
    private const float HideThresholdMultiplier = 0.72f;
    private const float PursuitScoreBonus = 0.18f;
    private const float PassiveScoreBonus = 0.06f;

    public static RThreatHudPresentationState BuildPresentation(
        Vector3 playerPosition,
        float displayedThreatIntensity,
        float deltaTime,
        float visibleThreshold,
        float rampUpSpeed,
        float rampDownSpeed,
        bool requireActivePursuit)
    {
        EvaluateThreat(playerPosition, requireActivePursuit, out float targetIntensity, out bool pursuitConfirmed);
        float rampSpeed = targetIntensity > displayedThreatIntensity ? rampUpSpeed : rampDownSpeed;
        float nextDisplayedIntensity = Mathf.MoveTowards(displayedThreatIntensity, targetIntensity, deltaTime * rampSpeed);
        float hideThreshold = visibleThreshold * HideThresholdMultiplier;
        bool showThreat = pursuitConfirmed && (nextDisplayedIntensity >= visibleThreshold || displayedThreatIntensity >= hideThreshold);

        return new RThreatHudPresentationState(
            nextDisplayedIntensity,
            new ThreatPanelPresentation(
                showThreat ? nextDisplayedIntensity : 0f,
                pursuitConfirmed,
                string.Empty,
                string.Empty));
    }

    private static void EvaluateThreat(Vector3 playerPosition, bool requireActivePursuit, out float bestIntensity, out bool pursuitConfirmed)
    {
        bestIntensity = 0f;
        pursuitConfirmed = false;
        float bestScore = float.NegativeInfinity;
        var sources = PlayerThreatFeedbackRegistry.Sources;

        for (int index = 0; index < sources.Count; index++)
        {
            IPlayerThreatFeedbackSource source = sources[index];
            Object sourceObject = source as Object;

            if (source == null || sourceObject == null)
            {
                continue;
            }

            float intensity = Mathf.Clamp01(source.ThreatIntensityNormalized);

            if (!source.IsConfirmedThreat || intensity <= 0.001f)
            {
                continue;
            }

            if (requireActivePursuit && !source.IsActivelyPursuingPlayer)
            {
                continue;
            }

            float distance = Vector2.Distance(playerPosition, source.ThreatWorldPosition);
            float distancePenalty = Mathf.Clamp01(distance / 18f) * 0.14f;
            float pursuitBonus = source.IsActivelyPursuingPlayer ? PursuitScoreBonus : PassiveScoreBonus;
            float score = intensity + pursuitBonus + 0.75f - distancePenalty;

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            pursuitConfirmed = true;
            bestIntensity = intensity;
        }
    }
}

[DisallowMultipleComponent]
public sealed class IRPlayerThreatHudBinder : MonoBehaviour, IRebuildHudBinder
{
    [SerializeField] private IRHudCanvas hudCanvas;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField, Min(0f)] private float activeRefreshInterval = 0.033f;
    [SerializeField, Min(0f)] private float idleRefreshInterval = 0.08f;
    [SerializeField, Min(0f)] private float settledRefreshInterval = 0.16f;
    [SerializeField, Min(0.05f)] private float spottedCornerPulseDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float spottedCornerPulseStrength = 0.9f;

    private WasdPlayerController playerController;
    private IRThreatPanelView panelView;
    private readonly System.Collections.Generic.List<IEnemyPlayerSpotSource> subscribedSpotSources = new(8);
    private float displayedThreatIntensity;
    private bool missingThreatPanelLogged;
    private bool hasRenderedPresentation;
    private bool lastRenderedPursuitConfirmed;
    private float lastRenderedThreatIntensity;
    private float lastRefreshTime = -1f;
    private float nextRefreshTime;
    private float spottedPulseStartTime = float.NegativeInfinity;
    private float spottedPulseEndTime = float.NegativeInfinity;
    private int lastSpotSubscriptionVersion = int.MinValue;

    private void Awake()
    {
        CacheDependencies();
        ResolvePanelView();
        RefreshPlayerSpotSubscriptions();
        RefreshView();
    }

    private void OnEnable()
    {
        RefreshPlayerSpotSubscriptions();
    }

    private void OnDisable()
    {
        UnsubscribeFromAllSpotSources();
    }

    private void OnValidate()
    {
        CacheDependencies();
        ResolvePanelView();
    }

    private void Update()
    {
        RefreshPlayerSpotSubscriptions();

        if (panelView == null || playerController == null)
        {
            return;
        }

        float now = Time.unscaledTime;

        if (now < nextRefreshTime)
        {
            return;
        }

        RefreshView();
    }

    public void BindHudCanvas(IRHudCanvas canvas)
    {
        hudCanvas = canvas;
        ResolvePanelView();
        RefreshPlayerSpotSubscriptions();
        RefreshView();
    }

    public void BindPlayerRuntime(RPlayerRuntimeReferences runtime)
    {
        playerRuntime = runtime;
        CacheDependencies();
        RefreshPlayerSpotSubscriptions();
        RefreshView();
    }

    private void CacheDependencies()
    {
        RPlayerRuntimeReferences runtime = playerRuntime != null ? playerRuntime : GetComponent<RPlayerRuntimeReferences>();
        playerController = runtime != null ? runtime.PlayerController : GetComponent<WasdPlayerController>();
    }

    private void ResolvePanelView()
    {
        panelView = hudCanvas != null ? hudCanvas.ThreatPanel : null;

        if (hudCanvas != null
            && (panelView == null || !panelView.HasRenderableEdges)
            && !missingThreatPanelLogged)
        {
            Debug.LogWarning($"{nameof(IRPlayerThreatHudBinder)} requires an authored threat panel on the assigned IRHudCanvas.", this);
            missingThreatPanelLogged = true;
        }
    }

    private void RefreshView()
    {
        if (panelView == null || playerController == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        float deltaTime = lastRefreshTime >= 0f
            ? Mathf.Max(0f, now - lastRefreshTime)
            : Time.unscaledDeltaTime;

        if (CanSkipSettledRefresh())
        {
            lastRefreshTime = now;
            nextRefreshTime = now + settledRefreshInterval;
            return;
        }

        RThreatHudPresentationState state = RThreatHudPresenter.BuildPresentation(
            playerController.transform.position,
            displayedThreatIntensity,
            deltaTime,
            RThreatHudPresenter.VisibleThreshold,
            9.2f,
            5.8f,
            requireActivePursuit: false);

        if (state.Presentation.PursuitConfirmed && !lastRenderedPursuitConfirmed)
        {
            TriggerSpottedPulse(now);
        }

        displayedThreatIntensity = state.DisplayedIntensity;
        RenderPresentation(ComposePresentation(state.Presentation, now));
        lastRefreshTime = now;
        nextRefreshTime = now + ResolveRefreshInterval();
    }

    private float ResolveRefreshInterval()
    {
        if (HasActiveSpottedPulse(Time.unscaledTime))
        {
            return activeRefreshInterval;
        }

        if (CanUseSettledRefreshInterval())
        {
            return settledRefreshInterval;
        }

        return displayedThreatIntensity > 0.001f
            ? activeRefreshInterval
            : idleRefreshInterval;
    }

    private bool CanSkipSettledRefresh()
    {
        return hasRenderedPresentation
            && IsLastRenderedHidden()
            && displayedThreatIntensity <= 0.001f
            && !HasActiveSpottedPulse(Time.unscaledTime)
            && !HasRelevantThreatCandidate();
    }

    private bool CanUseSettledRefreshInterval()
    {
        return displayedThreatIntensity <= 0.001f
            && IsLastRenderedHidden()
            && !HasActiveSpottedPulse(Time.unscaledTime)
            && !HasRelevantThreatCandidate();
    }

    private bool IsLastRenderedHidden()
    {
        return !lastRenderedPursuitConfirmed && lastRenderedThreatIntensity <= 0.001f;
    }

    private bool HasRelevantThreatCandidate()
    {
        var sources = PlayerThreatFeedbackRegistry.Sources;

        for (int index = 0; index < sources.Count; index++)
        {
            IPlayerThreatFeedbackSource source = sources[index];
            Object sourceObject = source as Object;

            if (source == null || sourceObject == null)
            {
                continue;
            }

            if (!source.IsConfirmedThreat)
            {
                continue;
            }

            if (source.ThreatIntensityNormalized <= 0.001f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void RenderPresentation(ThreatPanelPresentation presentation)
    {
        panelView.Render(presentation);
        hasRenderedPresentation = true;
        lastRenderedThreatIntensity = presentation.Intensity;
        lastRenderedPursuitConfirmed = presentation.PursuitConfirmed;
    }

    private ThreatPanelPresentation ComposePresentation(ThreatPanelPresentation presentation, float now)
    {
        return new ThreatPanelPresentation(
            presentation.Intensity,
            presentation.PursuitConfirmed,
            presentation.Title,
            presentation.Detail,
            ResolveSpottedPulseIntensity(now));
    }

    private void TriggerSpottedPulse(float now)
    {
        float duration = Mathf.Max(0.05f, spottedCornerPulseDuration);
        spottedPulseStartTime = now;
        spottedPulseEndTime = now + duration;
    }

    private bool HasActiveSpottedPulse(float now)
    {
        return now < spottedPulseEndTime;
    }

    private float ResolveSpottedPulseIntensity(float now)
    {
        if (!HasActiveSpottedPulse(now))
        {
            return 0f;
        }

        float duration = Mathf.Max(0.05f, spottedCornerPulseDuration);
        float elapsedNormalized = Mathf.Clamp01((now - spottedPulseStartTime) / duration);
        float fade = 1f - elapsedNormalized;
        return Mathf.Clamp01(spottedCornerPulseStrength) * Mathf.SmoothStep(0f, 1f, fade);
    }

    private void RefreshPlayerSpotSubscriptions()
    {
        var threatSources = PlayerThreatFeedbackRegistry.Sources;
        int registryVersion = PlayerThreatFeedbackRegistry.Version;

        if (lastSpotSubscriptionVersion == registryVersion)
        {
            return;
        }

        lastSpotSubscriptionVersion = registryVersion;

        for (int index = subscribedSpotSources.Count - 1; index >= 0; index--)
        {
            IEnemyPlayerSpotSource subscribedSource = subscribedSpotSources[index];

            if (subscribedSource == null || !ContainsSpotSource(threatSources, subscribedSource))
            {
                if (subscribedSource != null)
                {
                    subscribedSource.PlayerSpotted -= HandlePlayerSpotted;
                }

                subscribedSpotSources.RemoveAt(index);
            }
        }

        for (int index = 0; index < threatSources.Count; index++)
        {
            if (threatSources[index] is not IEnemyPlayerSpotSource spotSource || ContainsSubscribedSpotSource(spotSource))
            {
                continue;
            }

            spotSource.PlayerSpotted += HandlePlayerSpotted;
            subscribedSpotSources.Add(spotSource);
        }
    }

    private void UnsubscribeFromAllSpotSources()
    {
        for (int index = subscribedSpotSources.Count - 1; index >= 0; index--)
        {
            IEnemyPlayerSpotSource subscribedSource = subscribedSpotSources[index];

            if (subscribedSource != null)
            {
                subscribedSource.PlayerSpotted -= HandlePlayerSpotted;
            }
        }

        subscribedSpotSources.Clear();
        lastSpotSubscriptionVersion = int.MinValue;
    }

    private bool ContainsSubscribedSpotSource(IEnemyPlayerSpotSource target)
    {
        for (int index = 0; index < subscribedSpotSources.Count; index++)
        {
            if (ReferenceEquals(subscribedSpotSources[index], target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSpotSource(
        System.Collections.Generic.IReadOnlyList<IPlayerThreatFeedbackSource> threatSources,
        IEnemyPlayerSpotSource target)
    {
        for (int index = 0; index < threatSources.Count; index++)
        {
            if (ReferenceEquals(threatSources[index], target))
            {
                return true;
            }
        }

        return false;
    }

    private void HandlePlayerSpotted()
    {
        float now = Time.unscaledTime;
        TriggerSpottedPulse(now);
        nextRefreshTime = Mathf.Min(nextRefreshTime, now);

        if (panelView == null)
        {
            return;
        }

        RenderPresentation(ComposePresentation(
            new ThreatPanelPresentation(
                displayedThreatIntensity,
                lastRenderedPursuitConfirmed,
                string.Empty,
                string.Empty),
            now));
    }
}

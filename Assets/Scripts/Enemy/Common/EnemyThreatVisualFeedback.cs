using UnityEngine;

public readonly struct EnemyThreatVisualFeedbackProfile
{
    public EnemyThreatVisualFeedbackProfile(
        bool hasSource,
        bool isConfirmedThreat,
        bool shouldForceVisible,
        float rawIntensity,
        float readableIntensity,
        float pulse,
        float silhouetteAlpha,
        bool showVisionConeInFog,
        SpriteRenderer markerRenderer,
        Color accentColor)
    {
        HasSource = hasSource;
        IsConfirmedThreat = isConfirmedThreat;
        ShouldForceVisible = shouldForceVisible;
        RawIntensity = rawIntensity;
        ReadableIntensity = readableIntensity;
        Pulse = pulse;
        SilhouetteAlpha = silhouetteAlpha;
        ShowVisionConeInFog = showVisionConeInFog;
        MarkerRenderer = markerRenderer;
        AccentColor = accentColor;
    }

    public bool HasSource { get; }
    public bool IsConfirmedThreat { get; }
    public bool ShouldForceVisible { get; }
    public float RawIntensity { get; }
    public float ReadableIntensity { get; }
    public float Pulse { get; }
    public float SilhouetteAlpha { get; }
    public bool ShowVisionConeInFog { get; }
    public SpriteRenderer MarkerRenderer { get; }
    public Color AccentColor { get; }
    public bool ShouldHighlight => IsConfirmedThreat || ShouldForceVisible;
}

public static class EnemyThreatVisualFeedback
{
    private static readonly Color InvestigateAccentColor = new(1f, 0.74f, 0.34f, 1f);
    private static readonly Color ChaseAccentColor = new(1f, 0.24f, 0.18f, 1f);

    public static EnemyThreatVisualFeedbackProfile Evaluate(
        Component context,
        ref IPlayerThreatFeedbackSource cachedSource,
        float pulseSpeed = 7.2f)
    {
        IPlayerThreatFeedbackSource source = ResolveSource(context, ref cachedSource);
        bool hasSource = source != null;
        bool isConfirmedThreat = hasSource && source.IsConfirmedThreat;
        bool shouldForceVisible = hasSource && source.ShouldForceThreatFeedbackVisible;
        float rawIntensity = hasSource ? Mathf.Clamp01(source.ThreatIntensityNormalized) : 0f;
        float readableIntensity = isConfirmedThreat
            ? Mathf.Max(0.8f, rawIntensity)
            : shouldForceVisible
                ? Mathf.Max(0.5f, rawIntensity)
                : rawIntensity;
        readableIntensity = Mathf.Clamp01(readableIntensity);
        float pulse = 0.5f + (0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, pulseSpeed)));
        float silhouetteAlpha = isConfirmedThreat
            ? Mathf.Lerp(0.5f, 0.7f, pulse) * readableIntensity
            : shouldForceVisible
                ? Mathf.Lerp(0.3f, 0.46f, pulse) * readableIntensity
                : 0f;
        Color accentColor = isConfirmedThreat ? ChaseAccentColor : InvestigateAccentColor;

        return new EnemyThreatVisualFeedbackProfile(
            hasSource,
            isConfirmedThreat,
            shouldForceVisible,
            rawIntensity,
            readableIntensity,
            pulse,
            Mathf.Clamp01(silhouetteAlpha),
            isConfirmedThreat,
            hasSource ? source.ThreatMarkerRenderer : null,
            accentColor);
    }

    private static IPlayerThreatFeedbackSource ResolveSource(Component context, ref IPlayerThreatFeedbackSource cachedSource)
    {
        if (cachedSource is Object cachedObject && cachedObject != null)
        {
            return cachedSource;
        }

        cachedSource = null;

        if (context == null)
        {
            return null;
        }

        MonoBehaviour[] localBehaviours = context.GetComponents<MonoBehaviour>();

        for (int index = 0; index < localBehaviours.Length; index++)
        {
            if (localBehaviours[index] is IPlayerThreatFeedbackSource localSource)
            {
                cachedSource = localSource;
                return cachedSource;
            }
        }

        MonoBehaviour[] parentBehaviours = context.GetComponentsInParent<MonoBehaviour>(true);

        for (int index = 0; index < parentBehaviours.Length; index++)
        {
            if (parentBehaviours[index] is IPlayerThreatFeedbackSource parentSource)
            {
                cachedSource = parentSource;
                return cachedSource;
            }
        }

        return null;
    }
}

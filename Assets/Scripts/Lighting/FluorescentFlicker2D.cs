/*
 * File Role:
 * Provides seeded fluorescent flicker for authored 2D room lights.
 *
 * Runtime Use:
 * Attach to the room-bar light root so duplicated instances do not flicker in
 * lockstep while the beam sprites and Light2D intensity stay authorable.
 */

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class FluorescentFlicker2D : MonoBehaviour
{
    [Serializable]
    private struct VisualTargetState
    {
        public SpriteRenderer Renderer;
        public Color BaseColor;
    }

    [Header("References")]
    [SerializeField] private Light2D lightSource;
    [SerializeField] private SpriteRenderer[] visualRenderers = Array.Empty<SpriteRenderer>();

    [Header("Seed")]
    [SerializeField] private bool useInstanceSeed = true;
    [SerializeField] private int seedOffset;

    [Header("Timing")]
    [SerializeField, Min(0.05f)] private Vector2 steadyDurationRange = new(2.6f, 7.2f);
    [SerializeField, Min(0.01f)] private Vector2 burstDurationRange = new(0.08f, 0.22f);
    [SerializeField, Min(0.01f)] private Vector2 burstPulseIntervalRange = new(0.025f, 0.09f);
    [SerializeField, Min(1)] private Vector2Int burstPulseCountRange = new(2, 5);

    [Header("Brightness")]
    [SerializeField, Range(0f, 1f)] private Vector2 lightMultiplierRange = new(0.2f, 1f);
    [SerializeField, Range(0f, 1f)] private Vector2 spriteMultiplierRange = new(0.16f, 1f);
    [SerializeField, Min(0f)] private float responseSpeed = 24f;
    [SerializeField, Min(12f)] private float applyRate = 28f;
    [SerializeField, Range(0.001f, 0.2f)] private float lightApplyThreshold = 0.012f;
    [SerializeField, Range(0.001f, 0.2f)] private float spriteApplyThreshold = 0.01f;

    private VisualTargetState[] cachedVisualTargets = Array.Empty<VisualTargetState>();
    private System.Random random;
    private bool runtimeInitialized;
    private int runtimeSeed;
    private float nextNoiseSampleTime;
    private float nextDisturbanceTime;
    private float disturbanceEndTime;
    private float currentDisturbanceDepth;
    private float currentLightMultiplier = 1f;
    private float targetLightMultiplier = 1f;
    private float currentSpriteMultiplier = 1f;
    private float targetSpriteMultiplier = 1f;
    private float lastAppliedLightMultiplier = float.NaN;
    private float lastAppliedSpriteMultiplier = float.NaN;
    private float nextApplyTime;
    private float slowNoiseOffset;
    private float fastNoiseOffset;
    private float disturbanceNoiseOffset;
    private Color baseLightColor = Color.white;
    private float baseLightIntensity = 1f;

    public int RuntimeSeed => runtimeSeed;
    public bool UseInstanceSeed => useInstanceSeed;
    public float CurrentLightMultiplier => currentLightMultiplier;
    public float CurrentSpriteMultiplier => currentSpriteMultiplier;

    private void Awake()
    {
        RefreshCachedTargets();
        CaptureBaselines();
        InitializeRuntimeState();
        ApplyCurrentState(1f, 1f);
    }

    private void OnEnable()
    {
        RefreshCachedTargets();
        CaptureBaselines();
        InitializeRuntimeState();
        ApplyCurrentState(1f, 1f);
    }

    private void OnDisable()
    {
        RestoreBaseline();
    }

    private void OnValidate()
    {
        RefreshCachedTargets();

        if (!Application.isPlaying)
        {
            CaptureBaselines();
            RestoreBaseline();
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        long performanceSample = MainEscapePerformanceTracker.BeginSample(MainEscapePerformanceSampleId.FluorescentFlicker);

        try
        {
            EnsureRuntimeInitialized();
            StepState(Time.time);

            float lerpSpeed = Mathf.Max(0f, responseSpeed);
            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            currentLightMultiplier = Mathf.Lerp(currentLightMultiplier, targetLightMultiplier, t);
            currentSpriteMultiplier = Mathf.Lerp(currentSpriteMultiplier, targetSpriteMultiplier, t);

            if (ShouldApplyCurrentState(Time.time))
            {
                ApplyCurrentState(currentLightMultiplier, currentSpriteMultiplier);
            }
        }
        finally
        {
            MainEscapePerformanceTracker.EndSample(MainEscapePerformanceSampleId.FluorescentFlicker, performanceSample);
        }
    }

    private void RefreshCachedTargets()
    {
        lightSource ??= GetComponentInChildren<Light2D>(true);

        if (visualRenderers == null)
        {
            visualRenderers = Array.Empty<SpriteRenderer>();
        }

        List<VisualTargetState> targets = new(visualRenderers.Length);

        for (int index = 0; index < visualRenderers.Length; index++)
        {
            SpriteRenderer renderer = visualRenderers[index];

            if (renderer == null)
            {
                continue;
            }

            targets.Add(new VisualTargetState
            {
                Renderer = renderer,
                BaseColor = renderer.color
            });
        }

        cachedVisualTargets = targets.ToArray();

        if (lightSource != null)
        {
            baseLightColor = lightSource.color;
            baseLightIntensity = Mathf.Max(0.001f, lightSource.intensity);
        }
    }

    private void CaptureBaselines()
    {
        if (lightSource != null)
        {
            baseLightColor = lightSource.color;
            baseLightIntensity = Mathf.Max(0.001f, lightSource.intensity);
        }

        for (int index = 0; index < cachedVisualTargets.Length; index++)
        {
            VisualTargetState target = cachedVisualTargets[index];

            if (target.Renderer != null)
            {
                target.BaseColor = target.Renderer.color;
                cachedVisualTargets[index] = target;
            }
        }
    }

    private void RestoreBaseline()
    {
        if (lightSource != null)
        {
            lightSource.color = baseLightColor;
            lightSource.intensity = baseLightIntensity;
        }

        for (int index = 0; index < cachedVisualTargets.Length; index++)
        {
            VisualTargetState target = cachedVisualTargets[index];

            if (target.Renderer != null)
            {
                target.Renderer.color = target.BaseColor;
            }
        }
    }

    private void InitializeRuntimeState()
    {
        runtimeSeed = useInstanceSeed
            ? CombineSeed(seedOffset, gameObject.GetInstanceID())
            : seedOffset;
        random = new System.Random(runtimeSeed);
        runtimeInitialized = true;
        slowNoiseOffset = NextFloat(-1000f, 1000f);
        fastNoiseOffset = NextFloat(-1000f, 1000f);
        disturbanceNoiseOffset = NextFloat(-1000f, 1000f);
        nextNoiseSampleTime = Time.time;
        nextDisturbanceTime = Time.time + NextFloat(steadyDurationRange);
        disturbanceEndTime = float.NegativeInfinity;
        currentDisturbanceDepth = 0f;
        currentLightMultiplier = 1f;
        targetLightMultiplier = 1f;
        currentSpriteMultiplier = 1f;
        targetSpriteMultiplier = 1f;
        nextApplyTime = 0f;
        lastAppliedLightMultiplier = float.NaN;
        lastAppliedSpriteMultiplier = float.NaN;
    }

    private void EnsureRuntimeInitialized()
    {
        if (!runtimeInitialized)
        {
            InitializeRuntimeState();
        }
    }

    private void StepState(float currentTime)
    {
        if (currentTime >= nextDisturbanceTime)
        {
            int disturbanceStrength = Mathf.Max(1, NextInt(burstPulseCountRange));
            int minimumPulseCount = Mathf.Max(1, Mathf.Min(burstPulseCountRange.x, burstPulseCountRange.y));
            int maximumPulseCount = Mathf.Max(minimumPulseCount, Mathf.Max(burstPulseCountRange.x, burstPulseCountRange.y));
            float normalizedStrength = maximumPulseCount == minimumPulseCount
                ? 1f
                : Mathf.InverseLerp(minimumPulseCount, maximumPulseCount, disturbanceStrength);

            currentDisturbanceDepth = Mathf.Lerp(0.06f, 0.18f, normalizedStrength);
            disturbanceEndTime = currentTime + NextFloat(burstDurationRange);
            nextDisturbanceTime = disturbanceEndTime + NextFloat(steadyDurationRange);
        }

        if (currentTime >= nextNoiseSampleTime)
        {
            SampleContinuousTargets(currentTime);
            nextNoiseSampleTime = currentTime + Mathf.Max(0.008f, NextFloat(burstPulseIntervalRange));
        }
    }

    private void ApplyCurrentState(float lightMultiplier, float spriteMultiplier)
    {
        if (lightSource != null)
        {
            lightSource.color = baseLightColor;
            lightSource.intensity = Mathf.Max(0f, baseLightIntensity * lightMultiplier);
        }

        for (int index = 0; index < cachedVisualTargets.Length; index++)
        {
            VisualTargetState target = cachedVisualTargets[index];

            if (target.Renderer != null)
            {
                target.Renderer.color = ScaleAlpha(target.BaseColor, spriteMultiplier);
            }
        }

        lastAppliedLightMultiplier = lightMultiplier;
        lastAppliedSpriteMultiplier = spriteMultiplier;
        nextApplyTime = Time.time + (1f / Mathf.Max(1f, applyRate));
    }

    private void SampleContinuousTargets(float currentTime)
    {
        float steadyDurationAverage = AverageRange(steadyDurationRange, 0.35f);
        float pulseIntervalAverage = AverageRange(burstPulseIntervalRange, 0.04f);
        float burstDurationAverage = AverageRange(burstDurationRange, 0.16f);
        float slowNoise = Mathf.PerlinNoise(slowNoiseOffset, currentTime / steadyDurationAverage);
        float fastNoise = Mathf.PerlinNoise(fastNoiseOffset, currentTime / pulseIntervalAverage);
        float fastCentered = (fastNoise - 0.5f) * 2f;
        float lightBase = Mathf.Lerp(
            Mathf.Lerp(lightMultiplierRange.x, lightMultiplierRange.y, 0.7f),
            lightMultiplierRange.y,
            slowNoise);
        float spriteBase = Mathf.Lerp(
            Mathf.Lerp(spriteMultiplierRange.x, spriteMultiplierRange.y, 0.72f),
            spriteMultiplierRange.y,
            slowNoise);
        float lightBuzz = fastCentered * Mathf.Lerp(0.04f, 0.15f, 1f - slowNoise);
        float spriteBuzz = fastCentered * Mathf.Lerp(0.05f, 0.18f, 1f - slowNoise);

        if (currentTime <= disturbanceEndTime)
        {
            float disturbanceNoise = Mathf.PerlinNoise(
                disturbanceNoiseOffset,
                currentTime / Mathf.Max(0.025f, burstDurationAverage * 0.32f));
            float downwardBias = Mathf.Lerp(0.4f, 1f, disturbanceNoise);
            lightBuzz -= currentDisturbanceDepth * downwardBias;
            spriteBuzz -= (currentDisturbanceDepth * 0.85f) * downwardBias;
        }
        else
        {
            currentDisturbanceDepth = Mathf.MoveTowards(currentDisturbanceDepth, 0f, Time.deltaTime * 0.35f);
        }

        targetLightMultiplier = Mathf.Clamp(lightBase + lightBuzz, Mathf.Min(lightMultiplierRange.x, lightMultiplierRange.y), Mathf.Max(lightMultiplierRange.x, lightMultiplierRange.y));
        targetSpriteMultiplier = Mathf.Clamp(spriteBase + spriteBuzz, Mathf.Min(spriteMultiplierRange.x, spriteMultiplierRange.y), Mathf.Max(spriteMultiplierRange.x, spriteMultiplierRange.y));
    }

    private bool ShouldApplyCurrentState(float currentTime)
    {
        if (float.IsNaN(lastAppliedLightMultiplier) || float.IsNaN(lastAppliedSpriteMultiplier))
        {
            return true;
        }

        if (currentTime >= nextApplyTime)
        {
            return true;
        }

        return Mathf.Abs(currentLightMultiplier - lastAppliedLightMultiplier) >= lightApplyThreshold
            || Mathf.Abs(currentSpriteMultiplier - lastAppliedSpriteMultiplier) >= spriteApplyThreshold;
    }

    private static float AverageRange(Vector2 range, float fallback)
    {
        float average = (range.x + range.y) * 0.5f;
        return Mathf.Max(fallback, Mathf.Abs(average));
    }

    private float NextFloat(Vector2 range)
    {
        return NextFloat(range.x, range.y);
    }

    private float NextFloat(float min, float max)
    {
        if (random == null)
        {
            random = new System.Random(runtimeSeed);
        }

        float lower = Mathf.Min(min, max);
        float upper = Mathf.Max(min, max);

        if (Mathf.Abs(upper - lower) <= 0.0001f)
        {
            return lower;
        }

        return lower + ((float)random.NextDouble() * (upper - lower));
    }

    private int NextInt(Vector2Int range)
    {
        if (random == null)
        {
            random = new System.Random(runtimeSeed);
        }

        int lower = Mathf.Min(range.x, range.y);
        int upper = Mathf.Max(range.x, range.y);

        if (lower >= upper)
        {
            return lower;
        }

        return random.Next(lower, upper + 1);
    }

    private static Color ScaleAlpha(Color color, float multiplier)
    {
        float clamped = Mathf.Max(0f, multiplier);
        return new Color(color.r, color.g, color.b, color.a * clamped);
    }

    private static int CombineSeed(int seedA, int seedB)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + seedA;
            hash = (hash * 31) + seedB;
            return hash;
        }
    }
}

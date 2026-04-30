/*
 * File Role:
 * Stores recent noise events and draws pulse rings for them.
 *
 * Runtime Use:
 * Acts as the shared sound bus that regular enemies and the vent enemy both listen to,
 * while also surfacing the tension pulse effect players can read in-game.
 *
 * Study Notes:
 * This is a core study file for understanding how sound travels through the prototype.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

public enum NoiseSourceType
{
    Walk,
    Sprint,
    Interact,
    Collision,
    LoudFloor,
    Door,
    VentCrawl
}

public enum NoiseEmitterAffiliation
{
    Neutral,
    Player,
    Enemy
}

[Serializable]
public struct NoiseEventRecord
{
    public Vector2 position;
    public float radius;
    public NoiseSourceType sourceType;
    public int emitterInstanceId;
    public NoiseEmitterAffiliation emitterAffiliation;
    public float time;
    public int sequenceId;
}

internal readonly struct NoiseDebugPulseRenderKey : IEquatable<NoiseDebugPulseRenderKey>
{
    private readonly int emitterInstanceId;
    private readonly NoiseEmitterAffiliation emitterAffiliation;
    private readonly NoiseSourceType sourceType;

    public NoiseDebugPulseRenderKey(int emitterInstanceId, NoiseEmitterAffiliation emitterAffiliation, NoiseSourceType sourceType)
    {
        this.emitterInstanceId = emitterInstanceId;
        this.emitterAffiliation = emitterAffiliation;
        this.sourceType = sourceType;
    }

    public bool Equals(NoiseDebugPulseRenderKey other)
    {
        return emitterInstanceId == other.emitterInstanceId
            && emitterAffiliation == other.emitterAffiliation
            && sourceType == other.sourceType;
    }

    public override bool Equals(object obj)
    {
        return obj is NoiseDebugPulseRenderKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + emitterInstanceId;
            hash = (hash * 31) + (int)emitterAffiliation;
            hash = (hash * 31) + (int)sourceType;
            return hash;
        }
    }
}

internal readonly struct NoiseDebugPulseVisualSettings
{
    public static NoiseDebugPulseVisualSettings Default => new(1f, 1f, 1f, 0f);

    public NoiseDebugPulseVisualSettings(float visibleLifetimeScale, float widthScale, float alphaScale, float animationLifetime)
    {
        VisibleLifetimeScale = Mathf.Max(0.05f, visibleLifetimeScale);
        WidthScale = Mathf.Max(0.1f, widthScale);
        AlphaScale = Mathf.Clamp01(alphaScale);
        AnimationLifetime = Mathf.Max(0f, animationLifetime);
    }

    public float VisibleLifetimeScale { get; }
    public float WidthScale { get; }
    public float AlphaScale { get; }
    public float AnimationLifetime { get; }
}

[DisallowMultipleComponent]
public sealed class NoiseSystem : MonoBehaviour, INoiseDebugPulseApplier, INoiseEventBus
{
    private const int RuntimeMaxActiveDebugPulses = 4;
    private const float RuntimeMaxDebugPulseVisibleLifetime = 0.32f;
    private const float RuntimeMaxDebugPulseVisualRadius = 0.72f;
    private const float RuntimeMaxFrequentDebugPulseVisualRadius = 0.44f;
    private const float RuntimeMaxFrequentDebugPulseAnimationLifetime = 0.22f;
    private const int DefaultMovementDebugPulseRenderInterval = 2;
    private const float DefaultMovementDebugPulseVisibleLifetimeScale = 0.85f;
    private const float DefaultMovementDebugPulseWidthScale = 0.9f;
    private const float DefaultMovementDebugPulseAlphaScale = 0.8f;
    private const float DefaultMovementDebugPulseSpreadAnimationLifetime = 1.45f;
    private const float DefaultMovementDebugPulseWidthMultiplier = 0.68f;
    private const float DefaultMovementDebugPulseAlphaMultiplier = 0.72f;
    private const float DefaultWalkDebugPulseRenderCooldown = DefaultMovementDebugPulseSpreadAnimationLifetime;
    private const float DefaultSprintDebugPulseRenderCooldown = DefaultMovementDebugPulseSpreadAnimationLifetime;
    private const float DefaultVentCrawlDebugPulseRenderCooldown = DefaultMovementDebugPulseSpreadAnimationLifetime;
    private const float DefaultLoudFloorDebugPulseRenderCooldown = DefaultMovementDebugPulseSpreadAnimationLifetime;

    // Legacy static access keeps old compatibility paths alive; scene-local systems
    // should receive this component through INoiseEventBus instead.
    [SerializeField, Min(0.1f)] private float eventLifetime = 2.5f;
    [SerializeField, Min(0.05f)] private float debugPulseLifetime = 0.28f;
    [SerializeField, Min(1)] private int maxActiveDebugPulses = 16;
    [SerializeField, Min(0)] private int maxPooledDebugPulses = 16;
    [SerializeField] private bool debugPulsesEnabled = true;
    [SerializeField] private bool frequentNoiseDebugPulsesEnabled;
    [SerializeField, Min(1)] private int movementDebugPulseRenderInterval = DefaultMovementDebugPulseRenderInterval;
    [SerializeField, Range(0.5f, 1f)] private float movementDebugPulseVisibleLifetimeScale = DefaultMovementDebugPulseVisibleLifetimeScale;
    [SerializeField, Range(0.5f, 1f)] private float movementDebugPulseWidthScale = DefaultMovementDebugPulseWidthScale;
    [SerializeField, Range(0.25f, 1f)] private float movementDebugPulseAlphaScale = DefaultMovementDebugPulseAlphaScale;
    [SerializeField, Min(0.1f)] private float movementDebugPulseSpreadAnimationLifetime = DefaultMovementDebugPulseSpreadAnimationLifetime;
    [SerializeField, Range(0.25f, 1f)] private float movementDebugPulseWidthMultiplier = DefaultMovementDebugPulseWidthMultiplier;
    [SerializeField, Range(0.25f, 1f)] private float movementDebugPulseAlphaMultiplier = DefaultMovementDebugPulseAlphaMultiplier;
    [SerializeField, Min(0f)] private float walkDebugPulseRenderCooldown = DefaultWalkDebugPulseRenderCooldown;
    [SerializeField, Min(0f)] private float sprintDebugPulseRenderCooldown = DefaultSprintDebugPulseRenderCooldown;
    [SerializeField, Min(0f)] private float ventCrawlDebugPulseRenderCooldown = DefaultVentCrawlDebugPulseRenderCooldown;
    [SerializeField, Min(0f)] private float loudFloorDebugPulseRenderCooldown = DefaultLoudFloorDebugPulseRenderCooldown;
    private static NoiseSystem instance;
    private readonly List<NoiseEventRecord> recentEvents = new();
    private readonly List<NoiseDebugPulse> activeDebugPulses = new();
    private readonly Stack<NoiseDebugPulse> pooledDebugPulses = new();
    private readonly Dictionary<NoiseDebugPulseRenderKey, int> movementDebugPulseEmissionCounts = new();
    // Visual-only cooldowns keep repeated pulses readable without dropping AI hearing events.
    private readonly Dictionary<NoiseDebugPulseRenderKey, float> nextDebugPulseRenderTimes = new();
    private int nextEventSequenceId;
    private bool ownsLegacyInstance;

    [System.Obsolete("Legacy compatibility bridge. Prefer scene-local INoiseEventBus references.", false)]
    public static NoiseSystem Instance => instance;
    public IReadOnlyList<NoiseEventRecord> RecentEvents => recentEvents;
    public int RecentEventCount => recentEvents.Count;
    public bool DebugPulsesEnabled => debugPulsesEnabled;
    public bool FrequentNoiseDebugPulsesEnabled => frequentNoiseDebugPulsesEnabled;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            ownsLegacyInstance = true;
        }
    }

    private void OnDestroy()
    {
        if (ownsLegacyInstance && instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        CleanupExpiredEvents();
    }

    public NoiseEventRecord EmitNoise(
        Vector2 position,
        float radius,
        NoiseSourceType sourceType,
        int emitterInstanceId = 0,
        NoiseEmitterAffiliation emitterAffiliation = NoiseEmitterAffiliation.Neutral,
        bool allowDebugPulse = true)
    {
        // Every emitted noise is stored first, then visualized. That means AI and pulse
        // presentation always read the same event list.
        CleanupExpiredEvents();

        NoiseEventRecord record = new()
        {
            position = position,
            radius = Mathf.Max(0f, radius),
            sourceType = sourceType,
            emitterInstanceId = emitterInstanceId,
            emitterAffiliation = emitterAffiliation,
            time = Time.time,
            sequenceId = ++nextEventSequenceId
        };

        recentEvents.Add(record);

        if (debugPulsesEnabled && allowDebugPulse && ShouldRenderDebugPulse(record))
        {
            CreateDebugPulse(record);
        }

        return record;
    }

    public static bool TryEmitNoise(
        Vector2 position,
        float radius,
        NoiseSourceType sourceType,
        int emitterInstanceId = 0,
        NoiseEmitterAffiliation emitterAffiliation = NoiseEmitterAffiliation.Neutral,
        bool allowDebugPulse = true)
    {
        if (instance == null)
        {
            return false;
        }

        instance.EmitNoise(position, radius, sourceType, emitterInstanceId, emitterAffiliation, allowDebugPulse);
        return true;
    }

    bool INoiseEventBus.TryEmitNoise(
        Vector2 position,
        float radius,
        NoiseSourceType sourceType,
        int emitterInstanceId,
        NoiseEmitterAffiliation emitterAffiliation,
        bool allowDebugPulse)
    {
        EmitNoise(position, radius, sourceType, emitterInstanceId, emitterAffiliation, allowDebugPulse);
        return true;
    }

    public void SetDebugPulsesEnabled(bool enabled)
    {
        debugPulsesEnabled = enabled;
    }

    public void SetFrequentNoiseDebugPulsesEnabled(bool enabled)
    {
        frequentNoiseDebugPulsesEnabled = enabled;
    }

    public void ApplyNoiseDebugPulses(bool enabled)
    {
        SetDebugPulsesEnabled(enabled);
    }

    public bool TryGetLatestNoise(out NoiseEventRecord record)
    {
        // Many AI scripts only need "what happened last?" so this helper keeps them from
        // re-implementing event list access.
        CleanupExpiredEvents();

        if (recentEvents.Count == 0)
        {
            record = default;
            return false;
        }

        record = recentEvents[recentEvents.Count - 1];
        return true;
    }

    public NoiseEventRecord GetRecentEventAt(int index)
    {
        return recentEvents[index];
    }

    public static Color GetDebugColor(NoiseSourceType sourceType, NoiseEmitterAffiliation emitterAffiliation = NoiseEmitterAffiliation.Neutral)
    {
        if (emitterAffiliation == NoiseEmitterAffiliation.Enemy)
        {
            return sourceType switch
            {
                NoiseSourceType.Walk => new Color(1f, 0.46f, 0.12f, 1f),
                NoiseSourceType.Sprint => new Color(1f, 0.24f, 0.12f, 1f),
                NoiseSourceType.Interact => new Color(1f, 0.7f, 0.28f, 1f),
                NoiseSourceType.Collision => new Color(1f, 0.34f, 0.24f, 1f),
                NoiseSourceType.LoudFloor => new Color(1f, 0.18f, 0.26f, 1f),
                NoiseSourceType.Door => new Color(1f, 0.8f, 0.42f, 1f),
                NoiseSourceType.VentCrawl => new Color(1f, 0.6f, 0.28f, 1f),
                _ => new Color(1f, 0.52f, 0.22f, 1f)
            };
        }

        return sourceType switch
        {
            NoiseSourceType.Walk => new Color(0.35f, 0.86f, 1f, 1f),
            NoiseSourceType.Sprint => new Color(1f, 0.55f, 0.22f, 1f),
            NoiseSourceType.Interact => new Color(1f, 0.9f, 0.28f, 1f),
            NoiseSourceType.Collision => new Color(1f, 0.28f, 0.28f, 1f),
            NoiseSourceType.LoudFloor => new Color(1f, 0.18f, 0.4f, 1f),
            NoiseSourceType.Door => new Color(0.78f, 0.9f, 1f, 1f),
            NoiseSourceType.VentCrawl => new Color(0.76f, 0.54f, 1f, 1f),
            _ => Color.white
        };
    }

    private void CleanupExpiredEvents()
    {
        // Old sound events are removed every frame so listeners only react to a short
        // recent memory window instead of stale history.
        float thresholdTime = Time.time - eventLifetime;

        for (int index = recentEvents.Count - 1; index >= 0; index--)
        {
            if (recentEvents[index].time < thresholdTime)
            {
                recentEvents.RemoveAt(index);
            }
        }
    }

    private void CreateDebugPulse(NoiseEventRecord record)
    {
        // The pulse is a runtime tension effect layered on top of the shared noise event.
        TrimActiveDebugPulses();

        NoiseDebugPulse pulse = RentDebugPulse(record.sourceType);
        NoiseDebugPulseVisualSettings visualSettings = GetDebugPulseVisualSettings(record.sourceType);
        pulse.transform.SetParent(transform, false);
        pulse.transform.position = record.position;
        pulse.gameObject.name = $"{record.sourceType}NoisePulse";
        pulse.gameObject.SetActive(true);
        float animationLifetime = ResolveDebugPulseAnimationLifetime(visualSettings);
        pulse.Configure(
            radius: ResolveDebugPulseVisualRadius(record.radius, record.sourceType),
            color: GetDebugColor(record.sourceType, record.emitterAffiliation),
            lifetime: ResolveDebugPulseVisibleLifetime(visualSettings, animationLifetime),
            animationLifetime: animationLifetime,
            visualSettings: visualSettings,
            configuredOwner: this);
        activeDebugPulses.Add(pulse);
    }

    private bool ShouldRenderDebugPulse(NoiseEventRecord record)
    {
        if (IsFrequentDebugPulseSource(record.sourceType) && !frequentNoiseDebugPulsesEnabled)
        {
            return false;
        }

        float renderCooldown = GetDebugPulseRenderCooldown(record.sourceType);

        if (renderCooldown > 0f)
        {
            return ShouldPassDebugPulseRenderCooldown(record, renderCooldown);
        }

        if (!IsFrequentDebugPulseSource(record.sourceType))
        {
            return true;
        }

        int renderInterval = movementDebugPulseRenderInterval > 0
            ? movementDebugPulseRenderInterval
            : DefaultMovementDebugPulseRenderInterval;

        if (renderInterval <= 1)
        {
            return true;
        }

        NoiseDebugPulseRenderKey key = new(record.emitterInstanceId, record.emitterAffiliation, record.sourceType);
        movementDebugPulseEmissionCounts.TryGetValue(key, out int emissionCount);
        emissionCount++;
        movementDebugPulseEmissionCounts[key] = emissionCount;
        return emissionCount % renderInterval == 1;
    }

    private bool ShouldPassDebugPulseRenderCooldown(NoiseEventRecord record, float renderCooldown)
    {
        NoiseDebugPulseRenderKey key = new(record.emitterInstanceId, record.emitterAffiliation, record.sourceType);
        float currentTime = Time.time;

        if (nextDebugPulseRenderTimes.TryGetValue(key, out float nextAllowedTime) && currentTime < nextAllowedTime)
        {
            return false;
        }

        nextDebugPulseRenderTimes[key] = currentTime + renderCooldown;
        return true;
    }

    private float GetDebugPulseRenderCooldown(NoiseSourceType sourceType)
    {
        return sourceType switch
        {
            NoiseSourceType.Walk => Mathf.Max(0f, walkDebugPulseRenderCooldown),
            NoiseSourceType.Sprint => Mathf.Max(0f, sprintDebugPulseRenderCooldown),
            NoiseSourceType.VentCrawl => Mathf.Max(0f, ventCrawlDebugPulseRenderCooldown),
            NoiseSourceType.LoudFloor => Mathf.Max(0f, loudFloorDebugPulseRenderCooldown),
            _ => 0f
        };
    }

    private NoiseDebugPulseVisualSettings GetDebugPulseVisualSettings(NoiseSourceType sourceType)
    {
        if (!IsFrequentDebugPulseSource(sourceType))
        {
            return NoiseDebugPulseVisualSettings.Default;
        }

        float visibleLifetimeScale = movementDebugPulseVisibleLifetimeScale > 0f
            ? movementDebugPulseVisibleLifetimeScale
            : DefaultMovementDebugPulseVisibleLifetimeScale;
        float widthScale = movementDebugPulseWidthScale > 0f
            ? movementDebugPulseWidthScale
            : DefaultMovementDebugPulseWidthScale;
        float alphaScale = movementDebugPulseAlphaScale > 0f
            ? movementDebugPulseAlphaScale
            : DefaultMovementDebugPulseAlphaScale;
        float spreadAnimationLifetime = movementDebugPulseSpreadAnimationLifetime > 0f
            ? movementDebugPulseSpreadAnimationLifetime
            : DefaultMovementDebugPulseSpreadAnimationLifetime;
        spreadAnimationLifetime = Mathf.Min(spreadAnimationLifetime, RuntimeMaxFrequentDebugPulseAnimationLifetime);
        float widthMultiplier = movementDebugPulseWidthMultiplier > 0f
            ? movementDebugPulseWidthMultiplier
            : DefaultMovementDebugPulseWidthMultiplier;
        float alphaMultiplier = movementDebugPulseAlphaMultiplier > 0f
            ? movementDebugPulseAlphaMultiplier
            : DefaultMovementDebugPulseAlphaMultiplier;

        return new NoiseDebugPulseVisualSettings(
            visibleLifetimeScale,
            Mathf.Min(widthScale * widthMultiplier, 0.28f),
            Mathf.Min(alphaScale * alphaMultiplier, 0.36f),
            spreadAnimationLifetime);
    }

    private static float ResolveDebugPulseVisualRadius(float noiseRadius, NoiseSourceType sourceType)
    {
        float maxVisualRadius = IsFrequentDebugPulseSource(sourceType)
            ? RuntimeMaxFrequentDebugPulseVisualRadius
            : RuntimeMaxDebugPulseVisualRadius;
        return Mathf.Clamp(noiseRadius, 0.16f, maxVisualRadius);
    }

    private float ResolveDebugPulseAnimationLifetime(NoiseDebugPulseVisualSettings visualSettings)
    {
        float configuredAnimationLifetime = visualSettings.AnimationLifetime > 0f
            ? visualSettings.AnimationLifetime
            : debugPulseLifetime * visualSettings.VisibleLifetimeScale;
        return Mathf.Clamp(configuredAnimationLifetime, 0.05f, RuntimeMaxDebugPulseVisibleLifetime);
    }

    private float ResolveDebugPulseVisibleLifetime(
        NoiseDebugPulseVisualSettings visualSettings,
        float animationLifetime)
    {
        float configuredVisibleLifetime = debugPulseLifetime * visualSettings.VisibleLifetimeScale;
        float boundedVisibleLifetime = Mathf.Min(configuredVisibleLifetime, RuntimeMaxDebugPulseVisibleLifetime);
        return Mathf.Max(0.1f, boundedVisibleLifetime, animationLifetime);
    }

    private static bool IsFrequentDebugPulseSource(NoiseSourceType sourceType)
    {
        return sourceType is NoiseSourceType.Walk
            or NoiseSourceType.Sprint
            or NoiseSourceType.VentCrawl
            or NoiseSourceType.LoudFloor;
    }

    private NoiseDebugPulse RentDebugPulse(NoiseSourceType sourceType)
    {
        while (pooledDebugPulses.Count > 0)
        {
            NoiseDebugPulse pooledPulse = pooledDebugPulses.Pop();

            if (pooledPulse != null)
            {
                return pooledPulse;
            }
        }

        GameObject pulseObject = new($"{sourceType}NoisePulse");
        pulseObject.transform.SetParent(transform, false);
        return pulseObject.AddComponent<NoiseDebugPulse>();
    }

    private void TrimActiveDebugPulses()
    {
        int activeLimit = Mathf.Clamp(maxActiveDebugPulses, 1, RuntimeMaxActiveDebugPulses);

        for (int index = activeDebugPulses.Count - 1; index >= 0; index--)
        {
            NoiseDebugPulse pulse = activeDebugPulses[index];

            if (pulse == null || !pulse.gameObject.activeSelf)
            {
                activeDebugPulses.RemoveAt(index);
            }
        }

        while (activeDebugPulses.Count >= activeLimit)
        {
            NoiseDebugPulse oldestPulse = activeDebugPulses[0];
            activeDebugPulses.RemoveAt(0);

            if (oldestPulse != null && oldestPulse.gameObject.activeSelf)
            {
                oldestPulse.StopAndRecycle();
            }
        }
    }

    public void RecycleDebugPulse(NoiseDebugPulse pulse)
    {
        if (pulse == null)
        {
            return;
        }

        activeDebugPulses.Remove(pulse);
        pulse.gameObject.SetActive(false);
        pulse.transform.SetParent(transform, false);

        if (pooledDebugPulses.Count < Mathf.Max(0, maxPooledDebugPulses))
        {
            pooledDebugPulses.Push(pulse);
        }
        else
        {
            Destroy(pulse.gameObject);
        }
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class NoiseDebugPulse : MonoBehaviour
{
    private const int SegmentCount = 56;
    // Keep the noise pulse above the fog overlay, which renders at sorting order 90.
    private const int GlowSortingOrder = 95;
    private const int RingSortingOrder = 96;
    private const int CoreSortingOrder = 97;
    private const float BaseRingWidth = 0.05f;
    private const float BaseGlowWidth = 0.1f;
    private const float BaseCoreScale = 0.08f;
    private const float ExpandedCoreScale = 0.26f;
    private const float RingAlphaScale = 0.18f;
    private const float GlowAlphaScale = 0.045f;
    private const float CoreAlphaScale = 0.16f;
    private static readonly Vector3[] UnitCirclePoints = BuildUnitCirclePoints();
    private static Material sharedLineMaterial;
    private static Sprite sharedCoreSprite;
    private LineRenderer lineRenderer;
    private LineRenderer glowRenderer;
    private SpriteRenderer coreRenderer;
    private NoiseSystem owner;
    private float pulseRadius;
    private float pulseLifetime;
    private float pulseAnimationLifetime;
    private float pulseWidthScale = 1f;
    private float pulseAlphaScale = 1f;
    private Color pulseColor;
    private float spawnTime;
    private readonly Vector3[] scaledCirclePoints = new Vector3[SegmentCount];

    internal void Configure(
        float radius,
        Color color,
        float lifetime,
        float animationLifetime,
        NoiseDebugPulseVisualSettings visualSettings,
        NoiseSystem configuredOwner = null)
    {
        // Configure is separate from Awake so the spawner can pass meaningful values
        // immediately after creating the pulse object.
        owner = configuredOwner;
        pulseRadius = Mathf.Max(0.1f, radius);
        pulseColor = color;
        pulseLifetime = Mathf.Max(0.1f, lifetime);
        pulseAnimationLifetime = Mathf.Clamp(animationLifetime, 0.05f, pulseLifetime);
        pulseWidthScale = visualSettings.WidthScale;
        pulseAlphaScale = visualSettings.AlphaScale;
        spawnTime = Time.time;

        EnsureRenderer();
        ApplyRendererStyle();
        RefreshCircle(lineRenderer, Mathf.Max(0.16f, pulseRadius * 0.16f));
        RefreshCircle(glowRenderer, Mathf.Max(0.2f, pulseRadius * 0.2f));
    }

    private void Awake()
    {
        EnsureRenderer();
    }

    private void Update()
    {
        float normalizedAge = Mathf.Clamp01((Time.time - spawnTime) / pulseLifetime);
        float animationAge = Mathf.Clamp01((Time.time - spawnTime) / Mathf.Max(0.05f, pulseAnimationLifetime));
        float currentRadius = Mathf.Lerp(
            Mathf.Max(0.16f, pulseRadius * 0.16f),
            pulseRadius,
            Mathf.SmoothStep(0f, 1f, animationAge));
        float alpha = Mathf.Lerp(1f, 0f, normalizedAge);
        float expandingAlpha = alpha * Mathf.Lerp(1f, 0.32f, animationAge);

        RefreshCircle(lineRenderer, currentRadius);
        RefreshCircle(glowRenderer, currentRadius);
        Color color = new(pulseColor.r, pulseColor.g, pulseColor.b, expandingAlpha * RingAlphaScale * pulseAlphaScale);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        glowRenderer.startColor = new Color(pulseColor.r, pulseColor.g, pulseColor.b, expandingAlpha * GlowAlphaScale * pulseAlphaScale);
        glowRenderer.endColor = glowRenderer.startColor;
        coreRenderer.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, Mathf.Lerp(CoreAlphaScale * pulseAlphaScale, 0f, normalizedAge * 1.15f));
        coreRenderer.transform.localScale = Vector3.Lerp(
            new Vector3(BaseCoreScale, BaseCoreScale, 1f),
            new Vector3(ExpandedCoreScale, ExpandedCoreScale, 1f),
            normalizedAge);

        if (normalizedAge >= 1f)
        {
            StopAndRecycle();
        }
    }

    public void StopAndRecycle()
    {
        if (owner != null)
        {
            owner.RecycleDebugPulse(this);
            return;
        }

        Destroy(gameObject);
    }

    private void EnsureRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        if (glowRenderer == null)
        {
            Transform existingGlow = transform.Find("GlowRing");
            GameObject glowObject = existingGlow != null ? existingGlow.gameObject : new GameObject("GlowRing");
            glowObject.transform.SetParent(transform, false);
            glowRenderer = glowObject.GetComponent<LineRenderer>();

            if (glowRenderer == null)
            {
                glowRenderer = glowObject.AddComponent<LineRenderer>();
            }
        }

        if (coreRenderer == null)
        {
            Transform existingCore = transform.Find("Core");
            GameObject coreObject = existingCore != null ? existingCore.gameObject : new GameObject("Core");
            coreObject.transform.SetParent(transform, false);
            coreRenderer = coreObject.GetComponent<SpriteRenderer>();

            if (coreRenderer == null)
            {
                coreRenderer = coreObject.AddComponent<SpriteRenderer>();
            }
        }

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = SegmentCount;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.widthMultiplier = BaseRingWidth;
        lineRenderer.numCornerVertices = 8;
        lineRenderer.numCapVertices = 4;
        lineRenderer.sortingOrder = RingSortingOrder;
        lineRenderer.sharedMaterial = GetSharedLineMaterial();

        glowRenderer.useWorldSpace = false;
        glowRenderer.loop = true;
        glowRenderer.positionCount = SegmentCount;
        glowRenderer.alignment = LineAlignment.View;
        glowRenderer.textureMode = LineTextureMode.Stretch;
        glowRenderer.widthMultiplier = BaseGlowWidth;
        glowRenderer.numCornerVertices = 8;
        glowRenderer.numCapVertices = 4;
        glowRenderer.sortingOrder = GlowSortingOrder;
        glowRenderer.sharedMaterial = GetSharedLineMaterial();

        coreRenderer.sprite = GetSharedCoreSprite();
        coreRenderer.sortingOrder = CoreSortingOrder;
        ApplyRendererStyle();
    }

    private void ApplyRendererStyle()
    {
        lineRenderer.widthMultiplier = BaseRingWidth * pulseWidthScale;
        glowRenderer.widthMultiplier = BaseGlowWidth * pulseWidthScale;
    }

    private void RefreshCircle(LineRenderer targetRenderer, float radius)
    {
        if (targetRenderer == null)
        {
            return;
        }

        for (int index = 0; index < SegmentCount; index++)
        {
            Vector3 point = UnitCirclePoints[index];
            scaledCirclePoints[index] = new Vector3(point.x * radius, point.y * radius, 0f);
        }

        targetRenderer.SetPositions(scaledCirclePoints);
    }

    private static Vector3[] BuildUnitCirclePoints()
    {
        Vector3[] points = new Vector3[SegmentCount];
        float step = Mathf.PI * 2f / SegmentCount;

        for (int index = 0; index < points.Length; index++)
        {
            float angle = index * step;
            points[index] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        return points;
    }

    private static Material GetSharedLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        sharedLineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedLineMaterial;
    }

    private static Sprite GetSharedCoreSprite()
    {
        if (sharedCoreSprite != null)
        {
            return sharedCoreSprite;
        }

        Texture2D texture = new(24, 24, TextureFormat.RGBA32, false);
        Vector2 center = new(11.5f, 11.5f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalized = Mathf.Clamp01(distance / 11.5f);
                float alpha = Mathf.Clamp01(1f - (normalized * normalized));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        sharedCoreSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 24f);
        return sharedCoreSprite;
    }
}


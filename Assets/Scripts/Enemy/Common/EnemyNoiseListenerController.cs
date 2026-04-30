using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyNoiseListenerController : MonoBehaviour
{
    private const float DefaultLoudFloorSuspicionAmount = 1f;
    private const float DefaultLoudFloorSuspicionThreshold = 2f;
    private const float DefaultLoudFloorSuspicionMemoryDuration = 8f;

    [SerializeField] private int emitterInstanceId;
    [SerializeField] private float lastProcessedNoiseTime = float.NegativeInfinity;
    [SerializeField] private int lastProcessedNoiseSequenceId;
    [SerializeField] private bool loudFloorSuspicionEnabled = true;
    [SerializeField, Min(0f)] private float loudFloorSuspicionAmount = DefaultLoudFloorSuspicionAmount;
    [SerializeField, Min(0.001f)] private float loudFloorSuspicionThreshold = DefaultLoudFloorSuspicionThreshold;
    [SerializeField, Min(0f)] private float loudFloorSuspicionMemoryDuration = DefaultLoudFloorSuspicionMemoryDuration;
    [SerializeField] private NoiseSystem noiseSystem;
    private readonly EnemyNoiseSuspicionAccumulator loudFloorSuspicion = new();
    private INoiseEventBus noiseEventBus;

    public float LastProcessedNoiseTime => lastProcessedNoiseTime;
    public int LastProcessedNoiseSequenceId => lastProcessedNoiseSequenceId;
    public float LoudFloorSuspicionValue => loudFloorSuspicion.CurrentValue;
    public bool HasPendingLoudFloorSuspicion => loudFloorSuspicion.HasPendingNoise;

    public void Configure(int configuredEmitterInstanceId = 0, INoiseEventBus configuredNoiseEventBus = null)
    {
        emitterInstanceId = configuredEmitterInstanceId != 0 ? configuredEmitterInstanceId : gameObject.GetInstanceID();
        noiseEventBus = configuredNoiseEventBus ?? NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
    }

    public void ConfigureLoudFloorSuspicion(bool enabled, float amount, float threshold, float memoryDuration)
    {
        loudFloorSuspicionEnabled = enabled;
        loudFloorSuspicionAmount = Mathf.Max(0f, amount);
        loudFloorSuspicionThreshold = Mathf.Max(0.001f, threshold);
        loudFloorSuspicionMemoryDuration = Mathf.Max(0f, memoryDuration);
        loudFloorSuspicion.Clear();
    }

    public void ClearNoiseSuspicion()
    {
        loudFloorSuspicion.Clear();
    }

    public bool TryConsumeLatestRelevantNoise(Func<NoiseEventRecord, bool> isRelevantNoise, out NoiseEventRecord record)
    {
        record = default;

        INoiseEventBus eventBus = ResolveNoiseEventBus();

        if (isRelevantNoise == null || eventBus == null)
        {
            return false;
        }

        bool hasBestCandidate = false;
        NoiseEventRecord bestCandidate = default;
        int bestCandidateIndex = -1;
        bool hasLatestProcessedNoise = false;
        NoiseEventRecord latestProcessedNoise = default;
        int latestProcessedNoiseIndex = -1;
        int eventCount = eventBus.RecentEventCount;

        for (int index = 0; index < eventCount; index++)
        {
            NoiseEventRecord candidate = eventBus.GetRecentEventAt(index);

            if (IsAlreadyProcessed(candidate)
                || IsOwnNoise(candidate)
                || IsIgnoredEnemyNoise(candidate)
                || !isRelevantNoise(candidate))
            {
                continue;
            }

            if (!hasLatestProcessedNoise || IsNewerNoise(candidate, latestProcessedNoise, index, latestProcessedNoiseIndex))
            {
                latestProcessedNoise = candidate;
                latestProcessedNoiseIndex = index;
                hasLatestProcessedNoise = true;
            }

            if (!TryGetActionableNoise(candidate, out NoiseEventRecord actionableCandidate))
            {
                continue;
            }

            if (!hasBestCandidate || IsNewerNoise(actionableCandidate, bestCandidate, index, bestCandidateIndex))
            {
                bestCandidate = actionableCandidate;
                bestCandidateIndex = index;
                hasBestCandidate = true;
            }
        }

        if (hasLatestProcessedNoise)
        {
            MarkProcessed(latestProcessedNoise);
        }

        if (!hasBestCandidate)
        {
            return false;
        }

        record = bestCandidate;
        return true;
    }

    private void OnValidate()
    {
        loudFloorSuspicionAmount = Mathf.Max(0f, loudFloorSuspicionAmount);
        loudFloorSuspicionThreshold = Mathf.Max(0.001f, loudFloorSuspicionThreshold);
        loudFloorSuspicionMemoryDuration = Mathf.Max(0f, loudFloorSuspicionMemoryDuration);
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private bool TryGetActionableNoise(NoiseEventRecord candidate, out NoiseEventRecord actionableRecord)
    {
        if (!ShouldGateWithSuspicion(candidate.sourceType))
        {
            actionableRecord = candidate;
            return true;
        }

        return loudFloorSuspicion.TryAccumulate(
            candidate,
            loudFloorSuspicionAmount,
            loudFloorSuspicionThreshold,
            loudFloorSuspicionMemoryDuration,
            out actionableRecord);
    }

    private bool ShouldGateWithSuspicion(NoiseSourceType sourceType)
    {
        return loudFloorSuspicionEnabled && sourceType == NoiseSourceType.LoudFloor;
    }

    private bool IsAlreadyProcessed(NoiseEventRecord record)
    {
        if (record.time < lastProcessedNoiseTime)
        {
            return true;
        }

        if (record.time > lastProcessedNoiseTime)
        {
            return false;
        }

        if (record.sequenceId <= 0 || lastProcessedNoiseSequenceId <= 0)
        {
            return true;
        }

        return record.sequenceId <= lastProcessedNoiseSequenceId;
    }

    private void MarkProcessed(NoiseEventRecord record)
    {
        lastProcessedNoiseTime = record.time;
        lastProcessedNoiseSequenceId = record.sequenceId;
    }

    private static bool IsNewerNoise(NoiseEventRecord candidate, NoiseEventRecord current, int candidateIndex, int currentIndex)
    {
        if (candidate.time > current.time)
        {
            return true;
        }

        if (candidate.time < current.time)
        {
            return false;
        }

        if (candidate.sequenceId != current.sequenceId)
        {
            return candidate.sequenceId > current.sequenceId;
        }

        return candidateIndex > currentIndex;
    }

    private bool IsOwnNoise(NoiseEventRecord record)
    {
        return emitterInstanceId != 0 && record.emitterInstanceId != 0 && record.emitterInstanceId == emitterInstanceId;
    }

    private static bool IsIgnoredEnemyNoise(NoiseEventRecord record)
    {
        return record.emitterAffiliation == NoiseEmitterAffiliation.Enemy;
    }
}

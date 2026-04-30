using UnityEngine;

public sealed class EnemyNoiseSuspicionAccumulator
{
    private float currentValue;
    private float lastSuspicionTime = float.NegativeInfinity;
    private NoiseEventRecord latestSuspiciousNoise;
    private bool hasLatestSuspiciousNoise;

    public float CurrentValue => currentValue;
    public bool HasPendingNoise => hasLatestSuspiciousNoise;
    public float LastSuspicionTime => lastSuspicionTime;
    public NoiseEventRecord LatestSuspiciousNoise => latestSuspiciousNoise;

    public bool TryAccumulate(
        NoiseEventRecord record,
        float amount,
        float threshold,
        float memoryDuration,
        out NoiseEventRecord triggeredRecord)
    {
        triggeredRecord = default;

        float safeAmount = Mathf.Max(0f, amount);
        float safeThreshold = Mathf.Max(0.001f, threshold);
        float safeMemoryDuration = Mathf.Max(0f, memoryDuration);

        if (hasLatestSuspiciousNoise
            && safeMemoryDuration > 0f
            && record.time - lastSuspicionTime > safeMemoryDuration)
        {
            Clear();
        }

        currentValue += safeAmount;
        latestSuspiciousNoise = record;
        hasLatestSuspiciousNoise = true;
        lastSuspicionTime = record.time;

        if (currentValue < safeThreshold)
        {
            return false;
        }

        triggeredRecord = latestSuspiciousNoise;
        Clear();
        return true;
    }

    public void Clear()
    {
        currentValue = 0f;
        lastSuspicionTime = float.NegativeInfinity;
        latestSuspiciousNoise = default;
        hasLatestSuspiciousNoise = false;
    }
}

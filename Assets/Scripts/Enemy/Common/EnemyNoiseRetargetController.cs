using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyNoiseRetargetController : MonoBehaviour
{
    [SerializeField] private int activeTargetId = -1;
    [SerializeField] private int activePriority = int.MinValue;
    [SerializeField] private float nextRetargetTime;

    public int ActiveTargetId => activeTargetId;
    public int ActivePriority => activePriority;

    public void Clear()
    {
        activeTargetId = -1;
        activePriority = int.MinValue;
        nextRetargetTime = Time.time;
    }

    public bool CanRetarget(int targetId, int priority)
    {
        if (activeTargetId == targetId)
        {
            return false;
        }

        if (activeTargetId >= 0
            && priority <= activePriority
            && Time.time < nextRetargetTime)
        {
            return false;
        }

        return true;
    }

    public void Commit(int targetId, int priority, float cooldownDuration)
    {
        activeTargetId = targetId;
        activePriority = priority;
        nextRetargetTime = Time.time + Mathf.Max(0f, cooldownDuration);
    }

    public static int GetPriority(NoiseSourceType sourceType)
    {
        return sourceType switch
        {
            NoiseSourceType.Door => 4,
            NoiseSourceType.Interact => 3,
            NoiseSourceType.Collision => 3,
            NoiseSourceType.LoudFloor => 2,
            NoiseSourceType.Sprint => 1,
            NoiseSourceType.Walk => 0,
            _ => 0
        };
    }
}

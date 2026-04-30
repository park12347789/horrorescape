/*
 * File Role:
 * Stores reusable enemy tuning values in a ScriptableObject-friendly format.
 *
 * Runtime Use:
 * Defines movement speed, sensing ranges, capture distance, search time, and idle behavior defaults.
 *
 * Study Notes:
 * This file is the easiest place to see which numbers drive the shared enemy state machine.
 */

using UnityEngine;

public enum EnemyIdleBehavior
{
    Patrol,
    FollowTarget,
    FollowTrail,
    StandGuard
}

public enum EnemyAlertRecoveryBehavior
{
    SearchArea,
    HoldPosition
}

[CreateAssetMenu(menuName = "HorrorStealth/Enemy Archetype", fileName = "EnemyArchetype")]
public sealed class EnemyArchetype : ScriptableObject
{
    [SerializeField] private EnemyIdleBehavior idleBehavior = EnemyIdleBehavior.Patrol;
    [SerializeField, Min(0f)] private float captureDistance = 0.45f;
    [SerializeField, Min(0f)] private float patrolSpeed = 1.7f;
    [SerializeField, Min(0f)] private float investigateSpeed = 2.1f;
    [SerializeField, Min(0f)] private float chaseSpeed = 3.15f;
    [SerializeField, Min(0f)] private float visionDistance = 6.25f;
    [SerializeField, Range(0f, 360f)] private float visionAngle = 54f;
    [SerializeField, Min(0f)] private float hearingRadius = 6f;
    [SerializeField, Min(0.05f)] private float patrolWaitTime = 0.7f;
    [SerializeField, Min(1)] private int idleWanderRadius = 4;
    [SerializeField, Min(0.05f)] private float repathInterval = 0.22f;
    [SerializeField, Min(0.05f)] private float chaseMemoryDuration = 0.85f;
    [SerializeField, Min(0.1f)] private float searchDuration = 3.2f;
    [SerializeField, Min(1)] private int searchRadius = 2;
    [SerializeField] private EnemyAlertRecoveryBehavior alertRecoveryBehavior = EnemyAlertRecoveryBehavior.SearchArea;

    public EnemyIdleBehavior IdleBehavior => idleBehavior;
    public float CaptureDistance => captureDistance;
    public float PatrolSpeed => patrolSpeed;
    public float InvestigateSpeed => investigateSpeed;
    public float ChaseSpeed => chaseSpeed;
    public float VisionDistance => visionDistance;
    public float VisionAngle => visionAngle;
    public float HearingRadius => hearingRadius;
    public float PatrolWaitTime => patrolWaitTime;
    public int IdleWanderRadius => idleWanderRadius;
    public float RepathInterval => repathInterval;
    public float ChaseMemoryDuration => chaseMemoryDuration;
    public float SearchDuration => searchDuration;
    public int SearchRadius => searchRadius;
    public EnemyAlertRecoveryBehavior AlertRecoveryBehavior => alertRecoveryBehavior;

    public void Configure(
        float configuredPatrolSpeed,
        float configuredInvestigateSpeed,
        float configuredChaseSpeed,
        float configuredVisionDistance,
        float configuredVisionAngle,
        float configuredHearingRadius,
        float configuredPatrolWaitTime,
        int configuredIdleWanderRadius,
        float configuredRepathInterval,
        float configuredChaseMemoryDuration,
        float configuredSearchDuration,
        int configuredSearchRadius,
        EnemyIdleBehavior configuredIdleBehavior = EnemyIdleBehavior.Patrol,
        float configuredCaptureDistance = 0.45f,
        EnemyAlertRecoveryBehavior configuredAlertRecoveryBehavior = EnemyAlertRecoveryBehavior.SearchArea)
    {
        idleBehavior = configuredIdleBehavior;
        captureDistance = Mathf.Max(0f, configuredCaptureDistance);
        patrolSpeed = Mathf.Max(0f, configuredPatrolSpeed);
        investigateSpeed = Mathf.Max(0f, configuredInvestigateSpeed);
        chaseSpeed = Mathf.Max(0f, configuredChaseSpeed);
        visionDistance = Mathf.Max(0f, configuredVisionDistance);
        visionAngle = Mathf.Clamp(configuredVisionAngle, 0f, 360f);
        hearingRadius = Mathf.Max(0f, configuredHearingRadius);
        patrolWaitTime = Mathf.Max(0.05f, configuredPatrolWaitTime);
        idleWanderRadius = Mathf.Max(1, configuredIdleWanderRadius);
        repathInterval = Mathf.Max(0.05f, configuredRepathInterval);
        chaseMemoryDuration = Mathf.Max(0.05f, configuredChaseMemoryDuration);
        searchDuration = Mathf.Max(0.1f, configuredSearchDuration);
        searchRadius = Mathf.Max(1, configuredSearchRadius);
        alertRecoveryBehavior = configuredAlertRecoveryBehavior;
    }
}


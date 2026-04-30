using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyActivityWatchdogController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float inactivityTimeout = 3f;
    [SerializeField, Min(0f)] private float movementActivityThreshold = 0.04f;
    [SerializeField, Min(0.1f)] private float pathStallTimeout = 0.55f;
    [SerializeField, Min(0f)] private float pathProgressThreshold = 0.015f;

    private float lastActivityTime;
    private Vector3 lastActivityPosition;
    private bool initialized;
    private float lastPathProgressTime;
    private float lastPathRemainingDistance = float.PositiveInfinity;
    private Vector3Int trackedPathCell;
    private bool trackingPathProgress;

    public float InactivityTimeout => inactivityTimeout;

    public void Configure(float configuredInactivityTimeout, float configuredMovementActivityThreshold = 0.04f)
    {
        inactivityTimeout = Mathf.Max(0.1f, configuredInactivityTimeout);
        movementActivityThreshold = Mathf.Max(0f, configuredMovementActivityThreshold);
        ResetWatchdog(transform.position);
    }

    public void BeginFrame(Vector3 currentPosition)
    {
        if (!initialized)
        {
            ResetWatchdog(currentPosition);
            return;
        }

        if (Vector3.Distance(currentPosition, lastActivityPosition) >= movementActivityThreshold)
        {
            ReportActivity(currentPosition);
        }
    }

    public void ReportActivity()
    {
        ReportActivity(transform.position);
    }

    public void ReportActivity(Vector3 currentPosition)
    {
        lastActivityTime = Time.time;
        lastActivityPosition = currentPosition;
        initialized = true;
    }

    public void BeginPathTracking(Vector3Int targetCell, float remainingDistance)
    {
        remainingDistance = Mathf.Max(0f, remainingDistance);

        if (!trackingPathProgress || targetCell != trackedPathCell)
        {
            trackedPathCell = targetCell;
            lastPathRemainingDistance = remainingDistance;
            lastPathProgressTime = Time.time;
            trackingPathProgress = true;
            return;
        }

        if (remainingDistance + pathProgressThreshold < lastPathRemainingDistance)
        {
            lastPathRemainingDistance = remainingDistance;
            lastPathProgressTime = Time.time;
            ReportActivity();
        }
    }

    public bool TryConsumePathStall(Vector3Int targetCell, float remainingDistance)
    {
        BeginPathTracking(targetCell, remainingDistance);

        if (!trackingPathProgress)
        {
            return false;
        }

        if (targetCell != trackedPathCell)
        {
            return false;
        }

        if (Time.time - lastPathProgressTime < pathStallTimeout)
        {
            return false;
        }

        lastPathProgressTime = Time.time;
        lastPathRemainingDistance = Mathf.Max(0f, remainingDistance);
        return true;
    }

    public void ResetPathTracking()
    {
        trackingPathProgress = false;
        lastPathRemainingDistance = float.PositiveInfinity;
    }

    public bool TryConsumeTimeout(Vector3 currentPosition)
    {
        BeginFrame(currentPosition);

        if (Time.time - lastActivityTime < inactivityTimeout)
        {
            return false;
        }

        ResetWatchdog(currentPosition);
        return true;
    }

    public void ResetWatchdog(Vector3 currentPosition)
    {
        lastActivityTime = Time.time;
        lastActivityPosition = currentPosition;
        initialized = true;
        ResetPathTracking();
    }
}

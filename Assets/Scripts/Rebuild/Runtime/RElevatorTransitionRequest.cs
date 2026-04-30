using System;

using UnityEngine;

public readonly struct RElevatorTransitionRequest
{
    public RElevatorTransitionRequest(
        string targetScenePath,
        int sourceFloorNumber,
        int destinationFloorNumber,
        string reason)
    {
        TargetScenePath = targetScenePath?.Trim() ?? string.Empty;
        SourceFloorNumber = Mathf.Max(0, sourceFloorNumber);
        DestinationFloorNumber = Mathf.Max(0, destinationFloorNumber);
        Reason = reason?.Trim() ?? string.Empty;
    }

    public string TargetScenePath { get; }
    public int SourceFloorNumber { get; }
    public int DestinationFloorNumber { get; }
    public string Reason { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(TargetScenePath);
}

public static class RElevatorTransitionRequestStore
{
    private static bool hasPendingRequest;
    private static RElevatorTransitionRequest pendingRequest;

    public static bool HasPendingRequest => hasPendingRequest && pendingRequest.IsValid;

    public static void Set(RElevatorTransitionRequest request)
    {
        if (!request.IsValid)
        {
            Clear();
            Debug.LogError($"{nameof(RElevatorTransitionRequestStore)} received an empty target scene path.");
            return;
        }

        pendingRequest = request;
        hasPendingRequest = true;
    }

    public static bool TryConsume(out RElevatorTransitionRequest request)
    {
        if (HasPendingRequest)
        {
            request = pendingRequest;
            Clear();
            return true;
        }

        request = default;
        return false;
    }

    public static void Clear()
    {
        pendingRequest = default;
        hasPendingRequest = false;
    }
}

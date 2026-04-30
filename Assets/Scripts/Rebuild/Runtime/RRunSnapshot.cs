using System;

using UnityEngine;

[Serializable]
public readonly struct RRunSnapshot
{
    public RRunSnapshot(
        bool runStarted,
        bool hasActiveRun,
        int currentFloorNumber,
        int floorsCleared,
        RRunOutcome outcome,
        string failureSource)
    {
        RunStarted = runStarted;
        HasActiveRun = hasActiveRun;
        CurrentFloorNumber = Mathf.Max(0, currentFloorNumber);
        FloorsCleared = Mathf.Max(0, floorsCleared);
        Outcome = outcome;
        FailureSource = string.IsNullOrWhiteSpace(failureSource) ? string.Empty : failureSource;
    }

    public bool RunStarted { get; }
    public bool HasActiveRun { get; }
    public int CurrentFloorNumber { get; }
    public int FloorsCleared { get; }
    public RRunOutcome Outcome { get; }
    public string FailureSource { get; }

    public bool HasCompletedRun => Outcome == RRunOutcome.Cleared || Outcome == RRunOutcome.Failed;
    public bool WasSuccessful => Outcome == RRunOutcome.Cleared;

    public string SummaryTitle => Outcome switch
    {
        RRunOutcome.Cleared => "Route Cleared",
        RRunOutcome.Failed => "Last Descent Failed",
        RRunOutcome.InProgress when HasActiveRun => "Descent In Progress",
        _ => "Awaiting Deployment"
    };

    public string SummaryBody => Outcome switch
    {
        RRunOutcome.Cleared => "Escape route secured.\nExtraction window is open.",
        RRunOutcome.Failed => string.IsNullOrWhiteSpace(FailureSource)
            ? $"Descent collapsed on {CurrentFloorNumber}F.\nReset the route and redeploy."
            : $"Descent collapsed on {CurrentFloorNumber}F.\nHostile contact: {FailureSource}.",
        RRunOutcome.InProgress when HasActiveRun => $"Current floor locked to {CurrentFloorNumber}F.\nFloors secured: {FloorsCleared}",
        _ => "Stage on the entry floor, follow the route, and force a path to extraction."
    };
}

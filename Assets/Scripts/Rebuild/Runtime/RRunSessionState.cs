using System;

using UnityEngine;

[Serializable]
public sealed class RRunSessionState
{
    [SerializeField] private bool runStarted;
    [SerializeField] private bool hasActiveRun;
    [SerializeField, Min(0)] private int currentFloorNumber;
    [SerializeField, Min(0)] private int floorsCleared;
    [SerializeField] private RRunOutcome outcome;
    [SerializeField] private string failureSource = string.Empty;

    public bool RunStarted => runStarted;
    public bool HasActiveRun => hasActiveRun;
    public int CurrentFloorNumber => currentFloorNumber;
    public int FloorsCleared => floorsCleared;
    public RRunOutcome Outcome => outcome;
    public string FailureSource => failureSource;

    public void Overwrite(
        bool nextRunStarted,
        bool nextHasActiveRun,
        int nextCurrentFloorNumber,
        int nextFloorsCleared,
        RRunOutcome nextOutcome,
        string nextFailureSource)
    {
        runStarted = nextRunStarted;
        hasActiveRun = nextHasActiveRun;
        currentFloorNumber = Mathf.Max(0, nextCurrentFloorNumber);
        floorsCleared = Mathf.Max(0, nextFloorsCleared);
        outcome = nextOutcome;
        failureSource = string.IsNullOrWhiteSpace(nextFailureSource) ? string.Empty : nextFailureSource;
    }

    public RRunSnapshot ToSnapshot()
    {
        return new RRunSnapshot(
            runStarted,
            hasActiveRun,
            currentFloorNumber,
            floorsCleared,
            outcome,
            failureSource);
    }

    public void ResetForNewRun(int startingFloorNumber)
    {
        runStarted = true;
        hasActiveRun = true;
        currentFloorNumber = Mathf.Max(1, startingFloorNumber);
        floorsCleared = 0;
        outcome = RRunOutcome.InProgress;
        failureSource = string.Empty;
    }

    public void RecordFloorAdvance(int destinationFloorNumber, int clearedFloorNumber)
    {
        hasActiveRun = true;
        currentFloorNumber = Mathf.Max(1, destinationFloorNumber);
        floorsCleared = Mathf.Max(floorsCleared, Mathf.Max(0, clearedFloorNumber));
        outcome = RRunOutcome.InProgress;
        failureSource = string.Empty;
    }

    public void RecordRunFailure(int activeFloorNumber, string caughtBy)
    {
        hasActiveRun = false;
        currentFloorNumber = Mathf.Max(1, activeFloorNumber);
        outcome = RRunOutcome.Failed;
        failureSource = string.IsNullOrWhiteSpace(caughtBy) ? string.Empty : caughtBy;
    }

    public void RecordRunClear(int activeFloorNumber)
    {
        hasActiveRun = false;
        currentFloorNumber = Mathf.Max(1, activeFloorNumber);
        outcome = RRunOutcome.Cleared;
        failureSource = string.Empty;
    }
}

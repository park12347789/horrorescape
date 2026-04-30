using System;

using UnityEngine;

internal static class RunSessionStateTransitionUtility
{
    public static void BeginNewRun<TOutcome>(
        ref bool runStarted,
        ref bool hasActiveRun,
        ref int currentFloorNumber,
        ref int floorsCleared,
        ref TOutcome outcome,
        ref string failureSource,
        int startingFloorNumber,
        TOutcome inProgressOutcome)
        where TOutcome : struct
    {
        runStarted = true;
        hasActiveRun = true;
        currentFloorNumber = Mathf.Max(1, startingFloorNumber);
        floorsCleared = 0;
        outcome = inProgressOutcome;
        failureSource = string.Empty;
    }

    public static void EnsureGameplayRunInitialized<TOutcome>(
        ref bool runStarted,
        ref bool hasActiveRun,
        ref int currentFloorNumber,
        ref int floorsCleared,
        ref TOutcome outcome,
        ref string failureSource,
        int startingFloorNumber,
        TOutcome inProgressOutcome,
        Func<TOutcome, bool> hasCompletedRun,
        Func<TOutcome, bool> isNoneOutcome)
        where TOutcome : struct
    {
        if (!runStarted || hasCompletedRun(outcome))
        {
            BeginNewRun(
                ref runStarted,
                ref hasActiveRun,
                ref currentFloorNumber,
                ref floorsCleared,
                ref outcome,
                ref failureSource,
                startingFloorNumber,
                inProgressOutcome);
            return;
        }

        hasActiveRun = true;

        if (currentFloorNumber <= 0)
        {
            currentFloorNumber = Mathf.Max(1, startingFloorNumber);
        }

        if (isNoneOutcome(outcome))
        {
            outcome = inProgressOutcome;
        }
    }

    public static void RecordFloorClear<TOutcome>(
        ref bool runStarted,
        ref bool hasActiveRun,
        ref int currentFloorNumber,
        ref int floorsCleared,
        ref TOutcome outcome,
        ref string failureSource,
        int clearedFloorNumber,
        int destinationFloorNumber,
        TOutcome inProgressOutcome,
        Func<int, int, int> resolveFloorsCleared,
        Func<int, string> buildFailureSource)
        where TOutcome : struct
    {
        runStarted = true;
        hasActiveRun = true;
        outcome = inProgressOutcome;
        floorsCleared = resolveFloorsCleared(floorsCleared, destinationFloorNumber);
        currentFloorNumber = Mathf.Max(1, destinationFloorNumber);
        failureSource = buildFailureSource(Mathf.Max(1, clearedFloorNumber));
    }

    public static void RecordFinalClear<TOutcome>(
        ref bool runStarted,
        ref bool hasActiveRun,
        ref int currentFloorNumber,
        ref int floorsCleared,
        ref TOutcome outcome,
        ref string failureSource,
        int clearedFloorNumber,
        TOutcome clearedOutcome,
        Func<int, int, int> resolveFloorsCleared)
        where TOutcome : struct
    {
        runStarted = true;
        hasActiveRun = false;
        currentFloorNumber = Mathf.Max(1, clearedFloorNumber);
        floorsCleared = resolveFloorsCleared(floorsCleared, currentFloorNumber);
        outcome = clearedOutcome;
        failureSource = string.Empty;
    }

    public static void RecordFailure<TOutcome>(
        ref bool runStarted,
        ref bool hasActiveRun,
        ref int currentFloorNumber,
        ref TOutcome outcome,
        ref string failureSource,
        int floorNumber,
        TOutcome failedOutcome,
        string sourceName,
        string defaultSource)
        where TOutcome : struct
    {
        runStarted = true;
        hasActiveRun = false;
        currentFloorNumber = Mathf.Max(1, floorNumber);
        outcome = failedOutcome;
        failureSource = string.IsNullOrWhiteSpace(sourceName) ? defaultSource : sourceName;
    }
}

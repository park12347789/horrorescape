using System;
using UnityEngine;

public delegate bool EnemyTryFindReachableCell(Vector3Int targetCell, int searchRadius, out Vector3Int reachableCell);

public readonly struct EnemyPursuitResolution
{
    public EnemyPursuitResolution(Vector3Int preferredTargetCell, Vector3Int resolvedTargetCell, bool hasPath, bool isAlreadyAtTarget, bool usedFallback)
    {
        PreferredTargetCell = preferredTargetCell;
        ResolvedTargetCell = resolvedTargetCell;
        HasPath = hasPath;
        IsAlreadyAtTarget = isAlreadyAtTarget;
        UsedFallback = usedFallback;
    }

    public Vector3Int PreferredTargetCell { get; }
    public Vector3Int ResolvedTargetCell { get; }
    public bool HasPath { get; }
    public bool IsAlreadyAtTarget { get; }
    public bool UsedFallback { get; }
    public bool HasReachableTarget => HasPath || IsAlreadyAtTarget;
}

public static class EnemyPursuitTargetResolver
{
    public static EnemyPursuitResolution Resolve(
        Vector3Int preferredTargetCell,
        int fallbackSearchRadius,
        Func<Vector3Int, bool> tryBuildPath,
        Func<Vector3Int, bool> isAtTarget,
        EnemyTryFindReachableCell tryFindReachableNearbyCell)
    {
        if (tryBuildPath == null || isAtTarget == null)
        {
            return new EnemyPursuitResolution(preferredTargetCell, preferredTargetCell, false, false, false);
        }

        Vector3Int resolvedTargetCell = preferredTargetCell;
        bool isAlreadyAtTarget = isAtTarget(resolvedTargetCell);
        bool hasPath = !isAlreadyAtTarget && tryBuildPath(resolvedTargetCell);
        bool usedFallback = false;

        if (!hasPath
            && !isAlreadyAtTarget
            && tryFindReachableNearbyCell != null
            && tryFindReachableNearbyCell(resolvedTargetCell, Mathf.Max(0, fallbackSearchRadius), out Vector3Int fallbackCell))
        {
            usedFallback = fallbackCell != resolvedTargetCell;
            resolvedTargetCell = fallbackCell;
            isAlreadyAtTarget = isAtTarget(resolvedTargetCell);
            hasPath = !isAlreadyAtTarget && tryBuildPath(resolvedTargetCell);
        }

        return new EnemyPursuitResolution(preferredTargetCell, resolvedTargetCell, hasPath, isAlreadyAtTarget, usedFallback);
    }
}

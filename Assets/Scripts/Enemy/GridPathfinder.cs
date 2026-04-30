/*
 * File Role:
 * Provides grid-based A* pathfinding on top of GridMapService.
 *
 * Runtime Use:
 * Builds cell paths that enemies can follow through rooms, corridors, and doors.
 *
 * Study Notes:
 * This file is intentionally small, so it is a good first pathfinding reference.
 */

using System.Collections.Generic;
using UnityEngine;

public static class GridPathfinder
{
    private static readonly Vector3Int[] NeighborOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    };

    [System.ThreadStatic] private static List<Vector3Int> openSet;
    [System.ThreadStatic] private static HashSet<Vector3Int> openSetLookup;
    [System.ThreadStatic] private static HashSet<Vector3Int> closedSet;
    [System.ThreadStatic] private static Dictionary<Vector3Int, Vector3Int> cameFrom;
    [System.ThreadStatic] private static Dictionary<Vector3Int, int> gScore;
    [System.ThreadStatic] private static Dictionary<Vector3Int, int> fScore;

    public static bool TryBuildPath(
        GridMapService mapService,
        Vector3Int startCell,
        Vector3Int goalCell,
        List<Vector3Int> result,
        bool allowClosedDoors = false,
        Vector3Int? temporarilyBlockedCell = null)
    {
        result?.Clear();

        if (mapService == null || result == null)
        {
            return false;
        }

        if (startCell == goalCell)
        {
            return true;
        }

        if (temporarilyBlockedCell.HasValue && goalCell == temporarilyBlockedCell.Value)
        {
            return false;
        }

        if (!mapService.IsWalkable(goalCell, allowClosedDoors))
        {
            return false;
        }

        PrepareSearchBuffers();
        openSet.Add(startCell);
        openSetLookup.Add(startCell);
        gScore[startCell] = 0;
        fScore[startCell] = Heuristic(startCell, goalCell);

        int safetyCounter = 0;

        while (openSet.Count > 0 && safetyCounter < 1024)
        {
            safetyCounter++;
            Vector3Int current = GetLowestScoreNode(openSet, fScore);

            if (current == goalCell)
            {
                ReconstructPath(cameFrom, current, result);
                return true;
            }

            openSet.Remove(current);
            openSetLookup.Remove(current);
            closedSet.Add(current);

            int currentGScore = gScore.TryGetValue(current, out int cachedScore) ? cachedScore : int.MaxValue;

            foreach (Vector3Int offset in NeighborOffsets)
            {
                Vector3Int neighbor = current + offset;

                if (closedSet.Contains(neighbor)
                    || (temporarilyBlockedCell.HasValue && neighbor == temporarilyBlockedCell.Value)
                    || !mapService.IsWalkable(neighbor, allowClosedDoors))
                {
                    continue;
                }

                int tentativeGScore = currentGScore + 1;
                int knownGScore = gScore.TryGetValue(neighbor, out int existingScore) ? existingScore : int.MaxValue;

                if (tentativeGScore >= knownGScore)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goalCell);

                if (openSetLookup.Add(neighbor))
                {
                    openSet.Add(neighbor);
                }
            }
        }

        result.Clear();
        return false;
    }

    private static void PrepareSearchBuffers()
    {
        openSet ??= new List<Vector3Int>(64);
        openSetLookup ??= new HashSet<Vector3Int>();
        closedSet ??= new HashSet<Vector3Int>();
        cameFrom ??= new Dictionary<Vector3Int, Vector3Int>();
        gScore ??= new Dictionary<Vector3Int, int>();
        fScore ??= new Dictionary<Vector3Int, int>();

        openSet.Clear();
        openSetLookup.Clear();
        closedSet.Clear();
        cameFrom.Clear();
        gScore.Clear();
        fScore.Clear();
    }

    private static int Heuristic(Vector3Int from, Vector3Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }

    private static Vector3Int GetLowestScoreNode(List<Vector3Int> openSet, Dictionary<Vector3Int, int> fScore)
    {
        Vector3Int bestNode = openSet[0];
        int bestScore = fScore.TryGetValue(bestNode, out int currentScore) ? currentScore : int.MaxValue;

        for (int index = 1; index < openSet.Count; index++)
        {
            Vector3Int node = openSet[index];
            int score = fScore.TryGetValue(node, out int nodeScore) ? nodeScore : int.MaxValue;

            if (score < bestScore)
            {
                bestNode = node;
                bestScore = score;
            }
        }

        return bestNode;
    }

    private static void ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current, List<Vector3Int> result)
    {
        result.Clear();
        result.Add(current);

        while (cameFrom.TryGetValue(current, out Vector3Int previous))
        {
            current = previous;
            result.Add(current);
        }

        result.Reverse();

        if (result.Count > 0)
        {
            result.RemoveAt(0);
        }
    }
}


/*
 * File Role:
 * Records a recent history of player cells for follower-style enemies.
 *
 * Runtime Use:
 * Lets some enemies use trail data instead of pure hearing or direct sight.
 *
 * Study Notes:
 * A good example of storing short-lived spatial history for AI behaviors.
 */

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerTrailRecorder : MonoBehaviour
{
    [SerializeField] private GridMapService mapService;

    private readonly List<Vector3Int> trailCells = new();
    private readonly List<Vector3Int> pathScratch = new();
    private bool isInitialized;
    private Vector3Int lastRecordedCell;

    public int Count => trailCells.Count;

    public void Configure(GridMapService configuredMapService)
    {
        mapService = configuredMapService;
        trailCells.Clear();
        pathScratch.Clear();
        isInitialized = mapService != null;

        if (!isInitialized)
        {
            return;
        }

        AppendCell(mapService.ResolveNearestWalkableCell(transform.position, 2, true));
    }

    public void BindMapService(GridMapService configuredMapService)
    {
        Configure(configuredMapService);
    }

    public bool TryGetCell(int index, out Vector3Int cell)
    {
        if (index >= 0 && index < trailCells.Count)
        {
            cell = trailCells[index];
            return true;
        }

        cell = default;
        return false;
    }

    public bool TryGetLatestCell(out Vector3Int cell)
    {
        if (trailCells.Count > 0)
        {
            cell = trailCells[trailCells.Count - 1];
            return true;
        }

        cell = default;
        return false;
    }

    public bool TryGetRecentMovementDirection(int sampleCount, out Vector3Int direction)
    {
        direction = default;

        if (trailCells.Count < 2)
        {
            return false;
        }

        int latestIndex = trailCells.Count - 1;
        Vector3Int latestCell = trailCells[latestIndex];
        int earliestIndex = Mathf.Max(0, latestIndex - Mathf.Max(1, sampleCount));

        for (int index = latestIndex - 1; index >= earliestIndex; index--)
        {
            Vector3Int delta = latestCell - trailCells[index];

            if (delta.x == 0 && delta.y == 0)
            {
                continue;
            }

            int absX = Mathf.Abs(delta.x);
            int absY = Mathf.Abs(delta.y);
            direction = absX >= absY
                ? new Vector3Int(delta.x > 0 ? 1 : -1, 0, 0)
                : new Vector3Int(0, delta.y > 0 ? 1 : -1, 0);
            return true;
        }

        return false;
    }

    public int FindClosestIndex(Vector3Int cellPosition, int startIndex = 0)
    {
        if (trailCells.Count == 0)
        {
            return -1;
        }

        int safeStartIndex = Mathf.Clamp(startIndex, 0, trailCells.Count - 1);

        for (int index = safeStartIndex; index < trailCells.Count; index++)
        {
            if (trailCells[index] == cellPosition)
            {
                return index;
            }
        }

        int bestIndex = safeStartIndex;
        int bestDistance = ManhattanDistance(cellPosition, trailCells[safeStartIndex]);

        for (int index = safeStartIndex + 1; index < trailCells.Count; index++)
        {
            int distance = ManhattanDistance(cellPosition, trailCells[index]);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private void Update()
    {
        if (!TryInitialize())
        {
            return;
        }

        Vector3Int currentCell = mapService.ResolveNearestWalkableCell(transform.position, 2, true);

        if (trailCells.Count == 0)
        {
            AppendCell(currentCell);
            return;
        }

        if (currentCell == lastRecordedCell)
        {
            return;
        }

        pathScratch.Clear();

        if (GridPathfinder.TryBuildPath(mapService, lastRecordedCell, currentCell, pathScratch) && pathScratch.Count > 0)
        {
            foreach (Vector3Int cell in pathScratch)
            {
                AppendCell(cell);
            }

            return;
        }

        AppendCell(currentCell);
    }

    private bool TryInitialize()
    {
        if (isInitialized)
        {
            return true;
        }

        if (mapService == null)
        {
            return false;
        }

        isInitialized = true;
        trailCells.Clear();
        pathScratch.Clear();
        AppendCell(mapService.ResolveNearestWalkableCell(transform.position, 2, true));
        return true;
    }

    private void AppendCell(Vector3Int cell)
    {
        trailCells.Add(cell);
        lastRecordedCell = cell;
    }

    private static int ManhattanDistance(Vector3Int from, Vector3Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }
}


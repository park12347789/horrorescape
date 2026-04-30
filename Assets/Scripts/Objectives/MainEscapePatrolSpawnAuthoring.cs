using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class MainEscapePatrolSpawnAuthoring : MonoBehaviour
{
    [FormerlySerializedAs("waypointRoot")]
    [SerializeField] private Transform spawnPoint;

    public Transform SpawnPoint => ResolveSpawnPoint();

    public Vector3Int[] GetSpawnCells(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return System.Array.Empty<Vector3Int>();
        }

        List<Vector3Int> spawnCells = new(transform.childCount);

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);

            if (child == null)
            {
                continue;
            }

            Vector3Int cell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, child.position);

            if (!spawnCells.Contains(cell))
            {
                spawnCells.Add(cell);
            }
        }

        return spawnCells.ToArray();
    }

    public bool TryGetSpawnCell(Tilemap tilemap, out Vector3Int spawnCell)
    {
        Transform resolvedSpawnPoint = ResolveSpawnPoint();

        if (tilemap == null || resolvedSpawnPoint == null)
        {
            spawnCell = Vector3Int.zero;
            return false;
        }

        spawnCell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, resolvedSpawnPoint.position);
        return true;
    }

    [ContextMenu("Cache Spawn Point")]
    private void CacheSpawnPointFromHierarchy()
    {
        spawnPoint = ResolveSpawnPoint();
    }

    private Transform ResolveSpawnPoint()
    {
        if (spawnPoint != null && spawnPoint.IsChildOf(transform))
        {
            return spawnPoint;
        }

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);

            if (child == null)
            {
                continue;
            }

            spawnPoint = child;
            return spawnPoint;
        }

        spawnPoint = null;
        return null;
    }
}

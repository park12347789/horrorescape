using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public readonly struct MainEscapeSentrySpawnPoint
{
    public MainEscapeSentrySpawnPoint(string markerName, Vector3Int cell, Vector2 facing, Vector3 worldPosition)
    {
        MarkerName = string.IsNullOrWhiteSpace(markerName) ? "SentryMarker" : markerName;
        Cell = cell;
        Facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.up;
        WorldPosition = worldPosition;
    }

    public string MarkerName { get; }
    public Vector3Int Cell { get; }
    public Vector2 Facing { get; }
    public Vector3 WorldPosition { get; }
}

[DisallowMultipleComponent]
public sealed class MainEscapeSentrySpawnAuthoring : MonoBehaviour
{
    public MainEscapeSentrySpawnPoint[] GetSpawnPoints(Tilemap tilemap)
    {
        if (tilemap == null || transform.childCount == 0)
        {
            return Array.Empty<MainEscapeSentrySpawnPoint>();
        }

        List<MainEscapeSentrySpawnPoint> spawnPoints = new(transform.childCount);
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform spawnMarker = transform.GetChild(index);
            Vector3Int cell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, spawnMarker.position);
            spawnPoints.Add(new MainEscapeSentrySpawnPoint(spawnMarker.name, cell, spawnMarker.up, spawnMarker.position));
        }

        return spawnPoints.ToArray();
    }

    public Vector3Int[] GetSpawnCells(Tilemap tilemap)
    {
        MainEscapeSentrySpawnPoint[] spawnPoints = GetSpawnPoints(tilemap);

        if (spawnPoints.Length == 0)
        {
            return Array.Empty<Vector3Int>();
        }

        Vector3Int[] cells = new Vector3Int[spawnPoints.Length];

        for (int index = 0; index < spawnPoints.Length; index++)
        {
            cells[index] = spawnPoints[index].Cell;
        }

        return cells;
    }
}

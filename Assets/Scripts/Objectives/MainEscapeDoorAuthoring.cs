using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class MainEscapeDoorAuthoring : MonoBehaviour
{
    [SerializeField] private bool requiresKey;
    [SerializeField] private bool marksMainGate;
    [SerializeField] private Transform[] cellMarkers = Array.Empty<Transform>();

    public bool RequiresKey => requiresKey;
    public bool MarksMainGate => marksMainGate;

    public void Configure(bool configuredRequiresKey, bool configuredMarksMainGate, Transform[] configuredCellMarkers)
    {
        requiresKey = configuredRequiresKey;
        marksMainGate = configuredMarksMainGate;
        cellMarkers = configuredCellMarkers ?? Array.Empty<Transform>();
    }

    private void OnValidate()
    {
        SyncChildMarkers();
    }

    private void OnTransformChildrenChanged()
    {
        SyncChildMarkers();
    }

    public Vector3Int[] GetDoorCells(Tilemap tilemap, Tilemap doorTilemap = null)
    {
        Transform[] resolvedMarkers = GetResolvedMarkers();

        if (tilemap == null || resolvedMarkers.Length == 0)
        {
            return Array.Empty<Vector3Int>();
        }

        List<Vector3Int> cells = new(resolvedMarkers.Length);
        HashSet<Vector3Int> seen = new();

        for (int index = 0; index < resolvedMarkers.Length; index++)
        {
            Transform marker = resolvedMarkers[index];

            if (marker == null)
            {
                continue;
            }

            if (TryResolveDoorCell(marker.position, tilemap, doorTilemap, out Vector3Int cell)
                && seen.Add(cell))
            {
                cells.Add(cell);
            }
        }

        return cells.ToArray();
    }

    private static bool TryResolveDoorCell(Vector3 worldPosition, Tilemap referenceTilemap, Tilemap doorTilemap, out Vector3Int cell)
    {
        cell = MainEscapeTilemapCellUtility.WorldToCell2D(referenceTilemap, worldPosition);
        return doorTilemap == null || doorTilemap.HasTile(cell);
    }

    private Transform[] GetResolvedMarkers()
    {
        if (transform.childCount > 0
            && (cellMarkers == null
                || cellMarkers.Length != transform.childCount
                || HasMissingMarker()))
        {
            SyncChildMarkers();
        }

        return cellMarkers ?? Array.Empty<Transform>();
    }

    private void SyncChildMarkers()
    {
        if (transform.childCount == 0)
        {
            cellMarkers = Array.Empty<Transform>();
            return;
        }

        cellMarkers = new Transform[transform.childCount];

        for (int index = 0; index < transform.childCount; index++)
        {
            cellMarkers[index] = transform.GetChild(index);
        }
    }

    private bool HasMissingMarker()
    {
        if (cellMarkers == null)
        {
            return true;
        }

        for (int index = 0; index < cellMarkers.Length; index++)
        {
            if (cellMarkers[index] == null)
            {
                return true;
            }
        }

        return false;
    }
}

public static class MainEscapeTilemapCellUtility
{
    public static Vector3Int WorldToCell2D(Tilemap tilemap, Vector3 worldPosition)
    {
        if (tilemap == null)
        {
            return Vector3Int.zero;
        }

        Vector3 adjustedWorldPosition = worldPosition;
        adjustedWorldPosition.z = tilemap.transform.position.z;

        Vector3Int cell = tilemap.WorldToCell(adjustedWorldPosition);
        cell.z = 0;
        return cell;
    }
}

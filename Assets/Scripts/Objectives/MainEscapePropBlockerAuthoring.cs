using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class MainEscapePropBlockerAuthoring : MonoBehaviour
{
    [SerializeField] private Vector2Int footprint = Vector2Int.one;

    public void Configure(Vector2Int configuredFootprint)
    {
        footprint = new Vector2Int(Mathf.Max(1, configuredFootprint.x), Mathf.Max(1, configuredFootprint.y));
    }

    public Vector3Int[] GetOccupiedCells(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        int width = Mathf.Max(1, footprint.x);
        int height = Mathf.Max(1, footprint.y);
        Vector3Int centerCell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, transform.position);
        int startX = centerCell.x - Mathf.FloorToInt((width - 1) * 0.5f);
        int startY = centerCell.y - Mathf.FloorToInt((height - 1) * 0.5f);
        Vector3Int[] occupiedCells = new Vector3Int[width * height];
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                occupiedCells[index++] = new Vector3Int(startX + x, startY + y, 0);
            }
        }

        return occupiedCells;
    }
}

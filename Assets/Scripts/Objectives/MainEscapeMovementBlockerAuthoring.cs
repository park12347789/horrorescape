using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class MainEscapeMovementBlockerAuthoring : MonoBehaviour
{
    private const int PreviewSortingOrder = 2000;

    [SerializeField] private Vector2Int footprint = Vector2Int.one;
    [SerializeField] private Color visualColor = new(0.16f, 0.98f, 0.88f, 0.78f);

    public Vector2Int Footprint => footprint;

    public void Configure(Vector2Int configuredFootprint, Color configuredVisualColor)
    {
        footprint = new Vector2Int(
            Mathf.Max(1, configuredFootprint.x),
            Mathf.Max(1, configuredFootprint.y));
        visualColor = configuredVisualColor;
        transform.localScale = new Vector3(footprint.x, footprint.y, ResolveDepthScale());
        ApplyVisualsAndCollider();
    }

    public Vector3Int[] GetOccupiedCells(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        BoxCollider2D collider = GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            return GetFootprintCells(tilemap);
        }

        Bounds colliderBounds = collider.bounds;

        if (colliderBounds.size.sqrMagnitude <= Mathf.Epsilon)
        {
            return GetFootprintCells(tilemap);
        }

        const float BoundsInset = 0.001f;
        Vector3Int minCell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, colliderBounds.min + new Vector3(BoundsInset, BoundsInset, 0f));
        Vector3Int maxCell = MainEscapeTilemapCellUtility.WorldToCell2D(tilemap, colliderBounds.max - new Vector3(BoundsInset, BoundsInset, 0f));
        int minX = Mathf.Min(minCell.x, maxCell.x);
        int maxX = Mathf.Max(minCell.x, maxCell.x);
        int minY = Mathf.Min(minCell.y, maxCell.y);
        int maxY = Mathf.Max(minCell.y, maxCell.y);

        var occupiedCells = new System.Collections.Generic.List<Vector3Int>();

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3Int candidateCell = new(x, y, 0);

                if (DoesCellOverlapBounds(tilemap, candidateCell, colliderBounds))
                {
                    occupiedCells.Add(candidateCell);
                }
            }
        }

        return occupiedCells.Count > 0
            ? occupiedCells.ToArray()
            : GetFootprintCells(tilemap);
    }

    private void Reset()
    {
        SyncFootprintFromPlacedScale();
        ApplyVisualsAndCollider();
    }

    private void OnEnable()
    {
        SyncFootprintFromPlacedScale();
        ApplyVisualsAndCollider();
    }

    private void OnValidate()
    {
        SyncFootprintFromPlacedScale();
        ApplyVisualsAndCollider();
    }

    private void ApplyVisualsAndCollider()
    {
        gameObject.layer = 0;

        BoxCollider2D collider = GetComponent<BoxCollider2D>();

        if (collider != null)
        {
            collider.isTrigger = false;
            collider.offset = Vector2.zero;
            // Keep the collider in unscaled local space so footprint scale does not
            // get multiplied twice in world space.
            collider.size = Vector2.one;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
        {
            renderer.color = visualColor;
            renderer.sortingOrder = PreviewSortingOrder;
            renderer.enabled = !Application.isPlaying;
        }
    }

    private void SyncFootprintFromPlacedScale()
    {
        Vector3 placedScale = transform.localScale;
        footprint = new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(placedScale.x))),
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(placedScale.y))));
    }

    private Vector3Int[] GetFootprintCells(Tilemap tilemap)
    {
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

    private static bool DoesCellOverlapBounds(Tilemap tilemap, Vector3Int cell, Bounds colliderBounds)
    {
        Vector3 worldMin = tilemap.CellToWorld(cell);
        Vector3 worldMax = tilemap.CellToWorld(cell + Vector3Int.one);
        Bounds cellBounds = new();
        cellBounds.SetMinMax(
            new Vector3(Mathf.Min(worldMin.x, worldMax.x), Mathf.Min(worldMin.y, worldMax.y), colliderBounds.min.z),
            new Vector3(Mathf.Max(worldMin.x, worldMax.x), Mathf.Max(worldMin.y, worldMax.y), colliderBounds.max.z));
        return cellBounds.Intersects(colliderBounds);
    }

    private float ResolveDepthScale()
    {
        return Mathf.Approximately(transform.localScale.z, 0f) ? 1f : transform.localScale.z;
    }
}

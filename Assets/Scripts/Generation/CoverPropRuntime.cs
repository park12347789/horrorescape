/*
 * File Role:
 * Turns generated cover prop metadata into colliders, sprites, and shadow casters.
 *
 * Runtime Use:
 * Used for planters and crate stacks that block movement, sight, and flashlight light.
 *
 * Study Notes:
 * Read this with GeneratedFloorLayout and GridMapService to understand cover object spawning.
 */

using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CoverPropRuntime : MonoBehaviour
{
    private static Sprite sharedSprite;

    [SerializeField] private CoverPropType propType;
    [SerializeField] private GeneratedZoneType zoneType;
    [SerializeField] private Vector2Int footprint = Vector2Int.one;
    [SerializeField] private Vector3Int originCell;
    [SerializeField] private Vector3Int[] occupiedCells = Array.Empty<Vector3Int>();

    private BoxCollider2D boxCollider;

    public CoverPropType PropType => propType;
    public GeneratedZoneType ZoneType => zoneType;
    public Vector2Int Footprint => footprint;
    public Vector3Int OriginCell => originCell;
    public Vector3Int[] OccupiedCells => occupiedCells ?? Array.Empty<Vector3Int>();

    public void Configure(CoverPropPlacement placement, Vector3 worldCenter, int layer, string sortingLayerName, int sortingOrder)
    {
        propType = placement.PropType;
        zoneType = placement.ZoneType;
        footprint = placement.Size;
        originCell = placement.OriginCell;
        occupiedCells = placement.OccupiedCells ?? Array.Empty<Vector3Int>();

        gameObject.layer = layer;
        transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.size = new Vector2(footprint.x, footprint.y);
        boxCollider.offset = Vector2.zero;
        boxCollider.isTrigger = false;

        BuildVisuals(sortingLayerName, sortingOrder);
        RuntimeShadowCaster2DConfigurator.TryConfigureFromCollider(gameObject, boxCollider, out _);
    }

    private void BuildVisuals(string sortingLayerName, int sortingOrder)
    {
        EnsureSharedSprite();
        ClearGeneratedVisuals();

        switch (propType)
        {
            case CoverPropType.Planter:
                BuildPlanterVisuals(sortingLayerName, sortingOrder);
                break;
            default:
                BuildCrateVisuals(sortingLayerName, sortingOrder);
                break;
        }
    }

    private void BuildPlanterVisuals(string sortingLayerName, int sortingOrder)
    {
        float clampedWidth = Mathf.Max(0.72f, footprint.x * 0.78f);
        float clampedHeight = Mathf.Max(0.62f, footprint.y * 0.78f);

        CreateVisualPart(
            "Pot",
            new Color(0.36f, 0.39f, 0.42f, 1f),
            new Vector2(clampedWidth, Mathf.Max(0.32f, footprint.y * 0.36f)),
            new Vector3(0f, -(Mathf.Max(0.1f, footprint.y * 0.18f)), 0f),
            sortingLayerName,
            sortingOrder);

        CreateVisualPart(
            "Leaves",
            new Color(0.34f, 0.53f, 0.35f, 1f),
            new Vector2(Mathf.Max(0.9f, footprint.x * 0.96f), clampedHeight),
            new Vector3(0f, Mathf.Min(0.18f, footprint.y * 0.12f), 0f),
            sortingLayerName,
            sortingOrder + 1);

        CreateVisualPart(
            "LeavesHighlight",
            new Color(0.55f, 0.74f, 0.52f, 0.95f),
            new Vector2(Mathf.Max(0.55f, footprint.x * 0.55f), Mathf.Max(0.28f, footprint.y * 0.28f)),
            new Vector3(0f, Mathf.Min(0.22f, footprint.y * 0.2f), 0f),
            sortingLayerName,
            sortingOrder + 2);
    }

    private void BuildCrateVisuals(string sortingLayerName, int sortingOrder)
    {
        CreateVisualPart(
            "CrateBody",
            new Color(0.47f, 0.31f, 0.16f, 1f),
            new Vector2(Mathf.Max(0.8f, footprint.x * 0.92f), Mathf.Max(0.8f, footprint.y * 0.92f)),
            Vector3.zero,
            sortingLayerName,
            sortingOrder);

        CreateVisualPart(
            "CrateTop",
            new Color(0.67f, 0.46f, 0.24f, 0.92f),
            new Vector2(Mathf.Max(0.55f, footprint.x * 0.7f), Mathf.Max(0.16f, footprint.y * 0.2f)),
            new Vector3(0f, Mathf.Min(0.26f, footprint.y * 0.22f), 0f),
            sortingLayerName,
            sortingOrder + 1);

        CreateVisualPart(
            "CrateBand",
            new Color(0.28f, 0.18f, 0.1f, 1f),
            new Vector2(Mathf.Max(0.1f, footprint.x * 0.14f), Mathf.Max(0.72f, footprint.y * 0.84f)),
            Vector3.zero,
            sortingLayerName,
            sortingOrder + 2);
    }

    private void CreateVisualPart(string partName, Color color, Vector2 size, Vector3 localPosition, string sortingLayerName, int sortingOrder)
    {
        GameObject visualPart = new(partName);
        visualPart.transform.SetParent(transform, false);
        visualPart.transform.localPosition = localPosition;
        visualPart.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = visualPart.AddComponent<SpriteRenderer>();
        renderer.sprite = sharedSprite;
        renderer.color = color;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
    }

    private void ClearGeneratedVisuals()
    {
        for (int childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = transform.GetChild(childIndex);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureSharedSprite()
    {
        if (sharedSprite != null)
        {
            return;
        }

        Texture2D texture = Texture2D.whiteTexture;
        sharedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
    }
}


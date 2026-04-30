/*
 * File Role:
 * Provides cell-based queries for movement, doors, cover props, and vision blocking.
 *
 * Runtime Use:
 * Acts as the common interface between tilemaps and gameplay scripts.
 *
 * Study Notes:
 * Read this early because many other systems depend on these helper methods.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class GridMapService : MonoBehaviour
{
    private const float LineOfSightBoundaryEpsilon = 0.0001f;

    // The rest of the project talks to the world through this service instead of touching
    // tilemaps directly. That keeps movement, sight, and door logic consistent.
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap doorTilemap;
    [SerializeField] private LayerMask visionBlockingLayers;
    private readonly Dictionary<Vector3Int, TileBase> registeredDoorTiles = new();
    private readonly Dictionary<Vector3Int, GameObject> doorShadowBlockers = new();
    private readonly HashSet<Vector3Int> registeredPropCells = new();
    private readonly HashSet<Vector3Int> registeredMovementOnlyCells = new();

    public Grid Grid => grid;
    public Tilemap GroundTilemap => groundTilemap;
    public Tilemap WallTilemap => wallTilemap;
    public Tilemap DoorTilemap => doorTilemap;
    public LayerMask VisionBlockingLayers => visionBlockingLayers;

    public void Initialize(Grid sourceGrid, Tilemap sourceGround, Tilemap sourceWall, Tilemap sourceDoor, LayerMask sourceVisionBlockingLayers)
    {
        // Re-initialize whenever a new runtime-generated map is built.
        grid = sourceGrid;
        groundTilemap = sourceGround;
        wallTilemap = sourceWall;
        doorTilemap = sourceDoor;
        visionBlockingLayers = sourceVisionBlockingLayers;
        registeredPropCells.Clear();
        registeredMovementOnlyCells.Clear();
        CacheDoorTiles();
    }

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return groundTilemap != null ? MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, worldPosition) : Vector3Int.zero;
    }

    public bool TryResolveNearestWalkableCell(Vector3 worldPosition, out Vector3Int resolvedCell, int maxRadius = 0, bool allowClosedDoors = false)
    {
        return TryResolveNearestWalkableCell(WorldToCell(worldPosition), out resolvedCell, maxRadius, allowClosedDoors);
    }

    public bool TryResolveNearestWalkableCell(Vector3Int preferredCell, out Vector3Int resolvedCell, int maxRadius = 0, bool allowClosedDoors = false)
    {
        resolvedCell = preferredCell;

        if (groundTilemap == null)
        {
            return false;
        }

        if (IsWalkable(preferredCell, allowClosedDoors))
        {
            return true;
        }

        for (int radius = 1; radius <= Mathf.Max(0, maxRadius); radius++)
        {
            for (int y = radius; y >= -radius; y--)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector3Int candidate = preferredCell + new Vector3Int(x, y, 0);

                    if (IsWalkable(candidate, allowClosedDoors))
                    {
                        resolvedCell = candidate;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public Vector3Int ResolveNearestWalkableCell(Vector3 worldPosition, int maxRadius = 0, bool allowClosedDoors = false)
    {
        Vector3Int preferredCell = WorldToCell(worldPosition);
        return ResolveNearestWalkableCell(preferredCell, maxRadius, allowClosedDoors);
    }

    public Vector3Int ResolveNearestWalkableCell(Vector3Int preferredCell, int maxRadius = 0, bool allowClosedDoors = false)
    {
        return TryResolveNearestWalkableCell(preferredCell, out Vector3Int resolvedCell, maxRadius, allowClosedDoors)
            ? resolvedCell
            : preferredCell;
    }

    public Vector3 CellToWorldCenter(Vector3Int cellPosition)
    {
        if (groundTilemap != null)
        {
            return groundTilemap.GetCellCenterWorld(cellPosition);
        }

        return grid != null ? grid.GetCellCenterWorld(cellPosition) : Vector3.zero;
    }

    public bool IsWalkable(Vector3Int cellPosition)
    {
        return IsWalkable(cellPosition, false);
    }

    public bool IsWalkable(Vector3Int cellPosition, bool allowClosedDoors)
    {
        // "Walkable" means there is floor and there are no currently blocking systems
        // occupying the cell: walls, closed doors, or registered props.
        bool hasGround = groundTilemap != null && groundTilemap.HasTile(cellPosition);
        bool blockedByWall = wallTilemap != null && wallTilemap.HasTile(cellPosition);
        bool blockedByDoor = !allowClosedDoors && IsDoorClosed(cellPosition);
        bool blockedByProp = registeredPropCells.Contains(cellPosition);
        bool blockedByMovementOnly = registeredMovementOnlyCells.Contains(cellPosition);
        return hasGround && !blockedByWall && !blockedByDoor && !blockedByProp && !blockedByMovementOnly;
    }

    public bool IsWalkable(Vector3 worldPosition)
    {
        return IsWalkable(WorldToCell(worldPosition));
    }

    public bool BlocksVision(Vector3Int cellPosition)
    {
        return BlocksVision(cellPosition, ignoreClosedDoors: false);
    }

    public bool BlocksVision(Vector3Int cellPosition, bool ignoreClosedDoors)
    {
        // Vision should match the authored/runtime blocker contract:
        // walls and closed doors stop light, and BlockAll-style registered prop
        // cells do too. Movement-only blockers remain visually/light-open.
        return (wallTilemap != null && wallTilemap.HasTile(cellPosition))
            || (!ignoreClosedDoors && IsDoorClosed(cellPosition))
            || registeredPropCells.Contains(cellPosition);
    }

    public bool BlocksVision(Vector3 worldPosition)
    {
        return BlocksVision(worldPosition, ignoreClosedDoors: false);
    }

    public bool BlocksVision(Vector3 worldPosition, bool ignoreClosedDoors)
    {
        return BlocksVision(WorldToCell(worldPosition), ignoreClosedDoors);
    }

    public bool HasLineOfSight(Vector2 startWorld, Vector2 endWorld, bool ignoreClosedDoors = false)
    {
        if (groundTilemap == null)
        {
            return true;
        }

        Vector2 startCellPosition = WorldToInterpolatedCellPosition(startWorld);
        Vector2 endCellPosition = WorldToInterpolatedCellPosition(endWorld);
        Vector2 delta = endCellPosition - startCellPosition;
        float distance = delta.magnitude;

        if (distance <= 0.0001f)
        {
            return true;
        }

        Vector3Int currentCell = FloorToCell(startCellPosition);
        Vector3Int targetCell = FloorToCell(endCellPosition);

        if (currentCell == targetCell)
        {
            return true;
        }

        int stepX = ResolveTraversalStep(delta.x);
        int stepY = ResolveTraversalStep(delta.y);
        float tMaxX = ResolveInitialBoundaryCrossingTime(startCellPosition.x, currentCell.x, stepX, delta.x);
        float tMaxY = ResolveInitialBoundaryCrossingTime(startCellPosition.y, currentCell.y, stepY, delta.y);
        float tDeltaX = ResolveBoundaryStepTime(delta.x, stepX);
        float tDeltaY = ResolveBoundaryStepTime(delta.y, stepY);

        while (currentCell != targetCell)
        {
            if (tMaxX + LineOfSightBoundaryEpsilon < tMaxY)
            {
                currentCell.x += stepX;

                if (BlocksVision(currentCell, ignoreClosedDoors))
                {
                    return false;
                }

                tMaxX += tDeltaX;
                continue;
            }

            if (tMaxY + LineOfSightBoundaryEpsilon < tMaxX)
            {
                currentCell.y += stepY;

                if (BlocksVision(currentCell, ignoreClosedDoors))
                {
                    return false;
                }

                tMaxY += tDeltaY;
                continue;
            }

            if (stepX != 0)
            {
                Vector3Int xNeighbor = currentCell + new Vector3Int(stepX, 0, 0);

                if (BlocksVision(xNeighbor, ignoreClosedDoors))
                {
                    return false;
                }
            }

            if (stepY != 0)
            {
                Vector3Int yNeighbor = currentCell + new Vector3Int(0, stepY, 0);

                if (BlocksVision(yNeighbor, ignoreClosedDoors))
                {
                    return false;
                }
            }

            currentCell += new Vector3Int(stepX, stepY, 0);

            if (BlocksVision(currentCell, ignoreClosedDoors))
            {
                return false;
            }

            tMaxX += tDeltaX;
            tMaxY += tDeltaY;
        }

        return true;
    }

    public void RegisterDoorShadowBlocker(Vector3Int cellPosition, GameObject blocker)
    {
        if (blocker == null)
        {
            return;
        }

        doorShadowBlockers[cellPosition] = blocker;
        blocker.SetActive(IsDoorClosed(cellPosition));
    }

    public void SetDoorShadowBlockerActive(Vector3Int cellPosition, bool active)
    {
        if (doorShadowBlockers.TryGetValue(cellPosition, out GameObject blocker) && blocker != null)
        {
            blocker.SetActive(active);
        }
    }

    public void ClearRegisteredProps()
    {
        registeredPropCells.Clear();
        registeredMovementOnlyCells.Clear();
    }

    public void RegisterPropCell(Vector3Int cellPosition)
    {
        registeredPropCells.Add(cellPosition);
    }

    public void RegisterPropCells(IEnumerable<Vector3Int> cellPositions)
    {
        if (cellPositions == null)
        {
            return;
        }

        foreach (Vector3Int cellPosition in cellPositions)
        {
            registeredPropCells.Add(cellPosition);
        }
    }

    public void RegisterMovementBlockingCell(Vector3Int cellPosition)
    {
        registeredMovementOnlyCells.Add(cellPosition);
    }

    public void RegisterMovementBlockingCells(IEnumerable<Vector3Int> cellPositions)
    {
        if (cellPositions == null)
        {
            return;
        }

        foreach (Vector3Int cellPosition in cellPositions)
        {
            registeredMovementOnlyCells.Add(cellPosition);
        }
    }

    public bool HasBlockingProp(Vector3Int cellPosition)
    {
        return registeredPropCells.Contains(cellPosition) || registeredMovementOnlyCells.Contains(cellPosition);
    }

    public bool HasDoor(Vector3Int cellPosition)
    {
        return MainEscapeRuntimeDoorRegistry.TryGetAtCell(cellPosition, out _)
            || registeredDoorTiles.ContainsKey(cellPosition);
    }

    public bool IsDoorOpen(Vector3Int cellPosition)
    {
        if (MainEscapeRuntimeDoorRegistry.TryGetAtCell(cellPosition, out IMainEscapeRuntimeDoor runtimeDoor))
        {
            return runtimeDoor.IsOpen;
        }

        return registeredDoorTiles.ContainsKey(cellPosition)
            && (doorTilemap == null || !doorTilemap.HasTile(cellPosition));
    }

    private Vector2 WorldToInterpolatedCellPosition(Vector2 worldPosition)
    {
        GridLayout layout = groundTilemap != null ? groundTilemap.layoutGrid : grid;

        if (layout == null)
        {
            return worldPosition;
        }

        Vector3 worldPoint = new(worldPosition.x, worldPosition.y, groundTilemap.transform.position.z);
        Vector3 localPoint = layout.WorldToLocal(worldPoint);
        Vector3 cellPoint = layout.LocalToCellInterpolated(localPoint);
        return new Vector2(cellPoint.x, cellPoint.y);
    }

    private static Vector3Int FloorToCell(Vector2 cellPosition)
    {
        return new Vector3Int(
            Mathf.FloorToInt(cellPosition.x),
            Mathf.FloorToInt(cellPosition.y),
            0);
    }

    private static int ResolveTraversalStep(float axisDelta)
    {
        if (axisDelta > LineOfSightBoundaryEpsilon)
        {
            return 1;
        }

        if (axisDelta < -LineOfSightBoundaryEpsilon)
        {
            return -1;
        }

        return 0;
    }

    private static float ResolveInitialBoundaryCrossingTime(float startAxis, int currentCellAxis, int step, float axisDelta)
    {
        if (step == 0)
        {
            return float.PositiveInfinity;
        }

        float nextBoundary = step > 0 ? currentCellAxis + 1f : currentCellAxis;
        return Mathf.Max(0f, (nextBoundary - startAxis) / axisDelta);
    }

    private static float ResolveBoundaryStepTime(float axisDelta, int step)
    {
        return step == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(axisDelta);
    }

    public bool IsDoorClosed(Vector3Int cellPosition)
    {
        if (MainEscapeRuntimeDoorRegistry.TryGetAtCell(cellPosition, out IMainEscapeRuntimeDoor runtimeDoor))
        {
            return !runtimeDoor.IsOpen;
        }

        return registeredDoorTiles.ContainsKey(cellPosition)
            && doorTilemap != null
            && doorTilemap.HasTile(cellPosition);
    }

    public bool OpenDoor(Vector3Int cellPosition)
    {
        // Opening a door removes both gameplay blocking and its paired visual shadow
        // blocker so the map state stays visually consistent.
        if (MainEscapeRuntimeDoorRegistry.TryGetAtCell(cellPosition, out IMainEscapeRuntimeDoor runtimeDoor))
        {
            return runtimeDoor.SetOpenState(true);
        }

        if (doorTilemap == null || !HasDoor(cellPosition))
        {
            return false;
        }

        if (!doorTilemap.HasTile(cellPosition))
        {
            return true;
        }

        doorTilemap.SetTile(cellPosition, null);

        TilemapCollider2D tilemapCollider = doorTilemap.GetComponent<TilemapCollider2D>();
        tilemapCollider?.ProcessTilemapChanges();

        if (doorShadowBlockers.TryGetValue(cellPosition, out GameObject blocker) && blocker != null)
        {
            blocker.SetActive(false);
        }

        return true;
    }

    public bool CloseDoor(Vector3Int cellPosition)
    {
        // Closing restores the original cached tile rather than creating a new tile, so
        // runtime-generated doors reopen with the exact same asset they started with.
        if (MainEscapeRuntimeDoorRegistry.TryGetAtCell(cellPosition, out IMainEscapeRuntimeDoor runtimeDoor))
        {
            return runtimeDoor.SetOpenState(false);
        }

        if (doorTilemap == null || !registeredDoorTiles.TryGetValue(cellPosition, out TileBase doorTile))
        {
            return false;
        }

        if (doorTilemap.HasTile(cellPosition))
        {
            return true;
        }

        doorTilemap.SetTile(cellPosition, doorTile);

        TilemapCollider2D tilemapCollider = doorTilemap.GetComponent<TilemapCollider2D>();
        tilemapCollider?.ProcessTilemapChanges();

        if (doorShadowBlockers.TryGetValue(cellPosition, out GameObject blocker) && blocker != null)
        {
            blocker.SetActive(true);
        }

        return true;
    }

    public bool SuppressDoorTile(Vector3Int cellPosition)
    {
        if (doorTilemap == null || !registeredDoorTiles.ContainsKey(cellPosition))
        {
            return false;
        }

        if (!doorTilemap.HasTile(cellPosition))
        {
            return true;
        }

        doorTilemap.SetTile(cellPosition, null);
        doorTilemap.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        return true;
    }

    public bool RestoreDoorTile(Vector3Int cellPosition)
    {
        if (doorTilemap == null || !registeredDoorTiles.TryGetValue(cellPosition, out TileBase doorTile))
        {
            return false;
        }

        if (doorTilemap.GetTile(cellPosition) == doorTile)
        {
            return true;
        }

        doorTilemap.SetTile(cellPosition, doorTile);
        doorTilemap.GetComponent<TilemapCollider2D>()?.ProcessTilemapChanges();
        return true;
    }

    private void CacheDoorTiles()
    {
        // The service remembers every original door tile so it can later reopen/close
        // doors even after the tilemap cell has been cleared.
        registeredDoorTiles.Clear();

        if (doorTilemap == null)
        {
            return;
        }

        BoundsInt bounds = doorTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            TileBase tile = doorTilemap.GetTile(cellPosition);

            if (tile != null)
            {
                registeredDoorTiles[cellPosition] = tile;
            }
        }
    }
}


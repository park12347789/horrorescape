using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class MainEscapeVisualAuthoringSynthesis
{
    private const string VisualPropsPath = "Visual/Props";
    private const string VisualDoorsPath = "Visual/Props/Doors";
    private const string VisualSideDoorsPath = "Visual/Props/sidedoor";
    private static string VisualMoveOnlyContainerName => MainEscapeRuntimeSettings.Load().MoveOnlyOverlayRootName;

    public static Transform ResolveVisualPropsRoot(Transform floorRoot)
    {
        return floorRoot != null ? floorRoot.Find(VisualPropsPath) : null;
    }

    public static Transform ResolveVisualDoorsRoot(Transform floorRoot)
    {
        return floorRoot != null ? floorRoot.Find(VisualDoorsPath) : null;
    }

    private static List<Transform> ResolveVisualDoorRoots(Transform floorRoot)
    {
        List<Transform> roots = new();

        if (floorRoot == null)
        {
            return roots;
        }

        Transform doorsRoot = floorRoot.Find(VisualDoorsPath);
        if (doorsRoot != null)
        {
            roots.Add(doorsRoot);
        }

        Transform sideDoorsRoot = floorRoot.Find(VisualSideDoorsPath);
        if (sideDoorsRoot != null)
        {
            roots.Add(sideDoorsRoot);
        }

        return roots;
    }

    public static Vector3Int[] CollectVisualPropCells(Transform floorRoot, Tilemap groundTilemap)
    {
        Transform propsRoot = ResolveVisualPropsRoot(floorRoot);

        if (propsRoot == null || groundTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        HashSet<Vector3Int> occupiedCells = new();

        for (int index = 0; index < propsRoot.childCount; index++)
        {
            Transform propRoot = propsRoot.GetChild(index);

            if (IsMoveOnlyPropContainer(propRoot))
            {
                CollectMovementBlockingPropsFromContainer(propRoot, groundTilemap, occupiedCells);
                continue;
            }

            if (!ShouldTreatAsMovementBlockingProp(propRoot))
            {
                continue;
            }

            AddProjectedFootprintCells(propRoot, groundTilemap, occupiedCells, footprintDepthScale: 0.35f);
        }

        return HashSetToArray(occupiedCells);
    }

    public static Vector3Int[] CollectMovementBlockingPropCells(IEnumerable<Transform> propRoots, Tilemap groundTilemap)
    {
        if (propRoots == null || groundTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        HashSet<Vector3Int> occupiedCells = new();

        foreach (Transform propRoot in propRoots)
        {
            if (propRoot == null)
            {
                continue;
            }

            if (IsMoveOnlyPropContainer(propRoot))
            {
                CollectMovementBlockingPropsFromContainer(propRoot, groundTilemap, occupiedCells);
                continue;
            }

            if (!ShouldTreatAsMovementBlockingProp(propRoot))
            {
                continue;
            }

            AddProjectedFootprintCells(propRoot, groundTilemap, occupiedCells, footprintDepthScale: 0.35f);
        }

        return HashSetToArray(occupiedCells);
    }

    public static Vector3Int[] CollectDoorCellsForVisualRoot(Transform doorRoot, Tilemap groundTilemap)
    {
        return doorRoot != null && groundTilemap != null
            ? CollectProjectedFootprintCells(doorRoot, groundTilemap, footprintDepthScale: 0.2f)
            : Array.Empty<Vector3Int>();
    }

    public static GeneratedDoorGroupData[] BuildVisualDoorGroups(Transform floorRoot, Tilemap groundTilemap)
    {
        List<Transform> doorRoots = ResolveVisualDoorRoots(floorRoot);

        if (doorRoots.Count == 0 || groundTilemap == null)
        {
            return Array.Empty<GeneratedDoorGroupData>();
        }

        List<GeneratedDoorGroupData> groups = new();
        HashSet<Vector3Int> seenDoorCells = new();

        for (int rootIndex = 0; rootIndex < doorRoots.Count; rootIndex++)
        {
            Transform doorsRoot = doorRoots[rootIndex];

            if (doorsRoot == null)
            {
                continue;
            }

            for (int index = 0; index < doorsRoot.childCount; index++)
            {
                Transform doorRoot = doorsRoot.GetChild(index);

                if (doorRoot == null || !doorRoot.gameObject.activeInHierarchy)
                {
                    continue;
                }

            Vector3Int[] rawCells = CollectProjectedFootprintCells(doorRoot, groundTilemap, footprintDepthScale: 0.2f);

            if (rawCells.Length == 0)
            {
                continue;
            }

            List<Vector3Int> uniqueCells = new(rawCells.Length);

            for (int cellIndex = 0; cellIndex < rawCells.Length; cellIndex++)
            {
                Vector3Int cell = rawCells[cellIndex];

                if (seenDoorCells.Add(cell))
                {
                    uniqueCells.Add(cell);
                }
            }

            if (uniqueCells.Count == 0)
            {
                continue;
            }

                groups.Add(new GeneratedDoorGroupData(
                    groups.Count,
                    configuredRequiresKey: false,
                    configuredCells: uniqueCells.ToArray(),
                    configuredConnectedRoomIds: Array.Empty<int>()));
            }
        }

        return groups.Count == 0 ? Array.Empty<GeneratedDoorGroupData>() : groups.ToArray();
    }

    public static Transform[] FindVisualDoorRootsForCells(Transform floorRoot, Tilemap groundTilemap, Vector3Int[] targetCells)
    {
        List<Transform> doorRoots = ResolveVisualDoorRoots(floorRoot);

        if (doorRoots.Count == 0 || groundTilemap == null || targetCells == null || targetCells.Length == 0)
        {
            return Array.Empty<Transform>();
        }

        HashSet<Vector3Int> targetCellSet = new(targetCells);
        List<(Transform Root, Vector3Int[] Cells)> exactMatches = new();
        List<(Transform Root, Vector3Int[] Cells)> subsetMatches = new();
        List<Transform> bestOverlapMatches = new();
        int bestOverlapCount = 0;
        int bestExtraCellCount = int.MaxValue;
        float bestCenterDistanceSqr = float.MaxValue;
        Vector3 targetCenter = Vector3.zero;

        for (int index = 0; index < targetCells.Length; index++)
        {
            targetCenter += groundTilemap.GetCellCenterWorld(targetCells[index]);
        }

        targetCenter /= targetCells.Length;

        for (int rootIndex = 0; rootIndex < doorRoots.Count; rootIndex++)
        {
            Transform doorsRoot = doorRoots[rootIndex];

            if (doorsRoot == null)
            {
                continue;
            }

            for (int index = 0; index < doorsRoot.childCount; index++)
            {
                Transform doorRoot = doorsRoot.GetChild(index);

                if (doorRoot == null || !doorRoot.gameObject.activeInHierarchy)
                {
                    continue;
                }

            Vector3Int[] projectedCells = CollectProjectedFootprintCells(doorRoot, groundTilemap, footprintDepthScale: 0.2f);
            int overlapCount = 0;
            int extraCellCount = 0;

            for (int cellIndex = 0; cellIndex < projectedCells.Length; cellIndex++)
            {
                if (targetCellSet.Contains(projectedCells[cellIndex]))
                {
                    overlapCount++;
                }
                else
                {
                    extraCellCount++;
                }
            }

                if (overlapCount == 0)
                {
                    continue;
                }

                if (extraCellCount == 0 && projectedCells.Length == targetCellSet.Count)
                {
                    exactMatches.Add((doorRoot, projectedCells));
                    continue;
                }

                if (extraCellCount == 0)
                {
                    subsetMatches.Add((doorRoot, projectedCells));
                    continue;
                }

            float centerDistanceSqr = (doorRoot.position - targetCenter).sqrMagnitude;
            bool betterOverlap = overlapCount > bestOverlapCount;
            bool sameOverlapFewerExtras = overlapCount == bestOverlapCount && extraCellCount < bestExtraCellCount;
            bool sameOverlapSameExtrasCloser = overlapCount == bestOverlapCount
                && extraCellCount == bestExtraCellCount
                && centerDistanceSqr < bestCenterDistanceSqr;

                if (betterOverlap || sameOverlapFewerExtras || sameOverlapSameExtrasCloser)
                {
                    bestOverlapMatches.Clear();
                    bestOverlapMatches.Add(doorRoot);
                    bestOverlapCount = overlapCount;
                    bestExtraCellCount = extraCellCount;
                    bestCenterDistanceSqr = centerDistanceSqr;
                }
                else if (overlapCount == bestOverlapCount
                         && extraCellCount == bestExtraCellCount
                         && Mathf.Approximately(centerDistanceSqr, bestCenterDistanceSqr))
                {
                    bestOverlapMatches.Add(doorRoot);
                }
            }
        }

        if (exactMatches.Count > 0)
        {
            Transform[] matches = new Transform[exactMatches.Count];

            for (int index = 0; index < exactMatches.Count; index++)
            {
                matches[index] = exactMatches[index].Root;
            }

            return matches;
        }

        if (subsetMatches.Count > 0)
        {
            HashSet<Vector3Int> coveredCells = new();

            for (int index = 0; index < subsetMatches.Count; index++)
            {
                Vector3Int[] projectedCells = subsetMatches[index].Cells;

                for (int cellIndex = 0; cellIndex < projectedCells.Length; cellIndex++)
                {
                    coveredCells.Add(projectedCells[cellIndex]);
                }
            }

            if (coveredCells.SetEquals(targetCellSet))
            {
                Transform[] matches = new Transform[subsetMatches.Count];

                for (int index = 0; index < subsetMatches.Count; index++)
                {
                    matches[index] = subsetMatches[index].Root;
                }

                return matches;
            }
        }

        return bestOverlapMatches.Count == 0 ? Array.Empty<Transform>() : bestOverlapMatches.ToArray();
    }

    private static void CollectMovementBlockingPropsFromContainer(
        Transform containerRoot,
        Tilemap groundTilemap,
        HashSet<Vector3Int> occupiedCells)
    {
        if (containerRoot == null || groundTilemap == null || occupiedCells == null)
        {
            return;
        }

        for (int index = 0; index < containerRoot.childCount; index++)
        {
            Transform child = containerRoot.GetChild(index);

            if (ShouldSkipMovementBlockingTraversal(child))
            {
                continue;
            }

            if (ShouldTreatAsMovementBlockingProp(child))
            {
                AddProjectedFootprintCells(child, groundTilemap, occupiedCells, footprintDepthScale: 0.35f);
                continue;
            }

            CollectMovementBlockingPropsFromContainer(child, groundTilemap, occupiedCells);
        }
    }

    private static bool ShouldTreatAsMovementBlockingProp(Transform propRoot)
    {
        if (ShouldSkipMovementBlockingTraversal(propRoot) || IsMoveOnlyPropContainer(propRoot))
        {
            return false;
        }

        return HasSpriteRenderer(propRoot);
    }

    private static bool ShouldSkipMovementBlockingTraversal(Transform propRoot)
    {
        if (propRoot == null || !propRoot.gameObject.activeInHierarchy)
        {
            return true;
        }

        if (string.Equals(propRoot.name, "Doors", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(propRoot.name, "sidedoor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (propRoot.name.IndexOf("dropitem", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private static bool IsMoveOnlyPropContainer(Transform propRoot)
    {
        return propRoot != null
            && string.Equals(propRoot.name, VisualMoveOnlyContainerName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSpriteRenderer(Transform root)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            if (IsRenderable(renderers[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3Int[] CollectProjectedFootprintCells(Transform root, Tilemap groundTilemap, float footprintDepthScale)
    {
        HashSet<Vector3Int> occupiedCells = new();
        AddProjectedFootprintCells(root, groundTilemap, occupiedCells, footprintDepthScale);
        return HashSetToArray(occupiedCells);
    }

    private static void AddProjectedFootprintCells(
        Transform root,
        Tilemap groundTilemap,
        HashSet<Vector3Int> occupiedCells,
        float footprintDepthScale)
    {
        if (root == null || groundTilemap == null || occupiedCells == null)
        {
            return;
        }

        if (TryAddAuthoringFootprint(root, groundTilemap, occupiedCells))
        {
            return;
        }

        if (!TryGetRenderableBounds(root, out Bounds combinedBounds))
        {
            TryAddNearestGroundCell(root.position, groundTilemap, occupiedCells);
            return;
        }

        Vector2 cellSize = GetCellSize(groundTilemap);

        Vector3 anchorWorldPosition = new(
            combinedBounds.center.x,
            combinedBounds.min.y + cellSize.y * 0.5f,
            groundTilemap.transform.position.z);

        if (!TryResolveNearestGroundCell(anchorWorldPosition, groundTilemap, out Vector3Int anchorCell))
        {
            return;
        }

        bool doorLikeFootprint = footprintDepthScale <= 0.25f;
        int widthCells;
        int heightCells;

        if (doorLikeFootprint)
        {
            bool horizontalDoor = combinedBounds.size.x >= combinedBounds.size.y;
            widthCells = horizontalDoor
                ? Mathf.Clamp(Mathf.RoundToInt(combinedBounds.size.x / Mathf.Max(0.01f, cellSize.x)), 1, 2)
                : 1;
            heightCells = horizontalDoor
                ? 1
                : Mathf.Clamp(Mathf.RoundToInt(combinedBounds.size.y / Mathf.Max(0.01f, cellSize.y)), 1, 2);

            // Front doors are authored as double-width gate visuals, but side doors should
            // stay on their measured bounds so narrow doors do not over-block movement or light.
            if (IsFrontDoorRoot(root))
            {
                horizontalDoor = true;
                widthCells = Mathf.Max(widthCells, 2);
                heightCells = 1;
            }

        }
        else
        {
            float projectedDepth = Mathf.Max(cellSize.y * 1.05f, combinedBounds.size.y * Mathf.Clamp01(footprintDepthScale));
            widthCells = Mathf.Clamp(Mathf.RoundToInt(combinedBounds.size.x / Mathf.Max(0.01f, cellSize.x)), 1, 4);
            heightCells = Mathf.Clamp(Mathf.RoundToInt(projectedDepth / Mathf.Max(0.01f, cellSize.y)), 1, 2);
        }

        int startX = anchorCell.x - Mathf.FloorToInt((widthCells - 1) * 0.5f);
        int startY = anchorCell.y - Mathf.FloorToInt((heightCells - 1) * 0.5f);

        bool allowMissingGround = doorLikeFootprint && (IsFrontDoorRoot(root) || IsSideDoorRoot(root));

        for (int y = 0; y < heightCells; y++)
        {
            for (int x = 0; x < widthCells; x++)
            {
                Vector3Int cell = new(startX + x, startY + y, 0);

                if (allowMissingGround || groundTilemap.HasTile(cell))
                {
                    occupiedCells.Add(cell);
                }
            }
        }
    }

    private static bool IsSideDoorRoot(Transform root)
    {
        return MainEscapeDoorVisualVariantResolver.IsSideDoorRoot(root);
    }

    private static bool IsFrontDoorRoot(Transform root)
    {
        return MainEscapeDoorVisualVariantResolver.IsFrontDoorRoot(root);
    }

    private static bool TryGetRenderableBounds(Transform root, out Bounds combinedBounds)
    {
        combinedBounds = default;
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool foundBounds = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (!IsRenderable(renderer))
            {
                continue;
            }

            if (!foundBounds)
            {
                combinedBounds = renderer.bounds;
                foundBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return foundBounds;
    }

    private static bool TryAddAuthoringFootprint(
        Transform root,
        Tilemap groundTilemap,
        HashSet<Vector3Int> occupiedCells)
    {
        if (TryAddMovementBlockerAuthoringFootprint(root, groundTilemap, occupiedCells))
        {
            return true;
        }

        return TryAddPropBlockerAuthoringFootprint(root, groundTilemap, occupiedCells);
    }

    private static bool TryAddMovementBlockerAuthoringFootprint(
        Transform root,
        Tilemap groundTilemap,
        HashSet<Vector3Int> occupiedCells)
    {
        MainEscapeMovementBlockerAuthoring marker = root.GetComponentInChildren<MainEscapeMovementBlockerAuthoring>(true);

        if (marker == null)
        {
            return false;
        }

        return TryAddOccupiedCells(marker.GetOccupiedCells(groundTilemap), groundTilemap, occupiedCells);
    }

    private static bool TryAddPropBlockerAuthoringFootprint(
        Transform root,
        Tilemap groundTilemap,
        HashSet<Vector3Int> occupiedCells)
    {
        MainEscapePropBlockerAuthoring marker = root.GetComponentInChildren<MainEscapePropBlockerAuthoring>(true);

        if (marker == null)
        {
            return false;
        }

        return TryAddOccupiedCells(marker.GetOccupiedCells(groundTilemap), groundTilemap, occupiedCells);
    }

    private static bool TryAddOccupiedCells(
        Vector3Int[] cells,
        Tilemap groundTilemap,
        HashSet<Vector3Int> occupiedCells)
    {
        if (groundTilemap == null || occupiedCells == null)
        {
            return false;
        }

        if (cells == null || cells.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < cells.Length; index++)
        {
            Vector3Int cell = cells[index];

            if (groundTilemap.HasTile(cell))
            {
                occupiedCells.Add(cell);
            }
        }

        return occupiedCells.Count > 0;
    }

    private static bool IsRenderable(SpriteRenderer renderer)
    {
        return renderer != null
            && renderer.gameObject.activeInHierarchy
            && renderer.enabled
            && renderer.sprite != null
            && renderer.color.a > 0.01f;
    }

    private static void TryAddNearestGroundCell(Vector3 worldPosition, Tilemap groundTilemap, HashSet<Vector3Int> occupiedCells)
    {
        if (TryResolveNearestGroundCell(worldPosition, groundTilemap, out Vector3Int cell))
        {
            occupiedCells.Add(cell);
        }
    }

    private static bool TryResolveNearestGroundCell(Vector3 worldPosition, Tilemap groundTilemap, out Vector3Int resolvedCell)
    {
        if (groundTilemap == null)
        {
            resolvedCell = Vector3Int.zero;
            return false;
        }

        Vector3Int anchorCell = MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, worldPosition);

        for (int radius = 0; radius <= 2; radius++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (radius > 0 && Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector3Int candidate = anchorCell + new Vector3Int(x, y, 0);

                    if (groundTilemap.HasTile(candidate))
                    {
                        resolvedCell = candidate;
                        return true;
                    }
                }
            }
        }

        resolvedCell = Vector3Int.zero;
        return false;
    }

    private static Vector2 GetCellSize(Tilemap groundTilemap)
    {
        if (groundTilemap == null || groundTilemap.layoutGrid == null)
        {
            return Vector2.one;
        }

        Vector3 cellSize = groundTilemap.layoutGrid.cellSize;
        return new Vector2(Mathf.Max(0.01f, Mathf.Abs(cellSize.x)), Mathf.Max(0.01f, Mathf.Abs(cellSize.y)));
    }

    private static Vector3Int[] HashSetToArray(HashSet<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return Array.Empty<Vector3Int>();
        }

        Vector3Int[] result = new Vector3Int[cells.Count];
        cells.CopyTo(result);
        Array.Sort(result, CompareCells);
        return result;
    }

    private static int CompareCells(Vector3Int left, Vector3Int right)
    {
        int yComparison = left.y.CompareTo(right.y);

        if (yComparison != 0)
        {
            return yComparison;
        }

        int xComparison = left.x.CompareTo(right.x);

        if (xComparison != 0)
        {
            return xComparison;
        }

        return left.z.CompareTo(right.z);
    }
}

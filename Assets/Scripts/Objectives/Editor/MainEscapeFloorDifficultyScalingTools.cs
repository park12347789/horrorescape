#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MainEscapeFloorDifficultyScalingTools
{
    private const string MenuPath = "Tools/Main Escape/Apply Floor Difficulty Scaling (1F-4F)";
    private static readonly Vector3Int[] SearchOffsets = BuildSearchOffsets();

    [MenuItem(MenuPath)]
    private static void ApplyFloorDifficultyScaling()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        List<string> updatedScenes = new();
        List<string> warnings = new();

        try
        {
            RFloorSceneEntry[] routes = settings.FloorSceneRoutes ?? Array.Empty<RFloorSceneEntry>();

            foreach (RFloorSceneEntry route in routes.OrderBy(route => route.floorNumber))
            {
                if (!TryGetScaling(route.floorNumber, out FloorDifficultyScaling scaling))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(route.scenePath) || !File.Exists(route.scenePath))
                {
                    warnings.Add($"{route.floorNumber}F scene path is missing: '{route.scenePath}'.");
                    continue;
                }

                if (ApplyScalingToScene(route.scenePath, settings, scaling, out string warning))
                {
                    updatedScenes.Add($"{route.floorNumber}F");
                }

                if (!string.IsNullOrWhiteSpace(warning))
                {
                    warnings.Add(warning);
                }
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
        }

        if (warnings.Count > 0)
        {
            Debug.LogWarning("[MainEscapeFloorDifficultyScalingTools] Completed with warnings.\n- " + string.Join("\n- ", warnings));
        }

        Debug.Log(
            updatedScenes.Count > 0
                ? "[MainEscapeFloorDifficultyScalingTools] Updated scenes: " + string.Join(", ", updatedScenes)
                : "[MainEscapeFloorDifficultyScalingTools] No authored floor scenes were updated.");
    }

    private static bool ApplyScalingToScene(
        string scenePath,
        MainEscapeRuntimeSettings settings,
        FloorDifficultyScaling scaling,
        out string warning)
    {
        warning = string.Empty;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        MainEscapeFloorAuthoring floorAuthoring = UnityEngine.Object.FindFirstObjectByType<MainEscapeFloorAuthoring>(FindObjectsInactive.Include);

        if (floorAuthoring == null)
        {
            warning = $"{scaling.FloorNumber}F is missing {nameof(MainEscapeFloorAuthoring)}.";
            return false;
        }

        floorAuthoring.CacheReferencesFromHierarchy();

        if (floorAuthoring.GroundTilemap == null)
        {
            warning = $"{scaling.FloorNumber}F is missing the ground tilemap needed to place shared enemy markers.";
            return false;
        }

        Transform enemyRoot = FindEnemyRoot(floorAuthoring.transform, settings);

        if (enemyRoot == null)
        {
            warning = $"{scaling.FloorNumber}F is missing '{settings.EnemyPlacementMarkersRootName}'.";
            return false;
        }

        bool changed = TuneSharedEnemyMarkers(scene, floorAuthoring, enemyRoot, scaling.FloorNumber, scaling.SharedEnemyMarkerCount, out string markerWarning);
        SerializedObject serializedAuthoring = new(floorAuthoring);
        SerializedProperty enemyQuotaProperty = serializedAuthoring.FindProperty("enemyPlacementQuota");

        if (enemyQuotaProperty == null)
        {
            warning = $"{scaling.FloorNumber}F is missing serialized enemy quota fields.";
            return false;
        }

        changed |= SetInt(enemyQuotaProperty.FindPropertyRelative("patrolCount"), scaling.PatrolCount);
        changed |= SetInt(enemyQuotaProperty.FindPropertyRelative("sentryCount"), scaling.SentryCount);
        changed |= SetInt(enemyQuotaProperty.FindPropertyRelative("chaserCount"), scaling.ChaserCount);

        if (changed)
        {
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
        }

        floorAuthoring.CacheReferencesFromHierarchy();
        EditorUtility.SetDirty(floorAuthoring);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        warning = markerWarning;
        return changed;
    }

    private static bool SetInt(SerializedProperty property, int value)
    {
        if (property == null)
        {
            return false;
        }

        int resolvedValue = Mathf.Max(0, value);

        if (property.intValue == resolvedValue)
        {
            return false;
        }

        property.intValue = resolvedValue;
        return true;
    }

    private static bool TryGetScaling(int floorNumber, out FloorDifficultyScaling scaling)
    {
        // Patrol and sentry share one authored pool, so the quota keeps a 1:1 split
        // while each floor gets a fixed number of randomized shared enemy markers.
        scaling = floorNumber switch
        {
            1 => new FloorDifficultyScaling(1, sharedEnemyMarkerCount: 12, patrolCount: 3, sentryCount: 2, chaserCount: 1),
            2 => new FloorDifficultyScaling(2, sharedEnemyMarkerCount: 12, patrolCount: 2, sentryCount: 2, chaserCount: 1),
            3 => new FloorDifficultyScaling(3, sharedEnemyMarkerCount: 12, patrolCount: 2, sentryCount: 2, chaserCount: 1),
            4 => new FloorDifficultyScaling(4, sharedEnemyMarkerCount: 8, patrolCount: 2, sentryCount: 1, chaserCount: 1),
            _ => default
        };

        return scaling.IsValid;
    }

    private static bool TuneSharedEnemyMarkers(
        Scene scene,
        MainEscapeFloorAuthoring floorAuthoring,
        Transform enemyRoot,
        int floorNumber,
        int targetCount,
        out string warning)
    {
        warning = string.Empty;
        int resolvedTargetCount = Mathf.Max(0, targetCount);
        MainEscapeEnemyPlacementMarker[] sharedMarkers = CollectSharedEnemyMarkers(enemyRoot, scene);
        bool changed = false;

        if (sharedMarkers.Length > resolvedTargetCount)
        {
            MainEscapeEnemyPlacementMarker[] selectedMarkers = SelectSpreadMarkers(sharedMarkers, resolvedTargetCount);
            HashSet<MainEscapeEnemyPlacementMarker> selectedLookup = new(selectedMarkers);

            for (int index = enemyRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = enemyRoot.GetChild(index);
                MainEscapeEnemyPlacementMarker marker = child != null ? child.GetComponent<MainEscapeEnemyPlacementMarker>() : null;

                if (marker == null || marker.PlacementKind == MainEscapeEnemyPlacementKind.Chaser || selectedLookup.Contains(marker))
                {
                    continue;
                }

                changed = true;

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            floorAuthoring.CacheReferencesFromHierarchy();
            sharedMarkers = CollectSharedEnemyMarkers(enemyRoot, scene);
        }

        if (sharedMarkers.Length < resolvedTargetCount)
        {
            floorAuthoring.CacheReferencesFromHierarchy();
            HashSet<Vector3Int> occupiedCells = CollectOccupiedCells(floorAuthoring.GroundTilemap, floorAuthoring);
            List<MainEscapeEnemyPlacementMarker> markers = sharedMarkers
                .Where(marker => marker != null)
                .ToList();
            List<MainEscapeEnemyPlacementMarker> sourcePool = markers.Count > 0
                ? new List<MainEscapeEnemyPlacementMarker>(markers)
                : floorAuthoring.GetChaserPlacementMarkers()
                    .Where(marker => marker != null)
                    .ToList();

            if (sourcePool.Count == 0)
            {
                warning = $"{floorNumber}F has no authored enemy markers that can be cloned into the shared pool.";
                return changed;
            }

            int sourceIndex = 0;
            int maxAttempts = Math.Max(sourcePool.Count * SearchOffsets.Length * 4, resolvedTargetCount * SearchOffsets.Length);

            while (markers.Count < resolvedTargetCount && sourceIndex < maxAttempts)
            {
                MainEscapeEnemyPlacementMarker sourceMarker = sourcePool[sourceIndex % sourcePool.Count];
                sourceIndex++;

                if (sourceMarker == null)
                {
                    continue;
                }

                Vector3Int originCell = MainEscapeTilemapCellUtility.WorldToCell2D(floorAuthoring.GroundTilemap, sourceMarker.transform.position);

                if (!TryFindNearbyFreeCell(floorAuthoring.GroundTilemap, originCell, occupiedCells, out Vector3Int newCell))
                {
                    continue;
                }

                MainEscapeEnemyPlacementMarker newMarker = UnityEngine.Object.Instantiate(sourceMarker, enemyRoot);
                newMarker.transform.position = floorAuthoring.GroundTilemap.GetCellCenterWorld(newCell);
                newMarker.transform.rotation = sourceMarker.transform.rotation;
                ConfigureSharedEnemyMarker(newMarker, markers.Count);
                occupiedCells.Add(newCell);
                markers.Add(newMarker);
                sourcePool.Add(newMarker);
                EditorUtility.SetDirty(newMarker.gameObject);
                changed = true;
            }

            if (markers.Count < resolvedTargetCount)
            {
                warning = $"{floorNumber}F could only author {markers.Count} shared enemy markers out of the requested {resolvedTargetCount}.";
            }
        }

        MainEscapeEnemyPlacementMarker[] normalizedMarkers = CollectSharedEnemyMarkers(enemyRoot, scene);
        NormalizeSharedEnemyMarkers(normalizedMarkers);
        return changed;
    }

    private static void NormalizeSharedEnemyMarkers(MainEscapeEnemyPlacementMarker[] markers)
    {
        if (markers == null)
        {
            return;
        }

        Array.Sort(markers, CompareByPositionThenName);

        for (int index = 0; index < markers.Length; index++)
        {
            ConfigureSharedEnemyMarker(markers[index], index);
        }
    }

    private static void ConfigureSharedEnemyMarker(MainEscapeEnemyPlacementMarker marker, int index)
    {
        if (marker == null)
        {
            return;
        }

        string markerName = $"EnemySharedCandidate_{index + 1:00}";
        marker.name = markerName;
        marker.Configure(MainEscapeEnemyPlacementKind.Shared, markerName);
        EditorUtility.SetDirty(marker.gameObject);
    }

    private static MainEscapeEnemyPlacementMarker[] CollectSharedEnemyMarkers(Transform enemyRoot, Scene scene)
    {
        return enemyRoot != null
            ? enemyRoot.GetComponentsInChildren<MainEscapeEnemyPlacementMarker>(true)
                .Where(marker => marker != null
                    && marker.gameObject.scene == scene
                    && marker.PlacementKind != MainEscapeEnemyPlacementKind.Chaser)
                .ToArray()
            : Array.Empty<MainEscapeEnemyPlacementMarker>();
    }

    private static MainEscapeEnemyPlacementMarker[] SelectSpreadMarkers(
        MainEscapeEnemyPlacementMarker[] markers,
        int desiredCount)
    {
        if (markers == null || markers.Length == 0 || desiredCount <= 0)
        {
            return Array.Empty<MainEscapeEnemyPlacementMarker>();
        }

        List<MainEscapeEnemyPlacementMarker> candidates = markers
            .Where(marker => marker != null)
            .ToList();

        if (candidates.Count <= desiredCount)
        {
            return candidates.ToArray();
        }

        List<MainEscapeEnemyPlacementMarker> selected = new(desiredCount);
        Vector3 centroid = ComputeCentroid(candidates);
        MainEscapeEnemyPlacementMarker seed = candidates
            .OrderBy(marker => (marker.transform.position - centroid).sqrMagnitude)
            .ThenBy(marker => marker.name, StringComparer.Ordinal)
            .First();

        selected.Add(seed);
        candidates.Remove(seed);

        while (selected.Count < desiredCount && candidates.Count > 0)
        {
            MainEscapeEnemyPlacementMarker next = null;
            float bestDistance = float.MinValue;

            for (int index = 0; index < candidates.Count; index++)
            {
                MainEscapeEnemyPlacementMarker candidate = candidates[index];
                float minDistance = float.MaxValue;

                for (int selectedIndex = 0; selectedIndex < selected.Count; selectedIndex++)
                {
                    float distance = (candidate.transform.position - selected[selectedIndex].transform.position).sqrMagnitude;

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }

                if (minDistance > bestDistance
                    || (Mathf.Approximately(minDistance, bestDistance)
                        && string.CompareOrdinal(candidate.name, next != null ? next.name : string.Empty) < 0))
                {
                    bestDistance = minDistance;
                    next = candidate;
                }
            }

            if (next == null)
            {
                break;
            }

            selected.Add(next);
            candidates.Remove(next);
        }

        return selected.ToArray();
    }

    private static Vector3 ComputeCentroid(List<MainEscapeEnemyPlacementMarker> markers)
    {
        if (markers == null || markers.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 sum = Vector3.zero;

        for (int index = 0; index < markers.Count; index++)
        {
            sum += markers[index].transform.position;
        }

        return sum / markers.Count;
    }

    private static HashSet<Vector3Int> CollectOccupiedCells(Tilemap groundTilemap, MainEscapeFloorAuthoring floorAuthoring)
    {
        HashSet<Vector3Int> occupiedCells = new();
        AddOccupiedCells(groundTilemap, floorAuthoring.GetSupportItemPlacementMarkers(), occupiedCells);
        AddOccupiedCells(groundTilemap, floorAuthoring.GetKeyPlacementMarkers(), occupiedCells);
        AddOccupiedCells(groundTilemap, floorAuthoring.GetSharedEnemyPlacementMarkers(), occupiedCells);
        AddOccupiedCells(groundTilemap, floorAuthoring.GetChaserPlacementMarkers(), occupiedCells);
        AddOccupiedCells(groundTilemap, floorAuthoring.GetDangerPlacementMarkers(), occupiedCells);
        return occupiedCells;
    }

    private static void AddOccupiedCells<TMarker>(
        Tilemap groundTilemap,
        TMarker[] markers,
        HashSet<Vector3Int> occupiedCells)
        where TMarker : Component
    {
        if (groundTilemap == null || markers == null || occupiedCells == null)
        {
            return;
        }

        for (int index = 0; index < markers.Length; index++)
        {
            TMarker marker = markers[index];

            if (marker == null)
            {
                continue;
            }

            occupiedCells.Add(MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, marker.transform.position));
        }
    }

    private static bool TryFindNearbyFreeCell(
        Tilemap groundTilemap,
        Vector3Int originCell,
        HashSet<Vector3Int> occupiedCells,
        out Vector3Int newCell)
    {
        if (groundTilemap != null)
        {
            for (int index = 0; index < SearchOffsets.Length; index++)
            {
                Vector3Int candidateCell = originCell + SearchOffsets[index];

                if (occupiedCells.Contains(candidateCell) || !groundTilemap.HasTile(candidateCell))
                {
                    continue;
                }

                newCell = candidateCell;
                return true;
            }
        }

        newCell = originCell;
        return false;
    }

    private static Transform FindEnemyRoot(Transform floorRoot, MainEscapeRuntimeSettings settings)
    {
        if (floorRoot == null || settings == null)
        {
            return null;
        }

        Transform authoringRoot = FindDirectChild(floorRoot, settings.AuthoringMarkersRootName);
        return authoringRoot != null ? FindDirectChild(authoringRoot, settings.EnemyPlacementMarkersRootName) : null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static int CompareByPositionThenName(
        MainEscapeEnemyPlacementMarker left,
        MainEscapeEnemyPlacementMarker right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        Vector3 leftPosition = left.transform.position;
        Vector3 rightPosition = right.transform.position;
        int compareX = leftPosition.x.CompareTo(rightPosition.x);

        if (compareX != 0)
        {
            return compareX;
        }

        int compareY = leftPosition.y.CompareTo(rightPosition.y);

        if (compareY != 0)
        {
            return compareY;
        }

        return string.CompareOrdinal(left.name, right.name);
    }

    private static Vector3Int[] BuildSearchOffsets()
    {
        List<Vector3Int> offsets = new();

        for (int radius = 1; radius <= 6; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                    {
                        continue;
                    }

                    offsets.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        return offsets.ToArray();
    }

    private readonly struct FloorDifficultyScaling
    {
        public FloorDifficultyScaling(
            int floorNumber,
            int sharedEnemyMarkerCount,
            int patrolCount,
            int sentryCount,
            int chaserCount)
        {
            FloorNumber = floorNumber;
            SharedEnemyMarkerCount = Mathf.Max(0, sharedEnemyMarkerCount);
            PatrolCount = Mathf.Max(0, patrolCount);
            SentryCount = Mathf.Max(0, sentryCount);
            ChaserCount = Mathf.Clamp(chaserCount, 0, 1);
        }

        public int FloorNumber { get; }
        public int SharedEnemyMarkerCount { get; }
        public int PatrolCount { get; }
        public int SentryCount { get; }
        public int ChaserCount { get; }
        public bool IsValid => FloorNumber > 0;
    }
}
#endif

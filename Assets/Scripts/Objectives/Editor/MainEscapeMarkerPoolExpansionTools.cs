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

public static class MainEscapeMarkerPoolExpansionTools
{
    private const string MenuPath = "Tools/Main Escape/Normalize And Expand Shared Markers (1F-4F)";
    private const int AdditionalSupportMarkersPerFloor = 10;
    private const int AdditionalSharedEnemyMarkersPerFloor = 5;
    private const int TargetChaserMarkersPerFloor = 3;

    private static readonly Vector3Int[] SearchOffsets = BuildSearchOffsets();

    [MenuItem(MenuPath)]
    private static void NormalizeAndExpandSharedMarkers()
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
                if (route.floorNumber <= 0 || route.floorNumber >= 5)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(route.scenePath) || !File.Exists(route.scenePath))
                {
                    warnings.Add($"{route.floorNumber}F scene path is missing: '{route.scenePath}'.");
                    continue;
                }

                if (NormalizeAndExpandScene(route.scenePath, route.floorNumber, settings, out string warning))
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
            Debug.LogWarning("[MainEscapeMarkerPoolExpansionTools] Completed with warnings.\n- " + string.Join("\n- ", warnings));
        }

        Debug.Log(
            updatedScenes.Count > 0
                ? "[MainEscapeMarkerPoolExpansionTools] Updated scenes: " + string.Join(", ", updatedScenes)
                : "[MainEscapeMarkerPoolExpansionTools] No authored floor scenes were updated.");
    }

    private static bool NormalizeAndExpandScene(
        string scenePath,
        int floorNumber,
        MainEscapeRuntimeSettings settings,
        out string warning)
    {
        warning = string.Empty;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        MainEscapeFloorAuthoring floorAuthoring = UnityEngine.Object.FindFirstObjectByType<MainEscapeFloorAuthoring>(FindObjectsInactive.Include);

        if (floorAuthoring == null)
        {
            warning = $"{floorNumber}F is missing {nameof(MainEscapeFloorAuthoring)}.";
            return false;
        }

        floorAuthoring.CacheReferencesFromHierarchy();

        Tilemap groundTilemap = floorAuthoring.GroundTilemap;

        if (groundTilemap == null)
        {
            warning = $"{floorNumber}F is missing the ground tilemap needed to expand markers.";
            return false;
        }

        Transform authoringRoot = FindDirectChild(floorAuthoring.transform, settings.AuthoringMarkersRootName);
        Transform supportRoot = authoringRoot != null ? FindDirectChild(authoringRoot, settings.ItemPlacementMarkersRootName) : null;
        Transform enemyRoot = authoringRoot != null ? FindDirectChild(authoringRoot, settings.EnemyPlacementMarkersRootName) : null;
        Transform chaserRoot = authoringRoot != null ? FindDirectChild(authoringRoot, settings.ChaserPlacementMarkersRootName) : null;

        if (supportRoot == null || enemyRoot == null || chaserRoot == null)
        {
            warning = $"{floorNumber}F is missing one or more marker roots under '{settings.AuthoringMarkersRootName}'.";
            return false;
        }

        HashSet<Vector3Int> occupiedCells = CollectOccupiedCells(groundTilemap, floorAuthoring);
        bool changed = false;

        MainEscapeItemPlacementMarker[] supportMarkers = CollectMarkersInScene<MainEscapeItemPlacementMarker>(supportRoot, scene);
        NormalizeSupportMarkers(supportMarkers);
        changed |= EnsureMarkerCount(
            supportRoot,
            supportMarkers,
            supportMarkers.Length + AdditionalSupportMarkersPerFloor,
            occupiedCells,
            groundTilemap,
            supportMarkers,
            CreateSupportMarkerClone,
            ConfigureSupportMarker);
        supportMarkers = CollectMarkersInScene<MainEscapeItemPlacementMarker>(supportRoot, scene);
        NormalizeSupportMarkers(supportMarkers);

        MainEscapeEnemyPlacementMarker[] chaserMarkers = CollectMarkersInScene<MainEscapeEnemyPlacementMarker>(chaserRoot, scene)
            .Where(marker => marker != null)
            .ToArray();
        MainEscapeEnemyPlacementMarker[] sharedEnemyMarkers = CollectEnemyMarkersInScene(enemyRoot, scene);
        NormalizeSharedEnemyMarkers(sharedEnemyMarkers);
        changed |= EnsureMarkerCount(
            enemyRoot,
            sharedEnemyMarkers,
            sharedEnemyMarkers.Length + AdditionalSharedEnemyMarkersPerFloor,
            occupiedCells,
            groundTilemap,
            sharedEnemyMarkers.Length > 0 ? sharedEnemyMarkers : chaserMarkers,
            CreateEnemyMarkerClone,
            ConfigureSharedEnemyMarker);
        sharedEnemyMarkers = CollectEnemyMarkersInScene(enemyRoot, scene);
        NormalizeSharedEnemyMarkers(sharedEnemyMarkers);

        NormalizeChaserMarkers(chaserMarkers);
        changed |= EnsureMarkerCount(
            chaserRoot,
            chaserMarkers,
            TargetChaserMarkersPerFloor,
            occupiedCells,
            groundTilemap,
            chaserMarkers.Length > 0 ? chaserMarkers : sharedEnemyMarkers,
            CreateEnemyMarkerClone,
            ConfigureChaserMarker);
        chaserMarkers = CollectMarkersInScene<MainEscapeEnemyPlacementMarker>(chaserRoot, scene)
            .Where(marker => marker != null)
            .ToArray();
        NormalizeChaserMarkers(chaserMarkers);

        floorAuthoring.CacheReferencesFromHierarchy();
        EditorUtility.SetDirty(floorAuthoring);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return changed;
    }

    private static void NormalizeSupportMarkers(MainEscapeItemPlacementMarker[] markers)
    {
        NormalizeMarkers(
            markers,
            index => $"SupportItemCandidate_{index + 1:00}",
            (marker, markerName) => marker.Configure(MainEscapeItemPlacementCategory.SupportItem, markerName));
    }

    private static void NormalizeSharedEnemyMarkers(MainEscapeEnemyPlacementMarker[] markers)
    {
        NormalizeMarkers(
            markers,
            index => $"EnemySharedCandidate_{index + 1:00}",
            (marker, markerName) => marker.Configure(MainEscapeEnemyPlacementKind.Shared, markerName));
    }

    private static void NormalizeChaserMarkers(MainEscapeEnemyPlacementMarker[] markers)
    {
        NormalizeMarkers(
            markers,
            index => $"ChaserCandidate_{index + 1:00}",
            (marker, markerName) => marker.Configure(MainEscapeEnemyPlacementKind.Chaser, markerName));
    }

    private static void NormalizeMarkers<TMarker>(
        TMarker[] markers,
        Func<int, string> nameBuilder,
        Action<TMarker, string> configure)
        where TMarker : Component
    {
        if (markers == null)
        {
            return;
        }

        Array.Sort(markers, CompareByName);

        for (int index = 0; index < markers.Length; index++)
        {
            TMarker marker = markers[index];

            if (marker == null)
            {
                continue;
            }

            string markerName = nameBuilder(index);
            marker.name = markerName;
            configure(marker, markerName);
            EditorUtility.SetDirty(marker.gameObject);
        }
    }

    private static bool EnsureMarkerCount<TMarker>(
        Transform root,
        TMarker[] existingMarkers,
        int targetCount,
        HashSet<Vector3Int> occupiedCells,
        Tilemap groundTilemap,
        TMarker[] sourceMarkers,
        Func<TMarker, Transform, TMarker> cloneFactory,
        Action<TMarker, int> configureClone)
        where TMarker : Component
    {
        if (root == null || existingMarkers == null || groundTilemap == null || sourceMarkers == null)
        {
            return false;
        }

        List<TMarker> markers = existingMarkers.Where(marker => marker != null).ToList();
        List<TMarker> sourcePool = sourceMarkers.Where(marker => marker != null).ToList();

        if (sourcePool.Count == 0 || markers.Count >= targetCount)
        {
            return false;
        }

        bool changed = false;
        int sourceIndex = 0;

        while (markers.Count < targetCount && sourceIndex < sourcePool.Count * SearchOffsets.Length)
        {
            TMarker sourceMarker = sourcePool[sourceIndex % sourcePool.Count];
            sourceIndex++;

            if (sourceMarker == null)
            {
                continue;
            }

            Vector3Int originCell = MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, sourceMarker.transform.position);

            if (!TryFindNearbyFreeCell(groundTilemap, originCell, occupiedCells, out Vector3Int newCell))
            {
                continue;
            }

            TMarker newMarker = cloneFactory(sourceMarker, root);
            newMarker.transform.position = groundTilemap.GetCellCenterWorld(newCell);
            newMarker.transform.rotation = sourceMarker.transform.rotation;
            configureClone(newMarker, markers.Count);
            occupiedCells.Add(newCell);
            markers.Add(newMarker);
            sourcePool.Add(newMarker);
            EditorUtility.SetDirty(newMarker.gameObject);
            changed = true;
        }

        return changed;
    }

    private static MainEscapeItemPlacementMarker CreateSupportMarkerClone(
        MainEscapeItemPlacementMarker source,
        Transform parent)
    {
        return UnityEngine.Object.Instantiate(source, parent);
    }

    private static MainEscapeEnemyPlacementMarker CreateEnemyMarkerClone(
        MainEscapeEnemyPlacementMarker source,
        Transform parent)
    {
        return UnityEngine.Object.Instantiate(source, parent);
    }

    private static void ConfigureSupportMarker(MainEscapeItemPlacementMarker marker, int index)
    {
        string markerName = $"SupportItemCandidate_{index + 1:00}";
        marker.name = markerName;
        marker.Configure(MainEscapeItemPlacementCategory.SupportItem, markerName);
    }

    private static void ConfigureSharedEnemyMarker(MainEscapeEnemyPlacementMarker marker, int index)
    {
        string markerName = $"EnemySharedCandidate_{index + 1:00}";
        marker.name = markerName;
        marker.Configure(MainEscapeEnemyPlacementKind.Shared, markerName);
    }

    private static void ConfigureChaserMarker(MainEscapeEnemyPlacementMarker marker, int index)
    {
        string markerName = $"ChaserCandidate_{index + 1:00}";
        marker.name = markerName;
        marker.Configure(MainEscapeEnemyPlacementKind.Chaser, markerName);
    }

    private static TMarker[] CollectMarkersInScene<TMarker>(
        Transform root,
        Scene scene)
        where TMarker : Component
    {
        return root != null
            ? root.GetComponentsInChildren<TMarker>(true)
                .Where(marker => marker != null && marker.gameObject.scene == scene)
                .ToArray()
            : Array.Empty<TMarker>();
    }

    private static MainEscapeEnemyPlacementMarker[] CollectEnemyMarkersInScene(
        Transform root,
        Scene scene)
    {
        return root != null
            ? root.GetComponentsInChildren<MainEscapeEnemyPlacementMarker>(true)
                .Where(marker => marker != null
                    && marker.gameObject.scene == scene
                    && marker.PlacementKind != MainEscapeEnemyPlacementKind.Chaser)
                .ToArray()
            : Array.Empty<MainEscapeEnemyPlacementMarker>();
    }

    private static HashSet<Vector3Int> CollectOccupiedCells(Tilemap groundTilemap, MainEscapeFloorAuthoring floorAuthoring)
    {
        HashSet<Vector3Int> occupied = new();

        AddOccupiedCells(groundTilemap, floorAuthoring.GetSupportItemPlacementMarkers(), occupied);
        AddOccupiedCells(groundTilemap, floorAuthoring.GetKeyPlacementMarkers(), occupied);
        AddOccupiedCells(groundTilemap, floorAuthoring.GetSharedEnemyPlacementMarkers(), occupied);
        AddOccupiedCells(groundTilemap, floorAuthoring.GetChaserPlacementMarkers(), occupied);
        return occupied;
    }

    private static void AddOccupiedCells<TMarker>(
        Tilemap groundTilemap,
        TMarker[] markers,
        HashSet<Vector3Int> occupied)
        where TMarker : Component
    {
        if (groundTilemap == null || markers == null || occupied == null)
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

            occupied.Add(MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, marker.transform.position));
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

    private static int CompareByName<TMarker>(TMarker left, TMarker right)
        where TMarker : Component
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        return string.CompareOrdinal(leftName, rightName);
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
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainEscapeDangerMarkerTuningTools
{
    private const string MenuPath = "Tools/Main Escape/Tune Danger Marker Pools (1F-4F)";

    [MenuItem(MenuPath)]
    private static void TuneDangerMarkerPools()
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
                if (!TryGetDangerTuning(route.floorNumber, out DangerTuning tuning))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(route.scenePath) || !File.Exists(route.scenePath))
                {
                    warnings.Add($"{route.floorNumber}F scene path is missing: '{route.scenePath}'.");
                    continue;
                }

                if (TuneDangerMarkersInScene(route.scenePath, settings, tuning, out string warning))
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
            Debug.LogWarning("[MainEscapeDangerMarkerTuningTools] Completed with warnings.\n- " + string.Join("\n- ", warnings));
        }

        Debug.Log(
            updatedScenes.Count > 0
                ? "[MainEscapeDangerMarkerTuningTools] Updated scenes: " + string.Join(", ", updatedScenes)
                : "[MainEscapeDangerMarkerTuningTools] No authored floor scenes were updated.");
    }

    private static bool TuneDangerMarkersInScene(
        string scenePath,
        MainEscapeRuntimeSettings settings,
        DangerTuning tuning,
        out string warning)
    {
        warning = string.Empty;
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        MainEscapeFloorAuthoring floorAuthoring = UnityEngine.Object.FindFirstObjectByType<MainEscapeFloorAuthoring>(FindObjectsInactive.Include);

        if (floorAuthoring == null)
        {
            warning = $"{tuning.FloorNumber}F is missing {nameof(MainEscapeFloorAuthoring)}.";
            return false;
        }

        floorAuthoring.CacheReferencesFromHierarchy();
        Transform dangerRoot = FindDangerRoot(floorAuthoring.transform, settings);

        if (dangerRoot == null)
        {
            warning = $"{tuning.FloorNumber}F is missing '{settings.DangerMarkersRootName}'.";
            return false;
        }

        MainEscapeDangerPlacementMarker[] allMarkers = CollectDangerMarkers(dangerRoot, scene);

        if (allMarkers.Length == 0)
        {
            warning = $"{tuning.FloorNumber}F has no authored danger markers to tune.";
            return false;
        }

        MainEscapeDangerPlacementMarker[] selectedMarkers = SelectSpreadMarkers(allMarkers, tuning.MarkerCount);
        HashSet<MainEscapeDangerPlacementMarker> selectedLookup = new(selectedMarkers);
        bool changed = false;

        for (int index = dangerRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = dangerRoot.GetChild(index);
            MainEscapeDangerPlacementMarker marker = child != null ? child.GetComponent<MainEscapeDangerPlacementMarker>() : null;

            if (marker == null || selectedLookup.Contains(marker))
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

        selectedMarkers = CollectDangerMarkers(dangerRoot, scene);
        Array.Sort(selectedMarkers, CompareByPositionThenName);

        for (int index = 0; index < selectedMarkers.Length; index++)
        {
            MainEscapeDangerPlacementMarker marker = selectedMarkers[index];

            if (marker == null)
            {
                continue;
            }

            string markerName = $"DangerCandidate_{index + 1:00}";

            if (!string.Equals(marker.name, markerName, StringComparison.Ordinal)
                || !string.Equals(marker.PlacementId, markerName, StringComparison.Ordinal))
            {
                marker.name = markerName;
                marker.Configure(markerName);
                EditorUtility.SetDirty(marker.gameObject);
                changed = true;
            }
        }

        SerializedObject serializedAuthoring = new(floorAuthoring);
        SerializedProperty trapCountProperty = serializedAuthoring.FindProperty("glassTrapPlacementCount");

        if (trapCountProperty != null && trapCountProperty.intValue != tuning.TrapQuota)
        {
            trapCountProperty.intValue = tuning.TrapQuota;
            serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        floorAuthoring.CacheReferencesFromHierarchy();
        EditorUtility.SetDirty(floorAuthoring);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return changed;
    }

    private static Transform FindDangerRoot(Transform floorRoot, MainEscapeRuntimeSettings settings)
    {
        if (floorRoot == null || settings == null)
        {
            return null;
        }

        Transform authoringRoot = FindDirectChild(floorRoot, settings.AuthoringMarkersRootName);
        return authoringRoot != null ? FindDirectChild(authoringRoot, settings.DangerMarkersRootName) : null;
    }

    private static MainEscapeDangerPlacementMarker[] CollectDangerMarkers(Transform dangerRoot, Scene scene)
    {
        return dangerRoot != null
            ? dangerRoot.GetComponentsInChildren<MainEscapeDangerPlacementMarker>(true)
                .Where(marker => marker != null && marker.gameObject.scene == scene)
                .ToArray()
            : Array.Empty<MainEscapeDangerPlacementMarker>();
    }

    private static MainEscapeDangerPlacementMarker[] SelectSpreadMarkers(
        MainEscapeDangerPlacementMarker[] markers,
        int desiredCount)
    {
        if (markers == null || markers.Length == 0 || desiredCount <= 0)
        {
            return Array.Empty<MainEscapeDangerPlacementMarker>();
        }

        if (markers.Length <= desiredCount)
        {
            return markers.Where(marker => marker != null).ToArray();
        }

        List<MainEscapeDangerPlacementMarker> candidates = markers
            .Where(marker => marker != null)
            .ToList();

        if (candidates.Count <= desiredCount)
        {
            return candidates.ToArray();
        }

        List<MainEscapeDangerPlacementMarker> selected = new(desiredCount);
        Vector3 centroid = ComputeCentroid(candidates);
        MainEscapeDangerPlacementMarker seed = candidates
            .OrderBy(marker => (marker.transform.position - centroid).sqrMagnitude)
            .ThenBy(marker => marker.name, StringComparer.Ordinal)
            .First();

        selected.Add(seed);
        candidates.Remove(seed);

        while (selected.Count < desiredCount && candidates.Count > 0)
        {
            MainEscapeDangerPlacementMarker next = null;
            float bestDistance = float.MinValue;

            for (int index = 0; index < candidates.Count; index++)
            {
                MainEscapeDangerPlacementMarker candidate = candidates[index];
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

    private static Vector3 ComputeCentroid(List<MainEscapeDangerPlacementMarker> markers)
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
        MainEscapeDangerPlacementMarker left,
        MainEscapeDangerPlacementMarker right)
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

    private static bool TryGetDangerTuning(int floorNumber, out DangerTuning tuning)
    {
        tuning = floorNumber switch
        {
            1 => new DangerTuning(1, 8, 2),
            2 => new DangerTuning(2, 10, 3),
            3 => new DangerTuning(3, 12, 4),
            4 => new DangerTuning(4, 14, 5),
            _ => default
        };

        return tuning.IsValid;
    }

    private readonly struct DangerTuning
    {
        public DangerTuning(int floorNumber, int markerCount, int trapQuota)
        {
            FloorNumber = floorNumber;
            MarkerCount = Mathf.Max(0, markerCount);
            TrapQuota = Mathf.Max(0, trapQuota);
        }

        public int FloorNumber { get; }
        public int MarkerCount { get; }
        public int TrapQuota { get; }
        public bool IsValid => FloorNumber > 0 && MarkerCount > 0;
    }
}
#endif

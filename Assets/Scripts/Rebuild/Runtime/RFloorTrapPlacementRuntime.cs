using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct RFloorRuntimeTrapPlacement
{
    public RFloorRuntimeTrapPlacement(string markerId, Vector3 worldPosition)
    {
        MarkerId = string.IsNullOrWhiteSpace(markerId) ? string.Empty : markerId;
        WorldPosition = worldPosition;
    }

    public string MarkerId { get; }
    public Vector3 WorldPosition { get; }
}

public readonly struct RFloorTrapPlacementPlan
{
    public RFloorTrapPlacementPlan(RFloorRuntimeTrapPlacement[] placements)
    {
        Placements = placements ?? Array.Empty<RFloorRuntimeTrapPlacement>();
    }

    public RFloorRuntimeTrapPlacement[] Placements { get; }
    public bool HasPlacements => Placements != null && Placements.Length > 0;
}

public interface IFloorTrapPlacementPlanner
{
    RFloorTrapPlacementPlan BuildPlan(
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        int runSeed);
}

public interface IFloorRuntimeTrapPlacementController
{
    bool ApplyPlan(
        Scene scene,
        Transform floorRoot,
        MainEscapeRuntimePrefabCatalog catalog,
        RFloorTrapPlacementPlan plan);

    void Clear(Scene scene);
}

public sealed class RAuthoredFloorTrapPlacementPlanner : IFloorTrapPlacementPlanner
{
    public RFloorTrapPlacementPlan BuildPlan(
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        int runSeed)
    {
        if (floorAuthoring == null)
        {
            return new RFloorTrapPlacementPlan(Array.Empty<RFloorRuntimeTrapPlacement>());
        }

        int desiredCount = floorAuthoring.GlassTrapPlacementCount;

        if (desiredCount <= 0)
        {
            return new RFloorTrapPlacementPlan(Array.Empty<RFloorRuntimeTrapPlacement>());
        }

        MainEscapeDangerPlacementMarker[] candidateMarkers = floorAuthoring.GetDangerPlacementMarkers();

        if (candidateMarkers == null || candidateMarkers.Length == 0)
        {
            Debug.LogWarning(
                $"{nameof(RAuthoredFloorTrapPlacementPlanner)} could not resolve any danger placement markers for {floorAuthoring.name} even though {nameof(MainEscapeFloorAuthoring.GlassTrapPlacementCount)} is {desiredCount}.",
                floorAuthoring);
            return new RFloorTrapPlacementPlan(Array.Empty<RFloorRuntimeTrapPlacement>());
        }

        List<MainEscapeDangerPlacementMarker> shuffledMarkers = new(candidateMarkers.Length);

        for (int index = 0; index < candidateMarkers.Length; index++)
        {
            MainEscapeDangerPlacementMarker marker = candidateMarkers[index];

            if (marker != null)
            {
                shuffledMarkers.Add(marker);
            }
        }

        if (shuffledMarkers.Count == 0)
        {
            return new RFloorTrapPlacementPlan(Array.Empty<RFloorRuntimeTrapPlacement>());
        }

        SortMarkers(shuffledMarkers);
        Shuffle(new System.Random(CombineSeed(runSeed, floorNumber)), shuffledMarkers);

        if (desiredCount > shuffledMarkers.Count)
        {
            Debug.LogWarning(
                $"{nameof(RAuthoredFloorTrapPlacementPlanner)} requested {desiredCount} glass traps for {floorAuthoring.name} but found only {shuffledMarkers.Count} danger markers. The plan will clamp to the available markers.",
                floorAuthoring);
        }

        int placementCount = Mathf.Min(desiredCount, shuffledMarkers.Count);
        RFloorRuntimeTrapPlacement[] placements = new RFloorRuntimeTrapPlacement[placementCount];

        for (int index = 0; index < placementCount; index++)
        {
            MainEscapeDangerPlacementMarker marker = shuffledMarkers[index];
            placements[index] = new RFloorRuntimeTrapPlacement(marker.PlacementId, marker.GetWorldPosition());
        }

        return new RFloorTrapPlacementPlan(placements);
    }

    private static void SortMarkers(List<MainEscapeDangerPlacementMarker> markers)
    {
        if (markers == null || markers.Count <= 1)
        {
            return;
        }

        markers.Sort((left, right) => string.CompareOrdinal(
            left != null ? left.PlacementId : string.Empty,
            right != null ? right.PlacementId : string.Empty));
    }

    private static void Shuffle<T>(System.Random random, List<T> values)
    {
        if (random == null || values == null || values.Count <= 1)
        {
            return;
        }

        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static int CombineSeed(int runSeed, int floorNumber)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + runSeed;
            hash = (hash * 31) + floorNumber;
            hash = (hash * 31) + 2713;
            return hash;
        }
    }
}

public sealed class RAuthoredFloorRuntimeTrapPlacementController : IFloorRuntimeTrapPlacementController
{
    private const string RuntimeHazardRootName = "00_Hazards";
    private const string ManagedTrapNamePrefix = "RuntimeGlassTrap_";

    public bool ApplyPlan(
        Scene scene,
        Transform floorRoot,
        MainEscapeRuntimePrefabCatalog catalog,
        RFloorTrapPlacementPlan plan)
    {
        if (!scene.IsValid() || floorRoot == null || catalog == null || !plan.HasPlacements)
        {
            return false;
        }

        NoiseFloorPanel trapPrefab = catalog.GlassTrapPanelPrefab;

        if (trapPrefab == null)
        {
            Debug.LogWarning($"{nameof(RAuthoredFloorRuntimeTrapPlacementController)} could not resolve a glass trap prefab.");
            return false;
        }

        Transform runtimeHazardRoot = ResolveRuntimeHazardRoot(scene, floorRoot);

        if (runtimeHazardRoot == null)
        {
            return false;
        }

        ClearManagedChildren(runtimeHazardRoot);
        DisableLegacySceneTraps(scene, runtimeHazardRoot);

        RFloorRuntimeTrapPlacement[] placements = plan.Placements;

        for (int index = 0; index < placements.Length; index++)
        {
            SpawnTrap(runtimeHazardRoot, trapPrefab, placements[index], index);
        }

        return true;
    }

    public void Clear(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        Transform runtimeHazardRoot = RSceneReferenceLookup.FindTransformInScene(scene, RuntimeHazardRootName);

        if (runtimeHazardRoot != null)
        {
            ClearManagedChildren(runtimeHazardRoot);
        }
    }

    private static void SpawnTrap(
        Transform runtimeHazardRoot,
        NoiseFloorPanel trapPrefab,
        RFloorRuntimeTrapPlacement placement,
        int placementIndex)
    {
        if (runtimeHazardRoot == null || trapPrefab == null)
        {
            return;
        }

        NoiseFloorPanel trapInstance = UnityEngine.Object.Instantiate(trapPrefab, runtimeHazardRoot);
        trapInstance.name = $"{ManagedTrapNamePrefix}{placementIndex + 1:00}_{placement.MarkerId}";
        trapInstance.transform.SetParent(runtimeHazardRoot, false);
        trapInstance.transform.SetPositionAndRotation(placement.WorldPosition, Quaternion.identity);
        trapInstance.gameObject.SetActive(true);
    }

    private static Transform ResolveRuntimeHazardRoot(Scene scene, Transform floorRoot)
    {
        Transform runtimeHazardRoot = RSceneReferenceLookup.FindTransformInScene(scene, RuntimeHazardRootName);

        if (runtimeHazardRoot != null)
        {
            return runtimeHazardRoot;
        }

        if (floorRoot == null)
        {
            return null;
        }

        GameObject rootObject = new(RuntimeHazardRootName);
        rootObject.transform.SetParent(floorRoot, false);
        return rootObject.transform;
    }

    private static void ClearManagedChildren(Transform runtimeHazardRoot)
    {
        if (runtimeHazardRoot == null)
        {
            return;
        }

        for (int index = runtimeHazardRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = runtimeHazardRoot.GetChild(index);

            if (child == null || !child.name.StartsWith(ManagedTrapNamePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void DisableLegacySceneTraps(Scene scene, Transform runtimeHazardRoot)
    {
        if (!scene.IsValid())
        {
            return;
        }

        NoiseFloorPanel[] legacyTraps = RSceneReferenceLookup.FindComponentsInScene<NoiseFloorPanel>(scene);

        for (int index = 0; index < legacyTraps.Length; index++)
        {
            NoiseFloorPanel trap = legacyTraps[index];

            if (trap == null
                || (runtimeHazardRoot != null && trap.transform.IsChildOf(runtimeHazardRoot)))
            {
                continue;
            }

            trap.gameObject.SetActive(false);
        }
    }
}

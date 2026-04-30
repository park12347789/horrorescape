using System;
using System.Collections.Generic;
using UnityEngine;

public enum RSceneMarkerPlacementKind
{
    Custom = 0,
    SupportPickup = 10,
    Hazard = 20,
    Enemy = 30,
    Decor = 40
}

[DisallowMultipleComponent]
public sealed class RSceneMarkerPlacementManager : MonoBehaviour
{
    private const string ManagedInstanceNamePrefix = "SceneMarkerPlacement_";

    [SerializeField] private Transform runtimeRoot;
    [SerializeField] private bool clearRuntimeRootOnApply = true;
    [SerializeField] private PlacementRule[] placementRules = Array.Empty<PlacementRule>();

    // Report-only contract read: do not resolve runtimeRoot here because validation must not create scene objects.
    public ContractSummary BuildContractSummary()
    {
        PlacementRuleContractSummary[] ruleSummaries = Array.Empty<PlacementRuleContractSummary>();

        if (placementRules != null && placementRules.Length > 0)
        {
            ruleSummaries = new PlacementRuleContractSummary[placementRules.Length];

            for (int index = 0; index < placementRules.Length; index++)
            {
                PlacementRule rule = placementRules[index];
                ruleSummaries[index] = rule != null
                    ? rule.BuildContractSummary()
                    : PlacementRuleContractSummary.CreateMissing();
            }
        }

        return new ContractSummary(runtimeRoot, clearRuntimeRootOnApply, ruleSummaries);
    }

    public bool HasActiveRuleKind(RSceneMarkerPlacementKind placementKind)
    {
        if (!isActiveAndEnabled || placementRules == null)
        {
            return false;
        }

        for (int index = 0; index < placementRules.Length; index++)
        {
            PlacementRule rule = placementRules[index];

            if (rule != null && rule.IsActiveForKind(placementKind))
            {
                return true;
            }
        }

        return false;
    }

    public int Apply(int runSeed)
    {
        if (placementRules == null || placementRules.Length == 0)
        {
            return 0;
        }

        Transform resolvedRuntimeRoot = ResolveRuntimeRoot();

        if (resolvedRuntimeRoot == null)
        {
            return 0;
        }

        if (clearRuntimeRootOnApply)
        {
            ClearRuntimeRoot(resolvedRuntimeRoot);
        }

        int appliedCount = 0;
        HashSet<Transform> usedMarkers = new();

        for (int index = 0; index < placementRules.Length; index++)
        {
            PlacementRule rule = placementRules[index];

            if (rule == null)
            {
                continue;
            }

            appliedCount += rule.Apply(runSeed, resolvedRuntimeRoot, usedMarkers);
        }

        return appliedCount;
    }

    private Transform ResolveRuntimeRoot()
    {
        if (runtimeRoot != null)
        {
            return runtimeRoot;
        }

        Debug.LogWarning(
            $"{nameof(RSceneMarkerPlacementManager)} on '{name}' has no runtimeRoot assigned; marker placements were not applied to avoid mutating the authored scene hierarchy.",
            this);
        return null;
    }

    private static void ClearRuntimeRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int index = root.childCount - 1; index >= 0; index--)
        {
            Transform child = root.GetChild(index);

            if (child == null || !child.name.StartsWith(ManagedInstanceNamePrefix, StringComparison.Ordinal))
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

    public readonly struct ContractSummary
    {
        public ContractSummary(
            Transform runtimeRoot,
            bool clearRuntimeRootOnApply,
            IReadOnlyList<PlacementRuleContractSummary> rules)
        {
            RuntimeRoot = runtimeRoot;
            ClearRuntimeRootOnApply = clearRuntimeRootOnApply;
            Rules = rules ?? Array.Empty<PlacementRuleContractSummary>();
        }

        public Transform RuntimeRoot { get; }
        public bool ClearRuntimeRootOnApply { get; }
        public IReadOnlyList<PlacementRuleContractSummary> Rules { get; }
        public int RuleCount => Rules.Count;
    }

    public readonly struct PlacementRuleContractSummary
    {
        public PlacementRuleContractSummary(
            bool hasRule,
            string ruleId,
            RSceneMarkerPlacementKind placementKind,
            GameObject prefab,
            Transform markerRoot,
            int requestedCount,
            bool includeInactiveMarkers,
            bool avoidMarkersUsedByEarlierRules,
            bool useMarkerRotation,
            int seedOffset,
            bool usesExplicitMarkers,
            int explicitMarkerReferenceCount,
            int explicitMarkerSlotCount,
            int candidateCount,
            int expectedPlacementCount)
        {
            HasRule = hasRule;
            RuleId = string.IsNullOrWhiteSpace(ruleId) ? "PlacementRule" : ruleId.Trim();
            PlacementKind = placementKind;
            Prefab = prefab;
            MarkerRoot = markerRoot;
            RequestedCount = Mathf.Max(0, requestedCount);
            IncludeInactiveMarkers = includeInactiveMarkers;
            AvoidMarkersUsedByEarlierRules = avoidMarkersUsedByEarlierRules;
            UseMarkerRotation = useMarkerRotation;
            SeedOffset = seedOffset;
            UsesExplicitMarkers = usesExplicitMarkers;
            ExplicitMarkerReferenceCount = Mathf.Max(0, explicitMarkerReferenceCount);
            ExplicitMarkerSlotCount = Mathf.Max(0, explicitMarkerSlotCount);
            CandidateCount = Mathf.Max(0, candidateCount);
            ExpectedPlacementCount = Mathf.Max(0, expectedPlacementCount);
        }

        public bool HasRule { get; }
        public string RuleId { get; }
        public RSceneMarkerPlacementKind PlacementKind { get; }
        public GameObject Prefab { get; }
        public Transform MarkerRoot { get; }
        public int RequestedCount { get; }
        public bool IncludeInactiveMarkers { get; }
        public bool AvoidMarkersUsedByEarlierRules { get; }
        public bool UseMarkerRotation { get; }
        public int SeedOffset { get; }
        public bool UsesExplicitMarkers { get; }
        public int ExplicitMarkerReferenceCount { get; }
        public int ExplicitMarkerSlotCount { get; }
        public int CandidateCount { get; }
        public int ExpectedPlacementCount { get; }
        public bool HasPlacementInputs => Prefab != null && RequestedCount > 0 && CandidateCount > 0;

        public static PlacementRuleContractSummary CreateMissing()
        {
            return new PlacementRuleContractSummary(
                hasRule: false,
                ruleId: "<missing>",
                placementKind: RSceneMarkerPlacementKind.Custom,
                prefab: null,
                markerRoot: null,
                requestedCount: 0,
                includeInactiveMarkers: true,
                avoidMarkersUsedByEarlierRules: true,
                useMarkerRotation: true,
                seedOffset: 0,
                usesExplicitMarkers: false,
                explicitMarkerReferenceCount: 0,
                explicitMarkerSlotCount: 0,
                candidateCount: 0,
                expectedPlacementCount: 0);
        }
    }

    [Serializable]
    public sealed class PlacementRule
    {
        [SerializeField] private string ruleId = "PlacementRule";
        [SerializeField] private RSceneMarkerPlacementKind placementKind = RSceneMarkerPlacementKind.Custom;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Transform markerRoot;
        [SerializeField] private Transform[] markers = Array.Empty<Transform>();
        [SerializeField, Min(0)] private int count = 1;
        [SerializeField] private bool includeInactiveMarkers = true;
        [SerializeField] private bool avoidMarkersUsedByEarlierRules = true;
        [SerializeField] private bool useMarkerRotation = true;
        [SerializeField] private int seedOffset;

        public PlacementRuleContractSummary BuildContractSummary()
        {
            int requestedCount = Mathf.Max(0, count);
            bool usesExplicitMarkers = markers != null && markers.Length > 0;
            int explicitMarkerSlotCount = usesExplicitMarkers ? markers.Length : 0;
            int explicitMarkerReferenceCount = usesExplicitMarkers ? CountAssignedMarkers(markers) : 0;
            int candidateCount = usesExplicitMarkers
                ? explicitMarkerReferenceCount
                : CountRootMarkers(markerRoot, includeInactiveMarkers, placementKind);
            int expectedPlacementCount = prefab != null && requestedCount > 0
                ? Mathf.Min(requestedCount, candidateCount)
                : 0;

            return new PlacementRuleContractSummary(
                hasRule: true,
                ruleId: ruleId,
                placementKind: placementKind,
                prefab: prefab,
                markerRoot: markerRoot,
                requestedCount: requestedCount,
                includeInactiveMarkers: includeInactiveMarkers,
                avoidMarkersUsedByEarlierRules: avoidMarkersUsedByEarlierRules,
                useMarkerRotation: useMarkerRotation,
                seedOffset: seedOffset,
                usesExplicitMarkers: usesExplicitMarkers,
                explicitMarkerReferenceCount: explicitMarkerReferenceCount,
                explicitMarkerSlotCount: explicitMarkerSlotCount,
                candidateCount: candidateCount,
                expectedPlacementCount: expectedPlacementCount);
        }

        public bool IsActiveForKind(RSceneMarkerPlacementKind desiredPlacementKind)
        {
            return prefab != null
                && count > 0
                && placementKind == desiredPlacementKind;
        }

        public int Apply(int runSeed, Transform runtimeRoot, HashSet<Transform> usedMarkers)
        {
            if (prefab == null || runtimeRoot == null || count <= 0)
            {
                return 0;
            }

            List<Transform> resolvedMarkers = new();
            CollectMarkers(resolvedMarkers);
            SortMarkers(resolvedMarkers);

            if (avoidMarkersUsedByEarlierRules && usedMarkers != null && usedMarkers.Count > 0)
            {
                resolvedMarkers.RemoveAll(marker => marker == null || usedMarkers.Contains(marker));
            }

            if (resolvedMarkers.Count == 0)
            {
                return 0;
            }

            Shuffle(resolvedMarkers, CombineSeed(runSeed, seedOffset, StableHash(ruleId)));

            int placementCount = Mathf.Min(count, resolvedMarkers.Count);
            string resolvedRuleId = string.IsNullOrWhiteSpace(ruleId) ? prefab.name : ruleId.Trim();

            for (int index = 0; index < placementCount; index++)
            {
                Transform marker = resolvedMarkers[index];

                if (marker == null)
                {
                    continue;
                }

                Quaternion rotation = useMarkerRotation ? marker.rotation : Quaternion.identity;
                GameObject instance = UnityEngine.Object.Instantiate(prefab, marker.position, rotation, runtimeRoot);
                instance.name = $"{ManagedInstanceNamePrefix}{prefab.name}_{resolvedRuleId}_{index + 1:00}";
                usedMarkers?.Add(marker);
            }

            return placementCount;
        }

        private void CollectMarkers(List<Transform> results)
        {
            if (results == null)
            {
                return;
            }

            if (markers != null && markers.Length > 0)
            {
                for (int index = 0; index < markers.Length; index++)
                {
                    Transform marker = markers[index];

                    if (marker != null)
                    {
                        results.Add(marker);
                    }
                }

                return;
            }

            if (markerRoot == null)
            {
                return;
            }

            CollectRootMarkers(markerRoot, includeInactiveMarkers, placementKind, results);
        }

        private static void SortMarkers(List<Transform> results)
        {
            if (results == null || results.Count <= 1)
            {
                return;
            }

            results.Sort((left, right) => string.CompareOrdinal(
                BuildStableTransformKey(left),
                BuildStableTransformKey(right)));
        }

        private static void Shuffle<T>(IList<T> values, int seed)
        {
            if (values == null || values.Count <= 1)
            {
                return;
            }

            System.Random random = new(seed);

            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                T current = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = current;
            }
        }

        private static int CombineSeed(int runSeed, int seedOffset, int ruleHash)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + runSeed;
                hash = (hash * 31) + seedOffset;
                hash = (hash * 31) + ruleHash;
                return hash;
            }
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                int hash = 23;

                for (int index = 0; index < value.Length; index++)
                {
                    hash = (hash * 31) + value[index];
                }

                return hash;
            }
        }

        private static string BuildStableTransformKey(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private static int CountAssignedMarkers(Transform[] candidateMarkers)
        {
            if (candidateMarkers == null)
            {
                return 0;
            }

            int count = 0;

            for (int index = 0; index < candidateMarkers.Length; index++)
            {
                if (candidateMarkers[index] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountRootMarkers(
            Transform root,
            bool includeInactive,
            RSceneMarkerPlacementKind placementKind)
        {
            if (root == null)
            {
                return 0;
            }

            if (placementKind == RSceneMarkerPlacementKind.SupportPickup)
            {
                MainEscapeItemPlacementMarker[] itemMarkers = root.GetComponentsInChildren<MainEscapeItemPlacementMarker>(includeInactive);
                int itemMarkerCount = 0;

                for (int index = 0; index < itemMarkers.Length; index++)
                {
                    MainEscapeItemPlacementMarker marker = itemMarkers[index];

                    if (marker != null
                        && marker.transform != root
                        && marker.Category == MainEscapeItemPlacementCategory.SupportItem)
                    {
                        itemMarkerCount++;
                    }
                }

                return itemMarkerCount;
            }

            Transform[] childMarkers = root.GetComponentsInChildren<Transform>(includeInactive);
            int count = 0;

            for (int index = 0; index < childMarkers.Length; index++)
            {
                Transform childMarker = childMarkers[index];

                if (childMarker != null && childMarker != root)
                {
                    count++;
                }
            }

            return count;
        }

        private static void CollectRootMarkers(
            Transform root,
            bool includeInactive,
            RSceneMarkerPlacementKind placementKind,
            List<Transform> results)
        {
            if (root == null || results == null)
            {
                return;
            }

            if (placementKind == RSceneMarkerPlacementKind.SupportPickup)
            {
                MainEscapeItemPlacementMarker[] itemMarkers = root.GetComponentsInChildren<MainEscapeItemPlacementMarker>(includeInactive);

                for (int index = 0; index < itemMarkers.Length; index++)
                {
                    MainEscapeItemPlacementMarker marker = itemMarkers[index];

                    if (marker != null
                        && marker.transform != root
                        && marker.Category == MainEscapeItemPlacementCategory.SupportItem)
                    {
                        results.Add(marker.transform);
                    }
                }

                return;
            }

            Transform[] childMarkers = root.GetComponentsInChildren<Transform>(includeInactive);

            for (int index = 0; index < childMarkers.Length; index++)
            {
                Transform marker = childMarkers[index];

                if (marker != null && marker != root)
                {
                    results.Add(marker);
                }
            }
        }
    }
}

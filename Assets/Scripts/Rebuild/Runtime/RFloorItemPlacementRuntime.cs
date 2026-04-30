using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public readonly struct RFloorRuntimeItemPlacement
{
    public RFloorRuntimeItemPlacement(
        string itemId,
        string displayName,
        int quantity,
        Color color,
        Vector3 worldPosition,
        string markerId,
        MainEscapeItemPlacementCategory placementCategory)
    {
        ItemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
        Quantity = Mathf.Max(1, quantity);
        Tint = color;
        WorldPosition = worldPosition;
        MarkerId = string.IsNullOrWhiteSpace(markerId) ? string.Empty : markerId;
        PlacementCategory = placementCategory;
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public int Quantity { get; }
    public Color Tint { get; }
    public Vector3 WorldPosition { get; }
    public string MarkerId { get; }
    public MainEscapeItemPlacementCategory PlacementCategory { get; }
}

public readonly struct RFloorItemPlacementPlan
{
    public RFloorItemPlacementPlan(RFloorRuntimeItemPlacement[] placements)
    {
        Placements = placements ?? Array.Empty<RFloorRuntimeItemPlacement>();
    }

    public RFloorRuntimeItemPlacement[] Placements { get; }
    public bool HasPlacements => Placements != null && Placements.Length > 0;

    public RFloorItemPlacementPlan WithoutPlacementCategory(MainEscapeItemPlacementCategory placementCategory)
    {
        if (!HasPlacements)
        {
            return this;
        }

        int retainedCount = 0;

        for (int index = 0; index < Placements.Length; index++)
        {
            if (Placements[index].PlacementCategory != placementCategory)
            {
                retainedCount++;
            }
        }

        if (retainedCount == Placements.Length)
        {
            return this;
        }

        if (retainedCount == 0)
        {
            return new RFloorItemPlacementPlan(Array.Empty<RFloorRuntimeItemPlacement>());
        }

        RFloorRuntimeItemPlacement[] retainedPlacements = new RFloorRuntimeItemPlacement[retainedCount];
        int retainedIndex = 0;

        for (int index = 0; index < Placements.Length; index++)
        {
            RFloorRuntimeItemPlacement placement = Placements[index];

            if (placement.PlacementCategory == placementCategory)
            {
                continue;
            }

            retainedPlacements[retainedIndex] = placement;
            retainedIndex++;
        }

        return new RFloorItemPlacementPlan(retainedPlacements);
    }

    public RFloorItemPlacementPlan WithoutItemIds(IReadOnlyCollection<string> itemIds)
    {
        if (!HasPlacements || itemIds == null || itemIds.Count == 0)
        {
            return this;
        }

        int retainedCount = 0;

        for (int index = 0; index < Placements.Length; index++)
        {
            if (!ContainsItemId(itemIds, Placements[index].ItemId))
            {
                retainedCount++;
            }
        }

        if (retainedCount == Placements.Length)
        {
            return this;
        }

        if (retainedCount == 0)
        {
            return new RFloorItemPlacementPlan(Array.Empty<RFloorRuntimeItemPlacement>());
        }

        RFloorRuntimeItemPlacement[] retainedPlacements = new RFloorRuntimeItemPlacement[retainedCount];
        int retainedIndex = 0;

        for (int index = 0; index < Placements.Length; index++)
        {
            RFloorRuntimeItemPlacement placement = Placements[index];

            if (ContainsItemId(itemIds, placement.ItemId))
            {
                continue;
            }

            retainedPlacements[retainedIndex] = placement;
            retainedIndex++;
        }

        return new RFloorItemPlacementPlan(retainedPlacements);
    }

    private static bool ContainsItemId(IReadOnlyCollection<string> itemIds, string itemId)
    {
        if (itemIds == null || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (itemIds is HashSet<string> itemIdSet)
        {
            return itemIdSet.Contains(itemId);
        }

        foreach (string candidateItemId in itemIds)
        {
            if (string.Equals(candidateItemId, itemId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public interface IFloorItemPlacementPlanner
{
    RFloorItemPlacementPlan BuildPlan(
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        int runSeed);
}

public interface IFloorRuntimeItemPlacementController
{
    bool ApplyPlan(
        Scene scene,
        Transform floorRoot,
        MainEscapeFloorAuthoring floorAuthoring,
        MainEscapeRuntimePrefabCatalog catalog,
        RFloorItemPlacementPlan plan,
        IReadOnlyCollection<string> runtimeManagedItemIds);

    void Clear(Scene scene);
}

public sealed class RAuthoredFloorItemPlacementPlanner : IFloorItemPlacementPlanner
{
    private static readonly ItemSpec[] SupportItemSpecs =
    {
        new(PrototypeItemCatalog.GlassBottleItemId, "Glass Bottle", 2, new Color(0.72f, 0.92f, 1f, 1f), 1.15f),
        new(PrototypeItemCatalog.FlashlightBatteryItemId, "Flashlight Battery", 1, new Color(0.46f, 0.9f, 1f, 1f), 1f),
        new(PrototypeItemCatalog.MedkitItemId, "Medkit", 1, new Color(0.45f, 1f, 0.66f, 1f), 0.85f)
    };

    public RFloorItemPlacementPlan BuildPlan(
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        int runSeed)
    {
        if (floorAuthoring == null)
        {
            return new RFloorItemPlacementPlan(Array.Empty<RFloorRuntimeItemPlacement>());
        }

        List<MainEscapeItemPlacementMarker> supportMarkers = new(floorAuthoring.GetSupportItemPlacementMarkers());
        List<MainEscapeItemPlacementMarker> keyMarkers = new(floorAuthoring.GetKeyPlacementMarkers());

        if (supportMarkers.Count == 0 && keyMarkers.Count == 0)
        {
            return new RFloorItemPlacementPlan(Array.Empty<RFloorRuntimeItemPlacement>());
        }

        System.Random random = new(CombineSeed(runSeed, floorNumber));
        SortMarkers(supportMarkers);
        SortMarkers(keyMarkers);
        Shuffle(random, supportMarkers);
        Shuffle(random, keyMarkers);

        List<RFloorRuntimeItemPlacement> placements = new();
        MainEscapeSupportItemPlacementQuota supportQuota = floorAuthoring.SupportItemPlacementQuota;
        AppendWeightedSupportPlacements(
            placements,
            supportMarkers,
            Mathf.Min(supportMarkers.Count, supportQuota.TotalCount),
            random);

        if (keyMarkers.Count > 0)
        {
            MainEscapeItemPlacementMarker selectedKeyMarker = keyMarkers[0];
            placements.Add(new RFloorRuntimeItemPlacement(
                PrototypeItemCatalog.IronGateKeyItemId,
                PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.IronGateKeyItemId, "Iron Gate Key"),
                1,
                new Color(1f, 0.82f, 0.24f, 1f),
                selectedKeyMarker.GetWorldPosition(),
                selectedKeyMarker.PlacementId,
                MainEscapeItemPlacementCategory.Key));
        }

        return new RFloorItemPlacementPlan(placements.ToArray());
    }

    private static void AppendWeightedSupportPlacements(
        List<RFloorRuntimeItemPlacement> placements,
        List<MainEscapeItemPlacementMarker> markers,
        int desiredCount,
        System.Random random)
    {
        if (placements == null || markers == null || desiredCount <= 0 || random == null)
        {
            return;
        }

        int markerCount = Mathf.Min(markers.Count, desiredCount);

        for (int index = 0; index < markerCount; index++)
        {
            MainEscapeItemPlacementMarker marker = markers[index];

            if (marker == null)
            {
                continue;
            }

            ItemSpec itemSpec = ChooseWeightedSupportItem(random);
            placements.Add(new RFloorRuntimeItemPlacement(
                itemSpec.ItemId,
                itemSpec.DisplayName,
                itemSpec.Quantity,
                itemSpec.Tint,
                marker.GetWorldPosition(),
                marker.PlacementId,
                MainEscapeItemPlacementCategory.SupportItem));
        }
    }

    private static ItemSpec ChooseWeightedSupportItem(System.Random random)
    {
        float totalWeight = 0f;

        for (int index = 0; index < SupportItemSpecs.Length; index++)
        {
            totalWeight += Mathf.Max(0.01f, SupportItemSpecs[index].SpawnWeight);
        }

        double roll = random.NextDouble() * totalWeight;

        for (int index = 0; index < SupportItemSpecs.Length; index++)
        {
            ItemSpec spec = SupportItemSpecs[index];
            roll -= Mathf.Max(0.01f, spec.SpawnWeight);

            if (roll <= 0d)
            {
                return spec;
            }
        }

        return SupportItemSpecs[SupportItemSpecs.Length - 1];
    }

    private static void SortMarkers(List<MainEscapeItemPlacementMarker> markers)
    {
        if (markers == null || markers.Count <= 1)
        {
            return;
        }

        markers.Sort((left, right) =>
            string.CompareOrdinal(
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
            hash = (hash * 31) + 1733;
            return hash;
        }
    }

    private readonly struct ItemSpec
    {
        public ItemSpec(string itemId, string displayName, int quantity, Color tint, float spawnWeight)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Quantity = Mathf.Max(1, quantity);
            Tint = tint;
            SpawnWeight = spawnWeight <= 0f ? 1f : spawnWeight;
        }

        public string ItemId { get; }
        public string DisplayName { get; }
        public int Quantity { get; }
        public Color Tint { get; }
        public float SpawnWeight { get; }
    }
}

public sealed class RAuthoredFloorRuntimeItemPlacementController : IFloorRuntimeItemPlacementController
{
    private const string RuntimePickupRootName = "00_Pickups";
    private const string ManagedPickupNamePrefix = "RuntimePlacement_";

    public bool ApplyPlan(
        Scene scene,
        Transform floorRoot,
        MainEscapeFloorAuthoring floorAuthoring,
        MainEscapeRuntimePrefabCatalog catalog,
        RFloorItemPlacementPlan plan,
        IReadOnlyCollection<string> runtimeManagedItemIds)
    {
        if (!scene.IsValid() || floorAuthoring == null)
        {
            return false;
        }

        HashSet<string> managedItemIds = CreateManagedItemIdSet(runtimeManagedItemIds);

        if (managedItemIds.Count == 0)
        {
            return false;
        }

        if (!plan.HasPlacements)
        {
            DisableLegacyScenePickups(scene, runtimePickupRoot: null, managedItemIds: managedItemIds);
            return false;
        }

        if (floorRoot == null || catalog == null)
        {
            return false;
        }

        Transform runtimePickupRoot = ResolveRuntimePickupRoot(scene, floorRoot);

        if (runtimePickupRoot == null)
        {
            return false;
        }

        ClearManagedChildren(runtimePickupRoot);
        DisableLegacyRuntimeRootPickups(runtimePickupRoot, managedItemIds);
        DisableLegacyScenePickups(scene, runtimePickupRoot, managedItemIds);

        RFloorRuntimeItemPlacement[] placements = plan.Placements;

        for (int index = 0; index < placements.Length; index++)
        {
            SpawnPickup(runtimePickupRoot, catalog, placements[index], index);
        }

        return true;
    }

    public void Clear(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        Transform runtimePickupRoot = RSceneReferenceLookup.FindTransformInScene(scene, RuntimePickupRootName);

        if (runtimePickupRoot != null)
        {
            ClearManagedChildren(runtimePickupRoot);
        }
    }

    private static void SpawnPickup(
        Transform runtimePickupRoot,
        MainEscapeRuntimePrefabCatalog catalog,
        RFloorRuntimeItemPlacement placement,
        int placementIndex)
    {
        if (runtimePickupRoot == null || catalog == null)
        {
            return;
        }

        if (placement.PlacementCategory == MainEscapeItemPlacementCategory.Key)
        {
            SpawnKeyPickup(runtimePickupRoot, catalog, placement, placementIndex);
            return;
        }

        PrototypeInventoryPickup pickupPrefab = ResolveSupportPickupPrefab(catalog, placement.ItemId);

        if (pickupPrefab == null)
        {
            Debug.LogWarning($"{nameof(RAuthoredFloorRuntimeItemPlacementController)} could not resolve a pickup prefab for item '{placement.ItemId}'.");
            return;
        }

        PrototypeInventoryPickup pickup = UnityEngine.Object.Instantiate(pickupPrefab, runtimePickupRoot);
        pickup.name = $"{ManagedPickupNamePrefix}{placementIndex + 1:00}_{placement.MarkerId}";
        pickup.transform.position = placement.WorldPosition;
        pickup.Configure(
            placement.WorldPosition,
            placement.ItemId,
            placement.DisplayName,
            placement.Quantity,
            placement.Tint);
    }

    private static void SpawnKeyPickup(
        Transform runtimePickupRoot,
        MainEscapeRuntimePrefabCatalog catalog,
        RFloorRuntimeItemPlacement placement,
        int placementIndex)
    {
        GameObject keyObject = catalog.IronGateKeyVisualPrefab != null
            ? UnityEngine.Object.Instantiate(catalog.IronGateKeyVisualPrefab, runtimePickupRoot)
            : new GameObject("RuntimeKeyPickup");

        keyObject.name = $"{ManagedPickupNamePrefix}{placementIndex + 1:00}_{placement.MarkerId}";
        keyObject.transform.SetParent(runtimePickupRoot, false);
        keyObject.transform.position = placement.WorldPosition;

        MainEscapeKeyPickup keyPickup = keyObject.GetComponent<MainEscapeKeyPickup>();

        if (keyPickup == null)
        {
            keyPickup = keyObject.AddComponent<MainEscapeKeyPickup>();
        }

        EnsureKeyCollider(keyObject);
        keyPickup.Configure(placement.ItemId, placement.DisplayName, placement.Quantity);
    }

    private static void EnsureKeyCollider(GameObject keyObject)
    {
        if (keyObject == null || keyObject.GetComponent<Collider2D>() != null)
        {
            return;
        }

        CircleCollider2D collider = keyObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.4f;
    }

    private static PrototypeInventoryPickup ResolveSupportPickupPrefab(
        MainEscapeRuntimePrefabCatalog catalog,
        string itemId)
    {
        if (catalog == null)
        {
            return null;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal))
        {
            return catalog.FlashlightBatteryPickupPrefab;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal))
        {
            return catalog.GlassBottlePickupPrefab;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal))
        {
            return catalog.MedkitPickupPrefab;
        }

        return null;
    }

    private static Transform ResolveRuntimePickupRoot(Scene scene, Transform floorRoot)
    {
        Transform runtimePickupRoot = RSceneReferenceLookup.FindTransformInScene(scene, RuntimePickupRootName);

        if (runtimePickupRoot != null)
        {
            return runtimePickupRoot;
        }

        if (floorRoot == null)
        {
            return null;
        }

        Debug.LogWarning(
            $"{nameof(RAuthoredFloorRuntimeItemPlacementController)} could not find authored runtime pickup root '{RuntimePickupRootName}' in scene '{scene.name}'; item placement was not applied to avoid mutating the authored scene hierarchy.");
        return null;
    }

    private static void ClearManagedChildren(Transform runtimePickupRoot)
    {
        if (runtimePickupRoot == null)
        {
            return;
        }

        for (int index = runtimePickupRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = runtimePickupRoot.GetChild(index);

            if (child == null || !child.name.StartsWith(ManagedPickupNamePrefix, StringComparison.Ordinal))
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

    private static void DisableLegacyScenePickups(
        Scene scene,
        Transform runtimePickupRoot,
        IReadOnlyCollection<string> managedItemIds)
    {
        if (!scene.IsValid() || managedItemIds == null || managedItemIds.Count == 0)
        {
            return;
        }

        WorldInventoryPickupBase[] legacyPickups =
            RSceneReferenceLookup.FindComponentsInScene<WorldInventoryPickupBase>(scene);

        for (int index = 0; index < legacyPickups.Length; index++)
        {
            WorldInventoryPickupBase pickup = legacyPickups[index];

            if (pickup == null
                || pickup.SuppressRuntimeManagedPickupReplacement
                || !ShouldDisableLegacyPickup(pickup.ItemId, managedItemIds)
                || (runtimePickupRoot != null && pickup.transform.IsChildOf(runtimePickupRoot)))
            {
                continue;
            }

            pickup.gameObject.SetActive(false);
        }
    }

    private static bool ShouldDisableLegacyPickup(
        string itemId,
        IReadOnlyCollection<string> managedItemIds)
    {
        if (string.IsNullOrWhiteSpace(itemId) || managedItemIds == null)
        {
            return false;
        }

        if (managedItemIds is HashSet<string> itemIdSet)
        {
            return itemIdSet.Contains(itemId);
        }

        foreach (string managedItemId in managedItemIds)
        {
            if (string.Equals(managedItemId, itemId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void DisableLegacyRuntimeRootPickups(
        Transform runtimePickupRoot,
        IReadOnlyCollection<string> managedItemIds)
    {
        if (runtimePickupRoot == null || managedItemIds == null || managedItemIds.Count == 0)
        {
            return;
        }

        WorldInventoryPickupBase[] pickups = runtimePickupRoot.GetComponentsInChildren<WorldInventoryPickupBase>(true);

        for (int index = 0; index < pickups.Length; index++)
        {
            WorldInventoryPickupBase pickup = pickups[index];

            if (pickup == null
                || pickup.gameObject.name.StartsWith(ManagedPickupNamePrefix, StringComparison.Ordinal)
                || !ShouldDisableLegacyPickup(pickup.ItemId, managedItemIds))
            {
                continue;
            }

            pickup.gameObject.SetActive(false);
        }
    }

    private static HashSet<string> CreateManagedItemIdSet(IReadOnlyCollection<string> itemIds)
    {
        HashSet<string> managedItemIds = new(StringComparer.Ordinal);

        if (itemIds == null)
        {
            return managedItemIds;
        }

        foreach (string itemId in itemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                managedItemIds.Add(itemId);
            }
        }

        return managedItemIds;
    }
}

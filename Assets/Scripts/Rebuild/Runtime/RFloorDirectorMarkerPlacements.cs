using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class RFloorDirector
{
    private static HashSet<string> CollectSceneMarkerSupportPickupItemIds(MainEscapeFloorAuthoring floorAuthoring)
    {
        HashSet<string> ownedItemIds = new(StringComparer.Ordinal);

        if (floorAuthoring == null)
        {
            return ownedItemIds;
        }

        RSceneMarkerPlacementManager[] placementManagers =
            floorAuthoring.GetComponentsInChildren<RSceneMarkerPlacementManager>(true);

        for (int index = 0; index < placementManagers.Length; index++)
        {
            RSceneMarkerPlacementManager placementManager = placementManagers[index];

            if (placementManager == null || !placementManager.isActiveAndEnabled)
            {
                continue;
            }

            RSceneMarkerPlacementManager.ContractSummary contractSummary = placementManager.BuildContractSummary();

            if (contractSummary.RuntimeRoot == null)
            {
                continue;
            }

            for (int ruleIndex = 0; ruleIndex < contractSummary.Rules.Count; ruleIndex++)
            {
                RSceneMarkerPlacementManager.PlacementRuleContractSummary ruleSummary = contractSummary.Rules[ruleIndex];

                if (ruleSummary.PlacementKind == RSceneMarkerPlacementKind.SupportPickup
                    && ruleSummary.HasPlacementInputs
                    && TryResolveRuntimeManagedSupportItemId(ruleSummary.Prefab, out string itemId))
                {
                    ownedItemIds.Add(itemId);
                }
            }
        }

        return ownedItemIds;
    }

    private static bool TryResolveRuntimeManagedSupportItemId(GameObject prefab, out string itemId)
    {
        itemId = string.Empty;

        if (prefab == null)
        {
            return false;
        }

        PrototypeInventoryPickup pickup = prefab.GetComponentInChildren<PrototypeInventoryPickup>(true);

        if (pickup == null || !IsRuntimeManagedSupportItemId(pickup.ItemId))
        {
            return false;
        }

        itemId = pickup.ItemId;
        return true;
    }

    private static bool IsRuntimeManagedSupportItemId(string itemId)
    {
        return string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal)
            || string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal)
            || string.Equals(itemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal);
    }

    private void ApplySceneMarkerPlacementManagers(MainEscapeFloorAuthoring floorAuthoring, int runSeed)
    {
        if (floorAuthoring == null)
        {
            return;
        }

        RSceneMarkerPlacementManager[] placementManagers =
            floorAuthoring.GetComponentsInChildren<RSceneMarkerPlacementManager>(true);

        for (int index = 0; index < placementManagers.Length; index++)
        {
            RSceneMarkerPlacementManager placementManager = placementManagers[index];

            if (placementManager == null || !placementManager.isActiveAndEnabled)
            {
                continue;
            }

            int appliedCount = placementManager.Apply(runSeed);

            if (appliedCount > 0)
            {
                Debug.Log(
                    $"{nameof(RFloorDirector)} applied {appliedCount} scene marker placements from '{placementManager.name}'.",
                    placementManager);
            }
        }
    }
}

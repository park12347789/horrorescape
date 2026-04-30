#if UNITY_EDITOR
using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RSceneMarkerPlacementSupportPickupTools
{
    private const string ApplyLoadedFloorScenesMenuPath = "Tools/Main Escape Rebuild/Apply Support Pickup Scene Marker Managers To Loaded Floor Scenes";
    private const string ApplyActiveFloorSceneMenuPath = "Tools/Main Escape Rebuild/Apply Support Pickup Scene Marker Manager To Active Floor Scene";
    private const string ScenePlacementManagersRootName = "ScenePlacementManagers";
    private const string ManagerObjectName = "RSceneMarkerPlacementManager_SupportPickups";
    private const string AuthoringMarkersRootPath = "AuthoringMarkers/ItemPlacementMarkers";
    private const string RuntimePickupRootPath = "InteractiveProps/00_Pickups";
    private const string FlashlightBatteryPrefabPath = "Assets/Prefabs/Items/MainEscape/Inventory/Pickup_FlashlightBattery.prefab";
    private const string GlassBottlePrefabPath = "Assets/Prefabs/Items/MainEscape/Inventory/Pickup_GlassBottle.prefab";
    private const string MedkitPrefabPath = "Assets/Prefabs/Items/MainEscape/Inventory/Pickup_Medkit.prefab";

    private static readonly string[] SupportPickupRuleIds =
    {
        "SupportPickup_FlashlightBattery",
        "SupportPickup_GlassBottle",
        "SupportPickup_Medkit"
    };

    private static readonly string[] SupportPickupPrefabPaths =
    {
        FlashlightBatteryPrefabPath,
        GlassBottlePrefabPath,
        MedkitPrefabPath
    };

    [MenuItem(ApplyLoadedFloorScenesMenuPath)]
    private static void ApplySupportPickupPlacementManagersToLoadedFloorScenesFromMenu()
    {
        ApplySupportPickupPlacementManagersToLoadedFloorScenes();
    }

    [MenuItem(ApplyLoadedFloorScenesMenuPath, true)]
    private static bool ValidateApplySupportPickupPlacementManagersToLoadedFloorScenesFromMenu()
    {
        return HasLoadedFloorScene();
    }

    [MenuItem(ApplyActiveFloorSceneMenuPath)]
    private static void ApplySupportPickupPlacementManagerToActiveFloorSceneFromMenu()
    {
        ApplySupportPickupPlacementManagerToActiveScene();
    }

    [MenuItem(ApplyActiveFloorSceneMenuPath, true)]
    private static bool ValidateApplySupportPickupPlacementManagerToActiveFloorSceneFromMenu()
    {
        return FindFloorAuthoring(SceneManager.GetActiveScene()) != null;
    }

    public static void ApplySupportPickupPlacementManagerToActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} expects a loaded floor scene to be active before configuring support pickup placement.");
            return;
        }

        ConfigureSupportPickupPlacementManager(scene);
    }

    public static void ApplySupportPickupPlacementManagersToLoadedFloorScenes()
    {
        int configuredSceneCount = 0;

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            if (FindFloorAuthoring(scene) == null)
            {
                continue;
            }

            if (ConfigureSupportPickupPlacementManager(scene))
            {
                configuredSceneCount++;
            }
        }

        Debug.Log(
            $"{nameof(RSceneMarkerPlacementSupportPickupTools)} configured support pickup managers for {configuredSceneCount} loaded floor scene(s).");
    }

    private static bool ConfigureSupportPickupPlacementManager(Scene scene)
    {
        MainEscapeFloorAuthoring floorAuthoring = FindFloorAuthoring(scene);

        if (floorAuthoring == null)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} could not find {nameof(MainEscapeFloorAuthoring)} in '{scene.path}'.");
            return false;
        }

        Transform runtimeRoot = floorAuthoring.transform.Find(RuntimePickupRootPath);

        if (runtimeRoot == null)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} could not resolve runtimeRoot '{RuntimePickupRootPath}' under {nameof(MainEscapeFloorAuthoring)}.");
            return false;
        }

        Transform markerRoot = floorAuthoring.transform.Find(AuthoringMarkersRootPath);

        if (markerRoot == null)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} could not resolve markerRoot '{AuthoringMarkersRootPath}' under {nameof(MainEscapeFloorAuthoring)}.");
            return false;
        }

        int activeMarkerCount = CountActiveItemPlacementMarkers(markerRoot);

        if (activeMarkerCount <= 0)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} found no active item placement markers under '{AuthoringMarkersRootPath}'.");
            return false;
        }

        GameObject[] prefabAssets = LoadSupportPickupPrefabs();

        if (prefabAssets == null)
        {
            return false;
        }

        MainEscapeSupportItemPlacementQuota quota = floorAuthoring.SupportItemPlacementQuota;
        int quotaTargetCount = Mathf.Min(activeMarkerCount, quota.TotalCount);

        if (quotaTargetCount <= 0)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} found no support pickup quota for '{scene.path}'.");
            return false;
        }

        int[] supportPickupCounts = AllocateLargestRemainderCounts(
            quotaTargetCount,
            new[] { quota.BatteryCount, quota.GlassBottleCount, quota.MedkitCount });

        if (supportPickupCounts.Length != SupportPickupRuleIds.Length)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} could not allocate support pickup counts for '{scene.path}'.");
            return false;
        }

        Transform scenePlacementManagersRoot = EnsureChild(floorAuthoring.transform, ScenePlacementManagersRootName);
        RSceneMarkerPlacementManager placementManager = EnsureSupportPickupPlacementManager(
            floorAuthoring.transform,
            scenePlacementManagersRoot);

        if (placementManager == null)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} could not create or resolve {ManagerObjectName} under '{ScenePlacementManagersRootName}'.");
            return false;
        }

        bool changed = ConfigurePlacementManager(
            placementManager,
            runtimeRoot,
            markerRoot,
            prefabAssets,
            supportPickupCounts);

        EditorUtility.SetDirty(placementManager);
        EditorUtility.SetDirty(scenePlacementManagersRoot);
        EditorUtility.SetDirty(floorAuthoring);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            $"{nameof(RSceneMarkerPlacementSupportPickupTools)} configured '{BuildTransformPath(placementManager.transform)}' for '{scene.path}'. " +
            $"quota={quota.BatteryCount}/{quota.GlassBottleCount}/{quota.MedkitCount}, activeItemMarkers={activeMarkerCount}, " +
            $"target={quotaTargetCount}, counts={supportPickupCounts[0]}/{supportPickupCounts[1]}/{supportPickupCounts[2]}, changed={changed}.");
        return true;
    }

    private static MainEscapeFloorAuthoring FindFloorAuthoring(Scene scene)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            MainEscapeFloorAuthoring authoring = rootObjects[index].GetComponentInChildren<MainEscapeFloorAuthoring>(true);

            if (authoring != null)
            {
                return authoring;
            }
        }

        return null;
    }

    private static bool HasLoadedFloorScene()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            if (FindFloorAuthoring(SceneManager.GetSceneAt(index)) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject[] LoadSupportPickupPrefabs()
    {
        GameObject[] prefabs = new GameObject[SupportPickupPrefabPaths.Length];

        for (int index = 0; index < SupportPickupPrefabPaths.Length; index++)
        {
            string prefabPath = SupportPickupPrefabPaths[index];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"{nameof(RSceneMarkerPlacementSupportPickupTools)} could not load support pickup prefab at '{prefabPath}'.");
                return null;
            }

            prefabs[index] = prefab;
        }

        return prefabs;
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(childName);

        if (existing != null)
        {
            return existing;
        }

        GameObject childObject = new(childName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static RSceneMarkerPlacementManager EnsureSupportPickupPlacementManager(
        Transform floorAuthoringRoot,
        Transform scenePlacementManagersRoot)
    {
        if (scenePlacementManagersRoot == null || floorAuthoringRoot == null)
        {
            return null;
        }

        Transform managerTransform = scenePlacementManagersRoot.Find(ManagerObjectName);

        if (managerTransform == null)
        {
            RSceneMarkerPlacementManager[] placementManagers = floorAuthoringRoot.GetComponentsInChildren<RSceneMarkerPlacementManager>(true);

            for (int index = 0; index < placementManagers.Length; index++)
            {
                RSceneMarkerPlacementManager candidate = placementManagers[index];

                if (candidate != null && string.Equals(candidate.name, ManagerObjectName, StringComparison.Ordinal))
                {
                    managerTransform = candidate.transform;
                    break;
                }
            }
        }

        if (managerTransform == null)
        {
            GameObject managerObject = new(ManagerObjectName);
            managerTransform = managerObject.transform;
            managerTransform.SetParent(scenePlacementManagersRoot, false);
        }
        else if (managerTransform.parent != scenePlacementManagersRoot)
        {
            managerTransform.SetParent(scenePlacementManagersRoot, false);
        }

        if (managerTransform.name != ManagerObjectName)
        {
            managerTransform.name = ManagerObjectName;
        }

        if (managerTransform.localPosition != Vector3.zero)
        {
            managerTransform.localPosition = Vector3.zero;
        }

        if (managerTransform.localRotation != Quaternion.identity)
        {
            managerTransform.localRotation = Quaternion.identity;
        }

        if (managerTransform.localScale != Vector3.one)
        {
            managerTransform.localScale = Vector3.one;
        }

        RSceneMarkerPlacementManager placementManager = managerTransform.GetComponent<RSceneMarkerPlacementManager>();

        if (placementManager == null)
        {
            placementManager = managerTransform.gameObject.AddComponent<RSceneMarkerPlacementManager>();
        }

        return placementManager;
    }

    private static bool ConfigurePlacementManager(
        RSceneMarkerPlacementManager placementManager,
        Transform runtimeRoot,
        Transform markerRoot,
        GameObject[] prefabAssets,
        int[] supportPickupCounts)
    {
        if (placementManager == null || runtimeRoot == null || markerRoot == null || prefabAssets == null || supportPickupCounts == null)
        {
            return false;
        }

        bool changed = false;
        SerializedObject serializedManager = new(placementManager);
        serializedManager.Update();

        changed |= SetObjectReference(serializedManager, "runtimeRoot", runtimeRoot);
        changed |= SetBool(serializedManager, "clearRuntimeRootOnApply", true);

        SerializedProperty placementRulesProperty = serializedManager.FindProperty("placementRules");

        if (placementRulesProperty == null || !placementRulesProperty.isArray)
        {
            Debug.LogWarning(
                $"{nameof(RSceneMarkerPlacementSupportPickupTools)} could not find serialized array 'placementRules' on {nameof(RSceneMarkerPlacementManager)}.");
            return false;
        }

        placementRulesProperty.arraySize = SupportPickupRuleIds.Length;

        for (int index = 0; index < SupportPickupRuleIds.Length; index++)
        {
            SerializedProperty ruleProperty = placementRulesProperty.GetArrayElementAtIndex(index);
            changed |= ConfigureSupportPickupRule(
                ruleProperty,
                SupportPickupRuleIds[index],
                prefabAssets[index],
                markerRoot,
                supportPickupCounts[index]);
        }

        serializedManager.ApplyModifiedProperties();
        return changed;
    }

    private static bool ConfigureSupportPickupRule(
        SerializedProperty ruleProperty,
        string ruleId,
        GameObject prefab,
        Transform markerRoot,
        int count)
    {
        if (ruleProperty == null)
        {
            return false;
        }

        bool changed = false;
        changed |= SetString(ruleProperty, "ruleId", ruleId);
        changed |= SetInt(ruleProperty, "placementKind", (int)RSceneMarkerPlacementKind.SupportPickup);
        changed |= SetObjectReference(ruleProperty, "prefab", prefab);
        changed |= SetObjectReference(ruleProperty, "markerRoot", markerRoot);
        changed |= SetArraySize(ruleProperty, "markers", 0);
        changed |= SetInt(ruleProperty, "count", Mathf.Max(0, count));
        changed |= SetBool(ruleProperty, "includeInactiveMarkers", false);
        changed |= SetBool(ruleProperty, "avoidMarkersUsedByEarlierRules", true);
        changed |= SetBool(ruleProperty, "useMarkerRotation", true);
        changed |= SetInt(ruleProperty, "seedOffset", 0);
        return changed;
    }

    private static bool SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return false;
        }

        if (property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetObjectReference(SerializedProperty propertyRoot, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = propertyRoot.FindPropertyRelative(propertyName);

        if (property == null)
        {
            return false;
        }

        if (property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return false;
        }

        if (property.boolValue == value)
        {
            return false;
        }

        property.boolValue = value;
        return true;
    }

    private static bool SetBool(SerializedProperty propertyRoot, string propertyName, bool value)
    {
        SerializedProperty property = propertyRoot.FindPropertyRelative(propertyName);

        if (property == null)
        {
            return false;
        }

        if (property.boolValue == value)
        {
            return false;
        }

        property.boolValue = value;
        return true;
    }

    private static bool SetInt(SerializedProperty propertyRoot, string propertyName, int value)
    {
        SerializedProperty property = propertyRoot.FindPropertyRelative(propertyName);

        if (property == null)
        {
            return false;
        }

        if (property.intValue == value)
        {
            return false;
        }

        property.intValue = value;
        return true;
    }

    private static bool SetString(SerializedProperty propertyRoot, string propertyName, string value)
    {
        SerializedProperty property = propertyRoot.FindPropertyRelative(propertyName);

        if (property == null)
        {
            return false;
        }

        if (string.Equals(property.stringValue, value, StringComparison.Ordinal))
        {
            return false;
        }

        property.stringValue = value;
        return true;
    }

    private static bool SetArraySize(SerializedProperty propertyRoot, string propertyName, int size)
    {
        SerializedProperty property = propertyRoot.FindPropertyRelative(propertyName);

        if (property == null || !property.isArray)
        {
            return false;
        }

        if (property.arraySize == size)
        {
            return false;
        }

        property.arraySize = size;
        return true;
    }

    private static int[] AllocateLargestRemainderCounts(int markerCount, IReadOnlyList<int> quotaWeights)
    {
        if (markerCount <= 0 || quotaWeights == null || quotaWeights.Count == 0)
        {
            return Array.Empty<int>();
        }

        int totalWeight = 0;

        for (int index = 0; index < quotaWeights.Count; index++)
        {
            totalWeight += Mathf.Max(0, quotaWeights[index]);
        }

        if (totalWeight <= 0)
        {
            return Array.Empty<int>();
        }

        int[] counts = new int[quotaWeights.Count];
        int[] remainders = new int[quotaWeights.Count];
        int assignedCount = 0;

        for (int index = 0; index < quotaWeights.Count; index++)
        {
            int weight = Mathf.Max(0, quotaWeights[index]);
            int weightedCount = markerCount * weight;
            counts[index] = weightedCount / totalWeight;
            remainders[index] = weightedCount % totalWeight;
            assignedCount += counts[index];
        }

        int leftoverCount = markerCount - assignedCount;

        while (leftoverCount > 0)
        {
            int bestIndex = -1;

            for (int index = 0; index < quotaWeights.Count; index++)
            {
                if (remainders[index] < 0)
                {
                    continue;
                }

                if (bestIndex < 0)
                {
                    bestIndex = index;
                    continue;
                }

                int currentRemainder = remainders[index];
                int bestRemainder = remainders[bestIndex];

                if (currentRemainder > bestRemainder
                    || (currentRemainder == bestRemainder && quotaWeights[index] > quotaWeights[bestIndex])
                    || (currentRemainder == bestRemainder && quotaWeights[index] == quotaWeights[bestIndex] && index < bestIndex))
                {
                    bestIndex = index;
                }
            }

            if (bestIndex < 0)
            {
                break;
            }

            counts[bestIndex]++;
            remainders[bestIndex] = -1;
            leftoverCount--;
        }

        return counts;
    }

    private static int CountActiveItemPlacementMarkers(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        MainEscapeItemPlacementMarker[] markers = root.GetComponentsInChildren<MainEscapeItemPlacementMarker>(false);
        int count = 0;

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeItemPlacementMarker marker = markers[index];

            if (marker != null && marker.transform != root)
            {
                count++;
            }
        }

        return count;
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
        {
            return "<null>";
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
}
#endif

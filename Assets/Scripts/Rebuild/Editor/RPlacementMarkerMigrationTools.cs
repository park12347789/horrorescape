#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RPlacementMarkerMigrationTools
{
    private const string ItemMarkerPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/Markers/ItemPlacementMarker.prefab";
    private const string KeyMarkerPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/Markers/KeyPlacementMarker.prefab";
    private const string EnemyMarkerPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/Markers/EnemyPlacementMarker.prefab";
    private const string ChaserMarkerPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/Markers/ChaserPlacementMarker.prefab";
    private const string DangerMarkerPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/Markers/DangerMarker.prefab";
    private static readonly string[] LegacyResourceFloorPrefabPaths =
    {
        "Assets/Resources/Floors/MainEscape/1F.prefab",
        "Assets/Resources/Floors/MainEscape/2F.prefab",
        "Assets/Resources/Floors/MainEscape/3F.prefab",
        "Assets/Resources/Floors/MainEscape/4F.prefab",
        "Assets/Resources/Floors/MainEscape/5F.prefab"
    };

    [MenuItem("Tools/Main Escape Rebuild/Seed Placement Markers In Active Scene")]
    private static void SeedPlacementMarkersInActiveScene()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
        {
            Debug.LogWarning("Skipping placement marker seeding because there is no valid active authored floor scene.");
            return;
        }

        bool updated = SeedScenePlacementMarkers(activeScene, settings);

        if (updated)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncAuthoredFloorReferences();
        }
    }

    [MenuItem("Tools/Main Escape Rebuild/Seed Placement Markers For Open Authored Floor Scenes")]
    private static void SeedPlacementMarkersForOpenAuthoredFloorScenes()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        Scene[] scenes = GetOpenAuthoredFloorScenes();
        int updatedSceneCount = 0;

        for (int index = 0; index < scenes.Length; index++)
        {
            if (SeedScenePlacementMarkers(scenes[index], settings))
            {
                updatedSceneCount++;
            }
        }

        if (updatedSceneCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncAuthoredFloorReferences();
        }

        if (scenes.Length == 0)
        {
            Debug.LogWarning("Skipped placement marker seeding because no open authored floor scene contains MainEscapeFloorAuthoring.");
            return;
        }

        Debug.Log($"Seeded placement markers for {updatedSceneCount} open authored floor scene(s).");
    }

    [MenuItem("Tools/Main Escape Rebuild/Legacy/Seed Legacy Placement Markers For Resources Floor Prefabs")]
    private static void SeedLegacyPlacementMarkersForResourcesFloorPrefabs()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        int updatedPrefabCount = 0;

        for (int index = 0; index < LegacyResourceFloorPrefabPaths.Length; index++)
        {
            if (SeedLegacyPlacementMarkersInPrefab(LegacyResourceFloorPrefabPaths[index], settings))
            {
                updatedPrefabCount++;
            }
        }

        if (updatedPrefabCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncLegacyResourcesFloorPrefabReferences();
        }

        Debug.Log($"Seeded legacy placement markers for {updatedPrefabCount} Resources floor prefab(s). Open authored scenes were not touched.");
    }

    public static void RunSeedLegacyPlacementMarkersForResourcesFloorPrefabs()
    {
        SeedLegacyPlacementMarkersForResourcesFloorPrefabs();
    }

    [Obsolete("Use RunSeedLegacyPlacementMarkersForResourcesFloorPrefabs for legacy Resources floor prefab quarantine tools.")]
    public static void RunSeedLegacyPlacementMarkersForAuthoredFloorPrefabs()
    {
        RunSeedLegacyPlacementMarkersForResourcesFloorPrefabs();
    }

    [MenuItem("Tools/Main Escape Rebuild/Cleanup Legacy Placement Sources In Active Scene")]
    private static void CleanupLegacyPlacementSourcesInActiveScene()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
        {
            Debug.LogWarning("Skipping legacy placement cleanup because there is no valid active authored floor scene.");
            return;
        }

        bool updated = CleanupLegacyPlacementSources(activeScene, settings);

        if (updated)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncAuthoredFloorReferences();
        }
    }

    [MenuItem("Tools/Main Escape Rebuild/Cleanup Legacy Placement Sources For Open Authored Floor Scenes")]
    private static void CleanupLegacyPlacementSourcesForOpenAuthoredFloorScenes()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        Scene[] scenes = GetOpenAuthoredFloorScenes();
        int updatedSceneCount = 0;

        for (int index = 0; index < scenes.Length; index++)
        {
            if (CleanupLegacyPlacementSources(scenes[index], settings))
            {
                updatedSceneCount++;
            }
        }

        if (updatedSceneCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncAuthoredFloorReferences();
        }

        if (scenes.Length == 0)
        {
            Debug.LogWarning("Skipped legacy placement cleanup because no open authored floor scene contains MainEscapeFloorAuthoring.");
            return;
        }

        Debug.Log($"Cleaned legacy placement sources in {updatedSceneCount} open authored floor scene(s).");
    }

    [MenuItem("Tools/Main Escape Rebuild/Legacy/Cleanup Legacy Placement Sources For Resources Floor Prefabs")]
    private static void CleanupLegacyPlacementSourcesForResourcesFloorPrefabs()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        int updatedPrefabCount = 0;

        for (int index = 0; index < LegacyResourceFloorPrefabPaths.Length; index++)
        {
            if (CleanupLegacyPlacementSourcesInPrefab(LegacyResourceFloorPrefabPaths[index], settings))
            {
                updatedPrefabCount++;
            }
        }

        if (updatedPrefabCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncLegacyResourcesFloorPrefabReferences();
        }

        Debug.Log($"Cleaned legacy placement sources in {updatedPrefabCount} Resources floor prefab(s). Open authored scenes were not touched.");
    }

    public static void RunCleanupLegacyPlacementSourcesForResourcesFloorPrefabs()
    {
        CleanupLegacyPlacementSourcesForResourcesFloorPrefabs();
    }

    [Obsolete("Use RunCleanupLegacyPlacementSourcesForResourcesFloorPrefabs for legacy Resources floor prefab quarantine tools.")]
    public static void RunCleanupLegacyPlacementSourcesForAuthoredFloorPrefabs()
    {
        RunCleanupLegacyPlacementSourcesForResourcesFloorPrefabs();
    }

    [MenuItem("Tools/Main Escape Rebuild/Normalize Danger Markers In Active Scene")]
    private static void NormalizeDangerMarkersInActiveScene()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
        {
            Debug.LogWarning("Skipping danger marker normalization because there is no valid active authored floor scene.");
            return;
        }

        bool updated = NormalizeDangerMarkers(activeScene, settings);

        if (updated)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncAuthoredFloorReferences();
        }
    }

    [MenuItem("Tools/Main Escape Rebuild/Normalize Danger Markers For Open Authored Floor Scenes")]
    private static void NormalizeDangerMarkersForOpenAuthoredFloorScenes()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        Scene[] scenes = GetOpenAuthoredFloorScenes();
        int updatedSceneCount = 0;

        for (int index = 0; index < scenes.Length; index++)
        {
            if (NormalizeDangerMarkers(scenes[index], settings))
            {
                updatedSceneCount++;
            }
        }

        if (updatedSceneCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RAuthoringReferenceCacheTools.SyncAuthoredFloorReferences();
        }

        if (scenes.Length == 0)
        {
            Debug.LogWarning("Skipped danger marker normalization because no open authored floor scene contains MainEscapeFloorAuthoring.");
            return;
        }

        Debug.Log($"Normalized danger markers in {updatedSceneCount} open authored floor scene(s).");
    }

    private static Scene[] GetOpenAuthoredFloorScenes()
    {
        List<Scene> scenes = new();
        Scene activeScene = SceneManager.GetActiveScene();

        AddOpenAuthoredFloorScene(activeScene, scenes);

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            AddOpenAuthoredFloorScene(SceneManager.GetSceneAt(index), scenes);
        }

        return scenes.ToArray();
    }

    private static void AddOpenAuthoredFloorScene(Scene scene, List<Scene> scenes)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path) || scenes.Contains(scene))
        {
            return;
        }

        if (!SceneHasFloorAuthoring(scene))
        {
            return;
        }

        scenes.Add(scene);
    }

    private static bool SceneHasFloorAuthoring(Scene scene)
    {
        MainEscapeFloorAuthoring[] authorings = UnityEngine.Object.FindObjectsByType<MainEscapeFloorAuthoring>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int index = 0; index < authorings.Length; index++)
        {
            MainEscapeFloorAuthoring authoring = authorings[index];

            if (authoring != null && authoring.gameObject.scene == scene)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SeedScenePlacementMarkers(Scene scene, MainEscapeRuntimeSettings settings)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("Skipping placement marker seeding because the scene is not an open saved authored floor scene.");
            return false;
        }

        MainEscapeFloorAuthoring[] authorings = UnityEngine.Object.FindObjectsByType<MainEscapeFloorAuthoring>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (authorings == null || authorings.Length == 0)
        {
            Debug.LogWarning($"Skipping placement marker seeding because '{scene.path}' has no MainEscapeFloorAuthoring.");
            return false;
        }

        bool sceneChanged = false;

        for (int index = 0; index < authorings.Length; index++)
        {
            MainEscapeFloorAuthoring floorAuthoring = authorings[index];

            if (floorAuthoring == null || floorAuthoring.gameObject.scene != scene)
            {
                continue;
            }

            sceneChanged |= SeedFloorPlacementMarkers(scene, floorAuthoring, settings);
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return sceneChanged;
    }

    private static bool CleanupLegacyPlacementSources(Scene scene, MainEscapeRuntimeSettings settings)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("Skipping legacy placement cleanup because the scene is not an open saved authored floor scene.");
            return false;
        }

        MainEscapeFloorAuthoring[] authorings = UnityEngine.Object.FindObjectsByType<MainEscapeFloorAuthoring>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (authorings == null || authorings.Length == 0)
        {
            Debug.LogWarning($"Skipping legacy placement cleanup because '{scene.path}' has no MainEscapeFloorAuthoring.");
            return false;
        }

        bool sceneChanged = false;

        for (int index = 0; index < authorings.Length; index++)
        {
            MainEscapeFloorAuthoring floorAuthoring = authorings[index];

            if (floorAuthoring == null || floorAuthoring.gameObject.scene != scene)
            {
                continue;
            }

            sceneChanged |= CleanupLegacyPlacementSources(scene, floorAuthoring, settings);
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return sceneChanged;
    }

    private static bool SeedLegacyPlacementMarkersInPrefab(string prefabPath, MainEscapeRuntimeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) || !File.Exists(prefabPath))
        {
            Debug.LogWarning($"Skipping prefab legacy placement seeding because prefab is missing at '{prefabPath}'.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            MainEscapeFloorAuthoring floorAuthoring = prefabRoot != null
                ? prefabRoot.GetComponent<MainEscapeFloorAuthoring>()
                : null;

            if (floorAuthoring == null)
            {
                Debug.LogWarning($"Skipping prefab legacy placement seeding because '{prefabPath}' has no MainEscapeFloorAuthoring.");
                return false;
            }

            bool changed = SeedLegacyPlacementMarkers(prefabRoot, floorAuthoring, settings);

            if (!changed)
            {
                return false;
            }

            EditorUtility.SetDirty(floorAuthoring);
            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            return true;
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private static bool CleanupLegacyPlacementSourcesInPrefab(string prefabPath, MainEscapeRuntimeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) || !File.Exists(prefabPath))
        {
            Debug.LogWarning($"Skipping prefab legacy placement cleanup because prefab is missing at '{prefabPath}'.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            MainEscapeFloorAuthoring floorAuthoring = prefabRoot != null
                ? prefabRoot.GetComponent<MainEscapeFloorAuthoring>()
                : null;

            if (floorAuthoring == null)
            {
                Debug.LogWarning($"Skipping prefab legacy placement cleanup because '{prefabPath}' has no MainEscapeFloorAuthoring.");
                return false;
            }

            bool changed = CleanupLegacyPlacementSources(prefabRoot, floorAuthoring, settings);

            if (!changed)
            {
                return false;
            }

            EditorUtility.SetDirty(floorAuthoring);
            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            return true;
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private static bool NormalizeDangerMarkers(Scene scene, MainEscapeRuntimeSettings settings)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("Skipping danger marker normalization because the scene is not an open saved authored floor scene.");
            return false;
        }

        MainEscapeFloorAuthoring[] authorings = UnityEngine.Object.FindObjectsByType<MainEscapeFloorAuthoring>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (authorings == null || authorings.Length == 0)
        {
            Debug.LogWarning($"Skipping danger marker normalization because '{scene.path}' has no MainEscapeFloorAuthoring.");
            return false;
        }

        bool sceneChanged = false;

        for (int index = 0; index < authorings.Length; index++)
        {
            MainEscapeFloorAuthoring floorAuthoring = authorings[index];

            if (floorAuthoring == null || floorAuthoring.gameObject.scene != scene)
            {
                continue;
            }

            sceneChanged |= NormalizeDangerMarkers(scene, floorAuthoring, settings);
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return sceneChanged;
    }

    private static bool SeedFloorPlacementMarkers(Scene scene, MainEscapeFloorAuthoring floorAuthoring, MainEscapeRuntimeSettings settings)
    {
        if (!scene.IsValid() || floorAuthoring == null || settings == null)
        {
            return false;
        }

        GameObject itemMarkerPrefab = LoadPrefab(ItemMarkerPrefabPath);
        GameObject keyMarkerPrefab = LoadPrefab(KeyMarkerPrefabPath);
        GameObject enemyMarkerPrefab = LoadPrefab(EnemyMarkerPrefabPath);
        GameObject chaserMarkerPrefab = LoadPrefab(ChaserMarkerPrefabPath);

        if (itemMarkerPrefab == null || keyMarkerPrefab == null || enemyMarkerPrefab == null || chaserMarkerPrefab == null)
        {
            return false;
        }

        bool changed = false;
        Transform authoringMarkersRoot = EnsureChild(floorAuthoring.transform, settings.AuthoringMarkersRootName, ref changed);
        Transform itemRoot = EnsureChild(authoringMarkersRoot, settings.ItemPlacementMarkersRootName, ref changed);
        Transform keyRoot = EnsureChild(authoringMarkersRoot, settings.KeyPlacementMarkersRootName, ref changed);
        Transform enemyRoot = EnsureChild(authoringMarkersRoot, settings.EnemyPlacementMarkersRootName, ref changed);
        Transform chaserRoot = EnsureChild(authoringMarkersRoot, settings.ChaserPlacementMarkersRootName, ref changed);

        ClearChildren(itemRoot, ref changed);
        ClearChildren(keyRoot, ref changed);
        ClearChildren(enemyRoot, ref changed);
        ClearChildren(chaserRoot, ref changed);

        PrototypeInventoryPickup[] supportPickups = CollectSupportItemPickups(scene);
        MainEscapeKeyPickup[] keyPickups = CollectKeyPickups(scene);
        Transform[] patrolPoints = CollectDirectChildMarkers(authoringMarkersRoot, settings.PatrolSpawnRootName);
        Transform[] sentryPoints = CollectDirectChildMarkers(authoringMarkersRoot, settings.SentryGuardsRootName);
        Transform[] chaserPoints = CollectNamedMarkers(authoringMarkersRoot, settings.StalkerSpawnMarkerName);

        int batteryCount = 0;
        int glassBottleCount = 0;
        int medkitCount = 0;

        for (int index = 0; index < supportPickups.Length; index++)
        {
            PrototypeInventoryPickup pickup = supportPickups[index];
            string markerName = $"SupportItemCandidate_{index + 1:00}";
            MainEscapeItemPlacementMarker marker = InstantiateItemMarker(itemMarkerPrefab, itemRoot, markerName, pickup.transform.position);
            marker.Configure(MainEscapeItemPlacementCategory.SupportItem, markerName);
            changed = true;

            switch (pickup.ItemId)
            {
                case PrototypeItemCatalog.FlashlightBatteryItemId:
                    batteryCount++;
                    break;
                case PrototypeItemCatalog.GlassBottleItemId:
                    glassBottleCount++;
                    break;
                case PrototypeItemCatalog.MedkitItemId:
                    medkitCount++;
                    break;
            }
        }

        for (int index = 0; index < keyPickups.Length; index++)
        {
            MainEscapeKeyPickup pickup = keyPickups[index];
            string markerName = $"KeyCandidate_{index + 1:00}";
            MainEscapeItemPlacementMarker marker = InstantiateItemMarker(keyMarkerPrefab, keyRoot, markerName, pickup.transform.position);
            marker.Configure(MainEscapeItemPlacementCategory.Key, markerName);
            changed = true;
        }

        for (int index = 0; index < patrolPoints.Length; index++)
        {
            Transform source = patrolPoints[index];
            string markerName = $"PatrolCandidate_{index + 1:00}";
            MainEscapeEnemyPlacementMarker marker = InstantiateEnemyMarker(enemyMarkerPrefab, enemyRoot, markerName, source.position, source.rotation);
            marker.Configure(MainEscapeEnemyPlacementKind.Patrol, markerName);
            changed = true;
        }

        for (int index = 0; index < sentryPoints.Length; index++)
        {
            Transform source = sentryPoints[index];
            string markerName = $"SentryCandidate_{index + 1:00}";
            MainEscapeEnemyPlacementMarker marker = InstantiateEnemyMarker(enemyMarkerPrefab, enemyRoot, markerName, source.position, source.rotation);
            marker.Configure(MainEscapeEnemyPlacementKind.Sentry, markerName);
            changed = true;
        }

        for (int index = 0; index < chaserPoints.Length; index++)
        {
            Transform source = chaserPoints[index];
            string markerName = $"ChaserCandidate_{index + 1:00}";
            MainEscapeEnemyPlacementMarker marker = InstantiateEnemyMarker(chaserMarkerPrefab, chaserRoot, markerName, source.position, source.rotation);
            marker.Configure(MainEscapeEnemyPlacementKind.Chaser, markerName);
            changed = true;
        }

        ApplyPlacementQuotas(
            floorAuthoring,
            batteryCount,
            glassBottleCount,
            medkitCount,
            patrolPoints.Length,
            sentryPoints.Length,
            chaserPoints.Length);

        floorAuthoring.CacheReferencesFromHierarchy();
        EditorUtility.SetDirty(floorAuthoring);
        return changed;
    }

    private static bool SeedLegacyPlacementMarkers(
        GameObject prefabRoot,
        MainEscapeFloorAuthoring floorAuthoring,
        MainEscapeRuntimeSettings settings)
    {
        if (prefabRoot == null || floorAuthoring == null || settings == null)
        {
            return false;
        }

        GameObject itemMarkerPrefab = LoadPrefab(ItemMarkerPrefabPath);
        GameObject keyMarkerPrefab = LoadPrefab(KeyMarkerPrefabPath);
        GameObject enemyMarkerPrefab = LoadPrefab(EnemyMarkerPrefabPath);
        GameObject chaserMarkerPrefab = LoadPrefab(ChaserMarkerPrefabPath);

        if (itemMarkerPrefab == null || keyMarkerPrefab == null || enemyMarkerPrefab == null || chaserMarkerPrefab == null)
        {
            return false;
        }

        bool changed = false;
        Transform authoringMarkersRoot = EnsureChild(floorAuthoring.transform, settings.AuthoringMarkersRootName, ref changed);
        Transform itemRoot = EnsureChild(authoringMarkersRoot, settings.ItemPlacementMarkersRootName, ref changed);
        Transform keyRoot = EnsureChild(authoringMarkersRoot, settings.KeyPlacementMarkersRootName, ref changed);
        Transform enemyRoot = EnsureChild(authoringMarkersRoot, settings.EnemyPlacementMarkersRootName, ref changed);
        Transform chaserRoot = EnsureChild(authoringMarkersRoot, settings.ChaserPlacementMarkersRootName, ref changed);

        ClearChildren(itemRoot, ref changed);
        ClearChildren(keyRoot, ref changed);
        ClearChildren(enemyRoot, ref changed);
        ClearChildren(chaserRoot, ref changed);

        List<LegacySupportItemSeed> supportSeeds = CollectLegacySupportItemSeeds(prefabRoot, floorAuthoring, settings);
        Transform keySeed = ResolveLegacyKeyPlacementSeed(prefabRoot, floorAuthoring, settings);
        Transform[] patrolPoints = CollectDirectChildMarkers(authoringMarkersRoot, settings.PatrolSpawnRootName);
        Transform[] sentryPoints = CollectDirectChildMarkers(authoringMarkersRoot, settings.SentryGuardsRootName);
        Transform[] chaserPoints = CollectNamedMarkers(authoringMarkersRoot, settings.StalkerSpawnMarkerName);

        int batteryCount = 0;
        int glassBottleCount = 0;
        int medkitCount = 0;

        for (int index = 0; index < supportSeeds.Count; index++)
        {
            LegacySupportItemSeed seed = supportSeeds[index];
            string markerName = BuildSupportItemMarkerName(seed.ItemId, index);
            MainEscapeItemPlacementMarker marker = InstantiateItemMarker(itemMarkerPrefab, itemRoot, markerName, seed.Position);
            marker.Configure(MainEscapeItemPlacementCategory.SupportItem, markerName);
            changed = true;

            switch (seed.ItemId)
            {
                case PrototypeItemCatalog.FlashlightBatteryItemId:
                    batteryCount++;
                    break;
                case PrototypeItemCatalog.GlassBottleItemId:
                    glassBottleCount++;
                    break;
                case PrototypeItemCatalog.MedkitItemId:
                    medkitCount++;
                    break;
            }
        }

        if (keySeed != null)
        {
            const string markerName = "KeyCandidate_00";
            MainEscapeItemPlacementMarker marker = InstantiateItemMarker(keyMarkerPrefab, keyRoot, markerName, keySeed.position);
            marker.Configure(MainEscapeItemPlacementCategory.Key, markerName);
            changed = true;
        }

        for (int index = 0; index < patrolPoints.Length; index++)
        {
            Transform source = patrolPoints[index];
            string markerName = $"PatrolCandidate_{index + 1:00}";
            MainEscapeEnemyPlacementMarker marker = InstantiateEnemyMarker(enemyMarkerPrefab, enemyRoot, markerName, source.position, source.rotation);
            marker.Configure(MainEscapeEnemyPlacementKind.Patrol, markerName);
            changed = true;
        }

        for (int index = 0; index < sentryPoints.Length; index++)
        {
            Transform source = sentryPoints[index];
            string markerName = $"SentryCandidate_{index + 1:00}";
            MainEscapeEnemyPlacementMarker marker = InstantiateEnemyMarker(enemyMarkerPrefab, enemyRoot, markerName, source.position, source.rotation);
            marker.Configure(MainEscapeEnemyPlacementKind.Sentry, markerName);
            changed = true;
        }

        for (int index = 0; index < chaserPoints.Length; index++)
        {
            Transform source = chaserPoints[index];
            string markerName = $"ChaserCandidate_{index + 1:00}";
            MainEscapeEnemyPlacementMarker marker = InstantiateEnemyMarker(chaserMarkerPrefab, chaserRoot, markerName, source.position, source.rotation);
            marker.Configure(MainEscapeEnemyPlacementKind.Chaser, markerName);
            changed = true;
        }

        ApplyPlacementQuotas(
            floorAuthoring,
            batteryCount,
            glassBottleCount,
            medkitCount,
            patrolPoints.Length,
            sentryPoints.Length,
            chaserPoints.Length);

        floorAuthoring.CacheReferencesFromHierarchy();
        return changed;
    }

    private static bool CleanupLegacyPlacementSources(Scene scene, MainEscapeFloorAuthoring floorAuthoring, MainEscapeRuntimeSettings settings)
    {
        if (!scene.IsValid() || floorAuthoring == null || settings == null)
        {
            return false;
        }

        bool changed = false;
        Transform authoringMarkersRoot = FindDirectChild(floorAuthoring.transform, settings.AuthoringMarkersRootName);
        Transform pickupMarkersRoot = FindDirectChild(floorAuthoring.transform, settings.PickupMarkersRootName);

        DestroyObjects(CollectSupportItemPickups(scene), ref changed);
        DestroyObjects(CollectKeyPickups(scene), ref changed);
        DestroyObjects(CollectLegacyGlassTrapPanels(scene), ref changed);

        ClearChildren(pickupMarkersRoot, ref changed);
        DestroyLegacyPickupMarkersRootIfUnused(floorAuthoring, pickupMarkersRoot, ref changed);
        Transform patrolSpawnRoot = FindDirectChild(authoringMarkersRoot, settings.PatrolSpawnRootName);
        Transform patrolSpawnCandidatesRoot = FindDirectChild(authoringMarkersRoot, settings.PatrolSpawnCandidatesRootName);
        Transform sentryGuardsRoot = FindDirectChild(authoringMarkersRoot, settings.SentryGuardsRootName);
        Transform sentrySpawnCandidatesRoot = FindDirectChild(authoringMarkersRoot, settings.SentrySpawnCandidatesRootName);
        ClearChildren(patrolSpawnRoot, ref changed);
        ClearChildren(patrolSpawnCandidatesRoot, ref changed);
        ClearChildren(sentryGuardsRoot, ref changed);
        ClearChildren(sentrySpawnCandidatesRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, patrolSpawnRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, patrolSpawnCandidatesRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, sentryGuardsRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, sentrySpawnCandidatesRoot, ref changed);
        DestroyTransforms(CollectNamedMarkers(authoringMarkersRoot, settings.StalkerSpawnMarkerName), ref changed);
        DestroyTransforms(CollectNamedMarkers(floorAuthoring.transform, "DisabledPatrolSpawns"), ref changed);

        floorAuthoring.CacheReferencesFromHierarchy();
        EditorUtility.SetDirty(floorAuthoring);
        return changed;
    }

    private static bool CleanupLegacyPlacementSources(
        GameObject prefabRoot,
        MainEscapeFloorAuthoring floorAuthoring,
        MainEscapeRuntimeSettings settings)
    {
        if (prefabRoot == null || floorAuthoring == null || settings == null)
        {
            return false;
        }

        bool changed = false;
        Transform authoringMarkersRoot = FindDirectChild(floorAuthoring.transform, settings.AuthoringMarkersRootName);
        Transform pickupMarkersRoot = FindDirectChild(floorAuthoring.transform, settings.PickupMarkersRootName);
        Transform keyPlacementMarkersRoot = FindDirectChild(authoringMarkersRoot, settings.KeyPlacementMarkersRootName);
        PrototypeInventoryPickup[] supportPickups = CollectSupportItemPickups(prefabRoot);
        MainEscapeKeyPickup[] keyPickups = CollectKeyPickups(prefabRoot);
        bool hasSupportMarkers = floorAuthoring.HasSupportItemPlacementMarkers();
        bool hasKeyMarkers = floorAuthoring.HasKeyPlacementMarkers();
        bool hasLegacyKeySource = ResolveLegacyKeyPlacementSeed(prefabRoot, floorAuthoring, settings) != null;

        if (hasSupportMarkers)
        {
            DestroyObjects(supportPickups, ref changed);
            DestroyTransforms(CollectNamedMarkers(floorAuthoring.transform, settings.BatteryMarkerNames), ref changed);
            DestroyTransforms(CollectNamedMarkers(floorAuthoring.transform, settings.GlassBottleMarkerNames), ref changed);
        }

        if (hasKeyMarkers)
        {
            DestroyObjects(keyPickups, ref changed);
            DestroyTransforms(CollectNamedMarkers(floorAuthoring.transform, settings.KeyPickupSearchNames), ref changed);
        }

        if (hasSupportMarkers || hasKeyMarkers || !hasLegacyKeySource)
        {
            ClearChildren(pickupMarkersRoot, ref changed);

            if ((hasSupportMarkers && hasKeyMarkers) || (hasSupportMarkers && !hasLegacyKeySource))
            {
                DestroyLegacyPickupMarkersRootIfRedundant(pickupMarkersRoot, ref changed);
            }
        }

        DestroyEmptyKeyPlacementRootIfUnused(keyPlacementMarkersRoot, hasKeyMarkers, hasLegacyKeySource, ref changed);

        Transform patrolSpawnRoot = FindDirectChild(authoringMarkersRoot, settings.PatrolSpawnRootName);
        Transform patrolSpawnCandidatesRoot = FindDirectChild(authoringMarkersRoot, settings.PatrolSpawnCandidatesRootName);
        Transform sentryGuardsRoot = FindDirectChild(authoringMarkersRoot, settings.SentryGuardsRootName);
        Transform sentrySpawnCandidatesRoot = FindDirectChild(authoringMarkersRoot, settings.SentrySpawnCandidatesRootName);
        ClearChildren(patrolSpawnRoot, ref changed);
        ClearChildren(patrolSpawnCandidatesRoot, ref changed);
        ClearChildren(sentryGuardsRoot, ref changed);
        ClearChildren(sentrySpawnCandidatesRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, patrolSpawnRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, patrolSpawnCandidatesRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, sentryGuardsRoot, ref changed);
        DestroyLegacyEnemyRootIfUnused(floorAuthoring, sentrySpawnCandidatesRoot, ref changed);
        DestroyTransforms(CollectNamedMarkers(authoringMarkersRoot, settings.StalkerSpawnMarkerName), ref changed);
        DestroyTransforms(CollectNamedMarkers(floorAuthoring.transform, "DisabledPatrolSpawns"), ref changed);

        floorAuthoring.CacheReferencesFromHierarchy();
        return changed;
    }

    private static bool NormalizeDangerMarkers(Scene scene, MainEscapeFloorAuthoring floorAuthoring, MainEscapeRuntimeSettings settings)
    {
        if (!scene.IsValid() || floorAuthoring == null || settings == null)
        {
            return false;
        }

        GameObject dangerMarkerPrefab = LoadPrefab(DangerMarkerPrefabPath);

        if (dangerMarkerPrefab == null)
        {
            return false;
        }

        bool changed = false;
        Transform authoringMarkersRoot = EnsureChild(floorAuthoring.transform, settings.AuthoringMarkersRootName, ref changed);
        Transform dangerRoot = EnsureChild(authoringMarkersRoot, settings.DangerMarkersRootName, ref changed);

        if (dangerRoot == null || dangerRoot.childCount == 0)
        {
            changed |= ApplyGlassTrapPlacementCount(floorAuthoring, CountLegacyGlassTrapPanels(scene));
            floorAuthoring.CacheReferencesFromHierarchy();
            EditorUtility.SetDirty(floorAuthoring);
            return changed;
        }

        if (!NeedsDangerMarkerNormalization(dangerRoot))
        {
            changed |= ApplyGlassTrapPlacementCount(floorAuthoring, CountLegacyGlassTrapPanels(scene));
            floorAuthoring.CacheReferencesFromHierarchy();
            EditorUtility.SetDirty(floorAuthoring);
            return changed;
        }

        List<DangerMarkerSnapshot> snapshots = new(dangerRoot.childCount);

        for (int index = 0; index < dangerRoot.childCount; index++)
        {
            Transform child = dangerRoot.GetChild(index);

            if (child == null)
            {
                continue;
            }

            snapshots.Add(new DangerMarkerSnapshot(
                child.name,
                child.position,
                child.rotation,
                child.localScale));
        }

        ClearChildren(dangerRoot, ref changed);

        for (int index = 0; index < snapshots.Count; index++)
        {
            DangerMarkerSnapshot snapshot = snapshots[index];
            GameObject instance = PrefabUtility.InstantiatePrefab(dangerMarkerPrefab, dangerRoot.gameObject.scene) as GameObject;

            if (instance == null)
            {
                continue;
            }

            instance.name = snapshot.Name;
            instance.transform.SetParent(dangerRoot, false);
            instance.transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            instance.transform.localScale = snapshot.LocalScale;
            changed = true;
        }

        changed |= ApplyGlassTrapPlacementCount(floorAuthoring, CountLegacyGlassTrapPanels(scene));
        floorAuthoring.CacheReferencesFromHierarchy();
        EditorUtility.SetDirty(floorAuthoring);
        return changed;
    }

    private static GameObject LoadPrefab(string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

        if (prefab == null)
        {
            Debug.LogWarning($"Placement marker prefab is missing at '{assetPath}'.");
        }

        return prefab;
    }

    private static Transform EnsureChild(Transform parent, string childName, ref bool changed)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform child = parent.Find(childName);

        if (child != null)
        {
            return child;
        }

        GameObject childObject = new(childName);
        childObject.transform.SetParent(parent, false);
        changed = true;
        return childObject.transform;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        return parent.Find(childName);
    }

    private static void ClearChildren(Transform root, ref bool changed)
    {
        if (root == null)
        {
            return;
        }

        for (int index = root.childCount - 1; index >= 0; index--)
        {
            Transform child = root.GetChild(index);

            if (child == null)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
            changed = true;
        }
    }

    private static void DestroyObjects<T>(IReadOnlyList<T> objects, ref bool changed)
        where T : UnityEngine.Object
    {
        if (objects == null)
        {
            return;
        }

        for (int index = 0; index < objects.Count; index++)
        {
            T instance = objects[index];

            if (instance == null)
            {
                continue;
            }

            if (instance is Component component)
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
                changed = true;
                continue;
            }

            UnityEngine.Object.DestroyImmediate(instance);
            changed = true;
        }
    }

    private static void DestroyTransforms(IReadOnlyList<Transform> transforms, ref bool changed)
    {
        if (transforms == null)
        {
            return;
        }

        for (int index = 0; index < transforms.Count; index++)
        {
            Transform transform = transforms[index];

            if (transform == null)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(transform.gameObject);
            changed = true;
        }
    }

    private static void DestroyLegacyPickupMarkersRootIfUnused(
        MainEscapeFloorAuthoring floorAuthoring,
        Transform pickupMarkersRoot,
        ref bool changed)
    {
        if (floorAuthoring == null
            || pickupMarkersRoot == null
            || pickupMarkersRoot.childCount > 0
            || !floorAuthoring.HasSupportItemPlacementMarkers()
            || !floorAuthoring.HasKeyPlacementMarkers())
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(pickupMarkersRoot.gameObject);
        changed = true;
    }

    private static void DestroyLegacyEnemyRootIfUnused(
        MainEscapeFloorAuthoring floorAuthoring,
        Transform legacyRoot,
        ref bool changed)
    {
        if (floorAuthoring == null
            || legacyRoot == null
            || legacyRoot.childCount > 0
            || !floorAuthoring.HasEnemyPlacementMarkers())
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(legacyRoot.gameObject);
        changed = true;
    }

    private static void DestroyLegacyPickupMarkersRootIfRedundant(Transform pickupMarkersRoot, ref bool changed)
    {
        if (pickupMarkersRoot == null || pickupMarkersRoot.childCount > 0)
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(pickupMarkersRoot.gameObject);
        changed = true;
    }

    private static void DestroyEmptyKeyPlacementRootIfUnused(
        Transform keyPlacementMarkersRoot,
        bool hasKeyMarkers,
        bool hasLegacyKeySource,
        ref bool changed)
    {
        if (keyPlacementMarkersRoot == null
            || keyPlacementMarkersRoot.childCount > 0
            || hasKeyMarkers
            || hasLegacyKeySource)
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(keyPlacementMarkersRoot.gameObject);
        changed = true;
    }

    private static PrototypeInventoryPickup[] CollectSupportItemPickups(Scene scene)
    {
        PrototypeInventoryPickup[] pickups = UnityEngine.Object.FindObjectsByType<PrototypeInventoryPickup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<PrototypeInventoryPickup> results = new();

        for (int index = 0; index < pickups.Length; index++)
        {
            PrototypeInventoryPickup pickup = pickups[index];

            if (pickup == null || pickup.gameObject.scene != scene)
            {
                continue;
            }

            if (!IsMigratableSupportItemPickup(pickup))
            {
                continue;
            }

            results.Add(pickup);
        }

        results.Sort(CompareByPositionThenName);
        return results.ToArray();
    }

    private static PrototypeInventoryPickup[] CollectSupportItemPickups(GameObject prefabRoot)
    {
        if (prefabRoot == null)
        {
            return Array.Empty<PrototypeInventoryPickup>();
        }

        PrototypeInventoryPickup[] pickups = prefabRoot.GetComponentsInChildren<PrototypeInventoryPickup>(true);
        List<PrototypeInventoryPickup> results = new();

        for (int index = 0; index < pickups.Length; index++)
        {
            PrototypeInventoryPickup pickup = pickups[index];

            if (!IsMigratableSupportItemPickup(pickup))
            {
                continue;
            }

            results.Add(pickup);
        }

        results.Sort(CompareByPositionThenName);
        return results.ToArray();
    }

    private static MainEscapeKeyPickup[] CollectKeyPickups(Scene scene)
    {
        MainEscapeKeyPickup[] pickups = UnityEngine.Object.FindObjectsByType<MainEscapeKeyPickup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<MainEscapeKeyPickup> results = new();

        for (int index = 0; index < pickups.Length; index++)
        {
            MainEscapeKeyPickup pickup = pickups[index];

            if (pickup == null || pickup.gameObject.scene != scene)
            {
                continue;
            }

            results.Add(pickup);
        }

        results.Sort(CompareByPositionThenName);
        return results.ToArray();
    }

    private static MainEscapeKeyPickup[] CollectKeyPickups(GameObject prefabRoot)
    {
        if (prefabRoot == null)
        {
            return Array.Empty<MainEscapeKeyPickup>();
        }

        MainEscapeKeyPickup[] pickups = prefabRoot.GetComponentsInChildren<MainEscapeKeyPickup>(true);
        List<MainEscapeKeyPickup> results = new(pickups.Length);

        for (int index = 0; index < pickups.Length; index++)
        {
            MainEscapeKeyPickup pickup = pickups[index];

            if (pickup != null)
            {
                results.Add(pickup);
            }
        }

        results.Sort(CompareByPositionThenName);
        return results.ToArray();
    }

    private static List<LegacySupportItemSeed> CollectLegacySupportItemSeeds(
        GameObject prefabRoot,
        MainEscapeFloorAuthoring floorAuthoring,
        MainEscapeRuntimeSettings settings)
    {
        List<LegacySupportItemSeed> seeds = new();
        PrototypeInventoryPickup[] supportPickups = CollectSupportItemPickups(prefabRoot);
        Transform searchRoot = prefabRoot != null ? prefabRoot.transform : null;

        TryAddLegacySupportSeed(
            seeds,
            PrototypeItemCatalog.FlashlightBatteryItemId,
            FindFirstNamedMarker(searchRoot, settings.BatteryMarkerNames),
            supportPickups);
        TryAddLegacySupportSeed(
            seeds,
            PrototypeItemCatalog.GlassBottleItemId,
            FindFirstNamedMarker(searchRoot, settings.GlassBottleMarkerNames),
            supportPickups);

        for (int index = 0; index < supportPickups.Length; index++)
        {
            PrototypeInventoryPickup pickup = supportPickups[index];

            if (pickup == null || !string.Equals(pickup.ItemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal))
            {
                continue;
            }

            seeds.Add(new LegacySupportItemSeed(pickup.ItemId, pickup.transform.position));
        }

        return seeds;
    }

    private static Transform ResolveLegacyKeyPlacementSeed(
        GameObject prefabRoot,
        MainEscapeFloorAuthoring floorAuthoring,
        MainEscapeRuntimeSettings settings)
    {
        Transform searchRoot = prefabRoot != null ? prefabRoot.transform : null;
        Transform keyMarker = FindFirstNamedMarker(searchRoot, settings != null ? settings.KeyPickupSearchNames : Array.Empty<string>());

        if (keyMarker != null)
        {
            return keyMarker;
        }

        MainEscapeKeyPickup[] keyPickups = CollectKeyPickups(prefabRoot);
        return keyPickups.Length > 0 ? keyPickups[0].transform : null;
    }

    private static void TryAddLegacySupportSeed(
        List<LegacySupportItemSeed> seeds,
        string itemId,
        Transform marker,
        IReadOnlyList<PrototypeInventoryPickup> supportPickups)
    {
        if (seeds == null || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (marker != null)
        {
            seeds.Add(new LegacySupportItemSeed(itemId, marker.position));
            return;
        }

        if (supportPickups == null)
        {
            return;
        }

        for (int index = 0; index < supportPickups.Count; index++)
        {
            PrototypeInventoryPickup pickup = supportPickups[index];

            if (pickup == null || !string.Equals(pickup.ItemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            seeds.Add(new LegacySupportItemSeed(itemId, pickup.transform.position));
            return;
        }
    }

    private static Transform FindFirstNamedMarker(Transform root, params string[] markerNames)
    {
        if (root == null || markerNames == null || markerNames.Length == 0)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int nameIndex = 0; nameIndex < markerNames.Length; nameIndex++)
        {
            string markerName = markerNames[nameIndex];

            if (string.IsNullOrWhiteSpace(markerName))
            {
                continue;
            }

            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform marker = transforms[transformIndex];

                if (marker != null && string.Equals(marker.name, markerName, StringComparison.OrdinalIgnoreCase))
                {
                    return marker;
                }
            }
        }

        return null;
    }

    private static string BuildSupportItemMarkerName(string itemId, int fallbackIndex)
    {
        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal))
        {
            return "SupportItemCandidate_00_Battery";
        }

        if (string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal))
        {
            return "SupportItemCandidate_01_GlassBottle";
        }

        return $"SupportItemCandidate_{Mathf.Max(2, fallbackIndex + 2):00}_Medkit";
    }

    private readonly struct LegacySupportItemSeed
    {
        public LegacySupportItemSeed(string itemId, Vector3 position)
        {
            ItemId = itemId ?? string.Empty;
            Position = position;
        }

        public string ItemId { get; }
        public Vector3 Position { get; }
    }

    private static NoiseFloorPanel[] CollectLegacyGlassTrapPanels(Scene scene)
    {
        NoiseFloorPanel[] panels = UnityEngine.Object.FindObjectsByType<NoiseFloorPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<NoiseFloorPanel> results = new();

        for (int index = 0; index < panels.Length; index++)
        {
            NoiseFloorPanel panel = panels[index];

            if (panel == null || panel.gameObject.scene != scene)
            {
                continue;
            }

            results.Add(panel);
        }

        results.Sort(CompareByPositionThenName);
        return results.ToArray();
    }

    private static Transform[] CollectDirectChildMarkers(Transform authoringMarkersRoot, string rootName)
    {
        if (authoringMarkersRoot == null || string.IsNullOrWhiteSpace(rootName))
        {
            return Array.Empty<Transform>();
        }

        Transform root = authoringMarkersRoot.Find(rootName);

        if (root == null || root.childCount == 0)
        {
            return Array.Empty<Transform>();
        }

        List<Transform> markers = new(root.childCount);

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);

            if (child != null)
            {
                markers.Add(child);
            }
        }

        markers.Sort(CompareByPositionThenName);
        return markers.ToArray();
    }

    private static Transform[] CollectNamedMarkers(Transform authoringMarkersRoot, string markerName)
    {
        if (authoringMarkersRoot == null || string.IsNullOrWhiteSpace(markerName))
        {
            return Array.Empty<Transform>();
        }

        Transform[] markers = authoringMarkersRoot.GetComponentsInChildren<Transform>(true);
        List<Transform> matches = new();

        for (int index = 0; index < markers.Length; index++)
        {
            Transform marker = markers[index];

            if (marker == null || !string.Equals(marker.name, markerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(marker);
        }

        matches.Sort(CompareByPositionThenName);
        return matches.ToArray();
    }

    private static Transform[] CollectNamedMarkers(Transform root, IReadOnlyList<string> markerNames)
    {
        if (root == null || markerNames == null || markerNames.Count == 0)
        {
            return Array.Empty<Transform>();
        }

        List<Transform> matches = new();

        for (int index = 0; index < markerNames.Count; index++)
        {
            string markerName = markerNames[index];

            if (string.IsNullOrWhiteSpace(markerName))
            {
                continue;
            }

            Transform[] namedMatches = CollectNamedMarkers(root, markerName);

            for (int matchIndex = 0; matchIndex < namedMatches.Length; matchIndex++)
            {
                Transform marker = namedMatches[matchIndex];

                if (marker != null && !matches.Contains(marker))
                {
                    matches.Add(marker);
                }
            }
        }

        matches.Sort(CompareByPositionThenName);
        return matches.ToArray();
    }

    private static bool NeedsDangerMarkerNormalization(Transform root)
    {
        if (root == null)
        {
            return false;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);

            if (child == null)
            {
                continue;
            }

            MainEscapeDangerPlacementMarker placementMarker = child.GetComponent<MainEscapeDangerPlacementMarker>();
            MainEscapeAuthoringMarkerVisual visual = child.GetComponent<MainEscapeAuthoringMarkerVisual>();
            SpriteRenderer spriteRenderer = child.GetComponentInChildren<SpriteRenderer>(true);

            if (placementMarker == null || visual == null || spriteRenderer == null)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountLegacyGlassTrapPanels(Scene scene)
    {
        if (!scene.IsValid())
        {
            return 0;
        }

        NoiseFloorPanel[] panels = UnityEngine.Object.FindObjectsByType<NoiseFloorPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int count = 0;

        for (int index = 0; index < panels.Length; index++)
        {
            NoiseFloorPanel panel = panels[index];

            if (panel != null && panel.gameObject.scene == scene)
            {
                count++;
            }
        }

        return count;
    }

    private static bool ApplyGlassTrapPlacementCount(MainEscapeFloorAuthoring floorAuthoring, int trapCount)
    {
        if (floorAuthoring == null)
        {
            return false;
        }

        SerializedObject serializedAuthoring = new(floorAuthoring);
        SerializedProperty trapCountProperty = serializedAuthoring.FindProperty("glassTrapPlacementCount");

        if (trapCountProperty == null)
        {
            return false;
        }

        bool changed = false;

        if (trapCount > 0 || trapCountProperty.intValue <= 0)
        {
            int resolvedCount = Mathf.Max(0, trapCount);

            if (trapCountProperty.intValue != resolvedCount)
            {
                trapCountProperty.intValue = resolvedCount;
                changed = true;
            }
        }

        serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    private static MainEscapeItemPlacementMarker InstantiateItemMarker(
        GameObject prefab,
        Transform parent,
        string markerName,
        Vector3 worldPosition)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
        instance.name = markerName;
        instance.transform.SetParent(parent, false);
        instance.transform.position = worldPosition;
        instance.transform.rotation = Quaternion.identity;
        return instance.GetComponent<MainEscapeItemPlacementMarker>();
    }

    private static MainEscapeEnemyPlacementMarker InstantiateEnemyMarker(
        GameObject prefab,
        Transform parent,
        string markerName,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
        instance.name = markerName;
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
        return instance.GetComponent<MainEscapeEnemyPlacementMarker>();
    }

    private static void ApplyPlacementQuotas(
        MainEscapeFloorAuthoring floorAuthoring,
        int batteryCount,
        int glassBottleCount,
        int medkitCount,
        int patrolCount,
        int sentryCount,
        int chaserCount)
    {
        if (floorAuthoring == null)
        {
            return;
        }

        SerializedObject serializedAuthoring = new(floorAuthoring);
        SerializedProperty supportQuota = serializedAuthoring.FindProperty("supportItemPlacementQuota");
        SerializedProperty enemyQuota = serializedAuthoring.FindProperty("enemyPlacementQuota");

        if (supportQuota != null)
        {
            supportQuota.FindPropertyRelative("batteryCount").intValue = Mathf.Max(0, batteryCount);
            supportQuota.FindPropertyRelative("glassBottleCount").intValue = Mathf.Max(0, glassBottleCount);
            supportQuota.FindPropertyRelative("medkitCount").intValue = Mathf.Max(0, medkitCount);
        }

        if (enemyQuota != null)
        {
            enemyQuota.FindPropertyRelative("patrolCount").intValue = Mathf.Max(0, patrolCount);
            enemyQuota.FindPropertyRelative("sentryCount").intValue = Mathf.Max(0, sentryCount);
            enemyQuota.FindPropertyRelative("chaserCount").intValue = Mathf.Clamp(chaserCount, 0, 1);
        }

        serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool IsSupportItemPickup(string itemId)
    {
        return string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal)
            || string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal)
            || string.Equals(itemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal);
    }

    private static bool IsMigratableSupportItemPickup(PrototypeInventoryPickup pickup)
    {
        return pickup != null
            && !pickup.SuppressRuntimeManagedPickupReplacement
            && IsSupportItemPickup(pickup.ItemId);
    }

    private static int CompareByPositionThenName(Component left, Component right)
    {
        if (left == null && right == null)
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

        return CompareByPositionThenName(left.transform, right.transform);
    }

    private static int CompareByPositionThenName(Transform left, Transform right)
    {
        if (left == null && right == null)
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

        int compareY = right.position.y.CompareTo(left.position.y);

        if (compareY != 0)
        {
            return compareY;
        }

        int compareX = left.position.x.CompareTo(right.position.x);

        if (compareX != 0)
        {
            return compareX;
        }

        return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct DangerMarkerSnapshot
    {
        public DangerMarkerSnapshot(string name, Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Name = name;
            Position = position;
            Rotation = rotation;
            LocalScale = localScale;
        }

        public string Name { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 LocalScale { get; }
    }
}
#endif

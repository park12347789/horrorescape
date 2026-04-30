#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RAuthoringReferenceCacheTools
{
    private const string CacheMenuPath = "Tools/Main Escape Rebuild/Cache Authored Floor References";
    private const string BackfillMenuPath = "Tools/Main Escape Rebuild/Backfill Authored Floor References";
    private const string LegacyPrefabCacheMenuPath = "Tools/Main Escape Rebuild/Legacy/Cache Legacy Resources Floor Prefab References";
    private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly string[] ObjectReferenceFieldNames =
    {
        "grid",
        "groundTilemap",
        "wallTilemap",
        "doorTilemap",
        "playerStartMarker",
        "toolMarker",
        "transitionMarker",
        "stalkerSpawnMarker",
        "safeRoomMarker",
        "keyPickupMarker",
        "batteryMarker",
        "glassPanelMarker",
        "patrolSpawn",
        "sentrySpawns",
        "ventRoute",
        "authoringMarkersRoot",
        "pickupMarkersRoot",
        "dangerMarkersRoot",
        "doorMarkersRoot",
        "sentryGuardsRoot",
        "patrolSpawnRoot",
        "ventRouteRoot",
        "interactivePropsRoot",
        "goalVisualsRoot",
        "coverPropsRoot",
        "movementBlockersRoot",
        "itemPlacementMarkersRoot",
        "keyPlacementMarkersRoot",
        "enemyPlacementMarkersRoot",
        "chaserPlacementMarkersRoot"
    };

    private static readonly string[] ObjectReferenceArrayFieldNames =
    {
        "dangerMarkers",
        "dangerPlacementMarkers",
        "doorAuthorings",
        "propBlockers",
        "movementBlockers",
        "supportItemPlacementMarkers",
        "keyPlacementMarkers",
        "enemyPlacementMarkers",
        "chaserPlacementMarkers"
    };

    private static readonly string[] LegacyResourceFloorPrefabPaths =
    {
        "Assets/Resources/Floors/MainEscape/1F.prefab",
        "Assets/Resources/Floors/MainEscape/2F.prefab",
        "Assets/Resources/Floors/MainEscape/3F.prefab",
        "Assets/Resources/Floors/MainEscape/4F.prefab",
        "Assets/Resources/Floors/MainEscape/5F.prefab"
    };

    [MenuItem(CacheMenuPath)]
    private static void CacheAuthoredFloorReferencesFromMenu()
    {
        SyncAuthoredFloorReferences();
    }

    [MenuItem(BackfillMenuPath)]
    private static void BackfillAuthoredFloorReferencesFromMenu()
    {
        SyncAuthoredFloorReferences("Backfilled");
    }

    [MenuItem(LegacyPrefabCacheMenuPath)]
    private static void CacheLegacyResourcesFloorPrefabReferencesFromMenu()
    {
        SyncLegacyResourcesFloorPrefabReferences();
    }

    public static void SyncAuthoredFloorReferences()
    {
        SyncAuthoredFloorReferences("Cached");
    }

    private static void SyncAuthoredFloorReferences(string operationLabel)
    {
        Scene[] authoredScenes = GetOpenAuthoredFloorScenes();
        int updatedSceneCount = 0;

        for (int index = 0; index < authoredScenes.Length; index++)
        {
            if (CacheSceneFloorReferences(authoredScenes[index]))
            {
                updatedSceneCount++;
            }
        }

        if (updatedSceneCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (authoredScenes.Length == 0)
        {
            Debug.LogWarning(
                $"{operationLabel} authored floor references skipped because no open authored floor scene contains {nameof(MainEscapeFloorAuthoring)}. "
                + "Open the floor scene you want to update, or use the clearly named legacy prefab cache action for legacy Resources prefabs.");
            return;
        }

        Debug.Log($"{operationLabel} authored floor references for {updatedSceneCount} open authored floor scene(s). Legacy Resources floor prefabs were not touched.");
    }

    public static void SyncLegacyResourcesFloorPrefabReferences()
    {
        int updatedPrefabCount = 0;

        for (int index = 0; index < LegacyResourceFloorPrefabPaths.Length; index++)
        {
            if (CacheFloorPrefabReferences(LegacyResourceFloorPrefabPaths[index]))
            {
                updatedPrefabCount++;
            }
        }

        if (updatedPrefabCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Cached legacy Resources floor references for {updatedPrefabCount} Resources floor prefab(s). Open authored scenes were not touched.");
    }

    [Obsolete("Use SyncLegacyResourcesFloorPrefabReferences for legacy Resources floor prefab quarantine tools.")]
    public static void SyncLegacyAuthoredFloorPrefabReferences()
    {
        SyncLegacyResourcesFloorPrefabReferences();
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

    private static bool CacheFloorPrefabReferences(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) || !File.Exists(prefabPath))
        {
            Debug.LogWarning($"Skipping authored floor reference cache because prefab is missing at '{prefabPath}'.");
            return false;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            MainEscapeFloorAuthoring authoring = prefabRoot != null
                ? prefabRoot.GetComponent<MainEscapeFloorAuthoring>()
                : null;

            if (authoring == null)
            {
                Debug.LogWarning($"Skipping authored floor reference cache because '{prefabPath}' has no MainEscapeFloorAuthoring.");
                return false;
            }

            authoring.CacheReferencesFromHierarchy();
            CommitSerializedAuthoringReferences(authoring);
            EditorUtility.SetDirty(authoring);
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

    private static bool CacheSceneFloorReferences(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            Debug.LogWarning("Skipping scene floor reference cache because the scene is not an open saved authored floor scene.");
            return false;
        }

        MainEscapeFloorAuthoring[] authorings = UnityEngine.Object.FindObjectsByType<MainEscapeFloorAuthoring>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (authorings == null || authorings.Length == 0)
        {
            Debug.LogWarning($"Skipping scene floor reference cache because '{scene.path}' has no MainEscapeFloorAuthoring.");
            return false;
        }

        bool sceneChanged = false;

        for (int index = 0; index < authorings.Length; index++)
        {
            MainEscapeFloorAuthoring authoring = authorings[index];

            if (authoring == null || authoring.gameObject.scene != scene)
            {
                continue;
            }

            authoring.CacheReferencesFromHierarchy();
            CommitSerializedAuthoringReferences(authoring);
            EditorUtility.SetDirty(authoring);
            sceneChanged = true;
        }

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return sceneChanged;
    }

    private static void CommitSerializedAuthoringReferences(MainEscapeFloorAuthoring authoring)
    {
        if (authoring == null)
        {
            return;
        }

        SerializedObject serializedAuthoring = new(authoring);

        for (int index = 0; index < ObjectReferenceFieldNames.Length; index++)
        {
            string fieldName = ObjectReferenceFieldNames[index];
            SerializedProperty property = serializedAuthoring.FindProperty(fieldName);

            if (property == null)
            {
                continue;
            }

            property.objectReferenceValue = GetObjectField(authoring, fieldName);
        }

        for (int index = 0; index < ObjectReferenceArrayFieldNames.Length; index++)
        {
            string fieldName = ObjectReferenceArrayFieldNames[index];
            SerializedProperty property = serializedAuthoring.FindProperty(fieldName);

            if (property == null || !property.isArray)
            {
                continue;
            }

            UnityEngine.Object[] values = GetObjectArrayField(authoring, fieldName);
            property.arraySize = values.Length;

            for (int elementIndex = 0; elementIndex < values.Length; elementIndex++)
            {
                property.GetArrayElementAtIndex(elementIndex).objectReferenceValue = values[elementIndex];
            }
        }

        serializedAuthoring.ApplyModifiedPropertiesWithoutUndo();
    }

    private static UnityEngine.Object GetObjectField(MainEscapeFloorAuthoring authoring, string fieldName)
    {
        FieldInfo field = typeof(MainEscapeFloorAuthoring).GetField(fieldName, InstanceFieldFlags);
        return field?.GetValue(authoring) as UnityEngine.Object;
    }

    private static UnityEngine.Object[] GetObjectArrayField(MainEscapeFloorAuthoring authoring, string fieldName)
    {
        FieldInfo field = typeof(MainEscapeFloorAuthoring).GetField(fieldName, InstanceFieldFlags);

        if (field?.GetValue(authoring) is not Array values || values.Length == 0)
        {
            return Array.Empty<UnityEngine.Object>();
        }

        UnityEngine.Object[] objects = new UnityEngine.Object[values.Length];

        for (int index = 0; index < values.Length; index++)
        {
            objects[index] = values.GetValue(index) as UnityEngine.Object;
        }

        return objects;
    }
}
#endif

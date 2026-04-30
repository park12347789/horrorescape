#if UNITY_EDITOR
using System;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class RTutorialSceneAuthoringTools
{
    private const string TutorialScenePath = "Assets/Scenes/RMainEscape_tuto.unity";
    private const string FiveFloorScenePath = "Assets/Scenes/RMainScene_5F.unity";
    private const string AuthoringRootName = "RTutorialAuthoring";
    private const string TileGridName = "RTutorialTileGrid";
    private const string GroundTilemapName = "Tiles_ground";
    private const string WallTilemapName = "Tiles_wall";
    private const string DoorTilemapName = "Doors";
    private const string GameplayOverlayRootName = "GameplayOverlay";
    private const string LightPassWallTilemapName = "Tiles_movenonlywall";
    private const string LegacyGroundTilemapName = "GroundTilemap";
    private const string LegacyWallTilemapName = "WallTilemap";
    private const string ElevatorExitRootName = "ElevatorExit";
    private const string DirectExitElevatorVisualName = "RDirectExitElevatorVisual";
    private const string TutorialSentryMarkerName = "SentrySpawnMarker_01";
    private const string ExitTopDoorName = "VexedTileBProp_01_Top (8)";
    private const string ExitSideDoorName = "CustomSideDoorClosed";
    private const string MaterializeMenuPath = "Tools/Main Escape Rebuild/Materialize Tutorial Scene Authoring";
    private const string FlashlightPickupPrefabPath = "Assets/Prefabs/Items/MainEscape/Inventory/Pickup_Flashlight.prefab";
    private const string CrateBlockPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/CoverProps/CrateBlock_1x1.prefab";
    private const string DecorCuePrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/DecorProps/Decor_NoBlock_1x1.prefab";
    private const string SolidBlockerPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/SolidBlockers/SolidBlocker_1x1.prefab";
    private const string EnemyPlacementMarkerPrefabPath = "Assets/Prefabs/MainEscapeEditing/PlaceablePresets/Markers/EnemyPlacementMarker.prefab";
    private const string GroundEnemyPrefabPath = "Assets/Prefabs/Enemies/MainEscape/Ground/Enemy_GroundRuntime.prefab";
    private const string DirectExitElevatorPrefabPath = "Assets/Prefabs/Environment/MainEscape/Vexed/ElevatorDoors/VexedElevatorDoor_00.prefab";
    private const string TileBSplitDoorTopPrefabPath = "Assets/Prefabs/Environment/MainEscape/Vexed/TileBSplitDoors/VexedTileBProp_01_Top.prefab";
    private const string CustomSideDoorClosedSpritePath = "Assets/Resources/MainEscape/DoorSprites/CustomSideDoorClosed.png";
    private const string OverheadLightPrefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_OverheadPool.prefab";
    private const string WallLampPrefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_WallLamp.prefab";
    private const string HudCanvasPrefabPath = "Assets/Prefabs/IRHudCanvas.prefab";
    private const string AuthoredGameplayHudPrefabPath = "Assets/Prefabs/RAuthoredGameplayHudCanvas.prefab";
    private const string GlobalLightName = "RGlobalLight";

    [MenuItem(MaterializeMenuPath)]
    public static void MaterializeAndSaveTutorialScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        if (!scene.IsValid() || scene.path != TutorialScenePath)
        {
            scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
        }

        MaterializeAndSave(scene, "Materialized");
    }

    private static void MaterializeAndSave(Scene scene, string operationLabel)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"{nameof(RTutorialSceneAuthoringTools)} could not load '{TutorialScenePath}'.");
            return;
        }

        RTutorialSceneBootstrap bootstrap = FindBootstrap(scene);

        if (bootstrap == null)
        {
            GameObject bootstrapObject = new("RTutorialBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            bootstrap = bootstrapObject.AddComponent<RTutorialSceneBootstrap>();
        }

        Transform root = EnsureAuthoringRoot(scene);
        EnsureTutorialTilemaps(scene, root);
        RemoveGeneratedFloorPlaceholders(root);
        RemoveRetiredGeneratedAuthoringObjects(root);
        EnsureRequestedTutorialExitDoorPair(root);
        EnsureTutorialSentrySpawnMarker(root);
        EnsureTutorialHudCanvas(scene);
        EnsureTutorialAuthoredGameplayHud(scene);
        EnsureTutorialGlobalLight(scene);
        bootstrap.MaterializeTutorialGlassTrapsForEditing();
        DisableEditModeAutoMaterialization(bootstrap);
        EditorUtility.SetDirty(bootstrap);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"{operationLabel} tutorial authoring objects in '{TutorialScenePath}'.");
    }

    private static void EnsureTutorialTilemaps(Scene tutorialScene, Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform tileGridRoot = EnsureChild(root, TileGridName);
        Grid grid = tileGridRoot.GetComponent<Grid>();

        if (grid == null)
        {
            grid = tileGridRoot.gameObject.AddComponent<Grid>();
        }

        grid.cellSize = Vector3.one;
        grid.cellGap = Vector3.zero;
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

        MigrateLegacyTilemapName(tileGridRoot, LegacyGroundTilemapName, GroundTilemapName);
        MigrateLegacyTilemapName(tileGridRoot, LegacyWallTilemapName, WallTilemapName);

        Tilemap groundTilemap = EnsureTilemap(tileGridRoot, GroundTilemapName, GameLayers.GroundIndex, sortingOrder: 0, addCollider: false);
        _ = EnsureTilemap(tileGridRoot, WallTilemapName, GameLayers.WallIndex, sortingOrder: 5, addCollider: true);
        _ = EnsureTilemap(tileGridRoot, LightPassWallTilemapName, GameLayers.PropIndex, sortingOrder: 18, addCollider: false);
        _ = EnsureTilemap(tileGridRoot, DoorTilemapName, GameLayers.DoorIndex, sortingOrder: 7, addCollider: true);
        _ = EnsureChild(tileGridRoot, GameplayOverlayRootName);

        if (groundTilemap != null && !groundTilemap.HasTile(new Vector3Int(0, 0, 0)))
        {
            CopyGroundTilePatchFromFiveFloor(tutorialScene, groundTilemap);
        }
    }

    private static void MigrateLegacyTilemapName(Transform parent, string legacyName, string targetName)
    {
        if (parent == null || parent.Find(targetName) != null)
        {
            return;
        }

        Transform legacy = parent.Find(legacyName);

        if (legacy == null)
        {
            return;
        }

        legacy.name = targetName;
        EditorUtility.SetDirty(legacy);
    }

    private static Tilemap EnsureTilemap(
        Transform parent,
        string objectName,
        int layer,
        int sortingOrder,
        bool addCollider)
    {
        Transform tilemapTransform = EnsureChild(parent, objectName);
        tilemapTransform.gameObject.layer = layer;

        Tilemap tilemap = tilemapTransform.GetComponent<Tilemap>();

        if (tilemap == null)
        {
            tilemap = tilemapTransform.gameObject.AddComponent<Tilemap>();
        }

        TilemapRenderer renderer = tilemapTransform.GetComponent<TilemapRenderer>();

        if (renderer == null)
        {
            renderer = tilemapTransform.gameObject.AddComponent<TilemapRenderer>();
        }

        renderer.sortingOrder = sortingOrder;

        TilemapCollider2D collider = tilemapTransform.GetComponent<TilemapCollider2D>();

        if (addCollider && collider == null)
        {
            collider = tilemapTransform.gameObject.AddComponent<TilemapCollider2D>();
        }

        if (addCollider && collider != null)
        {
            collider.enabled = true;
            collider.isTrigger = false;
        }
        else if (!addCollider && collider != null)
        {
            collider.enabled = false;
        }

        return tilemap;
    }

    private static void CopyGroundTilePatchFromFiveFloor(Scene tutorialScene, Tilemap destination)
    {
        Scene sourceScene = default;
        bool openedSourceScene = false;

        try
        {
            sourceScene = EditorSceneManager.OpenScene(FiveFloorScenePath, OpenSceneMode.Additive);
            openedSourceScene = sourceScene.IsValid() && sourceScene.isLoaded;
            Tilemap source = FindTilemapInScene(sourceScene, "Tiles_ground");

            if (source == null)
            {
                Debug.LogWarning($"{nameof(RTutorialSceneAuthoringTools)} could not find 5F ground tilemap.");
                return;
            }

            TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
            TilemapRenderer destinationRenderer = destination.GetComponent<TilemapRenderer>();

            if (sourceRenderer != null && destinationRenderer != null && sourceRenderer.sharedMaterial != null)
            {
                destinationRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }

            BoundsInt sourceBounds = source.cellBounds;
            Vector3Int sourceCenter = new(
                Mathf.RoundToInt((sourceBounds.xMin + sourceBounds.xMax) * 0.5f),
                Mathf.RoundToInt((sourceBounds.yMin + sourceBounds.yMax) * 0.5f),
                0);
            TileBase fallbackTile = FindFirstTile(source);

            for (int x = -7; x <= 6; x++)
            {
                for (int y = -3; y <= 2; y++)
                {
                    Vector3Int sourceCell = sourceCenter + new Vector3Int(x, y, 0);
                    TileBase tile = source.GetTile(sourceCell);

                    if (tile == null)
                    {
                        tile = fallbackTile;
                    }

                    if (tile == null)
                    {
                        continue;
                    }

                    Vector3Int destinationCell = new(x, y, 0);
                    destination.SetTile(destinationCell, tile);
                    destination.SetTransformMatrix(destinationCell, source.GetTransformMatrix(sourceCell));
                    destination.SetTileFlags(destinationCell, source.GetTileFlags(sourceCell));
                    destination.SetColor(destinationCell, source.GetColor(sourceCell));
                }
            }

            EditorUtility.SetDirty(destination);
        }
        finally
        {
            if (openedSourceScene && sourceScene.IsValid() && sourceScene.path == FiveFloorScenePath)
            {
                EditorSceneManager.CloseScene(sourceScene, removeScene: true);
                EditorSceneManager.SetActiveScene(tutorialScene);
            }
        }
    }

    private static TileBase FindFirstTile(Tilemap source)
    {
        BoundsInt bounds = source.cellBounds;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            TileBase tile = source.GetTile(cell);

            if (tile != null)
            {
                return tile;
            }
        }

        return null;
    }

    private static Tilemap FindTilemapInScene(Scene scene, string objectName)
    {
        Transform transform = FindInScene(scene, objectName);
        return transform != null ? transform.GetComponent<Tilemap>() : null;
    }

    private static void RemoveGeneratedFloorPlaceholders(Transform root)
    {
        DestroyChildIfPresent(root, "TileFloor_Base");
        DestroyChildIfPresent(root, "FloorBand");
    }

    private static void RemoveRetiredGeneratedAuthoringObjects(Transform root)
    {
        string[] retiredNames =
        {
            "Backdrop",
            "MoveOnlyBlocker_LowDesk",
            "MoveOnlyBlocker_Cart",
            "VisionBlocker_Wall",
            "BoundaryWall_Top",
            "BoundaryWall_Bottom",
            "BoundaryWall_Left",
            "BoundaryWall_Right",
            "TargetPen_Back",
            "TargetPen_Left",
            "TargetPen_Right",
            "TutorialLight_Pickups",
            "TutorialLight_BottleLesson",
            "TutorialLight_Elevator",
            "ElevatorCenterLine"
        };

        for (int index = 0; index < retiredNames.Length; index++)
        {
            DestroyChildIfPresent(root, retiredNames[index]);
        }

        for (int index = 0; index < 4; index++)
        {
            DestroyChildIfPresent(root, $"MoveCue_{index:00}");
        }

        for (int index = 0; index < 5; index++)
        {
            DestroyChildIfPresent(root, $"ThrowArc_{index:00}");
        }

        for (int index = 0; index < 3; index++)
        {
            DestroyChildIfPresent(root, $"BreakWallShardCue_{index:00}");
        }
    }

    private static void DestroyChildIfPresent(Transform root, string objectName)
    {
        Transform existing = FindChildByName(root, objectName);

        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static void EnsureFiveFloorPickupPrefabs(Transform root)
    {
        if (root == null)
        {
            return;
        }

        MainEscapeRuntimePrefabCatalog catalog = MainEscapeRuntimePrefabCatalog.Load();

        EnsurePickupPrefab(
            root,
            "FlashlightPickup",
            AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPickupPrefabPath),
            new Vector2(-4.4f, 0f),
            PrototypeItemCatalog.FlashlightItemId,
            1,
            Color.white);
        EnsurePickupPrefab(
            root,
            "MedkitPickup",
            catalog != null && catalog.MedkitPickupPrefab != null ? catalog.MedkitPickupPrefab.gameObject : null,
            new Vector2(-3.35f, -0.75f),
            PrototypeItemCatalog.MedkitItemId,
            1,
            new Color(0.45f, 1f, 0.66f, 1f));
        EnsurePickupPrefab(
            root,
            "FlashlightBatteryPickup",
            catalog != null && catalog.FlashlightBatteryPickupPrefab != null ? catalog.FlashlightBatteryPickupPrefab.gameObject : null,
            new Vector2(-3.35f, 0.75f),
            PrototypeItemCatalog.FlashlightBatteryItemId,
            1,
            new Color(0.46f, 0.9f, 1f, 1f));
        EnsurePickupPrefab(
            root,
            "GlassBottlePickup",
            catalog != null && catalog.GlassBottlePickupPrefab != null ? catalog.GlassBottlePickupPrefab.gameObject : null,
            new Vector2(-2.2f, 0f),
            PrototypeItemCatalog.GlassBottleItemId,
            2,
            new Color(0.72f, 0.92f, 1f, 1f));

        EnsureKeyPickupPrefab(root, catalog);
    }

    private static void EnsureExistingTutorialObjectPrefabs(Transform root)
    {
        if (root == null)
        {
            return;
        }

        GameObject cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrateBlockPrefabPath);
        GameObject decorCuePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DecorCuePrefabPath);
        GameObject solidBlockerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SolidBlockerPrefabPath);
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundEnemyPrefabPath);
        GameObject directExitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirectExitElevatorPrefabPath);
        GameObject splitDoorTopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TileBSplitDoorTopPrefabPath);
        Sprite sideDoorClosedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CustomSideDoorClosedSpritePath);
        GameObject overheadLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OverheadLightPrefabPath);
        GameObject wallLampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallLampPrefabPath);

        EnsureScenePrefab(
            root,
            "MoveOnlyBlocker_LowDesk",
            cratePrefab,
            new Vector2(-1.08f, -0.95f),
            new Vector3(1.35f, 0.32f, 1f),
            GameLayers.PropIndex,
            sortingOrder: 118,
            ConfigureMovementOnlyCollider);
        EnsureScenePrefab(
            root,
            "MoveOnlyBlocker_Cart",
            cratePrefab,
            new Vector2(0.14f, 1.08f),
            new Vector3(0.42f, 1.15f, 1f),
            GameLayers.PropIndex,
            sortingOrder: 118,
            ConfigureMovementOnlyCollider);

        EnsureScenePrefab(
            root,
            "VisionBlocker_Wall",
            solidBlockerPrefab,
            new Vector2(2.46f, 1.08f),
            new Vector3(0.48f, 1.78f, 1f),
            GameLayers.WallIndex,
            sortingOrder: 122,
            ConfigureSolidCollider);
        EnsureScenePrefab(root, "BoundaryWall_Top", solidBlockerPrefab, new Vector2(0f, 2.7f), new Vector3(13.6f, 0.18f, 1f), GameLayers.WallIndex, 112, ConfigureSolidCollider);
        EnsureScenePrefab(root, "BoundaryWall_Bottom", solidBlockerPrefab, new Vector2(0f, -2.7f), new Vector3(13.6f, 0.18f, 1f), GameLayers.WallIndex, 112, ConfigureSolidCollider);
        EnsureScenePrefab(root, "BoundaryWall_Left", solidBlockerPrefab, new Vector2(-6.8f, 0f), new Vector3(0.18f, 5.4f, 1f), GameLayers.WallIndex, 112, ConfigureSolidCollider);
        EnsureScenePrefab(root, "BoundaryWall_Right", solidBlockerPrefab, new Vector2(6.8f, 0f), new Vector3(0.18f, 5.4f, 1f), GameLayers.WallIndex, 112, ConfigureSolidCollider);

        EnsureScenePrefab(
            root,
            "CagedEnemyBottleTarget",
            enemyPrefab,
            new Vector2(1.45f, 0f),
            new Vector3(1f, 1f, 1f),
            layer: 0,
            sortingOrder: 145,
            ConfigureTutorialEnemyTarget);
        EnsureScenePrefab(root, "TargetPen_Back", cratePrefab, new Vector2(1.45f, 0.64f), new Vector3(1.8f, 0.14f, 1f), GameLayers.PropIndex, 126, ConfigureMovementOnlyCollider);
        EnsureScenePrefab(root, "TargetPen_Left", cratePrefab, new Vector2(0.51f, 0f), new Vector3(0.14f, 1.38f, 1f), GameLayers.PropIndex, 126, ConfigureMovementOnlyCollider);
        EnsureScenePrefab(root, "TargetPen_Right", cratePrefab, new Vector2(2.39f, 0f), new Vector3(0.14f, 1.38f, 1f), GameLayers.PropIndex, 126, ConfigureMovementOnlyCollider);

        EnsureScenePrefab(
            root,
            "BottleBreakWall",
            solidBlockerPrefab,
            new Vector2(2.65f, -1.2f),
            new Vector3(0.42f, 1.05f, 1f),
            GameLayers.WallIndex,
            sortingOrder: 124,
            ConfigureBreakableBottleWall);
        DestroyChildIfPresent(root, ElevatorExitRootName);
        EnsureDirectExitElevator(root, directExitPrefab);
        EnsureStandaloneDoorPair(root, splitDoorTopPrefab, sideDoorClosedSprite);
        EnsureTutorialSentrySpawnMarker(root);

        EnsureScenePrefab(
            root,
            "TutorialLight_Pickups",
            overheadLightPrefab,
            new Vector2(-3.4f, 1.55f),
            Vector3.one,
            layer: 0,
            sortingOrder: 20,
            ConfigureLightOnly);
        EnsureScenePrefab(root, "TutorialLight_BottleLesson", wallLampPrefab, new Vector2(1.45f, 1.72f), Vector3.one, layer: 0, sortingOrder: 24, ConfigureLightOnly);
        EnsureScenePrefab(root, "TutorialLight_Elevator", wallLampPrefab, new Vector2(5.35f, 1.72f), Vector3.one, layer: 0, sortingOrder: 24, ConfigureLightOnly);

        for (int index = 0; index < 4; index++)
        {
            EnsureScenePrefab(
                root,
                $"MoveCue_{index:00}",
                decorCuePrefab,
                new Vector2(-4.9f + (index * 0.52f), -1.28f),
                new Vector3(0.28f, 0.08f, 1f),
                layer: 0,
                sortingOrder: 130,
                ConfigureCueOnly);
        }

        Vector2[] throwArcPositions =
        {
            new(-1.55f, 0.36f),
            new(-0.98f, 0.69f),
            new(-0.41f, 0.78f),
            new(0.17f, 0.57f),
            new(1.03f, 0.2f)
        };

        for (int index = 0; index < throwArcPositions.Length; index++)
        {
            EnsureScenePrefab(
                root,
                $"ThrowArc_{index:00}",
                decorCuePrefab,
                throwArcPositions[index],
                new Vector3(0.11f, 0.11f, 1f),
                layer: 0,
                sortingOrder: 130,
                ConfigureCueOnly);
        }

        for (int index = 0; index < 3; index++)
        {
            EnsureScenePrefab(
                root,
                $"BreakWallShardCue_{index:00}",
                decorCuePrefab,
                new Vector2(3.23f + (index * 0.22f), -1.38f + (index * 0.18f)),
                new Vector3(0.1f, 0.1f, 1f),
                layer: 0,
                sortingOrder: 130,
                ConfigureCueOnly);
        }
        DestroyChildIfPresent(root, "ElevatorCenterLine");
    }

    private static void EnsureRequestedTutorialExitDoorPair(Transform root)
    {
        GameObject directExitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirectExitElevatorPrefabPath);
        GameObject splitDoorTopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TileBSplitDoorTopPrefabPath);
        Sprite sideDoorClosedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CustomSideDoorClosedSpritePath);
        DestroyChildIfPresent(root, ElevatorExitRootName);
        EnsureDirectExitElevator(root, directExitPrefab);
        EnsureStandaloneDoorPair(root, splitDoorTopPrefab, sideDoorClosedSprite);
    }

    private static void EnsureTutorialSentrySpawnMarker(Transform root)
    {
        if (root == null)
        {
            return;
        }

        GameObject markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPlacementMarkerPrefabPath);
        Transform markerTransform = EnsurePrefabChild(
            root,
            TutorialSentryMarkerName,
            markerPrefab,
            new Vector3(1.45f, 0f, 0f),
            Quaternion.Euler(0f, 0f, 90f),
            Vector3.one);

        if (markerTransform == null)
        {
            return;
        }

        MainEscapeEnemyPlacementMarker marker = markerTransform.GetComponent<MainEscapeEnemyPlacementMarker>();

        if (marker != null)
        {
            marker.Configure(MainEscapeEnemyPlacementKind.Sentry, TutorialSentryMarkerName);
            EditorUtility.SetDirty(marker);
        }

        SetSpriteSortingBase(markerTransform.gameObject, 150);
        EditorUtility.SetDirty(markerTransform.gameObject);
    }

    private static void EnsureDirectExitElevator(Transform root, GameObject directExitPrefab)
    {
        if (root == null || directExitPrefab == null)
        {
            return;
        }

        Transform existing = FindChildByName(root, DirectExitElevatorVisualName);
        bool replaceExisting = existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == null;
        Vector3 defaultLocalPosition = new(5.4f, 0f, 0f);
        Vector3 localPosition = existing != null && !IsLostExitRootTransform(existing)
            ? existing.localPosition
            : defaultLocalPosition;
        Quaternion localRotation = existing != null && !replaceExisting ? existing.localRotation : Quaternion.identity;
        Vector3 localScale = existing != null && !replaceExisting ? existing.localScale : Vector3.one;
        GameObject instance = existing != null ? existing.gameObject : null;

        if (instance == null || replaceExisting)
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            instance = PrefabUtility.InstantiatePrefab(directExitPrefab, root) as GameObject;

            if (instance == null)
            {
                return;
            }

            instance.name = DirectExitElevatorVisualName;
        }

        if (instance.transform.parent != root)
        {
            instance.transform.SetParent(root, false);
        }

        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        instance.transform.localScale = localScale;
        SetLayerRecursively(instance, 0);
        SetSpriteSortingBase(instance, 100);
        ConfigureDirectTutorialExitElevator(instance);
        EditorUtility.SetDirty(instance);
    }

    private static void EnsureStandaloneDoorPair(Transform root, GameObject splitDoorTopPrefab, Sprite sideDoorClosedSprite)
    {
        if (root == null || splitDoorTopPrefab == null || sideDoorClosedSprite == null)
        {
            return;
        }

        Transform topDoor = EnsurePrefabChild(
            root,
            ExitTopDoorName,
            splitDoorTopPrefab,
            new Vector3(-1.2f, 1.2f, -40f),
            Quaternion.identity,
            new Vector3(1.4f, 1.4f, 1f));
        Transform sideDoor = EnsureSideDoorClosedVisual(
            root,
            sideDoorClosedSprite,
            new Vector3(0.65f, 1.2f, -40f));

        if (topDoor != null)
        {
            if (IsLostExitChildTransform(topDoor))
            {
                topDoor.localPosition = new Vector3(-1.2f, 1.2f, -40f);
            }

            SetLayerRecursively(topDoor.gameObject, 0);
            SetSpriteSortingBase(topDoor.gameObject, 126);
            EditorUtility.SetDirty(topDoor.gameObject);
        }

        if (sideDoor != null)
        {
            if (IsLostExitChildTransform(sideDoor))
            {
                sideDoor.localPosition = new Vector3(0.65f, 1.2f, -40f);
            }

            SetLayerRecursively(sideDoor.gameObject, 0);
            SetSpriteSortingBase(sideDoor.gameObject, 127);
            EditorUtility.SetDirty(sideDoor.gameObject);
        }
    }

    private static void EnsureTutorialElevatorExit(Transform root, GameObject splitDoorTopPrefab, Sprite sideDoorClosedSprite)
    {
        if (root == null || splitDoorTopPrefab == null || sideDoorClosedSprite == null)
        {
            return;
        }

        Transform existing = FindChildByName(root, ElevatorExitRootName);
        Vector3 defaultExitPosition = new(5.4f, 0f, 0f);
        Vector3 localPosition = existing != null && !IsLostExitRootTransform(existing)
            ? existing.localPosition
            : defaultExitPosition;
        Quaternion localRotation = existing != null ? existing.localRotation : Quaternion.identity;
        Vector3 localScale = existing != null ? existing.localScale : Vector3.one;

        if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
            existing = null;
        }

        Transform exitRoot = existing != null ? existing : EnsureChild(root, ElevatorExitRootName);
        exitRoot.localPosition = localPosition;
        exitRoot.localRotation = localRotation;
        exitRoot.localScale = localScale;
        SetLayerRecursively(exitRoot.gameObject, 0);

        Transform topDoor = EnsurePrefabChild(
            exitRoot,
            ExitTopDoorName,
            splitDoorTopPrefab,
            new Vector3(-0.55f, 0.18f, -40f),
            Quaternion.identity,
            new Vector3(1.4f, 1.4f, 1f));
        Transform sideDoor = EnsureSideDoorClosedVisual(
            exitRoot,
            sideDoorClosedSprite,
            new Vector3(1.25f, 0f, -40f));

        if (topDoor != null)
        {
            ResetLostExitChildTransform(
                topDoor,
                new Vector3(-0.55f, 0.18f, -40f),
                new Vector3(1.4f, 1.4f, 1f));
            SetLayerRecursively(topDoor.gameObject, 0);
            SetSpriteSortingBase(topDoor.gameObject, 126);
            EditorUtility.SetDirty(topDoor.gameObject);
        }

        if (sideDoor != null)
        {
            ResetLostExitChildTransform(
                sideDoor,
                new Vector3(1.25f, 0f, -40f),
                Vector3.one);
            SetLayerRecursively(sideDoor.gameObject, 0);
            SetSpriteSortingBase(sideDoor.gameObject, 127);
            EditorUtility.SetDirty(sideDoor.gameObject);
        }

        ConfigureTutorialElevatorExit(exitRoot.gameObject);
        EditorUtility.SetDirty(exitRoot.gameObject);
    }

    private static Transform EnsurePrefabChild(
        Transform parent,
        string objectName,
        GameObject prefab,
        Vector3 defaultLocalPosition,
        Quaternion defaultLocalRotation,
        Vector3 defaultLocalScale)
    {
        if (parent == null || prefab == null)
        {
            return null;
        }

        Transform existing = FindChildByName(parent, objectName);
        bool replaceExisting = existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == null;
        Vector3 localPosition = existing != null ? existing.localPosition : defaultLocalPosition;
        Quaternion localRotation = existing != null && !replaceExisting ? existing.localRotation : defaultLocalRotation;
        Vector3 localScale = existing != null && !replaceExisting ? existing.localScale : defaultLocalScale;
        GameObject instance = existing != null ? existing.gameObject : null;

        if (instance == null || replaceExisting)
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

            if (instance == null)
            {
                return null;
            }

            instance.name = objectName;
        }

        if (instance.transform.parent != parent)
        {
            instance.transform.SetParent(parent, false);
        }

        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        instance.transform.localScale = localScale;
        return instance.transform;
    }

    private static bool IsLostExitRootTransform(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        Vector3 localPosition = transform.localPosition;
        return Mathf.Abs(localPosition.x) > 8f || Mathf.Abs(localPosition.y) > 4f;
    }

    private static bool IsLostExitChildTransform(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        Vector3 localPosition = transform.localPosition;
        return Mathf.Abs(localPosition.x) > 4f || Mathf.Abs(localPosition.y) > 3f;
    }

    private static void ResetLostExitChildTransform(Transform transform, Vector3 localPosition, Vector3 localScale)
    {
        if (!IsLostExitChildTransform(transform))
        {
            return;
        }

        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.identity;
        transform.localScale = localScale;
    }

    private static Transform EnsureSideDoorClosedVisual(Transform parent, Sprite sideDoorClosedSprite, Vector3 defaultLocalPosition)
    {
        Transform existing = FindChildByName(parent, ExitSideDoorName);
        Transform sideDoor = existing != null ? existing : EnsureChild(parent, ExitSideDoorName);

        if (sideDoor.parent != parent)
        {
            sideDoor.SetParent(parent, false);
        }

        if (existing == null || IsLostExitChildTransform(sideDoor))
        {
            sideDoor.localPosition = defaultLocalPosition;
        }

        sideDoor.localRotation = Quaternion.identity;
        sideDoor.localScale = Vector3.one;

        SpriteRenderer renderer = sideDoor.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = sideDoor.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = sideDoorClosedSprite;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.size = new Vector2(0.625f, 2f);
        renderer.sortingOrder = 127;

        MainEscapeDoorVisualVariantOverride variantOverride = sideDoor.GetComponent<MainEscapeDoorVisualVariantOverride>();

        if (variantOverride == null)
        {
            variantOverride = sideDoor.gameObject.AddComponent<MainEscapeDoorVisualVariantOverride>();
        }

        variantOverride.Configure(MainEscapeDoorVisualVariantKind.SideDoor42);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(variantOverride);
        return sideDoor;
    }

    private static GameObject EnsureScenePrefab(
        Transform root,
        string objectName,
        GameObject prefab,
        Vector2 defaultPosition,
        Vector3 defaultScale,
        int layer,
        int sortingOrder,
        Action<GameObject> configure)
    {
        if (root == null || prefab == null)
        {
            return null;
        }

        Transform existing = FindChildByName(root, objectName);
        bool replaceExisting = existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == null;
        Vector3 localPosition = existing != null ? existing.localPosition : (Vector3)defaultPosition;
        Quaternion localRotation = existing != null && !replaceExisting ? existing.localRotation : Quaternion.identity;
        Vector3 localScale = existing != null && !replaceExisting ? existing.localScale : defaultScale;
        GameObject instance = existing != null ? existing.gameObject : null;

        if (instance == null || replaceExisting)
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            instance = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
            instance.name = objectName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
        }

        SetLayerRecursively(instance, layer);
        SetSpriteSortingBase(instance, sortingOrder);
        configure?.Invoke(instance);
        EditorUtility.SetDirty(instance);
        return instance;
    }

    private static void ConfigureMovementOnlyCollider(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        BoxCollider2D collider = instance.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider = instance.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = false;
        collider.offset = Vector2.zero;
        collider.size = Vector2.one;
    }

    private static void ConfigureSolidCollider(GameObject instance)
    {
        ConfigureMovementOnlyCollider(instance);
        instance.layer = GameLayers.WallIndex;
    }

    private static void ConfigureTutorialEnemyTarget(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        DisableIfPresent<VisionSensor2D>(instance);
        DisableIfPresent<EnemyStateMachine>(instance);
        DisableIfPresent<EnemyVisionVisualizer>(instance);
        DisableIfPresent<PrototypeEnemyAudioDriver>(instance);
        DisableIfPresent<EnemyPlayerSpottedScreamAudio>(instance);
        DisableIfPresent<EnemyPassiveAmbientAudio>(instance);

        RTutorialBottleTarget target = instance.GetComponent<RTutorialBottleTarget>();

        if (target == null)
        {
            target = instance.AddComponent<RTutorialBottleTarget>();
        }

        CircleCollider2D collider = instance.GetComponent<CircleCollider2D>();

        if (collider != null)
        {
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.42f, collider.radius);
        }
    }

    private static void ConfigureBreakableBottleWall(GameObject instance)
    {
        ConfigureSolidCollider(instance);

        if (instance != null && instance.GetComponent<RTutorialBottleBreakWall>() == null)
        {
            instance.AddComponent<RTutorialBottleBreakWall>();
        }
    }

    private static void ConfigureTutorialElevatorExit(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        BoxCollider2D collider = instance.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider = instance.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;
        collider.offset = new Vector2(-0.24f, 0.08f);
        collider.size = new Vector2(3.8f, 2.6f);

        RTutorialElevatorExitInteractable exit = instance.GetComponent<RTutorialElevatorExitInteractable>();

        if (exit == null)
        {
            exit = instance.AddComponent<RTutorialElevatorExitInteractable>();
        }

        exit.Configure(
            requiresConfiguredItem: true,
            configuredRequiredItemId: PrototypeItemCatalog.IronGateKeyItemId,
            configuredShowPromptText: false,
            configuredConsumeRequiredItemBeforeExit: false);
    }

    private static void ConfigureDirectTutorialExitElevator(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        MainEscapeElevatorExitInteractable floorExit = instance.GetComponent<MainEscapeElevatorExitInteractable>();

        if (floorExit != null)
        {
            floorExit.enabled = false;
            EditorUtility.SetDirty(floorExit);
        }

        RTutorialElevatorExitInteractable tutorialExit = MainEscapeComponentUtility.GetOrAddComponent<RTutorialElevatorExitInteractable>(instance);
        tutorialExit.Configure(
            requiresConfiguredItem: true,
            configuredRequiredItemId: PrototypeItemCatalog.IronGateKeyItemId,
            configuredShowPromptText: false,
            configuredConsumeRequiredItemBeforeExit: false);
        EditorUtility.SetDirty(tutorialExit);
    }

    private static void ConfigureLightOnly(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        SetLayerRecursively(instance, 0);
    }

    private static void ConfigureCueOnly(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);

        for (int index = 0; index < colliders.Length; index++)
        {
            Collider2D collider = colliders[index];

            if (collider != null)
            {
                collider.enabled = false;
                EditorUtility.SetDirty(collider);
            }
        }
    }

    private static void EnsurePickupPrefab(
        Transform root,
        string objectName,
        GameObject prefab,
        Vector2 defaultPosition,
        string itemId,
        int quantity,
        Color color)
    {
        if (prefab == null)
        {
            return;
        }

        Transform existing = FindChildByName(root, objectName);
        Vector3 localPosition = existing != null ? existing.localPosition : (Vector3)defaultPosition;
        WorldInventoryPickupBase existingPickup = existing != null ? existing.GetComponent<WorldInventoryPickupBase>() : null;
        GameObject instance = existing != null ? existing.gameObject : null;

        if (existingPickup == null || PrefabUtility.GetCorrespondingObjectFromSource(instance) == null)
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            instance = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
            instance.name = objectName;
            instance.transform.localPosition = localPosition;
        }

        ConfigureWorldPickup(instance, itemId, quantity, color);
    }

    private static void EnsureKeyPickupPrefab(Transform root, MainEscapeRuntimePrefabCatalog catalog)
    {
        GameObject prefab = catalog != null ? catalog.IronGateKeyVisualPrefab : null;

        if (prefab == null)
        {
            return;
        }

        const string objectName = "IronGateKeyPickup";
        Transform existing = FindChildByName(root, objectName);
        Vector3 localPosition = existing != null ? existing.localPosition : new Vector3(3.55f, 0f, 0f);
        MainEscapeKeyPickup keyPickup = existing != null ? existing.GetComponent<MainEscapeKeyPickup>() : null;
        GameObject instance = existing != null ? existing.gameObject : null;

        if (keyPickup == null || PrefabUtility.GetCorrespondingObjectFromSource(instance) == null)
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            instance = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
            instance.name = objectName;
            instance.transform.localPosition = localPosition;
            keyPickup = instance.GetComponent<MainEscapeKeyPickup>();

            if (keyPickup == null)
            {
                keyPickup = instance.AddComponent<MainEscapeKeyPickup>();
            }
        }

        keyPickup.Configure(
            PrototypeItemCatalog.IronGateKeyItemId,
            PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.IronGateKeyItemId),
            1);
    }

    private static void ConfigureWorldPickup(GameObject instance, string itemId, int quantity, Color color)
    {
        if (instance == null)
        {
            return;
        }

        if (instance.GetComponent<PrototypeInventoryPickup>() is { } pickup)
        {
            pickup.ConfigureAuthored(
                itemId,
                PrototypeItemCatalog.GetDisplayName(itemId),
                Mathf.Max(1, quantity),
                color);
        }
    }

    private static void DisableIfPresent<TComponent>(GameObject instance)
        where TComponent : Behaviour
    {
        TComponent[] components = instance.GetComponentsInChildren<TComponent>(true);

        for (int index = 0; index < components.Length; index++)
        {
            TComponent component = components[index];

            if (component != null)
            {
                component.enabled = false;
                EditorUtility.SetDirty(component);
            }
        }
    }

    private static void SetLayerRecursively(GameObject instance, int layer)
    {
        if (instance == null || layer < 0)
        {
            return;
        }

        Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            Transform child = transforms[index];

            if (child != null)
            {
                child.gameObject.layer = layer;
            }
        }
    }

    private static void SetSpriteSortingBase(GameObject instance, int baseSortingOrder)
    {
        if (instance == null)
        {
            return;
        }

        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers.Length == 0)
        {
            return;
        }

        int minimumOrder = int.MaxValue;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer != null)
            {
                minimumOrder = Mathf.Min(minimumOrder, renderer.sortingOrder);
            }
        }

        if (minimumOrder == int.MaxValue)
        {
            return;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer == null)
            {
                continue;
            }

            renderer.sortingOrder = baseSortingOrder + (renderer.sortingOrder - minimumOrder);
            EditorUtility.SetDirty(renderer);
        }
    }

    private static Transform EnsureChild(Transform parent, string objectName)
    {
        Transform existing = FindChildByName(parent, objectName);

        if (existing != null)
        {
            return existing;
        }

        GameObject child = new(objectName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static Transform EnsureAuthoringRoot(Scene scene)
    {
        Transform root = FindInScene(scene, AuthoringRootName);

        if (root != null)
        {
            return root;
        }

        GameObject rootObject = new(AuthoringRootName);
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        return rootObject.transform;
    }

    private static void EnsureTutorialHudCanvas(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        if (FindComponentInScene<IRHudCanvas>(scene) != null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudCanvasPrefabPath);

        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(RTutorialSceneAuthoringTools)} could not load HUD prefab at '{HudCanvasPrefabPath}'.");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (instance == null)
        {
            return;
        }

        instance.name = "IRHudCanvas";
        EditorUtility.SetDirty(instance);
    }

    private static void EnsureTutorialAuthoredGameplayHud(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        if (FindComponentInScene<IRAuthoredGameplayHudView>(scene) != null)
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AuthoredGameplayHudPrefabPath);

        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(RTutorialSceneAuthoringTools)} could not load authored HUD prefab at '{AuthoredGameplayHudPrefabPath}'.");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

        if (instance == null)
        {
            return;
        }

        instance.name = "RAuthoredGameplayHudCanvas";
        EditorUtility.SetDirty(instance);
    }

    private static void EnsureTutorialGlobalLight(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        Light2D globalLight = FindGlobalLight(scene);

        if (globalLight == null)
        {
            GameObject lightObject = new(GlobalLightName);
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            globalLight = lightObject.AddComponent<Light2D>();
        }

        globalLight.gameObject.name = GlobalLightName;
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.color = Color.white;
        globalLight.intensity = 1f;
        globalLight.volumeIntensity = 0f;
        globalLight.shadowsEnabled = false;
        globalLight.enabled = true;
        EditorUtility.SetDirty(globalLight);
    }

    private static Light2D FindGlobalLight(Scene scene)
    {
        Light2D[] lights = UnityEngine.Object.FindObjectsByType<Light2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int index = 0; index < lights.Length; index++)
        {
            Light2D candidate = lights[index];

            if (candidate != null
                && candidate.gameObject.scene == scene
                && candidate.lightType == Light2D.LightType.Global)
            {
                return candidate;
            }
        }

        Transform named = FindInScene(scene, GlobalLightName);
        return named != null ? named.GetComponent<Light2D>() : null;
    }

    private static TComponent FindComponentInScene<TComponent>(Scene scene)
        where TComponent : Component
    {
        TComponent[] components = UnityEngine.Object.FindObjectsByType<TComponent>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int index = 0; index < components.Length; index++)
        {
            TComponent component = components[index];

            if (component != null && component.gameObject.scene == scene)
            {
                return component;
            }
        }

        return null;
    }

    private static void DisableEditModeAutoMaterialization(RTutorialSceneBootstrap bootstrap)
    {
        SerializedObject serializedObject = new(bootstrap);
        SerializedProperty property = serializedObject.FindProperty("materializeAuthoringInEditMode");

        if (property == null || !property.boolValue)
        {
            return;
        }

        property.boolValue = false;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RTutorialSceneBootstrap FindBootstrap(Scene scene)
    {
        RTutorialSceneBootstrap[] bootstraps = UnityEngine.Object.FindObjectsByType<RTutorialSceneBootstrap>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int index = 0; index < bootstraps.Length; index++)
        {
            RTutorialSceneBootstrap bootstrap = bootstraps[index];

            if (bootstrap != null && bootstrap.gameObject.scene == scene)
            {
                return bootstrap;
            }
        }

        return null;
    }

    private static Transform FindInScene(Scene scene, string objectName)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            Transform match = FindChildByName(rootObjects[index].transform, objectName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < children.Length; index++)
        {
            Transform child = children[index];

            if (child != null && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }
}
#endif

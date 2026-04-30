#if UNITY_EDITOR
using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class RFloorTileAuthoringSetup
{
    private const string SceneRootName = "RSceneRoot";
    private const string AuthoringRootName = "RAuthoring";
    private const string PaletteName = "MainEscapeEditingPalette";
    private const int TileTextureSize = 32;

    private static readonly string[] FloorScenePaths =
    {
        "Assets/Scenes/RMainScene_5F.unity",
        "Assets/Scenes/RMainScene_4F.unity",
        "Assets/Scenes/RMainScene_3F.unity",
        "Assets/Scenes/RMainScene_2F.unity",
        "Assets/Scenes/RMainScene_1F.unity"
    };

    private static void PrepareActiveSceneForTileAuthoring()
    {
        MainEscapeRuntimeSettings runtimeSettings = MainEscapeRuntimeSettings.Load();
        TileAuthoringAssets assets = EnsureTileAuthoringAssets();
        Scene scene = EditorSceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"{nameof(RFloorTileAuthoringSetup)} could not find an active scene to prepare.");
            return;
        }

        PrepareSceneForTileAuthoring(scene, runtimeSettings);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Prepared '{scene.path}' for tile authoring. Palette: {assets.PalettePath}");
    }

    [MenuItem("Tools/Main Escape Rebuild/Prepare R Floors For Tile Authoring (1F-5F)")]
    private static void PrepareAllRFloorScenesForTileAuthoring()
    {
        MainEscapeRuntimeSettings runtimeSettings = MainEscapeRuntimeSettings.Load();
        TileAuthoringAssets assets = EnsureTileAuthoringAssets();
        string originalScenePath = EditorSceneManager.GetActiveScene().path;
        int updatedSceneCount = 0;

        EditorSceneManager.SaveOpenScenes();

        for (int index = 0; index < FloorScenePaths.Length; index++)
        {
            string scenePath = FloorScenePaths[index];

            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"{nameof(RFloorTileAuthoringSetup)} skipped missing scene '{scenePath}'.");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            PrepareSceneForTileAuthoring(scene, runtimeSettings);
            EditorSceneManager.SaveScene(scene);
            updatedSceneCount++;
        }

        if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Prepared {updatedSceneCount} R floor scene(s) for tile authoring. Palette: {assets.PalettePath}");
    }

    private static void PrepareSceneForTileAuthoring(Scene scene, MainEscapeRuntimeSettings runtimeSettings)
    {
        bool changed = false;

        Transform sceneRoot = FindOrCreateSceneRoot(scene, SceneRootName, ref changed);
        Transform authoringRoot = EnsureChild(sceneRoot, AuthoringRootName, ref changed);
        Transform editableFloorRoot = FindEditableFloorRoot(scene, authoringRoot, runtimeSettings.EditableFloorRootName, ref changed);

        Grid grid = editableFloorRoot.GetComponent<Grid>();
        if (grid == null)
        {
            grid = editableFloorRoot.gameObject.AddComponent<Grid>();
            changed = true;
        }

        GridMapService mapService = editableFloorRoot.GetComponent<GridMapService>();
        if (mapService == null)
        {
            mapService = editableFloorRoot.gameObject.AddComponent<GridMapService>();
            changed = true;
        }

        MainEscapeFloorAuthoring floorAuthoring = editableFloorRoot.GetComponent<MainEscapeFloorAuthoring>();
        if (floorAuthoring == null)
        {
            floorAuthoring = editableFloorRoot.gameObject.AddComponent<MainEscapeFloorAuthoring>();
            changed = true;
        }

        Tilemap groundTilemap = EnsureTilemapRoot(editableFloorRoot, runtimeSettings.GroundTilemapName, GameLayers.GroundIndex, 0, false, ref changed);
        Tilemap wallTilemap = EnsureTilemapRoot(editableFloorRoot, runtimeSettings.WallTilemapName, GameLayers.WallIndex, 5, true, ref changed);
        Tilemap doorTilemap = EnsureTilemapRoot(editableFloorRoot, runtimeSettings.DoorTilemapName, GameLayers.DoorIndex, 7, true, ref changed);

        Transform interactivePropsRoot = EnsureChild(editableFloorRoot, runtimeSettings.InteractivePropsRootName, ref changed);
        EnsureChild(interactivePropsRoot, runtimeSettings.GoalVisualsRootName, ref changed);
        Transform visualRoot = EnsureChild(editableFloorRoot, runtimeSettings.VisualRootName, ref changed);
        EnsureChild(visualRoot, runtimeSettings.VisualTilesRootName, ref changed);
        Transform visualPropsRoot = EnsureChild(visualRoot, runtimeSettings.VisualPropsRootName, ref changed);
        EnsureChild(visualPropsRoot, runtimeSettings.MoveOnlyOverlayRootName, ref changed);
        Transform gameplayOverlayRoot = EnsureChild(editableFloorRoot, runtimeSettings.GameplayOverlayRootName, ref changed);
        EnsureTilemapRoot(gameplayOverlayRoot, runtimeSettings.BlockAllOverlayRootName, GameLayers.WallIndex, 9, false, ref changed);
        EnsureTilemapRoot(gameplayOverlayRoot, runtimeSettings.MoveOnlyOverlayRootName, GameLayers.PropIndex, 8, false, ref changed);

        Transform markersRoot = EnsureChild(editableFloorRoot, runtimeSettings.AuthoringMarkersRootName, ref changed);
        EnsureChild(markersRoot, runtimeSettings.DangerMarkersRootName, ref changed);
        EnsureChild(markersRoot, runtimeSettings.DoorMarkersRootName, ref changed);
        EnsureChild(markersRoot, runtimeSettings.ItemPlacementMarkersRootName, ref changed);
        EnsureChild(markersRoot, runtimeSettings.KeyPlacementMarkersRootName, ref changed);
        EnsureChild(markersRoot, runtimeSettings.EnemyPlacementMarkersRootName, ref changed);
        EnsureChild(markersRoot, runtimeSettings.ChaserPlacementMarkersRootName, ref changed);

        Transform ventRouteRoot = EnsureChild(markersRoot, runtimeSettings.VentRouteRootName, ref changed);
        if (ventRouteRoot.GetComponent<MainEscapeVentRouteAuthoring>() == null)
        {
            ventRouteRoot.gameObject.AddComponent<MainEscapeVentRouteAuthoring>();
            changed = true;
        }

        mapService.Initialize(grid, groundTilemap, wallTilemap, doorTilemap, GameLayers.VisionBlockingMask);
        floorAuthoring.CacheReferencesFromHierarchy();

        EditorUtility.SetDirty(grid);
        EditorUtility.SetDirty(mapService);
        EditorUtility.SetDirty(floorAuthoring);
        EditorUtility.SetDirty(groundTilemap);
        EditorUtility.SetDirty(wallTilemap);
        EditorUtility.SetDirty(doorTilemap);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Transform FindOrCreateSceneRoot(Scene scene, string objectName, ref bool changed)
    {
        Transform existing = FindTransformInScene(scene, objectName);

        if (existing != null)
        {
            return existing;
        }

        GameObject sceneRoot = new(objectName);
        SceneManager.MoveGameObjectToScene(sceneRoot, scene);
        changed = true;
        return sceneRoot.transform;
    }

    private static Transform FindEditableFloorRoot(Scene scene, Transform authoringRoot, string editableFloorName, ref bool changed)
    {
        Transform existing = authoringRoot.Find(editableFloorName);

        if (existing != null)
        {
            return existing;
        }

        Transform foundElsewhere = FindTransformInScene(scene, editableFloorName);

        if (foundElsewhere != null)
        {
            foundElsewhere.SetParent(authoringRoot, false);
            changed = true;
            return foundElsewhere;
        }

        return EnsureChild(authoringRoot, editableFloorName, ref changed);
    }

    private static Tilemap EnsureTilemapRoot(
        Transform parent,
        string objectName,
        int layer,
        int sortingOrder,
        bool requiresCollider,
        ref bool changed)
    {
        Transform tilemapTransform = EnsureChild(parent, objectName, ref changed);

        if (tilemapTransform.gameObject.layer != layer)
        {
            tilemapTransform.gameObject.layer = layer;
            changed = true;
        }

        Tilemap tilemap = tilemapTransform.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            tilemap = tilemapTransform.gameObject.AddComponent<Tilemap>();
            changed = true;
        }

        if (tilemap.tileAnchor != new Vector3(0.5f, 0.5f, 0f))
        {
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            changed = true;
        }

        if (tilemap.color != Color.white)
        {
            tilemap.color = Color.white;
            changed = true;
        }

        TilemapRenderer renderer = tilemapTransform.GetComponent<TilemapRenderer>();
        if (renderer == null)
        {
            renderer = tilemapTransform.gameObject.AddComponent<TilemapRenderer>();
            changed = true;
        }

        if (renderer.sortingOrder != sortingOrder)
        {
            renderer.sortingOrder = sortingOrder;
            changed = true;
        }

        TilemapCollider2D collider = tilemapTransform.GetComponent<TilemapCollider2D>();
        if (requiresCollider)
        {
            if (collider == null)
            {
                tilemapTransform.gameObject.AddComponent<TilemapCollider2D>();
                changed = true;
            }
        }
        return tilemap;
    }

    private static Transform EnsureChild(Transform parent, string childName, ref bool changed)
    {
        Transform child = parent != null ? parent.Find(childName) : null;

        if (child != null)
        {
            return child;
        }

        GameObject childObject = new(childName);
        childObject.transform.SetParent(parent, false);
        changed = true;
        return childObject.transform;
    }

    private static Transform FindTransformInScene(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
        {
            Transform found = FindTransformRecursive(roots[index].transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindTransformRecursive(Transform current, string objectName)
    {
        if (current == null)
        {
            return null;
        }

        if (current.name == objectName)
        {
            return current;
        }

        for (int index = 0; index < current.childCount; index++)
        {
            Transform found = FindTransformRecursive(current.GetChild(index), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TileAuthoringAssets EnsureTileAuthoringAssets()
    {
        EnsureFolderPath(MainEscapeTileAssetCatalog.AssetFolder);
        EnsureFolderPath(MainEscapeTileAssetCatalog.TextureAssetFolder);
        EnsureFolderPath(MainEscapeTileAssetCatalog.PaletteAssetFolder);

        Sprite groundSprite = EnsureSpriteAsset(MainEscapeTileAssetCatalog.GroundTextureAssetPath, BuildGroundTexture);
        Sprite wallSprite = EnsureSpriteAsset(MainEscapeTileAssetCatalog.WallTextureAssetPath, BuildWallTexture);
        Sprite doorSprite = EnsureSpriteAsset(MainEscapeTileAssetCatalog.DoorTextureAssetPath, BuildDoorTexture);

        Tile groundTile = EnsureTileAsset(MainEscapeTileAssetCatalog.GroundTileAssetPath, groundSprite, Tile.ColliderType.None);
        Tile wallTile = EnsureTileAsset(MainEscapeTileAssetCatalog.WallTileAssetPath, wallSprite, Tile.ColliderType.Grid);
        Tile doorTile = EnsureTileAsset(MainEscapeTileAssetCatalog.DoorTileAssetPath, doorSprite, Tile.ColliderType.Grid);
        string palettePath = EnsurePaletteAsset(groundTile, wallTile, doorTile);

        return new TileAuthoringAssets(groundTile, wallTile, doorTile, palettePath);
    }

    private static Sprite EnsureSpriteAsset(string textureAssetPath, System.Func<Texture2D> createTexture)
    {
        if (!File.Exists(textureAssetPath))
        {
            Texture2D texture = createTexture();
            File.WriteAllBytes(textureAssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
        }

        TextureImporter importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;

        if (importer != null)
        {
            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spritePixelsPerUnit != TileTextureSize)
            {
                importer.spritePixelsPerUnit = TileTextureSize;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(textureAssetPath);
    }

    private static Tile EnsureTileAsset(string tileAssetPath, Sprite sprite, Tile.ColliderType colliderType)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);

        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = Path.GetFileNameWithoutExtension(tileAssetPath);
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = colliderType;
            AssetDatabase.CreateAsset(tile, tileAssetPath);
            return tile;
        }

        bool changed = false;

        if (tile.sprite != sprite)
        {
            tile.sprite = sprite;
            changed = true;
        }

        if (tile.colliderType != colliderType)
        {
            tile.colliderType = colliderType;
            changed = true;
        }

        if (tile.color != Color.white)
        {
            tile.color = Color.white;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(tile);
        }

        return tile;
    }

    private static string EnsurePaletteAsset(Tile groundTile, Tile wallTile, Tile doorTile)
    {
        string palettePath = ResolvePaletteAssetPath();

        if (string.IsNullOrEmpty(palettePath))
        {
            GridPaletteUtility.CreateNewPalette(
                MainEscapeTileAssetCatalog.PaletteAssetFolder,
                PaletteName,
                GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic,
                Vector3.one,
                GridLayout.CellSwizzle.XYZ);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            palettePath = ResolvePaletteAssetPath();
        }

        if (string.IsNullOrEmpty(palettePath))
        {
            return MainEscapeTileAssetCatalog.PaletteAssetPath;
        }

        GameObject paletteRoot = PrefabUtility.LoadPrefabContents(palettePath);

        try
        {
            Tilemap paletteTilemap = paletteRoot.GetComponentInChildren<Tilemap>(true);
            if (paletteTilemap == null)
            {
                return palettePath;
            }

            paletteTilemap.ClearAllTiles();
            paletteTilemap.SetTile(Vector3Int.zero, groundTile);
            paletteTilemap.SetTile(Vector3Int.right, wallTile);
            paletteTilemap.SetTile(new Vector3Int(2, 0, 0), doorTile);
            paletteTilemap.RefreshAllTiles();
            EditorUtility.SetDirty(paletteTilemap);
            PrefabUtility.SaveAsPrefabAsset(paletteRoot, palettePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(paletteRoot);
        }

        return palettePath;
    }

    private static string ResolvePaletteAssetPath()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MainEscapeTileAssetCatalog.PaletteAssetPath) != null)
        {
            return MainEscapeTileAssetCatalog.PaletteAssetPath;
        }

        string[] paletteGuids = AssetDatabase.FindAssets($"{PaletteName} t:Prefab", new[] { MainEscapeTileAssetCatalog.PaletteAssetFolder });
        return paletteGuids.Length > 0 ? AssetDatabase.GUIDToAssetPath(paletteGuids[0]) : null;
    }

    private static void EnsureFolderPath(string folderPath)
    {
        string normalizedPath = folderPath.Replace('\\', '/');
        string[] segments = normalizedPath.Split('/');
        string currentPath = segments[0];

        for (int index = 1; index < segments.Length; index++)
        {
            string nextPath = $"{currentPath}/{segments[index]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, segments[index]);
            }

            currentPath = nextPath;
        }
    }

    private static Texture2D BuildGroundTexture()
    {
        Texture2D texture = CreateBlankTexture(new Color32(212, 212, 212, 255));

        for (int index = 0; index < TileTextureSize; index++)
        {
            texture.SetPixel(index, 0, new Color32(176, 176, 176, 255));
            texture.SetPixel(index, TileTextureSize - 1, new Color32(176, 176, 176, 255));
            texture.SetPixel(0, index, new Color32(176, 176, 176, 255));
            texture.SetPixel(TileTextureSize - 1, index, new Color32(176, 176, 176, 255));
        }

        for (int index = 4; index < TileTextureSize; index += 8)
        {
            for (int offset = 2; offset < TileTextureSize - 2; offset++)
            {
                texture.SetPixel(index, offset, new Color32(198, 198, 198, 255));
                texture.SetPixel(offset, index, new Color32(198, 198, 198, 255));
            }
        }

        return FinalizeTexture(texture);
    }

    private static Texture2D BuildWallTexture()
    {
        Texture2D texture = CreateBlankTexture(new Color32(236, 236, 236, 255));

        for (int index = 0; index < TileTextureSize; index++)
        {
            texture.SetPixel(index, 0, new Color32(104, 104, 104, 255));
            texture.SetPixel(index, TileTextureSize - 1, new Color32(104, 104, 104, 255));
            texture.SetPixel(0, index, new Color32(104, 104, 104, 255));
            texture.SetPixel(TileTextureSize - 1, index, new Color32(104, 104, 104, 255));
        }

        for (int x = 6; x < TileTextureSize - 6; x++)
        {
            texture.SetPixel(x, 8, new Color32(164, 164, 164, 255));
            texture.SetPixel(x, TileTextureSize - 9, new Color32(164, 164, 164, 255));
        }

        for (int y = 8; y < TileTextureSize - 8; y++)
        {
            texture.SetPixel(8, y, new Color32(164, 164, 164, 255));
            texture.SetPixel(TileTextureSize - 9, y, new Color32(164, 164, 164, 255));
        }

        return FinalizeTexture(texture);
    }

    private static Texture2D BuildDoorTexture()
    {
        Texture2D texture = CreateBlankTexture(new Color32(228, 228, 228, 255));

        for (int index = 0; index < TileTextureSize; index++)
        {
            texture.SetPixel(index, 0, new Color32(88, 88, 88, 255));
            texture.SetPixel(index, TileTextureSize - 1, new Color32(88, 88, 88, 255));
            texture.SetPixel(0, index, new Color32(88, 88, 88, 255));
            texture.SetPixel(TileTextureSize - 1, index, new Color32(88, 88, 88, 255));
        }

        for (int x = 6; x < TileTextureSize - 6; x += 6)
        {
            for (int y = 4; y < TileTextureSize - 4; y++)
            {
                texture.SetPixel(x, y, new Color32(150, 150, 150, 255));
            }
        }

        for (int x = 12; x < TileTextureSize - 12; x++)
        {
            texture.SetPixel(x, 6, new Color32(170, 170, 170, 255));
            texture.SetPixel(x, TileTextureSize - 7, new Color32(170, 170, 170, 255));
        }

        return FinalizeTexture(texture);
    }

    private static Texture2D CreateBlankTexture(Color32 fillColor)
    {
        Texture2D texture = new(TileTextureSize, TileTextureSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[TileTextureSize * TileTextureSize];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = fillColor;
        }

        texture.SetPixels32(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;
        return texture;
    }

    private static Texture2D FinalizeTexture(Texture2D texture)
    {
        texture.Apply();
        return texture;
    }

    private readonly struct TileAuthoringAssets
    {
        public TileAuthoringAssets(Tile groundTile, Tile wallTile, Tile doorTile, string palettePath)
        {
            GroundTile = groundTile;
            WallTile = wallTile;
            DoorTile = doorTile;
            PalettePath = palettePath;
        }

        public Tile GroundTile { get; }
        public Tile WallTile { get; }
        public Tile DoorTile { get; }
        public string PalettePath { get; }
    }
}
#endif

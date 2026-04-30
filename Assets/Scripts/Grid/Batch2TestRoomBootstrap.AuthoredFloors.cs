using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static partial class Batch2TestRoomBootstrap
{
    private static readonly Vector3Int InvalidOptionalAuthoredCell = new(int.MinValue / 4, int.MinValue / 4, 0);
    private const string AuthoredGroundTilemapName = "Tiles_ground";
    private const string AuthoredWallTilemapName = "Tiles_wall";
    private const string AuthoredMoveOnlyTilemapName = "Tiles_movenonlywall";
    private static readonly string[] AuthoredGroundTilemapAliases = { AuthoredGroundTilemapName, "Ground" };
    private static readonly string[] AuthoredWallTilemapAliases = { AuthoredWallTilemapName, "Walls" };
    private static readonly string[] AuthoredMoveOnlyTilemapAliases =
    {
        AuthoredMoveOnlyTilemapName,
        "Tiles_moveonlywall",
        "MoveOnlyWall",
        "MoveOnlyWalls"
    };
    private const string RuntimeDoorTilemapName = "RuntimeDoorTilemap";
    private const string RuntimePropBlockersName = "RuntimePropBlockers";

    public static bool TryBuildSceneResidentAuthoredFloor(EscapeFloorDefinition floorDefinition, out OfficeFloorBuildResult buildResult)
    {
        return TryBuildSceneResidentAuthoredFloor(
            floorDefinition,
            ResolveLoadedSceneForFloor(floorDefinition),
            out buildResult);
    }

    public static bool TryBuildSceneResidentAuthoredFloor(
        EscapeFloorDefinition floorDefinition,
        Scene targetScene,
        out OfficeFloorBuildResult buildResult)
    {
        buildResult = default;

        if (!CanUseSceneResidentAuthoredFloor(floorDefinition, targetScene))
        {
            return false;
        }

        if (!TryResolveSceneResidentFloorAuthoring(targetScene, out MainEscapeFloorAuthoring authoring))
        {
            return false;
        }

        return TryBuildSceneResidentAuthoredFloor(floorDefinition, authoring, out buildResult);
    }

    public static bool TryBuildSceneResidentAuthoredFloor(
        EscapeFloorDefinition floorDefinition,
        MainEscapeFloorAuthoring authoring,
        out OfficeFloorBuildResult buildResult)
    {
        buildResult = default;

        if (floorDefinition == null || authoring == null)
        {
            return false;
        }

        if (!CanUseSceneResidentAuthoredFloor(floorDefinition, authoring.gameObject.scene))
        {
            return false;
        }

        return TryFinalizeAuthoredFloorBuild(authoring.gameObject, authoring, floorDefinition, out buildResult);
    }

    public static bool TryResolveSceneResidentFloorAuthoring(Scene scene, out MainEscapeFloorAuthoring authoring)
    {
        authoring = null;

        if (!scene.IsValid())
        {
            return false;
        }

        authoring = RSceneReferenceLookup.FindFirstComponentInScene<MainEscapeFloorAuthoring>(scene);
        return authoring != null;
    }

    private static bool TryBuildAuthoredOfficeFloor(EscapeFloorDefinition floorDefinition, Transform parent, out OfficeFloorBuildResult buildResult)
    {
        buildResult = default;
        string resourcePath = floorDefinition.GetResolvedAuthoredFloorResourcePath();

        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return false;
        }

        GameObject authoredPrefab = Resources.Load<GameObject>(resourcePath);

        if (authoredPrefab == null)
        {
            return false;
        }

        GameObject root = parent != null
            ? UnityEngine.Object.Instantiate(authoredPrefab, parent, false)
            : UnityEngine.Object.Instantiate(authoredPrefab);

        root.name = $"MainEscapeFloor_{floorDefinition.FloorNumber}F";
        MainEscapeFloorAuthoring authoring = root.GetComponent<MainEscapeFloorAuthoring>();

        if (TryFinalizeAuthoredFloorBuild(root, authoring, floorDefinition, out buildResult))
        {
            return true;
        }

        Debug.LogWarning($"Authored floor prefab '{resourcePath}' is missing required MainEscapeFloorAuthoring references.");
        DestroyFloorObject(root);
        return false;
    }

    private static bool TryFinalizeAuthoredFloorBuild(GameObject root, MainEscapeFloorAuthoring authoring, EscapeFloorDefinition floorDefinition, out OfficeFloorBuildResult buildResult)
    {
        buildResult = default;

        if (root == null || authoring == null || !HasRequiredVisibleAuthoredTilemaps(authoring))
        {
            return false;
        }

        GeneratedFloorLayout existingLayout = root.GetComponent<GeneratedFloorLayout>();

        if (!TryBuildAuthoredSceneMapData(authoring, existingLayout, floorDefinition, out GeneratedMapData mapData))
        {
            return false;
        }

        GridMapService mapService = EnsureAuthoredMapService(root);
        ConfigureAuthoredRuntimeServices(root.transform, authoring, mapService);

        GeneratedFloorLayout layout = EnsureAuthoredLayout(root);
        ApplyAuthoredLayoutData(layout, mapData);

        buildResult = CreateAuthoredFloorBuildResult(root, authoring, mapService, layout);
        return true;
    }

    private static GridMapService EnsureAuthoredMapService(GameObject root)
    {
        GridMapService mapService = root.GetComponent<GridMapService>();

        if (mapService == null)
        {
            mapService = root.AddComponent<GridMapService>();
        }

        return mapService;
    }

    private static void ConfigureAuthoredRuntimeServices(Transform root, MainEscapeFloorAuthoring authoring, GridMapService mapService)
    {
        Tilemap groundTilemap = ResolveVisibleAuthoredTilemap(root, AuthoredGroundTilemapName);
        Tilemap wallTilemap = ResolveVisibleAuthoredTilemap(root, AuthoredWallTilemapName);
        Tilemap moveOnlyTilemap = ResolveVisibleAuthoredTilemap(root, AuthoredMoveOnlyTilemapName);
        Tilemap doorTilemap = ResolveAuthoredDoorTilemap(root, authoring, groundTilemap);

        RemoveChildIfPresent(root, "WallBlockers");
        RemoveChildIfPresent(root, "WallShadowBlockers");
        RemoveChildIfPresent(root, "MoveOnlyBlockers");
        RemoveChildIfPresent(root, "DoorShadowBlockers");
        RemoveChildIfPresent(root, RuntimePropBlockersName);

        mapService.Initialize(authoring.Grid, groundTilemap, wallTilemap, doorTilemap, GameLayers.VisionBlockingMask);
        BuildVisibleWallBlockers(root, wallTilemap, mapService);
        BuildVisibleMoveOnlyBlockers(root, moveOnlyTilemap, mapService);
        authoring.RegisterPropBlockers(mapService);
        BuildVisualPropBlockers(root, authoring, groundTilemap);
        BuildShadowBlockers(root, doorTilemap, GameLayers.DoorIndex, "DoorShadowBlockers", mapService);
        DisableShadowCasters(root.Find("CoverProps"));
        DisableShadowCasters(MainEscapeVisualAuthoringSynthesis.ResolveVisualPropsRoot(root));
        ApplyNoFrictionToDescendantColliders(root);
    }

    private static GeneratedFloorLayout EnsureAuthoredLayout(GameObject root)
    {
        GeneratedFloorLayout layout = root.GetComponent<GeneratedFloorLayout>();

        if (layout == null)
        {
            layout = root.AddComponent<GeneratedFloorLayout>();
        }

        return layout;
    }

    private static void ApplyAuthoredLayoutData(GeneratedFloorLayout layout, GeneratedMapData mapData)
    {
        layout.Configure(
            mapData.PlayerStartCell,
            mapData.KeyCell,
            mapData.BatteryCell,
            mapData.ExitCell,
            mapData.PatrolSpawnCell,
            mapData.GlassPanelCell,
            mapData.SafeRoomCell,
            mapData.MainDoorCells,
            mapData.DangerCells,
            mapData.Zones.ToArray(),
            mapData.RoomRecords.ToArray(),
            mapData.RouteRecords.ToArray(),
            mapData.DoorGroups.ToArray(),
            mapData.CoverProps.ToArray());
    }

    private static OfficeFloorBuildResult CreateAuthoredFloorBuildResult(
        GameObject root,
        MainEscapeFloorAuthoring authoring,
        GridMapService mapService,
        GeneratedFloorLayout layout)
    {
        GetAuthoredFloorBounds(authoring, out Vector2 worldCenter, out Vector2 worldSize);
        ConfigureAuthoredFloorPresentation(root.transform, authoring, worldCenter, worldSize);
        Vector3Int[] patrolRoute = authoring.GetPatrolRouteCells();
        Vector3Int[] patrolSpawnCells = authoring.GetPatrolSpawnCells();
        Vector3Int stalkerSpawnCell = ResolveAuthoredStalkerSpawnCell(root, authoring);
        MainEscapeSentrySpawnPoint[] sentrySpawns = authoring.GetSentrySpawnPoints();
        MainEscapeVentRouteDefinition ventRoute = authoring.GetVentRouteDefinition();
        bool hasPlayerSpawnWorldPosition = authoring.TryResolvePlayerStartWorldPosition(out Vector3 playerSpawnWorldPosition);

        return new OfficeFloorBuildResult(
            root.transform,
            mapService,
            layout,
            worldCenter,
            worldSize,
            patrolRoute,
            patrolSpawnCells,
            stalkerSpawnCell,
            sentrySpawns,
            ventRoute,
            isAuthored: true,
            hasPlayerSpawnWorldPosition: hasPlayerSpawnWorldPosition,
            playerSpawnWorldPosition: playerSpawnWorldPosition);
    }

    private static Vector3Int ResolveAuthoredStalkerSpawnCell(GameObject root, MainEscapeFloorAuthoring authoring)
    {
        if (authoring.TryResolveStalkerSpawnCell(out Vector3Int stalkerSpawnCell))
        {
            return stalkerSpawnCell;
        }

        Debug.LogWarning(
            $"Authored floor '{root.name}' is missing an explicit stalker spawn marker. The clean loop will skip stalker spawn until one is authored.",
            root);
        return Vector3Int.zero;
    }

    private static bool TryBuildAuthoredSceneMapData(
        MainEscapeFloorAuthoring authoring,
        GeneratedFloorLayout existingLayout,
        EscapeFloorDefinition floorDefinition,
        out GeneratedMapData data)
    {
        data = new GeneratedMapData();

        if (existingLayout != null)
        {
            data.Zones.AddRange(existingLayout.Zones);
            data.RoomRecords.AddRange(existingLayout.Rooms);
            data.RouteRecords.AddRange(existingLayout.Routes);
            data.CoverProps.AddRange(existingLayout.CoverProps);
        }

        bool valid = true;

        valid &= TryResolveRequiredAuthoringCell(authoring.TryResolvePlayerStartCell, "player start", out data.PlayerStartCell, authoring);
        valid &= TryResolveRequiredAuthoringCell(authoring.TryResolveToolCell, "tool", out data.KeyCell, authoring);
        valid &= TryResolveRequiredAuthoringCell(authoring.TryResolveTransitionCell, "transition", out data.ExitCell, authoring);
        TryResolveOptionalAuthoringCell(authoring.TryResolveSafeRoomCell, "safe room", out data.SafeRoomCell, authoring, data.PlayerStartCell);
        TryResolveOptionalAuthoringCell(authoring.TryResolveBatteryCell, "battery", out data.BatteryCell, authoring, InvalidOptionalAuthoredCell);
        TryResolveOptionalAuthoringCell(authoring.TryResolveGlassPanelCell, "glass bottle", out data.GlassPanelCell, authoring, InvalidOptionalAuthoredCell);

        data.DangerCells = authoring.GetDangerCells();
        data.MainDoorCells = authoring.GetMainDoorCells();
        data.DoorGroups.AddRange(authoring.BuildDoorGroups());

        if (authoring.TryResolvePatrolSpawnCell(out Vector3Int patrolSpawnCell))
        {
            data.PatrolSpawnCell = patrolSpawnCell;
        }
        else
        {
            data.PatrolSpawnCell = existingLayout != null
                ? existingLayout.PatrolSpawnCell
                : data.DangerCells.Length > 0
                    ? data.DangerCells[0]
                    : data.KeyCell;

            if (ShouldLogOptionalAuthoringFallbackWarnings())
            {
                Debug.LogWarning(
                    $"Authored floor '{authoring.name}' is missing an explicit patrol spawn marker. Runtime will fall back to the cached patrol spawn cell or nearby walkable cells.",
                    authoring);
            }
        }

        bool isCurrentAuthoredFloor = IsCurrentAuthoredFloor(authoring, floorDefinition);

        if (isCurrentAuthoredFloor && data.MainDoorCells.Length == 0 && !UsesDirectAuthoredExitInteraction(authoring))
        {
            Debug.Log(
                $"Authored floor '{authoring.name}' is using scene-authored swing doors without a keyed main gate. Exit flow stays on the direct authored interaction path.",
                authoring);
        }

        return valid;
    }

    private static bool CanUseSceneResidentAuthoredFloor(EscapeFloorDefinition floorDefinition, Scene activeScene)
    {
        if (floorDefinition == null || !activeScene.IsValid())
        {
            return false;
        }

        return new RRuntimeSettingsFloorResidencyPolicy().RequiresSceneResidentAuthoring(floorDefinition, activeScene);
    }

    private static bool UsesDirectAuthoredExitInteraction(MainEscapeFloorAuthoring authoring)
    {
        if (authoring == null)
        {
            return false;
        }

        Scene scene = authoring.gameObject.scene;

        if (!scene.IsValid())
        {
            return false;
        }

        RRunController runController = RSceneReferenceLookup.FindFirstComponentInScene<RRunController>(scene);
        return runController != null && runController.UsesDirectAuthoredExitInteraction;
    }

    private static bool IsCurrentAuthoredFloor(MainEscapeFloorAuthoring authoring, EscapeFloorDefinition floorDefinition)
    {
        if (authoring == null || floorDefinition == null)
        {
            return false;
        }

        Scene scene = authoring.gameObject.scene;

        if (Application.isPlaying && scene.IsValid())
        {
            RRunSessionController sessionController = RRunSessionResolver.ResolveForScene(scene);

            if (sessionController != null
                && sessionController.TryResolveFloorNumberForScene(scene, out int sceneFloorNumber))
            {
                return floorDefinition.FloorNumber == sceneFloorNumber;
            }

            if (sessionController != null)
            {
                return false;
            }
        }

        return floorDefinition.FloorNumber == MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
    }

    private static bool TryResolveRequiredAuthoringCell(
        TryResolveCellDelegate resolver,
        string label,
        out Vector3Int cell,
        MainEscapeFloorAuthoring authoring)
    {
        if (resolver(out cell))
        {
            return true;
        }

        Debug.LogError(
            $"Authored floor '{authoring.name}' is missing an explicit {label} marker. Clean loop no longer falls back to serialized layout or floor-definition defaults.",
            authoring);
        return false;
    }

    private static bool TryResolveOptionalAuthoringCell(
        TryResolveCellDelegate resolver,
        string label,
        out Vector3Int cell,
        MainEscapeFloorAuthoring authoring,
        Vector3Int fallbackCell)
    {
        if (resolver(out cell))
        {
            return true;
        }

        cell = fallbackCell;

        if (ShouldLogOptionalAuthoringFallbackWarnings())
        {
            Debug.LogWarning(
                $"Authored floor '{authoring.name}' is missing an explicit {label} marker. Core floor build will continue and the subsystem will use a fallback placeholder until that marker is authored.",
                authoring);
        }

        return false;
    }

    private static bool ShouldLogOptionalAuthoringFallbackWarnings()
    {
        try
        {
            return MainEscapeRuntimeSettings.Load().LogOptionalAuthoringFallbackWarnings;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private delegate bool TryResolveCellDelegate(out Vector3Int cell);

    private static void GetAuthoredFloorBounds(MainEscapeFloorAuthoring authoring, out Vector2 worldCenter, out Vector2 worldSize)
    {
        if (authoring == null)
        {
            worldCenter = Vector2.zero;
            worldSize = Vector2.one;
            return;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;
        bool hasOccupiedCells = false;

        CollectTilemapBounds(ResolveVisibleAuthoredTilemap(authoring.transform, AuthoredGroundTilemapName), ref minX, ref maxX, ref minY, ref maxY, ref hasOccupiedCells);
        CollectTilemapBounds(ResolveVisibleAuthoredTilemap(authoring.transform, AuthoredWallTilemapName), ref minX, ref maxX, ref minY, ref maxY, ref hasOccupiedCells);
        CollectTilemapBounds(ResolveVisibleAuthoredTilemap(authoring.transform, AuthoredMoveOnlyTilemapName), ref minX, ref maxX, ref minY, ref maxY, ref hasOccupiedCells);

        if (!hasOccupiedCells)
        {
            Tilemap fallbackTilemap = ResolveVisibleAuthoredTilemap(authoring.transform, AuthoredGroundTilemapName)
                ?? ResolveVisibleAuthoredTilemap(authoring.transform, AuthoredWallTilemapName)
                ?? ResolveVisibleAuthoredTilemap(authoring.transform, AuthoredMoveOnlyTilemapName);
            worldCenter = fallbackTilemap != null ? (Vector2)fallbackTilemap.transform.position : Vector2.zero;
            worldSize = Vector2.one;
            return;
        }

        worldCenter = new Vector2((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f);
        worldSize = new Vector2((maxX - minX) + WorldBoundsPadding, (maxY - minY) + WorldBoundsPadding);
    }

    private static void CollectTilemapBounds(
        Tilemap tilemap,
        ref int minX,
        ref int maxX,
        ref int minY,
        ref int maxY,
        ref bool hasOccupiedCells)
    {
        if (tilemap == null)
        {
            return;
        }

        BoundsInt bounds = tilemap.cellBounds;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
            {
                continue;
            }

            hasOccupiedCells = true;
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }
    }

    private static void ConfigureAuthoredFloorPresentation(Transform root, MainEscapeFloorAuthoring authoring, Vector2 worldCenter, Vector2 worldSize)
    {
        if (root == null || authoring == null)
        {
            return;
        }

        NormalizeAuthoredVisualSources(root, authoring);
        RemoveChildIfPresent(root, "UnifiedFloorBackdrop");
        SetTilemapRendererEnabled(ResolveVisibleAuthoredTilemap(root, AuthoredGroundTilemapName), true);
    }

    private static void NormalizeAuthoredVisualSources(Transform root, MainEscapeFloorAuthoring authoring)
    {
        SetTilemapRendererEnabled(ResolveVisibleAuthoredTilemap(root, AuthoredGroundTilemapName), true);
        SetTilemapRendererEnabled(ResolveVisibleAuthoredTilemap(root, AuthoredWallTilemapName), true);
        SetTilemapRendererEnabled(ResolveVisibleAuthoredTilemap(root, AuthoredMoveOnlyTilemapName), true);
    }

    private static bool HasRequiredVisibleAuthoredTilemaps(MainEscapeFloorAuthoring authoring)
    {
        if (authoring == null)
        {
            return false;
        }

        Transform root = authoring.transform;
        return ResolveVisibleAuthoredTilemap(root, AuthoredGroundTilemapName) != null
            && ResolveVisibleAuthoredTilemap(root, AuthoredWallTilemapName) != null;
    }

    private static Tilemap ResolveVisibleAuthoredTilemap(Transform root, string tilemapName)
    {
        if (root == null || string.IsNullOrWhiteSpace(tilemapName))
        {
            return null;
        }

        string[] aliases = ResolveAuthoredTilemapAliases(tilemapName);
        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
        Tilemap fallbackCandidate = null;

        for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
        {
            string alias = aliases[aliasIndex];
            Tilemap enabledCandidate = null;

            for (int index = 0; index < tilemaps.Length; index++)
            {
                Tilemap tilemap = tilemaps[index];

                if (tilemap == null
                    || !string.Equals(tilemap.gameObject.name, alias, StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsPreferredVisibleAuthoredTilemap(tilemap))
                {
                    return tilemap;
                }

                if (enabledCandidate == null && tilemap.gameObject.activeInHierarchy)
                {
                    enabledCandidate = tilemap;
                }

                fallbackCandidate ??= tilemap;
            }

            if (enabledCandidate != null)
            {
                return enabledCandidate;
            }
        }

        return fallbackCandidate;
    }

    private static bool IsPreferredVisibleAuthoredTilemap(Tilemap tilemap)
    {
        if (tilemap == null || !tilemap.gameObject.activeInHierarchy)
        {
            return false;
        }

        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
        return renderer == null || renderer.enabled;
    }

    private static string[] ResolveAuthoredTilemapAliases(string tilemapName)
    {
        if (string.Equals(tilemapName, AuthoredGroundTilemapName, StringComparison.Ordinal))
        {
            return AuthoredGroundTilemapAliases;
        }

        if (string.Equals(tilemapName, AuthoredWallTilemapName, StringComparison.Ordinal))
        {
            return AuthoredWallTilemapAliases;
        }

        if (string.Equals(tilemapName, AuthoredMoveOnlyTilemapName, StringComparison.Ordinal))
        {
            return AuthoredMoveOnlyTilemapAliases;
        }

        return new[] { tilemapName };
    }

    private static Tilemap ResolveAuthoredDoorTilemap(Transform root, MainEscapeFloorAuthoring authoring, Tilemap groundTilemap)
    {
        Tilemap authoredDoorTilemap = authoring != null ? authoring.DoorTilemap : null;

        if (HasAnyTiles(authoredDoorTilemap))
        {
            return authoredDoorTilemap;
        }

        RemoveChildIfPresent(root, RuntimeDoorTilemapName);

        if (authoring == null || groundTilemap == null)
        {
            return null;
        }

        GeneratedDoorGroupData[] doorGroups = authoring.BuildDoorGroups();

        if (doorGroups == null || doorGroups.Length == 0)
        {
            return null;
        }

        GameObject tilemapObject = new(RuntimeDoorTilemapName);
        tilemapObject.transform.SetParent(root, false);
        tilemapObject.layer = GameLayers.DoorIndex;
        Tilemap doorTilemap = tilemapObject.AddComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
        TilemapCollider2D tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();
        tilemapRenderer.enabled = false;
        TopDownCollisionMaterialUtility.ApplyNoFriction(tilemapCollider);

        Tile runtimeDoorTile = ScriptableObject.CreateInstance<Tile>();
        runtimeDoorTile.name = "RuntimeDoorTile";
        runtimeDoorTile.hideFlags = HideFlags.HideAndDontSave;

        for (int groupIndex = 0; groupIndex < doorGroups.Length; groupIndex++)
        {
            Vector3Int[] cells = doorGroups[groupIndex].Cells;

            for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                doorTilemap.SetTile(cells[cellIndex], runtimeDoorTile);
            }
        }

        return doorTilemap;
    }

    private static bool HasAnyTiles(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return false;
        }

        BoundsInt bounds = tilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cellPosition))
            {
                return true;
            }
        }

        return false;
    }

    private static void BuildVisualPropBlockers(Transform root, MainEscapeFloorAuthoring authoring, Tilemap groundTilemap)
    {
        if (root == null || authoring == null || groundTilemap == null)
        {
            return;
        }

        Vector3Int[] propCells = authoring.GetVisualPropCells();

        if (propCells.Length == 0)
        {
            return;
        }

        GameObject blockerRoot = new(RuntimePropBlockersName);
        blockerRoot.transform.SetParent(root, false);

        for (int index = 0; index < propCells.Length; index++)
        {
            Vector3Int cellPosition = propCells[index];
            GameObject blocker = new($"PropBlocker_{cellPosition.x}_{cellPosition.y}");
            blocker.layer = GameLayers.PropIndex;
            blocker.transform.SetParent(blockerRoot.transform, false);
            blocker.transform.position = groundTilemap.GetCellCenterWorld(cellPosition);

            BoxCollider2D boxCollider = blocker.AddComponent<BoxCollider2D>();
            boxCollider.size = Vector2.one;
            boxCollider.offset = Vector2.zero;
            boxCollider.isTrigger = false;
            TopDownCollisionMaterialUtility.ApplyNoFriction(boxCollider);
        }
    }

    private static void DisableShadowCasters(Transform root)
    {
        if (root == null)
        {
            return;
        }

        ShadowCaster2D[] shadowCasters = root.GetComponentsInChildren<ShadowCaster2D>(true);

        for (int index = 0; index < shadowCasters.Length; index++)
        {
            ShadowCaster2D shadowCaster = shadowCasters[index];

            if (shadowCaster != null)
            {
                shadowCaster.enabled = false;
            }
        }
    }

    private static void BuildVisibleWallBlockers(Transform parent, Tilemap wallTilemap, GridMapService mapService)
    {
        if (wallTilemap == null)
        {
            return;
        }

        GameObject blockerRoot = new("WallBlockers");
        blockerRoot.transform.SetParent(parent, false);

        BoundsInt bounds = wallTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!wallTilemap.HasTile(cellPosition))
            {
                continue;
            }

            GameObject blocker = new($"WallBlocker_{cellPosition.x}_{cellPosition.y}");
            blocker.layer = GameLayers.WallIndex;
            blocker.transform.SetParent(blockerRoot.transform, false);
            blocker.transform.position = wallTilemap.GetCellCenterWorld(cellPosition);

            BoxCollider2D boxCollider = blocker.AddComponent<BoxCollider2D>();
            boxCollider.size = Vector2.one;
            boxCollider.offset = Vector2.zero;
            boxCollider.isTrigger = false;

            if (!RuntimeShadowCaster2DConfigurator.TryConfigureFromCollider(blocker, boxCollider, out _))
            {
                Debug.LogWarning($"Failed to configure wall blocker shadow caster at {cellPosition}.", blocker);
            }

            mapService?.RegisterPropCell(cellPosition);
        }
    }

    private static void BuildVisibleMoveOnlyBlockers(Transform parent, Tilemap moveOnlyTilemap, GridMapService mapService)
    {
        if (moveOnlyTilemap == null)
        {
            return;
        }

        GameObject blockerRoot = new("MoveOnlyBlockers");
        blockerRoot.transform.SetParent(parent, false);

        BoundsInt bounds = moveOnlyTilemap.cellBounds;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!moveOnlyTilemap.HasTile(cellPosition))
            {
                continue;
            }

            GameObject blocker = new($"MoveOnlyBlocker_{cellPosition.x}_{cellPosition.y}");
            blocker.layer = GameLayers.PropIndex;
            blocker.transform.SetParent(blockerRoot.transform, false);
            blocker.transform.position = moveOnlyTilemap.GetCellCenterWorld(cellPosition);

            BoxCollider2D boxCollider = blocker.AddComponent<BoxCollider2D>();
            boxCollider.size = Vector2.one;
            boxCollider.offset = Vector2.zero;
            boxCollider.isTrigger = false;

            mapService?.RegisterMovementBlockingCell(cellPosition);
        }
    }

    private static void SetTilemapRendererEnabled(Tilemap tilemap, bool enabled)
    {
        if (tilemap == null)
        {
            return;
        }

        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();

        if (renderer != null)
        {
            renderer.enabled = enabled;
        }
    }

    private static void DestroyFloorObject(GameObject floorObject)
    {
        if (floorObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(floorObject);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(floorObject);
        }
    }

    private static void RemoveChildIfPresent(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return;
        }

        Transform child = parent.Find(childName);

        if (child == null)
        {
            return;
        }

        DestroyFloorObject(child.gameObject);
    }
}

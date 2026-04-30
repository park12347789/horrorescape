using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MainEscapeRuntimeValidator
{
    private const string CanonicalLobbyScenePath = "Assets/Scenes/RMainEscape_Lobby.unity";
    private const string TutorialSupportScenePath = "Assets/Scenes/RMainEscape_tuto.unity";
    private const string FrontDoorPrefabPath = "Assets/Prefabs/Environment/MainEscape/Vexed/TileBSplitDoors/VexedTileBProp_01_Top.prefab";
    private const string SideDoorPrefabPath = "Assets/Prefabs/Environment/MainEscape/Doors/CustomSideDoorClosed.prefab";
    private const int WarmupFrameCount = 20;
    private const string ValidationRequestedKey = "MainEscapeRuntimeValidator.Requested";
    private const string WarmupFramesKey = "MainEscapeRuntimeValidator.WarmupFrames";
    private const string LobbyRoutingSnapshotKey = "MainEscapeRuntimeValidator.LobbyRoutingSnapshot";
    private const string LobbyRoutingSnapshotLobbyScenePathKey = "MainEscapeRuntimeValidator.LobbyRoutingSnapshot.LobbyScenePath";
    private const string LobbyRoutingSnapshotStartingFloorNumberKey = "MainEscapeRuntimeValidator.LobbyRoutingSnapshot.StartingFloorNumber";
    private const string LobbyRoutingSnapshotUseSceneLocalRoutingOverridesKey = "MainEscapeRuntimeValidator.LobbyRoutingSnapshot.UseSceneLocalRoutingOverrides";
    private const string LobbyRoutingSnapshotFloorScenesCountKey = "MainEscapeRuntimeValidator.LobbyRoutingSnapshot.FloorScenes.Count";
    private const string LobbyRoutingSnapshotFloorSceneFloorNumberKeyPrefix = "MainEscapeRuntimeValidator.LobbyRoutingSnapshot.FloorScenes.FloorNumber.";
    private const string LobbyRoutingSnapshotFloorScenePathKeyPrefix = "MainEscapeRuntimeValidator.LobbyRoutingSnapshot.FloorScenes.ScenePath.";
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const float ThreatCueIntensityFloor = 0.20f;
    private const float VisibleAlphaFloor = 0.08f;

    private static readonly Vector3Int[] CardinalNeighborOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    };

    private static bool validationRequested;
    private static bool validationExecuted;
    private static int warmupFramesRemaining;

    [Serializable]
    private sealed class LobbySceneRoutingSnapshot
    {
        public string lobbyScenePath;
        public int startingFloorNumber;
        public bool useSceneLocalRoutingOverrides;
        public RFloorSceneEntry[] floorScenes;

        public string GetStartFloorScenePath()
        {
            RFloorSceneEntry[] routes = floorScenes ?? Array.Empty<RFloorSceneEntry>();

            for (int index = 0; index < routes.Length; index++)
            {
                RFloorSceneEntry route = routes[index];

                if (route.floorNumber == Mathf.Max(1, startingFloorNumber) && !string.IsNullOrWhiteSpace(route.scenePath))
                {
                    return route.scenePath.Trim();
                }
            }

            return string.Empty;
        }
    }

    static MainEscapeRuntimeValidator()
    {
        validationRequested = TryRestorePendingValidationRequest();
        warmupFramesRemaining = validationRequested ? SessionState.GetInt(WarmupFramesKey, 0) : 0;
        EditorApplication.update += HandleEditorUpdate;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Main Escape/Validate Start Floor Runtime")]
    public static void ValidateWorkingSceneRuntime()
    {
        if (validationRequested)
        {
            Debug.LogWarning("[MainEscapeRuntimeValidator] Validation is already running.");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[MainEscapeRuntimeValidator] Wait for the current play mode transition to finish before validating.");
            return;
        }

        if (!TryReadLobbySceneRoutingSnapshot(out LobbySceneRoutingSnapshot routingSnapshot, out string routingError))
        {
            Debug.LogError($"[MainEscapeRuntimeValidator] Could not resolve lobby scene routing from the authored scene source: {routingError}");
            return;
        }

        CacheLobbySceneRoutingSnapshot(routingSnapshot);
        string startFloorScenePath = routingSnapshot.GetStartFloorScenePath();

        if (string.IsNullOrWhiteSpace(startFloorScenePath) || !System.IO.File.Exists(startFloorScenePath))
        {
            Debug.LogError($"[MainEscapeRuntimeValidator] Start-floor scene path from the lobby scene routing is missing or invalid: '{startFloorScenePath}'.");
            return;
        }

        EditorSceneManager.OpenScene(startFloorScenePath);
        validationRequested = true;
        validationExecuted = false;
        warmupFramesRemaining = WarmupFrameCount;
        SessionState.SetBool(ValidationRequestedKey, true);
        SessionState.SetInt(WarmupFramesKey, WarmupFrameCount);
        Debug.Log("[MainEscapeRuntimeValidator] Starting start-floor runtime validation for the live floor-chain loop.");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Main Escape/Validate Lobby Scene Preflight")]
    public static void ValidateLobbyScenePreflight()
    {
        List<string> passes = new();
        List<string> failures = new();

        bool hasLobbyRoutingSnapshot = TryReadLobbySceneRoutingSnapshot(out LobbySceneRoutingSnapshot routingSnapshot, out string routingError);
        Require(
            hasLobbyRoutingSnapshot,
            $"Could not resolve lobby scene routing from '{CanonicalLobbyScenePath}': {routingError}",
            "Lobby scene routing resolved from the authored lobby scene.",
            failures,
            passes);

        if (hasLobbyRoutingSnapshot)
        {
            string lobbyScenePath = routingSnapshot.lobbyScenePath;
            string startFloorScenePath = routingSnapshot.GetStartFloorScenePath();

            Require(!string.IsNullOrWhiteSpace(lobbyScenePath), "Lobby scene routing is missing a lobby path.", $"Lobby scene path = {lobbyScenePath}", failures, passes);
            Require(
                !string.IsNullOrWhiteSpace(startFloorScenePath) && System.IO.File.Exists(startFloorScenePath),
                $"Lobby scene routing is missing an existing start-floor scene path: '{startFloorScenePath}'.",
                $"Start-floor scene path exists: {startFloorScenePath}",
                failures,
                passes);
            passes.Add(
                routingSnapshot.useSceneLocalRoutingOverrides
                    ? "Lobby scene legacy routing flag is enabled; scene-local serialized routes are still used."
                    : "Lobby scene legacy routing flag is disabled; scene-local serialized routes are still used.");

            ValidateLobbyAuthoredBindings(failures, passes);
        }

        Type sessionControllerType = FindTypeByName("RRunSessionController");
        Require(
            sessionControllerType != null,
            "RRunSessionController type is missing. Lobby -> gameplay loop persistence cannot be validated.",
            "RRunSessionController type resolved.",
            failures,
            passes);

        if (failures.Count == 0)
        {
            Debug.Log("[MainEscapeRuntimeValidator][LobbyPreflight] PASS\n- " + string.Join("\n- ", passes));
            return;
        }

        Debug.LogError(
            "[MainEscapeRuntimeValidator][LobbyPreflight] FAIL\n- " + string.Join("\n- ", failures)
            + (passes.Count > 0 ? "\nPasses\n- " + string.Join("\n- ", passes) : string.Empty));
    }

    [MenuItem("Tools/Main Escape/Report Floor Scene Contracts")]
    public static void ReportFloorSceneContracts()
    {
        List<string> lines = new();
        List<string> warnings = new();

        if (!TryReadLobbySceneRoutingSnapshot(out LobbySceneRoutingSnapshot routingSnapshot, out string routingError))
        {
            Debug.LogError($"[MainEscapeRuntimeValidator][FloorSceneContracts] Could not resolve lobby scene routing: {routingError}");
            return;
        }

        SceneSetup[] originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            RFloorSceneEntry[] routes = routingSnapshot.floorScenes ?? Array.Empty<RFloorSceneEntry>();
            lines.Add(
                "Authored scene contract report. Source of truth is the lobby-authored floor routing plus each opened floor scene hierarchy.");
            lines.Add(
                $"Lobby route source={routingSnapshot.lobbyScenePath}, startingFloor={Mathf.Max(1, routingSnapshot.startingFloorNumber)}F, floorRoutes={routes.Length}.");

            foreach (RFloorSceneEntry route in routes.OrderByDescending(route => route.floorNumber))
            {
                int floorNumber = Mathf.Max(1, route.floorNumber);

                if (string.IsNullOrWhiteSpace(route.scenePath) || !System.IO.File.Exists(route.scenePath))
                {
                    warnings.Add($"{floorNumber}F scene path is missing or invalid: '{route.scenePath}'.");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(route.scenePath, OpenSceneMode.Single);
                MainEscapeFloorAuthoring floorAuthoring = Object.FindFirstObjectByType<MainEscapeFloorAuthoring>();

                if (floorAuthoring == null)
                {
                    warnings.Add($"{floorNumber}F is missing {nameof(MainEscapeFloorAuthoring)}.");
                    continue;
                }

                floorAuthoring.CacheReferencesFromHierarchy();

                MainEscapeItemPlacementMarker[] supportMarkers = floorAuthoring.GetSupportItemPlacementMarkers();
                MainEscapeItemPlacementMarker[] keyMarkers = floorAuthoring.GetKeyPlacementMarkers();
                MainEscapeDangerPlacementMarker[] dangerMarkers = floorAuthoring.GetDangerPlacementMarkers();
                MainEscapeEnemyPlacementMarker[] sharedEnemyMarkers = floorAuthoring.GetSharedEnemyPlacementMarkers();
                MainEscapeEnemyPlacementMarker[] chaserMarkers = floorAuthoring.GetChaserPlacementMarkers();
                MainEscapeSupportItemPlacementQuota supportQuota = floorAuthoring.SupportItemPlacementQuota;
                MainEscapeEnemyPlacementQuota enemyQuota = floorAuthoring.EnemyPlacementQuota;

                FixedSupportPickupSummary fixedSupport = CountFixedSupportPickups(floorAuthoring);
                int supportMarkerCount = CountUnityObjects(supportMarkers);
                int keyMarkerCount = CountUnityObjects(keyMarkers);
                int dangerMarkerCount = CountUnityObjects(dangerMarkers);
                int sharedEnemyMarkerCount = CountUnityObjects(sharedEnemyMarkers);
                int chaserMarkerCount = CountUnityObjects(chaserMarkers);
                int currentRuntimeSupportTarget = supportMarkerCount > 0
                    ? Mathf.Min(supportMarkerCount, supportQuota.TotalCount)
                    : 0;
                string chaserSource = chaserMarkerCount > 0
                    ? "chaser markers"
                    : enemyQuota.ChaserCount > 0
                        ? "implicit fallback"
                        : "none";

                lines.Add($"{floorNumber}F authored scene path: {route.scenePath}.");
                lines.Add($"{floorNumber}F floor authoring root: {BuildTransformPath(floorAuthoring.transform)}.");
                lines.Add(
                    $"{floorNumber}F [AuthoredSceneContract] support markers={supportMarkerCount}, key markers={keyMarkerCount}, danger markers={dangerMarkerCount}, shared enemy markers={sharedEnemyMarkerCount}, chaser markers={chaserMarkerCount}.");
                lines.Add(
                    $"{floorNumber}F authored fixed support pickups: battery={fixedSupport.BatteryCount}, glassBottle={fixedSupport.GlassBottleCount}, medkit={fixedSupport.MedkitCount}, total={fixedSupport.TotalCount}.");
                lines.Add(
                    $"{floorNumber}F support quota data: battery={supportQuota.BatteryCount}, glassBottle={supportQuota.GlassBottleCount}, medkit={supportQuota.MedkitCount}, total={supportQuota.TotalCount}; current runtime random support target={currentRuntimeSupportTarget}.");
                lines.Add(
                    $"{floorNumber}F support marker pool status: populated={DescribeMarkerPoolPopulation(supportMarkerCount)}, empty={DescribeMarkerPoolEmpty(supportMarkerCount)}, legacyQuotaBalance={DescribeMarkerPoolQuotaBalance(supportMarkerCount, supportQuota.TotalCount)}, markers={supportMarkerCount}, legacySupportQuotaTotal={supportQuota.TotalCount}, delta={supportMarkerCount - supportQuota.TotalCount}, legacyClampTarget={currentRuntimeSupportTarget}.");
                lines.Add(
                    $"{floorNumber}F enemy quota data: patrol={enemyQuota.PatrolCount}, sentry={enemyQuota.SentryCount}, chaser={enemyQuota.ChaserCount}; chaser source={chaserSource}; glass trap quota={floorAuthoring.GlassTrapPlacementCount}.");
                AppendSupportPickupManagerAuthoringCandidate(
                    scene,
                    floorAuthoring,
                    floorNumber,
                    supportQuota,
                    supportMarkerCount,
                    lines,
                    warnings);
                AppendSceneMarkerPlacementManagerReport(floorAuthoring, floorNumber, lines, warnings);
                AppendRegularDoorContractReport(scene, floorAuthoring, $"{floorNumber}F", lines, warnings);

                if (fixedSupport.TotalCount > 0 && currentRuntimeSupportTarget > 0)
                {
                    lines.Add(
                        $"{floorNumber}F has authored fixed support pickups and a runtime-managed support placement layer; this is explicit mixed ownership, not marker-quota validation.");
                }

                if (floorNumber == routingSnapshot.startingFloorNumber
                    && fixedSupport.GlassBottleCount == 3
                    && fixedSupport.BatteryCount == 2
                    && fixedSupport.MedkitCount == 2)
                {
                    lines.Add($"{floorNumber}F fixed starter item set matches the intended 3 bottles, 2 batteries, and 2 medkits.");
                }

                if (enemyQuota.ChaserCount > 0 && chaserMarkerCount == 0)
                {
                    warnings.Add(
                        $"{floorNumber}F requests chaser quota {enemyQuota.ChaserCount} but has no authored chaser marker. Current behavior depends on fallback, not visible scene markers.");
                }

                Require(
                    scene.IsValid() && floorAuthoring.gameObject.scene == scene,
                    $"{floorNumber}F floor authoring resolved outside the opened scene.",
                    $"{floorNumber}F floor authoring resolved from the opened scene.",
                    warnings,
                    lines);
            }

            AppendSupportSceneRegularDoorContractReport(TutorialSupportScenePath, "Tutorial", lines, warnings);
        }
        catch (Exception exception)
        {
            warnings.Add($"Unhandled exception while reporting floor scene contracts: {exception}");
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
        }

        string report = "[MainEscapeRuntimeValidator][FloorSceneContracts][AuthoredSceneContract]\n- " + string.Join("\n- ", lines);

        if (warnings.Count > 0)
        {
            Debug.LogWarning(report + "\nWarnings\n- " + string.Join("\n- ", warnings));
            return;
        }

        Debug.Log(report);
    }

    private readonly struct FixedSupportPickupSummary
    {
        public FixedSupportPickupSummary(int batteryCount, int glassBottleCount, int medkitCount)
        {
            BatteryCount = Mathf.Max(0, batteryCount);
            GlassBottleCount = Mathf.Max(0, glassBottleCount);
            MedkitCount = Mathf.Max(0, medkitCount);
        }

        public int BatteryCount { get; }
        public int GlassBottleCount { get; }
        public int MedkitCount { get; }
        public int TotalCount => BatteryCount + GlassBottleCount + MedkitCount;
    }

    private static FixedSupportPickupSummary CountFixedSupportPickups(MainEscapeFloorAuthoring floorAuthoring)
    {
        if (floorAuthoring == null)
        {
            return new FixedSupportPickupSummary(0, 0, 0);
        }

        PrototypeInventoryPickup[] pickups = floorAuthoring.GetComponentsInChildren<PrototypeInventoryPickup>(true);
        int batteryCount = 0;
        int glassBottleCount = 0;
        int medkitCount = 0;

        for (int index = 0; index < pickups.Length; index++)
        {
            PrototypeInventoryPickup pickup = pickups[index];

            if (pickup == null || !pickup.SuppressRuntimeManagedPickupReplacement)
            {
                continue;
            }

            if (string.Equals(pickup.ItemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal))
            {
                batteryCount++;
            }
            else if (string.Equals(pickup.ItemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal))
            {
                glassBottleCount++;
            }
            else if (string.Equals(pickup.ItemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal))
            {
                medkitCount++;
            }
        }

        return new FixedSupportPickupSummary(batteryCount, glassBottleCount, medkitCount);
    }

    private static void AppendSupportSceneRegularDoorContractReport(
        string scenePath,
        string sceneLabel,
        List<string> lines,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(scenePath) || !System.IO.File.Exists(scenePath))
        {
            warnings?.Add($"{sceneLabel} support scene path is missing or invalid: '{scenePath}'.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        MainEscapeFloorAuthoring floorAuthoring = Object.FindFirstObjectByType<MainEscapeFloorAuthoring>(FindObjectsInactive.Include);

        if (floorAuthoring != null)
        {
            floorAuthoring.CacheReferencesFromHierarchy();
        }

        AppendRegularDoorContractReport(scene, floorAuthoring, sceneLabel, lines, warnings);
    }

    private static void AppendRegularDoorContractReport(
        Scene scene,
        MainEscapeFloorAuthoring floorAuthoring,
        string sceneLabel,
        List<string> lines,
        List<string> warnings)
    {
        if (!scene.IsValid() || lines == null || warnings == null)
        {
            return;
        }

        MainEscapeDoorVisualVariantOverride[] regularDoorOverrides = Object
            .FindObjectsByType<MainEscapeDoorVisualVariantOverride>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(variantOverride =>
                variantOverride != null
                && variantOverride.gameObject.scene == scene
                && IsRegularDoorVariant(variantOverride.VisualVariant))
            .OrderBy(variantOverride => BuildTransformPath(variantOverride.transform))
            .ToArray();
        HashSet<int> explicitRegularDoorRootIds = new();
        int frontDoorCount = 0;
        int sideDoorCount = 0;
        int missingSelfContainedCount = 0;
        int nonPrefabCount = 0;
        int wrongPrefabSourceCount = 0;

        for (int index = 0; index < regularDoorOverrides.Length; index++)
        {
            MainEscapeDoorVisualVariantOverride variantOverride = regularDoorOverrides[index];
            string rootPath = BuildTransformPath(variantOverride.transform);
            MainEscapeSelfContainedDoor selfContainedDoor = variantOverride.GetComponent<MainEscapeSelfContainedDoor>();
            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(variantOverride.gameObject);
            string expectedPrefabPath = ExpectedRegularDoorPrefabPath(variantOverride.VisualVariant);
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(variantOverride.gameObject);

            explicitRegularDoorRootIds.Add(variantOverride.gameObject.GetInstanceID());

            if (variantOverride.VisualVariant == MainEscapeDoorVisualVariantKind.SideDoor42)
            {
                sideDoorCount++;
            }
            else
            {
                frontDoorCount++;
            }

            if (selfContainedDoor == null)
            {
                missingSelfContainedCount++;
                warnings.Add(
                    $"{sceneLabel} regular door root '{rootPath}' is missing {nameof(MainEscapeSelfContainedDoor)}.");
            }

            if (!isPrefabInstance || string.IsNullOrWhiteSpace(prefabPath))
            {
                nonPrefabCount++;
                warnings.Add(
                    $"{sceneLabel} regular door root '{rootPath}' is not backed by a prefab instance.");
                continue;
            }

            if (!string.Equals(prefabPath, expectedPrefabPath, StringComparison.Ordinal))
            {
                wrongPrefabSourceCount++;
                warnings.Add(
                    $"{sceneLabel} regular door root '{rootPath}' resolves prefab '{prefabPath}', expected '{expectedPrefabPath}'.");
            }
        }

        Transform[] namedRegularDoorRoots = FindNamedRegularDoorRoots(scene);
        int looseVisualOnlyCount = 0;

        for (int index = 0; index < namedRegularDoorRoots.Length; index++)
        {
            Transform root = namedRegularDoorRoots[index];

            if (root == null || explicitRegularDoorRootIds.Contains(root.gameObject.GetInstanceID()))
            {
                continue;
            }

            looseVisualOnlyCount++;
            warnings.Add(
                $"{sceneLabel} regular door visual '{BuildTransformPath(root)}' still exists as a name-based scene object without the explicit prefabized contract.");
        }

        string authoredGroupCoverage = "n/a";

        if (floorAuthoring != null && floorAuthoring.gameObject.scene == scene)
        {
            floorAuthoring.CacheReferencesFromHierarchy();
            Tilemap groundTilemap = floorAuthoring.GroundTilemap;

            if (groundTilemap == null)
            {
                warnings.Add($"{sceneLabel} regular door contract could not resolve the ground tilemap.");
            }
            else
            {
                GeneratedDoorGroupData[] doorGroups = floorAuthoring.BuildDoorGroups();
                HashSet<string> groupSignatures = new(
                    (doorGroups ?? Array.Empty<GeneratedDoorGroupData>())
                        .Where(group => group.Cells != null && group.Cells.Length > 0)
                        .Select(group => BuildDoorCellSignature(group.Cells)),
                    StringComparer.Ordinal);
                int matchedGroups = 0;

                for (int index = 0; index < regularDoorOverrides.Length; index++)
                {
                    MainEscapeSelfContainedDoor selfContainedDoor = regularDoorOverrides[index] != null
                        ? regularDoorOverrides[index].GetComponent<MainEscapeSelfContainedDoor>()
                        : null;

                    if (selfContainedDoor == null)
                    {
                        continue;
                    }

                    string cellSignature = BuildDoorCellSignature(selfContainedDoor.ResolveDoorCells(groundTilemap));

                    if (groupSignatures.Contains(cellSignature))
                    {
                        matchedGroups++;
                    }
                    else
                    {
                        warnings.Add(
                            $"{sceneLabel} regular door root '{BuildTransformPath(selfContainedDoor.transform)}' did not round-trip into {nameof(MainEscapeFloorAuthoring)}.{nameof(MainEscapeFloorAuthoring.BuildDoorGroups)}().");
                    }
                }

                authoredGroupCoverage = $"{matchedGroups}/{regularDoorOverrides.Length}";
            }
        }

        lines.Add(
            $"{sceneLabel} regular doors: total={regularDoorOverrides.Length}, front={frontDoorCount}, side={sideDoorCount}, looseVisualOnly={looseVisualOnlyCount}, missingSelfContained={missingSelfContainedCount}, nonPrefab={nonPrefabCount}, wrongPrefabSource={wrongPrefabSourceCount}, authoredGroupCoverage={authoredGroupCoverage}.");
    }

    private static int CountUnityObjects<T>(IEnumerable<T> objects)
        where T : Object
    {
        if (objects == null)
        {
            return 0;
        }

        int count = 0;

        foreach (T candidate in objects)
        {
            if (candidate != null)
            {
                count++;
            }
        }

        return count;
    }

    private static string DescribeMarkerPoolPopulation(int markerCount)
    {
        return markerCount > 0 ? "populated" : "empty";
    }

    private static string DescribeMarkerPoolEmpty(int markerCount)
    {
        return markerCount > 0 ? "false" : "true";
    }

    private static string DescribeMarkerPoolQuotaBalance(int markerCount, int quotaTotal)
    {
        if (markerCount <= 0)
        {
            return quotaTotal > 0 ? "empty" : "empty-no-legacy-quota";
        }

        if (quotaTotal <= 0)
        {
            return "surplus";
        }

        if (markerCount < quotaTotal)
        {
            return "shortage";
        }

        if (markerCount > quotaTotal)
        {
            return "surplus";
        }

        return "matched";
    }

    private static void AppendSupportPickupManagerAuthoringCandidate(
        Scene scene,
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        MainEscapeSupportItemPlacementQuota supportQuota,
        int supportMarkerCount,
        List<string> lines,
        List<string> warnings)
    {
        const string markerRootPath = "AuthoringMarkers/ItemPlacementMarkers";
        const string runtimeRootName = "00_Pickups";

        if (lines == null || warnings == null)
        {
            return;
        }

        Transform markerRoot = FindChildByPath(floorAuthoring != null ? floorAuthoring.transform : null, "AuthoringMarkers", "ItemPlacementMarkers");
        Transform runtimeRoot = FindFirstSceneTransform(runtimeRootName);
        MainEscapeRuntimePrefabCatalog catalog = scene.IsValid()
            ? MainEscapeRuntimePrefabCatalog.LoadForScene(scene)
            : MainEscapeRuntimePrefabCatalog.Load();
        PrototypeInventoryPickup batteryPrefab = catalog != null ? catalog.FlashlightBatteryPickupPrefab : null;
        PrototypeInventoryPickup glassBottlePrefab = catalog != null ? catalog.GlassBottlePickupPrefab : null;
        PrototypeInventoryPickup medkitPrefab = catalog != null ? catalog.MedkitPickupPrefab : null;
        int totalCountCandidate = supportQuota.TotalCount;
        int expectedPlacementCap = Mathf.Min(supportMarkerCount, totalCountCandidate);

        lines.Add(
            $"{floorNumber}F support item manager manual authoring candidate: manager={nameof(RSceneMarkerPlacementManager)}, kind={nameof(RSceneMarkerPlacementKind.SupportPickup)}, markerRoot={markerRootPath}, runtimeRoot={runtimeRootName}, clearRuntimeRootOnApply=true, avoidMarkersUsedByEarlierRules=true.");
        lines.Add(
            $"{floorNumber}F support item manager count candidates use current support quota B/G/M: battery={supportQuota.BatteryCount}, glassBottle={supportQuota.GlassBottleCount}, medkit={supportQuota.MedkitCount}, total={totalCountCandidate}; this is not 1:1 identical to the legacy weighted random support placement.");
        lines.Add(
            $"{floorNumber}F support item manager prefab candidates must use {nameof(MainEscapeRuntimePrefabCatalog)} pickups: battery={DescribeSupportPickupPrefabCandidate(nameof(MainEscapeRuntimePrefabCatalog.FlashlightBatteryPickupPrefab), batteryPrefab)}, glassBottle={DescribeSupportPickupPrefabCandidate(nameof(MainEscapeRuntimePrefabCatalog.GlassBottlePickupPrefab), glassBottlePrefab)}, medkit={DescribeSupportPickupPrefabCandidate(nameof(MainEscapeRuntimePrefabCatalog.MedkitPickupPrefab), medkitPrefab)}.");
        lines.Add(
            $"{floorNumber}F support item manager shared marker cap candidate: markerCandidates={supportMarkerCount}, totalCountCandidate={totalCountCandidate}, expectedMaxPlacements={expectedPlacementCap}; separate battery/glassBottle/medkit rules should share the same markerRoot with avoidMarkersUsedByEarlierRules=true.");
        lines.Add(
            $"{floorNumber}F support item manager root resolution: markerRootResolved={BuildTransformPath(markerRoot)}, runtimeRootResolved={BuildTransformPath(runtimeRoot)}.");

        if (floorNumber == 5)
        {
            lines.Add(
                "5F fixed starter pickups and the random support layer must stay separated: fixed starter pickups remain authored scene/prefab objects, while SupportPickup manager rules should write only under 00_Pickups.");
        }

        if (catalog == null)
        {
            warnings.Add(
                $"{floorNumber}F support item manager candidate could not load {nameof(MainEscapeRuntimePrefabCatalog)}; prefab assignment must use the catalog battery/glassBottle/medkit pickup prefabs.");
        }

        AppendMissingSupportPickupPrefabWarning(
            floorNumber,
            nameof(MainEscapeRuntimePrefabCatalog.FlashlightBatteryPickupPrefab),
            batteryPrefab,
            supportQuota.BatteryCount,
            warnings);
        AppendMissingSupportPickupPrefabWarning(
            floorNumber,
            nameof(MainEscapeRuntimePrefabCatalog.GlassBottlePickupPrefab),
            glassBottlePrefab,
            supportQuota.GlassBottleCount,
            warnings);
        AppendMissingSupportPickupPrefabWarning(
            floorNumber,
            nameof(MainEscapeRuntimePrefabCatalog.MedkitPickupPrefab),
            medkitPrefab,
            supportQuota.MedkitCount,
            warnings);

        if (supportQuota.TotalCount > 0 && markerRoot == null)
        {
            warnings.Add(
                $"{floorNumber}F support item manager candidate expects markerRoot={markerRootPath}, but that root was not resolved under {nameof(MainEscapeFloorAuthoring)}.");
        }

        if (supportQuota.TotalCount > 0 && runtimeRoot == null)
        {
            warnings.Add(
                $"{floorNumber}F support item manager candidate expects runtimeRoot={runtimeRootName}; author it manually before applying the SupportPickup rules.");
        }
    }

    private static string DescribeSupportPickupPrefabCandidate(string catalogPropertyName, PrototypeInventoryPickup prefab)
    {
        string propertyPath = $"{nameof(MainEscapeRuntimePrefabCatalog)}.{catalogPropertyName}.gameObject";

        if (prefab == null)
        {
            return $"{propertyPath}(<missing>)";
        }

        return $"{propertyPath}('{prefab.gameObject.name}')";
    }

    private static void AppendMissingSupportPickupPrefabWarning(
        int floorNumber,
        string catalogPropertyName,
        PrototypeInventoryPickup prefab,
        int requestedCount,
        List<string> warnings)
    {
        if (warnings == null || requestedCount <= 0 || prefab != null)
        {
            return;
        }

        warnings.Add(
            $"{floorNumber}F support item manager candidate requests {requestedCount} placement(s), but {nameof(MainEscapeRuntimePrefabCatalog)}.{catalogPropertyName} is missing.");
    }

    private static Transform FindChildByPath(Transform root, params string[] pathSegments)
    {
        if (root == null || pathSegments == null || pathSegments.Length == 0)
        {
            return null;
        }

        Transform current = root;

        for (int pathIndex = 0; pathIndex < pathSegments.Length; pathIndex++)
        {
            current = FindDirectChild(current, pathSegments[pathIndex]);

            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
        {
            Transform child = parent.GetChild(childIndex);

            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static void AppendSceneMarkerPlacementManagerReport(
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        List<string> lines,
        List<string> warnings)
    {
        if (floorAuthoring == null)
        {
            return;
        }

        Scene floorScene = floorAuthoring.gameObject.scene;
        RSceneMarkerPlacementManager[] placementManagers = Object
            .FindObjectsByType<RSceneMarkerPlacementManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(placementManager => placementManager != null && placementManager.gameObject.scene == floorScene)
            .OrderBy(placementManager => BuildTransformPath(placementManager.transform))
            .ToArray();
        int managerCount = CountUnityObjects(placementManagers);

        if (managerCount == 0)
        {
            lines?.Add(
                $"{floorNumber}F authored {nameof(RSceneMarkerPlacementManager)} contracts: status=no-manager, not placed. Random support placement still depends on the legacy support quota/marker path unless a manager is authored under {nameof(MainEscapeFloorAuthoring)}.");
            return;
        }

        int floorAuthoringScopedCount = placementManagers.Count(placementManager =>
            placementManager != null && placementManager.transform.IsChildOf(floorAuthoring.transform));

        lines.Add(
            $"{floorNumber}F authored {nameof(RSceneMarkerPlacementManager)} contracts: instances={managerCount}, underFloorAuthoring={floorAuthoringScopedCount}. " +
            $"{nameof(RFloorDirector)} applies only active/enabled managers under {nameof(MainEscapeFloorAuthoring)}.");

        for (int index = 0; index < placementManagers.Length; index++)
        {
            RSceneMarkerPlacementManager placementManager = placementManagers[index];

            if (placementManager == null)
            {
                continue;
            }

            RSceneMarkerPlacementManager.ContractSummary contractSummary = placementManager.BuildContractSummary();
            string managerPath = BuildTransformPath(placementManager.transform);
            bool underFloorAuthoring = placementManager.transform.IsChildOf(floorAuthoring.transform);
            bool runtimeEligible = underFloorAuthoring && placementManager.isActiveAndEnabled;
            string managerStatus = DescribeSceneMarkerPlacementManagerStatus(
                placementManager,
                underFloorAuthoring,
                contractSummary,
                out int supportRuleCount,
                out int supportInputRuleCount,
                out int supportItemIdCount);

            lines.Add(
                $"{floorNumber}F {nameof(RSceneMarkerPlacementManager)}[{index}] status={managerStatus}, path={managerPath}, underFloorAuthoring={underFloorAuthoring}, activeAndEnabled={placementManager.isActiveAndEnabled}, runtimeEligible={runtimeEligible}, runtimeRoot={BuildTransformPath(contractSummary.RuntimeRoot)}, clearRuntimeRootOnApply={contractSummary.ClearRuntimeRootOnApply}, rules={contractSummary.RuleCount}, supportRules={supportRuleCount}, supportRulesWithInputs={supportInputRuleCount}, supportItemIds={supportItemIdCount}.");

            if (!underFloorAuthoring)
            {
                warnings.Add(
                    $"{floorNumber}F {nameof(RSceneMarkerPlacementManager)} '{managerPath}' is authored outside {nameof(MainEscapeFloorAuthoring)}, so {nameof(RFloorDirector)} will not apply it.");
            }

            if (underFloorAuthoring && !placementManager.isActiveAndEnabled)
            {
                warnings.Add(
                    $"{floorNumber}F {nameof(RSceneMarkerPlacementManager)} '{managerPath}' is authored but inactive/disabled, so runtime placement will skip it.");
            }

            for (int ruleIndex = 0; ruleIndex < contractSummary.Rules.Count; ruleIndex++)
            {
                AppendSceneMarkerPlacementRuleReport(
                    floorNumber,
                    managerPath,
                    ruleIndex,
                    runtimeEligible,
                    contractSummary.Rules[ruleIndex],
                    lines,
                    warnings);
            }
        }
    }

    private static string DescribeSceneMarkerPlacementManagerStatus(
        RSceneMarkerPlacementManager placementManager,
        bool underFloorAuthoring,
        RSceneMarkerPlacementManager.ContractSummary contractSummary,
        out int supportRuleCount,
        out int supportInputRuleCount,
        out int supportItemIdCount)
    {
        supportRuleCount = 0;
        supportInputRuleCount = 0;
        supportItemIdCount = 0;

        IReadOnlyList<RSceneMarkerPlacementManager.PlacementRuleContractSummary> rules =
            contractSummary.Rules ?? Array.Empty<RSceneMarkerPlacementManager.PlacementRuleContractSummary>();

        for (int index = 0; index < rules.Count; index++)
        {
            RSceneMarkerPlacementManager.PlacementRuleContractSummary ruleSummary = rules[index];

            if (!ruleSummary.HasRule || ruleSummary.PlacementKind != RSceneMarkerPlacementKind.SupportPickup)
            {
                continue;
            }

            supportRuleCount++;

            if (ruleSummary.HasPlacementInputs)
            {
                supportInputRuleCount++;
            }

            if (TryGetSupportPickupItemId(ruleSummary.Prefab, out _))
            {
                supportItemIdCount++;
            }
        }

        if (placementManager == null || !underFloorAuthoring || !placementManager.isActiveAndEnabled)
        {
            return "inactive";
        }

        if (contractSummary.RuntimeRoot == null)
        {
            return "missing runtimeRoot";
        }

        if (supportInputRuleCount == 0)
        {
            return "missing SupportPickup rule/input";
        }

        if (supportItemIdCount == 0)
        {
            return "no support item ids";
        }

        return "ready";
    }

    private static void AppendSceneMarkerPlacementRuleReport(
        int floorNumber,
        string managerPath,
        int ruleIndex,
        bool managerRuntimeEligible,
        RSceneMarkerPlacementManager.PlacementRuleContractSummary ruleSummary,
        List<string> lines,
        List<string> warnings)
    {
        if (!ruleSummary.HasRule)
        {
            warnings.Add($"{floorNumber}F {managerPath} rule[{ruleIndex}] is missing from the placement manager contract.");
            return;
        }

        string ruleId = ruleSummary.RuleId;
        string placementKind = ruleSummary.PlacementKind.ToString();
        GameObject prefab = ruleSummary.Prefab;
        int requestedCount = ruleSummary.RequestedCount;
        int candidateCount = ruleSummary.CandidateCount;
        int expectedPlacementCount = ruleSummary.ExpectedPlacementCount;
        string markerSource = DescribeSceneMarkerPlacementRuleSource(ruleSummary);
        string prefabName = prefab != null ? prefab.name : "<none>";
        string supportItemId = TryGetSupportPickupItemId(prefab, out string resolvedSupportItemId)
            ? resolvedSupportItemId
            : "<none>";
        bool ruleCanApply = managerRuntimeEligible && ruleSummary.HasPlacementInputs;

        lines.Add(
            $"{floorNumber}F {managerPath} rule[{ruleIndex}] id='{ruleId}', kind={placementKind}, prefab={prefabName}, supportItemId={supportItemId}, requested={requestedCount}, candidates={candidateCount}, expectedPlacements={expectedPlacementCount}, canApplyNow={ruleCanApply}, source={markerSource}, avoidUsed={ruleSummary.AvoidMarkersUsedByEarlierRules}, useRotation={ruleSummary.UseMarkerRotation}, seedOffset={ruleSummary.SeedOffset}.");

        if (requestedCount > 0 && prefab == null)
        {
            warnings.Add(
                $"{floorNumber}F {managerPath} rule[{ruleIndex}] '{ruleId}' requests {requestedCount} placement(s) but has no prefab.");
        }

        if (prefab != null && requestedCount > 0 && candidateCount == 0)
        {
            warnings.Add(
                $"{floorNumber}F {managerPath} rule[{ruleIndex}] '{ruleId}' has prefab '{prefab.name}' but no resolved marker candidates.");
        }
    }

    private static bool TryGetSupportPickupItemId(GameObject prefab, out string itemId)
    {
        itemId = string.Empty;

        if (prefab == null)
        {
            return false;
        }

        PrototypeInventoryPickup pickup = prefab.GetComponent<PrototypeInventoryPickup>();

        if (pickup == null || string.IsNullOrWhiteSpace(pickup.ItemId))
        {
            return false;
        }

        itemId = pickup.ItemId.Trim();
        return true;
    }

    private static string DescribeSceneMarkerPlacementRuleSource(
        RSceneMarkerPlacementManager.PlacementRuleContractSummary ruleSummary)
    {
        if (ruleSummary.UsesExplicitMarkers)
        {
            return $"explicitMarkers={ruleSummary.ExplicitMarkerReferenceCount}/{ruleSummary.ExplicitMarkerSlotCount}";
        }

        if (ruleSummary.MarkerRoot == null)
        {
            return "markerSource=<none>";
        }

        return $"markerRoot={BuildTransformPath(ruleSummary.MarkerRoot)}, includeInactive={ruleSummary.IncludeInactiveMarkers}";
    }

    private static Transform[] FindNamedRegularDoorRoots(Scene scene)
    {
        return Object
            .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(transform =>
                transform != null
                && transform.gameObject.scene == scene
                && IsNamedRegularDoorRoot(transform))
            .OrderBy(transform => BuildTransformPath(transform))
            .ToArray();
    }

    private static bool IsRegularDoorVariant(MainEscapeDoorVisualVariantKind variant)
    {
        return variant == MainEscapeDoorVisualVariantKind.FrontDoor
            || variant == MainEscapeDoorVisualVariantKind.SideDoor42;
    }

    private static bool IsNamedRegularDoorRoot(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        string name = transform.name ?? string.Empty;
        return name.StartsWith("VexedTileBProp_01_Top", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("CustomSideDoorClosed", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpectedRegularDoorPrefabPath(MainEscapeDoorVisualVariantKind variant)
    {
        return variant == MainEscapeDoorVisualVariantKind.SideDoor42
            ? SideDoorPrefabPath
            : FrontDoorPrefabPath;
    }

    private static string BuildDoorCellSignature(IReadOnlyList<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return string.Empty;
        }

        Vector3Int[] sortedCells = new Vector3Int[cells.Count];

        for (int index = 0; index < cells.Count; index++)
        {
            sortedCells[index] = cells[index];
        }

        Array.Sort(sortedCells, CompareDoorCells);
        return string.Join("|", sortedCells.Select(cell => $"{cell.x},{cell.y},{cell.z}"));
    }

    private static int CompareDoorCells(Vector3Int left, Vector3Int right)
    {
        int xCompare = left.x.CompareTo(right.x);
        if (xCompare != 0)
        {
            return xCompare;
        }

        int yCompare = left.y.CompareTo(right.y);
        if (yCompare != 0)
        {
            return yCompare;
        }

        return left.z.CompareTo(right.z);
    }

    private static string BuildTransformPath(Transform transform)
    {
        if (transform == null)
        {
            return "<none>";
        }

        Stack<string> pathSegments = new();
        Transform current = transform;

        while (current != null)
        {
            pathSegments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", pathSegments);
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (!validationRequested)
        {
            return;
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            validationRequested = SessionState.GetBool(ValidationRequestedKey, validationRequested);
            warmupFramesRemaining = SessionState.GetInt(WarmupFramesKey, WarmupFrameCount);
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            ResetValidationState();
        }
    }

    private static void HandleEditorUpdate()
    {
        if (!validationRequested || validationExecuted || !EditorApplication.isPlaying)
        {
            return;
        }

        if (warmupFramesRemaining-- > 0)
        {
            SessionState.SetInt(WarmupFramesKey, warmupFramesRemaining);
            return;
        }

        SessionState.SetInt(WarmupFramesKey, 0);
        validationExecuted = true;
        RunValidation();
    }

    private static void RunValidation()
    {
        MainEscapeRuntimeSettings settings = MainEscapeRuntimeSettings.Load();
        LobbySceneRoutingSnapshot routingSnapshot = LoadCachedLobbySceneRoutingSnapshot();
        List<string> passes = new();
        List<string> failures = new();

        try
        {
            RSceneCompositionRoot compositionRoot = Object.FindFirstObjectByType<RSceneCompositionRoot>();
            WasdPlayerController player = Object.FindFirstObjectByType<WasdPlayerController>();
            RPlayerRuntimeReferences playerRuntime = player != null
                ? player.GetComponent<RPlayerRuntimeReferences>()
                : Object.FindFirstObjectByType<RPlayerRuntimeReferences>();
            RRunController runController = Object.FindFirstObjectByType<RRunController>();
            RRunSessionController activeSessionController = Object.FindFirstObjectByType<RRunSessionController>(FindObjectsInactive.Include);
            RFloorDirector floorDirector = Object.FindFirstObjectByType<RFloorDirector>();
            MainEscapeFloorAuthoring floorAuthoring = Object.FindFirstObjectByType<MainEscapeFloorAuthoring>();
            PlayerInventory inventory = playerRuntime != null
                ? playerRuntime.Inventory
                : player != null ? player.GetComponent<PlayerInventory>() : null;
            MainEscapeDebugModeController debugModeController = Object.FindFirstObjectByType<MainEscapeDebugModeController>();
            FlashlightFogOfWarOverlay fogOfWarOverlay = Object.FindFirstObjectByType<FlashlightFogOfWarOverlay>();
            IRHudCanvas hudCanvas = Object.FindFirstObjectByType<IRHudCanvas>();
            GridMapService mapService = Object.FindFirstObjectByType<GridMapService>();
            IRGameClearPanelView gameClearPanel = hudCanvas != null
                ? hudCanvas.GameClearPanel
                : Object.FindFirstObjectByType<IRGameClearPanelView>();
            IRThreatPanelView threatPanel = hudCanvas != null
                ? hudCanvas.ThreatPanel
                : Object.FindFirstObjectByType<IRThreatPanelView>();
            IRPlayerThreatHudBinder threatFeedbackHud = player != null
                ? player.GetComponent<IRPlayerThreatHudBinder>()
                : null;
            threatFeedbackHud ??= Object.FindFirstObjectByType<IRPlayerThreatHudBinder>();

            Require(compositionRoot != null, "Composition root is missing.", "Composition root resolved.", failures, passes);
            Require(player != null, "Player runtime is missing.", "Player runtime resolved.", failures, passes);
            Require(runController != null, "Run controller is missing.", "Run controller resolved.", failures, passes);
            Require(floorDirector != null, "Floor director is missing.", "Floor director resolved.", failures, passes);
            Require(floorAuthoring != null, "Floor authoring root is missing.", "Floor authoring resolved.", failures, passes);
            Require(inventory != null, "Player inventory is missing.", "Player inventory resolved.", failures, passes);
            if (debugModeController != null)
            {
                passes.Add("Debug mode controller resolved.");
            }
            else
            {
                passes.Add("Debug mode controller is not authored in this scene; debug fog toggle validation skipped.");
            }
            Require(fogOfWarOverlay != null, "Fog of war overlay is missing.", "Fog of war overlay resolved.", failures, passes);
            Require(hudCanvas != null, "HUD canvas is missing.", "HUD canvas resolved.", failures, passes);
            Require(mapService != null, "GridMapService is missing.", "GridMapService resolved.", failures, passes);
            Require(gameClearPanel != null, "Run modal panel (IRGameClearPanelView) is missing.", "Run modal panel resolved.", failures, passes);

            ValidateLobbySceneRoutingSurface(routingSnapshot, failures, passes);
            ValidateSessionContracts(failures, passes);
            ValidateCompositionRootAuthoredReferences(
                compositionRoot,
                floorAuthoring != null && floorAuthoring.HasSupportItemPlacementMarkers(),
                floorAuthoring != null && floorAuthoring.HasKeyPlacementMarkers(),
                failures,
                passes);
            ValidateRunModalSurface(gameClearPanel, failures, passes);
            ValidateEnemyChasePathCoverage(mapService, floorAuthoring, player, failures, passes);
            ValidateThreatReadabilityCoverage(threatFeedbackHud, threatPanel, failures, passes);

            if (floorAuthoring != null)
            {
                Transform workspaceRoot = floorAuthoring.transform.parent;
                Require(workspaceRoot != null, "Editable workspace root is missing.", "Editable workspace root resolved.", failures, passes);

                if (workspaceRoot != null)
                {
                    string[] editorOnlyRoots = settings.EditorOnlyWorkspaceRootNames;

                    for (int index = 0; index < editorOnlyRoots.Length; index++)
                    {
                        string rootName = editorOnlyRoots[index];
                        Transform root = workspaceRoot.Find(rootName);
                        Require(
                            root == null || root.gameObject.activeSelf == false,
                            $"{rootName} root is still active during play.",
                            root == null
                                ? $"{rootName} root is absent during play."
                                : $"{rootName} root is disabled during play.",
                            failures,
                            passes);
                    }
                }

                bool hasSupportItemPlacementMarkers = floorAuthoring.HasSupportItemPlacementMarkers();
                bool hasKeyPlacementMarkers = floorAuthoring.HasKeyPlacementMarkers();

                WorldInventoryPickupBase[] authoredInventoryPickups = Object
                    .FindObjectsByType<WorldInventoryPickupBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(pickup => pickup != null && pickup.transform.IsChildOf(floorAuthoring.transform))
                    .ToArray();
                PrototypeInventoryPickup[] authoredPickups = authoredInventoryPickups
                    .OfType<PrototypeInventoryPickup>()
                    .Where(pickup => pickup != null && pickup.transform.IsChildOf(floorAuthoring.transform))
                    .ToArray();
                PrototypeInventoryPickup authoredBattery = authoredPickups
                    .FirstOrDefault(pickup => pickup != null && pickup.ItemId == PrototypeItemCatalog.FlashlightBatteryItemId);
                MainEscapeKeyPickup authoredKey = authoredInventoryPickups
                    .OfType<MainEscapeKeyPickup>()
                    .FirstOrDefault(pickup => pickup != null && pickup.ItemId == PrototypeItemCatalog.IronGateKeyItemId);
                FloorEscapeTransitionPoint finalExitPoint = GetSerializedFieldValue<FloorEscapeTransitionPoint>(compositionRoot, "finalExitPoint");
                MainEscapeEmergencyStairsPoint authoredStairsPoint = Object
                    .FindObjectsByType<MainEscapeEmergencyStairsPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(point => point != null && point.gameObject.scene == floorAuthoring.gameObject.scene);
                MainEscapeKeyGatePoint authoredKeyGate = Object
                    .FindObjectsByType<MainEscapeKeyGatePoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(point => point != null && point.gameObject.scene == floorAuthoring.gameObject.scene);
                MainEscapeElevatorExitInteractable elevatorExitInteractable = Object
                    .FindObjectsByType<MainEscapeElevatorExitInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(point => point != null && point.gameObject.scene == floorAuthoring.gameObject.scene);
                Transform authoredKeyGateVisual = FindFirstSceneTransform(settings.KeyGateVisualName, "RKeyGateVisual");
                Transform authoredElevatorVisual = FindFirstSceneTransform("RDirectExitElevatorVisual", "elevator");
                int currentFloorNumber = runController != null ? runController.CurrentFloorNumber : 1;
                bool isTerminalFloor = currentFloorNumber <= 1;
                bool usesDirectAuthoredExit = !isTerminalFloor
                    && runController != null
                    && runController.UsesDirectAuthoredExitInteraction;
                int authoredSentryCount = floorAuthoring.GetSentrySpawnPoints().Length;
                int runtimeSentryCount = Object
                    .FindObjectsByType<EnemyStateMachine>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Count(enemy => enemy != null && enemy.name.StartsWith("SentryGuard_"));
                Require(
                    hasSupportItemPlacementMarkers || authoredBattery != null,
                    "No support-item placement markers or scene-managed battery pickup was found for validator coverage.",
                    hasSupportItemPlacementMarkers
                        ? "Support-item placement markers resolved."
                        : "Scene-managed battery pickup coverage resolved.",
                    failures,
                    passes);
                Require(
                    hasKeyPlacementMarkers || authoredKey != null,
                    "No key placement markers or scene-managed key pickup was found for validator coverage.",
                    hasKeyPlacementMarkers
                        ? "Key placement markers resolved."
                        : "Scene-managed key pickup coverage resolved.",
                    failures,
                    passes);
                if (hasSupportItemPlacementMarkers)
                {
                    passes.Add("Support-item placement markers are present; scene battery pickup validation skipped.");
                }
                else if (authoredBattery != null)
                {
                    passes.Add("Authored battery pickup located.");
                }
                else
                {
                    passes.Add("Authored battery pickup is not placed on the current authored floor; battery pickup validation skipped.");
                }

                if (hasKeyPlacementMarkers)
                {
                    passes.Add("Key placement markers are present; scene key pickup validation skipped.");
                }
                else if (authoredKey != null)
                {
                    passes.Add("Authored key pickup located.");
                }
                else
                {
                    passes.Add("Authored key pickup is not placed on the current authored floor; key pickup validation skipped.");
                }

                bool usesElevatorPropDirectExit = usesDirectAuthoredExit
                    && RDirectExitRouteUtility.UsesElevatorPropDirectExit(currentFloorNumber)
                    && MainEscapeSceneIdentityUtility.IsAuthoredSceneName(floorAuthoring.gameObject.scene.name);
                bool hasTileBackedMainGate = floorDirector != null && floorDirector.HasMainGate;
                bool hasLegacyKeyGateAnchor = authoredKeyGate != null || authoredKeyGateVisual != null;

                if (usesElevatorPropDirectExit)
                {
                    passes.Add("Current floor uses direct authored elevator exit interaction; legacy key gate validation skipped.");

                    Require(
                        elevatorExitInteractable != null || authoredElevatorVisual != null,
                        "Direct-exit elevator interaction is missing.",
                        elevatorExitInteractable != null
                            ? "Direct-exit elevator interactable resolved."
                            : "Direct-exit elevator visual resolved.",
                        failures,
                        passes);
                }
                else if (isTerminalFloor)
                {
                    Require(
                        finalExitPoint != null,
                        "1F final exit interaction is missing.",
                        "1F final exit interaction resolved.",
                        failures,
                        passes);
                    passes.Add("1F legacy key gate validation skipped because the final exit owns the route.");
                }
                else
                {
                    if (!usesDirectAuthoredExit)
                    {
                        Require(
                            hasTileBackedMainGate,
                            "Authored main gate routing is missing.",
                            "Tile-backed main gate resolved.",
                            failures,
                            passes);
                    }

                    if (hasLegacyKeyGateAnchor)
                    {
                        passes.Add(
                            authoredKeyGate != null
                                ? "Legacy key gate interactable resolved as optional coverage."
                                : "Legacy key gate visual resolved as optional coverage.");
                    }
                    else
                    {
                        passes.Add("Legacy key gate coverage is optional on the current route.");
                    }
                }

                if (usesElevatorPropDirectExit)
                {
                    passes.Add("Authored stairs validation skipped because this upper floor now exits through the elevator prop.");
                }
                else if (isTerminalFloor)
                {
                    passes.Add("1F authored stairs validation skipped because the final exit owns the route.");
                }
                else
                {
                    Require(
                        authoredStairsPoint != null,
                        "Authored stairs interaction is missing.",
                        "Authored stairs interaction resolved.",
                        failures,
                        passes);
                }
                Require(
                    runtimeSentryCount == authoredSentryCount,
                    $"Expected {authoredSentryCount} authored sentries, but only {runtimeSentryCount} runtime sentries spawned.",
                    $"All {authoredSentryCount} authored sentries spawned.",
                    failures,
                    passes);
                if (!hasSupportItemPlacementMarkers && authoredBattery != null)
                {
                    Require(
                        !authoredPickups.Any(pickup => pickup != null && pickup.name == settings.BatteryRuntimePickupName),
                        "Runtime battery duplicate was created instead of reusing the authored pickup.",
                        "Authored battery pickup reused without duplicate runtime spawn.",
                        failures,
                        passes);
                }
                else if (hasSupportItemPlacementMarkers)
                {
                    passes.Add("Runtime battery duplicate check skipped because support-item placement markers now drive the pickup placement.");
                }
                else
                {
                    passes.Add("Runtime battery duplicate check skipped because the current authored floor does not place a battery pickup.");
                }

                if (!hasSupportItemPlacementMarkers && authoredBattery != null)
                {
                    Require(
                        authoredBattery.GetComponent<PickupFlashlightDiscoveryController>() != null,
                        "Authored battery pickup is missing flashlight discovery visibility.",
                        "Authored battery pickup has flashlight discovery visibility.",
                        failures,
                        passes);
                }

                if (!hasKeyPlacementMarkers && authoredKey != null && inventory != null && player != null)
                {
                    bool hadKeyBefore = inventory.HasItem(PrototypeItemCatalog.IronGateKeyItemId);
                    authoredKey.Interact(player);
                    bool collectedKey = !authoredKey.gameObject.activeSelf && inventory.HasItem(PrototypeItemCatalog.IronGateKeyItemId);
                    Require(
                        collectedKey && !hadKeyBefore,
                        "Authored key pickup did not add the key to inventory.",
                        "Authored key pickup updates inventory.",
                        failures,
                        passes);
                }
                else if (hasKeyPlacementMarkers)
                {
                    passes.Add("Key pickup interaction validation skipped because key placement markers now drive the pickup placement.");
                }
                else if (authoredKey == null)
                {
                    passes.Add("Authored key interaction skipped because the current authored floor does not place the key pickup.");
                }

                if (!usesDirectAuthoredExit && !isTerminalFloor && authoredKeyGate != null && runController != null && floorDirector != null)
                {
                    authoredKeyGate.Interact(player);
                    Require(
                        runController.IsAuthoredGateUnlocked,
                        "Key gate interaction did not unlock the authored gate route.",
                        "Key gate interaction unlocks the authored gate route.",
                        failures,
                        passes);
                }
                else if (isTerminalFloor)
                {
                    passes.Add("1F legacy key gate interaction skipped because the final exit owns the route.");
                }
                else if (usesDirectAuthoredExit)
                {
                    passes.Add("Legacy key gate interaction skipped because the current route does not require MainEscapeKeyGatePoint.");
                }
                else
                {
                    passes.Add("Legacy key gate interaction skipped because MainEscapeKeyGatePoint is optional on the current route.");
                }

                if (isTerminalFloor)
                {
                    passes.Add("1F authored stairs interaction skipped because the final exit owns the route.");
                }
                else if (usesElevatorPropDirectExit)
                {
                    passes.Add("Authored stairs interaction skipped because this upper floor exits through the elevator prop.");
                }
                else if (authoredStairsPoint != null && runController != null)
                {
                    int expectedDestinationFloor = 0;
                    bool hasExpectedDestinationFloor = TryResolveNextRouteFloorNumber(routingSnapshot, currentFloorNumber, out expectedDestinationFloor);

                    authoredStairsPoint.Interact(player);
                    bool advancedToExpectedFloor = hasExpectedDestinationFloor
                        && (runController.CurrentFloorNumber == expectedDestinationFloor
                        || (activeSessionController != null
                            && activeSessionController.Snapshot.CurrentFloorNumber == expectedDestinationFloor));
                    Require(
                        advancedToExpectedFloor,
                        hasExpectedDestinationFloor
                            ? $"Emergency stair transition did not advance to the next authored route floor {expectedDestinationFloor}F."
                            : $"Emergency stair transition could not be validated because no authored route follows {currentFloorNumber}F.",
                        $"Emergency stair transition advanced to the next authored route floor {expectedDestinationFloor}F.",
                        failures,
                        passes);
                }
            }

            if (debugModeController != null && fogOfWarOverlay != null)
            {
                debugModeController.SetDebugModeEnabled(true);
                Require(
                    fogOfWarOverlay.BypassEnabled,
                    "Debug mode did not disable fog of war.",
                    "Debug mode disables fog of war.",
                    failures,
                    passes);

                debugModeController.SetDebugModeEnabled(false);
                Require(
                    !fogOfWarOverlay.BypassEnabled,
                    "Fog of war did not restore after leaving debug mode.",
                    "Fog of war restores after leaving debug mode.",
                    failures,
                    passes);
            }

            Require(
                Object.FindFirstObjectByType<EnemyStateMachine>() != null,
                "Ground enemy did not spawn.",
                "Ground enemy spawned.",
                failures,
                passes);
            Require(
                Object.FindFirstObjectByType<CeilingVentEnemyController>() != null,
                "Vent enemy did not spawn.",
                "Vent enemy spawned.",
                failures,
                passes);

            if (runController != null)
            {
                int expectedStartFloorNumber = routingSnapshot != null ? Mathf.Max(1, routingSnapshot.startingFloorNumber) : 0;
                bool hasExpectedNextFloorNumber = TryResolveNextRouteFloorNumber(routingSnapshot, expectedStartFloorNumber, out int expectedNextFloorNumber);
                bool isOnExpectedRouteFloor = expectedStartFloorNumber > 0
                    && (runController.CurrentFloorNumber == expectedStartFloorNumber
                        || (hasExpectedNextFloorNumber && runController.CurrentFloorNumber == expectedNextFloorNumber));

                Require(
                    isOnExpectedRouteFloor,
                    expectedStartFloorNumber > 0
                        ? $"Run controller is on floor {runController.CurrentFloorNumber}F, outside the authored start-floor route {expectedStartFloorNumber}F."
                        : "Run controller floor could not be validated because the lobby routing snapshot is missing a start floor.",
                    $"Run controller floor is on the authored start-floor route: {runController.CurrentFloorNumber}F.",
                    failures,
                    passes);
            }
        }
        catch (System.Exception exception)
        {
            failures.Add($"Unhandled exception: {exception}");
        }
        finally
        {
            if (failures.Count == 0)
            {
                Debug.Log("[MainEscapeRuntimeValidator] PASS\n- " + string.Join("\n- ", passes));
            }
            else
            {
                Debug.LogError(
                    "[MainEscapeRuntimeValidator] FAIL\n- " + string.Join("\n- ", failures)
                    + (passes.Count > 0 ? "\nPasses\n- " + string.Join("\n- ", passes) : string.Empty));
            }

            ResetValidationState();
            EditorApplication.isPlaying = false;
        }
    }

    private static void ResetValidationState()
    {
        validationRequested = false;
        validationExecuted = false;
        warmupFramesRemaining = 0;
        SessionState.EraseBool(ValidationRequestedKey);
        SessionState.EraseInt(WarmupFramesKey);
        ClearCachedLobbySceneRoutingSnapshot();
    }

    private static bool TryRestorePendingValidationRequest()
    {
        if (!SessionState.GetBool(ValidationRequestedKey, false))
        {
            return false;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            return true;
        }

        ResetValidationState();
        return false;
    }

    private static void Require(bool condition, string failure, string pass, List<string> failures, List<string> passes)
    {
        if (condition)
        {
            passes.Add(pass);
            return;
        }

        failures.Add(failure);
    }

    private static void ValidateLobbyAuthoredBindings(List<string> failures, List<string> passes)
    {
        Scene openedScene = EditorSceneManager.OpenScene(CanonicalLobbyScenePath, OpenSceneMode.Single);
        Require(openedScene.IsValid(), $"Could not open lobby scene '{CanonicalLobbyScenePath}'.", "Lobby scene opened.", failures, passes);

        if (!openedScene.IsValid())
        {
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        IRLobbyController lobbyController = Object.FindFirstObjectByType<IRLobbyController>(FindObjectsInactive.Include);
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

        Require(canvas != null, "Lobby scene is missing a Canvas.", "Lobby scene Canvas resolved.", failures, passes);
        Require(buttons.Length > 0, "Lobby scene does not contain any Button components.", $"Lobby scene has {buttons.Length} button(s).", failures, passes);
        Require(lobbyController != null, "Lobby scene is missing IRLobbyController.", "IRLobbyController resolved in the lobby scene.", failures, passes);

        if (lobbyController == null)
        {
            return;
        }

        SerializedObject serializedLobbyController = new(lobbyController);
        ReadSerializedReference<RRunSessionController>(serializedLobbyController, "runSessionController", failures, passes);
        Button startRunButton = ReadSerializedReference<Button>(serializedLobbyController, "startRunButton", failures, passes);
        Button optionsButton = ReadSerializedReference<Button>(serializedLobbyController, "optionsButton", failures, passes);
        Button creditsButton = ReadSerializedReference<Button>(serializedLobbyController, "creditsButton", failures, passes);
        Button quitButton = ReadSerializedReference<Button>(serializedLobbyController, "quitButton", failures, passes);
        TextMeshProUGUI summaryTitleText = ReadSerializedReference<TextMeshProUGUI>(serializedLobbyController, "summaryTitleText", failures, passes);
        TextMeshProUGUI summaryBodyText = ReadSerializedReference<TextMeshProUGUI>(serializedLobbyController, "summaryBodyText", failures, passes);
        TextMeshProUGUI footerHintText = ReadSerializedReference<TextMeshProUGUI>(serializedLobbyController, "footerHintText", failures, passes);
        RectTransform modalBackdrop = ReadSerializedReference<RectTransform>(serializedLobbyController, "modalBackdrop", failures, passes);
        RectTransform optionsPanel = ReadSerializedReference<RectTransform>(serializedLobbyController, "optionsPanel", failures, passes);
        RectTransform creditsPanel = ReadSerializedReference<RectTransform>(serializedLobbyController, "creditsPanel", failures, passes);
        Button optionsCloseButton = ReadSerializedReference<Button>(serializedLobbyController, "optionsCloseButton", failures, passes);
        Button creditsCloseButton = ReadSerializedReference<Button>(serializedLobbyController, "creditsCloseButton", failures, passes);

        RequirePersistentButtonBinding(startRunButton, lobbyController, nameof(IRLobbyController.StartRun), failures, passes);
        RequirePersistentButtonBinding(optionsButton, lobbyController, nameof(IRLobbyController.OpenOptionsModal), failures, passes);
        RequirePersistentButtonBinding(creditsButton, lobbyController, nameof(IRLobbyController.OpenCreditsModal), failures, passes);
        RequirePersistentButtonBinding(quitButton, lobbyController, nameof(IRLobbyController.QuitGame), failures, passes);
        RequirePersistentButtonBinding(optionsCloseButton, lobbyController, nameof(IRLobbyController.CloseActiveModal), failures, passes);
        RequirePersistentButtonBinding(creditsCloseButton, lobbyController, nameof(IRLobbyController.CloseActiveModal), failures, passes);

        Require(
            eventSystem != null,
            "Lobby scene is missing an EventSystem.",
            "Lobby scene EventSystem resolved.",
            failures,
            passes);

        if (eventSystem != null && startRunButton != null)
        {
            Require(
                eventSystem.firstSelectedGameObject == startRunButton.gameObject,
                $"Lobby EventSystem first selected object should be '{startRunButton.gameObject.name}', but is '{eventSystem.firstSelectedGameObject?.name ?? "<null>"}'.",
                $"Lobby EventSystem first selected object is '{startRunButton.gameObject.name}'.",
                failures,
                passes);
        }
    }

    private static T ReadSerializedReference<T>(SerializedObject serializedObject, string propertyName, List<string> failures, List<string> passes)
        where T : Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            failures.Add($"IRLobbyController is missing the serialized '{propertyName}' field.");
            return null;
        }

        T value = property.objectReferenceValue as T;
        Require(
            value != null,
            $"IRLobbyController.{propertyName} is not assigned.",
            $"IRLobbyController.{propertyName} -> {value.name}.",
            failures,
            passes);
        return value;
    }

    private static void RequirePersistentButtonBinding(Button button, Object target, string methodName, List<string> failures, List<string> passes)
    {
        if (button == null)
        {
            failures.Add($"Expected a button binding for '{methodName}', but the button reference is missing.");
            return;
        }

        int persistentCount = button.onClick.GetPersistentEventCount();
        bool matched = false;

        for (int index = 0; index < persistentCount; index++)
        {
            if (button.onClick.GetPersistentTarget(index) != target)
            {
                continue;
            }

            if (!string.Equals(button.onClick.GetPersistentMethodName(index), methodName, StringComparison.Ordinal))
            {
                continue;
            }

            matched = true;
            break;
        }

        Require(
            matched,
            $"Button '{button.name}' is missing a persistent onClick binding to {target.GetType().Name}.{methodName}.",
            $"Button '{button.name}' persistently calls {target.GetType().Name}.{methodName}.",
            failures,
            passes);
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < descendants.Length; index++)
        {
            Transform descendant = descendants[index];

            if (descendant != null && string.Equals(descendant.name, targetName, StringComparison.Ordinal))
            {
                return descendant;
            }
        }

        Transform[] sceneTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int index = 0; index < sceneTransforms.Length; index++)
        {
            Transform transform = sceneTransforms[index];

            if (transform != null && string.Equals(transform.name, targetName, StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }

    private static Transform FindFirstSceneTransform(params string[] candidateNames)
    {
        Transform[] sceneTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int nameIndex = 0; nameIndex < candidateNames.Length; nameIndex++)
        {
            string candidateName = candidateNames[nameIndex];

            if (string.IsNullOrWhiteSpace(candidateName))
            {
                continue;
            }

            for (int transformIndex = 0; transformIndex < sceneTransforms.Length; transformIndex++)
            {
                Transform transform = sceneTransforms[transformIndex];

                if (transform != null && string.Equals(transform.name, candidateName, StringComparison.Ordinal))
                {
                    return transform;
                }
            }
        }

        return null;
    }

    private static void ValidateLobbySceneRoutingSurface(LobbySceneRoutingSnapshot routingSnapshot, List<string> failures, List<string> passes)
    {
        Require(routingSnapshot != null, "Lobby scene routing snapshot was not captured before runtime validation.", "Lobby scene routing snapshot restored.", failures, passes);

        if (routingSnapshot == null)
        {
            return;
        }

        string lobbyScenePath = routingSnapshot.lobbyScenePath?.Trim() ?? string.Empty;
        string startFloorScenePath = routingSnapshot.GetStartFloorScenePath();
        RFloorSceneEntry[] routes = routingSnapshot.floorScenes ?? Array.Empty<RFloorSceneEntry>();

        Require(
            !string.IsNullOrWhiteSpace(lobbyScenePath),
            "Lobby scene routing snapshot is missing LobbyScenePath.",
            $"LobbyScenePath = {lobbyScenePath}",
            failures,
            passes);
        passes.Add(
            routingSnapshot.useSceneLocalRoutingOverrides
                ? "Legacy scene-local routing flag is enabled; serialized scene routes remain authoritative."
                : "Legacy scene-local routing flag is disabled; serialized scene routes remain authoritative.");
        Require(
            routingSnapshot.startingFloorNumber > 0,
            "Lobby scene routing snapshot is missing a valid starting floor number.",
            $"StartingFloorNumber = {routingSnapshot.startingFloorNumber}",
            failures,
            passes);
        Require(
            !string.IsNullOrWhiteSpace(startFloorScenePath),
            "Lobby scene routing snapshot is missing a start floor scene path.",
            $"StartFloorScenePath = {startFloorScenePath}",
            failures,
            passes);
        Require(
            routes.Length > 0,
            "Lobby scene routing snapshot has no floor scene routes configured.",
            $"Floor route count = {routes.Length}",
            failures,
            passes);

        int previousFloorNumber = int.MaxValue;
        var seenFloors = new HashSet<int>();

        for (int index = 0; index < routes.Length; index++)
        {
            RFloorSceneEntry route = routes[index];
            int resolvedFloor = Mathf.Max(1, route.floorNumber);

            Require(
                route.floorNumber > 0,
                $"Floor route at index {index} has an invalid floor number '{route.floorNumber}'.",
                $"Route index {index} uses floor {route.floorNumber}F.",
                failures,
                passes);
            Require(
                !string.IsNullOrWhiteSpace(route.scenePath),
                $"Floor route for {resolvedFloor}F is missing a scene path.",
                $"{resolvedFloor}F scene path = {route.scenePath}",
                failures,
                passes);
            Require(
                resolvedFloor < previousFloorNumber,
                $"Floor route order must descend strictly, but index {index} is {resolvedFloor}F after {previousFloorNumber}F.",
                $"Route order keeps descending through {resolvedFloor}F.",
                failures,
                passes);
            Require(
                seenFloors.Add(resolvedFloor),
                $"Floor route for {resolvedFloor}F is duplicated.",
                $"{resolvedFloor}F route is unique.",
                failures,
                passes);

            if (index == 0)
            {
                Require(
                    resolvedFloor == routingSnapshot.startingFloorNumber,
                    $"First floor route is {resolvedFloor}F but the lobby scene starts on {routingSnapshot.startingFloorNumber}F.",
                    $"Start floor route begins on {resolvedFloor}F.",
                    failures,
                    passes);
            }

            previousFloorNumber = resolvedFloor;
        }
    }

    private static bool TryResolveNextRouteFloorNumber(
        LobbySceneRoutingSnapshot routingSnapshot,
        int currentFloorNumber,
        out int nextFloorNumber)
    {
        nextFloorNumber = 0;

        if (routingSnapshot == null || currentFloorNumber <= 0)
        {
            return false;
        }

        RFloorSceneEntry[] routes = routingSnapshot.floorScenes ?? Array.Empty<RFloorSceneEntry>();

        for (int index = 0; index < routes.Length - 1; index++)
        {
            if (Mathf.Max(1, routes[index].floorNumber) != currentFloorNumber)
            {
                continue;
            }

            nextFloorNumber = Mathf.Max(1, routes[index + 1].floorNumber);
            return true;
        }

        return false;
    }

    private static bool TryReadLobbySceneRoutingSnapshot(out LobbySceneRoutingSnapshot routingSnapshot, out string error)
    {
        routingSnapshot = null;
        error = string.Empty;

        if (!System.IO.File.Exists(CanonicalLobbyScenePath))
        {
            error = $"Lobby scene asset does not exist at '{CanonicalLobbyScenePath}'.";
            return false;
        }

        Scene openedScene = EditorSceneManager.OpenScene(CanonicalLobbyScenePath, OpenSceneMode.Single);

        if (!openedScene.IsValid())
        {
            error = "Could not open the canonical lobby scene.";
            return false;
        }

        RRunSessionController controller = Object.FindFirstObjectByType<RRunSessionController>(FindObjectsInactive.Include);

        if (controller == null || controller.gameObject.scene != openedScene)
        {
            error = "Canonical lobby scene is missing RRunSessionController.";
            return false;
        }

        SerializedObject serializedController = new(controller);
        SerializedProperty lobbyScenePathProperty = serializedController.FindProperty("lobbyScenePath");
        SerializedProperty startingFloorNumberProperty = serializedController.FindProperty("startingFloorNumber");
        SerializedProperty useSceneLocalRoutingOverridesProperty = serializedController.FindProperty("useSceneLocalRoutingOverrides");
        SerializedProperty floorScenesProperty = serializedController.FindProperty("floorScenes");

        if (lobbyScenePathProperty == null || startingFloorNumberProperty == null || useSceneLocalRoutingOverridesProperty == null || floorScenesProperty == null)
        {
            error = "Canonical lobby scene RRunSessionController is missing serialized routing fields.";
            return false;
        }

        routingSnapshot = new LobbySceneRoutingSnapshot
        {
            lobbyScenePath = lobbyScenePathProperty.stringValue?.Trim() ?? string.Empty,
            startingFloorNumber = Mathf.Max(1, startingFloorNumberProperty.intValue),
            useSceneLocalRoutingOverrides = useSceneLocalRoutingOverridesProperty.boolValue,
            floorScenes = ReadSerializedFloorSceneEntries(floorScenesProperty)
        };

        if (routingSnapshot.floorScenes == null || routingSnapshot.floorScenes.Length == 0)
        {
            error = "Canonical lobby scene RRunSessionController has no serialized floor routes.";
            routingSnapshot = null;
            return false;
        }

        return true;
    }

    private static RFloorSceneEntry[] ReadSerializedFloorSceneEntries(SerializedProperty floorScenesProperty)
    {
        if (floorScenesProperty == null || !floorScenesProperty.isArray || floorScenesProperty.arraySize <= 0)
        {
            return Array.Empty<RFloorSceneEntry>();
        }

        var routes = new List<RFloorSceneEntry>(floorScenesProperty.arraySize);

        for (int index = 0; index < floorScenesProperty.arraySize; index++)
        {
            SerializedProperty routeProperty = floorScenesProperty.GetArrayElementAtIndex(index);

            if (routeProperty == null)
            {
                continue;
            }

            SerializedProperty floorNumberProperty = routeProperty.FindPropertyRelative("floorNumber");
            SerializedProperty scenePathProperty = routeProperty.FindPropertyRelative("scenePath");

            if (floorNumberProperty == null || scenePathProperty == null)
            {
                continue;
            }

            routes.Add(new RFloorSceneEntry(floorNumberProperty.intValue, scenePathProperty.stringValue?.Trim() ?? string.Empty));
        }

        return routes.ToArray();
    }

    private static void CacheLobbySceneRoutingSnapshot(LobbySceneRoutingSnapshot routingSnapshot)
    {
        if (routingSnapshot == null)
        {
            ClearCachedLobbySceneRoutingSnapshot();
            return;
        }

        SessionState.SetString(LobbyRoutingSnapshotKey, "cached");
        SessionState.SetString(LobbyRoutingSnapshotLobbyScenePathKey, routingSnapshot.lobbyScenePath ?? string.Empty);
        SessionState.SetInt(LobbyRoutingSnapshotStartingFloorNumberKey, routingSnapshot.startingFloorNumber);
        SessionState.SetBool(LobbyRoutingSnapshotUseSceneLocalRoutingOverridesKey, routingSnapshot.useSceneLocalRoutingOverrides);

        RFloorSceneEntry[] floorScenes = routingSnapshot.floorScenes ?? Array.Empty<RFloorSceneEntry>();
        SessionState.SetInt(LobbyRoutingSnapshotFloorScenesCountKey, floorScenes.Length);

        for (int index = 0; index < floorScenes.Length; index++)
        {
            RFloorSceneEntry route = floorScenes[index];
            SessionState.SetInt($"{LobbyRoutingSnapshotFloorSceneFloorNumberKeyPrefix}{index}", route.floorNumber);
            SessionState.SetString($"{LobbyRoutingSnapshotFloorScenePathKeyPrefix}{index}", route.scenePath ?? string.Empty);
        }
    }

    private static LobbySceneRoutingSnapshot LoadCachedLobbySceneRoutingSnapshot()
    {
        if (string.IsNullOrWhiteSpace(SessionState.GetString(LobbyRoutingSnapshotKey, string.Empty)))
        {
            return null;
        }

        int floorSceneCount = Mathf.Max(0, SessionState.GetInt(LobbyRoutingSnapshotFloorScenesCountKey, 0));
        var floorScenes = new RFloorSceneEntry[floorSceneCount];

        for (int index = 0; index < floorSceneCount; index++)
        {
            int floorNumber = SessionState.GetInt($"{LobbyRoutingSnapshotFloorSceneFloorNumberKeyPrefix}{index}", 0);
            string scenePath = SessionState.GetString($"{LobbyRoutingSnapshotFloorScenePathKeyPrefix}{index}", string.Empty);
            floorScenes[index] = new RFloorSceneEntry(floorNumber, scenePath);
        }

        return new LobbySceneRoutingSnapshot
        {
            lobbyScenePath = SessionState.GetString(LobbyRoutingSnapshotLobbyScenePathKey, string.Empty),
            startingFloorNumber = SessionState.GetInt(LobbyRoutingSnapshotStartingFloorNumberKey, 0),
            useSceneLocalRoutingOverrides = SessionState.GetBool(LobbyRoutingSnapshotUseSceneLocalRoutingOverridesKey, false),
            floorScenes = floorScenes
        };
    }

    private static void ClearCachedLobbySceneRoutingSnapshot()
    {
        int floorSceneCount = Mathf.Max(0, SessionState.GetInt(LobbyRoutingSnapshotFloorScenesCountKey, 0));

        for (int index = 0; index < floorSceneCount; index++)
        {
            SessionState.EraseInt($"{LobbyRoutingSnapshotFloorSceneFloorNumberKeyPrefix}{index}");
            SessionState.EraseString($"{LobbyRoutingSnapshotFloorScenePathKeyPrefix}{index}");
        }

        SessionState.EraseString(LobbyRoutingSnapshotKey);
        SessionState.EraseString(LobbyRoutingSnapshotLobbyScenePathKey);
        SessionState.EraseInt(LobbyRoutingSnapshotStartingFloorNumberKey);
        SessionState.EraseBool(LobbyRoutingSnapshotUseSceneLocalRoutingOverridesKey);
        SessionState.EraseInt(LobbyRoutingSnapshotFloorScenesCountKey);
    }

    private static void ValidateSessionContracts(List<string> failures, List<string> passes)
    {
        Type controllerType = FindTypeByName("RRunSessionController");
        Type snapshotType = FindTypeByName("RRunSnapshot");
        Type outcomeType = FindTypeByName("RRunOutcome");

        Require(
            controllerType != null,
            "RRunSessionController type is missing.",
            "RRunSessionController type resolved.",
            failures,
            passes);
        Require(
            snapshotType != null,
            "RRunSnapshot type is missing.",
            "RRunSnapshot type resolved.",
            failures,
            passes);
        Require(
            outcomeType != null,
            "RRunOutcome type is missing.",
            "RRunOutcome type resolved.",
            failures,
            passes);
    }

    private static void ValidateCompositionRootAuthoredReferences(
        RSceneCompositionRoot compositionRoot,
        bool hasSupportItemPlacementMarkers,
        bool hasKeyPlacementMarkers,
        List<string> failures,
        List<string> passes)
    {
        if (compositionRoot == null)
        {
            return;
        }

        List<string> requiredFieldNames = BuildRequiredCompositionRootFieldNames(
            compositionRoot,
            hasSupportItemPlacementMarkers,
            hasKeyPlacementMarkers);

        int assignedCount = 0;

        for (int index = 0; index < requiredFieldNames.Count; index++)
        {
            string fieldName = requiredFieldNames[index];
            FieldInfo field = compositionRoot.GetType().GetField(fieldName, MemberFlags);

            if (field == null)
            {
                failures.Add($"{nameof(RSceneCompositionRoot)} is missing serialized field contract '{fieldName}'.");
                continue;
            }

            if (field.GetValue(compositionRoot) is UnityEngine.Object reference && reference != null)
            {
                assignedCount++;
                continue;
            }

            failures.Add($"{nameof(RSceneCompositionRoot)} is missing authored reference '{fieldName}'.");
        }

        passes.Add($"{nameof(RSceneCompositionRoot)} authored reference coverage: {assignedCount}/{requiredFieldNames.Count} required references assigned.");

        RRunController runController = GetSerializedFieldValue<RRunController>(compositionRoot, "runController");
        int currentFloorNumber = runController != null ? runController.CurrentFloorNumber : 1;

        if (currentFloorNumber <= 1)
        {
            FieldInfo finalExitField = compositionRoot.GetType().GetField("finalExitPoint", MemberFlags);

            if (finalExitField == null)
            {
                failures.Add($"{nameof(RSceneCompositionRoot)} is missing serialized field contract 'finalExitPoint'.");
            }
            else if (finalExitField.GetValue(compositionRoot) is not UnityEngine.Object reference || reference == null)
            {
                failures.Add($"{nameof(RSceneCompositionRoot)} should assign 'finalExitPoint' on 1F so the final exit door can drive the clear flow.");
            }
            else
            {
                passes.Add($"{nameof(RSceneCompositionRoot)} assigns 'finalExitPoint' for the 1F final exit door.");
            }
        }
    }

    private static List<string> BuildRequiredCompositionRootFieldNames(
        RSceneCompositionRoot compositionRoot,
        bool hasSupportItemPlacementMarkers,
        bool hasKeyPlacementMarkers)
    {
        List<string> requiredFieldNames = new()
        {
            "systemsRoot",
            "gameplayRoot",
            "runtimeRoot",
            "authoringWorkspaceRoot",
            "floorAuthoring",
            "playerController",
            "playerRuntime",
            "floorDirector",
            "runController",
            "playerStateStoreSource",
            "noiseSystem",
            "audioManager",
            "fogOfWarOverlay",
            "hudCanvas",
            "inventoryHudBinder",
            "healthHudBinder",
            "threatHudBinder",
            "quickSlotsHudBinder",
            "bottlePickup",
            "keyPickup"
        };

        if (hasSupportItemPlacementMarkers)
        {
            requiredFieldNames.Remove("bottlePickup");
        }

        if (hasKeyPlacementMarkers)
        {
            requiredFieldNames.Remove("keyPickup");
        }

        RRunController runController = GetSerializedFieldValue<RRunController>(compositionRoot, "runController");
        int currentFloorNumber = runController != null ? runController.CurrentFloorNumber : 1;
        bool usesDirectAuthoredExit = currentFloorNumber > 1
            && runController != null
            && runController.UsesDirectAuthoredExitInteraction;
        bool usesElevatorPropDirectExit = usesDirectAuthoredExit
            && RDirectExitRouteUtility.UsesElevatorPropDirectExit(currentFloorNumber)
            && MainEscapeSceneIdentityUtility.IsAuthoredSceneName(compositionRoot.gameObject.scene.name);

        if (currentFloorNumber <= 1)
        {
            requiredFieldNames.Add("finalExitPoint");
        }
        else if (usesElevatorPropDirectExit)
        {
            requiredFieldNames.Add("elevatorExitPoint");
        }
        else
        {
            requiredFieldNames.Add("authoredStairsPoint");
        }

        return requiredFieldNames;
    }

    private static T GetSerializedFieldValue<T>(UnityEngine.Object owner, string fieldName) where T : class
    {
        if (owner == null)
        {
            return null;
        }

        FieldInfo field = owner.GetType().GetField(fieldName, MemberFlags);
        return field != null ? field.GetValue(owner) as T : null;
    }

    private static void ValidateRunModalSurface(IRGameClearPanelView panelView, List<string> failures, List<string> passes)
    {
        if (panelView == null)
        {
            return;
        }

        Type panelType = panelView.GetType();
        Require(HasMethod(panelType, "ShowFloorClear"), "Run modal API missing: ShowFloorClear", "Run modal API resolved: ShowFloorClear", failures, passes);
        Require(HasMethod(panelType, "ShowFinalClear"), "Run modal API missing: ShowFinalClear", "Run modal API resolved: ShowFinalClear", failures, passes);
        Require(HasMethod(panelType, "ShowFailure"), "Run modal API missing: ShowFailure", "Run modal API resolved: ShowFailure", failures, passes);
        Require(HasMethod(panelType, "HideAndResume"), "Run modal API missing: HideAndResume", "Run modal API resolved: HideAndResume", failures, passes);
    }

    private static void ValidateEnemyChasePathCoverage(
        GridMapService mapService,
        MainEscapeFloorAuthoring floorAuthoring,
        WasdPlayerController player,
        List<string> failures,
        List<string> passes)
    {
        if (mapService == null || player == null)
        {
            return;
        }

        EnemyStateMachine[] enemies = Object.FindObjectsByType<EnemyStateMachine>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Require(enemies.Length > 0, "No EnemyStateMachine instances found for chase path validation.", "EnemyStateMachine instances found for chase path validation.", failures, passes);

        if (enemies.Length == 0)
        {
            return;
        }

        List<Vector3Int> runtimePath = new();
        List<Vector3Int> baselinePath = new();
        int reachableEnemies = 0;

        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyStateMachine enemy = enemies[index];

            if (enemy == null)
            {
                continue;
            }

            Vector3Int enemyCell = mapService.ResolveNearestWalkableCell(enemy.transform.position, 1, true);
            bool hasRuntimePath = TryBuildPathToPlayerNeighborhood(
                mapService,
                enemyCell,
                player.transform.position,
                runtimePath,
                allowClosedDoors: true,
                TryBuildRuntimePathToCell,
                out Vector3Int runtimeTargetCell);
            bool hasBaselinePath = TryBuildPathToPlayerNeighborhood(
                mapService,
                enemyCell,
                player.transform.position,
                baselinePath,
                allowClosedDoors: true,
                TryBuildBaselinePathToCell,
                out Vector3Int baselineTargetCell);

            passes.Add(
                $"Enemy path probe '{enemy.name}': enemyCell={enemyCell}, runtimeTarget={runtimeTargetCell}, baselineTarget={baselineTargetCell}, runtimePath={hasRuntimePath}, baselinePath={hasBaselinePath}.");

            if (hasRuntimePath)
            {
                reachableEnemies++;
            }

            if (!hasRuntimePath && hasBaselinePath)
            {
                string suspectSummary = DescribeAuthoringBlockersOnPath(floorAuthoring, mapService, baselinePath);
                failures.Add(
                    $"Enemy '{enemy.name}' cannot chase-path to player while base tiles are connected. " +
                    $"A prop/movement blocker is likely sealing the route. {suspectSummary}");
            }

            if (enemy.CurrentState == EnemyState.Chase && !hasRuntimePath)
            {
                failures.Add($"Enemy '{enemy.name}' is in Chase state but no reachable path to the player was found.");
            }
        }

        Require(
            reachableEnemies > 0,
            "No enemy has a reachable runtime path to the player (allowClosedDoors=true).",
            $"Runtime chase path exists for {reachableEnemies}/{enemies.Length} enemy instance(s).",
            failures,
            passes);
    }

    private static void ValidateThreatReadabilityCoverage(
        IRPlayerThreatHudBinder threatFeedbackHud,
        IRThreatPanelView threatPanel,
        List<string> failures,
        List<string> passes)
    {
        Require(
            threatFeedbackHud != null,
            "IRPlayerThreatHudBinder is missing, so chase threat readability cannot be presented.",
            "IRPlayerThreatHudBinder resolved.",
            failures,
            passes);
        Require(
            threatPanel != null && threatPanel.HasRenderableEdges,
            "Threat panel is missing or lacks renderable edges.",
            "Threat panel render surface resolved.",
            failures,
            passes);
        Require(
            PlayerThreatFeedbackRegistry.Sources.Count > 0,
            "No threat feedback sources are registered.",
            $"Threat feedback sources registered: {PlayerThreatFeedbackRegistry.Sources.Count}.",
            failures,
            passes);

        EnemyStateMachine[] chasingEnemies = Object
            .FindObjectsByType<EnemyStateMachine>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(enemy => enemy != null && enemy.CurrentState == EnemyState.Chase)
            .ToArray();

        if (chasingEnemies.Length == 0)
        {
            passes.Add("No enemy currently in Chase during validator frame; threat readability covered as smoke-only.");
            return;
        }

        int readableCueCount = 0;

        for (int index = 0; index < chasingEnemies.Length; index++)
        {
            EnemyStateMachine enemy = chasingEnemies[index];
            bool hasIntensityCue = enemy.IsConfirmedThreat && enemy.ThreatIntensityNormalized >= ThreatCueIntensityFloor;
            bool hasForcedMarkerCue = enemy.ShouldForceThreatFeedbackVisible && IsMarkerVisible(enemy.ThreatMarkerRenderer);

            if (hasIntensityCue || hasForcedMarkerCue)
            {
                readableCueCount++;
                continue;
            }

            failures.Add(
                $"Enemy '{enemy.name}' is chasing, but no strong threat cue was detected " +
                $"(intensity={enemy.ThreatIntensityNormalized:0.00}, forcedMarker={enemy.ShouldForceThreatFeedbackVisible}).");
        }

        passes.Add($"Chase readability cues detected for {readableCueCount}/{chasingEnemies.Length} chasing enemy instance(s).");
    }

    private static bool TryBuildPathIgnoringDynamicBlockers(
        GridMapService mapService,
        Vector3Int startCell,
        Vector3Int goalCell,
        List<Vector3Int> result,
        bool allowClosedDoors)
    {
        result?.Clear();

        if (mapService == null || result == null)
        {
            return false;
        }

        Tilemap groundTilemap = mapService.GroundTilemap;
        Tilemap wallTilemap = mapService.WallTilemap;
        Tilemap doorTilemap = mapService.DoorTilemap;

        if (groundTilemap == null)
        {
            return false;
        }

        if (startCell == goalCell)
        {
            return true;
        }

        if (!IsBaselineWalkable(groundTilemap, wallTilemap, doorTilemap, goalCell, allowClosedDoors))
        {
            return false;
        }

        Queue<Vector3Int> frontier = new();
        HashSet<Vector3Int> visited = new();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new();

        frontier.Enqueue(startCell);
        visited.Add(startCell);

        int safetyCounter = 0;

        while (frontier.Count > 0 && safetyCounter < 4096)
        {
            safetyCounter++;
            Vector3Int current = frontier.Dequeue();

            if (current == goalCell)
            {
                ReconstructPath(cameFrom, current, result);
                return true;
            }

            for (int index = 0; index < CardinalNeighborOffsets.Length; index++)
            {
                Vector3Int neighbor = current + CardinalNeighborOffsets[index];

                if (visited.Contains(neighbor) || !IsBaselineWalkable(groundTilemap, wallTilemap, doorTilemap, neighbor, allowClosedDoors))
                {
                    continue;
                }

                visited.Add(neighbor);
                cameFrom[neighbor] = current;
                frontier.Enqueue(neighbor);
            }
        }

        result.Clear();
        return false;
    }

    private delegate bool TryBuildPathToCellDelegate(
        GridMapService mapService,
        Vector3Int startCell,
        Vector3Int goalCell,
        List<Vector3Int> result,
        bool allowClosedDoors);

    private static bool TryBuildPathToPlayerNeighborhood(
        GridMapService mapService,
        Vector3Int startCell,
        Vector3 playerWorldPosition,
        List<Vector3Int> result,
        bool allowClosedDoors,
        TryBuildPathToCellDelegate tryBuildPath,
        out Vector3Int resolvedTargetCell)
    {
        resolvedTargetCell = mapService != null ? mapService.WorldToCell(playerWorldPosition) : Vector3Int.zero;
        result?.Clear();

        if (mapService == null || result == null || tryBuildPath == null)
        {
            return false;
        }

        Vector3Int playerOriginCell = mapService.WorldToCell(playerWorldPosition);

        if (TryBuildPathToPlayerCandidate(mapService, startCell, playerOriginCell, result, allowClosedDoors, tryBuildPath))
        {
            resolvedTargetCell = playerOriginCell;
            return true;
        }

        for (int radius = 1; radius <= 4; radius++)
        {
            foreach (Vector3Int candidate in EnumeratePlayerNeighborhood(playerOriginCell, radius))
            {
                if (!TryBuildPathToPlayerCandidate(mapService, startCell, candidate, result, allowClosedDoors, tryBuildPath))
                {
                    continue;
                }

                resolvedTargetCell = candidate;
                return true;
            }
        }

        result.Clear();
        return false;
    }

    private static bool TryBuildPathToPlayerCandidate(
        GridMapService mapService,
        Vector3Int startCell,
        Vector3Int goalCell,
        List<Vector3Int> result,
        bool allowClosedDoors,
        TryBuildPathToCellDelegate tryBuildPath)
    {
        return mapService != null
            && mapService.IsWalkable(goalCell, allowClosedDoors)
            && tryBuildPath(mapService, startCell, goalCell, result, allowClosedDoors);
    }

    private static bool TryBuildRuntimePathToCell(
        GridMapService mapService,
        Vector3Int startCell,
        Vector3Int goalCell,
        List<Vector3Int> result,
        bool allowClosedDoors)
    {
        return GridPathfinder.TryBuildPath(mapService, startCell, goalCell, result, allowClosedDoors);
    }

    private static bool TryBuildBaselinePathToCell(
        GridMapService mapService,
        Vector3Int startCell,
        Vector3Int goalCell,
        List<Vector3Int> result,
        bool allowClosedDoors)
    {
        return TryBuildPathIgnoringDynamicBlockers(mapService, startCell, goalCell, result, allowClosedDoors);
    }

    private static IEnumerable<Vector3Int> EnumeratePlayerNeighborhood(Vector3Int originCell, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                {
                    continue;
                }

                yield return originCell + new Vector3Int(x, y, 0);
            }
        }
    }

    private static bool IsBaselineWalkable(
        Tilemap groundTilemap,
        Tilemap wallTilemap,
        Tilemap doorTilemap,
        Vector3Int cell,
        bool allowClosedDoors)
    {
        bool hasGround = groundTilemap != null && groundTilemap.HasTile(cell);
        bool blockedByWall = wallTilemap != null && wallTilemap.HasTile(cell);
        bool blockedByDoor = !allowClosedDoors && doorTilemap != null && doorTilemap.HasTile(cell);
        return hasGround && !blockedByWall && !blockedByDoor;
    }

    private static void ReconstructPath(
        IReadOnlyDictionary<Vector3Int, Vector3Int> cameFrom,
        Vector3Int current,
        List<Vector3Int> result)
    {
        result.Clear();
        result.Add(current);

        while (cameFrom.TryGetValue(current, out Vector3Int previous))
        {
            current = previous;
            result.Add(current);
        }

        result.Reverse();

        if (result.Count > 0)
        {
            result.RemoveAt(0);
        }
    }

    private static string DescribeAuthoringBlockersOnPath(
        MainEscapeFloorAuthoring floorAuthoring,
        GridMapService mapService,
        IReadOnlyCollection<Vector3Int> baselinePath)
    {
        if (floorAuthoring == null || mapService == null || baselinePath == null || baselinePath.Count == 0)
        {
            return "No authored blocker context available.";
        }

        Tilemap groundTilemap = mapService.GroundTilemap ?? floorAuthoring.GroundTilemap;

        if (groundTilemap == null)
        {
            return "Ground tilemap context is unavailable.";
        }

        HashSet<Vector3Int> pathCells = baselinePath as HashSet<Vector3Int> ?? new HashSet<Vector3Int>(baselinePath);
        List<string> suspects = new();

        MainEscapeMovementBlockerAuthoring[] movementBlockers = floorAuthoring.GetComponentsInChildren<MainEscapeMovementBlockerAuthoring>(true);

        for (int index = 0; index < movementBlockers.Length; index++)
        {
            MainEscapeMovementBlockerAuthoring blocker = movementBlockers[index];

            if (blocker == null)
            {
                continue;
            }

            Collider2D collider = blocker.GetComponent<Collider2D>();

            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            Vector3Int[] occupiedCells = blocker.GetOccupiedCells(groundTilemap);
            bool intersectsPath = occupiedCells.Any(pathCells.Contains);

            if (!intersectsPath)
            {
                continue;
            }

            bool hidden = IsGameObjectVisuallyHidden(blocker.gameObject);
            suspects.Add(hidden ? $"{blocker.name} (movement, hidden)" : $"{blocker.name} (movement)");
        }

        MainEscapePropBlockerAuthoring[] propBlockers = floorAuthoring.GetComponentsInChildren<MainEscapePropBlockerAuthoring>(true);

        for (int index = 0; index < propBlockers.Length; index++)
        {
            MainEscapePropBlockerAuthoring blocker = propBlockers[index];

            if (blocker == null)
            {
                continue;
            }

            Vector3Int[] occupiedCells = blocker.GetOccupiedCells(groundTilemap);
            bool intersectsPath = occupiedCells.Any(pathCells.Contains);

            if (!intersectsPath)
            {
                continue;
            }

            bool hidden = IsGameObjectVisuallyHidden(blocker.gameObject);
            suspects.Add(hidden ? $"{blocker.name} (prop, hidden)" : $"{blocker.name} (prop)");
        }

        if (suspects.Count == 0)
        {
            return "No authored blocker component directly overlapped the baseline route.";
        }

        const int maxShown = 6;
        string suffix = suspects.Count > maxShown ? $" (+{suspects.Count - maxShown} more)" : string.Empty;
        return "Suspects: " + string.Join(", ", suspects.Take(maxShown)) + suffix;
    }

    private static bool IsGameObjectVisuallyHidden(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
        {
            return true;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (renderer is SpriteRenderer spriteRenderer && spriteRenderer.color.a <= VisibleAlphaFloor)
            {
                continue;
            }

            if (renderer is TilemapRenderer tilemapRenderer)
            {
                Tilemap tilemap = tilemapRenderer.GetComponent<Tilemap>();

                if (tilemap != null && tilemap.color.a <= VisibleAlphaFloor)
                {
                    continue;
                }
            }

            return false;
        }

        return true;
    }

    private static bool IsMarkerVisible(SpriteRenderer markerRenderer)
    {
        return markerRenderer != null
            && markerRenderer.enabled
            && markerRenderer.gameObject.activeInHierarchy
            && markerRenderer.color.a > VisibleAlphaFloor;
    }

    private static bool HasMethod(Type type, string methodName)
    {
        return type != null
            && !string.IsNullOrWhiteSpace(methodName)
            && type.GetMethod(methodName, MemberFlags) != null;
    }

    private static Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int index = 0; index < assemblies.Length; index++)
        {
            Type found = assemblies[index].GetType(typeName, false);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

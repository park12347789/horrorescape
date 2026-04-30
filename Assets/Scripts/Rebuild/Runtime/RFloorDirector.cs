using System.Collections.Generic;
using UnityEngine;

public interface IFloorEncounterRandomizationPlanner
{
    RFloorEncounterRandomizationPlan BuildPlan(
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        int runSeed,
        Vector3Int[] defaultPatrolSpawnCells,
        MainEscapeSentrySpawnPoint[] defaultSentrySpawns,
        Vector3Int defaultChaserSpawnCell);
}

public readonly struct RFloorEncounterRandomizationPlan
{
    public RFloorEncounterRandomizationPlan(
        Vector3Int[] patrolSpawnCells,
        MainEscapeSentrySpawnPoint[] sentrySpawns,
        Vector3Int chaserSpawnCell)
    {
        PatrolSpawnCells = patrolSpawnCells ?? System.Array.Empty<Vector3Int>();
        SentrySpawns = sentrySpawns ?? System.Array.Empty<MainEscapeSentrySpawnPoint>();
        ChaserSpawnCell = chaserSpawnCell;
    }

    public Vector3Int[] PatrolSpawnCells { get; }
    public MainEscapeSentrySpawnPoint[] SentrySpawns { get; }
    public Vector3Int ChaserSpawnCell { get; }
}

public sealed class RAuthoredFloorEncounterRandomizationPlanner : IFloorEncounterRandomizationPlanner
{
    private const float KeySpawnSoftAvoidDistance = 16f;
    private const float PlayerStartSpawnSoftAvoidDistance = 6f;

    public RFloorEncounterRandomizationPlan BuildPlan(
        MainEscapeFloorAuthoring floorAuthoring,
        int floorNumber,
        int runSeed,
        Vector3Int[] defaultPatrolSpawnCells,
        MainEscapeSentrySpawnPoint[] defaultSentrySpawns,
        Vector3Int defaultChaserSpawnCell)
    {
        Vector3Int[] resolvedPatrolSpawnCells = defaultPatrolSpawnCells ?? System.Array.Empty<Vector3Int>();
        MainEscapeSentrySpawnPoint[] resolvedSentrySpawns = defaultSentrySpawns ?? System.Array.Empty<MainEscapeSentrySpawnPoint>();
        Vector3Int resolvedChaserSpawnCell = defaultChaserSpawnCell;

        if (floorAuthoring == null)
        {
            return new RFloorEncounterRandomizationPlan(resolvedPatrolSpawnCells, resolvedSentrySpawns, resolvedChaserSpawnCell);
        }

        System.Random random = new(CombineSeed(runSeed, floorNumber));

        if (!TryResolveSharedEnemySpawns(floorAuthoring, random, out resolvedPatrolSpawnCells, out resolvedSentrySpawns))
        {
            resolvedPatrolSpawnCells = ResolvePatrolSpawnCells(
                floorAuthoring.GetPatrolSpawnCandidateCells(),
                resolvedPatrolSpawnCells,
                random);

            resolvedSentrySpawns = ResolveSentrySpawns(
                floorAuthoring.GetSentrySpawnCandidatePoints(),
                resolvedSentrySpawns,
                random);
        }

        resolvedChaserSpawnCell = ResolveChaserSpawnCell(
            floorAuthoring.GetEnemyPlacementMarkers(MainEscapeEnemyPlacementKind.Chaser),
            resolvedChaserSpawnCell,
            floorAuthoring.EnemyPlacementQuota.ChaserCount,
            floorAuthoring.GroundTilemap,
            floorAuthoring,
            random);

        return new RFloorEncounterRandomizationPlan(resolvedPatrolSpawnCells, resolvedSentrySpawns, resolvedChaserSpawnCell);
    }

    private static bool TryResolveSharedEnemySpawns(
        MainEscapeFloorAuthoring floorAuthoring,
        System.Random random,
        out Vector3Int[] patrolSpawnCells,
        out MainEscapeSentrySpawnPoint[] sentrySpawns)
    {
        patrolSpawnCells = System.Array.Empty<Vector3Int>();
        sentrySpawns = System.Array.Empty<MainEscapeSentrySpawnPoint>();

        if (floorAuthoring == null || random == null || floorAuthoring.GroundTilemap == null)
        {
            return false;
        }

        MainEscapeEnemyPlacementMarker[] sharedMarkers = floorAuthoring.GetSharedEnemyPlacementMarkers();

        if (sharedMarkers == null || sharedMarkers.Length == 0)
        {
            return false;
        }

        List<SharedEnemySpawnCandidate> uniqueCandidates = new(sharedMarkers.Length);
        HashSet<Vector3Int> seenCells = new();

        for (int index = 0; index < sharedMarkers.Length; index++)
        {
            MainEscapeEnemyPlacementMarker marker = sharedMarkers[index];

            if (marker == null || !marker.TryResolveCell(floorAuthoring.GroundTilemap, out Vector3Int cell) || !seenCells.Add(cell))
            {
                continue;
            }

            uniqueCandidates.Add(new SharedEnemySpawnCandidate(
                marker.PlacementId,
                cell,
                marker.Facing,
                marker.GetWorldPosition()));
        }

        if (uniqueCandidates.Count == 0)
        {
            return false;
        }

        Shuffle(random, uniqueCandidates);
        PrioritizeCandidatesAwayFromSoftAvoids(floorAuthoring, uniqueCandidates);

        MainEscapeEnemyPlacementQuota enemyQuota = floorAuthoring.EnemyPlacementQuota;
        int patrolCount = Mathf.Min(enemyQuota.PatrolCount, uniqueCandidates.Count);
        int sentryCount = Mathf.Min(enemyQuota.SentryCount, Mathf.Max(0, uniqueCandidates.Count - patrolCount));

        if (patrolCount <= 0 && sentryCount <= 0)
        {
            return false;
        }

        patrolSpawnCells = new Vector3Int[patrolCount];

        for (int index = 0; index < patrolCount; index++)
        {
            patrolSpawnCells[index] = uniqueCandidates[index].Cell;
        }

        sentrySpawns = new MainEscapeSentrySpawnPoint[sentryCount];

        for (int index = 0; index < sentryCount; index++)
        {
            SharedEnemySpawnCandidate candidate = uniqueCandidates[patrolCount + index];
            sentrySpawns[index] = new MainEscapeSentrySpawnPoint(
                candidate.PlacementId,
                candidate.Cell,
                candidate.Facing,
                candidate.WorldPosition);
        }

        return true;
    }

    private static void PrioritizeCandidatesAwayFromSoftAvoids(
        MainEscapeFloorAuthoring floorAuthoring,
        List<SharedEnemySpawnCandidate> candidates)
    {
        if (floorAuthoring == null
            || candidates == null
            || candidates.Count <= 1)
        {
            return;
        }

        bool hasKeyPosition = floorAuthoring.TryResolveKeyPickupWorldPosition(out Vector3 keyWorldPosition);
        bool hasPlayerStartPosition = floorAuthoring.TryResolvePlayerStartWorldPosition(out Vector3 playerStartWorldPosition);

        if (!hasKeyPosition && !hasPlayerStartPosition)
        {
            return;
        }

        float minimumDistanceSqr = KeySpawnSoftAvoidDistance * KeySpawnSoftAvoidDistance;
        float playerStartMinimumDistanceSqr = PlayerStartSpawnSoftAvoidDistance * PlayerStartSpawnSoftAvoidDistance;
        int safeInsertIndex = 0;

        for (int index = 0; index < candidates.Count; index++)
        {
            SharedEnemySpawnCandidate candidate = candidates[index];
            Vector2 candidatePosition = candidate.WorldPosition;

            if (IsInsideSoftAvoidRadius(candidatePosition, hasKeyPosition, keyWorldPosition, minimumDistanceSqr)
                || IsInsideSoftAvoidRadius(candidatePosition, hasPlayerStartPosition, playerStartWorldPosition, playerStartMinimumDistanceSqr))
            {
                continue;
            }

            if (index != safeInsertIndex)
            {
                (candidates[safeInsertIndex], candidates[index]) = (candidates[index], candidates[safeInsertIndex]);
            }

            safeInsertIndex++;
        }
    }

    private static bool IsInsideSoftAvoidRadius(
        Vector2 candidatePosition,
        bool hasAvoidPosition,
        Vector3 avoidWorldPosition,
        float minimumDistanceSqr)
    {
        if (!hasAvoidPosition)
        {
            return false;
        }

        Vector2 avoidPosition = avoidWorldPosition;
        return (candidatePosition - avoidPosition).sqrMagnitude < minimumDistanceSqr;
    }

    private static Vector3Int[] ResolvePatrolSpawnCells(
        Vector3Int[] candidateCells,
        Vector3Int[] defaultCells,
        System.Random random)
    {
        if (!HasUsableCandidatePool(candidateCells, defaultCells.Length))
        {
            return defaultCells;
        }

        List<Vector3Int> shuffled = new(candidateCells);
        Shuffle(random, shuffled);
        return shuffled.GetRange(0, defaultCells.Length).ToArray();
    }

    private static MainEscapeSentrySpawnPoint[] ResolveSentrySpawns(
        MainEscapeSentrySpawnPoint[] candidateSpawns,
        MainEscapeSentrySpawnPoint[] defaultSpawns,
        System.Random random)
    {
        if (!HasUsableCandidatePool(candidateSpawns, defaultSpawns.Length))
        {
            return defaultSpawns;
        }

        List<MainEscapeSentrySpawnPoint> shuffled = new(candidateSpawns);
        Shuffle(random, shuffled);
        return shuffled.GetRange(0, defaultSpawns.Length).ToArray();
    }

    private static Vector3Int ResolveChaserSpawnCell(
        MainEscapeEnemyPlacementMarker[] candidateMarkers,
        Vector3Int defaultCell,
        int desiredCount,
        UnityEngine.Tilemaps.Tilemap groundTilemap,
        MainEscapeFloorAuthoring floorAuthoring,
        System.Random random)
    {
        if (desiredCount <= 0 || candidateMarkers == null || candidateMarkers.Length == 0 || groundTilemap == null)
        {
            return defaultCell;
        }

        List<MainEscapeEnemyPlacementMarker> shuffled = new(candidateMarkers);
        Shuffle(random, shuffled);

        if (floorAuthoring != null && floorAuthoring.TryResolveKeyPickupWorldPosition(out Vector3 keyWorldPosition))
        {
            PrioritizeMarkersAwayFromKey(shuffled, keyWorldPosition);
        }

        for (int index = 0; index < shuffled.Count; index++)
        {
            MainEscapeEnemyPlacementMarker marker = shuffled[index];

            if (marker != null && marker.TryResolveCell(groundTilemap, out Vector3Int cell))
            {
                return cell;
            }
        }

        return defaultCell;
    }

    private static void PrioritizeMarkersAwayFromKey(
        List<MainEscapeEnemyPlacementMarker> markers,
        Vector3 keyWorldPosition)
    {
        if (markers == null || markers.Count <= 1)
        {
            return;
        }

        float minimumDistanceSqr = KeySpawnSoftAvoidDistance * KeySpawnSoftAvoidDistance;
        int safeInsertIndex = 0;

        for (int index = 0; index < markers.Count; index++)
        {
            MainEscapeEnemyPlacementMarker marker = markers[index];

            if (marker == null)
            {
                continue;
            }

            Vector2 markerPosition = marker.GetWorldPosition();
            Vector2 keyPosition = keyWorldPosition;

            if ((markerPosition - keyPosition).sqrMagnitude < minimumDistanceSqr)
            {
                continue;
            }

            if (index != safeInsertIndex)
            {
                (markers[safeInsertIndex], markers[index]) = (markers[index], markers[safeInsertIndex]);
            }

            safeInsertIndex++;
        }
    }

    private static bool HasUsableCandidatePool<T>(T[] candidates, int desiredCount)
    {
        return candidates != null
            && desiredCount > 0
            && candidates.Length >= desiredCount;
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
            hash = (hash * 31) + 911;
            return hash;
        }
    }

    private readonly struct SharedEnemySpawnCandidate
    {
        public SharedEnemySpawnCandidate(
            string placementId,
            Vector3Int cell,
            Vector2 facing,
            Vector3 worldPosition)
        {
            PlacementId = string.IsNullOrWhiteSpace(placementId) ? string.Empty : placementId;
            Cell = cell;
            Facing = facing;
            WorldPosition = worldPosition;
        }

        public string PlacementId { get; }
        public Vector3Int Cell { get; }
        public Vector2 Facing { get; }
        public Vector3 WorldPosition { get; }
    }
}

[DisallowMultipleComponent]
public sealed partial class RFloorDirector : MonoBehaviour, IMainEscapeDebugPresentationController, IDebugPresentationApplier, IGateAnchorReadModel
{
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField] private SpriteRenderer accentBackdrop;
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private REncounterSpawner encounterSpawner;
    [SerializeField] private DoorController mainGateDoorController;
    [SerializeField] private bool usesRuntimeManagedPickupPlacements;

    private readonly HashSet<string> runtimeManagedPickupItemIds = new(System.StringComparer.Ordinal);
    private readonly IFloorBuildPipeline floorBuildPipeline = new RLegacyFloorBuildPipeline();
    private readonly IFloorDoorAssembler floorDoorAssembler = new RRuntimeDoorAssembler();
    private readonly IFloorEncounterRandomizationPlanner encounterRandomizationPlanner = new RAuthoredFloorEncounterRandomizationPlanner();
    private readonly IFloorItemPlacementPlanner itemPlacementPlanner = new RAuthoredFloorItemPlacementPlanner();
    private readonly IFloorRuntimeItemPlacementController runtimeItemPlacementController = new RAuthoredFloorRuntimeItemPlacementController();
    private readonly IFloorTrapPlacementPlanner trapPlacementPlanner = new RAuthoredFloorTrapPlacementPlanner();
    private readonly IFloorRuntimeTrapPlacementController runtimeTrapPlacementController = new RAuthoredFloorRuntimeTrapPlacementController();
    private OfficeFloorBuildResult currentBuild;

    public GridMapService CurrentMapService => currentBuild.MapService;
    public GeneratedFloorLayout CurrentLayout => currentBuild.Layout;
    public Transform CurrentFloorRoot => currentBuild.FloorRoot;
    public Vector2 CurrentWorldCenter => currentBuild.WorldCenter;
    public Vector2 CurrentWorldSize => currentBuild.WorldSize;
    public Vector3Int[] CurrentPatrolRoute => currentBuild.PatrolRoute;
    public Vector3Int CurrentStalkerSpawnCell => currentBuild.StalkerSpawnCell;
    public MainEscapeSentrySpawnPoint[] CurrentSentrySpawns => currentBuild.SentrySpawns;
    public MainEscapeVentRouteDefinition CurrentVentRoute => currentBuild.VentRoute;
    public bool DebugPresentationEnabled => encounterSpawner != null && encounterSpawner.ShowVentMarkers;
    public bool UsesRuntimeManagedPickupPlacements => runtimeManagedPickupItemIds.Count > 0;
    public bool HasMainGate => currentBuild.Layout != null
        && currentBuild.Layout.MainDoorCells != null
        && currentBuild.Layout.MainDoorCells.Length > 0;

    public bool TryGetCurrentPlayerSpawnWorldPosition(out Vector3 worldPosition)
    {
        if (currentBuild.HasPlayerSpawnWorldPosition)
        {
            worldPosition = currentBuild.PlayerSpawnWorldPosition;
            return true;
        }

        worldPosition = default;
        return false;
    }

    public bool IsRuntimeManagedPickupPlacement(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId)
            && runtimeManagedPickupItemIds.Contains(itemId);
    }

    public bool TryGetGateWorldPosition(out Vector3 worldPosition)
    {
        if (mainGateDoorController != null)
        {
            worldPosition = mainGateDoorController.transform.position;
            return true;
        }

        if (currentBuild.MapService != null
            && currentBuild.Layout != null
            && currentBuild.Layout.MainDoorCells != null
            && currentBuild.Layout.MainDoorCells.Length > 0)
        {
            Vector3 accumulatedPosition = Vector3.zero;

            for (int index = 0; index < currentBuild.Layout.MainDoorCells.Length; index++)
            {
                accumulatedPosition += currentBuild.MapService.CellToWorldCenter(currentBuild.Layout.MainDoorCells[index]);
            }

            worldPosition = accumulatedPosition / currentBuild.Layout.MainDoorCells.Length;
            return true;
        }

        worldPosition = default;
        return false;
    }

    public void Initialize(
        WasdPlayerController player,
        RPlayerRuntimeReferences runtime,
        FlashlightFogOfWarOverlay fogOverlay,
        SpriteRenderer backdrop,
        bool enableVentMarkers = false)
    {
        playerController = player;
        playerRuntime = runtime;
        fogOfWarOverlay = fogOverlay;
        accentBackdrop = backdrop;

        if (encounterSpawner == null)
        {
            Debug.LogError($"{nameof(RFloorDirector)} requires an authored {nameof(REncounterSpawner)} reference.", this);
            return;
        }

        encounterSpawner.Initialize(playerController, playerRuntime, fogOfWarOverlay, enableVentMarkers);
    }

    public bool BuildFloor(EscapeFloorDefinition floorDefinition)
    {
        if (floorDefinition == null)
        {
            return false;
        }

        DestroyCurrentFloor();
        ClearRuntimeManagedPickupItemIds();

        if (!floorBuildPipeline.TryBuildFloor(floorDefinition, gameObject.scene, transform, out currentBuild, out string failureReason))
        {
            Debug.LogError(string.IsNullOrWhiteSpace(failureReason)
                ? $"Failed to build rebuild floor {floorDefinition.FloorNumber}F."
                : failureReason, this);
            return false;
        }

        EnableSceneMovementBlockerColliders();
        ConfigureBackdrop(floorDefinition);
        mainGateDoorController = floorDoorAssembler.AssembleDoors(currentBuild, transform).MainGateDoorController;

        LayerMask blockingLayers = currentBuild.MapService != null
            ? currentBuild.MapService.VisionBlockingLayers
            : GameLayers.VisionBlockingMask;
        fogOfWarOverlay?.Initialize(playerController, currentBuild.WorldCenter, currentBuild.WorldSize, blockingLayers, currentBuild.MapService);

        if (encounterSpawner == null)
        {
            Debug.LogError($"{nameof(RFloorDirector)} cannot configure encounters because no {nameof(REncounterSpawner)} is assigned.", this);
            return false;
        }

        Vector3Int[] patrolSpawnCells = currentBuild.PatrolSpawnCells;
        Vector3Int stalkerSpawnCell = currentBuild.StalkerSpawnCell;
        MainEscapeSentrySpawnPoint[] sentrySpawns = currentBuild.SentrySpawns;

        RRunSessionController sessionController = ResolveSceneSessionController();
        MainEscapeFloorAuthoring floorAuthoring = currentBuild.FloorRoot != null
            ? currentBuild.FloorRoot.GetComponent<MainEscapeFloorAuthoring>()
            : null;
        int runSeed = sessionController != null ? sessionController.RunRandomizationSeed : 0;
        MainEscapeRuntimePrefabCatalog prefabCatalog = MainEscapeRuntimePrefabCatalog.LoadForScene(gameObject.scene);

        if (floorAuthoring != null)
        {
            HashSet<string> sceneMarkerSupportPickupItemIds = CollectSceneMarkerSupportPickupItemIds(floorAuthoring);
            RFloorItemPlacementPlan itemPlacementPlan = itemPlacementPlanner.BuildPlan(
                floorAuthoring,
                floorDefinition.FloorNumber,
                runSeed);

            if (sceneMarkerSupportPickupItemIds.Count > 0)
            {
                itemPlacementPlan = itemPlacementPlan.WithoutItemIds(sceneMarkerSupportPickupItemIds);
            }

            HashSet<string> plannedRuntimeManagedPickupItemIds = CollectPlanItemIds(itemPlacementPlan);
            HashSet<string> requestedRuntimeManagedPickupItemIds = MergeItemIds(
                sceneMarkerSupportPickupItemIds,
                plannedRuntimeManagedPickupItemIds);
            bool legacyItemPlacementApplied = runtimeItemPlacementController.ApplyPlan(
                gameObject.scene,
                currentBuild.FloorRoot,
                floorAuthoring,
                prefabCatalog,
                itemPlacementPlan,
                requestedRuntimeManagedPickupItemIds);

            ReplaceRuntimeManagedPickupItemIds(sceneMarkerSupportPickupItemIds);

            if (legacyItemPlacementApplied)
            {
                AddRuntimeManagedPickupItemIds(plannedRuntimeManagedPickupItemIds);
            }

            RFloorTrapPlacementPlan trapPlacementPlan = trapPlacementPlanner.BuildPlan(
                floorAuthoring,
                floorDefinition.FloorNumber,
                runSeed);

            if (floorAuthoring.GlassTrapPlacementCount > 0 && !trapPlacementPlan.HasPlacements)
            {
                Debug.LogWarning(
                    $"{nameof(RFloorDirector)} could not build any runtime glass-trap placements for {floorDefinition.FloorNumber}F. Check {nameof(MainEscapeFloorAuthoring)} danger markers and trap quota.",
                    floorAuthoring);
            }
            else if (trapPlacementPlan.HasPlacements && !runtimeTrapPlacementController.ApplyPlan(
                         gameObject.scene,
                         currentBuild.FloorRoot,
                         prefabCatalog,
                         trapPlacementPlan))
            {
                Debug.LogError(
                    $"{nameof(RFloorDirector)} failed to apply runtime glass-trap placements for {floorDefinition.FloorNumber}F.",
                    floorAuthoring);
            }

            ApplySceneMarkerPlacementManagers(floorAuthoring, runSeed);
        }

        if (sessionController != null && sessionController.RandomizeGroundEnemyPlacements)
        {
            RFloorEncounterRandomizationPlan encounterPlan = encounterRandomizationPlanner.BuildPlan(
                floorAuthoring,
                floorDefinition.FloorNumber,
                runSeed,
                patrolSpawnCells,
                sentrySpawns,
                stalkerSpawnCell);

            patrolSpawnCells = encounterPlan.PatrolSpawnCells;
            sentrySpawns = encounterPlan.SentrySpawns;
            stalkerSpawnCell = encounterPlan.ChaserSpawnCell;
        }

        encounterSpawner.ConfigureFloor(
            playerController,
            playerRuntime,
            currentBuild.MapService,
            currentBuild.Layout,
            floorDefinition,
            currentBuild.PatrolRoute,
            patrolSpawnCells,
            stalkerSpawnCell,
            sentrySpawns,
            currentBuild.VentRoute,
            prefabCatalog);

        return true;
    }

    private RRunSessionController ResolveSceneSessionController()
    {
        return RRunSessionResolver.ResolveForContext(this);
    }

    public void SetDebugPresentation(bool enabled)
    {
        encounterSpawner?.SetVentMarkersVisible(enabled);
    }

    public void ApplyDebugPresentation(bool enabled)
    {
        SetDebugPresentation(enabled);
    }

    private void ConfigureBackdrop(EscapeFloorDefinition floorDefinition)
    {
        if (accentBackdrop == null)
        {
            return;
        }

        accentBackdrop.transform.position = new Vector3(currentBuild.WorldCenter.x, currentBuild.WorldCenter.y, 0f);
        accentBackdrop.transform.localScale = new Vector3(currentBuild.WorldSize.x * 0.92f, currentBuild.WorldSize.y * 0.92f, 1f);
        Color accentColor = floorDefinition.AccentColor;
        accentColor.a = 0.24f;
        accentBackdrop.color = accentColor;
        accentBackdrop.sortingOrder = -4;
    }

    private void DestroyCurrentFloor()
    {
        encounterSpawner?.ClearFloor();
        runtimeItemPlacementController.Clear(gameObject.scene);
        runtimeTrapPlacementController.Clear(gameObject.scene);
        ClearRuntimeManagedPickupItemIds();
        floorDoorAssembler.DestroyDoors(transform);
        mainGateDoorController = null;

        if (currentBuild.FloorRoot != null && currentBuild.FloorRoot.parent == transform)
        {
            currentBuild.FloorRoot.gameObject.SetActive(false);
            Destroy(currentBuild.FloorRoot.gameObject);
        }
        else if (Application.isPlaying && currentBuild.IsAuthored && currentBuild.FloorRoot != null)
        {
            currentBuild.FloorRoot.gameObject.SetActive(false);
        }

        currentBuild = default;
    }

    private void ReplaceRuntimeManagedPickupItemIds(IReadOnlyCollection<string> itemIds)
    {
        runtimeManagedPickupItemIds.Clear();
        AddRuntimeManagedPickupItemIds(itemIds);
    }

    private void AddRuntimeManagedPickupItemIds(IReadOnlyCollection<string> itemIds)
    {
        if (itemIds == null)
        {
            usesRuntimeManagedPickupPlacements = runtimeManagedPickupItemIds.Count > 0;
            return;
        }

        foreach (string itemId in itemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                runtimeManagedPickupItemIds.Add(itemId);
            }
        }

        usesRuntimeManagedPickupPlacements = runtimeManagedPickupItemIds.Count > 0;
    }

    private void ClearRuntimeManagedPickupItemIds()
    {
        runtimeManagedPickupItemIds.Clear();
        usesRuntimeManagedPickupPlacements = false;
    }

    private static HashSet<string> CollectPlanItemIds(RFloorItemPlacementPlan plan)
    {
        HashSet<string> itemIds = new(System.StringComparer.Ordinal);

        if (!plan.HasPlacements)
        {
            return itemIds;
        }

        RFloorRuntimeItemPlacement[] placements = plan.Placements;

        for (int index = 0; index < placements.Length; index++)
        {
            string itemId = placements[index].ItemId;

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                itemIds.Add(itemId);
            }
        }

        return itemIds;
    }

    private static HashSet<string> MergeItemIds(
        IReadOnlyCollection<string> firstItemIds,
        IReadOnlyCollection<string> secondItemIds)
    {
        HashSet<string> mergedItemIds = new(System.StringComparer.Ordinal);
        AddItemIds(mergedItemIds, firstItemIds);
        AddItemIds(mergedItemIds, secondItemIds);
        return mergedItemIds;
    }

    private static void AddItemIds(HashSet<string> target, IReadOnlyCollection<string> itemIds)
    {
        if (target == null || itemIds == null)
        {
            return;
        }

        foreach (string itemId in itemIds)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                target.Add(itemId);
            }
        }
    }

    private void EnableSceneMovementBlockerColliders()
    {
        if (!Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        MainEscapeMovementBlockerAuthoring[] movementBlockers =
            RSceneReferenceLookup.FindComponentsInScene<MainEscapeMovementBlockerAuthoring>(gameObject.scene);

        for (int index = 0; index < movementBlockers.Length; index++)
        {
            MainEscapeMovementBlockerAuthoring blocker = movementBlockers[index];

            if (blocker == null)
            {
                continue;
            }

            BoxCollider2D collider = blocker.GetComponent<BoxCollider2D>();

            if (collider != null)
            {
                collider.enabled = true;
            }
        }
    }

    public void SetMainGateInteractionEnabled(bool enabled)
    {
        if (mainGateDoorController != null)
        {
            mainGateDoorController.gameObject.SetActive(enabled);
        }
    }

    public bool TryUnlockMainGateRoute()
    {
        bool hasTileBackedGate = HasMainGate;
        if (!hasTileBackedGate)
        {
            Debug.LogError($"{nameof(RFloorDirector)} cannot unlock the authored main gate because no main gate cells were built for the current floor.", this);
            return false;
        }

        bool openedDoor = SetMainGateOpen(true);

        if (openedDoor)
        {
            SetMainGateInteractionEnabled(false);
        }

        return openedDoor;
    }

    public void ApplyMainGateRouting(bool usesAuthoredKeyGateSequence, bool gateUnlocked)
    {
        if (usesAuthoredKeyGateSequence)
        {
            SetMainGateInteractionEnabled(false);
            SetMainGateOpen(gateUnlocked);
            return;
        }

        SetMainGateInteractionEnabled(true);
    }

    public bool SetMainGateOpen(bool open)
    {
        if (!HasMainGate || currentBuild.MapService == null)
        {
            return false;
        }

        bool changed = false;
        Vector3Int[] mainGateCells = currentBuild.Layout.MainDoorCells;

        for (int index = 0; index < mainGateCells.Length; index++)
        {
            changed |= open
                ? currentBuild.MapService.OpenDoor(mainGateCells[index])
                : currentBuild.MapService.CloseDoor(mainGateCells[index]);
        }

        return changed;
    }
}

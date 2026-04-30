using System.Collections.Generic;

using UnityEngine;

[DisallowMultipleComponent]
public sealed class REncounterSpawner : MonoBehaviour
{
    private const int MinimumEnemySortingOrder = 24;
    private const string RuntimeEnemyRootName = "RuntimeSpawnedEnemies";

    [SerializeField, Min(0f)] private float stalkerSpawnDelay = 12f;
    [SerializeField, Min(0f)] private float chaseReachabilityRepairDelay = 0.2f;

    [Header("Authored Inputs")]
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private bool showVentMarkers;
    [SerializeField] private GridMapService mapService;
    [SerializeField] private GeneratedFloorLayout layout;
    [SerializeField] private EscapeFloorDefinition currentFloor;

    [Header("Authored Scene Wiring")]
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private MainEscapeVentNetworkDebugOverlay ventNetworkDebugOverlay;

    private Transform runtimeEnemyRoot;
    private Vector3Int[] patrolRouteCells = System.Array.Empty<Vector3Int>();
    private Vector3Int[] patrolSpawnCells = System.Array.Empty<Vector3Int>();
    private MainEscapeSentrySpawnPoint[] sentrySpawns = System.Array.Empty<MainEscapeSentrySpawnPoint>();
    private MainEscapeVentRouteDefinition ventRoute = MainEscapeVentRouteDefinition.Empty;
    private Vector3Int stalkerSpawnCell;
    private VisibilityTarget2D playerTarget;
    private PlayerTrailRecorder playerTrail;
    private MainEscapeRuntimePrefabCatalog runtimePrefabCatalog;
    private float stalkerSpawnTime;
    private float chaseReachabilityRepairTime;
    private bool stalkerSpawned;
    private bool chaseReachabilityReconciled;

    public bool ShowVentMarkers => showVentMarkers;
    public bool HasFloor => currentFloor != null;
    public EscapeFloorDefinition CurrentFloor => currentFloor;
    private bool IsFiveFloorVentOnlyMode => currentFloor != null && currentFloor.FloorNumber == 5;

    private void Awake()
    {
        ValidateBindings();
    }

    private void OnValidate()
    {
        ValidateBindings();
    }

    private void Update()
    {
        if (!IsFiveFloorVentOnlyMode
            && !chaseReachabilityReconciled
            && currentFloor != null
            && mapService != null
            && playerController != null
            && GetRuntimeEnemyRoot() != null
            && Time.time >= chaseReachabilityRepairTime)
        {
            chaseReachabilityReconciled = EnsurePlayerReachableGroundEnemy();

            if (!chaseReachabilityReconciled)
            {
                chaseReachabilityRepairTime = Time.time + chaseReachabilityRepairDelay;
            }
        }

        if (IsFiveFloorVentOnlyMode
            || stalkerSpawned
            || currentFloor == null
            || mapService == null
            || layout == null
            || playerTarget == null
            || GetRuntimeEnemyRoot() == null
            || Time.time < stalkerSpawnTime)
        {
            return;
        }

        SpawnStalker();
        stalkerSpawned = true;
    }

    public void Initialize(
        WasdPlayerController configuredPlayerController,
        RPlayerRuntimeReferences configuredPlayerRuntime,
        FlashlightFogOfWarOverlay configuredFogOfWarOverlay = null,
        bool enableVentMarkers = false)
    {
        playerController = configuredPlayerController;
        playerRuntime = configuredPlayerRuntime;
        fogOfWarOverlay = configuredFogOfWarOverlay;
        showVentMarkers = enableVentMarkers;
        CachePlayerBindings();
    }

    public void ConfigureFloor(
        WasdPlayerController configuredPlayerController,
        RPlayerRuntimeReferences configuredPlayerRuntime,
        GridMapService configuredMapService,
        GeneratedFloorLayout configuredLayout,
        EscapeFloorDefinition floorDefinition,
        Vector3Int[] configuredPatrolRoute = null,
        Vector3Int[] configuredPatrolSpawnCells = null,
        Vector3Int configuredStalkerSpawnCell = default,
        MainEscapeSentrySpawnPoint[] configuredSentrySpawns = null,
        MainEscapeVentRouteDefinition configuredVentRoute = default,
        MainEscapeRuntimePrefabCatalog configuredRuntimePrefabCatalog = null)
    {
        playerController = configuredPlayerController;
        playerRuntime = configuredPlayerRuntime;
        mapService = configuredMapService;
        layout = configuredLayout;
        currentFloor = floorDefinition;
        patrolRouteCells = configuredPatrolRoute ?? System.Array.Empty<Vector3Int>();
        patrolSpawnCells = configuredPatrolSpawnCells ?? System.Array.Empty<Vector3Int>();
        sentrySpawns = configuredSentrySpawns ?? System.Array.Empty<MainEscapeSentrySpawnPoint>();
        ventRoute = configuredVentRoute.IsValid ? configuredVentRoute : MainEscapeVentRouteDefinition.Empty;
        stalkerSpawnCell = configuredStalkerSpawnCell;
        runtimePrefabCatalog = configuredRuntimePrefabCatalog != null
            ? configuredRuntimePrefabCatalog
            : MainEscapeRuntimePrefabCatalog.LoadForScene(gameObject.scene);

        CachePlayerBindings();
        if (!CanConfigureFloor())
        {
            return;
        }

        EnsureRuntimeEnemyRoot();
        ClearFloor();
        playerTrail?.Configure(mapService);
        GetOrCreateVentNetworkDebugOverlay().Configure(layout, mapService, ventRoute, showVentMarkers);

        if (IsFiveFloorVentOnlyMode)
        {
            SpawnStationarySentries();
            SpawnVentEnemy();
            stalkerSpawned = true;
            stalkerSpawnTime = 0f;
            chaseReachabilityReconciled = true;
            chaseReachabilityRepairTime = 0f;
            return;
        }

        SpawnPatrolGuards();
        SpawnStationarySentries();
        SpawnVentEnemy();
        stalkerSpawned = false;
        stalkerSpawnTime = Time.time + stalkerSpawnDelay;
        chaseReachabilityReconciled = false;
        chaseReachabilityRepairTime = Time.time + chaseReachabilityRepairDelay;
    }

    public void ClearFloor()
    {
        ventNetworkDebugOverlay?.Clear();

        Transform runtimeRoot = GetRuntimeEnemyRoot();

        if (runtimeRoot != null)
        {
            for (int index = runtimeRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = runtimeRoot.GetChild(index);

                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        stalkerSpawned = false;
        stalkerSpawnTime = 0f;
        chaseReachabilityReconciled = false;
        chaseReachabilityRepairTime = 0f;
    }

    public void SetVentMarkersVisible(bool visible)
    {
        showVentMarkers = visible;
        GetOrCreateVentNetworkDebugOverlay().Configure(layout, mapService, ventRoute, visible);
    }

    private void CachePlayerBindings()
    {
        if (playerController == null)
        {
            playerTarget = null;
            playerTrail = null;
            return;
        }

        playerTarget = playerController.GetComponent<VisibilityTarget2D>();

        if (playerTarget == null)
        {
            Debug.LogError($"{nameof(REncounterSpawner)} requires an authored VisibilityTarget2D on the assigned player.", this);
        }

        if (playerRuntime == null)
        {
            Debug.LogError($"{nameof(REncounterSpawner)} requires an authored RPlayerRuntimeReferences reference.", this);
        }

        playerTrail = playerRuntime != null ? playerRuntime.TrailRecorder : null;

        if (playerTrail == null && playerRuntime != null)
        {
            Debug.LogWarning($"{nameof(REncounterSpawner)} could not resolve a PlayerTrailRecorder from RPlayerRuntimeReferences.", this);
        }
    }

    private bool CanConfigureFloor()
    {
        if (playerController == null)
        {
            Debug.LogError($"{nameof(REncounterSpawner)} cannot configure floor without a player controller.", this);
            return false;
        }

        if (playerRuntime == null)
        {
            Debug.LogError($"{nameof(REncounterSpawner)} cannot configure floor without player runtime references.", this);
            return false;
        }

        if (playerTarget == null)
        {
            Debug.LogError($"{nameof(REncounterSpawner)} cannot configure floor without a VisibilityTarget2D on the player.", this);
            return false;
        }

        if (enemyRoot == null)
        {
            Debug.LogError($"{nameof(REncounterSpawner)} requires an authored enemy root.", this);
            return false;
        }

        if (mapService == null || layout == null || currentFloor == null)
        {
            Debug.LogError($"{nameof(REncounterSpawner)} requires authored map, layout, and floor data.", this);
            return false;
        }

        return true;
    }

    private void SpawnPatrolGuards()
    {
        Transform spawnRoot = EnsureRuntimeEnemyRoot();

        if (spawnRoot == null)
        {
            return;
        }

        if (TryResolvePatrolSpawnCells(out Vector3Int[] resolvedPatrolSpawnCells))
        {
            EnemyArchetype patrolSpawnArchetype = CreatePatrolArchetype();

            for (int index = 0; index < resolvedPatrolSpawnCells.Length; index++)
            {
                Vector3Int patrolSpawnCell = resolvedPatrolSpawnCells[index];
                string enemyName = resolvedPatrolSpawnCells.Length > 1
                    ? $"PatrolGuard_{index + 1}"
                    : "PatrolGuard";

                EnemyStateMachine spawnedPatrol = EnemyRuntimeFactory.CreateEnemy(
                    spawnRoot,
                    enemyName,
                    mapService.CellToWorldCenter(patrolSpawnCell),
                    mapService,
                    playerTarget,
                    patrolSpawnArchetype,
                    playerTrail,
                    runtimePrefabCatalog: runtimePrefabCatalog,
                    patrolSpawnCell);

                ConfigureEnemySafety(spawnedPatrol);
            }

            return;
        }

        if (!TryResolvePatrolRoute(out Vector3Int[] resolvedPatrolRoute))
        {
            Debug.LogWarning(
                $"{nameof(REncounterSpawner)} skipped patrol guard spawn because no authored patrol route, patrol spawn markers, or walkable fallback cell was available on this floor.",
                this);
            return;
        }

        EnemyArchetype archetype = CreatePatrolArchetype();
        EnemyStateMachine patrol = EnemyRuntimeFactory.CreateEnemy(
            spawnRoot,
            "PatrolGuard",
            mapService.CellToWorldCenter(resolvedPatrolRoute[0]),
            mapService,
            playerTarget,
            archetype,
            playerTrail,
            runtimePrefabCatalog: runtimePrefabCatalog,
            resolvedPatrolRoute);

        ConfigureEnemySafety(patrol);
    }

    private static EnemyArchetype CreatePatrolArchetype()
    {
        EnemyArchetype archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
        archetype.Configure(
            configuredPatrolSpeed: 1.5f,
            configuredInvestigateSpeed: 2.05f,
            configuredChaseSpeed: 3f,
            configuredVisionDistance: 5.8f,
            configuredVisionAngle: 54f,
            configuredHearingRadius: 6.4f,
            configuredPatrolWaitTime: 0.45f,
            configuredIdleWanderRadius: 4,
            configuredRepathInterval: 0.18f,
            configuredChaseMemoryDuration: 0.95f,
            configuredSearchDuration: 3f,
            configuredSearchRadius: 2);
        return archetype;
    }

    private void SpawnStalker()
    {
        if (mapService == null)
        {
            return;
        }

        if (!TryResolveStalkerSpawnCell(out Vector3Int resolvedStalkerSpawnCell))
        {
            Debug.LogWarning(
                $"{nameof(REncounterSpawner)} skipped stalker spawn because no explicit authored stalker cell or nearby fallback cell was available on this floor.",
                this);
            return;
        }

        EnemyArchetype archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
        archetype.Configure(
            configuredPatrolSpeed: 0.95f,
            configuredInvestigateSpeed: 1.2f,
            configuredChaseSpeed: 3.7f,
            configuredVisionDistance: 4.25f,
            configuredVisionAngle: 32f,
            configuredHearingRadius: 4.5f,
            configuredPatrolWaitTime: 0.1f,
            configuredIdleWanderRadius: 2,
            configuredRepathInterval: 0.2f,
            configuredChaseMemoryDuration: 1.3f,
            configuredSearchDuration: 2.25f,
            configuredSearchRadius: 1,
            configuredIdleBehavior: EnemyIdleBehavior.FollowTrail,
            configuredCaptureDistance: 0.42f);

        Transform spawnRoot = EnsureRuntimeEnemyRoot();

        if (spawnRoot == null)
        {
            return;
        }

        EnemyStateMachine stalker = EnemyRuntimeFactory.CreateEnemy(
            spawnRoot,
            "Stalker",
            mapService.CellToWorldCenter(resolvedStalkerSpawnCell),
            mapService,
            playerTarget,
            archetype,
            playerTrail,
            runtimePrefabCatalog: runtimePrefabCatalog);

        ConfigureEnemySafety(stalker);
        chaseReachabilityReconciled = false;
        chaseReachabilityRepairTime = Time.time + 0.05f;
    }

    private void SpawnStationarySentries()
    {
        Transform spawnRoot = EnsureRuntimeEnemyRoot();

        if (sentrySpawns == null
            || sentrySpawns.Length == 0
            || spawnRoot == null
            || mapService == null
            || playerTarget == null)
        {
            return;
        }

        EnemyArchetype archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
        archetype.Configure(
            configuredPatrolSpeed: 0f,
            configuredInvestigateSpeed: 2f,
            configuredChaseSpeed: 3.2f,
            configuredVisionDistance: 6.2f,
            configuredVisionAngle: 52f,
            configuredHearingRadius: 7f,
            configuredPatrolWaitTime: 0.1f,
            configuredIdleWanderRadius: 1,
            configuredRepathInterval: 0.18f,
            configuredChaseMemoryDuration: 1f,
            configuredSearchDuration: 0.5f,
            configuredSearchRadius: 1,
            configuredIdleBehavior: EnemyIdleBehavior.StandGuard,
            configuredCaptureDistance: 0.42f,
            configuredAlertRecoveryBehavior: EnemyAlertRecoveryBehavior.SearchArea);

        HashSet<Vector3Int> usedCells = new();

        for (int index = 0; index < sentrySpawns.Length; index++)
        {
            MainEscapeSentrySpawnPoint spawnPoint = sentrySpawns[index];

            if (!TryResolveSentrySpawnCell(spawnPoint, usedCells, out Vector3Int spawnCell))
            {
                continue;
            }

            EnemyStateMachine sentry = EnemyRuntimeFactory.CreateEnemy(
                spawnRoot,
                $"SentryGuard_{index + 1:00}",
                mapService.CellToWorldCenter(spawnCell),
                mapService,
                playerTarget,
                archetype,
                null,
                spawnPoint.Facing,
                runtimePrefabCatalog: runtimePrefabCatalog);

            ConfigureEnemySafety(sentry);
        }
    }

    private void SpawnVentEnemy()
    {
        Transform spawnRoot = EnsureRuntimeEnemyRoot();

        if (spawnRoot == null)
        {
            return;
        }

        CeilingVentEnemyController controller = BaseOfficeVentEnemyBootstrap.CreateRuntimeEnemy(
            spawnRoot,
            layout,
            mapService,
            playerTarget,
            showVentMarkers: false,
            customVentRoute: ventRoute,
            runtimePrefabCatalog: runtimePrefabCatalog);

        if (controller == null)
        {
            return;
        }

        bool shouldUseVentOnlyAmbientMode = IsFiveFloorVentOnlyMode;
        controller.ConfigureRuntimeBehavior(
            shouldRespondToNoise: !shouldUseVentOnlyAmbientMode,
            shouldFollowPlayerInVentNetwork: !shouldUseVentOnlyAmbientMode,
            followPlayerRetargetInterval: 0.18f,
            shouldAllowEmergence: !shouldUseVentOnlyAmbientMode);
        controller.BindPlayerTrail(playerTrail);
        controller.ConfigureBehaviorProfile(ResolveVentBehaviorProfile());

        if (shouldUseVentOnlyAmbientMode)
        {
            controller.SetVentMarkersVisible(showVentMarkers);
        }

        VentEnemyAudioDriver audioDriver = controller.GetComponent<VentEnemyAudioDriver>();

        if (audioDriver == null)
        {
            Debug.LogWarning($"{nameof(REncounterSpawner)} expected a VentEnemyAudioDriver on the vent enemy, but none was found.", controller);
            return;
        }

        audioDriver.Initialize(playerController);
        InitializePassiveAmbientAudio(controller.gameObject);
        InitializePlayerSpottedAudio(controller.gameObject);
        ApplyFogReactiveVisibility(controller.gameObject);
    }

    private CeilingVentEnemyController.VentBehaviorProfile ResolveVentBehaviorProfile()
    {
        int floorNumber = currentFloor != null ? currentFloor.FloorNumber : 0;
        int authoredNodeCount = ventRoute.IsValid && ventRoute.Nodes != null ? ventRoute.Nodes.Length : 0;

        return floorNumber switch
        {
            2 => new CeilingVentEnemyController.VentBehaviorProfile(
                crawlMoveSpeed: 5.9f,
                ambientRoamDelay: 0.52f,
                noiseRetargetCooldown: 0.46f,
                lowPriorityNoiseEmergeDelay: 0.28f,
                emergeLookDuration: 3.1f,
                playerFollowRetargetInterval: 0.13f,
                playerTrailPredictionSamples: authoredNodeCount >= 28 ? 7 : 6,
                playerTrailInterceptSamples: authoredNodeCount >= 28 ? 12 : 10,
                playerTrailClosestSearchWindow: 22,
                playerFollowTargetCommitDuration: 1.18f),
            3 => new CeilingVentEnemyController.VentBehaviorProfile(
                crawlMoveSpeed: 6.2f,
                ambientRoamDelay: 0.4f,
                noiseRetargetCooldown: 0.38f,
                lowPriorityNoiseEmergeDelay: 0.2f,
                emergeLookDuration: 2.55f,
                playerFollowRetargetInterval: 0.1f,
                playerTrailPredictionSamples: 5,
                playerTrailInterceptSamples: 8,
                playerTrailClosestSearchWindow: 18,
                playerFollowTargetCommitDuration: 0.72f),
            4 => new CeilingVentEnemyController.VentBehaviorProfile(
                crawlMoveSpeed: 5.8f,
                ambientRoamDelay: 0.48f,
                noiseRetargetCooldown: 0.42f,
                lowPriorityNoiseEmergeDelay: 0.24f,
                emergeLookDuration: 3.35f,
                playerFollowRetargetInterval: 0.12f,
                playerTrailPredictionSamples: 7,
                playerTrailInterceptSamples: 11,
                playerTrailClosestSearchWindow: 20,
                playerFollowTargetCommitDuration: 1.24f),
            _ => new CeilingVentEnemyController.VentBehaviorProfile(
                crawlMoveSpeed: 5.5f,
                ambientRoamDelay: 0.9f,
                noiseRetargetCooldown: 0.7f,
                lowPriorityNoiseEmergeDelay: 0.45f,
                emergeLookDuration: 2.8f,
                playerFollowRetargetInterval: 0.18f,
                playerTrailPredictionSamples: 5,
                playerTrailInterceptSamples: 9,
                playerTrailClosestSearchWindow: 18,
                playerFollowTargetCommitDuration: 0.9f)
        };
    }

    private bool TryResolveSentrySpawnCell(
        MainEscapeSentrySpawnPoint spawnPoint,
        HashSet<Vector3Int> usedCells,
        out Vector3Int resolvedCell)
    {
        if (TryReserveSentryCell(spawnPoint.Cell, usedCells, out resolvedCell))
        {
            return true;
        }

        if (TryResolveNearbySentrySpawnCell(spawnPoint, usedCells, out resolvedCell))
        {
            return true;
        }

        resolvedCell = Vector3Int.zero;
        Debug.LogWarning(
            $"{nameof(REncounterSpawner)} skipped authored sentry '{spawnPoint.MarkerName}' because its authored cell {spawnPoint.Cell} and nearby fallback cells were blocked or duplicated.",
            this);
        return false;
    }

    private bool TryReserveSentryCell(Vector3Int candidateCell, HashSet<Vector3Int> usedCells, out Vector3Int reservedCell)
    {
        if (!IsSentrySpawnCellAvailable(candidateCell, usedCells))
        {
            reservedCell = Vector3Int.zero;
            return false;
        }

        usedCells.Add(candidateCell);
        reservedCell = candidateCell;
        return true;
    }

    private bool IsSentrySpawnCellAvailable(Vector3Int candidateCell, HashSet<Vector3Int> usedCells)
    {
        return usedCells != null
            && !usedCells.Contains(candidateCell)
            && mapService != null
            && mapService.IsWalkable(candidateCell, true);
    }

    private bool TryResolvePatrolRoute(out Vector3Int[] resolvedRoute)
    {
        Vector3Int[] chaseableRoute = FilterChaseableRouteCells(patrolRouteCells);

        if (chaseableRoute.Length > 0)
        {
            patrolRouteCells = chaseableRoute;
            resolvedRoute = chaseableRoute;
            return true;
        }

        if (TryResolvePlayerReachableSpawnCell(8, 18, null, out Vector3Int playerReachablePatrolCell))
        {
            patrolRouteCells = new[] { playerReachablePatrolCell };
            resolvedRoute = patrolRouteCells;
            return true;
        }

        if (TryResolvePlayerNeighborhoodCell(2, 6, null, out Vector3Int playerNeighborhoodPatrolCell))
        {
            patrolRouteCells = new[] { playerNeighborhoodPatrolCell };
            resolvedRoute = patrolRouteCells;
            return true;
        }

        Vector3Int[] sanitizedRoute = FilterWalkableRouteCells(patrolRouteCells);

        if (sanitizedRoute.Length > 0)
        {
            patrolRouteCells = sanitizedRoute;
            resolvedRoute = sanitizedRoute;
            return true;
        }

        if (layout != null && IsWalkableSpawnCell(layout.PatrolSpawnCell))
        {
            patrolRouteCells = new[] { layout.PatrolSpawnCell };
            resolvedRoute = patrolRouteCells;
            return true;
        }

        if (layout != null && TryResolveNearbyWalkableCell(layout.PatrolSpawnCell, 5, out Vector3Int fallbackPatrolCell))
        {
            patrolRouteCells = new[] { fallbackPatrolCell };
            resolvedRoute = patrolRouteCells;
            return true;
        }

        resolvedRoute = System.Array.Empty<Vector3Int>();
        return false;
    }

    private bool TryResolvePatrolSpawnCells(out Vector3Int[] resolvedCells)
    {
        Vector3Int[] authoredResolvedSpawnCells = ResolvePatrolSpawnCellsNearMarkers(8);

        if (authoredResolvedSpawnCells.Length > 0)
        {
            patrolSpawnCells = authoredResolvedSpawnCells;
            resolvedCells = authoredResolvedSpawnCells;
            return true;
        }

        Vector3Int[] walkableSpawnCells = FilterWalkableRouteCells(patrolSpawnCells);

        if (walkableSpawnCells.Length > 0)
        {
            patrolSpawnCells = walkableSpawnCells;
            resolvedCells = walkableSpawnCells;
            return true;
        }

        Vector3Int[] chaseableSpawnCells = FilterChaseableRouteCells(patrolSpawnCells);

        if (chaseableSpawnCells.Length > 0)
        {
            patrolSpawnCells = chaseableSpawnCells;
            resolvedCells = chaseableSpawnCells;
            return true;
        }

        resolvedCells = System.Array.Empty<Vector3Int>();
        return false;
    }

    private Vector3Int[] ResolvePatrolSpawnCellsNearMarkers(int maxResolveRadius)
    {
        if (patrolSpawnCells == null || patrolSpawnCells.Length == 0)
        {
            return System.Array.Empty<Vector3Int>();
        }

        List<Vector3Int> resolvedSpawnCells = new(patrolSpawnCells.Length);

        for (int index = 0; index < patrolSpawnCells.Length; index++)
        {
            Vector3Int candidateCell = patrolSpawnCells[index];

            if (!IsWalkableSpawnCell(candidateCell)
                && !TryResolveNearbyWalkableCell(candidateCell, maxResolveRadius, out candidateCell))
            {
                continue;
            }

            if (resolvedSpawnCells.Contains(candidateCell))
            {
                continue;
            }

            resolvedSpawnCells.Add(candidateCell);
        }

        return resolvedSpawnCells.ToArray();
    }

    private Vector3Int[] FilterWalkableRouteCells(Vector3Int[] routeCells)
    {
        if (routeCells == null || routeCells.Length == 0)
        {
            return System.Array.Empty<Vector3Int>();
        }

        List<Vector3Int> walkableCells = new(routeCells.Length);

        for (int index = 0; index < routeCells.Length; index++)
        {
            Vector3Int cell = routeCells[index];

            if (!IsWalkableSpawnCell(cell) && !TryResolveNearbyWalkableCell(cell, 4, out cell))
            {
                continue;
            }

            if (walkableCells.Contains(cell) || !IsWalkableSpawnCell(cell))
            {
                continue;
            }

            walkableCells.Add(cell);
        }

        return walkableCells.ToArray();
    }


    private Vector3Int[] FilterChaseableRouteCells(Vector3Int[] routeCells)
    {
        if (routeCells == null || routeCells.Length == 0)
        {
            return System.Array.Empty<Vector3Int>();
        }

        List<Vector3Int> chaseableCells = new(routeCells.Length);

        for (int index = 0; index < routeCells.Length; index++)
        {
            Vector3Int cell = routeCells[index];

            if (!CanReachPlayerFrom(cell) && !TryResolveNearbyChaseableCell(cell, 4, out cell))
            {
                continue;
            }

            if (chaseableCells.Contains(cell) || !CanReachPlayerFrom(cell))
            {
                continue;
            }

            chaseableCells.Add(cell);
        }

        return chaseableCells.ToArray();
    }

    private bool TryResolveStalkerSpawnCell(out Vector3Int resolvedCell)
    {
        if (IsWalkableSpawnCell(stalkerSpawnCell))
        {
            resolvedCell = stalkerSpawnCell;
            return true;
        }

        if (TryResolveNearbyWalkableCell(stalkerSpawnCell, 5, out resolvedCell))
        {
            stalkerSpawnCell = resolvedCell;
            return true;
        }

        if (currentFloor != null && TryResolveNearbyWalkableCell(currentFloor.StalkerSpawnCell, 5, out resolvedCell))
        {
            stalkerSpawnCell = resolvedCell;
            return true;
        }

        if (layout != null && TryResolveNearbyWalkableCell(layout.PatrolSpawnCell, 4, out resolvedCell))
        {
            stalkerSpawnCell = resolvedCell;
            return true;
        }

        resolvedCell = Vector3Int.zero;
        return false;
    }

    private bool TryResolveNearbySentrySpawnCell(
        MainEscapeSentrySpawnPoint spawnPoint,
        HashSet<Vector3Int> usedCells,
        out Vector3Int resolvedCell)
    {
        Vector3 desiredWorldPosition = spawnPoint.WorldPosition;
        float bestDistance = float.MaxValue;
        Vector3Int bestCell = Vector3Int.zero;
        bool foundCandidate = false;
        int maxResolveRadius = 5;

        for (int radius = 1; radius <= maxResolveRadius; radius++)
        {
            foreach (Vector3Int candidate in EnumerateResolveRing(spawnPoint.Cell, radius))
            {
                if (!IsSentrySpawnCellAvailable(candidate, usedCells))
                {
                    continue;
                }

                float candidateDistance = (mapService.CellToWorldCenter(candidate) - desiredWorldPosition).sqrMagnitude;

                if (!foundCandidate || candidateDistance < bestDistance)
                {
                    bestDistance = candidateDistance;
                    bestCell = candidate;
                    foundCandidate = true;
                }
            }

            if (foundCandidate)
            {
                usedCells.Add(bestCell);
                resolvedCell = bestCell;
                return true;
            }
        }

        resolvedCell = Vector3Int.zero;
        return false;
    }

    private bool TryResolveNearbyWalkableCell(Vector3Int originCell, int maxRadius, out Vector3Int resolvedCell)
    {
        if (IsWalkableSpawnCell(originCell))
        {
            resolvedCell = originCell;
            return true;
        }

        for (int radius = 1; radius <= Mathf.Max(1, maxRadius); radius++)
        {
            foreach (Vector3Int candidate in EnumerateResolveRing(originCell, radius))
            {
                if (!IsWalkableSpawnCell(candidate))
                {
                    continue;
                }

                resolvedCell = candidate;
                return true;
            }
        }

        resolvedCell = Vector3Int.zero;
        return false;
    }

    private bool TryResolveNearbyChaseableCell(Vector3Int originCell, int maxRadius, out Vector3Int resolvedCell)
    {
        if (CanReachPlayerFrom(originCell))
        {
            resolvedCell = originCell;
            return true;
        }

        for (int radius = 1; radius <= Mathf.Max(1, maxRadius); radius++)
        {
            foreach (Vector3Int candidate in EnumerateResolveRing(originCell, radius))
            {
                if (!CanReachPlayerFrom(candidate))
                {
                    continue;
                }

                resolvedCell = candidate;
                return true;
            }
        }

        resolvedCell = Vector3Int.zero;
        return false;
    }

    private bool TryResolvePlayerReachableSpawnCell(
        int minRadius,
        int maxRadius,
        HashSet<Vector3Int> usedCells,
        out Vector3Int resolvedCell)
    {
        if (mapService == null || playerController == null)
        {
            resolvedCell = Vector3Int.zero;
            return false;
        }

        Vector3Int playerCell = mapService.WorldToCell(playerController.transform.position);
        int startRadius = Mathf.Max(1, minRadius);
        int endRadius = Mathf.Max(startRadius, maxRadius);

        for (int radius = startRadius; radius <= endRadius; radius++)
        {
            foreach (Vector3Int candidate in EnumerateResolveRing(playerCell, radius))
            {
                if ((usedCells != null && usedCells.Contains(candidate)) || !CanReachPlayerFrom(candidate))
                {
                    continue;
                }

                resolvedCell = candidate;
                return true;
            }
        }

        resolvedCell = Vector3Int.zero;
        return false;
    }

    private bool IsWalkableSpawnCell(Vector3Int cell)
    {
        return mapService != null && mapService.IsWalkable(cell, true);
    }

    private bool CanReachPlayerFrom(Vector3Int candidateCell)
    {
        if (!IsWalkableSpawnCell(candidateCell) || mapService == null || playerController == null)
        {
            return false;
        }

        List<Vector3Int> path = new();
        return TryBuildPathToPlayerNeighborhood(candidateCell, path, out _);
    }

    private static IEnumerable<Vector3Int> EnumerateResolveRing(Vector3Int originCell, int radius)
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

    private bool TryBuildPathToPlayerNeighborhood(Vector3Int startCell, List<Vector3Int> pathBuffer, out Vector3Int resolvedTargetCell)
    {
        resolvedTargetCell = Vector3Int.zero;

        if (mapService == null || playerController == null)
        {
            return false;
        }

        Vector3Int playerOriginCell = mapService.WorldToCell(playerController.transform.position);

        if (TryBuildPathToPlayerCandidate(startCell, playerOriginCell, pathBuffer))
        {
            resolvedTargetCell = playerOriginCell;
            return true;
        }

        for (int radius = 1; radius <= 4; radius++)
        {
            foreach (Vector3Int candidate in EnumerateResolveRing(playerOriginCell, radius))
            {
                if (!TryBuildPathToPlayerCandidate(startCell, candidate, pathBuffer))
                {
                    continue;
                }

                resolvedTargetCell = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryBuildPathToPlayerCandidate(Vector3Int startCell, Vector3Int candidateCell, List<Vector3Int> pathBuffer)
    {
        if (mapService == null || pathBuffer == null || !mapService.IsWalkable(candidateCell, true))
        {
            return false;
        }

        return GridPathfinder.TryBuildPath(mapService, startCell, candidateCell, pathBuffer, allowClosedDoors: true);
    }

    private Transform GetRuntimeEnemyRoot()
    {
        if (runtimeEnemyRoot != null && runtimeEnemyRoot.parent == enemyRoot)
        {
            return runtimeEnemyRoot;
        }

        runtimeEnemyRoot = null;

        if (enemyRoot == null)
        {
            return null;
        }

        runtimeEnemyRoot = enemyRoot.Find(RuntimeEnemyRootName);
        return runtimeEnemyRoot;
    }

    private Transform EnsureRuntimeEnemyRoot()
    {
        Transform existingRoot = GetRuntimeEnemyRoot();

        if (existingRoot != null)
        {
            return existingRoot;
        }

        if (enemyRoot == null)
        {
            return null;
        }

        GameObject runtimeRootObject = new GameObject(RuntimeEnemyRootName);
        runtimeRootObject.transform.SetParent(enemyRoot, false);
        runtimeRootObject.transform.localPosition = Vector3.zero;
        runtimeRootObject.transform.localRotation = Quaternion.identity;
        runtimeRootObject.transform.localScale = Vector3.one;
        runtimeEnemyRoot = runtimeRootObject.transform;
        return runtimeEnemyRoot;
    }

    private void ConfigureEnemySafety(EnemyStateMachine enemy)
    {
        if (enemy == null)
        {
            return;
        }

        PrototypeEnemyAudioDriver audioDriver = enemy.GetComponent<PrototypeEnemyAudioDriver>();

        if (audioDriver == null)
        {
            Debug.LogWarning($"{nameof(REncounterSpawner)} expected a PrototypeEnemyAudioDriver on '{enemy.name}', but none was found.", enemy);
        }
        else
        {
            audioDriver.Initialize(playerController);
        }

        InitializePassiveAmbientAudio(enemy.gameObject);
        InitializePlayerSpottedAudio(enemy.gameObject);

        if (IsFiveFloorVentOnlyMode)
        {
            EnemyVisionVisualizer[] visionVisualizers = enemy.GetComponentsInChildren<EnemyVisionVisualizer>(true);

            for (int index = 0; index < visionVisualizers.Length; index++)
            {
                if (visionVisualizers[index] != null)
                {
                    visionVisualizers[index].SetRevealOnlyWithPlayerFlashlight(false);
                }
            }
        }

        ApplyFogReactiveVisibility(enemy.gameObject);
    }

    private void InitializePassiveAmbientAudio(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return;
        }

        EnemyPassiveAmbientAudio passiveAmbientAudio = enemyObject.GetComponent<EnemyPassiveAmbientAudio>();

        if (passiveAmbientAudio != null)
        {
            passiveAmbientAudio.Initialize(playerController);
        }
    }

    private void InitializePlayerSpottedAudio(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return;
        }

        EnemyPlayerSpottedScreamAudio spottedAudio = enemyObject.GetComponent<EnemyPlayerSpottedScreamAudio>();

        if (spottedAudio != null)
        {
            spottedAudio.Initialize(playerController);
        }
    }

    private void ApplyFogReactiveVisibility(GameObject enemyObject)
    {
        MainEscapeEnemyVisibilityUtility.ApplyFogReactiveVisibility(
            enemyObject,
            fogOfWarOverlay,
            MinimumEnemySortingOrder);
    }

    private bool EnsurePlayerReachableGroundEnemy()
    {
        Transform runtimeRoot = GetRuntimeEnemyRoot();

        if (runtimeRoot == null || mapService == null || playerController == null)
        {
            return false;
        }

        EnemyStateMachine[] enemies = runtimeRoot.GetComponentsInChildren<EnemyStateMachine>(true);

        if (enemies == null || enemies.Length == 0)
        {
            return false;
        }

        EnemyStateMachine relocationCandidate = null;
        HashSet<Vector3Int> occupiedCells = new();

        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyStateMachine enemy = enemies[index];

            if (enemy == null)
            {
                continue;
            }

            Vector3Int enemyCell = mapService.ResolveNearestWalkableCell(enemy.transform.position, 1, true);

            if (CanReachPlayerFrom(enemyCell))
            {
                return true;
            }

            if (relocationCandidate == null || enemy.name.StartsWith("SentryGuard_"))
            {
                relocationCandidate = enemy;
            }

            occupiedCells.Add(enemyCell);
        }

        if (relocationCandidate == null)
        {
            return false;
        }

        occupiedCells.Remove(mapService.ResolveNearestWalkableCell(relocationCandidate.transform.position, 1, true));

        if (!TryResolvePlayerReachableSpawnCell(2, 8, occupiedCells, out Vector3Int resolvedCell)
            && !TryResolvePlayerNeighborhoodCell(2, 8, occupiedCells, out resolvedCell))
        {
            return false;
        }

        Vector3 worldCenter = mapService.CellToWorldCenter(resolvedCell);
        relocationCandidate.transform.position = new Vector3(worldCenter.x, worldCenter.y, relocationCandidate.transform.position.z);
        Physics2D.SyncTransforms();
        Debug.LogWarning(
            $"{nameof(REncounterSpawner)} relocated '{relocationCandidate.name}' to reachable cell {resolvedCell} so at least one ground enemy can chase from the current authored map state.",
            relocationCandidate);
        return true;
    }

    private bool TryResolvePlayerNeighborhoodCell(
        int minRadius,
        int maxRadius,
        HashSet<Vector3Int> usedCells,
        out Vector3Int resolvedCell)
    {
        resolvedCell = Vector3Int.zero;

        if (mapService == null || playerController == null)
        {
            return false;
        }

        Vector3Int playerCell = mapService.WorldToCell(playerController.transform.position);
        int startRadius = Mathf.Max(1, minRadius);
        int endRadius = Mathf.Max(startRadius, maxRadius);

        for (int radius = startRadius; radius <= endRadius; radius++)
        {
            foreach (Vector3Int candidate in EnumerateResolveRing(playerCell, radius))
            {
                if ((usedCells != null && usedCells.Contains(candidate)) || !IsWalkableSpawnCell(candidate))
                {
                    continue;
                }

                resolvedCell = candidate;
                return true;
            }
        }

        return false;
    }

    private void ValidateBindings()
    {
        ventNetworkDebugOverlay ??= GetComponent<MainEscapeVentNetworkDebugOverlay>();

        if (fogOfWarOverlay == null)
        {
            Debug.LogWarning($"{nameof(REncounterSpawner)} has no fog of war overlay assigned.", this);
        }

        if (enemyRoot == null)
        {
            Debug.LogWarning($"{nameof(REncounterSpawner)} has no authored enemy root assigned.", this);
        }
    }

    private MainEscapeVentNetworkDebugOverlay GetOrCreateVentNetworkDebugOverlay()
    {
        ventNetworkDebugOverlay ??= GetComponent<MainEscapeVentNetworkDebugOverlay>();

        if (ventNetworkDebugOverlay == null)
        {
            ventNetworkDebugOverlay = gameObject.AddComponent<MainEscapeVentNetworkDebugOverlay>();
        }

        return ventNetworkDebugOverlay;
    }
}

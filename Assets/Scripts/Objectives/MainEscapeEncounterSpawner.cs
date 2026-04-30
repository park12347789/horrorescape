using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeEncounterSpawner : MonoBehaviour
{
    private static MainEscapeRuntimeSettings RuntimeSettings => MainEscapeRuntimeSettings.Load();

    [SerializeField, Min(0f)] private float stalkerSpawnDelay = 12f;

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private MainEscapePlayerRuntimeReferences playerRuntime;
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private bool showVentMarkers;
    [SerializeField] private GridMapService mapService;
    [SerializeField] private GeneratedFloorLayout layout;
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private MainEscapeVentNetworkDebugOverlay ventNetworkDebugOverlay;

    private EscapeFloorDefinition currentFloor;
    private Vector3Int[] patrolRouteCells = System.Array.Empty<Vector3Int>();
    private Vector3Int[] patrolSpawnCells = System.Array.Empty<Vector3Int>();
    private MainEscapeSentrySpawnPoint[] sentrySpawns = System.Array.Empty<MainEscapeSentrySpawnPoint>();
    private MainEscapeVentRouteDefinition ventRoute = MainEscapeVentRouteDefinition.Empty;
    private Vector3Int stalkerSpawnCell;
    private VisibilityTarget2D playerTarget;
    private PlayerTrailRecorder playerTrail;
    private MainEscapeRuntimePrefabCatalog runtimePrefabCatalog;
    private float stalkerSpawnTime;
    private bool stalkerSpawned;

    public bool ShowVentMarkers => showVentMarkers;

    public void Initialize(WasdPlayerController player, FlashlightFogOfWarOverlay overlay = null, bool enableVentMarkers = false)
    {
        playerController = player;
        playerRuntime = player != null ? MainEscapePlayerRuntimeReferences.Resolve(player) : null;
        fogOfWarOverlay = overlay;
        showVentMarkers = enableVentMarkers;
        EnsurePlayerBindings();
    }

    public void SetVentMarkersVisible(bool visible)
    {
        showVentMarkers = visible;
        GetOrCreateVentNetworkDebugOverlay().Configure(layout, mapService, ventRoute, visible);
    }

    public void ConfigureFloor(
        WasdPlayerController player,
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
        playerController = player;
        playerRuntime = player != null ? MainEscapePlayerRuntimeReferences.Resolve(player) : null;
        mapService = configuredMapService;
        layout = configuredLayout;
        currentFloor = floorDefinition;
        patrolRouteCells = configuredPatrolRoute ?? floorDefinition?.PatrolRoute ?? System.Array.Empty<Vector3Int>();
        patrolSpawnCells = configuredPatrolSpawnCells ?? System.Array.Empty<Vector3Int>();
        sentrySpawns = configuredSentrySpawns ?? System.Array.Empty<MainEscapeSentrySpawnPoint>();
        ventRoute = configuredVentRoute.IsValid ? configuredVentRoute : MainEscapeVentRouteDefinition.Empty;
        stalkerSpawnCell = configuredStalkerSpawnCell != default
            ? configuredStalkerSpawnCell
            : floorDefinition != null
                ? floorDefinition.StalkerSpawnCell
                : Vector3Int.zero;
        runtimePrefabCatalog = configuredRuntimePrefabCatalog != null
            ? configuredRuntimePrefabCatalog
            : MainEscapeRuntimePrefabCatalog.LoadForScene(gameObject.scene);
        EnsurePlayerBindings();
        ClearFloor();

        if (mapService == null || layout == null || currentFloor == null || playerTarget == null)
        {
            return;
        }

        enemyRoot = new GameObject($"MainEscapeEnemies_{currentFloor.FloorNumber}F").transform;
        enemyRoot.SetParent(transform, false);
        GetOrCreateVentNetworkDebugOverlay().Configure(layout, mapService, ventRoute, showVentMarkers);

        if (playerTrail != null)
        {
            playerTrail.Configure(mapService);
        }

        SpawnPatrolGuards();
        SpawnStationarySentries();
        SpawnVentEnemy();
        stalkerSpawned = false;
        stalkerSpawnTime = Time.time + stalkerSpawnDelay;
    }

    public void ClearFloor()
    {
        ventNetworkDebugOverlay?.Clear();

        if (enemyRoot != null)
        {
            enemyRoot.gameObject.SetActive(false);
            Destroy(enemyRoot.gameObject);
        }

        enemyRoot = null;
        stalkerSpawned = false;
        stalkerSpawnTime = 0f;
    }

    private void Update()
    {
        if (stalkerSpawned
            || currentFloor == null
            || mapService == null
            || playerTarget == null
            || enemyRoot == null
            || Time.time < stalkerSpawnTime)
        {
            return;
        }

        SpawnStalker();
        stalkerSpawned = true;
    }

    private void EnsurePlayerBindings()
    {
        if (playerController == null)
        {
            return;
        }

        playerTarget = EnemyRuntimeFactory.EnsurePlayerTarget(playerController);
        playerRuntime ??= MainEscapePlayerRuntimeReferences.Resolve(playerController);
        playerRuntime?.EnsureRuntimeComponents();
        playerTrail = playerRuntime != null ? playerRuntime.TrailRecorder : null;

        if (playerTrail != null && mapService != null)
        {
            playerTrail.Configure(mapService);
        }
    }

    private void SpawnPatrolGuards()
    {
        if (patrolSpawnCells != null && patrolSpawnCells.Length > 0)
        {
            EnemyArchetype patrolSpawnArchetype = CreatePatrolArchetype();

            for (int index = 0; index < patrolSpawnCells.Length; index++)
            {
                Vector3Int patrolSpawnCell = patrolSpawnCells[index];
                string enemyName = patrolSpawnCells.Length > 1
                    ? $"PatrolGuard_{index + 1}"
                    : "PatrolGuard";

                EnemyStateMachine spawnedPatrol = EnemyRuntimeFactory.CreateEnemy(
                    enemyRoot,
                    enemyName,
                    mapService.CellToWorldCenter(patrolSpawnCell),
                    mapService,
                    playerTarget,
                    patrolSpawnArchetype,
                    null,
                    runtimePrefabCatalog: runtimePrefabCatalog,
                    patrolSpawnCell);

                ConfigurePatrol(spawnedPatrol);
            }

            return;
        }

        if ((patrolRouteCells == null || patrolRouteCells.Length == 0)
            && layout != null
            && mapService != null
            && mapService.IsWalkable(layout.PatrolSpawnCell))
        {
            patrolRouteCells = new[] { layout.PatrolSpawnCell };
        }

        if (patrolRouteCells == null || patrolRouteCells.Length == 0)
        {
            return;
        }

        EnemyArchetype archetype = CreatePatrolArchetype();

        EnemyStateMachine patrol = EnemyRuntimeFactory.CreateEnemy(
            enemyRoot,
            "PatrolGuard",
            mapService.CellToWorldCenter(patrolRouteCells[0]),
            mapService,
            playerTarget,
            archetype,
            null,
            runtimePrefabCatalog: runtimePrefabCatalog,
            patrolRouteCells);

        ConfigurePatrol(patrol);
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

    private void ConfigurePatrol(EnemyStateMachine patrol)
    {
        if (patrol == null)
        {
            return;
        }

        PrototypeEnemyAudioDriver audioDriver = patrol.GetComponent<PrototypeEnemyAudioDriver>();

        if (audioDriver == null)
        {
            audioDriver = patrol.gameObject.AddComponent<PrototypeEnemyAudioDriver>();
        }

        audioDriver.Initialize(playerController);
        InitializePassiveAmbientAudio(patrol.gameObject);
        InitializePlayerSpottedAudio(patrol.gameObject);
        ApplyFogReactiveVisibility(patrol.gameObject);
    }

    private void SpawnStalker()
    {
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

        EnemyStateMachine stalker = EnemyRuntimeFactory.CreateEnemy(
            enemyRoot,
            "Stalker",
            mapService.CellToWorldCenter(stalkerSpawnCell),
            mapService,
            playerTarget,
            archetype,
            playerTrail,
            runtimePrefabCatalog: runtimePrefabCatalog);

        if (stalker != null)
        {
            PrototypeEnemyAudioDriver audioDriver = stalker.GetComponent<PrototypeEnemyAudioDriver>();

            if (audioDriver == null)
            {
                audioDriver = stalker.gameObject.AddComponent<PrototypeEnemyAudioDriver>();
            }

            audioDriver.Initialize(playerController);
            InitializePassiveAmbientAudio(stalker.gameObject);
            InitializePlayerSpottedAudio(stalker.gameObject);
            ApplyFogReactiveVisibility(stalker.gameObject);
        }
    }

    private void SpawnStationarySentries()
    {
        if (sentrySpawns == null
            || sentrySpawns.Length == 0
            || enemyRoot == null
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
                enemyRoot,
                $"SentryGuard_{index + 1:00}",
                mapService.CellToWorldCenter(spawnCell),
                mapService,
                playerTarget,
                archetype,
                null,
                spawnPoint.Facing,
                runtimePrefabCatalog: runtimePrefabCatalog);

            if (sentry == null)
            {
                continue;
            }

            PrototypeEnemyAudioDriver audioDriver = sentry.GetComponent<PrototypeEnemyAudioDriver>();

            if (audioDriver == null)
            {
                audioDriver = sentry.gameObject.AddComponent<PrototypeEnemyAudioDriver>();
            }

            audioDriver.Initialize(playerController);
            InitializePassiveAmbientAudio(sentry.gameObject);
            InitializePlayerSpottedAudio(sentry.gameObject);
            ApplyFogReactiveVisibility(sentry.gameObject);
        }
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

        Vector3 desiredWorldPosition = spawnPoint.WorldPosition;
        float bestDistance = float.MaxValue;
        Vector3Int bestCell = Vector3Int.zero;
        bool foundCandidate = false;
        int maxResolveRadius = Mathf.Max(1, RuntimeSettings.SentrySpawnResolveRadius);

        for (int radius = 1; radius <= maxResolveRadius; radius++)
        {
            foreach (Vector3Int candidate in EnumerateSentryResolveRing(spawnPoint.Cell, radius))
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
        Debug.LogWarning(
            $"[MainEscapeEncounterSpawner] Skipped authored sentry '{spawnPoint.MarkerName}' because no walkable cell was available near {spawnPoint.Cell}.");
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

    private static IEnumerable<Vector3Int> EnumerateSentryResolveRing(Vector3Int originCell, int radius)
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

    private void SpawnVentEnemy()
    {
        CeilingVentEnemyController controller = BaseOfficeVentEnemyBootstrap.CreateRuntimeEnemy(
            enemyRoot,
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

        controller.ConfigureRuntimeBehavior(
            shouldRespondToNoise: true,
            shouldFollowPlayerInVentNetwork: true,
            followPlayerRetargetInterval: 0.18f,
            shouldAllowEmergence: true);
        controller.BindPlayerTrail(playerTrail);
        controller.ConfigureBehaviorProfile(ResolveVentBehaviorProfile());

        VentEnemyAudioDriver audioDriver = controller.GetComponent<VentEnemyAudioDriver>();

        if (audioDriver == null)
        {
            audioDriver = controller.gameObject.AddComponent<VentEnemyAudioDriver>();
        }

        audioDriver.Initialize(playerController);
        InitializePassiveAmbientAudio(controller.gameObject);
        InitializePlayerSpottedAudio(controller.gameObject);
        ApplyFogReactiveVisibility(controller.gameObject);
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
        MainEscapeEnemyVisibilityUtility.ApplyFogReactiveVisibility(enemyObject, fogOfWarOverlay);
    }
}

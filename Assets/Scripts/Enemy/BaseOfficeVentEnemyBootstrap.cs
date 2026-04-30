/*
 * File Role:
 * Controls the hidden ceiling vent enemy used in the BaseOffice scene.
 *
 * Runtime Use:
 * Builds invisible vent nodes, reacts to noise, emerges from vents, and retreats back into the ceiling.
 *
 * Study Notes:
 * Read this file together with NoiseSystem, GeneratedFloorLayout, and VisionSensor2D to follow the full behavior loop.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class BaseOfficeVentEnemyBootstrap : MonoBehaviour
{
    public static CeilingVentEnemyController CreateRuntimeEnemy(
        Transform parent,
        GeneratedFloorLayout layout,
        GridMapService mapService,
        VisibilityTarget2D playerTarget,
        bool showVentMarkers,
        MainEscapeVentRouteDefinition customVentRoute = default,
        INoiseEventBus configuredNoiseEventBus = null,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog = null)
    {
        Scene owningScene = parent != null && parent.gameObject.scene.IsValid()
            ? parent.gameObject.scene
            : layout != null
                ? layout.gameObject.scene
                : default;
        INoiseEventBus eventBus = configuredNoiseEventBus ?? NoiseEventBusResolver.Resolve(owningScene);

        if (!Application.isPlaying
            || layout == null
            || mapService == null
            || playerTarget == null
            || eventBus == null)
        {
            return null;
        }

        CeilingVentEnemyController controller = TryInstantiateRuntimeEnemyPrefab(parent, layout, runtimePrefabCatalog);

        if (controller == null)
        {
            Debug.LogError(
                $"Vent enemy runtime fallback creation is disabled. Assign a valid vent enemy prefab in {nameof(MainEscapeRuntimePrefabCatalog)} before entering the clean loop.",
                layout);
            return null;
        }

        controller.ConfigureNoiseEventBus(eventBus);
        controller.Configure(layout, mapService, playerTarget, showVentMarkers, customVentRoute);
        return controller;
    }

    private static CeilingVentEnemyController TryInstantiateRuntimeEnemyPrefab(
        Transform parent,
        GeneratedFloorLayout layout,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog)
    {
        MainEscapeRuntimePrefabCatalog catalog = ResolveRuntimePrefabCatalog(parent, layout, runtimePrefabCatalog);
        CeilingVentEnemyPrefabBindings enemyPrefab = catalog != null ? catalog.VentEnemyPrefab : null;

        if (enemyPrefab == null)
        {
            return null;
        }

        CeilingVentEnemyPrefabBindings instance = UnityEngine.Object.Instantiate(
            enemyPrefab,
            parent != null ? parent : layout.transform);
        instance.name = "CeilingVentEnemy";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        CeilingVentEnemyController controller = instance.Controller != null
            ? instance.Controller
            : instance.GetComponent<CeilingVentEnemyController>();

        if (controller == null)
        {
            controller = instance.gameObject.AddComponent<CeilingVentEnemyController>();
        }

        instance.AutoAssign();
        return controller;
    }

    private static MainEscapeRuntimePrefabCatalog ResolveRuntimePrefabCatalog(
        Transform parent,
        GeneratedFloorLayout layout,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog)
    {
        if (runtimePrefabCatalog != null)
        {
            return runtimePrefabCatalog;
        }

        if (parent != null && parent.gameObject.scene.IsValid())
        {
            return MainEscapeRuntimePrefabCatalog.LoadForScene(parent.gameObject.scene);
        }

        return layout != null && layout.gameObject.scene.IsValid()
            ? MainEscapeRuntimePrefabCatalog.LoadForScene(layout.gameObject.scene)
            : MainEscapeRuntimePrefabCatalog.LoadDefault();
    }

}

[DisallowMultipleComponent]
public sealed class CeilingVentEnemyController : MonoBehaviour, IPlayerThreatFeedbackSource, IThrowableStunTarget, IEnemyPlayerSpotSource, IEnemyPassiveAudioStateSource
{
    public readonly struct VentBehaviorProfile
    {
        public VentBehaviorProfile(
            float crawlMoveSpeed,
            float ambientRoamDelay,
            float noiseRetargetCooldown,
            float lowPriorityNoiseEmergeDelay,
            float emergeLookDuration,
            float playerFollowRetargetInterval,
            int playerTrailPredictionSamples,
            int playerTrailInterceptSamples,
            int playerTrailClosestSearchWindow,
            float playerFollowTargetCommitDuration)
        {
            CrawlMoveSpeed = crawlMoveSpeed;
            AmbientRoamDelay = ambientRoamDelay;
            NoiseRetargetCooldown = noiseRetargetCooldown;
            LowPriorityNoiseEmergeDelay = lowPriorityNoiseEmergeDelay;
            EmergeLookDuration = emergeLookDuration;
            PlayerFollowRetargetInterval = playerFollowRetargetInterval;
            PlayerTrailPredictionSamples = playerTrailPredictionSamples;
            PlayerTrailInterceptSamples = playerTrailInterceptSamples;
            PlayerTrailClosestSearchWindow = playerTrailClosestSearchWindow;
            PlayerFollowTargetCommitDuration = playerFollowTargetCommitDuration;
        }

        public float CrawlMoveSpeed { get; }
        public float AmbientRoamDelay { get; }
        public float NoiseRetargetCooldown { get; }
        public float LowPriorityNoiseEmergeDelay { get; }
        public float EmergeLookDuration { get; }
        public float PlayerFollowRetargetInterval { get; }
        public int PlayerTrailPredictionSamples { get; }
        public int PlayerTrailInterceptSamples { get; }
        public int PlayerTrailClosestSearchWindow { get; }
        public float PlayerFollowTargetCommitDuration { get; }
    }

    private const string VentProfileResourcePath = "MainEscape/EnemyArt/VentEnemy_Venter";

    // This controller simulates an enemy that mainly exists in a hidden vent graph and
    // only briefly enters the visible world.
    private enum VentEnemyState
    {
        Crawling,
        Emerged
    }

    private enum VentTravelIntent
    {
        None,
        NoiseResponse,
        AmbientRoam
    }

    private readonly struct VentNode
    {
        public VentNode(int nodeId, Vector3Int cell, bool isCorridor, int roomId, bool allowsSurfaceAccess)
        {
            NodeId = nodeId;
            Cell = cell;
            IsCorridor = isCorridor;
            RoomId = roomId;
            AllowsSurfaceAccess = allowsSurfaceAccess;
        }

        public int NodeId { get; }
        public Vector3Int Cell { get; }
        public bool IsCorridor { get; }
        public int RoomId { get; }
        public bool AllowsSurfaceAccess { get; }
    }

    private readonly struct NodeDistance
    {
        public NodeDistance(int nodeId, float distance)
        {
            NodeId = nodeId;
            Distance = distance;
        }

        public int NodeId { get; }
        public float Distance { get; }
    }

    [SerializeField] private GeneratedFloorLayout layout;
    [SerializeField] private GridMapService mapService;
    [SerializeField] private VisibilityTarget2D playerTarget;
    [SerializeField] private PlayerTrailRecorder playerTrail;
    [SerializeField] private bool showVentMarkers = true;
    [SerializeField] private bool enableDebugLogs;
    [SerializeField] private Color recognizedGlowColor = new(1f, 0.1f, 0.04f, 1f);
    [SerializeField, Min(1f)] private float recognizedGlowScaleMultiplier = 3.2f;
    [SerializeField, Min(0f)] private float recognizedGlowPulseSpeed = 8.9f;
    [SerializeField, Range(0f, 1f)] private float recognizedGlowPulseStrength = 0.5f;
    [SerializeField] private int recognizedGlowSortingOrder = 152;
    [SerializeField, Min(1f)] private float crawlMoveSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float crawlPulseInterval = 0.65f;
    [SerializeField, Min(0.25f)] private float crawlPulseRadius = 3.4f;
    [SerializeField, Min(0.05f)] private float noiseRetargetCooldown = 0.7f;
    [SerializeField, Min(0.1f)] private float ambientRoamDelay = 0.9f;
    [SerializeField, Min(0f)] private float lowPriorityNoiseEmergeDelay = 0.45f;
    [SerializeField, Min(0.5f)] private float emergeLookDuration = 2.8f;
    [SerializeField, Min(0.1f)] private float emergeSeenHoldDuration = 1.4f;
    [SerializeField, Min(1f)] private float emergeVisionDistance = 8.5f;
    [SerializeField, Min(0.5f)] private float emergeChaseSpeed = 3.2f;
    [SerializeField, Min(0.1f)] private float emergeCaptureDistance = 0.55f;
    [SerializeField, Min(0f)] private float attackRecoveryDuration = 0.75f;
    [SerializeField, Min(0.5f)] private float inactivityRecoveryTimeout = 3.75f;
    [SerializeField, Range(10f, 180f)] private float emergeVisionAngle = 70f;
    [SerializeField, Range(10f, 180f)] private float scanSweepAngle = 80f;
    [SerializeField, Min(0.2f)] private float scanCyclesPerSecond = 0.65f;
    [SerializeField] private bool useExposureForDetection = true;
    [SerializeField, Min(0f)] private float threatFeedbackHoldDuration = 1f;
    [SerializeField, Min(0.05f)] private float emergedDetectionBuildDuration = 0.4f;
    [SerializeField, Min(0.05f)] private float emergedDetectionDecayDuration = 0.6f;
    [SerializeField, Range(0.1f, 1f)] private float confirmedDetectionThreshold = 0.95f;
    [SerializeField] private bool respondToNoise = true;
    [SerializeField] private NoiseSystem noiseSystem;
    [SerializeField] private bool followPlayerInVentNetwork;
    [SerializeField] private bool allowEmergence = true;
    [SerializeField, Min(0f)] private float initialPlayerFollowGraceDuration = 1.75f;
    [SerializeField, Min(0f)] private float initialPlayerFollowSearchDuration = 4f;
    [SerializeField, Min(0.02f)] private float playerFollowRetargetInterval = 0.32f;
    [SerializeField, Min(0)] private int playerTrailPredictionSamples = 5;
    [SerializeField, Min(0)] private int playerTrailInterceptSamples = 9;
    [SerializeField, Min(0)] private int playerTrailClosestSearchWindow = 18;
    [SerializeField, Min(0.05f)] private float playerFollowTargetCommitDuration = 0.9f;
    [Header("Randomized Patterns")]
    [SerializeField, Range(0f, 1f)] private float randomImmediateNoiseEmergeChance = 0.16f;
    [SerializeField, Range(0f, 1f)] private float playerFollowAmbientInterferenceChance = 0.14f;
    [SerializeField, Range(0f, 1f)] private float earlyFakeRetreatChance = 0.22f;
    [SerializeField, Min(0.1f)] private float earlyFakeRetreatDelayMin = 0.6f;
    [SerializeField, Min(0.1f)] private float earlyFakeRetreatDelayMax = 1.35f;
    [SerializeField, Range(0f, 1f)] private float irregularVentRattleChance = 0.48f;
    [SerializeField, Min(0.1f)] private float irregularVentRattleIntervalMin = 1.35f;
    [SerializeField, Min(0.1f)] private float irregularVentRattleIntervalMax = 3.1f;
    [SerializeField, Range(0.1f, 2f)] private float irregularVentRattleRadiusMinMultiplier = 0.6f;
    [SerializeField, Range(0.1f, 2f)] private float irregularVentRattleRadiusMaxMultiplier = 1.2f;

    private readonly List<VentNode> nodes = new();
    private readonly List<int> currentNodePath = new();
    private readonly List<int> rebuiltNodePathScratch = new();
    private readonly Queue<int> nodePathFrontier = new();
    private readonly Dictionary<int, int> previousByNodeScratch = new();
    private readonly Queue<int> fallbackNodeFrontier = new();
    private readonly HashSet<int> fallbackVisitedNodes = new();
    private readonly List<Vector3Int> emergedGroundPath = new();
    private readonly List<Vector3Int> emergedPathProbeScratch = new();
    private readonly Dictionary<int, List<int>> adjacencyByNode = new();
    private readonly Dictionary<int, List<int>> roomNodeIdsByRoom = new();
    private readonly HashSet<Vector3Int> usedCells = new();
    private static Sprite sharedSprite;
    private static Material sharedDebugLineMaterial;
    private float lastCrawlPulseTime = float.NegativeInfinity;
    private float emergedUntilTime;
    private float emergedStartTime;
    private float nextAmbientRoamTime;
    private float nextEmergedRepathTime;
    private int currentNodeId = -1;
    private int currentPathIndex;
    private int instanceId;
    private System.Random random;
    private VentEnemyState currentState;
    private VentTravelIntent currentTravelIntent;
    private float emergedDetectionMeter;
    private float threatFeedbackUntil = float.NegativeInfinity;
    private Vector3 facingMarkerBaseScale = Vector3.one;
    private int facingMarkerBaseSortingOrder;
    private bool hasFacingMarkerDefaults;
    private Vector2 facingDirection = Vector2.up;
    private Vector2 emergeBaseFacing = Vector2.up;
    private Vector2 lastNoisePosition;
    private VisionSensor2D visionSensor;
    private Transform visualRoot;
    private Transform ventMarkerRoot;
    private Transform ventConnectionRoot;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer facingMarkerRenderer;
    private EnemyVisionVisualizer visionVisualizer;
    private VentEnemyAudioDriver audioDriver;
    private VentEnemySpriteAnimationDriver animationDriver;
    private CircleCollider2D hitbox;
    private EnemyDisruptionController disruptionController;
    private EnemyPlayerInteractionController playerInteractionController;
    private EnemySightMemoryController sightMemoryController;
    private EnemyNoiseListenerController noiseListenerController;
    private INoiseEventBus noiseEventBus;
    private EnemyNoiseRetargetController noiseRetargetController;
    private EnemyActivityWatchdogController activityWatchdog;
    private MainEscapeVentRouteDefinition customVentRoute;
    private float nextPlayerFollowRetargetTime;
    private float playerFollowStartupGraceUntilTime = float.NegativeInfinity;
    private float playerFollowSearchUntilTime = float.NegativeInfinity;
    private float committedFollowNodeUntilTime = float.NegativeInfinity;
    private float scheduledEarlyFakeRetreatTime = float.PositiveInfinity;
    private float nextIrregularVentRattleTime = float.PositiveInfinity;
    private bool pendingNoiseEmergence;
    private float pendingNoiseEmergenceTime = float.PositiveInfinity;
    private string pendingNoiseEmergenceReason;
    private int committedFollowNodeId = -1;

    public event Action PlayerSpotted;

    public bool VentMarkersVisible => showVentMarkers;
    public bool IsEmerged => currentState == VentEnemyState.Emerged;
    public bool ShouldPlayPassiveAmbientAudio => !IsStunned && currentState == VentEnemyState.Crawling;
    public bool IsConfirmedThreat => !IsStunned
        && Time.time <= threatFeedbackUntil;
    public bool IsActivelyPursuingPlayer => !IsStunned
        && currentState == VentEnemyState.Emerged
        && (HasLastSeenPlayerTarget() || Time.time <= threatFeedbackUntil);
    public bool ShouldForceThreatFeedbackVisible => !IsStunned
        && currentState == VentEnemyState.Emerged
        && (Time.time <= threatFeedbackUntil || emergedDetectionMeter >= 0.56f);
    public float ThreatIntensityNormalized => !IsStunned && currentState == VentEnemyState.Emerged
        ? Mathf.Clamp01(IsConfirmedThreat ? Mathf.Max(0.8f, emergedDetectionMeter) : 0f)
        : 0f;
    public Vector3 ThreatWorldPosition => transform.position;
    public SpriteRenderer ThreatMarkerRenderer => facingMarkerRenderer;
    public bool CanBeStunnedByThrowable => isActiveAndEnabled && currentState == VentEnemyState.Emerged && !IsStunned;
    public Vector3 ThrowableStunAimPoint => ResolveThrowableTargetBounds().center;
    public float ThrowableStunHitRadius => ResolveThrowableTargetRadius();
    public bool IsRecoveringFromAttack => disruptionController != null && disruptionController.IsAttackRecovering;

    public void Configure(
        GeneratedFloorLayout configuredLayout,
        GridMapService configuredMapService,
        VisibilityTarget2D configuredPlayerTarget,
        bool configureShowVentMarkers = true,
        MainEscapeVentRouteDefinition configuredVentRoute = default)
    {
        // Configure seeds the hidden vent network from the already generated office map.
        layout = configuredLayout;
        mapService = configuredMapService;
        playerTarget = configuredPlayerTarget;
        showVentMarkers = configureShowVentMarkers;
        customVentRoute = configuredVentRoute.IsValid ? configuredVentRoute : MainEscapeVentRouteDefinition.Empty;
        instanceId = gameObject.GetInstanceID();

        int seed = Environment.TickCount;

        if (layout != null)
        {
            seed ^= layout.Rooms.Length * 397;
            seed ^= layout.Routes.Length * 971;
            seed ^= layout.PlayerStartCell.x * 53;
            seed ^= layout.PlayerStartCell.y * 89;
        }

        random = new System.Random(seed);
        EnsureVisuals();
        CacheFacingMarkerDefaults();
        ConfigureDisruptionController();
        ConfigurePlayerInteractionController();
        ConfigureSightMemoryController();
        ConfigureNoiseListenerController();
        ConfigureNoiseRetargetController();
        ConfigureActivityWatchdog();
        audioDriver = GetComponent<VentEnemyAudioDriver>();
        EnsureAnimationDriver();
        BuildVentGraph();
        RebuildVentMarkers();

        if (nodes.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(CeilingVentEnemyController)} could not build any usable vent nodes. Check the authored VentRoute markers, walkable cell alignment, and explicit vent links for this scene.",
                this);
            enabled = false;
            return;
        }

        currentNodeId = FindFarthestNodeFrom(layout.PlayerStartCell);
        transform.position = mapService.CellToWorldCenter(nodes[currentNodeId].Cell);
        currentState = VentEnemyState.Crawling;
        currentTravelIntent = VentTravelIntent.None;
        emergedDetectionMeter = 0f;
        threatFeedbackUntil = float.NegativeInfinity;
        ClearPlayerFollowOpeningWindow();
        ClearCommittedFollowTarget();
        ClearPendingNoiseEmergence();
        sightMemoryController?.ClearLastSeenTarget();
        ClearNoiseResponseTarget();
        SetVisualsVisible(false);
        lastCrawlPulseTime = Time.time;
        scheduledEarlyFakeRetreatTime = float.PositiveInfinity;
        nextIrregularVentRattleTime = Time.time + ResolveIrregularVentRattleDelay();
        nextAmbientRoamTime = Time.time + ambientRoamDelay;
        LogDebug($"Initialized at {DescribeNode(currentNodeId)} with {nodes.Count} vent nodes.");
    }

    private void OnEnable()
    {
        PlayerThreatFeedbackRegistry.Register(this);
        ThrowableStunTargetRegistry.Register(this);
    }

    private void OnDisable()
    {
        PlayerThreatFeedbackRegistry.Unregister(this);
        ThrowableStunTargetRegistry.Unregister(this);
    }

    private void OnDestroy()
    {
        PlayerThreatFeedbackRegistry.Unregister(this);
        ThrowableStunTargetRegistry.Unregister(this);
    }

    public bool TryApplyThrowableStun(float duration)
    {
        return TryApplyStun(duration);
    }

    private Bounds ResolveThrowableTargetBounds()
    {
        Bounds bounds = new(transform.position, Vector3.zero);
        bool hasBounds = false;

        if (hitbox != null && hitbox.enabled)
        {
            bounds = hitbox.bounds;
            hasBounds = true;
        }

        if (bodyRenderer != null && bodyRenderer.enabled)
        {
            if (hasBounds)
            {
                bounds.Encapsulate(bodyRenderer.bounds);
            }
            else
            {
                bounds = bodyRenderer.bounds;
                hasBounds = true;
            }
        }

        return hasBounds ? bounds : new Bounds(transform.position, new Vector3(0.84f, 0.84f, 0.01f));
    }

    private float ResolveThrowableTargetRadius()
    {
        Bounds bounds = ResolveThrowableTargetBounds();
        Vector3 extents = bounds.extents;
        float radius = Mathf.Max(extents.x, extents.y);
        return Mathf.Max(0.46f, radius + 0.08f);
    }

    public void SetVentMarkersVisible(bool visible)
    {
        showVentMarkers = visible;
        RebuildVentMarkers();
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
        noiseListenerController?.ConfigureNoiseEventBus(configuredNoiseEventBus);
    }

    public void ConfigureRuntimeBehavior(
        bool shouldRespondToNoise,
        bool shouldFollowPlayerInVentNetwork,
        float followPlayerRetargetInterval = 0.32f,
        bool shouldAllowEmergence = true)
    {
        respondToNoise = shouldRespondToNoise;
        followPlayerInVentNetwork = shouldFollowPlayerInVentNetwork;
        allowEmergence = shouldAllowEmergence;
        playerFollowRetargetInterval = Mathf.Max(0.02f, followPlayerRetargetInterval);

        if (followPlayerInVentNetwork)
        {
            ResetPlayerFollowOpeningWindow();
        }
        else
        {
            ClearPlayerFollowOpeningWindow();
            nextPlayerFollowRetargetTime = Time.time;
        }

        if (!allowEmergence && currentState == VentEnemyState.Emerged)
        {
            BeginHiddenRetreat("runtime ambient-only mode");
            return;
        }

        if (!followPlayerInVentNetwork || currentState != VentEnemyState.Crawling)
        {
            ClearCommittedFollowTarget();
            return;
        }

        ClearCommittedFollowTarget();
        ClearPendingNoiseEmergence();
        currentNodePath.Clear();
        currentPathIndex = 0;
        currentTravelIntent = VentTravelIntent.None;
        ClearNoiseResponseTarget();

        if (!IsPlayerFollowStartupGraceActive())
        {
            TryUpdatePlayerVentFollow(forceRepath: true);
        }
    }

    public void BindPlayerTrail(PlayerTrailRecorder configuredPlayerTrail)
    {
        playerTrail = configuredPlayerTrail;
    }

    public void ConfigureBehaviorProfile(VentBehaviorProfile profile)
    {
        crawlMoveSpeed = Mathf.Max(1f, profile.CrawlMoveSpeed);
        ambientRoamDelay = Mathf.Max(0.1f, profile.AmbientRoamDelay);
        noiseRetargetCooldown = Mathf.Max(0.05f, profile.NoiseRetargetCooldown);
        lowPriorityNoiseEmergeDelay = Mathf.Max(0f, profile.LowPriorityNoiseEmergeDelay);
        emergeLookDuration = Mathf.Max(0.5f, profile.EmergeLookDuration);
        playerFollowRetargetInterval = Mathf.Max(0.02f, profile.PlayerFollowRetargetInterval);
        playerTrailPredictionSamples = Mathf.Max(0, profile.PlayerTrailPredictionSamples);
        playerTrailInterceptSamples = Mathf.Max(0, profile.PlayerTrailInterceptSamples);
        playerTrailClosestSearchWindow = Mathf.Max(0, profile.PlayerTrailClosestSearchWindow);
        playerFollowTargetCommitDuration = Mathf.Max(0.05f, profile.PlayerFollowTargetCommitDuration);
    }

    private void Update()
    {
        // The vent enemy has two modes:
        // Crawling = hidden in vents and reacting to sound.
        // Emerged  = visible in the room, scanning or chasing.
        if (layout == null || mapService == null || playerTarget == null || ResolveNoiseEventBus() == null || nodes.Count == 0)
        {
            TryGetAudioDriver()?.SetCrawlLoopActive(false);
            return;
        }

        if (playerInteractionController != null && playerInteractionController.IsPlayerCaught)
        {
            TryGetAudioDriver()?.SetCrawlLoopActive(false);
            return;
        }

        if (IsStunned)
        {
            TryGetAudioDriver()?.SetCrawlLoopActive(false);

            if (currentState == VentEnemyState.Emerged)
            {
                disruptionController?.UpdateWhileStunned(
                    onWhileStunned: () => emergedGroundPath.Clear(),
                    onAfterPresentation: ApplyFacing);
                animationDriver?.TickAnimationFrame();
            }

            return;
        }

        activityWatchdog?.BeginFrame(transform.position);
        disruptionController?.BeginNormalFrame();

        if (currentState == VentEnemyState.Crawling)
        {
            TryGetAudioDriver()?.SetCrawlLoopActive(true);
            TryHandleLatestNoise();
            bool hasPlayerFollowIntent = TryUpdatePlayerVentFollow(forceRepath: false);
            UpdateMovement();
            UpdateIrregularVentRattle();

            if (TryConsumePendingNoiseEmergence())
            {
                CheckActivityTimeout();
                return;
            }

            if ((!followPlayerInVentNetwork || !hasPlayerFollowIntent)
                && currentTravelIntent != VentTravelIntent.NoiseResponse)
            {
                TryStartAmbientRoam();
            }

            CheckActivityTimeout();
            return;
        }

        TryGetAudioDriver()?.SetCrawlLoopActive(false);
        UpdateEmergedState();
        animationDriver?.TickAnimationFrame();
        CheckActivityTimeout();
    }

    private void TryHandleLatestNoise()
    {
        if (!respondToNoise)
        {
            return;
        }

        // This enemy listens across the shared noise bus and only remembers the latest
        // relevant noise. Its horror value comes from "the ceiling thing is following what I did."
        if (noiseListenerController == null
            || !noiseListenerController.TryConsumeLatestRelevantNoise(
                CanRespondToNoise,
                out NoiseEventRecord latestRelevantNoise))
        {
            return;
        }

        lastNoisePosition = latestRelevantNoise.position;
        int targetNodeId = FindNoiseResponseNode(latestRelevantNoise.position);
        int noisePriority = EnemyNoiseRetargetController.GetPriority(latestRelevantNoise.sourceType);

        if (targetNodeId < 0)
        {
            LogDebug($"Heard {latestRelevantNoise.sourceType} noise at {FormatWorld(latestRelevantNoise.position)} but found no vent response node.");
            return;
        }

        if (targetNodeId == currentNodeId)
        {
            LogDebug($"Heard {latestRelevantNoise.sourceType} noise at {FormatWorld(latestRelevantNoise.position)} -> target {DescribeNode(targetNodeId)}.");
            ClearNoiseResponseTarget();
            ScheduleNoiseEmergence("noise reached current vent node", noisePriority);
            return;
        }

        int activeDestinationNodeId = GetActiveNoiseDestinationNodeId();

        if (activeDestinationNodeId == targetNodeId)
        {
            return;
        }

        if (noiseRetargetController != null && !noiseRetargetController.CanRetarget(targetNodeId, noisePriority))
        {
            return;
        }

        int preferredTargetNodeId = targetNodeId;

        if (!TryBuildNodePath(currentNodeId, targetNodeId, currentNodePath))
        {
            int fallbackNodeId = FindReachableFallbackNode(latestRelevantNoise.position);

            if (fallbackNodeId < 0 || !TryBuildNodePath(currentNodeId, fallbackNodeId, currentNodePath))
            {
                LogDebug($"No vent path from {DescribeNode(currentNodeId)} to {DescribeNode(targetNodeId)}.");
                return;
            }

            targetNodeId = fallbackNodeId;
            LogDebug($"Preferred target {DescribeNode(preferredTargetNodeId)} is unreachable from {DescribeNode(currentNodeId)}. Falling back to {DescribeNode(targetNodeId)}.");
        }

        LogDebug($"Heard {latestRelevantNoise.sourceType} noise at {FormatWorld(latestRelevantNoise.position)} -> target {DescribeNode(targetNodeId)}.");
        ClearPendingNoiseEmergence();
        currentTravelIntent = VentTravelIntent.NoiseResponse;
        currentPathIndex = currentNodePath.Count > 1 ? 1 : 0;
        noiseRetargetController?.Commit(targetNodeId, noisePriority, noiseRetargetCooldown);
        activityWatchdog?.ReportActivity();
        LogDebug($"Routing through vents from {DescribeNode(currentNodeId)} to {DescribeNode(targetNodeId)} ({Mathf.Max(0, currentNodePath.Count - 1)} hops).");
        EmitCrawlPulse();
    }

    private bool TryUpdatePlayerVentFollow(bool forceRepath)
    {
        if (!followPlayerInVentNetwork
            || currentState != VentEnemyState.Crawling
            || playerTarget == null
            || nodes.Count == 0)
        {
            return false;
        }

        if (IsPlayerFollowStartupGraceActive())
        {
            return false;
        }

        if (!forceRepath && Time.time < nextPlayerFollowRetargetTime)
        {
            return currentNodePath.Count > 0 || currentTravelIntent != VentTravelIntent.None;
        }

        nextPlayerFollowRetargetTime = Time.time + playerFollowRetargetInterval;
        int targetNodeId = ResolvePlayerFollowTargetNode();

        if (targetNodeId < 0)
        {
            return false;
        }

        int activeDestinationNodeId = currentNodePath.Count > 0
            ? currentNodePath[currentNodePath.Count - 1]
            : currentNodeId;

        if (!forceRepath
            && currentNodePath.Count > 0
            && (activeDestinationNodeId == targetNodeId
                || ShouldHoldCommittedFollowTarget(activeDestinationNodeId, targetNodeId)))
        {
            return true;
        }

        if (targetNodeId == currentNodeId)
        {
            ClearPendingNoiseEmergence();

            if (Time.time >= lastCrawlPulseTime + crawlPulseInterval)
            {
                EmitCrawlPulse();
            }

            activityWatchdog?.ReportActivity();
            return false;
        }

        rebuiltNodePathScratch.Clear();

        if (!TryBuildNodePath(currentNodeId, targetNodeId, rebuiltNodePathScratch))
        {
            return false;
        }

        currentNodePath.Clear();
        currentNodePath.AddRange(rebuiltNodePathScratch);
        currentPathIndex = currentNodePath.Count > 1 ? 1 : 0;
        currentTravelIntent = VentTravelIntent.AmbientRoam;
        CommitFollowTarget(targetNodeId);
        ClearPendingNoiseEmergence();
        activityWatchdog?.ReportActivity();

        if (Time.time >= lastCrawlPulseTime + (crawlPulseInterval * 0.35f))
        {
            EmitCrawlPulse();
        }

        return true;
    }

    private bool CanRespondToNoise(NoiseEventRecord record)
    {
        // Hear from the hidden vent node, not the last surfaced world position.
        // A small slack keeps grid rounding from suppressing near-miss sounds.
        Vector2 hearingOrigin = ResolveHiddenVentWorldPosition();
        return Vector2.Distance(hearingOrigin, record.position) <= record.radius + 0.35f;
    }

    private Vector2 ResolveHiddenVentWorldPosition()
    {
        if (mapService != null && currentNodeId >= 0 && currentNodeId < nodes.Count)
        {
            return mapService.CellToWorldCenter(nodes[currentNodeId].Cell);
        }

        return transform.position;
    }

    private void UpdateMovement()
    {
        // Hidden vent travel moves node-to-node through the invisible graph and leaves
        // debug pulses so we can study its presence before real audio is added.
        if (currentNodePath.Count == 0 || currentPathIndex >= currentNodePath.Count)
        {
            return;
        }

        int nextNodeId = currentNodePath[currentPathIndex];
        Vector3 nextPosition = mapService.CellToWorldCenter(nodes[nextNodeId].Cell);
        Vector3 previousPosition = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, nextPosition, crawlMoveSpeed * Time.deltaTime);

        if ((transform.position - previousPosition).sqrMagnitude > 0.000001f)
        {
            TryGetAudioDriver()?.NotifyCrawlMovement();
        }

        if (Time.time >= lastCrawlPulseTime + crawlPulseInterval)
        {
            EmitCrawlPulse();
        }

        if (Vector2.Distance(transform.position, nextPosition) > 0.04f)
        {
            return;
        }

        currentNodeId = nextNodeId;
        currentPathIndex++;
        TryGetAudioDriver()?.OnVentNodeReached();

        if (currentPathIndex >= currentNodePath.Count)
        {
            currentNodePath.Clear();

            if (currentTravelIntent == VentTravelIntent.NoiseResponse)
            {
                LogDebug($"Reached noise response vent {DescribeNode(currentNodeId)}. Preparing to emerge.");
                ClearNoiseResponseTarget();
                ScheduleNoiseEmergence("arrived at noise response node", noisePriority: 1);
            }
            else
            {
                currentTravelIntent = VentTravelIntent.None;
                nextAmbientRoamTime = Time.time + ambientRoamDelay;
            }
        }
    }

    private void BeginEmergence(string reason = null)
    {
        if (!allowEmergence)
        {
            string blockedSuffix = string.IsNullOrWhiteSpace(reason) ? "." : $" ({reason}).";
            LogDebug($"Skipped emergence at {DescribeNode(currentNodeId)} because runtime behavior disallows it{blockedSuffix}");
            currentTravelIntent = VentTravelIntent.None;
            currentPathIndex = 0;
            currentNodePath.Clear();
            ClearCommittedFollowTarget();
            ClearNoiseResponseTarget();
            nextAmbientRoamTime = Time.time + ambientRoamDelay;
            activityWatchdog?.ReportActivity();

            if (followPlayerInVentNetwork)
            {
                nextPlayerFollowRetargetTime = Time.time;
                TryUpdatePlayerVentFollow(forceRepath: true);
            }
            else
            {
                StartAmbientRoam(immediate: true);
            }

            return;
        }

        if (!CanUseNodeAsSurfaceAccess(currentNodeId))
        {
            int surfaceAccessNodeId = FindNearestSurfaceAccessNode(ResolveHiddenVentWorldPosition());

            if (surfaceAccessNodeId >= 0
                && surfaceAccessNodeId != currentNodeId
                && TryBuildNodePath(currentNodeId, surfaceAccessNodeId, currentNodePath))
            {
                LogDebug($"Rerouting emergence from {DescribeNode(currentNodeId)} to surface access {DescribeNode(surfaceAccessNodeId)}.");
                currentTravelIntent = VentTravelIntent.NoiseResponse;
                currentPathIndex = currentNodePath.Count > 1 ? 1 : 0;
                ClearPendingNoiseEmergence();
                activityWatchdog?.ReportActivity();
                return;
            }

            LogDebug($"Skipped emergence at {DescribeNode(currentNodeId)} because the node is not marked as a surface access point.");
            currentTravelIntent = VentTravelIntent.None;
            currentPathIndex = 0;
            currentNodePath.Clear();
            ClearCommittedFollowTarget();
            ClearNoiseResponseTarget();
            nextAmbientRoamTime = Time.time + ambientRoamDelay;
            activityWatchdog?.ReportActivity();
            return;
        }

        // Emergence switches from hidden graph motion to visible room behavior.
        TryGetAudioDriver()?.SetCrawlLoopActive(false);
        ClearCommittedFollowTarget();
        ClearPendingNoiseEmergence();
        currentState = VentEnemyState.Emerged;
        currentPathIndex = 0;
        currentNodePath.Clear();
        emergedStartTime = Time.time;
        emergedUntilTime = Time.time + emergeLookDuration;
        scheduledEarlyFakeRetreatTime = ResolveScheduledEarlyFakeRetreatTime();
        nextIrregularVentRattleTime = float.PositiveInfinity;
        ClearLastSeenPlayerTarget();
        emergedGroundPath.Clear();
        nextEmergedRepathTime = 0f;
        emergeBaseFacing = GetPreferredFacing();
        facingDirection = emergeBaseFacing;
        emergedDetectionMeter = 0f;
        ClearNoiseResponseTarget();
        SetVisualsVisible(true);
        ApplyFacing();
        activityWatchdog?.ReportActivity();
        TryGetAudioDriver()?.OnEmerge();

        string suffix = string.IsNullOrWhiteSpace(reason) ? "." : $" ({reason}).";
        LogDebug($"Emerging from vent at {DescribeNode(currentNodeId)}{suffix}");
    }

    private void UpdateEmergedState()
    {
        // While emerged, the enemy gets a normal vision cone and can briefly scan, chase,
        // and then retreat back into the vent system.
        if (visionVisualizer != null && !visionVisualizer.gameObject.activeSelf)
        {
            visionVisualizer.gameObject.SetActive(true);
        }

        visionSensor.Configure(transform, emergeVisionDistance, emergeVisionAngle, mapService.VisionBlockingLayers);
        visionSensor.UseExposureMultiplier = useExposureForDetection;

        if (visionVisualizer != null)
        {
            visionVisualizer.Configure(
                emergeVisionDistance,
                emergeVisionAngle,
                bodyRenderer != null ? bodyRenderer.sortingLayerName : "Default",
                bodyRenderer != null ? bodyRenderer.sortingOrder - 1 : 18,
                transform,
                mapService.VisionBlockingLayers);
        }

        VisionSensor2D.VisionReading rawReading = visionSensor.GetReading(playerTarget);
        VisionSensor2D.VisionReading reading = BuildConfirmedVisionReading(rawReading);
        UpdateThreatFeedback(reading.CanSee);
        EnemySightMemoryController.Observation sightObservation = sightMemoryController != null
            ? sightMemoryController.Observe(reading, transform.position, useAimPointDirection: true)
            : default;

        if (reading.CanSee)
        {
            emergedUntilTime = Mathf.Max(emergedUntilTime, Time.time + emergeSeenHoldDuration);
            activityWatchdog?.ReportActivity();

            if (sightObservation.JustSpottedPlayer)
            {
                PlayerSpotted?.Invoke();

                if (enableDebugLogs)
                {
                    LogDebug($"Spotted player while emerged at {FormatCell(sightObservation.LastSeenCell)} (distance {reading.Distance:0.00}, angle {reading.Angle:0.0}).");
                }
            }

            Vector2 lookDirection = sightObservation.FacingDirection;

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                facingDirection = lookDirection;
            }

            EnemyPursuitResolution pursuit = EnemyPursuitTargetResolver.Resolve(
                sightObservation.LastSeenCell,
                4,
                RefreshEmergedPath,
                HasReachedGroundTargetCell,
                TryFindReachableNearbyGroundCell);

            if (pursuit.UsedFallback)
            {
                if (enableDebugLogs)
                {
                    LogDebug($"Visible emerged target {FormatCell(pursuit.PreferredTargetCell)} is blocked. Falling back to reachable nearby cell {FormatCell(pursuit.ResolvedTargetCell)}.");
                }
            }

            sightMemoryController?.SetLastSeenCell(pursuit.ResolvedTargetCell);

            if (!IsAttackRecovering)
            {
                MoveAlongEmergedGroundPath();
                TryCapturePlayer(reading);
            }
        }
        else
        {
            if (sightObservation.JustLostPlayer)
            {
                if (enableDebugLogs)
                {
                    LogDebug($"Lost sight of player while emerged. Last seen cell {FormatCell(GetLastSeenPlayerCell())}. Hold duration {emergeSeenHoldDuration:0.00}s.");
                }
            }

            if (HasLastSeenPlayerTarget() && !HasReachedLastSeenPlayerPosition())
            {
                Vector3Int lastSeenPlayerCell = GetLastSeenPlayerCell();
                Vector2 chaseDirection = (Vector2)mapService.CellToWorldCenter(lastSeenPlayerCell) - (Vector2)transform.position;

                if (chaseDirection.sqrMagnitude > 0.0001f)
                {
                    facingDirection = chaseDirection.normalized;
                }

                EnemyPursuitResolution pursuit = EnemyPursuitTargetResolver.Resolve(
                    lastSeenPlayerCell,
                    4,
                    RefreshEmergedPath,
                    HasReachedGroundTargetCell,
                    TryFindReachableNearbyGroundCell);
                bool hasGroundPath = pursuit.HasReachableTarget;

                if (pursuit.UsedFallback)
                {
                    if (enableDebugLogs)
                    {
                        LogDebug($"Last seen emerged target {FormatCell(pursuit.PreferredTargetCell)} is blocked. Falling back to reachable nearby cell {FormatCell(pursuit.ResolvedTargetCell)}.");
                    }
                }

                sightMemoryController?.SetLastSeenCell(pursuit.ResolvedTargetCell);
                lastSeenPlayerCell = pursuit.ResolvedTargetCell;

                if (!hasGroundPath
                    && !HasReachedLastSeenPlayerPosition()
                    && Time.time - GetLastSeenPlayerTime() > emergeSeenHoldDuration)
                {
                    LogDebug($"Could not reach emerged chase target {FormatCell(lastSeenPlayerCell)}. Abandoning last seen position and returning to scan.");
                    ClearLastSeenPlayerTarget();
                }

                if (!IsAttackRecovering)
                {
                    MoveAlongEmergedGroundPath();
                    TryCapturePlayer(reading);
                }
            }
            else
            {
                emergedGroundPath.Clear();
                float oscillation = Mathf.Sin((Time.time - emergedStartTime) * scanCyclesPerSecond * Mathf.PI * 2f);
                float scanOffset = oscillation * scanSweepAngle * 0.5f;
                Vector3 rotatedFacing = Quaternion.Euler(0f, 0f, scanOffset) * (Vector3)emergeBaseFacing;
                facingDirection = new Vector2(rotatedFacing.x, rotatedFacing.y);
                activityWatchdog?.ReportActivity();
            }
        }

        ApplyFacing();
        UpdateVisual(reading);

        if (ShouldTriggerEarlyFakeRetreat(reading))
        {
            BeginHiddenRetreat("fake retreat feint");
            return;
        }

        if (Time.time < emergedUntilTime)
        {
            return;
        }

        if (HasLastSeenPlayerTarget() && !HasReachedLastSeenPlayerPosition())
        {
            return;
        }

        if (Time.time - GetLastSeenPlayerTime() <= emergeSeenHoldDuration)
        {
            return;
        }

        string retreatReason = HasLastSeenPlayerTarget()
            ? $"lost player near {FormatCell(GetLastSeenPlayerCell())}"
            : "scan window finished";
        BeginHiddenRetreat(retreatReason);
    }

    private bool RefreshEmergedPath(Vector3Int targetCell)
    {
        // When visible, the enemy reuses the regular ground pathfinder instead of trying
        // to do special-case movement.
        if (Time.time < nextEmergedRepathTime && emergedGroundPath.Count > 0)
        {
            return true;
        }

        Vector3Int currentCell = mapService.WorldToCell(transform.position);
        emergedGroundPath.Clear();
        bool hasPath = GridPathfinder.TryBuildPath(mapService, currentCell, targetCell, emergedGroundPath, allowClosedDoors: true);
        nextEmergedRepathTime = Time.time + 0.15f;
        return hasPath || Vector2.Distance(transform.position, mapService.CellToWorldCenter(targetCell)) <= 0.18f;
    }

    private void MoveAlongEmergedGroundPath()
    {
        if (emergedGroundPath.Count == 0)
        {
            return;
        }

        Vector3 targetPosition = mapService.CellToWorldCenter(emergedGroundPath[0]);
        Vector2 currentPosition = transform.position;
        Vector2 toTarget = (Vector2)targetPosition - currentPosition;

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            facingDirection = toTarget.normalized;
        }

        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, emergeChaseSpeed * Time.deltaTime);
        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);

        if (Vector2.Distance(nextPosition, targetPosition) <= 0.04f)
        {
            emergedGroundPath.RemoveAt(0);
        }
    }

    private void TryCapturePlayer(VisionSensor2D.VisionReading reading)
    {
        if (playerTarget == null || playerInteractionController == null)
        {
            return;
        }

        if (IsAttackRecovering)
        {
            return;
        }

        if (!reading.CanSee)
        {
            return;
        }

        if (playerInteractionController.TryStrikePlayer(transform, emergeCaptureDistance, gameObject.name))
        {
            BeginAttackRecovery();
        }
    }

    private VisionSensor2D.VisionReading BuildConfirmedVisionReading(VisionSensor2D.VisionReading rawReading)
    {
        return EnemyVisionConfirmationUtility.BuildConfirmedReading(
            rawReading,
            Time.deltaTime,
            ref emergedDetectionMeter,
            emergedDetectionBuildDuration,
            emergedDetectionDecayDuration,
            confirmedDetectionThreshold);
    }

    private bool IsStunned => disruptionController != null && disruptionController.IsStunned;
    private bool IsAttackRecovering => disruptionController != null && disruptionController.IsAttackRecovering;

    private void BeginAttackRecovery()
    {
        disruptionController?.BeginAttackRecovery(
            attackRecoveryDuration,
            onRecoveryStarted: () =>
            {
                emergedGroundPath.Clear();
                nextEmergedRepathTime = 0f;
                activityWatchdog?.ReportActivity();
            });
    }

    private bool HasLastSeenPlayerTarget()
    {
        return sightMemoryController != null && sightMemoryController.HasLastSeenTarget;
    }

    private bool HasReachedLastSeenPlayerPosition()
    {
        return Vector2.Distance(transform.position, mapService.CellToWorldCenter(GetLastSeenPlayerCell())) <= 0.18f;
    }

    private bool HasReachedGroundTargetCell(Vector3Int targetCell)
    {
        return Vector2.Distance(transform.position, mapService.CellToWorldCenter(targetCell)) <= 0.18f;
    }

    private void ClearLastSeenPlayerTarget()
    {
        sightMemoryController?.ClearLastSeenTarget();
        emergedGroundPath.Clear();
        nextEmergedRepathTime = 0f;
    }

    private bool TryFindReachableNearbyGroundCell(Vector3Int targetCell, int searchRadius, out Vector3Int reachableCell)
    {
        reachableCell = targetCell;

        if (mapService == null)
        {
            return false;
        }

        Vector3Int currentCell = mapService.WorldToCell(transform.position);

        for (int radius = 0; radius <= Mathf.Max(0, searchRadius); radius++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) != radius)
                    {
                        continue;
                    }

                    Vector3Int candidate = targetCell + new Vector3Int(offsetX, offsetY, 0);

                    if (!mapService.IsWalkable(candidate, true))
                    {
                        continue;
                    }

                    if (candidate == currentCell)
                    {
                        reachableCell = candidate;
                        return true;
                    }

                    emergedPathProbeScratch.Clear();

                    if (GridPathfinder.TryBuildPath(mapService, currentCell, candidate, emergedPathProbeScratch, allowClosedDoors: true))
                    {
                        reachableCell = candidate;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void EmitCrawlPulse(float radiusMultiplier = 1f, bool resetPulseTimer = true)
    {
        float pulseRadius = crawlPulseRadius * Mathf.Max(0.1f, radiusMultiplier);
        ResolveNoiseEventBus()?.TryEmitNoise(
            transform.position,
            pulseRadius,
            NoiseSourceType.VentCrawl,
            instanceId,
            NoiseEmitterAffiliation.Enemy);

        if (resetPulseTimer)
        {
            lastCrawlPulseTime = Time.time;
        }

        TryGetAudioDriver()?.OnCrawlPulse();
    }

    private void BuildVentGraph()
    {
        // The vent graph is deliberately structured instead of randomly sampled:
        // one vent anchor per room, one straight corridor spine, and direct room-to-corridor links.
        nodes.Clear();
        adjacencyByNode.Clear();
        roomNodeIdsByRoom.Clear();
        usedCells.Clear();

        if (layout == null || mapService == null)
        {
            return;
        }

        if (TryBuildCustomVentGraph())
        {
            EnsureAdjacencyCoverage();
            return;
        }

        if (ShouldSuppressGeneratedVentFallback())
        {
            LogDebug("Skipping generated vent fallback in MainScene. Author explicit vent links to enable the vent graph.");
            return;
        }

        Dictionary<int, GeneratedRoomData> roomById = new();
        List<Vector3Int> corridorCells = new();
        List<GeneratedRoomData> upperRooms = new();
        List<GeneratedRoomData> lowerRooms = new();
        Dictionary<int, int> corridorNodeIdsByX = new();
        List<int> corridorNodeIds = new();
        List<int> upperRoomNodeIds = new();
        List<int> lowerRoomNodeIds = new();

        foreach (GeneratedRoomData room in layout.Rooms)
        {
            roomById[room.RoomId] = room;
        }

        foreach (GeneratedRouteData route in layout.Routes)
        {
            corridorCells.AddRange(CollectCorridorCells(route, roomById));
        }

        int corridorBandY = ResolveCorridorBandY(corridorCells);

        foreach (GeneratedRoomData room in layout.Rooms)
        {
            if (room.CenterCell.y >= corridorBandY)
            {
                upperRooms.Add(room);
            }
            else
            {
                lowerRooms.Add(room);
            }
        }

        int upperBandY = ResolveRoomBandY(upperRooms, corridorBandY, useUpperBand: true);
        int lowerBandY = ResolveRoomBandY(lowerRooms, corridorBandY, useUpperBand: false);

        foreach (GeneratedRoomData room in layout.Rooms)
        {
            bool isUpperRoom = room.CenterCell.y >= corridorBandY;
            int roomBandY = isUpperRoom ? upperBandY : lowerBandY;

            if (!TryCreateStructuredRoomVent(room, roomBandY, corridorBandY, out int roomNodeId, out int connectorX))
            {
                continue;
            }

            if (isUpperRoom)
            {
                upperRoomNodeIds.Add(roomNodeId);
            }
            else
            {
                lowerRoomNodeIds.Add(roomNodeId);
            }

            if (TryGetOrCreateCorridorConnectorNode(
                    connectorX,
                    corridorBandY,
                    corridorNodeIdsByX,
                    out int corridorNodeId))
            {
                if (!corridorNodeIds.Contains(corridorNodeId))
                {
                    corridorNodeIds.Add(corridorNodeId);
                }

                ConnectNodes(roomNodeId, corridorNodeId);
            }
        }

        int corridorMinX = ResolveCorridorEndpointX(corridorNodeIdsByX, corridorCells, useMinimumX: true);
        int corridorMaxX = ResolveCorridorEndpointX(corridorNodeIdsByX, corridorCells, useMinimumX: false);
        AddCorridorEndpointNode(corridorMinX, corridorBandY, corridorNodeIdsByX, corridorNodeIds);
        AddCorridorEndpointNode(corridorMaxX, corridorBandY, corridorNodeIdsByX, corridorNodeIds);

        SortNodeIdsByHorizontalPosition(upperRoomNodeIds);
        SortNodeIdsByHorizontalPosition(lowerRoomNodeIds);
        SortNodeIdsByHorizontalPosition(corridorNodeIds);
        ConnectSequentialNodes(upperRoomNodeIds);
        ConnectSequentialNodes(lowerRoomNodeIds);
        ConnectSequentialNodes(corridorNodeIds);

        EnsureAdjacencyCoverage();
    }

    private bool TryBuildCustomVentGraph()
    {
        if (!customVentRoute.IsValid || customVentRoute.Nodes == null || customVentRoute.Nodes.Length == 0)
        {
            return false;
        }

        if (!customVentRoute.UsesExplicitConnections
            && gameObject.scene.IsValid()
            && RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(gameObject.scene))
        {
            LogDebug("Ignoring implicit custom vent route in MainScene. Explicit vent links are required.");
            return false;
        }

        List<int> orderedNodeIds = new(customVentRoute.Nodes.Length);
        List<int> authoredNodeIds = new(customVentRoute.Nodes.Length);
        int skippedNonWalkableNodeCount = 0;
        int skippedDuplicateNodeCount = 0;

        for (int index = 0; index < customVentRoute.Nodes.Length; index++)
        {
            MainEscapeVentNodeDefinition nodeDefinition = customVentRoute.Nodes[index];

            if (!mapService.IsWalkable(nodeDefinition.Cell, true))
            {
                authoredNodeIds.Add(-1);
                skippedNonWalkableNodeCount++;
                continue;
            }

            if (!usedCells.Add(nodeDefinition.Cell))
            {
                authoredNodeIds.Add(-1);
                skippedDuplicateNodeCount++;
                continue;
            }

            int roomId = nodeDefinition.IsCorridor
                ? -1
                : ResolveCustomRoomId(nodeDefinition.Cell, nodeDefinition.RoomId);
            int nodeId = AddNode(nodeDefinition.Cell, nodeDefinition.IsCorridor, roomId, nodeDefinition.AllowsSurfaceAccess);

            if (!nodeDefinition.IsCorridor && roomId >= 0)
            {
                RegisterRoomNode(roomId, nodeId);
            }

            authoredNodeIds.Add(nodeId);
            orderedNodeIds.Add(nodeId);
        }

        if (orderedNodeIds.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(CeilingVentEnemyController)} rejected all authored vent nodes in '{gameObject.scene.name}'. " +
                $"Rejected {skippedNonWalkableNodeCount} non-walkable node(s) and {skippedDuplicateNodeCount} duplicate node(s).",
                this);
            return false;
        }

        if (customVentRoute.UsesExplicitConnections)
        {
            int connectedPairs = 0;

            if (customVentRoute.Connections != null)
            {
                for (int index = 0; index < customVentRoute.Connections.Length; index++)
                {
                    MainEscapeVentConnectionDefinition connection = customVentRoute.Connections[index];

                    if (connection.FromIndex < 0
                        || connection.ToIndex < 0
                        || connection.FromIndex >= authoredNodeIds.Count
                        || connection.ToIndex >= authoredNodeIds.Count)
                    {
                        continue;
                    }

                    int fromNodeId = authoredNodeIds[connection.FromIndex];
                    int toNodeId = authoredNodeIds[connection.ToIndex];

                    if (fromNodeId < 0 || toNodeId < 0)
                    {
                        continue;
                    }

                    ConnectNodes(fromNodeId, toNodeId);
                    connectedPairs++;
                }
            }

            LogDebug($"Using custom vent graph with {orderedNodeIds.Count} node(s) and {connectedPairs} authored link(s).");
        }
        else
        {
            ConnectSequentialNodes(orderedNodeIds);

            if (customVentRoute.LoopPath && orderedNodeIds.Count > 2)
            {
                ConnectNodes(orderedNodeIds[0], orderedNodeIds[orderedNodeIds.Count - 1]);
            }

            LogDebug($"Using custom vent route with {orderedNodeIds.Count} node(s){(customVentRoute.LoopPath ? " in loop mode" : string.Empty)}.");
        }
        return true;
    }

    private bool ShouldSuppressGeneratedVentFallback()
    {
        return gameObject.scene.IsValid()
            && RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(gameObject.scene);
    }

    private int ResolveCorridorBandY(IReadOnlyList<Vector3Int> corridorCells)
    {
        if (corridorCells != null && corridorCells.Count > 0)
        {
            List<int> sortedYs = new(corridorCells.Count);

            for (int index = 0; index < corridorCells.Count; index++)
            {
                sortedYs.Add(corridorCells[index].y);
            }

            sortedYs.Sort();
            return sortedYs[sortedYs.Count / 2];
        }

        List<int> doorYs = new();

        if (layout != null)
        {
            foreach (GeneratedRoomData room in layout.Rooms)
            {
                if (room.DoorCells == null)
                {
                    continue;
                }

                for (int index = 0; index < room.DoorCells.Length; index++)
                {
                    doorYs.Add(room.DoorCells[index].y);
                }
            }
        }

        if (doorYs.Count > 0)
        {
            doorYs.Sort();
            return doorYs[doorYs.Count / 2];
        }

        return layout != null ? layout.PlayerStartCell.y : 0;
    }

    private int ResolveRoomBandY(IReadOnlyList<GeneratedRoomData> rooms, int corridorBandY, bool useUpperBand)
    {
        if (rooms == null || rooms.Count == 0)
        {
            return corridorBandY + (useUpperBand ? 3 : -3);
        }

        int intersectionMin = int.MinValue;
        int intersectionMax = int.MaxValue;

        for (int index = 0; index < rooms.Count; index++)
        {
            RectInt bounds = rooms[index].Bounds;
            int roomMinY = bounds.yMin + 1;
            int roomMaxY = Mathf.Max(roomMinY, bounds.yMax - 2);
            intersectionMin = Mathf.Max(intersectionMin, roomMinY);
            intersectionMax = Mathf.Min(intersectionMax, roomMaxY);
        }

        if (intersectionMin <= intersectionMax)
        {
            return useUpperBand ? intersectionMax : intersectionMin;
        }

        int fallbackSum = 0;

        for (int index = 0; index < rooms.Count; index++)
        {
            RectInt bounds = rooms[index].Bounds;
            int roomMinY = bounds.yMin + 1;
            int roomMaxY = Mathf.Max(roomMinY, bounds.yMax - 2);
            fallbackSum += useUpperBand ? roomMaxY : roomMinY;
        }

        return Mathf.RoundToInt(fallbackSum / (float)rooms.Count);
    }

    private bool TryCreateStructuredRoomVent(GeneratedRoomData room, int roomBandY, int corridorBandY, out int nodeId, out int connectorX)
    {
        connectorX = ResolveRoomConnectorX(room, corridorBandY);

        if (!TryResolveRoomVentCell(room, connectorX, roomBandY, out Vector3Int roomCell))
        {
            nodeId = -1;
            return false;
        }

        connectorX = roomCell.x;
        nodeId = AddNode(roomCell, isCorridor: false, room.RoomId);
        RegisterRoomNode(room.RoomId, nodeId);
        return true;
    }

    private int ResolveRoomConnectorX(GeneratedRoomData room, int corridorBandY)
    {
        int interiorMinX = room.Bounds.xMin + 1;
        int interiorMaxX = Mathf.Max(interiorMinX, room.Bounds.xMax - 2);
        int preferredX = room.CenterCell.x;

        if (room.DoorCells != null && room.DoorCells.Length > 0)
        {
            Vector3Int bestDoor = room.DoorCells[0];
            int bestScore = int.MaxValue;

            for (int index = 0; index < room.DoorCells.Length; index++)
            {
                Vector3Int doorCell = room.DoorCells[index];
                int score = (Mathf.Abs(doorCell.y - corridorBandY) * 10) + Mathf.Abs(doorCell.x - room.CenterCell.x);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestDoor = doorCell;
                }
            }

            preferredX = bestDoor.x;
        }

        return Mathf.Clamp(preferredX, interiorMinX, interiorMaxX);
    }

    private bool TryResolveRoomVentCell(GeneratedRoomData room, int targetX, int targetY, out Vector3Int cell)
    {
        int minX = room.Bounds.xMin + 1;
        int maxX = Mathf.Max(minX, room.Bounds.xMax - 2);
        int minY = room.Bounds.yMin + 1;
        int maxY = Mathf.Max(minY, room.Bounds.yMax - 2);
        int clampedTargetX = Mathf.Clamp(targetX, minX, maxX);
        int clampedTargetY = Mathf.Clamp(targetY, minY, maxY);
        int bestScore = int.MaxValue;
        bool found = false;
        cell = new Vector3Int(clampedTargetX, clampedTargetY, 0);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3Int candidate = new(x, y, 0);

                if (usedCells.Contains(candidate) || !mapService.IsWalkable(candidate, true))
                {
                    continue;
                }

                int score = (Mathf.Abs(x - clampedTargetX) * 3) + (Mathf.Abs(y - clampedTargetY) * 8);

                if (!found || score < bestScore)
                {
                    bestScore = score;
                    cell = candidate;
                    found = true;
                }
            }
        }

        if (found)
        {
            usedCells.Add(cell);
        }

        return found;
    }

    private int ResolveCorridorEndpointX(
        IReadOnlyDictionary<int, int> corridorNodeIdsByX,
        IReadOnlyList<Vector3Int> corridorCells,
        bool useMinimumX)
    {
        if (corridorNodeIdsByX != null && corridorNodeIdsByX.Count > 0)
        {
            int selectedX = useMinimumX ? int.MaxValue : int.MinValue;

            foreach (int x in corridorNodeIdsByX.Keys)
            {
                if (useMinimumX)
                {
                    selectedX = Mathf.Min(selectedX, x);
                }
                else
                {
                    selectedX = Mathf.Max(selectedX, x);
                }
            }

            return selectedX;
        }

        if (corridorCells != null && corridorCells.Count > 0)
        {
            int selectedX = corridorCells[0].x;

            for (int index = 1; index < corridorCells.Count; index++)
            {
                int cellX = corridorCells[index].x;

                if (useMinimumX ? cellX < selectedX : cellX > selectedX)
                {
                    selectedX = cellX;
                }
            }

            return selectedX;
        }

        if (layout != null && layout.Rooms != null && layout.Rooms.Length > 0)
        {
            int selectedX = useMinimumX ? int.MaxValue : int.MinValue;

            for (int index = 0; index < layout.Rooms.Length; index++)
            {
                RectInt bounds = layout.Rooms[index].Bounds;
                int candidateX = useMinimumX ? bounds.xMin + 1 : bounds.xMax - 2;

                if (useMinimumX)
                {
                    selectedX = Mathf.Min(selectedX, candidateX);
                }
                else
                {
                    selectedX = Mathf.Max(selectedX, candidateX);
                }
            }

            if (selectedX != int.MaxValue && selectedX != int.MinValue)
            {
                return selectedX;
            }
        }

        return layout != null ? layout.PlayerStartCell.x : 0;
    }

    private void AddCorridorEndpointNode(
        int targetX,
        int corridorBandY,
        IDictionary<int, int> corridorNodeIdsByX,
        ICollection<int> corridorNodeIds)
    {
        if (TryGetOrCreateCorridorConnectorNode(
                targetX,
                corridorBandY,
                corridorNodeIdsByX,
                out int corridorNodeId))
        {
            if (!corridorNodeIds.Contains(corridorNodeId))
            {
                corridorNodeIds.Add(corridorNodeId);
            }
        }
    }

    private bool TryGetOrCreateCorridorConnectorNode(
        int targetX,
        int corridorBandY,
        IDictionary<int, int> corridorNodeIdsByX,
        out int nodeId)
    {
        if (corridorNodeIdsByX.TryGetValue(targetX, out nodeId))
        {
            return true;
        }

        Vector3Int corridorCell = new(targetX, corridorBandY, 0);
        usedCells.Add(corridorCell);

        nodeId = AddNode(corridorCell, isCorridor: true, roomId: -1);
        corridorNodeIdsByX[targetX] = nodeId;
        return true;
    }

    private void SortNodeIdsByHorizontalPosition(List<int> nodeIds)
    {
        if (nodeIds == null)
        {
            return;
        }

        nodeIds.Sort((left, right) =>
        {
            Vector3Int leftCell = nodes[left].Cell;
            Vector3Int rightCell = nodes[right].Cell;
            int xComparison = leftCell.x.CompareTo(rightCell.x);
            return xComparison != 0 ? xComparison : leftCell.y.CompareTo(rightCell.y);
        });
    }

    private void ConnectSequentialNodes(IReadOnlyList<int> nodeIds)
    {
        if (nodeIds == null || nodeIds.Count <= 1)
        {
            return;
        }

        for (int index = 1; index < nodeIds.Count; index++)
        {
            ConnectNodes(nodeIds[index - 1], nodeIds[index]);
        }
    }

    private void CreateRoomNodes(GeneratedRoomData room)
    {
        int nodeCount = GetRoomNodeCount(room.Bounds);
        int createdCount = 0;

        for (int attempt = 0; attempt < 10 && createdCount < nodeCount; attempt++)
        {
            if (!TryPickRoomCell(room.Bounds, out Vector3Int pickedCell))
            {
                break;
            }

            int nodeId = AddNode(pickedCell, isCorridor: false, room.RoomId);
            RegisterRoomNode(room.RoomId, nodeId);
            createdCount++;
        }

        if (createdCount == 0)
        {
            Vector3Int fallbackCell = room.CenterCell;

            if (mapService.IsWalkable(fallbackCell, true) && usedCells.Add(fallbackCell))
            {
                int nodeId = AddNode(fallbackCell, isCorridor: false, room.RoomId);
                RegisterRoomNode(room.RoomId, nodeId);
            }
        }

        if (!roomNodeIdsByRoom.TryGetValue(room.RoomId, out List<int> roomNodeIds) || roomNodeIds.Count == 0)
        {
            return;
        }

        for (int index = 1; index < roomNodeIds.Count; index++)
        {
            ConnectNodes(roomNodeIds[index - 1], roomNodeIds[index]);
        }
    }

    private void BuildRouteNodes(GeneratedRouteData route, IReadOnlyDictionary<int, GeneratedRoomData> roomById)
    {
        List<Vector3Int> routeCells = CollectCorridorCells(route, roomById);
        List<int> routeNodeIds = new();

        if (routeCells.Count == 0)
        {
            routeCells.AddRange(route.Cells);
        }

        int sampleCount = Mathf.Clamp(2 + (routeCells.Count / 6), 2, 8);

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            int cellIndex = Mathf.Clamp(
                Mathf.RoundToInt((routeCells.Count - 1) * ((sampleIndex + 1f) / (sampleCount + 1f))),
                0,
                routeCells.Count - 1);

            Vector3Int cell = routeCells[cellIndex];

            if (!usedCells.Add(cell))
            {
                continue;
            }

            int nodeId = AddNode(cell, isCorridor: true, roomId: -1);
            routeNodeIds.Add(nodeId);
        }

        int fromRoomNodeId = GetNearestRoomNode(route.FromRoomId, route.Cells.Length > 0 ? route.Cells[0] : layout.PlayerStartCell);
        int toRoomNodeId = GetNearestRoomNode(route.ToRoomId, route.Cells.Length > 0 ? route.Cells[route.Cells.Length - 1] : layout.ExitCell);

        if (routeNodeIds.Count == 0)
        {
            if (fromRoomNodeId >= 0 && toRoomNodeId >= 0)
            {
                ConnectNodes(fromRoomNodeId, toRoomNodeId);
            }

            return;
        }

        if (fromRoomNodeId >= 0)
        {
            ConnectNodes(fromRoomNodeId, routeNodeIds[0]);
        }

        for (int index = 1; index < routeNodeIds.Count; index++)
        {
            ConnectNodes(routeNodeIds[index - 1], routeNodeIds[index]);
        }

        if (toRoomNodeId >= 0)
        {
            ConnectNodes(routeNodeIds[routeNodeIds.Count - 1], toRoomNodeId);
        }
    }

    private List<Vector3Int> CollectCorridorCells(GeneratedRouteData route, IReadOnlyDictionary<int, GeneratedRoomData> roomById)
    {
        List<Vector3Int> cells = new();

        foreach (Vector3Int cell in route.Cells)
        {
            if (!mapService.IsWalkable(cell, true) || IsInsideRoom(cell, roomById))
            {
                continue;
            }

            if (cells.Count == 0 || cells[cells.Count - 1] != cell)
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    private bool IsInsideRoom(Vector3Int cell, IReadOnlyDictionary<int, GeneratedRoomData> roomById)
    {
        foreach (GeneratedRoomData room in roomById.Values)
        {
            if (room.Bounds.Contains(new Vector2Int(cell.x, cell.y)))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryPickRoomCell(RectInt bounds, out Vector3Int cell)
    {
        int minX = bounds.xMin + 1;
        int maxX = bounds.xMax - 2;
        int minY = bounds.yMin + 1;
        int maxY = bounds.yMax - 2;

        if (minX > maxX || minY > maxY)
        {
            cell = new Vector3Int(Mathf.RoundToInt(bounds.center.x), Mathf.RoundToInt(bounds.center.y), 0);
            return mapService.IsWalkable(cell, true) && usedCells.Add(cell);
        }

        for (int attempt = 0; attempt < 12; attempt++)
        {
            int x = random.Next(minX, maxX + 1);
            int y = random.Next(minY, maxY + 1);
            cell = new Vector3Int(x, y, 0);

            if (!mapService.IsWalkable(cell, true) || !usedCells.Add(cell))
            {
                continue;
            }

            return true;
        }

        cell = new Vector3Int(Mathf.RoundToInt(bounds.center.x), Mathf.RoundToInt(bounds.center.y), 0);

        if (!mapService.IsWalkable(cell, true) || !usedCells.Add(cell))
        {
            return false;
        }

        return true;
    }

    private int GetRoomNodeCount(RectInt bounds)
    {
        int area = bounds.width * bounds.height;

        if (area >= 180)
        {
            return 3;
        }

        if (area >= 90)
        {
            return 2;
        }

        return 1;
    }

    private void BeginHiddenRetreat(string reason = null)
    {
        string suffix = string.IsNullOrWhiteSpace(reason) ? "." : $" ({reason}).";
        LogDebug($"Retreating into vents from {FormatCell(mapService.WorldToCell(transform.position))}{suffix}");
        SetVisualsVisible(false);
        TryGetAudioDriver()?.OnRetreat();
        currentState = VentEnemyState.Crawling;
        currentTravelIntent = VentTravelIntent.None;
        currentPathIndex = 0;
        ClearCommittedFollowTarget();
        ClearPendingNoiseEmergence();
        currentNodePath.Clear();
        emergedGroundPath.Clear();
        sightMemoryController?.ClearLastSeenTarget();
        ClearNoiseResponseTarget();
        transform.position = ResolveHiddenVentWorldPosition();
        scheduledEarlyFakeRetreatTime = float.PositiveInfinity;
        nextIrregularVentRattleTime = Time.time + ResolveIrregularVentRattleDelay();
        nextAmbientRoamTime = Time.time + ambientRoamDelay;
        activityWatchdog?.ReportActivity();

        if (followPlayerInVentNetwork)
        {
            nextPlayerFollowRetargetTime = Time.time;
            TryUpdatePlayerVentFollow(forceRepath: true);
        }
        else
        {
            StartAmbientRoam(immediate: true);
        }
    }

    private void TryStartAmbientRoam()
    {
        if (currentNodePath.Count > 0 || Time.time < nextAmbientRoamTime)
        {
            return;
        }

        StartAmbientRoam(immediate: false);
    }

    private void StartAmbientRoam(bool immediate)
    {
        int targetNodeId = FindAmbientTargetNode();

        if (targetNodeId < 0 || targetNodeId == currentNodeId)
        {
            nextAmbientRoamTime = Time.time + ambientRoamDelay;
            return;
        }

        if (!TryBuildNodePath(currentNodeId, targetNodeId, currentNodePath))
        {
            nextAmbientRoamTime = Time.time + ambientRoamDelay;
            return;
        }

        currentTravelIntent = VentTravelIntent.AmbientRoam;
        currentPathIndex = currentNodePath.Count > 1 ? 1 : 0;
        nextAmbientRoamTime = Time.time + ambientRoamDelay;
        activityWatchdog?.ReportActivity();

        if (immediate)
        {
            EmitCrawlPulse();
        }
    }

    private int FindAmbientTargetNode()
    {
        if (nodes.Count <= 1)
        {
            return currentNodeId;
        }

        List<int> corridorCandidates = new();
        Vector2 playerWorld = playerTarget != null ? playerTarget.transform.position : Vector2.zero;
        Vector2 predictedPlayerWorld = ResolvePredictedPlayerFollowWorldPosition();
        Vector2 interceptPlayerWorld = ResolveInterceptPlayerFollowWorldPosition(playerWorld, predictedPlayerWorld);
        Vector2 playerMotion = ResolveRecentPlayerMotionDirection();
        bool hasPlayerMotion = playerMotion.sqrMagnitude > 0.0001f;
        int currentNodeDegree = GetNodeDegree(currentNodeId);

        for (int index = 0; index < nodes.Count; index++)
        {
            if (index == currentNodeId || !nodes[index].IsCorridor)
            {
                continue;
            }

            corridorCandidates.Add(index);
        }

        if (corridorCandidates.Count == 0)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                if (index != currentNodeId)
                {
                    corridorCandidates.Add(index);
                }
            }
        }

        corridorCandidates.Sort((left, right) =>
        {
            float leftScore = ScoreAmbientNode(left, playerWorld, predictedPlayerWorld, interceptPlayerWorld, playerMotion, hasPlayerMotion, currentNodeDegree);
            float rightScore = ScoreAmbientNode(right, playerWorld, predictedPlayerWorld, interceptPlayerWorld, playerMotion, hasPlayerMotion, currentNodeDegree);
            return leftScore.CompareTo(rightScore);
        });

        int choiceCount = Mathf.Clamp(corridorCandidates.Count, 1, 3);
        return corridorCandidates[random.Next(0, choiceCount)];
    }

    private float ScoreAmbientNode(
        int nodeId,
        Vector2 playerWorld,
        Vector2 predictedPlayerWorld,
        Vector2 interceptPlayerWorld,
        Vector2 playerMotion,
        bool hasPlayerMotion,
        int currentNodeDegree)
    {
        Vector2 nodeWorld = mapService.CellToWorldCenter(nodes[nodeId].Cell);
        float score =
            Vector2.Distance(nodeWorld, interceptPlayerWorld) * 1.2f
            + Vector2.Distance(nodeWorld, predictedPlayerWorld) * 0.7f
            + Vector2.Distance(nodeWorld, playerWorld) * 0.18f;

        float travelDistance = currentNodeId >= 0
            ? Vector2.Distance(nodeWorld, mapService.CellToWorldCenter(nodes[currentNodeId].Cell))
            : 0f;

        if (travelDistance < 4f)
        {
            score += 1.6f;
        }

        int nodeDegree = GetNodeDegree(nodeId);

        if (nodeDegree > currentNodeDegree)
        {
            score -= 0.45f * Mathf.Min(3, nodeDegree - currentNodeDegree);
        }

        if (hasPlayerMotion)
        {
            Vector2 towardNode = nodeWorld - playerWorld;
            float aheadDot = Vector2.Dot(playerMotion, towardNode.normalized);
            score -= Mathf.Clamp(aheadDot, -1f, 1f) * 1.15f;
        }

        return score;
    }

    private Vector2 GetPreferredFacing()
    {
        Vector2 directionToNoise = lastNoisePosition - (Vector2)transform.position;

        if (directionToNoise.sqrMagnitude > 0.001f)
        {
            return directionToNoise.normalized;
        }

        return random.NextDouble() > 0.5d ? Vector2.right : Vector2.left;
    }

    private void UpdateIrregularVentRattle()
    {
        if (currentState != VentEnemyState.Crawling
            || Time.time < nextIrregularVentRattleTime)
        {
            return;
        }

        nextIrregularVentRattleTime = Time.time + ResolveIrregularVentRattleDelay();

        if (!RollChance(irregularVentRattleChance)
            || Time.time < lastCrawlPulseTime + (crawlPulseInterval * 0.3f))
        {
            return;
        }

        EmitCrawlPulse(
            RandomRange(irregularVentRattleRadiusMinMultiplier, irregularVentRattleRadiusMaxMultiplier),
            resetPulseTimer: false);
    }

    private void ScheduleNoiseEmergence(string reason, int noisePriority)
    {
        if (ShouldEmergeImmediatelyForNoise(noisePriority))
        {
            LogDebug("Noise response qualifies for immediate emergence.");
            BeginEmergence(reason);
            return;
        }

        pendingNoiseEmergence = true;
        pendingNoiseEmergenceTime = Time.time + lowPriorityNoiseEmergeDelay;
        pendingNoiseEmergenceReason = reason;
        currentTravelIntent = VentTravelIntent.None;
        currentPathIndex = 0;
        currentNodePath.Clear();
        nextAmbientRoamTime = pendingNoiseEmergenceTime;
        activityWatchdog?.ReportActivity();
        LogDebug($"Holding emergence for {lowPriorityNoiseEmergeDelay:0.00}s at {DescribeNode(currentNodeId)} before surfacing.");
    }

    private bool TryConsumePendingNoiseEmergence()
    {
        if (!pendingNoiseEmergence || Time.time < pendingNoiseEmergenceTime)
        {
            return false;
        }

        string reason = pendingNoiseEmergenceReason;
        ClearPendingNoiseEmergence();
        BeginEmergence(reason);
        return true;
    }

    private void ClearPendingNoiseEmergence()
    {
        pendingNoiseEmergence = false;
        pendingNoiseEmergenceTime = float.PositiveInfinity;
        pendingNoiseEmergenceReason = null;
    }

    private bool ShouldEmergeImmediatelyForNoise(int noisePriority)
    {
        if (noisePriority >= 3)
        {
            return true;
        }

        if (IsPlayerFollowStartupGraceActive() || IsPlayerFollowOpeningSearchActive())
        {
            return false;
        }

        int predictedNodeId = FindPlayerFollowNode(ResolvePredictedPlayerFollowWorldPosition());

        if (predictedNodeId >= 0 && predictedNodeId == currentNodeId)
        {
            return true;
        }

        return RollChance(randomImmediateNoiseEmergeChance);
    }

    private void CommitFollowTarget(int targetNodeId)
    {
        committedFollowNodeId = targetNodeId;
        committedFollowNodeUntilTime = Time.time + playerFollowTargetCommitDuration;
    }

    private void ClearCommittedFollowTarget()
    {
        committedFollowNodeId = -1;
        committedFollowNodeUntilTime = float.NegativeInfinity;
    }

    private bool ShouldHoldCommittedFollowTarget(int activeDestinationNodeId, int proposedTargetNodeId)
    {
        if (Time.time >= committedFollowNodeUntilTime
            || committedFollowNodeId < 0
            || activeDestinationNodeId != committedFollowNodeId)
        {
            return false;
        }

        if (proposedTargetNodeId == committedFollowNodeId)
        {
            return true;
        }

        if (!adjacencyByNode.TryGetValue(committedFollowNodeId, out List<int> neighbors))
        {
            return false;
        }

        return neighbors.Contains(proposedTargetNodeId);
    }

    private bool IsPlayerFollowStartupGraceActive()
    {
        return Time.time < playerFollowStartupGraceUntilTime;
    }

    private bool IsPlayerFollowOpeningSearchActive()
    {
        return Time.time >= playerFollowStartupGraceUntilTime
            && Time.time < playerFollowSearchUntilTime;
    }

    private void ResetPlayerFollowOpeningWindow()
    {
        float startupGraceDuration = Mathf.Max(0f, initialPlayerFollowGraceDuration);
        float searchDuration = Mathf.Max(0f, initialPlayerFollowSearchDuration);
        playerFollowStartupGraceUntilTime = Time.time + startupGraceDuration;
        playerFollowSearchUntilTime = playerFollowStartupGraceUntilTime + searchDuration;
        nextPlayerFollowRetargetTime = playerFollowStartupGraceUntilTime;
    }

    private void ClearPlayerFollowOpeningWindow()
    {
        playerFollowStartupGraceUntilTime = float.NegativeInfinity;
        playerFollowSearchUntilTime = float.NegativeInfinity;
    }

    private int ResolvePlayerFollowTargetNode()
    {
        if (playerTarget == null || mapService == null || nodes.Count == 0)
        {
            return -1;
        }

        if (IsPlayerFollowOpeningSearchActive())
        {
            int openingSearchTargetNodeId = FindAmbientTargetNode();

            if (openingSearchTargetNodeId >= 0)
            {
                return openingSearchTargetNodeId;
            }
        }

        Vector2 currentPlayerPosition = playerTarget.transform.position;
        Vector2 predictedPlayerPosition = ResolvePredictedPlayerFollowWorldPosition();
        Vector2 interceptPlayerPosition = ResolveInterceptPlayerFollowWorldPosition(currentPlayerPosition, predictedPlayerPosition);
        Vector2 playerMotion = ResolveRecentPlayerMotionDirection();
        bool hasPlayerMotion = playerMotion.sqrMagnitude > 0.0001f;

        List<int> candidateNodeIds = new(3);
        AppendCandidateNode(candidateNodeIds, FindPlayerFollowNode(interceptPlayerPosition));
        AppendCandidateNode(candidateNodeIds, FindPlayerFollowNode(predictedPlayerPosition));
        AppendCandidateNode(candidateNodeIds, FindPlayerFollowNode(currentPlayerPosition));

        if (candidateNodeIds.Count == 0)
        {
            return -1;
        }

        if (RollChance(playerFollowAmbientInterferenceChance))
        {
            int ambientTargetNodeId = FindAmbientTargetNode();

            if (ambientTargetNodeId >= 0 && ambientTargetNodeId != currentNodeId)
            {
                return ambientTargetNodeId;
            }
        }

        int bestNodeId = candidateNodeIds[0];
        float bestScore = float.MaxValue;

        for (int index = 0; index < candidateNodeIds.Count; index++)
        {
            int candidateNodeId = candidateNodeIds[index];
            Vector2 candidateWorldPosition = mapService.CellToWorldCenter(nodes[candidateNodeId].Cell);
            float score =
                Vector2.Distance(candidateWorldPosition, interceptPlayerPosition) * 1.15f
                + Vector2.Distance(candidateWorldPosition, predictedPlayerPosition) * 0.75f
                + Vector2.Distance(candidateWorldPosition, currentPlayerPosition) * 0.2f;

            if (candidateNodeId == currentNodeId)
            {
                score += 1.4f;
            }

            int nodeDegree = GetNodeDegree(candidateNodeId);

            if (nodes[candidateNodeId].IsCorridor)
            {
                score -= 0.24f;
            }

            if (nodeDegree >= 3)
            {
                score -= 0.22f * Mathf.Min(3, nodeDegree - 2);
            }

            if (hasPlayerMotion)
            {
                Vector2 towardCandidate = candidateWorldPosition - currentPlayerPosition;
                float aheadDot = Vector2.Dot(playerMotion, towardCandidate.normalized);
                score -= Mathf.Clamp(aheadDot, -1f, 1f) * 0.9f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestNodeId = candidateNodeId;
            }
        }

        return bestNodeId;
    }

    private void AppendCandidateNode(List<int> candidateNodeIds, int nodeId)
    {
        if (nodeId < 0 || candidateNodeIds.Contains(nodeId))
        {
            return;
        }

        candidateNodeIds.Add(nodeId);
    }

    private Vector2 ResolveInterceptPlayerFollowWorldPosition(Vector2 currentPlayerPosition, Vector2 predictedPlayerPosition)
    {
        if (playerTrail == null
            || mapService == null
            || playerTrail.Count <= 1
            || playerTrailInterceptSamples <= 0)
        {
            return predictedPlayerPosition;
        }

        Vector3Int currentPlayerCell = mapService.WorldToCell(currentPlayerPosition);
        int startIndex = Mathf.Max(0, playerTrail.Count - Mathf.Max(2, playerTrailClosestSearchWindow));
        int closestIndex = playerTrail.FindClosestIndex(currentPlayerCell, startIndex);

        if (closestIndex < 0)
        {
            return predictedPlayerPosition;
        }

        int interceptIndex = Mathf.Min(playerTrail.Count - 1, closestIndex + playerTrailInterceptSamples);

        if (!playerTrail.TryGetCell(interceptIndex, out Vector3Int interceptCell))
        {
            return predictedPlayerPosition;
        }

        Vector2 interceptPosition = mapService.CellToWorldCenter(interceptCell);
        return interceptPosition == currentPlayerPosition ? predictedPlayerPosition : interceptPosition;
    }

    private Vector2 ResolveRecentPlayerMotionDirection()
    {
        if (playerTrail == null || mapService == null || playerTrail.Count <= 1)
        {
            return Vector2.zero;
        }

        int latestIndex = playerTrail.Count - 1;
        int earliestIndex = Mathf.Max(0, latestIndex - Mathf.Max(2, playerTrailPredictionSamples));

        if (!playerTrail.TryGetCell(latestIndex, out Vector3Int latestCell)
            || !playerTrail.TryGetCell(earliestIndex, out Vector3Int earliestCell))
        {
            return Vector2.zero;
        }

        Vector2 motion = mapService.CellToWorldCenter(latestCell) - mapService.CellToWorldCenter(earliestCell);
        return motion.sqrMagnitude > 0.0001f ? motion.normalized : Vector2.zero;
    }

    private Vector2 ResolvePredictedPlayerFollowWorldPosition()
    {
        if (playerTarget == null)
        {
            return transform.position;
        }

        Vector2 currentPlayerPosition = playerTarget.transform.position;

        if (playerTrail == null
            || mapService == null
            || playerTrail.Count <= 1
            || playerTrailPredictionSamples <= 0)
        {
            return currentPlayerPosition;
        }

        Vector3Int currentPlayerCell = mapService.WorldToCell(currentPlayerPosition);
        int startIndex = Mathf.Max(0, playerTrail.Count - Mathf.Max(2, playerTrailClosestSearchWindow));
        int closestIndex = playerTrail.FindClosestIndex(currentPlayerCell, startIndex);

        if (closestIndex < 0)
        {
            return currentPlayerPosition;
        }

        int predictedIndex = Mathf.Min(playerTrail.Count - 1, closestIndex + playerTrailPredictionSamples);

        if (!playerTrail.TryGetCell(predictedIndex, out Vector3Int predictedCell))
        {
            return currentPlayerPosition;
        }

        if (predictedCell == currentPlayerCell)
        {
            return currentPlayerPosition;
        }

        return mapService.CellToWorldCenter(predictedCell);
    }

    private bool ShouldTriggerEarlyFakeRetreat(VisionSensor2D.VisionReading reading)
    {
        if (currentState != VentEnemyState.Emerged
            || Time.time < scheduledEarlyFakeRetreatTime
            || reading.CanSee
            || HasLastSeenPlayerTarget())
        {
            return false;
        }

        scheduledEarlyFakeRetreatTime = float.PositiveInfinity;
        return true;
    }

    private float ResolveScheduledEarlyFakeRetreatTime()
    {
        if (!RollChance(earlyFakeRetreatChance))
        {
            return float.PositiveInfinity;
        }

        float clampedMin = Mathf.Max(0.1f, Mathf.Min(earlyFakeRetreatDelayMin, earlyFakeRetreatDelayMax));
        float clampedMax = Mathf.Max(clampedMin, earlyFakeRetreatDelayMax);
        float delay = RandomRange(clampedMin, clampedMax);
        float latestAllowedDelay = Mathf.Max(0.1f, emergeLookDuration - 0.1f);
        return Time.time + Mathf.Min(delay, latestAllowedDelay);
    }

    private float ResolveIrregularVentRattleDelay()
    {
        return RandomRange(irregularVentRattleIntervalMin, irregularVentRattleIntervalMax);
    }

    private bool RollChance(float chance)
    {
        if (random == null || chance <= 0f)
        {
            return false;
        }

        return random.NextDouble() < Mathf.Clamp01(chance);
    }

    private float RandomRange(float min, float max)
    {
        float clampedMin = Mathf.Min(min, max);
        float clampedMax = Mathf.Max(min, max);

        if (random == null || Mathf.Approximately(clampedMin, clampedMax))
        {
            return clampedMin;
        }

        return Mathf.Lerp(clampedMin, clampedMax, (float)random.NextDouble());
    }

    private void EnsureVisuals()
    {
        CeilingVentEnemyPrefabBindings prefabBindings = GetComponent<CeilingVentEnemyPrefabBindings>();

        if (prefabBindings != null)
        {
            prefabBindings.AutoAssign();
            visionSensor = prefabBindings.VisionSensor != null ? prefabBindings.VisionSensor : visionSensor;
            hitbox = prefabBindings.Hitbox != null ? prefabBindings.Hitbox : hitbox;
            visualRoot = prefabBindings.VisualRoot != null ? prefabBindings.VisualRoot : visualRoot;
            bodyRenderer = prefabBindings.BodyRenderer != null ? prefabBindings.BodyRenderer : bodyRenderer;
            facingMarkerRenderer = prefabBindings.FacingMarkerRenderer != null ? prefabBindings.FacingMarkerRenderer : facingMarkerRenderer;
            visionVisualizer = prefabBindings.VisionVisualizer != null ? prefabBindings.VisionVisualizer : visionVisualizer;
        }

        if (visionSensor == null)
        {
            visionSensor = gameObject.GetComponent<VisionSensor2D>();
        }

        if (visionSensor == null)
        {
            visionSensor = gameObject.AddComponent<VisionSensor2D>();
        }

        hitbox = gameObject.GetComponent<CircleCollider2D>();

        if (hitbox == null)
        {
            hitbox = gameObject.AddComponent<CircleCollider2D>();
        }

        hitbox.radius = 0.42f;
        hitbox.offset = Vector2.zero;
        hitbox.isTrigger = true;

        if (visualRoot != null && bodyRenderer != null && visionVisualizer != null)
        {
            DetachVisionVisualizerFromPresentationRoot();
            MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(bodyRenderer);
            LockPresentationToWorldAxes();
            return;
        }

        EnsureSharedSprite();

        GameObject visualRootObject = new("VentEnemyVisual");
        visualRootObject.transform.SetParent(transform, false);
        visualRootObject.transform.localPosition = Vector3.zero;
        visualRoot = visualRootObject.transform;

        GameObject bodyObject = new("Body");
        bodyObject.transform.SetParent(visualRoot, false);
        bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = sharedSprite;
        bodyRenderer.color = new Color(0.46f, 0.46f, 0.5f, 1f);
        bodyRenderer.sortingLayerName = "Default";
        bodyRenderer.sortingOrder = 19;
        bodyObject.transform.localScale = new Vector3(0.7f, 0.88f, 1f);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(bodyRenderer);
        CacheFacingMarkerDefaults();

        GameObject visionObject = new("VisionCone");
        visionObject.transform.SetParent(transform, false);
        visionObject.transform.localPosition = Vector3.zero;
        visionVisualizer = visionObject.AddComponent<EnemyVisionVisualizer>();
        LockPresentationToWorldAxes();
    }

    private void RebuildVentMarkers()
    {
        // These markers are temporary study aids so we can reason about vent placement.
        if (ventMarkerRoot != null)
        {
            Destroy(ventMarkerRoot.gameObject);
            ventMarkerRoot = null;
        }

        if (ventConnectionRoot != null)
        {
            Destroy(ventConnectionRoot.gameObject);
            ventConnectionRoot = null;
        }

        if (!showVentMarkers || layout == null || nodes.Count == 0)
        {
            return;
        }

        EnsureSharedSprite();

        GameObject markerRootObject = new("VentMarkers");
        markerRootObject.transform.SetParent(layout.transform, false);
        ventMarkerRoot = markerRootObject.transform;

        GameObject connectionRootObject = new("VentConnections");
        connectionRootObject.transform.SetParent(layout.transform, false);
        ventConnectionRoot = connectionRootObject.transform;

        for (int index = 0; index < nodes.Count; index++)
        {
            CreateVentMarker(nodes[index]);
        }

        CreateVentConnectionVisuals();
    }

    private void CreateVentMarker(VentNode node)
    {
        GameObject markerObject = new($"Vent_{node.NodeId}");
        markerObject.transform.SetParent(ventMarkerRoot, false);
        markerObject.transform.position = mapService.CellToWorldCenter(node.Cell);

        SpriteRenderer baseRenderer = markerObject.AddComponent<SpriteRenderer>();
        baseRenderer.sprite = sharedSprite;
        baseRenderer.color = node.IsCorridor
            ? new Color(0.22f, 0.26f, 0.3f, 1f)
            : new Color(0.18f, 0.2f, 0.22f, 1f);
        baseRenderer.sortingLayerName = "Default";
        baseRenderer.sortingOrder = 8;
        markerObject.transform.localScale = node.IsCorridor
            ? new Vector3(0.78f, 0.28f, 1f)
            : new Vector3(0.62f, 0.24f, 1f);

        for (int slatIndex = 0; slatIndex < 3; slatIndex++)
        {
            GameObject slatObject = new($"Slat_{slatIndex}");
            slatObject.transform.SetParent(markerObject.transform, false);
            slatObject.transform.localPosition = new Vector3(0f, (slatIndex - 1) * 0.08f, 0f);
            slatObject.transform.localScale = new Vector3(0.88f, 0.04f, 1f);

            SpriteRenderer slatRenderer = slatObject.AddComponent<SpriteRenderer>();
            slatRenderer.sprite = sharedSprite;
            slatRenderer.color = new Color(0.43f, 0.48f, 0.53f, 1f);
            slatRenderer.sortingLayerName = "Default";
            slatRenderer.sortingOrder = 9;
        }
    }

    private void CreateVentConnectionVisuals()
    {
        if (ventConnectionRoot == null)
        {
            return;
        }

        HashSet<ulong> createdConnections = new();

        foreach (KeyValuePair<int, List<int>> pair in adjacencyByNode)
        {
            int nodeId = pair.Key;

            if (pair.Value == null || nodeId < 0 || nodeId >= nodes.Count)
            {
                continue;
            }

            for (int index = 0; index < pair.Value.Count; index++)
            {
                int neighborId = pair.Value[index];

                if (neighborId < 0 || neighborId >= nodes.Count)
                {
                    continue;
                }

                int low = Mathf.Min(nodeId, neighborId);
                int high = Mathf.Max(nodeId, neighborId);
                ulong key = ((ulong)(uint)low << 32) | (uint)high;

                if (!createdConnections.Add(key))
                {
                    continue;
                }

                CreateVentConnection(nodes[low], nodes[high]);
            }
        }
    }

    private void CreateVentConnection(VentNode fromNode, VentNode toNode)
    {
        GameObject lineObject = new($"VentLink_{fromNode.NodeId}_{toNode.NodeId}");
        lineObject.transform.SetParent(ventConnectionRoot, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = 2;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 2;
        lineRenderer.widthMultiplier = 0.09f;
        lineRenderer.sharedMaterial = GetSharedDebugLineMaterial();
        lineRenderer.sortingOrder = 7;

        Color connectionColor = (fromNode.IsCorridor && toNode.IsCorridor)
            ? new Color(0.22f, 0.92f, 1f, 0.62f)
            : new Color(1f, 0.88f, 0.34f, 0.58f);
        lineRenderer.startColor = connectionColor;
        lineRenderer.endColor = connectionColor;
        lineRenderer.SetPosition(0, mapService.CellToWorldCenter(fromNode.Cell));
        lineRenderer.SetPosition(1, mapService.CellToWorldCenter(toNode.Cell));
    }

    private void SetVisualsVisible(bool visible)
    {
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(visible);
        }

        if (visionVisualizer != null)
        {
            visionVisualizer.SetPresentationVisible(visible);
            visionVisualizer.enabled = visible;
            visionVisualizer.gameObject.SetActive(visible);
        }

        if (!visible && visionSensor != null)
        {
            visionSensor.Configure(transform, 0f, 0f, mapService != null ? mapService.VisionBlockingLayers : default);
        }

        if (hitbox != null)
        {
            hitbox.enabled = visible;
        }

        if (!visible)
        {
            disruptionController?.HideStunEffect();
        }
    }

    private void EnsureAnimationDriver()
    {
        animationDriver = GetComponent<VentEnemySpriteAnimationDriver>();

        if (animationDriver == null)
        {
            animationDriver = gameObject.AddComponent<VentEnemySpriteAnimationDriver>();
        }

        animationDriver.ConfigureDependencies(
            this,
            GetComponent<CeilingVentEnemyPrefabBindings>(),
            bodyRenderer,
            Resources.Load<VentEnemySpriteProfile>(VentProfileResourcePath));
        animationDriver.ResolveReferences();
    }

    public bool TryApplyStun(float duration)
    {
        if (duration <= 0f || currentState != VentEnemyState.Emerged)
        {
            return false;
        }

        return disruptionController != null && disruptionController.TryApplyStun(
            duration,
            onStunApplied: () =>
            {
                emergedUntilTime = Mathf.Max(emergedUntilTime, disruptionController.StunUntilTime);
                emergedGroundPath.Clear();
                nextEmergedRepathTime = 0f;
            });
    }

    private void ApplyFacing()
    {
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 normalizedFacing = facingDirection.normalized;
        float facingAngle = Mathf.Atan2(normalizedFacing.y, normalizedFacing.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, facingAngle);
        LockPresentationToWorldAxes();
    }

    private void LockPresentationToWorldAxes()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (visualRoot.parent == transform)
        {
            visualRoot.localRotation = Quaternion.Inverse(transform.localRotation);
            return;
        }

        visualRoot.rotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
    }

    private void DetachVisionVisualizerFromPresentationRoot()
    {
        if (visionVisualizer == null || visualRoot == null || visionVisualizer.transform.parent != visualRoot)
        {
            return;
        }

        bool shouldRemainActiveSelf = visionVisualizer.gameObject.activeSelf;

        if (!visualRoot.gameObject.activeSelf && shouldRemainActiveSelf)
        {
            shouldRemainActiveSelf = false;
        }

        visionVisualizer.transform.SetParent(transform, false);
        visionVisualizer.transform.localPosition = Vector3.zero;
        visionVisualizer.transform.localRotation = Quaternion.identity;
        visionVisualizer.transform.localScale = Vector3.one;
        visionVisualizer.gameObject.SetActive(shouldRemainActiveSelf);
    }

    private void UpdateVisual(VisionSensor2D.VisionReading reading)
    {
        CacheFacingMarkerDefaults();

        if (bodyRenderer != null)
        {
            Color bodyColor = reading.CanSee
                ? new Color(0.92f, 0.56f, 0.56f, 1f)
                : new Color(0.46f, 0.46f, 0.5f, 1f);

            if (!reading.CanSee && emergedDetectionMeter > 0.001f)
            {
                bodyColor = Color.Lerp(bodyColor, new Color(1f, 0.68f, 0.24f, 1f), Mathf.Clamp01(emergedDetectionMeter * 0.24f));
            }

            if (IsAttackRecovering)
            {
                float recoveryBlend = 0.42f + Mathf.PingPong(Time.time * 5.5f, 0.26f);
                bodyColor = Color.Lerp(bodyColor, new Color(1f, 0.84f, 0.46f, 1f), recoveryBlend);
            }

            bodyRenderer.color = bodyColor;
        }

        if (facingMarkerRenderer != null)
        {
            Color markerColor = reading.CanSee
                ? new Color(1f, 0.96f, 0.66f, 1f)
                : new Color(0.62f, 0.12f, 0.12f, 1f);

            float markerScaleMultiplier = 1f;
            int markerSortingOrder = hasFacingMarkerDefaults ? facingMarkerBaseSortingOrder : facingMarkerRenderer.sortingOrder;

            if (reading.CanSee)
            {
                float pulse = 1f + Mathf.PingPong(Time.time * recognizedGlowPulseSpeed, recognizedGlowPulseStrength);
                markerColor = Color.Lerp(recognizedGlowColor, Color.white, 0.62f) * pulse;
                markerColor.a = 1f;
                markerScaleMultiplier = recognizedGlowScaleMultiplier * pulse;
                markerSortingOrder = Mathf.Max(markerSortingOrder, recognizedGlowSortingOrder);
            }
            else if (emergedDetectionMeter > 0.001f)
            {
                float partialBlend = Mathf.Clamp01(emergedDetectionMeter * 0.86f);
                markerColor = Color.Lerp(markerColor, recognizedGlowColor, partialBlend);
                markerScaleMultiplier = Mathf.Lerp(1f, 1.7f, partialBlend);
            }

            if (IsAttackRecovering)
            {
                markerColor = Color.Lerp(markerColor, new Color(1f, 0.92f, 0.7f, 1f), 0.65f);
            }

            facingMarkerRenderer.color = markerColor;
            facingMarkerRenderer.sortingOrder = markerSortingOrder;
            facingMarkerRenderer.transform.localScale = facingMarkerBaseScale * markerScaleMultiplier;
        }
    }

    private void CacheFacingMarkerDefaults()
    {
        if (facingMarkerRenderer == null || hasFacingMarkerDefaults)
        {
            return;
        }

        facingMarkerBaseScale = facingMarkerRenderer.transform.localScale;
        facingMarkerBaseSortingOrder = facingMarkerRenderer.sortingOrder;
        hasFacingMarkerDefaults = true;
    }

    private void UpdateThreatFeedback(bool hasConfirmedVisual)
    {
        if (hasConfirmedVisual)
        {
            threatFeedbackUntil = Time.time + Mathf.Max(0f, threatFeedbackHoldDuration);
        }
    }

    private void ConfigureDisruptionController()
    {
        disruptionController ??= GetComponent<EnemyDisruptionController>();

        if (disruptionController == null)
        {
            disruptionController = gameObject.AddComponent<EnemyDisruptionController>();
        }

        disruptionController.Configure(
            bodyRenderer,
            facingMarkerRenderer,
            visionVisualizer,
            configuredStunAnchor: bodyRenderer != null ? bodyRenderer.transform : transform,
            configuredStunSortingOrder: bodyRenderer != null ? bodyRenderer.sortingOrder + 6 : 26,
            configuredStunHeightOffset: 1.05f,
            configuredStunBodyColor: new Color(0.58f, 0.74f, 1f, 1f),
            configuredStunMarkerColor: new Color(0.92f, 0.98f, 1f, 1f));
    }

    private void ConfigurePlayerInteractionController()
    {
        playerInteractionController ??= GetComponent<EnemyPlayerInteractionController>();

        if (playerInteractionController == null)
        {
            playerInteractionController = gameObject.AddComponent<EnemyPlayerInteractionController>();
        }

        playerInteractionController.Configure(playerTarget);
    }

    private void ConfigureSightMemoryController()
    {
        sightMemoryController ??= GetComponent<EnemySightMemoryController>();

        if (sightMemoryController == null)
        {
            sightMemoryController = gameObject.AddComponent<EnemySightMemoryController>();
        }

        sightMemoryController.Configure(playerTarget, mapService);
    }

    private void ConfigureNoiseListenerController()
    {
        noiseListenerController ??= GetComponent<EnemyNoiseListenerController>();

        if (noiseListenerController == null)
        {
            noiseListenerController = gameObject.AddComponent<EnemyNoiseListenerController>();
        }

        noiseListenerController.Configure(instanceId, ResolveNoiseEventBus());
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private void ConfigureNoiseRetargetController()
    {
        noiseRetargetController ??= GetComponent<EnemyNoiseRetargetController>();

        if (noiseRetargetController == null)
        {
            noiseRetargetController = gameObject.AddComponent<EnemyNoiseRetargetController>();
        }

        noiseRetargetController.Clear();
    }

    private void ConfigureActivityWatchdog()
    {
        activityWatchdog ??= GetComponent<EnemyActivityWatchdogController>();

        if (activityWatchdog == null)
        {
            activityWatchdog = gameObject.AddComponent<EnemyActivityWatchdogController>();
        }

        activityWatchdog.Configure(inactivityRecoveryTimeout);
    }

    private Vector3Int GetLastSeenPlayerCell()
    {
        return sightMemoryController != null ? sightMemoryController.LastSeenCell : mapService.WorldToCell(transform.position);
    }

    private float GetLastSeenPlayerTime()
    {
        return sightMemoryController != null ? sightMemoryController.LastSeenTime : float.NegativeInfinity;
    }

    private void CheckActivityTimeout()
    {
        if (activityWatchdog == null)
        {
            return;
        }

        bool shouldMonitor = currentState == VentEnemyState.Emerged || currentTravelIntent != VentTravelIntent.None;

        if (shouldMonitor && activityWatchdog.TryConsumeTimeout(transform.position))
        {
            RecoverFromInactivity();
        }
    }

    private void RecoverFromInactivity()
    {
        if (currentState == VentEnemyState.Emerged)
        {
            LogDebug($"No meaningful activity detected for {inactivityRecoveryTimeout:0.0}s while emerged. Retreating back into vents.");
            BeginHiddenRetreat("activity watchdog recovery");
            activityWatchdog?.ResetWatchdog(transform.position);
            return;
        }

        LogDebug($"No meaningful activity detected for {inactivityRecoveryTimeout:0.0}s in vent travel. Returning to ambient crawl.");
        currentNodePath.Clear();
        currentPathIndex = 0;
        currentTravelIntent = VentTravelIntent.None;
        ClearNoiseResponseTarget();

        if (followPlayerInVentNetwork)
        {
            nextPlayerFollowRetargetTime = Time.time;
            TryUpdatePlayerVentFollow(forceRepath: true);
        }
        else
        {
            nextAmbientRoamTime = Time.time;
            StartAmbientRoam(immediate: true);
        }

        activityWatchdog?.ResetWatchdog(transform.position);
    }

    private VentEnemyAudioDriver TryGetAudioDriver()
    {
        audioDriver ??= GetComponent<VentEnemyAudioDriver>();
        return audioDriver;
    }

    private static void EnsureSharedSprite()
    {
        if (sharedSprite != null)
        {
            return;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        sharedSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private static Material GetSharedDebugLineMaterial()
    {
        if (sharedDebugLineMaterial != null)
        {
            return sharedDebugLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        sharedDebugLineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedDebugLineMaterial;
    }

    private void RegisterRoomNode(int roomId, int nodeId)
    {
        if (!roomNodeIdsByRoom.TryGetValue(roomId, out List<int> nodeIds))
        {
            nodeIds = new List<int>();
            roomNodeIdsByRoom[roomId] = nodeIds;
        }

        nodeIds.Add(nodeId);
    }

    private int ResolveCustomRoomId(Vector3Int cell, int preferredRoomId)
    {
        if (preferredRoomId >= 0)
        {
            return preferredRoomId;
        }

        return TryFindContainingRoom(cell, out GeneratedRoomData roomData)
            ? roomData.RoomId
            : -1;
    }

    private int AddNode(Vector3Int cell, bool isCorridor, int roomId, bool allowsSurfaceAccess = true)
    {
        int nodeId = nodes.Count;
        nodes.Add(new VentNode(nodeId, cell, isCorridor, roomId, allowsSurfaceAccess));
        adjacencyByNode[nodeId] = new List<int>();
        return nodeId;
    }

    private void ConnectNodes(int a, int b)
    {
        if (a < 0 || b < 0 || a == b)
        {
            return;
        }

        if (!adjacencyByNode.TryGetValue(a, out List<int> aLinks))
        {
            aLinks = new List<int>();
            adjacencyByNode[a] = aLinks;
        }

        if (!adjacencyByNode.TryGetValue(b, out List<int> bLinks))
        {
            bLinks = new List<int>();
            adjacencyByNode[b] = bLinks;
        }

        if (!aLinks.Contains(b))
        {
            aLinks.Add(b);
        }

        if (!bLinks.Contains(a))
        {
            bLinks.Add(a);
        }
    }

    private int GetNearestRoomNode(int roomId, Vector3Int nearCell)
    {
        if (!roomNodeIdsByRoom.TryGetValue(roomId, out List<int> nodeIds) || nodeIds.Count == 0)
        {
            return -1;
        }

        int bestNodeId = nodeIds[0];
        float bestDistance = float.MaxValue;
        Vector3 nearWorld = mapService.CellToWorldCenter(nearCell);

        foreach (int nodeId in nodeIds)
        {
            float distance = Vector2.Distance(nearWorld, mapService.CellToWorldCenter(nodes[nodeId].Cell));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNodeId = nodeId;
            }
        }

        return bestNodeId;
    }

    private int FindNoiseResponseNode(Vector2 worldPosition)
    {
        // Prefer the same room or corridor as the sound source. This makes the vent enemy
        // feel like it is truly homing in on where the player made noise.
        Vector3Int noiseCell = mapService.WorldToCell(worldPosition);

        if (TryFindContainingRoom(noiseCell, out GeneratedRoomData containingRoom))
        {
            int roomNodeId = GetNearestRoomNode(containingRoom.RoomId, noiseCell);

            if (CanUseNodeAsSurfaceAccess(roomNodeId))
            {
                return roomNodeId;
            }
        }

        int corridorNodeId = FindNearestCorridorNode(worldPosition, surfaceAccessOnly: true);

        if (corridorNodeId >= 0)
        {
            return corridorNodeId;
        }

        List<NodeDistance> candidates = new();

        for (int index = 0; index < nodes.Count; index++)
        {
            if (!CanUseNodeAsSurfaceAccess(index))
            {
                continue;
            }

            float distance = Vector2.Distance(worldPosition, mapService.CellToWorldCenter(nodes[index].Cell));
            candidates.Add(new NodeDistance(index, distance));
        }

        if (candidates.Count == 0)
        {
            return -1;
        }

        candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));

        int candidateWindow = Mathf.Clamp(candidates.Count, 1, 5);
        int selectedIndex = random.Next(0, candidateWindow);
        return candidates[selectedIndex].NodeId;
    }

    private int FindPlayerFollowNode(Vector2 worldPosition)
    {
        if (mapService == null || nodes.Count == 0)
        {
            return -1;
        }

        Vector3Int playerCell = mapService.WorldToCell(worldPosition);

        if (TryFindContainingRoom(playerCell, out GeneratedRoomData containingRoom))
        {
            int roomNodeId = GetNearestRoomNode(containingRoom.RoomId, playerCell);

            if (CanUseNodeAsSurfaceAccess(roomNodeId))
            {
                return roomNodeId;
            }
        }

        int corridorNodeId = FindNearestCorridorNode(worldPosition, surfaceAccessOnly: true);

        if (corridorNodeId >= 0)
        {
            return corridorNodeId;
        }

        int bestNodeId = -1;
        float bestDistance = float.MaxValue;

        for (int index = 0; index < nodes.Count; index++)
        {
            if (!CanUseNodeAsSurfaceAccess(index))
            {
                continue;
            }

            float distance = Vector2.Distance(worldPosition, mapService.CellToWorldCenter(nodes[index].Cell));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNodeId = index;
            }
        }

        return bestNodeId;
    }

    private bool CanUseNodeAsSurfaceAccess(int nodeId)
    {
        return nodeId >= 0
            && nodeId < nodes.Count
            && nodes[nodeId].AllowsSurfaceAccess;
    }

    private int FindNearestSurfaceAccessNode(Vector2 worldPosition)
    {
        int bestNodeId = -1;
        float bestDistance = float.MaxValue;

        for (int index = 0; index < nodes.Count; index++)
        {
            if (!CanUseNodeAsSurfaceAccess(index))
            {
                continue;
            }

            float distance = Vector2.Distance(worldPosition, mapService.CellToWorldCenter(nodes[index].Cell));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNodeId = index;
            }
        }

        return bestNodeId;
    }

    private int GetNodeDegree(int nodeId)
    {
        return adjacencyByNode.TryGetValue(nodeId, out List<int> neighbors) ? neighbors.Count : 0;
    }

    private bool TryFindContainingRoom(Vector3Int cell, out GeneratedRoomData roomData)
    {
        if (layout != null)
        {
            foreach (GeneratedRoomData room in layout.Rooms)
            {
                if (room.Bounds.Contains(new Vector2Int(cell.x, cell.y)))
                {
                    roomData = room;
                    return true;
                }
            }
        }

        roomData = default;
        return false;
    }

    private int FindNearestCorridorNode(Vector2 worldPosition, bool surfaceAccessOnly = false)
    {
        int bestNodeId = -1;
        float bestDistance = float.MaxValue;

        for (int index = 0; index < nodes.Count; index++)
        {
            if (!nodes[index].IsCorridor)
            {
                continue;
            }

            if (surfaceAccessOnly && !CanUseNodeAsSurfaceAccess(index))
            {
                continue;
            }

            float distance = Vector2.Distance(worldPosition, mapService.CellToWorldCenter(nodes[index].Cell));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNodeId = index;
            }
        }

        return bestNodeId;
    }

    private int FindFarthestNodeFrom(Vector3Int originCell)
    {
        int bestNodeId = -1;
        float bestDistance = float.MinValue;
        Vector3 originWorld = mapService.CellToWorldCenter(originCell);

        for (int index = 0; index < nodes.Count; index++)
        {
            if (!CanUseNodeAsSurfaceAccess(index))
            {
                continue;
            }

            float distance = Vector2.Distance(originWorld, mapService.CellToWorldCenter(nodes[index].Cell));

            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestNodeId = index;
            }
        }

        if (bestNodeId >= 0)
        {
            return bestNodeId;
        }

        bestNodeId = 0;
        bestDistance = float.MinValue;

        for (int index = 0; index < nodes.Count; index++)
        {
            float distance = Vector2.Distance(originWorld, mapService.CellToWorldCenter(nodes[index].Cell));

            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestNodeId = index;
            }
        }

        return bestNodeId;
    }

    private bool TryBuildNodePath(int startNodeId, int targetNodeId, List<int> pathBuffer)
    {
        pathBuffer.Clear();

        if (startNodeId < 0 || targetNodeId < 0)
        {
            return false;
        }

        if (startNodeId == targetNodeId)
        {
            pathBuffer.Add(startNodeId);
            return true;
        }

        nodePathFrontier.Clear();
        previousByNodeScratch.Clear();
        nodePathFrontier.Enqueue(startNodeId);
        previousByNodeScratch[startNodeId] = -1;

        while (nodePathFrontier.Count > 0)
        {
            int nodeId = nodePathFrontier.Dequeue();

            if (nodeId == targetNodeId)
            {
                break;
            }

            if (!adjacencyByNode.TryGetValue(nodeId, out List<int> links))
            {
                continue;
            }

            foreach (int neighborId in links)
            {
                if (previousByNodeScratch.ContainsKey(neighborId))
                {
                    continue;
                }

                previousByNodeScratch[neighborId] = nodeId;
                nodePathFrontier.Enqueue(neighborId);
            }
        }

        if (!previousByNodeScratch.ContainsKey(targetNodeId))
        {
            return false;
        }

        int currentNode = targetNodeId;

        while (currentNode >= 0)
        {
            pathBuffer.Add(currentNode);
            currentNode = previousByNodeScratch[currentNode];
        }

        pathBuffer.Reverse();
        return true;
    }

    private int FindReachableFallbackNode(Vector2 worldPosition)
    {
        if (currentNodeId < 0 || !adjacencyByNode.ContainsKey(currentNodeId))
        {
            return -1;
        }

        fallbackNodeFrontier.Clear();
        fallbackVisitedNodes.Clear();
        fallbackNodeFrontier.Enqueue(currentNodeId);
        fallbackVisitedNodes.Add(currentNodeId);

        int bestNodeId = CanUseNodeAsSurfaceAccess(currentNodeId) ? currentNodeId : -1;
        float bestDistance = bestNodeId >= 0
            ? Vector2.Distance(worldPosition, mapService.CellToWorldCenter(nodes[currentNodeId].Cell))
            : float.MaxValue;

        while (fallbackNodeFrontier.Count > 0)
        {
            int nodeId = fallbackNodeFrontier.Dequeue();

            if (CanUseNodeAsSurfaceAccess(nodeId))
            {
                float distance = Vector2.Distance(worldPosition, mapService.CellToWorldCenter(nodes[nodeId].Cell));

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNodeId = nodeId;
                }
            }

            if (!adjacencyByNode.TryGetValue(nodeId, out List<int> links))
            {
                continue;
            }

            foreach (int neighborId in links)
            {
                if (fallbackVisitedNodes.Add(neighborId))
                {
                    fallbackNodeFrontier.Enqueue(neighborId);
                }
            }
        }

        return bestNodeId;
    }

    private int GetActiveNoiseDestinationNodeId()
    {
        if (currentTravelIntent != VentTravelIntent.NoiseResponse)
        {
            return -1;
        }

        if (currentNodePath.Count > 0)
        {
            return currentNodePath[currentNodePath.Count - 1];
        }

        return noiseRetargetController != null ? noiseRetargetController.ActiveTargetId : -1;
    }

    private void ClearNoiseResponseTarget()
    {
        noiseRetargetController?.Clear();
    }

    private void EnsureAdjacencyCoverage()
    {
        for (int nodeId = 0; nodeId < nodes.Count; nodeId++)
        {
            if (!adjacencyByNode.ContainsKey(nodeId))
            {
                adjacencyByNode[nodeId] = new List<int>();
            }
        }
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log($"[VentEnemy:{gameObject.name}] {message}", this);
    }

    private string DescribeNode(int nodeId)
    {
        if (nodeId < 0 || nodeId >= nodes.Count)
        {
            return $"node {nodeId}";
        }

        VentNode node = nodes[nodeId];
        string areaLabel = node.IsCorridor ? "corridor" : $"room {node.RoomId}";
        return $"node {node.NodeId} {FormatCell(node.Cell)} [{areaLabel}]";
    }

    private static string FormatCell(Vector3Int cellPosition)
    {
        return $"({cellPosition.x}, {cellPosition.y})";
    }

    private static string FormatWorld(Vector2 worldPosition)
    {
        return $"({worldPosition.x:0.00}, {worldPosition.y:0.00})";
    }
}


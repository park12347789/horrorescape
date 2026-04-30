/*
 * File Role:
 * Implements the main ground enemy AI loop.
 *
 * Runtime Use:
 * Handles idle movement, hearing, chase, search, facing, door opening, and capture transitions.
 *
 * Study Notes:
 * This is one of the core study files because many systems meet here: pathfinding, vision, noise, and objectives.
 */

using System.Collections.Generic;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Investigate,
    Chase,
    Search
}

[DisallowMultipleComponent]
[RequireComponent(typeof(VisionSensor2D))]
public sealed class EnemyStateMachine : MonoBehaviour, IPlayerThreatFeedbackSource, IThrowableStunTarget, IEnemyPlayerSpotSource, IEnemyPassiveAudioStateSource
{
    private enum PatrolPattern
    {
        None,
        Observe,
        DirectionCheck
    }

    // The ground enemy is built as a small state machine. Each state owns its own update
    // logic, while shared systems like hearing and vision feed those state transitions.
    private static readonly Vector3Int[] CardinalOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    };
    private const float PathArrivalThreshold = 0.08f;

    [SerializeField] private EnemyArchetype archetype;
    [SerializeField] private GridMapService mapService;
    [SerializeField] private VisibilityTarget2D playerTarget;
    [SerializeField] private PlayerTrailRecorder playerTrail;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform visionOrigin;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer facingMarkerRenderer;
    [SerializeField] private EnemyVisionVisualizer visionVisualizer;
    [SerializeField] private EnemySpriteAnimationDriver spriteAnimationDriver;
    [SerializeField] private bool enableDebugLogs;
    [SerializeField] private Color recognizedGlowColor = new(1f, 0.1f, 0.04f, 1f);
    [SerializeField, Min(1f)] private float recognizedGlowScaleMultiplier = 3.05f;
    [SerializeField, Min(0f)] private float recognizedGlowPulseSpeed = 8.6f;
    [SerializeField, Range(0f, 1f)] private float recognizedGlowPulseStrength = 0.46f;
    [SerializeField] private int recognizedGlowSortingOrder = 152;
    [SerializeField, Min(1)] private int chaseRetargetCellThreshold = 2;
    [SerializeField, Min(0)] private int chasePathRefreshRemainingSteps = 1;
    [SerializeField, Min(0.1f)] private float searchAnchorScanDuration = 0.7f;
    [SerializeField, Range(10f, 180f)] private float searchAnchorScanSweepAngle = 90f;
    [SerializeField, Min(0.1f)] private float searchAnchorScanCyclesPerSecond = 0.9f;
    [SerializeField, Min(0.1f)] private float standGuardScoutDuration = 1.75f;
    [SerializeField, Min(1)] private int standGuardScoutRadius = 2;
    [SerializeField, Min(0.05f)] private float standGuardScoutPauseTime = 0.18f;
    [SerializeField] private Transform[] standGuardShiftPoints;
    [SerializeField, Min(0f)] private float standGuardShiftInterval = 8f;
    [SerializeField, Min(0f)] private float standGuardShiftGuardTime = 2.2f;
    [SerializeField, Min(1)] private int standGuardShiftRadius = 3;
    [SerializeField, Min(0f)] private float attackRecoveryDuration = 0.65f;
    [SerializeField, Min(0.5f)] private float inactivityRecoveryTimeout = 3.25f;
    [SerializeField] private bool useExposureForDetection = true;
    [SerializeField, Min(0f)] private float threatFeedbackHoldDuration = 1f;
    [SerializeField, Min(0.05f)] private float visualDetectionBuildDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float visualDetectionDecayDuration = 0.65f;
    [SerializeField, Range(0.1f, 1f)] private float confirmedDetectionThreshold = 0.95f;
    [SerializeField, Min(0.1f)] private float blockedPathRecoveryCooldown = 0.16f;
    [SerializeField, Min(0.15f)] private float temporaryBlockedCellMemoryDuration = 0.9f;
    [SerializeField, Min(0.05f)] private float collisionProbeRadius = 0.18f;
    [SerializeField, Min(0.01f)] private float pathProgressEpsilon = 0.0025f;
    [SerializeField, Min(0.2f)] private float movementNoiseRadius = 0.92f;
    [SerializeField, Min(0.05f)] private float movementNoiseInterval = 0.56f;
    [SerializeField, Min(0.05f)] private float movementNoiseTravelThreshold = 0.36f;
    [SerializeField, Range(1f, 2f)] private float chaseMovementNoiseRadiusMultiplier = 1.18f;
    [SerializeField, Min(0f)] private float failedPathCacheDuration = 0.08f;
    [SerializeField] private bool enableUnpredictablePatterns = true;
    [SerializeField, Range(0f, 1f)] private float suddenBurstChance = 0.18f;
    [SerializeField, Min(0.1f)] private float suddenBurstCooldown = 2.75f;
    [SerializeField, Min(0.05f)] private float suddenBurstDuration = 0.4f;
    [SerializeField, Range(1f, 3f)] private float suddenBurstSpeedMultiplier = 1.65f;
    [SerializeField, Range(1f, 3f)] private float suddenBurstNoiseRadiusMultiplier = 1.35f;
    [SerializeField, Range(0f, 1f)] private float threatCallChance = 0.24f;
    [SerializeField, Min(0.2f)] private float threatCallInterval = 1.45f;
    [SerializeField, Min(0f)] private float threatCallRadius = 2.35f;
    [SerializeField] private NoiseSourceType threatCallNoiseType = NoiseSourceType.Interact;
    [SerializeField, Range(0f, 1f)] private float trailVariantChance = 0.32f;
    [SerializeField, Min(0.25f)] private float trailVariantCooldown = 1.2f;
    [SerializeField, Min(1)] private int trailLeapStepCount = 2;
    [SerializeField, Min(1)] private int trailOffsetSearchRadius = 1;
    [SerializeField, Min(0)] private int trailRecentTargetWindow = 24;
    [SerializeField, Min(2)] private int trailLeadSampleCount = 6;
    [SerializeField, Min(0)] private int trailLeadPredictionStepCount = 3;
    [SerializeField, Range(0f, 1f)] private float patrolObserveChance = 0.28f;
    [SerializeField, Range(0f, 1f)] private float patrolDirectionCheckChance = 0.22f;
    [SerializeField, Min(0f)] private float patrolPatternCooldown = 1.25f;
    [SerializeField, Min(0.05f)] private float patrolObserveDurationMin = 0.45f;
    [SerializeField, Min(0.05f)] private float patrolObserveDurationMax = 1.1f;
    [SerializeField, Min(0.05f)] private float patrolDirectionCheckDurationMin = 0.5f;
    [SerializeField, Min(0.05f)] private float patrolDirectionCheckDurationMax = 0.9f;
    [SerializeField, Min(0.05f)] private float patrolDirectionCheckStepTime = 0.22f;
    [SerializeField] private NoiseSystem noiseSystem;
    [SerializeField] private EnemyState currentState;

    private readonly List<Vector3Int> currentPath = new();
    private readonly List<Vector3Int> patrolRoute = new();
    private readonly List<Vector3Int> searchTargets = new();
    private readonly List<Vector3Int> standGuardScoutRoute = new();
    private readonly List<Vector3Int> standGuardShiftRoute = new();
    private readonly List<Vector3Int> pathScratch = new();
    private readonly List<Vector3Int> repathScratch = new();
    private readonly List<Vector3Int> idleCandidateCells = new();
    private readonly Collider2D[] collisionProbeResults = new Collider2D[8];
    private readonly Queue<Vector3Int> idleCandidateFrontier = new();
    private readonly Dictionary<Vector3Int, int> idleCandidateDistanceByCell = new();
    private readonly List<Vector3Int> idleCandidateFallbackCells = new();
    private VisionSensor2D visionSensor;
    private Vector3Int investigateCell;
    private float patrolPauseUntil;
    private float nextRepathTime;
    private int patrolIndex;
    private int searchIndex;
    private float searchAnchorScanStartTime;
    private float searchEndTime;
    private float standGuardScoutUntil;
    private float standGuardNextShiftTime;
    private float standGuardShiftGuardUntil;
    private Vector2 facingDirection = Vector2.up;
    private Vector3Int lastIdleTargetCell;
    private Vector3Int idleAnchorCell;
    private Vector3Int activeChaseTargetCell;
    private Vector3Int searchAnchorCell;
    private Vector2 searchAnchorBaseFacing = Vector2.up;
    private bool hasIdleTargetCell;
    private bool hasActiveChaseTargetCell;
    private bool hasSearchAnchorCell;
    private bool isSearchAnchorScanActive;
    private bool isStandGuardScoutActive;
    private bool isStandGuardShiftActive;
    private int trailProgressIndex;
    private int followTrailPathProgressIndex;
    private int standGuardScoutIndex;
    private int standGuardShiftIndex;
    private int standGuardShiftCandidateCursor;
    private bool trailInitialized;
    private bool hasFollowTrailPathTargetCell;
    private Vector3Int followTrailPathTargetCell;
    private float visualDetectionMeter;
    private float threatFeedbackUntil = float.NegativeInfinity;
    private Vector3 facingMarkerBaseScale = Vector3.one;
    private int facingMarkerBaseSortingOrder;
    private bool hasFacingMarkerDefaults;
    private CircleCollider2D throwableHitbox;
    private EnemyDisruptionController disruptionController;
    private EnemyPlayerInteractionController playerInteractionController;
    private EnemySightMemoryController sightMemoryController;
    private EnemyNoiseListenerController noiseListenerController;
    private INoiseEventBus noiseEventBus;
    private EnemyActivityWatchdogController activityWatchdog;
    private bool hasTemporarilyBlockedCell;
    private Vector3Int temporarilyBlockedCell;
    private float temporarilyBlockedCellUntil;
    private float nextBlockedPathRecoveryTime;
    private float nextMovementNoiseTime;
    private Vector2 lastMovementNoisePosition;
    private bool hasMovementNoiseAnchor;
    private float suddenBurstUntil = float.NegativeInfinity;
    private float nextSuddenBurstEvaluationTime;
    private float nextThreatCallEvaluationTime;
    private float nextTrailVariantEvaluationTime;
    private PatrolPattern activePatrolPattern = PatrolPattern.None;
    private float activePatrolPatternUntil = float.NegativeInfinity;
    private float nextPatrolPatternEvaluationTime;
    private float nextPatrolDirectionCheckTime;
    private int patrolDirectionCheckIndex;
    private Transform cachedVisionOrigin;
    private float cachedVisionDistance;
    private float cachedVisionAngle;
    private int cachedVisionBlockingLayerMask;
    private bool cachedVisionUsesExposureMultiplier;
    private bool hasCachedVisionSensorConfiguration;
    private string cachedVisionVisualizerSortingLayerName;
    private int cachedVisionVisualizerSortingOrder;
    private bool hasCachedVisionVisualizerConfiguration;
    private float lastAppliedFacingAngle;
    private bool hasAppliedFacing;
    private EnemyState cachedVisualState;
    private bool cachedVisualCanSee;
    private bool cachedVisualAttackRecovering;
    private int cachedVisualDetectionBucket;
    private bool hasCachedVisualState;
    private Vector3Int lastFailedPathStartCell;
    private Vector3Int lastFailedPathTargetCell;
    private float lastFailedPathUntil;
    private bool hasFailedPathCache;
    private Vector3 exactStandGuardAnchorWorldPosition;
    private bool hasExactStandGuardAnchor;
    private Vector2 investigateFocusWorldPosition;
    private bool hasInvestigateFocusWorldPosition;

    public event System.Action PlayerSpotted;

    public EnemyState CurrentState => currentState;
    public bool IsStunned => disruptionController != null && disruptionController.IsStunned;
    public bool IsAttackRecovering => disruptionController != null && disruptionController.IsAttackRecovering;
    public bool IsConfirmedThreat => !IsStunned && Time.time <= threatFeedbackUntil;
    public bool IsActivelyPursuingPlayer => !IsStunned && currentState == EnemyState.Chase;
    public bool ShouldPlayPassiveAmbientAudio => !IsStunned && currentState == EnemyState.Idle;
    public bool ShouldForceThreatFeedbackVisible => !IsStunned && (Time.time <= threatFeedbackUntil || visualDetectionMeter >= 0.58f);
    public float ThreatIntensityNormalized => IsStunned
        ? 0f
        : Mathf.Clamp01(IsConfirmedThreat ? Mathf.Max(0.8f, visualDetectionMeter) : 0f);
    public Vector3 ThreatWorldPosition => transform.position;
    public SpriteRenderer ThreatMarkerRenderer => facingMarkerRenderer;
    public bool CanBeStunnedByThrowable => isActiveAndEnabled && !IsStunned;
    public Vector3 ThrowableStunAimPoint => ResolveThrowableTargetBounds().center;
    public float ThrowableStunHitRadius => ResolveThrowableTargetRadius();

    public void Configure(
        EnemyArchetype configuredArchetype,
        GridMapService configuredMapService,
        VisibilityTarget2D configuredPlayerTarget,
        Transform configuredVisualRoot,
        Transform configuredVisionOrigin,
        SpriteRenderer configuredBodyRenderer,
        SpriteRenderer configuredFacingMarkerRenderer,
        EnemyVisionVisualizer configuredVisionVisualizer,
        PlayerTrailRecorder configuredPlayerTrail = null,
        params Vector3Int[] configuredPatrolRoute)
    {
        // Configure wires together runtime-created scene objects, then resets the enemy
        // into a clean starting state.
        archetype = configuredArchetype;
        mapService = configuredMapService;
        playerTarget = configuredPlayerTarget;
        playerTrail = configuredPlayerTrail;
        visualRoot = configuredVisualRoot;
        visionOrigin = configuredVisionOrigin;
        bodyRenderer = configuredBodyRenderer;
        facingMarkerRenderer = configuredFacingMarkerRenderer;
        visionVisualizer = configuredVisionVisualizer;
        CacheFacingMarkerDefaults();
        patrolRoute.Clear();

        if (configuredPatrolRoute != null)
        {
            patrolRoute.AddRange(configuredPatrolRoute);
        }

        visionSensor = GetComponent<VisionSensor2D>();
        hasCachedVisionSensorConfiguration = false;
        hasCachedVisionVisualizerConfiguration = false;
        RefreshVisionConfiguration();

        ConfigureDisruptionController();
        ConfigurePlayerInteractionController();
        ConfigureSightMemoryController();
        ConfigureNoiseListenerController();
        ConfigureActivityWatchdog();

        facingDirection = Vector2.up;
        idleAnchorCell = mapService != null ? mapService.ResolveNearestWalkableCell(transform.position, 1, true) : Vector3Int.zero;
        exactStandGuardAnchorWorldPosition = transform.position;
        hasExactStandGuardAnchor = false;
        hasInvestigateFocusWorldPosition = false;
        hasIdleTargetCell = false;
        hasActiveChaseTargetCell = false;
        standGuardScoutRoute.Clear();
        standGuardScoutUntil = 0f;
        isStandGuardScoutActive = false;
        standGuardScoutIndex = 0;
        ResetStandGuardShiftPattern(scheduleNextShift: true);
        BuildAutoPatrolRouteIfNeeded();
        trailProgressIndex = 0;
        followTrailPathProgressIndex = 0;
        trailInitialized = false;
        hasFollowTrailPathTargetCell = false;
        visualDetectionMeter = 0f;
        hasCachedVisualState = false;
        hasAppliedFacing = false;
        hasFailedPathCache = false;
        threatFeedbackUntil = float.NegativeInfinity;
        lastMovementNoisePosition = transform.position;
        hasMovementNoiseAnchor = true;
        nextMovementNoiseTime = Time.time + (movementNoiseInterval * 0.35f);
        suddenBurstUntil = float.NegativeInfinity;
        nextSuddenBurstEvaluationTime = Time.time + (suddenBurstCooldown * 0.65f);
        nextThreatCallEvaluationTime = Time.time + (threatCallInterval * 0.85f);
        nextTrailVariantEvaluationTime = Time.time + (trailVariantCooldown * 0.75f);
        StopActivePatrolPattern();
        nextPatrolPatternEvaluationTime = Time.time + (Mathf.Max(0f, patrolPatternCooldown) * 0.35f);
        nextPatrolDirectionCheckTime = 0f;
        patrolDirectionCheckIndex = 0;
        ApplyFacing();
        currentState = EnemyState.Idle;
        patrolIndex = 0;
        patrolPauseUntil = Time.time + (archetype != null ? archetype.PatrolWaitTime : 0.5f);
    }

    public void SetFacingDirection(Vector2 configuredFacingDirection)
    {
        if (configuredFacingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        facingDirection = configuredFacingDirection.normalized;
        searchAnchorBaseFacing = facingDirection;
        ApplyFacing();
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
        noiseListenerController?.ConfigureNoiseEventBus(configuredNoiseEventBus);
    }

    public void ConfigureExactStandGuardAnchor(Vector3 worldPosition)
    {
        exactStandGuardAnchorWorldPosition = worldPosition;
        exactStandGuardAnchorWorldPosition.z = transform.position.z;
        hasExactStandGuardAnchor = true;

        if (mapService != null)
        {
            idleAnchorCell = mapService.WorldToCell(worldPosition);
        }

        currentPath.Clear();
        hasIdleTargetCell = false;
        lastIdleTargetCell = idleAnchorCell;
        ResetStandGuardShiftPattern(scheduleNextShift: true);
    }

    private void SetInvestigateFocus(Vector2 worldPosition)
    {
        investigateFocusWorldPosition = worldPosition;
        hasInvestigateFocusWorldPosition = true;
        _ = TryFaceInvestigateFocus();
    }

    private void ClearInvestigateFocus()
    {
        hasInvestigateFocusWorldPosition = false;
    }

    private bool TryFaceInvestigateFocus()
    {
        if (!hasInvestigateFocusWorldPosition || !IsStandGuardEnemy())
        {
            return false;
        }

        Vector2 toFocus = investigateFocusWorldPosition - (Vector2)transform.position;

        if (toFocus.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        facingDirection = toFocus.normalized;
        return true;
    }

    private void Awake()
    {
        visionSensor = GetComponent<VisionSensor2D>();
        throwableHitbox = GetComponent<CircleCollider2D>();
        disruptionController = GetComponent<EnemyDisruptionController>();
        playerInteractionController = GetComponent<EnemyPlayerInteractionController>();
        sightMemoryController = GetComponent<EnemySightMemoryController>();
        noiseListenerController = GetComponent<EnemyNoiseListenerController>();
        activityWatchdog = GetComponent<EnemyActivityWatchdogController>();
        CacheFacingMarkerDefaults();
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
        CircleCollider2D hitbox = throwableHitbox != null ? throwableHitbox : GetComponent<CircleCollider2D>();

        if (hitbox != null && hitbox.enabled)
        {
            bounds = hitbox.bounds;
            hasBounds = true;
        }

        if (bodyRenderer != null)
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

    private void RefreshVisionConfiguration()
    {
        if (visionSensor == null)
        {
            return;
        }

        Transform sensorOrigin = visionOrigin != null ? visionOrigin : transform;
        float visionDistance = archetype != null ? archetype.VisionDistance : 6f;
        float visionAngle = archetype != null ? archetype.VisionAngle : 55f;
        LayerMask blockingLayers = mapService != null ? mapService.VisionBlockingLayers : default;
        int blockingLayerMask = blockingLayers.value;

        bool sensorConfigurationChanged = !hasCachedVisionSensorConfiguration
            || cachedVisionOrigin != sensorOrigin
            || !Mathf.Approximately(cachedVisionDistance, visionDistance)
            || !Mathf.Approximately(cachedVisionAngle, visionAngle)
            || cachedVisionBlockingLayerMask != blockingLayerMask
            || cachedVisionUsesExposureMultiplier != useExposureForDetection;

        if (sensorConfigurationChanged)
        {
            visionSensor.Configure(sensorOrigin, visionDistance, visionAngle, blockingLayers);
            visionSensor.UseExposureMultiplier = useExposureForDetection;
            cachedVisionOrigin = sensorOrigin;
            cachedVisionDistance = visionDistance;
            cachedVisionAngle = visionAngle;
            cachedVisionBlockingLayerMask = blockingLayerMask;
            cachedVisionUsesExposureMultiplier = useExposureForDetection;
            hasCachedVisionSensorConfiguration = true;
        }

        if (visionVisualizer == null)
        {
            hasCachedVisionVisualizerConfiguration = false;
            return;
        }

        string sortingLayerName = bodyRenderer != null ? bodyRenderer.sortingLayerName : "Default";
        int sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder - 1 : 12;
        bool visualizerConfigurationChanged = sensorConfigurationChanged
            || !hasCachedVisionVisualizerConfiguration
            || cachedVisionOrigin != sensorOrigin
            || !Mathf.Approximately(cachedVisionDistance, visionDistance)
            || !Mathf.Approximately(cachedVisionAngle, visionAngle)
            || cachedVisionBlockingLayerMask != blockingLayerMask
            || cachedVisionVisualizerSortingLayerName != sortingLayerName
            || cachedVisionVisualizerSortingOrder != sortingOrder;

        if (!visualizerConfigurationChanged)
        {
            return;
        }

        visionVisualizer.Configure(
            visionDistance,
            visionAngle,
            sortingLayerName,
            sortingOrder,
            sensorOrigin,
            blockingLayers);
        cachedVisionVisualizerSortingLayerName = sortingLayerName;
        cachedVisionVisualizerSortingOrder = sortingOrder;
        hasCachedVisionVisualizerConfiguration = true;
    }

    private void Update()
    {
        // Update follows the same high-level order every frame:
        // 1. Refresh sensors.
        // 2. Read the player.
        // 3. Transition state if needed.
        // 4. Run the current state's behavior.
        // 5. Apply facing/visual feedback.
        if (archetype == null || mapService == null || playerTarget == null || visionSensor == null)
        {
            return;
        }

        if (playerInteractionController != null && playerInteractionController.IsPlayerCaught)
        {
            return;
        }

        if (IsStunned)
        {
            disruptionController?.UpdateWhileStunned(
                onWhileStunned: () =>
                {
                    currentPath.Clear();
                    activityWatchdog?.ResetPathTracking();
                },
                onAfterPresentation: ForceApplyFacing);
            return;
        }

        activityWatchdog?.BeginFrame(transform.position);
        disruptionController?.BeginNormalFrame();
        RefreshTemporaryBlockedCell();

        RefreshVisionConfiguration();

        VisionSensor2D.VisionReading rawPlayerReading = visionSensor.GetReading(playerTarget);
        VisionSensor2D.VisionReading playerReading = BuildConfirmedVisionReading(rawPlayerReading);
        UpdateThreatFeedback(playerReading.CanSee);
        EnemySightMemoryController.Observation sightObservation = sightMemoryController != null
            ? sightMemoryController.Observe(playerReading, transform.position)
            : default;

        if (playerReading.CanSee)
        {
            activityWatchdog?.ReportActivity();

            if (sightObservation.JustSpottedPlayer)
            {
                PlayerSpotted?.Invoke();

                if (enableDebugLogs)
                {
                    LogDebug($"Player spotted at {FormatCell(sightObservation.LastSeenCell)} (distance {playerReading.Distance:0.00}, angle {playerReading.Angle:0.0}).");
                }
            }

            if (currentState != EnemyState.Chase)
            {
                SetState(
                    EnemyState.Chase,
                    enableDebugLogs ? $"player visible at {FormatCell(sightObservation.LastSeenCell)}" : null);
            }
        }
        else
        {
            if (sightObservation.JustLostPlayer)
            {
                activityWatchdog?.ReportActivity();

                if (enableDebugLogs)
                {
                    LogDebug($"Lost sight of player. Moving to last seen cell {FormatCell(GetLastSeenPlayerCell())} for reacquisition.");
                }
            }
        }

        if (currentState != EnemyState.Chase && !rawPlayerReading.CanSee)
        {
            TryHandleLatestNoise();
        }

        bool canMoveOrAttack = !IsAttackRecovering;

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                if (canMoveOrAttack)
                {
                    MoveAlongPath(ResolveMovementSpeedWithPatterns(ResolveIdleMovementSpeed()));
                }

                break;
            case EnemyState.Investigate:
                UpdateInvestigate();
                if (canMoveOrAttack)
                {
                    MoveAlongPath(ResolveMovementSpeedWithPatterns(archetype.InvestigateSpeed));
                }

                break;
            case EnemyState.Chase:
                UpdateChase(playerReading);
                if (canMoveOrAttack)
                {
                    MoveAlongPath(ResolveMovementSpeedWithPatterns(archetype.ChaseSpeed));
                }

                break;
            case EnemyState.Search:
                UpdateSearch();
                if (canMoveOrAttack)
                {
                    MoveAlongPath(ResolveMovementSpeedWithPatterns(archetype.InvestigateSpeed));
                }

                break;
        }

        if (!playerReading.CanSee)
        {
            ResolveLostPlayerAfterChase();
        }

        if (canMoveOrAttack && TryCapturePlayer(playerReading))
        {
            return;
        }

        UpdateFacing(rawPlayerReading);
        ApplyFacing();
        UpdateVisual(playerReading);

        if (currentState != EnemyState.Idle
            && activityWatchdog != null
            && activityWatchdog.TryConsumeTimeout(transform.position))
        {
            RecoverFromInactivity();
        }
    }

    private void UpdateIdle()
    {
        // Idle is really a dispatcher for three idle styles: patrol, roam, and tracker
        // variants that follow the player's past trail.
        if (archetype != null && archetype.IdleBehavior == EnemyIdleBehavior.StandGuard)
        {
            UpdateIdleStandGuard();
            return;
        }

        if (archetype == null)
        {
            UpdateIdleWander();
            return;
        }

        if (archetype.IdleBehavior == EnemyIdleBehavior.FollowTrail)
        {
            UpdateIdleFollowTrail();
            return;
        }

        if (archetype.IdleBehavior == EnemyIdleBehavior.FollowTarget)
        {
            UpdateIdleFollowTarget();
            return;
        }

        if (!HasUsablePatrolRoute())
        {
            UpdateIdleWander();
            return;
        }

        Vector3Int patrolCell = patrolRoute[Mathf.Clamp(patrolIndex, 0, patrolRoute.Count - 1)];

        if (UpdateActivePatrolPattern())
        {
            return;
        }

        if (IsAtCell(patrolCell))
        {
            if (patrolPauseUntil <= 0f)
            {
                if (TryBeginPatrolWaypointPattern())
                {
                    return;
                }

                patrolPauseUntil = Time.time + archetype.PatrolWaitTime;
            }

            if (Time.time < patrolPauseUntil)
            {
                currentPath.Clear();
                return;
            }

            patrolPauseUntil = 0f;
            patrolIndex = (patrolIndex + 1) % patrolRoute.Count;
            patrolCell = patrolRoute[patrolIndex];
            RequestPathTo(patrolCell);
            return;
        }

        if (currentPath.Count == 0)
        {
            RequestPathTo(patrolCell);
        }
    }

    private bool UpdateActivePatrolPattern()
    {
        if (activePatrolPattern == PatrolPattern.None)
        {
            return false;
        }

        currentPath.Clear();
        activityWatchdog?.ResetPathTracking();

        if (!CanUseRoutePatrolPattern())
        {
            StopActivePatrolPattern();
            return false;
        }

        if (Time.time >= activePatrolPatternUntil)
        {
            StopActivePatrolPattern();
            return false;
        }

        if (activePatrolPattern == PatrolPattern.DirectionCheck
            && Time.time >= nextPatrolDirectionCheckTime)
        {
            ApplyNextPatrolDirectionCheckFacing();
            nextPatrolDirectionCheckTime = Time.time + Mathf.Max(0.05f, patrolDirectionCheckStepTime);
        }

        activityWatchdog?.ReportActivity(transform.position);
        return true;
    }

    private bool TryBeginPatrolWaypointPattern()
    {
        if (!enableUnpredictablePatterns
            || !CanUseRoutePatrolPattern()
            || IsStunned
            || IsAttackRecovering
            || Time.time < nextPatrolPatternEvaluationTime)
        {
            return false;
        }

        nextPatrolPatternEvaluationTime = Time.time + Mathf.Max(0f, patrolPatternCooldown);
        float observeWeight = Mathf.Clamp01(patrolObserveChance);
        float directionCheckWeight = Mathf.Clamp01(patrolDirectionCheckChance);
        float totalWeight = observeWeight + directionCheckWeight;

        if (totalWeight <= 0f || Random.value >= Mathf.Clamp01(totalWeight))
        {
            return false;
        }

        float patternRoll = Random.value * totalWeight;

        if (patternRoll < observeWeight)
        {
            BeginPatrolObservationPattern();
            return true;
        }

        if (directionCheckWeight > 0f)
        {
            BeginPatrolDirectionCheckPattern();
            return true;
        }

        return false;
    }

    private void BeginPatrolObservationPattern()
    {
        float duration = ResolvePatrolPatternDuration(patrolObserveDurationMin, patrolObserveDurationMax);
        BeginPatrolPattern(PatrolPattern.Observe, duration);
        LogDebug($"Patrol observe pause for {duration:0.00}s.");
    }

    private void BeginPatrolDirectionCheckPattern()
    {
        float duration = ResolvePatrolPatternDuration(patrolDirectionCheckDurationMin, patrolDirectionCheckDurationMax);
        BeginPatrolPattern(PatrolPattern.DirectionCheck, duration);
        patrolDirectionCheckIndex = Random.Range(0, CardinalOffsets.Length);
        ApplyNextPatrolDirectionCheckFacing();
        nextPatrolDirectionCheckTime = Time.time + Mathf.Max(0.05f, patrolDirectionCheckStepTime);
        LogDebug($"Patrol direction check for {duration:0.00}s.");
    }

    private void BeginPatrolPattern(PatrolPattern pattern, float duration)
    {
        activePatrolPattern = pattern;
        activePatrolPatternUntil = Time.time + Mathf.Max(0.05f, duration);
        patrolPauseUntil = activePatrolPatternUntil;
        currentPath.Clear();
        activityWatchdog?.ResetPathTracking();
        activityWatchdog?.ReportActivity(transform.position);
    }

    private void StopActivePatrolPattern()
    {
        activePatrolPattern = PatrolPattern.None;
        activePatrolPatternUntil = float.NegativeInfinity;
        nextPatrolDirectionCheckTime = 0f;
    }

    private void ApplyNextPatrolDirectionCheckFacing()
    {
        if (CardinalOffsets.Length == 0)
        {
            return;
        }

        Vector3Int offset = CardinalOffsets[patrolDirectionCheckIndex % CardinalOffsets.Length];
        patrolDirectionCheckIndex = (patrolDirectionCheckIndex + 1) % CardinalOffsets.Length;
        Vector2 checkDirection = new(offset.x, offset.y);

        if (checkDirection.sqrMagnitude > 0.0001f)
        {
            facingDirection = checkDirection.normalized;
        }
    }

    private float ResolvePatrolPatternDuration(float minimum, float maximum)
    {
        float resolvedMinimum = Mathf.Max(0.05f, minimum);
        float resolvedMaximum = Mathf.Max(resolvedMinimum, maximum);
        return resolvedMaximum > resolvedMinimum
            ? Random.Range(resolvedMinimum, resolvedMaximum)
            : resolvedMinimum;
    }

    private bool CanUseRoutePatrolPattern()
    {
        return currentState == EnemyState.Idle
            && archetype != null
            && archetype.IdleBehavior == EnemyIdleBehavior.Patrol
            && HasUsablePatrolRoute();
    }

    private void UpdateIdleStandGuard()
    {
        if (UpdateStandGuardScout())
        {
            return;
        }

        if (UpdateStandGuardShiftPattern())
        {
            return;
        }

        if (TryHoldExactStandGuardAnchor())
        {
            return;
        }

        if (mapService == null || !mapService.IsWalkable(idleAnchorCell, true))
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            return;
        }

        if (IsAtCell(idleAnchorCell))
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            lastIdleTargetCell = idleAnchorCell;
            return;
        }

        bool shouldRefreshPath = currentPath.Count == 0
            || !hasIdleTargetCell
            || lastIdleTargetCell != idleAnchorCell
            || Time.time >= nextRepathTime;

        if (!shouldRefreshPath)
        {
            return;
        }

        if (!RequestPathTo(idleAnchorCell) && !IsAtCell(idleAnchorCell))
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            nextRepathTime = Time.time + archetype.RepathInterval;
            return;
        }

        lastIdleTargetCell = idleAnchorCell;
        hasIdleTargetCell = true;
        nextRepathTime = Time.time + archetype.RepathInterval;
    }

    private bool TryHoldExactStandGuardAnchor()
    {
        if (!hasExactStandGuardAnchor || !IsStandGuardEnemy())
        {
            return false;
        }

        Vector2 currentPosition = transform.position;
        Vector2 anchorPosition = exactStandGuardAnchorWorldPosition;

        if (Vector2.Distance(currentPosition, anchorPosition) <= PathArrivalThreshold
            || (mapService != null && mapService.WorldToCell(transform.position) == idleAnchorCell))
        {
            transform.position = exactStandGuardAnchorWorldPosition;
            currentPath.Clear();
            hasIdleTargetCell = false;
            lastIdleTargetCell = idleAnchorCell;
            return true;
        }

        if (mapService == null || !mapService.IsWalkable(idleAnchorCell, true))
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            lastIdleTargetCell = idleAnchorCell;
            return true;
        }

        return false;
    }

    private void UpdateIdleFollowTarget()
    {
        if (archetype == null || archetype.IdleBehavior != EnemyIdleBehavior.FollowTarget || playerTarget == null || mapService == null)
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            return;
        }

        Vector3Int targetCell = mapService.ResolveNearestWalkableCell(playerTarget.transform.position, 4, true);

        if (IsAtCell(targetCell))
        {
            currentPath.Clear();
            lastIdleTargetCell = targetCell;
            hasIdleTargetCell = true;
            return;
        }

        bool targetChanged = !hasIdleTargetCell || targetCell != lastIdleTargetCell;

        if (targetChanged || currentPath.Count == 0 || Time.time >= nextRepathTime)
        {
            RequestPathTo(targetCell);
            nextRepathTime = Time.time + archetype.RepathInterval;
            lastIdleTargetCell = targetCell;
            hasIdleTargetCell = true;
        }
    }

    private void UpdateIdleWander()
    {
        if (archetype == null || mapService == null)
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            return;
        }

        Vector3Int currentCell = mapService.WorldToCell(transform.position);

        if (hasIdleTargetCell && IsAtCell(lastIdleTargetCell))
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            patrolPauseUntil = Time.time + Mathf.Min(0.18f, archetype.PatrolWaitTime);
            return;
        }

        if (hasIdleTargetCell)
        {
            if (currentPath.Count == 0 || Time.time >= nextRepathTime)
            {
                if (!RequestPathTo(lastIdleTargetCell) && !IsAtCell(lastIdleTargetCell))
                {
                    hasIdleTargetCell = false;
                    patrolPauseUntil = Time.time + 0.25f;
                    return;
                }

                nextRepathTime = Time.time + archetype.RepathInterval;
            }

            return;
        }

        if (Time.time < patrolPauseUntil)
        {
            currentPath.Clear();
            return;
        }

        if (!TryPickIdleWanderCell(currentCell, out Vector3Int nextIdleCell))
        {
            currentPath.Clear();
            patrolPauseUntil = Time.time + 0.25f;
            return;
        }

        lastIdleTargetCell = nextIdleCell;
        hasIdleTargetCell = true;
        currentPath.Clear();
        currentPath.AddRange(pathScratch);
        nextRepathTime = Time.time + archetype.RepathInterval;
    }

    private void UpdateIdleFollowTrail()
    {
        if (archetype == null || archetype.IdleBehavior != EnemyIdleBehavior.FollowTrail || playerTrail == null)
        {
            ClearFollowTrailPath();
            trailInitialized = false;
            return;
        }

        if (playerTrail.Count == 0)
        {
            ClearFollowTrailPath();
            return;
        }

        Vector3Int currentCell = mapService.WorldToCell(transform.position);
        int recentStartIndex = ResolveRecentTrailStartIndex();

        if (!trailInitialized || trailProgressIndex < recentStartIndex)
        {
            trailProgressIndex = playerTrail.FindClosestIndex(currentCell, recentStartIndex);
            trailInitialized = trailProgressIndex >= 0;
            hasFollowTrailPathTargetCell = false;

            if (!trailInitialized)
            {
                ClearFollowTrailPath();
                return;
            }
        }

        AdvanceTrailProgress(currentCell);

        if (!playerTrail.TryGetCell(trailProgressIndex, out Vector3Int targetCell))
        {
            ClearFollowTrailPath();
            return;
        }

        if (!mapService.IsWalkable(targetCell))
        {
            trailProgressIndex++;
            ClearFollowTrailPath();
            return;
        }

        bool shouldRefreshPath = currentPath.Count == 0
            || !hasFollowTrailPathTargetCell
            || followTrailPathProgressIndex != trailProgressIndex
            || currentPath[currentPath.Count - 1] != followTrailPathTargetCell
            || Time.time >= nextRepathTime;

        if (shouldRefreshPath)
        {
            currentPath.Clear();
            Vector3Int pathTargetCell = targetCell;

            if (TryBuildTrailLeadPath(currentCell, targetCell, out pathTargetCell)
                || TryBuildTrailVariantPath(currentCell, ref pathTargetCell))
            {
                currentPath.AddRange(pathScratch);
            }
            else
            {
                TryBuildPath(currentCell, pathTargetCell, currentPath);
            }

            followTrailPathTargetCell = pathTargetCell;
            followTrailPathProgressIndex = trailProgressIndex;
            hasFollowTrailPathTargetCell = true;
            nextRepathTime = Time.time + archetype.RepathInterval;
        }
    }

    private void UpdateInvestigate()
    {
        // Investigate is a short targeted move to the heard cell. Most enemies fan out into
        // Search afterward, while sentry-style guards simply hold the alert position.
        TryEmitThreatCallPattern();

        if (IsAtCell(investigateCell))
        {
            ResolveAlertAtCell(investigateCell, "investigation complete");
            return;
        }

        if (currentPath.Count == 0 || Time.time >= nextRepathTime)
        {
            RequestPathTo(investigateCell);
            nextRepathTime = Time.time + archetype.RepathInterval;

            if (currentPath.Count == 0 && !IsAtCell(investigateCell))
            {
                ResolveAlertAtCell(mapService.WorldToCell(transform.position), "investigation path failed");
            }
        }
    }

    private void UpdateChase(VisionSensor2D.VisionReading playerReading)
    {
        // Chase always heads toward the latest known player cell. Even if line of sight is
        // lost, the enemy keeps pushing until it reaches that remembered spot.
        Vector3Int lastSeenCell = GetLastSeenPlayerCell();
        Vector3Int preferredChaseCell = !playerReading.CanSee && hasActiveChaseTargetCell
            ? activeChaseTargetCell
            : lastSeenCell;

        bool shouldRefreshPath = currentPath.Count == 0
            || !hasActiveChaseTargetCell
            || (Time.time >= nextRepathTime && ShouldRefreshChasePath(preferredChaseCell));

        if (shouldRefreshPath)
        {
            EnemyPursuitResolution pursuit = EnemyPursuitTargetResolver.Resolve(
                preferredChaseCell,
                4,
                RequestPathTo,
                IsAtCell,
                TryFindReachableNearbyCell);
            Vector3Int chaseTargetCell = pursuit.ResolvedTargetCell;

            if (pursuit.UsedFallback)
            {
                if (enableDebugLogs)
                {
                    string chaseLabel = preferredChaseCell == lastSeenCell
                        ? "Last seen player cell"
                        : "Committed chase cell";
                    LogDebug($"{chaseLabel} {FormatCell(pursuit.PreferredTargetCell)} is blocked. Falling back to reachable nearby cell {FormatCell(chaseTargetCell)}.");
                }
            }

            nextRepathTime = Time.time + archetype.RepathInterval;

            if (pursuit.HasReachableTarget)
            {
                activeChaseTargetCell = chaseTargetCell;
                hasActiveChaseTargetCell = true;
            }

            if (!pursuit.HasReachableTarget && currentPath.Count == 0 && !ShouldHoldPositionAfterAlert())
            {
                Vector3Int localSearchCell = mapService.WorldToCell(transform.position);

                if (enableDebugLogs)
                {
                    LogDebug($"Could not build a reachable chase path toward {FormatCell(chaseTargetCell)}. Starting local search from {FormatCell(localSearchCell)}.");
                }

                BeginSearch(localSearchCell);
                return;
            }

            if (!pursuit.HasReachableTarget && Time.time - GetLastSeenPlayerTime() >= archetype.ChaseMemoryDuration)
            {
                Vector3Int localSearchCell = mapService.WorldToCell(transform.position);
                if (ShouldHoldPositionAfterAlert())
                {
                    if (enableDebugLogs)
                    {
                        LogDebug($"Could not reach chase target {FormatCell(chaseTargetCell)} before chase memory expired. Holding at {FormatCell(localSearchCell)}.");
                    }

                    HoldPosition(localSearchCell, "chase memory expired");
                }
                else
                {
                    if (enableDebugLogs)
                    {
                        LogDebug($"Could not reach chase target {FormatCell(chaseTargetCell)} before chase memory expired. Starting search from {FormatCell(localSearchCell)}.");
                    }

                    BeginSearch(localSearchCell);
                }
            }
        }
    }

    private void UpdateSearch()
    {
        // Search is a short list of cells around the loss point rather than a fully
        // simulated investigation tree. That keeps the prototype readable and tunable.
        TryEmitThreatCallPattern();

        if (UpdateSearchAnchorScan())
        {
            return;
        }

        if (Time.time >= searchEndTime && currentPath.Count == 0)
        {
            CompleteSearch("search timer finished");
            return;
        }

        if (currentPath.Count > 0)
        {
            return;
        }

        while (searchIndex < searchTargets.Count)
        {
            Vector3Int nextTarget = searchTargets[searchIndex++];

            if (!mapService.IsWalkable(nextTarget))
            {
                continue;
            }

            RequestPathTo(nextTarget);

            if (currentPath.Count > 0 || IsAtCell(nextTarget))
            {
                return;
            }
        }

        if (Time.time >= searchEndTime || searchIndex >= searchTargets.Count)
        {
            CompleteSearch("search area exhausted");
        }
    }

    private void TryHandleLatestNoise()
    {
        // Noise only matters when we are not already in full visual chase. That prevents
        // the enemy from bouncing between hearing and sight priorities every frame.
        if (noiseListenerController == null)
        {
            return;
        }

        if (!noiseListenerController.TryConsumeLatestRelevantNoise(CanHear, out NoiseEventRecord bestCandidate))
        {
            return;
        }

        investigateCell = mapService.WorldToCell(bestCandidate.position);
        SetInvestigateFocus(bestCandidate.position);

        if (enableDebugLogs)
        {
            LogDebug($"Heard {bestCandidate.sourceType} noise at {FormatWorld(bestCandidate.position)} -> investigate {FormatCell(investigateCell)}.");
        }

        SetState(
            EnemyState.Investigate,
            enableDebugLogs ? $"noise at {FormatCell(investigateCell)}" : null);
        activityWatchdog?.ReportActivity();
        bool hasPath = RequestPathTo(investigateCell);
        nextRepathTime = Time.time + archetype.RepathInterval;

        if (!hasPath && !IsAtCell(investigateCell))
        {
            LogDebug($"No path to investigate cell {FormatCell(investigateCell)} from {FormatCell(mapService.WorldToCell(transform.position))}.");
        }
    }

    private bool CanHear(NoiseEventRecord record)
    {
        float distance = Vector2.Distance(transform.position, record.position);
        // NoiseEventRecord.radius is the shared authored hearing footprint used by both
        // pulse visualization and AI hearing, so every visible pulse source should use
        // the same radius the player reads in the scene.
        return distance <= record.radius;
    }

    private bool ShouldHoldPositionAfterAlert()
    {
        return archetype != null && archetype.AlertRecoveryBehavior == EnemyAlertRecoveryBehavior.HoldPosition;
    }

    private float ResolveMovementSpeedWithPatterns(float baseSpeed)
    {
        if (baseSpeed <= 0f || !enableUnpredictablePatterns)
        {
            return baseSpeed;
        }

        if (IsSuddenBurstActive())
        {
            return baseSpeed * suddenBurstSpeedMultiplier;
        }

        if (!CanEvaluateSuddenBurstPattern())
        {
            return baseSpeed;
        }

        nextSuddenBurstEvaluationTime = Time.time + suddenBurstCooldown;

        if (Random.value > suddenBurstChance)
        {
            return baseSpeed;
        }

        suddenBurstUntil = Time.time + suddenBurstDuration;
        nextMovementNoiseTime = Mathf.Min(nextMovementNoiseTime, Time.time);
        LogDebug($"Triggered sudden burst during {currentState} for {suddenBurstDuration:0.00}s.");
        return baseSpeed * suddenBurstSpeedMultiplier;
    }

    private bool CanEvaluateSuddenBurstPattern()
    {
        if (IsStunned
            || IsAttackRecovering
            || currentPath.Count == 0
            || Time.time < nextSuddenBurstEvaluationTime)
        {
            return false;
        }

        if (currentState == EnemyState.Idle)
        {
            return CanUseRoutePatrolPattern();
        }

        return currentState == EnemyState.Investigate
            || currentState == EnemyState.Chase
            || currentState == EnemyState.Search;
    }

    private bool IsSuddenBurstActive()
    {
        return enableUnpredictablePatterns && Time.time < suddenBurstUntil;
    }

    private void TryEmitThreatCallPattern()
    {
        INoiseEventBus eventBus = ResolveNoiseEventBus();

        if (!enableUnpredictablePatterns
            || eventBus == null
            || IsStunned
            || IsAttackRecovering
            || threatCallRadius <= 0f
            || Time.time < nextThreatCallEvaluationTime)
        {
            return;
        }

        nextThreatCallEvaluationTime = Time.time + threatCallInterval;

        if (currentState != EnemyState.Investigate
            && currentState != EnemyState.Search)
        {
            return;
        }

        if (Random.value > threatCallChance)
        {
            return;
        }

        eventBus.TryEmitNoise(
            (Vector2)transform.position,
            threatCallRadius,
            threatCallNoiseType,
            gameObject.GetInstanceID(),
            NoiseEmitterAffiliation.Enemy);

        activityWatchdog?.ReportActivity(transform.position);
        LogDebug($"Emitted threat call during {currentState} with radius {threatCallRadius:0.00}.");
    }

    private void ClearFollowTrailPath()
    {
        currentPath.Clear();
        hasFollowTrailPathTargetCell = false;
    }

    private int ResolveRecentTrailStartIndex()
    {
        if (playerTrail == null || trailRecentTargetWindow <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, playerTrail.Count - trailRecentTargetWindow);
    }

    private bool TryBuildTrailLeadPath(Vector3Int currentCell, Vector3Int baseTargetCell, out Vector3Int leadCell)
    {
        pathScratch.Clear();
        leadCell = baseTargetCell;

        if (!IsFollowTrailEnemy()
            || playerTrail == null
            || mapService == null
            || trailLeadPredictionStepCount <= 0
            || !playerTrail.TryGetLatestCell(out Vector3Int latestTrailCell)
            || !playerTrail.TryGetRecentMovementDirection(trailLeadSampleCount, out Vector3Int recentDirection))
        {
            return false;
        }

        bool hasCandidate = false;
        int bestScore = int.MinValue;
        Vector3Int bestCell = baseTargetCell;
        int maxPredictionSteps = Mathf.Max(1, trailLeadPredictionStepCount);

        for (int step = maxPredictionSteps; step >= 1; step--)
        {
            Vector3Int candidateCell = latestTrailCell + ScaleCellDirection(recentDirection, step);

            if (!TryScoreTrailLeadCandidate(currentCell, baseTargetCell, candidateCell, step, out int score)
                || (hasCandidate && score <= bestScore))
            {
                continue;
            }

            hasCandidate = true;
            bestScore = score;
            bestCell = candidateCell;
        }

        if (!hasCandidate)
        {
            pathScratch.Clear();
            return false;
        }

        pathScratch.Clear();

        if (!TryBuildPath(currentCell, bestCell, pathScratch) || pathScratch.Count == 0)
        {
            return false;
        }

        leadCell = bestCell;
        trailProgressIndex = Mathf.Max(trailProgressIndex, playerTrail.Count - 1);
        LogDebug($"Applied trail lead prediction from {FormatCell(baseTargetCell)} to {FormatCell(leadCell)}.");
        return true;
    }

    private bool TryScoreTrailLeadCandidate(
        Vector3Int currentCell,
        Vector3Int baseTargetCell,
        Vector3Int candidateCell,
        int predictionStep,
        out int score)
    {
        score = 0;

        if (candidateCell == currentCell
            || candidateCell == baseTargetCell
            || !mapService.IsWalkable(candidateCell, true))
        {
            return false;
        }

        pathScratch.Clear();

        if (!TryBuildPath(currentCell, candidateCell, pathScratch) || pathScratch.Count == 0)
        {
            return false;
        }

        int walkableNeighborCount = CountWalkableNeighbors(candidateCell);
        score = (predictionStep * 4) + (walkableNeighborCount * 2);

        if (walkableNeighborCount >= 3)
        {
            score += 12;
        }

        return true;
    }

    private bool TryBuildTrailVariantPath(Vector3Int currentCell, ref Vector3Int targetCell)
    {
        pathScratch.Clear();

        if (!enableUnpredictablePatterns
            || !IsFollowTrailEnemy()
            || Time.time < nextTrailVariantEvaluationTime)
        {
            return false;
        }

        nextTrailVariantEvaluationTime = Time.time + trailVariantCooldown;

        if (Random.value > trailVariantChance)
        {
            return false;
        }

        bool preferLeap = trailLeapStepCount > 0 && (trailOffsetSearchRadius <= 0 || Random.value < 0.5f);

        if (preferLeap)
        {
            if (TryBuildTrailLeapVariantPath(currentCell, out Vector3Int leapCell))
            {
                targetCell = leapCell;
                return true;
            }

            if (TryBuildTrailOffsetVariantPath(currentCell, targetCell, out Vector3Int offsetCell))
            {
                targetCell = offsetCell;
                return true;
            }

            return false;
        }

        if (TryBuildTrailOffsetVariantPath(currentCell, targetCell, out Vector3Int alternateOffsetCell))
        {
            targetCell = alternateOffsetCell;
            return true;
        }

        if (TryBuildTrailLeapVariantPath(currentCell, out Vector3Int alternateLeapCell))
        {
            targetCell = alternateLeapCell;
            return true;
        }

        return false;
    }

    private bool TryBuildTrailLeapVariantPath(Vector3Int currentCell, out Vector3Int leapCell)
    {
        leapCell = currentCell;

        if (playerTrail == null || mapService == null)
        {
            return false;
        }

        int maxLeapSteps = Mathf.Max(1, trailLeapStepCount);

        for (int leapOffset = maxLeapSteps; leapOffset >= 1; leapOffset--)
        {
            int candidateIndex = trailProgressIndex + leapOffset;

            if (!playerTrail.TryGetCell(candidateIndex, out Vector3Int candidateCell)
                || !mapService.IsWalkable(candidateCell))
            {
                continue;
            }

            pathScratch.Clear();

            if (!TryBuildPath(currentCell, candidateCell, pathScratch) || pathScratch.Count == 0)
            {
                continue;
            }

            trailProgressIndex = candidateIndex;
            leapCell = candidateCell;
            LogDebug($"Applied trail leap variant to {FormatCell(leapCell)}.");
            return true;
        }

        return false;
    }

    private bool TryBuildTrailOffsetVariantPath(Vector3Int currentCell, Vector3Int baseTargetCell, out Vector3Int offsetCell)
    {
        offsetCell = baseTargetCell;

        if (trailOffsetSearchRadius <= 0
            || mapService == null
            || !TryFindReachableTrailOffsetCell(currentCell, baseTargetCell, trailOffsetSearchRadius, out Vector3Int candidateCell))
        {
            return false;
        }

        offsetCell = candidateCell;
        LogDebug($"Applied trail offset variant from {FormatCell(baseTargetCell)} to {FormatCell(offsetCell)}.");
        return true;
    }

    private bool TryFindReachableTrailOffsetCell(Vector3Int currentCell, Vector3Int targetCell, int searchRadius, out Vector3Int reachableCell)
    {
        reachableCell = targetCell;

        if (mapService == null)
        {
            return false;
        }

        for (int radius = 1; radius <= Mathf.Max(1, searchRadius); radius++)
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

                    if (candidate == currentCell
                        || candidate == targetCell
                        || !mapService.IsWalkable(candidate, true))
                    {
                        continue;
                    }

                    pathScratch.Clear();

                    if (!TryBuildPath(currentCell, candidate, pathScratch) || pathScratch.Count == 0)
                    {
                        continue;
                    }

                    reachableCell = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private void ResolveAlertAtCell(Vector3Int resolvedCell, string reason)
    {
        ClearInvestigateFocus();

        if (ShouldHoldPositionAfterAlert())
        {
            HoldPosition(resolvedCell, reason);
            return;
        }

        BeginSearch(resolvedCell);
    }

    private void HoldPosition(Vector3Int holdCell, string reason)
    {
        currentPath.Clear();
        nextRepathTime = 0f;
        activityWatchdog?.ResetPathTracking();

        if (!IsStandGuardEnemy())
        {
            idleAnchorCell = holdCell;
        }

        lastIdleTargetCell = holdCell;
        hasIdleTargetCell = false;
        patrolPauseUntil = 0f;
        SetState(EnemyState.Idle, reason);
    }

    private bool TryCapturePlayer(VisionSensor2D.VisionReading playerReading)
    {
        if (archetype == null || playerTarget == null)
        {
            return false;
        }

        if (IsAttackRecovering)
        {
            return false;
        }

        if (playerInteractionController == null)
        {
            return false;
        }

        if (currentState != EnemyState.Chase || !playerReading.CanSee)
        {
            return false;
        }

        if (!playerInteractionController.TryStrikePlayer(transform, archetype.CaptureDistance, gameObject.name))
        {
            return false;
        }

        BeginAttackRecovery();
        return true;
    }

    private VisionSensor2D.VisionReading BuildConfirmedVisionReading(VisionSensor2D.VisionReading rawReading)
    {
        return EnemyVisionConfirmationUtility.BuildConfirmedReading(
            rawReading,
            Time.deltaTime,
            ref visualDetectionMeter,
            visualDetectionBuildDuration,
            visualDetectionDecayDuration,
            confirmedDetectionThreshold);
    }

    private void BeginAttackRecovery()
    {
        disruptionController?.BeginAttackRecovery(
            attackRecoveryDuration,
            onRecoveryStarted: () =>
            {
                currentPath.Clear();
                nextRepathTime = 0f;
                activityWatchdog?.ResetPathTracking();
                activityWatchdog?.ReportActivity();
            });
    }

    public bool TryApplyStun(float duration)
    {
        return disruptionController != null && disruptionController.TryApplyStun(
            duration,
            onStunApplied: () =>
            {
                currentPath.Clear();
                nextRepathTime = 0f;
                activityWatchdog?.ResetPathTracking();
            });
    }

    private void SetState(EnemyState newState, string reason = null)
    {
        if (currentState == newState)
        {
            return;
        }

        EnemyState previousState = currentState;
        currentState = newState;
        currentPath.Clear();
        StopActivePatrolPattern();
        activityWatchdog?.ResetPathTracking();
        activityWatchdog?.ReportActivity();

        if (newState != EnemyState.Investigate)
        {
            ClearInvestigateFocus();
        }

        if (enableDebugLogs)
        {
            string suffix = string.IsNullOrWhiteSpace(reason) ? "." : $" ({reason}).";
            Debug.Log($"[EnemyAI:{gameObject.name}] State {previousState} -> {newState}{suffix}", this);
        }

        if (newState == EnemyState.Idle)
        {
            patrolPauseUntil = 0f;
            nextRepathTime = 0f;
            hasIdleTargetCell = false;
            trailInitialized = false;
            suddenBurstUntil = float.NegativeInfinity;
            if (IsStandGuardEnemy())
            {
                ScheduleNextStandGuardShift();
            }
        }
        else
        {
            StopStandGuardScout(resetThreatFeedback: false);
            if (IsStandGuardEnemy())
            {
                StopStandGuardShift(scheduleNextShift: false);
            }
        }

        if (newState != EnemyState.Chase)
        {
            hasActiveChaseTargetCell = false;
        }

        if (newState != EnemyState.Search)
        {
            hasSearchAnchorCell = false;
            isSearchAnchorScanActive = false;
        }
    }

    private void BuildAutoPatrolRouteIfNeeded()
    {
        if (archetype == null
            || mapService == null
            || archetype.IdleBehavior != EnemyIdleBehavior.Patrol
            || patrolRoute.Count >= 2)
        {
            return;
        }

        Vector3Int anchorCell = patrolRoute.Count == 1 && mapService.IsWalkable(patrolRoute[0], true)
            ? patrolRoute[0]
            : mapService.IsWalkable(idleAnchorCell, true)
                ? idleAnchorCell
                : mapService.WorldToCell(transform.position);

        int radius = Mathf.Max(1, archetype.IdleWanderRadius);
        AddAutoPatrolCell(anchorCell);

        foreach (Vector3Int offset in CardinalOffsets)
        {
            Vector3Int furthestCell = FindFurthestWalkableCell(anchorCell, offset, radius);
            AddAutoPatrolCell(furthestCell);
        }

        if (patrolRoute.Count >= 2)
        {
            return;
        }

        CollectReachableIdleCandidates(anchorCell, anchorCell, radius);

        foreach (Vector3Int candidate in idleCandidateCells)
        {
            AddAutoPatrolCell(candidate);

            if (patrolRoute.Count >= 4)
            {
                break;
            }
        }
    }

    private Vector3Int FindFurthestWalkableCell(Vector3Int startCell, Vector3Int direction, int maxSteps)
    {
        Vector3Int furthestCell = startCell;

        for (int step = 1; step <= maxSteps; step++)
        {
            Vector3Int candidate = startCell + (direction * step);

            if (!mapService.IsWalkable(candidate, true))
            {
                break;
            }

            furthestCell = candidate;
        }

        return furthestCell;
    }

    private void AddAutoPatrolCell(Vector3Int cell)
    {
        if (!mapService.IsWalkable(cell, true) || patrolRoute.Contains(cell))
        {
            return;
        }

        patrolRoute.Add(cell);
    }

    private bool TryPickIdleWanderCell(Vector3Int currentCell, out Vector3Int targetCell)
    {
        targetCell = currentCell;

        if (archetype == null || mapService == null)
        {
            pathScratch.Clear();
            return false;
        }

        Vector3Int anchorCell = mapService.IsWalkable(idleAnchorCell, true) ? idleAnchorCell : currentCell;
        int radius = Mathf.Max(1, archetype.IdleWanderRadius);
        CollectReachableIdleCandidates(anchorCell, currentCell, radius);

        if (idleCandidateCells.Count == 0)
        {
            pathScratch.Clear();
            return false;
        }

        for (int attempt = 0; attempt < idleCandidateCells.Count; attempt++)
        {
            Vector3Int candidate = idleCandidateCells[Random.Range(0, idleCandidateCells.Count)];
            pathScratch.Clear();

            if (!TryBuildPath(currentCell, candidate, pathScratch) || pathScratch.Count == 0)
            {
                continue;
            }

            targetCell = candidate;
            return true;
        }

        pathScratch.Clear();
        return false;
    }

    private bool HasUsablePatrolRoute()
    {
        return patrolRoute.Count >= 2;
    }

    private void CompleteSearch(string reason)
    {
        if (TryBeginStandGuardScout(reason))
        {
            return;
        }

        SetState(EnemyState.Idle, reason);
    }

    private bool TryBeginStandGuardScout(string reason)
    {
        if (!IsStandGuardEnemy()
            || mapService == null
            || standGuardScoutDuration <= 0f
            || !BuildStandGuardScoutRoute(hasSearchAnchorCell ? searchAnchorCell : mapService.WorldToCell(transform.position)))
        {
            return false;
        }

        SetState(EnemyState.Idle, $"{reason}; stand-guard scout");
        isStandGuardScoutActive = true;
        standGuardScoutIndex = 0;
        standGuardScoutUntil = Time.time + standGuardScoutDuration;
        visualDetectionMeter = Mathf.Min(visualDetectionMeter, 0.28f);
        threatFeedbackUntil = float.NegativeInfinity;
        return true;
    }

    private bool BuildStandGuardScoutRoute(Vector3Int centerCell)
    {
        standGuardScoutRoute.Clear();

        if (mapService == null)
        {
            return false;
        }

        Vector3Int scoutAnchorCell = mapService.IsWalkable(centerCell, true)
            ? centerCell
            : mapService.IsWalkable(idleAnchorCell, true)
                ? idleAnchorCell
                : mapService.WorldToCell(transform.position);
        int scoutRadius = Mathf.Max(1, standGuardScoutRadius);

        foreach (Vector3Int offset in CardinalOffsets)
        {
            AddStandGuardScoutCell(FindFurthestWalkableCell(scoutAnchorCell, offset, scoutRadius), scoutAnchorCell);
        }

        if (standGuardScoutRoute.Count > 0)
        {
            return true;
        }

        CollectReachableIdleCandidates(scoutAnchorCell, scoutAnchorCell, scoutRadius);

        foreach (Vector3Int candidate in idleCandidateCells)
        {
            if (AddStandGuardScoutCell(candidate, scoutAnchorCell) && standGuardScoutRoute.Count >= 3)
            {
                break;
            }
        }

        return standGuardScoutRoute.Count > 0;
    }

    private bool AddStandGuardScoutCell(Vector3Int cell, Vector3Int scoutAnchorCell)
    {
        if (mapService == null
            || cell == scoutAnchorCell
            || !mapService.IsWalkable(cell, true)
            || standGuardScoutRoute.Contains(cell))
        {
            return false;
        }

        standGuardScoutRoute.Add(cell);
        return true;
    }

    private bool UpdateStandGuardScout()
    {
        if (!isStandGuardScoutActive)
        {
            return false;
        }

        if (standGuardScoutRoute.Count == 0 || Time.time >= standGuardScoutUntil)
        {
            StopStandGuardScout(resetThreatFeedback: true);
            return false;
        }

        Vector3Int scoutCell = standGuardScoutRoute[Mathf.Clamp(standGuardScoutIndex, 0, standGuardScoutRoute.Count - 1)];

        if (IsAtCell(scoutCell))
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            lastIdleTargetCell = scoutCell;

            if (patrolPauseUntil <= 0f)
            {
                patrolPauseUntil = Time.time + standGuardScoutPauseTime;
            }

            if (Time.time < patrolPauseUntil)
            {
                return true;
            }

            patrolPauseUntil = 0f;
            standGuardScoutIndex++;

            if (standGuardScoutIndex >= standGuardScoutRoute.Count)
            {
                StopStandGuardScout(resetThreatFeedback: true);
                return false;
            }

            scoutCell = standGuardScoutRoute[standGuardScoutIndex];
        }

        bool shouldRefreshPath = currentPath.Count == 0
            || !hasIdleTargetCell
            || lastIdleTargetCell != scoutCell
            || Time.time >= nextRepathTime;

        if (!shouldRefreshPath)
        {
            return true;
        }

        if (!RequestPathTo(scoutCell) && !IsAtCell(scoutCell))
        {
            StopStandGuardScout(resetThreatFeedback: true);
            return false;
        }

        lastIdleTargetCell = scoutCell;
        hasIdleTargetCell = true;
        nextRepathTime = Time.time + archetype.RepathInterval;
        return true;
    }

    private void StopStandGuardScout(bool resetThreatFeedback)
    {
        standGuardScoutRoute.Clear();
        standGuardScoutUntil = 0f;
        isStandGuardScoutActive = false;
        standGuardScoutIndex = 0;
        currentPath.Clear();
        hasIdleTargetCell = false;
        nextRepathTime = 0f;
        patrolPauseUntil = 0f;

        if (!resetThreatFeedback)
        {
            return;
        }

        visualDetectionMeter = Mathf.Min(visualDetectionMeter, 0.18f);
        threatFeedbackUntil = float.NegativeInfinity;
        ScheduleNextStandGuardShift();
    }

    private void ResetStandGuardShiftPattern(bool scheduleNextShift)
    {
        StopStandGuardShift(scheduleNextShift: false);
        standGuardShiftCandidateCursor = 0;

        if (scheduleNextShift)
        {
            ScheduleNextStandGuardShift();
        }
    }

    private void ScheduleNextStandGuardShift()
    {
        if (!IsStandGuardEnemy() || standGuardShiftInterval <= 0f)
        {
            standGuardNextShiftTime = float.PositiveInfinity;
            return;
        }

        standGuardNextShiftTime = Time.time + Mathf.Max(0f, standGuardShiftInterval);
    }

    private bool UpdateStandGuardShiftPattern()
    {
        if (!IsStandGuardEnemy() || mapService == null || standGuardShiftInterval <= 0f)
        {
            return false;
        }

        if (isStandGuardShiftActive)
        {
            return UpdateActiveStandGuardShift();
        }

        if (Time.time < standGuardNextShiftTime || !CanBeginStandGuardShift())
        {
            return false;
        }

        Vector3Int currentCell = mapService.WorldToCell(transform.position);
        if (!TryBuildStandGuardShiftRoute(currentCell))
        {
            ScheduleNextStandGuardShift();
            return false;
        }

        isStandGuardShiftActive = true;
        standGuardShiftIndex = 0;
        standGuardShiftGuardUntil = 0f;
        currentPath.Clear();
        hasIdleTargetCell = false;
        nextRepathTime = 0f;
        patrolPauseUntil = 0f;
        return UpdateActiveStandGuardShift();
    }

    private bool CanBeginStandGuardShift()
    {
        return mapService != null
            && mapService.IsWalkable(idleAnchorCell, true)
            && IsAtCell(idleAnchorCell)
            && currentPath.Count == 0;
    }

    private bool TryBuildStandGuardShiftRoute(Vector3Int currentCell)
    {
        standGuardShiftRoute.Clear();

        if (mapService == null || !mapService.IsWalkable(idleAnchorCell, true))
        {
            return false;
        }

        if (!TryResolveStandGuardShiftCell(currentCell, out Vector3Int shiftCell))
        {
            return false;
        }

        standGuardShiftRoute.Add(shiftCell);
        standGuardShiftRoute.Add(idleAnchorCell);
        return true;
    }

    private bool TryResolveStandGuardShiftCell(Vector3Int currentCell, out Vector3Int shiftCell)
    {
        return TryResolveConfiguredStandGuardShiftCell(currentCell, out shiftCell)
            || TryResolveCardinalStandGuardShiftCell(currentCell, out shiftCell)
            || TryResolveReachableStandGuardShiftCell(currentCell, out shiftCell);
    }

    private bool TryResolveConfiguredStandGuardShiftCell(Vector3Int currentCell, out Vector3Int shiftCell)
    {
        shiftCell = idleAnchorCell;

        if (standGuardShiftPoints == null || standGuardShiftPoints.Length == 0)
        {
            return false;
        }

        int pointCount = standGuardShiftPoints.Length;
        int startIndex = Mathf.Abs(standGuardShiftCandidateCursor) % pointCount;

        for (int attempt = 0; attempt < pointCount; attempt++)
        {
            int pointIndex = (startIndex + attempt) % pointCount;
            Transform shiftPoint = standGuardShiftPoints[pointIndex];

            if (shiftPoint == null)
            {
                continue;
            }

            Vector3Int candidateCell = mapService.ResolveNearestWalkableCell(shiftPoint.position, 1, true);

            if (!TryAcceptStandGuardShiftCell(currentCell, candidateCell, out shiftCell))
            {
                continue;
            }

            standGuardShiftCandidateCursor = pointIndex + 1;
            return true;
        }

        return false;
    }

    private bool TryResolveCardinalStandGuardShiftCell(Vector3Int currentCell, out Vector3Int shiftCell)
    {
        shiftCell = idleAnchorCell;

        int directionCount = CardinalOffsets.Length;
        int startIndex = Mathf.Abs(standGuardShiftCandidateCursor) % directionCount;
        int shiftRadius = Mathf.Max(1, standGuardShiftRadius);

        for (int attempt = 0; attempt < directionCount; attempt++)
        {
            int directionIndex = (startIndex + attempt) % directionCount;
            Vector3Int candidateCell = FindFurthestWalkableCell(idleAnchorCell, CardinalOffsets[directionIndex], shiftRadius);

            if (!TryAcceptStandGuardShiftCell(currentCell, candidateCell, out shiftCell))
            {
                continue;
            }

            standGuardShiftCandidateCursor = directionIndex + 1;
            return true;
        }

        return false;
    }

    private bool TryResolveReachableStandGuardShiftCell(Vector3Int currentCell, out Vector3Int shiftCell)
    {
        shiftCell = idleAnchorCell;
        CollectReachableIdleCandidates(idleAnchorCell, idleAnchorCell, Mathf.Max(1, standGuardShiftRadius));

        int candidateCount = idleCandidateCells.Count;
        if (candidateCount == 0)
        {
            return false;
        }

        int startIndex = Mathf.Abs(standGuardShiftCandidateCursor) % candidateCount;

        for (int attempt = 0; attempt < candidateCount; attempt++)
        {
            int candidateIndex = (startIndex + attempt) % candidateCount;
            Vector3Int candidateCell = idleCandidateCells[candidateIndex];

            if (!TryAcceptStandGuardShiftCell(currentCell, candidateCell, out shiftCell))
            {
                continue;
            }

            standGuardShiftCandidateCursor = candidateIndex + 1;
            return true;
        }

        return false;
    }

    private bool TryAcceptStandGuardShiftCell(Vector3Int currentCell, Vector3Int candidateCell, out Vector3Int shiftCell)
    {
        shiftCell = idleAnchorCell;

        if (candidateCell == idleAnchorCell || !mapService.IsWalkable(candidateCell, true))
        {
            return false;
        }

        pathScratch.Clear();
        if (!TryBuildPath(currentCell, candidateCell, pathScratch) || pathScratch.Count == 0)
        {
            return false;
        }

        pathScratch.Clear();
        if (!TryBuildPath(candidateCell, idleAnchorCell, pathScratch) || pathScratch.Count == 0)
        {
            return false;
        }

        shiftCell = candidateCell;
        return true;
    }

    private bool UpdateActiveStandGuardShift()
    {
        if (!isStandGuardShiftActive)
        {
            return false;
        }

        if (standGuardShiftRoute.Count == 0)
        {
            StopStandGuardShift(scheduleNextShift: true);
            return false;
        }

        Vector3Int targetCell = standGuardShiftRoute[Mathf.Clamp(standGuardShiftIndex, 0, standGuardShiftRoute.Count - 1)];

        if (IsAtCell(targetCell))
        {
            currentPath.Clear();
            hasIdleTargetCell = false;
            lastIdleTargetCell = targetCell;

            if (standGuardShiftIndex == 0)
            {
                if (standGuardShiftGuardUntil <= 0f)
                {
                    standGuardShiftGuardUntil = Time.time + Mathf.Max(0f, standGuardShiftGuardTime);
                }

                if (Time.time < standGuardShiftGuardUntil)
                {
                    return true;
                }

                standGuardShiftGuardUntil = 0f;
                standGuardShiftIndex++;

                if (standGuardShiftIndex >= standGuardShiftRoute.Count)
                {
                    StopStandGuardShift(scheduleNextShift: true);
                    return false;
                }

                targetCell = standGuardShiftRoute[standGuardShiftIndex];
            }
            else
            {
                StopStandGuardShift(scheduleNextShift: true);
                return false;
            }
        }

        bool shouldRefreshPath = currentPath.Count == 0
            || !hasIdleTargetCell
            || lastIdleTargetCell != targetCell
            || Time.time >= nextRepathTime;

        if (!shouldRefreshPath)
        {
            return true;
        }

        if (!RequestPathTo(targetCell) && !IsAtCell(targetCell))
        {
            StopStandGuardShift(scheduleNextShift: true);
            return false;
        }

        lastIdleTargetCell = targetCell;
        hasIdleTargetCell = true;
        nextRepathTime = Time.time + archetype.RepathInterval;
        return true;
    }

    private void StopStandGuardShift(bool scheduleNextShift)
    {
        standGuardShiftRoute.Clear();
        isStandGuardShiftActive = false;
        standGuardShiftIndex = 0;
        standGuardShiftGuardUntil = 0f;
        currentPath.Clear();
        hasIdleTargetCell = false;
        nextRepathTime = 0f;
        patrolPauseUntil = 0f;

        if (scheduleNextShift)
        {
            ScheduleNextStandGuardShift();
        }
    }

    private void CollectReachableIdleCandidates(Vector3Int anchorCell, Vector3Int currentCell, int radius)
    {
        idleCandidateCells.Clear();

        if (!mapService.IsWalkable(anchorCell, true))
        {
            return;
        }

        idleCandidateFrontier.Clear();
        idleCandidateDistanceByCell.Clear();
        idleCandidateFallbackCells.Clear();

        idleCandidateFrontier.Enqueue(anchorCell);
        idleCandidateDistanceByCell[anchorCell] = 0;

        while (idleCandidateFrontier.Count > 0)
        {
            Vector3Int cell = idleCandidateFrontier.Dequeue();
            int distance = idleCandidateDistanceByCell[cell];

            if (cell != currentCell)
            {
                int openNeighborCount = CountWalkableNeighbors(cell);

                if (openNeighborCount >= 2)
                {
                    idleCandidateCells.Add(cell);
                }
                else
                {
                    idleCandidateFallbackCells.Add(cell);
                }
            }

            if (distance >= radius)
            {
                continue;
            }

            foreach (Vector3Int offset in CardinalOffsets)
            {
                Vector3Int neighbor = cell + offset;

                if (idleCandidateDistanceByCell.ContainsKey(neighbor) || !mapService.IsWalkable(neighbor, true))
                {
                    continue;
                }

                idleCandidateDistanceByCell[neighbor] = distance + 1;
                idleCandidateFrontier.Enqueue(neighbor);
            }
        }

        if (idleCandidateCells.Count == 0)
        {
            idleCandidateCells.AddRange(idleCandidateFallbackCells);
        }
    }

    private int CountWalkableNeighbors(Vector3Int cell)
    {
        int count = 0;

        foreach (Vector3Int offset in CardinalOffsets)
        {
            if (mapService.IsWalkable(cell + offset, true))
            {
                count++;
            }
        }

        return count;
    }

    private void AdvanceTrailProgress(Vector3Int currentCell)
    {
        while (playerTrail != null && playerTrail.TryGetCell(trailProgressIndex, out Vector3Int trailCell))
        {
            if (trailCell != currentCell)
            {
                break;
            }

            trailProgressIndex++;
        }
    }

    private void BeginSearch(Vector3Int centerCell)
    {
        SetState(EnemyState.Search, $"search centered on {FormatCell(centerCell)}");
        searchTargets.Clear();
        searchIndex = 1;
        searchEndTime = Time.time + archetype.SearchDuration;
        searchAnchorCell = centerCell;
        searchAnchorBaseFacing = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector2.up;
        searchAnchorScanStartTime = 0f;
        hasSearchAnchorCell = true;
        isSearchAnchorScanActive = false;
        searchTargets.Add(centerCell);

        for (int radius = 1; radius <= archetype.SearchRadius; radius++)
        {
            searchTargets.Add(centerCell + new Vector3Int(radius, 0, 0));
            searchTargets.Add(centerCell + new Vector3Int(-radius, 0, 0));
            searchTargets.Add(centerCell + new Vector3Int(0, radius, 0));
            searchTargets.Add(centerCell + new Vector3Int(0, -radius, 0));
            searchTargets.Add(centerCell + new Vector3Int(radius, radius, 0));
            searchTargets.Add(centerCell + new Vector3Int(-radius, radius, 0));
            searchTargets.Add(centerCell + new Vector3Int(radius, -radius, 0));
            searchTargets.Add(centerCell + new Vector3Int(-radius, -radius, 0));
        }
    }

    private bool RequestPathTo(Vector3Int targetCell)
    {
        Vector3Int startCell = mapService.WorldToCell(transform.position);
        if (hasFailedPathCache
            && Time.time < lastFailedPathUntil
            && lastFailedPathStartCell == startCell
            && lastFailedPathTargetCell == targetCell)
        {
            currentPath.Clear();
            activityWatchdog?.ResetPathTracking();
            return false;
        }

        repathScratch.Clear();
        bool hasPath = TryBuildPath(startCell, targetCell, repathScratch);

        if (hasPath)
        {
            hasFailedPathCache = false;
            currentPath.Clear();
            currentPath.AddRange(repathScratch);
            activityWatchdog?.ReportActivity();
            activityWatchdog?.ResetPathTracking();
            return true;
        }

        currentPath.Clear();
        hasFailedPathCache = true;
        lastFailedPathStartCell = startCell;
        lastFailedPathTargetCell = targetCell;
        lastFailedPathUntil = Time.time + Mathf.Max(0f, failedPathCacheDuration);
        activityWatchdog?.ResetPathTracking();
        return false;
    }

    private bool TryBuildPath(Vector3Int startCell, Vector3Int targetCell, List<Vector3Int> buffer)
    {
        return GridPathfinder.TryBuildPath(
            mapService,
            startCell,
            targetCell,
            buffer,
            allowClosedDoors: true,
            temporarilyBlockedCell: hasTemporarilyBlockedCell ? temporarilyBlockedCell : null);
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
            configuredStunSortingOrder: bodyRenderer != null ? bodyRenderer.sortingOrder + 6 : 24,
            configuredStunHeightOffset: 0.92f,
            configuredStunBodyColor: new Color(0.58f, 0.74f, 1f, 1f),
            configuredStunMarkerColor: new Color(0.9f, 0.96f, 1f, 1f));
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

        noiseListenerController.Configure(configuredNoiseEventBus: ResolveNoiseEventBus());
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
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

    private void RecoverFromInactivity()
    {
        if (currentPath.Count > 0 && TryRecoverFromBlockedPath(currentPath[0], "activity watchdog stall recovery"))
        {
            return;
        }

        LogDebug($"No meaningful activity detected for {inactivityRecoveryTimeout:0.0}s. Returning to idle behavior.");
        currentPath.Clear();
        nextRepathTime = 0f;
        SetState(EnemyState.Idle, "activity watchdog recovery");
        activityWatchdog?.ResetWatchdog(transform.position);
    }

    private void RefreshTemporaryBlockedCell()
    {
        if (!hasTemporarilyBlockedCell || Time.time < temporarilyBlockedCellUntil)
        {
            return;
        }

        hasTemporarilyBlockedCell = false;
    }

    private bool ShouldTriggerBlockedPathRecovery(Vector3Int nextCell, Vector3 targetPosition, float distanceToTarget)
    {
        if (mapService == null || distanceToTarget <= PathArrivalThreshold)
        {
            return false;
        }

        if (!mapService.IsWalkable(nextCell, true))
        {
            return true;
        }

        return IsCellPhysicallyBlocked(nextCell, targetPosition);
    }

    private bool IsCellPhysicallyBlocked(Vector3Int nextCell, Vector3 targetPosition)
    {
        float probeRadius = Mathf.Max(0.05f, collisionProbeRadius);
        int hitCount = Physics2D.OverlapCircle((Vector2)targetPosition, probeRadius, ContactFilter2D.noFilter, collisionProbeResults);

        for (int index = 0; index < hitCount; index++)
        {
            Collider2D hit = collisionProbeResults[index];

            if (ShouldIgnoreMovementCollider(hit))
            {
                continue;
            }

            LogDebug($"Detected physical blocker '{hit.name}' near path cell {FormatCell(nextCell)}.");
            return true;
        }

        return false;
    }

    private bool ShouldIgnoreMovementCollider(Collider2D candidate)
    {
        if (candidate == null || !candidate.enabled || candidate.isTrigger)
        {
            return true;
        }

        Transform candidateTransform = candidate.transform;

        if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
        {
            return true;
        }

        if (playerTarget != null)
        {
            Transform playerTransform = playerTarget.transform;

            if (candidateTransform == playerTransform
                || candidateTransform.IsChildOf(playerTransform)
                || playerTransform.IsChildOf(candidateTransform))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRecoverFromBlockedPath(Vector3Int blockedCell, string reason)
    {
        if (mapService == null)
        {
            return false;
        }

        nextBlockedPathRecoveryTime = Time.time + blockedPathRecoveryCooldown;
        RememberTemporarilyBlockedCell(blockedCell);

        Vector3Int recoveryTargetCell = ResolveRecoveryTargetCell();

        if (RequestPathTo(recoveryTargetCell))
        {
            LogDebug($"Recovered from blocked path at {FormatCell(blockedCell)} by repathing toward {FormatCell(recoveryTargetCell)} ({reason}).");
            return true;
        }

        if (currentState == EnemyState.Chase)
        {
            Vector3Int preferredTargetCell = hasActiveChaseTargetCell
                ? activeChaseTargetCell
                : GetLastSeenPlayerCell();
            EnemyPursuitResolution pursuit = EnemyPursuitTargetResolver.Resolve(
                preferredTargetCell,
                4,
                RequestPathTo,
                IsAtCell,
                TryFindReachableNearbyCell);

            if (pursuit.HasReachableTarget)
            {
                activeChaseTargetCell = pursuit.ResolvedTargetCell;
                hasActiveChaseTargetCell = true;
                LogDebug($"Recovered from blocked chase path at {FormatCell(blockedCell)} using fallback target {FormatCell(pursuit.ResolvedTargetCell)} ({reason}).");
                return true;
            }
        }

        currentPath.Clear();
        nextRepathTime = 0f;
        activityWatchdog?.ResetPathTracking();
        LogDebug($"Blocked path recovery failed at {FormatCell(blockedCell)} while heading to {FormatCell(recoveryTargetCell)} ({reason}).");
        return false;
    }

    private Vector3Int ResolveRecoveryTargetCell()
    {
        return currentState switch
        {
            EnemyState.Chase => hasActiveChaseTargetCell ? activeChaseTargetCell : GetLastSeenPlayerCell(),
            EnemyState.Investigate => investigateCell,
            EnemyState.Search => hasSearchAnchorCell ? searchAnchorCell : mapService.WorldToCell(transform.position),
            EnemyState.Idle when hasIdleTargetCell => lastIdleTargetCell,
            _ => currentPath.Count > 0 ? currentPath[currentPath.Count - 1] : mapService.WorldToCell(transform.position)
        };
    }

    private void RememberTemporarilyBlockedCell(Vector3Int blockedCell)
    {
        Vector3Int currentCell = mapService.WorldToCell(transform.position);

        if (blockedCell == currentCell)
        {
            return;
        }

        temporarilyBlockedCell = blockedCell;
        temporarilyBlockedCellUntil = Time.time + temporaryBlockedCellMemoryDuration;
        hasTemporarilyBlockedCell = true;
        activityWatchdog?.ResetPathTracking();
    }

    private bool TryFindReachableNearbyCell(Vector3Int targetCell, int searchRadius, out Vector3Int reachableCell)
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

                    pathScratch.Clear();

                    if (TryBuildPath(currentCell, candidate, pathScratch))
                    {
                        reachableCell = candidate;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool UpdateSearchAnchorScan()
    {
        if (!hasSearchAnchorCell)
        {
            return false;
        }

        if (!IsAtCell(searchAnchorCell))
        {
            if (currentPath.Count == 0 || Time.time >= nextRepathTime)
            {
                bool hasPath = RequestPathTo(searchAnchorCell);
                nextRepathTime = Time.time + archetype.RepathInterval;

                if (!hasPath && !IsAtCell(searchAnchorCell))
                {
                    LogDebug($"Could not reach search anchor {FormatCell(searchAnchorCell)}. Falling back to local scan at {FormatCell(mapService.WorldToCell(transform.position))}.");
                    searchAnchorCell = mapService.WorldToCell(transform.position);
                    currentPath.Clear();
                }
            }

            return true;
        }

        currentPath.Clear();

        if (!isSearchAnchorScanActive)
        {
            isSearchAnchorScanActive = true;
            searchAnchorScanStartTime = Time.time;
            LogDebug($"Reached search anchor {FormatCell(searchAnchorCell)}. Scanning for target.");
        }

        float scanElapsed = Time.time - searchAnchorScanStartTime;

        if (scanElapsed < searchAnchorScanDuration)
        {
            float oscillation = Mathf.Sin(scanElapsed * searchAnchorScanCyclesPerSecond * Mathf.PI * 2f);
            float scanOffset = oscillation * searchAnchorScanSweepAngle * 0.5f;
            Vector3 rotatedFacing = Quaternion.Euler(0f, 0f, scanOffset) * (Vector3)searchAnchorBaseFacing;
            facingDirection = new Vector2(rotatedFacing.x, rotatedFacing.y);
            activityWatchdog?.ReportActivity();
            return true;
        }

        hasSearchAnchorCell = false;
        isSearchAnchorScanActive = false;
        nextRepathTime = 0f;
        return false;
    }

    private bool ShouldRefreshChasePath(Vector3Int desiredTargetCell)
    {
        if (!hasActiveChaseTargetCell)
        {
            return true;
        }

        if (desiredTargetCell == activeChaseTargetCell)
        {
            return currentPath.Count <= chasePathRefreshRemainingSteps;
        }

        Vector3Int currentCell = mapService.WorldToCell(transform.position);
        int retargetDistance = GetCellDistance(activeChaseTargetCell, desiredTargetCell);
        int distanceToActiveTarget = GetCellDistance(currentCell, activeChaseTargetCell);

        return retargetDistance >= chaseRetargetCellThreshold
            || distanceToActiveTarget <= 1
            || currentPath.Count <= chasePathRefreshRemainingSteps;
    }

    private void MoveAlongPath(float speed)
    {
        // Movement happens cell by cell along a path built by GridPathfinder, so door
        // logic and tile-based cover all stay consistent.
        if (currentPath.Count == 0)
        {
            activityWatchdog?.ResetPathTracking();
            return;
        }

        while (currentPath.Count > 0
            && Vector2.Distance(transform.position, mapService.CellToWorldCenter(currentPath[0])) <= PathArrivalThreshold)
        {
            currentPath.RemoveAt(0);
        }

        if (currentPath.Count == 0)
        {
            activityWatchdog?.ResetPathTracking();
            return;
        }

        TryOpenDoorAhead();

        Vector3Int nextCell = currentPath[0];
        Vector3 targetPosition = mapService.CellToWorldCenter(nextCell);
        Vector2 currentPosition = transform.position;
        Vector2 toTarget = (Vector2)targetPosition - currentPosition;
        float distanceBeforeMove = toTarget.magnitude;

        activityWatchdog?.BeginPathTracking(nextCell, distanceBeforeMove);

        if (Time.time >= nextBlockedPathRecoveryTime
            && ShouldTriggerBlockedPathRecovery(nextCell, targetPosition, distanceBeforeMove))
        {
            if (TryRecoverFromBlockedPath(nextCell, "movement blocked ahead"))
            {
                return;
            }
        }

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            if (!TryFaceInvestigateFocus())
            {
                facingDirection = toTarget.normalized;
            }
        }

        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, speed * Time.deltaTime);
        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
        TryEmitMovementNoise(currentPosition, nextPosition);

        float distanceAfterMove = Vector2.Distance(nextPosition, targetPosition);

        if (distanceAfterMove <= PathArrivalThreshold)
        {
            currentPath.RemoveAt(0);
            activityWatchdog?.ResetPathTracking();
            activityWatchdog?.ReportActivity(nextPosition);
            return;
        }

        if (distanceBeforeMove - distanceAfterMove > pathProgressEpsilon)
        {
            activityWatchdog?.BeginPathTracking(nextCell, distanceAfterMove);
            activityWatchdog?.ReportActivity(nextPosition);
            return;
        }

        if (Time.time >= nextBlockedPathRecoveryTime
            && activityWatchdog != null
            && activityWatchdog.TryConsumePathStall(nextCell, distanceAfterMove))
        {
            TryRecoverFromBlockedPath(nextCell, "path stall detected");
        }
    }

    private void TryEmitMovementNoise(Vector2 previousPosition, Vector2 currentPosition)
    {
        INoiseEventBus eventBus = ResolveNoiseEventBus();

        if (eventBus == null)
        {
            return;
        }

        if (!hasMovementNoiseAnchor)
        {
            lastMovementNoisePosition = previousPosition;
            hasMovementNoiseAnchor = true;
        }

        if (Time.time < nextMovementNoiseTime)
        {
            return;
        }

        float distanceSinceLastNoise = Vector2.Distance(lastMovementNoisePosition, currentPosition);

        if (distanceSinceLastNoise < movementNoiseTravelThreshold)
        {
            return;
        }

        float radius = movementNoiseRadius;
        NoiseSourceType sourceType = NoiseSourceType.Walk;

        if (currentState == EnemyState.Chase)
        {
            radius *= chaseMovementNoiseRadiusMultiplier;
            sourceType = NoiseSourceType.Sprint;
        }

        if (IsSuddenBurstActive())
        {
            radius *= suddenBurstNoiseRadiusMultiplier;
            sourceType = NoiseSourceType.Sprint;
        }

        eventBus.TryEmitNoise(
            currentPosition,
            radius,
            sourceType,
            gameObject.GetInstanceID(),
            NoiseEmitterAffiliation.Enemy);

        lastMovementNoisePosition = currentPosition;
        nextMovementNoiseTime = Time.time + movementNoiseInterval;
    }

    private void TryOpenDoorAhead()
    {
        // Door opening is proactive: the enemy checks its own cell, nearby cells, and a
        // small look-ahead in the path so it does not freeze against closed doors.
        if (mapService == null)
        {
            return;
        }

        Vector3Int currentCell = mapService.WorldToCell(transform.position);

        if (TryOpenDoorCell(currentCell))
        {
            return;
        }

        foreach (Vector3Int offset in CardinalOffsets)
        {
            if (TryOpenDoorCell(currentCell + offset))
            {
                return;
            }
        }

        int lookAheadCount = Mathf.Min(2, currentPath.Count);

        for (int index = 0; index < lookAheadCount; index++)
        {
            if (TryOpenDoorCell(currentPath[index]))
            {
                return;
            }
        }
    }

    private bool TryOpenDoorCell(Vector3Int cellPosition)
    {
        if (mapService == null || !mapService.IsDoorClosed(cellPosition))
        {
            return false;
        }

        if (MainEscapeRuntimeDoorRegistry.TryGetAtCell(cellPosition, out IMainEscapeRuntimeDoor runtimeDoor))
        {
            return runtimeDoor.TryOpenForEnemy(this);
        }

        if (DoorController.TryGetAtCell(cellPosition, out DoorController doorController))
        {
            return doorController.TryOpenForEnemy(this);
        }

        if (mapService.OpenDoor(cellPosition))
        {
            ResolveNoiseEventBus()?.TryEmitNoise(
                mapService.CellToWorldCenter(cellPosition),
                2.75f,
                NoiseSourceType.Door,
                gameObject.GetInstanceID(),
                NoiseEmitterAffiliation.Enemy);
            return true;
        }

        return false;
    }

    private bool IsAtCell(Vector3Int cellPosition)
    {
        if (mapService != null && mapService.WorldToCell(transform.position) == cellPosition)
        {
            return true;
        }

        return Vector2.Distance(transform.position, mapService.CellToWorldCenter(cellPosition)) <= PathArrivalThreshold;
    }

    private bool HasReachedLastSeenCell()
    {
        Vector3Int lastSeenCell = GetLastSeenPlayerCell();
        return IsAtCell(lastSeenCell) || (currentPath.Count == 0 && Vector2.Distance(transform.position, mapService.CellToWorldCenter(lastSeenCell)) <= 0.16f);
    }

    private bool HasReachedResolvedChaseCell()
    {
        Vector3Int chaseCell = hasActiveChaseTargetCell ? activeChaseTargetCell : GetLastSeenPlayerCell();
        return IsAtCell(chaseCell) || (currentPath.Count == 0 && Vector2.Distance(transform.position, mapService.CellToWorldCenter(chaseCell)) <= 0.16f);
    }

    private void ResolveLostPlayerAfterChase()
    {
        if (currentState != EnemyState.Chase || !HasReachedResolvedChaseCell())
        {
            return;
        }

        Vector3Int resolvedChaseCell = hasActiveChaseTargetCell ? activeChaseTargetCell : GetLastSeenPlayerCell();
        LogDebug($"Reached chase resolution cell {FormatCell(resolvedChaseCell)} without reacquiring target. Starting search and scan.");
        BeginSearch(resolvedChaseCell);
    }

    private void UpdateFacing(VisionSensor2D.VisionReading playerReading)
    {
        if (!playerReading.CanSee)
        {
            return;
        }

        Vector2 lookDirection = (playerTarget.AimPoint - (Vector2)transform.position).normalized;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            facingDirection = lookDirection;
        }
    }

    private void ApplyFacing()
    {
        // The root object rotates in world space, while child visuals stay locally aligned.
        // This keeps the vision cone and body marker reading from one shared facing value.
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 normalizedFacing = facingDirection.normalized;
        float facingAngle = Mathf.Atan2(normalizedFacing.y, normalizedFacing.x) * Mathf.Rad2Deg - 90f;
        if (hasAppliedFacing && Mathf.Abs(Mathf.DeltaAngle(lastAppliedFacingAngle, facingAngle)) <= 0.01f)
        {
            return;
        }

        Quaternion facingRotation = Quaternion.Euler(0f, 0f, facingAngle);

        transform.SetPositionAndRotation(transform.position, facingRotation);

        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            // Keep the AI root freely rotated for sensing, but counter-rotate the
            // visual subtree so 4-direction sprites stay upright in world space.
            visualRoot.localRotation = Quaternion.Inverse(facingRotation);
        }

        if (visionOrigin != null)
        {
            visionOrigin.localPosition = Vector3.zero;
            visionOrigin.localRotation = Quaternion.identity;
        }

        lastAppliedFacingAngle = facingAngle;
        hasAppliedFacing = true;
    }

    private void ForceApplyFacing()
    {
        hasAppliedFacing = false;
        ApplyFacing();
    }

    private void UpdateVisual(VisionSensor2D.VisionReading playerReading)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        CacheFacingMarkerDefaults();
        spriteAnimationDriver ??= GetComponent<EnemySpriteAnimationDriver>();

        bool attackRecovering = IsAttackRecovering;
        bool preserveSpriteColors = spriteAnimationDriver != null && spriteAnimationDriver.PreserveSpriteColors;
        int detectionBucket = Mathf.RoundToInt(Mathf.Clamp01(visualDetectionMeter) * 100f);
        if (!playerReading.CanSee
            && !attackRecovering
            && hasCachedVisualState
            && cachedVisualState == currentState
            && cachedVisualCanSee == playerReading.CanSee
            && cachedVisualDetectionBucket == detectionBucket
            && cachedVisualAttackRecovering == attackRecovering)
        {
            return;
        }

        Color bodyColor = ResolveBodyColor(playerReading.CanSee, attackRecovering, preserveSpriteColors);

        bodyRenderer.color = bodyColor;

        if (facingMarkerRenderer != null)
        {
            Color markerColor = currentState == EnemyState.Chase
                ? new Color(1f, 0.98f, 0.6f, 1f)
                : new Color(0.48f, 0.08f, 0.08f, 1f);

            float markerScaleMultiplier = 1f;
            int markerSortingOrder = hasFacingMarkerDefaults ? facingMarkerBaseSortingOrder : facingMarkerRenderer.sortingOrder;

            if (playerReading.CanSee)
            {
                float pulse = 1f + Mathf.PingPong(Time.time * recognizedGlowPulseSpeed, recognizedGlowPulseStrength);
                markerColor = Color.Lerp(recognizedGlowColor, Color.white, 0.58f) * pulse;
                markerColor.a = 1f;
                markerScaleMultiplier = recognizedGlowScaleMultiplier * pulse;
                markerSortingOrder = Mathf.Max(markerSortingOrder, recognizedGlowSortingOrder);
            }
            else if (visualDetectionMeter > 0.001f)
            {
                float partialBlend = Mathf.Clamp01(visualDetectionMeter * 0.82f);
                markerColor = Color.Lerp(markerColor, recognizedGlowColor, partialBlend);
                markerScaleMultiplier = Mathf.Lerp(1f, 1.62f, partialBlend);
            }

            if (attackRecovering)
            {
                markerColor = Color.Lerp(markerColor, new Color(1f, 0.92f, 0.72f, 1f), 0.65f);
            }

            facingMarkerRenderer.color = markerColor;
            facingMarkerRenderer.sortingOrder = markerSortingOrder;
            facingMarkerRenderer.transform.localScale = facingMarkerBaseScale * markerScaleMultiplier;
        }

        cachedVisualState = currentState;
        cachedVisualCanSee = playerReading.CanSee;
        cachedVisualAttackRecovering = attackRecovering;
        cachedVisualDetectionBucket = detectionBucket;
        hasCachedVisualState = true;
    }

    private Color ResolveBodyColor(bool canSeePlayer, bool attackRecovering, bool preserveSpriteColors)
    {
        Color bodyColor = preserveSpriteColors
            ? ResolvePreservedSpriteBaseColor()
            : ResolveLegacyBodyColor();

        if (canSeePlayer)
        {
            Color playerSpottedTint = preserveSpriteColors
                ? new Color(1f, 0.84f, 0.84f, 1f)
                : new Color(1f, 0.1f, 0.1f, 1f);
            float playerSpottedBlend = preserveSpriteColors ? 0.22f : 0.3f;
            bodyColor = Color.Lerp(bodyColor, playerSpottedTint, playerSpottedBlend);
        }
        else if (visualDetectionMeter > 0.001f)
        {
            Color awarenessTint = preserveSpriteColors
                ? new Color(1f, 0.93f, 0.82f, 1f)
                : new Color(1f, 0.66f, 0.24f, 1f);
            float awarenessBlend = Mathf.Clamp01(visualDetectionMeter * (preserveSpriteColors ? 0.16f : 0.22f));
            bodyColor = Color.Lerp(bodyColor, awarenessTint, awarenessBlend);
        }

        if (attackRecovering)
        {
            float recoveryBlend = 0.45f + Mathf.PingPong(Time.time * 5.5f, 0.25f);
            Color recoveryTint = preserveSpriteColors
                ? new Color(1f, 0.95f, 0.86f, 1f)
                : new Color(1f, 0.86f, 0.48f, 1f);
            float appliedBlend = preserveSpriteColors ? Mathf.Min(0.35f, recoveryBlend * 0.45f) : recoveryBlend;
            bodyColor = Color.Lerp(bodyColor, recoveryTint, appliedBlend);
        }

        return bodyColor;
    }

    private Color ResolveLegacyBodyColor()
    {
        return currentState switch
        {
            EnemyState.Idle => new Color(0.62f, 0.66f, 0.74f, 1f),
            EnemyState.Investigate => new Color(1f, 0.82f, 0.28f, 1f),
            EnemyState.Chase => new Color(1f, 0.34f, 0.34f, 1f),
            EnemyState.Search => new Color(0.96f, 0.46f, 0.82f, 1f),
            _ => Color.white
        };
    }

    private Color ResolvePreservedSpriteBaseColor()
    {
        return currentState switch
        {
            EnemyState.Idle => Color.white,
            EnemyState.Investigate => new Color(1f, 0.97f, 0.9f, 1f),
            EnemyState.Chase => new Color(1f, 0.91f, 0.91f, 1f),
            EnemyState.Search => new Color(0.98f, 0.94f, 1f, 1f),
            _ => Color.white
        };
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

    private float ResolveIdleMovementSpeed()
    {
        if (archetype == null)
        {
            return 0f;
        }

        if (IsStandGuardEnemy() && hasIdleTargetCell)
        {
            return archetype.InvestigateSpeed;
        }

        return archetype.PatrolSpeed;
    }

    private bool IsStandGuardEnemy()
    {
        return archetype != null && archetype.IdleBehavior == EnemyIdleBehavior.StandGuard;
    }

    private bool IsFollowTrailEnemy()
    {
        return archetype != null && archetype.IdleBehavior == EnemyIdleBehavior.FollowTrail;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log($"[EnemyAI:{gameObject.name}] {message}", this);
    }

    private static string FormatCell(Vector3Int cellPosition)
    {
        return $"({cellPosition.x}, {cellPosition.y})";
    }

    private static string FormatWorld(Vector2 worldPosition)
    {
        return $"({worldPosition.x:0.00}, {worldPosition.y:0.00})";
    }

    private static int GetCellDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static Vector3Int ScaleCellDirection(Vector3Int direction, int scale)
    {
        return new Vector3Int(direction.x * scale, direction.y * scale, 0);
    }

    private void OnDrawGizmosSelected()
    {
        if (mapService == null)
        {
            return;
        }

        Gizmos.color = new Color(0.95f, 0.6f, 0.22f, 0.85f);
        Vector3 previousPoint = transform.position;

        foreach (Vector3Int cell in currentPath)
        {
            Vector3 point = mapService.CellToWorldCenter(cell);
            Gizmos.DrawLine(previousPoint, point);
            Gizmos.DrawSphere(point, 0.06f);
            previousPoint = point;
        }
    }
}


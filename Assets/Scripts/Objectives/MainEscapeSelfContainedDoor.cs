using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class MainEscapeSelfContainedDoor : PlayerInteractable2D, IMainEscapeRuntimeDoor
{
    private const float RegistrationRetryInterval = 0.5f;
    private const float MinimumBoundsSize = 0.0001f;
    private const int DoorAboveFogSortingOrder = 110;
    private const float SideDoorInteractionAssistDistance = 0.35f;
    private const float SideDoorLineOfSightSideOffset = 0.08f;
    private const float SideDoorPromptSideOffset = 0.32f;
    private const float SideDoorPromptVerticalOffsetFactor = 0.38f;

    [SerializeField] private string keyId = string.Empty;
    [SerializeField] private bool marksMainGate;
    [SerializeField] private bool showPromptText = true;
    [SerializeField] private bool isOpen;
    [SerializeField] private GridMapService mapService;
    [SerializeField] private ObjectiveManager objectiveManager;
    [SerializeField] private NoiseSystem noiseSystem;
    [SerializeField] private MainEscapeDoorVisualVariantKind visualVariant = MainEscapeDoorVisualVariantKind.None;
    [SerializeField] private Transform doorCellMarkersRoot;
    [SerializeField] private Transform[] doorCellMarkers = Array.Empty<Transform>();
    [SerializeField] private Transform openVisual;
    [SerializeField] private SpriteRenderer closedRenderer;
    [SerializeField] private SpriteRenderer openRenderer;
    [SerializeField] private BoxCollider2D blockerCollider;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.6f;
    [SerializeField, Min(0f)] private float enemyDoorNoiseRadius = 2.75f;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.12f;
    [SerializeField] private bool hasCapturedVisualDefaults;
    [SerializeField] private Color closedRendererBaseColor = Color.white;
    [SerializeField] private Color openRendererBaseColor = Color.white;
    [SerializeField] private Vector3Int[] resolvedDoorCells = Array.Empty<Vector3Int>();

    private Vector3Int[] registeredDoorCells = Array.Empty<Vector3Int>();
    private bool isRegisteredWithRuntimeServices;
    private float visualOpenAmount;
    private float nextRegistrationAttemptTime;
    private INoiseEventBus noiseEventBus;

    public bool RequiresKey => !string.IsNullOrWhiteSpace(keyId);
    public bool MarksMainGate => marksMainGate;
    public Vector3Int[] DoorCells => resolvedDoorCells ?? Array.Empty<Vector3Int>();
    public bool IsOpen => isOpen;
    public bool IsAvailable => this != null && isActiveAndEnabled;
    protected override float MaxInteractionDistance => interactionDistance;
    public override Vector2 InteractionPoint => ResolveBlockerBounds().center;

    public void ConfigureRuntimeReferences(
        GridMapService configuredMapService,
        ObjectiveManager configuredObjectiveManager,
        INoiseEventBus configuredNoiseEventBus = null)
    {
        mapService = configuredMapService;
        objectiveManager = configuredObjectiveManager;
        noiseEventBus = configuredNoiseEventBus;
        TryRegisterWithRuntimeServices(forceRefreshCells: true);
    }

    private void Reset()
    {
        CacheReferences();
        ApplyDoorLayer();
        CaptureVisualDefaultsIfNeeded();
        ApplyVisualState(immediate: true);
    }

    private void Awake()
    {
        CacheReferences();
        ApplyDoorLayer();
        CaptureVisualDefaultsIfNeeded();
        ApplyVisualState(immediate: true);
        ApplyRuntimeBlockerState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        TryRegisterWithRuntimeServices(forceRefreshCells: true);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnregisterFromRuntimeServices();
    }

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        enemyDoorNoiseRadius = Mathf.Max(0f, enemyDoorNoiseRadius);
        animationDuration = Mathf.Max(0.01f, animationDuration);

        CacheReferences();
        ApplyDoorLayer();
        CaptureVisualDefaultsIfNeeded(forceRefresh: !isOpen);
        ApplyVisualState(immediate: true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyVisualState(immediate: true);
            ApplyRuntimeBlockerState();
            return;
        }

        if (HasPendingVisualState())
        {
            ApplyVisualState(immediate: false);
        }

        if (!isRegisteredWithRuntimeServices && Time.unscaledTime >= nextRegistrationAttemptTime)
        {
            nextRegistrationAttemptTime = Time.unscaledTime + RegistrationRetryInterval;
            TryRegisterWithRuntimeServices(forceRefreshCells: true);
        }
    }

    public Vector3Int[] ResolveDoorCells(Tilemap groundTilemap)
    {
        if (groundTilemap == null)
        {
            return Array.Empty<Vector3Int>();
        }

        List<Vector3Int> explicitCells = CollectExplicitDoorCells(groundTilemap);
        if (explicitCells.Count > 0)
        {
            return explicitCells.ToArray();
        }

        Vector3Int anchorCell = MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, transform.position);
        MainEscapeDoorVisualVariantKind resolvedVariant = ResolveVisualVariant();

        if (resolvedVariant == MainEscapeDoorVisualVariantKind.SideDoor42)
        {
            return new[] { anchorCell, anchorCell + Vector3Int.up };
        }

        if (resolvedVariant == MainEscapeDoorVisualVariantKind.FrontDoor)
        {
            return new[] { anchorCell, anchorCell + Vector3Int.right };
        }

        return new[] { anchorCell };
    }

    public bool MatchesDoorCells(Tilemap groundTilemap, Vector3Int[] candidateCells)
    {
        Vector3Int[] doorCells = ResolveDoorCells(groundTilemap);
        return MainEscapeDoorRuntimeUtility.CellsMatch(doorCells, candidateCells);
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        Bounds blockerBounds = ResolveBlockerBounds();
        float distance = blockerBounds.size.sqrMagnitude > 0.0001f
            ? Vector2.Distance(playerController.transform.position, blockerBounds.ClosestPoint(playerController.transform.position))
            : base.GetInteractionDistance(playerController);
        return Mathf.Max(0f, distance - ResolveInteractionAssistDistance());
    }

    public override bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        return base.CanInteractAtDistance(playerController, interactionDistance);
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        if (ResolveVisualVariant() == MainEscapeDoorVisualVariantKind.SideDoor42 && playerController != null)
        {
            return ResolveSideDoorApproachPoint(playerController);
        }

        return playerController != null
            ? ResolveBlockerBounds().ClosestPoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override bool TryGetPromptWorldPosition(WasdPlayerController playerController, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (ResolveVisualVariant() != MainEscapeDoorVisualVariantKind.SideDoor42)
        {
            return false;
        }

        Bounds blockerBounds = ResolveBlockerBounds();
        float horizontalDirection = ResolvePlayerHorizontalSide(playerController, blockerBounds);

        worldPosition = new Vector3(
            blockerBounds.center.x + horizontalDirection * (blockerBounds.extents.x + SideDoorPromptSideOffset),
            blockerBounds.center.y + blockerBounds.extents.y * SideDoorPromptVerticalOffsetFactor,
            transform.position.z);
        return true;
    }

    public override bool AllowsLineOfSightBlocker(Collider2D blocker, Vector2 hitPoint, WasdPlayerController playerController)
    {
        if (blocker == null)
        {
            return false;
        }

        Transform blockerTransform = blocker.transform;
        return blocker == blockerCollider
            || blockerTransform == transform
            || blockerTransform.IsChildOf(transform)
            || IsDoorCellLineOfSightBlocker(blocker, hitPoint);
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (!showPromptText)
        {
            return string.Empty;
        }

        if (!isOpen && !IsUnlockedForPlayer())
        {
            return "Door Locked";
        }

        return isOpen ? "E Close Door" : "E Open Door";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        if (!isOpen && !IsUnlockedForPlayer())
        {
            PrototypeAudioManager.TryPlayDenied();
            return;
        }

        if (isOpen && IsActorBlockingDoorway(playerController))
        {
            PrototypeAudioManager.TryPlayDenied();
            return;
        }

        SetOpenStateInternal(
            !isOpen,
            playerController != null ? playerController.gameObject.GetInstanceID() : 0,
            playerController != null ? NoiseEmitterAffiliation.Player : NoiseEmitterAffiliation.Neutral,
            playAudio: true,
            emitNoise: true);
    }

    public bool SetOpenState(bool open)
    {
        return SetOpenStateInternal(
            open,
            emitterId: 0,
            emitterAffiliation: NoiseEmitterAffiliation.Neutral,
            playAudio: false,
            emitNoise: false);
    }

    public bool TryOpenForEnemy(EnemyStateMachine opener)
    {
        return SetOpenStateInternal(
            open: true,
            emitterId: opener != null ? opener.gameObject.GetInstanceID() : 0,
            emitterAffiliation: NoiseEmitterAffiliation.Enemy,
            playAudio: true,
            emitNoise: true);
    }

    private bool SetOpenStateInternal(
        bool open,
        int emitterId,
        NoiseEmitterAffiliation emitterAffiliation,
        bool playAudio,
        bool emitNoise)
    {
        if (isOpen == open)
        {
            ApplyRuntimeBlockerState();
            return false;
        }

        isOpen = open;
        ApplyRuntimeBlockerState();
        ApplyVisualState(immediate: false);

        if (playAudio)
        {
            if (open)
            {
                PrototypeAudioManager.TryPlayDoorOpen();
            }
            else
            {
                PrototypeAudioManager.TryPlayDoorClose();
            }
        }

        if (emitNoise)
        {
            ResolveNoiseEventBus()?.TryEmitNoise(
                InteractionPoint,
                enemyDoorNoiseRadius,
                NoiseSourceType.Door,
                emitterId,
                emitterAffiliation);
        }

        return true;
    }

    private void CacheReferences()
    {
        closedRenderer ??= GetComponent<SpriteRenderer>();
        blockerCollider ??= GetComponent<BoxCollider2D>();

        if (openVisual == null)
        {
            openVisual = transform.Find("OpenVisual");
        }

        if (openVisual == null)
        {
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);

                if (child != null && child.GetComponentInChildren<SpriteRenderer>(true) != null)
                {
                    openVisual = child;
                    break;
                }
            }
        }

        if (openRenderer == null && openVisual != null)
        {
            openRenderer = openVisual.GetComponentInChildren<SpriteRenderer>(true);
        }

        if ((doorCellMarkers == null || doorCellMarkers.Length == 0) && doorCellMarkersRoot == null)
        {
            doorCellMarkersRoot = transform.Find("DoorCells");
        }

        if (doorCellMarkersRoot != null)
        {
            List<Transform> markers = new();

            for (int index = 0; index < doorCellMarkersRoot.childCount; index++)
            {
                Transform marker = doorCellMarkersRoot.GetChild(index);
                if (marker != null)
                {
                    markers.Add(marker);
                }
            }

            if (markers.Count > 0)
            {
                doorCellMarkers = markers.ToArray();
            }
        }
    }

    private void CaptureVisualDefaultsIfNeeded(bool forceRefresh = false)
    {
        if (!forceRefresh && hasCapturedVisualDefaults && HasValidCapturedVisualDefaults())
        {
            return;
        }

        CaptureVisualDefaults();
    }

    private bool HasValidCapturedVisualDefaults()
    {
        return openRenderer == null || openRendererBaseColor.a > MinimumBoundsSize;
    }

    private void CaptureVisualDefaults()
    {
        CacheReferences();

        if (closedRenderer != null)
        {
            closedRendererBaseColor = closedRenderer.color;
        }

        if (openRenderer != null)
        {
            openRendererBaseColor = ResolveOpenRendererBaseColor(openRenderer.color);
        }

        hasCapturedVisualDefaults = true;
    }

    private void ApplyDoorLayer()
    {
        gameObject.layer = GameLayers.DoorIndex;

        if (Application.isPlaying && openVisual != null)
        {
            ApplyLayerRecursively(openVisual, GameLayers.DoorIndex);
        }
    }

    private MainEscapeDoorVisualVariantKind ResolveVisualVariant()
    {
        return visualVariant != MainEscapeDoorVisualVariantKind.None
            ? visualVariant
            : MainEscapeDoorVisualVariantResolver.ResolveForVisualRoot(transform);
    }

    private List<Vector3Int> CollectExplicitDoorCells(Tilemap groundTilemap)
    {
        List<Vector3Int> cells = new();

        if (groundTilemap == null || doorCellMarkers == null || doorCellMarkers.Length == 0)
        {
            return cells;
        }

        HashSet<Vector3Int> unique = new();

        for (int index = 0; index < doorCellMarkers.Length; index++)
        {
            Transform marker = doorCellMarkers[index];
            if (marker == null)
            {
                continue;
            }

            Vector3Int cell = MainEscapeTilemapCellUtility.WorldToCell2D(groundTilemap, marker.position);
            if (unique.Add(cell))
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    private bool TryRegisterWithRuntimeServices(bool forceRefreshCells)
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        if (!TryResolveMapService())
        {
            return false;
        }

        Vector3Int[] doorCells = ResolveDoorCells(mapService.GroundTilemap);
        if (doorCells.Length == 0)
        {
            return false;
        }

        if (forceRefreshCells || !MainEscapeDoorRuntimeUtility.CellsMatch(resolvedDoorCells, doorCells))
        {
            resolvedDoorCells = doorCells;
        }

        if (isRegisteredWithRuntimeServices
            && MainEscapeDoorRuntimeUtility.CellsMatch(registeredDoorCells, resolvedDoorCells))
        {
            ApplyRuntimeBlockerState();
            return true;
        }

        UnregisterFromRuntimeServices();
        registeredDoorCells = (Vector3Int[])resolvedDoorCells.Clone();
        MainEscapeRuntimeDoorRegistry.Register(this);
        isRegisteredWithRuntimeServices = true;

        for (int index = 0; index < registeredDoorCells.Length; index++)
        {
            mapService.SuppressDoorTile(registeredDoorCells[index]);
            mapService.SetDoorShadowBlockerActive(registeredDoorCells[index], !isOpen);
        }

        ApplyRuntimeBlockerState();
        return true;
    }

    private void UnregisterFromRuntimeServices()
    {
        if (!isRegisteredWithRuntimeServices)
        {
            registeredDoorCells = Array.Empty<Vector3Int>();
            return;
        }

        MainEscapeRuntimeDoorRegistry.Unregister(this);

        if (mapService != null)
        {
            for (int index = 0; index < registeredDoorCells.Length; index++)
            {
                mapService.RestoreDoorTile(registeredDoorCells[index]);
                mapService.SetDoorShadowBlockerActive(registeredDoorCells[index], active: true);
            }
        }

        registeredDoorCells = Array.Empty<Vector3Int>();
        isRegisteredWithRuntimeServices = false;
    }

    private bool TryResolveMapService()
    {
        if (mapService != null && mapService.GroundTilemap != null)
        {
            return true;
        }

        mapService = RSceneReferenceLookup.FindUniqueComponentInScene<GridMapService>(
            gameObject.scene,
            this,
            nameof(MainEscapeSelfContainedDoor),
            nameof(mapService));
        return mapService != null && mapService.GroundTilemap != null;
    }

    private bool IsDoorCellLineOfSightBlocker(Collider2D blocker, Vector2 hitPoint)
    {
        if (blocker == null || mapService == null || mapService.GroundTilemap == null)
        {
            return false;
        }

        Vector3Int[] doorCells = ResolveLineOfSightDoorCells();
        if (doorCells.Length == 0)
        {
            Vector3Int[] resolvedCells = ResolveDoorCells(mapService.GroundTilemap);
            if (resolvedCells.Length == 0)
            {
                return false;
            }

            resolvedDoorCells = resolvedCells;
            doorCells = resolvedDoorCells;
        }

        if (ContainsDoorCell(doorCells, mapService.WorldToCell(hitPoint)))
        {
            return true;
        }

        Vector2 blockerPoint = blocker.ClosestPoint(InteractionPoint);
        return ContainsDoorCell(doorCells, mapService.WorldToCell(blockerPoint));
    }

    private Vector3Int[] ResolveLineOfSightDoorCells()
    {
        return registeredDoorCells != null && registeredDoorCells.Length > 0
            ? registeredDoorCells
            : resolvedDoorCells ?? Array.Empty<Vector3Int>();
    }

    private static bool ContainsDoorCell(Vector3Int[] doorCells, Vector3Int cell)
    {
        for (int index = 0; index < doorCells.Length; index++)
        {
            if (doorCells[index] == cell)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyRuntimeBlockerState()
    {
        if (blockerCollider != null)
        {
            blockerCollider.enabled = !isOpen;
            blockerCollider.isTrigger = false;
        }

        if (mapService == null)
        {
            return;
        }

        Vector3Int[] doorCells = isRegisteredWithRuntimeServices ? registeredDoorCells : resolvedDoorCells;

        for (int index = 0; index < doorCells.Length; index++)
        {
            mapService.SetDoorShadowBlockerActive(doorCells[index], !isOpen);
        }
    }

    private void ApplyVisualState(bool immediate)
    {
        CacheReferences();
        CaptureVisualDefaultsIfNeeded();

        float targetOpenAmount = isOpen ? 1f : 0f;
        visualOpenAmount = immediate || !Application.isPlaying
            ? targetOpenAmount
            : Mathf.MoveTowards(visualOpenAmount, targetOpenAmount, Time.unscaledDeltaTime / animationDuration);

        if (closedRenderer != null)
        {
            ApplyAboveFogSorting(closedRenderer);
            closedRenderer.color = WithAlpha(closedRendererBaseColor, Mathf.Lerp(closedRendererBaseColor.a, 0f, visualOpenAmount));
        }

        if (openRenderer != null)
        {
            ApplyAboveFogSorting(openRenderer);
            Color visibleOpenRendererBaseColor = ResolveOpenRendererBaseColor(openRendererBaseColor);
            openRenderer.color = WithAlpha(
                visibleOpenRendererBaseColor,
                Mathf.Lerp(0f, visibleOpenRendererBaseColor.a, visualOpenAmount));
        }
    }

    private bool HasPendingVisualState()
    {
        float targetOpenAmount = isOpen ? 1f : 0f;
        return !Mathf.Approximately(visualOpenAmount, targetOpenAmount);
    }

    private bool IsUnlockedForPlayer()
    {
        if (objectiveManager == null)
        {
            objectiveManager = RSceneReferenceLookup.FindUniqueComponentInScene<ObjectiveManager>(
                gameObject.scene,
                this,
                nameof(MainEscapeSelfContainedDoor),
                nameof(objectiveManager));
        }

        return !RequiresKey || objectiveManager == null || objectiveManager.HasKey(keyId);
    }

    private bool IsActorBlockingDoorway(WasdPlayerController playerController)
    {
        Bounds doorwayBounds = ResolveBlockerBounds();

        if (playerController != null && doorwayBounds.Contains(playerController.transform.position))
        {
            return true;
        }

        EnemyStateMachine[] enemies = RSceneReferenceLookup.FindComponentsInScene<EnemyStateMachine>(gameObject.scene);

        for (int index = 0; index < enemies.Length; index++)
        {
            EnemyStateMachine enemy = enemies[index];

            if (enemy != null && doorwayBounds.Contains(enemy.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private Bounds ResolveBlockerBounds()
    {
        if (blockerCollider == null)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        Vector3 center = transform.TransformPoint(blockerCollider.offset);
        Vector3 lossyScale = transform.lossyScale;
        Vector3 size = new(
            Mathf.Abs(blockerCollider.size.x * lossyScale.x),
            Mathf.Abs(blockerCollider.size.y * lossyScale.y),
            0.1f);
        return new Bounds(center, size);
    }

    private float ResolveInteractionAssistDistance()
    {
        return ResolveVisualVariant() == MainEscapeDoorVisualVariantKind.SideDoor42
            ? SideDoorInteractionAssistDistance
            : 0f;
    }

    private Vector2 ResolveSideDoorApproachPoint(WasdPlayerController playerController)
    {
        Bounds blockerBounds = ResolveBlockerBounds();
        float horizontalDirection = ResolvePlayerHorizontalSide(playerController, blockerBounds);
        float clampedY = Mathf.Clamp(
            playerController.transform.position.y,
            blockerBounds.min.y + SideDoorLineOfSightSideOffset,
            blockerBounds.max.y - SideDoorLineOfSightSideOffset);

        return new Vector2(
            blockerBounds.center.x + horizontalDirection * (blockerBounds.extents.x + SideDoorLineOfSightSideOffset),
            clampedY);
    }

    private static float ResolvePlayerHorizontalSide(WasdPlayerController playerController, Bounds blockerBounds)
    {
        return playerController != null && playerController.transform.position.x > blockerBounds.center.x
            ? 1f
            : -1f;
    }

    private static void ApplyLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;

        for (int index = 0; index < root.childCount; index++)
        {
            ApplyLayerRecursively(root.GetChild(index), layer);
        }
    }

    private static Color ResolveOpenRendererBaseColor(Color source)
    {
        if (source.a <= MinimumBoundsSize)
        {
            source.a = 1f;
        }

        return source;
    }

    private static void ApplyAboveFogSorting(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, DoorAboveFogSortingOrder);
        renderer.maskInteraction = SpriteMaskInteraction.None;
    }

    private static Color WithAlpha(Color source, float alpha)
    {
        source.a = Mathf.Clamp01(alpha);
        return source;
    }
}

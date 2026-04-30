/*
 * File Role:
 * Contains the current prototype objective manager and door logic.
 *
 * Runtime Use:
 * Keeps the remaining live objective coordinator and door behavior for the demo flow.
 *
 * Study Notes:
 * Study this with PlayerInventory to see how the remaining mission loop pieces fit together.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class ObjectiveManager : MonoBehaviour
{
    [SerializeField] private string[] requiredExitKeyIds = Array.Empty<string>();
    [SerializeField] private bool escaped;
    private readonly HashSet<string> collectedKeys = new(StringComparer.Ordinal);

    public int CollectedKeyCount => collectedKeys.Count;
    public bool IsEscaped => escaped;

    public void Configure(params string[] requiredKeys)
    {
        requiredExitKeyIds = requiredKeys ?? Array.Empty<string>();
    }

    public bool TryCollectKey(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId) || !collectedKeys.Add(keyId))
        {
            return false;
        }

        Debug.Log($"Collected key: {keyId}", this);
        return true;
    }

    public bool HasKey(string keyId)
    {
        return !string.IsNullOrWhiteSpace(keyId) && collectedKeys.Contains(keyId);
    }

    public bool CanUseExit()
    {
        if (escaped)
        {
            return false;
        }

        foreach (string requiredKeyId in requiredExitKeyIds)
        {
            if (!HasKey(requiredKeyId))
            {
                return false;
            }
        }

        return true;
    }

    public bool CanUseExit(PlayerInventory inventory)
    {
        if (escaped || inventory == null)
        {
            return false;
        }

        foreach (string requiredKeyId in requiredExitKeyIds)
        {
            if (!inventory.HasItem(requiredKeyId))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryUseExit(PlayerInventory inventory)
    {
        if (!CanUseExit(inventory))
        {
            return false;
        }

        foreach (string requiredKeyId in requiredExitKeyIds)
        {
            if (!inventory.RemoveItem(requiredKeyId))
            {
                return false;
            }
        }

        escaped = true;
        Debug.Log("Exit used successfully.", this);
        return true;
    }
}

[DisallowMultipleComponent]
public sealed class DoorController : PlayerInteractable2D
{
    private enum AuthoredDoorVisualVariant
    {
        None,
        FrontDoor,
        SideDoor42
    }

    private static readonly Vector3 FrontDoorOpenVisualFixedScale = new(1.4f, 1.4f, 1f);
    private const string CustomAuthoredOpenDoorResourcePath = "MainEscape/DoorSprites/CustomOpenDoor";
    private const string CustomSideDoorClosedResourcePath = "MainEscape/DoorSprites/CustomSideDoorClosed";
    private const string CustomSideDoorOpenResourcePath = "MainEscape/DoorSprites/CustomSideDoorOpen";
    private const string FrontDoorClosedPrefabResourcePath = "MainEscape/DoorPrefabs/FrontDoorClosed";
    private const string FrontDoorOpenPrefabResourcePath = "MainEscape/DoorPrefabs/FrontDoorOpen";
    private const string SideDoorClosedPrefabResourcePath = "MainEscape/DoorPrefabs/SideDoor/SideDoorClosed";
    private const string SideDoorOpenPrefabResourcePath = "MainEscape/DoorPrefabs/SideDoor/SideDoorOpen";
    private const int DoorAboveFogSortingOrder = 110;
    private static readonly List<DoorController> ActiveDoors = new();
    private static readonly Dictionary<Vector3Int, DoorController> ActiveDoorByCell = new();
    private static Sprite sharedMarkerSprite;
    private static Sprite sharedDoorIndicatorSprite;
    private static Sprite sharedCustomAuthoredOpenDoorSprite;
    private static Sprite sharedCustomSideDoorClosedSprite;
    private static Sprite sharedCustomSideDoorOpenSprite;
    private static Material sharedFrontDoorOpenMaterial;
    private static Material sharedDoorPrefabMaterial;
    private static GameObject sharedFrontDoorClosedPrefab;
    private static GameObject sharedFrontDoorOpenPrefab;
    private static GameObject sharedSideDoorClosedPrefab;
    private static GameObject sharedSideDoorOpenPrefab;
    [SerializeField] private string keyId = "demo.elevator";
    [SerializeField] private Vector3Int[] doorCells = { new Vector3Int(2, 0, 0) };
    [SerializeField] private ObjectiveManager objectiveManager;
    [SerializeField] private GridMapService mapService;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.6f;
    [SerializeField, Min(0f)] private float enemyDoorNoiseRadius = 2.75f;
    [SerializeField] private bool isOpen;
    [SerializeField] private bool builtInVisualsEnabled = true;
    [SerializeField] private bool overlayFeedbackEnabled = true;
    [SerializeField] private SpriteRenderer panelRenderer;
    [SerializeField] private SpriteRenderer frameRenderer;
    [SerializeField] private SpriteRenderer openingRenderer;
    [SerializeField] private SpriteRenderer leafPrimaryRenderer;
    [SerializeField] private SpriteRenderer leafSecondaryRenderer;
    [SerializeField] private SpriteRenderer beaconRenderer;
    [SerializeField] private SpriteRenderer lowerBeaconRenderer;
    [SerializeField] private SpriteRenderer headerSignPlateRenderer;
    [SerializeField] private SpriteRenderer headerSignIconRenderer;
    [SerializeField] private SpriteRenderer authoredOpenDoorRenderer;
    [SerializeField] private GameObject closedDoorPrefab;
    [SerializeField] private GameObject openDoorPrefab;
    [SerializeField] private MainEscapeDoorPresentationController doorPresentationController;
    [SerializeField] private BoxCollider2D physicalBlockerCollider;
    [SerializeField] private BoxCollider2D lightBlockerCollider;
    [SerializeField] private ShadowCaster2D lightBlockerShadowCaster;
    [SerializeField] private MainEscapeDoorPassabilityController doorPassabilityController;
    [SerializeField] private Transform[] authoredVisualRoots = Array.Empty<Transform>();
    [SerializeField] private Vector3[] authoredVisualClosedLocalPositions = Array.Empty<Vector3>();
    [SerializeField] private Vector3[] authoredVisualClosedLocalScales = Array.Empty<Vector3>();
    [SerializeField] private AuthoredDoorVisualVariant authoredDoorVisualVariant;
    private SpriteRenderer[][] authoredVisualRenderersByRoot = Array.Empty<SpriteRenderer[]>();
    private Color[][] authoredVisualBaseColorsByRoot = Array.Empty<Color[]>();
    private Sprite[][] authoredVisualOriginalSpritesByRoot = Array.Empty<Sprite[]>();
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private NoiseSystem noiseSystem;
    [SerializeField] private Color closedColor = new(1f, 0.74f, 0.24f, 0.82f);
    [SerializeField] private Color openColor = new(0.34f, 0.98f, 0.82f, 0.34f);
    [SerializeField] private Color lockedColor = new(1f, 0.28f, 0.26f, 0.86f);
    [SerializeField] private Color beaconColor = new(1f, 0.96f, 0.8f, 1f);
    [SerializeField] private Color frameColor = new(0.09f, 0.1f, 0.11f, 0.9f);
    [SerializeField] private Color headerSignPlateColor = new(0.08f, 0.11f, 0.13f, 0.96f);
    [SerializeField] private Color headerSignIconColor = new(0.97f, 0.95f, 0.84f, 1f);
    [SerializeField] private float visualOpenAmount;
    [SerializeField] private float visualTransitionImpulse;
    [SerializeField, Min(0.02f)] private float runtimeStatePollInterval = 0.1f;
    private bool hasTrackedVisualState;
    private bool trackedOpenState;
    private bool visualRefreshDirty = true;
    private bool physicalBlockerDirty = true;
    private float nextStatePollTime;
    private float nextPlayerResolveTime;
    private float nextPlayerPulsePollTime;
    private bool playerNearbyForPulse;
    private bool hasCachedDoorwayCenter;
    private Vector2 cachedDoorwayCenter;
    private Vector3Int[] registeredDoorCells = Array.Empty<Vector3Int>();
    private INoiseEventBus noiseEventBus;

    public bool IsOpen => isOpen;
    public override Vector2 InteractionPoint => GetCachedDoorwayCenter();
    protected override float MaxInteractionDistance => interactionDistance;

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        return InteractionPoint;
    }

    public override bool AllowsLineOfSightBlocker(Collider2D blocker, Vector2 hitPoint, WasdPlayerController playerController)
    {
        if (blocker == null)
        {
            return false;
        }

        Transform blockerTransform = blocker.transform;

        if (blocker == physicalBlockerCollider || blocker == lightBlockerCollider)
        {
            return true;
        }

        if (blockerTransform == transform || blockerTransform.IsChildOf(transform))
        {
            return true;
        }

        if (mapService == null || doorCells == null || doorCells.Length == 0)
        {
            return false;
        }

        Vector3Int hitCell = mapService.WorldToCell(hitPoint);

        for (int index = 0; index < doorCells.Length; index++)
        {
            if (doorCells[index] == hitCell)
            {
                return true;
            }
        }

        Vector2 blockerPoint = blocker.ClosestPoint(InteractionPoint);
        Vector3Int blockerCell = mapService.WorldToCell(blockerPoint);

        for (int index = 0; index < doorCells.Length; index++)
        {
            if (doorCells[index] == blockerCell)
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        SetPlayerController(RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>(
            gameObject.scene,
            this,
            nameof(DoorController),
            nameof(playerController)));
        EnsureVisuals();
        visualOpenAmount = isOpen ? 1f : 0f;
        RefreshPhysicalBlocker();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!ActiveDoors.Contains(this))
        {
            ActiveDoors.Add(this);
        }

        RegisterDoorCells();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnregisterDoorCells();
        ActiveDoors.Remove(this);
        ClearDoorPrefabVisuals();
    }

    public static bool TryGetAtCell(Vector3Int cellPosition, out DoorController controller)
    {
        if (ActiveDoorByCell.TryGetValue(cellPosition, out controller)
            && controller != null
            && controller.isActiveAndEnabled)
        {
            return true;
        }

        ActiveDoorByCell.Remove(cellPosition);
        controller = null;
        return false;
    }

    private void RegisterDoorCells()
    {
        if (!isActiveAndEnabled || doorCells == null || doorCells.Length == 0)
        {
            registeredDoorCells = Array.Empty<Vector3Int>();
            return;
        }

        registeredDoorCells = new Vector3Int[doorCells.Length];
        for (int index = 0; index < doorCells.Length; index++)
        {
            Vector3Int cell = doorCells[index];
            registeredDoorCells[index] = cell;
            ActiveDoorByCell[cell] = this;
        }
    }

    private void UnregisterDoorCells()
    {
        if (registeredDoorCells == null || registeredDoorCells.Length == 0)
        {
            registeredDoorCells = Array.Empty<Vector3Int>();
            return;
        }

        for (int index = 0; index < registeredDoorCells.Length; index++)
        {
            Vector3Int cell = registeredDoorCells[index];
            if (ActiveDoorByCell.TryGetValue(cell, out DoorController registeredDoor)
                && registeredDoor == this)
            {
                ActiveDoorByCell.Remove(cell);
            }
        }

        registeredDoorCells = Array.Empty<Vector3Int>();
    }

    public void Configure(ObjectiveManager manager, GridMapService configuredMapService, string requiredKeyId, params Vector3Int[] configuredDoorCells)
    {
        objectiveManager = manager;
        mapService = configuredMapService;
        keyId = requiredKeyId;
        UnregisterDoorCells();
        doorCells = configuredDoorCells != null && configuredDoorCells.Length > 0
            ? configuredDoorCells
            : new[] { new Vector3Int(2, 0, 0) };
        hasCachedDoorwayCenter = false;
        RegisterDoorCells();
        transform.position = RefreshCachedDoorwayCenter();
        EnsureVisuals();
        RefreshVisualLayout();
        SyncState();
        visualOpenAmount = isOpen ? 1f : 0f;
        RefreshVisualState();
        RefreshPhysicalBlocker();
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
    }

    public void BindPlayerController(WasdPlayerController boundPlayerController)
    {
        SetPlayerController(boundPlayerController);
    }

    public void SetBuiltInVisualsEnabled(bool enabled)
    {
        builtInVisualsEnabled = enabled;
        visualRefreshDirty = true;
        RefreshVisualVisibility();
    }

    public void SetDoorPrefabs(GameObject closedPrefab, GameObject openPrefab)
    {
        closedDoorPrefab = closedPrefab;
        openDoorPrefab = openPrefab;
        visualRefreshDirty = true;
        RefreshVisualVisibility();
        RefreshDoorPrefabVisuals();
    }

    public bool IsSideDoorVisual()
    {
        return authoredDoorVisualVariant == AuthoredDoorVisualVariant.SideDoor42;
    }

    public void BindAuthoredVisualRoots(params Transform[] visualRoots)
    {
        if (visualRoots == null || visualRoots.Length == 0)
        {
            authoredVisualRoots = Array.Empty<Transform>();
            authoredVisualClosedLocalPositions = Array.Empty<Vector3>();
            authoredVisualClosedLocalScales = Array.Empty<Vector3>();
            authoredDoorVisualVariant = AuthoredDoorVisualVariant.None;
            authoredVisualRenderersByRoot = Array.Empty<SpriteRenderer[]>();
            authoredVisualBaseColorsByRoot = Array.Empty<Color[]>();
            authoredVisualOriginalSpritesByRoot = Array.Empty<Sprite[]>();
            RefreshVisualState();
            RefreshPhysicalBlocker();
            return;
        }

        List<Transform> uniqueRoots = new();

        for (int index = 0; index < visualRoots.Length; index++)
        {
            Transform candidate = visualRoots[index];

            if (candidate != null && !uniqueRoots.Contains(candidate))
            {
                uniqueRoots.Add(candidate);
            }
        }

        authoredVisualRoots = uniqueRoots.ToArray();
        authoredVisualClosedLocalPositions = new Vector3[authoredVisualRoots.Length];
        authoredVisualClosedLocalScales = new Vector3[authoredVisualRoots.Length];
        authoredVisualRenderersByRoot = new SpriteRenderer[authoredVisualRoots.Length][];
        authoredVisualBaseColorsByRoot = new Color[authoredVisualRoots.Length][];
        authoredVisualOriginalSpritesByRoot = new Sprite[authoredVisualRoots.Length][];

        for (int index = 0; index < authoredVisualRoots.Length; index++)
        {
            Transform authoredRoot = authoredVisualRoots[index];
            authoredVisualClosedLocalPositions[index] = authoredRoot.localPosition;
            authoredVisualClosedLocalScales[index] = authoredRoot.localScale;

            SpriteRenderer[] renderers = authoredRoot.GetComponentsInChildren<SpriteRenderer>(true);
            authoredVisualRenderersByRoot[index] = renderers;
            authoredVisualBaseColorsByRoot[index] = new Color[renderers.Length];
            authoredVisualOriginalSpritesByRoot[index] = new Sprite[renderers.Length];

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                authoredVisualBaseColorsByRoot[index][rendererIndex] = renderers[rendererIndex] != null
                    ? renderers[rendererIndex].color
                    : Color.white;
                authoredVisualOriginalSpritesByRoot[index][rendererIndex] = renderers[rendererIndex] != null
                    ? renderers[rendererIndex].sprite
                    : null;
            }
        }

        authoredDoorVisualVariant = ResolveAuthoredDoorVisualVariant();
        ApplyAuthoredVisualVariantClosedSprites();

        RefreshVisualState();
        RefreshPhysicalBlocker();
    }

    private AuthoredDoorVisualVariant ResolveAuthoredDoorVisualVariant()
    {
        if (authoredVisualRoots == null || authoredVisualRoots.Length == 0)
        {
            return AuthoredDoorVisualVariant.None;
        }

        MainEscapeDoorVisualVariantKind resolvedVariant =
            MainEscapeDoorVisualVariantResolver.ResolveForVisualRoots(authoredVisualRoots);

        if (resolvedVariant == MainEscapeDoorVisualVariantKind.None)
        {
            return AuthoredDoorVisualVariant.FrontDoor;
        }

        return ConvertAuthoredDoorVisualVariant(resolvedVariant);
    }

    private void UpdateDoorVisualVariant()
    {
        AuthoredDoorVisualVariant resolvedVariant = ResolveAuthoredDoorVisualVariant();

        if (resolvedVariant != authoredDoorVisualVariant)
        {
            authoredDoorVisualVariant = resolvedVariant;
        }

        EnsurePrefabVisualsForVariant(resolvedVariant);
    }

    private void EnsurePrefabVisualsForVariant(AuthoredDoorVisualVariant variant)
    {
        if (variant == AuthoredDoorVisualVariant.None)
        {
            return;
        }

        if (variant == AuthoredDoorVisualVariant.SideDoor42)
        {
            sharedSideDoorClosedPrefab ??= Resources.Load<GameObject>(SideDoorClosedPrefabResourcePath);
            sharedSideDoorOpenPrefab ??= Resources.Load<GameObject>(SideDoorOpenPrefabResourcePath);

            if (sharedSideDoorClosedPrefab != null && sharedSideDoorOpenPrefab != null)
            {
                if (closedDoorPrefab != sharedSideDoorClosedPrefab || openDoorPrefab != sharedSideDoorOpenPrefab)
                {
                    SetDoorPrefabs(sharedSideDoorClosedPrefab, sharedSideDoorOpenPrefab);
                }
            }

            return;
        }

        sharedFrontDoorClosedPrefab ??= Resources.Load<GameObject>(FrontDoorClosedPrefabResourcePath);
        sharedFrontDoorOpenPrefab ??= Resources.Load<GameObject>(FrontDoorOpenPrefabResourcePath);

        if (sharedFrontDoorClosedPrefab != null && sharedFrontDoorOpenPrefab != null)
        {
            if (closedDoorPrefab != sharedFrontDoorClosedPrefab || openDoorPrefab != sharedFrontDoorOpenPrefab)
            {
                SetDoorPrefabs(sharedFrontDoorClosedPrefab, sharedFrontDoorOpenPrefab);
            }
        }
    }

    private void ApplyAuthoredVisualVariantClosedSprites()
    {
        if (authoredVisualRenderersByRoot == null || authoredVisualOriginalSpritesByRoot == null)
        {
            return;
        }

        Sprite replacementSprite = null;
        bool useReplacementSprite = authoredDoorVisualVariant == AuthoredDoorVisualVariant.SideDoor42
            && TryGetCustomSideDoorClosedSprite(out replacementSprite);

        for (int rootIndex = 0; rootIndex < authoredVisualRenderersByRoot.Length; rootIndex++)
        {
            SpriteRenderer[] renderers = authoredVisualRenderersByRoot[rootIndex];
            Sprite[] originalSprites = rootIndex < authoredVisualOriginalSpritesByRoot.Length
                ? authoredVisualOriginalSpritesByRoot[rootIndex]
                : Array.Empty<Sprite>();

            if (renderers == null)
            {
                continue;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SpriteRenderer renderer = renderers[rendererIndex];

                if (renderer == null)
                {
                    continue;
                }

                Sprite originalSprite = rendererIndex < originalSprites.Length ? originalSprites[rendererIndex] : renderer.sprite;
                renderer.sprite = useReplacementSprite && replacementSprite != null
                    ? replacementSprite
                    : originalSprite;
            }
        }
    }

    private bool IsVisualTransitionActive()
    {
        float targetOpenAmount = isOpen ? 1f : 0f;
        return Mathf.Abs(visualOpenAmount - targetOpenAmount) > 0.002f
            || visualTransitionImpulse > 0.002f;
    }

    private bool IsPlayerNearEnoughForDoorPulse()
    {
        ResolvePlayerControllerIfNeeded();
        if (Time.unscaledTime < nextPlayerPulsePollTime)
        {
            return playerNearbyForPulse;
        }

        nextPlayerPulsePollTime = Time.unscaledTime + 0.06f;

        return playerController != null
            && RefreshPlayerPulseProximity();
    }

    private bool RefreshPlayerPulseProximity()
    {
        float pulseDistance = interactionDistance + 0.6f;
        playerNearbyForPulse = ((Vector2)playerController.transform.position - InteractionPoint).sqrMagnitude <= pulseDistance * pulseDistance;
        return playerNearbyForPulse;
    }

    private void ResolvePlayerControllerIfNeeded()
    {
        if (playerController != null || Time.unscaledTime < nextPlayerResolveTime)
        {
            return;
        }

        nextPlayerResolveTime = Time.unscaledTime + 0.5f;
        SetPlayerController(RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>(
            gameObject.scene,
            this,
            nameof(DoorController),
            nameof(playerController)));
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            SyncState();
            RefreshVisualState();
            RefreshPhysicalBlocker();
            return;
        }

        if (Time.unscaledTime >= nextStatePollTime)
        {
            nextStatePollTime = Time.unscaledTime + Mathf.Max(0.02f, runtimeStatePollInterval);
            SyncState();
        }

        bool shouldRefreshVisuals = visualRefreshDirty
            || IsVisualTransitionActive()
            || IsPlayerNearEnoughForDoorPulse();

        if (shouldRefreshVisuals)
        {
            RefreshVisualState(visualRefreshDirty);
        }

        if (physicalBlockerDirty)
        {
            RefreshPhysicalBlocker();
        }
    }

    public override bool CanInteract(WasdPlayerController playerController)
    {
        return CanInteractAtDistance(playerController, GetInteractionDistance(playerController));
    }

    public override bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        if (playerController == null || !enabled || !gameObject.activeInHierarchy || mapService == null)
        {
            return false;
        }

        if (!isOpen && !IsUnlockedForPlayer())
        {
            return false;
        }

        return interactionDistance <= this.interactionDistance;
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        if (!isOpen && physicalBlockerCollider != null)
        {
            Vector2 playerPosition = playerController.transform.position;
            Vector2 closestPoint = physicalBlockerCollider.ClosestPoint(playerPosition);
            return Vector2.Distance(playerPosition, closestPoint);
        }

        return base.GetInteractionDistance(playerController);
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        if (!isOpen && !IsUnlockedForPlayer())
        {
            return "Door Locked";
        }

        return isOpen ? "E Close Door" : "E Open Door";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        if (isOpen)
        {
            TryClose();
            return;
        }

        TryOpenForPlayer();
    }

    public bool SetOpenState(bool open)
    {
        bool changed = open ? OpenAllDoorCells() : CloseAllDoorCells();
        SyncState();
        RefreshVisualState();
        RefreshPhysicalBlocker();
        return changed;
    }

    public bool TryOpenForEnemy(EnemyStateMachine opener)
    {
        if (mapService == null)
        {
            return false;
        }

        bool opened = OpenAllDoorCells();
        SyncState();

        if (opened)
        {
            int emitterId = opener != null ? opener.gameObject.GetInstanceID() : 0;
            ResolveNoiseEventBus()?.TryEmitNoise(
                InteractionPoint,
                enemyDoorNoiseRadius,
                NoiseSourceType.Door,
                emitterId,
                NoiseEmitterAffiliation.Enemy);
            PrototypeAudioManager.TryPlayDoorOpen();
        }

        RefreshVisualState();
        RefreshPhysicalBlocker();
        return opened;
    }

    private bool TryOpenForPlayer()
    {
        if (mapService == null || !IsUnlockedForPlayer())
        {
            return false;
        }

        bool opened = OpenAllDoorCells();
        SyncState();

        if (opened)
        {
            PrototypeAudioManager.TryPlayDoorOpen();
        }

        RefreshVisualState();
        RefreshPhysicalBlocker();
        return opened;
    }

    private bool TryClose()
    {
        if (mapService == null || IsActorBlockingDoor())
        {
            return false;
        }

        bool closed = CloseAllDoorCells();
        SyncState();

        if (closed)
        {
            PrototypeAudioManager.TryPlayDoorClose();
        }

        RefreshVisualState();
        RefreshPhysicalBlocker();
        return closed;
    }

    private bool IsUnlockedForPlayer()
    {
        return string.IsNullOrWhiteSpace(keyId)
            || objectiveManager == null
            || objectiveManager.HasKey(keyId);
    }

    private bool IsActorBlockingDoor()
    {
        foreach (EnemyStateMachine enemy in RSceneReferenceLookup.FindComponentsInScene<EnemyStateMachine>(gameObject.scene))
        {
            if (enemy != null && IsPointInsideDoorway(enemy.transform.position))
            {
                return true;
            }
        }

        WasdPlayerController player = ResolvePlayerController();
        return player != null && IsPointInsideDoorway(player.transform.position);
    }

    private WasdPlayerController ResolvePlayerController()
    {
        if (playerController != null && playerController.gameObject.scene == gameObject.scene)
        {
            return playerController;
        }

        SetPlayerController(RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>(
            gameObject.scene,
            this,
            nameof(DoorController),
            nameof(playerController)));
        return playerController;
    }

    private void SetPlayerController(WasdPlayerController boundPlayerController)
    {
        playerController = boundPlayerController != null
            && boundPlayerController.gameObject.scene == gameObject.scene
            ? boundPlayerController
            : null;
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private void SyncState()
    {
        bool resolvedOpenState = true;
        bool previousOpenState = isOpen;

        if (mapService == null || doorCells == null || doorCells.Length == 0)
        {
            resolvedOpenState = false;
        }
        else
        {
            for (int index = 0; index < doorCells.Length; index++)
            {
                if (!mapService.IsDoorOpen(doorCells[index]))
                {
                    resolvedOpenState = false;
                    break;
                }
            }
        }

        isOpen = resolvedOpenState;

        if (!hasTrackedVisualState)
        {
            trackedOpenState = isOpen;
            hasTrackedVisualState = true;
        }
        else if (trackedOpenState != isOpen)
        {
            trackedOpenState = isOpen;
            visualTransitionImpulse = 1f;
        }

        if (previousOpenState != isOpen)
        {
            visualRefreshDirty = true;
            physicalBlockerDirty = true;
        }

        Vector2 doorwayCenter = RefreshCachedDoorwayCenter();
        if (((Vector2)transform.position - doorwayCenter).sqrMagnitude > 0.0001f)
        {
            transform.position = doorwayCenter;
            visualRefreshDirty = true;
            physicalBlockerDirty = true;
        }
    }

    private bool OpenAllDoorCells()
    {
        bool openedAny = false;

        if (mapService == null || doorCells == null)
        {
            return false;
        }

        for (int index = 0; index < doorCells.Length; index++)
        {
            if (!mapService.IsDoorClosed(doorCells[index]))
            {
                continue;
            }

            openedAny |= mapService.OpenDoor(doorCells[index]);
        }

        return openedAny;
    }

    private bool CloseAllDoorCells()
    {
        bool closedAny = false;

        if (mapService == null || doorCells == null)
        {
            return false;
        }

        for (int index = 0; index < doorCells.Length; index++)
        {
            if (!mapService.IsDoorOpen(doorCells[index]))
            {
                continue;
            }

            closedAny |= mapService.CloseDoor(doorCells[index]);
        }

        return closedAny;
    }

    private Vector2 GetCachedDoorwayCenter()
    {
        if (!hasCachedDoorwayCenter)
        {
            return RefreshCachedDoorwayCenter();
        }

        return cachedDoorwayCenter;
    }

    private Vector2 RefreshCachedDoorwayCenter()
    {
        cachedDoorwayCenter = GetDoorwayCenter();
        hasCachedDoorwayCenter = true;
        return cachedDoorwayCenter;
    }

    private Vector2 GetDoorwayCenter()
    {
        if (mapService == null || doorCells == null || doorCells.Length == 0)
        {
            return transform.position;
        }

        Vector2 sum = Vector2.zero;

        for (int index = 0; index < doorCells.Length; index++)
        {
            sum += (Vector2)mapService.CellToWorldCenter(doorCells[index]);
        }

        return sum / doorCells.Length;
    }

    private bool IsPointInsideDoorway(Vector2 worldPoint)
    {
        if (mapService == null || doorCells == null)
        {
            return false;
        }

        for (int index = 0; index < doorCells.Length; index++)
        {
            if (Vector2.Distance(worldPoint, mapService.CellToWorldCenter(doorCells[index])) <= 0.4f)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureVisuals()
    {
        EnsureSharedMarkerSprite();
        EnsureSharedDoorIndicatorSprite();

        if (panelRenderer == null)
        {
            Transform existingPanel = transform.Find("DoorMarkerPanel");
            GameObject panelObject = existingPanel != null ? existingPanel.gameObject : new GameObject("DoorMarkerPanel");
            panelObject.transform.SetParent(transform, false);
            panelRenderer = panelObject.GetComponent<SpriteRenderer>();

            if (panelRenderer == null)
            {
                panelRenderer = panelObject.AddComponent<SpriteRenderer>();
            }
        }

        if (frameRenderer == null)
        {
            Transform existingFrame = transform.Find("DoorMarkerFrame");
            GameObject frameObject = existingFrame != null ? existingFrame.gameObject : new GameObject("DoorMarkerFrame");
            frameObject.transform.SetParent(transform, false);
            frameRenderer = frameObject.GetComponent<SpriteRenderer>();

            if (frameRenderer == null)
            {
                frameRenderer = frameObject.AddComponent<SpriteRenderer>();
            }
        }

        if (openingRenderer == null)
        {
            Transform existingOpening = transform.Find("DoorMarkerOpening");
            GameObject openingObject = existingOpening != null ? existingOpening.gameObject : new GameObject("DoorMarkerOpening");
            openingObject.transform.SetParent(transform, false);
            openingRenderer = openingObject.GetComponent<SpriteRenderer>();

            if (openingRenderer == null)
            {
                openingRenderer = openingObject.AddComponent<SpriteRenderer>();
            }
        }

        if (leafPrimaryRenderer == null)
        {
            Transform existingLeafPrimary = transform.Find("DoorLeafPrimary");
            GameObject leafPrimaryObject = existingLeafPrimary != null ? existingLeafPrimary.gameObject : new GameObject("DoorLeafPrimary");
            leafPrimaryObject.transform.SetParent(transform, false);
            leafPrimaryRenderer = leafPrimaryObject.GetComponent<SpriteRenderer>();

            if (leafPrimaryRenderer == null)
            {
                leafPrimaryRenderer = leafPrimaryObject.AddComponent<SpriteRenderer>();
            }
        }

        if (leafSecondaryRenderer == null)
        {
            Transform existingLeafSecondary = transform.Find("DoorLeafSecondary");
            GameObject leafSecondaryObject = existingLeafSecondary != null ? existingLeafSecondary.gameObject : new GameObject("DoorLeafSecondary");
            leafSecondaryObject.transform.SetParent(transform, false);
            leafSecondaryRenderer = leafSecondaryObject.GetComponent<SpriteRenderer>();

            if (leafSecondaryRenderer == null)
            {
                leafSecondaryRenderer = leafSecondaryObject.AddComponent<SpriteRenderer>();
            }
        }

        if (beaconRenderer == null)
        {
            Transform existingBeacon = transform.Find("DoorMarkerBeacon");
            GameObject beaconObject = existingBeacon != null ? existingBeacon.gameObject : new GameObject("DoorMarkerBeacon");
            beaconObject.transform.SetParent(transform, false);
            beaconRenderer = beaconObject.GetComponent<SpriteRenderer>();

            if (beaconRenderer == null)
            {
                beaconRenderer = beaconObject.AddComponent<SpriteRenderer>();
            }
        }

        if (lowerBeaconRenderer == null)
        {
            Transform existingLowerBeacon = transform.Find("DoorMarkerBeaconLower");
            GameObject lowerBeaconObject = existingLowerBeacon != null ? existingLowerBeacon.gameObject : new GameObject("DoorMarkerBeaconLower");
            lowerBeaconObject.transform.SetParent(transform, false);
            lowerBeaconRenderer = lowerBeaconObject.GetComponent<SpriteRenderer>();

            if (lowerBeaconRenderer == null)
            {
                lowerBeaconRenderer = lowerBeaconObject.AddComponent<SpriteRenderer>();
            }
        }

        if (headerSignPlateRenderer == null)
        {
            Transform existingHeaderSignPlate = transform.Find("DoorHeaderSignPlate");
            GameObject headerSignPlateObject = existingHeaderSignPlate != null
                ? existingHeaderSignPlate.gameObject
                : new GameObject("DoorHeaderSignPlate");
            headerSignPlateObject.transform.SetParent(transform, false);
            headerSignPlateRenderer = headerSignPlateObject.GetComponent<SpriteRenderer>();

            if (headerSignPlateRenderer == null)
            {
                headerSignPlateRenderer = headerSignPlateObject.AddComponent<SpriteRenderer>();
            }
        }

        if (headerSignIconRenderer == null)
        {
            Transform existingHeaderSignIcon = transform.Find("DoorHeaderSignIcon");
            GameObject headerSignIconObject = existingHeaderSignIcon != null
                ? existingHeaderSignIcon.gameObject
                : new GameObject("DoorHeaderSignIcon");
            headerSignIconObject.transform.SetParent(transform, false);
            headerSignIconRenderer = headerSignIconObject.GetComponent<SpriteRenderer>();

            if (headerSignIconRenderer == null)
            {
                headerSignIconRenderer = headerSignIconObject.AddComponent<SpriteRenderer>();
            }
        }

        if (authoredOpenDoorRenderer == null)
        {
            Transform existingAuthoredOpenDoor = transform.Find("AuthoredOpenDoorVisual");
            GameObject authoredOpenDoorObject = existingAuthoredOpenDoor != null
                ? existingAuthoredOpenDoor.gameObject
                : new GameObject("AuthoredOpenDoorVisual");
            authoredOpenDoorObject.transform.SetParent(transform, false);
            authoredOpenDoorRenderer = authoredOpenDoorObject.GetComponent<SpriteRenderer>();

            if (authoredOpenDoorRenderer == null)
            {
                authoredOpenDoorRenderer = authoredOpenDoorObject.AddComponent<SpriteRenderer>();
            }
        }

        if (physicalBlockerCollider == null)
        {
            Transform existingPhysicalBlocker = transform.Find("DoorPhysicalBlocker");
            GameObject blockerObject = existingPhysicalBlocker != null ? existingPhysicalBlocker.gameObject : new GameObject("DoorPhysicalBlocker");
            blockerObject.transform.SetParent(transform, false);
            physicalBlockerCollider = blockerObject.GetComponent<BoxCollider2D>();

            if (physicalBlockerCollider == null)
            {
                physicalBlockerCollider = blockerObject.AddComponent<BoxCollider2D>();
            }

            physicalBlockerCollider.isTrigger = false;
            blockerObject.layer = gameObject.layer;
        }

        if (lightBlockerCollider == null)
        {
            Transform existingLightBlocker = transform.Find("DoorLightBlocker");
            GameObject lightBlockerObject = existingLightBlocker != null ? existingLightBlocker.gameObject : new GameObject("DoorLightBlocker");
            lightBlockerObject.transform.SetParent(transform, false);
            lightBlockerCollider = lightBlockerObject.GetComponent<BoxCollider2D>();

            if (lightBlockerCollider == null)
            {
                lightBlockerCollider = lightBlockerObject.AddComponent<BoxCollider2D>();
            }
        }

        if (lightBlockerCollider != null)
        {
            lightBlockerCollider.isTrigger = true;
            lightBlockerCollider.gameObject.layer = GameLayers.DoorIndex;
            RuntimeShadowCaster2DConfigurator.TryConfigureFromCollider(lightBlockerCollider.gameObject, lightBlockerCollider, out lightBlockerShadowCaster);
            lightBlockerShadowCaster ??= lightBlockerCollider.GetComponent<ShadowCaster2D>();
        }

        panelRenderer.sprite = sharedMarkerSprite;
        panelRenderer.sortingOrder = 27;
        frameRenderer.sprite = sharedMarkerSprite;
        frameRenderer.sortingOrder = 24;
        openingRenderer.sprite = sharedMarkerSprite;
        openingRenderer.sortingOrder = 25;
        leafPrimaryRenderer.sprite = sharedMarkerSprite;
        leafPrimaryRenderer.sortingOrder = 26;
        leafSecondaryRenderer.sprite = sharedMarkerSprite;
        leafSecondaryRenderer.sortingOrder = 26;
        beaconRenderer.sprite = sharedMarkerSprite;
        beaconRenderer.sortingOrder = 28;
        lowerBeaconRenderer.sprite = sharedMarkerSprite;
        lowerBeaconRenderer.sortingOrder = 28;
        headerSignPlateRenderer.sprite = sharedMarkerSprite;
        headerSignPlateRenderer.sortingOrder = 29;
        headerSignIconRenderer.sprite = sharedDoorIndicatorSprite;
        headerSignIconRenderer.sortingOrder = 30;
        authoredOpenDoorRenderer.sprite = TryGetOpenDoorPresentationSprite(out Sprite openDoorSprite) ? openDoorSprite : null;
        authoredOpenDoorRenderer.sortingOrder = 23;
        authoredOpenDoorRenderer.color = new Color(1f, 1f, 1f, 0f);
        RefreshVisualVisibility();
    }

    private void RefreshVisualLayout()
    {
        bool hasBuiltInVisuals = panelRenderer != null
            && frameRenderer != null
            && openingRenderer != null
            && leafPrimaryRenderer != null
            && leafSecondaryRenderer != null
            && beaconRenderer != null
            && lowerBeaconRenderer != null;

        Vector2 panelSize = GetDoorPanelSize();
        Vector2 frameSize = new(panelSize.x + 0.2f, panelSize.y + 0.2f);
        panelRenderer.transform.localPosition = Vector3.zero;
        panelRenderer.transform.localScale = new Vector3(panelSize.x, panelSize.y, 1f);
        frameRenderer.transform.localPosition = Vector3.zero;
        frameRenderer.transform.localScale = new Vector3(frameSize.x, frameSize.y, 1f);

        bool horizontalDoor = panelSize.x >= panelSize.y;
        Vector2 openingSize = horizontalDoor
            ? new Vector2(Mathf.Max(0.7f, panelSize.x * 0.72f), Mathf.Max(0.92f, panelSize.y * 3.1f))
            : new Vector2(Mathf.Max(0.92f, panelSize.x * 3.1f), Mathf.Max(0.7f, panelSize.y * 0.72f));
        Vector2 leafSize = horizontalDoor
            ? new Vector2(Mathf.Clamp(panelSize.x * 0.2f, 0.22f, 0.42f), Mathf.Max(0.84f, panelSize.y * 2.9f))
            : new Vector2(Mathf.Max(0.84f, panelSize.x * 2.9f), Mathf.Clamp(panelSize.y * 0.2f, 0.22f, 0.42f));
        float leafOffset = horizontalDoor
            ? Mathf.Max(0.22f, openingSize.x * 0.5f + leafSize.x * 0.5f + 0.05f)
            : Mathf.Max(0.22f, openingSize.y * 0.5f + leafSize.y * 0.5f + 0.05f);
        openingRenderer.transform.localPosition = Vector3.zero;
        openingRenderer.transform.localScale = new Vector3(openingSize.x, openingSize.y, 1f);
        leafPrimaryRenderer.transform.localScale = new Vector3(leafSize.x, leafSize.y, 1f);
        leafSecondaryRenderer.transform.localScale = new Vector3(leafSize.x, leafSize.y, 1f);
        leafPrimaryRenderer.transform.localPosition = horizontalDoor
            ? new Vector3(-leafOffset, 0f, 0f)
            : new Vector3(0f, leafOffset, 0f);
        leafSecondaryRenderer.transform.localPosition = horizontalDoor
            ? new Vector3(leafOffset, 0f, 0f)
            : new Vector3(0f, -leafOffset, 0f);

        float beaconOffsetY = horizontalDoor
            ? Mathf.Max(0.96f, panelSize.y * 0.85f + 0.48f)
            : Mathf.Max(1.05f, panelSize.y * 0.58f + 0.5f);
        Vector3 beaconScale = horizontalDoor
            ? new Vector3(Mathf.Clamp(panelSize.x * 0.5f, 0.42f, 1.35f), 0.22f, 1f)
            : new Vector3(0.28f, 0.28f, 1f);
        beaconRenderer.transform.localPosition = new Vector3(0f, beaconOffsetY, 0f);
        beaconRenderer.transform.localScale = beaconScale;
        lowerBeaconRenderer.transform.localPosition = new Vector3(0f, -beaconOffsetY, 0f);
        lowerBeaconRenderer.transform.localScale = beaconScale;

        if (headerSignPlateRenderer != null && headerSignIconRenderer != null)
        {
            float headerSignOffsetY = ResolveHeaderSignOffsetY(panelSize, horizontalDoor);
            Vector3 plateScale = horizontalDoor
                ? new Vector3(Mathf.Clamp(panelSize.x * 0.42f, 0.48f, 0.86f), 0.2f, 1f)
                : new Vector3(0.52f, 0.2f, 1f);
            Vector3 iconScale = horizontalDoor
                ? new Vector3(0.32f, 0.32f, 1f)
                : new Vector3(0.28f, 0.28f, 1f);
            Vector3 signPosition = new(0f, headerSignOffsetY, 0f);
            headerSignPlateRenderer.transform.localPosition = signPosition;
            headerSignPlateRenderer.transform.localScale = plateScale;
            headerSignIconRenderer.transform.localPosition = signPosition;
            headerSignIconRenderer.transform.localScale = iconScale;
        }

        if (physicalBlockerCollider != null)
        {
            ResolvePhysicalBlockerShape(out Vector2 blockerOffset, out Vector2 blockerSize);
            physicalBlockerCollider.offset = blockerOffset;
            physicalBlockerCollider.size = blockerSize;
        }

        if (lightBlockerCollider != null)
        {
            ResolveLightBlockerShape(out Vector2 blockerOffset, out Vector2 blockerSize);
            lightBlockerCollider.offset = blockerOffset;
            lightBlockerCollider.size = blockerSize;
        }
    }

    private void RefreshVisualState(bool refreshStaticLayout = true)
    {
        if (refreshStaticLayout)
        {
            EnsureVisuals();
            RefreshVisualLayout();
            UpdateDoorVisualVariant();
            RefreshVisualVisibility();
        }

        ResolvePlayerControllerIfNeeded();

        bool usesPrefabDoorVisuals = UsesDoorPrefabVisuals();
        if (refreshStaticLayout && !usesPrefabDoorVisuals)
        {
            ApplyAuthoredVisualVariantClosedSprites();
        }

        bool hasBuiltInVisuals = panelRenderer != null
            && frameRenderer != null
            && openingRenderer != null
            && leafPrimaryRenderer != null
            && leafSecondaryRenderer != null
            && beaconRenderer != null
            && lowerBeaconRenderer != null;

        float targetOpenAmount = isOpen ? 1f : 0f;
        visualOpenAmount = Application.isPlaying
            ? Mathf.MoveTowards(visualOpenAmount, targetOpenAmount, Time.deltaTime * 6f)
            : targetOpenAmount;
        visualTransitionImpulse = Application.isPlaying
            ? Mathf.MoveTowards(visualTransitionImpulse, 0f, Time.deltaTime * 2.75f)
            : 0f;

        bool unlocked = IsUnlockedForPlayer();
        bool playerNearby = playerNearbyForPulse;
        if (!Application.isPlaying || refreshStaticLayout)
        {
            playerNearby = playerController != null && RefreshPlayerPulseProximity();
        }
        float openPulse = 0.82f + Mathf.PingPong(Time.time * 1.35f, 0.18f);
        float transitionPulse = 1f + visualTransitionImpulse * 0.48f;
        bool useCustomAuthoredOpenDoorPresentation = !usesPrefabDoorVisuals && UseCustomAuthoredOpenDoorPresentation();
        bool overlayOnlyMode = !useCustomAuthoredOpenDoorPresentation && !builtInVisualsEnabled && overlayFeedbackEnabled;

        if (refreshStaticLayout && usesPrefabDoorVisuals)
        {
            RefreshDoorPrefabVisuals();
        }

        Color panelColor = isOpen
            ? openColor
            : (unlocked ? closedColor : lockedColor);

        Color leafColor = Color.Lerp(panelColor, new Color(0.82f, 0.98f, 0.96f, 0.95f), visualOpenAmount * 0.42f);
        Color doorFrameColor = Color.Lerp(frameColor, new Color(0.2f, 0.45f, 0.4f, 0.94f), visualOpenAmount * 0.58f);
        Color openingColor = new(openColor.r, openColor.g, openColor.b, Mathf.Lerp(0.02f, 0.42f, visualOpenAmount) * openPulse);

        if (playerNearby)
        {
            panelColor *= 1.16f;
            openingColor.a *= 1.12f;
        }

        ApplyDoorLeafMotion(visualOpenAmount, overlayOnlyMode);

        if (hasBuiltInVisuals && builtInVisualsEnabled)
        {
            panelRenderer.color = new Color(panelColor.r, panelColor.g, panelColor.b, Mathf.Lerp(panelColor.a, 0.04f, visualOpenAmount));
            frameRenderer.color = new Color(doorFrameColor.r, doorFrameColor.g, doorFrameColor.b, Mathf.Lerp(0.7f, 0.94f, visualOpenAmount));
        }

        float openingAlpha = overlayOnlyMode
            ? Mathf.Lerp(0.14f, 0.62f, visualOpenAmount) * transitionPulse
            : openingColor.a * transitionPulse;
        float leafAlpha = overlayOnlyMode
            ? Mathf.Lerp(0.42f, 0.98f, visualOpenAmount) * transitionPulse
            : Mathf.Lerp(0f, 0.92f, visualOpenAmount);
        Color overlayLeafColor = overlayOnlyMode
            ? Color.Lerp(unlocked ? closedColor : lockedColor, openColor, visualOpenAmount * 0.82f)
            : leafColor;

        if (openingRenderer != null)
        {
            openingRenderer.color = new Color(openingColor.r, openingColor.g, openingColor.b, Mathf.Clamp01(openingAlpha));
        }

        if (leafPrimaryRenderer != null)
        {
            leafPrimaryRenderer.color = new Color(overlayLeafColor.r, overlayLeafColor.g, overlayLeafColor.b, Mathf.Clamp01(leafAlpha));
        }

        if (leafSecondaryRenderer != null)
        {
            leafSecondaryRenderer.color = new Color(overlayLeafColor.r, overlayLeafColor.g, overlayLeafColor.b, Mathf.Clamp01(leafAlpha));
        }

        if (headerSignPlateRenderer != null)
        {
            headerSignPlateRenderer.color = headerSignPlateColor;
        }

        if (headerSignIconRenderer != null)
        {
            headerSignIconRenderer.color = headerSignIconColor;
        }

        RefreshAuthoredVisualRoots(visualOpenAmount, transitionPulse, unlocked, useCustomAuthoredOpenDoorPresentation);

        if (authoredVisualRoots == null || authoredVisualRoots.Length == 0)
        {
            RefreshCustomAuthoredOpenDoorPresentation(visualOpenAmount, transitionPulse, useCustomAuthoredOpenDoorPresentation);
        }

        visualRefreshDirty = false;
    }

    private void RefreshVisualVisibility()
    {
        if (UsesDoorPrefabVisuals())
        {
            SetBuiltInRenderersEnabled(false);
            SetAuthoredVisualRootsEnabled(false);

            if (authoredOpenDoorRenderer != null)
            {
                authoredOpenDoorRenderer.enabled = false;
            }

            return;
        }

        bool useCustomAuthoredOpenDoorPresentation = UseCustomAuthoredOpenDoorPresentation();

        if (panelRenderer != null)
        {
            panelRenderer.enabled = builtInVisualsEnabled;
        }

        if (frameRenderer != null)
        {
            frameRenderer.enabled = builtInVisualsEnabled;
        }

        if (openingRenderer != null)
        {
            openingRenderer.enabled = !useCustomAuthoredOpenDoorPresentation && (builtInVisualsEnabled || overlayFeedbackEnabled);
        }

        if (leafPrimaryRenderer != null)
        {
            leafPrimaryRenderer.enabled = !useCustomAuthoredOpenDoorPresentation && (builtInVisualsEnabled || overlayFeedbackEnabled);
        }

        if (leafSecondaryRenderer != null)
        {
            leafSecondaryRenderer.enabled = !useCustomAuthoredOpenDoorPresentation && (builtInVisualsEnabled || overlayFeedbackEnabled);
        }

        if (beaconRenderer != null)
        {
            beaconRenderer.enabled = false;
        }

        if (lowerBeaconRenderer != null)
        {
            lowerBeaconRenderer.enabled = false;
        }

        if (headerSignPlateRenderer != null)
        {
            headerSignPlateRenderer.enabled = true;
        }

        if (headerSignIconRenderer != null)
        {
            headerSignIconRenderer.enabled = true;
        }

        if (authoredOpenDoorRenderer != null)
        {
            authoredOpenDoorRenderer.enabled = useCustomAuthoredOpenDoorPresentation;
        }
    }

    private bool UsesDoorPrefabVisuals()
    {
        return closedDoorPrefab != null && openDoorPrefab != null;
    }

    private void RefreshDoorPrefabVisuals()
    {
        if (!UsesDoorPrefabVisuals())
        {
            ClearDoorPrefabVisuals();
            return;
        }

        if (!TryGetDoorPresentationSprites(out Sprite closedSprite, out Sprite openSprite))
        {
            ClearDoorPrefabVisuals();
            return;
        }

        MainEscapeDoorPresentationController presentationController = EnsureDoorPresentationController();
        if (presentationController == null)
        {
            return;
        }

        presentationController.Configure(
            IsSideDoorVisual() ? "SideDoorPresentation" : "FrontDoorPresentation",
            closedSprite,
            openSprite);
        presentationController.SetOpen(isOpen);
        presentationController.SetVisible(true);
        ApplyDoorPrefabRendererSettings();
        ApplyDoorPrefabTransform();
    }

    private static AuthoredDoorVisualVariant ConvertAuthoredDoorVisualVariant(MainEscapeDoorVisualVariantKind variant)
    {
        return variant switch
        {
            MainEscapeDoorVisualVariantKind.SideDoor42 => AuthoredDoorVisualVariant.SideDoor42,
            MainEscapeDoorVisualVariantKind.FrontDoor => AuthoredDoorVisualVariant.FrontDoor,
            _ => AuthoredDoorVisualVariant.None
        };
    }

    private void ClearDoorPrefabVisuals()
    {
        if (doorPresentationController != null)
        {
            doorPresentationController.SetVisible(false);
        }
    }

    private void ApplyDoorPrefabTransform()
    {
        if (!TryGetDoorPresentationRenderer(out SpriteRenderer presentationRenderer))
        {
            return;
        }

        Transform presentationTransform = presentationRenderer.transform;
        Transform anchor = GetPreferredDoorVisualAnchor();
        if (anchor != null)
        {
            Vector3 anchorPosition = anchor.position;
            if (isOpen && IsSideDoorVisual())
            {
                anchorPosition += new Vector3(-0.55f, 0f, 0f);
            }

            presentationTransform.position = anchorPosition;
            presentationTransform.rotation = anchor.rotation;
            presentationTransform.localScale = anchor.localScale;
            return;
        }

        Vector2 doorwayCenter = GetDoorwayCenter();
        Vector3 fallbackPosition = new Vector3(doorwayCenter.x, doorwayCenter.y, -40f);
        if (isOpen && IsSideDoorVisual())
        {
            fallbackPosition += new Vector3(-0.55f, 0f, 0f);
        }

        presentationTransform.position = fallbackPosition;
        presentationTransform.rotation = Quaternion.identity;
        presentationTransform.localScale = Vector3.one;
    }

    private void ApplyDoorPrefabRendererSettings()
    {
        if (!TryGetDoorPresentationRenderer(out SpriteRenderer renderer))
        {
            return;
        }

        int sortingLayerId = 0;
        int sortingOrder = 0;
        Material prefabMaterial = GetDoorPrefabMaterial();

        if (TryGetRepresentativeAuthoredRenderer(out SpriteRenderer representativeRenderer))
        {
            CopyRepresentativeRendererSettings(renderer, representativeRenderer, 0);
            sortingLayerId = representativeRenderer.sortingLayerID;
            sortingOrder = representativeRenderer.sortingOrder;

            if (renderer.sharedMaterial != null)
            {
                prefabMaterial = renderer.sharedMaterial;
            }
        }

        Color baseColor = renderer.color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = Mathf.Max(sortingOrder, DoorAboveFogSortingOrder);
        renderer.maskInteraction = SpriteMaskInteraction.None;
        renderer.color = baseColor;

        if (prefabMaterial != null)
        {
            renderer.sharedMaterial = prefabMaterial;
        }
    }

    private static Material GetDoorPrefabMaterial()
    {
        if (sharedDoorPrefabMaterial != null)
        {
            return sharedDoorPrefabMaterial;
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Shader fallbackShader = unlitShader;

        if (fallbackShader == null)
        {
            fallbackShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }

        if (fallbackShader == null)
        {
            return null;
        }

        sharedDoorPrefabMaterial = new Material(fallbackShader);
        return sharedDoorPrefabMaterial;
    }

    private MainEscapeDoorPresentationController EnsureDoorPresentationController()
    {
        if (doorPresentationController != null)
        {
            return doorPresentationController;
        }

        doorPresentationController = GetComponent<MainEscapeDoorPresentationController>();
        if (doorPresentationController == null)
        {
            doorPresentationController = gameObject.AddComponent<MainEscapeDoorPresentationController>();
        }

        return doorPresentationController;
    }

    private bool TryGetDoorPresentationRenderer(out SpriteRenderer renderer)
    {
        renderer = EnsureDoorPresentationController()?.Renderer;
        return renderer != null;
    }

    private bool TryGetDoorPresentationSprites(out Sprite closedSprite, out Sprite openSprite)
    {
        closedSprite = null;
        openSprite = null;
        return TryExtractPrimarySprite(closedDoorPrefab, out closedSprite)
            && TryExtractPrimarySprite(openDoorPrefab, out openSprite);
    }

    private static bool TryExtractPrimarySprite(GameObject prefab, out Sprite sprite)
    {
        sprite = null;

        if (prefab == null)
        {
            return false;
        }

        SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null || renderer.sprite == null)
        {
            return false;
        }

        sprite = renderer.sprite;
        return true;
    }

    private Transform GetPreferredDoorVisualAnchor()
    {
        if (authoredVisualRoots != null)
        {
            for (int index = 0; index < authoredVisualRoots.Length; index++)
            {
                Transform root = authoredVisualRoots[index];
                if (root != null)
                {
                    return root;
                }
            }
        }

        return authoredOpenDoorRenderer != null ? authoredOpenDoorRenderer.transform : null;
    }

    private void SetBuiltInRenderersEnabled(bool enabled)
    {
        if (panelRenderer != null)
        {
            panelRenderer.enabled = enabled;
        }

        if (frameRenderer != null)
        {
            frameRenderer.enabled = enabled;
        }

        if (openingRenderer != null)
        {
            openingRenderer.enabled = enabled;
        }

        if (leafPrimaryRenderer != null)
        {
            leafPrimaryRenderer.enabled = enabled;
        }

        if (leafSecondaryRenderer != null)
        {
            leafSecondaryRenderer.enabled = enabled;
        }

        if (beaconRenderer != null)
        {
            beaconRenderer.enabled = enabled;
        }

        if (lowerBeaconRenderer != null)
        {
            lowerBeaconRenderer.enabled = enabled;
        }

        if (headerSignPlateRenderer != null)
        {
            headerSignPlateRenderer.enabled = enabled;
        }

        if (headerSignIconRenderer != null)
        {
            headerSignIconRenderer.enabled = enabled;
        }
    }

    private void SetAuthoredVisualRootsEnabled(bool enabled)
    {
        if (authoredVisualRoots == null || authoredVisualRoots.Length == 0)
        {
            return;
        }

        if (authoredVisualRenderersByRoot == null || authoredVisualRenderersByRoot.Length != authoredVisualRoots.Length)
        {
            authoredVisualRenderersByRoot = new SpriteRenderer[authoredVisualRoots.Length][];
        }

        for (int rootIndex = 0; rootIndex < authoredVisualRoots.Length; rootIndex++)
        {
            Transform root = authoredVisualRoots[rootIndex];
            if (root == null)
            {
                continue;
            }

            SpriteRenderer[] renderers = authoredVisualRenderersByRoot[rootIndex];
            if (renderers == null || renderers.Length == 0)
            {
                renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                authoredVisualRenderersByRoot[rootIndex] = renderers;
            }

            if (renderers == null)
            {
                continue;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SpriteRenderer renderer = renderers[rendererIndex];
                if (renderer != null)
                {
                    renderer.enabled = enabled;
                }
            }
        }
    }

    private void ApplyDoorLeafMotion(float openAmount, bool overlayOnlyMode)
    {
        if (openingRenderer == null || leafPrimaryRenderer == null || leafSecondaryRenderer == null)
        {
            return;
        }

        Vector2 panelSize = GetDoorPanelSize();
        bool horizontalDoor = panelSize.x >= panelSize.y;
        Vector2 openingSize = horizontalDoor
            ? new Vector2(Mathf.Max(0.7f, panelSize.x * 0.72f), Mathf.Max(0.92f, panelSize.y * 3.1f))
            : new Vector2(Mathf.Max(0.92f, panelSize.x * 3.1f), Mathf.Max(0.7f, panelSize.y * 0.72f));
        Vector2 leafSize = horizontalDoor
            ? new Vector2(Mathf.Clamp(panelSize.x * 0.2f, 0.22f, 0.42f), Mathf.Max(0.84f, panelSize.y * 2.9f))
            : new Vector2(Mathf.Max(0.84f, panelSize.x * 2.9f), Mathf.Clamp(panelSize.y * 0.2f, 0.22f, 0.42f));

        float openLeafOffset = horizontalDoor
            ? Mathf.Max(0.22f, openingSize.x * 0.5f + leafSize.x * 0.5f + 0.05f)
            : Mathf.Max(0.22f, openingSize.y * 0.5f + leafSize.y * 0.5f + 0.05f);
        float closedLeafOffset = horizontalDoor
            ? Mathf.Max(0.04f, leafSize.x * 0.5f - 0.02f)
            : Mathf.Max(0.04f, leafSize.y * 0.5f - 0.02f);
        float currentLeafOffset = Mathf.Lerp(closedLeafOffset, openLeafOffset, openAmount);

        Vector2 closedOpeningSize = horizontalDoor
            ? new Vector2(Mathf.Max(0.08f, leafSize.x * 0.55f), openingSize.y)
            : new Vector2(openingSize.x, Mathf.Max(0.08f, leafSize.y * 0.55f));
        Vector2 currentOpeningSize = Vector2.Lerp(closedOpeningSize, openingSize, openAmount);

        if (overlayOnlyMode)
        {
            currentOpeningSize = Vector2.Lerp(currentOpeningSize, openingSize * 1.08f, 0.2f + openAmount * 0.35f);
        }

        openingRenderer.transform.localScale = new Vector3(currentOpeningSize.x, currentOpeningSize.y, 1f);
        leafPrimaryRenderer.transform.localPosition = horizontalDoor
            ? new Vector3(-currentLeafOffset, 0f, 0f)
            : new Vector3(0f, currentLeafOffset, 0f);
        leafSecondaryRenderer.transform.localPosition = horizontalDoor
            ? new Vector3(currentLeafOffset, 0f, 0f)
            : new Vector3(0f, -currentLeafOffset, 0f);
    }

    private void RefreshAuthoredVisualRoots(float openAmount, float transitionPulse, bool unlocked, bool useCustomAuthoredOpenDoorPresentation)
    {
        if (authoredVisualRoots == null
            || authoredVisualRoots.Length == 0
            || authoredVisualClosedLocalPositions == null
            || authoredVisualClosedLocalScales == null)
        {
            return;
        }

        Vector2 doorwayCenter = GetDoorwayCenter();
        Vector2 panelSize = GetDoorPanelSize();
        bool horizontalDoor = panelSize.x >= panelSize.y;
        float translationDistance = horizontalDoor
            ? Mathf.Clamp(panelSize.x * 0.22f, 0.08f, 0.22f)
            : Mathf.Clamp(panelSize.y * 0.18f, 0.08f, 0.24f);

        for (int index = 0; index < authoredVisualRoots.Length; index++)
        {
            Transform visualRoot = authoredVisualRoots[index];

            if (visualRoot == null)
            {
                continue;
            }

            Vector3 closedLocalPosition = index < authoredVisualClosedLocalPositions.Length
                ? authoredVisualClosedLocalPositions[index]
                : visualRoot.localPosition;
            Vector3 closedLocalScale = index < authoredVisualClosedLocalScales.Length
                ? authoredVisualClosedLocalScales[index]
                : visualRoot.localScale;

            Vector3 movementDirection = ResolveAuthoredVisualDirection(visualRoot, doorwayCenter, horizontalDoor);
            bool centralPanel = movementDirection == Vector3.zero;
            Vector3 targetLocalPosition = useCustomAuthoredOpenDoorPresentation || centralPanel
                ? closedLocalPosition
                : closedLocalPosition + movementDirection * (translationDistance * openAmount);
            Vector3 targetLocalScale = closedLocalScale;

            if (!useCustomAuthoredOpenDoorPresentation && centralPanel)
            {
                if (horizontalDoor)
                {
                    targetLocalScale = new Vector3(
                        Mathf.Lerp(closedLocalScale.x, closedLocalScale.x * 0.72f, openAmount),
                        closedLocalScale.y,
                        closedLocalScale.z);
                }
                else
                {
                    targetLocalScale = new Vector3(
                        closedLocalScale.x,
                        Mathf.Lerp(closedLocalScale.y, closedLocalScale.y * 0.72f, openAmount),
                        closedLocalScale.z);
                }
            }

            visualRoot.localPosition = Application.isPlaying
                ? Vector3.Lerp(visualRoot.localPosition, targetLocalPosition, Time.deltaTime * 10f)
                : targetLocalPosition;
            visualRoot.localScale = Application.isPlaying
                ? Vector3.Lerp(visualRoot.localScale, targetLocalScale, Time.deltaTime * 10f)
                : targetLocalScale;

            SpriteRenderer[] renderers = index < authoredVisualRenderersByRoot.Length
                ? authoredVisualRenderersByRoot[index]
                : Array.Empty<SpriteRenderer>();
            Color[] baseColors = index < authoredVisualBaseColorsByRoot.Length
                ? authoredVisualBaseColorsByRoot[index]
                : Array.Empty<Color>();

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SpriteRenderer renderer = renderers[rendererIndex];

                if (renderer == null)
                {
                    continue;
                }

                Color baseColor = rendererIndex < baseColors.Length ? baseColors[rendererIndex] : renderer.color;
                Color tintMultiplier = unlocked
                    ? Color.Lerp(Color.white, new Color(0.92f, 1.08f, 1.02f, 1f), openAmount * 0.2f)
                    : Color.Lerp(Color.white, new Color(1.08f, 0.92f, 0.92f, 1f), 0.12f);
                float authoredAlphaMultiplier = useCustomAuthoredOpenDoorPresentation
                    ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.88f, openAmount))
                    : Mathf.Lerp(1f, centralPanel ? 0.82f : 0.92f, openAmount);
                Color emphasizedColor = new(
                    Mathf.Clamp01(baseColor.r * tintMultiplier.r),
                    Mathf.Clamp01(baseColor.g * tintMultiplier.g),
                    Mathf.Clamp01(baseColor.b * tintMultiplier.b),
                    Mathf.Clamp01(baseColor.a * authoredAlphaMultiplier * transitionPulse));
                renderer.color = emphasizedColor;
            }
        }

        RefreshCustomAuthoredOpenDoorPresentation(openAmount, transitionPulse, useCustomAuthoredOpenDoorPresentation);
    }

    private void RefreshCustomAuthoredOpenDoorPresentation(float openAmount, float transitionPulse, bool useCustomAuthoredOpenDoorPresentation)
    {
        if (authoredOpenDoorRenderer == null)
        {
            return;
        }

        if (!useCustomAuthoredOpenDoorPresentation || !TryGetOpenDoorPresentationSprite(out Sprite customOpenDoorSprite))
        {
            authoredOpenDoorRenderer.enabled = false;
            authoredOpenDoorRenderer.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        authoredOpenDoorRenderer.enabled = true;
        authoredOpenDoorRenderer.sprite = customOpenDoorSprite;

        bool isFrontDoorVariant = authoredDoorVisualVariant == AuthoredDoorVisualVariant.FrontDoor;
        if (isFrontDoorVariant)
        {
            bool isOpenState = IsDoorOpenRuntime() || isOpen || visualOpenAmount > 0.01f;
            authoredOpenDoorRenderer.enabled = isOpenState;
            authoredOpenDoorRenderer.forceRenderingOff = false;
            authoredOpenDoorRenderer.sharedMaterial = GetFrontDoorOpenMaterial();
            authoredOpenDoorRenderer.transform.localScale = FrontDoorOpenVisualFixedScale;
            authoredOpenDoorRenderer.transform.localPosition = authoredVisualClosedLocalPositions.Length > 0
                ? authoredVisualClosedLocalPositions[0]
                : Vector3.zero;
            if (TryGetRepresentativeAuthoredRenderer(out SpriteRenderer frontDoorRepresentative))
            {
                CopyRepresentativeRendererSettings(authoredOpenDoorRenderer, frontDoorRepresentative, 2);
                authoredOpenDoorRenderer.sharedMaterial = GetFrontDoorOpenMaterial();
            }
            authoredOpenDoorRenderer.color = isOpenState ? Color.white : new Color(1f, 1f, 1f, 0f);
            return;
        }

        bool hasPresentationBounds = false;
        Bounds presentationBounds = default;

        if (TryGetRepresentativeAuthoredRenderer(out SpriteRenderer representativeRenderer))
        {
            CopyRepresentativeRendererSettings(authoredOpenDoorRenderer, representativeRenderer, 2);

            if (representativeRenderer.sprite != null)
            {
                Bounds worldBounds = representativeRenderer.bounds;
                Vector3 minLocal = transform.InverseTransformPoint(worldBounds.min);
                Vector3 maxLocal = transform.InverseTransformPoint(worldBounds.max);
                presentationBounds = new Bounds((minLocal + maxLocal) * 0.5f, maxLocal - minLocal);
                hasPresentationBounds = true;
            }
        }

        if (!hasPresentationBounds)
        {
            hasPresentationBounds = TryGetAuthoredVisualLocalBounds(out presentationBounds);
        }

        if (hasPresentationBounds)
        {
            Vector2 spriteSize = customOpenDoorSprite.bounds.size;
            float scaleX = spriteSize.x > 0.0001f ? presentationBounds.size.x / spriteSize.x : 1f;
            float scaleY = spriteSize.y > 0.0001f ? presentationBounds.size.y / spriteSize.y : 1f;
            Vector3 localScale = new Vector3(scaleX, scaleY, 1f);

            Vector3 pivotOffset = Vector3.Scale(customOpenDoorSprite.bounds.center, localScale);
            Vector3 targetCenter = presentationBounds.center;

            if (authoredDoorVisualVariant == AuthoredDoorVisualVariant.SideDoor42)
            {
                float visibleOpenWidth = spriteSize.x * Mathf.Abs(localScale.x);
                float slideOffset = presentationBounds.extents.x + visibleOpenWidth * 0.55f + 0.04f;
                targetCenter += new Vector3(slideOffset, 0f, 0f);
            }

            authoredOpenDoorRenderer.transform.localScale = localScale;
            authoredOpenDoorRenderer.transform.localPosition = targetCenter - pivotOffset;
        }
        else
        {
            authoredOpenDoorRenderer.transform.localPosition = Vector3.zero;
            authoredOpenDoorRenderer.transform.localScale = Vector3.one;
        }

        float openVisualAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.38f, openAmount));
        authoredOpenDoorRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(openVisualAlpha * transitionPulse));
    }

    private bool UseCustomAuthoredOpenDoorPresentation()
    {
        return TryGetOpenDoorPresentationSprite(out _);
    }

    private bool TryGetOpenDoorPresentationSprite(out Sprite sprite)
    {
        switch (authoredDoorVisualVariant)
        {
            case AuthoredDoorVisualVariant.SideDoor42:
                return TryGetCustomSideDoorOpenSprite(out sprite);
            case AuthoredDoorVisualVariant.FrontDoor:
                return TryGetCustomAuthoredOpenDoorSprite(out sprite);
            default:
                sprite = null;
                return false;
        }
    }

    private static bool TryGetCustomAuthoredOpenDoorSprite(out Sprite sprite)
    {
        return TryLoadCustomDoorSprite(CustomAuthoredOpenDoorResourcePath, ref sharedCustomAuthoredOpenDoorSprite, out sprite);
    }

    private static bool TryGetCustomSideDoorClosedSprite(out Sprite sprite)
    {
        return TryLoadCustomDoorSprite(CustomSideDoorClosedResourcePath, ref sharedCustomSideDoorClosedSprite, out sprite);
    }

    private static bool TryGetCustomSideDoorOpenSprite(out Sprite sprite)
    {
        return TryLoadCustomDoorSprite(CustomSideDoorOpenResourcePath, ref sharedCustomSideDoorOpenSprite, out sprite);
    }

    private static bool TryLoadCustomDoorSprite(string resourcePath, ref Sprite cachedSprite, out Sprite sprite)
    {
        sprite = cachedSprite;

        if (sprite != null)
        {
            return true;
        }

        Sprite loadedSprite = null;
        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcePath);

        if (loadedSprites != null && loadedSprites.Length > 0)
        {
            loadedSprite = loadedSprites[0];
        }

        loadedSprite ??= Resources.Load<Sprite>(resourcePath);

        if (loadedSprite == null)
        {
            return false;
        }

        cachedSprite = loadedSprite;
        sprite = cachedSprite;
        return true;
    }

    private static void CopyRepresentativeRendererSettings(SpriteRenderer targetRenderer, SpriteRenderer representativeRenderer, int sortingOrderOffset)
    {
        if (targetRenderer == null || representativeRenderer == null)
        {
            return;
        }

        targetRenderer.sharedMaterial = representativeRenderer.sharedMaterial;
        targetRenderer.sortingLayerID = representativeRenderer.sortingLayerID;
        targetRenderer.sortingOrder = representativeRenderer.sortingOrder + sortingOrderOffset;
        targetRenderer.maskInteraction = representativeRenderer.maskInteraction;
        targetRenderer.flipX = representativeRenderer.flipX;
        targetRenderer.flipY = representativeRenderer.flipY;
        targetRenderer.drawMode = SpriteDrawMode.Simple;
        targetRenderer.spriteSortPoint = representativeRenderer.spriteSortPoint;
    }

    private static Material GetFrontDoorOpenMaterial()
    {
        if (sharedFrontDoorOpenMaterial != null)
        {
            return sharedFrontDoorOpenMaterial;
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Shader fallback = litShader != null ? litShader : unlitShader;

        if (fallback == null)
        {
            return null;
        }

        sharedFrontDoorOpenMaterial = new Material(fallback);
        return sharedFrontDoorOpenMaterial;
    }

    private bool TryGetRepresentativeAuthoredRenderer(out SpriteRenderer renderer)
    {
        renderer = null;
        int bestSortingOrder = int.MinValue;
        float bestArea = float.MinValue;

        if (authoredVisualRenderersByRoot == null || authoredVisualRenderersByRoot.Length == 0)
        {
            if (authoredVisualRoots == null || authoredVisualRoots.Length == 0)
            {
                return false;
            }

            authoredVisualRenderersByRoot = new SpriteRenderer[authoredVisualRoots.Length][];
        }

        for (int rootIndex = 0; rootIndex < authoredVisualRenderersByRoot.Length; rootIndex++)
        {
            SpriteRenderer[] renderers = authoredVisualRenderersByRoot[rootIndex];

            if ((renderers == null || renderers.Length == 0)
                && authoredVisualRoots != null
                && rootIndex < authoredVisualRoots.Length
                && authoredVisualRoots[rootIndex] != null)
            {
                renderers = authoredVisualRoots[rootIndex].GetComponentsInChildren<SpriteRenderer>(true);
                authoredVisualRenderersByRoot[rootIndex] = renderers;
            }

            if (renderers == null)
            {
                continue;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SpriteRenderer candidateRenderer = renderers[rendererIndex];

                if (candidateRenderer == null || candidateRenderer.sprite == null)
                {
                    continue;
                }

                string candidateName = candidateRenderer.name ?? string.Empty;

                if (candidateName.IndexOf("Sign", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidateName.IndexOf("Beacon", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidateName.IndexOf("Marker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                Vector3 spriteSize = candidateRenderer.sprite.bounds.size;
                float area = spriteSize.x * spriteSize.y;
                bool betterSorting = candidateRenderer.sortingOrder > bestSortingOrder;
                bool sameSortingLargerArea = candidateRenderer.sortingOrder == bestSortingOrder && area > bestArea;

                if (!betterSorting && !sameSortingLargerArea)
                {
                    continue;
                }

                renderer = candidateRenderer;
                bestSortingOrder = candidateRenderer.sortingOrder;
                bestArea = area;
            }
        }

        return renderer != null;
    }

    private bool IsDoorOpenRuntime()
    {
        if (mapService == null || doorCells == null || doorCells.Length == 0)
        {
            return isOpen;
        }

        for (int index = 0; index < doorCells.Length; index++)
        {
            if (!mapService.IsDoorOpen(doorCells[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static Vector3 ResolveAuthoredVisualDirection(Transform visualRoot, Vector2 doorwayCenter, bool horizontalDoor)
    {
        if (visualRoot == null)
        {
            return Vector3.zero;
        }

        string visualName = visualRoot.name ?? string.Empty;

        if (visualName.IndexOf("Top", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Vector3.up;
        }

        if (visualName.IndexOf("Bottom", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Vector3.down;
        }

        if (visualName.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Vector3.left;
        }

        if (visualName.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Vector3.right;
        }

        Vector2 visualCenter = visualRoot.position;
        float delta = horizontalDoor
            ? visualCenter.x - doorwayCenter.x
            : visualCenter.y - doorwayCenter.y;

        if (Mathf.Abs(delta) <= 0.08f)
        {
            return Vector3.zero;
        }

        if (horizontalDoor)
        {
            return delta > 0f ? Vector3.right : Vector3.left;
        }

        return delta > 0f ? Vector3.up : Vector3.down;
    }

    private void RefreshPhysicalBlocker()
    {
        EnsureVisuals();
        RefreshVisualLayout();

        if (physicalBlockerCollider != null)
        {
            ResolvePhysicalBlockerShape(out Vector2 blockerOffset, out Vector2 blockerSize);
            physicalBlockerCollider.offset = blockerOffset;
            physicalBlockerCollider.size = blockerSize;
        }

        if (physicalBlockerCollider == null
            && lightBlockerCollider == null
            && lightBlockerShadowCaster == null)
        {
            physicalBlockerDirty = false;
            return;
        }

        MainEscapeDoorPassabilityController passabilityController = EnsureDoorPassabilityController();
        if (passabilityController != null)
        {
            passabilityController.Configure(physicalBlockerCollider, lightBlockerCollider, lightBlockerShadowCaster);
            passabilityController.SetPassable(isOpen);
        }

        physicalBlockerDirty = false;
    }

    private MainEscapeDoorPassabilityController EnsureDoorPassabilityController()
    {
        if (doorPassabilityController != null)
        {
            return doorPassabilityController;
        }

        doorPassabilityController = GetComponent<MainEscapeDoorPassabilityController>();
        if (doorPassabilityController == null)
        {
            doorPassabilityController = gameObject.AddComponent<MainEscapeDoorPassabilityController>();
        }

        return doorPassabilityController;
    }

    private void ResolvePhysicalBlockerShape(out Vector2 offset, out Vector2 size)
    {
        size = GetPhysicalBlockerSize();
        offset = Vector2.zero;
    }

    private float ResolveHeaderSignOffsetY(Vector2 panelSize, bool horizontalDoor)
    {
        float fallbackOffsetY = horizontalDoor
            ? Mathf.Max(0.98f, panelSize.y * 0.85f + 0.62f)
            : Mathf.Max(1.08f, panelSize.y * 0.5f + 0.62f);

        if (!TryGetAuthoredVisualLocalBounds(out Bounds authoredBounds))
        {
            return fallbackOffsetY;
        }

        return Mathf.Max(fallbackOffsetY, authoredBounds.max.y + 0.24f);
    }

    private void ResolveLightBlockerShape(out Vector2 offset, out Vector2 size)
    {
        Vector2 panelSize = GetDoorPanelSize();
        Vector2 physicalSize = GetPhysicalBlockerSize();
        bool horizontalDoor = panelSize.x >= panelSize.y;

        offset = horizontalDoor
            ? new Vector2(0f, 0.2f)
            : new Vector2(0f, 0.16f);
        size = horizontalDoor
            ? new Vector2(
                Mathf.Max(physicalSize.x + 0.08f, panelSize.x + 0.14f),
                Mathf.Max(1.18f, panelSize.y * 3.8f))
            : new Vector2(
                Mathf.Max(0.84f, physicalSize.x + 0.08f),
                Mathf.Max(1.52f, physicalSize.y + 0.74f));

        if (!TryGetAuthoredVisualLocalBounds(out Bounds authoredBounds))
        {
            return;
        }

        offset = authoredBounds.center;
        size = new Vector2(
            Mathf.Max(size.x, authoredBounds.size.x + 0.08f),
            Mathf.Max(size.y, authoredBounds.size.y + 0.08f));
    }

    private bool TryGetAuthoredVisualLocalBounds(out Bounds localBounds)
    {
        localBounds = default;

        if (authoredVisualRoots == null || authoredVisualRoots.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;

        for (int index = 0; index < authoredVisualRoots.Length; index++)
        {
            Transform authoredRoot = authoredVisualRoots[index];

            if (authoredRoot == null)
            {
                continue;
            }

            SpriteRenderer[] renderers = index < authoredVisualRenderersByRoot.Length
                ? authoredVisualRenderersByRoot[index]
                : authoredRoot.GetComponentsInChildren<SpriteRenderer>(true);

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SpriteRenderer renderer = renderers[rendererIndex];

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3 minLocal = transform.InverseTransformPoint(worldBounds.min);
                Vector3 maxLocal = transform.InverseTransformPoint(worldBounds.max);
                Bounds rendererLocalBounds = new((minLocal + maxLocal) * 0.5f, maxLocal - minLocal);

                if (!hasBounds)
                {
                    localBounds = rendererLocalBounds;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(rendererLocalBounds.min);
                    localBounds.Encapsulate(rendererLocalBounds.max);
                }
            }
        }

        return hasBounds;
    }

    private Vector2 GetDoorPanelSize()
    {
        if (doorCells == null || doorCells.Length == 0)
        {
            return new Vector2(0.45f, 1.3f);
        }

        int minX = doorCells[0].x;
        int maxX = doorCells[0].x;
        int minY = doorCells[0].y;
        int maxY = doorCells[0].y;

        for (int index = 1; index < doorCells.Length; index++)
        {
            Vector3Int cell = doorCells[index];
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        bool horizontalDoor = (maxX - minX) >= (maxY - minY);
        float spanX = (maxX - minX) + 1f;
        float spanY = (maxY - minY) + 1f;

        return horizontalDoor
            ? new Vector2(Mathf.Max(1.1f, spanX + 0.12f), 0.34f)
            : new Vector2(0.34f, Mathf.Max(1.1f, spanY + 0.12f));
    }

    private Vector2 GetPhysicalBlockerSize()
    {
        if (doorCells == null || doorCells.Length == 0)
        {
            return new Vector2(0.95f, 0.95f);
        }

        int minX = doorCells[0].x;
        int maxX = doorCells[0].x;
        int minY = doorCells[0].y;
        int maxY = doorCells[0].y;

        for (int index = 1; index < doorCells.Length; index++)
        {
            Vector3Int cell = doorCells[index];
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        bool horizontalDoor = (maxX - minX) >= (maxY - minY);
        float spanX = (maxX - minX) + 1f;
        float spanY = (maxY - minY) + 1f;
        return horizontalDoor
            ? new Vector2(Mathf.Max(1f, spanX + 0.05f), 0.96f)
            : new Vector2(0.96f, Mathf.Max(1f, spanY + 0.05f));
    }

    private static void EnsureSharedMarkerSprite()
    {
        if (sharedMarkerSprite != null)
        {
            return;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        sharedMarkerSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private static void EnsureSharedDoorIndicatorSprite()
    {
        if (sharedDoorIndicatorSprite != null)
        {
            return;
        }

        const int textureSize = 16;
        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color clear = Color.clear;
        Color ink = Color.white;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        for (int x = 4; x <= 11; x++)
        {
            texture.SetPixel(x, 12, ink);
        }

        for (int y = 4; y <= 12; y++)
        {
            texture.SetPixel(4, y, ink);
            texture.SetPixel(11, y, ink);
        }

        for (int x = 6; x <= 9; x++)
        {
            texture.SetPixel(x, 10, ink);
        }

        for (int y = 5; y <= 10; y++)
        {
            texture.SetPixel(8, y, ink);
        }

        texture.SetPixel(9, 7, ink);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        sharedDoorIndicatorSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
    }
}


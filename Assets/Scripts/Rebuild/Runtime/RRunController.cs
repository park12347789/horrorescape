using System;

using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class RRunController : MonoBehaviour, IRunStateController, IFloorEscapeInteractionController
{
    [Header("Authored Runtime")]
    [SerializeField] private RRunSessionController runSessionController;
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private FloorEscapeGoalPickup goalPickup;
    [SerializeField] private FloorEscapeTransitionPoint stairPoint;
    [SerializeField] private FloorEscapeTransitionPoint finalExitPoint;
    [SerializeField] private PrototypeInventoryPickup batteryPickup;
    [SerializeField] private PrototypeInventoryPickup bottlePickup;
    [SerializeField] private PrototypeInventoryPickup medkitPickup;
    [SerializeField] private MainEscapeKeyGatePoint keyGatePoint;
    [SerializeField] private MainEscapeEmergencyStairsPoint authoredStairsPoint;
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private SpriteRenderer accentBackdrop;
    [SerializeField] private RFloorDirector floorDirector;
    [SerializeField] private IRHudCanvas hudCanvas;

    [Header("State")]
    [SerializeField, Min(1)] private int authoredFloorNumber = 5;
    [SerializeField] private bool directExitRequiresKeyOnAuthoredFloors;
    [SerializeField] private int currentFloorIndex;
    [SerializeField] private bool currentFloorGateUnlocked;
    [SerializeField] private bool escaped;
    [SerializeField] private string statusMessage = "Wake up on 5F and reach the street.";

    public event Action<int, int> FloorCleared;
    public event Action<int> FinalEscapeCompleted;
    public event Action<string> RunFailed;

    public int CurrentFloorNumber => GetCurrentFloor().FloorNumber;
    public bool IsEscaped => escaped;
    public bool IsPickupVisible => false;
    public bool UsesDirectAuthoredExitInteraction => UsesAuthoredKeyGateSequence
        && (ResolveDirectExitRequiresKeyOnAuthoredFloors() || !HasTileBackedAuthoredGate);
    public bool UsesFinalClearPanelShortcut => !escaped
        && CurrentFloorNumber == 1
        && finalExitPoint == null
        && authoredStairsPoint != null;
    public string StatusMessage => statusMessage;
    public bool IsAuthoredGateVisible => !escaped && CurrentFloorNumber > 1 && !UsesDirectAuthoredExitInteraction;
    public bool RequiresAuthoredGateInteraction => UsesAuthoredKeyGateSequence && !UsesDirectAuthoredExitInteraction;
    public bool IsAuthoredStairsVisible => !escaped
        && ((CurrentFloorNumber > 1 && !UsesElevatorPropDirectExit) || UsesFinalClearPanelShortcut);
    public bool IsAuthoredGateUnlocked => currentFloorGateUnlocked;
    public bool HasAuthoredGateKey => !escaped
        && playerInventory != null
        && playerInventory.HasItem(PrototypeItemCatalog.IronGateKeyItemId);
    private bool UsesAuthoredKeyGateSequence => !escaped && CurrentFloorNumber > 1;
    private bool UsesElevatorPropDirectExit => UsesDirectAuthoredExitInteraction
        && RDirectExitRouteUtility.UsesElevatorPropDirectExit(CurrentFloorNumber);
    private bool HasTileBackedAuthoredGate => floorDirector != null && floorDirector.HasMainGate;
    private int TerminalFloorNumber
    {
        get
        {
            RRunSessionController sessionController = ResolveSessionController();
            return sessionController != null ? sessionController.TerminalFloorNumber : 1;
        }
    }

    private void Awake()
    {
    }

    private void Reset()
    {
        AssignCanonicalProgressionRulesIfMissing();
    }

    private void OnValidate()
    {
        AssignCanonicalProgressionRulesIfMissing();
        authoredFloorNumber = Mathf.Max(1, authoredFloorNumber);

        if (!Application.isPlaying)
        {
            currentFloorIndex = ResolveFloorIndex(authoredFloorNumber);
        }

        ValidateBindings();
    }

    [ContextMenu("Assign Canonical Progression Rules")]
    private void AssignCanonicalProgressionRules()
    {
        progressionRules = RRunCanonicalAssetLocator.TryLoadProgressionRules(this);
    }

    private void OnDestroy()
    {
        if (runSessionController != null)
        {
            runSessionController.SnapshotChanged -= HandleSnapshotChanged;
        }
    }

    public void Initialize(
        RRunSessionController sessionController,
        WasdPlayerController controller,
        PlayerInventory inventory,
        FloorEscapeGoalPickup pickup,
        FloorEscapeTransitionPoint legacyStairProxy,
        FloorEscapeTransitionPoint finalExit,
        PrototypeInventoryPickup flashlightBatteryPickup,
        PrototypeInventoryPickup throwableBottlePickup,
        PrototypeInventoryPickup healPickup,
        MainEscapeKeyGatePoint keyGate,
        MainEscapeEmergencyStairsPoint authoredStairs,
        FlashlightFogOfWarOverlay fogOfWar,
        SpriteRenderer backdrop,
        RFloorDirector director,
        IRHudCanvas hud,
        int activeFloorNumber = 5)
    {
        BindSessionController(sessionController ?? ResolveSessionControllerFallback());
        playerController = controller;
        playerRuntime = controller != null ? controller.GetComponent<RPlayerRuntimeReferences>() : null;
        playerInventory = inventory != null ? inventory : playerRuntime != null ? playerRuntime.Inventory : null;
        goalPickup = pickup;
        stairPoint = authoredStairs == null ? legacyStairProxy : null;
        finalExitPoint = finalExit;
        batteryPickup = flashlightBatteryPickup;
        bottlePickup = throwableBottlePickup;
        medkitPickup = healPickup;
        keyGatePoint = keyGate;
        authoredStairsPoint = authoredStairs;
        fogOfWarOverlay = fogOfWar;
        accentBackdrop = backdrop;
        floorDirector = director;
        hudCanvas = hud;

        int resolvedFloorNumber = activeFloorNumber > 0
            ? activeFloorNumber
            : runSessionController != null && runSessionController.Snapshot.CurrentFloorNumber > 0
                ? runSessionController.Snapshot.CurrentFloorNumber
                : authoredFloorNumber;
        currentFloorIndex = ResolveFloorIndex(resolvedFloorNumber);
        currentFloorGateUnlocked = false;
        escaped = false;
        statusMessage = BuildStartupStatusMessage();

        ValidateBindings();
        ApplyCurrentFloorState(true);
        BindHudCanvas(hudCanvas);
    }

    public void BindSessionController(RRunSessionController sessionController)
    {
        if (runSessionController == sessionController)
        {
            return;
        }

        if (runSessionController != null)
        {
            runSessionController.SnapshotChanged -= HandleSnapshotChanged;
        }

        runSessionController = sessionController;

        if (runSessionController != null)
        {
            runSessionController.SnapshotChanged += HandleSnapshotChanged;
        }
    }

    private RRunSessionController ResolveSessionController()
    {
        if (runSessionController != null)
        {
            return runSessionController;
        }

        runSessionController = ResolveSessionControllerFallback();

        if (runSessionController != null)
        {
            BindSessionController(runSessionController);
        }

        return runSessionController;
    }

    private RRunSessionController ResolveSessionControllerFallback()
    {
        return RRunSessionResolver.ResolveForContext(this);
    }

    private void ValidateBindings()
    {
        if (progressionRules == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError($"{nameof(RRunController)} is missing its {nameof(RRunProgressionRules)} asset reference.", this);
            }
            else
            {
                Debug.LogWarning($"{nameof(RRunController)} is missing its {nameof(RRunProgressionRules)} asset reference.", this);
            }
        }

        if (Application.isPlaying && runSessionController == null)
        {
            RRunSessionController resolvedSessionController = ResolveSessionControllerFallback();

            if (resolvedSessionController != null)
            {
                BindSessionController(resolvedSessionController);
            }
        }

        if (Application.isPlaying && hudCanvas == null)
        {
            Debug.LogError($"{nameof(RRunController)} is missing its IRHudCanvas reference.", this);
        }
    }

    private void AssignCanonicalProgressionRulesIfMissing()
    {
        progressionRules ??= RRunCanonicalAssetLocator.TryLoadProgressionRules(this);
    }

    private void HandleSnapshotChanged(RRunSnapshot snapshot)
    {
        bool shouldRefreshFloorPresentation = false;

        if (snapshot.CurrentFloorNumber > 0)
        {
            int resolvedFloorIndex = ResolveFloorIndex(snapshot.CurrentFloorNumber);

            if (currentFloorIndex != resolvedFloorIndex)
            {
                currentFloorIndex = resolvedFloorIndex;
                shouldRefreshFloorPresentation = true;
            }
        }

        if (snapshot.HasCompletedRun && snapshot.WasSuccessful)
        {
            if (!escaped)
            {
                escaped = true;
                shouldRefreshFloorPresentation = true;
            }
        }

        if (shouldRefreshFloorPresentation)
        {
            RefreshFloorPresentation();
            RefreshPresentation();
        }
    }

    private int ResolveFloorIndex(int floorNumber)
    {
        EscapeFloorDefinition[] floors = MainEscapeFloorCatalog.AllFloors;
        int resolvedFloorNumber = Mathf.Max(1, floorNumber);

        for (int index = 0; index < floors.Length; index++)
        {
            if (floors[index].FloorNumber == resolvedFloorNumber)
            {
                return index;
            }
        }

        return 0;
    }

    private EscapeFloorDefinition GetCurrentFloor()
    {
        return MainEscapeFloorCatalog.GetFloorByIndex(currentFloorIndex);
    }

    private bool IsTerminalRouteFloor(int floorNumber)
    {
        return Mathf.Max(1, floorNumber) == TerminalFloorNumber;
    }

    private string BuildStartupStatusMessage()
    {
        return progressionRules != null
            ? progressionRules.BuildStartupMessage(
                CurrentFloorNumber,
                UsesFinalClearPanelShortcut,
                UsesElevatorPropDirectExit,
                IsTerminalRouteFloor(CurrentFloorNumber))
            : CurrentFloorNumber <= 1
                ? UsesFinalClearPanelShortcut
                    ? "Wake up on 1F and trigger the clear panel."
                    : "Wake up on 1F and use the street exit."
                : UsesElevatorPropDirectExit
                    ? $"Wake up on {CurrentFloorNumber}F. Find the Iron Gate Key, then use the elevator."
                    : IsTerminalRouteFloor(CurrentFloorNumber)
                        ? $"Wake up on {CurrentFloorNumber}F. Find the Iron Gate Key, then use the exit route."
                        : $"Wake up on {CurrentFloorNumber}F. Find the Iron Gate Key.";
    }

    private void ApplyCurrentFloorState(bool resetPlayerPosition)
    {
        EscapeFloorDefinition floor = GetCurrentFloor();

        if (!UsesAuthoredKeyGateSequence)
        {
            currentFloorGateUnlocked = false;
        }

        if (!RRunFloorStateApplier.Apply(new RRunFloorStateContext(
            this,
            floor,
            CurrentFloorNumber,
            escaped,
            resetPlayerPosition,
            UsesAuthoredKeyGateSequence,
            playerController,
            playerRuntime,
            fogOfWarOverlay,
            floorDirector,
            goalPickup,
            stairPoint,
            finalExitPoint,
            batteryPickup,
            bottlePickup,
            medkitPickup,
            keyGatePoint,
            authoredStairsPoint)))
        {
            return;
        }
        RefreshFloorPresentation();
        RefreshPresentation();
    }

    public bool IsTransitionVisible(FloorEscapeTransitionKind kind)
    {
        if (escaped)
        {
            return kind == FloorEscapeTransitionKind.FinalExit;
        }

        return kind switch
        {
            FloorEscapeTransitionKind.EmergencyStairs => IsAuthoredStairsVisible,
            FloorEscapeTransitionKind.FinalExit => CurrentFloorNumber == 1,
            _ => false
        };
    }

    public bool IsTransitionUnlocked(FloorEscapeTransitionKind kind)
    {
        if (escaped)
        {
            return true;
        }

        return kind switch
        {
            FloorEscapeTransitionKind.EmergencyStairs => CurrentFloorNumber > 1
                && (UsesDirectAuthoredExitInteraction
                    ? HasAuthoredGateKey
                    : currentFloorGateUnlocked)
                || UsesFinalClearPanelShortcut,
            FloorEscapeTransitionKind.FinalExit => CurrentFloorNumber == 1
                && HasAuthoredGateKey,
            _ => false
        };
    }

    public bool TryRecoverCurrentTool()
    {
        if (escaped)
        {
            statusMessage = ResolveRouteAlreadyClearMessage();
            RefreshPresentation();
            return false;
        }

        statusMessage = ResolveNoFloorToolPickupMessage();
        PrototypeAudioManager.TryPlayDenied();
        RefreshPresentation();
        return false;
    }

    public bool TryUnlockKeyGate()
    {
        if (!UsesAuthoredKeyGateSequence)
        {
            statusMessage = ResolveGateNotPartOfRouteMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        if (UsesDirectAuthoredExitInteraction)
        {
            statusMessage = HasAuthoredGateKey
                ? ResolveDirectExitReadyMessage()
                : ResolveNeedIronGateKeyMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        if (currentFloorGateUnlocked)
        {
            statusMessage = ResolveGateAlreadyOpenMessage();
            RefreshFloorPresentation();
            RefreshPresentation();
            return true;
        }

        if (!HasAuthoredGateKey)
        {
            statusMessage = ResolveNeedIronGateKeyMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        if (playerInventory == null || !playerInventory.RemoveItem(PrototypeItemCatalog.IronGateKeyItemId))
        {
            statusMessage = ResolveKeyWentMissingMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        bool openedDoor = floorDirector == null || floorDirector.TryUnlockMainGateRoute();

        if (!openedDoor)
        {
            statusMessage = ResolveGateOpenFailedMessage();
            Debug.LogError($"{nameof(RRunController)} could not open the authored iron gate. Check the authored main gate door cells on the floor authoring.", this);
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        currentFloorGateUnlocked = true;
        statusMessage = ResolveGateUnlockedMessage();
        PrototypeAudioManager.TryPlayDoorOpen();
        RefreshFloorPresentation();
        RefreshPresentation();
        return true;
    }

    public bool TryUseEmergencyStairs()
    {
        if (escaped)
        {
            statusMessage = ResolveRouteAlreadyClearMessage();
            RefreshPresentation();
            return false;
        }

        if (CurrentFloorNumber <= 1)
        {
            if (!UsesFinalClearPanelShortcut)
            {
                statusMessage = ResolveNoLowerStairRouteMessage();
                PrototypeAudioManager.TryPlayDenied();
                RefreshPresentation();
                return false;
            }

            escaped = true;
            statusMessage = ResolveClearPanelEscapeSuccessMessage();
            PrototypeAudioManager.TryPlayFinalEscape();
            RefreshFloorPresentation();
            RefreshPresentation();
            RecordFinalClearHandoff(ResolveSessionController(), CurrentFloorNumber);
            ShowFinalClearPanel();
            FinalEscapeCompleted?.Invoke(CurrentFloorNumber);
            return true;
        }

        int clearedFloorNumber = CurrentFloorNumber;

        if (UsesDirectAuthoredExitInteraction)
        {
            if (!HasAuthoredGateKey)
            {
                statusMessage = ResolveNeedKeyForDirectExitMessage();
                PrototypeAudioManager.TryPlayDenied();
                RefreshPresentation();
                return false;
            }

            if (playerInventory == null || !playerInventory.RemoveItem(PrototypeItemCatalog.IronGateKeyItemId))
            {
                statusMessage = ResolveKeyWentMissingMessage();
                PrototypeAudioManager.TryPlayDenied();
                RefreshPresentation();
                return false;
            }

            currentFloorGateUnlocked = true;
        }
        else if (UsesAuthoredKeyGateSequence && !currentFloorGateUnlocked)
        {
            statusMessage = ResolveUnlockGateBeforeStairsMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        RRunSessionController sessionController = ResolveSessionController();

        if (sessionController == null)
        {
            Debug.LogError($"{nameof(RRunController)} cannot move to the next floor because no RRunSessionController is bound.", this);
            statusMessage = ResolveRunSessionUnavailableMessage();
            RefreshPresentation();
            return false;
        }

        int destinationFloorNumber = RSceneRouter.ResolveExitDestinationFloor(sessionController, CurrentFloorNumber);

        if (destinationFloorNumber <= 0)
        {
            if (sessionController.IsTerminalFloor(CurrentFloorNumber))
            {
                escaped = true;
                statusMessage = ResolveEscapeReturnLobbyMessage();
                PrototypeAudioManager.TryPlayFinalEscape();
                RefreshFloorPresentation();
                RefreshPresentation();
                RecordFinalClearHandoff(sessionController, CurrentFloorNumber);
                ShowFinalClearPanel();
                FinalEscapeCompleted?.Invoke(CurrentFloorNumber);
                return true;
            }

            statusMessage = ResolveNoLowerFloorSceneConfiguredMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        bool destinationIsTerminal = sessionController.IsTerminalFloor(destinationFloorNumber);
        statusMessage = ResolveFloorArrivalMessage(destinationFloorNumber, destinationIsTerminal);
        FloorCleared?.Invoke(clearedFloorNumber, destinationFloorNumber);
        AdvanceToFloorHandoff(
            sessionController,
            clearedFloorNumber,
            destinationFloorNumber);
        return true;
    }

    public bool TryUseFinalExit()
    {
        if (escaped)
        {
            statusMessage = ResolveRouteAlreadyClearMessage();
            RefreshPresentation();
            return false;
        }

        if (CurrentFloorNumber != 1)
        {
            statusMessage = ResolveStreetAccessOnlyFromFirstFloorMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        if (!HasAuthoredGateKey)
        {
            statusMessage = ResolveNeedKeyForDirectExitMessage();
            PrototypeAudioManager.TryPlayDenied();
            RefreshPresentation();
            return false;
        }

        escaped = true;
        statusMessage = ResolveFinalExitSuccessMessage();
        PrototypeAudioManager.TryPlayFinalEscape();
        RefreshFloorPresentation();
        RefreshPresentation();
        RecordFinalClearHandoff(ResolveSessionController(), CurrentFloorNumber);
        ShowFinalClearPanel();
        FinalEscapeCompleted?.Invoke(CurrentFloorNumber);
        return true;
    }

    public bool NotifyRunFailure(string caughtBy)
    {
        IRGameClearPanelView panelView = ResolveGameClearPanel();

        if (panelView == null)
        {
            Debug.LogError($"{nameof(RRunController)} cannot show a failure modal because no IRGameClearPanelView is assigned.", this);
            return false;
        }

        RRunSessionController sessionController = ResolveSessionController();
        RecordFailureHandoff(sessionController, CurrentFloorNumber, caughtBy);
        RRunModalPresenter.TryShowFailure(panelView, sessionController, playerController, caughtBy);
        RunFailed?.Invoke(caughtBy);
        return true;
    }
}

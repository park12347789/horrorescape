using System;
using System.Collections;

using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class RSceneCompositionRoot : MonoBehaviour
{
    private static MainEscapeRuntimeSettings RuntimeSettings => MainEscapeRuntimeSettings.Load();
    private const int RuntimePlayerSortingOrder = 140;
    private const string DirectExitElevatorVisualName = "RDirectExitElevatorVisual";

    [Header("Roots")]
    [SerializeField] private Transform systemsRoot;
    [SerializeField] private Transform gameplayRoot;
    [SerializeField] private Transform runtimeRoot;
    [SerializeField] private Transform authoringWorkspaceRoot;

    [Header("Authored Scene References")]
    [SerializeField] private MainEscapeFloorAuthoring floorAuthoring;
    [SerializeField] private RRunSessionController runSessionController;
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField] private RFloorDirector floorDirector;
    [SerializeField] private RRunController runController;
    [SerializeField] private MainEscapeDebugModeController debugModeController;
    [SerializeField] private NoiseSystem noiseSystem;
    [SerializeField] private PrototypeAudioManager audioManager;
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private IRHudCanvas hudCanvas;
    [SerializeField] private IRPlayerInventoryHudBinder inventoryHudBinder;
    [SerializeField] private IRPlayerHealthHudBinder healthHudBinder;
    [SerializeField] private IRPlayerThreatHudBinder threatHudBinder;
    [SerializeField] private IRPlayerQuickSlotsHudBinder quickSlotsHudBinder;
    [SerializeField] private IRPlayerStaminaHudBinder staminaHudBinder;
    [SerializeField] private FloorEscapeGoalPickup goalPickup;
    [SerializeField] private FloorEscapeTransitionPoint emergencyStairsPoint;
    [SerializeField] private FloorEscapeTransitionPoint finalExitPoint;
    [SerializeField] private PrototypeInventoryPickup batteryPickup;
    [SerializeField] private PrototypeInventoryPickup bottlePickup;
    [SerializeField] private PrototypeInventoryPickup medkitPickup;
    [SerializeField] private MainEscapeKeyPickup keyPickup;
    [SerializeField] private MainEscapeKeyGatePoint keyGatePoint;
    [SerializeField] private MainEscapeEmergencyStairsPoint authoredStairsPoint;
    [SerializeField] private Transform elevatorExitRoot;
    [SerializeField] private SpriteRenderer elevatorExitVisual;
    [SerializeField] private MainEscapeElevatorExitInteractable elevatorExitPoint;
    [SerializeField] private SpriteRenderer accentBackdrop;

    [Header("Runtime Binding Cache")]
    [SerializeField] private MonoBehaviour[] fogOverlayConsumerBehaviours = Array.Empty<MonoBehaviour>();
    [SerializeField] private MonoBehaviour[] debugModeApplierBehaviours = Array.Empty<MonoBehaviour>();
    [SerializeField] private MonoBehaviour[] hudBinderBehaviours = Array.Empty<MonoBehaviour>();

    [Header("Composition")]
    [SerializeField] private bool composeOnPlay = true;
    [SerializeField] private bool disableWorkspaceRootsOnCompose = true;

    private bool composed;
    private bool compositionInProgress;
    private Coroutine stagedCompositionCoroutine;

    public bool IsRuntimeCompositionReady => composed;
    public bool ExpectsRuntimeCompositionOnPlay => composeOnPlay && isActiveAndEnabled;

    private void Reset()
    {
        CacheBindingReferences();
    }

    private void Start()
    {
        if (!Application.isPlaying || !composeOnPlay)
        {
            return;
        }

        stagedCompositionCoroutine = StartCoroutine(ComposeRuntimeAcrossFrames());
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        CacheBindingReferences();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        runSessionController?.UnbindGameplayRuntime(playerRuntime);
    }

    public void ComposeRuntime()
    {
        if (composed || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return;
        }

        if (stagedCompositionCoroutine != null)
        {
            StopCoroutine(stagedCompositionCoroutine);
            stagedCompositionCoroutine = null;
        }

        if (!compositionInProgress)
        {
            if (!BeginRuntimeComposition())
            {
                return;
            }

            ComposeCoreRuntime();
        }

        CompleteRuntimeComposition();
    }

    private IEnumerator ComposeRuntimeAcrossFrames()
    {
        if (!BeginRuntimeComposition())
        {
            stagedCompositionCoroutine = null;
            yield break;
        }

        ComposeCoreRuntime();
        yield return null;
        CompleteRuntimeComposition();
    }

    private bool BeginRuntimeComposition()
    {
        if (compositionInProgress || composed || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return false;
        }

        if (!PrepareRuntimeComposition())
        {
            return false;
        }

        PrepareSceneForRuntimeComposition();
        compositionInProgress = true;
        return true;
    }

    private void ComposeCoreRuntime()
    {
        ComposeFloorRuntime();
        InitializeRunRuntime();
    }

    private void CompleteRuntimeComposition()
    {
        if (!compositionInProgress || composed)
        {
            stagedCompositionCoroutine = null;
            return;
        }

        InstallPostComposeFeatures();
        composed = true;
        compositionInProgress = false;
        stagedCompositionCoroutine = null;
    }

    private bool PrepareRuntimeComposition()
    {
        runSessionController = ResolveSessionController();
        BindOptionalPlayerStateStore();
        CacheSceneReferences();
        EnsureRuntimePlayerBindings();
        CacheBindingReferences();
        return ValidateBindings();
    }

    private void PrepareSceneForRuntimeComposition()
    {
        if (disableWorkspaceRootsOnCompose)
        {
            DisableEditorOnlyWorkspaceRoots();
        }

        PrototypeItemUiIconResolver.ClearCache();
    }

    private void ComposeFloorRuntime()
    {
        if (floorDirector == null || playerController == null || playerRuntime == null)
        {
            Debug.LogWarning($"{nameof(RSceneCompositionRoot)} skipped floor runtime composition because core runtime references are incomplete.", this);
            return;
        }

        floorDirector.Initialize(playerController, playerRuntime, fogOfWarOverlay, accentBackdrop, enableVentMarkers: false);
        BindGameplayRuntime();
    }

    private void InitializeRunRuntime()
    {
        if (runController == null)
        {
            Debug.LogWarning($"{nameof(RSceneCompositionRoot)} skipped run runtime initialization because no {nameof(RRunController)} is assigned.", this);
            return;
        }

        int activeFloorNumber = ResolveActiveFloorNumber(runSessionController);
        RRunSceneBindings sceneBindings = BuildRunSceneBindings();
        runController.Initialize(runSessionController, sceneBindings, activeFloorNumber);
    }

    private void InstallPostComposeFeatures()
    {
        debugModeController = ResolveDebugModeControllerForPostCompose();

        if (debugModeController != null)
        {
            debugModeController.Initialize(
                playerController,
                playerRuntime,
                floorDirector,
                noiseSystem,
                fogOfWarOverlay,
                RuntimeSettings.StartInDebugMode);
            BindDebugRuntime();
        }

        InitializeShadowStartleRuntime();
        ConfigureDirectExitInteractable();
        BindHudRuntime();
    }

    private MainEscapeDebugModeController ResolveDebugModeControllerForPostCompose()
    {
        debugModeController ??= GetComponent<MainEscapeDebugModeController>();
        debugModeController ??=
            RSceneReferenceLookup.FindFirstComponentInScene<MainEscapeDebugModeController>(gameObject.scene);

        if (debugModeController != null)
        {
            return debugModeController;
        }

        if (!RuntimeSettings.StartInDebugMode)
        {
            return null;
        }

        return gameObject.AddComponent<MainEscapeDebugModeController>();
    }

    [ContextMenu("Cache Scene References")]
    public void CacheSceneReferences()
    {
        MainEscapeRuntimeSettings settings = RuntimeSettings;
        systemsRoot ??= RSceneReferenceLookup.FindTransformInScene(gameObject.scene, "RSystems", settings.ReferenceSystemsRootName);
        gameplayRoot ??= RSceneReferenceLookup.FindTransformInScene(gameObject.scene, "RGameplay", settings.ReferenceGameplayRootName);
        runtimeRoot ??= RSceneReferenceLookup.FindTransformInScene(gameObject.scene, "RRuntime", settings.ReferenceFloorRuntimeRootName, "MainSceneRuntime");
        floorAuthoring ??= RSceneReferenceLookup.FindUniqueComponentInScene<MainEscapeFloorAuthoring>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(floorAuthoring));
        authoringWorkspaceRoot ??= floorAuthoring != null
            ? floorAuthoring.transform.parent
            : RSceneReferenceLookup.FindTransformInScene(gameObject.scene, "RAuthoring", settings.WorkspaceRootName);
        playerController ??= RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(playerController));

        if (playerRuntime == null && playerController != null)
        {
            playerRuntime = playerController.GetComponent<RPlayerRuntimeReferences>();
        }

        floorDirector ??= RSceneReferenceLookup.FindUniqueComponentInScene<RFloorDirector>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(floorDirector));
        runController ??= RSceneReferenceLookup.FindUniqueComponentInScene<RRunController>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(runController));
        int activeFloorNumber = ResolveBoundFloorNumber();
        bool usesDirectAuthoredExit = activeFloorNumber > 1
            && runController != null
            && runController.UsesDirectAuthoredExitInteraction;
        bool usesElevatorPropDirectExit = UsesElevatorPropDirectExit(activeFloorNumber);
        debugModeController ??= GetComponent<MainEscapeDebugModeController>();
        noiseSystem ??= RSceneReferenceLookup.FindUniqueComponentInScene<NoiseSystem>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(noiseSystem));
        audioManager ??= RSceneReferenceLookup.FindUniqueComponentInScene<PrototypeAudioManager>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(audioManager));
        fogOfWarOverlay ??= RSceneReferenceLookup.FindUniqueComponentInScene<FlashlightFogOfWarOverlay>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(fogOfWarOverlay));
        hudCanvas ??= RSceneReferenceLookup.FindUniqueComponentInScene<IRHudCanvas>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(hudCanvas));

        if (playerController != null)
        {
            RRuntimeHudBinderSet resolvedHudBinders = RRuntimePlayerInstaller.ResolveHudBinders(playerController);
            inventoryHudBinder ??= resolvedHudBinders.InventoryHudBinder;
            healthHudBinder ??= resolvedHudBinders.HealthHudBinder;
            threatHudBinder ??= resolvedHudBinders.ThreatHudBinder;
            quickSlotsHudBinder ??= resolvedHudBinders.QuickSlotsHudBinder;
            staminaHudBinder ??= resolvedHudBinders.StaminaHudBinder;
        }

        RAuthoredExitReferenceSet authoredExitReferences = RAuthoredExitReferenceResolver.ResolveSceneInteractables(
            gameObject.scene,
            this,
            nameof(RSceneCompositionRoot),
            settings,
            usesDirectAuthoredExit,
            activeFloorNumber,
            new RAuthoredExitReferenceSet(
                goalPickup,
                emergencyStairsPoint,
                finalExitPoint,
                batteryPickup,
                bottlePickup,
                medkitPickup,
                keyPickup,
                keyGatePoint,
                authoredStairsPoint));
        goalPickup = authoredExitReferences.GoalPickup;
        emergencyStairsPoint = authoredExitReferences.EmergencyStairsPoint;
        finalExitPoint = authoredExitReferences.FinalExitPoint;
        batteryPickup = authoredExitReferences.BatteryPickup;
        bottlePickup = authoredExitReferences.BottlePickup;
        medkitPickup = authoredExitReferences.MedkitPickup;
        keyPickup = authoredExitReferences.KeyPickup;
        keyGatePoint = authoredExitReferences.KeyGatePoint;
        authoredStairsPoint = authoredExitReferences.AuthoredStairsPoint;

        if (usesElevatorPropDirectExit)
        {
            RDirectExitElevatorReferenceSet elevatorReferences = RAuthoredExitReferenceResolver.ResolveDirectElevatorExit(
                gameObject.scene,
                floorAuthoring,
                elevatorExitPoint ?? RSceneReferenceLookup.FindUniqueComponentInScene<MainEscapeElevatorExitInteractable>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(elevatorExitPoint)),
                elevatorExitVisual,
                elevatorExitRoot,
                DirectExitElevatorVisualName);
            elevatorExitPoint = elevatorReferences.ElevatorExitPoint;
            elevatorExitVisual = elevatorReferences.ElevatorExitVisual;
            elevatorExitRoot = elevatorReferences.ElevatorExitRoot;
        }

        accentBackdrop ??= RSceneReferenceLookup.FindComponentByNames<SpriteRenderer>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(accentBackdrop), "RAccentBackdrop", "AccentBackdrop");
        CacheBindingReferences();
    }

    private bool ValidateBindings()
    {
        int activeFloorNumber = ResolveBoundFloorNumber();
        bool usesDirectAuthoredExit = activeFloorNumber > 1
            && runController != null
            && runController.UsesDirectAuthoredExitInteraction;
        bool usesElevatorPropDirectExit = UsesElevatorPropDirectExit(activeFloorNumber);
        bool hasRequiredBindings = true;

        WarnIfMissing(systemsRoot, nameof(systemsRoot));
        WarnIfMissing(gameplayRoot, nameof(gameplayRoot));
        WarnIfMissing(runtimeRoot, nameof(runtimeRoot));
        hasRequiredBindings &= RequireBinding(floorAuthoring, nameof(floorAuthoring), "Floor authoring is required to keep the authored/runtime scene contract intact.");
        hasRequiredBindings &= RequireBinding(playerController, nameof(playerController), "The player controller is required for the playable loop.");
        hasRequiredBindings &= RequireBinding(ResolveSessionController(), nameof(runSessionController), "A run session controller is required for floor routing.");
        hasRequiredBindings &= RequireBinding(playerRuntime, nameof(playerRuntime), "Runtime player references are required for floor initialization.");
        hasRequiredBindings &= RequireBinding(floorDirector, nameof(floorDirector), "Floor runtime initialization requires a floor director.");
        hasRequiredBindings &= RequireBinding(runController, nameof(runController), "Run progression and authored interactables require a run controller.");
        WarnIfMissing(noiseSystem, nameof(noiseSystem));
        WarnIfMissing(audioManager, nameof(audioManager));
        hasRequiredBindings &= RequireBinding(fogOfWarOverlay, nameof(fogOfWarOverlay), "Fog visibility drives discovery and authored-light visibility on these floors.");
        WarnIfMissing(hudCanvas, nameof(hudCanvas));
        WarnIfMissing(inventoryHudBinder, nameof(inventoryHudBinder));
        WarnIfMissing(healthHudBinder, nameof(healthHudBinder));
        WarnIfMissing(threatHudBinder, nameof(threatHudBinder));
        WarnIfMissing(quickSlotsHudBinder, nameof(quickSlotsHudBinder));

        WarnIfOptionalMissing(goalPickup, nameof(goalPickup), "Floor tool pickup authoring is optional for this floor.");
        bool hasMarkerDrivenSupportItems = floorAuthoring != null && floorAuthoring.HasSupportItemPlacementMarkers();
        bool hasMarkerDrivenKey = floorAuthoring != null && floorAuthoring.HasKeyPlacementMarkers();

        if (!hasMarkerDrivenSupportItems)
        {
            WarnIfOptionalMissing(batteryPickup, nameof(batteryPickup), "Battery pickup authoring is optional for this floor.");
            WarnIfOptionalMissing(bottlePickup, nameof(bottlePickup), "Bottle pickup authoring is optional for this floor.");
            WarnIfOptionalMissing(medkitPickup, nameof(medkitPickup), "Medkit pickup authoring is optional for this floor.");
        }

        if (!hasMarkerDrivenKey)
        {
            hasRequiredBindings &= RequireBinding(keyPickup, nameof(keyPickup), "The Iron Gate Key pickup is required to keep the authored route completable.");
        }

        if (usesElevatorPropDirectExit)
        {
            WarnIfOptionalMissing(
                elevatorExitPoint,
                nameof(elevatorExitPoint),
                "This floor can re-resolve the direct elevator exit interactable during post-compose configuration.");
        }
        else if (activeFloorNumber <= 1)
        {
            hasRequiredBindings &= RequireBinding(finalExitPoint, nameof(finalExitPoint), "1F requires the final exit interaction to finish the loop.");
        }
        else if (!usesDirectAuthoredExit)
        {
            hasRequiredBindings &= RequireBinding(keyGatePoint, nameof(keyGatePoint), "Upper authored floors require the iron gate interaction to gate progression.");
            hasRequiredBindings &= RequireBinding(authoredStairsPoint, nameof(authoredStairsPoint), "Upper authored floors require the authored stairs interaction to continue the route.");
        }
        else
        {
            hasRequiredBindings &= RequireBinding(authoredStairsPoint, nameof(authoredStairsPoint), "The direct authored exit route still requires the authored stairs interaction anchor.");
        }

        if (accentBackdrop == null)
        {
            Debug.LogWarning($"{nameof(RSceneCompositionRoot)} is missing an authored accent backdrop. The clean loop will still run, but backdrop tinting is disabled.", this);
        }

        return hasRequiredBindings;
    }

    private RRunSessionController ResolveSessionController()
    {
        if (runSessionController != null)
        {
            return runSessionController;
        }

        RRunSessionController resolvedSessionController = RRunSessionResolver.ResolveForScene(gameObject.scene);

        if (resolvedSessionController != null)
        {
            runSessionController = resolvedSessionController;
            return runSessionController;
        }

        if (!Application.isPlaying)
        {
            return RSceneReferenceLookup.FindUniqueComponentInScene<RRunSessionController>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(runSessionController));
        }

        runSessionController = RRunSessionController.EnsureExistsForRuntime();
        return runSessionController;
    }

    private int ResolveActiveFloorNumber(RRunSessionController sessionController)
    {
        if (sessionController == null)
        {
            return MainEscapeFloorCatalog.GetFloorByIndex(0).FloorNumber;
        }

        if (sessionController.TryResolveFloorNumberForScene(gameObject.scene, out int sceneFloorNumber))
        {
            return sceneFloorNumber;
        }

        int snapshotFloorNumber = sessionController.Snapshot.CurrentFloorNumber;

        if (snapshotFloorNumber > 0)
        {
            return snapshotFloorNumber;
        }

        return sessionController.StartingFloorNumber;
    }

    private bool WarnIfMissing(UnityEngine.Object reference, string label)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogWarning($"{nameof(RSceneCompositionRoot)} is missing an authored reference for '{label}'. The runtime will continue with reduced functionality if possible.", this);
        return false;
    }

    private bool RequireBinding(UnityEngine.Object reference, string label, string reason)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(RSceneCompositionRoot)} is missing required authored reference '{label}'. {reason}", this);
        return false;
    }

    private void WarnIfOptionalMissing(UnityEngine.Object reference, string label, string reason)
    {
        if (reference != null || !RuntimeSettings.LogOptionalAuthoringFallbackWarnings)
        {
            return;
        }

        Debug.LogWarning($"{nameof(RSceneCompositionRoot)} is missing an authored '{label}' reference. {reason}", this);
    }

    private void EnsureRuntimePlayerBindings()
    {
        if (playerController == null)
        {
            playerController = RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>(gameObject.scene, this, nameof(RSceneCompositionRoot), nameof(playerController));
        }

        if (playerController == null)
        {
            playerController = InstantiatePlayerFromCatalog();
        }

        if (playerController == null)
        {
            return;
        }

        playerRuntime = RRuntimePlayerInstaller.PrepareRuntimePlayer(playerController, RuntimePlayerSortingOrder);
        RRuntimeHudBinderSet hudBinders = RRuntimePlayerInstaller.EnsureHudBinders(playerController);
        inventoryHudBinder = hudBinders.InventoryHudBinder;
        healthHudBinder = hudBinders.HealthHudBinder;
        threatHudBinder = hudBinders.ThreatHudBinder;
        quickSlotsHudBinder = hudBinders.QuickSlotsHudBinder;
        staminaHudBinder = hudBinders.StaminaHudBinder;
        CacheBindingReferences();
    }

    private WasdPlayerController InstantiatePlayerFromCatalog()
    {
        Transform parent = gameplayRoot != null ? gameplayRoot : transform;
        return MainEscapePlayerSpawnUtility.SpawnPlayerFromCatalog(
            gameObject.scene,
            parent,
            this,
            nameof(RSceneCompositionRoot),
            destroyExistingPlayers: false);
    }

    private void CacheBindingReferences()
    {
        fogOverlayConsumerBehaviours = RSceneBindingCacheUtility.ResolveBindingBehaviours<IFogOfWarOverlayConsumer>(
            RSceneBindingCacheUtility.FindSceneBindings<IFogOfWarOverlayConsumer>(gameObject.scene));
        debugModeApplierBehaviours = RSceneBindingCacheUtility.ResolveBindingBehaviours<IMainEscapeDebugModeApplier>(
            RSceneBindingCacheUtility.FindSceneBindings<IMainEscapeDebugModeApplier>(gameObject.scene));
        hudBinderBehaviours = RSceneBindingCacheUtility.ResolveBindingBehaviours<IRebuildHudBinder>(
            inventoryHudBinder,
            healthHudBinder,
            threatHudBinder,
            quickSlotsHudBinder,
            staminaHudBinder);
    }

    private void DisableEditorOnlyWorkspaceRoots()
    {
        Transform workspaceRoot = authoringWorkspaceRoot != null
            ? authoringWorkspaceRoot
            : floorAuthoring != null
                ? floorAuthoring.transform.parent
                : null;
        MainEscapeWorkspaceUtility.DisableEditorOnlyWorkspaceRoots(workspaceRoot, RuntimeSettings.EditorOnlyWorkspaceRootNames);
    }

    private void ConfigureDirectExitInteractable()
    {
        if (runController == null)
        {
            return;
        }

        if (!UsesElevatorPropDirectExit())
        {
            if (elevatorExitPoint != null)
            {
                elevatorExitRoot ??= elevatorExitPoint.transform.parent;
                elevatorExitPoint.Configure(null, null, elevatorExitRoot);
            }

            return;
        }

        if (elevatorExitPoint != null)
        {
            elevatorExitRoot ??= elevatorExitPoint.transform.parent;
            elevatorExitPoint.Configure(runController, null, elevatorExitRoot);
            return;
        }

        RDirectExitElevatorReferenceSet elevatorReferences = RAuthoredExitReferenceResolver.ResolveDirectElevatorExit(
            gameObject.scene,
            floorAuthoring,
            elevatorExitPoint,
            elevatorExitVisual,
            elevatorExitRoot,
            DirectExitElevatorVisualName);
        elevatorExitPoint = elevatorReferences.ElevatorExitPoint;
        elevatorExitVisual = elevatorReferences.ElevatorExitVisual;
        elevatorExitRoot = elevatorReferences.ElevatorExitRoot;

        if (elevatorExitVisual == null)
        {
            Debug.LogWarning($"{nameof(RSceneCompositionRoot)} could not find the visual elevator prop needed for direct authored elevator exit interaction.", this);
            return;
        }

        if (!TryResolveExistingElevatorExitPointFromVisual())
        {
            Debug.LogError(
                $"{nameof(RSceneCompositionRoot)} requires an authored {nameof(MainEscapeElevatorExitInteractable)} on the direct elevator exit visual. Add it in the scene or assign {nameof(elevatorExitPoint)} before runtime composition.",
                this);
            return;
        }

        elevatorExitRoot ??= elevatorExitPoint.transform.parent;
        elevatorExitPoint.Configure(runController, elevatorExitVisual, elevatorExitRoot);
    }

    private bool TryResolveExistingElevatorExitPointFromVisual()
    {
        if (elevatorExitPoint != null)
        {
            return true;
        }

        if (elevatorExitVisual == null)
        {
            return false;
        }

        elevatorExitPoint = elevatorExitVisual.GetComponent<MainEscapeElevatorExitInteractable>();
        return elevatorExitPoint != null;
    }

    private bool UsesElevatorPropDirectExit()
    {
        return UsesElevatorPropDirectExit(ResolveBoundFloorNumber());
    }

    private bool UsesElevatorPropDirectExit(int activeFloorNumber)
    {
        return runController != null
            && runController.UsesDirectAuthoredExitInteraction
            && RDirectExitRouteUtility.UsesElevatorPropDirectExit(activeFloorNumber)
            && IsManagedGameplaySceneForRouting();
    }

    private bool IsManagedGameplaySceneForRouting()
    {
        return RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(gameObject.scene, runSessionController);
    }

    private int ResolveBoundFloorNumber()
    {
        if (runSessionController != null)
        {
            int resolvedFloorNumber = ResolveActiveFloorNumber(runSessionController);

            if (resolvedFloorNumber > 0)
            {
                return resolvedFloorNumber;
            }
        }

        if (runController != null)
        {
            int authoredFloorNumber = runController.CurrentFloorNumber;

            if (authoredFloorNumber > 0)
            {
                return authoredFloorNumber;
            }
        }

        return MainEscapeFloorCatalog.GetFloorByIndex(0).FloorNumber;
    }
}

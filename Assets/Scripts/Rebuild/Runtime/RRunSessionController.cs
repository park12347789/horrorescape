using System;
using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

[Serializable]
public struct RRunSavedInventoryItem
{
    public string itemId;
    public string displayName;
    public int quantity;
}

[Serializable]
public struct RRunSavedPlayerState
{
    public bool hasState;
    public int playerHealth;
    public float flashlightChargeNormalized;
    public bool flashlightEnabled;
    public RRunSavedInventoryItem[] inventoryItems;
}

// Legacy compatibility shim. Real capture/restore now lives in RRunPlayerStateStoreUtility
// so the session controller and standalone store share the same owner-based path.
public static class RRunPlayerStatePersistence
{
    public static RRunSavedPlayerState Capture(RPlayerRuntimeReferences runtime, RRunSavedPlayerState previousState)
    {
        return RRunPlayerStateStoreUtility
            .Capture(runtime, RRunPlayerStateSnapshot.FromLegacy(previousState))
            .ToLegacyState();
    }

    public static bool TryRestore(RPlayerRuntimeReferences runtime, RRunSavedPlayerState primaryState, RRunSavedPlayerState fallbackState)
    {
        return RRunPlayerStateStoreUtility.TryRestore(
            runtime,
            RRunPlayerStateSnapshot.FromLegacy(primaryState),
            RRunPlayerStateSnapshot.FromLegacy(fallbackState));
    }

    public static RRunSavedPlayerState Clear()
    {
        return RRunPlayerStateSnapshot.CreateDefault().ToLegacyState();
    }

    public static RRunSavedPlayerState Clone(RRunSavedPlayerState source)
    {
        return RRunPlayerStateSnapshot.FromLegacy(source).ToLegacyState();
    }
}

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed partial class RRunSessionController : MonoBehaviour
{
    private static MainEscapeRuntimeSettings RuntimeSettings => MainEscapeRuntimeSettings.Load();
    private const int MaxGameplaySceneCompositionWaitFrames = 8;
    private static RRunSessionController instance;

    [Header("Scene Routing")]
    [SerializeField] private string lobbyScenePath = MainEscapeSceneIdentityUtility.GetCanonicalLobbyScenePath();
    [SerializeField] private string tutorialScenePath = MainEscapeSceneIdentityUtility.GetCanonicalTutorialScenePath();
    [SerializeField] private string elevatorTransitionScenePath = MainEscapeSceneIdentityUtility.GetCanonicalElevatorTransitionScenePath();
    [SerializeField, Min(1)] private int startingFloorNumber = MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber();
    [SerializeField] private RFloorSceneEntry[] floorScenes = MainEscapeSceneIdentityUtility.GetCanonicalFloorSceneEntries();
    [SerializeField, Tooltip("Legacy authoring flag. Runtime routing now prefers this component's serialized scene-local routes whenever they are valid.")]
    private bool useSceneLocalRoutingOverrides = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool capturePlayerStateContinuously;
    [SerializeField] private RRunRoutingSettings routingSettings;
    [SerializeField] private ChapterDefinition activeChapter;
    [SerializeField] private RRunPlayerDefaults playerDefaults;

    [Header("Run State")]
    [SerializeField] private bool runStarted;
    [SerializeField] private bool hasActiveRun;
    [SerializeField] private int currentFloorNumber;
    [SerializeField] private int floorsCleared;
    [SerializeField] private RRunOutcome outcome;
    [SerializeField] private string failureSource = string.Empty;
    [SerializeField, Min(0f)] private float elapsedRunSeconds;
    [SerializeField] private bool hasRunClockStart;
    [SerializeField] private float runClockStartTime;
    [SerializeField] private bool hasSavedPlayerState;
    [SerializeField] private int savedPlayerHealth = 3;
    [SerializeField] private float savedFlashlightChargeNormalized = 1f;
    [SerializeField] private bool savedFlashlightEnabled;
    [SerializeField] private RRunSavedInventoryItem[] savedInventoryItems = Array.Empty<RRunSavedInventoryItem>();
    private RPlayerRuntimeReferences boundPlayerRuntime;
    private Coroutine pendingRestoreCoroutine;

    public static bool HasInstance => instance != null;
    [System.Obsolete("Legacy compatibility bridge. Prefer RRunSessionResolver or TryGetCachedInstance.", false)]
    public static RRunSessionController Instance => instance;

    public static bool TryGetCachedInstance(out RRunSessionController sessionController)
    {
        sessionController = instance;
        return sessionController != null;
    }

    public static RRunSessionController EnsureExistsForRuntime()
    {
        if (instance != null)
        {
            return instance;
        }

        if (!Application.isPlaying)
        {
            return null;
        }

        GameObject sessionObject = new(nameof(RRunSessionController));
        return sessionObject.AddComponent<RRunSessionController>();
    }

    public string LobbyScenePath => GetConfiguredLobbyScenePath();
    public string TutorialScenePath => GetConfiguredTutorialScenePath();
    public string ElevatorTransitionScenePath => GetConfiguredElevatorTransitionScenePath();
    public string GameplayScenePath => GetStartFloorScenePath();
    public int StartingFloorNumber => GetConfiguredStartingFloorNumber();
    public int TerminalFloorNumber => RRunSceneConfigurationUtility.ResolveTerminalFloorNumber(StartingFloorNumber, GetConfiguredFloorScenes());
    public int RouteFloorCount => RRunSceneConfigurationUtility.ResolveRouteFloorCount(StartingFloorNumber, GetConfiguredFloorScenes());
    public RRunSnapshot Snapshot
    {
        get
        {
            SyncRefactorSkeletonSessionState();
            return sessionState.ToSnapshot();
        }
    }
    public float ElapsedRunSeconds => GetElapsedRunSeconds();

    public event Action<RRunSnapshot> SnapshotChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            instance.AdoptSceneRoutingConfigurationFrom(this);
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = nameof(RRunSessionController);

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyConfiguredPerformanceSettings();
        ValidateConfiguration();
    }

    private void Start()
    {
        HandleLoadedScenesAtStartup();
    }

    private void HandleLoadedScenesAtStartup()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            HandleSceneLoaded(scene, LoadSceneMode.Single);
        }
    }

    private void Reset()
    {
        AssignCanonicalRunAssetsIfMissing();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        AssignCanonicalRunAssetsIfMissing();
        ValidateConfiguration();
    }

    [ContextMenu("Use Canonical Run Assets")]
    private void UseCanonicalRunAssets()
    {
        AssignCanonicalRunAssets(forceAssign: true);
        ValidateConfiguration();
    }

    private void LateUpdate()
    {
        if (!capturePlayerStateContinuously || !hasActiveRun)
        {
            return;
        }

        if (pendingRestoreCoroutine != null)
        {
            return;
        }

        if (boundPlayerRuntime == null)
        {
            return;
        }

        CapturePlayerState(boundPlayerRuntime);
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    public void BeginNewRun()
    {
        RunSessionStateTransitionUtility.BeginNewRun(
            ref runStarted,
            ref hasActiveRun,
            ref currentFloorNumber,
            ref floorsCleared,
            ref outcome,
            ref failureSource,
            StartingFloorNumber,
            RRunOutcome.InProgress);
        RestartRunClock();
        ResetRunRandomizationForNewRun();
        ClearSavedPlayerState();
        PublishSnapshot();
    }

    public void SelectChapter(ChapterDefinition chapterDefinition)
    {
        activeChapter = chapterDefinition != null && chapterDefinition.IsValid
            ? chapterDefinition
            : null;
    }

    public void StartChapterAndLoadGameplay(ChapterDefinition chapterDefinition)
    {
        SelectChapter(chapterDefinition != null ? chapterDefinition : GetDefaultChapterDefinition());
        BeginNewRun();
        RSceneRouter.LoadCurrentFloorScene(this);
    }

    public void StartNewRunAndLoadGameplay()
    {
        StartChapterAndLoadGameplay(GetDefaultChapterDefinition());
    }

    public void LoadTutorialScene()
    {
        SelectChapter(GetCurrentChapterDefinition());
        StopPendingRestore();
        PublishSnapshot();
        RSceneRouter.LoadTutorialScene(this);
    }

    public void StartNewRunFromTutorialAndLoadStartFloor()
    {
        BeginNewRun();
        int destinationFloorNumber = StartingFloorNumber;

        if (RSceneRouter.LoadFloorSceneThroughElevatorTransition(
                this,
                destinationFloorNumber + 1,
                destinationFloorNumber))
        {
            return;
        }

        RSceneRouter.LoadCurrentFloorScene(this);
    }

    public void RetryCurrentRun()
    {
        BeginNewRun();
        RSceneRouter.ReloadCurrentFloorScene(this);
    }

    public void ReturnToLobby()
    {
        PublishSnapshot();
        RSceneRouter.LoadLobbyScene(this);
    }

    public void EnsureGameplayRunInitialized()
    {
        EnsureGameplayRunInitialized(StartingFloorNumber);
    }

    private void EnsureGameplayRunInitialized(int initializationFloorNumber)
    {
        bool shouldRestartRunClock = !runStarted
            || outcome == RRunOutcome.Cleared
            || outcome == RRunOutcome.Failed;

        RunSessionStateTransitionUtility.EnsureGameplayRunInitialized(
            ref runStarted,
            ref hasActiveRun,
            ref currentFloorNumber,
            ref floorsCleared,
            ref outcome,
            ref failureSource,
            Mathf.Max(1, initializationFloorNumber),
            RRunOutcome.InProgress,
            static value => value == RRunOutcome.Cleared || value == RRunOutcome.Failed,
            static value => value == RRunOutcome.None);

        if (shouldRestartRunClock)
        {
            RestartRunClock();
        }
        else
        {
            EnsureRunClockStarted();
        }

        PublishSnapshot();
    }

    public void RecordFloorClear(int clearedFloorNumber, int destinationFloorNumber)
    {
        CaptureBoundPlayerState();
        CaptureElapsedRunTime();
        int configuredStartingFloorNumber = StartingFloorNumber;
        RFloorSceneEntry[] configuredScenes = GetConfiguredFloorScenes();
        RunSessionStateTransitionUtility.RecordFloorClear(
            ref runStarted,
            ref hasActiveRun,
            ref currentFloorNumber,
            ref floorsCleared,
            ref outcome,
            ref failureSource,
            clearedFloorNumber,
            destinationFloorNumber,
            RRunOutcome.InProgress,
            (existingFloorsCleared, destination) => Mathf.Max(
                existingFloorsCleared,
                RRunSceneConfigurationUtility.ResolveClearedFloorCountForDestination(
                    destination,
                    configuredStartingFloorNumber,
                    configuredScenes)),
            static clearedFloor => $"{clearedFloor}F cleared");
        PublishSnapshot();
    }

    public bool TryGetScenePathForFloor(int floorNumber, out string scenePath)
    {
        if (RRunSceneRouteFloorResolver.TryResolveScenePathForFloor(
                StartingFloorNumber,
                GetConfiguredFloorScenes(),
                floorNumber,
                out scenePath))
        {
            return true;
        }

        RFloorSceneEntry[] configuredScenes = GetConfiguredFloorScenes();

        for (int index = 0; index < configuredScenes.Length; index++)
        {
            RFloorSceneEntry entry = configuredScenes[index];

            if (entry.floorNumber != Mathf.Max(1, floorNumber) || string.IsNullOrWhiteSpace(entry.scenePath))
            {
                continue;
            }

            scenePath = entry.scenePath;
            return true;
        }

        scenePath = string.Empty;
        return false;
    }

    public string GetScenePathForFloor(int floorNumber)
    {
        return TryGetScenePathForFloor(floorNumber, out string scenePath) ? scenePath : string.Empty;
    }

    public string GetCurrentFloorScenePath()
    {
        int floorNumber = currentFloorNumber > 0 ? currentFloorNumber : StartingFloorNumber;
        return GetScenePathForFloor(floorNumber);
    }

    public string GetStartFloorScenePath()
    {
        return GetScenePathForFloor(StartingFloorNumber);
    }

    public int ResolveNextFloorNumber(int currentFloorNumber)
    {
        if (TryResolveRouteExitFloorNumber(Mathf.Max(1, currentFloorNumber), out int graphDestinationFloorNumber))
        {
            return graphDestinationFloorNumber;
        }

        return RRunSceneConfigurationUtility.ResolveNextFloorNumber(Mathf.Max(1, currentFloorNumber), GetConfiguredFloorScenes());
    }

    public bool IsTerminalFloor(int floorNumber)
    {
        return Mathf.Max(1, floorNumber) == TerminalFloorNumber;
    }

    public bool IsGameplayScene(Scene scene)
    {
        return MainEscapeSceneRouteResolver.IsManagedGameplayScene(RuntimeSettings, GetConfiguredFloorScenes(), scene);
    }

    public bool TryResolveFloorNumberForScene(Scene scene, out int floorNumber)
    {
        if (RRunSceneRouteFloorResolver.TryResolveFloorNumberForScene(
                StartingFloorNumber,
                GetConfiguredFloorScenes(),
                scene,
                out floorNumber))
        {
            return true;
        }

        return MainEscapeSceneRouteResolver.TryResolveFloorNumber(RuntimeSettings, GetConfiguredFloorScenes(), scene, out floorNumber);
    }

    public void RecordFinalClear(int clearedFloorNumber)
    {
        CaptureBoundPlayerState();
        CaptureElapsedRunTime();
        int totalRouteFloorCount = RouteFloorCount;
        RunSessionStateTransitionUtility.RecordFinalClear(
            ref runStarted,
            ref hasActiveRun,
            ref currentFloorNumber,
            ref floorsCleared,
            ref outcome,
            ref failureSource,
            clearedFloorNumber,
            RRunOutcome.Cleared,
            (existingFloorsCleared, __) => Mathf.Max(existingFloorsCleared, totalRouteFloorCount));
        StopRunClock();
        PublishSnapshot();
    }

    public void RecordFailure(int floorNumber, string sourceName)
    {
        CaptureBoundPlayerState();
        CaptureElapsedRunTime();
        RunSessionStateTransitionUtility.RecordFailure(
            ref runStarted,
            ref hasActiveRun,
            ref currentFloorNumber,
            ref outcome,
            ref failureSource,
            floorNumber,
            RRunOutcome.Failed,
            sourceName,
            "Enemy");
        StopRunClock();
        PublishSnapshot();
    }

    public float GetElapsedRunSeconds()
    {
        if (Application.isPlaying && hasRunClockStart && hasActiveRun)
        {
            return Mathf.Max(0f, Time.time - runClockStartTime);
        }

        return Mathf.Max(0f, elapsedRunSeconds);
    }

    public static string FormatElapsedRunTime(float seconds)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));

        if (elapsed.TotalHours >= 1d)
        {
            return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }

        return $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        ApplyConfiguredPerformanceSettings();

        if (IsGameplayScene(scene))
        {
            int initializationFloorNumber = StartingFloorNumber;

            if (TryResolveFloorNumberForScene(scene, out int loadedFloorNumber))
            {
                currentFloorNumber = Mathf.Max(1, loadedFloorNumber);
                initializationFloorNumber = currentFloorNumber;
            }

            EnsureGameplayRunInitialized(initializationFloorNumber);
            StopPendingRestore();
            pendingRestoreCoroutine = StartCoroutine(RestoreGameplayStateWhenReady(scene));
            return;
        }

        if (RSceneRouter.SceneMatches(scene, GetConfiguredLobbyScenePath()))
        {
            PublishSnapshot();
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(GetConfiguredLobbyScenePath()))
        {
            Debug.LogError("RRunSessionController requires a lobby scene path.", this);
        }

        if (string.IsNullOrWhiteSpace(GetConfiguredElevatorTransitionScenePath()))
        {
            Debug.LogError("RRunSessionController requires an elevator transition scene path.", this);
        }

        if (string.IsNullOrWhiteSpace(GetStartFloorScenePath()))
        {
            Debug.LogError("RRunSessionController requires a configured start-floor scene path.", this);
        }

        if (!TryGetScenePathForFloor(StartingFloorNumber, out _))
        {
            Debug.LogError($"RRunSessionController requires a floor-scene entry for the starting floor {StartingFloorNumber}F.", this);
        }
    }

    private static void ApplyConfiguredPerformanceSettings()
    {
        RLobbyRuntimeOptionsSnapshot snapshot = RLobbyRuntimeOptions.Load(runtimeSettings: RuntimeSettings);
        RLobbyRuntimeOptions.ApplyPerformanceToRuntime(snapshot);
    }

    public void PrepareHiddenElevatorTransitionWindow()
    {
        ApplyConfiguredPerformanceSettings();
        EnsureRunRandomizationSeedInitialized();
    }

    private void RestartRunClock()
    {
        elapsedRunSeconds = 0f;

        if (!Application.isPlaying)
        {
            hasRunClockStart = false;
            runClockStartTime = 0f;
            return;
        }

        hasRunClockStart = true;
        runClockStartTime = Time.time;
    }

    private void EnsureRunClockStarted()
    {
        if (!Application.isPlaying || hasRunClockStart || !hasActiveRun)
        {
            return;
        }

        hasRunClockStart = true;
        runClockStartTime = Time.time - Mathf.Max(0f, elapsedRunSeconds);
    }

    private void CaptureElapsedRunTime()
    {
        elapsedRunSeconds = GetElapsedRunSeconds();
    }

    private void StopRunClock()
    {
        hasRunClockStart = false;
    }

    private void AssignCanonicalRunAssetsIfMissing()
    {
        AssignCanonicalRunAssets(forceAssign: false);
    }

    private void AssignCanonicalRunAssets(bool forceAssign)
    {
        if (forceAssign || routingSettings == null)
        {
            routingSettings = RRunCanonicalAssetLocator.TryLoadRoutingSettings(this);
        }

        if (forceAssign || playerDefaults == null)
        {
            playerDefaults = RRunCanonicalAssetLocator.TryLoadPlayerDefaults(this);
        }
    }

    private void AdoptSceneRoutingConfigurationFrom(RRunSessionController source)
    {
        if (source == null)
        {
            return;
        }

        lobbyScenePath = source.lobbyScenePath;
        tutorialScenePath = source.tutorialScenePath;
        elevatorTransitionScenePath = source.elevatorTransitionScenePath;
        startingFloorNumber = source.startingFloorNumber;
        useSceneLocalRoutingOverrides = source.useSceneLocalRoutingOverrides;
        capturePlayerStateContinuously = source.capturePlayerStateContinuously;
        routingSettings = source.routingSettings;
        activeChapter = source.activeChapter;
        playerDefaults = source.playerDefaults;
        persistAcrossScenes = source.persistAcrossScenes;
        floorScenes = CloneFloorScenes(source.floorScenes);
        ValidateConfiguration();
    }

    private static RFloorSceneEntry[] CloneFloorScenes(RFloorSceneEntry[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<RFloorSceneEntry>();
        }

        RFloorSceneEntry[] clone = new RFloorSceneEntry[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    private void ClearSavedPlayerState()
    {
        ApplySavedPlayerState(CreateDefault().ToLegacyState());
    }

    public void BindGameplayRuntime(
        RPlayerRuntimeReferences runtime,
        IRunStateController runStateController,
        bool restorePlayerStateImmediately = true)
    {
        if (runtime == null)
        {
            return;
        }

        if (restorePlayerStateImmediately)
        {
            StopPendingRestore();
        }

        ApplyRunStateBinding(runtime, runStateController);
        boundPlayerRuntime = runtime;

        if (restorePlayerStateImmediately)
        {
            RestorePlayerState(runtime);
        }
    }

    public void UnbindGameplayRuntime(RPlayerRuntimeReferences runtime)
    {
        if (runtime == null || boundPlayerRuntime != runtime)
        {
            return;
        }

        StopPendingRestore();
        ApplyRunStateBinding(boundPlayerRuntime, null);
        boundPlayerRuntime = null;
    }

    public void BindPlayerRuntime(RPlayerRuntimeReferences runtime)
    {
        if (runtime == null)
        {
            return;
        }

        StopPendingRestore();
        boundPlayerRuntime = runtime;
        CapturePlayerState(runtime);
    }

    public void UnbindPlayerRuntime(RPlayerRuntimeReferences runtime)
    {
        UnbindGameplayRuntime(runtime);
    }

    public void CaptureBoundPlayerState(RPlayerRuntimeReferences fallbackRuntime = null)
    {
        RPlayerRuntimeReferences runtime = boundPlayerRuntime != null ? boundPlayerRuntime : fallbackRuntime;

        if (runtime == null)
        {
            return;
        }

        CapturePlayerState(runtime);
    }

    public void CapturePlayerState(RPlayerRuntimeReferences runtime)
    {
        if (runtime == null)
        {
            return;
        }

        RRunSavedPlayerState capturedState = ResolvePlayerStateStore()
            .Capture(runtime, playerStateSnapshot)
            .ToLegacyState();
        ApplySavedPlayerState(capturedState);
        boundPlayerRuntime = runtime;
    }

    public void RestorePlayerState(RPlayerRuntimeReferences runtime)
    {
        if (runtime == null)
        {
            return;
        }

        RRunSavedPlayerState primaryState = GetSavedPlayerState();

        if (!primaryState.hasState)
        {
            RestoreFreshPlayerState(runtime);
            ApplyCurrentFloorPlayerDefaults(runtime, GetConfiguredFlashlightEnabledByDefault());
            CapturePlayerState(runtime);
            return;
        }

        bool restoreSucceeded = ResolvePlayerStateStore().TryRestore(
            runtime,
            RRunPlayerStateSnapshot.FromLegacy(primaryState));

        if (!restoreSucceeded)
        {
            CapturePlayerState(runtime);
            return;
        }

        boundPlayerRuntime = runtime;
    }

    private IEnumerator RestoreGameplayStateWhenReady(Scene scene)
    {
        yield return null;

        int waitedFrames = 0;

        while (waitedFrames < MaxGameplaySceneCompositionWaitFrames && ShouldWaitForGameplaySceneComposition(scene))
        {
            waitedFrames++;
            yield return null;
        }

        if (boundPlayerRuntime == null)
        {
            boundPlayerRuntime = RSceneReferenceLookup.FindFirstComponentInScene<RPlayerRuntimeReferences>(scene);
        }

        if (boundPlayerRuntime != null)
        {
            RestorePlayerState(boundPlayerRuntime);
        }

        pendingRestoreCoroutine = null;
    }

    private static bool ShouldWaitForGameplaySceneComposition(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        RSceneCompositionRoot[] compositionRoots = RSceneReferenceLookup.FindComponentsInScene<RSceneCompositionRoot>(scene);

        for (int index = 0; index < compositionRoots.Length; index++)
        {
            RSceneCompositionRoot compositionRoot = compositionRoots[index];

            if (compositionRoot == null
                || compositionRoot.gameObject.scene != scene
                || !compositionRoot.ExpectsRuntimeCompositionOnPlay)
            {
                continue;
            }

            if (!compositionRoot.IsRuntimeCompositionReady)
            {
                return true;
            }
        }

        return false;
    }

    private void StopPendingRestore()
    {
        if (pendingRestoreCoroutine == null)
        {
            return;
        }

        StopCoroutine(pendingRestoreCoroutine);
        pendingRestoreCoroutine = null;
    }

    private void RestoreFreshPlayerState(RPlayerRuntimeReferences runtime)
    {
        if (runtime == null)
        {
            return;
        }

        PlayerInventory inventory = runtime.Inventory;

        if (inventory != null)
        {
            inventory.SetItems(BuildConfiguredStartingInventory());
        }

        if (runtime.PlayerHealth != null)
        {
            int defaultHealth = playerDefaults != null
                ? Mathf.Clamp(playerDefaults.DefaultHealth, 1, runtime.PlayerHealth.MaxHealth)
                : runtime.PlayerHealth.MaxHealth;
            runtime.PlayerHealth.SetCurrentHealth(defaultHealth);
        }

        RFlashlightStateOwnerUtility.TryApplyConfiguredCharge(runtime, GetConfiguredFlashlightChargeNormalized());
    }

    private bool ApplyCurrentFloorPlayerDefaults(RPlayerRuntimeReferences runtime, bool enableFlashlightByDefault)
    {
        if (runtime == null)
        {
            return false;
        }

        int activeFloorNumber = currentFloorNumber > 0 ? currentFloorNumber : StartingFloorNumber;
        bool stateChanged = false;

        if (activeFloorNumber <= 4)
        {
            stateChanged |= RFlashlightStateOwnerUtility.TryApplyConfiguredEnabledState(runtime, enableFlashlightByDefault);
        }

        return stateChanged;
    }

    private static void ApplyRunStateBinding(RPlayerRuntimeReferences runtime, IRunStateController runStateController)
    {
        if (runtime == null)
        {
            return;
        }

        runtime.PlayerHealth?.BindRunController(runStateController);
        runtime.CaughtState?.BindRunController(runStateController);
    }

    private RRunSavedPlayerState GetSavedPlayerState()
    {
        return new RRunSavedPlayerState
        {
            hasState = hasSavedPlayerState,
            playerHealth = savedPlayerHealth,
            flashlightChargeNormalized = savedFlashlightChargeNormalized,
            flashlightEnabled = savedFlashlightEnabled,
            inventoryItems = savedInventoryItems ?? Array.Empty<RRunSavedInventoryItem>()
        };
    }

    private void ApplySavedPlayerState(RRunSavedPlayerState state)
    {
        hasSavedPlayerState = state.hasState;
        savedPlayerHealth = state.playerHealth;
        savedFlashlightChargeNormalized = state.flashlightChargeNormalized;
        savedFlashlightEnabled = state.flashlightEnabled;
        savedInventoryItems = state.inventoryItems ?? Array.Empty<RRunSavedInventoryItem>();
        SyncRefactorSkeletonPlayerState(state);
    }

    private void PublishSnapshot()
    {
        SyncRefactorSkeletonSessionState();
        SnapshotChanged?.Invoke(Snapshot);
    }

    private string GetConfiguredLobbyScenePath()
    {
        return RRunSceneConfigurationUtility.ResolveLobbyScenePath(
            RuntimeSettings,
            lobbyScenePath,
            preferSceneLocal: true);
    }

    private string GetConfiguredTutorialScenePath()
    {
        if (!string.IsNullOrWhiteSpace(tutorialScenePath))
        {
            return tutorialScenePath.Trim();
        }

        if (routingSettings != null && !string.IsNullOrWhiteSpace(routingSettings.TutorialScenePath))
        {
            return routingSettings.TutorialScenePath.Trim();
        }

        return tutorialScenePath?.Trim() ?? string.Empty;
    }

    private string GetConfiguredElevatorTransitionScenePath()
    {
        if (!string.IsNullOrWhiteSpace(elevatorTransitionScenePath))
        {
            return elevatorTransitionScenePath.Trim();
        }

        if (routingSettings != null && !string.IsNullOrWhiteSpace(routingSettings.ElevatorTransitionScenePath))
        {
            return routingSettings.ElevatorTransitionScenePath.Trim();
        }

        return elevatorTransitionScenePath?.Trim() ?? string.Empty;
    }

    private int GetConfiguredStartingFloorNumber()
    {
        if (RouteGraphFloorRouteAdapter.TryResolveStartingFloorNumber(
                GetCurrentChapterDefinition(),
                out int routeGraphStartingFloorNumber))
        {
            return routeGraphStartingFloorNumber;
        }

        return RRunSceneConfigurationUtility.ResolveStartingFloorNumber(
            RuntimeSettings,
            startingFloorNumber,
            preferSceneLocal: true);
    }

    private RFloorSceneEntry[] GetConfiguredFloorScenes()
    {
        if (RouteGraphFloorRouteAdapter.TryBuildFloorSceneEntries(
                GetCurrentChapterDefinition(),
                out RFloorSceneEntry[] routeGraphFloorScenes))
        {
            return routeGraphFloorScenes;
        }

        return RRunSceneConfigurationUtility.ResolveFloorScenes(
            RuntimeSettings,
            floorScenes,
            preferSceneLocal: true);
    }

    private ChapterDefinition GetCurrentChapterDefinition()
    {
        return activeChapter != null && activeChapter.IsValid
            ? activeChapter
            : GetDefaultChapterDefinition();
    }

    private ChapterDefinition GetDefaultChapterDefinition()
    {
        return routingSettings != null ? routingSettings.DefaultChapter : null;
    }

    private float GetConfiguredFlashlightChargeNormalized()
    {
        return playerDefaults != null
            ? Mathf.Clamp01(playerDefaults.DefaultFlashlightChargeNormalized)
            : 1f;
    }

    private bool GetConfiguredFlashlightEnabledByDefault()
    {
        return playerDefaults == null || playerDefaults.FlashlightEnabledByDefault;
    }

    private PlayerInventory.ItemStack[] BuildConfiguredStartingInventory()
    {
        if (playerDefaults == null || playerDefaults.StartingItems.Length == 0)
        {
            return Array.Empty<PlayerInventory.ItemStack>();
        }

        PlayerInventory.ItemStack[] configuredItems = new PlayerInventory.ItemStack[playerDefaults.StartingItems.Length];

        for (int index = 0; index < playerDefaults.StartingItems.Length; index++)
        {
            RRunPlayerDefaults.StartingInventoryItem item = playerDefaults.StartingItems[index];

            configuredItems[index] = new PlayerInventory.ItemStack
            {
                itemId = item.itemId,
                displayName = item.displayName,
                quantity = Mathf.Max(0, item.quantity)
            };
        }

        return configuredItems;
    }
}

public readonly struct RLobbyRuntimeOptionsSnapshot
{
    public RLobbyRuntimeOptionsSnapshot(
        float masterVolume,
        float sfxVolume,
        float ambienceVolume,
        int targetFrameRate,
        bool vSyncEnabled)
    {
        MasterVolume = Mathf.Clamp01(masterVolume);
        SfxVolume = Mathf.Clamp01(sfxVolume);
        AmbienceVolume = Mathf.Clamp01(ambienceVolume);
        TargetFrameRate = targetFrameRate < 0 ? -1 : targetFrameRate;
        VSyncEnabled = vSyncEnabled;
    }

    public float MasterVolume { get; }
    public float SfxVolume { get; }
    public float AmbienceVolume { get; }
    public int TargetFrameRate { get; }
    public bool VSyncEnabled { get; }
}

public static class RLobbyRuntimeOptions
{
    private const string Prefix = "IRLobby.";
    public const string MasterVolumeKey = Prefix + "MasterVolume";
    public const string SfxVolumeKey = Prefix + "SfxVolume";
    public const string AmbienceVolumeKey = Prefix + "AmbienceVolume";
    public const string TargetFrameRateKey = Prefix + "TargetFrameRate";
    public const string VSyncEnabledKey = Prefix + "VSyncEnabled";

    private const float DefaultMasterVolume = 0.92f;
    private const float DefaultSfxVolume = 1f;
    private const float DefaultAmbienceVolume = 0.05f;
    private const int DefaultTargetFrameRate = 60;

    public static RLobbyRuntimeOptionsSnapshot Load(
        PrototypeAudioManager audioManager = null,
        MainEscapeRuntimeSettings runtimeSettings = null)
    {
        PrototypeAudioManager resolvedAudioManager = audioManager;

        if (resolvedAudioManager == null)
        {
            _ = PrototypeAudioManager.TryGetCachedInstance(out resolvedAudioManager);
        }

        MainEscapeRuntimeSettings resolvedRuntimeSettings = runtimeSettings ?? TryLoadRuntimeSettings();

        float defaultMasterVolume = resolvedAudioManager != null ? resolvedAudioManager.MasterVolume : DefaultMasterVolume;
        float defaultSfxVolume = resolvedAudioManager != null ? resolvedAudioManager.SfxVolume : DefaultSfxVolume;
        float defaultAmbienceVolume = resolvedAudioManager != null ? resolvedAudioManager.AmbienceVolume : DefaultAmbienceVolume;
        int defaultFrameRate = resolvedRuntimeSettings != null ? resolvedRuntimeSettings.TargetFrameRate : DefaultTargetFrameRate;
        bool defaultVSyncEnabled = resolvedRuntimeSettings != null && !resolvedRuntimeSettings.DisableVSyncForTargetFrameRate;

        return new RLobbyRuntimeOptionsSnapshot(
            PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume),
            PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume),
            PlayerPrefs.GetFloat(AmbienceVolumeKey, defaultAmbienceVolume),
            PlayerPrefs.GetInt(TargetFrameRateKey, defaultFrameRate),
            PlayerPrefs.GetInt(VSyncEnabledKey, defaultVSyncEnabled ? 1 : 0) == 1);
    }

    public static void Save(RLobbyRuntimeOptionsSnapshot snapshot, bool flushToDisk)
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, snapshot.MasterVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, snapshot.SfxVolume);
        PlayerPrefs.SetFloat(AmbienceVolumeKey, snapshot.AmbienceVolume);
        PlayerPrefs.SetInt(TargetFrameRateKey, snapshot.TargetFrameRate);
        PlayerPrefs.SetInt(VSyncEnabledKey, snapshot.VSyncEnabled ? 1 : 0);

        if (flushToDisk)
        {
            PlayerPrefs.Save();
        }
    }

    public static void ApplyPerformanceToRuntime(RLobbyRuntimeOptionsSnapshot snapshot)
    {
        QualitySettings.vSyncCount = snapshot.VSyncEnabled ? 1 : 0;
        Application.targetFrameRate = snapshot.TargetFrameRate;
        OnDemandRendering.renderFrameInterval = 1;
    }

    public static void ApplyAudioToRuntime(RLobbyRuntimeOptionsSnapshot snapshot, PrototypeAudioManager audioManager)
    {
        if (audioManager == null)
        {
            return;
        }

        audioManager.SetRuntimeMixVolumes(
            snapshot.MasterVolume,
            snapshot.SfxVolume,
            snapshot.AmbienceVolume);
    }

    public static void ApplyAllToRuntime(
        RLobbyRuntimeOptionsSnapshot snapshot,
        PrototypeAudioManager audioManager)
    {
        ApplyAudioToRuntime(snapshot, audioManager);
        ApplyPerformanceToRuntime(snapshot);
    }

    private static MainEscapeRuntimeSettings TryLoadRuntimeSettings()
    {
        try
        {
            return MainEscapeRuntimeSettings.Load();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

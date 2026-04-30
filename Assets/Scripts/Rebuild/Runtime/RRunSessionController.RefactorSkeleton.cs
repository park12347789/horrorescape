using System;

using UnityEngine;

public sealed partial class RRunSessionController
{
    private sealed class FallbackRunPlayerStateStore : IRunPlayerStateStore
    {
        public RRunPlayerStateSnapshot CreateDefault()
        {
            return RRunPlayerStateStoreUtility.CreateDefault();
        }

        public RRunPlayerStateSnapshot Capture(RPlayerRuntimeReferences runtime, RRunPlayerStateSnapshot previousSnapshot = null)
        {
            return RRunPlayerStateStoreUtility.Capture(runtime, previousSnapshot);
        }

        public bool TryRestore(
            RPlayerRuntimeReferences runtime,
            RRunPlayerStateSnapshot primarySnapshot,
            RRunPlayerStateSnapshot fallbackSnapshot = null)
        {
            return RRunPlayerStateStoreUtility.TryRestore(runtime, primarySnapshot, fallbackSnapshot);
        }
    }

    [Header("Refactor Skeleton")]
    [SerializeField] private RRunSessionState sessionState = new();
    [SerializeField] private RRunPlayerStateSnapshot playerStateSnapshot = new();
    [SerializeField] private MonoBehaviour playerStateStoreSource;
    [Header("Run Randomization")]
    [SerializeField] private bool randomizeGroundEnemyPlacements = true;
    [SerializeField] private bool useFixedRunSeedForDebug;
    [SerializeField] private int fixedRunSeed = 12345;
    [SerializeField] private bool hasRunRandomizationSeed;
    [SerializeField] private int runRandomizationSeed;

    private IRunPlayerStateStore playerStateStore;
    private static readonly IRunPlayerStateStore FallbackPlayerStateStore = new FallbackRunPlayerStateStore();

    public RRunSessionState SessionStateData => sessionState;
    public RRunPlayerStateSnapshot PlayerStateSnapshot => playerStateSnapshot;
    public bool RandomizeGroundEnemyPlacements => randomizeGroundEnemyPlacements;
    public int RunRandomizationSeed => EnsureRunRandomizationSeedInitialized();

    public RRunPlayerStateSnapshot CreateDefault()
    {
        return RRunPlayerStateStoreUtility.CreateDefault();
    }

    public RRunPlayerStateSnapshot Capture(RPlayerRuntimeReferences runtime, RRunPlayerStateSnapshot previousSnapshot = null)
    {
        return RRunPlayerStateStoreUtility.Capture(runtime, previousSnapshot);
    }

    public bool TryRestore(
        RPlayerRuntimeReferences runtime,
        RRunPlayerStateSnapshot primarySnapshot,
        RRunPlayerStateSnapshot fallbackSnapshot = null)
    {
        return RRunPlayerStateStoreUtility.TryRestore(runtime, primarySnapshot, fallbackSnapshot);
    }

    public void BindPlayerStateStore(MonoBehaviour playerStateStoreBehaviour)
    {
        if (ReferenceEquals(playerStateStoreBehaviour, this))
        {
            // Older authored scenes could still point the session at itself; clear that
            // legacy wiring so the explicit store boundary stays intact.
            playerStateStoreSource = null;
            playerStateStore = null;
            return;
        }

        if (playerStateStoreBehaviour != null && playerStateStoreBehaviour is not IRunPlayerStateStore)
        {
            Debug.LogError(
                $"{nameof(RRunSessionController)} received a player state store source that does not implement {nameof(IRunPlayerStateStore)}.",
                playerStateStoreBehaviour);
            return;
        }

        playerStateStoreSource = playerStateStoreBehaviour;
        playerStateStore = playerStateStoreSource as IRunPlayerStateStore;
    }

    public void BeginRunSkeleton(int startingFloorNumber)
    {
        sessionState.ResetForNewRun(startingFloorNumber);
        SnapshotChanged?.Invoke(sessionState.ToSnapshot());
    }

    public void CapturePlayerStateIntoSnapshot(RPlayerRuntimeReferences runtime)
    {
        playerStateSnapshot = ResolvePlayerStateStore().Capture(runtime, playerStateSnapshot);
    }

    public bool TryRestorePlayerStateFromSnapshot(RPlayerRuntimeReferences runtime, RRunPlayerStateSnapshot fallbackSnapshot = null)
    {
        return ResolvePlayerStateStore().TryRestore(runtime, playerStateSnapshot, fallbackSnapshot);
    }

    private void SyncRefactorSkeletonSessionState()
    {
        sessionState ??= new RRunSessionState();
        sessionState.Overwrite(
            runStarted,
            hasActiveRun,
            currentFloorNumber,
            floorsCleared,
            outcome,
            failureSource);
    }

    private void SyncRefactorSkeletonPlayerState(RRunSavedPlayerState state)
    {
        playerStateSnapshot = RRunPlayerStateSnapshot.FromLegacy(state);
    }

    private IRunPlayerStateStore ResolvePlayerStateStore()
    {
        if (playerStateStore != null)
        {
            return playerStateStore;
        }

        if (playerStateStoreSource != null)
        {
            if (ReferenceEquals(playerStateStoreSource, this))
            {
                playerStateStoreSource = null;
            }
            else if (playerStateStoreSource is IRunPlayerStateStore sourceStore)
            {
                playerStateStore = sourceStore;
                return playerStateStore;
            }

            if (playerStateStoreSource != null)
            {
                Debug.LogError(
                    $"{nameof(RRunSessionController)} has a player state store source that does not implement {nameof(IRunPlayerStateStore)}.",
                    playerStateStoreSource);
                playerStateStoreSource = null;
            }
        }

        MonoBehaviour[] localBehaviours = GetComponents<MonoBehaviour>();

        for (int index = 0; index < localBehaviours.Length; index++)
        {
            MonoBehaviour behaviour = localBehaviours[index];

            if (ReferenceEquals(behaviour, this) || behaviour is not IRunPlayerStateStore localStore)
            {
                continue;
            }

            playerStateStoreSource = behaviour;
            playerStateStore = localStore;
            return playerStateStore;
        }

        playerStateStore = FallbackPlayerStateStore;
        return playerStateStore;
    }

    private void ResetRunRandomizationForNewRun()
    {
        hasRunRandomizationSeed = true;
        runRandomizationSeed = ResolveNextRunRandomizationSeed();
    }

    private int EnsureRunRandomizationSeedInitialized()
    {
        if (hasRunRandomizationSeed)
        {
            return runRandomizationSeed;
        }

        hasRunRandomizationSeed = true;
        runRandomizationSeed = ResolveNextRunRandomizationSeed();
        return runRandomizationSeed;
    }

    private int ResolveNextRunRandomizationSeed()
    {
        if (useFixedRunSeedForDebug)
        {
            return fixedRunSeed;
        }

        unchecked
        {
            int seed = Environment.TickCount;
            seed = (seed * 397) ^ StartingFloorNumber;
            seed = (seed * 397) ^ GetInstanceID();
            return seed;
        }
    }
}

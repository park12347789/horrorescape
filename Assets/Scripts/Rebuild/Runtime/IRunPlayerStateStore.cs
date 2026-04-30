public interface IRunPlayerStateStore
{
    RRunPlayerStateSnapshot CreateDefault();
    RRunPlayerStateSnapshot Capture(RPlayerRuntimeReferences runtime, RRunPlayerStateSnapshot previousSnapshot = null);
    bool TryRestore(
        RPlayerRuntimeReferences runtime,
        RRunPlayerStateSnapshot primarySnapshot,
        RRunPlayerStateSnapshot fallbackSnapshot = null);
}

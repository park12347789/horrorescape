using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WasdPlayerController))]
public sealed class RPlayerRuntimeReferences : PlayerRuntimeReferencesBase
{
    private void Reset()
    {
        CacheExistingReferences();
    }

    private void Awake()
    {
        CacheExistingReferences();
    }

    private void OnValidate()
    {
        CacheExistingReferences();
    }

    public static RPlayerRuntimeReferences Resolve(WasdPlayerController controller)
    {
        return ResolveReference<RPlayerRuntimeReferences>(controller);
    }
}

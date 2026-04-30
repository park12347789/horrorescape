using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WasdPlayerController))]
public sealed class MainEscapePlayerRuntimeReferences : PlayerRuntimeReferencesBase
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

    public static MainEscapePlayerRuntimeReferences Resolve(WasdPlayerController controller)
    {
        return ResolveReference<MainEscapePlayerRuntimeReferences>(controller);
    }
}

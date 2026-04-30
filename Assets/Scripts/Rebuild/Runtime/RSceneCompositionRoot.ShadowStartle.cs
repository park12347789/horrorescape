using UnityEngine;

public sealed partial class RSceneCompositionRoot
{
    [SerializeField] private RShadowStartleDirector shadowStartleDirector;

    private void InitializeShadowStartleRuntime()
    {
        if (playerController == null || playerRuntime == null || floorAuthoring == null)
        {
            return;
        }

        shadowStartleDirector ??= GetComponent<RShadowStartleDirector>();

        if (shadowStartleDirector == null)
        {
            if (!ShouldAutoInstallShadowStartleDirector())
            {
                return;
            }

            if (Application.isPlaying)
            {
                shadowStartleDirector = gameObject.AddComponent<RShadowStartleDirector>();
            }
        }

        Transform cuePresentationRoot = gameplayRoot != null
            ? gameplayRoot
            : runtimeRoot != null
                ? runtimeRoot
                : transform;

        shadowStartleDirector?.Initialize(
            playerController,
            fogOfWarOverlay,
            floorAuthoring,
            cuePresentationRoot);
    }

    private bool ShouldAutoInstallShadowStartleDirector()
    {
        if (floorAuthoring == null)
        {
            return false;
        }

        MainEscapeShadowStartleMarker[] authoredMarkers = floorAuthoring.GetShadowStartleMarkers();
        return authoredMarkers != null && authoredMarkers.Length > 0;
    }
}

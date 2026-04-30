using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainEscapeSceneRouteResolver
{
    public static bool IsManagedGameplayScene(MainEscapeRuntimeSettings settings, RFloorSceneEntry[] configuredScenes, Scene scene)
    {
        return TryResolveFloorNumber(settings, configuredScenes, scene, out _);
    }

    public static bool TryResolveFloorNumber(MainEscapeRuntimeSettings settings, RFloorSceneEntry[] configuredScenes, Scene scene, out int floorNumber)
    {
        if (!scene.IsValid())
        {
            floorNumber = 0;
            return false;
        }

        RFloorSceneEntry[] routes = configuredScenes ?? System.Array.Empty<RFloorSceneEntry>();
        RFloorSceneEntry[] routeGraphScenes = routes.Length > 0
            ? routes
            : settings != null
                ? settings.FallbackFloorSceneRoutes
                : System.Array.Empty<RFloorSceneEntry>();

        if (routeGraphScenes.Length > 0)
        {
            int startingFloorNumber = settings != null
                ? settings.FallbackStartFloorNumber
                : routeGraphScenes[0].floorNumber;

            if (RRunSceneRouteFloorResolver.TryResolveFloorNumberForScene(
                    startingFloorNumber,
                    routeGraphScenes,
                    scene,
                    out floorNumber))
            {
                floorNumber = Mathf.Max(1, floorNumber);
                return true;
            }

            floorNumber = 0;
            return false;
        }

        if (MainEscapeSceneIdentityUtility.TryGetCanonicalFloorNumber(scene, out floorNumber))
        {
            floorNumber = Mathf.Max(1, floorNumber);
            return true;
        }

        string scenePathOrName = MainEscapeSceneIdentityUtility.GetScenePathOrName(scene);

        if (settings != null && settings.TryGetFallbackFloorNumberForScene(scenePathOrName, out floorNumber))
        {
            floorNumber = Mathf.Max(1, floorNumber);
            return true;
        }

        floorNumber = 0;
        return false;
    }

    public static bool SceneMatchesFloor(MainEscapeRuntimeSettings settings, Scene scene, int floorNumber)
    {
        return SceneMatchesFloor(settings, null, scene, floorNumber);
    }

    public static bool SceneMatchesFloor(
        MainEscapeRuntimeSettings settings,
        RFloorSceneEntry[] configuredScenes,
        Scene scene,
        int floorNumber)
    {
        if (!scene.IsValid() || floorNumber <= 0)
        {
            return false;
        }

        RFloorSceneEntry[] routes = configuredScenes ?? System.Array.Empty<RFloorSceneEntry>();
        RFloorSceneEntry[] routeGraphScenes = routes.Length > 0
            ? routes
            : settings != null
                ? settings.FallbackFloorSceneRoutes
                : System.Array.Empty<RFloorSceneEntry>();

        if (routeGraphScenes.Length > 0)
        {
            int startingFloorNumber = settings != null
                ? settings.FallbackStartFloorNumber
                : routeGraphScenes[0].floorNumber;

            return RRunSceneRouteFloorResolver.TryResolveFloorNumberForScene(
                    startingFloorNumber,
                    routeGraphScenes,
                    scene,
                    out int routeFloorNumber)
                && routeFloorNumber == Mathf.Max(1, floorNumber);
        }

        if (MainEscapeSceneIdentityUtility.MatchesCanonicalFloorScene(scene, floorNumber))
        {
            return true;
        }

        if (settings == null)
        {
            return false;
        }

        if (!settings.TryGetFallbackScenePathForFloor(floorNumber, out string scenePath))
        {
            return false;
        }

        return MainEscapeSceneIdentityUtility.MatchesScene(scene, scenePath);
    }
}

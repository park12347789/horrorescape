using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RSceneRouteMembershipUtility
{
    public static bool IsManagedGameplayOrAuthoredScene(Scene scene, RRunSessionController sessionController = null)
    {
        return IsManagedGameplayOrAuthoredScene(scene, sessionController, null);
    }

    internal static bool IsManagedGameplayOrAuthoredScene(
        Scene scene,
        RRunSessionController sessionController,
        MainEscapeRuntimeSettings runtimeSettingsOverride)
    {
        return IsManagedGameplayOrAuthoredScene(scene, sessionController, runtimeSettingsOverride, null);
    }

    internal static bool IsManagedGameplayOrAuthoredScene(
        Scene scene,
        RRunSessionController sessionController,
        MainEscapeRuntimeSettings runtimeSettingsOverride,
        RRunRoutingSettings routingSettingsOverride)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        if (sessionController != null)
        {
            return IsManagedGameplayScene(scene, sessionController);
        }

        if (Application.isPlaying)
        {
            RRunSessionController activeSessionController = RRunSessionResolver.ResolveForScene(scene);

            if (activeSessionController != null)
            {
                return IsManagedGameplayScene(scene, activeSessionController);
            }
        }

        if (TryResolveRoutingSettingsMembership(scene, routingSettingsOverride, out bool routingSettingsMatch))
        {
            return routingSettingsMatch;
        }

        MainEscapeRuntimeSettings settings = ResolveRuntimeSettings(runtimeSettingsOverride);

        if (settings != null && HasRouteMembershipData(settings))
        {
            return MainEscapeSceneIdentityUtility.MatchesCanonicalLobbyScene(scene)
                || RRunSceneRouteFloorResolver.TryResolveFloorNumberForScene(
                    settings.FallbackStartFloorNumber,
                    settings.FallbackFloorSceneRoutes,
                    scene,
                    out _);
        }

        return IsLegacyAuthoredSceneName(MainEscapeSceneIdentityUtility.GetScenePathOrName(scene));
    }

    private static bool IsManagedGameplayScene(Scene scene, RRunSessionController sessionController)
    {
        return sessionController != null
            && sessionController.TryResolveFloorNumberForScene(scene, out _);
    }

    private static bool IsLegacyAuthoredSceneName(string sceneName)
    {
        try
        {
            return MainEscapeSceneIdentityUtility.IsAuthoredSceneName(sceneName);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static MainEscapeRuntimeSettings ResolveRuntimeSettings(MainEscapeRuntimeSettings runtimeSettingsOverride)
    {
        if (runtimeSettingsOverride != null)
        {
            return runtimeSettingsOverride;
        }

        try
        {
            return MainEscapeRuntimeSettings.Load();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool HasRouteMembershipData(MainEscapeRuntimeSettings settings)
    {
        return settings != null && settings.FallbackFloorSceneRoutes.Length > 0;
    }

    private static bool TryResolveRoutingSettingsMembership(
        Scene scene,
        RRunRoutingSettings routingSettingsOverride,
        out bool isMember)
    {
        RRunRoutingSettings routingSettings = ResolveRoutingSettings(routingSettingsOverride);

        if (routingSettings == null
            || !RouteGraphFloorRouteAdapter.TryResolveStartingFloorNumber(
                routingSettings.DefaultChapter,
                out int startingFloorNumber)
            || !RouteGraphFloorRouteAdapter.TryBuildFloorSceneEntries(
                routingSettings.DefaultChapter,
                out RFloorSceneEntry[] routeGraphScenes))
        {
            isMember = false;
            return false;
        }

        isMember = MainEscapeSceneIdentityUtility.MatchesScene(scene, routingSettings.LobbyScenePath)
            || RRunSceneRouteFloorResolver.TryResolveFloorNumberForScene(
                startingFloorNumber,
                routeGraphScenes,
                scene,
                out _);
        return true;
    }

    private static RRunRoutingSettings ResolveRoutingSettings(RRunRoutingSettings routingSettingsOverride)
    {
        if (routingSettingsOverride != null)
        {
            return routingSettingsOverride;
        }

        return RRunCanonicalAssetLocator.TryLoadRoutingSettings();
    }
}

using System;
using UnityEngine.SceneManagement;

public sealed class RRuntimeSettingsFloorResidencyPolicy : IFloorSceneResidencyPolicy
{
    private readonly MainEscapeRuntimeSettings runtimeSettingsOverride;
    private readonly RRunRoutingSettings routingSettingsOverride;

    public RRuntimeSettingsFloorResidencyPolicy()
    {
    }

    internal RRuntimeSettingsFloorResidencyPolicy(MainEscapeRuntimeSettings runtimeSettingsOverride)
    {
        this.runtimeSettingsOverride = runtimeSettingsOverride;
    }

    internal RRuntimeSettingsFloorResidencyPolicy(
        MainEscapeRuntimeSettings runtimeSettingsOverride,
        RRunRoutingSettings routingSettingsOverride)
    {
        this.runtimeSettingsOverride = runtimeSettingsOverride;
        this.routingSettingsOverride = routingSettingsOverride;
    }

    public bool RequiresSceneResidentAuthoring(EscapeFloorDefinition floorDefinition, Scene scene)
    {
        if (floorDefinition == null)
        {
            return false;
        }

        if (ApplicationIsPlaying())
        {
            RRunSessionController sessionController = RRunSessionResolver.ResolveForScene(scene);

            if (sessionController != null)
            {
                return sessionController.TryResolveFloorNumberForScene(scene, out int sessionFloorNumber)
                    && sessionFloorNumber == floorDefinition.FloorNumber;
            }
        }

        if (TryResolveRoutingSettingsFloor(scene, out int routingSettingsFloorNumber))
        {
            return routingSettingsFloorNumber == floorDefinition.FloorNumber;
        }

        MainEscapeRuntimeSettings settings = ResolveRuntimeSettings();

        if (settings != null)
        {
            return RRunSceneRouteFloorResolver.TryResolveFloorNumberForScene(
                    settings.FallbackStartFloorNumber,
                    settings.FallbackFloorSceneRoutes,
                    scene,
                    out int routeFloorNumber)
                && routeFloorNumber == floorDefinition.FloorNumber;
        }

        return MainEscapeSceneIdentityUtility.MatchesCanonicalFloorScene(scene, floorDefinition.FloorNumber);
    }

    private static bool ApplicationIsPlaying()
    {
        return UnityEngine.Application.isPlaying;
    }

    private MainEscapeRuntimeSettings ResolveRuntimeSettings()
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

    private bool TryResolveRoutingSettingsFloor(Scene scene, out int floorNumber)
    {
        RRunRoutingSettings routingSettings = ResolveRoutingSettings();

        if (routingSettings == null
            || !RouteGraphFloorRouteAdapter.TryResolveStartingFloorNumber(
                routingSettings.DefaultChapter,
                out int startingFloorNumber)
            || !RouteGraphFloorRouteAdapter.TryBuildFloorSceneEntries(
                routingSettings.DefaultChapter,
                out RFloorSceneEntry[] routeGraphScenes))
        {
            floorNumber = 0;
            return false;
        }

        if (RRunSceneRouteFloorResolver.TryResolveFloorNumberForScene(
                startingFloorNumber,
                routeGraphScenes,
                scene,
                out floorNumber))
        {
            return true;
        }

        floorNumber = 0;
        return true;
    }

    private RRunRoutingSettings ResolveRoutingSettings()
    {
        if (routingSettingsOverride != null)
        {
            return routingSettingsOverride;
        }

        return RRunCanonicalAssetLocator.TryLoadRoutingSettings();
    }
}

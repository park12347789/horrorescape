using UnityEngine;

public sealed partial class RSceneCompositionRoot
{
    private void BindGameplayRuntime()
    {
        if (playerRuntime == null || runController == null)
        {
            return;
        }

        runSessionController?.BindGameplayRuntime(playerRuntime, runController, restorePlayerStateImmediately: false);

        if (!UsesElevatorPropDirectExit())
        {
            keyGatePoint?.Configure(runController);
            authoredStairsPoint?.Configure(runController);
        }

        IFogVisibilityService fogVisibilityService = ResolveFogVisibilityService();

        if (fogVisibilityService == null)
        {
            return;
        }

        IFogOfWarOverlayConsumer[] fogConsumers = RSceneBindingCacheUtility.ResolveBindings<IFogOfWarOverlayConsumer>(
            gameObject.scene,
            fogOverlayConsumerBehaviours);

        for (int index = 0; index < fogConsumers.Length; index++)
        {
            fogConsumers[index]?.BindFogVisibilityService(fogVisibilityService);
        }
    }

    private void BindDebugRuntime()
    {
        if (debugModeController == null)
        {
            return;
        }

        debugModeController.BindDebugModeAppliers(debugModeApplierBehaviours);
    }

    private IFogVisibilityService ResolveFogVisibilityService()
    {
        return fogOfWarOverlay;
    }

    private void BindHudRuntime()
    {
        if (playerRuntime == null || hudCanvas == null)
        {
            return;
        }

        IRebuildHudBinder[] hudBinders = RSceneBindingCacheUtility.ResolveBindings<IRebuildHudBinder>(
            gameObject.scene,
            hudBinderBehaviours);

        for (int index = 0; index < hudBinders.Length; index++)
        {
            IRebuildHudBinder binder = hudBinders[index];
            binder?.BindPlayerRuntime(playerRuntime);
            binder?.BindHudCanvas(hudCanvas);
        }
    }
}

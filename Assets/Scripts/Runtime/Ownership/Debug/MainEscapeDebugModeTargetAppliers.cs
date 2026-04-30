using System.Collections.Generic;

internal static class MainEscapeDebugModeTargetAppliers
{
    private static MainEscapeRuntimeSettings RuntimeSettings => MainEscapeRuntimeSettings.Load();

    public static IMainEscapeDebugModeApplier[] Create(
        WasdPlayerController playerController,
        IInvincibilityDebugApplier invincibilityDebugApplier,
        IFogBypassDebugApplier fogBypassDebugApplier,
        IDebugPresentationApplier debugPresentationApplier,
        INoiseDebugPulseApplier noiseDebugPulseApplier,
        bool syncNoisePulsesWithDebugMode)
    {
        List<IMainEscapeDebugModeApplier> appliers = new(5);

        if (playerController != null)
        {
            appliers.Add(new PlayerFlashlightPresentationApplier(playerController));
        }

        if (invincibilityDebugApplier != null)
        {
            appliers.Add(new PlayerInvincibilityApplier(invincibilityDebugApplier));
        }

        if (fogBypassDebugApplier != null)
        {
            appliers.Add(new FogVisibilityBypassApplier(fogBypassDebugApplier));
        }

        if (debugPresentationApplier != null)
        {
            appliers.Add(new DebugPresentationApplier(debugPresentationApplier));
        }

        if (syncNoisePulsesWithDebugMode && noiseDebugPulseApplier != null)
        {
            appliers.Add(new NoiseDebugPulsesApplier(noiseDebugPulseApplier));
        }

        return appliers.Count == 0
            ? System.Array.Empty<IMainEscapeDebugModeApplier>()
            : appliers.ToArray();
    }

    private sealed class PlayerFlashlightPresentationApplier : IMainEscapeDebugModeApplier
    {
        private readonly WasdPlayerController playerController;

        public PlayerFlashlightPresentationApplier(WasdPlayerController playerController)
        {
            this.playerController = playerController;
        }

        public void ApplyDebugMode(MainEscapeDebugModeState state)
        {
            if (playerController == null)
            {
                return;
            }

            MainEscapeRuntimeSettings runtimeSettings = RuntimeSettings;

            if (state.DebugModeEnabled)
            {
                playerController.SetFlashlightPresentation(
                    runtimeSettings.DebugFlashlightPresentationIntensityScale,
                    runtimeSettings.DebugFlashlightPresentationVolumeScale);
                playerController.SetFlashlightShadowEnabled(runtimeSettings.DebugFlashlightShadowsEnabled);
                return;
            }

            playerController.SetFlashlightPresentation(
                runtimeSettings.DefaultFlashlightPresentationIntensityScale,
                runtimeSettings.DefaultFlashlightPresentationVolumeScale);
            playerController.SetFlashlightShadowEnabled(runtimeSettings.DefaultFlashlightShadowsEnabled);
        }
    }

    private sealed class PlayerInvincibilityApplier : IMainEscapeDebugModeApplier
    {
        private readonly IInvincibilityDebugApplier invincibilityDebugApplier;

        public PlayerInvincibilityApplier(IInvincibilityDebugApplier invincibilityDebugApplier)
        {
            this.invincibilityDebugApplier = invincibilityDebugApplier;
        }

        public void ApplyDebugMode(MainEscapeDebugModeState state)
        {
            invincibilityDebugApplier?.ApplyInvincibility(state.InvincibilityEnabled);
        }
    }

    private sealed class FogVisibilityBypassApplier : IMainEscapeDebugModeApplier
    {
        private readonly IFogBypassDebugApplier fogBypassDebugApplier;

        public FogVisibilityBypassApplier(IFogBypassDebugApplier fogBypassDebugApplier)
        {
            this.fogBypassDebugApplier = fogBypassDebugApplier;
        }

        public void ApplyDebugMode(MainEscapeDebugModeState state)
        {
            fogBypassDebugApplier?.ApplyFogBypass(state.FogBypassEnabled);
        }
    }

    private sealed class DebugPresentationApplier : IMainEscapeDebugModeApplier
    {
        private readonly IDebugPresentationApplier debugPresentationApplier;

        public DebugPresentationApplier(IDebugPresentationApplier debugPresentationApplier)
        {
            this.debugPresentationApplier = debugPresentationApplier;
        }

        public void ApplyDebugMode(MainEscapeDebugModeState state)
        {
            debugPresentationApplier?.ApplyDebugPresentation(state.VentMarkersEnabled);
        }
    }

    private sealed class NoiseDebugPulsesApplier : IMainEscapeDebugModeApplier
    {
        private readonly INoiseDebugPulseApplier noiseDebugPulseApplier;

        public NoiseDebugPulsesApplier(INoiseDebugPulseApplier noiseDebugPulseApplier)
        {
            this.noiseDebugPulseApplier = noiseDebugPulseApplier;
        }

        public void ApplyDebugMode(MainEscapeDebugModeState state)
        {
            if (!state.NoiseDebugPulsesEnabled.HasValue || noiseDebugPulseApplier == null)
            {
                return;
            }

            noiseDebugPulseApplier.ApplyNoiseDebugPulses(state.NoiseDebugPulsesEnabled.Value);
        }
    }
}

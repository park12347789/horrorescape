public readonly struct MainEscapeDebugModeState
{
    public MainEscapeDebugModeState(
        bool debugModeEnabled,
        bool invincibilityEnabled,
        bool fogBypassEnabled,
        bool ventMarkersEnabled,
        bool? noiseDebugPulsesEnabled)
    {
        DebugModeEnabled = debugModeEnabled;
        InvincibilityEnabled = invincibilityEnabled;
        FogBypassEnabled = fogBypassEnabled;
        VentMarkersEnabled = ventMarkersEnabled;
        NoiseDebugPulsesEnabled = noiseDebugPulsesEnabled;
    }

    public bool DebugModeEnabled { get; }
    public bool InvincibilityEnabled { get; }
    public bool FogBypassEnabled { get; }
    public bool VentMarkersEnabled { get; }
    public bool? NoiseDebugPulsesEnabled { get; }
}

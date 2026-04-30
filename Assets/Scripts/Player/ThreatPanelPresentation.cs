public readonly struct ThreatPanelPresentation
{
    public ThreatPanelPresentation(float intensity, bool pursuitConfirmed, string title, string detail, float spottedPulseIntensity = 0f)
    {
        Intensity = intensity;
        PursuitConfirmed = pursuitConfirmed;
        Title = title;
        Detail = detail;
        SpottedPulseIntensity = spottedPulseIntensity;
    }

    public float Intensity { get; }
    public bool PursuitConfirmed { get; }
    public string Title { get; }
    public string Detail { get; }
    public float SpottedPulseIntensity { get; }
}

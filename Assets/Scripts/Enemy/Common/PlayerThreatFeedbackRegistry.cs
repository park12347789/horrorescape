using System.Collections.Generic;

public static class PlayerThreatFeedbackRegistry
{
    private static readonly List<IPlayerThreatFeedbackSource> sources = new();

    public static IReadOnlyList<IPlayerThreatFeedbackSource> Sources => sources;
    public static int Version { get; private set; }

    public static void Register(IPlayerThreatFeedbackSource source)
    {
        if (source == null || sources.Contains(source))
        {
            return;
        }

        sources.Add(source);
        Version++;
    }

    public static void Unregister(IPlayerThreatFeedbackSource source)
    {
        if (source == null)
        {
            return;
        }

        if (sources.Remove(source))
        {
            Version++;
        }
    }
}

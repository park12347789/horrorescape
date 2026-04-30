using System.IO;

public static class IRLobbySummaryFormatter
{
    public static string FormatTitle(string summaryTitle, bool analogThemeEnabled)
    {
        string resolved = string.IsNullOrWhiteSpace(summaryTitle) ? "Status" : summaryTitle;
        return analogThemeEnabled ? $"READY TO DEPLOY // {resolved}".ToUpperInvariant() : resolved;
    }

    public static string FormatBody(string summaryBody, bool analogThemeEnabled)
    {
        string resolved = string.IsNullOrWhiteSpace(summaryBody) ? "No message." : summaryBody;
        return analogThemeEnabled ? resolved.ToUpperInvariant() : resolved;
    }

    public static string FormatFooter(string gameplayScenePath, bool analogThemeEnabled)
    {
        if (analogThemeEnabled)
        {
            string sceneLabel = string.IsNullOrWhiteSpace(gameplayScenePath)
                ? "SCENE UNBOUND"
                : Path.GetFileNameWithoutExtension(gameplayScenePath).ToUpperInvariant();
            return $"SUBMISSION BUILD // {sceneLabel} // ANALOG TERMINAL READY";
        }

        return string.IsNullOrWhiteSpace(gameplayScenePath)
            ? "Gameplay scene unavailable."
            : $"Gameplay scene: {gameplayScenePath}";
    }
}

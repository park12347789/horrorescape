using UnityEngine;

public readonly struct RRunHudContext
{
    public RRunHudContext(
        int currentFloorNumber,
        bool escaped,
        bool currentFloorGateUnlocked,
        bool hasAuthoredGateKey,
        string statusMessage,
        string healthSummary,
        float flashlightChargeNormalized,
        int storedBatteryCount,
        string quickSummary,
        string interactionSummary)
    {
        CurrentFloorNumber = Mathf.Max(1, currentFloorNumber);
        Escaped = escaped;
        CurrentFloorGateUnlocked = currentFloorGateUnlocked;
        HasAuthoredGateKey = hasAuthoredGateKey;
        StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? string.Empty : statusMessage;
        HealthSummary = string.IsNullOrWhiteSpace(healthSummary) ? "HP -" : healthSummary;
        FlashlightChargeNormalized = Mathf.Clamp01(flashlightChargeNormalized);
        StoredBatteryCount = Mathf.Max(0, storedBatteryCount);
        QuickSummary = string.IsNullOrWhiteSpace(quickSummary) ? "1 Bottle x0  2 Medkit x0  Space Throw" : quickSummary;
        InteractionSummary = string.IsNullOrWhiteSpace(interactionSummary) ? "No interactable in reach" : interactionSummary;
    }

    public int CurrentFloorNumber { get; }
    public bool Escaped { get; }
    public bool CurrentFloorGateUnlocked { get; }
    public bool HasAuthoredGateKey { get; }
    public string StatusMessage { get; }
    public string HealthSummary { get; }
    public float FlashlightChargeNormalized { get; }
    public int StoredBatteryCount { get; }
    public string QuickSummary { get; }
    public string InteractionSummary { get; }
}

public static class RRunHudPresenter
{
    public static string BuildHeader(RRunHudContext context)
    {
        return context.Escaped
            ? "Route Cleared"
            : $"Emergency Route - {context.CurrentFloorNumber}F";
    }

    public static string BuildBody(RRunHudContext context)
    {
        if (context.Escaped)
        {
            return "Street exit reached.\nClean loop active.\n\nControls\nWASD move  Shift sprint  E interact  I inventory";
        }

        return $"Route\n{BuildRouteLine(context.CurrentFloorNumber)}\n\nGear\n{BuildGearSummary(context)}\n\nObjective\n{BuildObjectiveLine(context)}\n\nStatus\n{context.StatusMessage}\n\nNearby\n{context.InteractionSummary}\n\nControls\nWASD move  Shift sprint  E interact  I inventory";
    }

    public static string BuildObjectiveLine(RRunHudContext context)
    {
        if (context.CurrentFloorNumber > 1)
        {
            return !context.HasAuthoredGateKey
                ? "Find the Iron Gate Key."
                : context.CurrentFloorGateUnlocked
                    ? $"Use the emergency stairs and descend to {context.CurrentFloorNumber - 1}F."
                    : "Use the key on the iron gate.";
        }

        return "Use the street exit to escape.";
    }

    private static string BuildRouteLine(int currentFloorNumber)
    {
        if (currentFloorNumber <= 1)
        {
            return "1F";
        }

        string route = $"{currentFloorNumber}F";

        for (int nextFloor = currentFloorNumber - 1; nextFloor >= 1; nextFloor--)
        {
            route += $" -> {nextFloor}F";
        }

        return route;
    }

    private static string BuildGearSummary(RRunHudContext context)
    {
        string batterySummary = $"Battery {Mathf.RoundToInt(context.FlashlightChargeNormalized * 100f)}%  Cells {context.StoredBatteryCount}";
        return $"{context.HealthSummary}\n{batterySummary}\n{context.QuickSummary}";
    }
}

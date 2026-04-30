using System.IO;

using NUnit.Framework;

public sealed class FloorEscapeInteractionPointOptimizationEditModeTests
{
    private static readonly string[] SourcePaths =
    {
        "Assets/Scripts/Objectives/MainEscapeKeyGatePoint.cs",
        "Assets/Scripts/Objectives/FloorEscapeTransitionPoint.cs",
        "Assets/Scripts/Objectives/FloorEscapeGoalPickup.cs",
        "Assets/Scripts/Objectives/MainEscapeEmergencyStairsPoint.cs"
    };

    [TestCaseSource(nameof(SourcePaths))]
    public void EscapeInteractionPoints_CacheRenderedStateBeforeApplyingVisuals(string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("private bool hasRenderedState;"));
        Assert.That(source, Does.Contain("gameObject.activeSelf != visible"));
        Assert.That(source, Does.Contain("StoreRenderedState("));
        Assert.That(source, Does.Contain("InvalidateRenderedState();"));
    }

    [TestCaseSource(nameof(SourcePaths))]
    public void EscapeInteractionPoints_AvoidUnconditionalSetActiveWrites(string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Not.Contain("gameObject.SetActive(visible);\r\n\r\n        if"));
        Assert.That(source, Does.Not.Contain("gameObject.SetActive(visible);\n\n        if"));
    }
}

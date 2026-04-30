using System.IO;

using NUnit.Framework;

public sealed class QuickSlotsHudBinderOptimizationEditModeTests
{
    private const string QuickSlotsBinderSourcePath = "Assets/Scripts/Rebuild/UI/IRPlayerQuickSlotsHudBinder.cs";

    [Test]
    public void RefreshView_ReusesPresentationBuffer()
    {
        string source = File.ReadAllText(QuickSlotsBinderSourcePath);

        Assert.That(source, Does.Contain("private QuickSlotPresentation[] slotPresentationBuffer"));
        Assert.That(source, Does.Contain("EnsureSlotPresentationBuffer(slotCount);"));
        Assert.That(source, Does.Contain("slotPresentationBuffer[index] = new QuickSlotPresentation"));
        Assert.That(source, Does.Contain("new QuickSlotPanelPresentation(slotPresentationBuffer)"));
        Assert.That(source, Does.Not.Contain("new List<QuickSlotPresentation>"));
        Assert.That(source, Does.Not.Contain("slots.ToArray()"));
    }
}

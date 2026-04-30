using System.IO;

using NUnit.Framework;

public sealed class EnemyVisionVisualizerReferenceLookupEditModeTests
{
    private const string SourcePath = "Assets/Scripts/Enemy/EnemyVisionVisualizer.cs";

    [Test]
    public void ResolvePlayerController_UsesExplicitBindingAndSceneLocalFallback()
    {
        string source = File.ReadAllText(SourcePath);

        Assert.That(source, Does.Contain("[SerializeField] private WasdPlayerController playerController"));
        Assert.That(source, Does.Contain("public void BindPlayerController(WasdPlayerController boundPlayerController)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<WasdPlayerController>"));
    }
}

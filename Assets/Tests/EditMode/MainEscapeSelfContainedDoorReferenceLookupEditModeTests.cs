using System.IO;

using NUnit.Framework;

public sealed class MainEscapeSelfContainedDoorReferenceLookupEditModeTests
{
    private const string SourcePath = "Assets/Scripts/Objectives/MainEscapeSelfContainedDoor.cs";

    [Test]
    public void RuntimeReferences_UseSceneLocalLookupAndNoiseEventBusFallback()
    {
        string source = File.ReadAllText(SourcePath);

        Assert.That(source, Does.Contain("public void ConfigureRuntimeReferences("));
        Assert.That(source, Does.Contain("private INoiseEventBus noiseEventBus"));
        Assert.That(source, Does.Contain("NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindUniqueComponentInScene<GridMapService>"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindUniqueComponentInScene<ObjectiveManager>"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<EnemyStateMachine>"));
        Assert.That(source, Does.Not.Contain("NoiseSystem.TryEmitNoise"));
        Assert.That(source, Does.Not.Contain("FindFirstObjectByType<GridMapService>"));
        Assert.That(source, Does.Not.Contain("FindFirstObjectByType<ObjectiveManager>"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<EnemyStateMachine>"));
    }
}

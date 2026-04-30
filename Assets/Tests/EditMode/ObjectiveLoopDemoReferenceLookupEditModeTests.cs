using System.IO;

using NUnit.Framework;

public sealed class ObjectiveLoopDemoReferenceLookupEditModeTests
{
    private const string SourcePath = "Assets/Scripts/Objectives/ObjectiveLoopDemo.cs";

    [Test]
    public void DoorController_RuntimeFallbacksUseSceneLocalLookupAndNoiseEventBus()
    {
        string source = File.ReadAllText(SourcePath);

        Assert.That(source, Does.Contain("public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)"));
        Assert.That(source, Does.Contain("public void BindPlayerController(WasdPlayerController boundPlayerController)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<EnemyStateMachine>"));
        Assert.That(source, Does.Contain("NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem)"));
        Assert.That(source, Does.Not.Contain("UnityEngine.Object.FindFirstObjectByType<WasdPlayerController>"));
        Assert.That(source, Does.Not.Contain("UnityEngine.Object.FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None)"));
        Assert.That(source, Does.Not.Contain("NoiseSystem.TryEmitNoise"));
    }
}

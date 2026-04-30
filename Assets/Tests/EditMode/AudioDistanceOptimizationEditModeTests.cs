using System.IO;

using NUnit.Framework;

public sealed class AudioDistanceOptimizationEditModeTests
{
    private static readonly string[] SpatialAudioSourcePaths =
    {
        "Assets/Scripts/Audio/RoomLightHumAudio.cs",
        "Assets/Scripts/Audio/EnemyPassiveAmbientAudio.cs",
        "Assets/Scripts/Audio/EnemyPlayerSpottedScreamAudio.cs",
        "Assets/Scripts/Audio/VentEnemyAudioDriver.cs",
        "Assets/Scripts/Audio/PrototypeEnemyAudioDriver.cs"
    };

    [TestCaseSource(nameof(SpatialAudioSourcePaths))]
    public void SpatialAudioAttenuation_RejectsOutOfRangeUsingSquaredDistance(string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("sqrDistance"));
        Assert.That(source, Does.Contain("maxAudibleDistanceSqr"));
        Assert.That(source, Does.Contain("return 0f;"));
        Assert.That(source, Does.Contain("Mathf.Sqrt(sqrDistance)"));
        Assert.That(source, Does.Not.Contain("Vector2.Distance(transform.position, playerController.transform.position)"));
    }

    [Test]
    public void AudioPlayerReferences_UseSceneLocalFallbackResolver()
    {
        string resolverSource = File.ReadAllText("Assets/Scripts/Audio/AudioScenePlayerReferenceResolver.cs");

        Assert.That(resolverSource, Does.Contain("owner.scene"));
        Assert.That(resolverSource, Does.Contain("GetRootGameObjects()"));
        Assert.That(resolverSource, Does.Contain("GetComponentInChildren<WasdPlayerController>(false)"));

        for (int index = 0; index < 3; index++)
        {
            string source = File.ReadAllText(SpatialAudioSourcePaths[index]);

            Assert.That(source, Does.Contain("AudioScenePlayerReferenceResolver.ResolveCurrentOrSceneFallback"));
            Assert.That(source, Does.Not.Contain("FindFirstObjectByType<WasdPlayerController>"));
            Assert.That(source, Does.Not.Contain("FindObjectsByType<WasdPlayerController>"));
        }
    }
}

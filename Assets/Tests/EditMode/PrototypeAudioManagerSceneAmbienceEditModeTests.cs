using System.IO;
using NUnit.Framework;

public sealed class PrototypeAudioManagerSceneAmbienceEditModeTests
{
    private const string SourcePath = "Assets/Scripts/Audio/PrototypeAudioManager.cs";

    [Test]
    public void SceneAmbienceApi_ProvidesExplicitSceneWrapper_AndKeepsCompatibilityWrapper()
    {
        string source = File.ReadAllText(SourcePath);
        string activeSceneWrapper = ExtractMethodBody(source, "TryApplySceneAmbienceForActiveScene");

        Assert.That(source, Does.Contain("public static void TryApplySceneAmbienceForScene(Scene scene)"));
        Assert.That(source, Does.Contain("public static void TryApplySceneAmbienceForActiveScene()"));
        Assert.That(source, Does.Contain("Prefer TryApplySceneAmbienceForScene with an explicit owning scene."));
        Assert.That(activeSceneWrapper, Does.Contain("ApplySceneAmbienceForLoadedRuntimeScene(immediate: true)"));
        Assert.That(activeSceneWrapper, Does.Not.Contain("SceneManager.GetActiveScene()"));
    }

    [Test]
    public void RuntimeMixAndStartupTrace_UseCachedSceneInsteadOfActiveSceneLookup()
    {
        string source = File.ReadAllText(SourcePath);

        string setRuntimeMixVolumes = ExtractMethodBody(source, "SetRuntimeMixVolumes");
        Assert.That(setRuntimeMixVolumes, Does.Contain("ApplySceneAmbienceForCachedScene(immediate: true)"));
        Assert.That(setRuntimeMixVolumes, Does.Not.Contain("SceneManager.GetActiveScene()"));

        string shouldTraceStartup = ExtractMethodBody(source, "ShouldTraceStartup");
        Assert.That(shouldTraceStartup, Does.Contain("cachedRuntimeScene"));
        Assert.That(shouldTraceStartup, Does.Not.Contain("SceneManager.GetActiveScene()"));

        string handleSceneLoaded = ExtractMethodBody(source, "HandleSceneLoaded");
        Assert.That(handleSceneLoaded, Does.Contain("ApplySceneAmbienceForScene(scene, immediate: false)"));

        string bootstrapRuntimeAudio = ExtractMethodBody(source, "BootstrapRuntimeAudio");
        Assert.That(bootstrapRuntimeAudio, Does.Contain("ApplySceneAmbienceForLoadedRuntimeScene(immediate: true)"));
        Assert.That(bootstrapRuntimeAudio, Does.Not.Contain("SceneManager.GetActiveScene()"));

        string start = ExtractMethodBody(source, "Start");
        Assert.That(start, Does.Contain("ApplySceneAmbienceForLoadedRuntimeScene(immediate: true)"));
        Assert.That(start, Does.Not.Contain("SceneManager.GetActiveScene()"));
    }

    [Test]
    public void StartupAmbienceResolution_ScansLoadedManagedScenesInsteadOfActiveScene()
    {
        string source = File.ReadAllText(SourcePath);
        string loadedRuntimeScene = ExtractMethodBody(source, "ApplySceneAmbienceForLoadedRuntimeScene");
        string isManagedScene = ExtractMethodBody(source, "IsSceneAmbienceManagedScene");

        Assert.That(loadedRuntimeScene, Does.Contain("SceneManager.sceneCount"));
        Assert.That(loadedRuntimeScene, Does.Contain("SceneManager.GetSceneAt(index)"));
        Assert.That(loadedRuntimeScene, Does.Contain("IsSceneAmbienceManagedScene(scene)"));
        Assert.That(loadedRuntimeScene, Does.Not.Contain("SceneManager.GetActiveScene()"));
        Assert.That(isManagedScene, Does.Contain("MainEscapeSceneIdentityUtility.MatchesCanonicalLobbyScene(scene)"));
        Assert.That(isManagedScene, Does.Contain("MainEscapeSceneIdentityUtility.MatchesCanonicalTutorialScene(scene)"));
        Assert.That(isManagedScene, Does.Contain("RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(scene)"));
    }

    [Test]
    public void FloorAmbienceResolution_PrefersSceneLocalSessionBeforeSingletonFallback()
    {
        string source = File.ReadAllText(SourcePath);
        string resolveFloorNumber = ExtractMethodBody(source, "TryResolveFloorNumberForAmbience");
        string resolverSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RRunSessionResolver.cs");

        Assert.That(resolveFloorNumber, Does.Contain("RRunSessionResolver.ResolveForScene(scene)"));
        Assert.That(resolverSource, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<RRunSessionController>(scene)"));
        Assert.That(resolverSource, Does.Contain("RRunSessionController.TryGetCachedInstance"));
        Assert.That(resolverSource, Does.Not.Contain("RRunSessionController.Instance"));
        Assert.That(
            resolverSource.IndexOf("RSceneReferenceLookup.FindFirstComponentInScene<RRunSessionController>(scene)", System.StringComparison.Ordinal),
            Is.LessThan(resolverSource.IndexOf("RRunSessionController.TryGetCachedInstance", System.StringComparison.Ordinal)));
    }

    [Test]
    public void PlayerAudio_UsesAudioManagerCommandInsteadOfDirectInstance()
    {
        string audioManagerSource = File.ReadAllText(SourcePath);
        string playerAudioSource = File.ReadAllText("Assets/Scripts/Audio/PrototypePlayerAudio.cs");

        Assert.That(audioManagerSource, Does.Contain("public static bool TryPlayFootstep(bool sprinting, float strength)"));
        Assert.That(playerAudioSource, Does.Contain("PrototypeAudioManager.TryPlayFootstep(sprinting, strength)"));
        Assert.That(playerAudioSource, Does.Not.Contain("PrototypeAudioManager.Instance"));
    }

    [Test]
    public void LobbyRuntimeOptions_UseCachedAudioManagerAccessorInsteadOfDirectInstance()
    {
        string audioManagerSource = File.ReadAllText(SourcePath);
        string runSessionSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs");

        Assert.That(audioManagerSource, Does.Contain("public static bool TryGetCachedInstance(out PrototypeAudioManager audioManager)"));
        Assert.That(runSessionSource, Does.Contain("PrototypeAudioManager.TryGetCachedInstance(out resolvedAudioManager)"));
        Assert.That(runSessionSource, Does.Not.Contain("PrototypeAudioManager.Instance"));
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        int methodIndex = source.IndexOf(methodName, System.StringComparison.Ordinal);
        Assert.That(methodIndex, Is.GreaterThanOrEqualTo(0), $"{methodName} is missing.");

        int bodyStart = source.IndexOf('{', methodIndex);
        Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"{methodName} has no body.");

        int depth = 0;

        for (int index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return source.Substring(bodyStart, index - bodyStart + 1);
                }
            }
        }

        Assert.Fail($"{methodName} body was not closed.");
        return string.Empty;
    }
}

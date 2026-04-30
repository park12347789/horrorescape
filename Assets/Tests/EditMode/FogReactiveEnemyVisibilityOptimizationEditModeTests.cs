using System.IO;

using NUnit.Framework;

public sealed class FogReactiveEnemyVisibilityOptimizationEditModeTests
{
    private const string FogReactiveVisibilitySourcePath = "Assets/Scripts/Objectives/FogReactiveVisibility.cs";

    [Test]
    public void FogReactiveEnemyVisibility_DisablesHiddenVisionVisualizerMeshRendererUnlessThreatConeIsShown()
    {
        string source = File.ReadAllText(FogReactiveVisibilitySourcePath);

        Assert.That(source, Does.Contain("bool visualizerTargetEnabled = isVisible || showThreatVisionCone;"));
        Assert.That(source, Does.Contain("meshRenderer.enabled = visualizerTargetEnabled;"));
        Assert.That(source, Does.Not.Contain("meshRenderer.enabled = true;"));
    }

    [Test]
    public void FogReactiveEnemyVisibility_StaggersInitialVisibilityRefreshAcrossInstances()
    {
        string source = File.ReadAllText(FogReactiveVisibilitySourcePath);

        Assert.That(source, Does.Contain("private const float VisibilityRefreshPhaseSpread"));
        Assert.That(source, Does.Contain("private bool staggerNextVisibilityRefresh = true;"));
        Assert.That(source, Does.Contain("EnsureVisibilityRefreshPhase() * VisibilityRefreshPhaseSpread"));
        Assert.That(source, Does.Contain("staggerNextVisibilityRefresh = false;"));
    }

    [Test]
    public void FogReactiveEnemyVisibility_ReusesRendererDiscoveryBuffers()
    {
        string source = File.ReadAllText(FogReactiveVisibilitySourcePath);

        Assert.That(source, Does.Contain("private readonly List<SpriteRenderer> controlledSpriteRendererCache"));
        Assert.That(source, Does.Contain("private readonly List<EnemyVisionVisualizer> visionVisualizerScratch"));
        Assert.That(source, Does.Contain("GetComponentsInChildren(true, spriteRendererScratch);"));
        Assert.That(source, Does.Contain("GetComponentsInChildren(true, visionVisualizerScratch);"));
        Assert.That(source, Does.Contain("GetComponents(behaviourScratch);"));
        Assert.That(source, Does.Not.Contain("controlledSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);"));
        Assert.That(source, Does.Not.Contain("EnemyVisionVisualizer[] visualizers = GetComponentsInChildren<EnemyVisionVisualizer>(true);"));
        Assert.That(source, Does.Not.Contain("controlledMeshRenderers = new MeshRenderer[visualizers.Length];"));
    }

    [Test]
    public void FogReactiveEnemyVisibility_FiltersPointLightCacheByOwnerScene()
    {
        string source = File.ReadAllText(FogReactiveVisibilitySourcePath);

        Assert.That(source, Does.Contain("public static IReadOnlyList<Light2D> GetLights(Scene scene, float refreshInterval)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<Light2D>(scene)"));
        Assert.That(source, Does.Contain("System.Array.Empty<Light2D>()"));
        Assert.That(source, Does.Contain("light.gameObject.scene != scene"));
        Assert.That(source, Does.Contain("RuntimePointLight2DCache.GetLights(gameObject.scene, localLightRefreshInterval)"));
        Assert.That(source, Does.Not.Contain("Object.FindObjectsByType<Light2D>"));
    }
}

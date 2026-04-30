using UnityEngine;
using UnityEngine.SceneManagement;

public interface INoiseEventBus
{
    int RecentEventCount { get; }

    NoiseEventRecord GetRecentEventAt(int index);

    bool TryEmitNoise(
        Vector2 position,
        float radius,
        NoiseSourceType sourceType,
        int emitterInstanceId = 0,
        NoiseEmitterAffiliation emitterAffiliation = NoiseEmitterAffiliation.Neutral,
        bool allowDebugPulse = true);
}

public static class NoiseEventBusResolver
{
    public static INoiseEventBus Resolve(NoiseSystem explicitNoiseSystem)
    {
        return ResolveSystem(default, explicitNoiseSystem);
    }

    public static INoiseEventBus Resolve(Scene scene, NoiseSystem explicitNoiseSystem = null)
    {
        return ResolveSystem(scene, explicitNoiseSystem);
    }

    public static NoiseSystem ResolveSystem(Scene scene, NoiseSystem explicitNoiseSystem = null)
    {
        if (explicitNoiseSystem != null)
        {
            return explicitNoiseSystem;
        }

        return RSceneReferenceLookup.FindFirstComponentInScene<NoiseSystem>(scene);
    }
}

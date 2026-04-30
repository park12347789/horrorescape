/*
 * File Role:
 * Central place for the remaining prototype-scene generator decisions.
 *
 * Runtime Use:
 * Maps a small set of legacy prototype scene names to generation modes and
 * exposes a compatibility seed hook for bootstraps that still call into it.
 */

using System.IO;

using UnityEngine.SceneManagement;

public enum PrototypeGeneratorMode
{
    BaseOffice,
    Wfc,
    Hybrid,
    Bsp,
    OfficeCorridor,
    OfficeMixed,
    OfficeWinged,
    OfficeChained
}

public static class PrototypeSceneUtility
{
    public const string HybridPrototypeSceneName = "Test2";
    public const string BaseOfficeSceneName = "BaseOffice";

    public static PrototypeGeneratorMode GetGeneratorMode(Scene scene)
    {
        if (TryGetSceneSettings(scene, out PrototypeSceneGeneratorSettings settings))
        {
            return settings.GeneratorMode;
        }

        return GetGeneratorMode(BuildLegacySceneKey(scene));
    }

    public static PrototypeGeneratorMode GetGeneratorMode(string sceneName)
    {
        return sceneName switch
        {
            BaseOfficeSceneName => PrototypeGeneratorMode.BaseOffice,
            HybridPrototypeSceneName => PrototypeGeneratorMode.Hybrid,
            _ => PrototypeGeneratorMode.Wfc
        };
    }

    public static bool UseOverviewCamera(Scene scene)
    {
        if (TryGetSceneSettings(scene, out PrototypeSceneGeneratorSettings settings))
        {
            return settings.UseOverviewCamera;
        }

        return GetGeneratorMode(scene) == PrototypeGeneratorMode.BaseOffice;
    }

    public static bool TryConsumeForcedSeed(Scene scene, out int seed)
    {
        // Legacy generator bootstraps still call this hook, but the checked-in showcase
        // scenes that used to push one-off seeds were removed from the project.
        seed = 0;
        return false;
    }

    public static void NoteUsedSeed(Scene scene, int seed)
    {
        // Intentionally left blank. The remaining checked-in runtime no longer reads back
        // showcase seed history, but bootstraps still report their used seed here.
    }

    private static bool TryGetSceneSettings(Scene scene, out PrototypeSceneGeneratorSettings settings)
    {
        settings = scene.IsValid()
            ? RSceneReferenceLookup.FindFirstComponentInScene<PrototypeSceneGeneratorSettings>(scene)
            : null;
        return settings != null;
    }

    private static string BuildLegacySceneKey(Scene scene)
    {
        if (!scene.IsValid())
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(scene.path)
            ? Path.GetFileNameWithoutExtension(scene.path)
            : scene.name;
    }
}

using UnityEngine;

public static class RRunCanonicalAssetLocator
{
    private const string RoutingSettingsResourcePath = "MainEscape/Run/RRunRoutingSettings";
    private const string ProgressionRulesResourcePath = "MainEscape/Run/RRunProgressionRules";
    private const string PlayerDefaultsResourcePath = "MainEscape/Run/RRunPlayerDefaults";

    public static RRunRoutingSettings TryLoadRoutingSettings(Object context = null)
        => TryLoadFromResources<RRunRoutingSettings>(RoutingSettingsResourcePath, context);

    public static RRunProgressionRules TryLoadProgressionRules(Object context = null)
        => TryLoadFromResources<RRunProgressionRules>(ProgressionRulesResourcePath, context);

    public static RRunPlayerDefaults TryLoadPlayerDefaults(Object context = null)
        => TryLoadFromResources<RRunPlayerDefaults>(PlayerDefaultsResourcePath, context);

    private static T TryLoadFromResources<T>(string resourcePath, Object context)
        where T : Object
    {
        T loadedAsset = Resources.Load<T>(resourcePath);

        if (loadedAsset != null)
        {
            return loadedAsset;
        }

        string message = $"Missing canonical {typeof(T).Name} resource at 'Resources/{resourcePath}.asset'.";

        if (context != null)
        {
            Debug.LogError(message, context);
        }
        else
        {
            Debug.LogError(message);
        }

        return null;
    }
}

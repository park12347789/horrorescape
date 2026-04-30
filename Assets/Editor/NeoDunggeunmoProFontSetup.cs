using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

public static class NeoDunggeunmoProFontSetup
{
    private const string SourceFontPath = "Assets/Fonts/NeoDunggeunmoPro/NeoDunggeunmoPro-Regular.ttf";
    private const string FontAssetPath = "Assets/Fonts/NeoDunggeunmoPro/NeoDunggeunmoPro SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string LegacyFallbackFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string AutoApplySessionKey = "NeoDunggeunmoProFontSetup.AutoApplySessionKey";
    private static string ProjectRootPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static readonly string[] TargetScenePaths =
    {
        "Assets/Scenes/RMainEscape_Lobby.unity"
    };

    private static readonly string[] TargetPrefabPaths =
    {
        "Assets/Prefabs/IRHudCanvas.prefab"
    };

    private static readonly string[] SeedContentRoots =
    {
        "Assets/Scripts",
        "Assets/Scenes/RMainEscape_Lobby.unity",
        "Assets/Prefabs/IRHudCanvas.prefab",
        "Assets/Editor/RMainEscapeLobbySceneRebuilder.cs"
    };

    private const string BaseSeedCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        " !?.,:;+-*/=_()[]{}<>#%&@'\"`~|\\^$" +
        "\u2026\u00B7\u203B\u20A9";

    [InitializeOnLoadMethod]
    private static void AutoApplyIfNeeded()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoApplySessionKey, false))
        {
            return;
        }

        if (!File.Exists(GetFullProjectPath(SourceFontPath)) || !NeedsSetup())
        {
            return;
        }

        SessionState.SetBool(AutoApplySessionKey, true);
        EditorApplication.delayCall += TryAutoApply;
    }

    [MenuItem("Tools/Main Escape/Fonts/Apply NeoDunggeunmo Pro")]
    public static void ApplyMenu()
    {
        Apply();
    }

    public static void RunBatch()
    {
        Apply();
    }

    private static void Apply()
    {
        EnsureFontFileExists();

        AssetDatabase.ImportAsset(
            SourceFontPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            throw new FileNotFoundException($"Failed to load source font at '{SourceFontPath}'.");
        }

        TMP_FontAsset legacyFallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LegacyFallbackFontAssetPath);
        TMP_FontAsset fontAsset = EnsureFontAsset(sourceFont, legacyFallbackFont);

        UpdateTmpSettings(fontAsset, legacyFallbackFont);

        int updatedTextComponentCount = 0;

        foreach (string scenePath in TargetScenePaths)
        {
            updatedTextComponentCount += UpdateSceneFontReferences(scenePath, fontAsset);
        }

        foreach (string prefabPath in TargetPrefabPaths)
        {
            updatedTextComponentCount += UpdatePrefabFontReferences(prefabPath, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            FontAssetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);

        Debug.Log(
            $"[NeoDunggeunmoProFontSetup] Applied '{fontAsset.name}' as the TMP default font and updated {updatedTextComponentCount} text components.");
    }

    private static TMP_FontAsset EnsureFontAsset(Font sourceFont, TMP_FontAsset legacyFallbackFont)
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                throw new MissingReferenceException("TextMesh Pro could not create the NeoDunggeunmo Pro font asset.");
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        }

        ConfigureFontAsset(sourceFont, fontAsset, legacyFallbackFont);
        return fontAsset;
    }

    private static void ConfigureFontAsset(Font sourceFont, TMP_FontAsset fontAsset, TMP_FontAsset legacyFallbackFont)
    {
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        fontAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        fontAsset.fallbackFontAssetTable.RemoveAll(asset => asset == null || asset == fontAsset);

        if (legacyFallbackFont != null && !fontAsset.fallbackFontAssetTable.Contains(legacyFallbackFont))
        {
            fontAsset.fallbackFontAssetTable.Add(legacyFallbackFont);
        }

        SerializedObject serializedFontAsset = new(fontAsset);
        serializedFontAsset.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
        serializedFontAsset.FindProperty("m_SourceFontFileGUID").stringValue = AssetDatabase.AssetPathToGUID(SourceFontPath);
        serializedFontAsset.FindProperty("m_GetFontFeatures").boolValue = true;
        serializedFontAsset.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();

        EnsureEmbeddedSubAssets(fontAsset);
        ConfigureFontMaterial(fontAsset);

        string seedCharacters = CollectSeedCharacters();
        if (!string.IsNullOrEmpty(seedCharacters))
        {
            fontAsset.TryAddCharacters(seedCharacters, out _, true);
        }

        EnsureEmbeddedSubAssets(fontAsset);
        ConfigureFontMaterial(fontAsset);

        EditorUtility.SetDirty(fontAsset);

        if (fontAsset.material != null)
        {
            EditorUtility.SetDirty(fontAsset.material);
        }
    }

    private static void UpdateTmpSettings(TMP_FontAsset fontAsset, TMP_FontAsset legacyFallbackFont)
    {
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
        {
            throw new FileNotFoundException($"TMP Settings asset is missing at '{TmpSettingsPath}'.");
        }

        TMP_Settings.defaultFontAsset = fontAsset;

        SerializedObject serializedSettings = new(settings);
        serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;

        SerializedProperty fallbackFontsProperty = serializedSettings.FindProperty("m_fallbackFontAssets");
        List<TMP_FontAsset> fallbacks = new();

        if (legacyFallbackFont != null)
        {
            fallbacks.Add(legacyFallbackFont);
        }

        fallbackFontsProperty.arraySize = fallbacks.Count;
        for (int index = 0; index < fallbacks.Count; index++)
        {
            fallbackFontsProperty.GetArrayElementAtIndex(index).objectReferenceValue = fallbacks[index];
        }

        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static int UpdateSceneFontReferences(string scenePath, TMP_FontAsset fontAsset)
    {
        if (!File.Exists(GetFullProjectPath(scenePath)))
        {
            throw new FileNotFoundException($"Target scene is missing: '{scenePath}'.");
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int updatedTextComponentCount = ApplyFontToHierarchy(scene.GetRootGameObjects(), fontAsset);

        if (updatedTextComponentCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return updatedTextComponentCount;
    }

    private static int UpdatePrefabFontReferences(string prefabPath, TMP_FontAsset fontAsset)
    {
        if (!File.Exists(GetFullProjectPath(prefabPath)))
        {
            throw new FileNotFoundException($"Target prefab is missing: '{prefabPath}'.");
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            int updatedTextComponentCount = ApplyFontToHierarchy(new[] { prefabRoot }, fontAsset);

            if (updatedTextComponentCount > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }

            return updatedTextComponentCount;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static int ApplyFontToHierarchy(IEnumerable<GameObject> roots, TMP_FontAsset fontAsset)
    {
        int updatedTextComponentCount = 0;

        foreach (GameObject root in roots)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                bool changed = false;

                if (text.font != fontAsset)
                {
                    text.font = fontAsset;
                    changed = true;
                }

                if (fontAsset.material != null && text.fontSharedMaterial != fontAsset.material)
                {
                    text.fontSharedMaterial = fontAsset.material;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                text.havePropertiesChanged = true;
                EditorUtility.SetDirty(text);
                updatedTextComponentCount++;
            }
        }

        return updatedTextComponentCount;
    }

    private static void ConfigureFontMaterial(TMP_FontAsset fontAsset)
    {
        if (fontAsset.material == null)
        {
            return;
        }

        Shader shader = Shader.Find("TextMeshPro/Distance Field");
        if (shader != null)
        {
            fontAsset.material.shader = shader;
        }

        Texture2D atlasTexture = fontAsset.atlasTexture;
        if (atlasTexture != null)
        {
            fontAsset.material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
        }

        fontAsset.material.SetFloat(ShaderUtilities.ID_TextureWidth, Mathf.Max(1, fontAsset.atlasWidth));
        fontAsset.material.SetFloat(ShaderUtilities.ID_TextureHeight, Mathf.Max(1, fontAsset.atlasHeight));
        fontAsset.material.SetFloat(ShaderUtilities.ID_GradientScale, fontAsset.atlasPadding + 1);
        fontAsset.material.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
        fontAsset.material.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);
        fontAsset.material.name = $"{fontAsset.name} Material";
    }

    private static void EnsureEmbeddedSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset.material)))
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        if (fontAsset.atlasTextures == null)
        {
            return;
        }

        for (int index = 0; index < fontAsset.atlasTextures.Length; index++)
        {
            Texture2D atlasTexture = fontAsset.atlasTextures[index];
            if (atlasTexture == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlasTexture)))
            {
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }

            atlasTexture.name = index == 0
                ? $"{fontAsset.name} Atlas"
                : $"{fontAsset.name} Atlas {index + 1}";
            EditorUtility.SetDirty(atlasTexture);
        }
    }

    private static string CollectSeedCharacters()
    {
        HashSet<char> characters = new(BaseSeedCharacters);

        foreach (string path in EnumerateSeedFiles())
        {
            foreach (char character in File.ReadAllText(path))
            {
                if (ShouldSeed(character))
                {
                    characters.Add(character);
                }
            }
        }

        return new string(characters.OrderBy(character => character).ToArray());
    }

    private static IEnumerable<string> EnumerateSeedFiles()
    {
        foreach (string path in SeedContentRoots)
        {
            string fullPath = GetFullProjectPath(path);

            if (File.Exists(fullPath))
            {
                yield return fullPath;
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                continue;
            }

            foreach (string filePath in Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(filePath);
                if (extension is ".cs" or ".unity" or ".prefab" or ".asset")
                {
                    yield return filePath;
                }
            }
        }
    }

    private static bool ShouldSeed(char character)
    {
        if (char.IsControl(character))
        {
            return false;
        }

        return character is >= ' ' and <= '~'
            || character is >= '\u1100' and <= '\u11FF'
            || character is >= '\u3130' and <= '\u318F'
            || character is >= '\uAC00' and <= '\uD7A3'
            || character == '\u2026'
            || character == '\u00B7'
            || character == '\u203B'
            || character == '\u20A9';
    }

    private static void EnsureFontFileExists()
    {
        if (!File.Exists(GetFullProjectPath(SourceFontPath)))
        {
            throw new FileNotFoundException(
                $"NeoDunggeunmo Pro TTF is missing at '{SourceFontPath}'. Download the release asset before running the setup.");
        }
    }

    private static bool NeedsSetup()
    {
        TMP_FontAsset configuredFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (configuredFontAsset == null)
        {
            return true;
        }

        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        return settings == null || TMP_Settings.defaultFontAsset != configuredFontAsset;
    }

    private static string GetFullProjectPath(string assetRelativePath)
    {
        string normalizedAssetPath = assetRelativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(ProjectRootPath, normalizedAssetPath));
    }

    private static void TryAutoApply()
    {
        try
        {
            Apply();
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[NeoDunggeunmoProFontSetup] Auto-apply failed: {exception}");
        }
    }
}

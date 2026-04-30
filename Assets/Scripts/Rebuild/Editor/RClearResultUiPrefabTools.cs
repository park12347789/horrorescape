using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RClearResultUiPrefabTools
{
    private const string HudPrefabPath = "Assets/Prefabs/IRHudCanvas.prefab";
    private const string PanelSpritePath = "Assets/Art/UI/ClearResult/ClearResult_PanelFrame.png";
    private const string OkButtonSpritePath = "Assets/Art/UI/ClearResult/ClearResult_OkButton.png";
    private const string FontPath = "Assets/Fonts/NeoDunggeunmoPro/NeoDunggeunmoPro SDF.asset";

    [MenuItem("Tools/Main Escape/Apply Clear Result UI")]
    private static void ApplyClearResultUi()
    {
        Sprite panelSprite = LoadUiSprite(PanelSpritePath);
        Sprite okButtonSprite = LoadUiSprite(OkButtonSpritePath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(HudPrefabPath);

        try
        {
            Transform clearPanel = prefabRoot.transform.Find("IRPanelRoot/IRGameClearPanel");

            if (clearPanel == null)
            {
                Debug.LogError($"Could not find IRGameClearPanel in {HudPrefabPath}.");
                return;
            }

            RectTransform clearPanelRect = clearPanel.GetComponent<RectTransform>();
            ConfigureStretchRect(clearPanelRect);
            clearPanel.gameObject.SetActive(false);

            Image backdropImage = EnsureImage(clearPanel, "Backdrop");
            ConfigureStretchRect(backdropImage.rectTransform);
            backdropImage.color = new Color(0f, 0f, 0f, 0.82f);
            backdropImage.raycastTarget = true;

            Image frameImage = EnsureImage(clearPanel, "Panel");
            RectTransform frameRect = frameImage.rectTransform;
            ConfigureCenteredRect(frameRect, new Vector2(760f, 486f), new Vector2(0f, 32f));
            frameImage.sprite = panelSprite;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;

            RemoveLegacyTextChildren(frameRect);

            TextMeshProUGUI titleText = EnsureText(frameRect, "ClearTitleText", font);
            ConfigureCenteredRect(titleText.rectTransform, new Vector2(620f, 84f), new Vector2(0f, 126f));
            StyleText(titleText, "\uD074\uB9AC\uC5B4", 58f, FontStyles.Bold, new Color(0.96f, 0.91f, 0.72f, 1f));

            TextMeshProUGUI bodyText = EnsureText(frameRect, "ClearBodyText", font);
            ConfigureCenteredRect(bodyText.rectTransform, new Vector2(620f, 72f), new Vector2(0f, 46f));
            StyleText(bodyText, string.Empty, 24f, FontStyles.Normal, new Color(0.89f, 0.84f, 0.7f, 1f));
            bodyText.gameObject.SetActive(false);

            TextMeshProUGUI elapsedText = EnsureText(frameRect, "ElapsedTimeText", font);
            ConfigureCenteredRect(elapsedText.rectTransform, new Vector2(620f, 58f), new Vector2(0f, -18f));
            StyleText(elapsedText, "\uAC78\uB9B0 \uC2DC\uAC04 00:00", 30f, FontStyles.Bold, new Color(0.96f, 0.91f, 0.72f, 1f));

            TextMeshProUGUI promptText = EnsureText(frameRect, "PromptText", font);
            ConfigureCenteredRect(promptText.rectTransform, new Vector2(620f, 34f), new Vector2(0f, -122f));
            StyleText(promptText, string.Empty, 18f, FontStyles.Normal, new Color(0.84f, 0.75f, 0.56f, 0.9f));
            promptText.gameObject.SetActive(false);

            Image okButtonImage = EnsureImage(clearPanel, "OkButton");
            Button okButton = okButtonImage.GetComponent<Button>() ?? okButtonImage.gameObject.AddComponent<Button>();
            RectTransform okButtonRect = okButtonImage.rectTransform;
            ConfigureCenteredRect(okButtonRect, new Vector2(360f, 70f), new Vector2(0f, -282f));
            okButtonImage.sprite = okButtonSprite;
            okButtonImage.type = Image.Type.Simple;
            okButtonImage.preserveAspect = false;
            okButtonImage.color = Color.white;
            okButtonImage.raycastTarget = true;
            okButton.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = okButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.82f, 1f);
            colors.pressedColor = new Color(0.78f, 0.68f, 0.52f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.45f, 0.4f, 0.34f, 0.7f);
            okButton.colors = colors;
            okButton.targetGraphic = okButtonImage;

            IRGameClearPanelView view = clearPanel.GetComponent<IRGameClearPanelView>();

            if (view == null)
            {
                view = clearPanel.gameObject.AddComponent<IRGameClearPanelView>();
            }

            SerializedObject serializedView = new(view);
            SetObjectReference(serializedView, "panelRoot", clearPanelRect);
            SetObjectReference(serializedView, "backdropImage", backdropImage);
            SetObjectReference(serializedView, "panelImage", frameImage);
            SetObjectReference(serializedView, "titleText", titleText);
            SetObjectReference(serializedView, "bodyText", bodyText);
            SetObjectReference(serializedView, "elapsedTimeText", elapsedText);
            SetObjectReference(serializedView, "promptText", promptText);
            SetObjectReference(serializedView, "okButton", okButton);
            SetBool(serializedView, "useAuthoredVisuals", true);
            SetBool(serializedView, "showElapsedTimeOnClear", true);
            SetBool(serializedView, "showing", false);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, HudPrefabPath);
            Debug.Log($"Applied clear result UI to {HudPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static Sprite LoadUiSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
        {
            Debug.LogError($"Could not load UI sprite at {path}.");
        }

        return sprite;
    }

    private static Image EnsureImage(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        if (child == null)
        {
            GameObject childObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        CanvasRenderer canvasRenderer = child.GetComponent<CanvasRenderer>();

        if (canvasRenderer == null)
        {
            child.gameObject.AddComponent<CanvasRenderer>();
        }

        Image image = child.GetComponent<Image>();

        if (image == null)
        {
            image = child.gameObject.AddComponent<Image>();
        }

        child.SetAsLastSibling();
        child.gameObject.SetActive(true);
        return image;
    }

    private static TextMeshProUGUI EnsureText(Transform parent, string name, TMP_FontAsset font)
    {
        Transform child = parent.Find(name);

        if (child == null)
        {
            GameObject childObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();

        if (text == null)
        {
            text = child.gameObject.AddComponent<TextMeshProUGUI>();
        }

        if (font != null)
        {
            text.font = font;
        }

        child.gameObject.SetActive(true);
        return text;
    }

    private static void RemoveLegacyTextChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Transform child = parent.GetChild(index);

            if (child.name == "Text")
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void StyleText(
        TextMeshProUGUI text,
        string value,
        float fontSize,
        FontStyles style,
        Color color)
    {
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    private static void ConfigureStretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureCenteredRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
        {
            property.boolValue = value;
        }
    }
}

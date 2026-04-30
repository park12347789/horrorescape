using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RMainEscapeLobbySceneRebuilder
{
    private const string ScenePath = "Assets/Scenes/RMainEscape_Lobby.unity";
    private const string BackgroundTexturePath = "Assets/Art/UI/Lobby/RMainEscapeLobby_Background.png";
    private const string TitleTexturePath = "Assets/Art/UI/Lobby/RMainEscapeLobby_TitleTrimmed.png";
    private const string RuntimeSettingsResourcePath = "MainEscape/MainEscapeRuntimeSettings";
    private const string RoutingSettingsResourcePath = "MainEscape/Run/RRunRoutingSettings";
    private const string PlayerDefaultsResourcePath = "MainEscape/Run/RRunPlayerDefaults";
    private const string PreviewOutputRelativePath = "Logs/lobby_rebuild_preview.png";
    private const string QueuedCommandRelativePath = "Temp/LobbyRebuildCommand.txt";

    [InitializeOnLoadMethod]
    private static void RegisterQueuedCommandRunner()
    {
        EditorApplication.delayCall += TryExecuteQueuedRebuild;
    }

    [MenuItem("Tools/Main Escape/Rebuild Lobby Scene From Imported References")]
    public static void RebuildLobbySceneFromImportedReferences()
    {
        EnsureReferenceTexturesImported();

        Texture2D backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundTexturePath);
        Texture2D titleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleTexturePath);

        if (backgroundTexture == null || titleTexture == null)
        {
            throw new FileNotFoundException(
                $"Lobby reference textures are missing. Expected '{BackgroundTexturePath}' and '{TitleTexturePath}'.");
        }

        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;

        if (fontAsset == null)
        {
            throw new FileNotFoundException("TMP default font asset is missing. Import TMP essentials before rebuilding the lobby.");
        }

        MainEscapeRuntimeSettings runtimeSettings = Resources.Load<MainEscapeRuntimeSettings>(RuntimeSettingsResourcePath);
        RRunRoutingSettings routingSettings = Resources.Load<RRunRoutingSettings>(RoutingSettingsResourcePath);
        RRunPlayerDefaults playerDefaults = Resources.Load<RRunPlayerDefaults>(PlayerDefaultsResourcePath);

        if (runtimeSettings == null || routingSettings == null || playerDefaults == null)
        {
            throw new FileNotFoundException("Canonical run assets could not be loaded from Resources.");
        }

        Scene scene = LoadOrOpenLobbyScene(out bool sceneWasAlreadyLoaded);
        Scene previousActiveScene = EditorSceneManager.GetActiveScene();
        bool restorePreviousActiveScene = previousActiveScene.IsValid() && previousActiveScene.path != scene.path;

        EditorSceneManager.SetActiveScene(scene);
        ClearScene(scene);

        GameObject lobbyRoot = new("RLobbyRoot");
        GameObject sessionObject = BuildRunSessionController(routingSettings, playerDefaults);
        sessionObject.transform.SetAsLastSibling();

        EventSystem eventSystem = BuildEventSystem(lobbyRoot.transform);
        Camera mainCamera = BuildCamera(lobbyRoot.transform);
        Canvas canvas = BuildCanvas(lobbyRoot.transform, mainCamera);
        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform panelRect = BuildLobbyPanel(canvasRect, runtimeSettings, sessionObject.GetComponent<RRunSessionController>(), fontAsset, backgroundTexture, titleTexture);

        if (panelRect == null || eventSystem == null)
        {
            throw new MissingReferenceException("Failed to create the rebuilt lobby scene hierarchy.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CapturePreview(mainCamera);

        MainEscapeRuntimeValidator.ValidateLobbyScenePreflight();

        if (restorePreviousActiveScene && previousActiveScene.isLoaded)
        {
            EditorSceneManager.SetActiveScene(previousActiveScene);
        }

        if (!sceneWasAlreadyLoaded && scene.isLoaded)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log($"[RMainEscapeLobbySceneRebuilder] Rebuilt '{ScenePath}' and wrote preview to '{GetPreviewOutputPath()}'.");
    }

    public static void RebuildLobbySceneBatch()
    {
        RebuildLobbySceneFromImportedReferences();
    }

    private static void TryExecuteQueuedRebuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryExecuteQueuedRebuild;
            return;
        }

        string commandPath = GetQueuedCommandPath();

        if (!File.Exists(commandPath))
        {
            return;
        }

        File.Delete(commandPath);

        try
        {
            RebuildLobbySceneFromImportedReferences();
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[RMainEscapeLobbySceneRebuilder] Queued rebuild failed.\n{exception}");
        }
    }

    private static void EnsureReferenceTexturesImported()
    {
        ConfigureTextureImporter(BackgroundTexturePath, alphaIsTransparency: false);
        ConfigureTextureImporter(TitleTexturePath, alphaIsTransparency: true);
    }

    private static Scene LoadOrOpenLobbyScene(out bool sceneWasAlreadyLoaded)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        sceneWasAlreadyLoaded = scene.IsValid() && scene.isLoaded;

        if (sceneWasAlreadyLoaded)
        {
            return scene;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    private static void ClearScene(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(rootObject);
        }
    }

    private static void ConfigureTextureImporter(string assetPath, bool alphaIsTransparency)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            throw new FileNotFoundException($"Texture importer could not be resolved for '{assetPath}'.");
        }

        bool changed = false;

        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.alphaIsTransparency != alphaIsTransparency)
        {
            importer.alphaIsTransparency = alphaIsTransparency;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static GameObject BuildRunSessionController(RRunRoutingSettings routingSettings, RRunPlayerDefaults playerDefaults)
    {
        GameObject sessionObject = new("RRunSessionController");
        RRunSessionController sessionController = sessionObject.AddComponent<RRunSessionController>();
        RRunPlayerStateStore playerStateStore = sessionObject.AddComponent<RRunPlayerStateStore>();

        SerializedObject serializedController = new(sessionController);
        serializedController.FindProperty("lobbyScenePath").stringValue = ScenePath;
        serializedController.FindProperty("startingFloorNumber").intValue = 5;
        serializedController.FindProperty("useSceneLocalRoutingOverrides").boolValue = true;
        serializedController.FindProperty("persistAcrossScenes").boolValue = true;
        serializedController.FindProperty("capturePlayerStateContinuously").boolValue = false;
        serializedController.FindProperty("routingSettings").objectReferenceValue = routingSettings;
        serializedController.FindProperty("playerDefaults").objectReferenceValue = playerDefaults;
        serializedController.FindProperty("playerStateStoreSource").objectReferenceValue = playerStateStore;

        SerializedProperty floorScenes = serializedController.FindProperty("floorScenes");
        floorScenes.arraySize = 5;

        for (int index = 0; index < 5; index++)
        {
            SerializedProperty entry = floorScenes.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("floorNumber").intValue = 5 - index;
            entry.FindPropertyRelative("scenePath").stringValue = $"Assets/Scenes/RMainScene_{5 - index}F.unity";
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sessionController);
        EditorUtility.SetDirty(playerStateStore);
        return sessionObject;
    }

    private static EventSystem BuildEventSystem(Transform parent)
    {
        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(parent, false);
        EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
        eventSystem.sendNavigationEvents = true;
        return eventSystem;
    }

    private static Camera BuildCamera(Transform parent)
    {
        GameObject cameraObject = new("RMainCamera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(parent, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 1f);
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        camera.depth = -1f;
        return camera;
    }

    private static Canvas BuildCanvas(Transform parent, Camera worldCamera)
    {
        GameObject canvasObject = new("IRLobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = worldCamera;
        canvas.planeDistance = 10f;
        canvas.sortingOrder = 400;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        return canvas;
    }

    private static RectTransform BuildLobbyPanel(
        RectTransform canvasRect,
        MainEscapeRuntimeSettings runtimeSettings,
        RRunSessionController sessionController,
        TMP_FontAsset fontAsset,
        Texture2D backgroundTexture,
        Texture2D titleTexture)
    {
        BuildBackground(canvasRect, backgroundTexture);
        BuildOverlays(canvasRect);

        RectTransform lobbyPanel = CreateRect("IRLobbyPanel", canvasRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Image panelImage = lobbyPanel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0f);
        panelImage.raycastTarget = false;

        UiSettingsOwner settingsOwner = lobbyPanel.gameObject.AddComponent<UiSettingsOwner>();
        SerializedObject serializedSettingsOwner = new(settingsOwner);
        serializedSettingsOwner.FindProperty("runtimeSettings").objectReferenceValue = runtimeSettings;
        serializedSettingsOwner.ApplyModifiedPropertiesWithoutUndo();

        Button startButton = BuildMenuButton(lobbyPanel, fontAsset, "IRStartRunButton", "PLAY", new Vector2(92f, -404f), true, true);
        CreateMenuLabel(lobbyPanel, fontAsset, "IROptionsLabel", "OPTIONS", new Vector2(92f, -490f), new Color(0.82f, 0.82f, 0.82f, 0.62f));
        CreateMenuLabel(lobbyPanel, fontAsset, "IRCreditsLabel", "CREDITS", new Vector2(92f, -576f), new Color(0.82f, 0.82f, 0.82f, 0.62f));
        Button quitButton = BuildMenuButton(lobbyPanel, fontAsset, "IRQuitButton", "EXIT", new Vector2(92f, -662f), false, false);

        BuildTitleBlock(lobbyPanel, titleTexture);
        BuildObjectivePanel(lobbyPanel, fontAsset);
        BuildVersionLabel(lobbyPanel, fontAsset);

        TextMeshProUGUI summaryTitleText = BuildHiddenBindingText(lobbyPanel, fontAsset, "IRBindingSummaryTitle");
        TextMeshProUGUI summaryBodyText = BuildHiddenBindingText(lobbyPanel, fontAsset, "IRBindingSummaryBody");
        TextMeshProUGUI footerHintText = BuildHiddenBindingText(lobbyPanel, fontAsset, "IRBindingFooterHint");

        IRLobbyController controller = lobbyPanel.gameObject.AddComponent<IRLobbyController>();
        SerializedObject serializedController = new(controller);
        serializedController.FindProperty("uiSettingsReadModelSource").objectReferenceValue = settingsOwner;
        serializedController.FindProperty("runSessionController").objectReferenceValue = sessionController;
        serializedController.FindProperty("startRunButton").objectReferenceValue = startButton;
        serializedController.FindProperty("quitButton").objectReferenceValue = quitButton;
        serializedController.FindProperty("summaryTitleText").objectReferenceValue = summaryTitleText;
        serializedController.FindProperty("summaryBodyText").objectReferenceValue = summaryBodyText;
        serializedController.FindProperty("footerHintText").objectReferenceValue = footerHintText;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        return lobbyPanel;
    }

    private static void BuildBackground(RectTransform canvasRect, Texture2D backgroundTexture)
    {
        RectTransform backgroundRect = CreateRect("IRBackground", canvasRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RawImage backgroundImage = backgroundRect.gameObject.AddComponent<RawImage>();
        backgroundImage.texture = backgroundTexture;
        backgroundImage.raycastTarget = false;
        backgroundImage.color = Color.white;

        AspectRatioFitter fitter = backgroundRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)backgroundTexture.width / backgroundTexture.height;
    }

    private static void BuildOverlays(RectTransform canvasRect)
    {
        CreateColorFill("IRGlobalDim", canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.28f), false);
        CreateColorFill("IRLeftFog", canvasRect, new Vector2(0f, 0f), new Vector2(0.36f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.8f), false);
        CreateColorFill("IRTopVignette", canvasRect, new Vector2(0f, 0.74f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.42f), false);
        CreateColorFill("IRBottomVignette", canvasRect, new Vector2(0f, 0f), new Vector2(1f, 0.16f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.3f), false);
    }

    private static void BuildTitleBlock(RectTransform parent, Texture2D titleTexture)
    {
        float titleWidth = 600f;
        float titleHeight = titleWidth * titleTexture.height / titleTexture.width;
        RectTransform titleRect = CreateRect(
            "IRTitle",
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(74f, -96f),
            new Vector2(titleWidth, titleHeight));

        RawImage titleImage = titleRect.gameObject.AddComponent<RawImage>();
        titleImage.texture = titleTexture;
        titleImage.raycastTarget = false;
        titleImage.color = Color.white;

        RectTransform titleLine = CreateRect(
            "IRTitleLine",
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(74f, -228f),
            new Vector2(720f, 4f));
        Image titleLineImage = titleLine.gameObject.AddComponent<Image>();
        titleLineImage.color = new Color(0.64f, 0.12f, 0.12f, 0.9f);
        titleLineImage.raycastTarget = false;

        RectTransform pulse = CreateRect(
            "IRTitlePulse",
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(506f, -225f),
            new Vector2(100f, 10f));
        TextMeshProUGUI pulseText = pulse.gameObject.AddComponent<TextMeshProUGUI>();
        pulseText.font = TMP_Settings.defaultFontAsset;
        pulseText.text = "-/\\-";
        pulseText.fontSize = 24f;
        pulseText.color = new Color(0.64f, 0.12f, 0.12f, 0.95f);
        pulseText.alignment = TextAlignmentOptions.Center;
        pulseText.raycastTarget = false;
    }

    private static void BuildObjectivePanel(RectTransform parent, TMP_FontAsset fontAsset)
    {
        RectTransform objectiveRect = CreateRect(
            "IRObjectivePanel",
            parent,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-74f, 72f),
            new Vector2(430f, 170f));

        Image panelImage = objectiveRect.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.045f, 0.045f, 0.88f);
        panelImage.raycastTarget = false;

        Outline outline = objectiveRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.2f, 0.16f, 0.82f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI header = CreateText(
            "IRObjectiveHeader",
            objectiveRect,
            fontAsset,
            "OBJECTIVE",
            25f,
            FontStyles.Bold,
            new Color(0.76f, 0.16f, 0.14f, 1f),
            TextAlignmentOptions.Left);
        ConfigureRect(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(-48f, 30f));

        TextMeshProUGUI body = CreateText(
            "IRObjectiveBody",
            objectiveRect,
            fontAsset,
            "Escape the hospital.\nStay unseen and descend through every floor.",
            24f,
            FontStyles.Normal,
            new Color(0.86f, 0.84f, 0.8f, 0.96f),
            TextAlignmentOptions.TopLeft);
        ConfigureRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(24f, -58f), new Vector2(-48f, -74f));
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = -6f;
    }

    private static void BuildVersionLabel(RectTransform parent, TMP_FontAsset fontAsset)
    {
        TextMeshProUGUI version = CreateText(
            "IRVersionLabel",
            parent,
            fontAsset,
            "v1.0.0",
            18f,
            FontStyles.Normal,
            new Color(0.66f, 0.66f, 0.64f, 0.6f),
            TextAlignmentOptions.BottomLeft);
        ConfigureRect(version.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(70f, 52f), new Vector2(160f, 28f));
    }

    private static Button BuildMenuButton(
        RectTransform parent,
        TMP_FontAsset fontAsset,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        bool primary,
        bool addAccent)
    {
        RectTransform buttonRect = CreateRect(
            objectName,
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            anchoredPosition,
            new Vector2(280f, 48f));

        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = primary
            ? new Color(0f, 0f, 0f, 0.01f)
            : new Color(0f, 0f, 0f, 0.01f);

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = buttonImage;

        if (addAccent)
        {
            RectTransform accentRect = CreateRect(
                "IRMenuAccent",
                buttonRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(-22f, 0f),
                new Vector2(10f, 30f));
            Image accentImage = accentRect.gameObject.AddComponent<Image>();
            accentImage.color = new Color(0.78f, 0.18f, 0.18f, 0.95f);
            accentImage.raycastTarget = false;
        }

        TextMeshProUGUI text = CreateText(
            "Label",
            buttonRect,
            fontAsset,
            label,
            33f,
            FontStyles.Bold,
            primary
                ? new Color(0.98f, 0.55f, 0.55f, 0.96f)
                : new Color(0.82f, 0.82f, 0.82f, 0.74f),
            TextAlignmentOptions.Left);
        ConfigureRect(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), Vector2.zero);
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = primary
            ? new Color(0.64f, 0.1f, 0.1f, 0.9f)
            : new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -1f);
        return button;
    }

    private static void CreateMenuLabel(
        RectTransform parent,
        TMP_FontAsset fontAsset,
        string objectName,
        string label,
        Vector2 anchoredPosition,
        Color color)
    {
        TextMeshProUGUI text = CreateText(
            objectName,
            parent,
            fontAsset,
            label,
            30f,
            FontStyles.Bold,
            color,
            TextAlignmentOptions.Left);
        ConfigureRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, new Vector2(280f, 42f));
    }

    private static TextMeshProUGUI BuildHiddenBindingText(RectTransform parent, TMP_FontAsset fontAsset, string objectName)
    {
        TextMeshProUGUI text = CreateText(
            objectName,
            parent,
            fontAsset,
            string.Empty,
            20f,
            FontStyles.Normal,
            new Color(1f, 1f, 1f, 0f),
            TextAlignmentOptions.Left);
        ConfigureRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-4000f, 4000f), new Vector2(4f, 4f));
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateColorFill(
        string objectName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color,
        bool raycastTarget)
    {
        RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        RectTransform parent,
        TMP_FontAsset fontAsset,
        string textValue,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(objectName, parent, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.text = textValue;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(
        string objectName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        return rect;
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private static void CapturePreview(Camera mainCamera)
    {
        if (mainCamera == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        string outputPath = GetPreviewOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);

        RenderTexture renderTexture = new(1920, 1080, 24, RenderTextureFormat.ARGB32);
        Texture2D previewTexture = new(1920, 1080, TextureFormat.RGBA32, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = mainCamera.targetTexture;

        try
        {
            mainCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            mainCamera.Render();
            previewTexture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            previewTexture.Apply();
            File.WriteAllBytes(outputPath, previewTexture.EncodeToPNG());
        }
        finally
        {
            mainCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(previewTexture);
        }
    }

    private static string GetPreviewOutputPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", PreviewOutputRelativePath));
    }

    private static string GetQueuedCommandPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", QueuedCommandRelativePath));
    }
}

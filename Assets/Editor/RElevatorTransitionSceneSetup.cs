using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RElevatorTransitionSceneSetup
{
    public const string ScenePath = "Assets/Scenes/RMainEscape_ElevatorTransition.unity";
    public const string StillImagePath = "Assets/Art/Transitions/ElevatorTransition_Inside_Player.png";
    public const string TempLightOverlayPath = "Assets/Art/Transitions/ElevatorTransition_TempLightOverlay.png";

    [MenuItem("Tools/Main Escape/Setup Elevator Transition Scene")]
    public static void Setup()
    {
        ConfigureStillImageImporter();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        RElevatorTransitionController controller = CreateController(out RectTransform presentationRoot, out CanvasGroup fadeCanvasGroup, out Image flickerOverlay);
        AssignControllerReferences(controller, presentationRoot, fadeCanvasGroup, flickerOverlay);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildScene();
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureStillImageImporter()
    {
        AssetDatabase.ImportAsset(StillImagePath);

        if (AssetImporter.GetAtPath(StillImagePath) is not TextureImporter importer)
        {
            Debug.LogError($"{nameof(RElevatorTransitionSceneSetup)} could not find the transition still image at '{StillImagePath}'.");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";
    }

    private static RElevatorTransitionController CreateController(
        out RectTransform presentationRoot,
        out CanvasGroup fadeCanvasGroup,
        out Image flickerOverlay)
    {
        GameObject controllerObject = new(nameof(RElevatorTransitionController));
        RElevatorTransitionController controller = controllerObject.AddComponent<RElevatorTransitionController>();

        GameObject canvasObject = new("ElevatorTransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateFullScreenImage("BlackBackground", canvasObject.transform, null, Color.black, preserveAspect: false);

        Image stillImage = CreateFullScreenImage(
            "ElevatorTransition_Inside_Player",
            canvasObject.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(StillImagePath),
            Color.white,
            preserveAspect: true);
        presentationRoot = stillImage.rectTransform;

        flickerOverlay = CreateFullScreenImage(
            "ElevatorTransitionFlicker",
            canvasObject.transform,
            AssetDatabase.LoadAssetAtPath<Sprite>(TempLightOverlayPath),
            new Color(1f, 1f, 1f, 0.28f),
            preserveAspect: true);

        Image fadeImage = CreateFullScreenImage(
            "ElevatorTransitionFade",
            canvasObject.transform,
            null,
            Color.black,
            preserveAspect: false);
        fadeCanvasGroup = fadeImage.gameObject.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 1f;

        return controller;
    }

    private static Image CreateFullScreenImage(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        bool preserveAspect)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    private static void AssignControllerReferences(
        RElevatorTransitionController controller,
        RectTransform presentationRoot,
        CanvasGroup fadeCanvasGroup,
        Image flickerOverlay)
    {
        SerializedObject serializedController = new(controller);
        serializedController.FindProperty("presentationRoot").objectReferenceValue = presentationRoot;
        serializedController.FindProperty("fadeCanvasGroup").objectReferenceValue = fadeCanvasGroup;
        serializedController.FindProperty("flickerOverlay").objectReferenceValue = flickerOverlay;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureBuildScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        if (scenes.Any(scene => scene.path == ScenePath))
        {
            for (int index = 0; index < scenes.Length; index++)
            {
                if (scenes[index].path == ScenePath)
                {
                    scenes[index].enabled = true;
                }
            }

            EditorBuildSettings.scenes = scenes;
            return;
        }

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
            .ToArray();
    }
}

using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RShadowStartlePreviewCapture
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private static readonly string[] CandidateScenePaths =
    {
        "Assets/Scenes/RMainScene_2F.unity",
        "Assets/Scenes/RMainScene_4F.unity",
        "Assets/Scenes/RMainScene_3F.unity",
    };

    [MenuItem("Tools/HorrorStealth/Capture Shadow Startle Preview")]
    public static void CaptureDefaultPreviewFromMenu()
    {
        string outputPath = CaptureDefaultPreviewInternal();
        Debug.Log($"[RShadowStartlePreviewCapture] Saved preview to: {outputPath}");
        AssetDatabase.Refresh();
    }

    public static void CaptureDefaultPreview()
    {
        _ = CaptureDefaultPreviewInternal();
    }

    private static string CaptureDefaultPreviewInternal()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string outputDirectory = Path.Combine(projectRoot, "Temp", "ShadowStartlePreview");
        Directory.CreateDirectory(outputDirectory);

        PreviewSceneContext context = OpenFirstPreviewSceneWithCameraAndSpawn();
        GameObject previewPlayer = null;

        try
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (playerPrefab == null)
            {
                throw new System.InvalidOperationException($"Could not load player prefab at '{PlayerPrefabPath}'.");
            }

            previewPlayer = PrefabUtility.InstantiatePrefab(playerPrefab, context.Scene) as GameObject;

            if (previewPlayer == null)
            {
                throw new System.InvalidOperationException($"Could not instantiate player prefab '{PlayerPrefabPath}'.");
            }

            previewPlayer.name = "PreviewPlayer";
            previewPlayer.transform.position = context.SpawnWorldPosition;
            previewPlayer.transform.rotation = Quaternion.identity;

            WasdPlayerController playerController = previewPlayer.GetComponent<WasdPlayerController>();

            if (playerController == null)
            {
                throw new System.InvalidOperationException("Preview player prefab does not contain WasdPlayerController.");
            }

            context.Camera.gameObject.SetActive(true);
            context.Camera.enabled = true;

            string outputPath = Path.Combine(outputDirectory, $"ShadowStartle_{Path.GetFileNameWithoutExtension(context.ScenePath)}.png");
            CaptureScenePreview(playerController, context.Camera, outputPath, context.Scene);
            return outputPath;
        }
        finally
        {
            if (previewPlayer != null)
            {
                Object.DestroyImmediate(previewPlayer);
            }

            if (context.Scene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(context.Scene);
            }
        }
    }

    private static PreviewSceneContext OpenFirstPreviewSceneWithCameraAndSpawn()
    {
        for (int index = 0; index < CandidateScenePaths.Length; index++)
        {
            string scenePath = CandidateScenePaths[index];
            Scene previewScene = EditorSceneManager.OpenPreviewScene(scenePath);
            Camera camera = FindComponentInScene<Camera>(previewScene);

            if (camera == null)
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                continue;
            }

            if (TryResolvePlayerSpawnWorldPosition(previewScene, out Vector3 spawnWorldPosition))
            {
                return new PreviewSceneContext
                {
                    ScenePath = scenePath,
                    Scene = previewScene,
                    Camera = camera,
                    SpawnWorldPosition = spawnWorldPosition
                };
            }

            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        throw new System.InvalidOperationException("Could not find a previewable scene with both camera and player spawn data.");
    }

    private static bool TryResolvePlayerSpawnWorldPosition(Scene scene, out Vector3 spawnWorldPosition)
    {
        MainEscapeFloorAuthoring floorAuthoring = FindComponentInScene<MainEscapeFloorAuthoring>(scene);

        if (floorAuthoring != null && floorAuthoring.TryResolvePlayerStartWorldPosition(out spawnWorldPosition))
        {
            return true;
        }

        Transform playerStart = FindTransformInScene(scene, "PlayerStart");

        if (playerStart != null)
        {
            spawnWorldPosition = playerStart.position;
            return true;
        }

        spawnWorldPosition = Vector3.zero;
        return false;
    }

    private static T FindComponentInScene<T>(Scene scene)
        where T : Component
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            T component = rootObjects[index].GetComponentInChildren<T>(true);

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Transform FindTransformInScene(Scene scene, string transformName)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int index = 0; index < rootObjects.Length; index++)
        {
            Transform match = FindTransformRecursive(rootObjects[index].transform, transformName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindTransformRecursive(Transform root, string transformName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == transformName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform match = FindTransformRecursive(root.GetChild(index), transformName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void CaptureScenePreview(WasdPlayerController playerController, Camera camera, string outputPath, Scene scene)
    {
        Vector3 originalCameraPosition = camera.transform.position;
        float originalOrthographicSize = camera.orthographicSize;
        RenderTexture originalTargetTexture = camera.targetTexture;
        RenderTexture activeRenderTexture = RenderTexture.active;

        GameObject cueObject = null;
        RenderTexture renderTexture = null;
        Texture2D screenshot = null;

        try
        {
            Vector2 forward = playerController.AimDirection.sqrMagnitude > 0.0001f
                ? playerController.AimDirection.normalized
                : Vector2.up;
            Vector2 behind = -forward;
            Vector2 lateral = new(-behind.y, behind.x);
            Vector3 playerPosition = playerController.transform.position;
            Vector3 cuePosition = playerPosition + (Vector3)(behind * 1.2f) + (Vector3)(lateral * 0.12f);

            cueObject = new GameObject("ShadowStartlePreviewCue");
            SceneManager.MoveGameObjectToScene(cueObject, scene);
            RShadowStartleCue cue = cueObject.AddComponent<RShadowStartleCue>();
            cue.Configure(new ShadowStartleCueRequest
            {
                WorldPosition = cuePosition,
                VisualScale = new Vector3(0.65f, 0.65f, 1f),
                FacingDirection = forward,
                DriftDirection = Vector2.zero,
                UseWalkAnimation = false,
                MovementDistance = 0f,
                FadeInDuration = 0.01f,
                HoldDuration = 1.2f,
                FadeOutDuration = 0.4f,
                TargetAlpha = 0.82f,
                SortingLayerName = "Default",
                SortingOrder = 30,
                RevealClip = null,
                RevealClipVolume = 0f
            });

            MethodInfo applySpriteMethod = typeof(RShadowStartleCue).GetMethod(
                "ApplySprite",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (applySpriteMethod == null)
            {
                throw new System.MissingMethodException("RShadowStartleCue.ApplySprite(float) was not found.");
            }

            applySpriteMethod.Invoke(cue, new object[] { 0.2f });

            Vector3 midpoint = (playerPosition + cuePosition) * 0.5f;
            midpoint.z = originalCameraPosition.z;
            camera.transform.position = midpoint;

            if (camera.orthographic)
            {
                camera.orthographicSize = Mathf.Max(3.2f, originalOrthographicSize * 0.85f);
            }

            renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();

            screenshot = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            screenshot.Apply();

            byte[] pngBytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(outputPath, pngBytes);
        }
        finally
        {
            camera.transform.position = originalCameraPosition;
            camera.orthographicSize = originalOrthographicSize;
            camera.targetTexture = originalTargetTexture;
            RenderTexture.active = activeRenderTexture;

            if (screenshot != null)
            {
                Object.DestroyImmediate(screenshot);
            }

            if (renderTexture != null)
            {
                Object.DestroyImmediate(renderTexture);
            }

            if (cueObject != null)
            {
                Object.DestroyImmediate(cueObject);
            }
        }
    }

    private struct PreviewSceneContext
    {
        public string ScenePath;
        public Scene Scene;
        public Camera Camera;
        public Vector3 SpawnWorldPosition;
    }
}

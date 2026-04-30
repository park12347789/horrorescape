using System;
using System.Reflection;

using NUnit.Framework;

using UnityEditor;
using UnityEngine;

public sealed class AuthoredVisibilityLightEditModeTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Test]
    public void TrySampleStrongestReveal_UsesAuthoredWallLight()
    {
        GameObject lightRoot = CreateAuthoredLightRoot();
        Type lightType = FindTypeByName("AuthoredVisibilityLight2D");

        try
        {
            object[] visibleArguments = { new Vector2(0f, -1.6f), 0, 0.04f, 0f };
            bool isVisible = InvokeStaticBool(lightType, "TrySampleStrongestReveal", visibleArguments);

            Assert.That(isVisible, Is.True);
            Assert.That(Convert.ToSingle(visibleArguments[3]), Is.GreaterThan(0.15f));

            object[] occludedArguments = { new Vector2(0f, 1.4f), 0, 0.04f, 0f };
            bool visibleBehindFixture = InvokeStaticBool(lightType, "TrySampleStrongestReveal", occludedArguments);

            Assert.That(visibleBehindFixture, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lightRoot);
        }
    }

    [Test]
    public void VisibilityTarget_ReceivesExposureBoostFromAuthoredLight()
    {
        GameObject lightRoot = CreateAuthoredLightRoot();
        GameObject targetObject = new("VisibilityTarget");

        try
        {
            targetObject.transform.position = new Vector3(0f, -1.2f, 0f);
            Type visibilityTargetType = FindTypeByName("VisibilityTarget2D");
            Component visibilityTarget = targetObject.AddComponent(visibilityTargetType);

            Assert.That(InvokeInstanceFloat(visibilityTarget, "GetExposureMultiplier"), Is.GreaterThan(1.05f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(lightRoot);
        }
    }

    [Test]
    public void FogOverlayVisibility_UsesAuthoredLightReveal()
    {
        GameObject lightRoot = CreateAuthoredLightRoot();
        GameObject overlayObject = new("FogOverlay");

        try
        {
            overlayObject.AddComponent<SpriteRenderer>();
            Type overlayType = FindTypeByName("FlashlightFogOfWarOverlay");
            Component overlay = overlayObject.AddComponent(overlayType);
            MethodInfo tryComputeVisibility = overlayType.GetMethod("TryComputeVisibility", InstanceFlags);

            Assert.That(tryComputeVisibility, Is.Not.Null);

            object[] arguments =
            {
                Vector2.zero,
                Vector2.zero,
                Vector2.up,
                0,
                0f,
                0f,
                0f,
                1f,
                0f,
                0f,
                0f,
                0f,
                1f,
                1f,
                new Vector2(0f, -1.5f),
                0f
            };

            bool isVisible = tryComputeVisibility.Invoke(overlay, arguments) is bool result && result;

            Assert.That(isVisible, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(overlayObject);
            UnityEngine.Object.DestroyImmediate(lightRoot);
        }
    }

    [Test]
    public void SetColorPreset_UpdatesResolvedPresentationColors()
    {
        GameObject lightRoot = CreateAuthoredLightRoot();
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");
        Type presetType = FindTypeByName("AuthoredLightColorPreset");

        try
        {
            Component authoredLight = lightRoot.GetComponent(authoredLightType);
            MethodInfo setColorPreset = authoredLightType.GetMethod("SetColorPreset", InstanceFlags);

            Assert.That(authoredLight, Is.Not.Null);
            Assert.That(presetType, Is.Not.Null);
            Assert.That(setColorPreset, Is.Not.Null);

            object presetValue = Enum.Parse(presetType, "HospitalMint");
            setColorPreset.Invoke(authoredLight, new[] { presetValue });

            Color activeLightColor = InvokeInstanceColorProperty(authoredLight, "ActiveLightColor");
            Color activeFixtureColor = InvokeInstanceColorProperty(authoredLight, "ActiveFixtureColor");

            AssertColorApproximately(activeLightColor, new Color(0.76f, 1f, 0.9f, 1f));
            AssertColorApproximately(activeFixtureColor, new Color(0.18f, 0.24f, 0.22f, 1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lightRoot);
        }
    }

    [Test]
    public void UnlockedLight_UsesCurrentLight2DShapeForReveal()
    {
        GameObject lightRoot = CreateAuthoredLightRoot();
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");

        try
        {
            Component authoredLight = lightRoot.GetComponent(authoredLightType);
            Assert.That(authoredLight, Is.Not.Null);

            SerializedObject serializedLight = new(authoredLight);
            serializedLight.FindProperty("lockLightSourceToAuthoring").boolValue = false;
            serializedLight.ApplyModifiedPropertiesWithoutUndo();

            Component lightComponent = (Component)InvokeInstanceObjectProperty(authoredLight, "LightSource");
            Assert.That(lightComponent, Is.Not.Null);

            SetFloatProperty(lightComponent, "intensity", 1f);
            SetFloatProperty(lightComponent, "pointLightOuterRadius", 0.6f);
            SetFloatProperty(lightComponent, "pointLightInnerRadius", 0.2f);
            SetFloatProperty(lightComponent, "pointLightOuterAngle", 360f);
            SetFloatProperty(lightComponent, "pointLightInnerAngle", 360f);
            InvokeLifecycleMethod(authoredLight, "OnValidate");

            object[] arguments = { new Vector2(0f, -1.2f), 0, 0.04f, 0f };
            bool isVisible = InvokeStaticBool(authoredLightType, "TrySampleStrongestReveal", arguments);

            Assert.That(isVisible, Is.False);
            Assert.That(Convert.ToSingle(arguments[3]), Is.EqualTo(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lightRoot);
        }
    }

    [Test]
    public void UnlockedLight_IntensityReducesRevealStrength()
    {
        GameObject lightRoot = CreateAuthoredLightRoot();
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");

        try
        {
            Component authoredLight = lightRoot.GetComponent(authoredLightType);
            Assert.That(authoredLight, Is.Not.Null);

            SerializedObject serializedLight = new(authoredLight);
            serializedLight.FindProperty("lockLightSourceToAuthoring").boolValue = false;
            serializedLight.ApplyModifiedPropertiesWithoutUndo();

            Component lightComponent = (Component)InvokeInstanceObjectProperty(authoredLight, "LightSource");
            Assert.That(lightComponent, Is.Not.Null);

            SetFloatProperty(lightComponent, "pointLightOuterRadius", 4f);
            SetFloatProperty(lightComponent, "pointLightInnerRadius", 0.4f);
            SetFloatProperty(lightComponent, "pointLightOuterAngle", 360f);
            SetFloatProperty(lightComponent, "pointLightInnerAngle", 360f);

            SetFloatProperty(lightComponent, "intensity", 1f);
            InvokeLifecycleMethod(authoredLight, "OnValidate");

            object[] brightArguments = { new Vector2(0f, -1.2f), 0, 0.04f, 0f };
            bool brightVisible = InvokeStaticBool(authoredLightType, "TrySampleStrongestReveal", brightArguments);
            float brightStrength = Convert.ToSingle(brightArguments[3]);

            SetFloatProperty(lightComponent, "intensity", 0.2f);
            object[] dimArguments = { new Vector2(0f, -1.2f), 0, 0.04f, 0f };
            bool dimVisible = InvokeStaticBool(authoredLightType, "TrySampleStrongestReveal", dimArguments);
            float dimStrength = Convert.ToSingle(dimArguments[3]);

            Assert.That(brightVisible, Is.True);
            Assert.That(dimVisible, Is.True);
            Assert.That(brightStrength, Is.GreaterThan(0.85f));
            Assert.That(dimStrength, Is.LessThan(brightStrength * 0.45f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lightRoot);
        }
    }

    [TestCase(
        "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_RoomBar.prefab",
        "FluorescentCool",
        4f)]
    [TestCase(
        "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_WallLamp.prefab",
        "WarmWhite",
        3f)]
    [TestCase(
        "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_OverheadPool.prefab",
        "HospitalMint",
        5f)]
    public void AuthoredLightPrefab_Defaults_EnableFogReveal(string prefabPath, string expectedPresetName, float minimumOuterRadius)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        Assert.That(prefabAsset, Is.Not.Null, $"Missing light prefab at '{prefabPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");

        try
        {
            Component authoredLight = prefabRoot.GetComponent(authoredLightType);

            Assert.That(authoredLight, Is.Not.Null, $"{prefabPath} is missing AuthoredVisibilityLight2D.");
            Assert.That(InvokeInstanceEnumNameProperty(authoredLight, "ColorPreset"), Is.EqualTo(expectedPresetName));
            Assert.That(InvokeInstanceObjectProperty(authoredLight, "LightSource"), Is.Not.Null, $"{prefabPath} is missing Light2D.");

            SerializedObject serializedLight = new(authoredLight);
            Assert.That(serializedLight.FindProperty("revealInFog").boolValue, Is.True, $"{prefabPath} should reveal fog.");
            Assert.That(serializedLight.FindProperty("affectExposure").boolValue, Is.True, $"{prefabPath} should affect exposure.");
            Assert.That(serializedLight.FindProperty("lockLightSourceToAuthoring").boolValue, Is.False, $"{prefabPath} should allow manual Light2D adjustment.");
            Assert.That(serializedLight.FindProperty("pointLightOuterRadius").floatValue, Is.GreaterThanOrEqualTo(minimumOuterRadius));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void OverheadPoolPrefab_UsesTopDownCookieFromLightPack()
    {
        const string prefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_OverheadPool.prefab";
        const string expectedCookiePath = "Assets/Art/light/light2_alpha/512x512/2/Animation/4.png";

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime");

        try
        {
            Component lightComponent = prefabRoot.GetComponentInChildren(lightType);

            Assert.That(lightType, Is.Not.Null, "Light2D type is unavailable.");
            Assert.That(lightComponent, Is.Not.Null, $"{prefabPath} is missing Light2D.");

            SerializedObject serializedLight = new(lightComponent);
            UnityEngine.Object cookieSprite = serializedLight.FindProperty("m_LightCookieSprite").objectReferenceValue;

            Assert.That(cookieSprite, Is.Not.Null, $"{prefabPath} should use a top-down cookie sprite.");
            Assert.That(AssetDatabase.GetAssetPath(cookieSprite), Is.EqualTo(expectedCookiePath));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void RoomBarPrefab_UsesFreeformDownwardBeamShape()
    {
        const string prefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_RoomBar.prefab";

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime");

        try
        {
            Transform lightTransformRoot = prefabRoot.transform.Find("Light");
            Component lightComponent = lightTransformRoot != null ? lightTransformRoot.GetComponent(lightType) : null;

            Assert.That(lightType, Is.Not.Null, "Light2D type is unavailable.");
            Assert.That(lightComponent, Is.Not.Null, $"{prefabPath} is missing Light2D.");

            SerializedObject serializedLight = new(lightComponent);
            Assert.That(serializedLight.FindProperty("m_LightType").intValue, Is.EqualTo(1), $"{prefabPath} should use a freeform beam light.");
            Assert.That(serializedLight.FindProperty("m_LightCookieSprite").objectReferenceValue, Is.Null, $"{prefabPath} should not keep a circular cookie sprite.");

            Transform lightTransform = lightComponent.transform;
            Assert.That(Vector2.Dot(lightTransform.up, Vector2.down), Is.GreaterThan(0.99f));

            SerializedProperty shapePath = serializedLight.FindProperty("m_ShapePath");
            Assert.That(shapePath.arraySize, Is.GreaterThanOrEqualTo(4), $"{prefabPath} should define a beam polygon.");

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            for (int index = 0; index < shapePath.arraySize; index++)
            {
                Vector3 point = shapePath.GetArrayElementAtIndex(index).vector3Value;
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            float width = maxX - minX;
            float height = maxY - minY;

            Assert.That(minY, Is.GreaterThanOrEqualTo(0f));
            Assert.That(height, Is.GreaterThan(3.5f));
            Assert.That(width, Is.LessThan(2f));
            Assert.That(height, Is.GreaterThan(width * 1.8f));
            Assert.That(lightTransform.localScale.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(lightTransform.localPosition.x, Is.EqualTo(-0.09f).Within(0.001f));
            Assert.That(lightTransform.localPosition.y, Is.EqualTo(0.76f).Within(0.001f));
            Assert.That(serializedLight.FindProperty("m_Intensity").floatValue, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(serializedLight.FindProperty("m_FalloffIntensity").floatValue, Is.EqualTo(0.058f).Within(0.001f));
            Assert.That(serializedLight.FindProperty("m_ShapeLightFalloffSize").floatValue, Is.EqualTo(0.2f).Within(0.001f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void RoomBarPrefab_FreeformReveal_MatchesVisibleBeamFootprint()
    {
        const string prefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_RoomBar.prefab";

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");

        try
        {
            Component authoredLight = prefabRoot.GetComponent(authoredLightType);
            MethodInfo trySampleReveal = authoredLightType.GetMethod("TrySampleReveal", InstanceFlags);

            Assert.That(authoredLight, Is.Not.Null, $"{prefabPath} is missing AuthoredVisibilityLight2D.");
            Assert.That(trySampleReveal, Is.Not.Null, "AuthoredVisibilityLight2D.TrySampleReveal is missing.");

            InvokeLifecycleMethod(authoredLight, "OnValidate");
            InvokeLifecycleMethod(authoredLight, "OnEnable");

            object[] insideArguments = { new Vector2(-0.09f, -0.4f), 0, 0.04f, 0f };
            bool insideVisible = trySampleReveal.Invoke(authoredLight, insideArguments) is bool insideResult && insideResult;
            float insideStrength = Convert.ToSingle(insideArguments[3]);

            object[] outsideArguments = { new Vector2(1.4f, -0.4f), 0, 0.04f, 0f };
            bool outsideVisible = trySampleReveal.Invoke(authoredLight, outsideArguments) is bool outsideResult && outsideResult;
            float outsideStrength = Convert.ToSingle(outsideArguments[3]);

            Assert.That(insideVisible, Is.True);
            Assert.That(insideStrength, Is.GreaterThan(0.95f));
            Assert.That(outsideVisible, Is.False);
            Assert.That(outsideStrength, Is.EqualTo(0f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void RoomBarPrefab_UsesSceneTunedPlateOverlay_ForFixtureFeedback()
    {
        const string prefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_RoomBar.prefab";
        const string expectedPlateSpritePath = "Assets/Art/Environment/Lighting__MainEscape__GuideLight_Core.png";

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");

        try
        {
            Transform fixtureTransform = prefabRoot.transform.Find("Fixture");
            Transform lightPlateTransform = prefabRoot.transform.Find("LightPlate");
            Transform beamTransform = prefabRoot.transform.Find("VisualBeam");
            Transform glowCoreTransform = prefabRoot.transform.Find("GlowCore");
            Transform glowHaloTransform = prefabRoot.transform.Find("GlowHalo");
            SpriteRenderer fixtureRenderer = fixtureTransform != null ? fixtureTransform.GetComponent<SpriteRenderer>() : null;
            SpriteRenderer lightPlateRenderer = lightPlateTransform != null ? lightPlateTransform.GetComponent<SpriteRenderer>() : null;
            SpriteRenderer beamRenderer = beamTransform != null ? beamTransform.GetComponent<SpriteRenderer>() : null;
            SpriteRenderer glowCoreRenderer = glowCoreTransform != null ? glowCoreTransform.GetComponent<SpriteRenderer>() : null;
            SpriteRenderer glowHaloRenderer = glowHaloTransform != null ? glowHaloTransform.GetComponent<SpriteRenderer>() : null;
            Component authoredLight = prefabRoot.GetComponent(authoredLightType);

            Assert.That(fixtureRenderer, Is.Not.Null, $"{prefabPath} should include the fluorescent fixture sprite.");
            Assert.That(lightPlateRenderer, Is.Not.Null, $"{prefabPath} should include a light plate above the fixture.");
            Assert.That(lightPlateRenderer.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(lightPlateRenderer.sprite), Is.EqualTo(expectedPlateSpritePath));
            Assert.That(lightPlateTransform.localPosition.x, Is.EqualTo(fixtureTransform.localPosition.x).Within(0.001f));
            Assert.That(lightPlateTransform.localPosition.y, Is.EqualTo(fixtureTransform.localPosition.y).Within(0.001f));
            Assert.That(lightPlateTransform.localScale.x, Is.GreaterThan(lightPlateTransform.localScale.y * 4f));
            Assert.That(lightPlateRenderer.sortingOrder, Is.GreaterThan(fixtureRenderer.sortingOrder));
            Assert.That(lightPlateRenderer.color.a, Is.GreaterThan(0.6f));
            Assert.That(fixtureTransform.localPosition.x, Is.EqualTo(-0.09f).Within(0.001f));
            Assert.That(fixtureTransform.localPosition.y, Is.EqualTo(0.72f).Within(0.001f));

            Assert.That(beamRenderer, Is.Not.Null);
            Assert.That(glowCoreRenderer, Is.Not.Null);
            Assert.That(glowHaloRenderer, Is.Not.Null);
            Assert.That(beamRenderer.color.a, Is.LessThan(0.001f), $"{prefabPath} should keep the legacy beam sprite hidden by default.");
            Assert.That(glowCoreRenderer.color.a, Is.LessThan(0.001f), $"{prefabPath} should keep the legacy core glow hidden by default.");
            Assert.That(glowHaloRenderer.color.a, Is.LessThan(0.001f), $"{prefabPath} should keep the legacy halo glow hidden by default.");

            SerializedObject authoredSerialized = new(authoredLight);
            SerializedProperty glowRenderers = authoredSerialized.FindProperty("glowRenderers");
            Assert.That(glowRenderers.arraySize, Is.EqualTo(1), $"{prefabPath} should sync the overlay plate color to the active fluorescent light.");
            Assert.That(glowRenderers.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(lightPlateRenderer));

            Type flickerType = FindTypeByName("FluorescentFlicker2D");
            Component flicker = prefabRoot.GetComponent(flickerType);
            Assert.That(flicker, Is.Not.Null, $"{prefabPath} should use the room-bar flicker component.");

            SerializedObject flickerSerialized = new(flicker);
            Assert.That(flickerSerialized.FindProperty("useInstanceSeed").boolValue, Is.True);
            Assert.That(flickerSerialized.FindProperty("lightMultiplierRange").vector2Value.x, Is.GreaterThan(0.48f), $"{prefabPath} should keep fluorescent buzz continuous instead of dropping near-black.");
            Assert.That(flickerSerialized.FindProperty("spriteMultiplierRange").vector2Value.x, Is.GreaterThan(0.42f), $"{prefabPath} should keep the fixture plate visually present during buzz.");

            SerializedProperty visualRenderers = flickerSerialized.FindProperty("visualRenderers");
            Assert.That(visualRenderers.arraySize, Is.EqualTo(1), $"{prefabPath} should flicker the fixture plate overlay.");

            Assert.That(visualRenderers.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(lightPlateRenderer));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void RoomBarPrefab_UsesFlickerComponent_AndOverheadPoolDoesNot()
    {
        const string roomBarPrefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_RoomBar.prefab";
        const string overheadPoolPrefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_OverheadPool.prefab";

        Type flickerType = FindTypeByName("FluorescentFlicker2D");
        Assert.That(flickerType, Is.Not.Null, "FluorescentFlicker2D type is unavailable.");

        GameObject roomBarRoot = PrefabUtility.LoadPrefabContents(roomBarPrefabPath);
        GameObject overheadPoolRoot = PrefabUtility.LoadPrefabContents(overheadPoolPrefabPath);

        try
        {
            Assert.That(roomBarRoot.GetComponent(flickerType), Is.Not.Null, $"{roomBarPrefabPath} should use fluorescent flicker.");
            Assert.That(overheadPoolRoot.GetComponent(flickerType), Is.Null, $"{overheadPoolPrefabPath} should not use fluorescent flicker.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(roomBarRoot);
            PrefabUtility.UnloadPrefabContents(overheadPoolRoot);
        }
    }

    [Test]
    public void RoomBarPrefab_UsesHumAudioComponent_WithMechanicalLoopResource()
    {
        const string roomBarPrefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_RoomBar.prefab";
        const string overheadPoolPrefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_OverheadPool.prefab";
        const string expectedResourcePath = "Audio/Sfx/FlickeringFluorescentLightHum_kentspublicdomain_CC0";

        Type humAudioType = FindTypeByName("RoomLightHumAudio");
        Type flickerType = FindTypeByName("FluorescentFlicker2D");

        Assert.That(humAudioType, Is.Not.Null, "RoomLightHumAudio type is unavailable.");
        Assert.That(flickerType, Is.Not.Null, "FluorescentFlicker2D type is unavailable.");

        GameObject roomBarRoot = PrefabUtility.LoadPrefabContents(roomBarPrefabPath);
        GameObject overheadPoolRoot = PrefabUtility.LoadPrefabContents(overheadPoolPrefabPath);

        try
        {
            Component humAudio = roomBarRoot.GetComponent(humAudioType);
            Component roomBarFlicker = roomBarRoot.GetComponent(flickerType);

            Assert.That(humAudio, Is.Not.Null, $"{roomBarPrefabPath} should include room-light hum audio.");
            Assert.That(overheadPoolRoot.GetComponent(humAudioType), Is.Null, $"{overheadPoolPrefabPath} should not use room-light hum audio.");

            SerializedObject humAudioSerialized = new(humAudio);
            Assert.That(humAudioSerialized.FindProperty("humClipResourcePath").stringValue, Is.EqualTo(expectedResourcePath));
            Assert.That(humAudioSerialized.FindProperty("flicker").objectReferenceValue, Is.EqualTo(roomBarFlicker));
            Assert.That(humAudioSerialized.FindProperty("baseVolume").floatValue, Is.GreaterThan(0.01f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(roomBarRoot);
            PrefabUtility.UnloadPrefabContents(overheadPoolRoot);
        }
    }

    [Test]
    public void FluorescentFlicker_UsesDifferentSeedsForDuplicateInstances()
    {
        Type flickerType = FindTypeByName("FluorescentFlicker2D");
        Assert.That(flickerType, Is.Not.Null, "FluorescentFlicker2D type is unavailable.");

        GameObject firstRoot = new("Flicker_A");
        GameObject secondRoot = new("Flicker_B");

        try
        {
            Component firstFlicker = firstRoot.AddComponent(flickerType);
            Component secondFlicker = secondRoot.AddComponent(flickerType);

            InvokeLifecycleMethod(firstFlicker, "OnEnable");
            InvokeLifecycleMethod(secondFlicker, "OnEnable");

            int firstSeed = Convert.ToInt32(InvokeInstanceObjectProperty(firstFlicker, "RuntimeSeed"));
            int secondSeed = Convert.ToInt32(InvokeInstanceObjectProperty(secondFlicker, "RuntimeSeed"));

            Assert.That(firstSeed, Is.Not.EqualTo(secondSeed));
            Assert.That(Convert.ToBoolean(InvokeInstanceObjectProperty(firstFlicker, "UseInstanceSeed")), Is.True);
            Assert.That(Convert.ToBoolean(InvokeInstanceObjectProperty(secondFlicker, "UseInstanceSeed")), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstRoot);
            UnityEngine.Object.DestroyImmediate(secondRoot);
        }
    }

    [Test]
    public void OverheadPoolPrefab_UsesSet4VisualPoolSpriteThatCanBeMovedAndTinted()
    {
        const string prefabPath = "Assets/Prefabs/Environment/MainEscape/Lighting/MainEscapeLight_OverheadPool.prefab";
        const string expectedPoolSpritePath = "Assets/Art/light/light2_alpha/512x512/2/Animation/4.png";

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Transform poolTransform = prefabRoot.transform.Find("VisualPool");
            SpriteRenderer poolRenderer = poolTransform != null ? poolTransform.GetComponent<SpriteRenderer>() : null;

            Assert.That(poolRenderer, Is.Not.Null, $"{prefabPath} should include a scene-adjustable overhead pool sprite.");
            Assert.That(poolRenderer.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(poolRenderer.sprite), Is.EqualTo(expectedPoolSpritePath));
            Assert.That(poolRenderer.color.a, Is.GreaterThan(0.1f));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static GameObject CreateAuthoredLightRoot()
    {
        GameObject lightRoot = new("AuthoredLight");
        GameObject lightObject = new("Light");
        lightObject.transform.SetParent(lightRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        Component light2D = AddLight2D(lightObject);
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");
        Component authoredLight = lightRoot.AddComponent(authoredLightType);

        Assert.That(light2D, Is.Not.Null);
        Assert.That(authoredLight, Is.Not.Null);
        InvokeLifecycleMethod(authoredLight, "OnValidate");
        InvokeLifecycleMethod(authoredLight, "OnEnable");
        return lightRoot;
    }

    private static Component AddLight2D(GameObject target)
    {
        Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime");
        Assert.That(lightType, Is.Not.Null, "Light2D type is unavailable.");
        return target.AddComponent(lightType);
    }

    private static bool InvokeStaticBool(Type type, string methodName, object[] arguments)
    {
        MethodInfo method = type.GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} is missing.");
        return method.Invoke(null, arguments) is bool result && result;
    }

    private static float InvokeInstanceFloat(Component component, string methodName)
    {
        MethodInfo method = component.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{component.GetType().Name}.{methodName} is missing.");
        return Convert.ToSingle(method.Invoke(component, null));
    }

    private static Color InvokeInstanceColorProperty(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.{propertyName} is missing.");
        return (Color)property.GetValue(component);
    }

    private static string InvokeInstanceEnumNameProperty(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.{propertyName} is missing.");
        object value = property.GetValue(component);
        return value?.ToString();
    }

    private static object InvokeInstanceObjectProperty(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(component);
    }

    private static void InvokeLifecycleMethod(Component component, string methodName)
    {
        MethodInfo method = component.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{component.GetType().Name}.{methodName} is missing.");
        method.Invoke(component, null);
    }

    private static void SetFloatProperty(Component component, string propertyName, float value)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.{propertyName} is missing.");
        property.SetValue(component, value);
    }

    private static void AssertColorApproximately(Color actual, Color expected)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }

    private static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int index = 0; index < assemblies.Length; index++)
        {
            Type found = assemblies[index].GetType(typeName, false);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

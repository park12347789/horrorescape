using System;
using System.Collections.Generic;
using System.Reflection;

using NUnit.Framework;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;

public sealed class MainEscapeFogOverlaySceneConfigurationEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const float ExpectedFloorSceneLight2DIntensity = 0.35f;

    private static readonly FogOverlayExpectation[] Expectations =
    {
        new("Assets/Scenes/RMainScene_5F.unity", 4, 2, 2, 2, false, 0.9f, 0.9f, 0.12f, 0.02f, 0.08f),
        new("Assets/Scenes/RMainScene_4F.unity", 4, 2, 2, 2, true, 0.9f, 0.9f, 0.12f, 0.02f, 0.08f),
        new("Assets/Scenes/RMainScene_3F.unity", 4, 2, 2, 2, true, 0.9f, 0.9f, 0.12f, 0.02f, 0.08f),
        new("Assets/Scenes/RMainScene_2F.unity", 4, 2, 2, 2, true, 0.9f, 0.9f, 0.12f, 0.02f, 0.08f),
        new("Assets/Scenes/RMainScene_1F.unity", 4, 2, 2, 2, true, 0.9f, 0.9f, 0.12f, 0.02f, 0.08f)
    };

    [Test]
    public void RuntimeFloorScenes_FogOverlayProfile_MatchesStabilizedBaseline()
    {
        Type overlayType = FindTypeByName("FlashlightFogOfWarOverlay");
        Assert.That(overlayType, Is.Not.Null, "FlashlightFogOfWarOverlay type is missing.");
        List<string> failures = new();

        for (int index = 0; index < Expectations.Length; index++)
        {
            FogOverlayExpectation expected = Expectations[index];
            Component overlay = OpenSceneAndFindSingleComponent(expected.ScenePath, overlayType);
            SerializedObject serializedOverlay = new(overlay);

            CheckEqual(ReadInt(serializedOverlay, "pixelsPerUnit"), expected.PixelsPerUnit, $"{expected.ScenePath} pixelsPerUnit changed.", failures);
            CheckEqual(ReadInt(serializedOverlay, "interlacedUpdateGroups"), expected.InterlacedUpdateGroups, $"{expected.ScenePath} interlacedUpdateGroups changed.", failures);
            CheckEqual(ReadInt(serializedOverlay, "movingSampleStride"), expected.MovingSampleStride, $"{expected.ScenePath} movingSampleStride changed.", failures);
            CheckEqual(ReadInt(serializedOverlay, "idleSampleStride"), expected.IdleSampleStride, $"{expected.ScenePath} idleSampleStride changed.", failures);
            CheckEqual(ReadBool(serializedOverlay, "bakeAuthoredLightVisibilityOnReset"), expected.BakeAuthoredLightVisibilityOnReset, $"{expected.ScenePath} bakeAuthoredLightVisibilityOnReset changed.", failures);
            CheckApproximately(ReadFloat(serializedOverlay, "playerComfortRevealRadius"), expected.PlayerComfortRevealRadius, 0.001f, $"{expected.ScenePath} playerComfortRevealRadius changed.", failures);
            CheckApproximately(ReadFloat(serializedOverlay, "minimumRuntimeComfortRevealRadius"), expected.MinimumRuntimeComfortRevealRadius, 0.001f, $"{expected.ScenePath} minimumRuntimeComfortRevealRadius changed.", failures);
            CheckApproximately(ReadFloat(serializedOverlay, "edgeSoftness"), expected.EdgeSoftness, 0.001f, $"{expected.ScenePath} edgeSoftness changed.", failures);
            CheckApproximately(ReadFloat(serializedOverlay, "movingRefreshInterval"), expected.MovingRefreshInterval, 0.001f, $"{expected.ScenePath} movingRefreshInterval changed.", failures);
            CheckApproximately(ReadFloat(serializedOverlay, "idleRefreshInterval"), expected.IdleRefreshInterval, 0.001f, $"{expected.ScenePath} idleRefreshInterval changed.", failures);
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void RuntimeFloorScenes_GlobalLight2DIntensity_MatchesStabilizedBaseline()
    {
        Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime");

        Assert.That(lightType, Is.Not.Null, "Light2D type is unavailable.");
        List<string> failures = new();

        for (int index = 0; index < Expectations.Length; index++)
        {
            FogOverlayExpectation expected = Expectations[index];
            EditorSceneManager.OpenScene(expected.ScenePath, OpenSceneMode.Single);
            List<Component> lights = FindGlobalLightComponents(lightType, expected.ScenePath);

            if (lights.Count != 1)
            {
                failures.Add($"{expected.ScenePath} should have exactly one scene Global Light2D, actual: {lights.Count}.");
                continue;
            }

            SerializedObject serializedLight = new(lights[0]);
            CheckApproximately(
                ReadFloat(serializedLight, "m_Intensity"),
                ExpectedFloorSceneLight2DIntensity,
                0.001f,
                $"{expected.ScenePath} Light2D intensity changed.",
                failures);
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    [Test]
    public void RuntimeFloorScenes_BakedFogFloors_HaveAuthoredVisibilityLightsToBake()
    {
        Type overlayType = FindTypeByName("FlashlightFogOfWarOverlay");
        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");

        Assert.That(overlayType, Is.Not.Null, "FlashlightFogOfWarOverlay type is missing.");
        Assert.That(authoredLightType, Is.Not.Null, "AuthoredVisibilityLight2D type is missing.");
        List<string> failures = new();

        for (int index = 0; index < Expectations.Length; index++)
        {
            FogOverlayExpectation expected = Expectations[index];
            Component overlay = OpenSceneAndFindSingleComponent(expected.ScenePath, overlayType);
            SerializedObject serializedOverlay = new(overlay);
            bool bakeEnabled = ReadBool(serializedOverlay, "bakeAuthoredLightVisibilityOnReset");

            if (!bakeEnabled)
            {
                continue;
            }

            int authoredLightCount = FindSceneComponents(authoredLightType, expected.ScenePath).Count;

            if (authoredLightCount <= 0)
            {
                failures.Add($"{expected.ScenePath} enables authored-light fog baking but has no AuthoredVisibilityLight2D instances.");
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    private static Component OpenSceneAndFindSingleComponent(string scenePath, Type componentType)
    {
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
            Is.Not.Null,
            $"Scene asset '{scenePath}' is missing.");

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        List<Component> components = FindSceneComponents(componentType, scenePath);

        Assert.That(components, Has.Count.EqualTo(1), $"{scenePath} should have exactly one {componentType.Name}.");
        return components[0];
    }

    private static List<Component> FindSceneComponents(Type componentType, string scenePath)
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Component> components = new();

        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour == null
                || !componentType.IsAssignableFrom(behaviour.GetType())
                || behaviour.gameObject.scene.path != scenePath)
            {
                continue;
            }

            components.Add(behaviour);
        }

        return components;
    }

    private static List<Component> FindGlobalLightComponents(Type lightType, string scenePath)
    {
        List<Component> lights = FindSceneComponents(lightType, scenePath);
        List<Component> globalLights = new();

        for (int index = 0; index < lights.Count; index++)
        {
            SerializedObject serializedLight = new(lights[index]);
            SerializedProperty lightTypeProperty = serializedLight.FindProperty("m_LightType");

            if (lightTypeProperty != null && lightTypeProperty.intValue == 4)
            {
                globalLights.Add(lights[index]);
            }
        }

        return globalLights;
    }

    private static int ReadInt(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"Missing serialized int property '{propertyName}'.");
        return property.intValue;
    }

    private static float ReadFloat(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"Missing serialized float property '{propertyName}'.");
        return property.floatValue;
    }

    private static bool ReadBool(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"Missing serialized bool property '{propertyName}'.");
        return property.boolValue;
    }

    private static void CheckEqual<T>(T actual, T expected, string message, List<string> failures)
    {
        if (!Equals(actual, expected))
        {
            failures.Add($"{message} Expected: {expected}, actual: {actual}.");
        }
    }

    private static void CheckApproximately(float actual, float expected, float tolerance, string message, List<string> failures)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
        {
            failures.Add($"{message} Expected: {expected:0.###}, actual: {actual:0.###}.");
        }
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

    private readonly struct FogOverlayExpectation
    {
        public FogOverlayExpectation(
            string scenePath,
            int pixelsPerUnit,
            int interlacedUpdateGroups,
            int movingSampleStride,
            int idleSampleStride,
            bool bakeAuthoredLightVisibilityOnReset,
            float playerComfortRevealRadius,
            float minimumRuntimeComfortRevealRadius,
            float edgeSoftness,
            float movingRefreshInterval,
            float idleRefreshInterval)
        {
            ScenePath = scenePath;
            PixelsPerUnit = pixelsPerUnit;
            InterlacedUpdateGroups = interlacedUpdateGroups;
            MovingSampleStride = movingSampleStride;
            IdleSampleStride = idleSampleStride;
            BakeAuthoredLightVisibilityOnReset = bakeAuthoredLightVisibilityOnReset;
            PlayerComfortRevealRadius = playerComfortRevealRadius;
            MinimumRuntimeComfortRevealRadius = minimumRuntimeComfortRevealRadius;
            EdgeSoftness = edgeSoftness;
            MovingRefreshInterval = movingRefreshInterval;
            IdleRefreshInterval = idleRefreshInterval;
        }

        public string ScenePath { get; }
        public int PixelsPerUnit { get; }
        public int InterlacedUpdateGroups { get; }
        public int MovingSampleStride { get; }
        public int IdleSampleStride { get; }
        public bool BakeAuthoredLightVisibilityOnReset { get; }
        public float PlayerComfortRevealRadius { get; }
        public float MinimumRuntimeComfortRevealRadius { get; }
        public float EdgeSoftness { get; }
        public float MovingRefreshInterval { get; }
        public float IdleRefreshInterval { get; }
    }
}

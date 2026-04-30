using System;
using System.Reflection;

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RouteGraphFloorRouteAdapterEditModeTests
{
    private const string HospitalChapterAssetPath = "Assets/Data/MainEscape/Contracts/HospitalChapter.asset";
    private const string HospitalRouteGraphAssetPath = "Assets/Data/MainEscape/Contracts/HospitalRouteGraph.asset";
    private const string RoutingSettingsAssetPath = "Assets/Resources/MainEscape/Run/RRunRoutingSettings.asset";

    [Test]
    public void HospitalChapterAsset_ReferencesValidRouteGraph()
    {
        UnityEngine.Object chapter = LoadAsset("ChapterDefinition", HospitalChapterAssetPath);
        UnityEngine.Object routeGraph = LoadAsset("RouteGraphDefinition", HospitalRouteGraphAssetPath);

        Assert.That(ReadProperty(chapter, "ChapterId"), Is.EqualTo("hospital"));
        Assert.That(ReadProperty(chapter, "StartRouteGraph"), Is.SameAs(routeGraph));
        Assert.That(ReadProperty<bool>(chapter, "IsValid"), Is.True);
        Assert.That(ReadProperty(routeGraph, "RouteGraphId"), Is.EqualTo("hospital_default"));
        Assert.That(ReadProperty(routeGraph, "StartNodeId"), Is.EqualTo("hospital_5f"));
        Assert.That(ReadProperty<bool>(routeGraph, "IsValid"), Is.True);
        Assert.That(TryValidateRouteGraph(routeGraph, out Array errors), Is.True);
        Assert.That(errors, Is.Empty);
        Assert.That(TryValidateHospitalRouteGraph(chapter, routeGraph, out Array hospitalErrors), Is.True);
        Assert.That(hospitalErrors, Is.Empty);
    }

    [Test]
    public void CanonicalRoutingSettings_ReferencesHospitalChapterAsset()
    {
        UnityEngine.Object routingSettings = LoadAsset("RRunRoutingSettings", RoutingSettingsAssetPath);
        UnityEngine.Object chapter = LoadAsset("ChapterDefinition", HospitalChapterAssetPath);

        Assert.That(ReadProperty(routingSettings, "DefaultChapter"), Is.SameAs(chapter));
        Assert.That(ReadProperty(routingSettings, "DefaultRouteGraph"), Is.SameAs(ReadProperty(chapter, "StartRouteGraph")));
    }

    [Test]
    public void TryBuildFloorSceneEntries_UsesHospitalChapterAssetNodes()
    {
        UnityEngine.Object chapter = LoadAsset("ChapterDefinition", HospitalChapterAssetPath);

        Assert.That(TryBuildFloorSceneEntries(chapter, out Array floorScenes), Is.True);
        Assert.That(floorScenes, Has.Length.EqualTo(5));
        Assert.That(ReadField(floorScenes.GetValue(0), "floorNumber"), Is.EqualTo(5));
        Assert.That(ReadField(floorScenes.GetValue(0), "scenePath"), Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        Assert.That(ReadField(floorScenes.GetValue(4), "floorNumber"), Is.EqualTo(1));
    }

    [Test]
    public void TryResolveNextFloorNumber_UsesRouteGraphEdges()
    {
        UnityEngine.Object chapter = LoadAsset("ChapterDefinition", HospitalChapterAssetPath);

        AssertNextFloor(chapter, 5, 4);
        AssertNextFloor(chapter, 4, 3);
        AssertNextFloor(chapter, 3, 2);
        AssertNextFloor(chapter, 2, 1);
        Assert.That(TryResolveNextFloorNumber(chapter, 1, out _), Is.False);
    }

    [Test]
    public void HospitalRouteGraphAsset_ScenePathsExistInBuildSettings()
    {
        UnityEngine.Object routeGraph = LoadAsset("RouteGraphDefinition", HospitalRouteGraphAssetPath);
        Array sceneNodes = ReadProperty(routeGraph, "SceneNodes") as Array;

        Assert.That(sceneNodes, Is.Not.Null);

        for (int index = 0; index < sceneNodes.Length; index++)
        {
            string scenePath = ReadField(sceneNodes.GetValue(index), "scenePath") as string;
            Assert.That(IsEnabledBuildScene(scenePath), Is.True, $"{scenePath} is not enabled in Build Settings.");
        }
    }

    [Test]
    public void RouteGraphDefinitionValidator_RejectsDuplicateNodesScenePathsAndMissingTargets()
    {
        ScriptableObject routeGraph = MainEscapeReflectionTestHelper.CreateScriptableObject("RouteGraphDefinition");
        SerializedObject serializedGraph = new(routeGraph);
        serializedGraph.FindProperty("routeGraphId").stringValue = "invalid_test";
        serializedGraph.FindProperty("startNodeId").stringValue = "start";

        SerializedProperty nodes = serializedGraph.FindProperty("sceneNodes");
        nodes.arraySize = 3;
        SetNode(nodes.GetArrayElementAtIndex(0), "start", "Assets/Scenes/RMainScene_5F.unity");
        SetNode(nodes.GetArrayElementAtIndex(1), "start", "Assets/Scenes/RMainScene_4F.unity");
        SetNode(nodes.GetArrayElementAtIndex(2), "other", "Assets/Scenes/RMainScene_5F.unity");

        SerializedProperty edges = serializedGraph.FindProperty("edges");
        edges.arraySize = 1;
        SetEdge(edges.GetArrayElementAtIndex(0), "start", "descent", "missing_target");
        serializedGraph.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            Assert.That(TryValidateRouteGraph(routeGraph, out Array errors), Is.False);
            Assert.That(errors.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(ContainsError(errors, "duplicated"), Is.True);
            Assert.That(ContainsError(errors, "Scene path"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(routeGraph);
        }
    }

    private static UnityEngine.Object LoadAsset(string typeName, string assetPath)
    {
        Type type = MainEscapeReflectionTestHelper.RequireType(typeName);
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(assetPath, type);
        Assert.That(asset, Is.Not.Null, $"{assetPath} could not be loaded as {typeName}.");
        return asset;
    }

    private static bool TryBuildFloorSceneEntries(UnityEngine.Object chapter, out Array floorScenes)
    {
        Type adapterType = MainEscapeReflectionTestHelper.RequireType("RouteGraphFloorRouteAdapter");
        MethodInfo method = adapterType.GetMethod("TryBuildFloorSceneEntries", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "RouteGraphFloorRouteAdapter.TryBuildFloorSceneEntries is missing.");
        object[] arguments = { chapter, null };
        bool resolved = method.Invoke(null, arguments) is bool value && value;
        floorScenes = arguments[1] as Array;
        return resolved;
    }

    private static bool TryResolveNextFloorNumber(UnityEngine.Object chapter, int currentFloorNumber, out int destinationFloorNumber)
    {
        Type adapterType = MainEscapeReflectionTestHelper.RequireType("RouteGraphFloorRouteAdapter");
        MethodInfo method = adapterType.GetMethod("TryResolveNextFloorNumber", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "RouteGraphFloorRouteAdapter.TryResolveNextFloorNumber is missing.");
        object[] arguments = { chapter, currentFloorNumber, "descent", 1337, null };
        bool resolved = method.Invoke(null, arguments) is bool value && value;
        destinationFloorNumber = arguments[4] is int floorNumber ? floorNumber : 0;
        return resolved;
    }

    private static void AssertNextFloor(UnityEngine.Object chapter, int currentFloorNumber, int expectedDestinationFloorNumber)
    {
        Assert.That(TryResolveNextFloorNumber(chapter, currentFloorNumber, out int destinationFloorNumber), Is.True);
        Assert.That(destinationFloorNumber, Is.EqualTo(expectedDestinationFloorNumber));
    }

    private static bool TryValidateRouteGraph(UnityEngine.Object routeGraph, out Array errors)
    {
        Type validatorType = MainEscapeReflectionTestHelper.RequireType("RouteGraphDefinitionValidator");
        MethodInfo method = validatorType.GetMethod("TryValidate", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "RouteGraphDefinitionValidator.TryValidate is missing.");
        object[] arguments = { routeGraph, null };
        bool valid = method.Invoke(null, arguments) is bool value && value;
        errors = arguments[1] as Array;
        return valid;
    }

    private static bool TryValidateHospitalRouteGraph(UnityEngine.Object chapter, UnityEngine.Object routeGraph, out Array errors)
    {
        Type validatorType = MainEscapeReflectionTestHelper.RequireType("HospitalRouteGraphDefinitionValidator");
        MethodInfo method = validatorType.GetMethod("TryValidateHospitalChain", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "HospitalRouteGraphDefinitionValidator.TryValidateHospitalChain is missing.");
        object[] arguments = { chapter, routeGraph, null };
        bool valid = method.Invoke(null, arguments) is bool value && value;
        errors = arguments[2] as Array;
        return valid;
    }

    private static bool ContainsError(Array errors, string text)
    {
        for (int index = 0; index < errors.Length; index++)
        {
            if (errors.GetValue(index) is string error
                && error.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnabledBuildScene(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        for (int index = 0; index < scenes.Length; index++)
        {
            if (scenes[index].enabled && string.Equals(scenes[index].path, scenePath, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static T ReadProperty<T>(object owner, string propertyName)
    {
        return (T)ReadProperty(owner, propertyName);
    }

    private static object ReadProperty(object owner, string propertyName)
    {
        return MainEscapeReflectionTestHelper.GetPropertyValue(owner, propertyName);
    }

    private static object ReadField(object owner, string fieldName)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{fieldName} is missing.");
        return field.GetValue(owner);
    }

    private static void SetNode(SerializedProperty property, string nodeId, string scenePath)
    {
        property.FindPropertyRelative("nodeId").stringValue = nodeId;
        property.FindPropertyRelative("scenePath").stringValue = scenePath;
        property.FindPropertyRelative("kind").enumValueIndex = 0;
        property.FindPropertyRelative("chapterLocalLevelId").stringValue = nodeId;
    }

    private static void SetEdge(SerializedProperty property, string fromNodeId, string exitId, string toNodeId)
    {
        property.FindPropertyRelative("fromNodeId").stringValue = fromNodeId;
        property.FindPropertyRelative("exitId").stringValue = exitId;
        property.FindPropertyRelative("toNodeId").stringValue = toNodeId;
        property.FindPropertyRelative("policy").enumValueIndex = 0;
        property.FindPropertyRelative("weight").floatValue = 1f;
    }
}

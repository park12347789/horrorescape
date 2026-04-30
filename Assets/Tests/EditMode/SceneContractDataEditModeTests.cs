using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SceneContractDataEditModeTests
{
    [Test]
    public void RouteGraph_AllowsBranchingRandomAndLoopEdges()
    {
        ScriptableObject graph = MainEscapeReflectionTestHelper.CreateScriptableObject("RouteGraphDefinition");
        SerializedObject graphObject = new(graph);
        graphObject.FindProperty("routeGraphId").stringValue = "backrooms_test";
        graphObject.FindProperty("startNodeId").stringValue = "yellow_start";

        SerializedProperty nodes = graphObject.FindProperty("sceneNodes");
        nodes.arraySize = 3;
        SetNode(nodes.GetArrayElementAtIndex(0), "yellow_start", "Assets/Scenes/Backrooms_Yellow.unity");
        SetNode(nodes.GetArrayElementAtIndex(1), "pool_branch", "Assets/Scenes/Backrooms_Pool.unity");
        SetNode(nodes.GetArrayElementAtIndex(2), "yellow_loop", "Assets/Scenes/Backrooms_YellowLoop.unity");

        SerializedProperty edges = graphObject.FindProperty("edges");
        edges.arraySize = 3;
        SetEdge(edges.GetArrayElementAtIndex(0), "yellow_start", "left_exit", "pool_branch", "Choice");
        SetEdge(edges.GetArrayElementAtIndex(1), "yellow_start", "random_exit", "yellow_loop", "WeightedRandom");
        SetEdge(edges.GetArrayElementAtIndex(2), "yellow_loop", "back", "yellow_start", "Loop");
        graphObject.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(MainEscapeReflectionTestHelper.GetPropertyValue<bool>(graph, "IsValid"), Is.True);

        Array startEdges = InvokeArray(graph, "GetEdgesFrom", "yellow_start");
        Array loopEdges = InvokeArray(graph, "GetEdgesFrom", "yellow_loop");
        Assert.That(startEdges, Has.Length.EqualTo(2));
        Assert.That(ReadField(loopEdges.GetValue(0), "policy").ToString(), Is.EqualTo("Loop"));

        UnityEngine.Object.DestroyImmediate(graph);
    }

    [Test]
    public void SceneEntryContext_RequiresRouteIdentityButAllowsTestDefaults()
    {
        Type contextType = MainEscapeReflectionTestHelper.RequireType("SceneEntryContext");
        object context = Activator.CreateInstance(contextType);

        SetField(context, "gameModeId", "main_escape");
        SetField(context, "chapterId", "hospital");
        SetField(context, "routeGraphId", "hospital_default");
        SetField(context, "sceneNodeId", "hospital_5f");
        SetField(context, "runId", "test_run");
        SetField(context, "runSeed", 1337);
        SetField(context, "playerState", InvokeStatic("ScenePlayerStateSnapshot", "CreateDefault"));
        SetField(context, "spawnRequest", CreateSpawnRequest());
        SetField(context, "testDefaultsAllowed", true);
        SetField(context, "entryReason", MainEscapeReflectionTestHelper.EnumValue("SceneEntryReason", "StandaloneTest"));

        Assert.That(MainEscapeReflectionTestHelper.GetPropertyValue<bool>(context, "IsValid"), Is.True);
        Assert.That(ReadField(context, "testDefaultsAllowed"), Is.EqualTo(true));
        Assert.That(ReadField(ReadField(context, "playerState"), "hasState"), Is.EqualTo(true));
    }

    private static object CreateSpawnRequest()
    {
        Type spawnRequestType = MainEscapeReflectionTestHelper.RequireType("SceneSpawnRequest");
        object spawnRequest = Activator.CreateInstance(spawnRequestType);
        SetField(spawnRequest, "spawnId", "default");
        SetField(spawnRequest, "allowFallbackSpawn", true);
        return spawnRequest;
    }

    private static void SetNode(SerializedProperty property, string nodeId, string scenePath)
    {
        property.FindPropertyRelative("nodeId").stringValue = nodeId;
        property.FindPropertyRelative("scenePath").stringValue = scenePath;
        property.FindPropertyRelative("kind").enumValueIndex = EnumIndex("SceneNodeKind", "Level");
    }

    private static void SetEdge(
        SerializedProperty property,
        string fromNodeId,
        string exitId,
        string toNodeId,
        string policy)
    {
        property.FindPropertyRelative("fromNodeId").stringValue = fromNodeId;
        property.FindPropertyRelative("exitId").stringValue = exitId;
        property.FindPropertyRelative("toNodeId").stringValue = toNodeId;
        property.FindPropertyRelative("policy").enumValueIndex = EnumIndex("RouteEdgePolicy", policy);
        property.FindPropertyRelative("weight").floatValue = 1f;
    }

    private static Array InvokeArray(object owner, string methodName, params object[] arguments)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"{owner.GetType().Name}.{methodName} is missing.");
        return method.Invoke(owner, arguments) as Array;
    }

    private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
    {
        Type type = MainEscapeReflectionTestHelper.RequireType(typeName);
        MethodInfo method = type.GetMethod(methodName, MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, $"{typeName}.{methodName} is missing.");
        return method.Invoke(null, arguments);
    }

    private static object ReadField(object owner, string fieldName)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{fieldName} is missing.");
        return field.GetValue(owner);
    }

    private static void SetField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{fieldName} is missing.");
        field.SetValue(owner, value);
    }

    private static int EnumIndex(string enumTypeName, string enumValueName)
    {
        object value = MainEscapeReflectionTestHelper.EnumValue(enumTypeName, enumValueName);
        return Convert.ToInt32(value);
    }
}

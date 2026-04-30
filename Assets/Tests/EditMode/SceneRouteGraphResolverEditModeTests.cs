using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SceneRouteGraphResolverEditModeTests
{
    [Test]
    public void TryResolveNextSceneNode_FixedEdge_UsesConfiguredNodeOnly()
    {
        ScriptableObject graph = CreateGraph(
            new[]
            {
                Node("start", "Assets/Scenes/Test_Start.unity"),
                Node("next", "Assets/Scenes/Test_Next.unity")
            },
            new[]
            {
                Edge("start", "main_exit", "next", "Fixed")
            });

        try
        {
            bool resolved = TryResolveGraphNode(
                graph,
                "start",
                "main_exit",
                7,
                null,
                null,
                out object nextNode,
                out object edge);

            Assert.That(resolved, Is.True);
            Assert.That(ReadField(nextNode, "nodeId"), Is.EqualTo("next"));
            Assert.That(ReadField(edge, "toNodeId"), Is.EqualTo("next"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void TryResolveNextSceneNode_ChoiceEdge_ReturnsStableBranchForSeed()
    {
        ScriptableObject graph = CreateGraph(
            new[]
            {
                Node("start", "Assets/Scenes/Branch_Start.unity"),
                Node("branch_a", "Assets/Scenes/Branch_A.unity"),
                Node("branch_b", "Assets/Scenes/Branch_B.unity")
            },
            new[]
            {
                Edge("start", "branch_exit", "branch_a", "Choice"),
                Edge("start", "branch_exit", "branch_b", "Choice")
            });

        try
        {
            bool firstResolved = TryResolveGraphNode(
                graph,
                "start",
                "branch_exit",
                1234,
                null,
                null,
                out object firstNode,
                out _);
            bool secondResolved = TryResolveGraphNode(
                graph,
                "start",
                "branch_exit",
                1234,
                null,
                null,
                out object secondNode,
                out _);

            string firstNodeId = ReadField(firstNode, "nodeId") as string;
            Assert.That(firstResolved, Is.True);
            Assert.That(secondResolved, Is.True);
            Assert.That(firstNodeId, Is.EqualTo("branch_a").Or.EqualTo("branch_b"));
            Assert.That(ReadField(secondNode, "nodeId"), Is.EqualTo(firstNodeId));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void TryResolveNextSceneNode_WeightedRandom_IgnoresZeroWeightBranch()
    {
        ScriptableObject graph = CreateGraph(
            new[]
            {
                Node("start", "Assets/Scenes/Weighted_Start.unity"),
                Node("blocked", "Assets/Scenes/Weighted_Blocked.unity"),
                Node("selected", "Assets/Scenes/Weighted_Selected.unity")
            },
            new[]
            {
                Edge("start", "random_exit", "blocked", "WeightedRandom", weight: 0f),
                Edge("start", "random_exit", "selected", "WeightedRandom", weight: 10f)
            });

        try
        {
            bool resolved = TryResolveGraphNode(
                graph,
                "start",
                "random_exit",
                77,
                null,
                null,
                out object nextNode,
                out _);

            Assert.That(resolved, Is.True);
            Assert.That(ReadField(nextNode, "nodeId"), Is.EqualTo("selected"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void TryResolveNextSceneNode_ConditionalEdge_RequiresProfileEventAndRunFlag()
    {
        ScriptableObject graph = CreateGraph(
            new[]
            {
                Node("start", "Assets/Scenes/Conditional_Start.unity"),
                Node("fallback", "Assets/Scenes/Conditional_Fallback.unity"),
                Node("secret", "Assets/Scenes/Conditional_Secret.unity")
            },
            new[]
            {
                Edge("start", "locked_exit", "fallback", "Fixed"),
                Edge(
                    "start",
                    "locked_exit",
                    "secret",
                    "Conditional",
                    requiredProfileEventId: "found_hidden_note",
                    requiredRunFlag: "has_secret_key")
            });

        try
        {
            bool fallbackResolved = TryResolveGraphNode(
                graph,
                "start",
                "locked_exit",
                5,
                new[] { "found_hidden_note" },
                null,
                out object fallbackNode,
                out _);
            bool conditionalResolved = TryResolveGraphNode(
                graph,
                "start",
                "locked_exit",
                5,
                new[] { "found_hidden_note" },
                new[] { "has_secret_key" },
                out object conditionalNode,
                out _);

            Assert.That(fallbackResolved, Is.True);
            Assert.That(ReadField(fallbackNode, "nodeId"), Is.EqualTo("fallback"));
            Assert.That(conditionalResolved, Is.True);
            Assert.That(ReadField(conditionalNode, "nodeId"), Is.EqualTo("secret"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void TryResolveNextSceneNode_MissingTarget_ReturnsFalse()
    {
        ScriptableObject graph = CreateGraph(
            new[]
            {
                Node("start", "Assets/Scenes/Missing_Target_Start.unity")
            },
            new[]
            {
                Edge("start", "bad_exit", "missing_target", "Fixed")
            });

        try
        {
            bool resolved = TryResolveGraphNode(
                graph,
                "start",
                "bad_exit",
                9,
                null,
                null,
                out object nextNode,
                out _);

            Assert.That(resolved, Is.False);
            Assert.That(ReadProperty(nextNode, "IsValid"), Is.EqualTo(false));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    private static ScriptableObject CreateGraph(
        NodeSpec[] nodeDefinitions,
        EdgeSpec[] edgeDefinitions,
        string startNodeId = "start")
    {
        ScriptableObject graph = MainEscapeReflectionTestHelper.CreateScriptableObject("RouteGraphDefinition");
        SerializedObject graphObject = new(graph);
        graphObject.FindProperty("routeGraphId").stringValue = "resolver_test";
        graphObject.FindProperty("startNodeId").stringValue = startNodeId;

        SerializedProperty nodes = graphObject.FindProperty("sceneNodes");
        nodes.arraySize = nodeDefinitions.Length;

        for (int index = 0; index < nodeDefinitions.Length; index++)
        {
            SetNode(nodes.GetArrayElementAtIndex(index), nodeDefinitions[index]);
        }

        SerializedProperty edges = graphObject.FindProperty("edges");
        edges.arraySize = edgeDefinitions.Length;

        for (int index = 0; index < edgeDefinitions.Length; index++)
        {
            SetEdge(edges.GetArrayElementAtIndex(index), edgeDefinitions[index]);
        }

        graphObject.ApplyModifiedPropertiesWithoutUndo();
        return graph;
    }

    private static bool TryResolveGraphNode(
        ScriptableObject graph,
        string currentNodeId,
        string exitId,
        int runSeed,
        string[] grantedProfileEventIds,
        string[] activeRunFlags,
        out object nextNode,
        out object resolvedEdge)
    {
        Type resolverType = MainEscapeReflectionTestHelper.RequireType("SceneRouteGraphResolver");
        MethodInfo method = FindGraphResolveMethod(resolverType, graph.GetType());
        object[] arguments =
        {
            graph,
            currentNodeId,
            exitId,
            runSeed,
            grantedProfileEventIds,
            activeRunFlags,
            null,
            null
        };

        bool resolved = method.Invoke(null, arguments) is bool value && value;
        nextNode = arguments[6];
        resolvedEdge = arguments[7];
        return resolved;
    }

    private static MethodInfo FindGraphResolveMethod(Type resolverType, Type graphType)
    {
        MethodInfo[] methods = resolverType.GetMethods(MainEscapeReflectionTestHelper.StaticMemberFlags);

        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo method = methods[index];
            ParameterInfo[] parameters = method.GetParameters();

            if (method.Name == "TryResolveNextSceneNode"
                && parameters.Length == 8
                && parameters[0].ParameterType == graphType)
            {
                return method;
            }
        }

        Assert.Fail("SceneRouteGraphResolver.TryResolveNextSceneNode graph overload is missing.");
        return null;
    }

    private static NodeSpec Node(string nodeId, string scenePath)
    {
        return new NodeSpec(nodeId, scenePath);
    }

    private static EdgeSpec Edge(
        string fromNodeId,
        string exitId,
        string toNodeId,
        string policy,
        float weight = 1f,
        string requiredProfileEventId = null,
        string requiredRunFlag = null)
    {
        return new EdgeSpec(fromNodeId, exitId, toNodeId, policy, weight, requiredProfileEventId, requiredRunFlag);
    }

    private static void SetNode(SerializedProperty property, NodeSpec node)
    {
        property.FindPropertyRelative("nodeId").stringValue = node.NodeId;
        property.FindPropertyRelative("scenePath").stringValue = node.ScenePath;
        property.FindPropertyRelative("kind").enumValueIndex = EnumIndex("SceneNodeKind", "Level");
    }

    private static void SetEdge(SerializedProperty property, EdgeSpec edge)
    {
        property.FindPropertyRelative("fromNodeId").stringValue = edge.FromNodeId;
        property.FindPropertyRelative("exitId").stringValue = edge.ExitId;
        property.FindPropertyRelative("toNodeId").stringValue = edge.ToNodeId;
        property.FindPropertyRelative("policy").enumValueIndex = EnumIndex("RouteEdgePolicy", edge.Policy);
        property.FindPropertyRelative("weight").floatValue = edge.Weight;
        property.FindPropertyRelative("requiredProfileEventId").stringValue = edge.RequiredProfileEventId;
        property.FindPropertyRelative("requiredRunFlag").stringValue = edge.RequiredRunFlag;
    }

    private static object ReadField(object owner, string fieldName)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{fieldName} is missing.");
        return field.GetValue(owner);
    }

    private static object ReadProperty(object owner, string propertyName)
    {
        return MainEscapeReflectionTestHelper.GetPropertyValue(owner, propertyName);
    }

    private static int EnumIndex(string enumTypeName, string enumValueName)
    {
        object value = MainEscapeReflectionTestHelper.EnumValue(enumTypeName, enumValueName);
        return Convert.ToInt32(value);
    }

    private readonly struct NodeSpec
    {
        public NodeSpec(string nodeId, string scenePath)
        {
            NodeId = nodeId;
            ScenePath = scenePath;
        }

        public string NodeId { get; }
        public string ScenePath { get; }
    }

    private readonly struct EdgeSpec
    {
        public EdgeSpec(
            string fromNodeId,
            string exitId,
            string toNodeId,
            string policy,
            float weight,
            string requiredProfileEventId,
            string requiredRunFlag)
        {
            FromNodeId = fromNodeId;
            ExitId = exitId;
            ToNodeId = toNodeId;
            Policy = policy;
            Weight = weight;
            RequiredProfileEventId = requiredProfileEventId;
            RequiredRunFlag = requiredRunFlag;
        }

        public string FromNodeId { get; }
        public string ExitId { get; }
        public string ToNodeId { get; }
        public string Policy { get; }
        public float Weight { get; }
        public string RequiredProfileEventId { get; }
        public string RequiredRunFlag { get; }
    }
}

using System;
using System.Reflection;
using NUnit.Framework;

public sealed class HospitalRouteGraphAdapterEditModeTests
{
    [Test]
    public void Build_ConvertsFloorRoutesIntoSceneNodesAndFixedEdges()
    {
        object snapshot = BuildSnapshot(
            5,
            (5, "Assets/Scenes/RMainScene_5F.unity"),
            (4, "Assets/Scenes/RMainScene_4F.unity"),
            (3, "Assets/Scenes/RMainScene_3F.unity"));

        Array sceneNodes = ReadProperty(snapshot, "SceneNodes") as Array;
        Array edges = ReadProperty(snapshot, "Edges") as Array;

        Assert.That(ReadProperty(snapshot, "IsValid"), Is.EqualTo(true));
        Assert.That(ReadProperty(snapshot, "ChapterId"), Is.EqualTo("hospital"));
        Assert.That(ReadProperty(snapshot, "StartNodeId"), Is.EqualTo("hospital_5f"));
        Assert.That(sceneNodes, Has.Length.EqualTo(3));
        Assert.That(ReadField(sceneNodes.GetValue(0), "nodeId"), Is.EqualTo("hospital_5f"));
        Assert.That(edges, Has.Length.EqualTo(2));
        Assert.That(ReadField(edges.GetValue(0), "fromNodeId"), Is.EqualTo("hospital_5f"));
        Assert.That(ReadField(edges.GetValue(0), "toNodeId"), Is.EqualTo("hospital_4f"));
        Assert.That(ReadField(edges.GetValue(0), "exitId"), Is.EqualTo(ReadStaticField("HospitalRouteGraphAdapter", "DescentExitId")));
        Assert.That(ReadField(edges.GetValue(0), "policy").ToString(), Is.EqualTo("Fixed"));
    }

    [Test]
    public void Build_SortsRoutesBeforeCreatingDescentEdges()
    {
        object snapshot = BuildSnapshot(
            5,
            (2, "Assets/Scenes/RMainScene_2F.unity"),
            (5, "Assets/Scenes/RMainScene_5F.unity"),
            (4, "Assets/Scenes/RMainScene_4F.unity"));

        Array sceneNodes = ReadProperty(snapshot, "SceneNodes") as Array;
        Array edges = ReadProperty(snapshot, "Edges") as Array;

        Assert.That(ReadField(sceneNodes.GetValue(0), "nodeId"), Is.EqualTo("hospital_5f"));
        Assert.That(ReadField(sceneNodes.GetValue(1), "nodeId"), Is.EqualTo("hospital_4f"));
        Assert.That(ReadField(sceneNodes.GetValue(2), "nodeId"), Is.EqualTo("hospital_2f"));
        Assert.That(ReadField(edges.GetValue(1), "fromNodeId"), Is.EqualTo("hospital_4f"));
        Assert.That(ReadField(edges.GetValue(1), "toNodeId"), Is.EqualTo("hospital_2f"));
    }

    private static object BuildSnapshot(int startingFloorNumber, params (int FloorNumber, string ScenePath)[] routes)
    {
        Type adapterType = MainEscapeReflectionTestHelper.RequireType("HospitalRouteGraphAdapter");
        MethodInfo buildMethod = adapterType.GetMethod("Build", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(buildMethod, Is.Not.Null, "HospitalRouteGraphAdapter.Build is missing.");
        return buildMethod.Invoke(null, new object[] { startingFloorNumber, CreateRouteArray(routes) });
    }

    private static Array CreateRouteArray(params (int FloorNumber, string ScenePath)[] routes)
    {
        Type routeType = MainEscapeReflectionTestHelper.RequireType("RFloorSceneEntry");
        Array routeArray = Array.CreateInstance(routeType, routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            routeArray.SetValue(
                Activator.CreateInstance(routeType, routes[index].FloorNumber, routes[index].ScenePath),
                index);
        }

        return routeArray;
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

    private static object ReadStaticField(string typeName, string fieldName)
    {
        Type type = MainEscapeReflectionTestHelper.RequireType(typeName);
        FieldInfo field = type.GetField(fieldName, MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(field, Is.Not.Null, $"{typeName}.{fieldName} is missing.");
        return field.GetValue(null);
    }
}

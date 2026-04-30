using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

public sealed class MainEscapeVentRouteGraphUtilityTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Test]
    public void BuildConnections_UsesSequentialLinksWhenRouteHasNoConnectionData()
    {
        object route = CreateRoute(
            CreateNodeDefinitions(
                (new Vector3Int(0, 0, 0), true, -1),
                (new Vector3Int(1, 0, 0), true, -1),
                (new Vector3Int(2, 0, 0), false, 10)),
            loopPath: false);

        Array connections = InvokeBuildConnections(route);

        Assert.That(connections, Is.Not.Null);
        Assert.That(connections.Length, Is.EqualTo(2));
        AssertConnection(connections.GetValue(0), 0, 1);
        AssertConnection(connections.GetValue(1), 1, 2);
    }

    [Test]
    public void BuildConnections_PrefersDefinedConnectionsAndDeduplicatesPairs()
    {
        object route = CreateRoute(
            CreateNodeDefinitions(
                (new Vector3Int(0, 0, 0), true, -1),
                (new Vector3Int(1, 0, 0), true, -1),
                (new Vector3Int(2, 0, 0), false, 10),
                (new Vector3Int(3, 0, 0), false, 11)),
            CreateConnectionDefinitions((0, 2), (2, 0), (1, 3)),
            usesExplicitConnections: true,
            loopPath: true);

        Array connections = InvokeBuildConnections(route);

        Assert.That(connections, Is.Not.Null);
        Assert.That(connections.Length, Is.EqualTo(2));
        AssertConnection(connections.GetValue(0), 0, 2);
        AssertConnection(connections.GetValue(1), 1, 3);
    }

    [Test]
    public void BuildConnections_IgnoresStaleConnectionData_WhenExplicitLinksAreDisabled()
    {
        object route = CreateRoute(
            CreateNodeDefinitions(
                (new Vector3Int(0, 0, 0), true, -1),
                (new Vector3Int(1, 0, 0), true, -1),
                (new Vector3Int(2, 0, 0), false, 10)),
            CreateConnectionDefinitions((0, 2)),
            usesExplicitConnections: false,
            loopPath: false);

        Array connections = InvokeBuildConnections(route);

        Assert.That(connections, Is.Not.Null);
        Assert.That(connections.Length, Is.EqualTo(2));
        AssertConnection(connections.GetValue(0), 0, 1);
        AssertConnection(connections.GetValue(1), 1, 2);
    }

    private static Array InvokeBuildConnections(object route)
    {
        Type utilityType = FindTypeByName("MainEscapeVentRouteGraphUtility");
        Assert.That(utilityType, Is.Not.Null, "MainEscapeVentRouteGraphUtility type is missing.");

        MethodInfo buildConnectionsMethod = utilityType.GetMethod("BuildConnections", StaticFlags);
        Assert.That(buildConnectionsMethod, Is.Not.Null, "MainEscapeVentRouteGraphUtility.BuildConnections() is missing.");

        return buildConnectionsMethod.Invoke(null, new[] { route }) as Array;
    }

    private static object CreateRoute(Array nodes, bool loopPath)
    {
        Type routeType = FindTypeByName("MainEscapeVentRouteDefinition");
        Assert.That(routeType, Is.Not.Null, "MainEscapeVentRouteDefinition type is missing.");
        return Activator.CreateInstance(routeType, nodes, loopPath);
    }

    private static object CreateRoute(Array nodes, Array connections, bool usesExplicitConnections, bool loopPath)
    {
        Type routeType = FindTypeByName("MainEscapeVentRouteDefinition");
        Assert.That(routeType, Is.Not.Null, "MainEscapeVentRouteDefinition type is missing.");
        return Activator.CreateInstance(routeType, nodes, connections, usesExplicitConnections, loopPath);
    }

    private static Array CreateNodeDefinitions(params (Vector3Int cell, bool isCorridor, int roomId)[] nodeDefinitions)
    {
        Type nodeType = FindTypeByName("MainEscapeVentNodeDefinition");
        Assert.That(nodeType, Is.Not.Null, "MainEscapeVentNodeDefinition type is missing.");

        Array nodes = Array.CreateInstance(nodeType, nodeDefinitions.Length);

        for (int index = 0; index < nodeDefinitions.Length; index++)
        {
            (Vector3Int cell, bool isCorridor, int roomId) definition = nodeDefinitions[index];
            object node = Activator.CreateInstance(nodeType, definition.cell, definition.isCorridor, definition.roomId);
            nodes.SetValue(node, index);
        }

        return nodes;
    }

    private static Array CreateConnectionDefinitions(params (int fromIndex, int toIndex)[] connectionDefinitions)
    {
        Type connectionType = FindTypeByName("MainEscapeVentConnectionDefinition");
        Assert.That(connectionType, Is.Not.Null, "MainEscapeVentConnectionDefinition type is missing.");

        Array connections = Array.CreateInstance(connectionType, connectionDefinitions.Length);

        for (int index = 0; index < connectionDefinitions.Length; index++)
        {
            (int fromIndex, int toIndex) definition = connectionDefinitions[index];
            object connection = Activator.CreateInstance(connectionType, definition.fromIndex, definition.toIndex);
            connections.SetValue(connection, index);
        }

        return connections;
    }

    private static void AssertConnection(object connection, int expectedFromIndex, int expectedToIndex)
    {
        Assert.That(connection, Is.Not.Null, "Vent connection entry should not be null.");
        Assert.That(ReadIntProperty(connection, "FromIndex"), Is.EqualTo(expectedFromIndex));
        Assert.That(ReadIntProperty(connection, "ToIndex"), Is.EqualTo(expectedToIndex));
    }

    private static int ReadIntProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} property is missing.");
        return Convert.ToInt32(property.GetValue(instance));
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

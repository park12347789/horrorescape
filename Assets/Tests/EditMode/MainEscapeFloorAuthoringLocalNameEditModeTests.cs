using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class MainEscapeFloorAuthoringLocalNameEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void ResolvedRootProperties_UseLocalAuthoringNames()
    {
        Type floorAuthoringType = ResolveProjectType("MainEscapeFloorAuthoring");
        GameObject floorObject = new("FloorAuthoring");
        Component floorAuthoring = floorObject.AddComponent(floorAuthoringType);

        try
        {
            SetStringField(floorAuthoring, "authoringMarkersRootName", "LocalMarkers");
            SetStringField(floorAuthoring, "itemPlacementMarkersRootName", "LocalItems");
            SetStringField(floorAuthoring, "keyPlacementMarkersRootName", "LocalKeys");
            SetStringField(floorAuthoring, "enemyPlacementMarkersRootName", "LocalEnemies");
            SetStringField(floorAuthoring, "chaserPlacementMarkersRootName", "LocalChaser");
            SetStringField(floorAuthoring, "dangerMarkersRootName", "LocalDanger");
            SetStringField(floorAuthoring, "ventRouteRootName", "LocalVent");

            Transform markersRoot = CreateChild(floorObject.transform, "LocalMarkers");
            Transform itemRoot = CreateChild(markersRoot, "LocalItems");
            Transform keyRoot = CreateChild(markersRoot, "LocalKeys");
            Transform enemyRoot = CreateChild(markersRoot, "LocalEnemies");
            Transform chaserRoot = CreateChild(markersRoot, "LocalChaser");
            Transform dangerRoot = CreateChild(markersRoot, "LocalDanger");
            Transform ventRoot = CreateChild(markersRoot, "LocalVent");

            Assert.That(GetTransformProperty(floorAuthoring, "ItemPlacementMarkersRoot"), Is.SameAs(itemRoot));
            Assert.That(GetTransformProperty(floorAuthoring, "KeyPlacementMarkersRoot"), Is.SameAs(keyRoot));
            Assert.That(GetTransformProperty(floorAuthoring, "EnemyPlacementMarkersRoot"), Is.SameAs(enemyRoot));
            Assert.That(GetTransformProperty(floorAuthoring, "ChaserPlacementMarkersRoot"), Is.SameAs(chaserRoot));
            Assert.That(GetTransformProperty(floorAuthoring, "DangerMarkersRoot"), Is.SameAs(dangerRoot));
            Assert.That(GetTransformProperty(floorAuthoring, "VentRouteRoot"), Is.SameAs(ventRoot));
        }
        finally
        {
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    [Test]
    public void MarkerResolution_UsesLocalSearchNames()
    {
        Type floorAuthoringType = ResolveProjectType("MainEscapeFloorAuthoring");
        GameObject floorObject = new("FloorAuthoring");
        Component floorAuthoring = floorObject.AddComponent(floorAuthoringType);

        try
        {
            SetStringField(floorAuthoring, "authoringMarkersRootName", "LocalMarkers");
            SetStringArrayField(floorAuthoring, "playerStartMarkerSearchNames", "SpawnHere");

            Transform markersRoot = CreateChild(floorObject.transform, "LocalMarkers");
            Transform playerStart = CreateChild(markersRoot, "SpawnHere");
            playerStart.position = new Vector3(2f, 3f, 0f);

            Assert.That(TryResolvePlayerStartWorldPosition(floorAuthoring, out Vector3 worldPosition), Is.True);
            Assert.That(worldPosition, Is.EqualTo(playerStart.position));
        }
        finally
        {
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject childObject = new(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Type ResolveProjectType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Expected project type '{typeName}' in Assembly-CSharp.");
        return type;
    }

    private static Transform GetTransformProperty(Component authoring, string propertyName)
    {
        PropertyInfo property = authoring.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Expected {authoring.GetType().Name}.{propertyName}.");
        return property.GetValue(authoring) as Transform;
    }

    private static bool TryResolvePlayerStartWorldPosition(Component authoring, out Vector3 worldPosition)
    {
        MethodInfo method = authoring.GetType().GetMethod("TryResolvePlayerStartWorldPosition", InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Expected {authoring.GetType().Name}.TryResolvePlayerStartWorldPosition.");
        object[] arguments = { default(Vector3) };
        bool resolved = method.Invoke(authoring, arguments) is bool result && result;
        worldPosition = arguments[0] is Vector3 vector ? vector : default;
        return resolved;
    }

    private static void SetStringField(Component authoring, string fieldName, string value)
    {
        FieldInfo field = authoring.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{authoring.GetType().Name}.{fieldName} is missing.");
        field.SetValue(authoring, value);
    }

    private static void SetStringArrayField(Component authoring, string fieldName, params string[] values)
    {
        FieldInfo field = authoring.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{authoring.GetType().Name}.{fieldName} is missing.");
        field.SetValue(authoring, values);
    }
}

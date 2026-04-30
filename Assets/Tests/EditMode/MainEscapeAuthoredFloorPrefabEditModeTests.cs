using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MainEscapeAuthoredFloorPrefabEditModeTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void AuthoredFloorPrefab_Exists_AndHasRequiredEscapeAuthoring(int floorNumber)
    {
        string prefabPath = $"Assets/Resources/Floors/MainEscape/{floorNumber}F.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        Assert.That(prefabAsset, Is.Not.Null, $"Missing authored floor prefab at '{prefabPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Assert.That(prefabRoot.GetComponent<Grid>(), Is.Not.Null, $"{floorNumber}F is missing Grid.");
            Assert.That(prefabRoot.GetComponent("GridMapService"), Is.Not.Null, $"{floorNumber}F is missing GridMapService.");
            Assert.That(prefabRoot.GetComponent("GeneratedFloorLayout"), Is.Not.Null, $"{floorNumber}F is missing GeneratedFloorLayout.");
            Assert.That(prefabRoot.GetComponent("MainEscapeFloorAuthoring"), Is.Not.Null, $"{floorNumber}F is missing MainEscapeFloorAuthoring.");

            object settings = LoadRuntimeSettings();
            string authoringMarkersRootName = ReadStringProperty(settings, "AuthoringMarkersRootName");
            string playerStartMarkerName = ReadStringProperty(settings, "PlayerStartMarkerName");
            string transitionMarkerName = ReadStringProperty(settings, "TransitionMarkerName");
            Transform markersRoot = prefabRoot.transform.Find(authoringMarkersRootName);

            Assert.That(markersRoot, Is.Not.Null, $"{floorNumber}F is missing AuthoringMarkers root.");
            Assert.That(markersRoot.Find(playerStartMarkerName), Is.Not.Null, $"{floorNumber}F is missing PlayerStart marker.");
            Assert.That(markersRoot.Find(transitionMarkerName), Is.Not.Null, $"{floorNumber}F is missing Transition marker.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void AuthoredFloorPrefab_PlayerStartWorldPosition_MatchesMarkerTransform(int floorNumber)
    {
        string prefabPath = $"Assets/Resources/Floors/MainEscape/{floorNumber}F.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        Assert.That(prefabAsset, Is.Not.Null, $"Missing authored floor prefab at '{prefabPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Type floorAuthoringType = FindTypeByName("MainEscapeFloorAuthoring");
            Assert.That(floorAuthoringType, Is.Not.Null, "MainEscapeFloorAuthoring type is missing.");

            Component floorAuthoring = prefabRoot.GetComponent(floorAuthoringType);
            Assert.That(floorAuthoring, Is.Not.Null, $"{floorNumber}F is missing MainEscapeFloorAuthoring.");

            object settings = LoadRuntimeSettings();
            string authoringMarkersRootName = ReadStringProperty(settings, "AuthoringMarkersRootName");
            string playerStartMarkerName = ReadStringProperty(settings, "PlayerStartMarkerName");
            Transform markersRoot = prefabRoot.transform.Find(authoringMarkersRootName);
            Transform playerStartMarker = markersRoot != null ? markersRoot.Find(playerStartMarkerName) : null;

            Assert.That(playerStartMarker, Is.Not.Null, $"{floorNumber}F is missing PlayerStart marker.");
            Assert.That(
                TryInvokeVector3OutMethod(floorAuthoring, "TryResolvePlayerStartWorldPosition", out Vector3 resolvedWorldPosition),
                Is.True,
                $"{floorNumber}F could not resolve an authored PlayerStart world position.");
            Assert.That(resolvedWorldPosition, Is.EqualTo(playerStartMarker.position));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static object LoadRuntimeSettings()
    {
        System.Type settingsType = System.Type.GetType("MainEscapeRuntimeSettings, Assembly-CSharp");
        Assert.That(settingsType, Is.Not.Null, "MainEscapeRuntimeSettings type is missing.");
        System.Reflection.MethodInfo loadMethod = settingsType.GetMethod("Load", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimeSettings.Load() is missing.");
        return loadMethod.Invoke(null, null);
    }

    private static string ReadStringProperty(object instance, string propertyName)
    {
        System.Reflection.PropertyInfo property = instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance) as string;
    }

    private static bool TryInvokeVector3OutMethod(object instance, string methodName, out Vector3 result)
    {
        System.Reflection.MethodInfo method = instance?.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{instance?.GetType().Name}.{methodName} is missing.");

        object[] arguments = { Vector3.zero };
        bool resolved = method != null && method.Invoke(instance, arguments) is bool invocationResult && invocationResult;
        result = arguments[0] is Vector3 worldPosition ? worldPosition : Vector3.zero;
        return resolved;
    }

    private static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

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

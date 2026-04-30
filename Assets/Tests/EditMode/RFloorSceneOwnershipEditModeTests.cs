using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class RFloorSceneOwnershipEditModeTests
{
    private const string StartFloorScenePath = "Assets/Scenes/RMainScene_5F.unity";

    [Test]
    public void StartFloorScene_FloorAuthoring_IsSceneOwned()
    {
        EditorSceneManager.OpenScene(StartFloorScenePath, OpenSceneMode.Single);

        Type floorAuthoringType = FindTypeByName("MainEscapeFloorAuthoring");
        Assert.That(floorAuthoringType, Is.Not.Null, "MainEscapeFloorAuthoring type is missing.");

        Component floorAuthoring = FindComponentByType(floorAuthoringType);

        Assert.That(floorAuthoring, Is.Not.Null, $"{StartFloorScenePath} is missing MainEscapeFloorAuthoring.");
        Assert.That(floorAuthoring.gameObject.scene.path, Is.EqualTo(StartFloorScenePath));
        Assert.That(
            PrefabUtility.IsPartOfPrefabInstance(floorAuthoring.gameObject),
            Is.False,
            $"{StartFloorScenePath} should own its authored floor hierarchy directly instead of depending on a shared prefab instance.");
    }

    private static Component FindComponentByType(Type targetType)
    {
        MonoBehaviour[] behaviours = UnityObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour != null && targetType.IsAssignableFrom(behaviour.GetType()))
            {
                return behaviour;
            }
        }

        return null;
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

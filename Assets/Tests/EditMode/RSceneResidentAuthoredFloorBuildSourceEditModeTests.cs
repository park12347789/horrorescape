using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RSceneResidentAuthoredFloorBuildSourceEditModeTests
{
    [Test]
    public void TryResolveSceneResidentFloorAuthoring_UsesTargetScene_WhenActiveSceneDiffers()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene transitionScene = SceneManager.CreateScene("TransitionScene_ForAuthoringResolutionTest");
        Scene floorScene = SceneManager.CreateScene("FloorScene_ForAuthoringResolutionTest");
        Type floorAuthoringType = FindTypeByName("MainEscapeFloorAuthoring");
        Assert.That(floorAuthoringType, Is.Not.Null, "MainEscapeFloorAuthoring type is missing.");
        GameObject transitionRoot = CreateAuthoringRoot("TransitionAuthoring", transitionScene, floorAuthoringType);
        GameObject floorRoot = CreateAuthoringRoot("FloorAuthoring", floorScene, floorAuthoringType);
        Component floorAuthoring = floorRoot.GetComponent(floorAuthoringType);

        try
        {
            SceneManager.SetActiveScene(transitionScene);

            bool resolved = InvokeTryResolveSceneResidentFloorAuthoring(
                floorScene,
                out Component resolvedAuthoring);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedAuthoring, Is.SameAs(floorAuthoring));
            Assert.That(resolvedAuthoring.gameObject.scene, Is.EqualTo(floorScene));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(transitionRoot);
            UnityEngine.Object.DestroyImmediate(floorRoot);

            if (originalActiveScene.IsValid())
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            CloseSceneIfValid(transitionScene);
            CloseSceneIfValid(floorScene);
        }
    }

    private static GameObject CreateAuthoringRoot(string name, Scene scene, Type floorAuthoringType)
    {
        GameObject root = new(name);
        root.AddComponent(floorAuthoringType);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static bool InvokeTryResolveSceneResidentFloorAuthoring(Scene scene, out Component floorAuthoring)
    {
        Type bootstrapType = FindTypeByName("Batch2TestRoomBootstrap");
        Assert.That(bootstrapType, Is.Not.Null, "Batch2TestRoomBootstrap type is missing.");
        MethodInfo method = bootstrapType.GetMethod(
            "TryResolveSceneResidentFloorAuthoring",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "Batch2TestRoomBootstrap.TryResolveSceneResidentFloorAuthoring is missing.");

        object[] arguments = { scene, null };
        bool resolved = method.Invoke(null, arguments) is bool invocationResult && invocationResult;
        floorAuthoring = arguments[1] as Component;
        return resolved;
    }

    private static void CloseSceneIfValid(Scene scene)
    {
        if (scene.IsValid())
        {
            EditorSceneManager.CloseScene(scene, true);
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
}

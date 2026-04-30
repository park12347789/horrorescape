using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RSceneReferenceLookupEditModeTests
{
    [Test]
    public void Lookup_ReturnsTargetSceneComponents_WhenDifferentSceneIsActive()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene distractorScene = SceneManager.CreateScene("RSceneReferenceLookup_Distractor");
        Scene targetScene = SceneManager.CreateScene("RSceneReferenceLookup_Target");

        GameObject distractorRoot = CreateSceneRoot(distractorScene, "SharedRoot", "SharedChild");
        GameObject targetRoot = CreateSceneRoot(targetScene, "SharedRoot", "SharedChild");
        BoxCollider2D targetCollider = targetRoot.GetComponentInChildren<BoxCollider2D>(true);
        Transform targetChild = targetRoot.transform.Find("SharedChild");

        try
        {
            SceneManager.SetActiveScene(distractorScene);

            BoxCollider2D firstCollider = InvokeFindFirstComponentInScene<BoxCollider2D>(targetScene);
            BoxCollider2D[] targetColliders = InvokeFindComponentsInScene<BoxCollider2D>(targetScene);
            Transform resolvedChild = InvokeFindTransformInScene(targetScene, "SharedChild");

            Assert.That(firstCollider, Is.SameAs(targetCollider));
            Assert.That(targetColliders, Has.Length.EqualTo(1));
            Assert.That(targetColliders[0], Is.SameAs(targetCollider));
            Assert.That(resolvedChild, Is.SameAs(targetChild));
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            UnityEngine.Object.DestroyImmediate(distractorRoot);
            UnityEngine.Object.DestroyImmediate(targetRoot);
            EditorSceneManager.CloseScene(targetScene, true);
            EditorSceneManager.CloseScene(distractorScene, true);
        }
    }

    private static GameObject CreateSceneRoot(Scene scene, string rootName, string childName)
    {
        GameObject root = new(rootName);
        GameObject child = new(childName);
        child.transform.SetParent(root.transform, false);
        child.AddComponent<BoxCollider2D>();
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static TComponent InvokeFindFirstComponentInScene<TComponent>(Scene scene)
        where TComponent : Component
    {
        MethodInfo method = RequireLookupMethod("FindFirstComponentInScene").MakeGenericMethod(typeof(TComponent));
        return method.Invoke(null, new object[] { scene }) as TComponent;
    }

    private static TComponent[] InvokeFindComponentsInScene<TComponent>(Scene scene)
        where TComponent : Component
    {
        MethodInfo method = RequireLookupMethod("FindComponentsInScene").MakeGenericMethod(typeof(TComponent));
        return method.Invoke(null, new object[] { scene }) as TComponent[];
    }

    private static Transform InvokeFindTransformInScene(Scene scene, params string[] candidateNames)
    {
        MethodInfo method = RequireLookupMethod("FindTransformInScene");
        return method.Invoke(null, new object[] { scene, candidateNames }) as Transform;
    }

    private static MethodInfo RequireLookupMethod(string methodName)
    {
        Type lookupType = FindTypeByName("RSceneReferenceLookup");
        Assert.That(lookupType, Is.Not.Null, "RSceneReferenceLookup type is missing.");
        MethodInfo method = lookupType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, $"RSceneReferenceLookup.{methodName} is missing.");
        return method;
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

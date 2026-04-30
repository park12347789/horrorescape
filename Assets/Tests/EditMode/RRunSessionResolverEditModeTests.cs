using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RRunSessionResolverEditModeTests
{
    private sealed class ResolverContextProbe : MonoBehaviour
    {
    }

    [Test]
    public void ResolveForContext_PrefersContextSceneSessionOverCachedSingletonAndActiveScene()
    {
        Type sessionType = RequireType("RRunSessionController");
        Type resolverType = RequireType("RRunSessionResolver");
        FieldInfo instanceField = sessionType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(instanceField, Is.Not.Null, "RRunSessionController.instance is missing.");

        object originalInstance = instanceField.GetValue(null);
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene distractorScene = SceneManager.CreateScene("RRunSessionResolver_Distractor");
        Scene targetScene = SceneManager.CreateScene("RRunSessionResolver_Target");
        GameObject targetSessionObject = CreateSceneObject(targetScene, "TargetSession");
        GameObject distractorSessionObject = CreateSceneObject(distractorScene, "DistractorSession");
        GameObject contextObject = CreateSceneObject(targetScene, "TargetContext");

        try
        {
            Component targetSession = targetSessionObject.AddComponent(sessionType);
            instanceField.SetValue(null, null);
            Component distractorSession = distractorSessionObject.AddComponent(sessionType);
            ResolverContextProbe context = contextObject.AddComponent<ResolverContextProbe>();

            instanceField.SetValue(null, distractorSession);
            SceneManager.SetActiveScene(distractorScene);

            MethodInfo resolveForContext = resolverType.GetMethod(
                "ResolveForContext",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(MonoBehaviour), sessionType },
                null);
            Assert.That(resolveForContext, Is.Not.Null, "RRunSessionResolver.ResolveForContext is missing.");

            object resolved = resolveForContext.Invoke(null, new object[] { context, null });

            Assert.That(resolved, Is.SameAs(targetSession));
            Assert.That(resolved, Is.Not.SameAs(distractorSession));
        }
        finally
        {
            instanceField.SetValue(null, originalInstance);

            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            UnityEngine.Object.DestroyImmediate(contextObject);
            UnityEngine.Object.DestroyImmediate(targetSessionObject);
            UnityEngine.Object.DestroyImmediate(distractorSessionObject);
            EditorSceneManager.CloseScene(targetScene, true);
            EditorSceneManager.CloseScene(distractorScene, true);
        }
    }

    private static GameObject CreateSceneObject(Scene scene, string name)
    {
        GameObject gameObject = new(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        return gameObject;
    }

    private static Type RequireType(string typeName)
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

        Assert.Fail($"{typeName} type is missing.");
        return null;
    }
}

using System;
using System.Reflection;

using NUnit.Framework;

using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RGameplaySceneBootStagingEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private GameObject sessionControllerObject;
    private GameObject compositionRootObject;

    [TearDown]
    public void TearDown()
    {
        if (compositionRootObject != null)
        {
            UnityEngine.Object.DestroyImmediate(compositionRootObject);
        }

        if (sessionControllerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(sessionControllerObject);
        }
    }

    [Test]
    public void RunSessionController_WaitsForCompositionRoot_WhenRuntimeCompositionIsPending()
    {
        CreateIsolatedTestScene();
        object sessionController = CreateSessionController();
        Component compositionRoot = CreateCompositionRoot(composeOnPlay: true, composed: false);

        bool shouldWait = InvokeShouldWaitForGameplaySceneComposition(sessionController, compositionRoot.gameObject.scene);

        Assert.That(shouldWait, Is.True);
        Assert.That(ReadBoolProperty(compositionRoot, "IsRuntimeCompositionReady"), Is.False);
        Assert.That(ReadBoolProperty(compositionRoot, "ExpectsRuntimeCompositionOnPlay"), Is.True);
    }

    [Test]
    public void RunSessionController_DoesNotWaitForCompositionRoot_WhenRuntimeCompositionIsReady()
    {
        CreateIsolatedTestScene();
        object sessionController = CreateSessionController();
        Component compositionRoot = CreateCompositionRoot(composeOnPlay: true, composed: true);

        bool shouldWait = InvokeShouldWaitForGameplaySceneComposition(sessionController, compositionRoot.gameObject.scene);

        Assert.That(shouldWait, Is.False);
        Assert.That(ReadBoolProperty(compositionRoot, "IsRuntimeCompositionReady"), Is.True);
    }

    [Test]
    public void RunSessionController_DoesNotWaitForCompositionRoot_WhenComposeOnPlayIsDisabled()
    {
        CreateIsolatedTestScene();
        object sessionController = CreateSessionController();
        Component compositionRoot = CreateCompositionRoot(composeOnPlay: false, composed: false);

        bool shouldWait = InvokeShouldWaitForGameplaySceneComposition(sessionController, compositionRoot.gameObject.scene);

        Assert.That(shouldWait, Is.False);
        Assert.That(ReadBoolProperty(compositionRoot, "ExpectsRuntimeCompositionOnPlay"), Is.False);
    }

    private object CreateSessionController()
    {
        Type sessionControllerType = FindTypeByName("RRunSessionController");
        Assert.That(sessionControllerType, Is.Not.Null, "RRunSessionController type is missing.");

        sessionControllerObject = new GameObject("RRunSessionController");
        return sessionControllerObject.AddComponent(sessionControllerType);
    }

    private Component CreateCompositionRoot(bool composeOnPlay, bool composed)
    {
        Type compositionRootType = FindTypeByName("RSceneCompositionRoot");
        Assert.That(compositionRootType, Is.Not.Null, "RSceneCompositionRoot type is missing.");

        compositionRootObject = new GameObject("RSceneCompositionRoot_Test");
        Component compositionRoot = compositionRootObject.AddComponent(compositionRootType);
        SetPrivateField(compositionRoot, "composeOnPlay", composeOnPlay);
        SetPrivateField(compositionRoot, "composed", composed);
        return compositionRoot;
    }

    private static bool InvokeShouldWaitForGameplaySceneComposition(object sessionController, Scene scene)
    {
        MethodInfo method = sessionController.GetType().GetMethod("ShouldWaitForGameplaySceneComposition", StaticFlags);
        Assert.That(method, Is.Not.Null, "RRunSessionController.ShouldWaitForGameplaySceneComposition is missing.");

        return method.Invoke(null, new object[] { scene }) is bool value && value;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} is missing.");
        field.SetValue(instance, value);
    }

    private static bool ReadBoolProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance) is bool value && value;
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

    private static Scene CreateIsolatedTestScene()
    {
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}

using System.IO;
using System.Reflection;
using System;

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainEscapeFinalExitTouchTriggerReferenceLookupEditModeTests
{
    private const string SourcePath = "Assets/Scripts/Objectives/MainEscapeFinalExitTouchTrigger.cs";
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void CacheReferences_UsesSceneLocalRunControllerFallback()
    {
        string source = File.ReadAllText(SourcePath);

        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<RRunController>(gameObject.scene)"));
        Assert.That(source, Does.Not.Contain("FindFirstObjectByType<RRunController>"));
    }

    [Test]
    public void RunControllerFallback_IgnoresControllersInOtherLoadedScenes()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene distractorScene = SceneManager.CreateScene("FinalExit_Distractor");
        Scene targetScene = SceneManager.CreateScene("FinalExit_Target");
        GameObject triggerObject = new("FinalExitTrigger");
        GameObject distractorRunObject = new("DistractorRunController");
        GameObject targetRunObject = new("TargetRunController");

        try
        {
            SceneManager.MoveGameObjectToScene(triggerObject, targetScene);
            SceneManager.MoveGameObjectToScene(distractorRunObject, distractorScene);
            SceneManager.MoveGameObjectToScene(targetRunObject, targetScene);

            triggerObject.AddComponent<BoxCollider2D>();
            Type triggerType = FindTypeByName("MainEscapeFinalExitTouchTrigger");
            Type runControllerType = FindTypeByName("RRunController");
            Assert.That(triggerType, Is.Not.Null, "MainEscapeFinalExitTouchTrigger type is missing.");
            Assert.That(runControllerType, Is.Not.Null, "RRunController type is missing.");
            Component trigger = triggerObject.AddComponent(triggerType);
            Component distractorRunController = distractorRunObject.AddComponent(runControllerType);
            Component targetRunController = targetRunObject.AddComponent(runControllerType);

            SetRunController(trigger, distractorRunController);

            bool resolved = InvokeTryResolveRunController(trigger, force: true);

            Assert.That(resolved, Is.True);
            Assert.That(ReadRunController(trigger), Is.SameAs(targetRunController));
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            UnityEngine.Object.DestroyImmediate(triggerObject);
            UnityEngine.Object.DestroyImmediate(distractorRunObject);
            UnityEngine.Object.DestroyImmediate(targetRunObject);
            EditorSceneManager.CloseScene(targetScene, true);
            EditorSceneManager.CloseScene(distractorScene, true);
        }
    }

    [Test]
    public void RunControllerFallback_PrefersSiblingTransitionPointControllerBeforeSceneSearch()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene targetScene = SceneManager.CreateScene("FinalExit_TransitionPoint_Target");
        GameObject sceneFallbackRunObject = new("SceneFallbackRunController");
        GameObject triggerObject = new("FinalExitTrigger");
        GameObject transitionRunObject = new("TransitionRunController");

        try
        {
            SceneManager.MoveGameObjectToScene(sceneFallbackRunObject, targetScene);
            SceneManager.MoveGameObjectToScene(triggerObject, targetScene);
            SceneManager.MoveGameObjectToScene(transitionRunObject, targetScene);

            triggerObject.AddComponent<BoxCollider2D>();
            triggerObject.AddComponent<SpriteRenderer>();
            Type triggerType = FindTypeByName("MainEscapeFinalExitTouchTrigger");
            Type transitionPointType = FindTypeByName("FloorEscapeTransitionPoint");
            Type runControllerType = FindTypeByName("RRunController");
            Assert.That(triggerType, Is.Not.Null, "MainEscapeFinalExitTouchTrigger type is missing.");
            Assert.That(transitionPointType, Is.Not.Null, "FloorEscapeTransitionPoint type is missing.");
            Assert.That(runControllerType, Is.Not.Null, "RRunController type is missing.");

            Component sceneFallbackRunController = sceneFallbackRunObject.AddComponent(runControllerType);
            Component trigger = triggerObject.AddComponent(triggerType);
            Component transitionPoint = triggerObject.AddComponent(transitionPointType);
            Component transitionRunController = transitionRunObject.AddComponent(runControllerType);

            SetTransitionPointController(transitionPoint, transitionRunController);

            bool resolved = InvokeTryResolveRunController(trigger, force: true);

            Assert.That(resolved, Is.True);
            Assert.That(ReadRunController(trigger), Is.SameAs(transitionRunController));
            Assert.That(ReadRunController(trigger), Is.Not.SameAs(sceneFallbackRunController));
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            UnityEngine.Object.DestroyImmediate(sceneFallbackRunObject);
            UnityEngine.Object.DestroyImmediate(triggerObject);
            UnityEngine.Object.DestroyImmediate(transitionRunObject);
            EditorSceneManager.CloseScene(targetScene, true);
        }
    }

    private static bool InvokeTryResolveRunController(Component trigger, bool force)
    {
        MethodInfo method = trigger.GetType().GetMethod("TryResolveRunController", InstanceFlags);
        Assert.That(method, Is.Not.Null, "MainEscapeFinalExitTouchTrigger.TryResolveRunController is missing.");
        return method.Invoke(trigger, new object[] { force }) is bool value && value;
    }

    private static void SetRunController(Component trigger, Component runController)
    {
        FieldInfo field = trigger.GetType().GetField("runController", InstanceFlags);
        Assert.That(field, Is.Not.Null, "MainEscapeFinalExitTouchTrigger.runController is missing.");
        field.SetValue(trigger, runController);
    }

    private static Component ReadRunController(Component trigger)
    {
        FieldInfo field = trigger.GetType().GetField("runController", InstanceFlags);
        Assert.That(field, Is.Not.Null, "MainEscapeFinalExitTouchTrigger.runController is missing.");
        return field.GetValue(trigger) as Component;
    }

    private static void SetTransitionPointController(Component transitionPoint, Component runController)
    {
        FieldInfo field = transitionPoint.GetType().GetField("rebuildControllerSource", InstanceFlags);
        Assert.That(field, Is.Not.Null, "FloorEscapeTransitionPoint.rebuildControllerSource is missing.");
        field.SetValue(transitionPoint, runController);
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

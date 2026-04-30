using System;
using System.Reflection;

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

public sealed class RSceneCompositionRootRuntimeFallbackEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void ResolveDebugModeControllerForPostCompose_DoesNotAutoAdd_WhenDebugModeDisabledAndNoAuthoredController()
    {
        OpenIsolatedScene();
        Type compositionRootType = ResolveProjectType("RSceneCompositionRoot");
        Type debugControllerType = ResolveProjectType("MainEscapeDebugModeController");
        GameObject rootObject = new("RuntimeFallback_Test_CompositionRoot");

        try
        {
            Component compositionRoot = rootObject.AddComponent(compositionRootType);

            object resolvedController = InvokeInstance(compositionRoot, "ResolveDebugModeControllerForPostCompose");

            Assert.That(resolvedController, Is.Null);
            Assert.That(rootObject.GetComponent(debugControllerType), Is.Null);
        }
        finally
        {
            UnityObject.DestroyImmediate(rootObject);
        }
    }

    [Test]
    public void ResolveDebugModeControllerForPostCompose_UsesAuthoredController_WhenPresent()
    {
        OpenIsolatedScene();
        Type compositionRootType = ResolveProjectType("RSceneCompositionRoot");
        Type debugControllerType = ResolveProjectType("MainEscapeDebugModeController");
        GameObject rootObject = new("RuntimeFallback_Test_CompositionRoot");
        GameObject debugObject = new("RuntimeFallback_Test_AuthoredDebugModeController");

        try
        {
            Component compositionRoot = rootObject.AddComponent(compositionRootType);
            Component authoredController = debugObject.AddComponent(debugControllerType);

            object resolvedController = InvokeInstance(compositionRoot, "ResolveDebugModeControllerForPostCompose");

            Assert.That(resolvedController, Is.SameAs(authoredController));
        }
        finally
        {
            UnityObject.DestroyImmediate(rootObject);
            UnityObject.DestroyImmediate(debugObject);
        }
    }

    [Test]
    public void ShouldAutoInstallShadowStartleDirector_RequiresExplicitAuthoringMarkers()
    {
        OpenIsolatedScene();
        Type compositionRootType = ResolveProjectType("RSceneCompositionRoot");
        Type floorAuthoringType = ResolveProjectType("MainEscapeFloorAuthoring");
        Type shadowMarkerType = ResolveProjectType("MainEscapeShadowStartleMarker");
        GameObject rootObject = new("RuntimeFallback_Test_CompositionRoot");
        GameObject floorObject = new("RuntimeFallback_Test_FloorAuthoring");

        try
        {
            Component compositionRoot = rootObject.AddComponent(compositionRootType);
            Component floorAuthoring = floorObject.AddComponent(floorAuthoringType);
            SetPrivateField(compositionRoot, "floorAuthoring", floorAuthoring);

            Assert.That(
                InvokeBool(compositionRoot, "ShouldAutoInstallShadowStartleDirector"),
                Is.False,
                "A scene without explicit shadow startle markers should not auto-install the director.");

            GameObject authoringMarkers = new("AuthoringMarkers");
            GameObject scareMarkers = new("ScareMarkers");
            GameObject markerObject = new("ShadowStartle_01");
            authoringMarkers.transform.SetParent(floorObject.transform, false);
            scareMarkers.transform.SetParent(authoringMarkers.transform, false);
            markerObject.transform.SetParent(scareMarkers.transform, false);
            markerObject.AddComponent(shadowMarkerType);
            InvokeInstance(floorAuthoring, "CacheReferencesFromHierarchy");

            Assert.That(
                InvokeBool(compositionRoot, "ShouldAutoInstallShadowStartleDirector"),
                Is.True,
                "Explicit shadow startle markers are the only automatic director-install trigger.");
        }
        finally
        {
            UnityObject.DestroyImmediate(rootObject);
            UnityObject.DestroyImmediate(floorObject);
        }
    }

    [Test]
    public void TryResolveExistingElevatorExitPointFromVisual_DoesNotAddMissingInteractable()
    {
        OpenIsolatedScene();
        Type compositionRootType = ResolveProjectType("RSceneCompositionRoot");
        Type elevatorExitType = ResolveProjectType("MainEscapeElevatorExitInteractable");
        GameObject rootObject = new("RuntimeFallback_Test_CompositionRoot");
        GameObject visualObject = new("RuntimeFallback_Test_ElevatorVisual");

        try
        {
            Component compositionRoot = rootObject.AddComponent(compositionRootType);
            SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
            SetPrivateField(compositionRoot, "elevatorExitVisual", visual);

            Assert.That(
                InvokeBool(compositionRoot, "TryResolveExistingElevatorExitPointFromVisual"),
                Is.False,
                "A visual without an authored interactable should not receive one automatically.");
            Assert.That(visualObject.GetComponent(elevatorExitType), Is.Null);

            Component authoredInteractable = visualObject.AddComponent(elevatorExitType);

            Assert.That(InvokeBool(compositionRoot, "TryResolveExistingElevatorExitPointFromVisual"), Is.True);
            Assert.That(GetPrivateField(compositionRoot, "elevatorExitPoint"), Is.SameAs(authoredInteractable));
        }
        finally
        {
            UnityObject.DestroyImmediate(rootObject);
            UnityObject.DestroyImmediate(visualObject);
        }
    }

    [Test]
    public void UsesElevatorPropDirectExit_AllowsRouteDrivenNonCanonicalScene()
    {
        OpenIsolatedScene();
        Scene scene = SceneManager.GetActiveScene();
        Assert.That(scene.name, Is.Not.Empty, "The isolated test scene needs a name for route matching.");

        Type compositionRootType = ResolveProjectType("RSceneCompositionRoot");
        Type sessionControllerType = ResolveProjectType("RRunSessionController");
        Type runControllerType = ResolveProjectType("RRunController");
        GameObject compositionRootObject = new("RuntimeFallback_Test_CompositionRoot");
        GameObject sessionObject = new("RuntimeFallback_Test_RunSessionController");
        GameObject runObject = new("RuntimeFallback_Test_RunController");

        try
        {
            Component compositionRoot = compositionRootObject.AddComponent(compositionRootType);
            Component sessionController = sessionObject.AddComponent(sessionControllerType);
            Component runController = runObject.AddComponent(runControllerType);

            ConfigureSessionRoute(sessionController, 2, (2, scene.name));
            ConfigureRunControllerFloor(runController, 2);
            SetPrivateField(compositionRoot, "runSessionController", sessionController);
            SetPrivateField(compositionRoot, "runController", runController);

            Assert.That(
                InvokeBool(compositionRoot, "UsesElevatorPropDirectExit", 2),
                Is.True,
                "A noncanonical scene explicitly present in the session route graph should be eligible for direct elevator exit routing.");
        }
        finally
        {
            UnityObject.DestroyImmediate(compositionRootObject);
            UnityObject.DestroyImmediate(sessionObject);
            UnityObject.DestroyImmediate(runObject);
        }
    }

    [Test]
    public void UsesElevatorPropDirectExit_RejectsUnroutedNonCanonicalScene()
    {
        OpenIsolatedScene();
        Type compositionRootType = ResolveProjectType("RSceneCompositionRoot");
        Type sessionControllerType = ResolveProjectType("RRunSessionController");
        Type runControllerType = ResolveProjectType("RRunController");
        GameObject compositionRootObject = new("RuntimeFallback_Test_CompositionRoot");
        GameObject sessionObject = new("RuntimeFallback_Test_RunSessionController");
        GameObject runObject = new("RuntimeFallback_Test_RunController");

        try
        {
            Component compositionRoot = compositionRootObject.AddComponent(compositionRootType);
            Component sessionController = sessionObject.AddComponent(sessionControllerType);
            Component runController = runObject.AddComponent(runControllerType);

            ConfigureSessionRoute(sessionController, 2, (2, "Assets/Scenes/SomeOtherRouteDriven2F.unity"));
            ConfigureRunControllerFloor(runController, 2);
            SetPrivateField(compositionRoot, "runSessionController", sessionController);
            SetPrivateField(compositionRoot, "runController", runController);

            Assert.That(
                InvokeBool(compositionRoot, "UsesElevatorPropDirectExit", 2),
                Is.False,
                "A noncanonical scene should still be rejected when it is not present in the session route graph.");
        }
        finally
        {
            UnityObject.DestroyImmediate(compositionRootObject);
            UnityObject.DestroyImmediate(sessionObject);
            UnityObject.DestroyImmediate(runObject);
        }
    }

    private static Type ResolveProjectType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Expected project type '{typeName}' in Assembly-CSharp.");
        return type;
    }

    private static void OpenIsolatedScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static object InvokeInstance(Component target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Expected {target.GetType().Name}.{methodName}.");
        return method.Invoke(target, null);
    }

    private static bool InvokeBool(Component target, string methodName)
    {
        return InvokeInstance(target, methodName) is bool result && result;
    }

    private static bool InvokeBool(Component target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Expected {target.GetType().Name}.{methodName}.");
        return method.Invoke(target, arguments) is bool result && result;
    }

    private static void SetPrivateField(Component target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Expected {target.GetType().Name}.{fieldName}.");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(Component target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Expected {target.GetType().Name}.{fieldName}.");
        return field.GetValue(target);
    }

    private static void ConfigureSessionRoute(Component sessionController, int startingFloorNumber, params (int FloorNumber, string ScenePath)[] routes)
    {
        SetPrivateField(sessionController, "startingFloorNumber", startingFloorNumber);
        SetPrivateField(sessionController, "floorScenes", CreateRouteEntriesArray(routes));
    }

    private static void ConfigureRunControllerFloor(Component runController, int floorNumber)
    {
        SetPrivateField(runController, "authoredFloorNumber", floorNumber);
        MethodInfo resolveFloorIndex = runController.GetType().GetMethod("ResolveFloorIndex", InstanceFlags);
        Assert.That(resolveFloorIndex, Is.Not.Null, "Expected RRunController.ResolveFloorIndex.");
        int floorIndex = resolveFloorIndex.Invoke(runController, new object[] { floorNumber }) is int value ? value : 0;
        SetPrivateField(runController, "currentFloorIndex", floorIndex);
    }

    private static Array CreateRouteEntriesArray(params (int FloorNumber, string ScenePath)[] routes)
    {
        Type routeType = ResolveProjectType("RFloorSceneEntry");
        Array entries = Array.CreateInstance(routeType, routes.Length);

        for (int index = 0; index < routes.Length; index++)
        {
            entries.SetValue(Activator.CreateInstance(routeType, routes[index].FloorNumber, routes[index].ScenePath), index);
        }

        return entries;
    }
}

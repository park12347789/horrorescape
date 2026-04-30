using System;
using System.IO;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;

public sealed class IRGameClearPanelInputRouterTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [Test]
    public void ResolveAction_FailureRetry_ReturnsRetry()
    {
        object action = InvokeResolveAction("Failure", confirmPressed: false, retryPressed: true, returnToLobbyPressed: false);
        Assert.That(action.ToString(), Is.EqualTo("RetryCurrentRun"));
    }

    [Test]
    public void ResolveAction_FinalClearConfirm_ReturnsLobby()
    {
        object action = InvokeResolveAction("FinalClear", confirmPressed: true, retryPressed: false, returnToLobbyPressed: false);
        Assert.That(action.ToString(), Is.EqualTo("ReturnToLobby"));
    }

    [Test]
    public void ResolveAction_CustomWithoutInput_ReturnsNone()
    {
        object action = InvokeResolveAction("Custom", confirmPressed: false, retryPressed: false, returnToLobbyPressed: false);
        Assert.That(action.ToString(), Is.EqualTo("None"));
    }

    private static object InvokeResolveAction(string modalModeName, bool confirmPressed, bool retryPressed, bool returnToLobbyPressed)
    {
        Type routerType = FindTypeByName("IRGameClearPanelInputRouter");
        Type modeType = FindTypeByName("IRRunModalMode");
        Assert.That(routerType, Is.Not.Null, "IRGameClearPanelInputRouter type is missing.");
        Assert.That(modeType, Is.Not.Null, "IRRunModalMode type is missing.");

        MethodInfo method = routerType.GetMethod("ResolveAction", StaticFlags);
        Assert.That(method, Is.Not.Null, "IRGameClearPanelInputRouter.ResolveAction is missing.");

        object modalMode = Enum.Parse(modeType, modalModeName);
        return method.Invoke(null, new object[] { modalMode, confirmPressed, retryPressed, returnToLobbyPressed });
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

public sealed class IRGameClearPanelSessionActionsTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [Test]
    public void ResolveSessionController_WithAssignedSession_ReturnsAssignedInstance()
    {
        Type actionsType = FindTypeByName("IRGameClearPanelSessionActions");
        Type sessionType = FindTypeByName("RRunSessionController");
        Assert.That(actionsType, Is.Not.Null, "IRGameClearPanelSessionActions type is missing.");
        Assert.That(sessionType, Is.Not.Null, "RRunSessionController type is missing.");

        MethodInfo method = actionsType.GetMethod("ResolveSessionController", StaticFlags);
        Assert.That(method, Is.Not.Null, "ResolveSessionController method is missing.");

        GameObject sessionObject = new("IRGameClearPanelSessionActionsTests_Assigned");

        try
        {
            Component sessionInstance = sessionObject.AddComponent(sessionType);
            object resolved = method.Invoke(null, new object[] { sessionInstance });
            Assert.That(resolved, Is.SameAs(sessionInstance));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sessionObject);
        }
    }

    [Test]
    public void ResolveSessionController_WithAssignedSession_PrefersAssignedOverSingleton()
    {
        Type actionsType = FindTypeByName("IRGameClearPanelSessionActions");
        Type sessionType = FindTypeByName("RRunSessionController");
        Assert.That(actionsType, Is.Not.Null, "IRGameClearPanelSessionActions type is missing.");
        Assert.That(sessionType, Is.Not.Null, "RRunSessionController type is missing.");

        MethodInfo method = actionsType.GetMethod("ResolveSessionController", StaticFlags);
        FieldInfo instanceField = sessionType.GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "ResolveSessionController method is missing.");
        Assert.That(instanceField, Is.Not.Null, "RRunSessionController.instance field is missing.");

        object originalSingleton = instanceField.GetValue(null);
        GameObject singletonObject = new("IRGameClearPanelSessionActionsTests_Singleton");
        GameObject assignedObject = new("IRGameClearPanelSessionActionsTests_Assigned");

        try
        {
            Component singletonSession = singletonObject.AddComponent(sessionType);
            Component assignedSession = assignedObject.AddComponent(sessionType);
            instanceField.SetValue(null, singletonSession);
            object resolved = method.Invoke(null, new object[] { assignedSession });
            Assert.That(resolved, Is.SameAs(assignedSession));
        }
        finally
        {
            instanceField.SetValue(null, originalSingleton);
            UnityEngine.Object.DestroyImmediate(singletonObject);
            UnityEngine.Object.DestroyImmediate(assignedObject);
        }
    }

    [Test]
    public void SessionActions_UseSceneLocalFallbackBeforeGlobalSession()
    {
        string source = File.ReadAllText("Assets/Scripts/Rebuild/UI/IRGameClearPanelGameplayGate.cs");
        string viewSource = File.ReadAllText("Assets/Scripts/Rebuild/UI/IRGameClearPanelView.cs");

        Assert.That(source, Does.Contain("ResolveSessionControllerForScene"));
        Assert.That(source, Does.Contain("RRunSessionResolver.ResolveForScene(scene)"));
        Assert.That(source, Does.Not.Contain("FindFirstObjectByType<RRunSessionController>"));
        Assert.That(viewSource, Does.Contain("IRGameClearPanelSessionActions.ResolveSessionControllerForScene(runSessionController, gameObject.scene)"));
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

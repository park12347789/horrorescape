using System;
using System.IO;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

public sealed class MainEscapeDebugModeControllerEditModeTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string DebugControllerSourcePath = "Assets/Scripts/Objectives/MainEscapeDebugModeController.cs";

    [Test]
    public void InvincibilityOnlyMode_LeavesFogStateUntouched()
    {
        GameObject playerObject = new("Player");
        GameObject fogObject = new("Fog");

        try
        {
            Type playerHealthType = FindTypeByName("PlayerHealth");
            Type debugControllerType = FindTypeByName("MainEscapeDebugModeController");
            Type fogOverlayType = FindTypeByName("FlashlightFogOfWarOverlay");

            Assert.That(playerHealthType, Is.Not.Null, "PlayerHealth type is missing.");
            Assert.That(debugControllerType, Is.Not.Null, "MainEscapeDebugModeController type is missing.");
            Assert.That(fogOverlayType, Is.Not.Null, "FlashlightFogOfWarOverlay type is missing.");

            Component playerHealth = playerObject.AddComponent(playerHealthType);
            Component controller = playerObject.AddComponent(debugControllerType);

            fogObject.AddComponent<SpriteRenderer>();
            Component fogOverlay = fogObject.AddComponent(fogOverlayType);

            SetPrivateField(controller, "playerHealth", playerHealth);
            InvokeInstanceMethod(controller, "Initialize", null, null, null, null, fogOverlay, false);

            InvokeInstanceMethod(controller, "SetInvincibilityOnlyModeEnabled", true);

            Assert.That(ReadBoolProperty(controller, "DebugModeEnabled"), Is.False);
            Assert.That(ReadBoolProperty(controller, "InvincibilityOnlyModeEnabled"), Is.True);
            Assert.That(ReadBoolProperty(playerHealth, "IsInvincible"), Is.True);
            Assert.That(ReadBoolProperty(fogOverlay, "BypassEnabled"), Is.False);

            InvokeInstanceMethod(controller, "SetInvincibilityOnlyModeEnabled", false);

            Assert.That(ReadBoolProperty(playerHealth, "IsInvincible"), Is.False);
            Assert.That(ReadBoolProperty(fogOverlay, "BypassEnabled"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fogObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void InvincibilityOnlyMode_PersistsAfterFullDebugTurnsOff()
    {
        GameObject playerObject = new("Player");
        GameObject fogObject = new("Fog");

        try
        {
            Type playerHealthType = FindTypeByName("PlayerHealth");
            Type debugControllerType = FindTypeByName("MainEscapeDebugModeController");
            Type fogOverlayType = FindTypeByName("FlashlightFogOfWarOverlay");

            Assert.That(playerHealthType, Is.Not.Null, "PlayerHealth type is missing.");
            Assert.That(debugControllerType, Is.Not.Null, "MainEscapeDebugModeController type is missing.");
            Assert.That(fogOverlayType, Is.Not.Null, "FlashlightFogOfWarOverlay type is missing.");

            Component playerHealth = playerObject.AddComponent(playerHealthType);
            Component controller = playerObject.AddComponent(debugControllerType);

            fogObject.AddComponent<SpriteRenderer>();
            Component fogOverlay = fogObject.AddComponent(fogOverlayType);

            SetPrivateField(controller, "playerHealth", playerHealth);
            InvokeInstanceMethod(controller, "Initialize", null, null, null, null, fogOverlay, false);

            InvokeInstanceMethod(controller, "SetInvincibilityOnlyModeEnabled", true);
            InvokeInstanceMethod(controller, "SetDebugModeEnabled", true);

            Assert.That(ReadBoolProperty(playerHealth, "IsInvincible"), Is.True);
            Assert.That(ReadBoolProperty(fogOverlay, "BypassEnabled"), Is.EqualTo(ReadDebugDisablesFogOfWar()));

            InvokeInstanceMethod(controller, "SetDebugModeEnabled", false);

            Assert.That(ReadBoolProperty(controller, "InvincibilityOnlyModeEnabled"), Is.True);
            Assert.That(ReadBoolProperty(playerHealth, "IsInvincible"), Is.True);
            Assert.That(ReadBoolProperty(fogOverlay, "BypassEnabled"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fogObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void Initialize_WithDebugOff_DoesNotQueueStartupOffMessage()
    {
        GameObject playerObject = new("Player");
        GameObject fogObject = new("Fog");

        try
        {
            Type debugControllerType = FindTypeByName("MainEscapeDebugModeController");
            Type fogOverlayType = FindTypeByName("FlashlightFogOfWarOverlay");

            Assert.That(debugControllerType, Is.Not.Null, "MainEscapeDebugModeController type is missing.");
            Assert.That(fogOverlayType, Is.Not.Null, "FlashlightFogOfWarOverlay type is missing.");

            Component controller = playerObject.AddComponent(debugControllerType);
            fogObject.AddComponent<SpriteRenderer>();
            Component fogOverlay = fogObject.AddComponent(fogOverlayType);

            InvokeInstanceMethod(controller, "Initialize", null, null, null, null, fogOverlay, false);

            Assert.That(ReadPrivateField<string>(controller, "statusMessage"), Is.EqualTo(string.Empty));
            Assert.That(ReadPrivateField<float>(controller, "statusMessageUntilTime"), Is.LessThanOrEqualTo(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fogObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void Update_RefreshesReferencesOnlyWhenMissingOrDirty()
    {
        string source = File.ReadAllText(DebugControllerSourcePath);
        bool updateUsesRefreshGate = source.Contains(
            "private void Update()\r\n    {\r\n        RefreshReferencesFromUpdateIfNeeded();",
            StringComparison.Ordinal)
            || source.Contains(
                "private void Update()\n    {\n        RefreshReferencesFromUpdateIfNeeded();",
                StringComparison.Ordinal);

        Assert.That(source, Does.Contain("private void RefreshReferencesFromUpdateIfNeeded()"));
        Assert.That(source, Does.Contain("private bool ShouldRefreshReferencesFromUpdate()"));
        Assert.That(source, Does.Contain("ReferenceRefreshRetryInterval"));
        Assert.That(updateUsesRefreshGate, Is.True);
        Assert.That(source, Does.Not.Contain("private void Update()\r\n    {\r\n        CacheReferences();"));
        Assert.That(source, Does.Not.Contain("private void Update()\n    {\n        CacheReferences();"));
    }

    private static bool ReadDebugDisablesFogOfWar()
    {
        object settings = LoadRuntimeSettings();
        return ReadBoolProperty(settings, "DebugDisablesFogOfWar");
    }

    private static object LoadRuntimeSettings()
    {
        Type settingsType = FindTypeByName("MainEscapeRuntimeSettings");
        Assert.That(settingsType, Is.Not.Null, "MainEscapeRuntimeSettings type is missing.");

        MethodInfo loadMethod = settingsType.GetMethod("Load", StaticFlags);
        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimeSettings.Load is missing.");
        return loadMethod.Invoke(null, null);
    }

    private static bool ReadBoolProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance) is bool value && value;
    }

    private static object InvokeInstanceMethod(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        return method.Invoke(instance, arguments);
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} field is missing.");
        field.SetValue(instance, value);
    }

    private static T ReadPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} field is missing.");
        return (T)field.GetValue(instance);
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

using System;
using System.Collections;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

public sealed class CeilingVentEnemyControllerOpeningEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void ConfigureRuntimeBehavior_WhenPlayerFollowEnabled_SchedulesOpeningGraceAndSearchWindows()
    {
        GameObject enemyRoot = new("VentEnemy");

        try
        {
            Component controller = MainEscapeReflectionTestHelper.AddComponent(enemyRoot, "CeilingVentEnemyController");
            SetField(controller, "initialPlayerFollowGraceDuration", 1.5f);
            SetField(controller, "initialPlayerFollowSearchDuration", 2.25f);

            float beforeConfigureTime = Time.time;
            InvokePublic(
                controller,
                "ConfigureRuntimeBehavior",
                true,
                true,
                0.18f,
                true);

            float graceUntilTime = GetField<float>(controller, "playerFollowStartupGraceUntilTime");
            float searchUntilTime = GetField<float>(controller, "playerFollowSearchUntilTime");
            float nextRetargetTime = GetField<float>(controller, "nextPlayerFollowRetargetTime");

            Assert.That(graceUntilTime, Is.GreaterThanOrEqualTo(beforeConfigureTime + 1.45f));
            Assert.That(searchUntilTime, Is.GreaterThanOrEqualTo(graceUntilTime + 2.2f));
            Assert.That(nextRetargetTime, Is.EqualTo(graceUntilTime).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyRoot);
        }
    }

    [Test]
    public void TryUpdatePlayerVentFollow_ReturnsFalseDuringStartupGrace()
    {
        GameObject enemyRoot = new("VentEnemy");
        GameObject playerRoot = new("Player");

        try
        {
            Component controller = MainEscapeReflectionTestHelper.AddComponent(enemyRoot, "CeilingVentEnemyController");
            Component playerTarget = MainEscapeReflectionTestHelper.AddComponent(playerRoot, "VisibilityTarget2D");
            SetField(controller, "followPlayerInVentNetwork", true);
            SetField(controller, "playerTarget", playerTarget);
            SetField(controller, "currentNodeId", 0);
            SetField(controller, "playerFollowStartupGraceUntilTime", Time.time + 5f);
            AddVentNode(controller, nodeId: 0, new Vector3Int(0, 0, 0), isCorridor: true, roomId: -1);

            bool hasFollowIntent = InvokePrivate<bool>(controller, "TryUpdatePlayerVentFollow", true);
            IList currentNodePath = GetField(controller, "currentNodePath") as IList;

            Assert.That(hasFollowIntent, Is.False);
            Assert.That(currentNodePath, Is.Not.Null);
            Assert.That(currentNodePath.Count, Is.EqualTo(0));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerRoot);
            UnityEngine.Object.DestroyImmediate(enemyRoot);
        }
    }

    [Test]
    public void ShouldEmergeImmediatelyForNoise_ReturnsFalseForLowPriorityNoiseDuringOpeningSearch()
    {
        GameObject enemyRoot = new("VentEnemy");

        try
        {
            Component controller = MainEscapeReflectionTestHelper.AddComponent(enemyRoot, "CeilingVentEnemyController");
            SetField(controller, "playerFollowStartupGraceUntilTime", Time.time - 0.1f);
            SetField(controller, "playerFollowSearchUntilTime", Time.time + 5f);

            bool shouldEmergeImmediately = InvokePrivate<bool>(controller, "ShouldEmergeImmediatelyForNoise", 1);

            Assert.That(shouldEmergeImmediately, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyRoot);
        }
    }

    [Test]
    public void ShouldEmergeImmediatelyForNoise_StillAllowsHighPriorityNoiseDuringOpeningSearch()
    {
        GameObject enemyRoot = new("VentEnemy");

        try
        {
            Component controller = MainEscapeReflectionTestHelper.AddComponent(enemyRoot, "CeilingVentEnemyController");
            SetField(controller, "playerFollowStartupGraceUntilTime", Time.time - 0.1f);
            SetField(controller, "playerFollowSearchUntilTime", Time.time + 5f);

            bool shouldEmergeImmediately = InvokePrivate<bool>(controller, "ShouldEmergeImmediatelyForNoise", 3);

            Assert.That(shouldEmergeImmediately, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyRoot);
        }
    }

    private static void AddVentNode(Component controller, int nodeId, Vector3Int cell, bool isCorridor, int roomId)
    {
        Type nodeType = controller.GetType().GetNestedType("VentNode", BindingFlags.NonPublic);
        Assert.That(nodeType, Is.Not.Null, "CeilingVentEnemyController.VentNode type is missing.");

        object node = Activator.CreateInstance(
            nodeType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { nodeId, cell, isCorridor, roomId },
            culture: null);
        IList nodes = GetField(controller, "nodes") as IList;
        Assert.That(nodes, Is.Not.Null, "nodes field should be a list.");
        nodes.Add(node);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} field is missing.");
        return (T)field.GetValue(target);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} field is missing.");
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} field is missing.");
        field.SetValue(target, value);
    }

    private static T InvokePrivate<T>(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}() is missing.");
        return (T)method.Invoke(target, args);
    }

    private static void InvokePublic(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}() is missing.");
        method.Invoke(target, args);
    }
}

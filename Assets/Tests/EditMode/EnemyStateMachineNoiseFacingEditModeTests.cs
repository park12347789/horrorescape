using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class EnemyStateMachineNoiseFacingEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string EnemyStateMachineSourcePath = "Assets/Scripts/Enemy/EnemyStateMachine.cs";

    [Test]
    public void SetInvestigateFocus_StandGuardFacesExactNoiseWorldPosition()
    {
        GameObject enemyObject = new("EnemyNoiseFacingFixture");
        Component stateMachine = null;
        ScriptableObject archetype = null;

        try
        {
            stateMachine = enemyObject.AddComponent(FindRequiredType("EnemyStateMachine"));
            archetype = CreateStandGuardArchetype();
            SetField(stateMachine, "archetype", archetype);

            Vector2 enemyPosition = new(7.48f, 0.5f);
            Vector2 noisePosition = new(3.13f, -1.44f);
            enemyObject.transform.position = enemyPosition;

            Invoke(stateMachine, "SetInvestigateFocus", new object[] { noisePosition });

            Vector2 facingDirection = (Vector2)GetField(stateMachine, "facingDirection");
            Vector2 expectedDirection = (noisePosition - enemyPosition).normalized;

            Assert.That(Vector2.Dot(facingDirection.normalized, expectedDirection), Is.GreaterThan(0.999f));
        }
        finally
        {
            if (enemyObject != null)
            {
                UnityObject.DestroyImmediate(enemyObject);
            }

            if (archetype != null)
            {
                UnityObject.DestroyImmediate(archetype);
            }
        }
    }

    [Test]
    public void CollectReachableIdleCandidates_UsesReusableScratchBuffers()
    {
        string source = File.ReadAllText(EnemyStateMachineSourcePath);

        Assert.That(source, Does.Contain("private readonly Queue<Vector3Int> idleCandidateFrontier"));
        Assert.That(source, Does.Contain("private readonly Dictionary<Vector3Int, int> idleCandidateDistanceByCell"));
        Assert.That(source, Does.Not.Contain("Queue<Vector3Int> frontier = new()"));
        Assert.That(source, Does.Not.Contain("Dictionary<Vector3Int, int> distanceByCell = new()"));
    }

    private static ScriptableObject CreateStandGuardArchetype()
    {
        Type archetypeType = FindRequiredType("EnemyArchetype");
        Type idleBehaviorType = FindRequiredType("EnemyIdleBehavior");
        Type alertRecoveryType = FindRequiredType("EnemyAlertRecoveryBehavior");
        ScriptableObject archetype = ScriptableObject.CreateInstance(archetypeType);
        MethodInfo configureMethod = archetypeType.GetMethod(
            "Configure",
            MemberFlags,
            null,
            new[]
            {
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(int),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(int),
                idleBehaviorType,
                typeof(float),
                alertRecoveryType
            },
            null);

        Assert.That(configureMethod, Is.Not.Null, "EnemyArchetype.Configure() is missing.");
        configureMethod.Invoke(
            archetype,
            new[]
            {
                0f,
                1.8f,
                2.7f,
                5.4f,
                48f,
                4.5f,
                0.1f,
                1,
                0.22f,
                0.8f,
                0.7f,
                1,
                Enum.Parse(idleBehaviorType, "StandGuard"),
                0.42f,
                Enum.Parse(alertRecoveryType, "SearchArea")
            });
        return archetype;
    }

    private static void SetField(Component component, string fieldName, object value)
    {
        FieldInfo field = component.GetType().GetField(fieldName, MemberFlags);
        Assert.That(field, Is.Not.Null, $"{component.GetType().Name}.{fieldName} is missing.");
        field.SetValue(component, value);
    }

    private static object GetField(Component component, string fieldName)
    {
        FieldInfo field = component.GetType().GetField(fieldName, MemberFlags);
        Assert.That(field, Is.Not.Null, $"{component.GetType().Name}.{fieldName} is missing.");
        return field.GetValue(component);
    }

    private static void Invoke(Component component, string methodName, object[] arguments)
    {
        MethodInfo method = component.GetType().GetMethod(methodName, MemberFlags);
        Assert.That(method, Is.Not.Null, $"{component.GetType().Name}.{methodName}() is missing.");
        method.Invoke(component, arguments);
    }

    private static Type FindRequiredType(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] candidateTypes;

            try
            {
                candidateTypes = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                candidateTypes = exception.Types;
            }

            if (candidateTypes == null)
            {
                continue;
            }

            for (int typeIndex = 0; typeIndex < candidateTypes.Length; typeIndex++)
            {
                Type candidate = candidateTypes[typeIndex];

                if (candidate != null && (candidate.Name == typeName || candidate.FullName == typeName))
                {
                    return candidate;
                }
            }
        }

        Assert.Fail($"{typeName} type is missing.");
        return null;
    }
}

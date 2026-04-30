using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Reflection;

public sealed class MainEscapeCeilingVentEnemyPrefabContractEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string VentEnemyPrefabPath = "Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab";
    private const string VentEnemyBootstrapSourcePath = "Assets/Scripts/Enemy/BaseOfficeVentEnemyBootstrap.cs";

    [Test]
    public void CeilingVentEnemyPrefab_KeepsSpottedScreamAudioAndBindings()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VentEnemyPrefabPath);

        Assert.That(prefabAsset, Is.Not.Null, $"Missing ceiling vent enemy prefab at '{VentEnemyPrefabPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(VentEnemyPrefabPath);

        try
        {
            Type spottedAudioType = FindTypeByName("EnemyPlayerSpottedScreamAudio");
            Type bindingsType = FindTypeByName("CeilingVentEnemyPrefabBindings");
            Assert.That(spottedAudioType, Is.Not.Null, "EnemyPlayerSpottedScreamAudio type is missing.");
            Assert.That(bindingsType, Is.Not.Null, "CeilingVentEnemyPrefabBindings type is missing.");

            Component spottedScreamAudio = prefabRoot.GetComponent(spottedAudioType);
            Component bindings = prefabRoot.GetComponent(bindingsType);

            Assert.That(spottedScreamAudio, Is.Not.Null, "Ceiling vent enemy prefab root must keep the spotted scream audio component.");
            Assert.That(bindings, Is.Not.Null, "Ceiling vent enemy prefab root must keep prefab bindings.");
            Assert.That(spottedScreamAudio.gameObject, Is.SameAs(bindings.gameObject), "Spotted scream audio and prefab bindings must live on the same root GameObject.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void SpottedScreamAudio_InitializeResubscribesAfterRuntimeControllerIsAdded()
    {
        Type spottedAudioType = FindTypeByName("EnemyPlayerSpottedScreamAudio");
        Type controllerType = FindTypeByName("CeilingVentEnemyController");
        Assert.That(spottedAudioType, Is.Not.Null, "EnemyPlayerSpottedScreamAudio type is missing.");
        Assert.That(controllerType, Is.Not.Null, "CeilingVentEnemyController type is missing.");

        GameObject enemyObject = new("VentEnemySubscriptionTest");

        try
        {
            Component spottedAudio = enemyObject.AddComponent(spottedAudioType);
            Component controller = enemyObject.AddComponent(controllerType);

            Invoke(spottedAudio, "Initialize", new object[] { null });

            FieldInfo playerSpotSourceField = spottedAudioType.GetField("playerSpotSource", InstanceFlags);
            Assert.That(playerSpotSourceField, Is.Not.Null, "EnemyPlayerSpottedScreamAudio.playerSpotSource is missing.");
            Assert.That(playerSpotSourceField.GetValue(spottedAudio), Is.SameAs(controller));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void VentEnemyPathing_UsesReusableSearchBuffers()
    {
        string source = File.ReadAllText(VentEnemyBootstrapSourcePath);

        Assert.That(source, Does.Contain("private readonly List<int> rebuiltNodePathScratch"));
        Assert.That(source, Does.Contain("private readonly Queue<int> nodePathFrontier"));
        Assert.That(source, Does.Contain("private readonly Dictionary<int, int> previousByNodeScratch"));
        Assert.That(source, Does.Contain("private readonly Queue<int> fallbackNodeFrontier"));
        Assert.That(source, Does.Contain("private readonly HashSet<int> fallbackVisitedNodes"));
        Assert.That(source, Does.Not.Contain("List<int> rebuiltPath = new()"));
        Assert.That(source, Does.Not.Contain("Queue<int> frontier = new();"));
        Assert.That(source, Does.Not.Contain("Dictionary<int, int> previousByNode = new();"));
        Assert.That(source, Does.Not.Contain("HashSet<int> visited = new();"));
    }

    private static void Invoke(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        method.Invoke(instance, arguments);
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

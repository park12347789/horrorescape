using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

public sealed class NoiseSystemDebugPulseEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string NoiseSystemSourcePath = "Assets/Scripts/Noise/NoiseSystem.cs";
    private const string NoiseEmitterSourcePath = "Assets/Scripts/Noise/NoiseEmitter.cs";
    private const string NoiseFloorPanelSourcePath = "Assets/Scripts/Noise/NoiseFloorPanel.cs";
    private const string ThrowableBottleProjectileSourcePath = "Assets/Scripts/Inventory/ThrowableBottleProjectile.cs";
    private const string MedicalCabinetMedkitInteractableSourcePath = "Assets/Scripts/Objectives/MedicalCabinetMedkitInteractable.cs";

    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        ClearNoiseSystemInstance();
        ClearSharedLoudFloorVisualCooldowns();
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                UnityObject.DestroyImmediate(createdObjects[index]);
            }
        }

        createdObjects.Clear();
        ClearNoiseSystemInstance();
        ClearSharedLoudFloorVisualCooldowns();
    }

    [Test]
    public void EmitNoise_WithSuppressedDebugPulse_StillStoresRecentEvent()
    {
        Component system = CreateNoiseSystem();

        InvokeEmitNoise(
            system,
            Vector2.zero,
            4f,
            ParseEnum("NoiseSourceType", "LoudFloor"),
            12,
            ParseEnum("NoiseEmitterAffiliation", "Player"),
            false);

        Assert.That(ReadIntProperty(system, "RecentEventCount"), Is.EqualTo(1));
        Assert.That(GetActiveDebugPulseCount(system), Is.EqualTo(0));
    }

    [Test]
    public void EmitNoise_RepeatedMovementFromSameEmitter_ThrottlesVisualPulseOnly()
    {
        Component system = CreateNoiseSystem();
        SetField(system, "frequentNoiseDebugPulsesEnabled", true);
        SetField(system, "walkDebugPulseRenderCooldown", 1f);
        SetField(system, "movementDebugPulseRenderInterval", 1);
        object walk = ParseEnum("NoiseSourceType", "Walk");
        object player = ParseEnum("NoiseEmitterAffiliation", "Player");

        InvokeEmitNoise(system, Vector2.zero, 2f, walk, 41, player, true);
        InvokeEmitNoise(system, Vector2.right, 2f, walk, 41, player, true);
        InvokeEmitNoise(system, Vector2.up, 2f, walk, 82, player, true);

        Assert.That(ReadIntProperty(system, "RecentEventCount"), Is.EqualTo(3), "AI-readable noise events should not be dropped.");
        Assert.That(GetActiveDebugPulseCount(system), Is.EqualTo(2), "Only the repeated same-emitter visual pulse should be throttled.");
    }

    [Test]
    public void EmitNoise_FrequentDebugPulsesDisabledByDefault_StillStoresRecentEvents()
    {
        Component system = CreateNoiseSystem();
        object player = ParseEnum("NoiseEmitterAffiliation", "Player");

        InvokeEmitNoise(system, Vector2.zero, 2f, ParseEnum("NoiseSourceType", "Walk"), 41, player, true);
        InvokeEmitNoise(system, Vector2.right, 2f, ParseEnum("NoiseSourceType", "LoudFloor"), 41, player, true);
        InvokeEmitNoise(system, Vector2.up, 2f, ParseEnum("NoiseSourceType", "VentCrawl"), 41, player, true);

        Assert.That(ReadIntProperty(system, "RecentEventCount"), Is.EqualTo(3), "AI-readable noise events should remain live.");
        Assert.That(GetActiveDebugPulseCount(system), Is.EqualTo(0), "Frequent gameplay noise should not create default visual pulse load.");
    }

    [Test]
    public void DebugPulses_ClampSerializedActiveLimit()
    {
        Component system = CreateNoiseSystem();
        SetField(system, "maxActiveDebugPulses", 16);
        object door = ParseEnum("NoiseSourceType", "Door");
        object player = ParseEnum("NoiseEmitterAffiliation", "Player");

        for (int index = 0; index < 6; index++)
        {
            InvokeEmitNoise(system, Vector2.right * index, 2f, door, index, player, true);
        }

        Assert.That(GetActiveDebugPulseCount(system), Is.EqualTo(4));
    }

    [Test]
    public void DebugPulses_ClampVisibleLifetime()
    {
        Component system = CreateNoiseSystem();
        SetField(system, "eventLifetime", 3f);
        SetField(system, "debugPulseLifetime", 3f);

        InvokeEmitNoise(
            system,
            Vector2.zero,
            3f,
            ParseEnum("NoiseSourceType", "Door"),
            7,
            ParseEnum("NoiseEmitterAffiliation", "Player"),
            true);

        object pulse = GetActiveDebugPulse(system, 0);

        Assert.That(GetFloatField(pulse, "pulseLifetime"), Is.EqualTo(0.32f).Within(0.001f));
        Assert.That(GetFloatField(pulse, "pulseAnimationLifetime"), Is.EqualTo(0.32f).Within(0.001f));
    }

    [Test]
    public void MovementPulse_WhenEnabled_UsesCappedSpreadLifetimeAndThinPresentation()
    {
        Component system = CreateNoiseSystem();
        SetField(system, "frequentNoiseDebugPulsesEnabled", true);
        SetField(system, "eventLifetime", 0.6f);
        SetField(system, "debugPulseLifetime", 0.1f);
        SetField(system, "movementDebugPulseSpreadAnimationLifetime", 1.25f);
        SetField(system, "movementDebugPulseWidthScale", 0.9f);
        SetField(system, "movementDebugPulseAlphaScale", 0.8f);
        SetField(system, "movementDebugPulseWidthMultiplier", 0.68f);
        SetField(system, "movementDebugPulseAlphaMultiplier", 0.72f);

        InvokeEmitNoise(
            system,
            Vector2.zero,
            3f,
            ParseEnum("NoiseSourceType", "Walk"),
            7,
            ParseEnum("NoiseEmitterAffiliation", "Player"),
            true);

        object pulse = GetActiveDebugPulse(system, 0);

        Assert.That(GetFloatField(pulse, "pulseLifetime"), Is.EqualTo(0.22f).Within(0.001f));
        Assert.That(GetFloatField(pulse, "pulseAnimationLifetime"), Is.EqualTo(0.22f).Within(0.001f));
        Assert.That(GetFloatField(pulse, "pulseWidthScale"), Is.EqualTo(0.28f).Within(0.001f));
        Assert.That(GetFloatField(pulse, "pulseAlphaScale"), Is.EqualTo(0.36f).Within(0.001f));
    }

    [Test]
    public void DebugPulse_VisualRadiusStaysNearNoiseSource()
    {
        Component system = CreateNoiseSystem();

        InvokeEmitNoise(
            system,
            Vector2.zero,
            8f,
            ParseEnum("NoiseSourceType", "Door"),
            7,
            ParseEnum("NoiseEmitterAffiliation", "Player"),
            true);

        object pulse = GetActiveDebugPulse(system, 0);

        Assert.That(GetFloatField(pulse, "pulseRadius"), Is.EqualTo(0.72f).Within(0.001f));
    }

    [Test]
    public void NoiseDebugPulse_UsesBatchedLineRendererPositionUpdates()
    {
        string source = File.ReadAllText(NoiseSystemSourcePath);

        Assert.That(source, Does.Contain("private readonly Vector3[] scaledCirclePoints"));
        Assert.That(source, Does.Contain("targetRenderer.SetPositions(scaledCirclePoints)"));
        Assert.That(source, Does.Not.Contain("targetRenderer.SetPosition(index"));
    }

    [Test]
    public void PlayerNoiseEmitter_SuppressesAttachedDebugPulses()
    {
        string source = File.ReadAllText(NoiseEmitterSourcePath);

        Assert.That(source, Does.Contain("NoiseEmitterAffiliation.Player"));
        Assert.That(source, Does.Contain("allowDebugPulse: false"));
    }

    [Test]
    public void RuntimeNoiseProducers_EmitThroughNoiseEventBus()
    {
        string[] sources =
        {
            File.ReadAllText(NoiseEmitterSourcePath),
            File.ReadAllText(ThrowableBottleProjectileSourcePath),
            File.ReadAllText(MedicalCabinetMedkitInteractableSourcePath)
        };

        for (int index = 0; index < sources.Length; index++)
        {
            Assert.That(sources[index], Does.Contain("private INoiseEventBus noiseEventBus"));
            Assert.That(sources[index], Does.Contain("ConfigureNoiseEventBus"));
            Assert.That(sources[index], Does.Contain("NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem)"));
            Assert.That(sources[index], Does.Not.Contain("NoiseSystem.TryEmitNoise"));
        }
    }

    [Test]
    public void NoiseSystem_KeepsSceneLocalEventBusesInsteadOfDestroyingDuplicates()
    {
        string source = File.ReadAllText(NoiseSystemSourcePath);

        Assert.That(source, Does.Contain("private bool ownsLegacyInstance"));
        Assert.That(source, Does.Contain("if (instance == null)"));
        Assert.That(source, Does.Not.Contain("if (instance != null && instance != this)"));
        Assert.That(source, Does.Not.Contain("Destroy(gameObject);\r\n            return;").And.Not.Contain("Destroy(gameObject);\n            return;"));
    }

    [Test]
    public void NoiseEventBusResolver_UsesRequestedSceneBeforeActiveScene()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene distractorScene = SceneManager.CreateScene("NoiseResolver_Distractor");
        Scene targetScene = SceneManager.CreateScene("NoiseResolver_Target");
        GameObject distractorObject = CreateSceneObjectWithNoiseSystem(distractorScene, "DistractorNoiseSystem");
        GameObject targetObject = CreateSceneObjectWithNoiseSystem(targetScene, "TargetNoiseSystem");
        Component distractorSystem = GetNoiseSystem(distractorObject);
        Component targetSystem = GetNoiseSystem(targetObject);

        try
        {
            SceneManager.SetActiveScene(distractorScene);

            Assert.That(InvokeResolveNoiseSystem(targetScene), Is.SameAs(targetSystem));
            Assert.That(InvokeResolveNoiseSystem(targetScene, distractorSystem), Is.SameAs(distractorSystem));
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            UnityObject.DestroyImmediate(distractorObject);
            UnityObject.DestroyImmediate(targetObject);
            EditorSceneManager.CloseScene(targetScene, true);
            EditorSceneManager.CloseScene(distractorScene, true);
            ClearNoiseSystemInstance();
        }
    }

    [Test]
    public void NoiseFloorPanel_ThrottlesRepeatedReflectionInfluenceSampling()
    {
        string source = File.ReadAllText(NoiseFloorPanelSourcePath);

        Assert.That(source, Does.Contain("private bool ShouldRefreshReflectionInfluenceSample()"));
        Assert.That(source, Does.Contain("hasReflectionInfluenceSample"));
        Assert.That(source, Does.Contain("nextReflectionInfluenceRefreshTime"));
        Assert.That(source, Does.Contain("reflectionMovementRefreshThreshold"));
    }

    [Test]
    public void NoiseFloorPanel_UsesScenePlayerResolverAndNoiseEventBus()
    {
        string source = File.ReadAllText(NoiseFloorPanelSourcePath);

        Assert.That(source, Does.Contain("AudioScenePlayerReferenceResolver.ResolveCurrentOrSceneFallback"));
        Assert.That(source, Does.Contain("private INoiseEventBus noiseEventBus"));
        Assert.That(source, Does.Contain("NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem)"));
        Assert.That(source, Does.Not.Contain("Object.FindFirstObjectByType<WasdPlayerController>"));
        Assert.That(source, Does.Not.Contain("NoiseSystem.TryEmitNoise"));
    }

    [Test]
    public void NoiseFloorPanel_SharedActorVisualCooldown_AppliesAcrossPanels()
    {
        Component firstPanel = CreatePanel("NoiseFloorPanel_First");
        Component secondPanel = CreatePanel("NoiseFloorPanel_Second");
        SetField(firstPanel, "sharedActorVisualPulseInterval", 1f);
        SetField(secondPanel, "sharedActorVisualPulseInterval", 1f);

        bool firstActorFirstPanel = InvokeBool(firstPanel, "ShouldRequestSharedLoudFloorVisualPulse", 101);
        bool firstActorSecondPanel = InvokeBool(secondPanel, "ShouldRequestSharedLoudFloorVisualPulse", 101);
        bool secondActorSecondPanel = InvokeBool(secondPanel, "ShouldRequestSharedLoudFloorVisualPulse", 202);

        Assert.That(firstActorFirstPanel, Is.True);
        Assert.That(firstActorSecondPanel, Is.False);
        Assert.That(secondActorSecondPanel, Is.True);
    }

    private Component CreateNoiseSystem()
    {
        return CreateComponent("NoiseSystem_DebugPulse_Test", FindTypeByName("NoiseSystem"));
    }

    private static GameObject CreateSceneObjectWithNoiseSystem(Scene scene, string name)
    {
        GameObject gameObject = new(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        gameObject.AddComponent(FindTypeByName("NoiseSystem"));
        return gameObject;
    }

    private static Component GetNoiseSystem(GameObject gameObject)
    {
        return gameObject.GetComponent(FindTypeByName("NoiseSystem"));
    }

    private static object InvokeResolveNoiseSystem(Scene scene, Component explicitNoiseSystem = null)
    {
        Type resolverType = FindTypeByName("NoiseEventBusResolver");
        Type noiseSystemType = FindTypeByName("NoiseSystem");
        Assert.That(resolverType, Is.Not.Null, "NoiseEventBusResolver type is missing.");
        Assert.That(noiseSystemType, Is.Not.Null, "NoiseSystem type is missing.");
        MethodInfo method = resolverType.GetMethod(
            "ResolveSystem",
            StaticFlags,
            null,
            new[] { typeof(Scene), noiseSystemType },
            null);
        Assert.That(method, Is.Not.Null, "NoiseEventBusResolver.ResolveSystem(Scene, NoiseSystem) is missing.");
        return method.Invoke(null, new object[] { scene, explicitNoiseSystem });
    }

    private Component CreatePanel(string name)
    {
        return CreateComponent(name, FindTypeByName("NoiseFloorPanel"));
    }

    private Component CreateComponent(string name, Type componentType)
    {
        Assert.That(componentType, Is.Not.Null, $"{name} component type is missing.");
        GameObject gameObject = CreateObject(name);
        return gameObject.AddComponent(componentType);
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static int GetActiveDebugPulseCount(object system)
    {
        return GetActiveDebugPulses(system).Count;
    }

    private static object GetActiveDebugPulse(object system, int index)
    {
        return GetActiveDebugPulses(system)[index];
    }

    private static IList GetActiveDebugPulses(object system)
    {
        FieldInfo field = system.GetType().GetField("activeDebugPulses", InstanceFlags);
        Assert.That(field, Is.Not.Null, "NoiseSystem.activeDebugPulses is missing.");
        IList pulses = field.GetValue(system) as IList;
        Assert.That(pulses, Is.Not.Null, "NoiseSystem.activeDebugPulses should be list-like.");
        return pulses;
    }

    private static int ReadIntProperty(object owner, string propertyName)
    {
        PropertyInfo property = owner.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{owner.GetType().Name}.{propertyName} is missing.");
        return (int)property.GetValue(owner);
    }

    private static float GetFloatField(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} is missing.");
        return (float)field.GetValue(instance);
    }

    private static void SetField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{fieldName} is missing.");
        field.SetValue(owner, value);
    }

    private static void InvokeEmitNoise(
        object system,
        Vector2 position,
        float radius,
        object sourceType,
        int emitterInstanceId,
        object affiliation,
        bool allowDebugPulse)
    {
        MethodInfo method = system.GetType().GetMethod(
            "EmitNoise",
            InstanceFlags,
            null,
            new[]
            {
                typeof(Vector2),
                typeof(float),
                sourceType.GetType(),
                typeof(int),
                affiliation.GetType(),
                typeof(bool)
            },
            null);
        Assert.That(method, Is.Not.Null, "NoiseSystem.EmitNoise overload with allowDebugPulse is missing.");
        method.Invoke(system, new[] { position, radius, sourceType, emitterInstanceId, affiliation, allowDebugPulse });
    }

    private static bool InvokeBool(object owner, string methodName, int actorId)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{owner.GetType().Name}.{methodName}() is missing.");
        return method.Invoke(owner, new object[] { actorId }) is bool result && result;
    }

    private static object ParseEnum(string typeName, string value)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");
        return Enum.Parse(type, value);
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

    private static void ClearNoiseSystemInstance()
    {
        Type systemType = FindTypeByName("NoiseSystem");
        Assert.That(systemType, Is.Not.Null, "NoiseSystem type is missing.");
        FieldInfo instanceField = systemType.GetField("instance", StaticFlags);
        Assert.That(instanceField, Is.Not.Null, "NoiseSystem.instance is missing.");
        instanceField.SetValue(null, null);
    }

    private static void ClearSharedLoudFloorVisualCooldowns()
    {
        Type panelType = FindTypeByName("NoiseFloorPanel");
        Assert.That(panelType, Is.Not.Null, "NoiseFloorPanel type is missing.");
        FieldInfo cooldownField = panelType.GetField("nextSharedVisualPulseTimesByActorId", StaticFlags);
        Assert.That(cooldownField, Is.Not.Null, "NoiseFloorPanel.nextSharedVisualPulseTimesByActorId is missing.");

        if (cooldownField.GetValue(null) is IDictionary cooldowns)
        {
            cooldowns.Clear();
        }
    }
}

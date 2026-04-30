using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class MainEscapeDoorPassabilityControllerEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void SetPassable_TogglesAllConfiguredBlockers()
    {
        PassabilityFixture fixture = new();

        try
        {
            Configure(fixture.Controller, fixture.PhysicalBlocker, fixture.LightBlocker, fixture.ShadowCaster);

            SetPassable(fixture.Controller, true);
            Assert.That(ReadIsPassable(fixture.Controller), Is.True);
            Assert.That(fixture.PhysicalBlocker.enabled, Is.False);
            Assert.That(fixture.LightBlocker.enabled, Is.False);
            Assert.That(ReadEnabled(fixture.ShadowCaster), Is.False);

            SetPassable(fixture.Controller, false);
            Assert.That(ReadIsPassable(fixture.Controller), Is.False);
            Assert.That(fixture.PhysicalBlocker.enabled, Is.True);
            Assert.That(fixture.LightBlocker.enabled, Is.True);
            Assert.That(ReadEnabled(fixture.ShadowCaster), Is.True);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void Configure_AppliesCurrentPassableStateToNewReferences()
    {
        PassabilityFixture fixture = new();

        try
        {
            SetPassable(fixture.Controller, true);

            Configure(fixture.Controller, fixture.PhysicalBlocker, fixture.LightBlocker, fixture.ShadowCaster);

            Assert.That(ReadIsPassable(fixture.Controller), Is.True);
            Assert.That(fixture.PhysicalBlocker.enabled, Is.False);
            Assert.That(fixture.LightBlocker.enabled, Is.False);
            Assert.That(ReadEnabled(fixture.ShadowCaster), Is.False);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void RuntimeShadowCasterConfigurator_ReusesConfiguredCasterWithoutReenablingIt()
    {
        GameObject root = new("RuntimeShadowCasterRoot");

        try
        {
            Type configuratorType = FindTypeByName("RuntimeShadowCaster2DConfigurator");
            Assert.That(configuratorType, Is.Not.Null, "RuntimeShadowCaster2DConfigurator type is missing.");

            BoxCollider2D sourceCollider = root.AddComponent<BoxCollider2D>();

            Assert.That(TryConfigureShadowCaster(configuratorType, root, sourceCollider, out Component shadowCaster), Is.True);
            Assert.That(shadowCaster, Is.Not.Null);

            SetEnabled(shadowCaster, false);
            Assert.That(TryConfigureShadowCaster(configuratorType, root, sourceCollider, out Component reusedShadowCaster), Is.True);

            Assert.That(reusedShadowCaster, Is.SameAs(shadowCaster));
            Assert.That(ReadEnabled(reusedShadowCaster), Is.False);
        }
        finally
        {
            UnityObject.DestroyImmediate(root);
        }
    }

    [Test]
    public void RuntimeShadowCasterConfigurator_RepairsDestroyedMeshWhenReusingCaster()
    {
        GameObject root = new("RuntimeShadowCasterRepairRoot");

        try
        {
            Type configuratorType = FindTypeByName("RuntimeShadowCaster2DConfigurator");
            Assert.That(configuratorType, Is.Not.Null, "RuntimeShadowCaster2DConfigurator type is missing.");

            BoxCollider2D sourceCollider = root.AddComponent<BoxCollider2D>();

            Assert.That(TryConfigureShadowCaster(configuratorType, root, sourceCollider, out Component shadowCaster), Is.True);
            Assert.That(shadowCaster, Is.Not.Null);

            Mesh shadowMesh = ReadFieldOrProperty(shadowCaster, "mesh") as Mesh;
            Assert.That(shadowMesh, Is.Not.Null, "Configured shadow caster should create a backing mesh.");

            UnityObject.DestroyImmediate(shadowMesh);
            SetEnabled(shadowCaster, false);

            Assert.That(TryConfigureShadowCaster(configuratorType, root, sourceCollider, out Component reusedShadowCaster), Is.True);
            Assert.That(reusedShadowCaster, Is.SameAs(shadowCaster));
            Assert.That(ReadEnabled(reusedShadowCaster), Is.False);
            Assert.That(ReadFieldOrProperty(reusedShadowCaster, "mesh") as Mesh, Is.Not.Null, "Reused shadow casters should rebuild destroyed backing meshes before rendering.");
        }
        finally
        {
            UnityObject.DestroyImmediate(root);
        }
    }

    private static Component CreateController(GameObject root)
    {
        Type controllerType = FindTypeByName("MainEscapeDoorPassabilityController");
        Assert.That(controllerType, Is.Not.Null, "MainEscapeDoorPassabilityController type is missing.");
        return root.AddComponent(controllerType);
    }

    private static void Configure(Component controller, BoxCollider2D physicalBlocker, BoxCollider2D lightBlocker, Component shadowCaster)
    {
        MethodInfo[] methods = controller.GetType().GetMethods(MemberFlags);
        MethodInfo method = null;

        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo candidate = methods[index];
            ParameterInfo[] parameters = candidate.GetParameters();

            if (!string.Equals(candidate.Name, "Configure", StringComparison.Ordinal)
                || parameters.Length != 3
                || parameters[0].ParameterType != typeof(BoxCollider2D)
                || parameters[1].ParameterType != typeof(BoxCollider2D)
                || !string.Equals(parameters[2].ParameterType.Name, "ShadowCaster2D", StringComparison.Ordinal))
            {
                continue;
            }

            method = candidate;
            break;
        }

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.Configure(BoxCollider2D, BoxCollider2D, ShadowCaster2D) is missing.");
        method.Invoke(controller, new object[] { physicalBlocker, lightBlocker, shadowCaster });
    }

    private static void SetPassable(Component controller, bool isPassable)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "SetPassable",
            MemberFlags,
            null,
            new[] { typeof(bool) },
            null);

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.SetPassable(bool) is missing.");
        method.Invoke(controller, new object[] { isPassable });
    }

    private static bool ReadIsPassable(Component controller)
    {
        object value = ReadFieldOrProperty(controller, "IsPassable");
        Assert.That(value, Is.InstanceOf<bool>(), $"{controller.GetType().Name}.IsPassable should resolve to a bool.");
        return (bool)value;
    }

    private static bool ReadEnabled(Component component)
    {
        object value = ReadFieldOrProperty(component, "enabled");
        Assert.That(value, Is.InstanceOf<bool>(), $"{component.GetType().Name}.enabled should resolve to a bool.");
        return (bool)value;
    }

    private static void SetEnabled(Component component, bool enabled)
    {
        PropertyInfo property = component.GetType().GetProperty("enabled", MemberFlags);
        Assert.That(property, Is.Not.Null, $"{component.GetType().Name}.enabled property is missing.");
        property.SetValue(component, enabled);
    }

    private static bool TryConfigureShadowCaster(Type configuratorType, GameObject owner, Collider2D sourceCollider, out Component shadowCaster)
    {
        shadowCaster = null;
        MethodInfo[] methods = configuratorType.GetMethods(StaticMemberFlags);
        MethodInfo method = null;

        for (int index = 0; index < methods.Length; index++)
        {
            MethodInfo candidate = methods[index];
            ParameterInfo[] parameters = candidate.GetParameters();

            if (!string.Equals(candidate.Name, "TryConfigureFromCollider", StringComparison.Ordinal)
                || parameters.Length != 3
                || parameters[0].ParameterType != typeof(GameObject)
                || parameters[1].ParameterType != typeof(Collider2D)
                || !parameters[2].IsOut
                || !string.Equals(parameters[2].ParameterType.GetElementType()?.Name, "ShadowCaster2D", StringComparison.Ordinal))
            {
                continue;
            }

            method = candidate;
            break;
        }

        Assert.That(method, Is.Not.Null, $"{configuratorType.Name}.TryConfigureFromCollider(GameObject, Collider2D, out ShadowCaster2D) is missing.");
        object[] arguments = { owner, sourceCollider, null };
        object result = method.Invoke(null, arguments);
        Assert.That(result, Is.InstanceOf<bool>(), $"{configuratorType.Name}.TryConfigureFromCollider should return a bool.");
        shadowCaster = arguments[2] as Component;
        return (bool)result;
    }

    private static object ReadFieldOrProperty(object instance, string memberName)
    {
        PropertyInfo property = instance.GetType().GetProperty(memberName, MemberFlags);
        if (property != null)
        {
            return property.GetValue(instance);
        }

        FieldInfo field = instance.GetType().GetField(memberName, MemberFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{memberName} member is missing.");
        return field.GetValue(instance);
    }

    private static Type FindTypeByName(string typeName)
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

        return null;
    }

    private sealed class PassabilityFixture : IDisposable
    {
        public PassabilityFixture()
        {
            Root = new GameObject("DoorPassabilityRoot");
            Controller = CreateController(Root);
            PhysicalBlocker = Root.AddComponent<BoxCollider2D>();
            LightBlocker = Root.AddComponent<BoxCollider2D>();
            ShadowCaster = CreateShadowCaster(Root);
        }

        public GameObject Root { get; }

        public Component Controller { get; }

        public BoxCollider2D PhysicalBlocker { get; }

        public BoxCollider2D LightBlocker { get; }

        public Component ShadowCaster { get; }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityObject.DestroyImmediate(Root);
            }
        }

        private static Component CreateShadowCaster(GameObject root)
        {
            Type shadowCasterType = FindTypeByName("ShadowCaster2D");
            Assert.That(shadowCasterType, Is.Not.Null, "ShadowCaster2D type is missing.");
            return root.AddComponent(shadowCasterType);
        }
    }
}

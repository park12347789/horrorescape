using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MainEscapeDoorVariantResolutionEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void BindAuthoredVisualRoots_ExplicitVariantOverride_BeatsNameHeuristic()
    {
        DoorVariantFixture fixture = new();

        try
        {
            AttachDoorVariantOverride(fixture.VisualRoot.gameObject, "FrontDoor");

            BindAuthoredVisualRoots(fixture.Controller, fixture.VisualRoot);

            Assert.That(IsSideDoorVisual(fixture.Controller), Is.False, "An explicit FrontDoor override should win even when the visual root name matches the legacy side-door heuristic.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void BindAuthoredVisualRoots_NoExplicitVariantOverride_FallsBackToLegacyHeuristic()
    {
        DoorVariantFixture fixture = new();

        try
        {
            BindAuthoredVisualRoots(fixture.Controller, fixture.VisualRoot);

            Assert.That(IsSideDoorVisual(fixture.Controller), Is.True, "Without an explicit override, legacy side-door visual names should keep their current behavior.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void CustomAuthoredOpenPresentation_DisablesRenderer_WhenPrefabPresentationOwnsDoorVisual()
    {
        DoorVariantFixture fixture = new();

        try
        {
            BindAuthoredVisualRoots(fixture.Controller, fixture.VisualRoot);

            SpriteRenderer openRenderer = ReadAuthoredOpenDoorRenderer(fixture.Controller);
            Assert.That(openRenderer, Is.Not.Null, "DoorController should create an authored open-door renderer.");

            openRenderer.enabled = true;
            openRenderer.color = Color.white;

            RefreshCustomAuthoredOpenDoorPresentation(
                fixture.Controller,
                openAmount: 1f,
                transitionPulse: 1f,
                useCustomAuthoredOpenDoorPresentation: false);

            Assert.That(openRenderer.enabled, Is.False, "Prefab presentation mode should not leave a second authored open-door visual enabled.");
            Assert.That(openRenderer.color.a, Is.EqualTo(0f).Within(0.0001f));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static Component CreateDoorController(GameObject root)
    {
        Type controllerType = FindTypeByName("DoorController");
        Assert.That(controllerType, Is.Not.Null, "DoorController type is missing.");
        return root.AddComponent(controllerType);
    }

    private static void BindAuthoredVisualRoots(Component controller, Transform visualRoot)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "BindAuthoredVisualRoots",
            MemberFlags,
            null,
            new[] { typeof(Transform[]) },
            null);

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.BindAuthoredVisualRoots(params Transform[]) is missing.");
        method.Invoke(controller, new object[] { new[] { visualRoot } });
    }

    private static bool IsSideDoorVisual(Component controller)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "IsSideDoorVisual",
            MemberFlags,
            null,
            Type.EmptyTypes,
            null);

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.IsSideDoorVisual() is missing.");
        object value = method.Invoke(controller, null);
        Assert.That(value, Is.InstanceOf<bool>());
        return (bool)value;
    }

    private static SpriteRenderer ReadAuthoredOpenDoorRenderer(Component controller)
    {
        FieldInfo field = controller.GetType().GetField("authoredOpenDoorRenderer", MemberFlags);
        Assert.That(field, Is.Not.Null, $"{controller.GetType().Name}.authoredOpenDoorRenderer is missing.");
        return field.GetValue(controller) as SpriteRenderer;
    }

    private static void RefreshCustomAuthoredOpenDoorPresentation(
        Component controller,
        float openAmount,
        float transitionPulse,
        bool useCustomAuthoredOpenDoorPresentation)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "RefreshCustomAuthoredOpenDoorPresentation",
            MemberFlags,
            null,
            new[] { typeof(float), typeof(float), typeof(bool) },
            null);

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.RefreshCustomAuthoredOpenDoorPresentation(float, float, bool) is missing.");
        method.Invoke(controller, new object[] { openAmount, transitionPulse, useCustomAuthoredOpenDoorPresentation });
    }

    private static void AttachDoorVariantOverride(GameObject target, string variantName)
    {
        Type overrideType = FindTypeByName("MainEscapeDoorVisualVariantOverride");
        Assert.That(overrideType, Is.Not.Null, "MainEscapeDoorVisualVariantOverride type is missing.");

        Component overrideComponent = target.AddComponent(overrideType);
        MethodInfo configureMethod = overrideType.GetMethod("Configure", MemberFlags);
        Assert.That(configureMethod, Is.Not.Null, "MainEscapeDoorVisualVariantOverride.Configure() is missing.");

        Type enumType = FindTypeByName("MainEscapeDoorVisualVariantKind");
        Assert.That(enumType, Is.Not.Null, "MainEscapeDoorVisualVariantKind type is missing.");

        object enumValue = Enum.Parse(enumType, variantName, ignoreCase: false);
        configureMethod.Invoke(overrideComponent, new[] { enumValue });
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

    private sealed class DoorVariantFixture : IDisposable
    {
        public DoorVariantFixture()
        {
            Root = new GameObject("DoorVariantFixtureRoot");
            ControllerObject = new GameObject("DoorControllerRoot");
            ControllerObject.transform.SetParent(Root.transform, false);
            Controller = CreateDoorController(ControllerObject);

            GameObject visualParent = new("sidedoor");
            visualParent.transform.SetParent(Root.transform, false);

            GameObject visualRootObject = new("CustomSideDoorClosed");
            visualRootObject.transform.SetParent(visualParent.transform, false);
            visualRootObject.AddComponent<SpriteRenderer>();
            VisualRoot = visualRootObject.transform;
        }

        public GameObject Root { get; }

        public GameObject ControllerObject { get; }

        public Component Controller { get; }

        public Transform VisualRoot { get; }

        public void Dispose()
        {
            if (Root != null)
            {
                Object.DestroyImmediate(Root);
            }
        }
    }
}

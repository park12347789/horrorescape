using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class MainEscapeDoorPresentationControllerEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void Configure_CreatesManagedRendererChildOnce_AndReusesItAcrossStateChanges()
    {
        DoorPresentationFixture fixture = new();

        try
        {
            Component controller = CreateController(fixture.Root);

            Configure(controller, "PersistentDoor", fixture.ClosedSprite, fixture.OpenSprite);

            SpriteRenderer renderer = GetManagedRenderer(controller);
            GameObject rendererChild = renderer.gameObject;
            int childCountAfterConfigure = fixture.Root.transform.childCount;

            Assert.That(rendererChild.transform.parent, Is.SameAs(fixture.Root.transform), "The managed renderer should be hosted as a child of the controller root.");
            Assert.That(childCountAfterConfigure, Is.GreaterThanOrEqualTo(1), "Configure should create the managed renderer child.");

            SetOpen(controller, true);
            Assert.That(GetManagedRenderer(controller), Is.SameAs(renderer), "Open state changes should reuse the managed renderer instance.");
            Assert.That(fixture.Root.transform.childCount, Is.EqualTo(childCountAfterConfigure));
            Assert.That(rendererChild.transform.parent, Is.SameAs(fixture.Root.transform));

            SetVisible(controller, false);
            Assert.That(GetManagedRenderer(controller), Is.SameAs(renderer), "Visible state changes should not recreate the renderer child.");
            Assert.That(fixture.Root.transform.childCount, Is.EqualTo(childCountAfterConfigure));
            Assert.That(rendererChild.transform.parent, Is.SameAs(fixture.Root.transform));

            SetVisible(controller, true);
            SetOpen(controller, false);
            Assert.That(GetManagedRenderer(controller), Is.SameAs(renderer));
            Assert.That(fixture.Root.transform.childCount, Is.EqualTo(childCountAfterConfigure));
            Assert.That(rendererChild.transform.parent, Is.SameAs(fixture.Root.transform));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void SetOpen_SwapsClosedAndOpenSprites_WithoutReplacingManagedChild()
    {
        DoorPresentationFixture fixture = new();

        try
        {
            Component controller = CreateController(fixture.Root);

            Configure(controller, "PersistentDoor", fixture.ClosedSprite, fixture.OpenSprite);

            SpriteRenderer renderer = GetManagedRenderer(controller);
            GameObject rendererChild = renderer.gameObject;

            SetOpen(controller, false);
            Assert.That(GetManagedRenderer(controller).gameObject, Is.SameAs(rendererChild));
            Assert.That(renderer.sprite, Is.SameAs(fixture.ClosedSprite), "Closed state should use the closed sprite.");

            SetOpen(controller, true);
            Assert.That(GetManagedRenderer(controller).gameObject, Is.SameAs(rendererChild), "Open state should reuse the existing child object.");
            Assert.That(renderer.sprite, Is.SameAs(fixture.OpenSprite), "Open state should use the open sprite.");

            SetOpen(controller, false);
            Assert.That(GetManagedRenderer(controller).gameObject, Is.SameAs(rendererChild));
            Assert.That(renderer.sprite, Is.SameAs(fixture.ClosedSprite), "Closed state should restore the closed sprite.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void SetVisible_TogglesManagedRendererEnabledState()
    {
        DoorPresentationFixture fixture = new();

        try
        {
            Component controller = CreateController(fixture.Root);

            Configure(controller, "PersistentDoor", fixture.ClosedSprite, fixture.OpenSprite);

            SpriteRenderer renderer = GetManagedRenderer(controller);

            SetVisible(controller, false);
            Assert.That(renderer.enabled, Is.False, "SetVisible(false) should disable the managed renderer.");

            SetVisible(controller, true);
            Assert.That(renderer.enabled, Is.True, "SetVisible(true) should re-enable the managed renderer.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static Component CreateController(GameObject root)
    {
        Type controllerType = FindTypeByName("MainEscapeDoorPresentationController");
        Assert.That(controllerType, Is.Not.Null, "MainEscapeDoorPresentationController type is missing.");
        return root.AddComponent(controllerType);
    }

    private static void Configure(Component controller, string visualName, Sprite closedSprite, Sprite openSprite)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "Configure",
            MemberFlags,
            null,
            new[] { typeof(string), typeof(Sprite), typeof(Sprite) },
            null);

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.Configure(string, Sprite, Sprite) is missing.");
        method.Invoke(controller, new object[] { visualName, closedSprite, openSprite });
    }

    private static void SetOpen(Component controller, bool isOpen)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "SetOpen",
            MemberFlags,
            null,
            new[] { typeof(bool) },
            null);

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.SetOpen(bool) is missing.");
        method.Invoke(controller, new object[] { isOpen });
    }

    private static void SetVisible(Component controller, bool visible)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "SetVisible",
            MemberFlags,
            null,
            new[] { typeof(bool) },
            null);

        Assert.That(method, Is.Not.Null, $"{controller.GetType().Name}.SetVisible(bool) is missing.");
        method.Invoke(controller, new object[] { visible });
    }

    private static SpriteRenderer GetManagedRenderer(Component controller)
    {
        object value = ReadFieldOrProperty(controller, "Renderer");
        Assert.That(value, Is.Not.Null, $"{controller.GetType().Name}.Renderer should not be null after Configure.");
        Assert.That(value, Is.InstanceOf<SpriteRenderer>(), $"{controller.GetType().Name}.Renderer should resolve to a SpriteRenderer.");
        return (SpriteRenderer)value;
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

    private sealed class DoorPresentationFixture : IDisposable
    {
        private readonly Texture2D closedTexture;
        private readonly Texture2D openTexture;

        public DoorPresentationFixture()
        {
            Root = new GameObject("DoorPresentationRoot");
            closedTexture = CreateTexture(new Color(0.22f, 0.41f, 0.67f, 1f));
            openTexture = CreateTexture(new Color(0.83f, 0.72f, 0.28f, 1f));
            ClosedSprite = CreateSprite(closedTexture, "ClosedDoorSprite");
            OpenSprite = CreateSprite(openTexture, "OpenDoorSprite");
        }

        public GameObject Root { get; }

        public Sprite ClosedSprite { get; }

        public Sprite OpenSprite { get; }

        public void Dispose()
        {
            if (ClosedSprite != null)
            {
                UnityObject.DestroyImmediate(ClosedSprite);
            }

            if (OpenSprite != null)
            {
                UnityObject.DestroyImmediate(OpenSprite);
            }

            if (closedTexture != null)
            {
                UnityObject.DestroyImmediate(closedTexture);
            }

            if (openTexture != null)
            {
                UnityObject.DestroyImmediate(openTexture);
            }

            if (Root != null)
            {
                UnityObject.DestroyImmediate(Root);
            }
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new(4, 4, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color[] pixels = new Color[texture.width * texture.height];

            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture, string spriteName)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            sprite.name = spriteName;
            return sprite;
        }
    }
}

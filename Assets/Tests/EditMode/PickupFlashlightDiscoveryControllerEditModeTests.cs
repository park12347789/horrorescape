using System;
using System.IO;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

using UnityObject = UnityEngine.Object;

public sealed class PickupFlashlightDiscoveryControllerEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const string SourcePath = "Assets/Scripts/Objectives/PickupFlashlightDiscoveryController.cs";

    [Test]
    public void ApplyCurrentState_WithAuthoredSceneDiscoveryRequired_RendersWhenVisibleInBaseSightWithoutFlashlight()
    {
        PickupDiscoveryFixture fixture = new(Color.white);

        try
        {
            Invoke(
                fixture.Controller,
                "Initialize",
                new[] { typeof(SpriteRenderer), typeof(Color) },
                new object[] { fixture.Renderer, fixture.SourceColor });

            fixture.RequireAuthoredSceneDiscovery();
            Invoke(fixture.Controller, "ApplyCurrentState");

            Assert.That(fixture.Renderer.enabled, Is.False, "Authored-scene pickups should start hidden until player vision sees them.");

            object fogVisibilityService = FakeFogVisibilityService.Create(
                FindTypeByName("IFogVisibilityService"),
                isWorldPointVisible: true,
                flashlightVisibility: 0f,
                bypassEnabled: false);

            Invoke(
                fixture.Controller,
                "BindFogVisibilityService",
                new[] { FindTypeByName("IFogVisibilityService") },
                new[] { fogVisibilityService });

            Assert.That(fixture.Renderer.enabled, Is.True, "A pickup visible in the player's base sight should render even without flashlight visibility.");
            Assert.That(fixture.Renderer.color, Is.EqualTo(fixture.SourceColor));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ApplyCurrentState_WithAuthoredSceneDiscoveryRequired_RemainsHiddenWhenNeitherBaseSightNorFlashlightSeesIt()
    {
        PickupDiscoveryFixture fixture = new(Color.white);

        try
        {
            Invoke(
                fixture.Controller,
                "Initialize",
                new[] { typeof(SpriteRenderer), typeof(Color) },
                new object[] { fixture.Renderer, fixture.SourceColor });

            fixture.RequireAuthoredSceneDiscovery();

            object fogVisibilityService = FakeFogVisibilityService.Create(
                FindTypeByName("IFogVisibilityService"),
                isWorldPointVisible: false,
                flashlightVisibility: 0f,
                bypassEnabled: false);

            Invoke(
                fixture.Controller,
                "BindFogVisibilityService",
                new[] { FindTypeByName("IFogVisibilityService") },
                new[] { fogVisibilityService });

            Assert.That(fixture.Renderer.enabled, Is.False, "A pickup outside both base sight and flashlight visibility should stay hidden.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void FogVisibilityFallback_UsesSceneLocalLookupInsteadOfGlobalRuntimeSearch()
    {
        string source = File.ReadAllText(SourcePath);

        Assert.That(source, Does.Contain("public void BindFogVisibilityService(IFogVisibilityService boundFogVisibilityService)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindUniqueComponentInScene<FlashlightFogOfWarOverlay>"));
        Assert.That(source, Does.Contain("private string cachedSceneIdentity"));
        Assert.That(source, Does.Contain("MainEscapeSceneIdentityUtility.GetSceneIdentity(scene)"));
        Assert.That(source, Does.Not.Contain("cachedSceneName"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<FlashlightFogOfWarOverlay>"));
        Assert.That(source, Does.Not.Contain("FindFirstObjectByType<FlashlightFogOfWarOverlay>"));
    }

    private static void Invoke(object target, string methodName, Type[] parameterTypes, object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, MemberFlags, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}({string.Join(", ", Array.ConvertAll(parameterTypes, type => type.Name))}) is missing.");
        method.Invoke(target, arguments);
    }

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, MemberFlags);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} is missing.");
        method.Invoke(target, null);
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

        Assert.Fail($"Unable to resolve type '{typeName}'.");
        return null;
    }

    private sealed class PickupDiscoveryFixture : IDisposable
    {
        private readonly Texture2D texture;

        public PickupDiscoveryFixture(Color sourceColor)
        {
            Root = new GameObject("PickupDiscoveryRoot");

            SourceColor = sourceColor;
            Renderer = Root.AddComponent<SpriteRenderer>();
            texture = CreateTexture(Color.white);
            Renderer.sprite = CreateSprite(texture, "PickupDiscoverySprite");
            Renderer.color = sourceColor;

            Controller = Root.AddComponent(FindTypeByName("PickupFlashlightDiscoveryController"));
        }

        public GameObject Root { get; }

        public SpriteRenderer Renderer { get; }

        public Component Controller { get; }

        public Color SourceColor { get; }

        public void RequireAuthoredSceneDiscovery()
        {
            SetField(Controller, "requiresFlashlightDiscovery", true);
        }

        public void Dispose()
        {
            if (Renderer != null && Renderer.sprite != null)
            {
                UnityObject.DestroyImmediate(Renderer.sprite);
            }

            if (texture != null)
            {
                UnityObject.DestroyImmediate(texture);
            }

            if (Root != null)
            {
                UnityObject.DestroyImmediate(Root);
            }
        }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, MemberFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
    }

    public class FakeFogVisibilityService : DispatchProxy
    {
        private bool isWorldPointVisible;
        private float flashlightVisibility;
        private bool bypassEnabled;

        public FakeFogVisibilityService()
        {
        }

        public static object Create(Type interfaceType, bool isWorldPointVisible, float flashlightVisibility, bool bypassEnabled)
        {
            MethodInfo createMethod = typeof(DispatchProxy)
                .GetMethod(nameof(DispatchProxy.Create), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(interfaceType, typeof(FakeFogVisibilityService));
            object proxy = createMethod.Invoke(null, null);
            FakeFogVisibilityService handler = (FakeFogVisibilityService)proxy;
            handler.isWorldPointVisible = isWorldPointVisible;
            handler.flashlightVisibility = flashlightVisibility;
            handler.bypassEnabled = bypassEnabled;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return targetMethod.Name switch
            {
                "get_BypassEnabled" => bypassEnabled,
                "get_VisibilityBlockingLayers" => default(LayerMask),
                "GetStateAtWorldPoint" => isWorldPointVisible
                    ? Enum.Parse(FindTypeByName("FogVisibilityState"), "Visible")
                    : Enum.Parse(FindTypeByName("FogVisibilityState"), "Unexplored"),
                "IsWorldPointVisible" => isWorldPointVisible,
                "SampleFlashlightVisibility" => flashlightVisibility,
                "SetBypassEnabled" => null,
                _ => null
            };
        }
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new(4, 4, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

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

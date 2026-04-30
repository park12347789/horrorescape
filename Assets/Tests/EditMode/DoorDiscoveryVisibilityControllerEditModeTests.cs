using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityObject = UnityEngine.Object;

public sealed class DoorDiscoveryVisibilityControllerEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const string DoorDiscoverySourcePath = "Assets/Scripts/Objectives/DoorDiscoveryVisibilityController.cs";

    [Test]
    public void ApplyRenderVisibility_WhenDoorVisible_RendersAboveFogOverlay()
    {
        DoorDiscoveryFixture fixture = new(new Color(0.94f, 0.88f, 0.71f, 1f));

        try
        {
            Invoke(
                fixture.Controller,
                "Initialize",
                new[] { typeof(SpriteRenderer[]) },
                new object[] { new[] { fixture.Renderer } });
            fixture.Renderer.sortingOrder = 0;
            fixture.Renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            fixture.Renderer.enabled = false;
            Invoke(
                fixture.Controller,
                "ApplyRenderVisibility",
                new[] { typeof(bool) },
                new object[] { true });

            Assert.That(fixture.Renderer.enabled, Is.True, "A door visible in player sight should render.");
            Assert.That(fixture.Renderer.color, Is.EqualTo(fixture.SourceColor), "Visible doors should keep their authored source tint.");
            Assert.That(fixture.Renderer.sortingOrder, Is.GreaterThan(90), "Visible doors should render above the fog overlay.");
            Assert.That(fixture.Renderer.maskInteraction, Is.EqualTo(SpriteMaskInteraction.None), "Visible doors should not be clipped by the fog mask.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ApplyCurrentState_InAuthoredScene_RendersDoorItselfWhenPlayerSightSeesDoor()
    {
        Color sourceColor = new(0.94f, 0.88f, 0.71f, 1f);
        DoorDiscoveryFixture fixture = new(sourceColor);

        try
        {
            Invoke(
                fixture.Controller,
                "Initialize",
                new[] { typeof(SpriteRenderer[]) },
                new object[] { new[] { fixture.Renderer } });
            fixture.RequireAuthoredSceneDiscovery();
            fixture.Renderer.sortingOrder = 0;

            object fogVisibilityService = FakeFogVisibilityService.Create(
                FindTypeByName("IFogVisibilityService"),
                isWorldPointVisible: true,
                flashlightVisibility: 0f,
                bypassEnabled: false);
            Invoke(
                fixture.Controller,
                "BindFogVisibilityService",
                new[] { FindTypeByName("IFogVisibilityService") },
                new object[] { fogVisibilityService });

            Assert.That(fixture.Renderer.enabled, Is.True, "A door inside player sight should render the door sprite itself.");
            Assert.That(fixture.Renderer.color, Is.EqualTo(sourceColor), "Player-sight visibility should keep the authored door tint.");
            Assert.That(fixture.Renderer.sortingOrder, Is.GreaterThan(90), "Player-sight doors should render above the fog overlay.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ApplyCurrentState_InAuthoredScene_RendersDoorItselfWhenFlashlightSeesDoor()
    {
        Color sourceColor = new(0.94f, 0.88f, 0.71f, 1f);
        DoorDiscoveryFixture fixture = new(sourceColor);

        try
        {
            fixture.AddPlayer(new Vector3(-1.25f, 0f, 0f));
            Invoke(
                fixture.Controller,
                "Initialize",
                new[] { typeof(SpriteRenderer[]) },
                new object[] { new[] { fixture.Renderer } });
            fixture.RequireAuthoredSceneDiscovery();
            fixture.Renderer.sortingOrder = 0;

            object fogVisibilityService = FakeFogVisibilityService.Create(
                FindTypeByName("IFogVisibilityService"),
                isWorldPointVisible: false,
                flashlightVisibility: 1f,
                bypassEnabled: false);
            Invoke(
                fixture.Controller,
                "BindFogVisibilityService",
                new[] { FindTypeByName("IFogVisibilityService") },
                new object[] { fogVisibilityService });

            Assert.That(fixture.Renderer.enabled, Is.True, "A door inside the flashlight cone should render the door sprite itself.");
            Assert.That(fixture.Renderer.color, Is.EqualTo(sourceColor), "Flashlight visibility should keep the authored door tint.");
            Assert.That(fixture.Renderer.sortingOrder, Is.GreaterThan(90), "Flashlight-lit doors should render above the fog overlay.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void Configure_WithExplicitPlayerAndFog_DoesNotDependOnAmbiguousPlayerFallback()
    {
        Color sourceColor = new(0.94f, 0.88f, 0.71f, 1f);
        DoorDiscoveryFixture fixture = new(sourceColor);

        try
        {
            Component player = fixture.AddPlayer(new Vector3(-1.25f, 0f, 0f));
            fixture.AddPlayer(new Vector3(1.25f, 0f, 0f));
            Invoke(
                fixture.Controller,
                "Initialize",
                new[] { typeof(SpriteRenderer[]) },
                new object[] { new[] { fixture.Renderer } });
            fixture.RequireAuthoredSceneDiscovery();
            fixture.Renderer.enabled = false;

            object fogVisibilityService = FakeFogVisibilityService.Create(
                FindTypeByName("IFogVisibilityService"),
                isWorldPointVisible: false,
                flashlightVisibility: 1f,
                bypassEnabled: false);
            Invoke(
                fixture.Controller,
                "Configure",
                new[] { FindTypeByName("WasdPlayerController"), FindTypeByName("IFogVisibilityService") },
                new[] { player, fogVisibilityService });

            Assert.That(fixture.Renderer.enabled, Is.True, "Explicit player and fog references should drive discovery before scene fallback.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void TryGetControlledBounds_CachesRootColliderLookup()
    {
        string source = File.ReadAllText(DoorDiscoverySourcePath);

        Assert.That(source, Does.Contain("private bool controlledColliderCached"));
        Assert.That(source, Does.Contain("private Collider2D ResolveControlledCollider()"));
    }

    [Test]
    public void ResolvePlayerController_UsesSceneLocalFallbackInsteadOfGlobalRuntimeSearch()
    {
        string source = File.ReadAllText(DoorDiscoverySourcePath);

        Assert.That(source, Does.Contain("[SerializeField] private WasdPlayerController playerController"));
        Assert.That(source, Does.Contain("public void Configure(WasdPlayerController boundPlayerController, IFogVisibilityService boundFogVisibilityService)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindUniqueComponentInScene<WasdPlayerController>"));
        Assert.That(source, Does.Contain("private string cachedSceneIdentity"));
        Assert.That(source, Does.Contain("MainEscapeSceneIdentityUtility.GetSceneIdentity(scene)"));
        Assert.That(source, Does.Not.Contain("cachedSceneName"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<WasdPlayerController>"));
    }

    [Test]
    public void BindFogVisibilityService_WithBypassOverlay_PreservesSourceColor()
    {
        Color sourceColor = new(0.78f, 0.52f, 0.31f, 0.93f);
        DoorDiscoveryFixture fixture = new(sourceColor);

        try
        {
            Invoke(
                fixture.Controller,
                "Initialize",
                new[] { typeof(SpriteRenderer[]) },
                new object[] { new[] { fixture.Renderer } });

            Component overlay = CreateFogOverlay();
            Invoke(overlay, "SetBypassEnabled", new[] { typeof(bool) }, new object[] { true });
            Invoke(
                fixture.Controller,
                "BindFogVisibilityService",
                new[] { FindTypeByName("IFogVisibilityService") },
                new object[] { overlay });

            Assert.That(fixture.Renderer.enabled, Is.True, "Debug bypass should keep the renderer visible.");
            Assert.That(fixture.Renderer.color, Is.EqualTo(sourceColor), "Debug bypass should preserve the authored source tint.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ApplyDoorPrefabRendererSettings_RendersPresentationAboveFogOverlay()
    {
        DoorControllerFixture fixture = new();

        try
        {
            SetField(fixture.Controller, "authoredVisualRoots", new[] { fixture.AuthoredVisualRoot.transform });
            Invoke(fixture.Controller, "ApplyDoorPrefabRendererSettings");

            Type presentationType = FindTypeByName("MainEscapeDoorPresentationController");
            Component presentationController = fixture.Root.GetComponent(presentationType);

            Assert.That(presentationController, Is.Not.Null, "Door presentation controller should be created when prefab visuals are configured.");

            SpriteRenderer presentationRenderer = ReadFieldOrProperty(presentationController, "Renderer") as SpriteRenderer;

            Assert.That(presentationRenderer, Is.Not.Null, "Door presentation controller should expose a SpriteRenderer.");
            Assert.That(presentationRenderer.sortingOrder, Is.GreaterThan(90), "Door prefab visuals should render above the fog overlay.");
            Assert.That(presentationRenderer.sortingLayerID, Is.EqualTo(fixture.RepresentativeRenderer.sortingLayerID));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void ApplyDoorPrefabRendererSettings_DoesNotAttachDiscoveryControllerToGeneratedPresentation()
    {
        DoorControllerFixture fixture = new();

        try
        {
            SetField(fixture.Controller, "authoredVisualRoots", new[] { fixture.AuthoredVisualRoot.transform });
            Invoke(fixture.Controller, "ApplyDoorPrefabRendererSettings");

            Type presentationType = FindTypeByName("MainEscapeDoorPresentationController");
            Component presentationController = fixture.Root.GetComponent(presentationType);

            Assert.That(presentationController, Is.Not.Null, "Door presentation controller should be created for generated prefab visuals.");

            SpriteRenderer presentationRenderer = ReadFieldOrProperty(presentationController, "Renderer") as SpriteRenderer;
            Assert.That(presentationRenderer, Is.Not.Null, "Generated door presentation should expose a SpriteRenderer.");
            Assert.That(
                presentationRenderer.GetComponent(FindTypeByName("DoorDiscoveryVisibilityController")),
                Is.Null,
                "Generated door visuals should stay under DoorController ownership instead of adding a second discovery visibility gate.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static Component CreateFogOverlay()
    {
        GameObject overlayRoot = new("DoorDiscoveryFogOverlay");
        overlayRoot.AddComponent<SpriteRenderer>();
        return overlayRoot.AddComponent(FindTypeByName("FlashlightFogOfWarOverlay"));
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

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, MemberFlags);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} is missing.");
        method.Invoke(target, null);
    }

    private static void Invoke(object target, string methodName, Type[] parameterTypes, object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, MemberFlags, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}({string.Join(", ", Array.ConvertAll(parameterTypes, type => type.Name))}) is missing.");
        method.Invoke(target, arguments);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, MemberFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
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

    private sealed class DoorDiscoveryFixture : IDisposable
    {
        private readonly List<GameObject> extraRoots = new();
        private readonly Texture2D texture;

        public DoorDiscoveryFixture(Color sourceColor)
        {
            Root = new GameObject("DoorDiscoveryRoot");

            Renderer = Root.AddComponent<SpriteRenderer>();
            texture = CreateTexture(Color.white);
            Renderer.sprite = CreateSprite(texture, "DoorDiscoverySprite");
            Renderer.color = sourceColor;
            SourceColor = sourceColor;

            Controller = Root.AddComponent(FindTypeByName("DoorDiscoveryVisibilityController"));
        }

        public GameObject Root { get; }

        public SpriteRenderer Renderer { get; }

        public Component Controller { get; }

        public Color SourceColor { get; }

        public void RequireAuthoredSceneDiscovery()
        {
            SetField(Controller, "cachedSceneIdentity", ResolveSceneIdentity(Root.scene));
            SetField(Controller, "requiresFlashlightDiscovery", true);
        }

        public Component AddPlayer(Vector3 worldPosition)
        {
            GameObject playerRoot = new("DoorDiscoveryPlayer");
            extraRoots.Add(playerRoot);

            playerRoot.transform.position = worldPosition;
            return playerRoot.AddComponent(FindTypeByName("WasdPlayerController"));
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

            for (int index = 0; index < extraRoots.Count; index++)
            {
                if (extraRoots[index] != null)
                {
                    UnityObject.DestroyImmediate(extraRoots[index]);
                }
            }

            if (Root != null)
            {
                UnityObject.DestroyImmediate(Root);
        }
    }

    private static string ResolveSceneIdentity(Scene scene)
    {
        Type utilityType = FindTypeByName("MainEscapeSceneIdentityUtility");
        MethodInfo method = utilityType.GetMethod(
            "GetSceneIdentity",
            BindingFlags.Static | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, "MainEscapeSceneIdentityUtility.GetSceneIdentity is missing.");
        return method.Invoke(null, new object[] { scene }) as string ?? string.Empty;
    }
}

    public class FakeFogVisibilityService : DispatchProxy
    {
        private bool isWorldPointVisible;
        private float flashlightVisibility;
        private bool bypassEnabled;

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
                "ResetMemory" => null,
                _ => null
            };
        }
    }

    private sealed class DoorControllerFixture : IDisposable
    {
        private readonly Texture2D texture;

        public DoorControllerFixture()
        {
            Root = new GameObject("DoorControllerRoot");
            Controller = Root.AddComponent(FindTypeByName("DoorController"));

            AuthoredVisualRoot = new GameObject("AuthoredVisualRoot");
            AuthoredVisualRoot.transform.SetParent(Root.transform, false);
            RepresentativeRenderer = AuthoredVisualRoot.AddComponent<SpriteRenderer>();

            texture = CreateTexture(new Color(0.68f, 0.74f, 0.82f, 1f));
            RepresentativeRenderer.sprite = CreateSprite(texture, "RepresentativeDoorSprite");
            RepresentativeRenderer.sortingOrder = 24;
        }

        public GameObject Root { get; }

        public Component Controller { get; }

        public GameObject AuthoredVisualRoot { get; }

        public SpriteRenderer RepresentativeRenderer { get; }

        public void Dispose()
        {
            if (RepresentativeRenderer != null && RepresentativeRenderer.sprite != null)
            {
                UnityObject.DestroyImmediate(RepresentativeRenderer.sprite);
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

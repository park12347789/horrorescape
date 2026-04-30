using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

public sealed class FlashlightFogOfWarOverlayOptimizationEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private GameObject overlayRoot;
    private GameObject authoredLightRoot;
    private Component overlay;
    private Type overlayType;
    private GameObject wallObject;

    [TearDown]
    public void TearDown()
    {
        if (wallObject != null)
        {
            UnityEngine.Object.DestroyImmediate(wallObject);
        }

        if (overlayRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(overlayRoot);
        }

        if (authoredLightRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(authoredLightRoot);
        }
    }

    [Test]
    public void TryComputePlayerReveal_SkipsLineOfSightInsideDirectComfortRadius()
    {
        overlay = CreateOverlay("FogOverlay_DirectComfort");
        wallObject = CreateWall(new Vector2(0.2f, 0f), new Vector2(0.08f, 1f));
        LayerMask wallMask = LayerMask.GetMask("Wall");
        SetPrivateField("visibilityBlockingLayers", wallMask);

        object[] arguments =
        {
            Vector2.zero,
            new Vector2(0.4f, 0f),
            0,
            1.3f,
            1.3f * 1.3f,
            0.5f * 0.5f,
            1f,
            0f
        };

        bool isVisible = InvokePrivateBool("TryComputePlayerReveal", arguments);

        Assert.That(isVisible, Is.True);
        Assert.That((float)arguments[7], Is.GreaterThan(0f));
    }

    [Test]
    public void TryComputePlayerReveal_StillChecksLineOfSightOutsideDirectComfortRadius()
    {
        overlay = CreateOverlay("FogOverlay_ComfortLos");
        wallObject = CreateWall(new Vector2(0.45f, 0f), new Vector2(0.08f, 1f));
        LayerMask wallMask = LayerMask.GetMask("Wall");
        SetPrivateField("visibilityBlockingLayers", wallMask);

        object[] arguments =
        {
            Vector2.zero,
            new Vector2(0.9f, 0f),
            0,
            1.3f,
            1.3f * 1.3f,
            0.3f * 0.3f,
            1f,
            0f
        };

        bool isVisible = InvokePrivateBool("TryComputePlayerReveal", arguments);

        Assert.That(isVisible, Is.False);
    }

    [Test]
    public void ResetMemory_BakesAuthoredLightVisibilityIntoVisibleCells()
    {
        authoredLightRoot = CreateAuthoredLightRoot();
        overlay = CreateOverlay("FogOverlay_BakedReset");
        SetPrivateField("bakeAuthoredLightVisibilityOnReset", true);
        SetPrivateField("worldSize", new Vector2(4f, 4f));
        SetPrivateField("pixelsPerUnit", 4);

        InvokePrivateVoid("ResetMemory");

        MethodInfo getStateAtWorldPoint = overlayType.GetMethod("GetStateAtWorldPoint", BindingFlags.Instance | BindingFlags.Public);
        Type fogVisibilityStateType = FindTypeByName("FogVisibilityState");
        object visibleState = Enum.Parse(fogVisibilityStateType, "Visible");
        object unexploredState = Enum.Parse(fogVisibilityStateType, "Unexplored");

        Assert.That(getStateAtWorldPoint, Is.Not.Null, "Expected public method GetStateAtWorldPoint to exist.");
        Assert.That(getStateAtWorldPoint.Invoke(overlay, new object[] { new Vector2(0f, -1.2f) }), Is.EqualTo(visibleState));
        Assert.That(getStateAtWorldPoint.Invoke(overlay, new object[] { new Vector2(0f, 1.4f) }), Is.EqualTo(unexploredState));
    }

    [Test]
    public void TryComputeVisibility_FallsBackToAuthoredLightWhenEarlierChecksFail()
    {
        authoredLightRoot = CreateAuthoredLightRoot();
        overlay = CreateOverlay("FogOverlay_AuthoredFallback");
        SetPrivateField("visibilityBlockingLayers", new LayerMask());
        InvokePrivateVoid("EnsureTexture");

        MethodInfo method = overlayType.GetMethod("TryComputeVisibility", InstanceFlags);
        Assert.That(method, Is.Not.Null, "Expected private method TryComputeVisibility to exist.");

        object[] arguments =
        {
            Vector2.zero,
            Vector2.zero,
            Vector2.up,
            0,
            0f,
            0f,
            0f,
            1f,
            0f,
            0f,
            0f,
            0f,
            1f,
            1f,
            new Vector2(0f, -1.2f),
            0f
        };

        bool isVisible = method.Invoke(overlay, arguments) is bool result && result;

        Assert.That(isVisible, Is.True);
        Assert.That(Convert.ToSingle(arguments[15]), Is.GreaterThan(0f));
    }

    [Test]
    public void RefreshLineOfSightCachePose_InvalidatesOnlyAfterMeaningfulPoseChange()
    {
        overlay = CreateOverlay("FogOverlay_LosCache");
        InvokePrivateVoid("EnsureTexture");
        InvokePrivateVoid("ResetLineOfSightCaches");

        InvokePrivateVoid("RefreshLineOfSightCachePose", Vector2.zero, Vector2.zero, Vector2.up);
        int initialRevision = GetPrivateField<int>("activeLineOfSightCacheRevision");

        Vector2 smallRotation = Quaternion.Euler(0f, 0f, 2f) * Vector2.up;
        InvokePrivateVoid("RefreshLineOfSightCachePose", new Vector2(0.05f, 0f), new Vector2(0.05f, 0f), smallRotation);

        Assert.That(GetPrivateField<int>("activeLineOfSightCacheRevision"), Is.EqualTo(initialRevision));

        Vector2 largeRotation = Quaternion.Euler(0f, 0f, 12f) * Vector2.up;
        InvokePrivateVoid("RefreshLineOfSightCachePose", new Vector2(0.3f, 0f), new Vector2(0.3f, 0f), largeRotation);

        Assert.That(GetPrivateField<int>("activeLineOfSightCacheRevision"), Is.EqualTo(initialRevision + 1));
    }

    [Test]
    public void ShouldProcessInterlacedBlockRow_UsesSampleBlockRows()
    {
        Type type = FindTypeByName("FlashlightFogOfWarOverlay");
        MethodInfo method = type.GetMethod("ShouldProcessInterlacedBlockRow", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, "Expected private static method ShouldProcessInterlacedBlockRow to exist.");
        Assert.That((bool)method.Invoke(null, new object[] { 0, 2, 2, 0 }), Is.True);
        Assert.That((bool)method.Invoke(null, new object[] { 2, 2, 2, 1 }), Is.True);
        Assert.That((bool)method.Invoke(null, new object[] { 2, 2, 2, 0 }), Is.False);
    }

    [Test]
    public void ResolveRefreshBoundsForGroup_CarriesPreviousInterlacedBoundsWhenGroupCountChanges()
    {
        overlay = CreateOverlay("FogOverlay_GroupBoundsCarry");
        Rect[] previousBounds =
        {
            Rect.MinMaxRect(-3f, -2f, -1f, 0f),
            Rect.MinMaxRect(1f, 2f, 3f, 4f)
        };
        bool[] previousBoundsValid = { true, true };
        SetPrivateField("partialRefreshBoundsByGroup", previousBounds);
        SetPrivateField("partialRefreshBoundsValidByGroup", previousBoundsValid);

        MethodInfo method = overlayType.GetMethod("ResolveRefreshBoundsForGroup", InstanceFlags);
        Assert.That(method, Is.Not.Null, "Expected private method ResolveRefreshBoundsForGroup to exist.");

        Rect result = (Rect)method.Invoke(overlay, new object[] { 0, 1, Rect.MinMaxRect(0f, 0f, 0.5f, 0.5f) });

        Assert.That(result.xMin, Is.EqualTo(-3f));
        Assert.That(result.yMin, Is.EqualTo(-2f));
        Assert.That(result.xMax, Is.EqualTo(3f));
        Assert.That(result.yMax, Is.EqualTo(4f));
    }

    private Component CreateOverlay(string name)
    {
        overlayRoot = new GameObject(name);
        overlayRoot.AddComponent<SpriteRenderer>();
        overlayType = FindTypeByName("FlashlightFogOfWarOverlay");
        return overlayRoot.AddComponent(overlayType);
    }

    private GameObject CreateAuthoredLightRoot()
    {
        authoredLightRoot = new GameObject("AuthoredLight");

        GameObject lightObject = new("Light");
        lightObject.transform.SetParent(authoredLightRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        AddLight2D(lightObject);

        Type authoredLightType = FindTypeByName("AuthoredVisibilityLight2D");
        Component authoredLight = authoredLightRoot.AddComponent(authoredLightType);

        Assert.That(authoredLight, Is.Not.Null);

        MethodInfo onValidate = authoredLightType.GetMethod("OnValidate", InstanceFlags);
        MethodInfo onEnable = authoredLightType.GetMethod("OnEnable", InstanceFlags);

        Assert.That(onValidate, Is.Not.Null, "Expected AuthoredVisibilityLight2D.OnValidate to exist.");
        Assert.That(onEnable, Is.Not.Null, "Expected AuthoredVisibilityLight2D.OnEnable to exist.");
        onValidate.Invoke(authoredLight, null);
        onEnable.Invoke(authoredLight, null);
        return authoredLightRoot;
    }

    private static void AddLight2D(GameObject target)
    {
        Type lightType = Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime");
        Assert.That(lightType, Is.Not.Null, "Light2D type is unavailable.");
        target.AddComponent(lightType);
    }

    private static GameObject CreateWall(Vector2 position, Vector2 size)
    {
        GameObject wall = new("VisionBlocker");
        wall.layer = LayerMask.NameToLayer("Wall");
        wall.transform.position = position;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        return wall;
    }

    private void InvokePrivateVoid(string methodName, params object[] arguments)
    {
        MethodInfo method = overlayType.GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Expected private method {methodName} to exist.");
        method.Invoke(overlay, arguments);
    }

    private bool InvokePrivateBool(string methodName, params object[] arguments)
    {
        MethodInfo method = overlayType.GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Expected private method {methodName} to exist.");
        return method.Invoke(overlay, arguments) is bool value && value;
    }

    private void SetPrivateField(string fieldName, object value)
    {
        FieldInfo field = overlayType.GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Expected private field {fieldName} to exist.");
        field.SetValue(overlay, value);
    }

    private T GetPrivateField<T>(string fieldName)
    {
        FieldInfo field = overlayType.GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Expected private field {fieldName} to exist.");
        return (T)field.GetValue(overlay);
    }

    private static Type FindTypeByName(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type foundType = assembly.GetType(typeName) ?? System.Array.Find(assembly.GetTypes(), candidate => candidate.Name == typeName);

            if (foundType != null)
            {
                return foundType;
            }
        }

        Assert.Fail($"Unable to resolve type '{typeName}'.");
        return null;
    }
}

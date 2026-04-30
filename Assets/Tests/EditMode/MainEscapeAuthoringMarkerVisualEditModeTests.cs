using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public sealed class MainEscapeAuthoringMarkerVisualEditModeTests
{
    private const BindingFlags NonPublicInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void MarkerVisual_SnapsVisualChildCenterToMarkerOrigin()
    {
        GameObject markerRoot = new("PlayerStartMarker");
        Texture2D markerTexture = new(32, 32, TextureFormat.RGBA32, false);
        Sprite markerSprite = null;

        try
        {
            Type markerVisualType = FindTypeByName("MainEscapeAuthoringMarkerVisual");
            Assert.That(markerVisualType, Is.Not.Null, "MainEscapeAuthoringMarkerVisual type is missing.");

            Component markerVisual = markerRoot.AddComponent(markerVisualType);
            GameObject visual = new("Visual");
            visual.transform.SetParent(markerRoot.transform, false);
            visual.transform.localPosition = new Vector3(-2.63f, 9.7f, 0f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 33f);
            visual.transform.localScale = new Vector3(0.62f, 0.62f, 1f);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            markerSprite = Sprite.Create(markerTexture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
            renderer.sprite = markerSprite;

            InvokeNonPublicMethod(markerVisual, "SnapVisualChildrenToMarkerOrigin");

            Assert.That(visual.transform.localPosition.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(visual.transform.localPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Quaternion.Angle(visual.transform.localRotation, Quaternion.identity), Is.EqualTo(0f).Within(0.0001f));
        }
        finally
        {
            if (markerSprite != null)
            {
                UnityObject.DestroyImmediate(markerSprite);
            }

            UnityObject.DestroyImmediate(markerTexture);
            UnityObject.DestroyImmediate(markerRoot);
        }
    }

    private static void InvokeNonPublicMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, NonPublicInstanceFlags);
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

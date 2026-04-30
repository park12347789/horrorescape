using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

public sealed class MainEscapeSolidBlockerAuthoringEditModeTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private GameObject blockerObject;

    [TearDown]
    public void TearDown()
    {
        if (blockerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(blockerObject);
        }
    }

    [Test]
    public void ThinVisualScale_StillKeepsFullCellBlockingCollider()
    {
        blockerObject = new GameObject("SolidBlocker");
        blockerObject.transform.localScale = new Vector3(1f, 0.18f, 1f);
        blockerObject.AddComponent<SpriteRenderer>();
        blockerObject.AddComponent<BoxCollider2D>();
        blockerObject.AddComponent(FindTypeByName("MainEscapePropBlockerAuthoring"));
        blockerObject.AddComponent(FindTypeByName("MainEscapeSolidBlockerAuthoring"));
        BoxCollider2D collider = blockerObject.GetComponent<BoxCollider2D>();

        Assert.That(collider, Is.Not.Null);
        Assert.That(collider.isTrigger, Is.False);
        Assert.That(collider.bounds.size.x, Is.EqualTo(1f).Within(0.02f));
        Assert.That(collider.bounds.size.y, Is.EqualTo(1f).Within(0.02f));
        Assert.That(blockerObject.layer, Is.EqualTo(ResolveWallLayerIndex()));
    }

    private static int ResolveWallLayerIndex()
    {
        Type gameLayersType = FindTypeByName("GameLayers");
        PropertyInfo wallIndexProperty = gameLayersType.GetProperty("WallIndex", StaticFlags);

        if (wallIndexProperty != null)
        {
            return (int)wallIndexProperty.GetValue(null);
        }

        FieldInfo wallIndexField = gameLayersType.GetField("WallIndex", StaticFlags);
        Assert.That(wallIndexField, Is.Not.Null, "Unable to resolve GameLayers.WallIndex.");
        return (int)wallIndexField.GetValue(null);
    }

    private static Type FindTypeByName(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type foundType = assembly.GetType(typeName) ?? Array.Find(assembly.GetTypes(), candidate => candidate.Name == typeName);

            if (foundType != null)
            {
                return foundType;
            }
        }

        Assert.Fail($"Unable to resolve type '{typeName}'.");
        return null;
    }
}

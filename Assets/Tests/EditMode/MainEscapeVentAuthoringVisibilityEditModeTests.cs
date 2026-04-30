using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

public sealed class MainEscapeVentAuthoringVisibilityEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [Test]
    public void HideVentAuthoringRenderersForRuntime_DisablesVentRouteAndPreviewLineRenderers()
    {
        GameObject floorObject = new("VentVisibility_Test_Floor");

        try
        {
            Type floorAuthoringType = FindTypeByName("MainEscapeFloorAuthoring");
            Assert.That(floorAuthoringType, Is.Not.Null, "MainEscapeFloorAuthoring type is missing.");

            Component floorAuthoring = floorObject.AddComponent(floorAuthoringType);
            Transform markersRoot = CreateChild(floorObject.transform, "AuthoringMarkers");
            Transform ventRouteRoot = CreateChild(markersRoot, "VentRoute");
            ventRouteRoot.gameObject.AddComponent(RequireType("MainEscapeVentRouteAuthoring"));

            MeshRenderer routeRenderer = CreateChild(ventRouteRoot, "Grid_R00_C00").gameObject.AddComponent<MeshRenderer>();
            routeRenderer.gameObject.AddComponent(RequireType("MainEscapeVentNodeAuthoring"));

            Transform previewRoot = CreateChild(markersRoot, "VentRouteVisualLines_CleanDirect");
            MeshRenderer previewRenderer = CreateChild(previewRoot, "VentGrid_H00").gameObject.AddComponent<MeshRenderer>();

            MeshRenderer unrelatedRenderer = CreateChild(markersRoot, "UnrelatedMarker").gameObject.AddComponent<MeshRenderer>();

            MethodInfo hideMethod = floorAuthoringType.GetMethod("HideVentAuthoringRenderersForRuntime", InstanceFlags);
            Assert.That(hideMethod, Is.Not.Null, "MainEscapeFloorAuthoring.HideVentAuthoringRenderersForRuntime is missing.");
            hideMethod.Invoke(floorAuthoring, null);

            Assert.That(routeRenderer.enabled, Is.False);
            Assert.That(previewRenderer.enabled, Is.False);
            Assert.That(unrelatedRenderer.enabled, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(floorObject);
        }
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject childObject = new(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Type RequireType(string typeName)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");
        return type;
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

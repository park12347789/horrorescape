using System;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class IRThreatPanelViewEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void Render_ConfirmedPursuit_UsesBottomGradientWithoutEdgeHighlight()
    {
        GameObject canvasObject = new("Threat Canvas", typeof(RectTransform), typeof(Canvas));

        try
        {
            GameObject panelObject = new("Threat Panel", typeof(RectTransform), typeof(CanvasGroup));
            panelObject.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRoot = panelObject.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
            RawImage topEdge = CreateEdge(panelRoot, "Top Edge");
            RawImage bottomEdge = CreateEdge(panelRoot, "Bottom Edge");
            RawImage leftEdge = CreateEdge(panelRoot, "Left Edge");
            RawImage rightEdge = CreateEdge(panelRoot, "Right Edge");

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            Component panelView;

            try
            {
                LogAssert.ignoreFailingMessages = true;
                panelView = panelObject.AddComponent(FindTypeByName("IRThreatPanelView"));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            InvokeConfigure(panelView, panelRoot, canvasGroup, topEdge, bottomEdge, leftEdge, rightEdge);
            InvokeRender(panelView, CreateThreatPanelPresentation(1f, true, "Detected", "Pursuit"));

            RawImage spottedOverlay = FindSpottedOverlay(panelRoot);

            Assert.That(spottedOverlay, Is.Not.Null, "Detected-state bottom gradient overlay was not created.");
            Assert.That(spottedOverlay.color.a, Is.GreaterThan(0.1f), "Detected-state bottom gradient overlay stayed hidden.");
            Assert.That(topEdge.color.a, Is.EqualTo(0f).Within(0.001f), "Top edge highlight should stay hidden.");
            Assert.That(bottomEdge.color.a, Is.EqualTo(0f).Within(0.001f), "Bottom edge highlight should stay hidden.");
            Assert.That(leftEdge.color.a, Is.EqualTo(0f).Within(0.001f), "Left edge highlight should stay hidden.");
            Assert.That(rightEdge.color.a, Is.EqualTo(0f).Within(0.001f), "Right edge highlight should stay hidden.");
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    private static RawImage CreateEdge(RectTransform parent, string name)
    {
        GameObject edgeObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        edgeObject.transform.SetParent(parent, false);
        RawImage edgeImage = edgeObject.GetComponent<RawImage>();
        edgeImage.color = Color.white;
        return edgeImage;
    }

    private static RawImage FindSpottedOverlay(RectTransform panelRoot)
    {
        Transform overlayTransform = panelRoot.Find("IRThreatSpottedOverlayCanvas/IRThreatSpottedOverlay");
        return overlayTransform != null ? overlayTransform.GetComponent<RawImage>() : null;
    }

    private static void InvokeConfigure(
        Component panelView,
        RectTransform panelRoot,
        CanvasGroup canvasGroup,
        RawImage topEdge,
        RawImage bottomEdge,
        RawImage leftEdge,
        RawImage rightEdge)
    {
        MethodInfo configureMethod = panelView.GetType().GetMethod("Configure", MemberFlags);
        Assert.That(configureMethod, Is.Not.Null, "IRThreatPanelView.Configure is missing.");
        configureMethod.Invoke(panelView, new object[] { panelRoot, canvasGroup, topEdge, bottomEdge, leftEdge, rightEdge });
    }

    private static void InvokeRender(Component panelView, object presentation)
    {
        MethodInfo renderMethod = panelView.GetType().GetMethod("Render", MemberFlags);
        Assert.That(renderMethod, Is.Not.Null, "IRThreatPanelView.Render is missing.");
        renderMethod.Invoke(panelView, new[] { presentation });
    }

    private static object CreateThreatPanelPresentation(float intensity, bool pursuitConfirmed, string title, string detail)
    {
        Type presentationType = FindTypeByName("ThreatPanelPresentation");
        Assert.That(presentationType, Is.Not.Null, "ThreatPanelPresentation type is missing.");

        return Activator.CreateInstance(
            presentationType,
            intensity,
            pursuitConfirmed,
            title,
            detail,
            0f);
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

        Assert.Fail($"{typeName} type is missing.");
        return null;
    }
}

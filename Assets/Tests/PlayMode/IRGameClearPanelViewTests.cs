using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class IRGameClearPanelViewTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        GameObject sessionObject = GameObject.Find("RRunSessionController");

        if (sessionObject != null)
        {
            UnityEngine.Object.DestroyImmediate(sessionObject);
        }

        if (root != null)
        {
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }
    }

    [UnityTest]
    public IEnumerator ShowFloorClear_SetsFloorClearMode()
    {
        Component view = CreateView();
        Invoke(view, "ShowFloorClear", 5, 4);

        yield return null;

        Assert.That(ReadProperty(view, "IsShowing"), Is.EqualTo(true));
        Assert.That(ReadProperty(view, "Mode").ToString(), Is.EqualTo("FloorClear"));
    }

    [UnityTest]
    public IEnumerator ShowFinalClear_SetsFinalClearMode()
    {
        Component view = CreateView();
        Invoke(view, "ShowFinalClear");

        yield return null;

        Assert.That(ReadProperty(view, "IsShowing"), Is.EqualTo(true));
        Assert.That(ReadProperty(view, "Mode").ToString(), Is.EqualTo("FinalClear"));
    }

    [UnityTest]
    public IEnumerator ShowFailure_SetsFailureModeAndCanHide()
    {
        Component view = CreateView();
        Invoke(view, "ShowFailure", "VentEnemy");

        yield return null;

        Assert.That(ReadProperty(view, "IsFailureModal"), Is.EqualTo(true));
        Invoke(view, "HideAndResume");
        Assert.That(ReadProperty(view, "IsShowing"), Is.EqualTo(false));
        Assert.That(ReadProperty(view, "Mode").ToString(), Is.EqualTo("None"));
    }

    private Component CreateView()
    {
        Type sessionType = FindTypeByName("RRunSessionController");
        Type viewType = FindTypeByName("IRGameClearPanelView");
        Assert.That(sessionType, Is.Not.Null, "RRunSessionController type is missing.");
        Assert.That(viewType, Is.Not.Null, "IRGameClearPanelView type is missing.");

        GameObject sessionObject = new("RRunSessionController");
        Component session = sessionObject.AddComponent(sessionType);

        root = new GameObject("ModalRoot", typeof(RectTransform), typeof(Image));
        RectTransform panelRoot = root.GetComponent<RectTransform>();
        Image backdropImage = root.GetComponent<Image>();

        GameObject modal = new("Modal", typeof(RectTransform), typeof(Image));
        modal.transform.SetParent(root.transform, false);
        Image panelImage = modal.GetComponent<Image>();

        Component titleText = CreateTmpText(modal.transform, "Title");
        Component bodyText = CreateTmpText(modal.transform, "Body");
        Component promptText = CreateTmpText(modal.transform, "Prompt");

        Component view = root.AddComponent(viewType);
        Invoke(view, "Configure", session, panelRoot, backdropImage, panelImage, titleText, bodyText, promptText);
        return view;
    }

    private static Component CreateTmpText(Transform parent, string name)
    {
        Type tmpType = FindTypeByName("TMPro.TextMeshProUGUI");
        Assert.That(tmpType, Is.Not.Null, "TextMeshProUGUI type is missing.");

        GameObject textObject = new(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Component textComponent = textObject.AddComponent(tmpType);

        Type settingsType = FindTypeByName("TMPro.TMP_Settings");
        PropertyInfo defaultFont = settingsType != null
            ? settingsType.GetProperty("defaultFontAsset", StaticFlags)
            : null;
        PropertyInfo fontProperty = tmpType.GetProperty("font", InstanceFlags);
        object defaultFontValue = defaultFont != null ? defaultFont.GetValue(null) : null;

        if (fontProperty != null && defaultFontValue != null)
        {
            fontProperty.SetValue(textComponent, defaultFontValue);
        }

        return textComponent;
    }

    private static object ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance);
    }

    private static void Invoke(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        method.Invoke(instance, arguments);
    }

    private static Type FindTypeByName(string typeName)
    {
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

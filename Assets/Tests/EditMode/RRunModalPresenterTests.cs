using System;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;

public sealed class RRunModalPresenterTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [TearDown]
    public void TearDown()
    {
        GameObject panelObject = GameObject.Find("RRunModalPresenterTests_Panel");

        if (panelObject != null)
        {
            UnityEngine.Object.DestroyImmediate(panelObject);
        }
    }

    [Test]
    public void TryShowFailure_NullPanel_ReturnsFalse()
    {
        object result = InvokePresenter("TryShowFailure", null, null, null, "Enemy");
        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void TryShowFinalClear_SetsViewState()
    {
        Component panelView = CreatePanelView();

        object result = InvokePresenter("TryShowFinalClear", panelView, null, null);

        Assert.That(result, Is.EqualTo(true));
        Assert.That(ReadProperty(panelView, "IsShowing"), Is.EqualTo(true));
        Assert.That(ReadProperty(panelView, "Mode").ToString(), Is.EqualTo("FinalClear"));
    }

    private static Component CreatePanelView()
    {
        Type viewType = FindTypeByName("IRGameClearPanelView");
        Assert.That(viewType, Is.Not.Null, "IRGameClearPanelView type is missing.");

        GameObject panelObject = new("RRunModalPresenterTests_Panel");
        Component panelView = panelObject.AddComponent(viewType);
        GameObject rootObject = new("PanelRoot", typeof(RectTransform));
        rootObject.transform.SetParent(panelObject.transform, false);
        SetField(panelView, "panelRoot", rootObject.GetComponent<RectTransform>());
        return panelView;
    }

    private static object ReadProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance);
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} is missing.");
        field.SetValue(instance, value);
    }

    private static object InvokePresenter(string methodName, params object[] arguments)
    {
        Type presenterType = FindTypeByName("RRunModalPresenter");
        Assert.That(presenterType, Is.Not.Null, "RRunModalPresenter type is missing.");

        MethodInfo method = presenterType.GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"{presenterType.Name}.{methodName} is missing.");
        return method.Invoke(null, arguments);
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

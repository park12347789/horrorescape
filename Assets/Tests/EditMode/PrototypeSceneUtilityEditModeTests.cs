using System;
using System.Reflection;

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrototypeSceneUtilityEditModeTests
{
    [Test]
    public void GetGeneratorMode_UsesSceneLocalSettingsBeforeLegacySceneName()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene testScene = SceneManager.CreateScene("BaseOffice");
        GameObject root = new("PrototypeGeneratorSettingsRoot");
        SceneManager.MoveGameObjectToScene(root, testScene);
        Type settingsType = FindTypeByName("PrototypeSceneGeneratorSettings");
        Component settings = root.AddComponent(settingsType);

        try
        {
            InvokeConfigure(settings, "OfficeMixed", overviewCamera: false);

            Assert.That(InvokeGetGeneratorMode(testScene).ToString(), Is.EqualTo("OfficeMixed"));
            Assert.That(InvokeUseOverviewCamera(testScene), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);

            if (originalActiveScene.IsValid())
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }

            if (testScene.IsValid())
            {
                EditorSceneManager.CloseScene(testScene, true);
            }
        }
    }

    private static void InvokeConfigure(Component settings, string modeName, bool overviewCamera)
    {
        Type modeType = FindTypeByName("PrototypeGeneratorMode");
        object mode = Enum.Parse(modeType, modeName);
        MethodInfo method = settings.GetType().GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, "PrototypeSceneGeneratorSettings.Configure is missing.");
        method.Invoke(settings, new[] { mode, overviewCamera });
    }

    private static object InvokeGetGeneratorMode(Scene scene)
    {
        Type utilityType = FindTypeByName("PrototypeSceneUtility");
        MethodInfo method = utilityType.GetMethod(
            "GetGeneratorMode",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(Scene) },
            null);
        Assert.That(method, Is.Not.Null, "PrototypeSceneUtility.GetGeneratorMode(Scene) is missing.");
        return method.Invoke(null, new object[] { scene });
    }

    private static bool InvokeUseOverviewCamera(Scene scene)
    {
        Type utilityType = FindTypeByName("PrototypeSceneUtility");
        MethodInfo method = utilityType.GetMethod(
            "UseOverviewCamera",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(Scene) },
            null);
        Assert.That(method, Is.Not.Null, "PrototypeSceneUtility.UseOverviewCamera(Scene) is missing.");
        return method.Invoke(null, new object[] { scene }) is bool result && result;
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

        Assert.Fail($"Unable to resolve type '{typeName}'.");
        return null;
    }
}

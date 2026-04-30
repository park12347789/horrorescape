using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MainEscapeLobbyControllerTests
{
    private const string MasterVolumeKey = "IRLobby.MasterVolume";
    private const string SfxVolumeKey = "IRLobby.SfxVolume";
    private const string AmbienceVolumeKey = "IRLobby.AmbienceVolume";
    private const string TargetFrameRateKey = "IRLobby.TargetFrameRate";
    private const string VSyncEnabledKey = "IRLobby.VSyncEnabled";
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private int originalTargetFrameRate;
    private int originalVSyncCount;

    [SetUp]
    public void SetUp()
    {
        originalTargetFrameRate = Application.targetFrameRate;
        originalVSyncCount = QualitySettings.vSyncCount;
    }

    [TearDown]
    public void TearDown()
    {
        GameObject controllerObject = GameObject.Find("RRunSessionController");
        GameObject audioObject = GameObject.Find("PrototypeAudioManager");

        if (controllerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        if (audioObject != null)
        {
            UnityEngine.Object.DestroyImmediate(audioObject);
        }

        if (root != null)
        {
            UnityEngine.Object.DestroyImmediate(root);
            root = null;
        }

        Application.targetFrameRate = originalTargetFrameRate;
        QualitySettings.vSyncCount = originalVSyncCount;

        PlayerPrefs.DeleteKey(MasterVolumeKey);
        PlayerPrefs.DeleteKey(SfxVolumeKey);
        PlayerPrefs.DeleteKey(AmbienceVolumeKey);
        PlayerPrefs.DeleteKey(TargetFrameRateKey);
        PlayerPrefs.DeleteKey(VSyncEnabledKey);
        PlayerPrefs.Save();
    }

    [UnityTest]
    public IEnumerator RefreshSummary_ReflectsLastRunOutcome()
    {
        object session = EnsureController();
        yield return null;

        Invoke(session, "BeginNewRun");
        Invoke(session, "RecordFailure", 3, "SentryGuard_01");

        Type lobbyControllerType = FindTypeByName("IRLobbyController");
        Assert.That(lobbyControllerType, Is.Not.Null, "IRLobbyController type is missing.");

        root = new GameObject("LobbyRoot", typeof(RectTransform));
        Button startButton = CreateButton(root.transform, "Start");
        Button quitButton = CreateButton(root.transform, "Quit");
        Component title = CreateTmpText(root.transform, "Title");
        Component body = CreateTmpText(root.transform, "Body");
        Component footer = CreateTmpText(root.transform, "Footer");

        Component lobbyController = root.AddComponent(lobbyControllerType);
        Invoke(lobbyController, "Configure", session, startButton, quitButton, title, body, footer);
        Invoke(lobbyController, "RefreshSummary");

        yield return null;

        Assert.That(ReadStringProperty(title, "text"), Is.EqualTo("Last Descent Failed"));
        Assert.That(ReadStringProperty(body, "text"), Does.Contain("Descent collapsed on 3F"));
        Assert.That(ReadStringProperty(body, "text"), Does.Contain("SentryGuard_01"));
    }

    [UnityTest]
    public IEnumerator OptionsModal_OpenClose_TogglesRuntimeState()
    {
        object session = EnsureController();
        yield return null;

        Type lobbyControllerType = FindTypeByName("IRLobbyController");
        Assert.That(lobbyControllerType, Is.Not.Null, "IRLobbyController type is missing.");

        root = new GameObject("LobbyRoot", typeof(RectTransform));
        Button startButton = CreateButton(root.transform, "Start");
        Button quitButton = CreateButton(root.transform, "Quit");
        Component title = CreateTmpText(root.transform, "Title");
        Component body = CreateTmpText(root.transform, "Body");
        Component footer = CreateTmpText(root.transform, "Footer");
        Button optionsButton = CreateButton(root.transform, "Options");
        Button creditsButton = CreateButton(root.transform, "Credits");
        RectTransform modalBackdrop = CreateRect(root.transform, "IRLobbyModalBackdrop");
        RectTransform optionsPanel = CreateRect(modalBackdrop, "IRLobbyOptionsPanel");
        RectTransform creditsPanel = CreateRect(modalBackdrop, "IRLobbyCreditsPanel");
        Button optionsCloseButton = CreateButton(optionsPanel, "IRLobbyOptionsCloseButton");
        Button creditsCloseButton = CreateButton(creditsPanel, "IRLobbyCreditsCloseButton");
        Component uiSettingsOwner = root.AddComponent(FindTypeByName("UiSettingsOwner"));

        Component lobbyController = root.AddComponent(lobbyControllerType);
        SetField(lobbyController, "uiSettingsReadModelSource", uiSettingsOwner);
        SetField(lobbyController, "optionsButton", optionsButton);
        SetField(lobbyController, "creditsButton", creditsButton);
        SetField(lobbyController, "modalBackdrop", modalBackdrop);
        SetField(lobbyController, "optionsPanel", optionsPanel);
        SetField(lobbyController, "creditsPanel", creditsPanel);
        SetField(lobbyController, "optionsCloseButton", optionsCloseButton);
        SetField(lobbyController, "creditsCloseButton", creditsCloseButton);
        Invoke(lobbyController, "Configure", session, startButton, quitButton, title, body, footer);
        yield return null;

        object resolvedOptionsButton = ReadPropertyValue(lobbyController, "OptionsButton");
        Assert.That(resolvedOptionsButton, Is.Not.Null, "Options button should resolve from authored references.");

        Invoke(lobbyController, "OpenOptionsModal");
        yield return null;

        Assert.That((bool)ReadPropertyValue(lobbyController, "IsOptionsModalOpen"), Is.True);

        Invoke(lobbyController, "CloseActiveModal");
        yield return null;

        Assert.That((bool)ReadPropertyValue(lobbyController, "IsOptionsModalOpen"), Is.False);
    }

    [UnityTest]
    public IEnumerator RunSessionController_Awake_AppliesPersistedPerformanceOptions()
    {
        PlayerPrefs.SetInt(TargetFrameRateKey, 120);
        PlayerPrefs.SetInt(VSyncEnabledKey, 1);
        PlayerPrefs.Save();

        EnsureController();
        yield return null;

        Assert.That(Application.targetFrameRate, Is.EqualTo(120));
        Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PrototypeAudioManager_Awake_AppliesPersistedMixValues()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, 0.41f);
        PlayerPrefs.SetFloat(SfxVolumeKey, 0.52f);
        PlayerPrefs.SetFloat(AmbienceVolumeKey, 0.17f);
        PlayerPrefs.Save();

        GameObject audioObject = new("PrototypeAudioManager");
        Component audioManager = AddComponentByName(audioObject, "PrototypeAudioManager");
        yield return null;

        Assert.That(ReadFloatProperty(audioManager, "MasterVolume"), Is.EqualTo(0.41f).Within(0.0001f));
        Assert.That(ReadFloatProperty(audioManager, "SfxVolume"), Is.EqualTo(0.52f).Within(0.0001f));
        Assert.That(ReadFloatProperty(audioManager, "AmbienceVolume"), Is.EqualTo(0.17f).Within(0.0001f));
    }

    [UnityTest]
    public IEnumerator PrototypeAudioManager_TutorialScene_StopsLobbyAmbience()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene lobbyScene = SceneManager.CreateScene("RMainEscape_Lobby");
        Scene tutorialScene = SceneManager.CreateScene("RMainEscape_tuto");

        try
        {
            Assert.That(SceneManager.SetActiveScene(lobbyScene), Is.True, "Could not activate the canonical lobby scene.");

            GameObject audioObject = new("PrototypeAudioManager");
            SceneManager.MoveGameObjectToScene(audioObject, lobbyScene);
            Component audioManager = AddComponentByName(audioObject, "PrototypeAudioManager");
            yield return null;

            AudioSource ambienceSource = ReadFieldValue<AudioSource>(audioManager, "ambienceSource");
            Assert.That(ambienceSource, Is.Not.Null, "PrototypeAudioManager should create an ambience source.");
            Assert.That(ambienceSource.clip, Is.Not.Null, "Lobby ambience should assign a music clip.");
            Assert.That(ambienceSource.isPlaying, Is.True, "Lobby ambience should be playing before the tutorial scene takes over.");

            Assert.That(SceneManager.SetActiveScene(tutorialScene), Is.True, "Could not activate the canonical tutorial scene.");
            InvokeStatic("PrototypeAudioManager", "TryApplySceneAmbienceForActiveScene");

            Assert.That(ambienceSource.volume, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(ambienceSource.isPlaying, Is.False, "Tutorial scene should stop the lingering lobby ambience.");
            Assert.That(ambienceSource.clip, Is.Null, "Tutorial scene should clear the lobby clip so it cannot resume unintentionally.");
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }
        }
    }

    private static object EnsureController()
    {
        Type controllerType = FindTypeByName("RRunSessionController");
        Assert.That(controllerType, Is.Not.Null, "RRunSessionController type is missing.");

        GameObject controllerObject = new("RRunSessionController");
        return controllerObject.AddComponent(controllerType);
    }

    private static Button CreateButton(Transform parent, string name)
    {
        GameObject buttonObject = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        return buttonObject.GetComponent<Button>();
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject rectObject = new(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
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

    private static string ReadStringProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance) as string;
    }

    private static object ReadPropertyValue(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(instance);
    }

    private static float ReadFloatProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance.GetType().Name}.{propertyName} is missing.");
        return (float)property.GetValue(instance);
    }

    private static T ReadFieldValue<T>(object instance, string fieldName)
        where T : class
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} is missing.");
        return field.GetValue(instance) as T;
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName} is missing.");
        field.SetValue(instance, value);
    }

    private static void Invoke(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName} is missing.");
        method.Invoke(instance, arguments);
    }

    private static void InvokeStatic(string typeName, string methodName, params object[] arguments)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");

        MethodInfo method = type.GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} is missing.");
        method.Invoke(null, arguments);
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

    private static Component AddComponentByName(GameObject target, string typeName)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");
        return target.AddComponent(type);
    }
}

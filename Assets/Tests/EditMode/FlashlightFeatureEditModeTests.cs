using System;
using System.IO;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;

public sealed class FlashlightFeatureEditModeTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string WasdPlayerControllerSourcePath = "Assets/Scripts/Player/WasdPlayerController.cs";

    [Test]
    public void PrototypeItemCatalog_DefinesFlashlightAsPassiveItem()
    {
        Type catalogType = FindTypeByName("PrototypeItemCatalog");
        Type definitionType = FindTypeByName("PrototypeItemDefinition");
        Type useKindType = FindTypeByName("PrototypeItemUseKind");
        Assert.That(catalogType, Is.Not.Null, "PrototypeItemCatalog type is missing.");
        Assert.That(definitionType, Is.Not.Null, "PrototypeItemDefinition type is missing.");
        Assert.That(useKindType, Is.Not.Null, "PrototypeItemUseKind type is missing.");

        FieldInfo flashlightItemIdField = catalogType.GetField("FlashlightItemId", StaticFlags);
        MethodInfo tryGetDefinition = catalogType.GetMethod("TryGetDefinition", StaticFlags);
        Assert.That(flashlightItemIdField, Is.Not.Null, "PrototypeItemCatalog.FlashlightItemId is missing.");
        Assert.That(tryGetDefinition, Is.Not.Null, "PrototypeItemCatalog.TryGetDefinition is missing.");

        string flashlightItemId = flashlightItemIdField.GetValue(null) as string;
        object[] arguments = { flashlightItemId, Activator.CreateInstance(definitionType) };
        bool resolved = tryGetDefinition.Invoke(null, arguments) is bool result && result;
        object definition = arguments[1];

        Assert.That(resolved, Is.True);
        Assert.That(ReadPropertyValue<string>(definition, "DisplayName"), Is.EqualTo("Flashlight"));
        Assert.That(ReadPropertyValue<object>(definition, "UseKind"), Is.EqualTo(Enum.Parse(useKindType, "Passive")));
    }

    [Test]
    public void PrototypeItemCatalog_GlassBottleUsesFixedTwoSecondStun()
    {
        Type catalogType = FindTypeByName("PrototypeItemCatalog");
        Type definitionType = FindTypeByName("PrototypeItemDefinition");
        Assert.That(catalogType, Is.Not.Null, "PrototypeItemCatalog type is missing.");
        Assert.That(definitionType, Is.Not.Null, "PrototypeItemDefinition type is missing.");

        FieldInfo glassBottleItemIdField = catalogType.GetField("GlassBottleItemId", StaticFlags);
        MethodInfo tryGetDefinition = catalogType.GetMethod("TryGetDefinition", StaticFlags);
        Assert.That(glassBottleItemIdField, Is.Not.Null, "PrototypeItemCatalog.GlassBottleItemId is missing.");
        Assert.That(tryGetDefinition, Is.Not.Null, "PrototypeItemCatalog.TryGetDefinition is missing.");

        string glassBottleItemId = glassBottleItemIdField.GetValue(null) as string;
        object[] arguments = { glassBottleItemId, Activator.CreateInstance(definitionType) };
        bool resolved = tryGetDefinition.Invoke(null, arguments) is bool result && result;
        object definition = arguments[1];

        Assert.That(resolved, Is.True);
        Assert.That(ReadPropertyValue<float>(definition, "StunDurationMin"), Is.EqualTo(2f));
        Assert.That(ReadPropertyValue<float>(definition, "StunDurationMax"), Is.EqualTo(2f));
    }

    [Test]
    public void InventoryShortcutSummary_UsesInventoryClosePromptWithoutQuickItems()
    {
        object runtimeSettings = LoadRuntimeSettings();
        Type builderType = FindTypeByName("InventoryHudPresentationBuilder");
        Assert.That(builderType, Is.Not.Null, "InventoryHudPresentationBuilder type is missing.");

        MethodInfo buildShortcutSummary = builderType.GetMethod("BuildShortcutSummary", StaticFlags);
        Assert.That(buildShortcutSummary, Is.Not.Null, "BuildShortcutSummary is missing.");

        string summary = buildShortcutSummary.Invoke(null, new[] { runtimeSettings, null }) as string;

        Assert.That(summary, Does.Contain("I close"));
        Assert.That(summary, Does.Not.Contain("K Flashlight"));
    }

    [Test]
    public void EnemyPlayerSpottedScreamAudio_StopDelayUsesShorterOfRemainingClipAndMaxDuration()
    {
        Type audioType = FindTypeByName("EnemyPlayerSpottedScreamAudio");
        Assert.That(audioType, Is.Not.Null, "EnemyPlayerSpottedScreamAudio type is missing.");

        MethodInfo method = audioType.GetMethod("CalculatePlaybackStopDelay", StaticFlags);
        Assert.That(method, Is.Not.Null, "CalculatePlaybackStopDelay should keep scream stop timing testable.");

        Assert.That(InvokeStatic<float>(method, 4f, 1.2f, 1f), Is.EqualTo(1.2f).Within(0.0001f));
        Assert.That(InvokeStatic<float>(method, 0.6f, 1.2f, 1f), Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(InvokeStatic<float>(method, 1f, 5f, 2f), Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void FlashlightStateOwner_ToggleClickOnlyRequestedForSuccessfulEnable()
    {
        Type ownerType = FindTypeByName("FlashlightStateOwner");
        Assert.That(ownerType, Is.Not.Null, "FlashlightStateOwner type is missing.");

        MethodInfo method = ownerType.GetMethod("ShouldPlayFlashlightToggleOnSound", StaticFlags);
        Assert.That(method, Is.Not.Null, "ShouldPlayFlashlightToggleOnSound should keep F-input click policy explicit.");

        Assert.That(InvokeStatic<bool>(method, true, true), Is.True);
        Assert.That(InvokeStatic<bool>(method, true, false), Is.False);
        Assert.That(InvokeStatic<bool>(method, false, true), Is.False);
    }

    [Test]
    public void PlayerFlashlightEquipment_ExposesReusableToggleClickPlayback()
    {
        Type equipmentType = FindTypeByName("PlayerFlashlightEquipment");
        Assert.That(equipmentType, Is.Not.Null, "PlayerFlashlightEquipment type is missing.");

        MethodInfo method = equipmentType.GetMethod("TryPlayFlashlightToggleOnSound", InstanceFlags);
        Assert.That(method, Is.Not.Null, "FlashlightStateOwner should be able to reuse the equipment click playback path.");
        Assert.That(method.IsPublic, Is.True);
        Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
    }

    [Test]
    public void PlayerFlashlightBattery_QuantizesHudChangeNotificationsToDisplayPercent()
    {
        Type batteryType = FindTypeByName("PlayerFlashlightBattery");
        Assert.That(batteryType, Is.Not.Null, "PlayerFlashlightBattery type is missing.");

        MethodInfo method = batteryType.GetMethod("ResolveDisplayChargePercent", StaticFlags);
        Assert.That(method, Is.Not.Null, "PlayerFlashlightBattery should keep HUD charge notification quantization explicit.");

        Assert.That(InvokeStatic<int>(method, 1f), Is.EqualTo(100));
        Assert.That(InvokeStatic<int>(method, 0.994f), Is.EqualTo(99));
        Assert.That(InvokeStatic<int>(method, -1f), Is.EqualTo(0));
        Assert.That(InvokeStatic<int>(method, 2f), Is.EqualTo(100));
    }

    [Test]
    public void FlashlightStateOwner_UsesPublishedPresentationCacheForChangeEvents()
    {
        Type ownerType = FindTypeByName("FlashlightStateOwner");
        Assert.That(ownerType, Is.Not.Null, "FlashlightStateOwner type is missing.");

        Assert.That(ownerType.GetMethod("PublishChanged", InstanceFlags), Is.Not.Null);
        Assert.That(ownerType.GetField("publishedHasFlashlight", InstanceFlags), Is.Not.Null);
        Assert.That(ownerType.GetField("publishedFlashlightEnabled", InstanceFlags), Is.Not.Null);
        Assert.That(ownerType.GetField("publishedChargePercent", InstanceFlags), Is.Not.Null);
        Assert.That(ownerType.GetField("publishedStoredBatteryCount", InstanceFlags), Is.Not.Null);
    }

    [Test]
    public void FlashlightRuntimePresentation_SeparatesLightStateRefreshFromFullEnsurePath()
    {
        string source = File.ReadAllText(WasdPlayerControllerSourcePath);

        Assert.That(source, Does.Contain("private void ApplyFlashlightLightState()"));
        Assert.That(source, Does.Contain("private void RefreshFlashlightRuntimePresentation(bool updatePose = false)"));
        Assert.That(source, Does.Contain("ApplyFlashlightLightState();"));
        Assert.That(source, Does.Contain("RefreshFlashlightRuntimePresentation();"));
    }

    [Test]
    public void PlayerStamina_DrainsPastStartThresholdUntilExhausted()
    {
        Type staminaType = FindTypeByName("PlayerStamina");
        Assert.That(staminaType, Is.Not.Null, "PlayerStamina type is missing.");

        GameObject owner = new("PlayerStaminaTest");

        try
        {
            object stamina = owner.AddComponent(staminaType);
            MethodInfo refillMethod = staminaType.GetMethod("Refill", InstanceFlags);
            MethodInfo tickSprintIntentMethod = staminaType.GetMethod("TickSprintIntent", InstanceFlags);
            Assert.That(refillMethod, Is.Not.Null, "PlayerStamina.Refill is missing.");
            Assert.That(tickSprintIntentMethod, Is.Not.Null, "PlayerStamina.TickSprintIntent is missing.");

            refillMethod.Invoke(stamina, null);

            bool sprinting = true;

            for (int index = 0; index < 400 && sprinting; index++)
            {
                sprinting = (bool)tickSprintIntentMethod.Invoke(stamina, new object[] { true, true, 0.02f });
            }

            Assert.That(ReadPropertyValue<float>(stamina, "CurrentStamina"), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(ReadPropertyValue<bool>(stamina, "IsExhausted"), Is.True);
            Assert.That(ReadPropertyValue<bool>(stamina, "IsDraining"), Is.False);
            Assert.That(ReadPropertyValue<bool>(stamina, "CanSprint"), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static object LoadRuntimeSettings()
    {
        Type settingsType = FindTypeByName("MainEscapeRuntimeSettings");
        Assert.That(settingsType, Is.Not.Null, "MainEscapeRuntimeSettings type is missing.");

        MethodInfo loadMethod = settingsType.GetMethod("Load", StaticFlags);
        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimeSettings.Load is missing.");
        return loadMethod.Invoke(null, null);
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

    private static T ReadPropertyValue<T>(object instance, string propertyName)
    {
        PropertyInfo property = instance?.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"{instance?.GetType().Name}.{propertyName} is missing.");
        return property != null ? (T)property.GetValue(instance) : default;
    }

    private static T InvokeStatic<T>(MethodInfo method, params object[] arguments)
    {
        return (T)method.Invoke(null, arguments);
    }
}

using System;
using System.IO;
using System.Reflection;

using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RSceneBindingContractsTests
{
    private const string PlayerThreatFeedbackRegistrySourcePath = "Assets/Scripts/Enemy/Common/PlayerThreatFeedbackRegistry.cs";
    private const string PlayerThreatHudBinderSourcePath = "Assets/Scripts/Rebuild/UI/IRPlayerThreatHudBinder.cs";

    [TestCase("IRPlayerInventoryHudBinder", "IRebuildHudBinder")]
    [TestCase("IRPlayerHealthHudBinder", "IRebuildHudBinder")]
    [TestCase("IRPlayerThreatHudBinder", "IRebuildHudBinder")]
    [TestCase("IRPlayerQuickSlotsHudBinder", "IRebuildHudBinder")]
    [TestCase("PickupFlashlightDiscoveryController", "IFogOfWarOverlayConsumer")]
    [TestCase("FogReactiveEnemyVisibility", "IFogOfWarOverlayConsumer")]
    [TestCase("FlashlightStateOwner", "IFlashlightStateReadModel")]
    [TestCase("UiSettingsOwner", "IUiSettingsReadModel")]
    [TestCase("FlashlightFogOfWarOverlay", "IFogVisibilityService")]
    [TestCase("MainEscapeKeyGatePoint", "IGateAnchorReadModel")]
    [TestCase("RFloorDirector", "IGateAnchorReadModel")]
    [TestCase("MainEscapeFloorDirector", "IGateAnchorReadModel")]
    [TestCase("FlashlightFogOfWarOverlay", "IFogBypassDebugApplier")]
    [TestCase("PlayerHealth", "IInvincibilityDebugApplier")]
    [TestCase("NoiseSystem", "INoiseDebugPulseApplier")]
    [TestCase("RFloorDirector", "IDebugPresentationApplier")]
    public void BindingTypes_ImplementExpectedContracts(string typeName, string interfaceName)
    {
        Type targetType = FindTypeByName(typeName);
        Type contractType = FindTypeByName(interfaceName);

        Assert.That(targetType, Is.Not.Null, $"{typeName} type is missing.");
        Assert.That(contractType, Is.Not.Null, $"{interfaceName} type is missing.");
        Assert.That(contractType.IsAssignableFrom(targetType), Is.True, $"{typeName} should implement {interfaceName}.");
    }

    [TestCase("PlayerInventory")]
    [TestCase("PlayerHealth")]
    [TestCase("PlayerFlashlightBattery")]
    [TestCase("PlayerQuickItemController")]
    [TestCase("FlashlightStateOwner")]
    public void RuntimeHudSources_ExposeChangedEvent(string typeName)
    {
        Type targetType = FindTypeByName(typeName);
        Assert.That(targetType, Is.Not.Null, $"{typeName} type is missing.");

        EventInfo changedEvent = targetType.GetEvent("Changed", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(changedEvent, Is.Not.Null, $"{typeName} should expose a Changed event.");
    }

    [Test]
    public void ThreatHudPresenter_ContractExists()
    {
        Type presenterType = FindTypeByName("RThreatHudPresenter");
        Type stateType = FindTypeByName("RThreatHudPresentationState");

        Assert.That(presenterType, Is.Not.Null, "RThreatHudPresenter type is missing.");
        Assert.That(stateType, Is.Not.Null, "RThreatHudPresentationState type is missing.");
        Assert.That(presenterType.GetMethod("BuildPresentation", BindingFlags.Static | BindingFlags.Public), Is.Not.Null);
    }

    [Test]
    public void ThreatHudSpotSubscriptions_UseRegistryVersionGate()
    {
        string registrySource = File.ReadAllText(PlayerThreatFeedbackRegistrySourcePath);
        string binderSource = File.ReadAllText(PlayerThreatHudBinderSourcePath);

        Assert.That(registrySource, Does.Contain("public static int Version"));
        Assert.That(registrySource, Does.Contain("Version++;"));
        Assert.That(binderSource, Does.Contain("private int lastSpotSubscriptionVersion"));
        Assert.That(binderSource, Does.Contain("int registryVersion = PlayerThreatFeedbackRegistry.Version;"));
        Assert.That(binderSource, Does.Contain("if (lastSpotSubscriptionVersion == registryVersion)"));
        Assert.That(binderSource, Does.Contain("lastSpotSubscriptionVersion = int.MinValue;"));
    }

    [Test]
    public void FloorDirector_GateAnchorReadModel_UsesMainGateDoorControllerPosition()
    {
        Type floorDirectorType = FindTypeByName("RFloorDirector");
        Type doorControllerType = FindTypeByName("DoorController");
        Assert.That(floorDirectorType, Is.Not.Null, "RFloorDirector type is missing.");
        Assert.That(doorControllerType, Is.Not.Null, "DoorController type is missing.");

        GameObject directorObject = new("BindingContract_Test_FloorDirector");
        GameObject doorObject = new("BindingContract_Test_MainGateDoor");

        try
        {
            Component floorDirector = directorObject.AddComponent(floorDirectorType);
            Component mainGateDoorController = doorObject.AddComponent(doorControllerType);
            doorObject.transform.position = new Vector3(4f, -2f, 0f);

            SetPrivateField(floorDirector, "mainGateDoorController", mainGateDoorController);

            bool resolved = InvokeBoolWithOutVector3(floorDirector, "TryGetGateWorldPosition", out Vector3 worldPosition);

            Assert.That(resolved, Is.True);
            Assert.That(worldPosition, Is.EqualTo(doorObject.transform.position));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(directorObject);
            UnityEngine.Object.DestroyImmediate(doorObject);
        }
    }

    [Test]
    public void RunFloorStateApplier_UsesFloorDirectorGateAnchor_WhenKeyGatePointIsMissing()
    {
        Type floorDirectorType = FindTypeByName("RFloorDirector");
        Type doorControllerType = FindTypeByName("DoorController");
        Type contextType = FindTypeByName("RRunFloorStateContext");
        Type applierType = FindTypeByName("RRunFloorStateApplier");
        Assert.That(floorDirectorType, Is.Not.Null, "RFloorDirector type is missing.");
        Assert.That(doorControllerType, Is.Not.Null, "DoorController type is missing.");
        Assert.That(contextType, Is.Not.Null, "RRunFloorStateContext type is missing.");
        Assert.That(applierType, Is.Not.Null, "RRunFloorStateApplier type is missing.");

        GameObject directorObject = new("BindingContract_Test_FloorDirector");
        GameObject doorObject = new("BindingContract_Test_MainGateDoor");

        try
        {
            Component floorDirector = directorObject.AddComponent(floorDirectorType);
            Component mainGateDoorController = doorObject.AddComponent(doorControllerType);
            doorObject.transform.position = new Vector3(-3f, 6f, 0f);

            SetPrivateField(floorDirector, "mainGateDoorController", mainGateDoorController);

            object context = Activator.CreateInstance(
                contextType,
                null,
                null,
                3,
                false,
                false,
                true,
                null,
                null,
                null,
                floorDirector,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            MethodInfo resolveMethod = applierType.GetMethod("ResolveDirectExitWorldPosition", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null, "RRunFloorStateApplier.ResolveDirectExitWorldPosition is missing.");

            object boxedResult = resolveMethod.Invoke(null, new object[] { context });
            Vector3? resolvedWorldPosition = boxedResult is Vector3 directWorldPosition
                ? directWorldPosition
                : (Vector3?)null;

            Assert.That(resolvedWorldPosition.HasValue, Is.True);
            Assert.That(resolvedWorldPosition.Value, Is.EqualTo(doorObject.transform.position));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(directorObject);
            UnityEngine.Object.DestroyImmediate(doorObject);
        }
    }

    [Test]
    public void KeyGatePoint_GateAnchorReadModel_UsesOwnTransformPosition()
    {
        Type keyGatePointType = FindTypeByName("MainEscapeKeyGatePoint");
        Assert.That(keyGatePointType, Is.Not.Null, "MainEscapeKeyGatePoint type is missing.");

        GameObject keyGateObject = new("BindingContract_Test_KeyGate");

        try
        {
            keyGateObject.AddComponent<SpriteRenderer>();
            Component keyGatePoint = keyGateObject.AddComponent(keyGatePointType);
            keyGateObject.transform.position = new Vector3(1.5f, 2.5f, 0f);

            bool resolved = InvokeBoolWithOutVector3(keyGatePoint, "TryGetGateWorldPosition", out Vector3 worldPosition);

            Assert.That(resolved, Is.True);
            Assert.That(worldPosition, Is.EqualTo(keyGateObject.transform.position));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(keyGateObject);
        }
    }

    [Test]
    public void SceneCompositionRoot_DoesNotForwardLegacyEmergencyStairsProxy_WhenAuthoredOwnerExists()
    {
        Type compositionRootType = FindTypeByName("RSceneCompositionRoot");
        Type legacyStairsType = FindTypeByName("FloorEscapeTransitionPoint");
        Type authoredStairsType = FindTypeByName("MainEscapeEmergencyStairsPoint");
        Assert.That(compositionRootType, Is.Not.Null, "RSceneCompositionRoot type is missing.");
        Assert.That(legacyStairsType, Is.Not.Null, "FloorEscapeTransitionPoint type is missing.");
        Assert.That(authoredStairsType, Is.Not.Null, "MainEscapeEmergencyStairsPoint type is missing.");

        GameObject compositionRootObject = new("BindingContract_Test_CompositionRoot");
        GameObject legacyStairsObject = new("BindingContract_Test_LegacyEmergencyStairs");
        GameObject authoredStairsObject = new("BindingContract_Test_AuthoredEmergencyStairs");

        try
        {
            Component compositionRoot = compositionRootObject.AddComponent(compositionRootType);
            legacyStairsObject.AddComponent<SpriteRenderer>();
            authoredStairsObject.AddComponent<SpriteRenderer>();
            Component legacyStairs = legacyStairsObject.AddComponent(legacyStairsType);
            Component authoredStairs = authoredStairsObject.AddComponent(authoredStairsType);

            SetPrivateField(compositionRoot, "emergencyStairsPoint", legacyStairs);
            SetPrivateField(compositionRoot, "authoredStairsPoint", authoredStairs);

            MethodInfo resolveMethod = compositionRootType.GetMethod("ResolveLegacyEmergencyStairsProxyBinding", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null, "RSceneCompositionRoot.ResolveLegacyEmergencyStairsProxyBinding is missing.");

            object resolvedProxy = resolveMethod.Invoke(compositionRoot, null);
            Assert.That(resolvedProxy, Is.Null, "Authored stairs ownership should suppress the legacy emergency stairs proxy binding.");

            SetPrivateField(compositionRoot, "authoredStairsPoint", null);
            resolvedProxy = resolveMethod.Invoke(compositionRoot, null);
            Assert.That(ReferenceEquals(resolvedProxy, legacyStairs), Is.True, "Legacy emergency stairs proxy should remain available when no authored stairs owner is bound.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(compositionRootObject);
            UnityEngine.Object.DestroyImmediate(legacyStairsObject);
            UnityEngine.Object.DestroyImmediate(authoredStairsObject);
        }
    }

    [TestCase("PickupFlashlightDiscoveryController")]
    [TestCase("DoorDiscoveryVisibilityController")]
    public void FogOverlayConsumers_PreserveBoundSceneFogVisibilityService_WhenRefreshingReference(string consumerTypeName)
    {
        Type consumerType = FindTypeByName(consumerTypeName);
        Type overlayType = FindTypeByName("FlashlightFogOfWarOverlay");
        Assert.That(consumerType, Is.Not.Null, $"{consumerTypeName} type is missing.");
        Assert.That(overlayType, Is.Not.Null, "FlashlightFogOfWarOverlay type is missing.");

        GameObject overlayObject = new($"BindingContract_Test_{consumerTypeName}_Overlay");
        GameObject consumerObject = new($"BindingContract_Test_{consumerTypeName}_Consumer");

        try
        {
            overlayObject.AddComponent<SpriteRenderer>();
            consumerObject.AddComponent<SpriteRenderer>();

            Component overlay = overlayObject.AddComponent(overlayType);
            Component consumer = consumerObject.AddComponent(consumerType);
            SetPrivateField(consumer, "fogVisibilityServiceSource", overlay);

            MethodInfo refreshMethod = consumerType.GetMethod("RefreshFogVisibilityServiceReference", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refreshMethod, Is.Not.Null, $"{consumerTypeName}.RefreshFogVisibilityServiceReference is missing.");
            refreshMethod.Invoke(consumer, null);

            FieldInfo sourceField = consumerType.GetField("fogVisibilityServiceSource", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo serviceField = consumerType.GetField("fogVisibilityService", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(sourceField, Is.Not.Null, $"{consumerTypeName}.fogVisibilityServiceSource is missing.");
            Assert.That(serviceField, Is.Not.Null, $"{consumerTypeName}.fogVisibilityService is missing.");
            Assert.That(sourceField.GetValue(consumer), Is.SameAs(overlay));
            Assert.That(serviceField.GetValue(consumer), Is.SameAs(overlay));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(consumerObject);
            UnityEngine.Object.DestroyImmediate(overlayObject);
        }
    }

    [Test]
    public void SceneBindingCacheUtility_ResolveBindings_MergesCachedAndRuntimeFogConsumers()
    {
        Type overlayType = FindTypeByName("FlashlightFogOfWarOverlay");
        Type consumerType = FindTypeByName("PickupFlashlightDiscoveryController");
        Type cacheUtilityType = FindTypeByName("RSceneBindingCacheUtility");
        Type consumerInterfaceType = FindTypeByName("IFogOfWarOverlayConsumer");
        Assert.That(overlayType, Is.Not.Null, "FlashlightFogOfWarOverlay type is missing.");
        Assert.That(consumerType, Is.Not.Null, "PickupFlashlightDiscoveryController type is missing.");
        Assert.That(cacheUtilityType, Is.Not.Null, "RSceneBindingCacheUtility type is missing.");
        Assert.That(consumerInterfaceType, Is.Not.Null, "IFogOfWarOverlayConsumer type is missing.");

        Scene scene = SceneManager.GetActiveScene();
        GameObject overlayObject = new("BindingContract_Test_FogConsumerMerge_Overlay");
        GameObject cachedConsumerObject = new("BindingContract_Test_CachedFogConsumer");
        GameObject runtimeConsumerObject = new("BindingContract_Test_RuntimeFogConsumer");

        try
        {
            overlayObject.AddComponent<SpriteRenderer>();
            cachedConsumerObject.AddComponent<SpriteRenderer>();
            runtimeConsumerObject.AddComponent<SpriteRenderer>();
            overlayObject.AddComponent(overlayType);

            MonoBehaviour cachedBehaviour = (MonoBehaviour)cachedConsumerObject.AddComponent(consumerType);
            MonoBehaviour runtimeBehaviour = (MonoBehaviour)runtimeConsumerObject.AddComponent(consumerType);

            MethodInfo resolveMethod = cacheUtilityType
                .GetMethod("ResolveBindings", BindingFlags.Static | BindingFlags.Public)
                ?.MakeGenericMethod(consumerInterfaceType);
            Assert.That(resolveMethod, Is.Not.Null, "RSceneBindingCacheUtility.ResolveBindings<T> is missing.");

            Array resolvedConsumers = resolveMethod.Invoke(null, new object[] { scene, new[] { cachedBehaviour } }) as Array;
            Assert.That(resolvedConsumers, Is.Not.Null, "Resolved fog consumer list should not be null.");

            Assert.That(resolvedConsumers.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(ArrayContainsReference(resolvedConsumers, cachedBehaviour), Is.True);
            Assert.That(ArrayContainsReference(resolvedConsumers, runtimeBehaviour), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(runtimeConsumerObject);
            UnityEngine.Object.DestroyImmediate(cachedConsumerObject);
            UnityEngine.Object.DestroyImmediate(overlayObject);
        }
    }

    private static void SetPrivateField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{owner.GetType().Name}.{fieldName} is missing.");
        field.SetValue(owner, value);
    }

    private static bool InvokeBoolWithOutVector3(object owner, string methodName, out Vector3 worldPosition)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, $"{owner.GetType().Name}.{methodName} is missing.");

        object[] arguments = { null };
        bool resolved = method.Invoke(owner, arguments) is bool value && value;
        worldPosition = arguments[0] is Vector3 position ? position : default;
        return resolved;
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

    private static bool ArrayContainsReference(Array values, object candidate)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (ReferenceEquals(values.GetValue(index), candidate))
            {
                return true;
            }
        }

        return false;
    }
}

using System;
using System.IO;
using System.Reflection;

using NUnit.Framework;
using UnityEngine.SceneManagement;

public sealed class SceneLocalRuntimeLookupCleanupEditModeTests
{
    [Test]
    public void RuntimeFloorSystems_UseSceneLocalLookupForAuthoringFallbacks()
    {
        AssertSourceUsesSceneLookup(
            "Assets/Scripts/Rebuild/Runtime/RFloorDirector.cs",
            "RSceneReferenceLookup.FindComponentsInScene<MainEscapeMovementBlockerAuthoring>(gameObject.scene)",
            "FindObjectsByType<MainEscapeMovementBlockerAuthoring>");
        AssertSourceUsesSceneLookup(
            "Assets/Scripts/Objectives/MainEscapeFloorDirector.cs",
            "RSceneReferenceLookup.FindComponentsInScene<MainEscapeMovementBlockerAuthoring>(gameObject.scene)",
            "FindObjectsByType<MainEscapeMovementBlockerAuthoring>");
        AssertSourceUsesSceneLookup(
            "Assets/Scripts/Rebuild/Runtime/RShadowStartleDirector.cs",
            "RSceneReferenceLookup.FindComponentsInScene<MainEscapeShadowStartleMarker>(gameObject.scene)",
            "FindObjectsByType<MainEscapeShadowStartleMarker>");
    }

    [Test]
    public void RuntimePlacementSystems_UseSceneLocalLookupForLegacyCleanup()
    {
        AssertSourceUsesSceneLookup(
            "Assets/Scripts/Rebuild/Runtime/RFloorItemPlacementRuntime.cs",
            "RSceneReferenceLookup.FindComponentsInScene<WorldInventoryPickupBase>(scene)",
            "FindObjectsByType<WorldInventoryPickupBase>");
        AssertSourceUsesSceneLookup(
            "Assets/Scripts/Rebuild/Runtime/RFloorTrapPlacementRuntime.cs",
            "RSceneReferenceLookup.FindComponentsInScene<NoiseFloorPanel>(scene)",
            "FindObjectsByType<NoiseFloorPanel>");
    }

    [Test]
    public void FloorAuthoring_UsesSceneLocalLookupForLegacyMarkers()
    {
        string source = File.ReadAllText("Assets/Scripts/Objectives/MainEscapeFloorAuthoring.cs");

        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<NoiseFloorPanel>(scene).Length"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindTransformInScene(scene, searchNames)"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<NoiseFloorPanel>"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<Transform>"));
    }

    [Test]
    public void PlayerCameraFollow_UsesPlayerSceneForPrototypeOverviewCheck()
    {
        string source = File.ReadAllText("Assets/Scripts/Player/WasdPlayerController.cs");

        Assert.That(source, Does.Contain("PrototypeSceneUtility.UseOverviewCamera(gameObject.scene)"));
        Assert.That(source, Does.Not.Contain("SceneManager.GetActiveScene()"));
    }

    [Test]
    public void ShadowCasterRepair_UsesLoadedSceneOnly()
    {
        string source = File.ReadAllText("Assets/Scripts/Lighting/RuntimeShadowCaster2DConfigurator.cs");

        Assert.That(source, Does.Contain("RepairLoadedShadowCasters(scene)"));
        Assert.That(source, Does.Contain("RepairLoadedShadowCastersInLoadedScenes()"));
        Assert.That(source, Does.Contain("SceneManager.GetSceneAt(index)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<ShadowCaster2D>(scene)"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<ShadowCaster2D>"));
        Assert.That(source, Does.Not.Contain("SceneManager.GetActiveScene()"));
    }

    [Test]
    public void EnemyRuntimeFactories_AcceptSceneCatalogBeforeActiveSceneFallback()
    {
        string enemyFactory = File.ReadAllText("Assets/Scripts/Enemy/EnemyRuntimeFactory.cs");
        string ventFactory = File.ReadAllText("Assets/Scripts/Enemy/BaseOfficeVentEnemyBootstrap.cs");
        string floorDirector = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RFloorDirector.cs");
        string encounterSpawner = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/REncounterSpawner.cs");

        Assert.That(enemyFactory, Does.Contain("MainEscapeRuntimePrefabCatalog runtimePrefabCatalog"));
        Assert.That(enemyFactory, Does.Contain("if (runtimePrefabCatalog != null)"));
        Assert.That(enemyFactory, Does.Contain("MainEscapeRuntimePrefabCatalog.LoadForScene(parent.gameObject.scene)"));
        Assert.That(enemyFactory, Does.Contain("MainEscapeRuntimePrefabCatalog.LoadDefault()"));
        Assert.That(enemyFactory, Does.Not.Contain("MainEscapeRuntimePrefabCatalog.Load()"));
        Assert.That(enemyFactory, Does.Not.Contain("SceneManager.GetActiveScene()"));
        Assert.That(ventFactory, Does.Contain("MainEscapeRuntimePrefabCatalog runtimePrefabCatalog = null"));
        Assert.That(ventFactory, Does.Contain("if (runtimePrefabCatalog != null)"));
        Assert.That(ventFactory, Does.Contain("MainEscapeRuntimePrefabCatalog.LoadForScene(layout.gameObject.scene)"));
        Assert.That(ventFactory, Does.Contain("MainEscapeRuntimePrefabCatalog.LoadDefault()"));
        Assert.That(ventFactory, Does.Not.Contain("MainEscapeRuntimePrefabCatalog.Load()"));
        Assert.That(ventFactory, Does.Not.Contain("SceneManager.GetActiveScene()"));
        Assert.That(floorDirector, Does.Contain("MainEscapeRuntimePrefabCatalog prefabCatalog = MainEscapeRuntimePrefabCatalog.LoadForScene(gameObject.scene)"));
        Assert.That(encounterSpawner, Does.Contain("MainEscapeRuntimePrefabCatalog configuredRuntimePrefabCatalog = null"));
        Assert.That(encounterSpawner, Does.Contain("runtimePrefabCatalog: runtimePrefabCatalog"));
    }

    [Test]
    public void RunSessionConsumers_PreferSceneLocalSessionBeforeSingletonFallback()
    {
        AssertSessionResolverCentralizesSceneSessionFallback();
        AssertStartupSceneHandlingUsesLoadedScenesInsteadOfActiveScene();
        AssertSourceUsesSessionResolver(
            "Assets/Scripts/Player/PlayerCaughtState.cs",
            "RRunSessionResolver.ResolveForContext(this)");
        AssertSourceUsesSceneReload(
            "Assets/Scripts/Player/PlayerCaughtState.cs",
            "SceneLoadUtility.ReloadScene(",
            "SceneLoadUtility.ReloadActiveScene(");
        AssertSourceUsesSessionResolver(
            "Assets/Scripts/Rebuild/Runtime/RTutorialElevatorExitInteractable.cs",
            "RRunSessionResolver.ResolveForContext(this)");
        AssertSourceUsesSessionResolver(
            "Assets/Scripts/Rebuild/Runtime/RFloorDirector.cs",
            "RRunSessionResolver.ResolveForContext(this)");
        AssertSourceUsesSessionResolver(
            "Assets/Scripts/Rebuild/Runtime/RRunController.cs",
            "RRunSessionResolver.ResolveForContext(this)");
        AssertSourceUsesSessionResolver(
            "Assets/Scripts/Grid/Batch2TestRoomBootstrap.AuthoredFloors.cs",
            "RRunSessionResolver.ResolveForScene(scene)");
    }

    [Test]
    public void LegacyBootstrapActorPositioning_UsesSceneLocalNameLookup()
    {
        string source = File.ReadAllText("Assets/Scripts/Grid/Batch2TestRoomBootstrap.cs");

        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindTransformInScene(scene, \"Player\")"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindTransformInScene(scene, \"FocusDummy\")"));
        Assert.That(source, Does.Not.Contain("GameObject.Find(\"Player\")"));
        Assert.That(source, Does.Not.Contain("GameObject.Find(\"FocusDummy\")"));
    }

    [Test]
    public void GeneratedFloorBuildSource_PassesRuntimeParentSceneToBootstrap()
    {
        string buildSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RGeneratedOfficeFloorBuildSource.cs");
        string bootstrap = File.ReadAllText("Assets/Scripts/Grid/Batch2TestRoomBootstrap.cs");

        Assert.That(buildSource, Does.Contain("runtimeParent.gameObject.scene"));
        Assert.That(buildSource, Does.Contain("Batch2TestRoomBootstrap.BuildOfficeFloor(floorDefinition, targetScene, runtimeParent)"));
        Assert.That(bootstrap, Does.Contain("public static OfficeFloorBuildResult BuildOfficeFloor("));
        Assert.That(bootstrap, Does.Contain("ResolveLoadedSceneForFloor(floorDefinition)"));
        Assert.That(bootstrap, Does.Contain("SceneManager.GetSceneAt(index)"));
        Assert.That(bootstrap, Does.Contain("Scene targetScene"));
        Assert.That(bootstrap, Does.Contain("TryBuildSceneResidentAuthoredFloor(floorDefinition, targetScene, out OfficeFloorBuildResult authoredBuild)"));
        Assert.That(bootstrap, Does.Not.Contain("SceneManager.GetActiveScene()"));
    }

    [Test]
    public void SceneResidentAuthoredFloorResolution_UsesSharedSceneLookup()
    {
        string source = File.ReadAllText("Assets/Scripts/Grid/Batch2TestRoomBootstrap.AuthoredFloors.cs");

        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<MainEscapeFloorAuthoring>(scene)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<RRunController>(scene)"));
        Assert.That(source, Does.Not.Contain("scene.GetRootGameObjects()"));
    }

    [Test]
    public void UiSessionFallbacks_DoNotRepeatGlobalSessionFindAndKeepEditorLookupOutOfRuntime()
    {
        string lobbySource = File.ReadAllText("Assets/Scripts/Rebuild/UI/IRLobbyController.cs");
        string lobbyEditorMenuSource = File.ReadAllText("Assets/Scripts/Rebuild/Editor/IRLobbyControllerEditorMenu.cs");
        string gameClearSource = File.ReadAllText("Assets/Scripts/Rebuild/UI/IRGameClearPanelGameplayGate.cs");

        Assert.That(lobbySource, Does.Contain("resolvedSessionController = RRunSessionResolver.ResolveForContext(this)"));
        Assert.That(lobbySource, Does.Contain("PrototypeAudioManager.TryApplySceneAmbienceForScene(gameObject.scene)"));
        Assert.That(lobbySource, Does.Not.Contain("PrototypeAudioManager.TryApplySceneAmbienceForActiveScene()"));
        Assert.That(lobbySource, Does.Not.Contain("EditorSceneManager.GetActiveScene()"));
        Assert.That(lobbyEditorMenuSource, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<IRLobbyController>(EditorSceneManager.GetActiveScene())"));
        Assert.That(lobbySource, Does.Not.Contain("FindFirstObjectByType<RRunSessionController>"));
        Assert.That(lobbySource, Does.Not.Contain("FindFirstObjectByType<IRLobbyController>"));
        Assert.That(gameClearSource, Does.Contain("return RRunSessionResolver.ResolveForScene(scene);"));
        Assert.That(gameClearSource, Does.Not.Contain("FindFirstObjectByType<RRunSessionController>"));
    }

    [Test]
    public void ItemUiIconResolution_UsesSceneCatalogForRuntimeHudCallers()
    {
        Type resolverType = FindTypeByName("PrototypeItemUiIconResolver");
        Type iconType = FindTypeByName("PrototypeItemUiIcon");
        string resolverSource = File.ReadAllText("Assets/Scripts/Inventory/PrototypeItemUiIconResolver.cs");
        string inventorySource = File.ReadAllText("Assets/Scripts/Rebuild/UI/IRInventoryPanelView.cs");
        string quickSlotsSource = File.ReadAllText("Assets/Scripts/Rebuild/UI/IRQuickSlotsPanelView.cs");
        string authoredHudSource = File.ReadAllText("Assets/Scripts/Rebuild/UI/IRAuthoredGameplayHudView.cs");
        string tutorialPickupSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RTutorialInventoryPickup.cs");

        Assert.That(resolverType, Is.Not.Null, "PrototypeItemUiIconResolver type is missing.");
        Assert.That(iconType, Is.Not.Null, "PrototypeItemUiIcon type is missing.");
        MethodInfo tryResolve = resolverType.GetMethod(
            "TryResolve",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(Scene), typeof(string), typeof(string), iconType.MakeByRefType() },
            null);

        Assert.That(tryResolve, Is.Not.Null);
        Assert.That(tryResolve.ReturnType, Is.EqualTo(typeof(bool)));
        Assert.That(tryResolve.IsStatic, Is.True);
        Assert.That(resolverSource, Does.Contain("MainEscapeRuntimePrefabCatalog.LoadForScene(scene)"));
        Assert.That(resolverSource, Does.Contain("MainEscapeRuntimePrefabCatalog.LoadDefault()"));
        Assert.That(resolverSource, Does.Not.Contain("MainEscapeRuntimePrefabCatalog.Load()"));
        Assert.That(inventorySource, Does.Contain("PrototypeItemUiIconResolver.TryResolve(scene, slot.ItemId, slot.DisplayName, out resolvedIcon)"));
        Assert.That(quickSlotsSource, Does.Contain("PrototypeItemUiIconResolver.TryResolve(scene, slot.ItemId, slot.DisplayName, out resolvedIcon)"));
        Assert.That(authoredHudSource, Does.Contain("PrototypeItemUiIconResolver.TryResolve(gameObject.scene, slot.ItemId, slot.DisplayName"));
        Assert.That(tutorialPickupSource, Does.Contain("PrototypeItemUiIconResolver.TryResolve(gameObject.scene, itemId, inventoryDisplayName"));
    }

    [Test]
    public void LegacyCompatibilityWrappers_DelegateToSceneExplicitPaths()
    {
        string runSessionSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs");
        string tutorialBootstrapSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RTutorialSceneBootstrap.cs");

        AssertSceneLoadUtilityHasSceneExplicitReload();
        AssertRSceneRouterHasSessionExplicitReload();
        Assert.That(runSessionSource, Does.Contain("RSceneRouter.ReloadCurrentFloorScene(this);"));
        AssertDefaultCatalogLoadWrapperReturnsLoadDefault();
        AssertCatalogOverrideResolverUsesScenePathKeys();
        Assert.That(tutorialBootstrapSource, Does.Contain("EnsureTutorialNoiseSystem(scene)"));
        Assert.That(tutorialBootstrapSource, Does.Not.Contain("NoiseSystem.Instance"));
    }

    private static void AssertSourceUsesSceneLookup(string sourcePath, string expected, string forbidden)
    {
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain(expected));
        Assert.That(source, Does.Not.Contain(forbidden));
    }

    private static void AssertSessionResolverCentralizesSceneSessionFallback()
    {
        string source = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RRunSessionResolver.cs");
        string sceneLookup = "RSceneReferenceLookup.FindFirstComponentInScene<RRunSessionController>(scene)";
        string cachedInstanceLookup = "RRunSessionController.TryGetCachedInstance(out RRunSessionController cachedSessionController)";

        Assert.That(source, Does.Contain(sceneLookup));
        Assert.That(source, Does.Contain(cachedInstanceLookup));
        Assert.That(source, Does.Not.Contain("RRunSessionController.Instance"));
        Assert.That(source, Does.Not.Contain("FindFirstObjectByType<RRunSessionController>"));
        Assert.That(
            source.IndexOf(sceneLookup, StringComparison.Ordinal),
            Is.LessThan(source.IndexOf(cachedInstanceLookup, StringComparison.Ordinal)));

        string runSessionControllerSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs");

        Assert.That(runSessionControllerSource, Does.Contain("public static RRunSessionController Instance => instance;"));
        Assert.That(runSessionControllerSource, Does.Contain("public static bool TryGetCachedInstance(out RRunSessionController sessionController)"));
        Assert.That(runSessionControllerSource, Does.Not.Contain("FindFirstObjectByType<RRunSessionController>"));

        string compositionRootSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RSceneCompositionRoot.cs");
        string residencyPolicySource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RRuntimeSettingsFloorResidencyPolicy.cs");
        string membershipSource = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RSceneRouteMembershipUtility.cs");

        Assert.That(compositionRootSource, Does.Contain("RRunSessionResolver.ResolveForScene(gameObject.scene)"));
        Assert.That(residencyPolicySource, Does.Contain("RRunSessionResolver.ResolveForScene(scene)"));
        Assert.That(membershipSource, Does.Contain("RRunSessionResolver.ResolveForScene(scene)"));
        Assert.That(compositionRootSource, Does.Not.Contain("RRunSessionController.Instance"));
        Assert.That(residencyPolicySource, Does.Not.Contain("RRunSessionController.Instance"));
        Assert.That(membershipSource, Does.Not.Contain("RRunSessionController.Instance"));
    }

    private static void AssertStartupSceneHandlingUsesLoadedScenesInsteadOfActiveScene()
    {
        string source = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RRunSessionController.cs");

        Assert.That(source, Does.Contain("HandleLoadedScenesAtStartup();"));
        Assert.That(source, Does.Contain("SceneManager.GetSceneAt(index)"));
        Assert.That(source, Does.Not.Contain("HandleSceneLoaded(SceneManager.GetActiveScene()"));
    }

    private static void AssertSourceUsesSessionResolver(string sourcePath, string expectedResolverCall)
    {
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain(expectedResolverCall));
    }

    private static void AssertSourceUsesSceneReload(string sourcePath, string expectedReloadCall, string forbiddenReloadCall)
    {
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain(expectedReloadCall));
        Assert.That(source, Does.Not.Contain(forbiddenReloadCall));
    }

    private static void AssertSceneLoadUtilityHasSceneExplicitReload()
    {
        Type sceneLoadUtilityType = FindTypeByName("SceneLoadUtility");
        Assert.That(sceneLoadUtilityType, Is.Not.Null, "SceneLoadUtility type is missing.");

        MethodInfo reloadSceneMethod = sceneLoadUtilityType.GetMethod(
            "ReloadScene",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(Scene), typeof(bool), typeof(string), typeof(string), typeof(string) },
            null);

        Assert.That(reloadSceneMethod, Is.Not.Null);
        Assert.That(reloadSceneMethod.ReturnType, Is.EqualTo(typeof(void)));

        string source = File.ReadAllText("Assets/Scripts/Runtime/SceneLoadUtility.cs");
        Assert.That(source, Does.Not.Contain("SceneManager.GetActiveScene()"));
    }

    private static void AssertRSceneRouterHasSessionExplicitReload()
    {
        Type sceneRouterType = FindTypeByName("RSceneRouter");
        Type runSessionType = FindTypeByName("RRunSessionController");
        Assert.That(sceneRouterType, Is.Not.Null, "RSceneRouter type is missing.");
        Assert.That(runSessionType, Is.Not.Null, "RRunSessionController type is missing.");

        MethodInfo reloadCurrentFloorSceneMethod = sceneRouterType.GetMethod(
            "ReloadCurrentFloorScene",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { runSessionType },
            null);

        Assert.That(reloadCurrentFloorSceneMethod, Is.Not.Null);
        Assert.That(reloadCurrentFloorSceneMethod.ReturnType, Is.EqualTo(typeof(void)));

        string source = File.ReadAllText("Assets/Scripts/Rebuild/Runtime/RSceneRouter.cs");
        Assert.That(source, Does.Contain("sessionController.GetCurrentFloorScenePath()"));
        Assert.That(source, Does.Not.Contain("ReloadActiveScene"));
    }

    private static void AssertDefaultCatalogLoadWrapperReturnsLoadDefault()
    {
        Type resolverType = FindTypeByName("MainEscapeRuntimePrefabCatalogOverrideResolver");
        Type catalogType = FindTypeByName("MainEscapeRuntimePrefabCatalog");
        Assert.That(resolverType, Is.Not.Null, "MainEscapeRuntimePrefabCatalogOverrideResolver type is missing.");
        Assert.That(catalogType, Is.Not.Null, "MainEscapeRuntimePrefabCatalog type is missing.");

        MethodInfo loadMethod = resolverType.GetMethod("Load", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo loadDefaultMethod = resolverType.GetMethod("LoadDefault", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(loadMethod, Is.Not.Null, "MainEscapeRuntimePrefabCatalogOverrideResolver.Load is missing.");
        Assert.That(loadDefaultMethod, Is.Not.Null, "MainEscapeRuntimePrefabCatalogOverrideResolver.LoadDefault is missing.");
        Assert.That(loadMethod.ReturnType, Is.EqualTo(catalogType));
        Assert.That(loadDefaultMethod.ReturnType, Is.EqualTo(catalogType));
        Assert.That(loadMethod.GetParameters(), Is.Empty);
        Assert.That(loadDefaultMethod.GetParameters(), Is.Empty);

        object loadResult = loadMethod.Invoke(null, Array.Empty<object>());
        object loadDefaultResult = loadDefaultMethod.Invoke(null, Array.Empty<object>());

        Assert.That(loadDefaultResult, Is.Not.Null, "Default runtime prefab catalog should be loadable.");
        Assert.That(loadResult, Is.SameAs(loadDefaultResult));
    }

    private static void AssertCatalogOverrideResolverUsesScenePathKeys()
    {
        Type resolverType = FindTypeByName("MainEscapeRuntimePrefabCatalogOverrideResolver");
        Assert.That(resolverType, Is.Not.Null, "MainEscapeRuntimePrefabCatalogOverrideResolver type is missing.");

        MethodInfo loadForSceneMethod = resolverType.GetMethod(
            "LoadForScene",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(Scene) },
            null);
        Assert.That(loadForSceneMethod, Is.Not.Null);

        MethodInfo tryGetOverrideResourcePathMethod = resolverType.GetMethod(
            "TryGetOverrideResourcePath",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(tryGetOverrideResourcePathMethod, Is.Not.Null);

        object[] pathArgs = { "Assets/Scenes/RMainScene_5F.unity", null };
        bool resolved = (bool)tryGetOverrideResourcePathMethod.Invoke(null, pathArgs);
        Assert.That(resolved, Is.True);
        Assert.That(pathArgs[1], Is.EqualTo("MainEscape/PrefabCatalogOverrides/RMainScene_5F/MainEscapeRuntimePrefabCatalog"));

        object[] emptyArgs = { string.Empty, null };
        bool emptyResolved = (bool)tryGetOverrideResourcePathMethod.Invoke(null, emptyArgs);
        Assert.That(emptyResolved, Is.False);
        Assert.That(emptyArgs[1], Is.EqualTo(string.Empty));

        string source = File.ReadAllText("Assets/Scripts/Objectives/MainEscapeRuntimePrefabCatalogOverrideResolver.cs");
        Assert.That(source, Does.Not.Contain("IsProtectedPrefabOverrideSceneName(sceneName)"));
        Assert.That(source, Does.Not.Contain("SceneManager.GetActiveScene()"));
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

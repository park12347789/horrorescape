using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityObject = UnityEngine.Object;

public sealed class RTutorialSceneBootstrapEditModeTests
{
    private const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string SourcePath = "Assets/Scripts/Rebuild/Runtime/RTutorialSceneBootstrap.cs";

    [Test]
    public void SceneLookup_UsesSceneScopedReferenceLookup_ForLowRiskRuntimeReferences()
    {
        string source = File.ReadAllText(SourcePath);

        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindTransformInScene(scene, AuthoringRootName)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindTransformInScene(scene, \"RMainCamera\")"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<Light2D>(scene)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<IRHudCanvas>(scene)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<IRAuthoredGameplayHudView>(scene)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<FlashlightFogOfWarOverlay>(scene)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<AudioListener>(cameraObject.scene)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindComponentsInScene<EnemyStateMachine>(gameObject.scene)"));
        Assert.That(source, Does.Contain("RSceneReferenceLookup.FindFirstComponentInScene<NoiseSystem>(scene)"));
        Assert.That(source, Does.Contain("NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem)"));
        Assert.That(source, Does.Contain("ResolveNoiseEventBus()?.TryEmitNoise"));
        Assert.That(source, Does.Not.Contain("GameObject.Find(AuthoringRootName)"));
        Assert.That(source, Does.Not.Contain("GameObject.Find(\"RMainCamera\")"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<Light2D>"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<IRHudCanvas>"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<IRAuthoredGameplayHudView>"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<FlashlightFogOfWarOverlay>"));
        Assert.That(source, Does.Not.Contain("FindFirstObjectByType<AudioListener>"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<EnemyStateMachine>(FindObjectsSortMode.None)"));
        Assert.That(source, Does.Not.Contain("FindObjectsByType<NoiseSystem>"));
        Assert.That(source, Does.Not.Contain("NoiseSystem.Instance"));
        Assert.That(source, Does.Not.Contain("NoiseSystem.TryEmitNoise"));
    }

    [Test]
    public void ResolveTutorialSentrySpawnWorldPosition_UsesMarkerWorldPositionExactly()
    {
        TutorialFixture fixture = new();

        try
        {
            Component bootstrap = fixture.CreateBootstrap();
            GameObject markerObject = new("SentrySpawnMarker_01");
            markerObject.transform.SetParent(fixture.Root.transform, false);
            markerObject.transform.position = new Vector3(2.37f, -1.42f, 0f);
            Component marker = AddEnemyPlacementMarker(markerObject, "Sentry", "SentrySpawnMarker_01");

            Vector3 resolvedPosition = ResolveTutorialSentrySpawnWorldPosition(
                bootstrap,
                fixture.Root.transform,
                fixture.MapService,
                marker);

            Assert.That(resolvedPosition, Is.EqualTo(markerObject.transform.position), "Tutorial sentry markers should be trusted as exact authored world positions.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void BuildTutorialVisualPropBlockers_CreatesBoundsBlocker_WhenNoGroundCellsResolve()
    {
        TutorialFixture fixture = new();

        try
        {
            GameObject propObject = new("VexedTileBProp_15_Test");
            propObject.transform.SetParent(fixture.Root.transform, false);
            propObject.transform.position = new Vector3(3.25f, 1.75f, 40f);
            propObject.transform.localScale = new Vector3(2f, 1f, 1f);
            SpriteRenderer renderer = propObject.AddComponent<SpriteRenderer>();
            renderer.sprite = fixture.UnitSprite;

            BuildTutorialVisualPropBlockers(fixture.Root.transform, fixture.GroundTilemap, fixture.MapService);

            Transform blockerRoot = fixture.Root.transform.Find("RuntimePropBlockers");
            Assert.That(blockerRoot, Is.Not.Null, "Tutorial visual props should synthesize runtime blockers even when no ground tile cell resolves.");
            Assert.That(blockerRoot.childCount, Is.EqualTo(1));

            BoxCollider2D blockerCollider = blockerRoot.GetChild(0).GetComponent<BoxCollider2D>();
            Assert.That(blockerCollider, Is.Not.Null);
            Assert.That(blockerCollider.enabled, Is.True);
            Assert.That(blockerCollider.size.x, Is.GreaterThan(1.9f));
            Assert.That(blockerCollider.size.y, Is.GreaterThan(0.9f));

            Vector3Int fallbackCell = WorldToCell2D(fixture.GroundTilemap, renderer.bounds.center);
            Assert.That(HasBlockingProp(fixture.MapService, fallbackCell), Is.True, "Pathing should still know about a fallback blocker cell.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void EnsureTutorialRuntimeDoor_SelfContainedDoor_SkipsLegacyDoorController()
    {
        TutorialFixture fixture = new();

        try
        {
            Transform interactionRoot = new GameObject("RTutorialDoorInteractions").transform;
            interactionRoot.SetParent(fixture.Root.transform, false);
            GameObject visualRoot = CreateSelfContainedDoorVisual(
                fixture,
                "VexedTileBProp_01_Top (8)",
                "FrontDoor");

            bool createdLegacyDoor = EnsureTutorialRuntimeDoor(
                interactionRoot,
                fixture.Root.transform,
                fixture.MapService,
                "TutorialFrontDoorController",
                visualRoot.name,
                "VexedTileBProp_01_Top");

            Assert.That(createdLegacyDoor, Is.False, "Tutorial self-contained doors should own their own interaction instead of spawning a separate DoorController.");
            Assert.That(interactionRoot.Find("TutorialFrontDoorController"), Is.Null, "Tutorial bootstrap should not keep a legacy runtime controller for self-contained regular doors.");
            Assert.That(GetComponentsInChildren(interactionRoot, "DoorController"), Is.Empty, "No legacy DoorController should be synthesized when the authored door prefab already owns its runtime.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void EnsureTutorialDoorTilesForVisual_SelfContainedDoor_UsesExplicitDoorCells()
    {
        TutorialFixture fixture = new();
        Tile runtimeDoorTile = ScriptableObject.CreateInstance<Tile>();
        runtimeDoorTile.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            GameObject visualRoot = CreateSelfContainedDoorVisual(
                fixture,
                "CustomSideDoorClosed",
                "SideDoor42");
            Component selfContainedDoor = GetRequiredComponent(visualRoot, "MainEscapeSelfContainedDoor");
            Assert.That(selfContainedDoor, Is.Not.Null);

            Vector3Int[] expectedCells = ResolveDoorCells(selfContainedDoor, fixture.GroundTilemap);
            Assert.That(expectedCells, Has.Length.EqualTo(2), "Tutorial regular doors should keep the authored two-cell footprint.");

            EnsureTutorialDoorTilesForVisual(
                fixture.Root.transform,
                fixture.GroundTilemap,
                fixture.DoorTilemap,
                runtimeDoorTile,
                "CustomSideDoorClosed",
                "CustomSideDoorClosed");

            Assert.That(
                fixture.DoorTilemap.GetTile(expectedCells[0]),
                Is.SameAs(runtimeDoorTile),
                "Tutorial runtime door tile seeding should use the self-contained door's explicit anchor cell.");
            Assert.That(
                fixture.DoorTilemap.GetTile(expectedCells[1]),
                Is.SameAs(runtimeDoorTile),
                "Tutorial runtime door tile seeding should use the self-contained door's explicit second cell.");
        }
        finally
        {
            UnityObject.DestroyImmediate(runtimeDoorTile);
            fixture.Dispose();
        }
    }

    private static Component AddEnemyPlacementMarker(GameObject target, string placementKindName, string placementId)
    {
        Type markerType = FindTypeByName("MainEscapeEnemyPlacementMarker");
        Assert.That(markerType, Is.Not.Null, "MainEscapeEnemyPlacementMarker type is missing.");

        Type markerKindType = FindTypeByName("MainEscapeEnemyPlacementKind");
        Assert.That(markerKindType, Is.Not.Null, "MainEscapeEnemyPlacementKind type is missing.");

        Component marker = target.AddComponent(markerType);
        object markerKind = Enum.Parse(markerKindType, placementKindName, ignoreCase: false);
        MethodInfo configureMethod = markerType.GetMethod(
            "Configure",
            InstanceMemberFlags,
            null,
            new[] { markerKindType, typeof(string) },
            null);

        Assert.That(configureMethod, Is.Not.Null, "MainEscapeEnemyPlacementMarker.Configure() is missing.");
        configureMethod.Invoke(marker, new[] { markerKind, placementId });
        return marker;
    }

    private static Vector3 ResolveTutorialSentrySpawnWorldPosition(
        Component bootstrap,
        Transform root,
        Component mapService,
        Component marker)
    {
        MethodInfo method = bootstrap.GetType().GetMethod(
            "ResolveTutorialSentrySpawnWorldPosition",
            InstanceMemberFlags,
            null,
            new[] { typeof(Transform), mapService.GetType(), marker.GetType() },
            null);

        Assert.That(method, Is.Not.Null, "RTutorialSceneBootstrap.ResolveTutorialSentrySpawnWorldPosition() is missing.");
        object value = method.Invoke(bootstrap, new object[] { root, mapService, marker });
        Assert.That(value, Is.InstanceOf<Vector3>());
        return (Vector3)value;
    }

    private static void BuildTutorialVisualPropBlockers(
        Transform root,
        Tilemap groundTilemap,
        Component mapService)
    {
        Type bootstrapType = FindTypeByName("RTutorialSceneBootstrap");
        Assert.That(bootstrapType, Is.Not.Null, "RTutorialSceneBootstrap type is missing.");

        MethodInfo method = bootstrapType.GetMethod(
            "BuildTutorialVisualPropBlockers",
            StaticMemberFlags,
            null,
            new[] { typeof(Transform), typeof(Tilemap), mapService.GetType() },
            null);

        Assert.That(method, Is.Not.Null, "RTutorialSceneBootstrap.BuildTutorialVisualPropBlockers() is missing.");
        method.Invoke(null, new object[] { root, groundTilemap, mapService });
    }

    private static bool EnsureTutorialRuntimeDoor(
        Transform interactionRoot,
        Transform authoringRoot,
        Component mapService,
        string controllerName,
        string visualName,
        string visualNamePrefix)
    {
        Type bootstrapType = FindTypeByName("RTutorialSceneBootstrap");
        Assert.That(bootstrapType, Is.Not.Null, "RTutorialSceneBootstrap type is missing.");

        MethodInfo method = bootstrapType.GetMethod(
            "EnsureTutorialRuntimeDoor",
            StaticMemberFlags,
            null,
            new[] { typeof(Transform), typeof(Transform), mapService.GetType(), typeof(string), typeof(string), typeof(string) },
            null);

        Assert.That(method, Is.Not.Null, "RTutorialSceneBootstrap.EnsureTutorialRuntimeDoor() is missing.");
        object value = method.Invoke(null, new object[] { interactionRoot, authoringRoot, mapService, controllerName, visualName, visualNamePrefix });
        Assert.That(value, Is.InstanceOf<bool>());
        return (bool)value;
    }

    private static void EnsureTutorialDoorTilesForVisual(
        Transform root,
        Tilemap groundTilemap,
        Tilemap doorTilemap,
        TileBase runtimeDoorTile,
        string visualName,
        string visualNamePrefix)
    {
        Type bootstrapType = FindTypeByName("RTutorialSceneBootstrap");
        Assert.That(bootstrapType, Is.Not.Null, "RTutorialSceneBootstrap type is missing.");

        MethodInfo method = bootstrapType.GetMethod(
            "EnsureTutorialDoorTilesForVisual",
            StaticMemberFlags,
            null,
            new[] { typeof(Transform), typeof(Tilemap), typeof(Tilemap), typeof(TileBase), typeof(string), typeof(string) },
            null);

        Assert.That(method, Is.Not.Null, "RTutorialSceneBootstrap.EnsureTutorialDoorTilesForVisual() is missing.");
        method.Invoke(null, new object[] { root, groundTilemap, doorTilemap, runtimeDoorTile, visualName, visualNamePrefix });
    }

    private static GameObject CreateSelfContainedDoorVisual(
        TutorialFixture fixture,
        string objectName,
        string visualVariantName)
    {
        GameObject visualRoot = new(objectName);
        visualRoot.transform.SetParent(fixture.Root.transform, false);
        visualRoot.transform.position = fixture.GroundTilemap.GetCellCenterWorld(Vector3Int.zero);

        SpriteRenderer closedRenderer = visualRoot.AddComponent<SpriteRenderer>();
        closedRenderer.sprite = fixture.UnitSprite;
        BoxCollider2D blockerCollider = visualRoot.AddComponent<BoxCollider2D>();
        blockerCollider.size = Vector2.one;
        blockerCollider.offset = Vector2.zero;

        GameObject openVisual = new("OpenVisual");
        openVisual.transform.SetParent(visualRoot.transform, false);
        SpriteRenderer openRenderer = openVisual.AddComponent<SpriteRenderer>();
        openRenderer.sprite = fixture.UnitSprite;

        Type variantOverrideType = FindTypeByName("MainEscapeDoorVisualVariantOverride");
        Assert.That(variantOverrideType, Is.Not.Null, "MainEscapeDoorVisualVariantOverride type is missing.");
        Component variantOverride = visualRoot.AddComponent(variantOverrideType);
        Type variantType = FindTypeByName("MainEscapeDoorVisualVariantKind");
        Assert.That(variantType, Is.Not.Null, "MainEscapeDoorVisualVariantKind type is missing.");
        object visualVariant = Enum.Parse(variantType, visualVariantName, ignoreCase: false);
        MethodInfo configureMethod = variantOverride.GetType().GetMethod(
            "Configure",
            InstanceMemberFlags,
            null,
            new[] { variantType },
            null);
        Assert.That(configureMethod, Is.Not.Null, "MainEscapeDoorVisualVariantOverride.Configure() is missing.");
        configureMethod.Invoke(variantOverride, new[] { visualVariant });

        Type selfContainedDoorType = FindTypeByName("MainEscapeSelfContainedDoor");
        Assert.That(selfContainedDoorType, Is.Not.Null, "MainEscapeSelfContainedDoor type is missing.");
        Component selfContainedDoor = visualRoot.AddComponent(selfContainedDoorType);
        InvokeParameterlessMethod(selfContainedDoor, "OnValidate");
        return visualRoot;
    }

    private static bool HasBlockingProp(Component mapService, Vector3Int cellPosition)
    {
        MethodInfo method = mapService.GetType().GetMethod(
            "HasBlockingProp",
            InstanceMemberFlags,
            null,
            new[] { typeof(Vector3Int) },
            null);

        Assert.That(method, Is.Not.Null, "GridMapService.HasBlockingProp() is missing.");
        object value = method.Invoke(mapService, new object[] { cellPosition });
        Assert.That(value, Is.InstanceOf<bool>());
        return (bool)value;
    }

    private static Vector3Int[] ResolveDoorCells(Component selfContainedDoor, Tilemap groundTilemap)
    {
        MethodInfo method = selfContainedDoor?.GetType().GetMethod(
            "ResolveDoorCells",
            InstanceMemberFlags,
            null,
            new[] { typeof(Tilemap) },
            null);

        Assert.That(method, Is.Not.Null, $"{selfContainedDoor?.GetType().Name}.ResolveDoorCells() is missing.");
        object value = method.Invoke(selfContainedDoor, new object[] { groundTilemap });
        Assert.That(value, Is.InstanceOf<Vector3Int[]>());
        return (Vector3Int[])value;
    }

    private static Component GetRequiredComponent(GameObject owner, string typeName)
    {
        Type componentType = FindTypeByName(typeName);
        Assert.That(componentType, Is.Not.Null, $"{typeName} type is missing.");
        Component component = owner.GetComponent(componentType);
        Assert.That(component, Is.Not.Null, $"{owner.name} is missing {typeName}.");
        return component;
    }

    private static Component[] GetComponentsInChildren(Transform root, string typeName)
    {
        Type componentType = FindTypeByName(typeName);
        Assert.That(componentType, Is.Not.Null, $"{typeName} type is missing.");
        return root != null ? root.GetComponentsInChildren(componentType, true) : Array.Empty<Component>();
    }

    private static void InvokeParameterlessMethod(object target, string methodName)
    {
        MethodInfo method = target?.GetType().GetMethod(
            methodName,
            InstanceMemberFlags,
            null,
            Type.EmptyTypes,
            null);

        Assert.That(method, Is.Not.Null, $"{target?.GetType().Name}.{methodName}() is missing.");
        method.Invoke(target, null);
    }

    private static Vector3Int WorldToCell2D(Tilemap tilemap, Vector3 worldPosition)
    {
        if (tilemap == null)
        {
            return Vector3Int.zero;
        }

        Vector3 adjustedWorldPosition = worldPosition;
        adjustedWorldPosition.z = tilemap.transform.position.z;

        Vector3Int cell = tilemap.WorldToCell(adjustedWorldPosition);
        cell.z = 0;
        return cell;
    }

    private static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] candidateTypes;

            try
            {
                candidateTypes = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                candidateTypes = exception.Types;
            }

            if (candidateTypes == null)
            {
                continue;
            }

            for (int typeIndex = 0; typeIndex < candidateTypes.Length; typeIndex++)
            {
                Type candidate = candidateTypes[typeIndex];

                if (candidate != null && (candidate.Name == typeName || candidate.FullName == typeName))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private sealed class TutorialFixture : IDisposable
    {
        private readonly Texture2D texture;

        public TutorialFixture()
        {
            Root = new GameObject("RTutorialAuthoring");
            GridObject = new GameObject("RTutorialTileGrid");
            GridObject.transform.SetParent(Root.transform, false);
            Grid = GridObject.AddComponent<Grid>();

            GameObject groundObject = new("Tiles_ground");
            groundObject.transform.SetParent(GridObject.transform, false);
            GroundTilemap = groundObject.AddComponent<Tilemap>();
            groundObject.AddComponent<TilemapRenderer>();

            GameObject wallObject = new("Tiles_wall");
            wallObject.transform.SetParent(GridObject.transform, false);
            WallTilemap = wallObject.AddComponent<Tilemap>();

            GameObject doorObject = new("Doors");
            doorObject.transform.SetParent(GridObject.transform, false);
            DoorTilemap = doorObject.AddComponent<Tilemap>();

            MapService = CreateMapService(GridObject, Grid, GroundTilemap, WallTilemap, DoorTilemap);

            texture = CreateTexture();
            UnitSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            UnitSprite.name = "TutorialUnitSprite";
        }

        public GameObject Root { get; }
        public GameObject GridObject { get; }
        public Grid Grid { get; }
        public Tilemap GroundTilemap { get; }
        public Tilemap WallTilemap { get; }
        public Tilemap DoorTilemap { get; }
        public Component MapService { get; }
        public Sprite UnitSprite { get; }

        public Component CreateBootstrap()
        {
            Type bootstrapType = FindTypeByName("RTutorialSceneBootstrap");
            Assert.That(bootstrapType, Is.Not.Null, "RTutorialSceneBootstrap type is missing.");
            return Root.AddComponent(bootstrapType);
        }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityObject.DestroyImmediate(Root);
            }

            if (UnitSprite != null)
            {
                UnityObject.DestroyImmediate(UnitSprite);
            }

            if (texture != null)
            {
                UnityObject.DestroyImmediate(texture);
            }
        }

        private static Component CreateMapService(
            GameObject target,
            Grid grid,
            Tilemap groundTilemap,
            Tilemap wallTilemap,
            Tilemap doorTilemap)
        {
            Type mapServiceType = FindTypeByName("GridMapService");
            Assert.That(mapServiceType, Is.Not.Null, "GridMapService type is missing.");

            Component mapService = target.AddComponent(mapServiceType);
            MethodInfo initializeMethod = mapServiceType.GetMethod(
                "Initialize",
                InstanceMemberFlags,
                null,
                new[] { typeof(Grid), typeof(Tilemap), typeof(Tilemap), typeof(Tilemap), typeof(LayerMask) },
                null);

            Assert.That(initializeMethod, Is.Not.Null, "GridMapService.Initialize() is missing.");
            initializeMethod.Invoke(mapService, new object[] { grid, groundTilemap, wallTilemap, doorTilemap, (LayerMask)0 });
            return mapService;
        }

        private static Texture2D CreateTexture()
        {
            Texture2D createdTexture = new(16, 16, TextureFormat.RGBA32, false);
            createdTexture.hideFlags = HideFlags.HideAndDontSave;
            Color[] pixels = new Color[16 * 16];

            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = Color.white;
            }

            createdTexture.SetPixels(pixels);
            createdTexture.Apply();
            return createdTexture;
        }
    }
}

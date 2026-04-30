using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

using Object = UnityEngine.Object;

public sealed class MainEscapeSelfContainedRegularDoorContractEditModeTests
{
    private const string FrontDoorPrefabPath = "Assets/Prefabs/Environment/MainEscape/Vexed/TileBSplitDoors/VexedTileBProp_01_Top.prefab";
    private const string SideDoorPrefabPath = "Assets/Prefabs/Environment/MainEscape/Doors/CustomSideDoorClosed.prefab";
    private const string FrontDoorVariantName = "FrontDoor";
    private const string SideDoorVariantName = "SideDoor42";
    private const string SelfContainedDoorTypeName = "MainEscapeSelfContainedDoor";
    private const string DoorVariantOverrideTypeName = "MainEscapeDoorVisualVariantOverride";
    private const string FloorAuthoringTypeName = "MainEscapeFloorAuthoring";
    private const string SelfContainedDoorSourcePath = "Assets/Scripts/Objectives/MainEscapeSelfContainedDoor.cs";
    private const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase(FrontDoorPrefabPath, FrontDoorVariantName, false)]
    [TestCase(SideDoorPrefabPath, SideDoorVariantName, true)]
    public void RegularDoorPrefab_IsSelfContainedAndResolvesExpectedCells(
        string prefabPath,
        string expectedVariantName,
        bool expectVerticalCells)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefabAsset, Is.Not.Null, $"Missing regular door prefab at '{prefabPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Component selfContainedDoor = prefabRoot.GetComponent(SelfContainedDoorTypeName);
            Component variantOverride = prefabRoot.GetComponent(DoorVariantOverrideTypeName);
            BoxCollider2D blockerCollider = prefabRoot.GetComponent<BoxCollider2D>();

            Assert.That(selfContainedDoor, Is.Not.Null, $"{prefabPath} is missing {SelfContainedDoorTypeName}.");
            Assert.That(variantOverride, Is.Not.Null, $"{prefabPath} is missing {DoorVariantOverrideTypeName}.");
            Assert.That(blockerCollider, Is.Not.Null, $"{prefabPath} is missing {nameof(BoxCollider2D)}.");
            Assert.That(ReadVariantName(variantOverride), Is.EqualTo(expectedVariantName), $"{prefabPath} should keep the expected regular door visual variant.");

            InvokeParameterlessMethod(selfContainedDoor, "OnValidate");

            Assert.That(
                MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "openVisual") as Transform,
                Is.Not.Null,
                $"{prefabPath} must keep the OpenVisual child wired on {SelfContainedDoorTypeName}.");
            Assert.That(
                MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "closedRenderer") as SpriteRenderer,
                Is.Not.Null,
                $"{prefabPath} must keep the closed renderer wired on {SelfContainedDoorTypeName}.");
            Assert.That(
                MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "openRenderer") as SpriteRenderer,
                Is.Not.Null,
                $"{prefabPath} must keep the open renderer wired on {SelfContainedDoorTypeName}.");
            Assert.That(
                MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "blockerCollider") as BoxCollider2D,
                Is.Not.Null,
                $"{prefabPath} must keep the blocker collider wired on {SelfContainedDoorTypeName}.");
            Assert.That(
                prefabRoot.transform.Find("OpenVisual"),
                Is.Not.Null,
                $"{prefabPath} must keep an OpenVisual child so the prefab owns its own open-state presentation.");

            Tilemap groundTilemap = CreateGroundTilemap(prefabRoot.transform);
            prefabRoot.transform.position = groundTilemap.GetCellCenterWorld(Vector3Int.zero);

            Vector3Int[] cells = InvokeMethod<Vector3Int[]>(selfContainedDoor, "ResolveDoorCells", groundTilemap);

            Assert.That(cells, Has.Length.EqualTo(2), $"{prefabPath} should resolve a two-cell regular door footprint.");

            if (expectVerticalCells)
            {
                Assert.That(cells[0].x, Is.EqualTo(cells[1].x), $"{prefabPath} should keep the side-door vertical footprint.");
                Assert.That(cells[1], Is.EqualTo(cells[0] + Vector3Int.up), $"{prefabPath} should claim the anchor cell plus the cell above it.");
            }
            else
            {
                Assert.That(cells[0].y, Is.EqualTo(cells[1].y), $"{prefabPath} should keep the front-door horizontal footprint.");
                Assert.That(cells[1], Is.EqualTo(cells[0] + Vector3Int.right), $"{prefabPath} should claim the anchor cell plus the cell to the right.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [TestCase(FrontDoorPrefabPath)]
    [TestCase(SideDoorPrefabPath)]
    public void RegularDoorPrefab_UsesPrefabAuthoredVisualAndColliderValues(string prefabPath)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Component selfContainedDoor = prefabRoot.GetComponent(SelfContainedDoorTypeName);
            Assert.That(selfContainedDoor, Is.Not.Null, $"{prefabPath} is missing {SelfContainedDoorTypeName}.");

            SpriteRenderer closedRenderer = MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "closedRenderer") as SpriteRenderer;
            SpriteRenderer openRenderer = MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "openRenderer") as SpriteRenderer;
            Transform openVisual = MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "openVisual") as Transform;
            BoxCollider2D blockerCollider = MainEscapeReflectionTestHelper.GetFieldValue(selfContainedDoor, "blockerCollider") as BoxCollider2D;

            Assert.That(closedRenderer, Is.Not.Null, $"{prefabPath} should keep a closed renderer.");
            Assert.That(openRenderer, Is.Not.Null, $"{prefabPath} should keep an open renderer.");
            Assert.That(openVisual, Is.Not.Null, $"{prefabPath} should keep an open visual root.");
            Assert.That(blockerCollider, Is.Not.Null, $"{prefabPath} should keep a blocker collider.");

            Vector3 authoredOpenLocalPosition = new(0.37f, -0.21f, 0f);
            Vector3 authoredOpenLocalScale = new(1.43f, 0.82f, 1f);
            Vector2 authoredBlockerSize = new(0.78f, 1.62f);
            Vector2 authoredBlockerOffset = new(-0.16f, 0.29f);

            openVisual.localPosition = authoredOpenLocalPosition;
            openVisual.localScale = authoredOpenLocalScale;
            blockerCollider.size = authoredBlockerSize;
            blockerCollider.offset = authoredBlockerOffset;

            InvokeParameterlessMethod(selfContainedDoor, "OnValidate");

            Assert.That(
                InvokeMethod<bool>(selfContainedDoor, "SetOpenState", true),
                Is.True,
                $"{prefabPath} should transition into its open state during the prefab-authored value check.");

            Assert.That(openRenderer.color.a, Is.GreaterThan(0.99f), $"{prefabPath} should make the open visual visible when the door opens.");
            Assert.That(closedRenderer.sortingOrder, Is.GreaterThan(90), $"{prefabPath} closed visual should render above the fog overlay.");
            Assert.That(openRenderer.sortingOrder, Is.GreaterThan(90), $"{prefabPath} open visual should render above the fog overlay.");
            Assert.That(closedRenderer.maskInteraction, Is.EqualTo(SpriteMaskInteraction.None), $"{prefabPath} closed visual should not be clipped by the fog mask.");
            Assert.That(openRenderer.maskInteraction, Is.EqualTo(SpriteMaskInteraction.None), $"{prefabPath} open visual should not be clipped by the fog mask.");
            Assert.That(openVisual.localPosition, Is.EqualTo(authoredOpenLocalPosition), $"{prefabPath} should keep the prefab-authored open visual local position untouched.");
            Assert.That(openVisual.localScale, Is.EqualTo(authoredOpenLocalScale), $"{prefabPath} should keep the prefab-authored open visual local scale untouched.");
            Assert.That(blockerCollider.size, Is.EqualTo(authoredBlockerSize), $"{prefabPath} should keep the prefab-authored blocker collider size untouched.");
            Assert.That(blockerCollider.offset, Is.EqualTo(authoredBlockerOffset), $"{prefabPath} should keep the prefab-authored blocker collider offset untouched.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void RuntimeUpdate_RefreshesVisualStateOnlyWhileAnimationIsPending()
    {
        string source = File.ReadAllText(SelfContainedDoorSourcePath);

        Assert.That(source, Does.Contain("if (HasPendingVisualState())"));
        Assert.That(source, Does.Contain("private bool HasPendingVisualState()"));
    }

    [TestCase("Assets/Scenes/RMainEscape_tuto.unity", 2, 1, 1, false)]
    [TestCase("Assets/Scenes/RMainScene_1F.unity", 6, 3, 3, true)]
    [TestCase("Assets/Scenes/RMainScene_2F.unity", 11, 9, 2, true)]
    [TestCase("Assets/Scenes/RMainScene_3F.unity", 5, 4, 1, true)]
    [TestCase("Assets/Scenes/RMainScene_4F.unity", 11, 8, 3, true)]
    [TestCase("Assets/Scenes/RMainScene_5F.unity", 12, 8, 4, true)]
    public void SceneRegularDoors_ArePrefabizedSelfContainedAndRecoverIntoDoorGroups(
        string scenePath,
        int expectedTotal,
        int expectedFrontCount,
        int expectedSideCount,
        bool expectFloorAuthoring)
    {
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
            Is.Not.Null,
            $"Missing scene asset at '{scenePath}'.");

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        Assert.That(scene.IsValid(), Is.True, $"Could not open scene '{scenePath}'.");

        Component[] regularDoorOverrides = Object
            .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(transform => transform != null && transform.gameObject.scene == scene)
            .Select(transform => transform.GetComponent(DoorVariantOverrideTypeName))
            .Where(variantOverride =>
                variantOverride != null
                && IsRegularDoorVariant(ReadVariantName(variantOverride)))
            .OrderBy(variantOverride => BuildHierarchyPath(variantOverride.transform))
            .ToArray();
        Transform[] namedDoorRoots = Object
            .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(transform =>
                transform != null
                && transform.gameObject.scene == scene
                && IsNamedRegularDoorRoot(transform))
            .OrderBy(BuildHierarchyPath)
            .ToArray();

        Assert.That(
            regularDoorOverrides,
            Has.Length.EqualTo(expectedTotal),
            $"{scenePath} should keep the expected number of regular door roots after prefab replacement.");
        Assert.That(
            regularDoorOverrides.Count(variantOverride => string.Equals(ReadVariantName(variantOverride), FrontDoorVariantName, StringComparison.Ordinal)),
            Is.EqualTo(expectedFrontCount),
            $"{scenePath} should keep the expected front-door count.");
        Assert.That(
            regularDoorOverrides.Count(variantOverride => string.Equals(ReadVariantName(variantOverride), SideDoorVariantName, StringComparison.Ordinal)),
            Is.EqualTo(expectedSideCount),
            $"{scenePath} should keep the expected side-door count.");
        Assert.That(
            namedDoorRoots,
            Has.Length.EqualTo(expectedTotal),
            $"{scenePath} should not keep extra loose scene copies of the migrated regular door families.");

        Dictionary<string, Component> doorsBySignature = new(StringComparer.Ordinal);

        for (int index = 0; index < regularDoorOverrides.Length; index++)
        {
            Component variantOverride = regularDoorOverrides[index];
            Component selfContainedDoor = variantOverride.GetComponent(SelfContainedDoorTypeName);
            string hierarchyPath = BuildHierarchyPath(variantOverride.transform);
            string expectedPrefabPath = ExpectedPrefabPath(ReadVariantName(variantOverride));
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(variantOverride.gameObject);

            Assert.That(
                selfContainedDoor,
                Is.Not.Null,
                $"{hierarchyPath} must keep {SelfContainedDoorTypeName} on the same root as the visual variant override.");
            Assert.That(
                PrefabUtility.IsPartOfPrefabInstance(variantOverride.gameObject),
                Is.True,
                $"{hierarchyPath} must be an actual prefab instance, not a loose scene object.");
            Assert.That(
                prefabPath,
                Is.EqualTo(expectedPrefabPath),
                $"{hierarchyPath} should resolve to the canonical prefab asset for its regular door family.");

            doorsBySignature[BuildHierarchyPath(variantOverride.transform)] = selfContainedDoor;
        }

        for (int index = 0; index < namedDoorRoots.Length; index++)
        {
            Transform doorRoot = namedDoorRoots[index];
            string hierarchyPath = BuildHierarchyPath(doorRoot);

            Assert.That(
                doorRoot.GetComponent(SelfContainedDoorTypeName),
                Is.Not.Null,
                $"{hierarchyPath} must keep {SelfContainedDoorTypeName} after the scene replacement pass.");
            Assert.That(
                doorRoot.GetComponent(DoorVariantOverrideTypeName),
                Is.Not.Null,
                $"{hierarchyPath} must keep {DoorVariantOverrideTypeName} so the regular door family is explicit.");
            Assert.That(
                PrefabUtility.IsPartOfPrefabInstance(doorRoot.gameObject),
                Is.True,
                $"{hierarchyPath} must not remain as a loose authored scene object.");
        }

        if (!expectFloorAuthoring)
        {
            return;
        }

        Component floorAuthoring = FindSceneComponent(scene, FloorAuthoringTypeName);
        Assert.That(floorAuthoring, Is.Not.Null, $"{scenePath} should keep {FloorAuthoringTypeName}.");

        InvokeParameterlessMethod(floorAuthoring, "CacheReferencesFromHierarchy");
        Tilemap groundTilemap = MainEscapeReflectionTestHelper.GetPropertyValue<Tilemap>(floorAuthoring, "GroundTilemap");
        Array doorGroups = InvokeParameterlessArrayMethod(floorAuthoring, "BuildDoorGroups");

        Assert.That(groundTilemap, Is.Not.Null, $"{scenePath} should resolve a ground tilemap for regular door cell ownership.");
        Assert.That(doorGroups, Is.Not.Null, $"{scenePath} should build authored door groups.");

        HashSet<string> groupSignatures = new(
            doorGroups
                .Cast<object>()
                .Select(ReadDoorCells)
                .Where(cells => cells.Length > 0)
                .Select(BuildCellSignature),
            StringComparer.Ordinal);

        foreach (Component door in doorsBySignature.Values)
        {
            string hierarchyPath = BuildHierarchyPath(door.transform);
            string cellSignature = BuildCellSignature(InvokeMethod<Vector3Int[]>(door, "ResolveDoorCells", groundTilemap));

            Assert.That(
                groupSignatures.Contains(cellSignature),
                Is.True,
                $"{hierarchyPath} must feed its resolved cells into {FloorAuthoringTypeName}.BuildDoorGroups().");
        }
    }

    private static Tilemap CreateGroundTilemap(Transform parent)
    {
        GameObject gridObject = new("Grid");
        gridObject.transform.SetParent(parent, false);
        gridObject.AddComponent<Grid>();

        GameObject tilemapObject = new("Tiles_ground");
        tilemapObject.transform.SetParent(gridObject.transform, false);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();

        Tile groundTile = ScriptableObject.CreateInstance<Tile>();
        groundTile.hideFlags = HideFlags.HideAndDontSave;

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
        }

        return tilemap;
    }

    private static T InvokeMethod<T>(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target?.GetType().GetMethod(methodName, InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"{target?.GetType().Name}.{methodName}() is missing.");
        object value = method.Invoke(target, arguments);
        return value is T typedValue ? typedValue : default;
    }

    private static void InvokeParameterlessMethod(object target, string methodName)
    {
        InvokeMethod<object>(target, methodName);
    }

    private static Array InvokeParameterlessArrayMethod(object target, string methodName)
    {
        MethodInfo method = target?.GetType().GetMethod(methodName, InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"{target?.GetType().Name}.{methodName}() is missing.");
        object value = method.Invoke(target, null);
        Assert.That(value, Is.AssignableTo(typeof(Array)), $"{target?.GetType().Name}.{methodName}() should return {nameof(Array)}.");
        return value as Array;
    }

    private static Component FindSceneComponent(Scene scene, string componentTypeName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int index = 0; index < transforms.Length; index++)
        {
            Transform transform = transforms[index];

            if (transform == null || transform.gameObject.scene != scene)
            {
                continue;
            }

            Component component = transform.GetComponent(componentTypeName);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Vector3Int[] ReadDoorCells(object group)
    {
        object value = MainEscapeReflectionTestHelper.GetPropertyValue(group, "Cells");
        return value as Vector3Int[] ?? Array.Empty<Vector3Int>();
    }

    private static string ReadVariantName(Component variantOverride)
    {
        object value = MainEscapeReflectionTestHelper.GetPropertyValue(variantOverride, "VisualVariant");
        return value != null ? value.ToString() : string.Empty;
    }

    private static bool IsRegularDoorVariant(string variantName)
    {
        return string.Equals(variantName, FrontDoorVariantName, StringComparison.Ordinal)
            || string.Equals(variantName, SideDoorVariantName, StringComparison.Ordinal);
    }

    private static bool IsNamedRegularDoorRoot(Transform transform)
    {
        if (transform == null)
        {
            return false;
        }

        string name = transform.name ?? string.Empty;
        return name.StartsWith("VexedTileBProp_01_Top", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("CustomSideDoorClosed", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpectedPrefabPath(string variantName)
    {
        return string.Equals(variantName, SideDoorVariantName, StringComparison.Ordinal)
            ? SideDoorPrefabPath
            : FrontDoorPrefabPath;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return "<missing transform>";
        }

        Stack<string> segments = new();
        Transform current = transform;

        while (current != null)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", segments);
    }

    private static string BuildCellSignature(IReadOnlyList<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return string.Empty;
        }

        Vector3Int[] sortedCells = new Vector3Int[cells.Count];

        for (int index = 0; index < cells.Count; index++)
        {
            sortedCells[index] = cells[index];
        }

        Array.Sort(sortedCells, CompareCells);
        return string.Join("|", sortedCells.Select(cell => $"{cell.x},{cell.y},{cell.z}"));
    }

    private static int CompareCells(Vector3Int left, Vector3Int right)
    {
        int xCompare = left.x.CompareTo(right.x);
        if (xCompare != 0)
        {
            return xCompare;
        }

        int yCompare = left.y.CompareTo(right.y);
        if (yCompare != 0)
        {
            return yCompare;
        }

        return left.z.CompareTo(right.z);
    }
}

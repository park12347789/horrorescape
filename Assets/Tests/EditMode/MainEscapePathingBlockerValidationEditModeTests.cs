using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MainEscapePathingBlockerValidationEditModeTests
{
    private const float VisibleAlphaFloor = 0.08f;
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string GridPathfinderSourcePath = "Assets/Scripts/Enemy/GridPathfinder.cs";

    private static readonly Vector3Int[] CardinalNeighborOffsets =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0)
    };

    [Test]
    public void GridPathfinder_UsesOpenSetLookupWithoutChangingOrderedOpenSet()
    {
        string source = File.ReadAllText(GridPathfinderSourcePath);

        Assert.That(source, Does.Contain("private static HashSet<Vector3Int> openSetLookup"));
        Assert.That(source, Does.Contain("openSetLookup.Add(startCell)"));
        Assert.That(source, Does.Contain("openSetLookup.Remove(current)"));
        Assert.That(source, Does.Contain("if (openSetLookup.Add(neighbor))"));
        Assert.That(source, Does.Contain("Vector3Int current = GetLowestScoreNode(openSet, fScore);"));
        Assert.That(source, Does.Not.Contain("openSet.Contains(neighbor)"));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void AuthoredFloorPrefab_DynamicBlockers_DoNotSealEnemyToPlayerRoute(int floorNumber)
    {
        string prefabPath = $"Assets/Resources/Floors/MainEscape/{floorNumber}F.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefabAsset, Is.Not.Null, $"Missing authored floor prefab at '{prefabPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Type floorAuthoringType = FindTypeByName("MainEscapeFloorAuthoring");
            Type gridMapServiceType = FindTypeByName("GridMapService");
            Assert.That(floorAuthoringType, Is.Not.Null, "MainEscapeFloorAuthoring type is missing.");
            Assert.That(gridMapServiceType, Is.Not.Null, "GridMapService type is missing.");

            Component floorAuthoring = prefabRoot.GetComponent(floorAuthoringType);
            Component mapService = prefabRoot.GetComponent(gridMapServiceType);

            Assert.That(floorAuthoring, Is.Not.Null, $"{floorNumber}F is missing MainEscapeFloorAuthoring.");
            Assert.That(mapService, Is.Not.Null, $"{floorNumber}F is missing GridMapService.");
            Assert.That(InvokeBoolMethod(floorAuthoring, "HasRequiredTilemaps"), Is.True, $"{floorNumber}F tilemap references are incomplete.");

            Grid grid = GetPropertyValue<Grid>(floorAuthoring, "Grid");
            Tilemap groundTilemap = GetPropertyValue<Tilemap>(floorAuthoring, "GroundTilemap");
            Tilemap wallTilemap = GetPropertyValue<Tilemap>(floorAuthoring, "WallTilemap");
            Tilemap doorTilemap = GetPropertyValue<Tilemap>(floorAuthoring, "DoorTilemap");

            LayerMask visionBlockingMask = ResolveVisionBlockingMask();

            InvokeMethod(
                mapService,
                "Initialize",
                grid,
                groundTilemap,
                wallTilemap,
                doorTilemap,
                visionBlockingMask);
            InvokeMethod(mapService, "ClearRegisteredProps");
            InvokeMethod(floorAuthoring, "RegisterPropBlockers", mapService);

            Vector3Int playerCell = default;
            Vector3Int stalkerCell = default;

            if (!TryInvokeVector3IntOutMethod(floorAuthoring, "TryResolvePlayerStartCell", out playerCell)
                || !TryInvokeVector3IntOutMethod(floorAuthoring, "TryResolveStalkerSpawnCell", out stalkerCell))
            {
                Assert.Inconclusive($"{floorNumber}F is missing PlayerStart/StalkerSpawn marker cell mapping.");
            }

            List<Vector3Int> baselinePath = new();
            List<Vector3Int> runtimePath = new();
            bool hasBaselinePath = TryBuildPathIgnoringDynamicBlockers(
                groundTilemap,
                wallTilemap,
                doorTilemap,
                stalkerCell,
                playerCell,
                baselinePath,
                allowClosedDoors: true);
            bool hasRuntimePath = TryInvokeGridPathfinder(mapService, stalkerCell, playerCell, runtimePath, allowClosedDoors: true);

            if (hasBaselinePath && !hasRuntimePath)
            {
                string suspects = DescribeOverlappingBlockers(floorAuthoring, groundTilemap, baselinePath);
                Assert.Fail(
                    $"{floorNumber}F dynamic blockers seal a route needed for enemy pursuit. " +
                    $"Baseline tile route exists but runtime route does not. {suspects}");
            }

            Assert.That(true, Is.True);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void AuthoredFloorPrefab_MovementBlockers_WithSolidCollider_AreVisuallyDiscernible(int floorNumber)
    {
        string prefabPath = $"Assets/Resources/Floors/MainEscape/{floorNumber}F.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefabAsset, Is.Not.Null, $"Missing authored floor prefab at '{prefabPath}'.");

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Type movementBlockerType = FindTypeByName("MainEscapeMovementBlockerAuthoring");
            Assert.That(movementBlockerType, Is.Not.Null, "MainEscapeMovementBlockerAuthoring type is missing.");

            Component[] movementBlockers = prefabRoot.GetComponentsInChildren(movementBlockerType, true);

            for (int index = 0; index < movementBlockers.Length; index++)
            {
                Component blocker = movementBlockers[index];

                if (blocker == null)
                {
                    continue;
                }

                Collider2D collider = blocker.GetComponent<Collider2D>();

                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                Assert.That(
                    IsGameObjectVisuallyHidden(blocker.gameObject),
                    Is.False,
                    $"{floorNumber}F movement blocker '{blocker.name}' has active collision but is visually hidden.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool TryBuildPathIgnoringDynamicBlockers(
        Tilemap groundTilemap,
        Tilemap wallTilemap,
        Tilemap doorTilemap,
        Vector3Int startCell,
        Vector3Int goalCell,
        List<Vector3Int> result,
        bool allowClosedDoors)
    {
        result?.Clear();

        if (groundTilemap == null || result == null)
        {
            return false;
        }

        if (startCell == goalCell)
        {
            return true;
        }

        if (!IsBaselineWalkable(groundTilemap, wallTilemap, doorTilemap, goalCell, allowClosedDoors))
        {
            return false;
        }

        Queue<Vector3Int> frontier = new();
        HashSet<Vector3Int> visited = new();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new();

        frontier.Enqueue(startCell);
        visited.Add(startCell);

        int safetyCounter = 0;

        while (frontier.Count > 0 && safetyCounter < 4096)
        {
            safetyCounter++;
            Vector3Int current = frontier.Dequeue();

            if (current == goalCell)
            {
                ReconstructPath(cameFrom, current, result);
                return true;
            }

            for (int index = 0; index < CardinalNeighborOffsets.Length; index++)
            {
                Vector3Int neighbor = current + CardinalNeighborOffsets[index];

                if (visited.Contains(neighbor) || !IsBaselineWalkable(groundTilemap, wallTilemap, doorTilemap, neighbor, allowClosedDoors))
                {
                    continue;
                }

                visited.Add(neighbor);
                cameFrom[neighbor] = current;
                frontier.Enqueue(neighbor);
            }
        }

        result.Clear();
        return false;
    }

    private static bool IsBaselineWalkable(
        Tilemap groundTilemap,
        Tilemap wallTilemap,
        Tilemap doorTilemap,
        Vector3Int cell,
        bool allowClosedDoors)
    {
        bool hasGround = groundTilemap != null && groundTilemap.HasTile(cell);
        bool blockedByWall = wallTilemap != null && wallTilemap.HasTile(cell);
        bool blockedByDoor = !allowClosedDoors && doorTilemap != null && doorTilemap.HasTile(cell);
        return hasGround && !blockedByWall && !blockedByDoor;
    }

    private static void ReconstructPath(
        IReadOnlyDictionary<Vector3Int, Vector3Int> cameFrom,
        Vector3Int current,
        List<Vector3Int> result)
    {
        result.Clear();
        result.Add(current);

        while (cameFrom.TryGetValue(current, out Vector3Int previous))
        {
            current = previous;
            result.Add(current);
        }

        result.Reverse();

        if (result.Count > 0)
        {
            result.RemoveAt(0);
        }
    }

    private static string DescribeOverlappingBlockers(
        Component floorAuthoring,
        Tilemap groundTilemap,
        IReadOnlyCollection<Vector3Int> baselinePath)
    {
        if (floorAuthoring == null || groundTilemap == null || baselinePath == null || baselinePath.Count == 0)
        {
            return "No blocker overlap context available.";
        }

        Type movementBlockerType = FindTypeByName("MainEscapeMovementBlockerAuthoring");
        Type propBlockerType = FindTypeByName("MainEscapePropBlockerAuthoring");

        if (movementBlockerType == null || propBlockerType == null)
        {
            return "Blocker authoring types are missing.";
        }

        HashSet<Vector3Int> pathCells = baselinePath as HashSet<Vector3Int> ?? new HashSet<Vector3Int>(baselinePath);
        List<string> suspects = new();

        Component[] movementBlockers = floorAuthoring.GetComponentsInChildren(movementBlockerType, true);

        for (int index = 0; index < movementBlockers.Length; index++)
        {
            Component blocker = movementBlockers[index];

            if (blocker == null)
            {
                continue;
            }

            if (!InvokeOccupiedCells(blocker, groundTilemap).Any(pathCells.Contains))
            {
                continue;
            }

            bool hidden = IsGameObjectVisuallyHidden(blocker.gameObject);
            suspects.Add(hidden ? $"{blocker.name} (movement, hidden)" : $"{blocker.name} (movement)");
        }

        Component[] propBlockers = floorAuthoring.GetComponentsInChildren(propBlockerType, true);

        for (int index = 0; index < propBlockers.Length; index++)
        {
            Component blocker = propBlockers[index];

            if (blocker == null)
            {
                continue;
            }

            if (!InvokeOccupiedCells(blocker, groundTilemap).Any(pathCells.Contains))
            {
                continue;
            }

            bool hidden = IsGameObjectVisuallyHidden(blocker.gameObject);
            suspects.Add(hidden ? $"{blocker.name} (prop, hidden)" : $"{blocker.name} (prop)");
        }

        if (suspects.Count == 0)
        {
            return "No authored blocker overlapped the baseline route.";
        }

        return "Overlapping blockers: " + string.Join(", ", suspects.Take(8));
    }

    private static bool IsGameObjectVisuallyHidden(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
        {
            return true;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (renderer is SpriteRenderer spriteRenderer && spriteRenderer.color.a <= VisibleAlphaFloor)
            {
                continue;
            }

            if (renderer is TilemapRenderer tilemapRenderer)
            {
                Tilemap tilemap = tilemapRenderer.GetComponent<Tilemap>();

                if (tilemap != null && tilemap.color.a <= VisibleAlphaFloor)
                {
                    continue;
                }
            }

            return false;
        }

        return true;
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

    private static T GetPropertyValue<T>(object instance, string propertyName) where T : class
    {
        if (instance == null)
        {
            return null;
        }

        PropertyInfo property = instance.GetType().GetProperty(propertyName, MemberFlags);
        return property?.GetValue(instance) as T;
    }

    private static bool InvokeBoolMethod(object instance, string methodName)
    {
        MethodInfo method = instance?.GetType().GetMethod(methodName, MemberFlags);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        return method != null && method.Invoke(instance, null) is bool result && result;
    }

    private static void InvokeMethod(object instance, string methodName, params object[] arguments)
    {
        MethodInfo method = instance?.GetType().GetMethod(methodName, MemberFlags);
        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        method?.Invoke(instance, arguments);
    }

    private static bool TryInvokeVector3IntOutMethod(object instance, string methodName, out Vector3Int result)
    {
        result = default;
        MethodInfo method = instance?.GetType().GetMethod(methodName, MemberFlags);

        if (method == null)
        {
            return false;
        }

        object[] arguments = { null };
        bool success = method.Invoke(instance, arguments) is bool boolResult && boolResult;

        if (success && arguments[0] is Vector3Int cell)
        {
            result = cell;
        }

        return success;
    }

    private static Vector3Int[] InvokeOccupiedCells(Component blocker, Tilemap groundTilemap)
    {
        MethodInfo method = blocker?.GetType().GetMethod("GetOccupiedCells", MemberFlags);

        if (method == null || method.Invoke(blocker, new object[] { groundTilemap }) is not Vector3Int[] occupiedCells)
        {
            return Array.Empty<Vector3Int>();
        }

        return occupiedCells;
    }

    private static LayerMask ResolveVisionBlockingMask()
    {
        Type gameLayersType = FindTypeByName("GameLayers");

        if (gameLayersType == null)
        {
            return default;
        }

        PropertyInfo property = gameLayersType.GetProperty("VisionBlockingMask", StaticMemberFlags);

        if (property?.GetValue(null) is LayerMask propertyValue)
        {
            return propertyValue;
        }

        FieldInfo field = gameLayersType.GetField("VisionBlockingMask", StaticMemberFlags);
        return field?.GetValue(null) is LayerMask fieldValue ? fieldValue : default;
    }

    private static bool TryInvokeGridPathfinder(object mapService, Vector3Int startCell, Vector3Int goalCell, List<Vector3Int> result, bool allowClosedDoors)
    {
        Type gridPathfinderType = FindTypeByName("GridPathfinder");

        if (gridPathfinderType == null)
        {
            return false;
        }

        MethodInfo method = gridPathfinderType.GetMethod("TryBuildPath", StaticMemberFlags);

        if (method == null)
        {
            return false;
        }

        object[] arguments =
        {
            mapService,
            startCell,
            goalCell,
            result,
            allowClosedDoors,
            null
        };

        return method.Invoke(null, arguments) is bool success && success;
    }
}

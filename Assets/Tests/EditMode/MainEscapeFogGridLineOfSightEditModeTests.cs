using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MainEscapeFogGridLineOfSightEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private GameObject root;
    private Grid grid;
    private Tilemap groundTilemap;
    private Tilemap wallTilemap;
    private Tilemap doorTilemap;
    private Component mapService;
    private Type mapServiceType;
    private Tile testTile;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("FogGridRoot");
        grid = root.AddComponent<Grid>();
        groundTilemap = CreateTilemap("Ground", LayerMask.NameToLayer("Ground"));
        wallTilemap = CreateTilemap("Wall", LayerMask.NameToLayer("Wall"));
        doorTilemap = CreateTilemap("Door", LayerMask.NameToLayer("Door"));
        mapServiceType = FindTypeByName("GridMapService");
        mapService = root.AddComponent(mapServiceType);
        testTile = ScriptableObject.CreateInstance<Tile>();

        for (int x = 0; x < 4; x++)
        {
            groundTilemap.SetTile(new Vector3Int(x, 0, 0), testTile);
        }

        LayerMask blockingMask = LayerMask.GetMask("Wall", "Door", "Prop");

        InvokeMethod(
            mapService,
            "Initialize",
            grid,
            groundTilemap,
            wallTilemap,
            doorTilemap,
            blockingMask);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        if (testTile != null)
        {
            UnityEngine.Object.DestroyImmediate(testTile);
        }
    }

    [Test]
    public void HasLineOfSight_ReturnsFalse_WhenWallTileBlocksSegment()
    {
        wallTilemap.SetTile(new Vector3Int(1, 0, 0), testTile);

        bool hasLineOfSight = InvokeBool(
            mapService,
            "HasLineOfSight",
            CellCenter(0),
            CellCenter(3),
            false);

        Assert.That(hasLineOfSight, Is.False);
    }

    [Test]
    public void HasLineOfSight_CanIgnoreClosedDoorCells()
    {
        doorTilemap.SetTile(new Vector3Int(1, 0, 0), testTile);

        bool blockedByDoor = InvokeBool(
            mapService,
            "HasLineOfSight",
            CellCenter(0),
            CellCenter(3),
            false);
        bool ignoresDoor = InvokeBool(
            mapService,
            "HasLineOfSight",
            CellCenter(0),
            CellCenter(3),
            true);

        Assert.That(blockedByDoor, Is.False);
        Assert.That(ignoresDoor, Is.True);
    }

    [Test]
    public void HasLineOfSight_ReturnsFalse_WhenSegmentCutsBlockedCorner()
    {
        groundTilemap.SetTile(new Vector3Int(1, 1, 0), testTile);
        wallTilemap.SetTile(new Vector3Int(1, 0, 0), testTile);

        bool hasLineOfSight = InvokeBool(
            mapService,
            "HasLineOfSight",
            CellCenter(2, 0),
            CellCenter(1, 1),
            false);

        Assert.That(hasLineOfSight, Is.False);
    }

    [Test]
    public void BlocksVision_IncludesRegisteredPropCells()
    {
        Vector3Int blockedCell = new(1, 0, 0);
        InvokeMethod(mapService, "RegisterPropCell", blockedCell);

        Assert.That(InvokeBool(mapService, "BlocksVision", blockedCell), Is.True);
        Assert.That(InvokeBool(mapService, "BlocksVision", blockedCell, true), Is.True);
    }

    [Test]
    public void RegisterPropBlockers_SolidBlocker_BlocksVisionAndMovement()
    {
        Vector3Int blockedCell = new(1, 0, 0);
        Component floorAuthoring = CreateFloorAuthoringWithPropBlocker(blockedCell, includeSolidBlocker: true);

        InvokeMethod(floorAuthoring, "RegisterPropBlockers", mapService);

        Assert.That(InvokeBool(mapService, "BlocksVision", blockedCell), Is.True);
        Assert.That(InvokeBool(mapService, "IsWalkable", blockedCell), Is.False);
    }

    [Test]
    public void RegisterPropBlockers_PlainPropBlocker_LeavesVisionOpenButBlocksMovement()
    {
        Vector3Int blockedCell = new(2, 0, 0);
        Component floorAuthoring = CreateFloorAuthoringWithPropBlocker(blockedCell, includeSolidBlocker: false);

        InvokeMethod(floorAuthoring, "RegisterPropBlockers", mapService);

        Assert.That(InvokeBool(mapService, "BlocksVision", blockedCell), Is.False);
        Assert.That(InvokeBool(mapService, "IsWalkable", blockedCell), Is.False);
    }

    [Test]
    public void ResolveFlashlightVisionOrigin_ClampsBackToPlayerSide_WhenWallBlocksForwardOffset()
    {
        CreateWallBlocker(new Vector2(0.55f, 0f), new Vector2(0.08f, 1f));

        GameObject overlayObject = new("FogOverlay_OriginClamp");
        overlayObject.transform.SetParent(root.transform, false);
        overlayObject.AddComponent<SpriteRenderer>();
        Component overlay = overlayObject.AddComponent(FindTypeByName("FlashlightFogOfWarOverlay"));

        SetField(overlay, "gridMapService", mapService);

        object resolved = InvokeMethod(
            overlay,
            "ResolveFlashlightVisionOrigin",
            CellCenter(0),
            CellCenter(0) + new Vector2(1.1f, 0f),
            LayerMask.GetMask("Wall"));

        Assert.That(resolved, Is.TypeOf<Vector2>());
        Assert.That(((Vector2)resolved).x, Is.LessThan(0.55f));
        Assert.That(((Vector2)resolved).x, Is.LessThan(CellCenter(0).x + 1.1f));
        UnityEngine.Object.DestroyImmediate(overlayObject);
    }

    private GameObject CreateWallBlocker(Vector2 position, Vector2 size)
    {
        GameObject wall = new("WallBlocker");
        wall.transform.SetParent(root.transform, false);
        wall.layer = LayerMask.NameToLayer("Wall");
        wall.transform.position = position;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        return wall;
    }

    private Tilemap CreateTilemap(string name, int layer)
    {
        GameObject tilemapObject = new(name);
        tilemapObject.transform.SetParent(root.transform, false);
        tilemapObject.layer = layer;
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        return tilemap;
    }

    private Vector2 CellCenter(int x, int y = 0)
    {
        return groundTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
    }

    private Component CreateFloorAuthoringWithPropBlocker(Vector3Int blockedCell, bool includeSolidBlocker)
    {
        GameObject floorRoot = new("FloorAuthoringRoot");
        floorRoot.transform.SetParent(root.transform, false);
        Component floorAuthoring = floorRoot.AddComponent(FindTypeByName("MainEscapeFloorAuthoring"));
        InvokeMethod(floorAuthoring, "ConfigureTilemaps", grid, groundTilemap, wallTilemap, doorTilemap);

        GameObject blockerObject = new(includeSolidBlocker ? "SolidBlocker" : "PropBlocker");
        blockerObject.transform.SetParent(floorRoot.transform, false);
        blockerObject.transform.position = groundTilemap.GetCellCenterWorld(blockedCell);
        Component propBlocker = blockerObject.AddComponent(FindTypeByName("MainEscapePropBlockerAuthoring"));
        InvokeMethod(propBlocker, "Configure", Vector2Int.one);

        if (includeSolidBlocker)
        {
            blockerObject.AddComponent(FindTypeByName("MainEscapeSolidBlockerAuthoring"));
        }

        Type doorAuthoringType = FindTypeByName("MainEscapeDoorAuthoring");
        Array emptyDoorAuthorings = Array.CreateInstance(doorAuthoringType, 0);
        Type propBlockerType = FindTypeByName("MainEscapePropBlockerAuthoring");
        Array propBlockers = Array.CreateInstance(propBlockerType, 1);
        propBlockers.SetValue(propBlocker, 0);

        InvokeMethod(
            floorAuthoring,
            "ConfigureHelpers",
            null,
            null,
            null,
            emptyDoorAuthorings,
            propBlockers);

        return floorAuthoring;
    }

    private static Type FindTypeByName(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type foundType = assembly.GetType(typeName) ?? Array.Find(assembly.GetTypes(), candidate => candidate.Name == typeName);

            if (foundType != null)
            {
                return foundType;
            }
        }

        Assert.Fail($"Unable to resolve type '{typeName}'.");
        return null;
    }

    private static object InvokeMethod(object target, string methodName, params object[] args)
    {
        MethodInfo[] methods = target.GetType().GetMethods(InstanceFlags);
        MethodInfo method = Array.Find(methods, candidate =>
        {
            if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
            {
                return false;
            }

            ParameterInfo[] parameters = candidate.GetParameters();

            if (parameters.Length != args.Length)
            {
                return false;
            }

            for (int index = 0; index < parameters.Length; index++)
            {
                object argument = args[index];

                if (argument == null)
                {
                    continue;
                }

                if (!parameters[index].ParameterType.IsInstanceOfType(argument)
                    && parameters[index].ParameterType != argument.GetType())
                {
                    return false;
                }
            }

            return true;
        });

        Assert.That(method, Is.Not.Null, $"Missing method '{methodName}'.");
        return method.Invoke(target, args);
    }

    private static bool InvokeBool(object target, string methodName, params object[] args)
    {
        object result = InvokeMethod(target, methodName, args);
        Assert.That(result, Is.TypeOf<bool>());
        return (bool)result;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field.SetValue(target, value);
    }
}

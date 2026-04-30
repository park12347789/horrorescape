using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class MainEscapeVisualDoorFootprintEditModeTests
{
    private const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void BuildVisualDoorGroups_SideDoorUsesMeasuredTallFootprint()
    {
        GameObject floorRoot = CreateFloorRoot();

        try
        {
            Tilemap groundTilemap = CreateGroundTilemap(floorRoot.transform);
            CreateDoorVisual(
                floorRoot.transform,
                "Visual/Props/sidedoor",
                "CustomSideDoorClosed",
                groundTilemap.GetCellCenterWorld(Vector3Int.zero),
                new Vector2(0.625f, 2f));

            Array groups = BuildVisualDoorGroups(floorRoot.transform, groundTilemap);
            Assert.That(groups, Has.Length.EqualTo(1));
            Vector3Int[] cells = ReadDoorCells(groups.GetValue(0));
            Assert.That(cells, Has.Length.EqualTo(2), "Tall side doors should follow their measured vertical footprint.");
            Assert.That(cells[0].x, Is.EqualTo(cells[1].x));
            Assert.That(cells[0], Is.EqualTo(Vector3Int.zero));
            Assert.That(cells[1], Is.EqualTo(Vector3Int.up));
        }
        finally
        {
            Object.DestroyImmediate(floorRoot);
        }
    }

    [Test]
    public void BuildVisualDoorGroups_FrontDoorKeepsTwoCellFootprint()
    {
        GameObject floorRoot = CreateFloorRoot();

        try
        {
            Tilemap groundTilemap = CreateGroundTilemap(floorRoot.transform);
            CreateDoorVisual(
                floorRoot.transform,
                "Visual/Props/Doors",
                "VexedTileBProp_01_Top",
                groundTilemap.GetCellCenterWorld(Vector3Int.zero),
                new Vector2(0.8f, 0.8f));

            Array groups = BuildVisualDoorGroups(floorRoot.transform, groundTilemap);
            Assert.That(groups, Has.Length.EqualTo(1));
            Vector3Int[] cells = ReadDoorCells(groups.GetValue(0));
            Assert.That(cells, Has.Length.EqualTo(2), "Front doors should keep their authored double-width footprint.");
            Assert.That(cells[0].y, Is.EqualTo(cells[1].y));
        }
        finally
        {
            Object.DestroyImmediate(floorRoot);
        }
    }

    [Test]
    public void BuildVisualDoorGroups_SideDoorOverrideWinsOverFrontDoorHeuristic()
    {
        GameObject floorRoot = CreateFloorRoot();

        try
        {
            Tilemap groundTilemap = CreateGroundTilemap(floorRoot.transform);
            GameObject doorObject = CreateDoorVisual(
                floorRoot.transform,
                "Visual/Props/Doors",
                "VexedTileBProp_01_Top",
                groundTilemap.GetCellCenterWorld(Vector3Int.zero),
                new Vector2(0.625f, 2f));
            AttachDoorVariantOverride(doorObject, "SideDoor42");

            Array groups = BuildVisualDoorGroups(floorRoot.transform, groundTilemap);
            Assert.That(groups, Has.Length.EqualTo(1));
            Vector3Int[] cells = ReadDoorCells(groups.GetValue(0));
            Assert.That(cells, Has.Length.EqualTo(2), "A side-door override should keep the measured tall footprint even when the legacy name looks like a front door.");
            Assert.That(cells[0].x, Is.EqualTo(cells[1].x));
            Assert.That(cells[0], Is.EqualTo(Vector3Int.zero));
            Assert.That(cells[1], Is.EqualTo(Vector3Int.up));
        }
        finally
        {
            Object.DestroyImmediate(floorRoot);
        }
    }

    private static GameObject CreateFloorRoot()
    {
        return new GameObject("FloorRoot");
    }

    private static Tilemap CreateGroundTilemap(Transform floorRoot)
    {
        GameObject gridObject = new("Grid");
        gridObject.transform.SetParent(floorRoot, false);
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

    private static GameObject CreateDoorVisual(
        Transform floorRoot,
        string parentPath,
        string objectName,
        Vector3 worldPosition,
        Vector2 visualSize)
    {
        Transform parent = EnsurePath(floorRoot, parentPath);
        GameObject doorObject = new(objectName);
        doorObject.transform.SetParent(parent, false);
        doorObject.transform.position = worldPosition;
        doorObject.transform.localScale = new Vector3(visualSize.x, visualSize.y, 1f);

        SpriteRenderer renderer = doorObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateUnitSprite();
        return doorObject;
    }

    private static void AttachDoorVariantOverride(GameObject target, string variantName)
    {
        Type overrideType = FindTypeByName("MainEscapeDoorVisualVariantOverride");
        Assert.That(overrideType, Is.Not.Null, "MainEscapeDoorVisualVariantOverride type is missing.");

        Component overrideComponent = target.AddComponent(overrideType);
        MethodInfo configureMethod = overrideType.GetMethod("Configure", InstanceMemberFlags);
        Assert.That(configureMethod, Is.Not.Null, "MainEscapeDoorVisualVariantOverride.Configure() is missing.");

        Type enumType = FindTypeByName("MainEscapeDoorVisualVariantKind");
        Assert.That(enumType, Is.Not.Null, "MainEscapeDoorVisualVariantKind type is missing.");

        object enumValue = Enum.Parse(enumType, variantName, ignoreCase: false);
        configureMethod.Invoke(overrideComponent, new[] { enumValue });
    }

    private static Transform EnsurePath(Transform root, string path)
    {
        string[] segments = path.Split('/');
        Transform current = root;

        for (int index = 0; index < segments.Length; index++)
        {
            Transform next = current.Find(segments[index]);

            if (next == null)
            {
                GameObject child = new(segments[index]);
                child.transform.SetParent(current, false);
                next = child.transform;
            }

            current = next;
        }

        return current;
    }

    private static Sprite CreateUnitSprite()
    {
        Texture2D texture = new(32, 32, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[32 * 32];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
    }

    private static Array BuildVisualDoorGroups(Transform floorRoot, Tilemap groundTilemap)
    {
        Type synthesisType = FindTypeByName("MainEscapeVisualAuthoringSynthesis");
        Assert.That(synthesisType, Is.Not.Null, "MainEscapeVisualAuthoringSynthesis type is missing.");

        MethodInfo buildMethod = synthesisType.GetMethod("BuildVisualDoorGroups", StaticMemberFlags);
        Assert.That(buildMethod, Is.Not.Null, "MainEscapeVisualAuthoringSynthesis.BuildVisualDoorGroups() is missing.");

        Array groups = buildMethod.Invoke(null, new object[] { floorRoot, groundTilemap }) as Array;
        Assert.That(groups, Is.Not.Null, "BuildVisualDoorGroups should return an array.");
        return groups;
    }

    private static Vector3Int[] ReadDoorCells(object group)
    {
        Assert.That(group, Is.Not.Null, "Generated door group should not be null.");

        PropertyInfo property = group.GetType().GetProperty("Cells", InstanceMemberFlags);
        Assert.That(property, Is.Not.Null, $"{group.GetType().Name}.Cells is missing.");

        Vector3Int[] cells = property.GetValue(group) as Vector3Int[];
        Assert.That(cells, Is.Not.Null, $"{group.GetType().Name}.Cells should resolve to Vector3Int[].");
        return cells;
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

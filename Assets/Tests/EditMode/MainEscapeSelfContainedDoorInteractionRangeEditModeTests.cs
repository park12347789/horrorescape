using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;
using UnityEngine.Tilemaps;

using UnityObject = UnityEngine.Object;

public sealed class MainEscapeSelfContainedDoorInteractionRangeEditModeTests
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase(-1.25f, 0f, "left")]
    [TestCase(1.25f, 0f, "right")]
    [TestCase(0f, 1.35f, "top")]
    [TestCase(0f, -1.35f, "bottom")]
    [TestCase(1.2f, 1.2f, "diagonal")]
    public void SideDoorInteraction_AllowsNearbyPositionsAroundDoor(float playerX, float playerY, string direction)
    {
        using DoorInteractionFixture fixture = new();
        fixture.PlayerRoot.transform.position = new Vector3(playerX, playerY, 0f);

        float distance = Invoke<float>(
            fixture.Door,
            "GetInteractionDistance",
            new[] { fixture.Player.GetType() },
            fixture.Player);

        bool canInteract = Invoke<bool>(
            fixture.Door,
            "CanInteractAtDistance",
            new[] { fixture.Player.GetType(), typeof(float) },
            fixture.Player,
            distance);

        Assert.That(canInteract, Is.True, $"Side doors should stay generously interactable from the {direction} side.");
    }

    [TestCase(-1.25f, -0.53f, "left")]
    [TestCase(1.25f, 0.53f, "right")]
    public void SideDoorLineOfSight_UsesPlayerSideApproachPoint(float playerX, float expectedX, string side)
    {
        using DoorInteractionFixture fixture = new();
        fixture.PlayerRoot.transform.position = new Vector3(playerX, 0.25f, 0f);

        Vector2 lineOfSightPoint = Invoke<Vector2>(
            fixture.Door,
            "GetInteractionLineOfSightPoint",
            new[] { fixture.Player.GetType() },
            fixture.Player);

        Assert.That(lineOfSightPoint.x, Is.EqualTo(expectedX).Within(0.001f), $"Side door LOS should target the {side} approach point.");
        Assert.That(lineOfSightPoint.y, Is.EqualTo(0.25f).Within(0.001f), $"Side door LOS should preserve the player's vertical approach on the {side} side.");
    }

    [Test]
    public void SideDoorInteraction_RejectsOnlyWhenOutsideInteractionDistance()
    {
        using DoorInteractionFixture fixture = new();
        fixture.PlayerRoot.transform.position = new Vector3(0f, 4f, 0f);

        float distance = Invoke<float>(
            fixture.Door,
            "GetInteractionDistance",
            new[] { fixture.Player.GetType() },
            fixture.Player);

        Assert.That(distance, Is.GreaterThan(2.1f), "The far position should be outside the generous side-door range.");

        bool canInteract = Invoke<bool>(
            fixture.Door,
            "CanInteractAtDistance",
            new[] { fixture.Player.GetType(), typeof(float) },
            fixture.Player,
            distance);

        Assert.That(canInteract, Is.False, "Side doors should only reject interaction once the player is outside range.");
    }

    [Test]
    public void SideDoorLineOfSight_AllowsDoorTileBlockersOnResolvedDoorCells()
    {
        using DoorInteractionFixture fixture = new();
        fixture.ConfigureDoorCells(new[] { Vector3Int.zero, Vector3Int.up });
        BoxCollider2D doorTileBlocker = fixture.CreateDoorTileBlocker("ResolvedDoorTileBlocker", new Vector3Int(0, 0, 0));

        bool allowsBlocker = Invoke<bool>(
            fixture.Door,
            "AllowsLineOfSightBlocker",
            new[] { typeof(Collider2D), typeof(Vector2), fixture.Player.GetType() },
            doorTileBlocker,
            fixture.CellCenter(new Vector3Int(0, 0, 0)),
            fixture.Player);

        Assert.That(allowsBlocker, Is.True, "Side doors should allow their own door tile collider during line-of-sight checks.");
    }

    [Test]
    public void SideDoorLineOfSight_RejectsDoorTileBlockersOutsideResolvedDoorCells()
    {
        using DoorInteractionFixture fixture = new();
        fixture.ConfigureDoorCells(new[] { Vector3Int.zero, Vector3Int.up });
        BoxCollider2D unrelatedDoorTileBlocker = fixture.CreateDoorTileBlocker("UnrelatedDoorTileBlocker", new Vector3Int(3, 0, 0));

        bool allowsBlocker = Invoke<bool>(
            fixture.Door,
            "AllowsLineOfSightBlocker",
            new[] { typeof(Collider2D), typeof(Vector2), fixture.Player.GetType() },
            unrelatedDoorTileBlocker,
            fixture.CellCenter(new Vector3Int(3, 0, 0)),
            fixture.Player);

        Assert.That(allowsBlocker, Is.False, "Side doors must not ignore unrelated door colliders outside their resolved cells.");
    }

    private sealed class DoorInteractionFixture : IDisposable
    {
        public DoorInteractionFixture()
        {
            MapRoot = new GameObject("SideDoorInteractionRange_Map");
            Grid = MapRoot.AddComponent<Grid>();
            GroundTilemap = CreateTilemap("Ground", "Ground");
            WallTilemap = CreateTilemap("Wall", "Wall");
            DoorTilemap = CreateTilemap("Door", "Door");
            MapService = MapRoot.AddComponent(ResolveProjectType("GridMapService"));

            Invoke<object>(
                MapService,
                "Initialize",
                new[] { typeof(Grid), typeof(Tilemap), typeof(Tilemap), typeof(Tilemap), typeof(LayerMask) },
                Grid,
                GroundTilemap,
                WallTilemap,
                DoorTilemap,
                (LayerMask)0);

            DoorRoot = new GameObject("SideDoorInteractionRange_Door");
            DoorRoot.AddComponent<SpriteRenderer>();
            BoxCollider2D blockerCollider = DoorRoot.AddComponent<BoxCollider2D>();
            blockerCollider.size = new Vector2(0.9f, 1.9f);
            blockerCollider.offset = Vector2.zero;

            Component variantOverride = DoorRoot.AddComponent(ResolveProjectType("MainEscapeDoorVisualVariantOverride"));
            ConfigureVariant(variantOverride, "SideDoor42");

            Door = DoorRoot.AddComponent(ResolveProjectType("MainEscapeSelfContainedDoor"));
            SetField(Door, "blockerCollider", blockerCollider);
            SetField(Door, "interactionDistance", 2.1f);
            SetField(Door, "mapService", MapService);

            PlayerRoot = new GameObject("SideDoorInteractionRange_Player");
            Player = PlayerRoot.AddComponent(ResolveProjectType("WasdPlayerController"));
        }

        public GameObject MapRoot { get; }

        public GameObject DoorRoot { get; }

        public GameObject PlayerRoot { get; }

        public Grid Grid { get; }

        public Tilemap GroundTilemap { get; }

        public Tilemap WallTilemap { get; }

        public Tilemap DoorTilemap { get; }

        public Component MapService { get; }

        public Component Door { get; }

        public Component Player { get; }

        public void ConfigureDoorCells(Vector3Int[] doorCells)
        {
            SetField(Door, "resolvedDoorCells", doorCells);
        }

        public Vector2 CellCenter(Vector3Int cell)
        {
            return GroundTilemap.GetCellCenterWorld(cell);
        }

        public BoxCollider2D CreateDoorTileBlocker(string name, Vector3Int cell)
        {
            GameObject blockerObject = new(name);
            blockerObject.transform.SetParent(MapRoot.transform, false);
            blockerObject.transform.position = CellCenter(cell);
            SetLayerIfDefined(blockerObject, "Door");

            BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
            blocker.size = Vector2.one;
            return blocker;
        }

        public void Dispose()
        {
            if (DoorRoot != null)
            {
                UnityObject.DestroyImmediate(DoorRoot);
            }

            if (PlayerRoot != null)
            {
                UnityObject.DestroyImmediate(PlayerRoot);
            }

            if (MapRoot != null)
            {
                UnityObject.DestroyImmediate(MapRoot);
            }
        }

        private Tilemap CreateTilemap(string name, string layerName)
        {
            GameObject tilemapObject = new(name);
            tilemapObject.transform.SetParent(MapRoot.transform, false);
            SetLayerIfDefined(tilemapObject, layerName);

            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
            return tilemap;
        }
    }

    private static void ConfigureVariant(Component variantOverride, string variantName)
    {
        Type enumType = ResolveProjectType("MainEscapeDoorVisualVariantKind");
        object enumValue = Enum.Parse(enumType, variantName);

        Invoke<object>(
            variantOverride,
            "Configure",
            new[] { enumType },
            enumValue);
    }

    private static T Invoke<T>(object target, string methodName, Type[] parameterTypes, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, MemberFlags, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} is missing.");

        object value = method.Invoke(target, arguments);
        return value is T typedValue ? typedValue : default;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, MemberFlags);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
    }

    private static void SetLayerIfDefined(GameObject target, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            target.layer = layer;
        }
    }

    private static Type ResolveProjectType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Expected project type '{typeName}' in Assembly-CSharp.");
        return type;
    }
}

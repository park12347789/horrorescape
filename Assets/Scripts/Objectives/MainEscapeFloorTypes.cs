using System;
using UnityEngine;

public enum EscapeFloorVariantId
{
    Base5F,
    Segmented4F,
    Choke3F,
    Security2F,
    Lobby1F
}

[Serializable]
public sealed class EscapeFloorDefinition
{
    public EscapeFloorDefinition(
        int floorNumber,
        EscapeFloorVariantId variantId,
        Color accentColor,
        string toolItemId,
        string toolDisplayName,
        Vector3Int playerSpawnCell,
        Vector3Int toolCell,
        Vector3Int transitionCell,
        Vector3Int stalkerSpawnCell,
        Vector3Int[] patrolRoute,
        string authoredFloorResourcePath = null)
    {
        FloorNumber = floorNumber;
        VariantId = variantId;
        AccentColor = accentColor;
        ToolItemId = string.IsNullOrWhiteSpace(toolItemId) ? $"escape.tool.{floorNumber}f" : toolItemId;
        ToolDisplayName = string.IsNullOrWhiteSpace(toolDisplayName) ? $"{floorNumber}F stair tool" : toolDisplayName;
        PlayerSpawnCell = playerSpawnCell;
        ToolCell = toolCell;
        TransitionCell = transitionCell;
        StalkerSpawnCell = stalkerSpawnCell;
        PatrolRoute = patrolRoute ?? Array.Empty<Vector3Int>();
        AuthoredFloorResourcePath = authoredFloorResourcePath;
    }

    public int FloorNumber { get; }
    public EscapeFloorVariantId VariantId { get; }
    public Color AccentColor { get; }
    public string ToolItemId { get; }
    public string ToolDisplayName { get; }
    public Vector3Int PlayerSpawnCell { get; }
    public Vector3Int ToolCell { get; }
    public Vector3Int TransitionCell { get; }
    public Vector3Int StalkerSpawnCell { get; }
    public Vector3Int[] PatrolRoute { get; }
    public string AuthoredFloorResourcePath { get; }

    public string GetResolvedAuthoredFloorResourcePath()
    {
        return string.IsNullOrWhiteSpace(AuthoredFloorResourcePath)
            ? $"Floors/MainEscape/{FloorNumber}F"
            : AuthoredFloorResourcePath;
    }
}

public readonly struct OfficeFloorBuildResult
{
    public OfficeFloorBuildResult(
        Transform floorRoot,
        GridMapService mapService,
        GeneratedFloorLayout layout,
        Vector2 worldCenter,
        Vector2 worldSize,
        Vector3Int[] patrolRoute,
        Vector3Int[] patrolSpawnCells,
        Vector3Int stalkerSpawnCell,
        MainEscapeSentrySpawnPoint[] sentrySpawns,
        MainEscapeVentRouteDefinition ventRoute,
        bool isAuthored,
        bool hasPlayerSpawnWorldPosition = false,
        Vector3 playerSpawnWorldPosition = default)
    {
        FloorRoot = floorRoot;
        MapService = mapService;
        Layout = layout;
        WorldCenter = worldCenter;
        WorldSize = worldSize;
        PatrolRoute = patrolRoute ?? Array.Empty<Vector3Int>();
        PatrolSpawnCells = patrolSpawnCells ?? Array.Empty<Vector3Int>();
        StalkerSpawnCell = stalkerSpawnCell;
        SentrySpawns = sentrySpawns ?? Array.Empty<MainEscapeSentrySpawnPoint>();
        VentRoute = ventRoute.IsValid ? ventRoute : MainEscapeVentRouteDefinition.Empty;
        IsAuthored = isAuthored;
        HasPlayerSpawnWorldPosition = hasPlayerSpawnWorldPosition;
        PlayerSpawnWorldPosition = playerSpawnWorldPosition;
    }

    public Transform FloorRoot { get; }
    public GridMapService MapService { get; }
    public GeneratedFloorLayout Layout { get; }
    public Vector2 WorldCenter { get; }
    public Vector2 WorldSize { get; }
    public Vector3Int[] PatrolRoute { get; }
    public Vector3Int[] PatrolSpawnCells { get; }
    public Vector3Int StalkerSpawnCell { get; }
    public MainEscapeSentrySpawnPoint[] SentrySpawns { get; }
    public MainEscapeVentRouteDefinition VentRoute { get; }
    public bool IsAuthored { get; }
    public bool HasPlayerSpawnWorldPosition { get; }
    public Vector3 PlayerSpawnWorldPosition { get; }
    public bool IsValid => FloorRoot != null && MapService != null && Layout != null;
}

public static class MainEscapeFloorCatalog
{
    private static readonly EscapeFloorDefinition[] Floors =
    {
        new(
            5,
            EscapeFloorVariantId.Base5F,
            new Color(0.82f, 0.26f, 0.24f, 1f),
            "escape.tool.5f",
            "5F stair torch",
            new Vector3Int(-34, 8, 0),
            new Vector3Int(7, 10, 0),
            new Vector3Int(32, -9, 0),
            new Vector3Int(29, 8, 0),
            new[]
            {
                new Vector3Int(-19, 0, 0),
                new Vector3Int(-4, 0, 0),
                new Vector3Int(11, 0, 0),
                new Vector3Int(30, -9, 0)
            }),
        new(
            4,
            EscapeFloorVariantId.Segmented4F,
            new Color(0.78f, 0.44f, 0.18f, 1f),
            "escape.tool.4f",
            "4F bolt cutter",
            new Vector3Int(-33, -11, 0),
            new Vector3Int(32, 9, 0),
            new Vector3Int(31, -9, 0),
            new Vector3Int(28, 8, 0),
            new[]
            {
                new Vector3Int(-30, -10, 0),
                new Vector3Int(-15, 0, 0),
                new Vector3Int(0, 0, 0),
                new Vector3Int(29, 0, 0)
            }),
        new(
            3,
            EscapeFloorVariantId.Choke3F,
            new Color(0.7f, 0.63f, 0.18f, 1f),
            "escape.tool.3f",
            "3F hydraulic crank",
            new Vector3Int(-18, 8, 0),
            new Vector3Int(15, -12, 0),
            new Vector3Int(32, -9, 0),
            new Vector3Int(30, 8, 0),
            new[]
            {
                new Vector3Int(-18, 8, 0),
                new Vector3Int(-2, 0, 0),
                new Vector3Int(12, 0, 0),
                new Vector3Int(30, 8, 0)
            }),
        new(
            2,
            EscapeFloorVariantId.Security2F,
            new Color(0.24f, 0.65f, 0.42f, 1f),
            "escape.tool.2f",
            "2F bypass clamp",
            new Vector3Int(-18, -12, 0),
            new Vector3Int(6, 8, 0),
            new Vector3Int(32, -9, 0),
            new Vector3Int(29, 9, 0),
            new[]
            {
                new Vector3Int(-18, -12, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(13, -8, 0),
                new Vector3Int(30, 0, 0)
            }),
        new(
            1,
            EscapeFloorVariantId.Lobby1F,
            new Color(0.24f, 0.62f, 0.84f, 1f),
            "escape.tool.1f",
            "1F shutter fuse",
            new Vector3Int(-34, 8, 0),
            new Vector3Int(24, 8, 0),
            new Vector3Int(33, -10, 0),
            new Vector3Int(28, 8, 0),
            new[]
            {
                new Vector3Int(-20, 0, 0),
                new Vector3Int(0, 0, 0),
                new Vector3Int(15, 0, 0),
                new Vector3Int(32, -10, 0)
            })
    };

    public static EscapeFloorDefinition[] AllFloors => Floors;

    public static EscapeFloorDefinition GetFloorByIndex(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, Floors.Length - 1);
        return Floors[safeIndex];
    }

    public static bool TryGetFloorByNumber(int floorNumber, out EscapeFloorDefinition floor)
    {
        for (int index = 0; index < Floors.Length; index++)
        {
            if (Floors[index].FloorNumber == floorNumber)
            {
                floor = Floors[index];
                return true;
            }
        }

        floor = null;
        return false;
    }
}

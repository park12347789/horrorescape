/*
 * File Role:
 * Defines shared layer names, indices, and masks used by the runtime systems.
 *
 * Runtime Use:
 * Centralizes wall, door, prop, and ground layer knowledge so different scripts stay consistent.
 *
 * Study Notes:
 * Small file, but it prevents layer logic from being duplicated across the project.
 */

using UnityEngine;

public static class GameLayers
{
    public const string Ground = "Ground";
    public const string Wall = "Wall";
    public const string Door = "Door";
    public const string Prop = "Prop";

    public static int GroundIndex => ResolveLayer(Ground);
    public static int WallIndex => ResolveLayer(Wall);
    public static int DoorIndex => ResolveLayer(Door);
    public static int PropIndex => ResolveLayer(Prop);

    public static LayerMask VisionBlockingMask => LayerMask.GetMask(Wall, Door);

    private static int ResolveLayer(string layerName)
    {
        int layerIndex = LayerMask.NameToLayer(layerName);
        return layerIndex >= 0 ? layerIndex : 0;
    }
}


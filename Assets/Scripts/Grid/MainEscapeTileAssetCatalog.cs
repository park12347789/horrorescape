using UnityEngine;
using UnityEngine.Tilemaps;

public static class MainEscapeTileAssetCatalog
{
    public const string ResourceFolder = "Tiles/MainEscape";
    public const string AssetFolder = "Assets/Resources/Tiles/MainEscape";
    public const string TextureAssetFolder = AssetFolder + "/Textures";
    public const string PaletteAssetFolder = "Assets/TilePalettes";

    public const string GroundResourcePath = "Tiles/MainEscape/MainEscapeGroundTile";
    public const string WallResourcePath = "Tiles/MainEscape/MainEscapeWallTile";
    public const string DoorResourcePath = "Tiles/MainEscape/MainEscapeDoorTile";

    public const string GroundTileAssetPath = AssetFolder + "/MainEscapeGroundTile.asset";
    public const string WallTileAssetPath = AssetFolder + "/MainEscapeWallTile.asset";
    public const string DoorTileAssetPath = AssetFolder + "/MainEscapeDoorTile.asset";

    public const string GroundTextureAssetPath = TextureAssetFolder + "/MainEscapeGroundTile.png";
    public const string WallTextureAssetPath = TextureAssetFolder + "/MainEscapeWallTile.png";
    public const string DoorTextureAssetPath = TextureAssetFolder + "/MainEscapeDoorTile.png";

    public const string PaletteAssetPath = PaletteAssetFolder + "/MainEscapeEditingPalette.prefab";

    public static TileBase LoadGroundTile()
    {
        return Resources.Load<TileBase>(GroundResourcePath);
    }

    public static TileBase LoadWallTile()
    {
        return Resources.Load<TileBase>(WallResourcePath);
    }

    public static TileBase LoadDoorTile()
    {
        return Resources.Load<TileBase>(DoorResourcePath);
    }
}

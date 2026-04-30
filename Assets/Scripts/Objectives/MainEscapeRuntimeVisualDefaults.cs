using UnityEngine;

public static class MainEscapeRuntimeVisualDefaults
{
    public static void EnsurePickupSprite(SpriteRenderer spriteRenderer)
    {
        // Clean loop rule: missing authored pickup art should stay visible as a setup error,
        // not be hidden by a placeholder asset.
    }

    public static void EnsureSpriteMaterial(SpriteRenderer spriteRenderer)
    {
        // Clean loop rule: missing or incorrect sprite materials should surface as authored setup issues,
        // not be replaced at runtime.
    }
}

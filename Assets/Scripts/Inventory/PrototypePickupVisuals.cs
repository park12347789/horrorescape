using System;
using System.Collections.Generic;
using UnityEngine;

public static class PrototypePickupVisuals
{
    private enum PickupGlyphKind
    {
        Bottle,
        Medkit,
        Key,
        Battery,
        Torch,
        Cutter,
        Crank,
        Clamp,
        Fuse,
        EmergencyStairs,
        ExitDoor,
        Tool,
        Generic
    }

    private const int TextureSize = 48;
    private static readonly Dictionary<PickupGlyphKind, Sprite> GlyphSprites = new();

    public static void Apply(SpriteRenderer spriteRenderer, string itemId, string displayName, Color tint)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        PickupGlyphKind glyphKind = ResolveGlyph(itemId, displayName);
        spriteRenderer.sprite = GetGlyphSprite(glyphKind);
        spriteRenderer.color = tint;
    }

    public static void ApplyTransition(SpriteRenderer spriteRenderer, FloorEscapeTransitionKind kind, Color tint)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        PickupGlyphKind glyphKind = kind == FloorEscapeTransitionKind.EmergencyStairs
            ? PickupGlyphKind.EmergencyStairs
            : PickupGlyphKind.ExitDoor;
        spriteRenderer.sprite = GetGlyphSprite(glyphKind);
        spriteRenderer.color = tint;
    }

    public static bool TryGetItemGlyphSprite(string itemId, string displayName, out Sprite sprite)
    {
        sprite = GetGlyphSprite(ResolveGlyph(itemId, displayName));
        return sprite != null;
    }

    private static PickupGlyphKind ResolveGlyph(string itemId, string displayName)
    {
        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightItemId, StringComparison.Ordinal))
        {
            return PickupGlyphKind.Torch;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal))
        {
            return PickupGlyphKind.Bottle;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal))
        {
            return PickupGlyphKind.Medkit;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal))
        {
            return PickupGlyphKind.Battery;
        }

        string normalized = $"{itemId} {displayName}".ToLowerInvariant();

        if (normalized.Contains("key"))
        {
            return PickupGlyphKind.Key;
        }

        if (normalized.Contains("torch"))
        {
            return PickupGlyphKind.Torch;
        }

        if (normalized.Contains("cutter"))
        {
            return PickupGlyphKind.Cutter;
        }

        if (normalized.Contains("crank"))
        {
            return PickupGlyphKind.Crank;
        }

        if (normalized.Contains("clamp"))
        {
            return PickupGlyphKind.Clamp;
        }

        if (normalized.Contains("fuse"))
        {
            return PickupGlyphKind.Fuse;
        }

        if (normalized.Contains("battery") || normalized.Contains("cell"))
        {
            return PickupGlyphKind.Battery;
        }

        if (normalized.Contains("med") || normalized.Contains("heal"))
        {
            return PickupGlyphKind.Medkit;
        }

        if (normalized.Contains("bottle"))
        {
            return PickupGlyphKind.Bottle;
        }

        if (normalized.Contains("tool")
            || normalized.Contains("torch")
            || normalized.Contains("cutter")
            || normalized.Contains("clamp")
            || normalized.Contains("crank"))
        {
            return PickupGlyphKind.Tool;
        }

        return PickupGlyphKind.Generic;
    }

    private static Sprite GetGlyphSprite(PickupGlyphKind glyphKind)
    {
        if (GlyphSprites.TryGetValue(glyphKind, out Sprite sprite) && sprite != null)
        {
            return sprite;
        }

        sprite = CreateGlyphSprite(glyphKind);
        GlyphSprites[glyphKind] = sprite;
        return sprite;
    }

    private static Sprite CreateGlyphSprite(PickupGlyphKind glyphKind)
    {
        Color[] pixels = new Color[TextureSize * TextureSize];

        switch (glyphKind)
        {
            case PickupGlyphKind.Bottle:
                DrawBottle(pixels, TextureSize);
                break;
            case PickupGlyphKind.Medkit:
                DrawMedkit(pixels, TextureSize);
                break;
            case PickupGlyphKind.Key:
                DrawKey(pixels, TextureSize);
                break;
            case PickupGlyphKind.Battery:
                DrawBattery(pixels, TextureSize);
                break;
            case PickupGlyphKind.Torch:
                DrawTorch(pixels, TextureSize);
                break;
            case PickupGlyphKind.Cutter:
                DrawCutter(pixels, TextureSize);
                break;
            case PickupGlyphKind.Crank:
                DrawCrank(pixels, TextureSize);
                break;
            case PickupGlyphKind.Clamp:
                DrawClamp(pixels, TextureSize);
                break;
            case PickupGlyphKind.Fuse:
                DrawFuse(pixels, TextureSize);
                break;
            case PickupGlyphKind.EmergencyStairs:
                DrawEmergencyStairs(pixels, TextureSize);
                break;
            case PickupGlyphKind.ExitDoor:
                DrawExitDoor(pixels, TextureSize);
                break;
            case PickupGlyphKind.Tool:
                DrawTool(pixels, TextureSize);
                break;
            default:
                DrawDiamond(pixels, TextureSize);
                break;
        }

        Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        sprite.name = $"PickupGlyph_{glyphKind}";
        return sprite;
    }

    private static void DrawBottle(Color[] pixels, int size)
    {
        FillRect(pixels, size, 20, 13, 8, 14);
        FillRect(pixels, size, 18, 16, 12, 8);
        FillRect(pixels, size, 22, 27, 4, 8);
        FillRect(pixels, size, 21, 35, 6, 3);
    }

    private static void DrawMedkit(Color[] pixels, int size)
    {
        FillRect(pixels, size, 15, 16, 18, 14);
        FillRect(pixels, size, 21, 12, 6, 22);
        FillRect(pixels, size, 16, 20, 16, 6);
    }

    private static void DrawKey(Color[] pixels, int size)
    {
        DrawRing(pixels, size, 18f, 26f, 6.5f, 3.5f);
        FillRect(pixels, size, 22, 24, 12, 4);
        FillRect(pixels, size, 30, 20, 3, 8);
        FillRect(pixels, size, 33, 20, 2, 3);
        FillRect(pixels, size, 33, 25, 2, 3);
    }

    private static void DrawBattery(Color[] pixels, int size)
    {
        FillRect(pixels, size, 18, 14, 12, 18);
        FillRect(pixels, size, 21, 32, 6, 3);
        FillRect(pixels, size, 21, 18, 6, 2);
        FillRect(pixels, size, 21, 22, 6, 2);
        FillRect(pixels, size, 21, 26, 6, 2);
    }

    private static void DrawTool(Color[] pixels, int size)
    {
        DrawLine(pixels, size, 16, 17, 31, 32, 4);
        DrawLine(pixels, size, 18, 15, 23, 20, 3);
        DrawLine(pixels, size, 15, 18, 20, 23, 3);
        DrawRing(pixels, size, 32f, 33f, 5.5f, 2.75f);
    }

    private static void DrawTorch(Color[] pixels, int size)
    {
        FillRect(pixels, size, 21, 10, 6, 18);
        FillRect(pixels, size, 19, 28, 10, 6);
        FillRect(pixels, size, 20, 34, 8, 3);
        DrawLine(pixels, size, 24, 37, 28, 44, 3);
        DrawLine(pixels, size, 24, 37, 20, 44, 3);
        DrawLine(pixels, size, 24, 38, 24, 45, 2);
    }

    private static void DrawCutter(Color[] pixels, int size)
    {
        DrawLine(pixels, size, 16, 32, 24, 24, 4);
        DrawLine(pixels, size, 32, 32, 24, 24, 4);
        DrawLine(pixels, size, 13, 35, 20, 42, 4);
        DrawLine(pixels, size, 35, 35, 28, 42, 4);
        DrawLine(pixels, size, 20, 20, 24, 24, 3);
        DrawLine(pixels, size, 24, 24, 28, 20, 3);
    }

    private static void DrawCrank(Color[] pixels, int size)
    {
        DrawLine(pixels, size, 15, 30, 29, 30, 4);
        DrawLine(pixels, size, 29, 30, 29, 18, 4);
        DrawLine(pixels, size, 29, 18, 35, 18, 4);
        DrawLine(pixels, size, 18, 30, 18, 37, 3);
        FillCircle(pixels, size, 18f, 38f, 4f);
        FillCircle(pixels, size, 37f, 18f, 4f);
    }

    private static void DrawClamp(Color[] pixels, int size)
    {
        FillRect(pixels, size, 14, 15, 6, 18);
        FillRect(pixels, size, 14, 15, 16, 5);
        FillRect(pixels, size, 14, 28, 16, 5);
        DrawLine(pixels, size, 30, 18, 34, 14, 3);
        DrawLine(pixels, size, 30, 30, 34, 34, 3);
        DrawLine(pixels, size, 30, 24, 39, 24, 3);
        FillCircle(pixels, size, 40f, 24f, 3f);
    }

    private static void DrawFuse(Color[] pixels, int size)
    {
        FillRect(pixels, size, 12, 22, 24, 6);
        FillRect(pixels, size, 16, 18, 16, 14);
        FillRect(pixels, size, 8, 20, 4, 10);
        FillRect(pixels, size, 36, 20, 4, 10);
        DrawLine(pixels, size, 16, 20, 32, 30, 2);
        DrawLine(pixels, size, 16, 30, 32, 20, 2);
    }

    private static void DrawEmergencyStairs(Color[] pixels, int size)
    {
        FillRect(pixels, size, 11, 12, 6, 24);
        FillRect(pixels, size, 17, 30, 9, 6);
        FillRect(pixels, size, 26, 24, 9, 6);
        FillRect(pixels, size, 35, 18, 4, 6);
        DrawLine(pixels, size, 21, 14, 34, 27, 3);
        DrawLine(pixels, size, 21, 14, 17, 18, 3);
    }

    private static void DrawExitDoor(Color[] pixels, int size)
    {
        FillRect(pixels, size, 12, 12, 20, 24);
        FillRect(pixels, size, 16, 16, 12, 16);
        DrawLine(pixels, size, 28, 24, 38, 24, 3);
        DrawLine(pixels, size, 33, 19, 38, 24, 3);
        DrawLine(pixels, size, 33, 29, 38, 24, 3);
        FillCircle(pixels, size, 24f, 24f, 1.75f);
    }

    private static void DrawDiamond(Color[] pixels, int size)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Abs(x - (size / 2));
                int dy = Mathf.Abs(y - (size / 2));

                if (dx + dy <= 9)
                {
                    SetPixel(pixels, size, x, y);
                }
            }
        }
    }

    private static void DrawLine(Color[] pixels, int size, int x0, int y0, int x1, int y1, int thickness)
    {
        Vector2 start = new(x0, y0);
        Vector2 end = new(x1, y1);
        float segmentLengthSquared = (end - start).sqrMagnitude;
        float radius = Mathf.Max(0.5f, thickness * 0.5f);
        float radiusSquared = radius * radius;

        int minX = Mathf.Max(0, Mathf.Min(x0, x1) - thickness);
        int maxX = Mathf.Min(size - 1, Mathf.Max(x0, x1) + thickness);
        int minY = Mathf.Max(0, Mathf.Min(y0, y1) - thickness);
        int maxY = Mathf.Min(size - 1, Mathf.Max(y0, y1) + thickness);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new(x + 0.5f, y + 0.5f);
                float t = segmentLengthSquared <= 0.0001f
                    ? 0f
                    : Mathf.Clamp01(Vector2.Dot(point - start, end - start) / segmentLengthSquared);
                Vector2 nearest = Vector2.Lerp(start, end, t);

                if ((point - nearest).sqrMagnitude <= radiusSquared)
                {
                    SetPixel(pixels, size, x, y);
                }
            }
        }
    }

    private static void DrawRing(Color[] pixels, int size, float centerX, float centerY, float radius, float thickness)
    {
        float innerRadius = Mathf.Max(0f, radius - thickness);
        float outerRadius = radius + 1f;
        float innerSquared = innerRadius * innerRadius;
        float outerSquared = outerRadius * outerRadius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - centerX;
                float dy = y + 0.5f - centerY;
                float distanceSquared = (dx * dx) + (dy * dy);

                if (distanceSquared >= innerSquared && distanceSquared <= outerSquared)
                {
                    SetPixel(pixels, size, x, y);
                }
            }
        }
    }

    private static void FillRect(Color[] pixels, int size, int startX, int startY, int width, int height)
    {
        int endX = Mathf.Min(size, startX + width);
        int endY = Mathf.Min(size, startY + height);

        for (int y = Mathf.Max(0, startY); y < endY; y++)
        {
            for (int x = Mathf.Max(0, startX); x < endX; x++)
            {
                SetPixel(pixels, size, x, y);
            }
        }
    }

    private static void FillCircle(Color[] pixels, int size, float centerX, float centerY, float radius)
    {
        float radiusSquared = radius * radius;
        int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radius));
        int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(centerX + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radius));
        int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(centerY + radius));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - centerX;
                float dy = y + 0.5f - centerY;

                if ((dx * dx) + (dy * dy) <= radiusSquared)
                {
                    SetPixel(pixels, size, x, y);
                }
            }
        }
    }

    private static void SetPixel(Color[] pixels, int size, int x, int y)
    {
        if (x < 0 || x >= size || y < 0 || y >= size)
        {
            return;
        }

        pixels[(y * size) + x] = Color.white;
    }
}

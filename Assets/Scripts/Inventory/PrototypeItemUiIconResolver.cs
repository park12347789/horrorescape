using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct PrototypeItemUiIcon
{
    public PrototypeItemUiIcon(Sprite sprite, Color tint)
    {
        Sprite = sprite;
        Tint = tint;
    }

    public Sprite Sprite { get; }
    public Color Tint { get; }
    public bool IsValid => Sprite != null;
}

public static class PrototypeItemUiIconResolver
{
    private static readonly Dictionary<string, PrototypeItemUiIcon> CachedIcons = new(StringComparer.Ordinal);

    public static void ClearCache()
    {
        CachedIcons.Clear();
    }

    public static void Invalidate(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        CachedIcons.Remove(itemId);
    }

    public static bool TryResolve(string itemId, string displayName, out PrototypeItemUiIcon icon)
    {
        return TryResolve(default, itemId, displayName, out icon);
    }

    public static bool TryResolve(Scene scene, string itemId, string displayName, out PrototypeItemUiIcon icon)
    {
        if (!string.IsNullOrWhiteSpace(itemId)
            && CachedIcons.TryGetValue(BuildCacheKey(scene, itemId), out icon)
            && icon.IsValid)
        {
            return true;
        }

        if (TryResolveFromRuntimeCatalog(scene, itemId, displayName, out icon))
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                CachedIcons[BuildCacheKey(scene, itemId)] = icon;
            }

            return true;
        }

        icon = default;
        return false;
    }

    private static bool TryResolveFromRuntimeCatalog(Scene scene, string itemId, string displayName, out PrototypeItemUiIcon icon)
    {
        MainEscapeRuntimePrefabCatalog catalog = scene.IsValid()
            ? MainEscapeRuntimePrefabCatalog.LoadForScene(scene)
            : MainEscapeRuntimePrefabCatalog.LoadDefault();

        if (catalog != null)
        {
            SpriteRenderer spriteRenderer = ResolveCatalogRenderer(catalog, itemId, displayName);

            if (TryCreateIcon(spriteRenderer, out icon))
            {
                return true;
            }
        }

        if (PrototypePickupVisuals.TryGetItemGlyphSprite(itemId, displayName, out Sprite glyphSprite))
        {
            icon = new PrototypeItemUiIcon(glyphSprite, Color.white);
            return true;
        }

        icon = default;
        return false;
    }

    private static string BuildCacheKey(Scene scene, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        if (!scene.IsValid())
        {
            return itemId;
        }

        string sceneKey = MainEscapeSceneIdentityUtility.GetScenePathOrName(scene);
        return string.IsNullOrWhiteSpace(sceneKey)
            ? itemId
            : $"{sceneKey}::{itemId}";
    }

    private static SpriteRenderer ResolveCatalogRenderer(MainEscapeRuntimePrefabCatalog catalog, string itemId, string displayName)
    {
        if (catalog == null)
        {
            return null;
        }

        if (string.Equals(itemId, PrototypeItemCatalog.FlashlightBatteryItemId, StringComparison.Ordinal))
        {
            return ResolveRenderer(catalog.FlashlightBatteryPickupPrefab);
        }

        if (string.Equals(itemId, PrototypeItemCatalog.GlassBottleItemId, StringComparison.Ordinal))
        {
            return ResolveRenderer(catalog.GlassBottlePickupPrefab);
        }

        if (string.Equals(itemId, PrototypeItemCatalog.MedkitItemId, StringComparison.Ordinal))
        {
            return ResolveRenderer(catalog.MedkitPickupPrefab);
        }

        if (LooksLikeKey(itemId, displayName))
        {
            return ResolveRenderer(catalog.IronGateKeyVisualPrefab);
        }

        if (LooksLikeFloorTool(itemId, displayName))
        {
            return ResolveRenderer(catalog.FloorToolPickupPrefab);
        }

        return null;
    }

    private static SpriteRenderer ResolveRenderer(Component component)
    {
        return component != null ? component.GetComponentInChildren<SpriteRenderer>(true) : null;
    }

    private static SpriteRenderer ResolveRenderer(GameObject gameObject)
    {
        return gameObject != null ? gameObject.GetComponentInChildren<SpriteRenderer>(true) : null;
    }

    private static bool TryCreateIcon(SpriteRenderer spriteRenderer, out PrototypeItemUiIcon icon)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            icon = default;
            return false;
        }

        icon = new PrototypeItemUiIcon(spriteRenderer.sprite, spriteRenderer.color);
        return true;
    }

    private static bool LooksLikeKey(string itemId, string displayName)
    {
        return string.Equals(itemId, PrototypeItemCatalog.IronGateKeyItemId, StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(displayName)
                && displayName.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool LooksLikeFloorTool(string itemId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(itemId) && string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        if (PrototypeItemCatalog.TryGetDefinition(itemId, out _))
        {
            return false;
        }

        string normalized = $"{itemId} {displayName}";
        return normalized.IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("torch", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("cutter", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("crank", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("clamp", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("fuse", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

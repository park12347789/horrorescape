#if UNITY_EDITOR
using System.IO;

using UnityEditor;
#endif
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class RTutorialInventoryPickup : PlayerInteractable2D
{
    private const int SortingOrder = 150;
    private const float PickupReachPadding = 0.14f;
#if UNITY_EDITOR
    private const string EditorFallbackSpritePath = "Assets/Art/GameplaySprites/RTutorialPickupFallback.png";
#endif
    private static Sprite sharedFallbackSprite;

    [SerializeField] private string itemId = PrototypeItemCatalog.GlassBottleItemId;
    [SerializeField] private string inventoryDisplayName = "Glass Bottle";
    [SerializeField, Min(1)] private int quantity = 1;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color fallbackColor = Color.white;
    [SerializeField] private bool showPromptText;

    public void Configure(string configuredItemId, string displayName, int configuredQuantity, Color configuredFallbackColor)
    {
        itemId = string.IsNullOrWhiteSpace(configuredItemId)
            ? PrototypeItemCatalog.GlassBottleItemId
            : configuredItemId;
        inventoryDisplayName = PrototypeItemCatalog.GetDisplayName(itemId, displayName);
        quantity = Mathf.Max(1, configuredQuantity);
        fallbackColor = configuredFallbackColor;
        RefreshVisual();
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        return showPromptText ? $"E {inventoryDisplayName}" : string.Empty;
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        Vector2 playerPosition = playerController.transform.position;
        Vector2 surfacePoint = ResolveInteractionSurfacePoint(playerPosition);
        return Mathf.Max(0f, Vector2.Distance(playerPosition, surfacePoint) - PickupReachPadding);
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        return playerController != null
            ? ResolveInteractionSurfacePoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override void Interact(WasdPlayerController playerController)
    {
        PlayerInventory inventory = playerController != null ? playerController.GetComponent<PlayerInventory>() : null;

        if (inventory == null || !inventory.AddItem(itemId, inventoryDisplayName, quantity))
        {
            PrototypeAudioManager.TryPlayDenied();
            return;
        }

        PrototypeAudioManager.TryPlayPickup();
        gameObject.SetActive(false);
    }

    private void Reset()
    {
        CacheReferences();
        RefreshVisual();
    }

    private void Awake()
    {
        CacheReferences();
        RefreshVisual();
    }

    private void OnValidate()
    {
        quantity = Mathf.Max(1, quantity);
        CacheReferences();
        RefreshVisual();
    }

    private void CacheReferences()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        EnsureCollider();
    }

    private void RefreshVisual()
    {
        CacheReferences();

        if (spriteRenderer == null)
        {
            return;
        }

        if (PrototypeItemUiIconResolver.TryResolve(gameObject.scene, itemId, inventoryDisplayName, out PrototypeItemUiIcon icon))
        {
            spriteRenderer.sprite = icon.Sprite;
            spriteRenderer.color = icon.Tint;
        }
        else
        {
            spriteRenderer.sprite = GetFallbackSprite();
            spriteRenderer.color = fallbackColor;
        }

        spriteRenderer.sortingOrder = SortingOrder;
    }

    private void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null)
        {
            return;
        }

        CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.42f;
    }

    private Vector2 ResolveInteractionSurfacePoint(Vector2 playerPosition)
    {
        CacheReferences();

        if (spriteRenderer != null && spriteRenderer.bounds.size.sqrMagnitude > 0.0001f)
        {
            return spriteRenderer.bounds.ClosestPoint(playerPosition);
        }

        Collider2D pickupCollider = GetComponent<Collider2D>();

        if (pickupCollider != null && pickupCollider.enabled)
        {
            return pickupCollider.ClosestPoint(playerPosition);
        }

        return InteractionPoint;
    }

    private static Sprite GetFallbackSprite()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Sprite editorSprite = LoadOrCreateEditorFallbackSprite();

            if (editorSprite != null)
            {
                return editorSprite;
            }
        }
#endif

        if (sharedFallbackSprite != null)
        {
            return sharedFallbackSprite;
        }

        Texture2D texture = new(8, 8, TextureFormat.RGBA32, false)
        {
            name = "RTutorialPickupFallback",
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[64];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        sharedFallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
        sharedFallbackSprite.name = "RTutorialPickupFallback";
        sharedFallbackSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedFallbackSprite;
    }

#if UNITY_EDITOR
    private static Sprite LoadOrCreateEditorFallbackSprite()
    {
        Sprite existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorFallbackSpritePath);

        if (existingSprite != null)
        {
            return existingSprite;
        }

        string directory = Path.GetDirectoryName(EditorFallbackSpritePath);

        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Texture2D texture = new(8, 8, TextureFormat.RGBA32, false)
        {
            name = "RTutorialPickupFallback"
        };
        Color[] pixels = new Color[64];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        File.WriteAllBytes(EditorFallbackSpritePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(EditorFallbackSpritePath, ImportAssetOptions.ForceSynchronousImport);

        if (AssetImporter.GetAtPath(EditorFallbackSpritePath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 8f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(EditorFallbackSpritePath);
    }
#endif
}
